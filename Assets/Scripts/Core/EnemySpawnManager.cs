using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 敌人生成管理器
/// 单例模式，管理游戏中所有敌人的生成与重生
/// 支持多波次生成、重生点、场上敌人数量限制
/// WebGL平台兼容
/// </summary>
public class EnemySpawnManager : Singleton<EnemySpawnManager>
{
    #region 事件定义

    /// <summary>
    /// 生成管理器事件常量
    /// </summary>
    public static class SpawnEvents
    {
        /// <summary>新一波敌人开始生成</summary>
        public const string ON_WAVE_START = "Spawn_WaveStart";
        /// <summary>当前波次敌人全部消灭</summary>
        public const string ON_WAVE_CLEAR = "Spawn_WaveClear";
        /// <summary>敌人生成</summary>
        public const string ON_ENEMY_SPAWNED = "Spawn_EnemySpawned";
        /// <summary>敌人被消灭</summary>
        public const string ON_ENEMY_KILLED = "Spawn_EnemyKilled";
        /// <summary>所有敌人消灭（关卡胜利）</summary>
        public const string ON_ALL_ENEMIES_CLEARED = "Spawn_AllEnemiesCleared";
    }

    #endregion

    #region 波次配置

    /// <summary>
    /// 波次配置
    /// </summary>
    [System.Serializable]
    public class WaveConfig
    {
        [Tooltip("波次名称")]
        public string waveName = "第1波";

        [Tooltip("该波次敌人数量")]
        public int enemyCount = 5;

        [Tooltip("敌人生成延迟（秒）")]
        public float spawnDelay = 0.5f;

        [Tooltip("该波次使用的敌人预制体（可为空，使用默认）")]
        public GameObject enemyPrefabOverride;

        [Tooltip("是否使用重生点列表（false则使用全局重生点）")]
        public bool useCustomSpawnPoints = false;

        [Tooltip("自定义重生点列表")]
        public Transform[] customSpawnPoints;

        [Tooltip("波次完成后的等待时间（秒）")]
        public float delayAfterWave = 3f;
    }

    #endregion

    #region Inspector设置

    [Header("敌人设置")]
    [Tooltip("默认敌人预制体")]
    [SerializeField] private GameObject defaultEnemyPrefab;

    [Tooltip("目标玩家Transform")]
    [SerializeField] private Transform targetPlayer;

    [Tooltip("是否自动查找Player")]
    [SerializeField] private bool autoFindPlayer = true;

    [Header("生成点设置")]
    [Tooltip("全局生成点列表")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("是否在随机生成点生成")]
    [SerializeField] private bool randomSpawnPoint = true;

    [Header("波次设置")]
    [Tooltip("波次配置列表")]
    [SerializeField] private List<WaveConfig> waves = new List<WaveConfig>();

    [Tooltip("当前波次索引")]
    [SerializeField] private int currentWaveIndex = 0;

    [Tooltip("波次之间是否自动开始下一波")]
    [SerializeField] private bool autoStartNextWave = true;

    [Header("生成控制")]
    [Tooltip("场上最大敌人数量")]
    [SerializeField] private int maxAliveEnemies = 10;

    [Tooltip("重生延迟（秒）")]
    [SerializeField] private float respawnDelay = 2f;

    [Tooltip("是否启用敌人生成")]
    [SerializeField] private bool spawningEnabled = true;

    [Tooltip("生成后自动绑定玩家")]
    [SerializeField] private bool autoBindPlayer = true;

    [Tooltip("敌人生成时的Y轴偏移")]
    [SerializeField] private float spawnYOffset = 0f;

    [Header("调试设置")]
    [Tooltip("是否在Scene视图显示生成点")]
    [SerializeField] private bool showSpawnPoints = true;

    [Tooltip("生成点显示颜色")]
    [SerializeField] private Color spawnPointColor = Color.red;

    #endregion

    #region 私有变量

    /// <summary>当前存活的敌人列表</summary>
    private List<GameObject> aliveEnemies = new List<GameObject>();

    /// <summary>已生成的敌人总数</summary>
    private int totalSpawned = 0;

    /// <summary>已消灭的敌人总数</summary>
    private int totalKilled = 0;

    /// <summary>当前波次配置</summary>
    private WaveConfig currentWave;

    /// <summary>是否正在生成中</summary>
    private bool isSpawning = false;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>重生点索引（轮询使用）</summary>
    private int spawnPointIndex = 0;

