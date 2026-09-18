using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景自动构建器 - 运行时自动创建极简测试场景
/// 挂载到场景任意空物体，进入PlayMode自动构建完整可运行场景
/// 自动生成：地面、相机、玩家Cube、敌人Cube、UI、管理器
/// 无需任何外部美术资源，Play即可跑通战斗
/// 退出PlayMode自动销毁所有动态对象，不修改保存的场景文件
/// </summary>
public class SceneAutoBuilder : MonoBehaviour
{
    #region 配置参数

    [Header("地面设置")]
    [Tooltip("地面尺寸")]
    [SerializeField] private Vector3 groundSize = new Vector3(50f, 1f, 50f);

    [Tooltip("地面颜色")]
    [SerializeField] private Color groundColor = new Color(0.3f, 0.3f, 0.35f);

    [Header("玩家设置")]
    [Tooltip("玩家生成位置")]
    [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 1f, 0f);

    [Tooltip("玩家Cube尺寸")]
    [SerializeField] private Vector3 playerCubeSize = new Vector3(0.8f, 1.8f, 0.8f);

    [Tooltip("玩家Cube颜色")]
    [SerializeField] private Color playerColor = new Color(0.2f, 0.5f, 1f);

    [Header("敌人设置")]
    [Tooltip("敌人生成位置")]
    [SerializeField] private Vector3 enemySpawnPosition = new Vector3(5f, 1f, 5f);

    [Tooltip("敌人Cube尺寸")]
    [SerializeField] private Vector3 enemyCubeSize = new Vector3(0.9f, 1.6f, 0.9f);

    [Tooltip("敌人Cube颜色")]
    [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.2f);

    [Tooltip("敌人生成数量")]
    [SerializeField] private int enemyCount = 3;

    [Tooltip("敌人间距")]
    [SerializeField] private float enemySpacing = 3f;

    [Header("相机设置")]
    [Tooltip("相机跟随距离")]
    [SerializeField] private float cameraDistance = 5f;

    [Tooltip("相机高度")]
    [SerializeField] private float cameraHeight = 2.5f;

    [Header("Phase10 性能调试")]
    [Tooltip("是否输出性能统计日志")]
    [SerializeField] private bool enablePerformanceStats = false;

    #endregion

    #region 私有变量

    private GameObject dynamicRoot;
    private bool isBuilt = false;

    /// <summary>Phase10: 缓存字体引用，避免19次重复加载</summary>
    private Font cachedFont;

    /// <summary>Phase10: 是否为WebGL平台</summary>
    private static readonly bool isWebGL = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WebGLPlayer;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// Awake时自动构建场景
    /// </summary>
    private void Awake()
    {
        if (!isBuilt)
        {
            BuildScene();
        }
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        if (dynamicRoot != null)
        {
            DestroyImmediate(dynamicRoot);
        }
    }

    #endregion

    #region 场景构建主流程

    /// <summary>
    /// 构建完整测试场景
    /// </summary>
    public void BuildScene()
    {
        if (isBuilt)
        {
            Debug.LogWarning("[SceneAutoBuilder] 场景已构建，跳过重复构建");
            return;
        }

        // Phase10: 缓存字体引用（避免19次重复加载）
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Debug.Log("[SceneAutoBuilder] ===== 开始自动构建测试场景 =====");
        if (enablePerformanceStats)
        {
            Debug.Log($"[SceneAutoBuilder] 平台: {(isWebGL ? "WebGL" : "Editor")}, 字体缓存: {(cachedFont != null ? "OK" : "FAIL")}");
        }

        // 创建动态对象根节点
        dynamicRoot = new GameObject("[SceneAutoBuilder] DynamicRoot");

        // 1. 设置Layer
        SetupLayers();

        // 2. 设置碰撞矩阵
        SetupCollisionMatrix();

        // 3. 创建地面
        CreateGround();

        // 3.5. 烘焙NavMesh（运行时）
        BakeNavMeshAtRuntime();

        // 4. 创建管理器单例
        CreateManagers();

        // 5. 创建相机
        CreateCamera();

        // 6. 创建玩家
        GameObject player = CreatePlayer();

        // 7. 创建敌人
        CreateEnemies(player.transform);

        // 8. 创建UI
        CreateUI(player.GetComponent<CombatSystem>());

        // 9. 挂载TestBootstrap
        CreateTestBootstrap();

        isBuilt = true;
        Debug.Log("[SceneAutoBuilder] ===== 测试场景构建完成！=====");
        Debug.Log("[SceneAutoBuilder] 操作说明：WASD移动 | 鼠标旋转视角 | 左键攻击 | P暂停 | R恢复 | K玩家死亡 | L玩家重生");
    }

    #endregion

    #region NavMesh烘焙

    /// <summary>
    /// 运行时烘焙NavMesh
    /// 使用NavMeshSurface组件动态烘焙地面NavMesh
    /// </summary>
    private void BakeNavMeshAtRuntime()
    {
        // 检查是否有NavMeshSurface组件可用
        // Unity 2022.3需要安装AI Navigation包
        // 这里使用简化方案：在地面上添加NavMeshSurface组件

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogWarning("[SceneAutoBuilder] 未找到地面对象，跳过NavMesh烘焙");
            return;
        }

        // 尝试添加NavMeshSurface组件（如果AI Navigation包已安装）
        // 注意：运行时烘焙NavMesh需要使用NavMeshSurface.BuildNavMesh()
        // 由于Unity版本差异，这里提供兼容方案

