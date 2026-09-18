using UnityEngine;
using UnityEditor;

/// <summary>
/// 编辑器菜单项 - 提供便捷的编辑器工具入口
/// 仅编辑器运行，不打包进游戏
/// </summary>
public static class EditorMenuItems
{
    /// <summary>
    /// 创建PrefabValidator工具窗口
    /// </summary>
    [MenuItem("工具/预制体校验工具")]
    public static void OpenPrefabValidator()
    {
        // 查找场景中的PrefabValidatorTarget
        PrefabValidatorTarget target = FindObjectOfType<PrefabValidatorTarget>();

        if (target == null)
        {
            // 创建新的GameObject并添加组件
            GameObject obj = new GameObject("PrefabValidator");
            target = obj.AddComponent<PrefabValidatorTarget>();
            Selection.activeGameObject = obj;
            Debug.Log("[EditorMenuItems] 已创建 PrefabValidator 对象，请在Inspector中设置预制体后点击校验按钮");
        }
        else
        {
            Selection.activeGameObject = target.gameObject;
        }
    }

    /// <summary>
    /// 开启EventBus调试日志
    /// </summary>
    [MenuItem("工具/EventBus/开启调试日志")]
    public static void EnableEventBusDebugLog()
    {
        EventBus.EnableDebugLog = true;
        Debug.Log("[EditorMenuItems] EventBus调试日志已开启");
    }

    /// <summary>
    /// 关闭EventBus调试日志
    /// </summary>
    [MenuItem("工具/EventBus/关闭调试日志")]
    public static void DisableEventBusDebugLog()
    {
        EventBus.EnableDebugLog = false;
        Debug.Log("[EditorMenuItems] EventBus调试日志已关闭");
    }

    /// <summary>
    /// 显示EventBus订阅者统计
    /// </summary>
    [MenuItem("工具/EventBus/显示订阅者统计")]
    public static void ShowEventBusStats()
    {
        Debug.Log("========== EventBus 订阅者统计 ==========");

        string[] eventNames = {
            "ON_PLAYER_MOVE", "ON_PLAYER_HIT", "ON_PLAYER_DIE", "ON_PLAYER_RESPAWN",
            "ON_PLAYER_ATTACK", "ON_PLAYER_ATTACK_INPUT", "ON_PLAYER_JUMP_INPUT",
            "ON_ENEMY_HIT", "ON_ENEMY_DIE", "ON_ENEMY_RESPAWN", "ON_ENEMY_ATTACK", "ON_ENEMY_MOVE",
            "ON_ATTACK_ANIM_START", "ON_ATTACK_DAMAGE_FRAME", "ON_ATTACK_ANIM_END",
            "ON_DEATH_ANIM_END",
            "ON_GAME_PAUSE", "ON_GAME_RESUME"
        };

        foreach (string eventName in eventNames)
        {
            int count = EventBus.GetSubscriberCount(eventName);
            string status = count > 0 ? $"✓ ({count}个订阅者)" : "✗ (无订阅者)";
            Debug.Log($"  {eventName}: {status}");
        }

        Debug.Log("==========================================");
    }
}
