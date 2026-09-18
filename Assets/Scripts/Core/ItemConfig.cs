using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 物品配置表 - 存储所有物品的属性、堆叠上限、使用效果
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class ItemConfig
{
    [Header("基础设置")]
    [Tooltip("物品唯一标识ID")]
    public string itemId = "";

    [Tooltip("物品显示名称")]
    public string displayName = "未命名物品";

    [Tooltip("物品描述")]
    public string description = "";

    [Tooltip("物品图标（UI用）")]
    public Sprite icon;

    [Header("类型设置")]
    [Tooltip("物品类型：Consumable消耗品 / Material材料")]
    public ItemType itemType = ItemType.Consumable;

    [Tooltip("最大堆叠数量")]
    public int maxStack = 99;

    [Header("使用效果")]
    [Tooltip("恢复血量")]
    public int healAmount = 0;

    [Tooltip("恢复耐力")]
    public float staminaRestore = 0f;

    [Tooltip("攻击力加成百分比（0.5 = +50%）")]
    public float attackBonus = 0f;

    [Tooltip("增益持续时间（秒）")]
    public float buffDuration = 0f;

    [Header("掉落设置")]
    [Tooltip("击杀后掉落概率（0-1）")]
    [Range(0f, 1f)]
    public float dropChance = 0.3f;

    /// <summary>物品类型枚举</summary>
    public enum ItemType
    {
        Consumable,  // 消耗品
        Material     // 材料
    }

    /// <summary>物品配置字典</summary>
    private static Dictionary<string, ItemConfig> _configs = new Dictionary<string, ItemConfig>();

    /// <summary>注册物品配置</summary>
    public static void Register(string id, ItemConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取物品配置</summary>
    public static ItemConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[ItemConfig] 未找到物品: {id}");
        return null;
    }

    /// <summary>获取所有已注册的物品配置</summary>
    public static Dictionary<string, ItemConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
