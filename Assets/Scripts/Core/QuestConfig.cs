using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 任务配置表 - 存储所有任务的目标、奖励、剧情文本
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class QuestConfig
{
    [Header("基础设置")]
    [Tooltip("任务唯一标识ID")]
    public string questId = "";

    [Tooltip("任务显示名称")]
    public string displayName = "未命名任务";

    [Tooltip("任务描述")]
    public string description = "";

    [Tooltip("是否为")]
    [Tooltip("主线任务标记")]
    public bool isMainQuest = true;

    [Header("剧情对话")]
    [Tooltip("接取任务时的对话文本（分段，每段一行）")]
    [TextArea(3, 10)]
    public string acceptDialogue = "";

    [Tooltip("完成任务时的对话文本")]
    [TextArea(3, 10)]
    public string completeDialogue = "";

    [Header("目标设置")]
    [Tooltip("目标类型：KillEnemy击杀敌人 / KillBoss击杀Boss / CollectItem收集物品 / TalkToNPC与NPC对话 / ClearLevel通关关卡")]
    public QuestTarget target = QuestTarget.KillEnemy;

    [Tooltip("目标参数（敌人类型名/物品ID/NPC名/关卡号）")]
    public string targetParam = "";

    [Tooltip("目标数量")]
    public int targetCount = 5;

    [Header("奖励设置")]
    [Tooltip("奖励金币数量")]
    public int rewardCoins = 150;

    [Tooltip("奖励物品ID（空则无物品奖励）")]
    public string rewardItemId = "";

    [Tooltip("奖励物品数量")]
    public int rewardItemCount = 0;

    [Header("解锁设置")]
    [Tooltip("前置任务ID（空则无前置要求，主线串行用）")]
    public string prerequisiteQuestId = "";

    /// <summary>任务目标类型枚举</summary>
    public enum QuestTarget
    {
        KillEnemy,    // 击杀敌人
        KillBoss,     // 击杀Boss
        CollectItem,  // 收集物品
        TalkToNPC,    // 与NPC对话
        ClearLevel    // 通关关卡
    }

    /// <summary>任务配置字典</summary>
    private static Dictionary<string, QuestConfig> _configs = new Dictionary<string, QuestConfig>();

    /// <summary>注册任务配置</summary>
    public static void Register(string id, QuestConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取任务配置</summary>
    public static QuestConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[QuestConfig] 未找到任务配置: {id}");
        return null;
    }

    /// <summary>获取所有已注册的任务配置</summary>
    public static Dictionary<string, QuestConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
