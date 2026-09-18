using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Boss AI控制器 - 多阶段战斗AI
/// 2阶段机制：P1常规攻击，P2狂暴强化
/// 复用EnemyNavAgent寻路、CombatSystem受击/硬直逻辑
/// 通过EventBus发布Boss专属事件
/// WebGL平台兼容
/// </summary>
public class BossAI : MonoBehaviour
{
    #region 事件常量

    public const string ON_BOSS_PHASE_CHANGE = "ON_BOSS_PHASE_CHANGE";
    public const string ON_BOSS_DEFEATED = "ON_BOSS_DEFEATED";

    #endregion

    #region Boss阶段枚举

    public enum BossPhase
    {
        P1,  // 血量>50%，常规攻击
        P2   // 血量≤50%，狂暴强化
    }

    public enum BossState
    {
        Idle,
        MeleeAttack,
        RangedAttack,
        AOEAttack,
        Reposition,
        PhaseTransition,
        Dead
    }

    #endregion

    #region Inspector可配置参数

    [Header("Boss设置")]
    [Tooltip("Boss名称")]
    [SerializeField] private string bossName = "暗影领主";

    [Tooltip("P2触发血量百分比")]
    [Range(0f, 1f)]
    [SerializeField] private float phase2Threshold = 0.5f;

    [Header("P1 近战设置")]
    [Tooltip("近战攻击伤害")]
    [SerializeField] private float meleeDamage = 15f;

    [Tooltip("近战攻击范围")]
    [SerializeField] private float meleeRange = 2.5f;

    [Tooltip("近战攻击冷却")]
    [SerializeField] private float meleeCooldown = 1.5f;

    [Header("P1 远程设置")]
    [Tooltip("远程攻击伤害")]
    [SerializeField] private float rangedDamage = 10f;

    [Tooltip("远程攻击范围")]
    [SerializeField] private float rangedRange = 8f;

    [Tooltip("远程攻击冷却")]
    [SerializeField] private float rangedCooldown = 3f;

    [Tooltip("弹幕数量")]
    [SerializeField] private int bulletCount = 5;

    [Header("P2 狂暴设置")]
    [Tooltip("P2攻击力倍率")]
    [SerializeField] private float p2DamageMultiplier = 1.5f;

    [Tooltip("P2移动速度倍率")]
    [SerializeField] private float p2SpeedMultiplier = 1.3f;

    [Tooltip("AOE冲击波伤害")]
    [SerializeField] private float aoeDamage = 20f;

    [Tooltip("AOE冲击波范围")]
    [SerializeField] private float aoeRange = 5f;

    [Tooltip("AOE攻击冷却")]
    [SerializeField] private float aoeCooldown = 6f;

    [Header("移动设置")]
    [Tooltip("移动速度")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("走位间隔（秒）")]
    [SerializeField] private float repositionInterval = 4f;

    [Header("组件引用")]
    [Tooltip("CombatSystem组件")]
    [SerializeField] private CombatSystem combatSystem;

    [Tooltip("CharacterController")]
    [SerializeField] private CharacterController characterController;

    [Header("调试设置")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private BossPhase currentPhase = BossPhase.P1;
    private BossState currentState = BossState.Idle;
    private Transform playerTransform;
    private float attackTimer = 0f;
    private float repositionTimer = 0f;
    private bool isInitialized = false;
    private bool isDead = false;
    private float currentMoveSpeed;
    private float currentDamageMultiplier;

    /// <summary>缓存的玩家Transform</summary>
    private static Transform cachedPlayerTransform;

    #endregion

    #region 公共属性

    public string BossName => bossName;
    public BossPhase CurrentPhase => currentPhase;
    public bool IsDead => isDead;
    public CombatSystem CombatSystem => combatSystem;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeBoss();
    }

