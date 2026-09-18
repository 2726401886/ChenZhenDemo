using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情对话组件 - 挂载在NPC上，显示对话弹窗
/// 支持分段文字、继续/关闭操作
/// 接取/交付任务时触发剧情文本
/// WebGL平台兼容
/// </summary>
public class QuestDialogue : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("对话设置")]
    [Tooltip("NPC显示名称")]
    [SerializeField] private string npcName = "NPC";

    [Tooltip("空闲状态对话（无任务时的闲聊）")]
    [TextArea(2, 5)]
    [SerializeField] private string idleDialogue = "你好，旅行者。";

    [Tooltip("交互触发距离")]
    [SerializeField] private float interactDistance = 3f;

    [Tooltip("提示文字偏移")]
    [SerializeField] private Vector3 tipOffset = new Vector3(0f, 2.5f, 0f);

    [Header("组件引用")]
    [Tooltip("玩家QuestManager组件")]
    [SerializeField] private QuestManager questManager;

    [Tooltip("关联网联任务ID（空则不关联任务）")]
    [SerializeField] private string linkedQuestId = "";

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isPlayerNearby = false;
    private bool isDialogueActive = false;
    private GameObject tipObject;
    private GameObject dialogueRoot;
    private Text speakerText;
    private Text contentText;
    private Button continueButton;
    private Button closeButton;

    private string[] dialogueLines;
    private int currentLineIndex = 0;

    private static Transform cachedPlayerTransform;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        CreateDialogueUI();
    }

    private void Update()
    {
        if (!isDialogueActive)
        {
            CheckPlayerDistance();

            if (isPlayerNearby && Input.GetKeyDown(KeyCode.F))
            {
                StartDialogue();
            }
        }
        else
        {
            // 对话中按空格继续
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                ContinueDialogue();
            }
        }
    }

    private void OnDestroy()
    {
        if (isDialogueActive)
        {
            CloseDialogue();
        }
    }

    #endregion

    #region 交互逻辑

    private void CheckPlayerDistance()
    {
        if (cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                cachedPlayerTransform = player.transform;
        }

        if (cachedPlayerTransform == null) return;

        float dist = Vector3.Distance(transform.position, cachedPlayerTransform.position);
        bool wasNearby = isPlayerNearby;
        isPlayerNearby = dist <= interactDistance;

        if (isPlayerNearby && !wasNearby)
        {
            ShowTip("[F] 对话");
        }
        else if (!isPlayerNearby && wasNearby)
        {
            HideTip();
        }
    }

    #endregion

    #region 对话逻辑

    private void StartDialogue()
    {
        if (questManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                questManager = player.GetComponent<QuestManager>();
        }

        // 获取对话文本
        string dialogue = GetDialogueText();

        // 分割对话行
        dialogueLines = dialogue.Split('\n');
        currentLineIndex = 0;

        isDialogueActive = true;
        ShowDialogueUI();
        ShowCurrentLine();

        HideTip();
        DebugLog($"[QuestDialogue] {npcName}开始对话");
    }

    private string GetDialogueText()
    {
        if (questManager == null || string.IsNullOrEmpty(linkedQuestId))
        {
            return idleDialogue;
        }

        var questData = questManager.GetQuestData(linkedQuestId);
        var questConfig = QuestConfig.Get(linkedQuestId);

        if (questData == null || questConfig == null)
        {
            return idleDialogue;
        }

        switch (questData.state)
        {
            case QuestData.QuestState.NotAccepted:
                return questConfig.acceptDialogue;

            case QuestData.QuestState.InProgress:
                return $"正在进行任务：{questConfig.displayName}\n当前进度：{questData.progress}/{questConfig.targetCount}";

            case QuestData.QuestState.Completed:
                return questConfig.completeDialogue;

            case QuestData.QuestState.Finished:
                return "感谢你，旅行者！祝你旅途顺利。";

            default:
                return idleDialogue;
        }
    }

    private void ShowCurrentLine()
    {
        if (dialogueLines == null || currentLineIndex >= dialogueLines.Length)
        {
            CloseDialogue();
            return;
        }

        if (contentText != null)
            contentText.text = dialogueLines[currentLineIndex];

        if (speakerText != null)
            speakerText.text = npcName;

        // 最后一行显示关闭按钮，其他行显示继续按钮
        bool isLastLine = currentLineIndex >= dialogueLines.Length - 1;
        if (continueButton != null) continueButton.gameObject.SetActive(!isLastLine);
        if (closeButton != null) closeButton.gameObject.SetActive(isLastLine);
    }

    private void ContinueDialogue()
    {
        currentLineIndex++;

        if (currentLineIndex >= dialogueLines.Length)
        {
            // 对话结束，执行任务逻辑
            OnDialogueEnd();
            CloseDialogue();
            return;
        }

        ShowCurrentLine();
    }

    private void OnDialogueEnd()
    {
        if (questManager == null || string.IsNullOrEmpty(linkedQuestId)) return;

        var questData = questManager.GetQuestData(linkedQuestId);
        if (questData == null) return;

        // 如果任务已完成，自动交付
        if (questData.state == QuestData.QuestState.Completed)
        {
            questManager.FinishQuest(linkedQuestId);
            DebugLog($"[QuestDialogue] 自动交付任务：{linkedQuestId}");
        }
        // 如果任务未接取，自动接取
        else if (questData.state == QuestData.QuestState.NotAccepted)
        {
            questManager.AcceptQuest(linkedQuestId);
            DebugLog($"[QuestDialogue] 自动接取任务：{linkedQuestId}");
        }
    }

    private void CloseDialogue()
    {
        isDialogueActive = false;
        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    #endregion

    #region UI创建

    private void CreateDialogueUI()
    {
        // 创建对话面板（Canvas世界空间）
        dialogueRoot = new GameObject("DialoguePanel_" + npcName);
        dialogueRoot.transform.SetParent(transform);
        dialogueRoot.transform.localPosition = new Vector3(0f, 3f, 0f);
        dialogueRoot.transform.localScale = new Vector3(0.008f, 0.008f, 0.008f);

        Canvas canvas = dialogueRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;

        RectTransform canvasRect = dialogueRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400f, 150f);

        dialogueRoot.AddComponent<CanvasScaler>();

        // 背景
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(dialogueRoot.transform);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        // 说话者名字
        GameObject speakerObj = new GameObject("Speaker");
        speakerObj.transform.SetParent(dialogueRoot.transform);
        RectTransform speakerRect = speakerObj.AddComponent<RectTransform>();
        speakerRect.anchorMin = new Vector2(0.05f, 0.75f);
        speakerRect.anchorMax = new Vector2(0.95f, 0.95f);
        speakerRect.sizeDelta = Vector2.zero;
        speakerText = speakerObj.AddComponent<Text>();
        speakerText.text = npcName;
        speakerText.fontSize = 16;
        speakerText.fontStyle = FontStyle.Bold;
        speakerText.alignment = TextAnchor.MiddleLeft;
        speakerText.color = new Color(1f, 0.9f, 0.5f);
        speakerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 对话内容
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(dialogueRoot.transform);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.05f, 0.25f);
        contentRect.anchorMax = new Vector2(0.95f, 0.72f);
        contentRect.sizeDelta = Vector2.zero;
        contentText = contentObj.AddComponent<Text>();
        contentText.text = "";
        contentText.fontSize = 14;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.color = Color.white;
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 继续按钮
        continueButton = CreateDialogueButton("ContinueBtn", "[空格] 继续",
            new Vector2(0.55f, 0.02f), new Vector2(0.78f, 0.22f));

        // 关闭按钮
        closeButton = CreateDialogueButton("CloseBtn", "[空格] 关闭",
            new Vector2(0.8f, 0.02f), new Vector2(0.98f, 0.22f));

        dialogueRoot.SetActive(false);
    }

    private Button CreateDialogueButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(dialogueRoot.transform);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchorMin;
        btnRect.anchorMax = anchorMax;
        btnRect.sizeDelta = Vector2.zero;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text uiText = textObj.AddComponent<Text>();
        uiText.text = label;
        uiText.fontSize = 12;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = Color.white;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (name == "ContinueBtn")
            btn.onClick.AddListener(ContinueDialogue);
        else
            btn.onClick.AddListener(CloseDialogue);

        return btn;
    }

    private void ShowDialogueUI()
    {
        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);
    }

    #endregion

    #region 提示文字

    private void ShowTip(string text)
    {
        if (tipObject == null)
        {
            tipObject = new GameObject("TalkTip");
            tipObject.transform.SetParent(transform);
            tipObject.transform.localPosition = tipOffset;

            Canvas canvas = tipObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(150f, 30f);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            tipObject.AddComponent<CanvasScaler>();

            GameObject textObj = new GameObject("TipText");
            textObj.transform.SetParent(tipObject.transform);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text uiText = textObj.AddComponent<Text>();
            uiText.text = text;
            uiText.fontSize = 14;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = Color.cyan;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        else
        {
            tipObject.SetActive(true);
            var uiText = tipObject.GetComponentInChildren<Text>();
            if (uiText != null) uiText.text = text;
        }
    }

    private void HideTip()
    {
        if (tipObject != null)
            tipObject.SetActive(false);
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
