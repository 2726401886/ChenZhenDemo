using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 暂停面板控制器 - 游戏暂停时显示
/// 订阅ON_GAME_PAUSE/ON_GAME_RESUME事件
/// 暂停时弹出面板，恢复时隐藏
/// 按钮：继续游戏、游戏设置、返回主菜单
/// WebGL平台兼容
/// </summary>
public class PausePanel : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("暂停面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("继续游戏按钮")]
    [SerializeField] private Button resumeButton;

    [Tooltip("游戏设置按钮")]
    [SerializeField] private Button settingsButton;

    [Tooltip("返回主菜单按钮")]
    [SerializeField] private Button mainMenuButton;

    [Header("设置面板")]
    [Tooltip("设置面板对象")]
    [SerializeField] private GameObject settingsPanel;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>是否正在显示</summary>
    private bool isShowing = false;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializePausePanel();
    }

    /// <summary>
    /// 销毁时取消EventBus订阅
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化暂停面板
    /// </summary>
    private void InitializePausePanel()
    {
        if (isInitialized) return;

        // 绑定按钮事件
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        // 初始隐藏面板
        HidePanel();

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[PausePanel] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Subscribe("ON_GAME_RESUME", OnGameResume);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Unsubscribe("ON_GAME_RESUME", OnGameResume);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 游戏暂停事件处理
    /// </summary>
    private void OnGamePause()
    {
        ShowPanel();
    }

    /// <summary>
    /// 游戏恢复事件处理
    /// </summary>
    private void OnGameResume()
    {
        HidePanel();
    }

    #endregion

    #region UI显示控制

    /// <summary>
    /// 显示暂停面板
    /// </summary>
    public void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        // 隐藏设置面板（如果显示）
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        isShowing = true;
        DebugLog("[PausePanel] 显示暂停面板");
    }

    /// <summary>
    /// 隐藏暂停面板
    /// </summary>
    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        isShowing = false;
        DebugLog("[PausePanel] 隐藏暂停面板");
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 继续游戏按钮点击
    /// </summary>
    private void OnResumeClicked()
    {
        DebugLog("[PausePanel] 点击继续游戏");

        // 发布恢复游戏事件
        EventBus.Publish("ON_GAME_RESUME");
    }

    /// <summary>
    /// 游戏设置按钮点击
    /// </summary>
    private void OnSettingsClicked()
    {
        DebugLog("[PausePanel] 点击游戏设置");

        // 显示设置面板
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 返回主菜单按钮点击
    /// </summary>
    private void OnMainMenuClicked()
    {
        DebugLog("[PausePanel] 点击返回主菜单");

        // 发布返回主菜单事件
        EventBus.Publish("ON_RETURN_TO_MAINMENU");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 调试日志
    /// </summary>
    /// <param name="message">日志消息</param>
    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message);
        }
    }

    #endregion
}
