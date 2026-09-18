using UnityEngine;

/// <summary>
/// 精英近战敌人AI - 高血量高攻击力的精英怪
/// 拥有冲刺突袭技能，受击硬直抵抗更高
/// 死亡掉落概率更高
/// 复用EnemyNavAgent寻路、CombatSystem受击逻辑
/// WebGL平台兼容
/// </summary>
public class EliteEnemyAI : MonoBehaviour
{
    #region FSM状态枚举

    public enum AIState
    {
        Idle,
        Chase,
        Attack,
        Charge,
        Dead
    }

    #endregion

    #region Inspector可配置参数

    [Header("精英设置")]
    [Tooltip("精英敌人类型ID（用于EnemyConfig读取）")]
    [SerializeField] private string enemyTypeId = "EliteEnemy";

    [Header("冲刺设置")]
    [Tooltip("冲刺伤害")]
    [SerializeField] private float chargeDamage = 25f;

    [Tooltip("冲刺速度")]
    [SerializeField] private float chargeSpeed = 15f;

    [Tooltip("冲刺距离")]
    [SerializeField] private float chargeRange = 8f;

    [Tooltip("冲刺冷却时间")]
    [SerializeField] private float chargeCooldown = 5f;

    [Tooltip("冲刺前蓄力时间")]
    [SerializeField] private float chargeWindup = 0.5f;

    [Header("感知设置")]
    [Tooltip("视野检测距离")]
    [SerializeField] private float detectionRange = 12f;

    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 5f;

    [Header("攻击设置")]
    [Tooltip("攻击触发距离")]
    [SerializeField] private float attackRange = 2f;

    [Tooltip("攻击伤害")]
    [SerializeField] private float attackDamage = 18f;

    [Tooltip("攻击冷却")]
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("掉落设置")]
    [Tooltip("击杀掉落概率")]
    [Range(0f, 1f)]
    public float dropChance = 0.6f;

    [Header("组件引用")]
    [SerializeField] private CharacterController characterController;

    [Header("调试设置")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private AIState currentState = AIState.Idle;
    private Transform target;
    private Vector3 spawnPosition;
    private float attackTimer;
    private float chargeTimer;
    private bool isCharging = false;
    private bool isDead = false;
    private bool isInitialized = false;
    private CombatSystem combatSystem;
    private EnemyNavAgent navAgent;
    private static Transform cachedPlayerTransform;

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

        attackTimer -= Time.deltaTime;
        chargeTimer -= Time.deltaTime;

        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdle();
                break;
            case AIState.Chase:
                UpdateChase();
                break;
            case AIState.Attack:
                UpdateAttack();
                break;
            case AIState.Charge:
                UpdateCharge();
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

        spawnPosition = transform.position;

        // 从EnemyConfig读取配置
        var cfg = EnemyConfig.Get(enemyTypeId);
        if (cfg.characterId != enemyTypeId)
        {
            // 使用默认精英配置
            detectionRange = 12f;
            chaseSpeed = 5f;
            attackRange = 2f;
            attackDamage = 18f;
            attackCooldown = 1.2f;
        }
        else
        {
            detectionRange = cfg.detectionRange;
            chaseSpeed = cfg.chaseSpeed;
            attackRange = cfg.attackRange;
            attackDamage = cfg.attackDamage;
            attackCooldown = cfg.attackCooldown;
        }

        // 缓存玩家引用
        if (cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                cachedPlayerTransform = player.transform;
        }
        target = cachedPlayerTransform;

        attackTimer = attackCooldown;
        chargeTimer = chargeCooldown;

        // 监听受击
        if (combatSystem != null)
            combatSystem.OnHealthChanged += OnHit;

        isInitialized = true;
        currentState = AIState.Idle;
        DebugLog("[EliteEnemyAI] 精英敌人初始化完成");
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
            DebugLog("[EliteEnemyAI] 发现目标，开始追击");
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

        // 冲刺冷却完毕且距离适中时发动冲刺
        if (chargeTimer <= 0f && dist <= chargeRange && dist > attackRange)
        {
            currentState = AIState.Charge;
            return;
        }

        // 攻击范围内
        if (dist <= attackRange)
        {
            currentState = AIState.Attack;
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

    private void UpdateAttack()
    {
        if (target == null) { currentState = AIState.Idle; return; }

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > attackRange * 1.3f)
        {
            currentState = AIState.Chase;
            return;
        }

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }

        LookAtTarget();
    }

    private void UpdateCharge()
    {
        if (!isCharging)
        {
            // 蓄力阶段
            if (navAgent != null) navAgent.StopMoving();
            LookAtTarget();

            chargeTimer -= Time.deltaTime;
            if (chargeTimer <= -chargeWindup)
            {
                isCharging = true;
                chargeTimer = 2f; // 冲刺持续时间上限
                DebugLog("[EliteEnemyAI] 开始冲刺突袭");
            }
        }
        else
        {
            // 冲刺阶段
            Vector3 dir = (target.position - transform.position).normalized;
            characterController.Move(dir * chargeSpeed * Time.deltaTime);
            LookAtTarget();

            // 冲刺中检测碰撞
            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.position);
                if (dist <= 1.5f)
                {
                    // 造成冲刺伤害
                    CombatSystem targetCombat = target.GetComponent<CombatSystem>();
                    if (targetCombat != null)
                    {
                        targetCombat.TakeDamage(gameObject, chargeDamage, transform.position);
                        DebugLog("[EliteEnemyAI] 冲刺命中目标");
                    }
                    EndCharge();
                    return;
                }
            }

            chargeTimer -= Time.deltaTime;
            if (chargeTimer <= 0f)
            {
                EndCharge();
            }
        }
    }

    private void EndCharge()
    {
        isCharging = false;
        chargeTimer = chargeCooldown;
        currentState = AIState.Chase;
        DebugLog("[EliteEnemyAI] 冲刺结束，进入冷却");
    }

    private void PerformAttack()
    {
        if (target == null) return;

        CombatSystem targetCombat = target.GetComponent<CombatSystem>();
        if (targetCombat != null)
        {
            targetCombat.TakeDamage(gameObject, attackDamage, transform.position);
            EventBus.Publish("ON_ENEMY_HIT");
            DebugLog("[EliteEnemyAI] 近战攻击");
        }
    }

    private void OnHit(int currentHP, int maxHP)
    {
        if (isDead) return;
        DebugLog($"[EliteEnemyAI] 受击 HP:{currentHP}/{maxHP}");
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
