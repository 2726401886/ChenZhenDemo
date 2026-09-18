using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Buff配置表 - 存储所有Buff的数值参数
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class BuffConfig
{
    [Header("基础设置")]
    [Tooltip("Buff唯一标识名称")]
    public string buffId = "";

    [Tooltip("Buff显示名称")]
    public string displayName = "未命名Buff";

    [Tooltip("Buff描述")]
    public string description = "";

    [Tooltip("是否为增益Buff")]
    public bool isPositive = true;

    [Header("持续与叠加")]
    [Tooltip("Buff持续时间（秒），0表示永久")]
    public float duration = 10f;

    [Tooltip("最大叠加层数")]
    public int maxStacks = 1;

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

    /// <summary>Buff配置字典</summary>
    private static Dictionary<string, BuffConfig> _configs = new Dictionary<string, BuffConfig>();

    /// <summary>注册Buff配置</summary>
    public static void Register(string id, BuffConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取Buff配置</summary>
    public static BuffConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[BuffConfig] 未找到配置: {id}，使用默认值");
        return new BuffConfig { buffId = id };
    }

    /// <summary>获取所有已注册的Buff配置</summary>
    public static Dictionary<string, BuffConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
