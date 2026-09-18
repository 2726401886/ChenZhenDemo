using UnityEngine;

/// <summary>
/// 成就实例数据 - 记录成就ID、解锁状态、解锁时间
/// 可序列化，用于存档读写
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class AchievementData
{
    [Tooltip("成就ID")]
    public string achievementId = "";

    [Tooltip("是否已解锁")]
    public bool unlocked = false;

    [Tooltip("解锁时间戳")]
    public string unlockTime = "";

    /// <summary>无参构造（JsonUtility需要）</summary>
    public AchievementData() { }

    /// <summary>带参构造</summary>
    public AchievementData(string id, bool isUnlocked = false)
    {
        achievementId = id;
        unlocked = isUnlocked;
        unlockTime = "";
    }

    /// <summary>获取成就配置</summary>
    public AchievementConfig GetConfig()
    {
        return AchievementConfig.Get(achievementId);
    }

    /// <summary>获取显示名称</summary>
    public string DisplayName => GetConfig() != null ? GetConfig().displayName : achievementId;

    /// <summary>获取描述</summary>
    public string Description => GetConfig() != null ? GetConfig().description : "";
}