    private void Update()
    {
        if (!isInitialized || isDead) return;

        UpdateTimers();
        UpdateAI();
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    #endregion

    #region 初始化

    private void InitializeBoss()
    {
        if (isInitialized) return;

        if (combatSystem == null)
            combatSystem = GetComponent<CombatSystem>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        // Phase12: 从BossConfig读取Boss参数
        var cfg = BossConfig.Instance;
        bossName = cfg.bossTitle;
        phase2Threshold = cfg.phase2Threshold;
        meleeDamage = cfg.meleeDamage;
        meleeRange = cfg.meleeRange;
        meleeCooldown = cfg.meleeCooldown;
        rangedDamage = cfg.rangedDamage;
        rangedRange = cfg.rangedRange;
        rangedCooldown = cfg.rangedCooldown;
        bulletCount = cfg.bulletCount;
        p2DamageMultiplier = cfg.p2DamageMultiplier;
        p2SpeedMultiplier = cfg.p2SpeedMultiplier;
        aoeDamage = cfg.aoeDamage;
        aoeRange = cfg.aoeRange;
        aoeCooldown = cfg.aoeCooldown;
        moveSpeed = cfg.moveSpeed;

        currentMoveSpeed = moveSpeed;
        currentDamageMultiplier = 1f;

        // 缓存玩家引用
        if (cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                cachedPlayerTransform = player.transform;
        }
        playerTransform = cachedPlayerTransform;

        // 订阅事件
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);

        // 监听受击
        if (combatSystem != null)
        {
            combatSystem.OnHealthChanged += OnBossHit;
        }

        isInitialized = true;
        currentState = BossState.Idle;
        DebugLog("[BossAI] Boss初始化完成（配置驱动）: " + bossName);
    }

    #endregion

    #region 计时器

    private void UpdateTimers()
    {
        attackTimer -= Time.deltaTime;
        repositionTimer -= Time.deltaTime;
    }

    #endregion

    #region AI逻辑

    private void UpdateAI()
    {
        if (playerTransform == null) return;

        // 检查阶段切换
        CheckPhaseTransition();

        switch (currentState)
        {
            case BossState.Idle:
                UpdateIdle();
                break;
            case BossState.MeleeAttack:
                UpdateMeleeAttack();
                break;
            case BossState.RangedAttack:
                UpdateRangedAttack();
                break;
            case BossState.AOEAttack:
                UpdateAOEAttack();
                break;
            case BossState.Reposition:
                UpdateReposition();
                break;
            case BossState.PhaseTransition:
                // 阶段切换中，不做其他操作
                break;
        }
    }

