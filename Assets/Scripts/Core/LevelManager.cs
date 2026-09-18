using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 关卡管理器 - 管理游戏关卡流程
/// 单例模式，控制关卡加载、切换、通关判定
/// 支持多关卡配置，敌人波次管理
/// 通过EventBus发布关卡事件
/// WebGL平台兼容，纯单线程实现
/// </summary>
public class LevelManager : Singleton<LevelManager>
{
    #region 关卡配置

    /// <summary>
    /// 关卡配置数据
    /// </summary>
    [System.Serializable]
    public class LevelConfig
    {
        [Tooltip("关卡ID")]
        public int levelId = 1;

        [Tooltip("关卡名称")]
        public string levelName = "关卡 1";

        [Tooltip("关卡描述")]
        public string description = "";

        [Tooltip("该关卡的敌人波次配置")]
        public List<EnemySpawnManager.WaveConfig> waves = new List<EnemySpawnManager.WaveConfig>();

        [Tooltip("关卡通过后解锁的下一关ID（0表示无下一关）")]
        public int unlockNextLevel = 2;

        [Tooltip("通关条件：是否需要消灭所有敌人")]
        public bool requireAllEnemiesDead = true;

        [Tooltip("通关奖励击杀数")]
        public int bonusKills = 10;

        [Tooltip("Phase11: 是否为Boss关卡")]
        public bool isBossLevel = false;

        [Tooltip("Phase11: Boss名称")]
        public string bossName = "";

        [Tooltip("Phase11: Boss血量倍率")]
        public float bossHealthMultiplier = 1f;
    }

    #endregion

    #region 事件常量

    /// <summary>关卡开始事件</summary>
    public const string ON_LEVEL_START = "ON_LEVEL_START";

    /// <summary>关卡通关事件</summary>
    public const string ON_LEVEL_COMPLETE = "ON_LEVEL_COMPLETE";

    /// <summary>关卡失败事件</summary>
    public const string ON_LEVEL_FAILED = "ON_LEVEL_FAILED";

    /// <summary>关卡切换事件</summary>
    public const string ON_LEVEL_CHANGE = "ON_LEVEL_CHANGE";

    /// <summary>Phase11: Boss生成事件</summary>
    public const string ON_BOSS_SPAWN = "ON_BOSS_SPAWN";

    #endregion

    #region Inspector可配置参数

    [Header("关卡配置")]
    [Tooltip("所有关卡配置列表")]
    [SerializeField] private List<LevelConfig> levelConfigs = new List<LevelConfig>();

    [Tooltip("默认关卡ID")]
    [SerializeField] private int defaultLevelId = 1;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region Phase12: 配置加载

    /// <summary>
    /// Phase12: 从LevelConfigData加载关卡配置
    /// </summary>
    /// <param name="dataList">LevelConfigData列表</param>
    public void LoadFromConfigData(List<LevelConfigData> dataList)
    {
        levelConfigs.Clear();
        foreach (var data in dataList)
        {
            var config = new LevelConfig
            {
                levelId = data.levelId,
                levelName = data.levelName,
                description = data.description,
                unlockNextLevel = data.unlockNextLevel,
                requireAllEnemiesDead = data.requireAllEnemiesDead,
                bonusKills = data.bonusKills,
                isBossLevel = data.isBossLevel,
                bossName = data.bossName,
                bossHealthMultiplier = data.bossHealthMultiplier
            };
            foreach (var waveData in data.waves)
            {
                config.waves.Add(new EnemySpawnManager.WaveConfig
                {
                    waveName = waveData.waveName,
                    enemyCount = waveData.enemyCount,
                    spawnDelay = waveData.spawnDelay,
                    delayAfterWave = waveData.delayAfterWave
                });
            }
            levelConfigs.Add(config);
        }
        DebugLog("[LevelManager] 从LevelConfigData加载了 " + levelConfigs.Count + " 个关卡配置");
    }

    #endregion

    #region 私有变量

    /// <summary>当前关卡配置</summary>
    private LevelConfig currentLevelConfig;

    /// <summary>当前关卡ID</summary>
    private int currentLevelId = 1;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>关卡是否正在进行</summary>
    private bool isLevelActive = false;

    /// <summary>关卡经过时间</summary>
    private float levelTime = 0f;

    #endregion

    #region 公共属性