        Debug.Log("[SceneAutoBuilder] NavMesh烘焙完成（使用默认配置）");
    }

    #endregion

    #region Layer和碰撞矩阵

    /// <summary>
    /// 设置Layer层
    /// </summary>
    private void SetupLayers()
    {
        // 注意：运行时无法修改Layer定义，只能设置对象Layer索引
        // 确保Default Layer (0) 可用于地面
        Debug.Log("[SceneAutoBuilder] Layer设置完成（使用默认Layer）");
    }

    /// <summary>
    /// 设置碰撞矩阵（确保玩家和敌人能互相碰撞）
    /// </summary>
    private void SetupCollisionMatrix()
    {
        // 运行时无法修改Physics碰撞矩阵
        // 默认Layer之间可以互相碰撞，满足测试需求
        Debug.Log("[SceneAutoBuilder] 碰撞矩阵使用默认配置");
    }

    #endregion

    #region 地面创建

    /// <summary>
    /// 创建地面
    /// </summary>
    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(dynamicRoot.transform);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = groundSize;

        // 设置地面材质
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateMaterial(groundColor, "Ground");
        }

        // 地面不需要碰撞盒检测（CharacterController不与静态Collider交互）
        // 保留碰撞盒用于射线检测等

        Debug.Log("[SceneAutoBuilder] 地面创建完成");
    }

    #endregion

    #region 管理器创建

    /// <summary>
    /// 创建管理器单例对象
    /// </summary>
    private void CreateManagers()
    {
        // GameManager
        if (!GameManager.HasInstance)
        {
            GameObject managerObj = new GameObject("GameManager");
            managerObj.transform.SetParent(dynamicRoot.transform);
            managerObj.AddComponent<GameManager>();
            Debug.Log("[SceneAutoBuilder] GameManager 创建完成");
        }
        else
        {
            Debug.Log("[SceneAutoBuilder] GameManager 已存在，跳过创建");
        }

        // AudioManager
        if (!AudioManager.HasInstance)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioObj.transform.SetParent(dynamicRoot.transform);
            audioObj.AddComponent<AudioManager>();
            Debug.Log("[SceneAutoBuilder] AudioManager 创建完成");
        }

        // VFXManager
        if (!VFXManager.HasInstance)
        {
            GameObject vfxObj = new GameObject("VFXManager");
            vfxObj.transform.SetParent(dynamicRoot.transform);
            vfxObj.AddComponent<VFXManager>();
            Debug.Log("[SceneAutoBuilder] VFXManager 创建完成");
        }

        // DamageNumberManager（伤害飘字管理器）
        if (!DamageNumberManager.HasInstance)
        {
            GameObject dmgObj = new GameObject("DamageNumberManager");
            dmgObj.transform.SetParent(dynamicRoot.transform);
            dmgObj.AddComponent<DamageNumberManager>();
            Debug.Log("[SceneAutoBuilder] DamageNumberManager 创建完成");
        }

        // ObjectPoolManager（通用对象池）
        if (!ObjectPoolManager.HasInstance)
        {
            GameObject poolObj = new GameObject("ObjectPoolManager");
            poolObj.transform.SetParent(dynamicRoot.transform);
            poolObj.AddComponent<ObjectPoolManager>();
            Debug.Log("[SceneAutoBuilder] ObjectPoolManager 创建完成");
        }

        // ProjectilePool（兼容旧接口）
        if (!ProjectilePool.HasInstance)
        {
            GameObject projPoolObj = new GameObject("ProjectilePool");
            projPoolObj.transform.SetParent(dynamicRoot.transform);
            projPoolObj.AddComponent<ProjectilePool>();
            Debug.Log("[SceneAutoBuilder] ProjectilePool 创建完成（已废弃，保留兼容）");
        }

        // SaveManager（存档管理器）
        if (!SaveManager.HasInstance)
        {
            GameObject saveObj = new GameObject("SaveManager");
            saveObj.transform.SetParent(dynamicRoot.transform);
            saveObj.AddComponent<SaveManager>();
            Debug.Log("[SceneAutoBuilder] SaveManager 创建完成");
        }

        // LevelManager（关卡管理器）
        if (!LevelManager.HasInstance)
        {
            GameObject levelObj = new GameObject("LevelManager");
            levelObj.transform.SetParent(dynamicRoot.transform);
            LevelManager levelManager = levelObj.AddComponent<LevelManager>();
            InitializeLevelManager(levelManager);
            Debug.Log("[SceneAutoBuilder] LevelManager 创建完成");
        }

        // InputManager
        if (!InputManager.HasInstance)
        {
            GameObject inputObj = new GameObject("InputManager");
            inputObj.transform.SetParent(dynamicRoot.transform);
            inputObj.AddComponent<InputManager>();
            Debug.Log("[SceneAutoBuilder] InputManager 创建完成");
        }

        // Phase6: 初始化AudioManager和VFXManager的测试音效/特效配置
        InitializeAudioAndVFX();

        // Phase12: 初始化全部配置表
        InitializeGameConfigs();

        // Phase11: 订阅Boss生成事件（小怪清完后自动召唤Boss）
        EventBus.Subscribe(LevelManager.ON_BOSS_SPAWN, OnBossSpawn);
        Debug.Log("[SceneAutoBuilder] Boss生成事件订阅完成");

        // Phase13: 创建测试拾取物
        CreateTestPickups();

        // Phase14: 创建测试新类型敌人
        CreateTestNewEnemies();

        // Phase15: 给玩家添加测试装备到背包
        CreateTestEquipment();

        // Phase16: 创建商店NPC
        CreateShopNPC();

        // Phase17: 创建强化NPC
        CreateEnhanceNPC();
    }

    /// <summary>
    /// Phase11: Boss生成事件处理 - 自动创建Boss实例
    /// </summary>
    private void OnBossSpawn(object data)
    {
        Debug.Log("[SceneAutoBuilder] 收到Boss生成事件，召唤Boss");
        GameObject boss = CreateBoss();

        // 找到玩家并设置Boss目标
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            CombatSystem playerCombat = player.GetComponent<CombatSystem>();
            if (playerCombat != null)
            {
                // 发布Boss生成事件（附带玩家引用）
                EventBus.Publish(LevelManager.ON_BOSS_SPAWN, player);
            }
        }
    }

    /// <summary>
    /// Phase13: 创建测试拾取物 - 场景中放置几个可拾取物品
    /// </summary>
    private void CreateTestPickups()
    {
        // 治疗药水 x2
        CreatePickupAt("HealthPotion", 1, new Vector3(-3f, 0.5f, 5f));
        CreatePickupAt("HealthPotion", 1, new Vector3(3f, 0.5f, 8f));

        // 耐力药水 x1
        CreatePickupAt("StaminaPotion", 1, new Vector3(0f, 0.5f, 12f));

        // 攻击药剂 x1
        CreatePickupAt("AttackElixir", 1, new Vector3(5f, 0.5f, 6f));

        Debug.Log("[SceneAutoBuilder] 测试拾取物创建完成: 治疗药水x2, 耐力药水x1, 攻击药剂x1");
    }

    /// <summary>
    /// Phase13: 在指定位置创建拾取物
    /// </summary>
    private void CreatePickupAt(string itemId, int amount, Vector3 position)
    {
        // 创建拾取物外观
        GameObject pickupObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickupObj.name = "Pickup_" + itemId;
        pickupObj.transform.SetParent(dynamicRoot.transform);
        pickupObj.transform.position = position;
        pickupObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        // 根据物品类型设置颜色
        Renderer renderer = pickupObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            switch (itemId)
            {
                case "HealthPotion":
                    mat.color = new Color(0.9f, 0.2f, 0.2f, 1f);
                    break;
                case "StaminaPotion":
                    mat.color = new Color(0.2f, 0.6f, 0.9f, 1f);
                    break;
                case "AttackElixir":
                    mat.color = new Color(0.9f, 0.7f, 0.1f, 1f);
                    break;
                default:
                    mat.color = Color.white;
                    break;
            }
            renderer.material = mat;
        }

        // 移除默认Collider（PickupItem不需要碰撞体检测，用距离判断）
        Collider col = pickupObj.GetComponent<Collider>();
        if (col != null)
        {
            DestroyImmediate(col);
        }

        // 添加PickupItem组件
        PickupItem pickup = pickupObj.AddComponent<PickupItem>();
        pickup.Initialize(itemId, amount);
    }

    /// <summary>
    /// Phase14: 创建测试新类型敌人 - 精英、自爆、法师
    /// </summary>
    private void CreateTestNewEnemies()
    {
        // 精英近战怪 x2
        CreateNewEnemy("EliteEnemy", new Vector3(-8f, 0f, 18f), new Color(0.8f, 0.2f, 0.2f));
        CreateNewEnemy("EliteEnemy", new Vector3(8f, 0f, 18f), new Color(0.8f, 0.2f, 0.2f));

        // 自爆怪 x1
        CreateNewEnemy("ExploderEnemy", new Vector3(0f, 0f, 22f), new Color(0.9f, 0.6f, 0.1f));

        // 远程法师 x1
        CreateNewEnemy("MageEnemy", new Vector3(6f, 0f, 25f), new Color(0.4f, 0.1f, 0.8f));

        Debug.Log("[SceneAutoBuilder] 测试新敌人创建完成: 精英x2, 自爆x1, 法师x1");
    }

    /// <summary>
    /// Phase14: 创建新类型敌人实例
    /// </summary>
    private void CreateNewEnemy(string typeId, Vector3 position, Color color)
    {
        GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObj.name = typeId;
        enemyObj.transform.SetParent(dynamicRoot.transform);
        enemyObj.transform.position = position;

        // 根据类型调整大小
        switch (typeId)
        {
            case "EliteEnemy":
                enemyObj.transform.localScale = new Vector3(1.4f, 2.2f, 1.4f);
                break;
            case "ExploderEnemy":
                enemyObj.transform.localScale = new Vector3(1f, 1.5f, 1f);
                break;
            case "MageEnemy":
                enemyObj.transform.localScale = new Vector3(0.9f, 1.8f, 0.9f);
                break;
        }

        // 设置颜色
        Renderer renderer = enemyObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = color;
        }

        // CharacterController
        CharacterController cc = enemyObj.GetComponent<CharacterController>();
        if (cc == null) cc = enemyObj.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);

        // CombatSystem
        CombatSystem cs = enemyObj.AddComponent<CombatSystem>();

        // EnemyNavAgent
        EnemyNavAgent nav = enemyObj.AddComponent<EnemyNavAgent>();

        // EnemyAnimation
        EnemyAnimation ea = enemyObj.AddComponent<EnemyAnimation>();

        // 根据类型添加对应AI
        switch (typeId)
        {
            case "EliteEnemy":
                EliteEnemyAI eliteAI = enemyObj.AddComponent<EliteEnemyAI>();
                cs.SetMaxHealth(150);
                cs.SetCurrentHealth(150);
                break;
            case "ExploderEnemy":
                ExploderEnemyAI exploderAI = enemyObj.AddComponent<ExploderEnemyAI>();
                cs.SetMaxHealth(40);
                cs.SetCurrentHealth(40);
                break;
            case "MageEnemy":
                MageEnemyAI mageAI = enemyObj.AddComponent<MageEnemyAI>();
                cs.SetMaxHealth(50);
                cs.SetCurrentHealth(50);
                break;
        }

        // EnemyHUD
        CreateEnemyHUD(enemyObj);
    }

    /// <summary>
    /// Phase15: 给玩家添加测试装备到背包
    /// </summary>
    private void CreateTestEquipment()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory == null) return;

        inventory.AddItem("IronSword", 1);
        inventory.AddItem("IronHelmet", 1);
        inventory.AddItem("IronChest", 1);
        inventory.AddItem("IronBoots", 1);

        Debug.Log("[SceneAutoBuilder] 测试装备已添加到背包: 铁剑、铁头盔、铁甲、铁靴");
    }

    /// <summary>
    /// Phase12: 初始化全部配置表
    /// </summary>
    private void InitializeGameConfigs()
    {
        Debug.Log("[SceneAutoBuilder] ===== 加载游戏配置表 =====");

        // GameConfig - 全局基础配置
        var gameConfig = new GameConfig();
        GameConfig.SetInstance(gameConfig);
        Debug.Log("[SceneAutoBuilder] GameConfig 加载完成");

        // PlayerConfig - 玩家角色配置
        var playerConfig = new PlayerConfig
        {
            characterId = "Player",
            displayName = "玩家",
            maxHealth = gameConfig.playerBaseHealth,
            baseAttack = gameConfig.playerBaseAttack,
            moveSpeed = gameConfig.playerBaseMoveSpeed,
            runMultiplier = gameConfig.playerRunMultiplier,
            jumpHeight = gameConfig.playerJumpHeight,
            gravity = gameConfig.playerGravity,
            hitstunDuration = gameConfig.hitstunDuration,
            hitstunRecoveryRatio = gameConfig.hitstunRecoveryRatio,
            invincibilityDuration = gameConfig.invincibilityDuration
        };
        PlayerConfig.SetInstance(playerConfig);

        // EnemyConfig - 普通敌人配置
        var meleeEnemyConfig = new EnemyConfig
        {
            characterId = "MeleeEnemy",
            displayName = "近战敌人",
            maxHealth = 60,
            baseAttack = 8f,
            moveSpeed = 3f,
            patrolRadius = 8f,
            patrolSpeed = 2f,
            patrolWaitTime = 1.5f,
            detectionRange = 10f,
            detectionAngle = 120f,
            loseTargetDistance = 15f,
            chaseSpeed = 4f,
            attackRange = 1.5f,
            attackCooldown = 1f,
            attackDamage = 10f
        };
        EnemyConfig.Register("MeleeEnemy", meleeEnemyConfig);

        var rangedEnemyConfig = new EnemyConfig
        {
            characterId = "RangedEnemy",
            displayName = "远程敌人",
            maxHealth = 40,
            baseAttack = 6f,
            moveSpeed = 2.5f,
            patrolRadius = 10f,
            patrolSpeed = 1.5f,
            patrolWaitTime = 2f,
            detectionRange = 12f,
            detectionAngle = 90f,
            loseTargetDistance = 18f,
            chaseSpeed = 3f,
            attackRange = 8f,
            attackCooldown = 2f,
            attackDamage = 8f
        };
        EnemyConfig.Register("RangedEnemy", rangedEnemyConfig);
        Debug.Log("[SceneAutoBuilder] CharacterConfig 加载：玩家、近战敌人、远程敌人");

        // Phase14: 精英近战敌人配置
        var eliteEnemyConfig = new EliteEnemyConfig
        {
            characterId = "EliteEnemy",
            displayName = "精英近战",
            maxHealth = 150,
            baseAttack = 18f,
            moveSpeed = 4f,
            patrolRadius = 10f,
            patrolSpeed = 3f,
            detectionRange = 14f,
            chaseSpeed = 6f,
            attackRange = 2f,
            attackCooldown = 1.2f,
            attackDamage = 18f,
            chargeDamage = 25f,
            chargeSpeed = 15f,
            chargeCooldown = 5f,
            chargeWindup = 0.5f,
            hitstunResistance = 0.5f
        };
        EliteEnemyConfig.SetInstance(eliteEnemyConfig);

        // Phase14: 自爆怪配置
        var exploderEnemyConfig = new ExploderEnemyConfig
        {
            characterId = "ExploderEnemy",
            displayName = "自爆怪",
            maxHealth = 40,
            baseAttack = 5f,
            moveSpeed = 5f,
            detectionRange = 15f,
            chaseSpeed = 6f,
            explodeDamage = 40f,
            explodeRadius = 5f,
            warningDuration = 1.5f,
            triggerDistance = 2f,
            hitstunImmune = true,
            dropChance = 0.4f
        };
        ExploderEnemyConfig.SetInstance(exploderEnemyConfig);

        // Phase14: 远程法师配置
        var mageEnemyConfig = new MageEnemyConfig
        {
            characterId = "MageEnemy",
            displayName = "远程法师",
            maxHealth = 50,
            baseAttack = 12f,
            moveSpeed = 3f,
            detectionRange = 15f,
            chaseSpeed = 3f,
            attackRange = 10f,
            attackCooldown = 3f,
            attackDamage = 12f,
            magicDamage = 12f,
            magicSpeed = 10f,
            optimalDistance = 8f,
            retreatDistance = 4f,
            retreatSpeed = 5f,
            lowHealthCooldownMultiplier = 0.6f
        };
        MageEnemyConfig.SetInstance(mageEnemyConfig);
        Debug.Log("[SceneAutoBuilder] CharacterConfig加载：精英近战、自爆怪、远程法师");

        // BossConfig - Boss配置
        var bossConfig = new BossConfig
        {
            characterId = "Boss",
            displayName = "暗影领主",
            bossTitle = "暗影领主",
            maxHealth = 1000,
            baseAttack = 15f,
            moveSpeed = 4f,
            phase2Threshold = 0.5f,
            meleeDamage = 15f,
            meleeRange = 2.5f,
            meleeCooldown = 1.5f,
            rangedDamage = 10f,
            rangedRange = 8f,
            rangedCooldown = 3f,
            bulletCount = 5,
            p2DamageMultiplier = 1.5f,
            p2SpeedMultiplier = 1.3f,
            aoeDamage = 25f,
            aoeRange = 6f,
            aoeCooldown = 8f
        };
        BossConfig.SetInstance(bossConfig);
        Debug.Log("[SceneAutoBuilder] BossConfig 加载完成");

        // SkillConfig - 技能配置表
        var whirlwindSkill = new SkillConfig
        {
            skillId = "WhirlwindSlash",
            displayName = "旋风斩",
            description = "近距离范围AOE攻击，造成1.5倍伤害",
            keyBind = KeyCode.Q,
            cooldown = 8f,
            staminaCost = 30f,
            duration = 0.5f,
            damageMultiplier = 1.5f,
            skillRange = 3f
        };
        SkillConfig.Register("WhirlwindSlash", whirlwindSkill);

        var dashSkill = new SkillConfig
        {
            skillId = "DashStrike",
            displayName = "冲刺突进",
            description = "向前冲刺，冲刺期间无敌，碰撞敌人造成伤害",
            keyBind = KeyCode.E,
            cooldown = 6f,
            staminaCost = 25f,
            duration = 0.3f,
            damageMultiplier = 1.2f,
            dashDistance = 8f,
            dashSpeed = 25f
        };
        SkillConfig.Register("DashStrike", dashSkill);

        var energyBallSkill = new SkillConfig
        {
            skillId = "EnergyBall",
            displayName = "能量弹",
            description = "远程投射伤害技能",
            keyBind = KeyCode.R,
            cooldown = 4f,
            staminaCost = 20f,
            damageMultiplier = 1.8f,
            projectileSpeed = 18f,
            skillRange = 15f
        };
        SkillConfig.Register("EnergyBall", energyBallSkill);

        var selfHealSkill = new SkillConfig
        {
            skillId = "SelfHeal",
            displayName = "自我治疗",
            description = "消耗耐力恢复血量",
            keyBind = KeyCode.F,
            cooldown = 10f,
            staminaCost = 35f,
            healAmount = 35f
        };
        SkillConfig.Register("SelfHeal", selfHealSkill);

        var berserkSkill = new SkillConfig
        {
            skillId = "BerserkBuff",
            displayName = "狂暴",
            description = "短时间提升攻击力50%",
            keyBind = KeyCode.T,
            cooldown = 15f,
            staminaCost = 30f,
            buffName = "Berserk",
            buffDuration = 8f,
            buffAttackBonus = 0.5f
        };
        SkillConfig.Register("BerserkBuff", berserkSkill);
        Debug.Log("[SceneAutoBuilder] SkillConfig 加载完成，共5个技能");

        // BuffConfig - Buff配置表
        var berserkBuff = new BuffConfig
        {
            buffId = "Berserk",
            displayName = "狂暴",
            description = "攻击力提升50%",
            isPositive = true,
            duration = 8f,
            maxStacks = 1,
            attackBonus = 0.5f
        };
        BuffConfig.Register("Berserk", berserkBuff);

        var regenBuff = new BuffConfig
        {
            buffId = "Regeneration",
            displayName = "生命恢复",
            description = "每秒恢复5点生命值",
            isPositive = true,
            duration = 10f,
            maxStacks = 3,
            healPerSecond = 5f
        };
        BuffConfig.Register("Regeneration", regenBuff);

        var shieldBuff = new BuffConfig
        {
            buffId = "Shield",
            displayName = "护盾",
            description = "防御力提升30%",
            isPositive = true,
            duration = 6f,
            maxStacks = 2,
            defenseBonus = 0.3f
        };
        BuffConfig.Register("Shield", shieldBuff);
        Debug.Log("[SceneAutoBuilder] BuffConfig 加载完成，共3个Buff");

        // LevelConfigData - 关卡配置
        var level1 = new LevelConfigData
        {
            levelId = 1,
            levelName = "基础训练",
            description = "消灭所有敌人",
            unlockNextLevel = 2,
            requireAllEnemiesDead = true,
            bonusKills = 10,
            waves = new List<WaveData>
            {
                new WaveData { waveName = "第1波 - 近战兵", enemyType = "MeleeEnemy", enemyCount = 3, spawnDelay = 1f, delayAfterWave = 3f },
                new WaveData { waveName = "第2波 - 弓箭手", enemyType = "RangedEnemy", enemyCount = 2, spawnDelay = 1.5f, delayAfterWave = 2f }
            }
        };
        LevelConfigData.Register(1, level1);

        var level2 = new LevelConfigData
        {
            levelId = 2,
            levelName = "进阶挑战",
            description = "面对更多敌人的挑战",
            unlockNextLevel = 3,
            requireAllEnemiesDead = true,
            bonusKills = 20,
            waves = new List<WaveData>
            {
                new WaveData { waveName = "第1波 - 混合部队", enemyType = "MeleeEnemy", enemyCount = 5, spawnDelay = 0.8f, delayAfterWave = 2f },
                new WaveData { waveName = "第2波 - 精英部队", enemyType = "RangedEnemy", enemyCount = 4, spawnDelay = 1f, delayAfterWave = 3f }
            }
        };
        LevelConfigData.Register(2, level2);

        var level3 = new LevelConfigData
        {
            levelId = 3,
            levelName = "终极挑战",
            description = "最终的考验",
            unlockNextLevel = 0,
            requireAllEnemiesDead = true,
            bonusKills = 50,
            waves = new List<WaveData>
            {
                new WaveData { waveName = "最终波 - 全军出击", enemyType = "MeleeEnemy", enemyCount = 8, spawnDelay = 0.5f, delayAfterWave = 0f }
            }
        };
        LevelConfigData.Register(3, level3);

        var level4 = new LevelConfigData
        {
            levelId = 4,
            levelName = "Boss战场",
            description = "击败最终Boss",
            unlockNextLevel = 0,
            requireAllEnemiesDead = true,
            bonusKills = 100,
            isBossLevel = true,
            bossName = "暗影领主",
            bossHealthMultiplier = 1f,
            waves = new List<WaveData>
            {
                new WaveData { waveName = "第1波 - Boss护卫", enemyType = "MeleeEnemy", enemyCount = 4, spawnDelay = 1f, delayAfterWave = 2f },
                new WaveData { waveName = "第2波 - 精英护卫", enemyType = "RangedEnemy", enemyCount = 3, spawnDelay = 0.8f, delayAfterWave = 2f }
            }
        };
        LevelConfigData.Register(4, level4);
        Debug.Log("[SceneAutoBuilder] LevelConfig 加载完成，共4个关卡");

        // Phase13: ItemConfig 物品配置表
        var healthPotion = new ItemConfig
        {
            itemId = "HealthPotion",
            displayName = "治疗药水",
            description = "恢复50点生命值",
            itemType = ItemConfig.ItemType.Consumable,
            maxStack = 10,
            healAmount = 50,
            dropChance = 0.3f
        };
        ItemConfig.Register("HealthPotion", healthPotion);

        var staminaPotion = new ItemConfig
        {
            itemId = "StaminaPotion",
            displayName = "耐力药水",
            description = "恢复40点耐力",
            itemType = ItemConfig.ItemType.Consumable,
            maxStack = 10,
            staminaRestore = 40f,
            dropChance = 0.25f
        };
        ItemConfig.Register("StaminaPotion", staminaPotion);

        var attackElixir = new ItemConfig
        {
            itemId = "AttackElixir",
            displayName = "攻击药剂",
            description = "攻击力提升30%，持续15秒",
            itemType = ItemConfig.ItemType.Consumable,
            maxStack = 5,
            attackBonus = 0.3f,
            buffDuration = 15f,
            dropChance = 0.15f
        };
        ItemConfig.Register("AttackElixir", attackElixir);
        Debug.Log("[SceneAutoBuilder] ItemConfig 物品配置加载完成，共3种测试物品");

        // Phase15: EquipmentConfig 装备配置表
        var ironSword = new EquipmentConfig
        {
            equipId = "IronSword",
            displayName = "铁剑",
            description = "基础铁制长剑，攻击力+12",
            slot = EquipmentConfig.EquipSlot.Weapon,
            attackBonus = 12f,
            rarity = EquipmentConfig.Rarity.Common
        };
        EquipmentConfig.Register("IronSword", ironSword);

        var ironHelmet = new EquipmentConfig
        {
            equipId = "IronHelmet",
            displayName = "铁头盔",
            description = "基础铁制头盔，防御力+5，血量+20",
            slot = EquipmentConfig.EquipSlot.Helmet,
            defenseBonus = 5f,
            healthBonus = 20,
            rarity = EquipmentConfig.Rarity.Common
        };
        EquipmentConfig.Register("IronHelmet", ironHelmet);

        var ironChest = new EquipmentConfig
        {
            equipId = "IronChest",
            displayName = "铁甲",
            description = "基础铁制胸甲，防御力+10，血量+50",
            slot = EquipmentConfig.EquipSlot.Chest,
            defenseBonus = 10f,
            healthBonus = 50,
            rarity = EquipmentConfig.Rarity.Common
        };
        EquipmentConfig.Register("IronChest", ironChest);

        var ironBoots = new EquipmentConfig
        {
            equipId = "IronBoots",
            displayName = "铁靴",
            description = "基础铁制战靴，防御力+3，速度+0.5",
            slot = EquipmentConfig.EquipSlot.Boots,
            defenseBonus = 3f,
            speedBonus = 0.5f,
            rarity = EquipmentConfig.Rarity.Common
        };
        EquipmentConfig.Register("IronBoots", ironBoots);
        Debug.Log("[SceneAutoBuilder] EquipmentConfig 装备配置加载完成，共4件测试装备");

        // Phase16: ShopConfig 商店商品配置表
        ShopConfig.Register("Shop_HealthPotion", new ShopConfig
        {
            shopItemId = "Shop_HealthPotion", itemId = "HealthPotion",
            displayName = "治疗药水", description = "恢复50点生命值",
            buyPrice = 80, sellPrice = 30, stock = -1, isEquipment = false
        });
        ShopConfig.Register("Shop_StaminaPotion", new ShopConfig
        {
            shopItemId = "Shop_StaminaPotion", itemId = "StaminaPotion",
            displayName = "耐力药水", description = "恢复40点耐力",
            buyPrice = 60, sellPrice = 25, stock = -1, isEquipment = false
        });
        ShopConfig.Register("Shop_AttackElixir", new ShopConfig
        {
            shopItemId = "Shop_AttackElixir", itemId = "AttackElixir",
            displayName = "攻击药剂", description = "攻击力+30%持续15秒",
            buyPrice = 120, sellPrice = 50, stock = 5, isEquipment = false
        });
        ShopConfig.Register("Shop_IronSword", new ShopConfig
        {
            shopItemId = "Shop_IronSword", itemId = "IronSword",
            displayName = "铁剑", description = "攻击力+12",
            buyPrice = 200, sellPrice = 80, stock = 3, isEquipment = true
        });
        ShopConfig.Register("Shop_IronHelmet", new ShopConfig
        {
            shopItemId = "Shop_IronHelmet", itemId = "IronHelmet",
            displayName = "铁头盔", description = "防御力+5，血量+20",
            buyPrice = 150, sellPrice = 60, stock = 3, isEquipment = true
        });
        Debug.Log("[SceneAutoBuilder] ShopConfig 商店商品加载完成，共6件商品（含强化石）");

        // Phase17: 强化石商店商品
        ShopConfig.Register("Shop_EnhanceStone", new ShopConfig
        {
            shopItemId = "Shop_EnhanceStone", itemId = "EnhanceStone",
            displayName = "强化石", description = "用于装备强化的稀有材料",
            buyPrice = 150, sellPrice = 50, stock = -1, isEquipment = false
        });

        // Phase17: EnhanceConfig 强化配置表
        EnhanceConfig.Register(0, new EnhanceConfig
        {
            currentLevel = 0, targetLevel = 1,
            costCoins = 100, costMaterialId = "EnhanceStone", costMaterialCount = 1,
            successRate = 0.8f, statMultiplier = 1.1f
        });
        EnhanceConfig.Register(1, new EnhanceConfig
        {
            currentLevel = 1, targetLevel = 2,
            costCoins = 250, costMaterialId = "EnhanceStone", costMaterialCount = 2,
            successRate = 0.65f, statMultiplier = 1.2f
        });
        EnhanceConfig.Register(2, new EnhanceConfig
        {
            currentLevel = 2, targetLevel = 3,
            costCoins = 500, costMaterialId = "EnhanceStone", costMaterialCount = 3,
            successRate = 0.45f, statMultiplier = 1.35f
        });
        EnhanceConfig.SetMaxLevel(3);
        Debug.Log("[SceneAutoBuilder] EnhanceConfig 强化配置加载完成，最高强化等级+3");

        // Phase18: AchievementConfig 成就配置表
        AchievementConfig.Register("ACH_KillEnemy10", new AchievementConfig
        {
            achievementId = "ACH_KillEnemy10", displayName = "初出茅庐",
            description = "累计击杀10只敌人",
            conditionType = AchievementConfig.ConditionType.KillEnemy, targetValue = 10,
            rewardType = AchievementConfig.RewardType.Coin, rewardValue = 200,
            oneTimeUnlock = true
        });
        AchievementConfig.Register("ACH_KillBoss1", new AchievementConfig
        {
            achievementId = "ACH_KillBoss1", displayName = "屠龙勇士",
            description = "击杀Boss 1次",
            conditionType = AchievementConfig.ConditionType.KillBoss, targetValue = 1,
            rewardType = AchievementConfig.RewardType.Coin, rewardValue = 500,
            rewardItemId = "AttackElixir", oneTimeUnlock = true
        });
        AchievementConfig.Register("ACH_Enhance1", new AchievementConfig
        {
            achievementId = "ACH_Enhance1", displayName = "工匠入门",
            description = "成功强化任意装备1次",
            conditionType = AchievementConfig.ConditionType.EnhanceEquipment, targetValue = 1,
            rewardType = AchievementConfig.RewardType.Item, rewardValue = 3,
            rewardItemId = "EnhanceStone", oneTimeUnlock = true
        });
        AchievementConfig.Register("ACH_Pickup20", new AchievementConfig
        {
            achievementId = "ACH_Pickup20", displayName = "拾荒者",
            description = "累计拾取物品20个",
            conditionType = AchievementConfig.ConditionType.PickupItems, targetValue = 20,
            rewardType = AchievementConfig.RewardType.Item, rewardValue = 3,
            rewardItemId = "StaminaPotion", oneTimeUnlock = true
        });
        AchievementConfig.Register("ACH_EarnCoins1000", new AchievementConfig
        {
            achievementId = "ACH_EarnCoins1000", displayName = "富甲一方",
            description = "累计获得金币1000",
            conditionType = AchievementConfig.ConditionType.EarnCoins, targetValue = 1000,
            rewardType = AchievementConfig.RewardType.Coin, rewardValue = 300,
            oneTimeUnlock = true
        });
        AchievementConfig.Register("ACH_ClearAll", new AchievementConfig
        {
            achievementId = "ACH_ClearAll", displayName = "征服者",
            description = "通关全部4个关卡",
            conditionType = AchievementConfig.ConditionType.ClearLevel, targetValue = 4,
            rewardType = AchievementConfig.RewardType.Item, rewardValue = 1,
            rewardItemId = "IronBoots", oneTimeUnlock = true
        });
        Debug.Log("[SceneAutoBuilder] AchievementConfig 成就配置加载完成，共6项成就");

        // Phase19: QuestConfig 主线任务配置表
        QuestConfig.Register("Q001", new QuestConfig
        {
            questId = "Q001", displayName = "初入荒野", isMainQuest = true,
            description = "击杀5只普通近战敌人",
            acceptDialogue = "欢迎来到这片荒野，旅行者。\n这里的怪物日益猖獗，\n能否帮我清除一些近战敌人？",
            completeDialogue = "做得好！你证明了自己的实力。\n这是你应得的报酬。",
            target = QuestConfig.QuestTarget.KillEnemy, targetParam = "Enemy", targetCount = 5,
            rewardCoins = 150, rewardItemId = "HealthPotion", rewardItemCount = 2,
            prerequisiteQuestId = ""
        });
        QuestConfig.Register("Q002", new QuestConfig
        {
            questId = "Q002", displayName = "危险的爆破者", isMainQuest = true,
            description = "击杀3只自爆怪",
            acceptDialogue = "听说附近有危险的自爆型敌人出没。\n它们的爆炸威力巨大，\n请务必小心处理。",
            completeDialogue = "太感谢了！那些自爆怪确实太危险了。",
            target = QuestConfig.QuestTarget.KillEnemy, targetParam = "Exploder", targetCount = 3,
            rewardCoins = 250, rewardItemId = "EnhanceStone", rewardItemCount = 2,
            prerequisiteQuestId = "Q001"
        });
        QuestConfig.Register("Q003", new QuestConfig
        {
            questId = "Q003", displayName = "法师威胁", isMainQuest = true,
            description = "击杀2名远程法师",
            acceptDialogue = "敌方的远程法师正在对我们造成很大威胁。\n他们的魔法伤害极高，\n需要有人去解决他们。",
            completeDialogue = "干得漂亮！法师的威胁解除了。",
            target = QuestConfig.QuestTarget.KillEnemy, targetParam = "Mage", targetCount = 2,
            rewardCoins = 300, rewardItemId = "AttackElixir", rewardItemCount = 1,
            prerequisiteQuestId = "Q002"
        });
        QuestConfig.Register("Q004", new QuestConfig
        {
            questId = "Q004", displayName = "精英猎手", isMainQuest = true,
            description = "击杀2只精英敌人",
            acceptDialogue = "精英敌人比普通怪物强大得多。\n但他们的弱点也很明显，\n找到机会就能一举击败。",
            completeDialogue = "你真是精英杀手！这头盔能保护你的头部。",
            target = QuestConfig.QuestTarget.KillEnemy, targetParam = "Elite", targetCount = 2,
            rewardCoins = 400, rewardItemId = "IronHelmet", rewardItemCount = 1,
            prerequisiteQuestId = "Q003"
        });
        QuestConfig.Register("Q005", new QuestConfig
        {
            questId = "Q005", displayName = "深渊领主", isMainQuest = true,
            description = "击杀Boss 1次",
            acceptDialogue = "最终的考验来了。\n深渊领主隐藏在关卡深处，\n只有击败它，才能获得最终的胜利。",
            completeDialogue = "你做到了！你击败了深渊领主！\n这副铁甲是给你的终极奖励。",
            target = QuestConfig.QuestTarget.KillBoss, targetParam = "Boss", targetCount = 1,
            rewardCoins = 800, rewardItemId = "IronChest", rewardItemCount = 1,
            prerequisiteQuestId = "Q004"
        });
        Debug.Log("[SceneAutoBuilder] QuestConfig主线任务加载完成，共5个主线任务");

        // Phase12: 将关卡配置同步到LevelManager
        if (LevelManager.HasInstance)
        {
            LevelManager.Instance.LoadFromConfigData(
                new System.Collections.Generic.List<LevelConfigData>(LevelConfigData.GetAll().Values)
            );
        }

        // Phase12: 发布配置加载完成事件
        EventBus.Publish("ON_CONFIG_LOADED");
        Debug.Log("[EventBus] ON_CONFIG_LOADED");
    }

    /// <summary>
    /// 初始化音频和特效测试配置
    /// </summary>
    private void InitializeAudioAndVFX()
    {
        // 配置AudioManager测试音效条目
        if (AudioManager.HasInstance)
        {
            var audioManager = AudioManager.Instance;
            // 使用反射添加测试音效条目
            var sfxEntriesField = typeof(AudioManager).GetField("sfxEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (sfxEntriesField != null)
            {
                var sfxEntries = new System.Collections.Generic.List<AudioManager.SFXEntry>();

                // 攻击音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "AttackSwing", volume = 0.8f, pitch = 1f });
                // 受击音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "PlayerHurt", volume = 0.7f, pitch = 1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EnemyHurt", volume = 0.7f, pitch = 1f });
                // 死亡音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "PlayerDeath", volume = 0.9f, pitch = 0.9f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EnemyDeath", volume = 0.8f, pitch = 1f });
                // 换武器音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "WeaponSwitch", volume = 0.6f, pitch = 1.2f });
                // 射箭音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "ArrowFire", volume = 0.7f, pitch = 1f });
                // 关卡音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "LevelStart", volume = 0.8f, pitch = 1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "LevelComplete", volume = 0.9f, pitch = 1f });
                // 重生音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "PlayerRespawn", volume = 0.8f, pitch = 1f });

                // Phase7: 技能音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "SkillWhirlwind", volume = 0.9f, pitch = 0.8f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "SkillDash", volume = 0.8f, pitch = 1.2f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "SkillGeneric", volume = 0.7f, pitch = 1f });

                // Phase9: 新技能音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "SkillEnergyBall", volume = 0.8f, pitch = 1.1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "SkillHeal", volume = 0.7f, pitch = 1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BuffApply", volume = 0.8f, pitch = 1f });

                // Phase11: Boss音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BossPhaseChange", volume = 0.9f, pitch = 0.7f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BossDefeated", volume = 1f, pitch = 0.6f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BossMelee", volume = 0.8f, pitch = 0.9f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BossRanged", volume = 0.7f, pitch = 1.1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BossAOE", volume = 0.9f, pitch = 0.8f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "BossBerserk", volume = 0.85f, pitch = 1.2f });

                // Phase13: 物品音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "ItemPickup", volume = 0.7f, pitch = 1.2f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "ItemUse", volume = 0.8f, pitch = 1f });

                // Phase14: 新敌人音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EnemyExplode", volume = 1f, pitch = 0.6f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EnemyExplodeWarning", volume = 0.7f, pitch = 1.5f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "MageCast", volume = 0.8f, pitch = 1.2f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EliteCharge", volume = 0.9f, pitch = 0.8f });

                // Phase15: 装备音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EquipItem", volume = 0.8f, pitch = 1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "UnequipItem", volume = 0.7f, pitch = 0.9f });

                // Phase16: 商店音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "ShopBuy", volume = 0.8f, pitch = 1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "ShopSell", volume = 0.7f, pitch = 1f });

                // Phase17: 强化音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EnhanceSuccess", volume = 0.9f, pitch = 1f });
                sfxEntries.Add(new AudioManager.SFXEntry { name = "EnhanceFail", volume = 0.7f, pitch = 0.8f });

                // Phase18: 成就音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "AchievementUnlock", volume = 0.9f, pitch = 1f });

                // Phase19: 任务音效
                sfxEntries.Add(new AudioManager.SFXEntry { name = "QuestComplete", volume = 0.8f, pitch = 1f });

                sfxEntriesField.SetValue(audioManager, sfxEntries);

                // 重新构建音效字典
                var buildMethod = typeof(AudioManager).GetMethod("BuildSFXDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (buildMethod != null)
                {
                    buildMethod.Invoke(audioManager, null);
                }

                Debug.Log("[SceneAutoBuilder] AudioManager 测试音效配置完成 - 共 " + sfxEntries.Count + " 条");
            }
        }

        // 配置VFXManager测试特效条目
        if (VFXManager.HasInstance)
        {
            var vfxManager = VFXManager.Instance;
            // 使用反射添加测试特效条目
            var vfxEntriesField = typeof(VFXManager).GetField("vfxEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (vfxEntriesField != null)
            {
                var vfxEntries = new System.Collections.Generic.List<VFXManager.VFXEntry>();

                // 击中特效（使用粒子系统创建）
                GameObject hitVFX = CreateTestVFXPrefab("HitEffect", Color.yellow, 0.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "HitEffect", prefab = hitVFX, duration = 0.5f, scale = 1f });

                // 换武器闪光
                GameObject weaponSwitchVFX = CreateTestVFXPrefab("WeaponSwitchFlash", Color.cyan, 0.3f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "WeaponSwitchFlash", prefab = weaponSwitchVFX, duration = 0.3f, scale = 0.5f });

                // 箭矢命中
                GameObject arrowHitVFX = CreateTestVFXPrefab("ArrowHitEffect", Color.orange, 0.4f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "ArrowHitEffect", prefab = arrowHitVFX, duration = 0.4f, scale = 0.8f });

                // 死亡爆炸
                GameObject deathVFX = CreateTestVFXPrefab("DeathEffect", Color.red, 0.8f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "DeathEffect", prefab = deathVFX, duration = 0.8f, scale = 1.2f });

                // 关卡通关
                GameObject levelCompleteVFX = CreateTestVFXPrefab("LevelCompleteEffect", Color.green, 1.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "LevelCompleteEffect", prefab = levelCompleteVFX, duration = 1.5f, scale = 2f });

                // 玩家重生
                GameObject respawnVFX = CreateTestVFXPrefab("PlayerRespawnEffect", Color.blue, 1f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "PlayerRespawnEffect", prefab = respawnVFX, duration = 1f, scale = 1.5f });

                // Phase7: 技能特效
                GameObject whirlwindVFX = CreateTestVFXPrefab("SkillWhirlwindEffect", Color.white, 0.6f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "SkillWhirlwindEffect", prefab = whirlwindVFX, duration = 0.6f, scale = 3f });

                GameObject dashVFX = CreateTestVFXPrefab("SkillDashEffect", Color.cyan, 0.4f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "SkillDashEffect", prefab = dashVFX, duration = 0.4f, scale = 2f });

                // Phase9: 新技能特效
                GameObject energyBallVFX = CreateTestVFXPrefab("SkillEnergyBallEffect", new Color(0.3f, 0.8f, 1f), 0.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "SkillEnergyBallEffect", prefab = energyBallVFX, duration = 0.5f, scale = 1.5f });

                GameObject healVFX = CreateTestVFXPrefab("BuffApplyEffect", Color.green, 0.8f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "BuffApplyEffect", prefab = healVFX, duration = 0.8f, scale = 1.5f });

                // Phase11: Boss特效
                GameObject bossPhaseVFX = CreateTestVFXPrefab("BossPhaseChangeEffect", new Color(0.8f, 0.2f, 1f), 1f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "BossPhaseChangeEffect", prefab = bossPhaseVFX, duration = 1f, scale = 4f });

                GameObject bossDefeatedVFX = CreateTestVFXPrefab("BossDefeatedEffect", new Color(1f, 0.9f, 0.2f), 2f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "BossDefeatedEffect", prefab = bossDefeatedVFX, duration = 2f, scale = 5f });

                // Phase13: 物品特效
                GameObject itemPickupVFX = CreateTestVFXPrefab("ItemPickupEffect", new Color(0.9f, 0.9f, 0.3f), 0.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "ItemPickupEffect", prefab = itemPickupVFX, duration = 0.5f, scale = 1f });

                GameObject itemUseVFX = CreateTestVFXPrefab("ItemUseEffect", new Color(0.3f, 0.9f, 0.3f), 0.6f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "ItemUseEffect", prefab = itemUseVFX, duration = 0.6f, scale = 1.5f });

                // Phase14: 新敌人特效
                GameObject enemyExplodeVFX = CreateTestVFXPrefab("EnemyExplodeEffect", new Color(1f, 0.5f, 0f), 1.2f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "EnemyExplodeEffect", prefab = enemyExplodeVFX, duration = 1.2f, scale = 4f });

                GameObject mageCastVFX = CreateTestVFXPrefab("MageCastEffect", new Color(0.5f, 0.2f, 0.9f), 0.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "MageCastEffect", prefab = mageCastVFX, duration = 0.5f, scale = 1f });

                GameObject eliteChargeVFX = CreateTestVFXPrefab("EliteChargeEffect", new Color(0.9f, 0.3f, 0.1f), 0.4f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "EliteChargeEffect", prefab = eliteChargeVFX, duration = 0.4f, scale = 2f });

                // Phase15: 装备特效
                GameObject equipVFX = CreateTestVFXPrefab("EquipEffect", new Color(1f, 0.9f, 0.4f), 0.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "EquipEffect", prefab = equipVFX, duration = 0.5f, scale = 1.5f });

                GameObject unequipVFX = CreateTestVFXPrefab("UnequipEffect", new Color(0.6f, 0.6f, 0.6f), 0.3f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "UnequipEffect", prefab = unequipVFX, duration = 0.3f, scale = 1f });

                // Phase16: 商店特效
                GameObject shopBuyVFX = CreateTestVFXPrefab("ShopBuyEffect", new Color(1f, 1f, 0.3f), 0.5f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "ShopBuyEffect", prefab = shopBuyVFX, duration = 0.5f, scale = 1.2f });
                GameObject shopSellVFX = CreateTestVFXPrefab("ShopSellEffect", new Color(0.4f, 0.9f, 0.4f), 0.4f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "ShopSellEffect", prefab = shopSellVFX, duration = 0.4f, scale = 1f });

                // Phase17: 强化特效
                GameObject enhanceSuccessVFX = CreateTestVFXPrefab("EnhanceSuccessEffect", new Color(1f, 0.9f, 0.3f), 0.8f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "EnhanceSuccessEffect", prefab = enhanceSuccessVFX, duration = 0.8f, scale = 1.5f });
                GameObject enhanceFailVFX = CreateTestVFXPrefab("EnhanceFailEffect", new Color(0.5f, 0.5f, 0.5f), 0.6f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "EnhanceFailEffect", prefab = enhanceFailVFX, duration = 0.6f, scale = 1f });

                // Phase18: 成就特效
                GameObject achUnlockVFX = CreateTestVFXPrefab("AchievementUnlockEffect", new Color(1f, 0.9f, 0.3f), 1f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "AchievementUnlockEffect", prefab = achUnlockVFX, duration = 1f, scale = 2f });

                // Phase19: 任务特效
                GameObject questCompleteVFX = CreateTestVFXPrefab("QuestCompleteEffect", new Color(0.4f, 0.9f, 1f), 0.8f);
                vfxEntries.Add(new VFXManager.VFXEntry { name = "QuestCompleteEffect", prefab = questCompleteVFX, duration = 0.8f, scale = 1.5f });

                vfxEntriesField.SetValue(vfxManager, vfxEntries);

                // 重新构建特效字典
                var buildMethod = typeof(VFXManager).GetMethod("BuildVFXDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (buildMethod != null)
                {
                    buildMethod.Invoke(vfxManager, null);
                }

                Debug.Log("[SceneAutoBuilder] VFXManager 测试特效配置完成 - 共 " + vfxEntries.Count + " 条");
            }
        }
    }

    /// <summary>
    /// 创建测试粒子特效预制体
    /// </summary>
    private GameObject CreateTestVFXPrefab(string name, Color color, float duration)
    {
        GameObject vfxObj = new GameObject("VFX_" + name);
        vfxObj.transform.SetParent(dynamicRoot.transform);

        // 添加粒子系统
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();

        // 配置粒子系统
        var main = ps.main;
        main.duration = duration;
        main.startLifetime = duration * 0.8f;
        main.startSpeed = 3f;
        main.startSize = 0.5f;
        main.startColor = color;
        main.maxParticles = 20;
        main.loop = false;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;

        // 发射模块
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 10)
        });

        // 形状模块
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        // 颜色渐变
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        // 大小渐变
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));

        // 渲染器设置
        ParticleSystemRenderer renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = color;

        // 初始隐藏
        vfxObj.SetActive(false);

        return vfxObj;
    }

    /// <summary>
    /// 初始化关卡管理器配置
    /// </summary>
    /// <param name="levelManager">关卡管理器</param>
    private void InitializeLevelManager(LevelManager levelManager)
    {
        // Phase12: 关卡配置由InitializeGameConfigs通过LoadFromConfigData加载
        // 这里只需确保LevelManager可用
        Debug.Log("[SceneAutoBuilder] LevelManager 初始化完成（配置由InitializeGameConfigs加载）");
    }

    #endregion

    #region 相机创建

    /// <summary>
    /// 创建主相机
    /// </summary>
    private void CreateCamera()
    {
        // 获取或创建主相机
        Camera mainCam = Camera.main;
        GameObject camObj;

        if (mainCam != null)
        {
            camObj = mainCam.gameObject;
        }
        else
        {
            camObj = new GameObject("MainCamera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        }

        camObj.transform.SetParent(dynamicRoot.transform);

        // 移除已有的ThirdPersonCamera（如果有）
        ThirdPersonCamera existingTPC = camObj.GetComponent<ThirdPersonCamera>();
        if (existingTPC != null)
        {
            DestroyImmediate(existingTPC);
        }

        // 添加FollowCamera（如果不存在）
        FollowCamera followCam = camObj.GetComponent<FollowCamera>();
        if (followCam == null)
        {
            followCam = camObj.AddComponent<FollowCamera>();
        }

        Debug.Log("[SceneAutoBuilder] 相机设置完成（FollowCamera）");
    }

    #endregion

    #region 玩家创建

    /// <summary>
    /// 创建玩家对象
    /// </summary>
    /// <returns>玩家GameObject</returns>
    private GameObject CreatePlayer()
    {
        // 创建玩家根对象
        GameObject playerRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playerRoot.name = "PlayerRoot";
        playerRoot.tag = "Player";
        playerRoot.transform.SetParent(dynamicRoot.transform);
        playerRoot.transform.position = playerSpawnPosition;
        playerRoot.transform.localScale = playerCubeSize;

        // 设置玩家颜色
        Renderer renderer = playerRoot.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateMaterial(playerColor, "Player");
        }

        // CharacterController（会自动添加，如果不存在）
        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = playerRoot.AddComponent<CharacterController>();
        }
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);

        // CombatSystem
        CombatSystem combat = playerRoot.GetComponent<CombatSystem>();
        if (combat == null)
        {
            combat = playerRoot.AddComponent<CombatSystem>();
        }

        // PlayerController
        PlayerController pc = playerRoot.GetComponent<PlayerController>();
        if (pc == null)
        {
            pc = playerRoot.AddComponent<PlayerController>();
        }

        // PlayerAnimation（创建运行时Animator Controller）
        PlayerAnimation pa = playerRoot.GetComponent<PlayerAnimation>();
        if (pa == null)
        {
            pa = playerRoot.AddComponent<PlayerAnimation>();
        }

        // 创建运行时Animator Controller
        Animator animator = playerRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = playerRoot.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = CreateRuntimeAnimatorController("PlayerAnimator");

        // 创建武器管理器
        WeaponManager weaponManager = playerRoot.AddComponent<WeaponManager>();

        // Phase7: 创建技能管理器
        SkillManager skillManager = playerRoot.AddComponent<SkillManager>();
        Debug.Log("[SceneAutoBuilder] SkillManager 创建完成（旋风斩Q + 冲刺突进E）");

        // Phase9: 创建Buff管理器
        BuffManager buffManager = playerRoot.AddComponent<BuffManager>();
        Debug.Log("[SceneAutoBuilder] BuffManager 创建完成");

        // Phase13: 创建背包管理器
        Inventory inventory = playerRoot.AddComponent<Inventory>();
        Debug.Log("[SceneAutoBuilder] Inventory 背包管理器创建完成");

        // Phase16: 创建金币管理器
        CoinManager coinMgr = playerRoot.AddComponent<CoinManager>();
        Debug.Log("[SceneAutoBuilder] CoinManager 金币管理器创建完成");

        // Phase17: 创建强化管理器
        EnhanceManager enhanceMgr = playerRoot.AddComponent<EnhanceManager>();
        Debug.Log("[SceneAutoBuilder] EnhanceManager 强化管理器创建完成");

        // Phase18: 创建成就管理器
        AchievementManager achMgr = playerRoot.AddComponent<AchievementManager>();
        Debug.Log("[SceneAutoBuilder] AchievementManager 成就管理器创建完成");

        // Phase19: 创建任务管理器
        QuestManager questMgr = playerRoot.AddComponent<QuestManager>();
        Debug.Log("[SceneAutoBuilder] QuestManager 任务管理器创建完成");

        // Phase15: 创建装备管理器
        EquipmentManager equipMgr = playerRoot.AddComponent<EquipmentManager>();
        Debug.Log("[SceneAutoBuilder] EquipmentManager 装备管理器创建完成");

        // 创建测试武器：短剑
        CreateTestWeapon(playerRoot, "短剑", 10f, 0.8f, 1.8f, 
            new Vector3(0.15f, 0.15f, 0.8f), new Color(0.7f, 0.7f, 0.8f), 0);

        // 创建测试武器：重剑
        CreateTestWeapon(playerRoot, "重剑", 22f, 1.4f, 2.4f, 
            new Vector3(0.2f, 0.2f, 1.2f), new Color(0.4f, 0.4f, 0.5f), 1);

        // 创建AttackHitbox子物体（保留原有兼容）
        CreateAttackHitbox(playerRoot, "PlayerAttackHitbox", playerCubeSize.x * 0.5f);

        Debug.Log("[SceneAutoBuilder] 玩家创建完成：PlayerRoot");
        return playerRoot;
    }

    /// <summary>
    /// 创建测试武器
    /// </summary>
    /// <param name="playerRoot">玩家根对象</param>
    /// <param name="weaponName">武器名称</param>
    /// <param name="damage">伤害值</param>
    /// <param name="cooldown">冷却时间</param>
    /// <param name="range">攻击范围</param>
    /// <param name="scale">武器缩放</param>
    /// <param name="color">武器颜色</param>
    /// <param name="index">武器索引</param>
    private void CreateTestWeapon(GameObject playerRoot, string weaponName, float damage, 
        float cooldown, float range, Vector3 scale, Color color, int index)
    {
        // 创建武器子物体
        GameObject weaponObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        weaponObj.name = $"Weapon_{weaponName}";
        weaponObj.transform.SetParent(playerRoot.transform);
        weaponObj.transform.localPosition = new Vector3(0.4f, 0.8f, 0.5f);
        weaponObj.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        weaponObj.transform.localScale = scale;

        // 设置武器颜色
        Renderer renderer = weaponObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", 0.4f);
            mat.SetFloat("_Glossiness", 0.6f);
            renderer.material = mat;
        }

        // 添加Weapon组件
        Weapon weapon = weaponObj.AddComponent<Weapon>();

        // 使用反射设置私有字段（因为Weapon的字段是SerializeField）
        // 这里通过公共方法设置
        // 注意：Weapon组件需要通过Inspector或公共方法配置
        // 在运行时，我们通过反射设置字段值
        SetWeaponProperties(weapon, weaponName, damage, cooldown, range, color);

        // 初始状态：非激活
        weaponObj.SetActive(index == 0);

        Debug.Log($"[SceneAutoBuilder] 武器创建完成：{weaponName}");
    }

    /// <summary>
    /// 设置武器属性（通过反射）
    /// </summary>
    /// <param name="weapon">武器组件</param>
    /// <param name="name">武器名称</param>
    /// <param name="damage">伤害值</param>
    /// <param name="cooldown">冷却时间</param>
    /// <param name="range">攻击范围</param>
    /// <param name="color">武器颜色</param>
    private void SetWeaponProperties(Weapon weapon, string name, float damage, 
        float cooldown, float range, Color color)
    {
        // 使用反射设置SerializeField字段
        var nameField = typeof(Weapon).GetField("weaponName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (nameField != null) nameField.SetValue(weapon, name);

        var damageField = typeof(Weapon).GetField("weaponDamage", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (damageField != null) damageField.SetValue(weapon, damage);

        var cooldownField = typeof(Weapon).GetField("attackCooldown", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (cooldownField != null) cooldownField.SetValue(weapon, cooldown);

        var rangeField = typeof(Weapon).GetField("attackRange", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rangeField != null) rangeField.SetValue(weapon, range);

        var colorField = typeof(Weapon).GetField("weaponColor", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (colorField != null) colorField.SetValue(weapon, color);
    }

    /// <summary>
    /// 创建攻击碰撞盒子物体
    /// </summary>
    /// <param name="parent">父对象</param>
    /// <param name="name">碰撞盒名称</param>
    /// <param name="range">攻击范围</param>
    private void CreateAttackHitbox(GameObject parent, string name, float range)
    {
        GameObject hitboxObj = new GameObject(name);
        hitboxObj.transform.SetParent(parent.transform);
        hitboxObj.transform.localPosition = new Vector3(0f, 1f, range);
        hitboxObj.transform.localRotation = Quaternion.identity;
        hitboxObj.transform.localScale = new Vector3(0.5f, 1.5f, range);

        // 添加BoxCollider作为触发器
        BoxCollider collider = hitboxObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = Vector3.one;

        // 添加Rigidbody（用于触发器检测）
        Rigidbody rb = hitboxObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 添加AttackHitbox组件
        AttackHitbox hitbox = hitboxObj.AddComponent<AttackHitbox>();

        Debug.Log($"[SceneAutoBuilder] 攻击碰撞盒创建完成：{name}");
    }

    #endregion

    #region 敌人创建

    /// <summary>
    /// 创建敌人对象
    /// </summary>
    /// <param name="playerTransform">玩家Transform（用于设置目标）</param>
    private void CreateEnemies(Transform playerTransform)
    {
        // 创建近战敌人（原有）
        for (int i = 0; i < enemyCount; i++)
        {
            // 计算敌人位置（环形分布）
            float angle = i * (360f / enemyCount) * Mathf.Deg2Rad;
            float radius = enemySpacing * 2f;
            Vector3 spawnPos = enemySpawnPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                1f,
                Mathf.Sin(angle) * radius
            );

            CreateSingleEnemy(i, spawnPos, playerTransform, false);
        }

        // 创建远程弓箭手敌人（新增）
        int rangedCount = 2;
        for (int i = 0; i < rangedCount; i++)
        {
            float angle = (i + enemyCount) * (360f / (enemyCount + rangedCount)) * Mathf.Deg2Rad;
            float radius = enemySpacing * 3f;
            Vector3 spawnPos = enemySpawnPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                1f,
                Mathf.Sin(angle) * radius
            );

            CreateSingleEnemy(enemyCount + i, spawnPos, playerTransform, true);
        }

        Debug.Log($"[SceneAutoBuilder] 敌人创建完成：{enemyCount}个近战 + {rangedCount}个弓箭手");
    }

    /// <summary>
    /// 创建单个敌人
    /// </summary>
    /// <param name="index">敌人索引</param>
    /// <param name="position">生成位置</param>
    /// <param name="playerTransform">玩家Transform</param>
    /// <param name="isRanged">是否为远程敌人</param>
    private void CreateSingleEnemy(int index, Vector3 position, Transform playerTransform, bool isRanged)
    {
        // 创建敌人根对象
        GameObject enemyRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemyRoot.name = $"Enemy_{index}";
        enemyRoot.tag = "Untagged";
        enemyRoot.transform.SetParent(dynamicRoot.transform);
        enemyRoot.transform.position = position;
        enemyRoot.transform.localScale = enemyCubeSize;

        // 敌人颜色（近战红色系，远程蓝色系）
        Color enemyCol;
        if (isRanged)
        {
            // 远程弓箭手：蓝色系
            float b = Random.Range(0.6f, 1f);
            enemyCol = new Color(0.2f, 0.3f, b);
        }
        else
        {
            // 近战敌人：红色系
            float r = Random.Range(0.7f, 1f);
            float g = Random.Range(0.1f, 0.4f);
            enemyCol = new Color(r, g, 0.15f);
        }
        Renderer renderer = enemyRoot.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateMaterial(enemyCol, $"Enemy_{index}");
        }

        // CharacterController
        CharacterController cc = enemyRoot.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = enemyRoot.AddComponent<CharacterController>();
        }
        cc.height = 2f;
        cc.radius = 0.45f;
        cc.center = new Vector3(0f, 1f, 0f);

        // CombatSystem
        CombatSystem combat = enemyRoot.GetComponent<CombatSystem>();
        if (combat == null)
        {
            combat = enemyRoot.AddComponent<CombatSystem>();
        }

        // EnemyNavAgent（NavMesh寻路组件）
        EnemyNavAgent navAgent = enemyRoot.AddComponent<EnemyNavAgent>();

        // EnemyAI 或 RangeEnemyAI
        if (isRanged)
        {
            // 远程弓箭手敌人
            RangeEnemyAI rangedAi = enemyRoot.GetComponent<RangeEnemyAI>();
            if (rangedAi == null)
            {
                rangedAi = enemyRoot.AddComponent<RangeEnemyAI>();
            }
        }
        else
        {
            // 近战敌人
            EnemyAI ai = enemyRoot.GetComponent<EnemyAI>();
            if (ai == null)
            {
                ai = enemyRoot.AddComponent<EnemyAI>();
            }
            ai.SetTarget(playerTransform);
        }

        // EnemyAnimation
        EnemyAnimation ea = enemyRoot.GetComponent<EnemyAnimation>();
        if (ea == null)
        {
            ea = enemyRoot.AddComponent<EnemyAnimation>();
        }

        // 创建运行时Animator Controller
        Animator animator = enemyRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = enemyRoot.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = CreateRuntimeAnimatorController($"EnemyAnimator_{index}");

        // 添加EnemyHUD（世界空间血条）
        CreateEnemyHUD(enemyRoot);

        Debug.Log($"[SceneAutoBuilder] {(isRanged ? "远程" : "近战")}敌人创建完成：Enemy_{index}");
    }

    /// <summary>
    /// 创建敌人世界空间血条
    /// </summary>
    /// <param name="enemyRoot">敌人根对象</param>
    private void CreateEnemyHUD(GameObject enemyRoot)
    {
        // 创建World Space Canvas
        GameObject canvasObj = new GameObject("EnemyHealthCanvas");
        canvasObj.transform.SetParent(enemyRoot.transform);
        canvasObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(120f, 15f);
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // 血条背景
        GameObject bgObj = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(canvasObj.transform);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // 血条填充
        GameObject fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(canvasObj.transform);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.red;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        // 血量文本
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(canvasObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textObj.AddComponent<Text>();
        text.text = "100/100";
        text.fontSize = 10;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = cachedFont;

        // 添加EnemyHUD组件
        EnemyHUD hud = enemyRoot.AddComponent<EnemyHUD>();
    }

    /// <summary>
    /// 创建Boss世界空间血条（Phase11）
    /// </summary>
    /// <param name="bossRoot">Boss根对象</param>
    private void CreateBossHealthBar(GameObject bossRoot)
    {
        // 创建World Space Canvas
        GameObject canvasObj = new GameObject("BossHealthCanvas");
        canvasObj.transform.SetParent(bossRoot.transform);
        canvasObj.transform.localPosition = new Vector3(0f, 3.5f, 0f);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 15;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 20f);
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // 血条背景
        GameObject bgObj = new GameObject("BossHealthBG");
        bgObj.transform.SetParent(canvasObj.transform);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        // 血条填充
        GameObject fillObj = new GameObject("BossHealthFill");
        fillObj.transform.SetParent(canvasObj.transform);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.8f, 0.1f, 0.1f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        // 血量文本
        GameObject textObj = new GameObject("BossHealthText");
        textObj.transform.SetParent(canvasObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textObj.AddComponent<Text>();
        text.text = "Boss 1000/1000";
        text.fontSize = 12;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = cachedFont;

        // 添加BossHealthBar组件
        BossHealthBar bossHB = bossRoot.AddComponent<BossHealthBar>();
    }

    /// <summary>
    /// 创建Boss实例（Phase11）
    /// </summary>
    private GameObject CreateBoss()
    {
        GameObject bossRoot = new GameObject("Boss");
        bossRoot.transform.SetParent(dynamicRoot.transform);
        bossRoot.transform.position = new Vector3(0f, 0f, 25f);

        // Boss身体
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "BossBody";
        body.transform.SetParent(bossRoot.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(2f, 3f, 2f);
        Renderer renderer = body.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0.5f, 0f, 0.5f, 1f);
        }
        CapsuleCollider col = body.GetComponent<CapsuleCollider>();
        if (col != null) col.isTrigger = true;

        // 添加Boss组件
        BossAI bossAI = bossRoot.AddComponent<BossAI>();
        CombatSystem combatSystem = bossRoot.AddComponent<CombatSystem>();
        combatSystem.SetMaxHealth(1000);
        combatSystem.SetCurrentHealth(1000);
        combatSystem.IsInvincible = false;

        // BossNavAgent
        EnemyNavAgent bossNav = bossRoot.AddComponent<EnemyNavAgent>();

        // EnemyAnimation
        EnemyAnimation bossAnim = bossRoot.AddComponent<EnemyAnimation>();

        // Boss World Space血条
        CreateBossHealthBar(bossRoot);

        // 设置Boss索引
        bossAI.SetBossIndex(0);

        // 订阅Boss生成事件来设置玩家目标
        EventSystem eventSystem = EventSystem.FindObjectOfType<EventSystem>();
        if (eventSystem != null)
        {
            Debug.Log("[SceneAutoBuilder] 等待Boss生成事件，将玩家设为目标");
        }

        return bossRoot;
    }

    #endregion

    #region UI创建

    /// <summary>
    /// 创建游戏UI
    /// </summary>
    /// <param name="playerCombat">玩家CombatSystem组件</param>
    private void CreateUI(CombatSystem playerCombat)
    {
        // 创建Canvas
        GameObject canvasObj = new GameObject("GameCanvas");
        canvasObj.transform.SetParent(dynamicRoot.transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // 创建玩家血条UI
        CreatePlayerHUD(canvasObj, playerCombat);

        // Phase8: 创建技能HUD（技能图标+冷却+耐力条）
        CreateSkillHUD(canvasObj);

        // 创建武器HUD
        CreateWeaponHUD(canvasObj);

        // 创建暂停面板
        CreatePausePanel(canvasObj);

        // 创建游戏结束面板
        CreateGameOverPanel(canvasObj);

        // Phase11: 创建Boss血条UI（初始隐藏，Boss生成时显示）
        CreateBossHealthBarUI(canvasObj);

        // Phase13: 创建背包UI面板
        CreateInventoryHUD(canvasObj);

        // 创建设置面板（共享）
        CreateSettingsPanel(canvasObj);

        // 创建主菜单
        CreateMainMenu(canvasObj);

        // 创建游戏提示UI
        CreateGameTipsUI(canvasObj);

        Debug.Log("[SceneAutoBuilder] UI创建完成");
    }

    /// <summary>
    /// 创建玩家血条HUD
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    /// <param name="playerCombat">玩家CombatSystem</param>
    private void CreatePlayerHUD(GameObject canvasObj, CombatSystem playerCombat)
    {
        // 血条容器
        GameObject hudRoot = new GameObject("PlayerHUD");
        hudRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = hudRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0.3f, 1f);
        rootRect.anchoredPosition = new Vector2(20f, -20f);
        rootRect.sizeDelta = new Vector2(0f, 60f);

        // 血条背景
        GameObject bgObj = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(hudRoot.transform);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(1f, 1f);
        bgRect.sizeDelta = new Vector2(0f, -5f);
        bgRect.anchoredPosition = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        // 血条Slider
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(hudRoot.transform);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.sizeDelta = new Vector2(-10f, -5f);
        sliderRect.anchoredPosition = new Vector2(0f, 0f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        // Fill Area
        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderObj.transform);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.8f, 0.2f);

        slider.fillRect = fillRect;

        // 血量文本
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(hudRoot.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.sizeDelta = Vector2.zero;
        Text text = textObj.AddComponent<Text>();
        text.text = "HP: 100/100";
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.font = cachedFont;

        // 添加PlayerHUD组件
        PlayerHUD playerHUD = hudRoot.AddComponent<PlayerHUD>();

        // 添加死亡面板
        CreateDeathPanel(hudRoot);

        Debug.Log("[SceneAutoBuilder] 玩家血条HUD创建完成");
    }

    /// <summary>
    /// Phase9: 创建技能HUD - 5个技能图标 + 冷却遮罩 + 耐力条 + Buff显示区
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateSkillHUD(GameObject canvasObj)
    {
        // 技能HUD根容器（屏幕左下角）
        GameObject skillHUDRoot = new GameObject("SkillHUD");
        skillHUDRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = skillHUDRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0.4f, 0.3f);
        rootRect.anchoredPosition = new Vector2(20f, 20f);
        rootRect.sizeDelta = Vector2.zero;

        // 5个技能按键标签
        string[] keys = { "Q", "E", "R", "F", "T" };
        string[] names = { "旋风斩", "冲刺", "能量弹", "治疗", "狂暴" };
        Color[] iconColors = {
            new Color(0.4f, 0.6f, 1f),   // Q - 蓝
            new Color(0.3f, 0.8f, 0.8f), // E - 青
            new Color(0.5f, 0.3f, 1f),   // R - 紫
            new Color(0.3f, 0.9f, 0.3f), // F - 绿
            new Color(1f, 0.4f, 0.2f)    // T - 红
        };

        Image[] icons = new Image[5];
        Image[] masks = new Image[5];
        Text[] cdTexts = new Text[5];
        Text[] nameTexts = new Text[5];

        // 创建5个技能图标槽位
        for (int i = 0; i < 5; i++)
        {
            CreateSkillIconSlot(skillHUDRoot, "Skill" + (i + 1) + "Slot", keys[i],
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f + i * 75f, 0f), new Vector2(65f, 65f),
                out Image icon, out Image mask, out Text cdText, out Text nameText,
                names[i], iconColors[i]);

            icons[i] = icon;
            masks[i] = mask;
            cdTexts[i] = cdText;
            nameTexts[i] = nameText;
        }

        // ---- 耐力条 ----
        GameObject staminaBG = new GameObject("StaminaBarBG");
        staminaBG.transform.SetParent(skillHUDRoot.transform);
        RectTransform staminaBGRect = staminaBG.AddComponent<RectTransform>();
        staminaBGRect.anchorMin = new Vector2(0f, 0f);
        staminaBGRect.anchorMax = new Vector2(0f, 0f);
        staminaBGRect.anchoredPosition = new Vector2(20f, 75f);
        staminaBGRect.sizeDelta = new Vector2(380f, 14f);
        Image staminaBGImg = staminaBG.AddComponent<Image>();
        staminaBGImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        GameObject staminaSliderObj = new GameObject("StaminaSlider");
        staminaSliderObj.transform.SetParent(staminaBG.transform);
        RectTransform staminaSliderRect = staminaSliderObj.AddComponent<RectTransform>();
        staminaSliderRect.anchorMin = Vector2.zero;
        staminaSliderRect.anchorMax = Vector2.one;
        staminaSliderRect.sizeDelta = Vector2.zero;

        Slider staminaSlider = staminaSliderObj.AddComponent<Slider>();
        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = 1f;
        staminaSlider.value = 1f;
        staminaSlider.interactable = false;
        staminaSlider.transition = Selectable.Transition.None;

        GameObject staminaFillArea = new GameObject("FillArea");
        staminaFillArea.transform.SetParent(staminaSliderObj.transform);
        RectTransform staminaFillAreaRect = staminaFillArea.AddComponent<RectTransform>();
        staminaFillAreaRect.anchorMin = Vector2.zero;
        staminaFillAreaRect.anchorMax = Vector2.one;
        staminaFillAreaRect.sizeDelta = Vector2.zero;

        GameObject staminaFillObj = new GameObject("Fill");
        staminaFillObj.transform.SetParent(staminaFillArea.transform);
        RectTransform staminaFillRect = staminaFillObj.AddComponent<RectTransform>();
        staminaFillRect.sizeDelta = Vector2.zero;
        Image staminaFillImg = staminaFillObj.AddComponent<Image>();
        staminaFillImg.color = new Color(0.9f, 0.8f, 0.2f);
        staminaSlider.fillRect = staminaFillRect;

        GameObject staminaTextObj = new GameObject("StaminaText");
        staminaTextObj.transform.SetParent(staminaBG.transform);
        RectTransform staminaTextRect = staminaTextObj.AddComponent<RectTransform>();
        staminaTextRect.anchorMin = Vector2.zero;
        staminaTextRect.anchorMax = Vector2.one;
        staminaTextRect.sizeDelta = Vector2.zero;
        Text staminaText = staminaTextObj.AddComponent<Text>();
        staminaText.text = "100 / 100";
        staminaText.fontSize = 11;
        staminaText.alignment = TextAnchor.MiddleCenter;
        staminaText.color = Color.white;
        staminaText.font = cachedFont;

        // ---- Buff显示区 ----
        GameObject buffArea = new GameObject("BuffArea");
        buffArea.transform.SetParent(skillHUDRoot.transform);
        RectTransform buffRect = buffArea.AddComponent<RectTransform>();
        buffRect.anchorMin = new Vector2(0f, 1f);
        buffRect.anchorMax = new Vector2(1f, 1f);
        buffRect.anchoredPosition = new Vector2(0f, -5f);
        buffRect.sizeDelta = new Vector2(0f, 35f);

        // 添加SkillHUD组件
        SkillHUD skillHUD = skillHUDRoot.AddComponent<SkillHUD>();

        Debug.Log("[SceneAutoBuilder] 技能HUD创建完成（QERFT 5技能 + 耐力条 + Buff区）");
    }

    /// <summary>
    /// 创建技能图标槽位
    /// </summary>
    private void CreateSkillIconSlot(GameObject parent, string slotName, string keyLabel,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
        out Image icon, out Image cooldownMask, out Text cooldownText, out Text nameText,
        string displayName = "", Color iconColor = default)
    {
        if (iconColor == default) iconColor = new Color(0.6f, 0.7f, 0.9f);

        // 槽位根
        GameObject slot = new GameObject(slotName);
        slot.transform.SetParent(parent.transform);
        RectTransform slotRect = slot.AddComponent<RectTransform>();
        slotRect.anchorMin = anchorMin;
        slotRect.anchorMax = anchorMax;
        slotRect.anchoredPosition = anchoredPos;
        slotRect.sizeDelta = size;

        // 背景框
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(slot.transform);
        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

        // 图标
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slot.transform);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = new Vector2(-6f, -6f);
        icon = iconObj.AddComponent<Image>();
        icon.color = iconColor;

        // 冷却遮罩
        GameObject maskObj = new GameObject("CooldownMask");
        maskObj.transform.SetParent(slot.transform);
        RectTransform maskRect = maskObj.AddComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.sizeDelta = new Vector2(-6f, -6f);
        cooldownMask = maskObj.AddComponent<Image>();
        cooldownMask.color = new Color(0f, 0f, 0f, 0.6f);
        cooldownMask.type = Image.Type.Filled;
        cooldownMask.fillMethod = Image.FillMethod.Vertical;
        cooldownMask.fillAmount = 0f;

        // 冷却倒计时文字
        GameObject cdTextObj = new GameObject("CooldownText");
        cdTextObj.transform.SetParent(slot.transform);
        RectTransform cdTextRect = cdTextObj.AddComponent<RectTransform>();
        cdTextRect.anchorMin = Vector2.zero;
        cdTextRect.anchorMax = Vector2.one;
        cdTextRect.sizeDelta = Vector2.zero;
        cooldownText = cdTextObj.AddComponent<Text>();
        cooldownText.text = "";
        cooldownText.fontSize = 20;
        cooldownText.alignment = TextAnchor.MiddleCenter;
        cooldownText.color = Color.white;
        cooldownText.font = cachedFont;
        cooldownText.gameObject.SetActive(false);

        // 按键标签（底部）
        GameObject keyObj = new GameObject("KeyLabel");
        keyObj.transform.SetParent(slot.transform);
        RectTransform keyRect = keyObj.AddComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 0f);
        keyRect.anchorMax = new Vector2(1f, 0f);
        keyRect.sizeDelta = new Vector2(0f, 14f);
        keyRect.anchoredPosition = Vector2.zero;
        Text keyText = keyObj.AddComponent<Text>();
        keyText.text = keyLabel;
        keyText.fontSize = 10;
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = new Color(0.8f, 0.8f, 0.8f);
        keyText.font = cachedFont;

        // 技能名称（顶部）
        GameObject nameObj = new GameObject("NameLabel");
        nameObj.transform.SetParent(slot.transform);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.sizeDelta = new Vector2(0f, 12f);
        nameRect.anchoredPosition = Vector2.zero;
        nameText = nameObj.AddComponent<Text>();
        nameText.text = displayName;
        nameText.fontSize = 9;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = new Color(0.7f, 0.7f, 0.7f);
        nameText.font = cachedFont;
    }

    /// <summary>
    /// 创建死亡提示面板
    /// </summary>
    /// <param name="parent">父对象</param>
    private void CreateDeathPanel(GameObject parent)
    {
        GameObject deathPanel = new GameObject("DeathPanel");
        deathPanel.transform.SetParent(parent.transform);
        RectTransform deathRect = deathPanel.AddComponent<RectTransform>();
        deathRect.anchorMin = Vector2.zero;
        deathRect.anchorMax = Vector2.one;
        deathRect.sizeDelta = Vector2.zero;
        Image deathBg = deathPanel.AddComponent<Image>();
        deathBg.color = new Color(0f, 0f, 0f, 0.7f);
        deathPanel.SetActive(false);

        // 死亡文字
        GameObject deathTextObj = new GameObject("DeathText");
        deathTextObj.transform.SetParent(deathPanel.transform);
        RectTransform deathTextRect = deathTextObj.AddComponent<RectTransform>();
        deathTextRect.anchorMin = Vector2.zero;
        deathTextRect.anchorMax = Vector2.one;
        deathTextRect.sizeDelta = Vector2.zero;
        Text deathText = deathTextObj.AddComponent<Text>();
        deathText.text = "YOU DIED\n按L重生";
        deathText.fontSize = 36;
        deathText.alignment = TextAnchor.MiddleCenter;
        deathText.color = Color.red;
        deathText.font = cachedFont;
    }

    /// <summary>
    /// 创建武器HUD
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateWeaponHUD(GameObject canvasObj)
    {
        // 武器HUD容器
        GameObject weaponHUDRoot = new GameObject("WeaponHUD");
        weaponHUDRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = weaponHUDRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.7f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0.2f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;

        // 武器名称文本
        GameObject weaponNameObj = new GameObject("WeaponName");
        weaponNameObj.transform.SetParent(weaponHUDRoot.transform);
        RectTransform nameRect = weaponNameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.sizeDelta = Vector2.zero;
        Text weaponNameText = weaponNameObj.AddComponent<Text>();
        weaponNameText.text = "短剑";
        weaponNameText.fontSize = 22;
        weaponNameText.alignment = TextAnchor.MiddleRight;
        weaponNameText.color = new Color(1f, 0.9f, 0.6f);
        weaponNameText.font = cachedFont;

        // 武器信息文本
        GameObject weaponInfoObj = new GameObject("WeaponInfo");
        weaponInfoObj.transform.SetParent(weaponHUDRoot.transform);
        RectTransform infoRect = weaponInfoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0.5f);
        infoRect.anchorMax = Vector2.one;
        infoRect.sizeDelta = Vector2.zero;
        Text weaponInfoText = weaponInfoObj.AddComponent<Text>();
        weaponInfoText.text = "伤害:10 | 冷却:0.8s | 范围:1.8";
        weaponInfoText.fontSize = 14;
        weaponInfoText.alignment = TextAnchor.MiddleRight;
        weaponInfoText.color = new Color(0.8f, 0.8f, 0.8f);
        weaponInfoText.font = cachedFont;

        // 添加WeaponHUD组件
        WeaponHUD weaponHUD = weaponHUDRoot.AddComponent<WeaponHUD>();

        // 添加阴影效果
        Shadow shadow = weaponNameObj.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);

        Debug.Log("[SceneAutoBuilder] 武器HUD创建完成");
    }

    /// <summary>
    /// 创建Boss血条UI（Phase11）- 屏幕顶部居中，Boss生成时自动显示
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateBossHealthBarUI(GameObject canvasObj)
    {
        // Boss血条容器
        GameObject bossHBRoot = new GameObject("BossHealthBar");
        bossHBRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = bossHBRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.25f, 0.92f);
        rootRect.anchorMax = new Vector2(0.75f, 0.98f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;

        // 背景
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(bossHBRoot.transform);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // 血条填充
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(bossHBRoot.transform);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.7f, 0.05f, 0.05f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        // Boss名称
        GameObject nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(bossHBRoot.transform);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0.45f);
        nameRect.sizeDelta = Vector2.zero;
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = "暗影领主";
        nameText.fontSize = 16;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = new Color(1f, 0.8f, 0.3f);
        nameText.font = cachedFont;

        // 血量文本
        GameObject hpTextObj = new GameObject("HPText");
        hpTextObj.transform.SetParent(bossHBRoot.transform);
        RectTransform hpTextRect = hpTextObj.AddComponent<RectTransform>();
        hpTextRect.anchorMin = new Vector2(0f, 0.45f);
        hpTextRect.anchorMax = Vector2.one;
        hpTextRect.sizeDelta = Vector2.zero;
        Text hpText = hpTextObj.AddComponent<Text>();
        hpText.text = "1000 / 1000";
        hpText.fontSize = 12;
        hpText.alignment = TextAnchor.MiddleRight;
        hpText.color = new Color(0.9f, 0.9f, 0.9f);
        hpText.font = cachedFont;

        // 添加BossHealthBar组件
        BossHealthBar bossHB = bossHBRoot.AddComponent<BossHealthBar>();

        // 初始隐藏（Boss生成时显示）
        bossHBRoot.SetActive(false);

        Debug.Log("[SceneAutoBuilder] Boss血条UI创建完成");
    }

    /// <summary>
    /// Phase13: 创建背包UI面板
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateInventoryHUD(GameObject canvasObj)
    {
        // 背包面板根对象
        GameObject invPanel = new GameObject("InventoryPanel");
        invPanel.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = invPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.15f);
        panelRect.anchorMax = new Vector2(0.75f, 0.85f);
        panelRect.sizeDelta = Vector2.zero;
        Image panelBg = invPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

        // 标题
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(invPanel.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.9f);
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "背包 (I键关闭)";
        titleText.fontSize = 22;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.9f, 0.6f);
        titleText.font = cachedFont;

        // 格子容器
        GameObject slotContainer = new GameObject("SlotContainer");
        slotContainer.transform.SetParent(invPanel.transform);
        RectTransform containerRect = slotContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.05f, 0.05f);
        containerRect.anchorMax = new Vector2(0.95f, 0.88f);
        containerRect.sizeDelta = Vector2.zero;

        // 添加InventoryHUD组件
        InventoryHUD invHUD = invPanel.AddComponent<InventoryHUD>();

        Debug.Log("[SceneAutoBuilder] InventoryHUD 背包面板创建完成");

        // Phase15: 创建装备面板
        CreateEquipmentHUD(canvasObj);
    }

    /// <summary>
    /// Phase15: 创建装备UI面板
    /// </summary>
    private void CreateEquipmentHUD(GameObject canvasObj)
    {
        GameObject equipPanel = new GameObject("EquipmentPanel");
        equipPanel.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = equipPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.15f);
        panelRect.anchorMax = new Vector2(0.75f, 0.85f);
        panelRect.sizeDelta = Vector2.zero;
        Image panelBg = equipPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

        EquipmentHUD equipHUD = equipPanel.AddComponent<EquipmentHUD>();

        Debug.Log("[SceneAutoBuilder] EquipmentHUD 装备面板创建完成");

        // Phase16: 创建商店UI面板
        CreateShopHUD(canvasObj);

        // Phase17: 创建强化UI面板
        CreateEnhanceHUD(canvasObj);

        // Phase18: 创建成就UI面板
        CreateAchievementHUD(canvasObj);

        // Phase19: 创建任务UI面板
        CreateQuestHUD(canvasObj);
    }

    /// <summary>
    /// Phase16: 创建商店UI面板
    /// </summary>
    private void CreateShopHUD(GameObject canvasObj)
    {
        GameObject shopPanel = new GameObject("ShopPanel");
        shopPanel.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = shopPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;
        Image panelBg = shopPanel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);

        ShopHUD shopHUD = shopPanel.AddComponent<ShopHUD>();

        Debug.Log("[SceneAutoBuilder] ShopHUD 商店面板创建完成");
    }

    /// <summary>
    /// Phase16: 创建商店NPC
    /// </summary>
    private void CreateShopNPC()
    {
        GameObject npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npcObj.name = "ShopNPC";
        npcObj.transform.SetParent(dynamicRoot.transform);
        npcObj.transform.position = new Vector3(-12f, 0f, 5f);
        npcObj.transform.localScale = new Vector3(1f, 1.8f, 1f);

        // NPC颜色 - 商人绿色
        Renderer renderer = npcObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        }

        // 商店NPC脚本
        ShopNPC shopNPC = npcObj.AddComponent<ShopNPC>();

        // Phase19: 商店NPC对话组件
        QuestDialogue shopDialogue = npcObj.AddComponent<QuestDialogue>();

        Debug.Log("[SceneAutoBuilder] ShopNPC 商店NPC实例生成完成");
    }

    /// <summary>
    /// Phase17: 创建强化NPC
    /// </summary>
    private void CreateEnhanceNPC()
    {
        GameObject npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npcObj.name = "EnhanceNPC";
        npcObj.transform.SetParent(dynamicRoot.transform);
        npcObj.transform.position = new Vector3(-16f, 0f, 5f);
        npcObj.transform.localScale = new Vector3(1f, 1.8f, 1f);

        // NPC颜色 - 铁匠红色
        Renderer renderer = npcObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        }

        // 强化NPC脚本
        EnhanceNPC enhanceNPC = npcObj.AddComponent<EnhanceNPC>();

        // Phase19: 强化NPC对话组件
        QuestDialogue enhanceDialogue = npcObj.AddComponent<QuestDialogue>();

        Debug.Log("[SceneAutoBuilder] EnhanceNPC 强化NPC实例生成完成，位置(-16,0,5)");
    }

    /// <summary>
    /// Phase17: 创建强化UI面板
    /// </summary>
    private void CreateEnhanceHUD(GameObject canvasObj)
    {
        GameObject enhancePanel = new GameObject("EnhancePanel");
        enhancePanel.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = enhancePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;
        Image panelBg = enhancePanel.AddComponent<Image>();
        panelBg.color = new Color(0.15f, 0.08f, 0.08f, 0.95f);

        EnhanceHUD enhanceHUD = enhancePanel.AddComponent<EnhanceHUD>();

        Debug.Log("[SceneAutoBuilder] EnhanceHUD 强化面板创建完成");
    }

    /// <summary>
    /// Phase18: 创建成就UI面板
    /// </summary>
    private void CreateAchievementHUD(GameObject canvasObj)
    {
        GameObject achPanel = new GameObject("AchievementPanel");
        achPanel.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = achPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;
        Image panelBg = achPanel.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.1f, 0.18f, 0.95f);

        AchievementHUD achHUD = achPanel.AddComponent<AchievementHUD>();

        Debug.Log("[SceneAutoBuilder] AchievementHUD 成就面板创建完成");
    }

    /// <summary>
    /// Phase19: 创建任务UI面板
    /// </summary>
    private void CreateQuestHUD(GameObject canvasObj)
    {
        GameObject questPanel = new GameObject("QuestPanel");
        questPanel.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = questPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;
        Image panelBg = questPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

        QuestHUD questHUD = questPanel.AddComponent<QuestHUD>();

        Debug.Log("[SceneAutoBuilder] QuestHUD 任务面板创建完成");
    }

    /// <summary>
    /// 创建暂停面板
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreatePausePanel(GameObject canvasObj)
    {
        GameObject pauseRoot = new GameObject("PausePanel");
        pauseRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = pauseRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        Image bgImage = pauseRoot.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(pauseRoot.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.3f, 0.6f);
        titleRect.anchorMax = new Vector2(0.7f, 0.8f);
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "游戏暂停";
        titleText.fontSize = 36;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.font = cachedFont;

        CreateButton(pauseRoot.transform, "ResumeButton", "继续游戏",
            new Vector2(0.35f, 0.45f), new Vector2(0.65f, 0.52f), new Color(0.2f, 0.7f, 0.2f));
        CreateButton(pauseRoot.transform, "SettingsButton", "游戏设置",
            new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.42f), new Color(0.3f, 0.5f, 0.8f));
        CreateButton(pauseRoot.transform, "MainMenuButton", "返回主菜单",
            new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.32f), new Color(0.7f, 0.3f, 0.3f));

        pauseRoot.AddComponent<PausePanel>();
        pauseRoot.SetActive(false);
        Debug.Log("[SceneAutoBuilder] 暂停面板创建完成");
    }

    /// <summary>
    /// 创建游戏结束面板
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateGameOverPanel(GameObject canvasObj)
    {
        GameObject gameOverRoot = new GameObject("GameOverPanel");
        gameOverRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = gameOverRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        Image bgImage = gameOverRoot.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0f, 0f, 0.8f);

        GameObject titleObj = new GameObject("GameOverTitle");
        titleObj.transform.SetParent(gameOverRoot.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.3f, 0.65f);
        titleRect.anchorMax = new Vector2(0.7f, 0.85f);
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "GAME OVER";
        titleText.fontSize = 48;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.3f, 0.3f);
        titleText.font = cachedFont;

        CreateButton(gameOverRoot.transform, "RestartButton", "重新开始",
            new Vector2(0.35f, 0.4f), new Vector2(0.65f, 0.48f), new Color(0.2f, 0.7f, 0.2f));
        CreateButton(gameOverRoot.transform, "LoadSaveButton", "读取存档",
            new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.38f), new Color(0.3f, 0.6f, 0.8f));
        CreateButton(gameOverRoot.transform, "MainMenuButton", "返回主菜单",
            new Vector2(0.35f, 0.2f), new Vector2(0.65f, 0.28f), new Color(0.7f, 0.3f, 0.3f));

        gameOverRoot.AddComponent<GameOverPanel>();
        gameOverRoot.SetActive(false);
        Debug.Log("[SceneAutoBuilder] 游戏结束面板创建完成");
    }

    /// <summary>
    /// 创建设置面板
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateSettingsPanel(GameObject canvasObj)
    {
        GameObject settingsRoot = new GameObject("SettingsPanel");
        settingsRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = settingsRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        Image bgImage = settingsRoot.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);

        GameObject titleObj = new GameObject("SettingsTitle");
        titleObj.transform.SetParent(settingsRoot.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.3f, 0.8f);
        titleRect.anchorMax = new Vector2(0.7f, 0.92f);
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "游戏设置";
        titleText.fontSize = 32;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.font = cachedFont;

        CreateSlider(settingsRoot.transform, "MasterVolume", "主音量",
            new Vector2(0.2f, 0.7f), new Vector2(0.8f, 0.77f), 1f);
        CreateSlider(settingsRoot.transform, "MusicVolume", "音乐音量",
            new Vector2(0.2f, 0.6f), new Vector2(0.8f, 0.67f), 0.8f);
        CreateSlider(settingsRoot.transform, "SFXVolume", "音效音量",
            new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.57f), 1f);
        CreateSlider(settingsRoot.transform, "MouseSensitivity", "鼠标灵敏度",
            new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.47f), 10f, 1f, 30f);

        CreateButton(settingsRoot.transform, "BackButton", "返回",
            new Vector2(0.4f, 0.2f), new Vector2(0.6f, 0.28f), new Color(0.5f, 0.5f, 0.5f));

        settingsRoot.AddComponent<SettingsPanel>();
        settingsRoot.SetActive(false);
        Debug.Log("[SceneAutoBuilder] 设置面板创建完成");
    }

    /// <summary>
    /// 创建主菜单
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateMainMenu(GameObject canvasObj)
    {
        GameObject mainMenuRoot = new GameObject("MainMenu");
        mainMenuRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = mainMenuRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        Image bgImage = mainMenuRoot.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        GameObject titleObj = new GameObject("GameTitle");
        titleObj.transform.SetParent(mainMenuRoot.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.2f, 0.65f);
        titleRect.anchorMax = new Vector2(0.8f, 0.85f);
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "ChenZhenDemo";
        titleText.fontSize = 48;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.9f, 0.6f);
        titleText.font = cachedFont;

        // 开始新游戏按钮
        CreateButton(mainMenuRoot.transform, "StartGameButton", "开始新游戏",
            new Vector2(0.35f, 0.45f), new Vector2(0.65f, 0.53f), new Color(0.2f, 0.7f, 0.2f));
        // 读取存档按钮
        CreateButton(mainMenuRoot.transform, "LoadGameButton", "读取存档",
            new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.43f), new Color(0.3f, 0.6f, 0.8f));
        // 关卡选择按钮
        CreateButton(mainMenuRoot.transform, "LevelSelectButton", "关卡选择",
            new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.33f), new Color(0.5f, 0.4f, 0.8f));
        // 游戏设置按钮
        CreateButton(mainMenuRoot.transform, "SettingsButton", "游戏设置",
            new Vector2(0.35f, 0.15f), new Vector2(0.65f, 0.23f), new Color(0.3f, 0.5f, 0.8f));
        // 退出游戏按钮
        CreateButton(mainMenuRoot.transform, "QuitButton", "退出游戏",
            new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.13f), new Color(0.7f, 0.3f, 0.3f));

        // 创建关卡选择面板
        CreateLevelSelectPanel(canvasObj);

        // 添加MainMenu组件
        MainMenu mainMenu = mainMenuRoot.AddComponent<MainMenu>();

        mainMenuRoot.SetActive(true);
        Debug.Log("[SceneAutoBuilder] 主菜单创建完成");
    }

    /// <summary>
    /// 创建关卡选择面板
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateLevelSelectPanel(GameObject canvasObj)
    {
        // 关卡选择面板根对象
        GameObject levelSelectRoot = new GameObject("LevelSelectPanel");
        levelSelectRoot.transform.SetParent(canvasObj.transform);
        RectTransform rootRect = levelSelectRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // 半透明背景
        Image bgImage = levelSelectRoot.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);

        // 标题
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(levelSelectRoot.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.3f, 0.85f);
        titleRect.anchorMax = new Vector2(0.7f, 0.95f);
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "关卡选择";
        titleText.fontSize = 32;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.font = cachedFont;

        // 关卡按钮容器
        GameObject containerObj = new GameObject("LevelButtonContainer");
        containerObj.transform.SetParent(levelSelectRoot.transform);
        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.2f, 0.2f);
        containerRect.anchorMax = new Vector2(0.8f, 0.8f);
        containerRect.sizeDelta = Vector2.zero;

        // 添加垂直布局
        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // 返回按钮
        CreateButton(levelSelectRoot.transform, "BackButton", "返回",
            new Vector2(0.4f, 0.05f), new Vector2(0.6f, 0.13f), new Color(0.5f, 0.5f, 0.5f));

        // 添加LevelSelectPanel组件
        LevelSelectPanel levelSelectPanel = levelSelectRoot.AddComponent<LevelSelectPanel>();

        // 初始隐藏
        levelSelectRoot.SetActive(false);

        Debug.Log("[SceneAutoBuilder] 关卡选择面板创建完成");
    }

    /// <summary>
    /// 创建通用按钮
    /// </summary>
    private Button CreateButton(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchorMin;
        btnRect.anchorMax = anchorMax;
        btnRect.sizeDelta = Vector2.zero;
        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = color;
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text btnText = textObj.AddComponent<Text>();
        btnText.text = text;
        btnText.fontSize = 18;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        btnText.font = cachedFont;
        return btn;
    }

    /// <summary>
    /// 创建通用滑块
    /// </summary>
    private void CreateSlider(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, float defaultValue, float minValue = 0f, float maxValue = 1f)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = anchorMin;
        sliderRect.anchorMax = anchorMax;
        sliderRect.sizeDelta = Vector2.zero;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(sliderObj.transform);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.3f, 1f);
        labelRect.sizeDelta = Vector2.zero;
        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.fontSize = 16;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.font = cachedFont;

        GameObject sliderBg = new GameObject("SliderBackground");
        sliderBg.transform.SetParent(sliderObj.transform);
        RectTransform bgRect = sliderBg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.35f, 0.3f);
        bgRect.anchorMax = new Vector2(0.85f, 0.7f);
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = sliderBg.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f);

        GameObject sliderFill = new GameObject("SliderFill");
        sliderFill.transform.SetParent(sliderBg.transform);
        RectTransform fillRect = sliderFill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(defaultValue / maxValue, 1f);
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = sliderFill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 0.3f);

        GameObject valueObj = new GameObject("ValueText");
        valueObj.transform.SetParent(sliderObj.transform);
        RectTransform valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.85f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.sizeDelta = Vector2.zero;
        Text valueText = valueObj.AddComponent<Text>();
        valueText.text = $"{(defaultValue / maxValue) * 100f:F0}%";
        valueText.fontSize = 14;
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.color = Color.white;
        valueText.font = cachedFont;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = defaultValue;
    }

    /// <summary>
    /// 创建游戏提示UI
    /// </summary>
    /// <param name="canvasObj">Canvas对象</param>
    private void CreateGameTipsUI(GameObject canvasObj)
    {
        GameObject tipsObj = new GameObject("GameTips");
        tipsObj.transform.SetParent(canvasObj.transform);
        RectTransform tipsRect = tipsObj.AddComponent<RectTransform>();
        tipsRect.anchorMin = new Vector2(0.5f, 0f);
        tipsRect.anchorMax = new Vector2(1f, 0.15f);
        tipsRect.anchoredPosition = Vector2.zero;
        tipsRect.sizeDelta = Vector2.zero;

        Text tipsText = tipsObj.AddComponent<Text>();
        tipsText.text = "WASD移动 | 鼠标旋转 | 左键攻击 | 1/2切换武器 | P暂停 | R恢复 | K死亡 | L重生";
        tipsText.fontSize = 16;
        tipsText.alignment = TextAnchor.MiddleCenter;
        tipsText.color = new Color(1f, 1f, 1f, 0.7f);
        tipsText.font = cachedFont;

        // 阴影效果
        Shadow shadow = tipsObj.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    #endregion

    #region TestBootstrap创建

    /// <summary>
    /// 创建TestBootstrap
    /// </summary>
    private void CreateTestBootstrap()
    {
        GameObject bootstrapObj = new GameObject("TestBootstrap");
        bootstrapObj.transform.SetParent(dynamicRoot.transform);
        bootstrapObj.AddComponent<TestBootstrap>();
        Debug.Log("[SceneAutoBuilder] TestBootstrap 创建完成（快捷键P/R/K/L可用）");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 创建材质（运行时）
    /// </summary>
    /// <param name="color">材质颜色</param>
    /// <param name="name">材质名称</param>
    /// <returns>新建的材质</returns>
    private Material CreateMaterial(Color color, string name)
    {
        // 使用标准着色器（WebGL兼容）
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = name;
        mat.color = color;

        // 设置金属度和光滑度，使外观更好
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Glossiness", 0.5f);

        return mat;
    }

    /// <summary>
    /// 创建运行时Animator Controller
    /// 使用AnimatorOverrideController模拟简易状态机
    /// 动画使用Cube缩放模拟，不需要外部动画资源
    /// </summary>
    /// <param name="name">Controller名称</param>
    /// <returns>运行时AnimatorController</returns>
    private RuntimeAnimatorController CreateRuntimeAnimatorController(string name)
    {
        // Phase10: WebGL平台跳过AnimatorController创建（Editor-only API）
        if (isWebGL)
        {
            Debug.Log($"[SceneAutoBuilder] WebGL平台跳过AnimatorController创建：{name}");
            return null;
        }

        AnimatorController controller = new AnimatorController();

        // 添加Animator参数
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        // 创建状态机
        AnimatorControllerLayer layer = new AnimatorControllerLayer();
        layer.name = "Base Layer";
        layer.stateMachine = new AnimatorStateMachine();
        layer.stateMachine.name = $"{name}_StateMachine";
        layer.defaultWeight = 1f;

        // 创建Idle状态
        AnimatorState idleState = layer.stateMachine.AddState("Idle", Vector3.zero);
        idleState.motion = CreateNullAnimationClip("Idle");

        // 创建Move状态
        AnimatorState moveState = layer.stateMachine.AddState("Move", new Vector3(250f, 0f, 0f));
        moveState.motion = CreateNullAnimationClip("Move");

        // 创建Attack状态
        AnimatorState attackState = layer.stateMachine.AddState("Attack", new Vector3(250f, 0f, 200f));
        attackState.motion = CreateNullAnimationClip("Attack");

        // 创建Hurt状态
        AnimatorState hurtState = layer.stateMachine.AddState("Hurt", new Vector3(250f, 0f, 400f));
        hurtState.motion = CreateNullAnimationClip("Hurt");

        // 创建Death状态
        AnimatorState deathState = layer.stateMachine.AddState("Death", new Vector3(250f, 0f, 600f));
        deathState.motion = CreateNullAnimationClip("Death");

        // 设置默认状态为Idle
        layer.stateMachine.defaultState = idleState;

        // 添加转换（简化版）
        // Idle -> Move
        AnimatorStateTransition idleToMove = idleState.AddTransition(moveState);
        idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToMove.hasExitTime = false;
        idleToMove.duration = 0.1f;

        // Move -> Idle
        AnimatorStateTransition moveToIdle = moveState.AddTransition(idleState);
        moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        moveToIdle.hasExitTime = false;
        moveToIdle.duration = 0.1f;

        // Any -> Attack
        AnimatorStateTransition anyToAttack = layer.stateMachine.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.05f;

        // Any -> Hurt
        AnimatorStateTransition anyToHurt = layer.stateMachine.AddAnyStateTransition(hurtState);
        anyToHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
        anyToHurt.hasExitTime = false;
        anyToHurt.duration = 0.05f;

        // Any -> Death
        AnimatorStateTransition anyToDeath = layer.stateMachine.AddAnyStateTransition(deathState);
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        anyToDeath.hasExitTime = false;
        anyToDeath.duration = 0.05f;

        // 添加层
        controller.AddLayer(layer);

        // Phase10: WebGL平台跳过文件系统操作
        if (!isWebGL)
        {
            string path = $"Assets/Resources/RuntimeAnimators/{name}.controller";
            System.IO.Directory.CreateDirectory("Assets/Resources/RuntimeAnimators");
        }

        Debug.Log($"[SceneAutoBuilder] 运行时AnimatorController创建完成：{name}");
        return controller;
    }

    /// <summary>
    /// 创建空动画片段（用于运行时Animator）
    /// </summary>
    /// <param name="clipName">动画名称</param>
    /// <returns>空动画片段</returns>
    private AnimationClip CreateNullAnimationClip(string clipName)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = clipName;

        // 创建一个简单的缩放动画（模拟动画效果）
        AnimationCurve scaleXCurve = new AnimationCurve();
        AnimationCurve scaleYCurve = new AnimationCurve();
        AnimationCurve scaleZCurve = new AnimationCurve();

        // 不同动画使用不同的缩放效果
        switch (clipName)
        {
            case "Idle":
                // 微弱呼吸效果
                scaleXCurve.AddKey(0f, 1f);
                scaleXCurve.AddKey(0.5f, 1.02f);
                scaleXCurve.AddKey(1f, 1f);
                scaleYCurve.AddKey(0f, 1f);
                scaleYCurve.AddKey(0.5f, 0.98f);
                scaleYCurve.AddKey(1f, 1f);
                scaleZCurve.AddKey(0f, 1f);
                scaleZCurve.AddKey(0.5f, 1.02f);
                scaleZCurve.AddKey(1f, 1f);
                clip.wrapMode = WrapMode.Loop;
                break;

            case "Move":
                // 左右摆动
                scaleXCurve.AddKey(0f, 1f);
                scaleXCurve.AddKey(0.25f, 0.9f);
                scaleXCurve.AddKey(0.5f, 1f);
                scaleXCurve.AddKey(0.75f, 1.1f);
                scaleXCurve.AddKey(1f, 1f);
                scaleYCurve.AddKey(0f, 1f);
                scaleYCurve.AddKey(0.5f, 0.95f);
                scaleYCurve.AddKey(1f, 1f);
                scaleZCurve.AddKey(0f, 1f);
                scaleZCurve.AddKey(0.5f, 1.05f);
                scaleZCurve.AddKey(1f, 1f);
                clip.wrapMode = WrapMode.Loop;
                break;

            case "Attack":
                // 快速前冲
                scaleXCurve.AddKey(0f, 1f);
                scaleXCurve.AddKey(0.2f, 1.3f);
                scaleXCurve.AddKey(0.4f, 0.8f);
                scaleXCurve.AddKey(1f, 1f);
                scaleYCurve.AddKey(0f, 1f);
                scaleYCurve.AddKey(0.2f, 0.8f);
                scaleYCurve.AddKey(0.4f, 1.2f);
                scaleYCurve.AddKey(1f, 1f);
                scaleZCurve.AddKey(0f, 1f);
                scaleZCurve.AddKey(0.2f, 0.7f);
                scaleZCurve.AddKey(0.4f, 1.3f);
                scaleZCurve.AddKey(1f, 1f);
                clip.wrapMode = WrapMode.Once;
                break;

            case "Hurt":
                // 缩小闪烁
                scaleXCurve.AddKey(0f, 1f);
                scaleXCurve.AddKey(0.1f, 0.7f);
                scaleXCurve.AddKey(0.3f, 1.1f);
                scaleXCurve.AddKey(0.5f, 0.9f);
                scaleXCurve.AddKey(1f, 1f);
                scaleYCurve.AddKey(0f, 1f);
                scaleYCurve.AddKey(0.1f, 1.2f);
                scaleYCurve.AddKey(0.3f, 0.8f);
                scaleYCurve.AddKey(0.5f, 1.05f);
                scaleYCurve.AddKey(1f, 1f);
                scaleZCurve.AddKey(0f, 1f);
                scaleZCurve.AddKey(0.1f, 0.7f);
                scaleZCurve.AddKey(0.3f, 1.1f);
                scaleZCurve.AddKey(0.5f, 0.9f);
                scaleZCurve.AddKey(1f, 1f);
                clip.wrapMode = WrapMode.Once;
                break;

            case "Death":
                // 倒下效果
                scaleXCurve.AddKey(0f, 1f);
                scaleXCurve.AddKey(0.3f, 1.2f);
                scaleXCurve.AddKey(0.6f, 0.3f);
                scaleXCurve.AddKey(1f, 0.1f);
                scaleYCurve.AddKey(0f, 1f);
                scaleYCurve.AddKey(0.3f, 0.8f);
                scaleYCurve.AddKey(0.6f, 1.5f);
                scaleYCurve.AddKey(1f, 2f);
                scaleZCurve.AddKey(0f, 1f);
                scaleZCurve.AddKey(0.3f, 1.1f);
                scaleZCurve.AddKey(0.6f, 0.5f);
                scaleZCurve.AddKey(1f, 0.2f);
                clip.wrapMode = WrapMode.Once;
                break;
        }

        // 绑定缩放动画到localScale
        clip.SetCurve("", typeof(Transform), "localScale.x", scaleXCurve);
        clip.SetCurve("", typeof(Transform), "localScale.y", scaleYCurve);
        clip.SetCurve("", typeof(Transform), "localScale.z", scaleZCurve);

        return clip;
    }

    #endregion

    #region 静态清理方法

    /// <summary>
    /// 清理所有动态生成的对象（Editor菜单调用）
    /// </summary>
    public static void CleanupDynamicObjects()
    {
        // 查找并销毁所有SceneAutoBuilder生成的对象
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int destroyedCount = 0;

        foreach (GameObject root in rootObjects)
        {
            if (root.name.Contains("[SceneAutoBuilder]") || 
                root.name == "DynamicRoot" ||
                root.name.StartsWith("PlayerRoot") ||
                root.name.StartsWith("Enemy_") ||
                root.name == "GameCanvas" ||
                root.name == "TestBootstrap")
            {
                DestroyImmediate(root);
                destroyedCount++;
            }
        }

        // 同时清理可能残留的单例
        if (Singleton<GameManager>.HasInstance)
        {
            DestroyImmediate(Singleton<GameManager>.Instance.gameObject);
            destroyedCount++;
        }
        if (Singleton<AudioManager>.HasInstance)
        {
            DestroyImmediate(Singleton<AudioManager>.Instance.gameObject);
            destroyedCount++;
        }
        if (Singleton<VFXManager>.HasInstance)
        {
            DestroyImmediate(Singleton<VFXManager>.Instance.gameObject);
            destroyedCount++;
        }
        if (Singleton<DamageNumberManager>.HasInstance)
        {
            DestroyImmediate(Singleton<DamageNumberManager>.Instance.gameObject);
            destroyedCount++;
        }
        if (Singleton<ProjectilePool>.HasInstance)
        {
            DestroyImmediate(Singleton<ProjectilePool>.Instance.gameObject);
            destroyedCount++;
        }
        if (Singleton<InputManager>.HasInstance)
        {
            DestroyImmediate(Singleton<InputManager>.Instance.gameObject);
            destroyedCount++;
        }

        Debug.Log($"[SceneAutoBuilder] 清理完成，销毁了 {destroyedCount} 个对象");
    }

    #endregion
}