    private void UpdateIdle()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distToPlayer <= meleeRange && attackTimer <= 0f)
        {
            currentState = BossState.MeleeAttack;
        }
        else if (distToPlayer <= rangedRange && attackTimer <= 0f)
        {
            currentState = BossState.RangedAttack;
        }
        else if (repositionTimer <= 0f)
        {
            currentState = BossState.Reposition;
        }
        else
        {
            // 朝玩家移动
            MoveTowards(playerTransform.position);
        }
    }

    private void UpdateMeleeAttack()
    {
        if (attackTimer > 0f)
        {
            currentState = BossState.Idle;
            return;
        }

        DebugLog("[BossAI] 近战攻击");
        PerformMeleeAttack();
        attackTimer = meleeCooldown;
        repositionTimer = repositionInterval;
        currentState = BossState.Idle;
    }

    private void UpdateRangedAttack()
    {
        if (attackTimer > 0f)
        {
            currentState = BossState.Idle;
            return;
        }

        DebugLog("[BossAI] 远程弹幕攻击");
        PerformRangedAttack();
        attackTimer = rangedCooldown;
        repositionTimer = repositionInterval;
        currentState = BossState.Idle;
    }

    private void UpdateAOEAttack()
    {
        if (attackTimer > 0f)
        {
            currentState = BossState.Idle;
            return;
        }

        DebugLog("[BossAI] AOE冲击波");
        PerformAOEAttack();
        attackTimer = aoeCooldown;
        repositionTimer = repositionInterval;
        currentState = BossState.Idle;
    }

    private void UpdateReposition()
    {
        // P2中随机走位更频繁
        Vector3 offset = Random.insideUnitSphere * 3f;
        offset.y = 0;
        Vector3 targetPos = transform.position + offset;

        MoveTowards(targetPos);

        if (repositionTimer <= 0f)
        {
            currentState = BossState.Idle;
            repositionTimer = repositionInterval;
        }
    }

    #endregion

    #region 攻击执行

    private void PerformMeleeAttack()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, meleeRange);
        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == gameObject) continue;

            CombatSystem targetCombat = hit.GetComponent<CombatSystem>();
            if (targetCombat != null && targetCombat.IsPlayer)
            {
                float dmg = meleeDamage * currentDamageMultiplier;
                targetCombat.TakeDamage(gameObject, dmg, hit.transform.position);
                DebugLog("[BossAI] 近战命中: " + hit.name + " 伤害: " + dmg);
            }
        }
    }

    private void PerformRangedAttack()
    {
        if (playerTransform == null) return;

        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        float spreadAngle = 30f;
        float startAngle = -spreadAngle / 2f;
        float angleStep = spreadAngle / Mathf.Max(1, bulletCount - 1);

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * dirToPlayer;

            CreateBossProjectile(dir, rangedDamage * currentDamageMultiplier);
        }
    }

    private void PerformAOEAttack()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, aoeRange);
        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == gameObject) continue;

            CombatSystem targetCombat = hit.GetComponent<CombatSystem>();
            if (targetCombat != null && targetCombat.IsPlayer)
            {
                float dmg = aoeDamage * currentDamageMultiplier;
                targetCombat.TakeDamage(gameObject, dmg, hit.transform.position);
                DebugLog("[BossAI] AOE命中: " + hit.name + " 伤害: " + dmg);
            }
        }
    }

    private void CreateBossProjectile(Vector3 direction, float damage)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "BossProjectile";
        projectile.transform.position = transform.position + Vector3.up * 1.5f + direction * 0.5f;
        projectile.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color projColor = currentPhase == BossPhase.P1 ? new Color(0.8f, 0.2f, 0.8f) : new Color(1f, 0.3f, 0.1f);
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.SetColor("_Color", projColor);
        }

        Collider col = projectile.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        BossProjectile bp = projectile.AddComponent<BossProjectile>();
        bp.Initialize(direction, 12f, damage, gameObject);
    }

    #endregion

    #region 阶段切换

    private void CheckPhaseTransition()
    {
        if (currentPhase == BossPhase.P1 && combatSystem != null)
        {
            float healthPercent = (float)combatSystem.CurrentHealth / combatSystem.MaxHealth;
            if (healthPercent <= phase2Threshold)
            {
                TransitionToP2();
            }
        }
    }

    private void TransitionToP2()
    {
        currentPhase = BossPhase.P2;
        currentMoveSpeed = moveSpeed * p2SpeedMultiplier;
        currentDamageMultiplier = p2DamageMultiplier;

        Debug.Log("[BossAI] ===== 阶段切换: P2 狂暴 =====");

        // 发布阶段切换事件
        Dictionary<string, object> phaseData = new Dictionary<string, object>
        {
            { "bossName", bossName },
            { "phase", 2 }
        };
        EventBus.Publish(ON_BOSS_PHASE_CHANGE, phaseData);

        // 切换后立即释放一次AOE
        attackTimer = 1f;
        currentState = BossState.AOEAttack;
    }

    #endregion

    #region 移动

    private void MoveTowards(Vector3 targetPosition)
    {
        if (characterController == null) return;

        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        characterController.Move(direction * currentMoveSpeed * Time.deltaTime);

        // 朝向玩家
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
        }
    }

    #endregion

    #region 事件处理

    private void OnBossHit()
    {
        // 受击时切换到Idle重新决策
        if (currentState != BossState.PhaseTransition)
        {
            currentState = BossState.Idle;
        }
    }

    private void OnPlayerDie()
    {
        // 玩家死亡，Boss停止攻击
        isDead = true;
        currentState = BossState.Dead;
    }

    /// <summary>
    /// 由CombatSystem调用，Boss死亡时触发
    /// </summary>
    public void OnBossDeath()
    {
        if (isDead) return;

        isDead = true;
        currentState = BossState.Dead;

        Debug.Log("[BossAI] ===== Boss被击败 =====");

        // 发布Boss死亡事件
        Dictionary<string, object> deathData = new Dictionary<string, object>
        {
            { "bossName", bossName }
        };
        EventBus.Publish(ON_BOSS_DEFEATED, deathData);
    }

    #endregion

    #region 工具方法

    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
