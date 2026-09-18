using UnityEngine;

/// <summary>
/// 全局基础配置 - 存储玩家基础属性、耐力设置、全局伤害系数
/// 纯数据类，SceneAutoBuilder初始化时填充，运行时只读
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class GameConfig
{
    [Header("玩家基础属性")]
    [Tooltip("玩家基础血量")]
    public int playerBaseHealth = 100;

    [Tooltip("玩家基础攻击力")]
    public float playerBaseAttack = 10f;

    [Tooltip("玩家基础移动速度")]
    public float playerBaseMoveSpeed = 5f;

    [Tooltip("玩家奔跑速度倍率")]
    public float playerRunMultiplier = 1.5f;

    [Tooltip("玩家跳跃高度")]
    public float playerJumpHeight = 2f;

    [Tooltip("玩家重力加速度")]
    public float playerGravity = -20f;

    [Header("耐力设置")]
    [Tooltip("最大耐力值")]
    public float maxStamina = 100f;

    [Tooltip("耐力恢复速度（每秒）")]
    public float staminaRegenRate = 15f;

    [Tooltip("耐力恢复延迟（秒）")]
    public float staminaRegenDelay = 1f;

    [Tooltip("普攻耐力消耗")]
    public float attackStaminaCost = 10f;

    [Header("攻击设置")]
    [Tooltip("普攻冷却时间（秒）")]
    public float attackCooldown = 0.5f;

    [Tooltip("普攻持续时间（秒）")]
    public float attackDuration = 0.4f;

    [Tooltip("角色旋转灵敏度")]
    public float rotateSensitivity = 10f;

    [Header("全局系数")]
    [Tooltip("全局伤害系数（影响所有伤害计算）")]
    public float globalDamageMultiplier = 1f;

    [Tooltip("全局受伤系数")]
    public float globalDamageReceiveMultiplier = 1f;

    [Header("受击硬直")]
    [Tooltip("受击硬直时间（秒）")]
    public float hitstunDuration = 0.3f;

    [Tooltip("硬直恢复比例")]
    public float hitstunRecoveryRatio = 0.8f;

    [Header("无敌帧")]
    [Tooltip("受击后无敌帧时间（秒）")]
    public float invincibilityDuration = 0.5f;

    /// <summary>单例引用（SceneAutoBuilder设置）</summary>
    private static GameConfig _instance;

    /// <summary>获取全局配置实例</summary>
    public static GameConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameConfig();
                Debug.Log("[GameConfig] 使用默认配置");
            }
            return _instance;
        }
    }

    /// <summary>设置全局配置实例</summary>
    public static void SetInstance(GameConfig config)
    {
        _instance = config;
    }

    /// <summary>重置为默认配置</summary>
    public static void ResetToDefault()
    {
        _instance = new GameConfig();
        Debug.Log("[GameConfig] 重置为默认配置");
    }
}
