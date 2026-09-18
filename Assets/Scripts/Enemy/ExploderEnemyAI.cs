using UnityEngine;

/// <summary>
/// 自爆怪AI - 靠近玩家后触发范围爆炸
/// 自爆前有预警动画+特效，自爆后自身销毁
/// 近距离硬直抑制（不受硬直影响）
/// 复用EnemyNavAgent寻路、CombatSystem受击逻辑
/// WebGL平台兼容
/// </summary>
public class ExploderEnemyAI : MonoBehaviour
{
    #region FSM状态枚举

    public enum AIState
    {
        Idle,
        Chase,
        Warning,
        Explode,
        Dead
    }

    #endregion

    #region Inspector可配置参数

    [Header("自爆怪设置")]
    [Tooltip("敌人类型ID")]
    [SerializeField] private string enemyTypeId = "ExploderEnemy";

    [Header("自爆设置")]
    [Tooltip("自爆伤害")]
    [SerializeField] private float explodeDamage = 40f;

    [Tooltip("自爆范围半径")]
    [SerializeField] private float explodeRadius = 5f;

    [Tooltip("自爆前预警时间")]
    [SerializeField] private float warningDuration = 1.5f;

    [Tooltip("触发自爆的距离")]
    [SerializeField] private float triggerDistance = 2f;

    [Header("感知设置")]
    [Tooltip("视野检测距离")]
    [SerializeField] private float detectionRange = 15f;

    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 6f;

    [Header("掉落设置")]
    [Tooltip("击杀掉落概率")]
    [Range(0f, 1f)]
    public float dropChance = 0.4f;

    [Header("组件引用")]
    [SerializeField] private CharacterController characterController;

    [Header("调试设置")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private AIState currentState = AIState.Idle;
    private Transform target;
    private float warningTimer;
    private bool isDead = false;
    private bool isInitialized = false;
    private CombatSystem combatSystem;
    private EnemyNavAgent navAgent;
    private static Transform cachedPlayerTransform;

    /// <summary>预警闪烁Renderer列表</summary>
    private Renderer[] warningRenderers;

    /// <summary>预警原始颜色</summary>
    private Color warningOriginalColor;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (!isInitialized || isDead) return;

        if (combatSystem != null && combatSystem.IsDead)
        {
            if (currentState != AIState.Dead)
            {
                currentState = AIState.Dead;
                isDead = true;
            }
            return;
        }

        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdle();
                break;
            case AIState.Chase:
                UpdateChase();
                break;
            case AIState.Warning:
                UpdateWarning();
                break;
            case AIState.Explode:
                UpdateExplode();
                break;
        }
    }

    private void OnDestroy()
    {
        if (combatSystem != null)
            combatSystem.OnHealthChanged -= OnHit;
    }

    #endregion

    #region 初始化

    private void Initialize()
    {
        if (isInitialized) return;

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        combatSystem = GetComponent<CombatSystem>();
        navAgent = GetComponent<EnemyNavAgent>();

        // 从EnemyConfig读取配置
        var cfg = EnemyConfig.Get(enemyTypeId);
        if (cfg.characterId == enemyTypeId)
        {
            detectionRange = cfg.detectionRange;
            chaseSpeed = cfg.chaseSpeed;
        }

        // 缓存玩家引用
        if (cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                cachedPlayerTransform = player.transform;
        }
        target = cachedPlayerTransform;

        // 缓存Renderer用于预警闪烁
        warningRenderers = GetComponentsInChildren<Renderer>();
        if (warningRenderers.Length > 0 && warningRenderers[0] != null)
        {
            warningOriginalColor = warningRenderers[0].material.color;
        }

        // 监听受击
        if (combatSystem != null)
            combatSystem.OnHealthChanged += OnHit;

        isInitialized = true;
        currentState = AIState.Idle;
        DebugLog("[ExploderEnemyAI] 自爆怪初始化完成");
    }

    #endregion

    #region AI状态逻辑

    private void UpdateIdle()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= detectionRange)
        {
            currentState = AIState.Chase;
        }
    }

    private void UpdateChase()
    {
        if (target == null) { currentState = AIState.Idle; return; }

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > detectionRange * 1.5f)
        {
            currentState = AIState.Idle;
            if (navAgent != null) navAgent.StopMoving();
            return;
        }

        // 进入触发距离，开始预警
        if (dist <= triggerDistance)
        {
            currentState = AIState.Warning;
            warningTimer = warningDuration;
            if (navAgent != null) navAgent.StopMoving();
            EventBus.Publish("ON_ENEMY_EXPLODE_WARNING");
            DebugLog("[ExploderEnemyAI] 进入预警状态");
            return;
        }

        // 朝目标移动
        if (navAgent != null)
        {
            navAgent.SetDestination(target.position);
            navAgent.ResumeMoving();
        }
        else
        {
            Vector3 dir = (target.position - transform.position).normalized;
            characterController.Move(dir * chaseSpeed * Time.deltaTime);
        }

        LookAtTarget();
    }

    private void UpdateWarning()
    {
        // 预警闪烁效果
        warningTimer -= Time.deltaTime;
        float flash = Mathf.Sin(Time.time * 15f) * 0.5f + 0.5f;

        foreach (var rend in warningRenderers)
        {
            if (rend != null)
            {
                Color c = Color.Lerp(warningOriginalColor, Color.red, flash);
                rend.material.color = c;
            }
        }

        if (warningTimer <= 0f)
        {
            currentState = AIState.Explode;
        }
    }

    private void UpdateExplode()
    {
        DebugLog("[ExploderEnemyAI] 触发自爆！");

        // 发布自爆事件
        EventBus.Publish("ON_ENEMY_EXPLODE");

        // 范围伤害检测
        Collider[] hits = Physics.OverlapSphere(transform.position, explodeRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                CombatSystem playerCombat = hit.GetComponent<CombatSystem>();
                if (playerCombat != null)
                {
                    playerCombat.TakeDamage(gameObject, explodeDamage, transform.position);
                    DebugLog("[ExploderEnemyAI] 自爆命中玩家");
                }
            }
        }

        // 自爆后销毁（回收入池）
        isDead = true;
        if (ObjectPoolManager.HasInstance)
        {
            ObjectPoolManager.Instance.ReturnToPool("ExploderEnemy", gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnHit(int currentHP, int maxHP)
    {
        if (isDead) return;

        // 自爆怪硬直抑制：受击不打断当前状态
        DebugLog($"[ExploderEnemyAI] 受击 HP:{currentHP}/{maxHP}（硬直抑制）");
    }

    #endregion

    #region 辅助方法

    private void LookAtTarget()
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }
    }

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