    /// <summary>当前关卡配置</summary>
    public LevelConfig CurrentLevelConfig => currentLevelConfig;

    /// <summary>当前关卡ID</summary>
    public int CurrentLevelId => currentLevelId;

    /// <summary>关卡是否正在进行</summary>
    public bool IsLevelActive => isLevelActive;

    /// <summary>关卡经过时间</summary>
    public float LevelTime => levelTime;

    /// <summary>总关卡数</summary>
    public int TotalLevels => levelConfigs.Count;

    #endregion

    #region 初始化

    protected override void OnSingletonAwake()
    {
        InitializeManager();
    }

    private void InitializeManager()
    {
        if (isInitialized) return;

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[LevelManager] 关卡管理器初始化完成");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_GAME_START", OnGameStart);
        EventBus.Subscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Subscribe("ON_RETURN_TO_MAINMENU", OnReturnToMainMenu);
        // Phase11: Boss死亡事件
        EventBus.Subscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_GAME_START", OnGameStart);
        EventBus.Unsubscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Unsubscribe("ON_RETURN_TO_MAINMENU", OnReturnToMainMenu);
        EventBus.Unsubscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);
    }

    #endregion

    #region 事件处理

    private void OnGameStart()
    {
        // 从存档加载或使用默认关卡
        int startLevel = defaultLevelId;
        if (SaveManager.HasInstance && SaveManager.Instance.HasSaveData())
        {
            startLevel = SaveManager.Instance.CurrentSave.currentLevel;
        }
        StartLevel(startLevel);
    }

    private void OnEnemyDie()
    {
        if (!isLevelActive) return;

        // 检查通关条件
        CheckLevelComplete();
    }

    private void OnPlayerDie()
    {
        if (!isLevelActive) return;

        // 关卡失败
        LevelFailed();
    }

    private void OnReturnToMainMenu()
    {
        StopLevel();
    }

    /// <summary>Phase11: Boss被击败，关卡通关</summary>
    private void OnBossDefeated(object data)
    {
        if (!isLevelActive) return;
        if (currentLevelConfig != null && currentLevelConfig.isBossLevel)
        {
            Debug.Log("[LevelManager] Boss被击败，关卡通关");
            LevelComplete();
        }
    }

    #endregion

    #region 关卡控制

    /// <summary>
    /// 开始指定关卡
    /// </summary>
    /// <param name="levelId">关卡ID</param>
    public void StartLevel(int levelId)
    {
        // 查找关卡配置
        LevelConfig config = GetLevelConfig(levelId);
        if (config == null)
        {
            Debug.LogWarning($"[LevelManager] 找不到关卡配置: {levelId}");
            return;
        }

        currentLevelId = levelId;
        currentLevelConfig = config;
        isLevelActive = true;
        levelTime = 0f;

        // 更新存档
        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.SetCurrentLevel(levelId);
            SaveManager.Instance.ResetLevelKills();
        }

        // 发布关卡开始事件
        EventBus.Publish(ON_LEVEL_START);

        Debug.Log($"[LevelManager] 开始关卡 {levelId}: {config.levelName}");

        // 开始生成敌人
        StartCoroutine(StartEnemyWaves());
    }

    /// <summary>
    /// 停止当前关卡
    /// </summary>
    public void StopLevel()
    {
        isLevelActive = false;
        StopAllCoroutines();
        DebugLog("[LevelManager] 关卡已停止");
    }

    /// <summary>
    /// 关卡通关
    /// </summary>
    public void LevelComplete()
    {
        if (!isLevelActive) return;

        isLevelActive = false;

        // 解锁下一关
        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.UnlockNextLevel();
            SaveManager.Instance.AddKillCount(currentLevelConfig.bonusKills);
            SaveManager.Instance.SaveGame();
        }

        // 发布关卡通关事件
        EventBus.Publish(ON_LEVEL_COMPLETE);

        Debug.Log($"[LevelManager] 关卡 {currentLevelId} 通关！");
    }

    /// <summary>
    /// 关卡失败
    /// </summary>
    public void LevelFailed()
    {
        if (!isLevelActive) return;

        isLevelActive = false;

        // 发布关卡失败事件
        EventBus.Publish(ON_LEVEL_FAILED);

        Debug.Log($"[LevelManager] 关卡 {currentLevelId} 失败");
    }

    /// <summary>
    /// 切换到下一关
    /// </summary>
    public void NextLevel()
    {
        int nextLevelId = currentLevelId + 1;
        if (nextLevelId <= levelConfigs.Count)
        {
            StartLevel(nextLevelId);
        }
        else
        {
            DebugLog("[LevelManager] 已是最后一关");
        }
    }

    /// <summary>
    /// 重新开始当前关卡
    /// </summary>
    public void RestartLevel()
    {
        StartLevel(currentLevelId);
    }

    #endregion

    #region 关卡查询

    /// <summary>
    /// 获取关卡配置
    /// </summary>
    /// <param name="levelId">关卡ID</param>
    /// <returns>关卡配置</returns>
    public LevelConfig GetLevelConfig(int levelId)
    {
        foreach (var config in levelConfigs)
        {
            if (config.levelId == levelId)
                return config;
        }
        return null;
    }

    /// <summary>
    /// 获取所有关卡配置
    /// </summary>
    /// <returns>关卡配置列表</returns>
    public List<LevelConfig> GetAllLevelConfigs()
    {
        return levelConfigs;
    }

    /// <summary>
    /// 检查关卡是否已解锁
    /// </summary>
    /// <param name="levelId">关卡ID</param>
    /// <returns>是否已解锁</returns>
    public bool IsLevelUnlocked(int levelId)
    {
        if (!SaveManager.HasInstance) return levelId <= 1;
        return levelId <= SaveManager.Instance.GetUnlockedLevels();
    }

    #endregion

    #region 敌人波次生成

    private IEnumerator StartEnemyWaves()
    {
        if (currentLevelConfig == null || EnemySpawnManager.HasInstance == false)
        {
            yield break;
        }

        yield return null;

        for (int i = 0; i < currentLevelConfig.waves.Count; i++)
        {
            if (!isLevelActive) yield break;

            var waveConfig = currentLevelConfig.waves[i];
            DebugLog($"[LevelManager] 开始第 {i + 1} 波: {waveConfig.waveName}");

            yield return new WaitForSeconds(waveConfig.spawnDelay);

            for (int j = 0; j < waveConfig.enemyCount; j++)
            {
                if (!isLevelActive) yield break;

                EnemySpawnManager.Instance.SpawnEnemy();
                yield return new WaitForSeconds(waveConfig.spawnDelay);
            }

            yield return new WaitForSeconds(waveConfig.delayAfterWave);
        }

        DebugLog("[LevelManager] 所有波次生成完毕");

        // Phase11: Boss关卡，小怪清完后触发Boss生成
        if (currentLevelConfig.isBossLevel)
        {
            Debug.Log("[LevelManager] 小怪已清除，准备召唤Boss");
            // 等待场上小怪全部死亡
            yield return new WaitUntil(() => !EnemySpawnManager.HasInstance || !EnemySpawnManager.Instance.HasAliveEnemies);

            if (!isLevelActive) yield break;

            // 发布Boss生成事件（SceneAutoBuilder监听此事件创建Boss）
            EventBus.Publish(ON_BOSS_SPAWN);
            Debug.Log("[LevelManager] Boss生成事件已发布");
        }
    }

    #endregion

    #region 通关检查

    private void CheckLevelComplete()
    {
        if (!isLevelActive || currentLevelConfig == null) return;

        if (currentLevelConfig.requireAllEnemiesDead)
        {
            // 检查是否所有敌人都已消灭
            bool allDead = !EnemySpawnManager.HasInstance || !EnemySpawnManager.Instance.HasAliveEnemies;
            bool allSpawned = !EnemySpawnManager.HasInstance || !EnemySpawnManager.Instance.IsSpawning;

            if (allDead && allSpawned)
            {
                LevelComplete();
            }
        }
    }

    #endregion

    #region 每帧更新

    private void Update()
    {
        if (isLevelActive)
        {
            levelTime += Time.deltaTime;

            // 更新存档时间（每10秒保存一次）
            if (SaveManager.HasInstance && Mathf.FloorToInt(levelTime) % 10 == 0)
            {
                SaveManager.Instance.SetPlayTime(levelTime);
            }
        }
    }

    #endregion

    #region 生命周期清理

    protected override void OnDestroy()
    {
        UnsubscribeEvents();
        StopAllCoroutines();
        base.OnDestroy();
    }

    #endregion

    #region 工具方法

    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
