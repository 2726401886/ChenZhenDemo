using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 成就UI面板 - J键开关
/// 列表展示全部成就，区分已解锁/未解锁
/// 显示成就名称、描述、奖励、解锁状态
/// 订阅ON_ACHIEVEMENT_UNLOCK，脏标记节流刷新
/// WebGL平台兼容
/// </summary>
public class AchievementHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI设置")]
    [Tooltip("成就面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("组件引用")]
    [Tooltip("玩家AchievementManager组件")]
    [SerializeField] private AchievementManager achievementManager;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isInitialized = false;
    private bool isDirty = false;
    private bool isPanelOpen = false;
    private float refreshInterval = 0.2f;
    private float refreshTimer = 0f;

    private Text titleText;
    private Text progressText;
    private Transform contentContainer;
    private List<GameObject> achievementSlots = new List<GameObject>();

    /// <summary>解锁/未解锁颜色</summary>
    private static readonly Color unlockedColor = new Color(1f, 0.9f, 0.4f);
    private static readonly Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

    /// <summary>弹窗相关</summary>
    private GameObject popupRoot;
    private Text popupTitleText;
    private Text popupDescText;
    private float popupTimer = 0f;
    private bool isPopupActive = false;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeHUD();
    }

    private void Update()
    {
        if (!isInitialized) return;

        if (isDirty && isPanelOpen)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshInterval)
            {
                isDirty = false;
                refreshTimer = 0f;
                RefreshUI();
            }
        }

        // 弹窗自动消失
        if (isPopupActive)
        {
            popupTimer -= Time.deltaTime;
            if (popupTimer <= 0f)
            {
                HidePopup();
            }
        }
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe(AchievementManager.ON_ACHIEVEMENT_UNLOCK, OnAchievementUnlock);
    }

    #endregion

    #region 初始化

    private void InitializeHUD()
    {
        if (isInitialized) return;

        if (achievementManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                achievementManager = player.GetComponent<AchievementManager>();
            }
        }

        CreatePanelUI();
        CreatePopupUI();

        EventBus.Subscribe(AchievementManager.ON_ACHIEVEMENT_UNLOCK, OnAchievementUnlock);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (popupRoot != null)
            popupRoot.SetActive(false);

        isInitialized = true;
        DebugLog("[AchievementHUD] 成就UI初始化完成");
    }

    private void CreatePanelUI()
    {
        if (panelRoot == null)
        {
            panelRoot = new GameObject("AchievementPanel");
            panelRoot.transform.SetParent(transform);
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;

        Image bg = panelRoot.GetComponent<Image>();
        if (bg == null) bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.1f, 0.18f, 0.95f);

        // 标题
        CreateText("Title", panelRoot.transform, new Vector2(0f, 0.93f), Vector2.one,
            "成就系统", 24, unlockedColor, TextAnchor.MiddleCenter);

        // 进度显示
        GameObject progressObj = CreateText("Progress", panelRoot.transform,
            new Vector2(0.7f, 0.93f), new Vector2(1f, 0.98f),
            "0/6", 18, Color.white, TextAnchor.MiddleRight);
        progressText = progressObj.GetComponent<Text>();

        // 内容容器（滚动区域）
        GameObject scrollObj = new GameObject("ScrollContainer");
        scrollObj.transform.SetParent(panelRoot.transform);
        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.02f, 0.05f);
        scrollRect.anchorMax = new Vector2(0.98f, 0.90f);
        scrollRect.sizeDelta = Vector2.zero;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportBg = viewport.AddComponent<Image>();
        viewportBg.color = new Color(0.08f, 0.07f, 0.12f, 0.5f);
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        scroll.viewport = viewportRect;

        contentContainer = viewport.transform;

        DebugLog("[AchievementHUD] 成就面板UI创建完成");
    }

    private void CreatePopupUI()
    {
        popupRoot = new GameObject("AchievementPopup");
        popupRoot.transform.SetParent(transform);
        RectTransform popupRect = popupRoot.AddComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.3f, 0.75f);
        popupRect.anchorMax = new Vector2(0.7f, 0.9f);
        popupRect.sizeDelta = Vector2.zero;

        Image popupBg = popupRoot.AddComponent<Image>();
        popupBg.color = new Color(0.2f, 0.15f, 0.05f, 0.95f);

        // 成就解锁提示
        CreateText("PopupLabel", popupRoot.transform, new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.95f),
            "成就解锁！", 14, new Color(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter);

        popupTitleText = CreateText("PopupTitle", popupRoot.transform,
            new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.65f),
            "", 18, Color.white, TextAnchor.MiddleCenter).GetComponent<Text>();

        popupDescText = CreateText("PopupDesc", popupRoot.transform,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.35f),
            "", 12, Color.gray, TextAnchor.MiddleCenter).GetComponent<Text>();
    }

    private GameObject CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        string text, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = Vector2.zero;
        Text uiText = obj.AddComponent<Text>();
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return obj;
    }

    #endregion

    #region 面板操作

    public void OpenPanel()
    {
        isPanelOpen = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshUI();
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void TogglePanel()
    {
        if (isPanelOpen) ClosePanel();
        else OpenPanel();
    }

    #endregion

    #region 事件处理

    private void OnAchievementUnlock(object data)
    {
        isDirty = true;

        // 显示弹窗
        if (achievementManager != null)
        {
            var allData = achievementManager.GetAllAchievementData();
            foreach (var kvp in allData)
            {
                if (kvp.Value.unlocked)
                {
                    var config = AchievementConfig.Get(kvp.Key);
                    if (config != null)
                    {
                        ShowPopup(config.displayName, config.description);
                        break;
                    }
                }
            }
        }

        DebugLog("[AchievementHUD] 成就解锁通知");
    }

    #endregion

    #region 弹窗

    private void ShowPopup(string title, string desc)
    {
        if (popupRoot == null) return;

        if (popupTitleText != null) popupTitleText.text = title;
        if (popupDescText != null) popupDescText.text = desc;

        popupRoot.SetActive(true);
        popupTimer = 3f;
        isPopupActive = true;
    }

    private void HidePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
        isPopupActive = false;
    }

    #endregion

    #region UI刷新

    private void RefreshUI()
    {
        if (achievementManager == null) return;

        // 更新进度
        int unlocked = achievementManager.GetUnlockedCount();
        int total = achievementManager.GetTotalCount();
        if (progressText != null)
            progressText.text = $"{unlocked}/{total}";

        // 刷新成就列表
        RefreshAchievementList();
    }

    private void RefreshAchievementList()
    {
        foreach (var slot in achievementSlots)
            if (slot != null) Destroy(slot);
        achievementSlots.Clear();

        if (achievementManager == null || contentContainer == null) return;

        var allConfigs = AchievementConfig.GetAll();
        int index = 0;

        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            bool isUnlocked = achievementManager.IsUnlocked(config.achievementId);

            GameObject slotObj = CreateAchievementSlot(config, isUnlocked, index);
            achievementSlots.Add(slotObj);
            index++;
        }
    }

    private GameObject CreateAchievementSlot(AchievementConfig config, bool isUnlocked, int index)
    {
        GameObject slotObj = new GameObject("Achievement_" + config.achievementId);
        slotObj.transform.SetParent(contentContainer);
        RectTransform rect = slotObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(0, 70f);
        rect.anchoredPosition = new Vector2(0, -(index * 75f + 5f));

        Image bg = slotObj.AddComponent<Image>();
        bg.color = isUnlocked
            ? new Color(0.2f, 0.18f, 0.1f, 0.9f)
            : new Color(0.12f, 0.12f, 0.15f, 0.8f);

        // 状态标签
        string statusText = isUnlocked ? "已解锁" : "未解锁";
        Color statusColor = isUnlocked ? unlockedColor : lockedColor;
        CreateSlotText("Status", slotObj.transform, new Vector2(0.02f, 0.6f), new Vector2(0.15f, 0.95f),
            statusText, 11, statusColor, TextAnchor.MiddleLeft);

        // 成就名称
        Color nameColor = isUnlocked ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        CreateSlotText("Name", slotObj.transform, new Vector2(0.16f, 0.6f), new Vector2(0.55f, 0.95f),
            config.displayName, 14, nameColor, TextAnchor.MiddleLeft);

        // 奖励信息
        string rewardText = GetRewardText(config);
        CreateSlotText("Reward", slotObj.transform, new Vector2(0.56f, 0.6f), new Vector2(0.98f, 0.95f),
            rewardText, 12, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleRight);

        // 成就描述
        CreateSlotText("Desc", slotObj.transform, new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.55f),
            config.description, 11, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleLeft);

        return slotObj;
    }

    private string GetRewardText(AchievementConfig config)
    {
        switch (config.rewardType)
        {
            case AchievementConfig.RewardType.Coin:
                return $"奖励: 金币x{config.rewardValue}";
            case AchievementConfig.RewardType.Item:
                return $"奖励: {config.rewardItemId}x{config.rewardValue}";
            default:
                return "奖励: 无";
        }
    }

    private void CreateSlotText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        string text, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = Vector2.zero;
        Text uiText = obj.AddComponent<Text>();
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
