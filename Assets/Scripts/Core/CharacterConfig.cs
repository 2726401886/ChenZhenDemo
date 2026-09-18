using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 角色配置基类 - 存储角色血量、攻击力、移动速度、硬直时长等
/// 派生：PlayerConfig / EnemyConfig / BossConfig
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class CharacterConfig
{
    [Header("基础属性")]
    [Tooltip("角色唯一标识")]
    public string characterId = "";

    [Tooltip("角色显示名称")]
    public string displayName = "";

    [Tooltip("最大生命值")]
    public int maxHealth = 100;

    [Tooltip("基础攻击力")]
    public float baseAttack = 10f;

    [Tooltip("移动速度")]
    public float moveSpeed = 5f;

    [Header("受击设置")]
    [Tooltip("受击硬直时间（秒）")]
    public float hitstunDuration = 0.3f;

    [Tooltip("硬直恢复比例")]
    public float hitstunRecoveryRatio = 0.8f;

    [Tooltip("无敌帧时间（秒）")]
    public float invincibilityDuration = 0.5f;

    /// <summary>单例引用（SceneAutoBuilder设置）</summary>
    private static CharacterConfig _playerInstance;

    /// <summary>获取玩家配置实例</summary>
    public static CharacterConfig PlayerInstance
    {
        get
        {
            if (_playerInstance == null)
            {
                _playerInstance = new CharacterConfig
                {
                    characterId = "Player",
                    displayName = "玩家",
                    maxHealth = 100,
                    baseAttack = 10f,
                    moveSpeed = 5f
                };
            }
            return _playerInstance;
        }
    }

    /// <summary>设置玩家配置实例</summary>
    public static void SetPlayerInstance(CharacterConfig config)
    {
        _playerInstance = config;
    }
}

/// <summary>
/// 玩家角色配置
/// </summary>
[System.Serializable]
public class PlayerConfig : CharacterConfig
{
    [Header("玩家专属")]
    [Tooltip("奔跑速度倍率")]
    public float runMultiplier = 1.5f;

    [Tooltip("跳跃高度")]
    public float jumpHeight = 2f;

    [Tooltip("重力加速度")]
    public float gravity = -20f;

    /// <summary>单例引用</summary>
    private static PlayerConfig _instance;

    /// <summary>获取玩家配置实例</summary>
    public static PlayerConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PlayerConfig();
            }
            return _instance;
        }
    }

    /// <summary>设置玩家配置实例</summary>
    public static void SetInstance(PlayerConfig config)
    {
        _instance = config;
        CharacterConfig.SetPlayerInstance(config);
    }
}

/// <summary>
/// 普通敌人配置
/// </summary>
[System.Serializable]
public class EnemyConfig : CharacterConfig
{
    [Header("敌人AI设置")]
    [Tooltip("巡逻半径")]
    public float patrolRadius = 8f;

    [Tooltip("巡逻移动速度")]
    public float patrolSpeed = 2f;

    [Tooltip("到达巡逻点后的等待时间")]
    public float patrolWaitTime = 1.5f;

    [Tooltip("视野检测距离")]
    public float detectionRange = 10f;

    [Tooltip("视野检测角度")]
    public float detectionAngle = 120f;

    [Tooltip("目标丢失距离")]
    public float loseTargetDistance = 15f;

    [Tooltip("追击移动速度")]
    public float chaseSpeed = 4f;

    [Tooltip("攻击触发距离")]
    public float attackRange = 1.5f;

    [Tooltip("攻击冷却时间（秒）")]
    public float attackCooldown = 1f;

    [Tooltip("攻击伤害值")]
    public float attackDamage = 10f;

    /// <summary>敌人配置字典</summary>
    private static Dictionary<string, EnemyConfig> _configs = new Dictionary<string, EnemyConfig>();

    /// <summary>注册敌人配置</summary>
    public static void Register(string id, EnemyConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取敌人配置</summary>
    public static EnemyConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[EnemyConfig] 未找到配置: {id}，使用默认值");
        return new EnemyConfig { characterId = id };
    }

    /// <summary>获取所有已注册的敌人配置</summary>
    public static Dictionary<string, EnemyConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}

/// <summary>
/// Boss角色配置
/// </summary>
[System.Serializable]
public class BossConfig : CharacterConfig
{
    [Header("Boss专属")]
    [Tooltip("Boss名称")]
    public string bossTitle = "暗影领主";

    [Tooltip("P2触发血量百分比")]
    [Range(0f, 1f)]
    public float phase2Threshold = 0.5f;

    [Header("P1近战")]
    [Tooltip("近战攻击伤害")]
    public float meleeDamage = 15f;

    [Tooltip("近战攻击范围")]
    public float meleeRange = 2.5f;

