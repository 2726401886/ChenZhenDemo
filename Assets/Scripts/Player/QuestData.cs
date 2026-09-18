using UnityEngine;

/// <summary>
/// 任务实例数据 - 记录任务ID、状态、当前进度
/// 可序列化，用于存档读写
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class QuestData
{
    [Tooltip("任务ID")]
    public string questId = "";

    [Tooltip("任务状态")]
    public QuestState state = QuestState.NotAccepted;

    [Tooltip("当前进度计数")]
    public int progress = 0;

    /// <summary>任务状态枚举</summary>
    public enum QuestState
    {
        NotAccepted,  // 未接取
        InProgress,   // 进行中
        Completed,    // 已完成（等待交付）
        Finished      // 已交付（领取奖励）
    }

    /// <summary>无参构造（JsonUtility需要）</summary>
    public QuestData() { }

    /// <summary>带参构造</summary>
    public QuestData(string id, QuestState initState = QuestState.NotAccepted)
    {
        questId = id;
        state = initState;
        progress = 0;
    }

    /// <summary>获取任务配置</summary>
    public QuestConfig GetConfig()
    {
        return QuestConfig.Get(questId);
    }

    /// <summary>获取显示名称</summary>
    public string DisplayName => GetConfig() != null ? GetConfig().displayName : questId;

    /// <summary>获取描述</summary>
    public string Description => GetConfig() != null ? GetConfig().description : "";

    /// <summary>获取目标数量</summary>
    public int TargetCount => GetConfig() != null ? GetConfig().targetCount : 0;

    /// <summary>检查目标是否达成</summary>
    public bool IsTargetMet()
    {
        return progress >= TargetCount;
    }
}
