using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏结束面板控制器 - 玩家死亡时显示
/// 订阅ON_PLAYER_DIE事件，玩家死亡弹出面板
/// 按钮：重新开始、返回主菜单
/// WebGL平台兼容
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("游戏结束面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("游戏结束标题文本")]
    [SerializeField] private Text gameOverTitle;

    [Tooltip("重新开始按钮")]
    [SerializeField] private Button restartButton;

    [Tooltip("读取存档按钮")]
    [SerializeField] private Button loadSaveButton;

    [Tooltip("返回主菜单按钮")]
    [SerializeField] private Button mainMenuButton;

    [Header("显示设置")]
    [Tooltip("面板淡入时间（秒）")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>是否正在显示</summary>
    private bool isShowing = false;

    /// <summary>淡入计时器</summary>
    private float fadeInTimer = 0f;

    /// <summary>是否正在淡入</summary>
    private bool isFadingIn = false;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeGameOverPanel();
    }

    /// <summary>
    /// 每帧更新 - 处理淡入动画
    /// </summary>
    private void Update()
    {
        if (isFadingIn)
        {
            UpdateFadeIn();
        }
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
    /// 初始化游戏结束面板
    /// </summary>
    private void InitializeGameOverPanel()
    {
        if (isInitialized) return;

        // 绑定按钮事件
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        if (loadSaveButton != null)
        {
            loadSaveButton.onClick.AddListener(OnLoadSaveClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        // 设置标题文本
        if (gameOverTitle != null)
        {
            gameOverTitle.text = "GAME OVER";
        }

        // 初始隐藏面板
        HidePanel();

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[GameOverPanel] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家死亡事件处理
    /// </summary>
    private void OnPlayerDie()
    {
        // 延迟显示面板（等待死亡动画）
        Invoke(nameof(ShowPanel), 1f);
    }

    #endregion

    #region UI显示控制

    /// <summary>
    /// 显示游戏结束面板
    /// </summary>
    public void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        isShowing = true;
        isFadingIn = true;
        fadeInTimer = 0f;

        // 重置按钮状态
        if (restartButton != null)
        {
            restartButton.interactable = false;
        }
        if (loadSaveButton != null)
        {
            // 根据是否有存档启用/禁用按钮
            loadSaveButton.interactable = SaveManager.HasInstance && SaveManager.Instance.HasSaveData();
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.interactable = false;
        }

        DebugLog("[GameOverPanel] 显示游戏结束面板");
    }

    /// <summary>
    /// 隐藏游戏结束面板
    /// </summary>
    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        isShowing = false;
        isFadingIn = false;
        DebugLog("[GameOverPanel] 隐藏游戏结束面板");
    }

    /// <summary>
    /// 更新淡入动画
    /// </summary>
    private void UpdateFadeIn()
    {
        fadeInTimer += Time.deltaTime;
        float progress = fadeInTimer / fadeInDuration;

        if (progress >= 1f)
        {
            // 淡入完成
            isFadingIn = false;

            // 启用按钮
            if (restartButton != null)
            {
                restartButton.interactable = true;
            }
            if (loadSaveButton != null)
            {
                loadSaveButton.interactable = SaveManager.HasInstance && SaveManager.Instance.HasSaveData();
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.interactable = true;
            }
        }
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 重新开始按钮点击
    /// </summary>
    private void OnRestartClicked()
    {
        DebugLog("[GameOverPanel] 点击重新开始");

        // 隐藏面板
        HidePanel();

        // 发布重新开始事件（玩家重生）
        EventBus.Publish("ON_PLAYER_RESPAWN");
    }

    /// <summary>
    /// 读取存档按钮点击
    /// </summary>
    private void OnLoadSaveClicked()
    {
        DebugLog("[GameOverPanel] 点击读取存档");

        if (SaveManager.HasInstance && SaveManager.Instance.HasSaveData())
        {
            // 隐藏面板
            HidePanel();

            // 读取存档
            SaveManager.Instance.LoadGame();

            // 发布开始游戏事件（会从存档加载关卡）
            EventBus.Publish("ON_GAME_START");
        }
    }

    /// <summary>
    /// 返回主菜单按钮点击
    /// </summary>
    private void OnMainMenuClicked()
    {
        DebugLog("[GameOverPanel] 点击返回主菜单");

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
