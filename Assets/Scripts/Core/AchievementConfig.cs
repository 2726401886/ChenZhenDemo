using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 成就配置表 - 存储所有成就的触发条件、奖励、一次性标记
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class AchievementConfig
{
    [Header("基础设置")]
    [Tooltip("成就唯一标识ID")]
    public string achievementId = "";

    [Tooltip("成就显示名称")]
    public string displayName = "未命名成就";

    [Tooltip("成就描述")]
    public string description = "";

    [Tooltip("成就图标")]
    public Sprite icon;

    [Header("触发条件")]
    [Tooltip("条件类型：KillEnemy击杀敌人 / KillBoss击杀Boss / EnhanceEquipment首次强化 / PickupItems拾取物品 / EarnCoins累计金币 / ClearLevel通关关卡")]
    public ConditionType conditionType = ConditionType.KillEnemy;

    [Tooltip("触发条件目标值")]
    public int targetValue = 10;

    [Header("奖励设置")]
    [Tooltip("奖励类型：Coin金币 / Item物品")]
    public RewardType rewardType = RewardType.Coin;

    [Tooltip("奖励数值（金币数量或物品数量）")]
    public int rewardValue = 200;

    [Tooltip("奖励物品ID（rewardType=Item时使用）")]
    public string rewardItemId = "";

    [Header("解锁设置")]
    [Tooltip("是否为一次性解锁（解锁后不再触发）")]
    public bool oneTimeUnlock = true;

    /// <summary>条件类型枚举</summary>
    public enum ConditionType
    {
        KillEnemy,        // 击杀敌人数量
        KillBoss,         // 击杀Boss次数
        EnhanceEquipment, // 强化装备次数
        PickupItems,      // 拾取物品数量
        EarnCoins,        // 累计获得金币
        ClearLevel        // 通关关卡数量
    }

    /// <summary>奖励类型枚举</summary>
    public enum RewardType
    {
        Coin,  // 金币
        Item   // 物品
    }

    /// <summary>成就配置字典</summary>
    private static Dictionary<string, AchievementConfig> _configs = new Dictionary<string, AchievementConfig>();

    /// <summary>注册成就配置</summary>
    public static void Register(string id, AchievementConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取成就配置</summary>
    public static AchievementConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[AchievementConfig] 未找到成就配置: {id}");
        return null;
    }

    /// <summary>获取所有已注册的成就配置</summary>
    public static Dictionary<string, AchievementConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
