using UnityEngine;

/// <summary>
/// WebGL平台兼容工具类（Phase10优化版）
/// 提供WebGL平台检测、时间缩放兼容、日志输出优化、暂停事件分发等
/// Phase10新增：OnApplicationPause事件分发、暂停状态查询
/// </summary>
public static class WebGLPlatformHelper
{
    /// <summary>
    /// 是否在WebGL平台运行
    /// </summary>
    public static bool IsWebGL { get; } = Application.platform == RuntimePlatform.WebGLPlayer;

    /// <summary>
    /// Phase10新增：应用是否暂停（WebGL标签切换时为true）
    /// </summary>
    public static bool IsPaused { get; private set; } = false;

    /// <summary>
    /// Phase10新增：暂停状态变化回调
    /// </summary>
    public static event System.Action<bool> OnPauseChanged;

    /// <summary>
    /// Phase10新增：由MonoBehaviour调用，分发暂停事件
    /// </summary>
    public static void HandleApplicationPause(bool pauseStatus)
    {
        IsPaused = pauseStatus;
        OnPauseChanged?.Invoke(pauseStatus);

        if (IsWebGL)
        {
            Debug.Log($"[WebGL] 应用暂停状态: {pauseStatus}");
        }
    }

    /// <summary>
    /// 设置时间缩放（WebGL兼容）
    /// </summary>
    public static void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
    }

    /// <summary>
    /// 获取时间缩放
    /// </summary>
    public static float GetTimeScale()
    {
        return Time.timeScale;
    }

    /// <summary>
    /// WebGL平台友好的Debug.Log（非WebGL才输出）
    /// </summary>
    public static void Log(string message)
    {
        if (!IsWebGL)
        {
            Debug.Log(message);
        }
    }

    /// <summary>
    /// WebGL平台友好的Debug.LogWarning
    /// </summary>
    public static void LogWarning(string message)
    {
        Debug.LogWarning(message);
    }

    /// <summary>
    /// WebGL平台友好的Debug.LogError
    /// </summary>
    public static void LogError(string message)
    {
        Debug.LogError(message);
    }

    /// <summary>
    /// WebGL平台性能友好的帧计数等待
    /// </summary>
    public static int SecondsToFrames(float seconds)
    {
        return Mathf.CeilToInt(seconds * 60f);
    }

    /// <summary>
    /// WebGL平台内存优化提示
    /// </summary>
    public static void LogMemoryRecommendations()
    {
        if (IsWebGL)
        {
            Debug.Log("[WebGL] 内存配置建议: Initial=256MB, Max=512MB, UseIncrementalGC=true");
        }
    }

    /// <summary>
    /// WebGL平台输入兼容处理
    /// </summary>
    public static bool GetKeyDown(KeyCode keyCode)
    {
        return Input.GetKeyDown(keyCode);
    }

    /// <summary>
    /// WebGL平台鼠标位置安全获取
    /// </summary>
    public static Vector3 GetMousePosition()
    {
        try
        {
            return Input.mousePosition;
        }
        catch (System.Exception)
        {
            return Vector3.zero;
        }
    }
}
