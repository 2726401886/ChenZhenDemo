using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能配置表 - 存储所有技能的数值参数
/// 纯数据类，SceneAutoBuilder初始化时填充5个技能的完整配置
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class SkillConfig
{
    [Header("基础设置")]
    [Tooltip("技能唯一标识")]
    public string skillId = "";

    [Tooltip("技能显示名称")]
    public string displayName = "未命名技能";

    [Tooltip("技能描述")]
    public string description = "";

    [Tooltip("绑定按键")]
    public KeyCode keyBind = KeyCode.None;

    [Header("冷却与消耗")]
    [Tooltip("技能冷却时间（秒）")]
    public float cooldown = 5f;

    [Tooltip("耐力消耗值")]
    public float staminaCost = 20f;

    [Header("持续时间")]
    [Tooltip("技能持续时间（秒），0表示瞬发")]
    public float duration = 0f;

    [Header("伤害设置")]
    [Tooltip("技能伤害倍率（基于攻击力）")]
    public float damageMultiplier = 1.5f;

    [Tooltip("技能范围（AOE用）")]
    public float skillRange = 3f;

    [Header("冲刺设置")]
    [Tooltip("冲刺距离")]
    public float dashDistance = 8f;

    [Tooltip("冲刺速度")]
    public float dashSpeed = 25f;

    [Header("远程设置")]
    [Tooltip("投射物速度")]
    public float projectileSpeed = 18f;

    [Header("治疗设置")]
    [Tooltip("治疗量")]
    public float healAmount = 35f;

    [Header("Buff设置")]
    [Tooltip("Buff名称")]
    public string buffName = "";

    [Tooltip("Buff持续时间")]
    public float buffDuration = 8f;

    [Tooltip("攻击力加成百分比")]
    public float buffAttackBonus = 0.5f;

    /// <summary>技能配置字典</summary>
    private static Dictionary<string, SkillConfig> _configs = new Dictionary<string, SkillConfig>();

    /// <summary>注册技能配置</summary>
    public static void Register(string id, SkillConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取技能配置</summary>
    public static SkillConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[SkillConfig] 未找到配置: {id}，使用默认值");
        return new SkillConfig { skillId = id };
    }

    /// <summary>获取所有已注册的技能配置</summary>
    public static Dictionary<string, SkillConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
