using UnityEngine;

/// <summary>
/// 远程法师敌人AI - 保持距离释放魔法弹
/// 玩家近身时后撤拉开距离
/// 低血量时增加施法频率
/// 复用EnemyNavAgent寻路、CombatSystem受击逻辑
/// WebGL平台兼容
/// </summary>
public class MageEnemyAI : MonoBehaviour
{
    #region FSM状态枚举

    public enum AIState
    {
        Idle,
        Chase,
        Attack,
        Retreat,
        Dead
    }

    #endregion

    #region Inspector可配置参数

    [Header("法师设置")]
    [Tooltip("敌人类型ID")]
    [SerializeField] private string enemyTypeId = "MageEnemy";

    [Header("远程攻击设置")]
    [Tooltip("魔法弹伤害")]
    [SerializeField] private float magicDamage = 12f;

    [Tooltip("魔法弹速度")]
    [SerializeField] private float magicSpeed = 10f;

    [Tooltip("攻击冷却时间")]
    [SerializeField] private float attackCooldown = 3f;

    [Tooltip("攻击距离")]
    [SerializeField] private float attackRange = 10f;

    [Tooltip("最佳攻击距离")]
    [SerializeField] private float optimalDistance = 8f;

    [Header("后撤设置")]
    [Tooltip("后撤触发距离（玩家低于此距离时后撤）")]
    [SerializeField] private float retreatDistance = 4f;

    [Tooltip("后撤移动速度")]
    [SerializeField] private float retreatSpeed = 5f;

    [Tooltip("后撤持续时间")]
    [SerializeField] private float retreatDuration = 1.5f;

    [Header("感知设置")]
    [Tooltip("视野检测距离")]
    [SerializeField] private float detectionRange = 15f;

    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 3f;

    [Header("低血量设置")]
    [Tooltip("低血量阈值百分比")]
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Tooltip("低血量攻击冷却倍率（越小越快）")]
    [SerializeField] private float lowHealthCooldownMultiplier = 0.6f;

    [Header("掉落设置")]
    [Tooltip("击杀掉落概率")]
    [Range(0f, 1f)]
    public float dropChance = 0.35f;

    [Header("组件引用")]
    [SerializeField] private CharacterController characterController;

    [Header("调试设置")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private AIState currentState = AIState.Idle;
    private Transform target;
    private float attackTimer;
    private float retreatTimer;
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
            case AIState.Retreat:
                UpdateRetreat();
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
            attackRange = cfg.attackRange;
            attackCooldown = cfg.attackCooldown;
            magicDamage = cfg.attackDamage;
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

        // 监听受击
        if (combatSystem != null)
            combatSystem.OnHealthChanged += OnHit;

        isInitialized = true;
        currentState = AIState.Idle;
        DebugLog("[MageEnemyAI] 远程法师初始化完成");
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

        // 玩家太近，后撤
        if (dist < retreatDistance)
        {
            currentState = AIState.Retreat;
            retreatTimer = retreatDuration;
            return;
        }

        // 进入攻击范围
        if (dist <= attackRange)
        {
            currentState = AIState.Attack;
            if (navAgent != null) navAgent.StopMoving();
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

        // 玩家太近，后撤
        if (dist < retreatDistance)
        {
            currentState = AIState.Retreat;
            retreatTimer = retreatDuration;
            return;
        }

        // 超出攻击范围，追击
        if (dist > attackRange * 1.2f)
        {
            currentState = AIState.Chase;
            return;
        }

        // 施法
        if (attackTimer <= 0f)
        {
            CastMagic();
            // 低血量加快施法
            float cd = attackCooldown;
            if (combatSystem != null && combatSystem.HealthPercent <= lowHealthThreshold)
            {
                cd *= lowHealthCooldownMultiplier;
                DebugLog("[MageEnemyAI] 低血量，施法加速");
            }
            attackTimer = cd;
        }

        LookAtTarget();
    }

    private void UpdateRetreat()
    {
        if (target == null) { currentState = AIState.Idle; return; }

        retreatTimer -= Time.deltaTime;

        // 朝远离玩家的方向移动
        Vector3 dir = (transform.position - target.position).normalized;
        if (navAgent != null)
        {
            Vector3 retreatPos = transform.position + dir * 5f;
            navAgent.SetDestination(retreatPos);
            navAgent.ResumeMoving();
        }
        else
        {
            characterController.Move(dir * retreatSpeed * Time.deltaTime);
        }

        LookAtTarget();

        if (retreatTimer <= 0f)
        {
            float dist = Vector3.Distance(transform.position, target.position);
            currentState = dist <= attackRange ? AIState.Attack : AIState.Chase;
        }
    }

    private void CastMagic()
    {
        if (target == null) return;

        DebugLog("[MageEnemyAI] 释放魔法弹");
        EventBus.Publish("ON_MAGE_CAST");

        // 创建魔法弹
        GameObject magic = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        magic.name = "MagicProjectile";
        magic.transform.position = transform.position + Vector3.up * 1.2f;
        magic.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        // 设置颜色
        Renderer rend = magic.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.2f, 0.9f, 1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.5f, 0.2f, 0.9f) * 2f);
            rend.material = mat;
        }

        // 移除默认Collider
        Collider col = magic.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        // 添加投射物逻辑
        MagicProjectile mp = magic.AddComponent<MagicProjectile>();
        mp.Initialize(target, magicDamage, magicSpeed, 5f);
    }

    private void OnHit(int currentHP, int maxHP)
    {
        if (isDead) return;
        DebugLog($"[MageEnemyAI] 受击 HP:{currentHP}/{maxHP}");
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

/// <summary>
/// 魔法投射物 - 法师发射的远程弹幕
/// WebGL平台兼容
/// </summary>
public class MagicProjectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private float speed;
    private float lifetime;
    private float timer;

    public void Initialize(Transform target, float damage, float speed, float lifetime)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.lifetime = lifetime;
        this.timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
        {
            // 无目标时直线飞行
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
            return;
        }

        // 朝目标飞行
        Vector3 dir = (target.position + Vector3.up * 1f - transform.position).normalized;
        transform.Translate(dir * speed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1f);

        // 碰撞检测
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= 0.8f)
        {
            CombatSystem playerCombat = target.GetComponent<CombatSystem>();
            if (playerCombat != null)
            {
                playerCombat.TakeDamage(gameObject, damage, transform.position);
            }
            Destroy(gameObject);
        }
    }
}
