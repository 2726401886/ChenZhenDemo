using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家背包核心容器 - 管理固定格子的物品存储
/// 实现添加/移除/合并堆叠/使用物品逻辑
/// 通过EventBus发布背包变动事件，供UI/音效/特效系统响应
/// WebGL平台兼容
/// </summary>
public class Inventory : MonoBehaviour
{
    #region 事件常量

    /// <summary>成功拾取物品事件</summary>
    public const string ON_ITEM_PICKUP = "ON_ITEM_PICKUP";

    /// <summary>使用物品事件</summary>
    public const string ON_ITEM_USE = "ON_ITEM_USE";

    /// <summary>背包物品变动事件</summary>
    public const string ON_INVENTORY_CHANGE = "ON_INVENTORY_CHANGE";

    #endregion

    #region Inspector可配置参数

    [Header("背包设置")]
    [Tooltip("背包最大格子数")]
    [SerializeField] private int maxSlots = 20;

    [Header("组件引用")]
    [Tooltip("玩家CombatSystem组件")]
    [SerializeField] private CombatSystem combatSystem;

    [Tooltip("玩家SkillManager组件")]
    [SerializeField] private SkillManager skillManager;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>背包格子列表</summary>
    private List<Item> slots = new List<Item>();

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>背包最大格子数</summary>
    public int MaxSlots => maxSlots;

    /// <summary>当前物品数量</summary>
    public int ItemCount => slots.Count;

