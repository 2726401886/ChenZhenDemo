using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装备配置表 - 存储所有装备的属性、部位、加成、稀有度
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class EquipmentConfig
{
    [Header("基础设置")]
    [Tooltip("装备唯一标识ID")]
    public string equipId = "";

    [Tooltip("装备显示名称")]
    public string displayName = "未命名装备";

    [Tooltip("装备描述")]
    public string description = "";

    [Tooltip("装备图标（UI用）")]
    public Sprite icon;

    [Header("部位设置")]
    [Tooltip("装备部位：Weapon/Helmet/Chest/Boots")]
    public EquipSlot slot = EquipSlot.Weapon;

    [Header("属性加成")]
    [Tooltip("攻击力加成")]
    public float attackBonus = 0f;

    [Tooltip("防御力加成")]
    public float defenseBonus = 0f;

    [Tooltip("耐力上限加成")]
    public float staminaBonus = 0f;

    [Tooltip("移动速度加成")]
    public float speedBonus = 0f;

    [Tooltip("血量上限加成")]
    public int healthBonus = 0;

    [Header("稀有度")]
    [Tooltip("装备稀有度")]
    public Rarity rarity = Rarity.Common;

    /// <summary>装备部位枚举</summary>
    public enum EquipSlot
    {
        Weapon,   // 武器
        Helmet,   // 头盔
        Chest,    // 胸甲
        Boots     // 鞋子
    }

    /// <summary>稀有度枚举</summary>
    public enum Rarity
    {
        Common,    // 普通（白色）
        Uncommon,  // 优秀（绿色）
        Rare,      // 稀有（蓝色）
        Epic       // 史诗（紫色）
    }

    /// <summary>装备配置字典</summary>
    private static Dictionary<string, EquipmentConfig> _configs = new Dictionary<string, EquipmentConfig>();

    /// <summary>注册装备配置</summary>
    public static void Register(string id, EquipmentConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取装备配置</summary>
    public static EquipmentConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[EquipmentConfig] 未找到装备: {id}");
        return null;
    }

    /// <summary>获取所有已注册的装备配置</summary>
    public static Dictionary<string, EquipmentConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }

    /// <summary>根据稀有度获取颜色</summary>
    public static Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return Color.white;
            case Rarity.Uncommon: return Color.green;
            case Rarity.Rare: return Color.cyan;
            case Rarity.Epic: return new Color(0.7f, 0.3f, 1f);
            default: return Color.white;
        }
    }
}
