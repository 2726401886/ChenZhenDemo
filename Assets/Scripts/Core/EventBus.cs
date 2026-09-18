using System;
using System.Collections.Generic;

/// <summary>
/// 轻量级事件总线系统（Phase10优化版）
/// 静态全局事件中心，用于模块间解耦通信
/// 支持无参事件和泛型带参事件，防止装箱性能损耗
/// 支持可选的Debug.Log输出，通过EnableDebugLog全局开关控制
/// WebGL平台优化：日志采样输出，避免控制台卡顿
/// Phase10优化：修复重复订阅检查、消除Publish GC分配、移除反射调用
/// </summary>
public static class EventBus
{
    #region 调试设置

    public static bool EnableDebugLog = false;
    public static float WebGLLogSampleRate = 0.1f;
    private static int webGLLogCounter = 0;
    private static readonly bool isWebGL = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WebGLPlayer;

    #endregion

    #region 无参事件

    private delegate void EventAction();

    private static readonly Dictionary<string, List<EventAction>> eventActions =
        new Dictionary<string, List<EventAction>>();

    /// <summary>无参事件订阅计数（避免反射）</summary>
    private static readonly Dictionary<string, int> eventActionCounts =
        new Dictionary<string, int>();

    public static void Subscribe(string eventName, Action action)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            UnityEngine.Debug.LogError("[EventBus] 事件名称不能为空");
            return;
        }

        if (action == null)
        {
            UnityEngine.Debug.LogError($"[EventBus] 订阅的事件 '{eventName}' 回调方法不能为空");
            return;
        }

        if (!eventActions.ContainsKey(eventName))
        {
            eventActions[eventName] = new List<EventAction>();
            eventActionCounts[eventName] = 0;
        }

        // Phase10修复：使用Target+Method比较，与Unsubscribe逻辑一致
        List<EventAction> actions = eventActions[eventName];
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i].Target == action.Target && actions[i].Method == action.Method)
            {
                return; // 已订阅，跳过
            }
        }

        actions.Add(new EventAction(action));
        eventActionCounts[eventName] = actions.Count;
    }

    public static void Unsubscribe(string eventName, Action action)
    {
        if (string.IsNullOrEmpty(eventName) || action == null)
            return;

        if (eventActions.TryGetValue(eventName, out List<EventAction> actions))
        {
            for (int i = actions.Count - 1; i >= 0; i--)
            {
                if (actions[i].Target == action.Target && actions[i].Method == action.Method)
                {
                    actions.RemoveAt(i);
                    break;
                }
            }

            eventActionCounts[eventName] = actions.Count;

            if (actions.Count == 0)
            {
                eventActions.Remove(eventName);
                eventActionCounts.Remove(eventName);
            }
        }
    }

    /// <summary>
    /// 触发无参事件（Phase10优化：索引遍历，无GC分配）
    /// </summary>
    public static void Publish(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            UnityEngine.Debug.LogError("[EventBus] 事件名称不能为空");
            return;
        }

        if (EnableDebugLog)
        {
            bool shouldLog = true;
            if (isWebGL)
            {
                webGLLogCounter++;
                shouldLog = (webGLLogCounter % Mathf.Max(1, Mathf.RoundToInt(1f / WebGLLogSampleRate))) == 0;
            }

            if (shouldLog)
            {
                int count = 0;
                eventActionCounts.TryGetValue(eventName, out count);
                UnityEngine.Debug.Log($"[EventBus] Publish: {eventName} (订阅者: {count})");
            }
        }

        if (!eventActions.TryGetValue(eventName, out List<EventAction> actions))
            return;

        // Phase10优化：索引遍历，不分配新List
        // 回调中如果取消订阅会修改列表末尾元素，通过count快照保证安全
        int countSnapshot = actions.Count;
        for (int i = 0; i < countSnapshot; i++)
        {
            try
            {
                actions[i]?.Invoke();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[EventBus] 执行事件 '{eventName}' 时出错: {ex.Message}");
            }
        }
    }

    #endregion

    #region 带参数事件（泛型，防止装箱）

    private class EventSubscription<T>
    {
        public Action<T> Callback;
        public Action WrappedCallback;

        public EventSubscription(Action<T> callback)
        {
            Callback = callback;
        }
    }

    private class EventHolder<T>
    {
        public List<EventSubscription<T>> Subscribers = new List<EventSubscription<T>>();
        public int Count = 0;
    }

    private static readonly Dictionary<string, object> typedEvents =
        new Dictionary<string, object>();

    /// <summary>泛型事件订阅计数（避免反射）</summary>
    private static readonly Dictionary<string, int> typedEventCounts =
        new Dictionary<string, int>();

    public static void Subscribe<T>(string eventName, Action<T> callback)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            UnityEngine.Debug.LogError("[EventBus] 事件名称不能为空");
            return;
        }

        if (callback == null)
        {
            UnityEngine.Debug.LogError($"[EventBus] 订阅的事件 '{eventName}' 回调方法不能为空");
            return;
        }

        if (!typedEvents.ContainsKey(eventName))
        {
            typedEvents[eventName] = new EventHolder<T>();
            typedEventCounts[eventName] = 0;
        }

        EventHolder<T> holder = typedEvents[eventName] as EventHolder<T>;
        if (holder == null)
        {
            UnityEngine.Debug.LogError($"[EventBus] 事件 '{eventName}' 类型不匹配");
            return;
        }

        // 防止重复订阅
        for (int i = 0; i < holder.Subscribers.Count; i++)
        {
            if (holder.Subscribers[i].Callback.Target == callback.Target &&
                holder.Subscribers[i].Callback.Method == callback.Method)
            {
                return; // 已订阅
            }
        }

        holder.Subscribers.Add(new EventSubscription<T>(callback));
        holder.Count = holder.Subscribers.Count;
        typedEventCounts[eventName] = holder.Count;
    }

    public static void Unsubscribe<T>(string eventName, Action<T> callback)
    {
        if (string.IsNullOrEmpty(eventName) || callback == null)
            return;

        if (typedEvents.TryGetValue(eventName, out object obj))
        {
            EventHolder<T> holder = obj as EventHolder<T>;
            if (holder != null)
            {
                for (int i = holder.Subscribers.Count - 1; i >= 0; i--)
                {
                    if (holder.Subscribers[i].Callback.Target == callback.Target &&
                        holder.Subscribers[i].Callback.Method == callback.Method)
                    {
                        holder.Subscribers.RemoveAt(i);
                        break;
                    }
                }

                holder.Count = holder.Subscribers.Count;
                typedEventCounts[eventName] = holder.Count;

                if (holder.Subscribers.Count == 0)
                {
                    typedEvents.Remove(eventName);
                    typedEventCounts.Remove(eventName);
                }
            }
        }
    }

    /// <summary>
    /// 触发带参数的事件（Phase10优化：索引遍历，无GC分配）
    /// </summary>
    public static void Publish<T>(string eventName, T data)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            UnityEngine.Debug.LogError("[EventBus] 事件名称不能为空");
            return;
        }

        if (EnableDebugLog)
        {
            bool shouldLog = true;
            if (isWebGL)
            {
                webGLLogCounter++;
                shouldLog = (webGLLogCounter % Mathf.Max(1, Mathf.RoundToInt(1f / WebGLLogSampleRate))) == 0;
            }

            if (shouldLog)
            {
                int count = 0;
                typedEventCounts.TryGetValue(eventName, out count);
                UnityEngine.Debug.Log($"[EventBus] Publish<{typeof(T).Name}>: {eventName} (订阅者: {count})");
            }
        }

        if (!typedEvents.TryGetValue(eventName, out object obj))
            return;

        EventHolder<T> holder = obj as EventHolder<T>;
        if (holder == null)
            return;

        // Phase10优化：索引遍历，不分配新List
        int countSnapshot = holder.Subscribers.Count;
        for (int i = 0; i < countSnapshot; i++)
        {
            try
            {
                holder.Subscribers[i].Callback?.Invoke(data);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[EventBus] 执行事件 '{eventName}' 时出错: {ex.Message}");
            }
        }
    }

    #endregion

    #region 事件管理

    public static void ClearEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
            return;

        if (eventActions.ContainsKey(eventName))
        {
            eventActions[eventName].Clear();
            eventActions.Remove(eventName);
            eventActionCounts.Remove(eventName);
        }

        if (typedEvents.ContainsKey(eventName))
        {
            typedEvents.Remove(eventName);
            typedEventCounts.Remove(eventName);
        }
    }

    public static void ClearAll()
    {
        eventActions.Clear();
        typedEvents.Clear();
        eventActionCounts.Clear();
        typedEventCounts.Clear();

        UnityEngine.Debug.Log("[EventBus] 已清除所有事件订阅");
    }

    /// <summary>
    /// 获取指定事件的订阅者数量（Phase10优化：使用缓存计数，无反射）
    /// </summary>
    public static int GetSubscriberCount(string eventName)
    {
        int count = 0;

        if (eventActionCounts.TryGetValue(eventName, out int actionCount))
            count += actionCount;

        if (typedEventCounts.TryGetValue(eventName, out int typedCount))
            count += typedCount;

        return count;
    }

    /// <summary>
    /// 检查事件是否有订阅者（Phase10优化：不再调用GetSubscriberCount）
    /// </summary>
    public static bool HasSubscribers(string eventName)
    {
        if (eventActionCounts.TryGetValue(eventName, out int actionCount))
        {
            if (actionCount > 0) return true;
        }

        if (typedEventCounts.TryGetValue(eventName, out int typedCount))
        {
            if (typedCount > 0) return true;
        }

        return false;
    }

    #endregion
}
