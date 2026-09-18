using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家Buff管理器 - 管理玩家身上所有Buff的添加、计时、移除
/// 挂载到Player根GameObject
/// 通过EventBus发布Buff事件，供UI/音效/特效系统响应
/// WebGL平台兼容
/// </summary>
public class BuffManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>Buff添加/刷新事件</summary>
    public const string ON_BUFF_ADD = "ON_BUFF_ADD";

    /// <summary>Buff移除事件</summary>
    public const string ON_BUFF_REMOVE = "ON_BUFF_REMOVE";

    /// <summary>Buff更新事件（层数/持续时间变化）</summary>
    public const string ON_BUFF_UPDATE = "ON_BUFF_UPDATE";

    #endregion

    #region Inspector可配置参数

    [Header("Buff配置")]
    [Tooltip("当前生效的Buff列表")]
    [SerializeField] private List<Buff> activeBuffs = new List<Buff>();

    [Header("组件引用")]
    [Tooltip("CombatSystem组件")]
    [SerializeField] private CombatSystem combatSystem;

    [Tooltip("SkillManager组件")]
    [SerializeField] private SkillManager skillManager;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>当前生效Buff数量</summary>
    public int ActiveBuffCount => activeBuffs.Count;

    /// <summary>获取攻击力加成总百分比</summary>
    public float TotalAttackBonus
    {
        get
        {
            float total = 0f;
            foreach (var buff in activeBuffs)
            {
                if (buff.IsActive)
                    total += buff.GetTotalAttackBonus();
            }
            return total;
        }
    }

    /// <summary>获取防御力加成总百分比</summary>
    public float TotalDefenseBonus
    {
        get
        {
            float total = 0f;
            foreach (var buff in activeBuffs)
            {
                if (buff.IsActive)
                    total += buff.defenseBonus * buff.currentStacks;
            }
            return total;
        }
    }

    /// <summary>获取移动速度加成总百分比</summary>
    public float TotalSpeedBonus
    {
        get
        {
            float total = 0f;
            foreach (var buff in activeBuffs)
            {
                if (buff.IsActive)
                    total += buff.speedBonus * buff.currentStacks;
            }
            return total;
        }
    }

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeBuffManager();
    }

    private void Update()
    {
        if (!isInitialized) return;

        UpdateBuffTimers();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    private void InitializeBuffManager()
    {
        if (isInitialized) return;

        if (combatSystem == null)
            combatSystem = GetComponent<CombatSystem>();
        if (skillManager == null)
            skillManager = GetComponent<SkillManager>();

        SubscribeEvents();

        isInitialized = true;
        DebugLog("[BuffManager] Buff管理器初始化完成");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    private void OnPlayerDie()
    {
        ClearAllBuffs();
    }

    #endregion

    #region Buff计时更新

    private void UpdateBuffTimers()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            Buff buff = activeBuffs[i];

            // 处理持续伤害/治疗
            ProcessBuffEffect(buff);

            // 更新计时
            buff.UpdateTimer(Time.deltaTime);

            // 检查是否过期
            if (buff.IsExpired)
            {
                RemoveBuffAt(i);
            }
        }
    }

    /// <summary>
    /// 处理Buff效果（DOT/HOT）
    /// </summary>
    private void ProcessBuffEffect(Buff buff)
    {
        if (!buff.IsActive) return;

        // 每秒恢复血量
        if (buff.healPerSecond > 0f && combatSystem != null)
        {
            float healThisFrame = buff.healPerSecond * Time.deltaTime;
            combatSystem.Heal(Mathf.RoundToInt(healThisFrame));
        }

        // 每秒造成伤害
        if (buff.damagePerSecond > 0f && combatSystem != null)
        {
            float dmgThisFrame = buff.damagePerSecond * Time.deltaTime;
            combatSystem.TakeDamage(dmgThisFrame);
        }
    }

    #endregion

    #region Buff管理

    /// <summary>
    /// 添加Buff
    /// </summary>
    public void AddBuff(Buff newBuff)
    {
        if (newBuff == null) return;

        // 检查是否已存在同类Buff
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].buffName == newBuff.buffName)
            {
                // 叠加或刷新
                activeBuffs[i].StackBuff();
                DebugLog("[BuffManager] Buff叠加: " + newBuff.displayName + " 层数: " + activeBuffs[i].currentStacks);

                EventBus.Publish(ON_BUFF_UPDATE);
                return;
            }
        }

        // 新增Buff
        Buff buffCopy = new Buff();
        buffCopy.buffName = newBuff.buffName;
        buffCopy.displayName = newBuff.displayName;
        buffCopy.description = newBuff.description;
        buffCopy.isPositive = newBuff.isPositive;
        buffCopy.duration = newBuff.duration;
        buffCopy.currentDuration = newBuff.duration;
        buffCopy.maxStacks = newBuff.maxStacks;
        buffCopy.currentStacks = 1;
        buffCopy.attackBonus = newBuff.attackBonus;
        buffCopy.defenseBonus = newBuff.defenseBonus;
        buffCopy.speedBonus = newBuff.speedBonus;
        buffCopy.healPerSecond = newBuff.healPerSecond;
        buffCopy.damagePerSecond = newBuff.damagePerSecond;

        activeBuffs.Add(buffCopy);

        DebugLog("[BuffManager] 添加Buff: " + buffCopy.displayName);

        EventBus.Publish(ON_BUFF_ADD);
    }

    /// <summary>
    /// 按名称移除Buff
    /// </summary>
    public void RemoveBuff(string buffName)
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            if (activeBuffs[i].buffName == buffName)
            {
                RemoveBuffAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 移除指定索引的Buff
    /// </summary>
    private void RemoveBuffAt(int index)
    {
        Buff removedBuff = activeBuffs[index];
        activeBuffs.RemoveAt(index);

        DebugLog("[BuffManager] 移除Buff: " + removedBuff.displayName);

        EventBus.Publish(ON_BUFF_REMOVE);
    }

    /// <summary>
    /// 清除所有Buff
    /// </summary>
    public void ClearAllBuffs()
    {
        activeBuffs.Clear();
        DebugLog("[BuffManager] 清除所有Buff");
        EventBus.Publish(ON_BUFF_REMOVE);
    }

    /// <summary>
    /// 检查是否拥有指定Buff
    /// </summary>
    public bool HasBuff(string buffName)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].buffName == buffName && activeBuffs[i].IsActive)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 获取指定Buff
    /// </summary>
    public Buff GetBuff(string buffName)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].buffName == buffName)
                return activeBuffs[i];
        }
        return null;
    }

    /// <summary>
    /// 获取所有生效中的Buff
    /// </summary>
    public List<Buff> GetActiveBuffs()
    {
        return new List<Buff>(activeBuffs);
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
