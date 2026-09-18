using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装备管理器 - 管理玩家4个装备槽位的穿戴/卸下逻辑
/// 4个槽位：武器、头盔、胸甲、鞋子
/// 穿戴时叠加属性加成，卸下时移除加成
/// 通过EventBus发布装备事件
/// 属性加成与Buff系统兼容（独立乘区）
/// WebGL平台兼容
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>装备穿戴事件</summary>
    public const string ON_EQUIP = "ON_EQUIP";

    /// <summary>装备卸下事件</summary>
    public const string ON_UNEQUIP = "ON_UNEQUIP";

    #endregion

    #region Inspector可配置参数

    [Header("装备槽位")]
    [Tooltip("4个装备槽位（武器/头盔/胸甲/鞋子）")]
    [SerializeField] private Equipment[] equipSlots = new Equipment[4];

    [Header("组件引用")]
    [Tooltip("玩家CombatSystem组件")]
    [SerializeField] private CombatSystem combatSystem;

    [Tooltip("玩家Inventory组件")]
    [SerializeField] private Inventory inventory;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>累计属性加成缓存</summary>
    private float cachedAttackBonus = 0f;
    private float cachedDefenseBonus = 0f;
    private float cachedStaminaBonus = 0f;
    private float cachedSpeedBonus = 0f;
    private int cachedHealthBonus = 0;

    #endregion

    #region 公共属性

    /// <summary>总攻击力加成</summary>
    public float TotalAttackBonus => cachedAttackBonus;

    /// <summary>总防御力加成</summary>
    public float TotalDefenseBonus => cachedDefenseBonus;

    /// <summary>总耐力加成</summary>
    public float TotalStaminaBonus => cachedStaminaBonus;

    /// <summary>总速度加成</summary>
    public float TotalSpeedBonus => cachedSpeedBonus;

    /// <summary>总血量加成</summary>
    public int TotalHealthBonus => cachedHealthBonus;

    /// <summary>获取指定槽位装备</summary>
    public Equipment GetEquipped(EquipmentConfig.EquipSlot slot)
    {
        int index = (int)slot;
        if (index >= 0 && index < equipSlots.Length)
            return equipSlots[index];
        return null;
    }

    /// <summary>获取所有槽位</summary>
    public Equipment[] GetAllSlots => equipSlots;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeEquipmentManager();
    }

    #endregion

    #region 初始化

    private void InitializeEquipmentManager()
    {
        if (isInitialized) return;

        if (combatSystem == null)
            combatSystem = GetComponent<CombatSystem>();
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        // 初始化空槽位
        for (int i = 0; i < equipSlots.Length; i++)
        {
            if (equipSlots[i] == null)
                equipSlots[i] = new Equipment();
        }

        RecalculateBonuses();
        isInitialized = true;
        DebugLog("[EquipmentManager] 装备系统初始化完成");
    }

    #endregion

    #region 装备操作

    /// <summary>
    /// 穿戴装备
    /// </summary>
    /// <param name="equipId">装备ID</param>
    /// <returns>是否成功穿戴</returns>
    public bool Equip(string equipId)
    {
        var config = EquipmentConfig.Get(equipId);
        if (config == null)
        {
            Debug.LogWarning($"[EquipmentManager] 装备不存在: {equipId}");
            return false;
        }

        int slotIndex = (int)config.slot;

        // 如果该槽位已有装备，先卸下
        if (equipSlots[slotIndex] != null && !string.IsNullOrEmpty(equipSlots[slotIndex].equipId))
        {
            Unequip(config.slot);
        }

        // 从背包移除装备物品
        if (inventory != null)
        {
            if (!inventory.RemoveItem(equipId, 1))
            {
                Debug.LogWarning($"[EquipmentManager] 背包中没有: {config.displayName}");
                return false;
            }
        }

        // 放入装备槽
        equipSlots[slotIndex] = new Equipment(equipId);

        // 重新计算属性加成
        RecalculateBonuses();

        // 发布装备事件
        EventBus.Publish(ON_EQUIP, equipId);
        EventBus.Publish(ON_INVENTORY_CHANGE);

        DebugLog($"[EquipmentManager] 装备【{config.displayName}】穿戴完成，攻击+{config.attackBonus}，防御+{config.defenseBonus}");
        return true;
    }

    /// <summary>
    /// 卸下装备
    /// </summary>
    /// <param name="slot">装备部位</param>
    /// <returns>是否成功卸下</returns>
    public bool Unequip(EquipmentConfig.EquipSlot slot)
    {
        int slotIndex = (int)slot;

        if (equipSlots[slotIndex] == null || string.IsNullOrEmpty(equipSlots[slotIndex].equipId))
        {
            Debug.LogWarning("[EquipmentManager] 该槽位没有装备");
            return false;
        }

        Equipment oldEquip = equipSlots[slotIndex];
        var config = oldEquip.GetConfig();

        // 放回背包
        if (inventory != null && config != null)
        {
            inventory.AddItem(oldEquip.equipId, 1);
        }

        // 清空槽位
        equipSlots[slotIndex] = new Equipment();

        // 重新计算属性加成
        RecalculateBonuses();

        // 发布卸下事件
        EventBus.Publish(ON_UNEQUIP, oldEquip.equipId);

        DebugLog($"[EquipmentManager] 卸下【{config.displayName}】，属性加成移除");
        return true;
    }

    /// <summary>
    /// 检查指定槽位是否有装备
    /// </summary>
    public bool IsSlotOccupied(EquipmentConfig.EquipSlot slot)
    {
        int index = (int)slot;
        return index >= 0 && index < equipSlots.Length
            && equipSlots[index] != null
            && !string.IsNullOrEmpty(equipSlots[index].equipId);
    }

    /// <summary>
    /// 重新计算所有装备的总属性加成
    /// </summary>
    private void RecalculateBonuses()
    {
        cachedAttackBonus = 0f;
        cachedDefenseBonus = 0f;
        cachedStaminaBonus = 0f;
        cachedSpeedBonus = 0f;
        cachedHealthBonus = 0;

        foreach (var equip in equipSlots)
        {
            if (equip != null && !string.IsNullOrEmpty(equip.equipId))
            {
                cachedAttackBonus += equip.GetAttackBonus();
                cachedDefenseBonus += equip.GetDefenseBonus();
                cachedStaminaBonus += equip.GetStaminaBonus();
                cachedSpeedBonus += equip.GetSpeedBonus();
                cachedHealthBonus += equip.GetHealthBonus();
            }
        }

        DebugLog($"[EquipmentManager] 属性加成更新: 攻击+{cachedAttackBonus}, 防御+{cachedDefenseBonus}, 耐力+{cachedStaminaBonus}");
    }

    #endregion

    #region 存档序列化

    /// <summary>
    /// 序列化装备栏数据（用于存档）
    /// </summary>
    public List<Equipment> SerializeForSave()
    {
        List<Equipment> data = new List<Equipment>();
        foreach (var equip in equipSlots)
        {
            if (equip != null && !string.IsNullOrEmpty(equip.equipId))
                data.Add(new Equipment(equip.equipId, equip.upgradeLevel));
            else
                data.Add(new Equipment());
        }
        return data;
    }

    /// <summary>
    /// 从存档数据加载装备栏
    /// </summary>
    public void LoadFromSave(List<Equipment> savedSlots)
    {
        if (savedSlots == null) return;

        for (int i = 0; i < Mathf.Min(savedSlots.Count, equipSlots.Length); i++)
        {
            equipSlots[i] = savedSlots[i];
        }

        RecalculateBonuses();
        DebugLog("[EquipmentManager] 从存档加载装备栏完成");
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion

    /// <summary>背包变动事件（内部引用）</summary>
    private const string ON_INVENTORY_CHANGE = "ON_INVENTORY_CHANGE";
}
