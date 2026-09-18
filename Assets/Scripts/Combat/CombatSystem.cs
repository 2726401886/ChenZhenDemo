using UnityEngine;

/// <summary>
/// 战斗系统组件 - 通用血量与受伤死亡状态管理
/// 挂载在角色（玩家/敌人）身上，作为通用战斗血量组件
/// 玩家和敌人共用同一套逻辑，通过角色标签区分类型
/// 只负责血量、受伤、死亡状态，不包含移动、动画逻辑
/// 通过EventBus发布受伤/死亡事件，供UI、音效、特效系统响应
/// WebGL平台兼容
/// </summary>
public class CombatSystem : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("血量设置")]
    [Tooltip("最大生命值")]
    [SerializeField] private int maxHealth = 100;

    [Tooltip("当前生命值（Inspector中可观察，运行时由代码控制）")]
    [SerializeField] private int currentHealth;

    [Header("状态设置")]
    [Tooltip("是否为无敌状态（受伤时不扣血）")]
    [SerializeField] private bool isInvincible = false;

    [Tooltip("是否为玩家角色（自动根据Tag判断，也可手动覆盖）")]
    [SerializeField] private bool isPlayer = false;

    [Tooltip("手动覆盖角色类型（勾选后忽略Tag自动判断）")]
    [SerializeField] private bool overrideRoleType = false;

    [Header("受击硬直设置")]
    [Tooltip("受击硬直时间（秒），硬直期间禁止移动和攻击")]
    [SerializeField] private float hitstunDuration = 0.3f;

    [Tooltip("硬直恢复时间比例（0-1），硬直动画播放到此比例时恢复控制")]
    [SerializeField] private float hitstunRecoveryRatio = 0.8f;

    [Header("无敌帧设置")]
    [Tooltip("受击后无敌帧时间（秒）")]
    [SerializeField] private float invincibilityDuration = 0.5f;

    [Tooltip("无敌帧闪烁效果")]
    [SerializeField] private bool enableFlashEffect = true;

    [Tooltip("无敌帧闪烁频率")]
    [SerializeField] private float flashFrequency = 10f;

    #endregion

    #region 私有状态变量

    /// <summary>是否已死亡</summary>
    private bool isDead = false;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>是否正在受击硬直中</summary>
    private bool isInHitstun = false;

    /// <summary>硬直计时器</summary>
    private float hitstunTimer = 0f;

    /// <summary>无敌帧计时器</summary>
    private float invincibilityTimer = 0f;

    /// <summary>是否正在无敌帧中</summary>
    private bool isInInvincibility = false;

    /// <summary>受击硬直总时间（用于计算恢复比例）</summary>
    private float totalHitstunDuration = 0f;

    /// <summary>Renderer组件（用于无敌帧闪烁效果）</summary>
    private Renderer cachedRenderer;

    /// <summary>原始材质颜色</summary>
    private Color originalMaterialColor;

    #endregion

    #region 公共属性（只读）

    /// <summary>Phase11: 血量变化委托 - 参数为(当前血量, 最大血量)，BossAI订阅用于检测受伤</summary>
    public System.Action<int, int> OnHealthChanged;

    /// <summary>最大生命值</summary>
    public int MaxHealth => maxHealth;

    /// <summary>当前生命值</summary>
    public int CurrentHealth => currentHealth;

    /// <summary>是否已死亡</summary>
    public bool IsDead => isDead;

    /// <summary>是否为无敌状态</summary>
    public bool IsInvincible => isInvincible;

    /// <summary>生命值百分比（0-1）</summary>
    public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    /// <summary>是否为玩家角色</summary>
    public bool IsPlayer
    {
        get
        {
            if (overrideRoleType)
                return isPlayer;
            return gameObject.CompareTag("Player");
        }
    }

    /// <summary>是否正在受击硬直中</summary>
    public bool IsInHitstun => isInHitstun;

    /// <summary>是否正在无敌帧中</summary>
    public bool IsInInvincibility => isInInvincibility;

    /// <summary>硬直剩余时间比例（0-1，用于动画）</summary>
    public float HitstunProgress => totalHitstunDuration > 0 ? hitstunTimer / totalHitstunDuration : 0f;

    /// <summary>硬直恢复比例（硬直动画播放到此比例时恢复控制）</summary>
    public float HitstunRecoveryRatio => hitstunRecoveryRatio;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeCombatSystem();
    }

    /// <summary>
    /// 每帧更新 - 处理硬直和无敌帧计时器
    /// </summary>
    private void Update()
    {
        // 更新硬直计时器
        if (isInHitstun)
        {
            hitstunTimer += Time.deltaTime;
            if (hitstunTimer >= totalHitstunDuration)
            {
                EndHitstun();
            }
        }

        // 更新无敌帧计时器
        if (isInInvincibility)
        {
            invincibilityTimer += Time.deltaTime;

            // 闪烁效果
            if (enableFlashEffect && cachedRenderer != null)
            {
                float flash = Mathf.Sin(invincibilityTimer * flashFrequency) * 0.5f + 0.5f;
                Color c = originalMaterialColor;
                c.a = Mathf.Lerp(0.3f, 1f, flash);
                cachedRenderer.material.color = c;
            }

            if (invincibilityTimer >= invincibilityDuration)
            {
                EndInvincibility();
            }
        }
    }

    /// <summary>
    /// 销毁时取消EventBus订阅，防止内存泄漏
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化战斗系统
    /// </summary>
    private void InitializeCombatSystem()
    {
        if (isInitialized) return;

        // 初始化当前生命值为最大值
        currentHealth = maxHealth;

        // 重置死亡状态
        isDead = false;

        // Phase12: 从GameConfig读取硬直/无敌帧参数
        var cfg = GameConfig.Instance;
        hitstunDuration = cfg.hitstunDuration;
        hitstunRecoveryRatio = cfg.hitstunRecoveryRatio;
        invincibilityDuration = cfg.invincibilityDuration;

        // 重置硬直和无敌帧状态
        isInHitstun = false;
        hitstunTimer = 0f;
        isInInvincibility = false;
        invincibilityTimer = 0f;

        // 缓存Renderer组件（用于无敌帧闪烁效果）
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null && cachedRenderer.material != null)
        {
            originalMaterialColor = cachedRenderer.material.color;
        }

        // 自动判断角色类型（如果未手动覆盖）
        if (!overrideRoleType)
        {
            isPlayer = gameObject.CompareTag("Player");
        }

        // 订阅重生事件
        SubscribeEvents();

        isInitialized = true;
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅重生事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
        EventBus.Subscribe("ON_ENEMY_RESPAWN", OnEnemyRespawn);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
        EventBus.Unsubscribe("ON_ENEMY_RESPAWN", OnEnemyRespawn);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家重生事件处理
    /// </summary>
    private void OnPlayerRespawn()
    {
        if (IsPlayer)
        {
            ResetCombatState();
            Debug.Log("[CombatSystem] 玩家重生，血量已重置");
        }
    }

    /// <summary>
    /// 敌人重生事件处理
    /// </summary>
    private void OnEnemyRespawn()
    {
        if (!IsPlayer)
        {
            ResetCombatState();
            Debug.Log("[CombatSystem] 敌人重生，血量已重置");
        }
    }

    #endregion

    #region 核心战斗逻辑

    /// <summary>
    /// 受到伤害 - 通用扣血接口
    /// 玩家和敌人共用此方法
    /// 包含：硬直检查、无敌帧检查、扣血、发布事件
    /// </summary>
    /// <param name="attacker">攻击者对象（可为null）</param>
    /// <param name="damageValue">伤害值</param>
    /// <param name="hitPos">受击位置（用于特效生成）</param>
    public void TakeDamage(GameObject attacker, float damageValue, Vector3 hitPos)
    {
        // 状态锁检查：已死亡则禁止再次受击
        if (isDead)
        {
            Debug.LogWarning("[CombatSystem] 角色已死亡，无法再次受击");
            return;
        }

        // 无敌状态检查（包括硬性无敌和无敌帧）
        if (isInvincible || isInInvincibility)
        {
            Debug.Log("[CombatSystem] 角色处于无敌状态，免疫伤害");
            return;
        }

        // 硬直期间可以受击（但会打断当前硬直）
        if (isInHitstun)
        {
            // 重置硬直计时器（受击硬直可以被打断）
            hitstunTimer = 0f;
        }

        // 伤害值校验
        if (damageValue <= 0)
        {
            Debug.LogWarning("[CombatSystem] 伤害值必须大于0");
            return;
        }

        // 扣除生命值
        int damage = Mathf.CeilToInt(damageValue);
        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log($"[CombatSystem] {gameObject.name} 受到 {damage} 点伤害，剩余血量: {currentHealth}/{maxHealth}");

        // Phase11: 触发血量变化回调（BossAI等订阅者监听）
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // 发布受伤事件
        PublishHitEvent(attacker, hitPos);

        // 启动受击硬直
        if (hitstunDuration > 0)
        {
            StartHitstun();
        }

        // 启动无敌帧
        if (invincibilityDuration > 0)
        {
            StartInvincibility();
        }

        // 检查是否死亡
        if (currentHealth <= 0)
        {
            Die(attacker);
        }
    }

    /// <summary>
    /// 受到伤害（简化版本，不传递攻击者和位置）
    /// </summary>
    /// <param name="damageValue">伤害值</param>
    public void TakeDamage(float damageValue)
    {
        TakeDamage(null, damageValue, Vector3.zero);
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    /// <param name="healValue">恢复量</param>
    public void Heal(int healValue)
    {
        if (isDead)
        {
            Debug.LogWarning("[CombatSystem] 角色已死亡，无法恢复血量");
            return;
        }

        if (healValue <= 0)
        {
            Debug.LogWarning("[CombatSystem] 恢复量必须大于0");
            return;
        }

        int oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healValue);

        int actualHeal = currentHealth - oldHealth;
        Debug.Log($"[CombatSystem] {gameObject.name} 恢复 {actualHeal} 点血量，当前血量: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// 设置生命值（百分比）
    /// </summary>
    /// <param name="percent">百分比（0-1）</param>
    public void SetHealthPercent(float percent)
    {
        if (isDead) return;

        percent = Mathf.Clamp01(percent);
        currentHealth = Mathf.RoundToInt(maxHealth * percent);

        Debug.Log($"[CombatSystem] {gameObject.name} 血量设置为 {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// 立即死亡
    /// </summary>
    public void Kill()
    {
        if (isDead) return;

        currentHealth = 0;
        Die(null);
    }

    /// <summary>
    /// 死亡处理 - 内部方法
    /// </summary>
    /// <param name="attacker">击杀者（可为null）</param>
    private void Die(GameObject attacker)
    {
        // 状态锁：标记为已死亡
        isDead = true;

        Debug.Log($"[CombatSystem] {gameObject.name} 死亡");

        // 根据角色类型发布对应死亡事件
        if (IsPlayer)
        {
            EventBus.Publish("ON_PLAYER_DIE");
        }
        else
        {
            EventBus.Publish("ON_ENEMY_DIE");
        }
    }

    /// <summary>
    /// 发布受伤事件 - 内部方法
    /// </summary>
    /// <param name="attacker">攻击者</param>
    /// <param name="hitPos">受击位置</param>
    private void PublishHitEvent(GameObject attacker, Vector3 hitPos)
    {
        // 根据角色类型发布对应受伤事件
        if (IsPlayer)
        {
            EventBus.Publish("ON_PLAYER_HIT");
        }
        else
        {
            EventBus.Publish("ON_ENEMY_HIT");
        }
    }

    #endregion

    #region 状态重置

    /// <summary>
    /// 重置战斗状态 - 用于重生
    /// </summary>
    public void ResetCombatState()
    {
        // 重置血量
        currentHealth = maxHealth;

        // 重置死亡状态
        isDead = false;

        // 重置硬直状态
        EndHitstun();

        // 重置无敌帧状态
        EndInvincibility();

        // 重置材质颜色
        if (cachedRenderer != null)
        {
            cachedRenderer.material.color = originalMaterialColor;
        }

        Debug.Log($"[CombatSystem] {gameObject.name} 战斗状态已重置");
    }

    /// <summary>
    /// 设置无敌状态
    /// </summary>
    /// <param name="invincible">是否无敌</param>
    public void SetInvincible(bool invincible)
    {
        isInvincible = invincible;
        Debug.Log($"[CombatSystem] {gameObject.name} 无敌状态: {invincible}");
    }

    /// <summary>
    /// 设置最大生命值
    /// </summary>
    /// <param name="newMaxHealth">新的最大生命值</param>
    /// <param name="resetToFull">是否同时回满血</param>
    public void SetMaxHealth(int newMaxHealth, bool resetToFull = true)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);

        if (resetToFull)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
    }

    /// <summary>Phase11: 设置当前血量</summary>
    /// <param name="hp">新的当前血量</param>
    public void SetCurrentHealth(int hp)
    {
        currentHealth = Mathf.Clamp(hp, 0, maxHealth);
    }

    #endregion

    #region 硬直和无敌帧系统

    /// <summary>
    /// 启动受击硬直
    /// </summary>
    private void StartHitstun()
    {
        isInHitstun = true;
        hitstunTimer = 0f;
        totalHitstunDuration = hitstunDuration;

        // 根据角色类型发布硬直事件
        if (IsPlayer)
        {
            EventBus.Publish("ON_PLAYER_STUN");
        }
        else
        {
            EventBus.Publish("ON_ENEMY_STUN");
        }

        Debug.Log($"[CombatSystem] {gameObject.name} 进入受击硬直，持续 {hitstunDuration} 秒");
    }

    /// <summary>
    /// 结束受击硬直
    /// </summary>
    private void EndHitstun()
    {
        if (!isInHitstun) return;

        isInHitstun = false;
        hitstunTimer = 0f;

        Debug.Log($"[CombatSystem] {gameObject.name} 受击硬直结束");
    }

    /// <summary>
    /// 启动无敌帧
    /// </summary>
    private void StartInvincibility()
    {
        isInInvincibility = true;
        invincibilityTimer = 0f;

        Debug.Log($"[CombatSystem] {gameObject.name} 进入无敌帧，持续 {invincibilityDuration} 秒");
    }

    /// <summary>
    /// 结束无敌帧
    /// </summary>
    private void EndInvincibility()
    {
        if (!isInInvincibility) return;

        isInInvincibility = false;
        invincibilityTimer = 0f;

        // 恢复材质颜色
        if (cachedRenderer != null)
        {
            cachedRenderer.material.color = originalMaterialColor;
        }

        Debug.Log($"[CombatSystem] {gameObject.name} 无敌帧结束");
    }

    /// <summary>
    /// 手动结束硬直（用于外部调用，如玩家输入打断）
    /// </summary>
    public void ForceEndHitstun()
    {
        if (isInHitstun)
        {
            EndHitstun();
            Debug.Log($"[CombatSystem] {gameObject.name} 硬直被强制结束");
        }
    }

    /// <summary>
    /// 设置硬直持续时间（运行时可修改）
    /// </summary>
    /// <param name="duration">新的硬直时间</param>
    public void SetHitstunDuration(float duration)
    {
        hitstunDuration = Mathf.Max(0f, duration);
    }

    /// <summary>
    /// 设置无敌帧持续时间（运行时可修改）
    /// </summary>
    /// <param name="duration">新的无敌帧时间</param>
    public void SetInvincibilityDuration(float duration)
    {
        invincibilityDuration = Mathf.Max(0f, duration);
    }

    #endregion
}
