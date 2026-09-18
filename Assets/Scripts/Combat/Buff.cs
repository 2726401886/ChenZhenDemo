using UnityEngine;

/// <summary>
/// Buff数据载体 - 定义单个Buff的配置和状态
/// 纯数据类，不包含MonoBehaviour逻辑
/// 支持持续时间、叠加层数、增益/减益标记
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class Buff
{
    #region Buff配置

    [Header("基础设置")]
    [Tooltip("Buff唯一标识名称")]
    public string buffName = "UnnamedBuff";

    [Tooltip("Buff显示名称")]
    public string displayName = "未命名Buff";

    [Tooltip("Buff描述")]
    public string description = "";

    [Tooltip("Buff图标（UI用）")]
    public Sprite icon;

    [Tooltip("是否为增益Buff（true=增益，false=减益）")]
    public bool isPositive = true;

    [Header("持续设置")]
    [Tooltip("Buff持续时间（秒），0表示永久")]
    public float duration = 10f;

    [Tooltip("当前Buff剩余时间")]
    public float currentDuration = 0f;

    [Header("叠加设置")]
    [Tooltip("最大叠加层数")]
    public int maxStacks = 1;

    [Tooltip("当前叠加层数")]
    public int currentStacks = 0;

    [Header("效果设置")]
    [Tooltip("攻击力加成百分比（0.5 = +50%）")]
    public float attackBonus = 0f;

    [Tooltip("防御力加成百分比")]
    public float defenseBonus = 0f;

    [Tooltip("移动速度加成百分比")]
    public float speedBonus = 0f;

    [Tooltip("每秒恢复血量（治疗Buff用）")]
    public float healPerSecond = 0f;

    [Tooltip("每秒造成伤害（DOT用）")]
    public float damagePerSecond = 0f;

    #endregion

    #region 运行时状态

    /// <summary>Buff是否已过期</summary>
    public bool IsExpired => duration > 0f && currentDuration <= 0f;

    /// <summary>Buff是否永久（duration=0）</summary>
    public bool IsPermanent => duration <= 0f;

    /// <summary>Buff是否激活中</summary>
    public bool IsActive => currentStacks > 0 && !IsExpired;

    #endregion

    #region Buff操作

    /// <summary>
    /// 启动Buff持续时间
    /// </summary>
    public void StartDuration()
    {
        if (duration > 0f)
        {
            currentDuration = duration;
        }
    }

    /// <summary>
    /// 刷新Buff持续时间
    /// </summary>
    public void RefreshDuration()
    {
        if (duration > 0f)
        {
            currentDuration = duration;
        }
    }

    /// <summary>
    /// 叠加Buff
    /// </summary>
    public void StackBuff()
    {
        if (currentStacks < maxStacks)
        {
            currentStacks++;
        }
        RefreshDuration();
    }

    /// <summary>
    /// 更新Buff计时
    /// </summary>
    public void UpdateTimer(float deltaTime)
    {
        if (IsPermanent) return;

        currentDuration -= deltaTime;
        if (currentDuration < 0f)
            currentDuration = 0f;
    }

    /// <summary>
    /// 重置Buff状态
    /// </summary>
    public void Reset()
    {
        currentDuration = 0f;
        currentStacks = 0;
    }

    /// <summary>
    /// 获取持续时间百分比（0-1）
    /// </summary>
    public float GetDurationPercent()
    {
        if (duration <= 0f) return 1f;
        return currentDuration / duration;
    }

    /// <summary>
    /// 获取攻击力加成（考虑叠加层数）
    /// </summary>
    public float GetTotalAttackBonus()
    {
        return attackBonus * currentStacks;
    }

    #endregion

    #region 构造函数

    public Buff() { }

    public Buff(string name, float duration, bool isPositive = true)
    {
        this.buffName = name;
        this.displayName = name;
        this.duration = duration;
        this.isPositive = isPositive;
        this.currentStacks = 1;
        StartDuration();
    }

    #endregion
}
