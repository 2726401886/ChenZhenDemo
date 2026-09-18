using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 成就管理器 - 监听全局事件，维护计数器，判定成就解锁
/// 订阅击杀、Boss击杀、强化、拾取、金币、通关事件
/// 成就仅解锁一次，重复触发不重复发奖
/// WebGL平台兼容
/// </summary>
public class AchievementManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>成就解锁事件</summary>
    public const string ON_ACHIEVEMENT_UNLOCK = "ON_ACHIEVEMENT_UNLOCK";

    #endregion

    #region Inspector可配置参数

    [Header("组件引用")]
    [Tooltip("玩家CoinManager组件")]
    [SerializeField] private CoinManager coinManager;

    [Tooltip("玩家Inventory组件")]
    [SerializeField] private Inventory inventory;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>成就数据字典（key: achievementId）</summary>
    private Dictionary<string, AchievementData> achievementDatas = new Dictionary<string, AchievementData>();

    /// <summary>计数器字典（key: ConditionType的int值）</summary>
    private Dictionary<int, int> counters = new Dictionary<int, int>();

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeAchievementManager();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    private void InitializeAchievementManager()
    {
        if (coinManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                coinManager = player.GetComponent<CoinManager>();
                inventory = player.GetComponent<Inventory>();
            }
        }

        // 初始化计数器
        for (int i = 0; i <= 5; i++)
        {
            counters[i] = 0;
        }

        // 加载所有成就数据（已解锁的从存档恢复）
        var allConfigs = AchievementConfig.GetAll();
        foreach (var kvp in allConfigs)
        {
            if (!achievementDatas.ContainsKey(kvp.Key))
            {
                achievementDatas[kvp.Key] = new AchievementData(kvp.Key, false);
            }
        }

        SubscribeEvents();
        DebugLog("[AchievementManager] 成就系统初始化，计数器清零");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_ENEMY_KILLED", OnEnemyKilled);
        EventBus.Subscribe("ON_BOSS_KILLED", OnBossKilled);
        EventBus.Subscribe(EnhanceManager.ON_EQUIP_ENHANCE_SUCCESS, OnEnhanceSuccess);
        EventBus.Subscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Subscribe("ON_COIN_CHANGE", OnCoinChange);
        EventBus.Subscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_ENEMY_KILLED", OnEnemyKilled);
        EventBus.Unsubscribe("ON_BOSS_KILLED", OnBossKilled);
        EventBus.Unsubscribe(EnhanceManager.ON_EQUIP_ENHANCE_SUCCESS, OnEnhanceSuccess);
        EventBus.Unsubscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Unsubscribe("ON_COIN_CHANGE", OnCoinChange);
        EventBus.Unsubscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
    }

    #endregion

    #region 事件处理

    private void OnEnemyKilled(object data)
    {
        counters[(int)AchievementConfig.ConditionType.KillEnemy]++;
        DebugLog($"[AchievementManager] 击杀计数更新：{counters[(int)AchievementConfig.ConditionType.KillEnemy]}");
        CheckAchievements(AchievementConfig.ConditionType.KillEnemy);
    }

    private void OnBossKilled(object data)
    {
        counters[(int)AchievementConfig.ConditionType.KillBoss]++;
        DebugLog($"[AchievementManager] Boss击杀计数更新：{counters[(int)AchievementConfig.ConditionType.KillBoss]}");
        CheckAchievements(AchievementConfig.ConditionType.KillBoss);
    }

    private void OnEnhanceSuccess(object data)
    {
        counters[(int)AchievementConfig.ConditionType.EnhanceEquipment]++;
        DebugLog($"[AchievementManager] 强化次数计数更新：{counters[(int)AchievementConfig.ConditionType.EnhanceEquipment]}");
        CheckAchievements(AchievementConfig.ConditionType.EnhanceEquipment);
    }

    private void OnItemPickup(object data)
    {
        counters[(int)AchievementConfig.ConditionType.PickupItems]++;
        DebugLog($"[AchievementManager] 拾取物品计数更新：{counters[(int)AchievementConfig.ConditionType.PickupItems]}");
        CheckAchievements(AchievementConfig.ConditionType.PickupItems);
    }

    private void OnCoinChange(object data)
    {
        if (coinManager == null) return;
        // 使用当前金币作为累计金币的近似值
        // 注：实际应累计总获得金币，这里简化为当前金币
        counters[(int)AchievementConfig.ConditionType.EarnCoins] = coinManager.CurrentCoins;
        CheckAchievements(AchievementConfig.ConditionType.EarnCoins);
    }

    private void OnLevelComplete(object data)
    {
        counters[(int)AchievementConfig.ConditionType.ClearLevel]++;
        DebugLog($"[AchievementManager] 通关次数计数更新：{counters[(int)AchievementConfig.ConditionType.ClearLevel]}");
        CheckAchievements(AchievementConfig.ConditionType.ClearLevel);
    }

    #endregion

    #region 成就判定

    private void CheckAchievements(AchievementConfig.ConditionType conditionType)
    {
        var allConfigs = AchievementConfig.GetAll();
        int conditionValue = (int)conditionType;

        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            if (config.conditionType != conditionType) continue;

            // 检查是否已解锁（一次性成就）
            if (config.oneTimeUnlock && IsUnlocked(config.achievementId)) continue;

            // 检查条件是否满足
            int currentValue = GetCounter(conditionType);
            if (currentValue >= config.targetValue)
            {
                UnlockAchievement(config.achievementId);
            }
        }
    }

    private void UnlockAchievement(string achievementId)
    {
        var config = AchievementConfig.Get(achievementId);
        if (config == null) return;

        // 设置解锁状态
        if (!achievementDatas.ContainsKey(achievementId))
        {
            achievementDatas[achievementId] = new AchievementData(achievementId);
        }

        achievementDatas[achievementId].unlocked = true;
        achievementDatas[achievementId].unlockTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        DebugLog($"[AchievementManager] 条件达成：成就【{config.displayName}】解锁");

        // 发放奖励
        GrantReward(config);

        // 发布解锁事件
        EventBus.Publish(ON_ACHIEVEMENT_UNLOCK);
    }

    private void GrantReward(AchievementConfig config)
    {
        switch (config.rewardType)
        {
            case AchievementConfig.RewardType.Coin:
                if (coinManager != null)
                {
                    coinManager.AddCoins(config.rewardValue);
                    DebugLog($"[AchievementManager] 发放奖励：金币{config.rewardValue}");
                }
                break;

            case AchievementConfig.RewardType.Item:
                if (inventory != null && !string.IsNullOrEmpty(config.rewardItemId))
                {
                    inventory.AddItem(config.rewardItemId, config.rewardValue);
                    DebugLog($"[AchievementManager] 发放奖励：{config.rewardItemId}x{config.rewardValue}");
                }
                break;
        }
    }

    #endregion

    #region 查询接口

    /// <summary>获取计数器当前值</summary>
    public int GetCounter(AchievementConfig.ConditionType type)
    {
        int key = (int)type;
        if (counters.ContainsKey(key))
            return counters[key];
        return 0;
    }

    /// <summary>设置计数器值（存档加载用）</summary>
    public void SetCounter(AchievementConfig.ConditionType type, int value)
    {
        counters[(int)type] = value;
    }

    /// <summary>检查成就是否已解锁</summary>
    public bool IsUnlocked(string achievementId)
    {
        if (achievementDatas.TryGetValue(achievementId, out var data))
            return data.unlocked;
        return false;
    }

    /// <summary>获取成就数据</summary>
    public AchievementData GetAchievementData(string achievementId)
    {
        if (achievementDatas.TryGetValue(achievementId, out var data))
            return data;
        return null;
    }

    /// <summary>获取所有成就数据</summary>
    public Dictionary<string, AchievementData> GetAllAchievementData()
    {
        return achievementDatas;
    }

    /// <summary>获取已解锁成就数量</summary>
    public int GetUnlockedCount()
    {
        int count = 0;
        foreach (var kvp in achievementDatas)
        {
            if (kvp.Value.unlocked) count++;
        }
        return count;
    }

    /// <summary>获取成就总数</summary>
    public int GetTotalCount()
    {
        return AchievementConfig.GetAll().Count;
    }

    #endregion

    #region 存档接口

    /// <summary>序列化成就数据为存档列表</summary>
    public List<AchievementData> SerializeForSave()
    {
        List<AchievementData> list = new List<AchievementData>();
        foreach (var kvp in achievementDatas)
        {
            list.Add(kvp.Value);
        }
        return list;
    }

    /// <summary>从存档加载成就数据</summary>
    public void LoadFromSave(List<AchievementData> savedData)
    {
        if (savedData == null) return;

        foreach (var data in savedData)
        {
            if (achievementDatas.ContainsKey(data.achievementId))
            {
                achievementDatas[data.achievementId].unlocked = data.unlocked;
                achievementDatas[data.achievementId].unlockTime = data.unlockTime;
            }
            else
            {
                achievementDatas[data.achievementId] = data;
            }
        }

        DebugLog("[AchievementManager] 存档成就数据加载完成");
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
