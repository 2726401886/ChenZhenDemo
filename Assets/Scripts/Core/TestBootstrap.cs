using UnityEngine;

/// <summary>
/// 快速测试引导脚本 - 游戏启动时自动生成测试用玩家和敌人
/// 快捷键测试：P(暂停) R(恢复) K(玩家死亡) L(玩家重生)
/// 自动开启EventBus调试日志，控制台输出所有事件流
/// 仅用于开发测试，发布时应移除此脚本
/// WebGL平台兼容
/// </summary>
public class TestBootstrap : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("玩家设置")]
    [Tooltip("玩家预制体（为空则运行时创建基础玩家）")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("玩家生成位置")]
    [SerializeField] private Vector3 playerSpawnPos = new Vector3(0f, 0f, 0f);

    [Header("敌人设置")]
    [Tooltip("敌人预制体（为空则运行时创建基础敌人）")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("敌人生成位置")]
    [SerializeField] private Vector3 enemySpawnPos = new Vector3(3f, 0f, 3f);

    [Tooltip("敌人生成数量")]
    [SerializeField] private int enemyCount = 3;

    [Tooltip("敌人间距")]
    [SerializeField] private float enemySpacing = 2f;

    [Header("调试设置")]
    [Tooltip("是否启用EventBus调试日志")]
    [SerializeField] private bool enableEventBusLog = true;

    [Tooltip("是否自动生成测试对象")]
    [SerializeField] private bool autoSpawn = true;

    #endregion

    #region 私有变量

    /// <summary>生成的玩家对象</summary>
    private GameObject playerInstance;

    /// <summary>生成的敌人对象列表</summary>
    private GameObject[] enemyInstances;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        // 开启EventBus调试日志
        if (enableEventBusLog)
        {
            EventBus.EnableDebugLog = true;
            Debug.Log("[TestBootstrap] EventBus调试日志已开启");
        }

        // 自动生成测试对象
        if (autoSpawn)
        {
            SpawnTestObjects();
        }

        PrintHotkeyHelp();
    }

    /// <summary>
    /// 每帧更新 - 检测快捷键
    /// </summary>
    private void Update()
    {
        HandleHotkeys();
    }

    /// <summary>Phase10: 转发暂停事件到WebGLPlatformHelper</summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        WebGLPlatformHelper.HandleApplicationPause(pauseStatus);
    }

    #endregion

    #region 快捷键处理

    /// <summary>
    /// 处理快捷键输入
    /// </summary>
    private void HandleHotkeys()
    {
        // P: 发布 ON_GAME_PAUSE
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("[TestBootstrap] 快捷键 P - 发布 ON_GAME_PAUSE");
            EventBus.Publish("ON_GAME_PAUSE");
        }

        // R: 发布 ON_GAME_RESUME
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[TestBootstrap] 快捷键 R - 发布 ON_GAME_RESUME");
            EventBus.Publish("ON_GAME_RESUME");
        }

        // K: 玩家死亡测试
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[TestBootstrap] 快捷键 K - 玩家死亡测试");
            TestPlayerDeath();
        }

        // L: 玩家重生测试
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("[TestBootstrap] 快捷键 L - 玩家重生测试");
            TestPlayerRespawn();
        }
    }

    #endregion

    #region 测试对象生成

    /// <summary>
    /// 生成测试用玩家和敌人
    /// </summary>
    private void SpawnTestObjects()
    {
        // 生成玩家
        SpawnPlayer();

        // 生成敌人
        SpawnEnemies();

        Debug.Log($"[TestBootstrap] 测试对象生成完成 - 玩家: {(playerInstance != null ? "✓" : "✗")}, 敌人: {enemyInstances?.Length ?? 0}");
    }

    /// <summary>
    /// 生成玩家
    /// </summary>
    private void SpawnPlayer()
    {
        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawnPos, Quaternion.identity);
            playerInstance.name = "TestPlayer";
        }
        else
        {
            // 创建基础玩家对象
            playerInstance = CreateBasicPlayer(playerSpawnPos);
        }
    }

    /// <summary>
    /// 生成敌人
    /// </summary>
    private void SpawnEnemies()
    {
        enemyInstances = new GameObject[enemyCount];

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = enemySpawnPos + Vector3.right * (i * enemySpacing);

            if (enemyPrefab != null)
            {
                enemyInstances[i] = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                enemyInstances[i].name = $"TestEnemy_{i}";
            }
            else
            {
                // 创建基础敌人对象
                enemyInstances[i] = CreateBasicEnemy(spawnPos, i);
            }
        }
    }

    /// <summary>
    /// 创建基础玩家对象（无预制体时的备用方案）
    /// </summary>
    /// <param name="position">生成位置</param>
    /// <returns>玩家对象</returns>
    private GameObject CreateBasicPlayer(Vector3 position)
    {
        // 创建胶囊体作为玩家模型
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "TestPlayer";
        player.transform.position = position;

        // 设置Tag
        player.tag = "Player";

        // 设置Layer
        player.layer = LayerMask.NameToLayer("Default");

        // 添加CharacterController
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;

        // 添加CombatSystem
        player.AddComponent<CombatSystem>();

        // 添加PlayerController
        player.AddComponent<PlayerController>();

        // 添加PlayerAnimation
        player.AddComponent<PlayerAnimation>();

        // 设置材质颜色
        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.blue;
        }

        Debug.Log("[TestBootstrap] 创建基础玩家对象");
        return player;
    }

    /// <summary>
    /// 创建基础敌人对象（无预制体时的备用方案）
    /// </summary>
    /// <param name="position">生成位置</param>
    /// <param name="index">敌人索引</param>
    /// <returns>敌人对象</returns>
    private GameObject CreateBasicEnemy(Vector3 position, int index)
    {
        // 创建立方体作为敌人模型
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = $"TestEnemy_{index}";
        enemy.transform.position = position;

        // 设置Tag
        enemy.tag = "Untagged";

        // 添加CharacterController
        CharacterController cc = enemy.AddComponent<CharacterController>();
        cc.height = 1.5f;
        cc.radius = 0.4f;

        // 添加CombatSystem
        enemy.AddComponent<CombatSystem>();

        // 添加EnemyAI
        enemy.AddComponent<EnemyAI>();

        // 添加EnemyAnimation
        enemy.AddComponent<EnemyAnimation>();

        // 添加EnemyHUD
        enemy.AddComponent<EnemyHUD>();

        // 设置材质颜色（不同敌人不同颜色）
        Renderer renderer = enemy.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color[] colors = { Color.red, Color.green, Color.yellow, Color.magenta, Color.cyan };
            renderer.material.color = colors[index % colors.Length];
        }

        Debug.Log($"[TestBootstrap] 创建基础敌人对象: {enemy.name}");
        return enemy;
    }

    #endregion

    #region 测试方法

    /// <summary>
    /// 测试玩家死亡
    /// </summary>
    private void TestPlayerDeath()
    {
        if (playerInstance == null)
        {
            Debug.LogWarning("[TestBootstrap] 玩家对象不存在，无法测试死亡");
            return;
        }

        CombatSystem combat = playerInstance.GetComponent<CombatSystem>();
        if (combat != null)
        {
            combat.Kill();
            Debug.Log("[TestBootstrap] 玩家死亡测试完成");
        }
        else
        {
            Debug.LogWarning("[TestBootstrap] 玩家没有CombatSystem组件");
        }
    }

    /// <summary>
    /// 测试玩家重生
    /// </summary>
    private void TestPlayerRespawn()
    {
        Debug.Log("[TestBootstrap] 发布 ON_PLAYER_RESPAWN");
        EventBus.Publish("ON_PLAYER_RESPAWN");
        Debug.Log("[TestBootstrap] 玩家重生测试完成");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 打印快捷键帮助信息
    /// </summary>
    private void PrintHotkeyHelp()
    {
        Debug.Log("========================================");
        Debug.Log("[TestBootstrap] 快捷键测试指南：");
        Debug.Log("  P - 暂停游戏 (ON_GAME_PAUSE)");
        Debug.Log("  R - 恢复游戏 (ON_GAME_RESUME)");
        Debug.Log("  K - 玩家死亡测试");
        Debug.Log("  L - 玩家重生测试");
        Debug.Log("========================================");
        Debug.Log("[TestBootstrap] EventBus调试日志: " + (EventBus.EnableDebugLog ? "开启" : "关闭"));
        Debug.Log("========================================");
    }

    #endregion
}
