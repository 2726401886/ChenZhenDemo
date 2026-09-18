using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装备强化配置表 - 存储每个强化等级的消耗、成功率、属性倍率
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class EnhanceConfig
{
    [Header("强化等级设置")]
    [Tooltip("当前强化等级（源等级）")]
    public int currentLevel = 0;

    [Tooltip("目标强化等级（源等级+1）")]
    public int targetLevel = 1;

    [Header("消耗设置")]
    [Tooltip("消耗金币数量")]
    public int costCoins = 100;

    [Tooltip("消耗材料物品ID")]
    public string costMaterialId = "EnhanceStone";

    [Tooltip("消耗材料数量")]
    public int costMaterialCount = 1;

    [Header("成功率设置")]
    [Tooltip("强化成功率（0-1）")]
    [Range(0f, 1f)]
    public float successRate = 0.8f;

    [Header("属性设置")]
    [Tooltip("属性放大倍率（基础属性 × 此倍率）")]
    public float statMultiplier = 1.1f;

    /// <summary>强化配置字典（key: 当前等级）</summary>
    private static Dictionary<int, EnhanceConfig> _configs = new Dictionary<int, EnhanceConfig>();

    /// <summary>最高可强化等级</summary>
    private static int _maxLevel = 3;

    /// <summary>注册强化配置</summary>
    public static void Register(int currentLevel, EnhanceConfig config)
    {
        if (!_configs.ContainsKey(currentLevel))
            _configs.Add(currentLevel, config);
        else
            _configs[currentLevel] = config;
    }

    /// <summary>获取指定等级的强化配置</summary>
    public static EnhanceConfig Get(int currentLevel)
    {
        if (_configs.TryGetValue(currentLevel, out var config))
            return config;
        Debug.LogWarning($"[EnhanceConfig] 未找到强化配置: Lv{currentLevel}");
        return null;
    }

    /// <summary>获取最高强化等级</summary>
    public static int GetMaxLevel()
    {
        return _maxLevel;
    }

    /// <summary>设置最高强化等级</summary>
    public static void SetMaxLevel(int maxLevel)
    {
        _maxLevel = maxLevel;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