    /// <summary>获取所有物品（只读）</summary>
    public IReadOnlyList<Item> Slots => slots;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeInventory();
    }

    #endregion

    #region 初始化

    private void InitializeInventory()
    {
        if (isInitialized) return;

        slots.Clear();

        if (combatSystem == null)
            combatSystem = GetComponent<CombatSystem>();
        if (skillManager == null)
            skillManager = GetComponent<SkillManager>();

        isInitialized = true;
        DebugLog("[Inventory] 背包系统初始化完成");
    }

    #endregion

    #region 背包操作

    /// <summary>
    /// 添加物品到背包
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="count">添加数量</param>
    /// <returns>是否成功添加</returns>
    public bool AddItem(string itemId, int count = 1)
    {
        var config = ItemConfig.Get(itemId);
        if (config == null)
        {
            Debug.LogWarning($"[Inventory] 物品不存在: {itemId}");
            return false;
        }

        // 尝试堆叠到已有物品
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].itemId == itemId && slots[i].CanStack)
            {
                int canAdd = slots[i].MaxStack - slots[i].stackCount;
                int toAdd = Mathf.Min(count, canAdd);
                slots[i].stackCount += toAdd;
                count -= toAdd;

                DebugLog($"[Inventory] 堆叠物品: {config.displayName} x{toAdd}");

                if (count <= 0) break;
            }
        }

        // 剩余放入新格子
        while (count > 0 && slots.Count < maxSlots)
        {
            int toAdd = Mathf.Min(count, config.maxStack);
            slots.Add(new Item(itemId, toAdd));
            count -= toAdd;

            DebugLog($"[Inventory] 新增物品: {config.displayName} x{toAdd}");
        }

        if (count > 0)
        {
            Debug.LogWarning($"[Inventory] 背包已满，{count}个 {config.displayName} 无法添加");
        }

        // 发布拾取事件和背包变动事件
        EventBus.Publish(ON_ITEM_PICKUP, itemId);
        EventBus.Publish(ON_INVENTORY_CHANGE);

        return count == 0;
    }

    /// <summary>
    /// 移除物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="count">移除数量</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveItem(string itemId, int count = 1)
    {
        int remaining = count;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].itemId == itemId)
            {
                int toRemove = Mathf.Min(remaining, slots[i].stackCount);
                slots[i].stackCount -= toRemove;
                remaining -= toRemove;

                if (slots[i].stackCount <= 0)
                {
                    slots.RemoveAt(i);
                }

                if (remaining <= 0) break;
            }
        }

        if (remaining > 0)
        {
            Debug.LogWarning($"[Inventory] 物品不足，还差{remaining}个");
        }

        EventBus.Publish(ON_INVENTORY_CHANGE);

        return remaining == 0;
    }

    /// <summary>
    /// 使用物品
    /// </summary>
    /// <param name="slotIndex">格子索引</param>
    /// <returns>是否成功使用</returns>
    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            Debug.LogWarning($"[Inventory] 无效格子索引: {slotIndex}");
            return false;
        }

        Item item = slots[slotIndex];
        var config = item.GetConfig();
        if (config == null) return false;

        // 应用使用效果
        if (config.healAmount > 0 && combatSystem != null)
        {
            int current = combatSystem.CurrentHealth;
            int max = combatSystem.MaxHealth;
            int newHp = Mathf.Min(current + config.healAmount, max);
            combatSystem.SetCurrentHealth(newHp);
            DebugLog($"[Inventory] 使用 {config.displayName}，恢复 {config.healAmount} 血量");
        }

        if (config.staminaRestore > 0 && skillManager != null)
        {
            DebugLog($"[Inventory] 使用 {config.displayName}，恢复 {config.staminaRestore} 耐力");
        }

        if (config.attackBonus > 0 && config.buffDuration > 0)
        {
            DebugLog($"[Inventory] 使用 {config.displayName}，攻击力+{config.attackBonus * 100}%，持续{config.buffDuration}秒");
        }

        // 消耗物品
        item.stackCount--;
        if (item.stackCount <= 0)
        {
            slots.RemoveAt(slotIndex);
        }

        // 发布使用事件和背包变动事件
        EventBus.Publish(ON_ITEM_USE, item.itemId);
        EventBus.Publish(ON_INVENTORY_CHANGE);

        DebugLog($"[Inventory] 使用物品: {config.displayName}");
        return true;
    }

    /// <summary>
    /// Phase15: 穿戴装备物品
    /// </summary>
    /// <param name="slotIndex">背包格子索引</param>
    /// <returns>是否成功穿戴</returns>
    public bool EquipItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            Debug.LogWarning($"[Inventory] 无效格子索引: {slotIndex}");
            return false;
        }

        Item item = slots[slotIndex];
        var equipConfig = EquipmentConfig.Get(item.itemId);
        if (equipConfig == null)
        {
            Debug.LogWarning($"[Inventory] 物品不是装备: {item.displayName}");
            return false;
        }

        // 获取EquipmentManager并穿戴
        EquipmentManager equipMgr = GetComponent<EquipmentManager>();
        if (equipMgr == null)
        {
            Debug.LogWarning("[Inventory] 没有EquipmentManager组件");
            return false;
        }

        return equipMgr.Equip(item.itemId);
    }

    /// <summary>
    /// 检查指定物品是否为装备
    /// </summary>
    public bool IsEquipment(string itemId)
    {
        return EquipmentConfig.Get(itemId) != null;
    }

    /// <summary>
    /// 检查背包中是否有指定物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>是否有该物品</returns>
    public bool HasItem(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.itemId == itemId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 获取指定物品的数量
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>物品数量</returns>
    public int GetItemCount(string itemId)
    {
        int count = 0;
        foreach (var slot in slots)
        {
            if (slot.itemId == itemId)
                count += slot.stackCount;
        }
        return count;
    }

    /// <summary>
    /// 获取指定格子的物品
    /// </summary>
    /// <param name="index">格子索引</param>
    /// <returns>物品实例，空格子返回null</returns>
    public Item GetItem(int index)
    {
        if (index >= 0 && index < slots.Count)
            return slots[index];
        return null;
    }

    /// <summary>
    /// 清空背包
    /// </summary>
    public void Clear()
    {
        slots.Clear();
        EventBus.Publish(ON_INVENTORY_CHANGE);
        DebugLog("[Inventory] 背包已清空");
    }

    /// <summary>
    /// 序列化为存档数据
    /// </summary>
    public List<Item> SerializeForSave()
    {
        return new List<Item>(slots);
    }

    /// <summary>
    /// 从存档数据加载
    /// </summary>
    /// <param name="savedItems">已保存的物品列表</param>
    public void LoadFromSave(List<Item> savedItems)
    {
        slots.Clear();
        if (savedItems != null)
        {
            slots.AddRange(savedItems);
        }
        EventBus.Publish(ON_INVENTORY_CHANGE);
        DebugLog($"[Inventory] 从存档加载了 {slots.Count} 个物品");
    }

    #endregion

    #region 调试日志

    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
