using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 任务管理器 - 监听全局事件，维护任务进度，管理任务状态流转
/// 主线任务串行解锁，支线任务独立并行
/// WebGL平台兼容
/// </summary>
public class QuestManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>接取任务事件</summary>
    public const string ON_QUEST_ACCEPT = "ON_QUEST_ACCEPT";

    /// <summary>任务进度更新事件</summary>
    public const string ON_QUEST_PROGRESS_UPDATE = "ON_QUEST_PROGRESS_UPDATE";

    /// <summary>任务目标达成事件</summary>
    public const string ON_QUEST_COMPLETE = "ON_QUEST_COMPLETE";

    /// <summary>交付任务领取奖励事件</summary>
    public const string ON_QUEST_FINISH_REWARD = "ON_QUEST_FINISH_REWARD";

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

    /// <summary>任务数据字典（key: questId）</summary>
    private Dictionary<string, QuestData> questDatas = new Dictionary<string, QuestData>();

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeQuestManager();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    private void InitializeQuestManager()
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

        // 加载所有任务数据
        var allConfigs = QuestConfig.GetAll();
        foreach (var kvp in allConfigs)
        {
            if (!questDatas.ContainsKey(kvp.Key))
            {
                questDatas[kvp.Key] = new QuestData(kvp.Key, QuestData.QuestState.NotAccepted);
            }
        }

        SubscribeEvents();
        DebugLog("[QuestManager] 任务系统初始化");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_ENEMY_KILLED", OnEnemyKilled);
        EventBus.Subscribe("ON_BOSS_KILLED", OnBossKilled);
        EventBus.Subscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Subscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_ENEMY_KILLED", OnEnemyKilled);
        EventBus.Unsubscribe("ON_BOSS_KILLED", OnBossKilled);
        EventBus.Unsubscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Unsubscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
    }

    #endregion

    #region 任务操作

    /// <summary>
    /// 接取任务
    /// </summary>
    /// <param name="questId">任务ID</param>
    /// <returns>是否成功接取</returns>
    public bool AcceptQuest(string questId)
    {
        var config = QuestConfig.Get(questId);
        if (config == null)
        {
            Debug.LogWarning($"[QuestManager] 任务不存在: {questId}");
            return false;
        }

        if (!questDatas.ContainsKey(questId))
        {
            questDatas[questId] = new QuestData(questId);
        }

        var data = questDatas[questId];

        // 检查是否已接取
        if (data.state != QuestData.QuestState.NotAccepted)
        {
            Debug.LogWarning($"[QuestManager] 任务已接取或已完成: {config.displayName}");
            return false;
        }

        // 检查前置任务（主线串行）
        if (!string.IsNullOrEmpty(config.prerequisiteQuestId))
        {
            var prereqData = GetQuestData(config.prerequisiteQuestId);
            if (prereqData == null || prereqData.state != QuestData.QuestState.Finished)
            {
                Debug.LogWarning($"[QuestManager] 前置任务未完成: {config.prerequisiteQuestId}");
                return false;
            }
        }

        // 接取任务
        data.state = QuestData.QuestState.InProgress;
        data.progress = 0;

        DebugLog($"[QuestManager] 接取{(config.isMainQuest ? "主线" : "支线")}【{config.displayName}】，目标{GetTargetDescription(config)}");
        EventBus.Publish(ON_QUEST_ACCEPT);

        return true;
    }

    /// <summary>
    /// 交付任务（领取奖励）
    /// </summary>
    /// <param name="questId">任务ID</param>
    /// <returns>是否成功交付</returns>
    public bool FinishQuest(string questId)
    {
        var config = QuestConfig.Get(questId);
        if (config == null) return false;

        if (!questDatas.ContainsKey(questId))
        {
            Debug.LogWarning($"[QuestManager] 任务不存在: {questId}");
            return false;
        }

        var data = questDatas[questId];

        // 必须是已完成状态才能交付
        if (data.state != QuestData.QuestState.Completed)
        {
            Debug.LogWarning($"[QuestManager] 任务未完成，无法交付: {config.displayName}");
            return false;
        }

        // 发放奖励
        GrantReward(config);

        data.state = QuestData.QuestState.Finished;
        DebugLog($"[QuestManager] 交付任务，发放奖励：{config.displayName}");
        EventBus.Publish(ON_QUEST_FINISH_REWARD);

        return true;
    }

    #endregion

    #region 事件处理

    private void OnEnemyKilled(object data)
    {
        UpdateQuestProgress(QuestConfig.QuestTarget.KillEnemy, "Enemy");
    }

    private void OnBossKilled(object data)
    {
        UpdateQuestProgress(QuestConfig.QuestTarget.KillBoss, "Boss");
    }

    private void OnItemPickup(object data)
    {
        UpdateQuestProgress(QuestConfig.QuestTarget.CollectItem, "");
    }

    private void OnLevelComplete(object data)
    {
        UpdateQuestProgress(QuestConfig.QuestTarget.ClearLevel, "");
    }

    private void UpdateQuestProgress(QuestConfig.QuestTarget targetType, string paramFilter)
    {
        var allConfigs = QuestConfig.GetAll();

        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            if (config.target != targetType) continue;

            if (!questDatas.ContainsKey(kvp.Key)) continue;
            var data = questDatas[kvp.Key];

            // 只更新进行中的任务
            if (data.state != QuestData.QuestState.InProgress) continue;

            // 参数过滤（如果有）
            if (!string.IsNullOrEmpty(paramFilter) && !string.IsNullOrEmpty(config.targetParam)
                && config.targetParam != paramFilter) continue;

            // 增加进度
            data.progress++;

            string questName = config.displayName;
            DebugLog($"[QuestManager] 任务进度更新：{questName} {data.progress}/{config.targetCount}");
            EventBus.Publish(ON_QUEST_PROGRESS_UPDATE);

            // 检查目标是否达成
            if (data.IsTargetMet())
            {
                data.state = QuestData.QuestState.Completed;
                DebugLog($"[QuestManager] 任务目标达成【{questName}】，等待交付");
                EventBus.Publish(ON_QUEST_COMPLETE);
            }
        }
    }

    #endregion

    #region 奖励发放

    private void GrantReward(QuestConfig config)
    {
        // 发放金币
        if (config.rewardCoins > 0 && coinManager != null)
        {
            coinManager.AddCoins(config.rewardCoins);
            DebugLog($"[QuestManager] 发放奖励：金币{config.rewardCoins}");
        }

        // 发放物品
        if (!string.IsNullOrEmpty(config.rewardItemId) && config.rewardItemCount > 0 && inventory != null)
        {
            inventory.AddItem(config.rewardItemId, config.rewardItemCount);
            DebugLog($"[QuestManager] 发放奖励：{config.rewardItemId}x{config.rewardItemCount}");
        }
    }

    #endregion

    #region 查询接口

    /// <summary>获取任务数据</summary>
    public QuestData GetQuestData(string questId)
    {
        if (questDatas.TryGetValue(questId, out var data))
            return data;
        return null;
    }

    /// <summary>获取所有任务数据</summary>
    public Dictionary<string, QuestData> GetAllQuestData()
    {
        return questDatas;
    }

    /// <summary>检查任务是否可接取</summary>
    public bool CanAcceptQuest(string questId)
    {
        var config = QuestConfig.Get(questId);
        if (config == null) return false;

        if (!questDatas.ContainsKey(questId))
            return true;

        var data = questDatas[questId];
        if (data.state != QuestData.QuestState.NotAccepted)
            return false;

        // 检查前置任务
        if (!string.IsNullOrEmpty(config.prerequisiteQuestId))
        {
            var prereqData = GetQuestData(config.prerequisiteQuestId);
            if (prereqData == null || prereqData.state != QuestData.QuestState.Finished)
                return false;
        }

        return true;
    }

    /// <summary>获取当前激活的主线任务</summary>
    public QuestData GetActiveMainQuest()
    {
        var allConfigs = QuestConfig.GetAll();
        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            if (!config.isMainQuest) continue;

            if (!questDatas.ContainsKey(kvp.Key)) continue;
            var data = questDatas[kvp.Key];

            if (data.state == QuestData.QuestState.InProgress)
                return data;
        }
        return null;
    }

    /// <summary>获取已完成的主线任务数</summary>
    public int GetFinishedMainQuestCount()
    {
        int count = 0;
        var allConfigs = QuestConfig.GetAll();
        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            if (!config.isMainQuest) continue;

            if (!questDatas.ContainsKey(kvp.Key)) continue;
            var data = questDatas[kvp.Key];

            if (data.state == QuestData.QuestState.Finished)
                count++;
        }
        return count;
    }

    /// <summary>获取目标描述文本</summary>
    private string GetTargetDescription(QuestConfig config)
    {
        switch (config.target)
        {
            case QuestConfig.QuestTarget.KillEnemy:
                return $"击杀{config.targetParam}敌人{config.targetCount}只";
            case QuestConfig.QuestTarget.KillBoss:
                return $"击杀Boss{config.targetCount}次";
            case QuestConfig.QuestTarget.CollectItem:
                return $"收集{config.targetParam}x{config.targetCount}";
            case QuestConfig.QuestTarget.TalkToNPC:
                return $"与{config.targetParam}对话";
            case QuestConfig.QuestTarget.ClearLevel:
                return $"通关{config.targetCount}个关卡";
            default:
                return "未知目标";
        }
    }

    #endregion

    #region 存档接口

    /// <summary>序列化任务数据为存档列表</summary>
    public List<QuestData> SerializeForSave()
    {
        List<QuestData> list = new List<QuestData>();
        foreach (var kvp in questDatas)
        {
            list.Add(kvp.Value);
        }
        return list;
    }

    /// <summary>从存档加载任务数据</summary>
    public void LoadFromSave(List<QuestData> savedData)
    {
        if (savedData == null) return;

        foreach (var data in savedData)
        {
            if (questDatas.ContainsKey(data.questId))
            {
                questDatas[data.questId].state = data.state;
                questDatas[data.questId].progress = data.progress;
            }
            else
            {
                questDatas[data.questId] = data;
            }
        }

        DebugLog("[QuestManager] 存档任务数据加载完成");
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
