using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家技能管理器 - 管理玩家所有技能的释放、冷却、耐力系统
/// 挂载到Player根GameObject，监听输入事件
/// 包含耐力系统：普攻消耗10耐力，技能消耗配置耐力，耐力自动恢复
/// 通过EventBus发布技能事件，供音效/特效系统响应
/// WebGL平台兼容
/// </summary>
public class SkillManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>释放技能事件</summary>
    public const string ON_SKILL_CAST = "ON_SKILL_CAST";

    /// <summary>技能结束事件</summary>
    public const string ON_SKILL_END = "ON_SKILL_END";

    /// <summary>耐力变化事件</summary>
    public const string ON_STAMINA_CHANGE = "ON_STAMINA_CHANGE";

    #endregion

    #region Inspector可配置参数

    [Header("耐力设置")]
    [Tooltip("最大耐力值")]
    [SerializeField] private float maxStamina = 100f;

    [Tooltip("当前耐力值")]
    [SerializeField] private float currentStamina = 100f;

    [Tooltip("耐力恢复速度（每秒）")]
    [SerializeField] private float staminaRegenRate = 15f;

    [Tooltip("耐力恢复延迟（秒，停止消耗后多久开始恢复）")]
    [SerializeField] private float staminaRegenDelay = 1f;

    [Tooltip("普攻耐力消耗")]
    [SerializeField] private float attackStaminaCost = 10f;

    [Header("技能配置")]
    [Tooltip("技能列表")]
    [SerializeField] private List<Skill> skills = new List<Skill>();

    [Header("组件引用")]
    [Tooltip("CombatSystem组件")]
    [SerializeField] private CombatSystem combatSystem;

    [Tooltip("玩家控制器")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("BuffManager组件")]
    [SerializeField] private BuffManager buffManager;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>耐力恢复延迟计时器</summary>
    private float staminaRegenTimer = 0f;

    /// <summary>是否正在恢复耐力</summary>
    private bool isRegeneratingStamina = false;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>当前激活的技能索引（-1表示无）</summary>
    private int activeSkillIndex = -1;

    #endregion

    #region 公共属性

    /// <summary>最大耐力值</summary>
    public float MaxStamina => maxStamina;

    /// <summary>当前耐力值</summary>
    public float CurrentStamina => currentStamina;

    /// <summary>耐力百分比（0-1）</summary>
    public float StaminaPercent => maxStamina > 0 ? currentStamina / maxStamina : 0f;

    /// <summary>技能数量</summary>
    public int SkillCount => skills.Count;

    /// <summary>是否正在释放技能</summary>
    public bool IsCastingSkill => activeSkillIndex >= 0;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeSkillManager();
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 更新技能计时
        UpdateSkillTimers();

        // 更新耐力恢复
        UpdateStaminaRegen();

        // 检测技能输入
        CheckSkillInput();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    private void InitializeSkillManager()
    {
        if (isInitialized) return;

        // 获取组件引用
        if (combatSystem == null)
            combatSystem = GetComponent<CombatSystem>();
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (buffManager == null)
            buffManager = GetComponent<BuffManager>();

        // Phase12: 从GameConfig读取耐力设置
        var cfg = GameConfig.Instance;
        maxStamina = cfg.maxStamina;
        currentStamina = maxStamina;
        staminaRegenRate = cfg.staminaRegenRate;
        staminaRegenDelay = cfg.staminaRegenDelay;
        attackStaminaCost = cfg.attackStaminaCost;

        // 初始化技能列表（如果为空，创建默认测试技能）
        if (skills.Count == 0)
        {
            CreateDefaultSkills();
        }

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[SkillManager] 技能管理器初始化完成（配置驱动）");
    }

    /// <summary>
    /// 创建默认测试技能 - Phase12: 从SkillConfig读取数值
    /// </summary>
    private void CreateDefaultSkills()
    {
        // Q键旋风斩
        var qCfg = SkillConfig.Get("WhirlwindSlash");
        Skill whirlwind = new Skill("WhirlwindSlash", qCfg.cooldown, qCfg.staminaCost, qCfg.duration);
        whirlwind.displayName = qCfg.displayName;
        whirlwind.description = qCfg.description;
        whirlwind.keyBind = qCfg.keyBind;
        whirlwind.damageMultiplier = qCfg.damageMultiplier;
        whirlwind.skillRange = qCfg.skillRange;
        skills.Add(whirlwind);

        // E键冲刺突进
        var eCfg = SkillConfig.Get("DashStrike");
        Skill dash = new Skill("DashStrike", eCfg.cooldown, eCfg.staminaCost, eCfg.duration);
        dash.displayName = eCfg.displayName;
        dash.description = eCfg.description;
        dash.keyBind = eCfg.keyBind;
        dash.damageMultiplier = eCfg.damageMultiplier;
        dash.dashDistance = eCfg.dashDistance;
        dash.dashSpeed = eCfg.dashSpeed;
        skills.Add(dash);

        // R键能量弹
        var rCfg = SkillConfig.Get("EnergyBall");
        Skill energyBall = new Skill("EnergyBall", rCfg.cooldown, rCfg.staminaCost, rCfg.duration);
        energyBall.displayName = rCfg.displayName;
        energyBall.description = rCfg.description;
        energyBall.keyBind = rCfg.keyBind;
        energyBall.damageMultiplier = rCfg.damageMultiplier;
        energyBall.projectileSpeed = rCfg.projectileSpeed;
        energyBall.skillRange = rCfg.skillRange;
        skills.Add(energyBall);

        // F键自我治疗
        var fCfg = SkillConfig.Get("SelfHeal");
        Skill selfHeal = new Skill("SelfHeal", fCfg.cooldown, fCfg.staminaCost, fCfg.duration);
        selfHeal.displayName = fCfg.displayName;
        selfHeal.description = fCfg.description;
        selfHeal.keyBind = fCfg.keyBind;
        selfHeal.healAmount = fCfg.healAmount;
        skills.Add(selfHeal);

        // T键狂暴增益
        var tCfg = SkillConfig.Get("BerserkBuff");
        Skill berserk = new Skill("BerserkBuff", tCfg.cooldown, tCfg.staminaCost, tCfg.duration);
        berserk.displayName = tCfg.displayName;
        berserk.description = tCfg.description;
        berserk.keyBind = tCfg.keyBind;
        berserk.buffName = tCfg.buffName;
        berserk.buffDuration = tCfg.buffDuration;
        berserk.buffAttackBonus = tCfg.buffAttackBonus;
        skills.Add(berserk);

        DebugLog("[SkillManager] 从SkillConfig创建5个技能完成");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Subscribe("ON_GAME_RESUME", OnGameResume);
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Unsubscribe("ON_GAME_RESUME", OnGameResume);
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    private void OnGamePause()
    {
        enabled = false;
    }

    private void OnGameResume()
    {
        enabled = true;
    }

    private void OnPlayerDie()
    {
        // 死亡时重置所有技能
        ResetAllSkills();
    }

    #endregion

    #region 技能输入检测

    private void CheckSkillInput()
    {
        // 遍历所有技能，检查按键
        for (int i = 0; i < skills.Count; i++)
        {
            if (Input.GetKeyDown(skills[i].keyBind))
            {
                TryCastSkill(i);
                break; // 一次只释放一个技能
            }
        }
    }

    #endregion

    #region 技能释放

    /// <summary>
    /// 尝试释放技能
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    /// <returns>是否成功释放</returns>
    public bool TryCastSkill(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= skills.Count)
        {
            DebugLog("[SkillManager] 无效的技能索引: " + skillIndex);
            return false;
        }

        Skill skill = skills[skillIndex];

        // 检查技能是否可用
        if (!skill.IsReady)
        {
            DebugLog("[SkillManager] 技能不可用: " + skill.displayName + " (冷却中或正在释放)");
            return false;
        }

        // 检查耐力是否足够
        if (currentStamina < skill.staminaCost)
        {
            DebugLog("[SkillManager] 耐力不足: " + skill.displayName + " (需要:" + skill.staminaCost + " 当前:" + currentStamina + ")");
            return false;
        }

        // 消耗耐力
        ConsumeStamina(skill.staminaCost);

        // 启动技能
        StartSkill(skillIndex);

        return true;
    }

    /// <summary>
    /// 尝试释放技能（按名称）
    /// </summary>
    public bool TryCastSkill(string skillName)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].skillName == skillName)
            {
                return TryCastSkill(i);
            }
        }
        DebugLog("[SkillManager] 找不到技能: " + skillName);
        return false;
    }

    /// <summary>
    /// 释放普攻（消耗耐力）
    /// </summary>
    public bool TryAttack()
    {
        if (currentStamina < attackStaminaCost)
        {
            DebugLog("[SkillManager] 耐力不足，无法攻击");
            return false;
        }

        ConsumeStamina(attackStaminaCost);
        return true;
    }

    #endregion

    #region 技能执行

    /// <summary>
    /// 启动技能
    /// </summary>
    private void StartSkill(int skillIndex)
    {
        Skill skill = skills[skillIndex];
        activeSkillIndex = skillIndex;

        // 启动冷却和持续时间
        skill.StartCooldown();
        skill.StartDuration();

        DebugLog("[SkillManager] 释放技能: " + skill.displayName);

        // 发布技能释放事件
        Dictionary<string, object> skillData = new Dictionary<string, object>
        {
            { "skillName", skill.skillName },
            { "displayName", skill.displayName },
            { "skillIndex", skillIndex }
        };
        EventBus.Publish(ON_SKILL_CAST, skillData);

        // 执行技能效果
        ExecuteSkillEffect(skill);
    }

    /// <summary>
    /// 执行技能效果
    /// </summary>
    private void ExecuteSkillEffect(Skill skill)
    {
        switch (skill.skillName)
        {
            case "WhirlwindSlash":
                ExecuteWhirlwindSlash(skill);
                break;
            case "DashStrike":
                ExecuteDashStrike(skill);
                break;
            case "EnergyBall":
                ExecuteEnergyBall(skill);
                break;
            case "SelfHeal":
                ExecuteSelfHeal(skill);
                break;
            case "BerserkBuff":
                ExecuteBerserkBuff(skill);
                break;
            default:
                DebugLog("[SkillManager] 未知技能: " + skill.skillName);
                break;
        }
    }

    /// <summary>
    /// 执行旋风斩
    /// </summary>
    private void ExecuteWhirlwindSlash(Skill skill)
    {
        DebugLog("[SkillManager] 执行旋风斩 - 范围AOE攻击");

        // 获取附近所有敌人
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, skill.skillRange);

        foreach (var hitCollider in hitColliders)
        {
            // 跳过自己
            if (hitCollider.gameObject == gameObject) continue;

            // 检查是否是敌人
            CombatSystem targetCombat = hitCollider.GetComponent<CombatSystem>();
            if (targetCombat != null && !targetCombat.IsPlayer)
            {
                // 计算伤害（基础攻击力 * 技能倍率）
                float baseDamage = 10f; // 基础攻击力
                float finalDamage = baseDamage * skill.damageMultiplier;

                // 对目标造成伤害
                targetCombat.TakeDamage(gameObject, finalDamage, hitCollider.transform.position);

                DebugLog("[SkillManager] 旋风斩命中: " + hitCollider.name + " 伤害: " + finalDamage);
            }
        }
    }

    /// <summary>
    /// 执行冲刺突进
    /// </summary>
    private void ExecuteDashStrike(Skill skill)
    {
        DebugLog("[SkillManager] 执行冲刺突进");

        // 获取冲刺方向（向前）
        Vector3 dashDirection = transform.forward;

        // 启动冲刺协程
        StartCoroutine(DashCoroutine(skill, dashDirection));
    }

    /// <summary>
    /// 冲刺协程
    /// </summary>
    private System.Collections.IEnumerator DashCoroutine(Skill skill, Vector3 direction)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) yield break;

        // 冲刺期间临时启用无敌
        CombatSystem combat = GetComponent<CombatSystem>();
        bool wasInvincible = false;
        if (combat != null)
        {
            wasInvincible = combat.IsInvincible;
            combat.SetInvincible(true);
        }

        float distanceTraveled = 0f;
        float dashStep = skill.dashSpeed * Time.deltaTime;

        while (distanceTraveled < skill.dashDistance)
        {
            // 移动
            cc.Move(direction * dashStep);
            distanceTraveled += dashStep;

            // 检查碰撞
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1f);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == gameObject) continue;

                CombatSystem targetCombat = hitCollider.GetComponent<CombatSystem>();
                if (targetCombat != null && !targetCombat.IsPlayer)
                {
                    float baseDamage = 10f;
                    float finalDamage = baseDamage * skill.damageMultiplier;
                    targetCombat.TakeDamage(gameObject, finalDamage, hitCollider.transform.position);
                    DebugLog("[SkillManager] 冲刺命中: " + hitCollider.name);
                }
            }

            yield return null;
        }

        // 恢复无敌状态
        if (combat != null && !wasInvincible)
        {
            combat.SetInvincible(false);
        }

        DebugLog("[SkillManager] 冲刺结束");
    }

    /// <summary>
    /// 执行远程能量弹
    /// </summary>
    private void ExecuteEnergyBall(Skill skill)
    {
        DebugLog("[SkillManager] 执行远程能量弹");

        // 创建投射物
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "EnergyBall";
        projectile.transform.position = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
        projectile.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        // 设置颜色
        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.SetColor("_Color", new Color(0.3f, 0.8f, 1f));
        }

        // 添加碰撞器
        Collider col = projectile.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // 添加投射物脚本
        EnergyBallProjectile ebp = projectile.AddComponent<EnergyBallProjectile>();
        ebp.Initialize(transform.forward, skill.projectileSpeed, skill.damageMultiplier, gameObject);

        DebugLog("[SkillManager] 能量弹发射");
    }

    /// <summary>
    /// 执行自我治疗
    /// </summary>
    private void ExecuteSelfHeal(Skill skill)
    {
        DebugLog("[SkillManager] 执行自我治疗 - 恢复血量: " + skill.healAmount);

        if (combatSystem != null)
        {
            combatSystem.Heal(Mathf.RoundToInt(skill.healAmount));
        }
    }

    /// <summary>
    /// 执行狂暴增益
    /// </summary>
    private void ExecuteBerserkBuff(Skill skill)
    {
        DebugLog("[SkillManager] 执行狂暴增益 - 攻击力+50% 持续" + skill.buffDuration + "秒");

        if (buffManager != null)
        {
            Buff berserkBuff = new Buff(skill.buffName, skill.buffDuration, true);
            berserkBuff.displayName = "狂暴";
            berserkBuff.description = "攻击力提升50%";
            berserkBuff.attackBonus = skill.buffAttackBonus;
            buffManager.AddBuff(berserkBuff);
        }
    }

    #endregion

    #region 技能计时更新

    private void UpdateSkillTimers()
    {
        for (int i = 0; i < skills.Count; i++)
        {
            Skill skill = skills[i];
            bool wasActive = skill.IsActive;

            skill.UpdateTimers(Time.deltaTime);

            // 检查技能是否刚结束
            if (wasActive && !skill.IsActive)
            {
                OnSkillEnd(i);
            }
        }
    }

    /// <summary>
    /// 技能结束回调
    /// </summary>
    private void OnSkillEnd(int skillIndex)
    {
        Skill skill = skills[skillIndex];
        activeSkillIndex = -1;

        DebugLog("[SkillManager] 技能结束: " + skill.displayName);

        // 发布技能结束事件
        Dictionary<string, object> skillData = new Dictionary<string, object>
        {
            { "skillName", skill.skillName },
            { "skillIndex", skillIndex }
        };
        EventBus.Publish(ON_SKILL_END, skillData);
    }

    #endregion

    #region 耐力系统

    /// <summary>
    /// 消耗耐力
    /// </summary>
    private void ConsumeStamina(float amount)
    {
        currentStamina -= amount;
        if (currentStamina < 0f) currentStamina = 0f;

        // 重置恢复计时器
        staminaRegenTimer = staminaRegenDelay;
        isRegeneratingStamina = false;

        // 发布耐力变化事件
        EventBus.Publish(ON_STAMINA_CHANGE);

        DebugLog("[SkillManager] 消耗耐力: " + amount + " 剩余: " + currentStamina);
    }

    /// <summary>
    /// 更新耐力恢复
    /// </summary>
    private void UpdateStaminaRegen()
    {
        if (currentStamina >= maxStamina) return;

        if (!isRegeneratingStamina)
        {
            // 等待恢复延迟
            staminaRegenTimer -= Time.deltaTime;
            if (staminaRegenTimer <= 0f)
            {
                isRegeneratingStamina = true;
            }
        }
        else
        {
            // 恢复耐力
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina > maxStamina)
                currentStamina = maxStamina;

            // 发布耐力变化事件
            EventBus.Publish(ON_STAMINA_CHANGE);
        }
    }

    #endregion

    #region 公共API

    /// <summary>
    /// 获取技能信息
    /// </summary>
    public Skill GetSkill(int index)
    {
        if (index >= 0 && index < skills.Count)
            return skills[index];
        return null;
    }

    /// <summary>
    /// 获取技能信息（按名称）
    /// </summary>
    public Skill GetSkill(string skillName)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].skillName == skillName)
                return skills[i];
        }
        return null;
    }

    /// <summary>
    /// 重置所有技能
    /// </summary>
    public void ResetAllSkills()
    {
        foreach (var skill in skills)
        {
            skill.Reset();
        }
        activeSkillIndex = -1;
    }

    /// <summary>
    /// 恢复全部耐力
    /// </summary>
    public void RestoreFullStamina()
    {
        currentStamina = maxStamina;
        EventBus.Publish(ON_STAMINA_CHANGE);
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