    [Tooltip("近战攻击冷却")]
    public float meleeCooldown = 1.5f;

    [Header("P1远程")]
    [Tooltip("远程攻击伤害")]
    public float rangedDamage = 10f;

    [Tooltip("远程攻击范围")]
    public float rangedRange = 8f;

    [Tooltip("远程攻击冷却")]
    public float rangedCooldown = 3f;

    [Tooltip("弹幕数量")]
    public int bulletCount = 5;

    [Header("P2狂暴")]
    [Tooltip("P2攻击力倍率")]
    public float p2DamageMultiplier = 1.5f;

    [Tooltip("P2移动速度倍率")]
    public float p2SpeedMultiplier = 1.3f;

    [Tooltip("AOE攻击伤害")]
    public float aoeDamage = 25f;

    [Tooltip("AOE攻击范围")]
    public float aoeRange = 6f;

    [Tooltip("AOE攻击冷却")]
    public float aoeCooldown = 8f;

    /// <summary>单例引用</summary>
    private static BossConfig _instance;

    /// <summary>获取Boss配置实例</summary>
    public static BossConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new BossConfig();
            }
            return _instance;
        }
    }

    /// <summary>设置Boss配置实例</summary>
    public static void SetInstance(BossConfig config)
    {
        _instance = config;
    }
}

/// <summary>
/// 精英近战敌人配置
/// </summary>
[System.Serializable]
public class EliteEnemyConfig : EnemyConfig
{
    [Header("精英专属")]
    [Tooltip("冲刺伤害")]
    public float chargeDamage = 25f;

    [Tooltip("冲刺速度")]
    public float chargeSpeed = 15f;

    [Tooltip("冲刺冷却")]
    public float chargeCooldown = 5f;

    [Tooltip("冲刺蓄力时间")]
    public float chargeWindup = 0.5f;

    [Tooltip("硬直抵抗倍率（越高越难被打断）")]
    public float hitstunResistance = 0.5f;

    /// <summary>单例引用</summary>
    private static EliteEnemyConfig _instance;

    /// <summary>获取实例</summary>
    public static EliteEnemyConfig Instance
    {
        get
        {
            if (_instance == null) _instance = new EliteEnemyConfig();
            return _instance;
        }
    }

    /// <summary>设置实例</summary>
    public static void SetInstance(EliteEnemyConfig config)
    {
        _instance = config;
        EnemyConfig.Register(config.characterId, config);
    }
}

/// <summary>
/// 自爆怪敌人配置
/// </summary>
[System.Serializable]
public class ExploderEnemyConfig : EnemyConfig
{
    [Header("自爆专属")]
    [Tooltip("自爆伤害")]
    public float explodeDamage = 40f;

    [Tooltip("自爆范围半径")]
    public float explodeRadius = 5f;

    [Tooltip("自爆预警时间")]
    public float warningDuration = 1.5f;

    [Tooltip("触发自爆的距离")]
    public float triggerDistance = 2f;

    [Tooltip("是否免疫硬直")]
    public bool hitstunImmune = true;

    /// <summary>单例引用</summary>
    private static ExploderEnemyConfig _instance;

    /// <summary>获取实例</summary>
    public static ExploderEnemyConfig Instance
    {
        get
        {
            if (_instance == null) _instance = new ExploderEnemyConfig();
            return _instance;
        }
    }

    /// <summary>设置实例</summary>
    public static void SetInstance(ExploderEnemyConfig config)
    {
        _instance = config;
        EnemyConfig.Register(config.characterId, config);
    }
}

/// <summary>
/// 远程法师敌人配置
/// </summary>
[System.Serializable]
public class MageEnemyConfig : EnemyConfig
{
    [Header("法师专属")]
    [Tooltip("魔法弹伤害")]
    public float magicDamage = 12f;

    [Tooltip("魔法弹速度")]
    public float magicSpeed = 10f;

    [Tooltip("最佳攻击距离")]
    public float optimalDistance = 8f;

    [Tooltip("后撤触发距离")]
    public float retreatDistance = 4f;

    [Tooltip("后撤移动速度")]
    public float retreatSpeed = 5f;

    [Tooltip("低血量攻击冷却倍率")]
    public float lowHealthCooldownMultiplier = 0.6f;

    /// <summary>单例引用</summary>
    private static MageEnemyConfig _instance;

    /// <summary>获取实例</summary>
    public static MageEnemyConfig Instance
    {
        get
        {
            if (_instance == null) _instance = new MageEnemyConfig();
            return _instance;
        }
    }

    /// <summary>设置实例</summary>
    public static void SetInstance(MageEnemyConfig config)
    {
        _instance = config;
        EnemyConfig.Register(config.characterId, config);
    }
}
