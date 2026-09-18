using UnityEngine;

/// <summary>
/// 装备实例数据 - 记录装备ID和强化等级
/// 可序列化，用于装备栏存储和存档
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class Equipment
{
    [Tooltip("装备配置ID")]
    public string equipId = "";

    [Tooltip("装备强化等级")]
    public int upgradeLevel = 0;

    /// <summary>无参构造（JsonUtility需要）</summary>
    public Equipment() { }

    /// <summary>带参构造</summary>
    /// <param name="id">装备ID</param>
    /// <param name="level">初始强化等级</param>
    public Equipment(string id, int level = 0)
    {
        equipId = id;
        upgradeLevel = level;
    }

    /// <summary>获取装备配置</summary>
    public EquipmentConfig GetConfig()
    {
        return EquipmentConfig.Get(equipId);
    }

    /// <summary>获取显示名称</summary>
    public string DisplayName => GetConfig() != null ? GetConfig().displayName : equipId;

    /// <summary>获取装备图标</summary>
    public Sprite Icon => GetConfig() != null ? GetConfig().icon : null;

    /// <summary>获取装备部位</summary>
    public EquipmentConfig.EquipSlot Slot => GetConfig() != null ? GetConfig().slot : EquipmentConfig.EquipSlot.Weapon;

    /// <summary>获取稀有度</summary>
    public EquipmentConfig.Rarity Rarity => GetConfig() != null ? GetConfig().rarity : EquipmentConfig.Rarity.Common;

    /// <summary>获取攻击力加成（含强化加成）</summary>
    public float GetAttackBonus()
    {
        var cfg = GetConfig();
        if (cfg == null) return 0f;
        return cfg.attackBonus * (1f + upgradeLevel * 0.1f);
    }

    /// <summary>获取防御力加成（含强化加成）</summary>
    public float GetDefenseBonus()
    {
        var cfg = GetConfig();
        if (cfg == null) return 0f;
        return cfg.defenseBonus * (1f + upgradeLevel * 0.1f);
    }

    /// <summary>获取耐力加成</summary>
    public float GetStaminaBonus()
    {
        var cfg = GetConfig();
        if (cfg == null) return 0f;
        return cfg.staminaBonus;
    }

    /// <summary>获取速度加成</summary>
    public float GetSpeedBonus()
    {
        var cfg = GetConfig();
        if (cfg == null) return 0f;
        return cfg.speedBonus;
    }

    /// <summary>获取血量加成</summary>
    public int GetHealthBonus()
    {
        var cfg = GetConfig();
        if (cfg == null) return 0;
        return cfg.healthBonus;
    }
}