    /// <summary>协程引用</summary>
    private Coroutine spawnCoroutine;

    #endregion

    #region 公共属性

    /// <summary>当前存活敌人数量</summary>
    public int AliveEnemyCount => aliveEnemies.Count;

    /// <summary>场上是否有敌人</summary>
    public bool HasAliveEnemies => aliveEnemies.Count > 0;

    /// <summary>是否达到场上敌人上限</summary>
    public bool IsAtMaxCapacity => aliveEnemies.Count >= maxAliveEnemies;

    /// <summary>当前波次索引</summary>
    public int CurrentWaveIndex => currentWaveIndex;

    /// <summary>总波次数</summary>
    public int TotalWaves => waves.Count;

    /// <summary>已消灭敌人总数</summary>
    public int TotalKilled => totalKilled;

    /// <summary>是否正在生成</summary>
    public bool IsSpawning => isSpawning;

    /// <summary>是否启用生成</summary>
    public bool SpawningEnabled
    {
        get => spawningEnabled;
        set => spawningEnabled = value;
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化（由Singleton基类调用）
    /// </summary>
    protected override void OnSingletonAwake()
    {
        InitializeSpawnManager();
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    protected override void OnDestroy()
    {
        UnsubscribeEvents();
        base.OnDestroy();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化生成管理器
    /// </summary>
    private void InitializeSpawnManager()
    {
        if (isInitialized) return;

        // 自动查找玩家
        if (targetPlayer == null && autoFindPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                targetPlayer = player.transform;
            }
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning("[EnemySpawnManager] 未找到目标玩家，敌人将不会主动追击");
        }

        // 订阅事件
        SubscribeEvents();

        // 初始化波次
        if (waves.Count > 0)
        {
            currentWave = waves[0];
        }

        isInitialized = true;
        Debug.Log($"[EnemySpawnManager] 初始化完成 - 波次数: {waves.Count}, 场上上限: {maxAliveEnemies}");
    }

    #endregion

    #region 事件订阅

    /// <summary>
    /// 订阅事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 订阅敌人死亡事件
        EventBus.Subscribe("ON_ENEMY_DIE", OnEnemyDied);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_ENEMY_DIE", OnEnemyDied);
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 敌人死亡处理
    /// </summary>
    private void OnEnemyDied()
    {
        // 从存活列表中移除死亡的敌人
        CleanUpDeadEnemies();

        totalKilled++;

        // 触发敌人被消灭事件
        EventBus.Publish(SpawnEvents.ON_ENEMY_KILLED);

        Debug.Log($"[EnemySpawnManager] 敌人被消灭 - 剩余: {aliveEnemies.Count}, 总消灭: {totalKilled}");

        // 检查当前波次是否完成
        CheckWaveComplete();
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 开始第一波敌人生成
    /// </summary>
    public void StartFirstWave()
    {
        if (waves.Count == 0)
        {
            Debug.LogWarning("[EnemySpawnManager] 没有配置波次");
            return;
        }

        currentWaveIndex = 0;
        StartWave(currentWaveIndex);
    }

    /// <summary>
    /// 开始指定波次
    /// </summary>
    /// <param name="waveIndex">波次索引</param>
    public void StartWave(int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= waves.Count)
        {
            Debug.LogError($"[EnemySpawnManager] 无效的波次索引: {waveIndex}");
            return;
        }

        if (!spawningEnabled)
        {
            Debug.LogWarning("[EnemySpawnManager] 敌人生成已禁用");
            return;
        }

        currentWaveIndex = waveIndex;
        currentWave = waves[waveIndex];

        // 触发波次开始事件
        EventBus.Publish(SpawnEvents.ON_WAVE_START);

        Debug.Log($"[EnemySpawnManager] 开始第 {waveIndex + 1} 波: {currentWave.waveName}");

        // 开始生成协程
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnWaveCoroutine());
    }

    /// <summary>
    /// 开始下一波
    /// </summary>
    public void StartNextWave()
    {
        int nextWaveIndex = currentWaveIndex + 1;

        if (nextWaveIndex < waves.Count)
        {
            StartWave(nextWaveIndex);
        }
        else
        {
            Debug.Log("[EnemySpawnManager] 所有波次已完成");
            EventBus.Publish(SpawnEvents.ON_ALL_ENEMIES_CLEARED);
        }
    }

    /// <summary>
    /// 生成单个敌人
    /// </summary>
    /// <param name="prefab">敌人预制体（可为空，使用默认）</param>
    /// <param name="spawnPoint">生成点（可为空，使用随机点）</param>
    /// <returns>生成的敌人对象</returns>
    public GameObject SpawnEnemy(GameObject prefab = null, Transform spawnPoint = null)
    {
        if (!spawningEnabled)
        {
            Debug.LogWarning("[EnemySpawnManager] 敌人生成已禁用");
            return null;
        }

        if (IsAtMaxCapacity)
        {
            Debug.LogWarning($"[EnemySpawnManager] 场上敌人已达到上限: {maxAliveEnemies}");
            return null;
        }

        // 确定使用哪个预制体
        GameObject enemyPrefab = prefab ?? defaultEnemyPrefab;
        if (enemyPrefab == null)
        {
            Debug.LogError("[EnemySpawnManager] 未设置敌人预制体");
            return null;
        }

        // 确定生成位置
        Vector3 spawnPosition = GetSpawnPosition(spawnPoint);

        // 从ObjectPoolManager获取敌人实例
        GameObject enemy = null;
        if (ObjectPoolManager.HasInstance)
        {
            string poolName = "Enemy_" + enemyPrefab.name;
            enemy = ObjectPoolManager.Instance.Get(poolName, spawnPosition, Quaternion.identity);
        }

        // 如果ObjectPoolManager不存在或池中无可用对象，回退到Instantiate
        if (enemy == null)
        {
            enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        }

        // 初始化敌人
        InitializeEnemy(enemy);

        // 添加到存活列表
        aliveEnemies.Add(enemy);
        totalSpawned++;

        // 触发敌人生成事件
        EventBus.Publish(SpawnEvents.ON_ENEMY_SPAWNED);

        Debug.Log($"[EnemySpawnManager] 生成敌人 - 位置: {spawnPosition}, 存活数: {aliveEnemies.Count}");

        return enemy;
    }

    /// <summary>
    /// 在指定重生点重生敌人
    /// </summary>
    /// <param name="respawnPoint">重生点</param>
    /// <param name="delay">延迟时间（秒）</param>
    public void RespawnEnemy(Transform respawnPoint, float delay = -1f)
    {
        if (!spawningEnabled || IsAtMaxCapacity) return;

        float respawnDelayValue = delay >= 0 ? delay : respawnDelay;
        StartCoroutine(RespawnCoroutine(respawnPoint, respawnDelayValue));
    }

    /// <summary>
    /// 立即清除所有敌人
    /// </summary>
    public void ClearAllEnemies()
    {
        foreach (var enemy in aliveEnemies)
        {
            if (enemy != null)
            {
                if (ObjectPoolManager.HasInstance)
                {
                    string poolName = "Enemy_" + enemy.name.Replace("(Clone)", "").Trim();
                    ObjectPoolManager.Instance.Recycle(poolName, enemy);
                }
                else
                {
                    Destroy(enemy);
                }
            }
        }

        aliveEnemies.Clear();

        Debug.Log("[EnemySpawnManager] 已清除所有敌人");
    }

    /// <summary>
    /// 重置生成管理器（玩家重生时调用）
    /// </summary>
    public void ResetSpawnManager()
    {
        // 清除所有敌人
        ClearAllEnemies();

        // 重置计数器
        totalSpawned = 0;
        totalKilled = 0;

        // 重置波次
        currentWaveIndex = 0;
        if (waves.Count > 0)
        {
            currentWave = waves[0];
        }

        // 停止生成协程
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        isSpawning = false;

        Debug.Log("[EnemySpawnManager] 生成管理器已重置");
    }

    /// <summary>
    /// 启用/禁用敌人生成
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetSpawningEnabled(bool enabled)
    {
        spawningEnabled = enabled;

        if (!enabled && spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            isSpawning = false;
        }

        Debug.Log($"[EnemySpawnManager] 敌人生成{(enabled ? "已启用" : "已禁用")}");
    }

    /// <summary>
    /// 设置场上敌人上限
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    public void SetMaxAliveEnemies(int maxCount)
    {
        maxAliveEnemies = Mathf.Max(1, maxCount);
    }

    /// <summary>
    /// 获取存活敌人列表
    /// </summary>
    /// <returns>存活敌人列表</returns>
    public List<GameObject> GetAliveEnemies()
    {
        aliveEnemies.RemoveAll(e => e == null);
        return new List<GameObject>(aliveEnemies);
    }

    #endregion

    #region 生成逻辑

    /// <summary>
    /// 生成波次协程
    /// </summary>
    private IEnumerator SpawnWaveCoroutine()
    {
        isSpawning = true;

        // 等待一帧，确保上一波清理完成
        yield return null;

        int spawnedCount = 0;

        while (spawnedCount < currentWave.enemyCount)
        {
            // 检查是否可以继续生成
            if (!spawningEnabled || IsAtMaxCapacity)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // 确定预制体
            GameObject prefab = currentWave.enemyPrefabOverride ?? defaultEnemyPrefab;

            // 确定生成点
            Transform spawnPoint = null;
            if (currentWave.useCustomSpawnPoints && currentWave.customSpawnPoints != null && currentWave.customSpawnPoints.Length > 0)
            {
                spawnPoint = currentWave.customSpawnPoints[spawnedCount % currentWave.customSpawnPoints.Length];
            }

            // 生成敌人
            SpawnEnemy(prefab, spawnPoint);

            spawnedCount++;

            // 等待生成延迟
            if (spawnedCount < currentWave.enemyCount)
            {
                yield return new WaitForSeconds(currentWave.spawnDelay);
            }
        }

        isSpawning = false;
        Debug.Log($"[EnemySpawnManager] 第 {currentWaveIndex + 1} 波生成完成");
    }

    /// <summary>
    /// 重生协程
    /// </summary>
    /// <param name="spawnPoint">重生点</param>
    /// <param name="delay">延迟时间</param>
    private IEnumerator RespawnCoroutine(Transform spawnPoint, float delay)
    {
        yield return new WaitForSeconds(delay);

        SpawnEnemy(null, spawnPoint);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取生成位置
    /// </summary>
    /// <param name="spawnPoint">指定生成点（可为空）</param>
    /// <returns>生成位置</returns>
    private Vector3 GetSpawnPosition(Transform spawnPoint)
    {
        if (spawnPoint != null)
        {
            return spawnPoint.position + Vector3.up * spawnYOffset;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            // 如果没有生成点，使用原点
            return Vector3.up * spawnYOffset;
        }

        if (randomSpawnPoint)
        {
            // 随机选择生成点
            int index = Random.Range(0, spawnPoints.Length);
            Transform point = spawnPoints[index];
            return point != null ? point.position + Vector3.up * spawnYOffset : Vector3.up * spawnYOffset;
        }
        else
        {
            // 轮询选择生成点
            Transform point = spawnPoints[spawnPointIndex % spawnPoints.Length];
            spawnPointIndex++;
            return point != null ? point.position + Vector3.up * spawnYOffset : Vector3.up * spawnYOffset;
        }
    }

    /// <summary>
    /// 初始化敌人
    /// </summary>
    /// <param name="enemy">敌人对象</param>
    private void InitializeEnemy(GameObject enemy)
    {
        // 绑定玩家目标
        if (autoBindPlayer && targetPlayer != null)
        {
            EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.SetTarget(targetPlayer);
            }
        }
    }

    /// <summary>
    /// 清理死亡敌人
    /// </summary>
    private void CleanUpDeadEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 检查波次是否完成
    /// </summary>
    private void CheckWaveComplete()
    {
        CleanUpDeadEnemies();

        // 如果当前波次生成完成且场上没有敌人
        if (!isSpawning && aliveEnemies.Count == 0)
        {
            Debug.Log($"[EnemySpawnManager] 第 {currentWaveIndex + 1} 波完成");

            // 触发波次完成事件
            EventBus.Publish(SpawnEvents.ON_WAVE_CLEAR);

            // 自动开始下一波
            if (autoStartNextWave)
            {
                float delay = currentWave.delayAfterWave;
                StartCoroutine(StartNextWaveAfterDelay(delay));
            }
        }
    }

    /// <summary>
    /// 延迟后开始下一波
    /// </summary>
    /// <param name="delay">延迟时间</param>
    private IEnumerator StartNextWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNextWave();
    }

    #endregion

    #region 编辑器辅助

    /// <summary>
    /// 在Scene视图中绘制生成点
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showSpawnPoints || spawnPoints == null) return;

        Gizmos.color = spawnPointColor;

        foreach (var point in spawnPoints)
        {
            if (point != null)
            {
                // 绘制生成点球体
                Gizmos.DrawSphere(point.position, 0.5f);

                // 绘制生成点标签
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    point.position + Vector3.up * 1.5f,
                    point.name
                );
                #endif
            }
        }
    }

    #endregion
}
