using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单控制器 - 游戏启动时显示
/// 包含：开始游戏、设置、退出游戏按钮
/// 点击开始游戏后隐藏主菜单，生成战斗场景
/// WebGL平台兼容
/// </summary>
public class MainMenu : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("主菜单根对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("游戏标题文本")]
    [SerializeField] private Text titleText;

    [Tooltip("开始游戏按钮")]
    [SerializeField] private Button startGameButton;

    [Tooltip("读取存档按钮")]
    [SerializeField] private Button loadGameButton;

    [Tooltip("关卡选择按钮")]
    [SerializeField] private Button levelSelectButton;

    [Tooltip("游戏设置按钮")]
    [SerializeField] private Button settingsButton;

    [Tooltip("退出游戏按钮")]
    [SerializeField] private Button quitButton;

    [Header("设置面板")]
    [Tooltip("设置面板对象")]
    [SerializeField] private GameObject settingsPanel;

    [Header("关卡选择面板")]
    [Tooltip("关卡选择面板对象")]
    [SerializeField] private GameObject levelSelectPanel;

    [Header("显示设置")]
    [Tooltip("主菜单背景颜色")]
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);

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

    #region 公共属性

    /// <summary>是否正在显示</summary>
    public bool IsShowing => isShowing;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeMainMenu();
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
    /// 初始化主菜单
    /// </summary>
    private void InitializeMainMenu()
    {
        if (isInitialized) return;

        // 设置标题文本
        if (titleText != null)
        {
            titleText.text = "ChenZhenDemo";
        }

        // 绑定按钮事件
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.AddListener(OnLoadGameClicked);
            // 根据是否有存档启用/禁用按钮
            loadGameButton.interactable = SaveManager.HasInstance && SaveManager.Instance.HasSaveData();
        }

        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        // 订阅事件
        SubscribeEvents();

        // 显示主菜单
        ShowMenu();

        isInitialized = true;
        DebugLog("[MainMenu] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_RETURN_TO_MAINMENU", OnReturnToMainMenu);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_RETURN_TO_MAINMENU", OnReturnToMainMenu);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 返回主菜单事件处理
    /// </summary>
    private void OnReturnToMainMenu()
    {
        ShowMenu();
    }

    #endregion

    #region UI显示控制

    /// <summary>
    /// 显示主菜单
    /// </summary>
    public void ShowMenu()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        // 隐藏其他面板
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
        }

        // 更新读取存档按钮状态
        if (loadGameButton != null)
        {
            loadGameButton.interactable = SaveManager.HasInstance && SaveManager.Instance.HasSaveData();
        }

        isShowing = true;
        DebugLog("[MainMenu] 显示主菜单");
    }

    /// <summary>
    /// 隐藏主菜单
    /// </summary>
    public void HideMenu()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        isShowing = false;
        DebugLog("[MainMenu] 隐藏主菜单");
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 开始游戏按钮点击
    /// </summary>
    private void OnStartGameClicked()
    {
        DebugLog("[MainMenu] 点击开始游戏");

        // 隐藏主菜单
        HideMenu();

        // 发布开始游戏事件
        EventBus.Publish("ON_GAME_START");
    }

    /// <summary>
    /// 读取存档按钮点击
    /// </summary>
    private void OnLoadGameClicked()
    {
        DebugLog("[MainMenu] 点击读取存档");

        if (SaveManager.HasInstance && SaveManager.Instance.HasSaveData())
        {
            // 隐藏主菜单
            HideMenu();

            // 读取存档
            SaveManager.Instance.LoadGame();

            // 发布开始游戏事件（会从存档加载关卡）
            EventBus.Publish("ON_GAME_START");
        }
    }

    /// <summary>
    /// 关卡选择按钮点击
    /// </summary>
    private void OnLevelSelectClicked()
    {
        DebugLog("[MainMenu] 点击关卡选择");

        // 隐藏主菜单面板
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // 显示关卡选择面板
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 游戏设置按钮点击
    /// </summary>
    private void OnSettingsClicked()
    {
        DebugLog("[MainMenu] 点击游戏设置");

        // 显示设置面板
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 退出游戏按钮点击
    /// </summary>
    private void OnQuitClicked()
    {
        DebugLog("[MainMenu] 点击退出游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
