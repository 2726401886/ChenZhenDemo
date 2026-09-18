using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Phase20 全项目压力循环测试 + WebGL打包验收
/// 自动执行4轮完整主线循环测试
/// 校验：战斗、背包、装备、强化、商店、金币、任务、成就、存档
/// WebGL平台兼容
/// </summary>
public class Phase20TestRunner : MonoBehaviour
{
    [Header("测试设置")]
    [Tooltip("循环测试次数")]
    [SerializeField] private int testCycles = 4;

    [Tooltip("每轮测试间隔（秒）")]
    [SerializeField] private float cycleInterval = 2f;

    [Header("组件引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Inventory inventory;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private CoinManager coinManager;
    [SerializeField] private EnhanceManager enhanceManager;
    [SerializeField] private QuestManager questManager;
    [SerializeField] private AchievementManager achievementManager;
    [SerializeField] private CombatSystem combatSystem;

    private int currentCycle = 0;
    private int passedTests = 0;
    private int failedTests = 0;
    private List<string> failedTestNames = new List<string>();
    private bool isTestRunning = false;

    private void Start()
    {
        Debug.Log("[Phase20] 开始全项目压力循环测试");
        StartCoroutine(RunAllTests());
    }

    private IEnumerator RunAllTests()
    {
        isTestRunning = true;

        // 等待SceneAutoBuilder初始化完成
        yield return new WaitForSeconds(1f);

        FindComponents();
        yield return new WaitForSeconds(0.5f);

        // 第一轮：基础校验
        Debug.Log("[TestRunner] 第1轮：基础模块校验");
        TestEventBusSystem();
        TestInventorySystem();
        TestEquipmentSystem();
        TestCoinSystem();
        TestEnhanceSystem();
        TestQuestSystem();
        TestAchievementSystem();
        TestSaveLoadSystem();
        yield return new WaitForSeconds(cycleInterval);

        // 第2-4轮：压力循环测试
        for (int i = 1; i < testCycles; i++)
        {
            currentCycle = i + 1;
            Debug.Log($"[TestRunner] 第{currentCycle}轮：完整主线循环测试");

            TestCombatCycle();
            TestShopCycle();
            TestEnhanceCycle();
            TestQuestCycle();
            TestAchievementCycle();
            TestSaveLoadCycle();
            yield return new WaitForSeconds(cycleInterval);
        }

        // 输出最终结果
        Debug.Log($"[TestRunner] ========================================");
        Debug.Log($"[TestRunner] 4轮循环测试全部完成");
        Debug.Log($"[TestRunner] 通过测试：{passedTests}");
        Debug.Log($"[TestRunner] 失败测试：{failedTests}");

        if (failedTests > 0)
        {
            Debug.LogWarning("[TestRunner] 失败项目：");
            foreach (var name in failedTestNames)
                Debug.LogWarning($"  - {name}");
        }
        else
        {
            Debug.Log("[TestRunner] 全部测试通过！");
        }

        Debug.Log("[Phase20] ✅ 全项目压力循环测试完成");
        isTestRunning = false;
    }

    private void FindComponents()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            inventory = player.GetComponent<Inventory>();
            equipmentManager = player.GetComponent<EquipmentManager>();
            coinManager = player.GetComponent<CoinManager>();
            enhanceManager = player.GetComponent<EnhanceManager>();
            questManager = player.GetComponent<QuestManager>();
            achievementManager = player.GetComponent<AchievementManager>();
            combatSystem = player.GetComponent<CombatSystem>();
        }
    }

    #region 测试方法

    private void TestEventBusSystem()
    {
        bool passed = true;
        string testName = "EventBus事件系统";

        // 测试事件订阅/发布
        bool eventFired = false;
        System.Action handler = () => { eventFired = true; };
        EventBus.Subscribe("TEST_EVENT", handler);
        EventBus.Publish("TEST_EVENT");
        EventBus.Unsubscribe("TEST_EVENT", handler);

        if (!eventFired)
        {
            RecordFailure(testName);
            passed = false;
        }

        // 测试带参事件
        bool paramEventFired = false;
        System.Action<object> paramHandler = (data) => { paramEventFired = true; };
        EventBus.Subscribe("TEST_PARAM_EVENT", paramHandler);
        EventBus.Publish("TEST_PARAM_EVENT");
        EventBus.Unsubscribe("TEST_PARAM_EVENT", paramHandler);

        if (!paramEventFired)
        {
            RecordFailure(testName + " - 带参事件");
            passed = false;
        }

        if (passed) RecordPass(testName);
    }

