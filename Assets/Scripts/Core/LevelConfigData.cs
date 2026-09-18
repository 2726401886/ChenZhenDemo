using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡配置数据类 - 每一关独立配置波次、敌人类型、奖励、解锁条件
/// 替代原LevelManager.LevelConfig，实现策划数值与逻辑代码分离
/// SceneAutoBuilder初始化时填充4个关卡的完整配置
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class LevelConfigData
{
    [Tooltip("关卡ID")]
    public int levelId = 1;

    [Tooltip("关卡名称")]
    public string levelName = "关卡 1";

    [Tooltip("关卡描述")]
    public string description = "";

    [Tooltip("该关卡的敌人波次配置")]
    public List<WaveData> waves = new List<WaveData>();

    [Tooltip("通关后解锁的下一关ID（0表示无下一关）")]
    public int unlockNextLevel = 2;

    [Tooltip("通关条件：是否需要消灭所有敌人")]
    public bool requireAllEnemiesDead = true;

    [Tooltip("通关奖励击杀数")]
    public int bonusKills = 10;

    [Tooltip("是否为Boss关卡")]
    public bool isBossLevel = false;

    [Tooltip("Boss名称")]
    public string bossName = "";

    [Tooltip("Boss血量倍率")]
    public float bossHealthMultiplier = 1f;

    /// <summary>关卡配置字典</summary>
    private static Dictionary<int, LevelConfigData> _configs = new Dictionary<int, LevelConfigData>();

    /// <summary>注册关卡配置</summary>
    public static void Register(int levelId, LevelConfigData config)
    {
        if (!_configs.ContainsKey(levelId))
            _configs.Add(levelId, config);
        else
            _configs[levelId] = config;
    }

    /// <summary>获取关卡配置</summary>
    public static LevelConfigData Get(int levelId)
    {
        if (_configs.TryGetValue(levelId, out var config))
            return config;
        Debug.LogWarning($"[LevelConfigData] 未找到关卡配置: {levelId}");
        return null;
    }

    /// <summary>获取所有已注册的关卡配置</summary>
    public static Dictionary<int, LevelConfigData> GetAll()
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
/// 波次数据 - 存储每波敌人的类型和数量
/// </summary>
[System.Serializable]
public class WaveData
{
    [Tooltip("波次名称")]
    public string waveName = "第1波";

    [Tooltip("敌人类型ID（对应EnemyConfig.characterId）")]
    public string enemyType = "MeleeEnemy";

    [Tooltip("敌人数量")]
    public int enemyCount = 3;

    [Tooltip("生成间隔（秒）")]
    public float spawnDelay = 1f;

    [Tooltip("波次间隔（秒）")]
    public float delayAfterWave = 2f;
}
