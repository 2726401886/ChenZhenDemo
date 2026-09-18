using UnityEngine;

/// <summary>
/// 技能数据载体 - 定义单个技能的配置和状态
/// 纯数据类，不包含MonoBehaviour逻辑
/// 支持冷却计时、耐力消耗、技能持续时间
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class Skill
{
    #region 技能配置

    [Header("基础设置")]
    [Tooltip("技能唯一标识名称")]
    public string skillName = "UnnamedSkill";

    [Tooltip("技能显示名称")]
    public string displayName = "未命名技能";

    [Tooltip("技能描述")]
    public string description = "";

    [Tooltip("技能图标（UI用）")]
    public Sprite icon;

    [Tooltip("绑定按键")]
    public KeyCode keyBind = KeyCode.None;

    [Header("冷却设置")]
    [Tooltip("技能冷却时间（秒）")]
    public float cooldown = 5f;

    [Tooltip("当前冷却剩余时间")]
    public float currentCooldown = 0f;

    [Header("消耗设置")]
    [Tooltip("耐力消耗值")]
    public float staminaCost = 20f;

    [Header("持续设置")]
    [Tooltip("技能持续时间（秒），0表示瞬发")]
    public float duration = 0f;

    [Tooltip("当前技能已持续时间")]
    public float currentDuration = 0f;

    [Header("伤害设置")]
    [Tooltip("技能伤害倍率（基于攻击力）")]
    public float damageMultiplier = 1.5f;

    [Tooltip("技能范围（AOE用）")]
    public float skillRange = 3f;

    [Tooltip("冲刺距离（冲刺技能用）")]
    public float dashDistance = 8f;

    [Tooltip("冲刺速度")]
    public float dashSpeed = 20f;

    [Tooltip("投射物速度（远程技能用）")]
    public float projectileSpeed = 15f;

    [Tooltip("治疗量（治疗技能用）")]
    public float healAmount = 30f;

    [Tooltip("Buff名称（增益技能用）")]
    public string buffName = "";

    [Tooltip("Buff持续时间")]
    public float buffDuration = 8f;

    [Tooltip("攻击力加成百分比")]
    public float buffAttackBonus = 0.5f;

    #endregion

    #region 运行时状态

    /// <summary>技能是否正在冷却中</summary>
    public bool IsOnCooldown => currentCooldown > 0f;

    /// <summary>技能是否正在激活中</summary>
    public bool IsActive => currentDuration > 0f && duration > 0f;

    /// <summary>技能是否可用（不在冷却且耐力足够）</summary>
    public bool IsReady => !IsOnCooldown && !IsActive;

    #endregion

    #region 技能操作

    /// <summary>
    /// 启动技能冷却
    /// </summary>
    public void StartCooldown()
    {
        currentCooldown = cooldown;
    }

    /// <summary>
    /// 启动技能持续时间
    /// </summary>
    public void StartDuration()
    {
        if (duration > 0f)
        {
            currentDuration = duration;
        }
    }

    /// <summary>
    /// 更新冷却和持续时间计时
    /// </summary>
    /// <param name="deltaTime">帧间隔时间</param>
    public void UpdateTimers(float deltaTime)
    {
        // 更新冷却
        if (currentCooldown > 0f)
        {
            currentCooldown -= deltaTime;
            if (currentCooldown < 0f)
                currentCooldown = 0f;
        }

        // 更新持续时间
        if (currentDuration > 0f)
        {
            currentDuration -= deltaTime;
            if (currentDuration < 0f)
                currentDuration = 0f;
        }
    }

    /// <summary>
    /// 重置技能状态
    /// </summary>
    public void Reset()
    {
        currentCooldown = 0f;
        currentDuration = 0f;
    }

    /// <summary>
    /// 获取冷却剩余百分比（0-1）
    /// </summary>
    public float GetCooldownPercent()
    {
        if (cooldown <= 0f) return 0f;
        return currentCooldown / cooldown;
    }

    /// <summary>
    /// 获取持续时间百分比（0-1）
    /// </summary>
    public float GetDurationPercent()
    {
        if (duration <= 0f) return 0f;
        return currentDuration / duration;
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建技能实例
    /// </summary>
    public Skill() { }

    /// <summary>
    /// 创建技能实例（带参数）
    /// </summary>
    public Skill(string name, float cooldown, float staminaCost, float duration = 0f)
    {
        this.skillName = name;
        this.displayName = name;
        this.cooldown = cooldown;
        this.staminaCost = staminaCost;
        this.duration = duration;
    }

    #endregion
}