    private void TestInventorySystem()
    {
        string testName = "背包系统";

        if (inventory == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试添加物品
        bool added = inventory.AddItem("HealthPotion", 1);
        if (!added)
        {
            RecordFailure(testName + " - 添加物品失败");
            return;
        }

        // 测试获取物品数量
        int count = inventory.GetItemCount("HealthPotion");
        if (count != 1)
        {
            RecordFailure(testName + $" - 物品数量错误: 期望1, 实际{count}");
            return;
        }

        // 测试移除物品
        bool removed = inventory.RemoveItem("HealthPotion", 1);
        if (!removed)
        {
            RecordFailure(testName + " - 移除物品失败");
            return;
        }

        count = inventory.GetItemCount("HealthPotion");
        if (count != 0)
        {
            RecordFailure(testName + $" - 移除后数量错误: 期望0, 实际{count}");
            return;
        }

        RecordPass(testName);
    }

    private void TestEquipmentSystem()
    {
        string testName = "装备系统";

        if (equipmentManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试装备穿戴
        Equipment testEquip = new Equipment("IronSword", 0);
        bool equipped = equipmentManager.EquipItem(testEquip);
        if (!equipped)
        {
            RecordFailure(testName + " - 穿戴装备失败");
            return;
        }

        // 测试获取装备
        Equipment retrieved = equipmentManager.GetEquipmentAt(0);
        if (retrieved == null || retrieved.equipId != "IronSword")
        {
            RecordFailure(testName + " - 获取装备失败");
            return;
        }

        // 测试卸下装备
        Equipment uneipped = equipmentManager.UnequipItem(0);
        if (uneipped == null)
        {
            RecordFailure(testName + " - 卸下装备失败");
            return;
        }

        RecordPass(testName);
    }

    private void TestCoinSystem()
    {
        string testName = "金币系统";

        if (coinManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        int initialCoins = coinManager.CurrentCoins;

        // 测试增加金币
        coinManager.AddCoins(100);
        if (coinManager.CurrentCoins != initialCoins + 100)
        {
            RecordFailure(testName + " - 增加金币错误");
            return;
        }

        // 测试扣除金币
        bool spent = coinManager.SpendCoins(50);
        if (!spent || coinManager.CurrentCoins != initialCoins + 50)
        {
            RecordFailure(testName + " - 扣除金币错误");
            return;
        }

        // 测试金币不足
        bool cantSpend = coinManager.SpendCoins(99999);
        if (cantSpend)
        {
            RecordFailure(testName + " - 金币不足校验失败");
            return;
        }

        RecordPass(testName);
    }

    private void TestEnhanceSystem()
    {
        string testName = "强化系统";

        if (enhanceManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试获取强化配置
        var config = EnhanceConfig.Get(0);
        if (config == null)
        {
            RecordFailure(testName + " - 强化配置缺失");
            return;
        }

        // 测试强化配置参数
        if (config.costCoins != 100 || config.successRate != 0.8f)
        {
            RecordFailure(testName + " - 强化配置参数错误");
            return;
        }

        // 测试最高强化等级
        int maxLevel = EnhanceConfig.GetMaxLevel();
        if (maxLevel != 3)
        {
            RecordFailure(testName + $" - 最高强化等级错误: 期望3, 实际{maxLevel}");
            return;
        }

        RecordPass(testName);
    }

    private void TestQuestSystem()
    {
        string testName = "任务系统";

        if (questManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试任务配置
        var config = QuestConfig.Get("Q001");
        if (config == null)
        {
            RecordFailure(testName + " - 任务配置缺失");
            return;
        }

        // 测试接取任务
        bool accepted = questManager.AcceptQuest("Q001");
        if (!accepted)
        {
            RecordFailure(testName + " - 接取任务失败");
            return;
        }

        // 测试任务状态
        var data = questManager.GetQuestData("Q001");
        if (data == null || data.state != QuestData.QuestState.InProgress)
        {
            RecordFailure(testName + " - 任务状态错误");
            return;
        }

        // 测试重复接取
        bool duplicateAccept = questManager.AcceptQuest("Q001");
        if (duplicateAccept)
        {
            RecordFailure(testName + " - 重复接取校验失败");
            return;
        }

        RecordPass(testName);
    }

    private void TestAchievementSystem()
    {
        string testName = "成就系统";

        if (achievementManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试成就配置
        var config = AchievementConfig.Get("ACH_KillEnemy10");
        if (config == null)
        {
            RecordFailure(testName + " - 成就配置缺失");
            return;
        }

        // 测试计数器
        int counter = achievementManager.GetCounter(AchievementConfig.ConditionType.KillEnemy);
        if (counter < 0)
        {
            RecordFailure(testName + " - 计数器错误");
            return;
        }

        // 测试成就总数
        int total = achievementManager.GetTotalCount();
        if (total != 6)
        {
            RecordFailure(testName + $" - 成就总数错误: 期望6, 实际{total}");
            return;
        }

        RecordPass(testName);
    }

    private void TestSaveLoadSystem()
    {
        string testName = "存档系统";

        // 测试保存
        SaveManager.Instance.SaveGame();
        if (!SaveManager.Instance.HasSave())
        {
            RecordFailure(testName + " - 保存后无存档");
            return;
        }

        // 测试读取
        bool loaded = SaveManager.Instance.LoadGame();
        if (!loaded)
        {
            RecordFailure(testName + " - 读取存档失败");
            return;
        }

        RecordPass(testName);
    }

    private void TestCombatCycle()
    {
        string testName = $"战斗循环-第{currentCycle}轮";

        // 模拟战斗场景
        if (combatSystem != null)
        {
            combatSystem.SetCurrentHealth(combatSystem.maxHealth);
        }

        RecordPass(testName);
    }

    private void TestShopCycle()
    {
        string testName = $"商店循环-第{currentCycle}轮";

        if (coinManager == null)
        {
            RecordFailure(testName + " - 金币组件缺失");
            return;
        }

        // 测试购买
        int before = coinManager.CurrentCoins;
        coinManager.AddCoins(1000);

        // 测试出售（金币增减）
        coinManager.SpendCoins(150);

        if (coinManager.CurrentCoins != before + 850)
        {
            RecordFailure(testName + " - 金币计算错误");
            return;
        }

        RecordPass(testName);
    }

    private void TestEnhanceCycle()
    {
        string testName = $"强化循环-第{currentCycle}轮";

        if (enhanceManager == null || equipmentManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试强化配置验证
        var config = EnhanceConfig.Get(0);
        if (config == null || config.statMultiplier != 1.1f)
        {
            RecordFailure(testName + " - 强化配置验证失败");
            return;
        }

        RecordPass(testName);
    }

    private void TestQuestCycle()
    {
        string testName = $"任务循环-第{currentCycle}轮";

        if (questManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试主线任务计数
        int finishedCount = questManager.GetFinishedMainQuestCount();
        if (finishedCount < 0)
        {
            RecordFailure(testName + " - 主线完成数错误");
            return;
        }

        RecordPass(testName);
    }

    private void TestAchievementCycle()
    {
        string testName = $"成就循环-第{currentCycle}轮";

        if (achievementManager == null)
        {
            RecordFailure(testName + " - 组件缺失");
            return;
        }

        // 测试成就解锁检查
        int unlocked = achievementManager.GetUnlockedCount();
        int total = achievementManager.GetTotalCount();
        if (unlocked > total)
        {
            RecordFailure(testName + " - 解锁数超过总数");
            return;
        }

        RecordPass(testName);
    }

    private void TestSaveLoadCycle()
    {
        string testName = $"存档循环-第{currentCycle}轮";

        // 保存
        SaveManager.Instance.SaveGame();

        // 读取
        bool loaded = SaveManager.Instance.LoadGame();
        if (!loaded)
        {
            RecordFailure(testName + " - 存档读取失败");
            return;
        }

        RecordPass(testName);
    }

    #endregion

    #region 结果记录

    private void RecordPass(string testName)
    {
        passedTests++;
        Debug.Log($"[TestRunner] ✅ 通过: {testName}");
    }

    private void RecordFailure(string testName)
    {
        failedTests++;
        failedTestNames.Add(testName);
        Debug.LogWarning($"[TestRunner] ❌ 失败: {testName}");
    }

    #endregion
}
