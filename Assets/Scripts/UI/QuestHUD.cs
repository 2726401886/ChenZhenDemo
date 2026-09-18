using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 任务UI面板 - L键开关
/// 面板：全部任务列表，区分主线/支线
/// 右上角常驻简易任务追踪提示
/// 订阅任务事件，脏标记节流刷新
/// WebGL平台兼容
/// </summary>
public class QuestHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI设置")]
    [Tooltip("任务面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("组件引用")]
    [Tooltip("玩家QuestManager组件")]
    [SerializeField] private QuestManager questManager;

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

    private Text progressText;
    private Transform contentContainer;
    private List<GameObject> questSlots = new List<GameObject>();

    // 右上角追踪提示
    private GameObject trackerRoot;
    private Text trackerText;

    // 状态颜色
    private static readonly Color notAcceptedColor = new Color(0.4f, 0.4f, 0.4f);
    private static readonly Color inProgressColor = new Color(0.5f, 0.9f, 1f);
    private static readonly Color completedColor = new Color(1f, 0.9f, 0.4f);
    private static readonly Color finishedColor = new Color(0.4f, 0.8f, 0.4f);

    // 任务状态名称
    private static readonly string[] stateNames = { "未接取", "进行中", "等待交付", "已完成" };

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeHUD();
    }

    private void Update()
    {
        if (!isInitialized) return;

        if (isDirty)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshInterval)
            {
                isDirty = false;
                refreshTimer = 0f;
                if (isPanelOpen) RefreshUI();
                RefreshTracker();
            }
        }
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe(QuestManager.ON_QUEST_ACCEPT, OnQuestEvent);
        EventBus.Unsubscribe(QuestManager.ON_QUEST_PROGRESS_UPDATE, OnQuestEvent);
        EventBus.Unsubscribe(QuestManager.ON_QUEST_COMPLETE, OnQuestEvent);
        EventBus.Unsubscribe(QuestManager.ON_QUEST_FINISH_REWARD, OnQuestEvent);
    }

    #endregion

    #region 初始化

    private void InitializeHUD()
    {
        if (isInitialized) return;

        if (questManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                questManager = player.GetComponent<QuestManager>();
            }
        }

        CreatePanelUI();
        CreateTrackerUI();

        EventBus.Subscribe(QuestManager.ON_QUEST_ACCEPT, OnQuestEvent);
        EventBus.Subscribe(QuestManager.ON_QUEST_PROGRESS_UPDATE, OnQuestEvent);
        EventBus.Subscribe(QuestManager.ON_QUEST_COMPLETE, OnQuestEvent);
        EventBus.Subscribe(QuestManager.ON_QUEST_FINISH_REWARD, OnQuestEvent);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        isInitialized = true;
        RefreshTracker();
        DebugLog("[QuestHUD] 任务UI初始化完成");
    }

    private void CreatePanelUI()
    {
        if (panelRoot == null)
        {
            panelRoot = new GameObject("QuestPanel");
            panelRoot.transform.SetParent(transform);
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;

        Image bg = panelRoot.GetComponent<Image>();
        if (bg == null) bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

        // 标题
        CreateText("Title", panelRoot.transform, new Vector2(0f, 0.93f), Vector2.one,
            "任务系统", 24, new Color(0.5f, 0.9f, 1f), TextAnchor.MiddleCenter);

        // 进度显示
        GameObject progressObj = CreateText("Progress", panelRoot.transform,
            new Vector2(0.7f, 0.93f), new Vector2(1f, 0.98f),
            "0/5", 18, Color.white, TextAnchor.MiddleRight);
        progressText = progressObj.GetComponent<Text>();

        // 滚动区域
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
        viewportBg.color = new Color(0.06f, 0.07f, 0.1f, 0.5f);
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        scroll.viewport = viewportRect;

        contentContainer = viewport.transform;

        DebugLog("[QuestHUD] 任务面板UI创建完成");
    }

    private void CreateTrackerUI()
    {
        trackerRoot = new GameObject("QuestTracker");
        trackerRoot.transform.SetParent(transform);
        RectTransform trackerRect = trackerRoot.AddComponent<RectTransform>();
        trackerRect.anchorMin = new Vector2(0.6f, 0.85f);
        trackerRect.anchorMax = new Vector2(0.98f, 0.98f);
        trackerRect.sizeDelta = Vector2.zero;

        Image trackerBg = trackerRoot.AddComponent<Image>();
        trackerBg.color = new Color(0.08f, 0.1f, 0.15f, 0.85f);

        GameObject textObj = new GameObject("TrackerText");
        textObj.transform.SetParent(trackerRoot.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.05f);
        textRect.anchorMax = new Vector2(0.95f, 0.95f);
        textRect.sizeDelta = Vector2.zero;
        trackerText = textObj.AddComponent<Text>();
        trackerText.text = "";
        trackerText.fontSize = 12;
        trackerText.alignment = TextAnchor.UpperLeft;
        trackerText.color = inProgressColor;
        trackerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    private void OnQuestEvent(object data)
    {
        isDirty = true;
    }

    #endregion

    #region UI刷新

    private void RefreshUI()
    {
        if (questManager == null) return;

        // 更新进度
        int total = QuestConfig.GetAll().Count;
        int finished = questManager.GetFinishedMainQuestCount();
        if (progressText != null)
            progressText.text = $"{finished}/{total}";

        RefreshQuestList();
    }

    private void RefreshTracker()
    {
        if (trackerText == null || questManager == null) return;

        var activeQuest = questManager.GetActiveMainQuest();
        if (activeQuest != null)
        {
            var config = activeQuest.GetConfig();
            if (config != null)
            {
                trackerText.text = $"[主线] {config.displayName}\n{activeQuest.progress}/{config.targetCount}";
                trackerText.color = inProgressColor;
            }
        }
        else
        {
            trackerText.text = "暂无进行中的主线任务";
            trackerText.color = notAcceptedColor;
        }
    }

    private void RefreshQuestList()
    {
        foreach (var slot in questSlots)
            if (slot != null) Destroy(slot);
        questSlots.Clear();

        if (questManager == null || contentContainer == null) return;

        var allConfigs = QuestConfig.GetAll();
        int index = 0;

        // 先显示主线任务
        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            if (!config.isMainQuest) continue;

            var data = questManager.GetQuestData(kvp.Key);
            QuestData.QuestState state = data != null ? data.state : QuestData.QuestState.NotAccepted;
            int progress = data != null ? data.progress : 0;

            GameObject slotObj = CreateQuestSlot(config, state, progress, index, true);
            questSlots.Add(slotObj);
            index++;
        }

        // 再显示支线任务
        foreach (var kvp in allConfigs)
        {
            var config = kvp.Value;
            if (config.isMainQuest) continue;

            var data = questManager.GetQuestData(kvp.Key);
            QuestData.QuestState state = data != null ? data.state : QuestData.QuestState.NotAccepted;
            int progress = data != null ? data.progress : 0;

            GameObject slotObj = CreateQuestSlot(config, state, progress, index, false);
            questSlots.Add(slotObj);
            index++;
        }
    }

    private GameObject CreateQuestSlot(QuestConfig config, QuestData.QuestState state, int progress, int index, bool isMain)
    {
        GameObject slotObj = new GameObject("Quest_" + config.questId);
        slotObj.transform.SetParent(contentContainer);
        RectTransform rect = slotObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(0, 85f);
        rect.anchoredPosition = new Vector2(0, -(index * 90f + 5f));

        Image bg = slotObj.AddComponent<Image>();
        bg.color = isMain
            ? new Color(0.12f, 0.15f, 0.22f, 0.9f)
            : new Color(0.12f, 0.12f, 0.15f, 0.85f);

        // 主线/支线标记
        string tagText = isMain ? "【主线】" : "【支线】";
        Color tagColor = isMain ? new Color(0.5f, 0.8f, 1f) : new Color(0.7f, 0.7f, 0.5f);
        CreateSlotText("Tag", slotObj.transform, new Vector2(0.02f, 0.72f), new Vector2(0.18f, 0.95f),
            tagText, 11, tagColor, TextAnchor.MiddleLeft);

        // 任务名称
        CreateSlotText("Name", slotObj.transform, new Vector2(0.19f, 0.72f), new Vector2(0.65f, 0.95f),
            config.displayName, 14, Color.white, TextAnchor.MiddleLeft);

        // 状态
        Color stateColor;
        switch (state)
        {
            case QuestData.QuestState.InProgress: stateColor = inProgressColor; break;
            case QuestData.QuestState.Completed: stateColor = completedColor; break;
            case QuestData.QuestState.Finished: stateColor = finishedColor; break;
            default: stateColor = notAcceptedColor; break;
        }
        CreateSlotText("State", slotObj.transform, new Vector2(0.66f, 0.72f), new Vector2(0.98f, 0.95f),
            stateNames[(int)state], 12, stateColor, TextAnchor.MiddleRight);

        // 任务描述
        CreateSlotText("Desc", slotObj.transform, new Vector2(0.02f, 0.4f), new Vector2(0.98f, 0.7f),
            config.description, 11, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleLeft);

        // 进度/奖励
        string progressText = state == QuestData.QuestState.InProgress
            ? $"进度: {progress}/{config.targetCount}"
            : "";
        string rewardText = $"奖励: 金币{config.rewardCoins}";
        if (!string.IsNullOrEmpty(config.rewardItemId))
            rewardText += $" + {config.rewardItemId}x{config.rewardItemCount}";

        CreateSlotText("Progress", slotObj.transform, new Vector2(0.02f, 0.08f), new Vector2(0.4f, 0.38f),
            progressText, 11, inProgressColor, TextAnchor.MiddleLeft);
        CreateSlotText("Reward", slotObj.transform, new Vector2(0.41f, 0.08f), new Vector2(0.98f, 0.38f),
            rewardText, 11, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleRight);

        return slotObj;
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
