using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择面板控制器 - 显示可选关卡列表
/// 根据存档中已解锁的关卡显示按钮
/// 支持关卡预览和选择
/// WebGL平台兼容
/// </summary>
public class LevelSelectPanel : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("关卡选择面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("关卡按钮容器")]
    [SerializeField] private Transform levelButtonContainer;

    [Tooltip("关卡按钮预制体")]
    [SerializeField] private GameObject levelButtonPrefab;

    [Tooltip("返回按钮")]
    [SerializeField] private Button backButton;

    [Tooltip("关卡信息文本")]
    [SerializeField] private Text levelInfoText;

    [Header("关卡显示设置")]
    [Tooltip("最大显示关卡数")]
    [SerializeField] private int maxDisplayLevels = 10;

    [Tooltip("已解锁按钮颜色")]
    [SerializeField] private Color unlockedColor = new Color(0.2f, 0.7f, 0.2f);

    [Tooltip("未解锁按钮颜色")]
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("当前关卡按钮颜色")]
    [SerializeField] private Color currentColor = new Color(0.3f, 0.5f, 0.8f);

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>关卡按钮列表</summary>
    private Button[] levelButtons;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeLevelSelectPanel();
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        // 清理按钮列表
        if (levelButtons != null)
        {
            levelButtons = null;
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化关卡选择面板
    /// </summary>
    private void InitializeLevelSelectPanel()
    {
        if (isInitialized) return;

        // 绑定返回按钮
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        // 初始隐藏面板
        HidePanel();

        isInitialized = true;
        DebugLog("[LevelSelectPanel] 初始化完成");
    }

    #endregion

    #region UI显示控制

    /// <summary>
    /// 显示关卡选择面板
    /// </summary>
    public void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        // 刷新关卡列表
        RefreshLevelList();

        DebugLog("[LevelSelectPanel] 显示关卡选择面板");
    }

    /// <summary>
    /// 隐藏关卡选择面板
    /// </summary>
    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        DebugLog("[LevelSelectPanel] 隐藏关卡选择面板");
    }

    #endregion

    #region 关卡列表

    /// <summary>
    /// 刷新关卡列表
    /// </summary>
    private void RefreshLevelList()
    {
        if (!LevelManager.HasInstance)
        {
            Debug.LogWarning("[LevelSelectPanel] LevelManager不存在");
            return;
        }

        // 获取所有关卡配置
        var levelConfigs = LevelManager.Instance.GetAllLevelConfigs();
        int totalLevels = Mathf.Min(levelConfigs.Count, maxDisplayLevels);

        // 获取已解锁关卡数
        int unlockedLevels = 1;
        if (SaveManager.HasInstance)
        {
            unlockedLevels = SaveManager.Instance.GetUnlockedLevels();
        }

        // 当前关卡ID
        int currentLevelId = LevelManager.Instance.CurrentLevelId;

        // 清空容器
        if (levelButtonContainer != null)
        {
            foreach (Transform child in levelButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 创建关卡按钮
        levelButtons = new Button[totalLevels];
        for (int i = 0; i < totalLevels; i++)
        {
            var config = levelConfigs[i];
            bool isUnlocked = config.levelId <= unlockedLevels;
            bool isCurrent = config.levelId == currentLevelId;

            // 创建按钮
            GameObject btnObj;
            if (levelButtonPrefab != null && levelButtonContainer != null)
            {
                btnObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            }
            else
            {
                // 如果没有预制体，创建简单的按钮
                btnObj = CreateSimpleLevelButton(config.levelId, config.levelName, isUnlocked, isCurrent);
            }

            // 配置按钮
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                levelButtons[i] = btn;

                // 设置按钮颜色
                ColorBlock colors = btn.colors;
                if (isCurrent)
                {
                    colors.normalColor = currentColor;
                }
                else if (isUnlocked)
                {
                    colors.normalColor = unlockedColor;
                }
                else
                {
                    colors.normalColor = lockedColor;
                }
                btn.colors = colors;

                // 绑定点击事件
                int levelId = config.levelId;
                if (isUnlocked)
                {
                    btn.onClick.AddListener(() => OnLevelSelected(levelId));
                }
                else
                {
                    btn.interactable = false;
                }
            }

            // 更新按钮文本
            Text btnText = btnObj.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = isUnlocked ? config.levelName : $"???";
            }
        }

        // 更新关卡信息
        UpdateLevelInfo(levelConfigs.Count, unlockedLevels);

        DebugLog($"[LevelSelectPanel] 刷新关卡列表 - 总数: {totalLevels}, 已解锁: {unlockedLevels}");
    }

    /// <summary>
    /// 创建简单的关卡按钮（无预制体时使用）
    /// </summary>
    private GameObject CreateSimpleLevelButton(int levelId, string levelName, bool isUnlocked, bool isCurrent)
    {
        GameObject btnObj = new GameObject($"Level_{levelId}");
        if (levelButtonContainer != null)
        {
            btnObj.transform.SetParent(levelButtonContainer);
        }

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);

        Image image = btnObj.AddComponent<Image>();
        image.color = isCurrent ? currentColor : (isUnlocked ? unlockedColor : lockedColor);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = image;

        // 添加文本
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textObj.AddComponent<Text>();
        text.text = isUnlocked ? levelName : "???";
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btnObj;
    }

    /// <summary>
    /// 更新关卡信息文本
    /// </summary>
    private void UpdateLevelInfo(int totalLevels, int unlockedLevels)
    {
        if (levelInfoText != null)
        {
            levelInfoText.text = $"关卡进度: {unlockedLevels}/{totalLevels}";
        }
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 关卡被选中
    /// </summary>
    /// <param name="levelId">关卡ID</param>
    private void OnLevelSelected(int levelId)
    {
        DebugLog($"[LevelSelectPanel] 选择关卡: {levelId}");

        // 隐藏面板
        HidePanel();

        // 开始关卡
        if (LevelManager.HasInstance)
        {
            LevelManager.Instance.StartLevel(levelId);
        }
    }

    /// <summary>
    /// 返回按钮点击
    /// </summary>
    private void OnBackClicked()
    {
        DebugLog("[LevelSelectPanel] 点击返回");

        // 隐藏面板
        HidePanel();

        // 显示主菜单
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
