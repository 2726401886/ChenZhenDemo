using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 装备强化UI面板 - K键开关
/// 左侧：穿戴装备列表，选中待强化装备
/// 右侧：显示强化前后属性、消耗、成功率
/// 订阅强化事件，脏标记节流刷新UI
/// WebGL平台兼容
/// </summary>
public class EnhanceHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI设置")]
    [Tooltip("强化面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("组件引用")]
    [Tooltip("玩家EnhanceManager组件")]
    [SerializeField] private EnhanceManager enhanceManager;

    [Tooltip("玩家EquipmentManager组件")]
    [SerializeField] private EquipmentManager equipmentManager;

    [Tooltip("玩家CoinManager组件")]
    [SerializeField] private CoinManager coinManager;

    [Tooltip("玩家Inventory组件")]
    [SerializeField] private Inventory inventory;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isInitialized = false;
    private bool isDirty = false;
    private bool isPanelOpen = false;
    private float refreshInterval = 0.15f;
    private float refreshTimer = 0f;
    private int selectedSlotIndex = -1;

    private Text coinText;
    private Text titleText;
    private Text currentLevelText;
    private Text nextLevelText;
    private Text currentAtkText;
    private Text nextAtkText;
    private Text currentDefText;
    private Text nextDefText;
    private Text costCoinText;
    private Text costMaterialText;
    private Text successRateText;
    private Text enhanceResultText;
    private Button enhanceButton;
    private List<GameObject> equipSlots = new List<GameObject>();

    /// <summary>稀有度颜色</summary>
    private static readonly Color[] rarityColors = {
        Color.white, new Color(0.3f, 0.8f, 0.3f), Color.cyan, new Color(0.7f, 0.3f, 1f)
    };

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
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe(EnhanceNPC.ON_ENHANCE_UI_OPEN, OnEnhanceOpen);
        EventBus.Unsubscribe(EnhanceNPC.ON_ENHANCE_UI_CLOSE, OnEnhanceClose);
        EventBus.Unsubscribe(EnhanceManager.ON_EQUIP_ENHANCE_SUCCESS, OnEnhanceSuccess);
        EventBus.Unsubscribe(EnhanceManager.ON_EQUIP_ENHANCE_FAIL, OnEnhanceFail);
        EventBus.Unsubscribe(CoinManager.ON_COIN_CHANGE, OnDataChange);
    }

    #endregion

    #region 初始化

    private void InitializeHUD()
    {
        if (isInitialized) return;

        if (enhanceManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                enhanceManager = player.GetComponent<EnhanceManager>();
                equipmentManager = player.GetComponent<EquipmentManager>();
                coinManager = player.GetComponent<CoinManager>();
                inventory = player.GetComponent<Inventory>();
            }
        }

        CreatePanelUI();

        EventBus.Subscribe(EnhanceNPC.ON_ENHANCE_UI_OPEN, OnEnhanceOpen);
        EventBus.Subscribe(EnhanceNPC.ON_ENHANCE_UI_CLOSE, OnEnhanceClose);
        EventBus.Subscribe(EnhanceManager.ON_EQUIP_ENHANCE_SUCCESS, OnEnhanceSuccess);
        EventBus.Subscribe(EnhanceManager.ON_EQUIP_ENHANCE_FAIL, OnEnhanceFail);
        EventBus.Subscribe(CoinManager.ON_COIN_CHANGE, OnDataChange);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        isInitialized = true;
        DebugLog("[EnhanceHUD] 强化UI初始化完成");
    }

    private void CreatePanelUI()
    {
        if (panelRoot == null)
        {
            panelRoot = new GameObject("EnhancePanel");
            panelRoot.transform.SetParent(transform);
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;

        Image bg = panelRoot.GetComponent<Image>();
        if (bg == null) bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.08f, 0.08f, 0.95f);

        // 标题
        CreateText("Title", panelRoot.transform, new Vector2(0f, 0.93f), Vector2.one,
            "强化工坊", 24, new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleCenter);

        // 金币显示
        GameObject coinObj = CreateText("CoinText", panelRoot.transform, new Vector2(0.7f, 0.93f), new Vector2(1f, 0.98f),
            "金币: 0", 18, Color.yellow, TextAnchor.MiddleRight);
        coinText = coinObj.GetComponent<Text>();

        // 左侧 - 穿戴装备列表
        CreateText("EquipTitle", panelRoot.transform, new Vector2(0.02f, 0.88f), new Vector2(0.35f, 0.92f),
            "【穿戴装备】", 16, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleLeft);

        GameObject equipListObj = new GameObject("EquipList");
        equipListObj.transform.SetParent(panelRoot.transform);
        RectTransform equipListRect = equipListObj.AddComponent<RectTransform>();
        equipListRect.anchorMin = new Vector2(0.02f, 0.15f);
        equipListRect.anchorMax = new Vector2(0.35f, 0.87f);
        equipListRect.sizeDelta = Vector2.zero;

        // 右侧 - 强化信息
        CreateText("InfoTitle", panelRoot.transform, new Vector2(0.38f, 0.88f), new Vector2(0.98f, 0.92f),
            "【强化信息】", 16, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleLeft);

        // 当前等级
        currentLevelText = CreateText("CurrentLevel", panelRoot.transform,
            new Vector2(0.38f, 0.82f), new Vector2(0.65f, 0.86f),
            "当前等级: +0", 14, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();

        // 强化后等级
        nextLevelText = CreateText("NextLevel", panelRoot.transform,
            new Vector2(0.66f, 0.82f), new Vector2(0.98f, 0.86f),
            "强化后: +1", 14, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleLeft).GetComponent<Text>();

        // 当前攻击力
        currentAtkText = CreateText("CurrentAtk", panelRoot.transform,
            new Vector2(0.38f, 0.76f), new Vector2(0.65f, 0.80f),
            "当前攻击: 0", 14, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();

        // 强化后攻击力
        nextAtkText = CreateText("NextAtk", panelRoot.transform,
            new Vector2(0.66f, 0.76f), new Vector2(0.98f, 0.80f),
            "强化后攻击: 0", 14, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleLeft).GetComponent<Text>();

        // 当前防御力
        currentDefText = CreateText("CurrentDef", panelRoot.transform,
            new Vector2(0.38f, 0.70f), new Vector2(0.65f, 0.74f),
            "当前防御: 0", 14, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();

        // 强化后防御力
        nextDefText = CreateText("NextDef", panelRoot.transform,
            new Vector2(0.66f, 0.70f), new Vector2(0.98f, 0.74f),
            "强化后防御: 0", 14, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleLeft).GetComponent<Text>();

        // 消耗金币
        costCoinText = CreateText("CostCoin", panelRoot.transform,
            new Vector2(0.38f, 0.62f), new Vector2(0.98f, 0.66f),
            "消耗金币: 0", 14, Color.yellow, TextAnchor.MiddleLeft).GetComponent<Text>();

        // 消耗材料
        costMaterialText = CreateText("CostMaterial", panelRoot.transform,
            new Vector2(0.38f, 0.56f), new Vector2(0.98f, 0.60f),
            "消耗材料: 0", 14, Color.yellow, TextAnchor.MiddleLeft).GetComponent<Text>();

        // 成功率
        successRateText = CreateText("SuccessRate", panelRoot.transform,
            new Vector2(0.38f, 0.50f), new Vector2(0.98f, 0.54f),
            "成功率: 0%", 14, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();

        // 强化结果提示
        enhanceResultText = CreateText("EnhanceResult", panelRoot.transform,
            new Vector2(0.38f, 0.42f), new Vector2(0.98f, 0.48f),
            "", 16, Color.white, TextAnchor.MiddleCenter).GetComponent<Text>();

        // 强化按钮
        GameObject btnObj = new GameObject("EnhanceButton");
        btnObj.transform.SetParent(panelRoot.transform);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.45f, 0.28f);
        btnRect.anchorMax = new Vector2(0.7f, 0.38f);
        btnRect.sizeDelta = Vector2.zero;
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.7f, 0.3f, 0.1f, 0.9f);
        enhanceButton = btnObj.AddComponent<Button>();
        enhanceButton.targetGraphic = btnBg;
        enhanceButton.onClick.AddListener(OnEnhanceButtonClick);

        GameObject btnTextObj = new GameObject("ButtonText");
        btnTextObj.transform.SetParent(btnObj.transform);
        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.text = "强化";
        btnText.fontSize = 18;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        DebugLog("[EnhanceHUD] 强化面板UI创建完成");
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
        selectedSlotIndex = -1;
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshUI();
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnhanceOpen(object data)
    {
        OpenPanel();
        DebugLog("[EnhanceHUD] 强化面板激活");
    }

    private void OnEnhanceClose(object data)
    {
        ClosePanel();
    }

    #endregion

    #region 强化操作

    private void OnEnhanceButtonClick()
    {
        if (selectedSlotIndex < 0 || enhanceManager == null) return;

        bool executed = enhanceManager.EnhanceEquipment(selectedSlotIndex);
        if (executed)
        {
            isDirty = true;
        }
    }

    private void OnEnhanceSuccess(object data)
    {
        if (enhanceResultText != null)
            enhanceResultText.text = "强化成功！等级提升！";
        isDirty = true;
        DebugLog("[EnhanceHUD] 强化成功");
    }

    private void OnEnhanceFail(object data)
    {
        if (enhanceResultText != null)
            enhanceResultText.text = "强化失败...等级未变";
        isDirty = true;
        DebugLog("[EnhanceHUD] 强化失败");
    }

    #endregion

    #region UI刷新

    private void OnDataChange(object data)
    {
        isDirty = true;
    }

    private void RefreshUI()
    {
        if (coinText != null && coinManager != null)
        {
            coinText.text = $"金币: {coinManager.CurrentCoins}";
        }

        RefreshEquipList();
        RefreshEnhanceInfo();
    }

    private void RefreshEquipList()
    {
        foreach (var slot in equipSlots)
            if (slot != null) Destroy(slot);
        equipSlots.Clear();

        if (equipmentManager == null) return;

        for (int i = 0; i < 4; i++)
        {
            Equipment equip = equipmentManager.GetEquipmentAt(i);
            string slotName = ((EquipmentConfig.EquipSlot)i).ToString();
            string displayName = equip != null ? equip.DisplayName : $"[空] {slotName}";
            Color color = equip != null ? rarityColors[(int)equip.Rarity] : Color.gray;
            string levelInfo = equip != null ? $" +{equip.upgradeLevel}" : "";

            GameObject slotObj = CreateEquipSlot(displayName + levelInfo, color, i);

            int idx = i;
            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => SelectSlot(idx));
            }

            equipSlots.Add(slotObj);
        }
    }

    private void RefreshEnhanceInfo()
    {
        if (selectedSlotIndex < 0 || equipmentManager == null)
        {
            ClearEnhanceInfo();
            return;
        }

        Equipment equip = equipmentManager.GetEquipmentAt(selectedSlotIndex);
        if (equip == null)
        {
            ClearEnhanceInfo();
            return;
        }

        var equipConfig = equip.GetConfig();
        if (equipConfig == null) return;

        int currentLv = equip.upgradeLevel;
        int maxLv = EnhanceConfig.GetMaxLevel();

        // 当前等级信息
        if (currentLevelText != null)
            currentLevelText.text = $"当前等级: +{currentLv}";

        if (currentAtkText != null)
            currentAtkText.text = $"当前攻击: {equipConfig.attackBonus:F1}";

        if (currentDefText != null)
            currentDefText.text = $"当前防御: {equipConfig.defenseBonus:F1}";

        // 下一等级信息
        if (currentLv >= maxLv)
        {
            if (nextLevelText != null) nextLevelText.text = "已达最高";
            if (nextAtkText != null) nextAtkText.text = "---";
            if (nextDefText != null) nextDefText.text = "---";
            if (costCoinText != null) costCoinText.text = "---";
            if (costMaterialText != null) costMaterialText.text = "---";
            if (successRateText != null) successRateText.text = "---";
            if (enhanceButton != null) enhanceButton.interactable = false;
            return;
        }

        var enhanceConfig = EnhanceConfig.Get(currentLv);
        if (enhanceConfig == null) return;

        if (nextLevelText != null)
            nextLevelText.text = $"强化后: +{enhanceConfig.targetLevel}";

        float nextAtk = equipConfig.attackBonus * enhanceConfig.statMultiplier;
        if (nextAtkText != null)
            nextAtkText.text = $"强化后攻击: {nextAtk:F1}";

        float nextDef = equipConfig.defenseBonus * enhanceConfig.statMultiplier;
        if (nextDefText != null)
            nextDefText.text = $"强化后防御: {nextDef:F1}";

        if (costCoinText != null)
            costCoinText.text = $"消耗金币: {enhanceConfig.costCoins}";

        int materialHave = inventory != null ? inventory.GetItemCount(enhanceConfig.costMaterialId) : 0;
        if (costMaterialText != null)
            costMaterialText.text = $"消耗材料: {enhanceConfig.costMaterialId} x{enhanceConfig.costMaterialCount} (拥有{materialHave})";

        if (successRateText != null)
        {
            float rate = enhanceConfig.successRate * 100f;
            Color rateColor = rate >= 70f ? Color.green : rate >= 50f ? Color.yellow : Color.red;
            successRateText.text = $"成功率: {rate:F0}%";
            successRateText.color = rateColor;
        }

        // 检查是否可以强化
        bool canEnhance = coinManager != null && coinManager.HasEnoughCoins(enhanceConfig.costCoins)
            && materialHave >= enhanceConfig.costMaterialCount;
        if (enhanceButton != null)
            enhanceButton.interactable = canEnhance;
    }

    private void ClearEnhanceInfo()
    {
        if (currentLevelText != null) currentLevelText.text = "当前等级: --";
        if (nextLevelText != null) nextLevelText.text = "强化后: --";
        if (currentAtkText != null) currentAtkText.text = "当前攻击: --";
        if (nextAtkText != null) nextAtkText.text = "强化后攻击: --";
        if (currentDefText != null) currentDefText.text = "当前防御: --";
        if (nextDefText != null) nextDefText.text = "强化后防御: --";
        if (costCoinText != null) costCoinText.text = "消耗金币: --";
        if (costMaterialText != null) costMaterialText.text = "消耗材料: --";
        if (successRateText != null) successRateText.text = "成功率: --";
        if (enhanceButton != null) enhanceButton.interactable = false;
    }

    private void SelectSlot(int index)
    {
        selectedSlotIndex = index;
        if (enhanceResultText != null)
            enhanceResultText.text = "";
        isDirty = true;
    }

    private GameObject CreateEquipSlot(string name, Color color, int index)
    {
        GameObject slotObj = new GameObject("EquipSlot_" + name);
        slotObj.transform.SetParent(panelRoot.transform);
        RectTransform rect = slotObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f, 1f);
        rect.anchorMax = new Vector2(0.35f, 1f);
        rect.sizeDelta = new Vector2(0, 38f);
        rect.anchoredPosition = new Vector2(0, -(index * 42f + 5f));

        Image bg = slotObj.AddComponent<Image>();
        bg.color = new Color(0.25f, 0.2f, 0.3f, 0.9f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(slotObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0f);
        textRect.anchorMax = new Vector2(0.95f, 1f);
        textRect.sizeDelta = Vector2.zero;
        Text uiText = textObj.AddComponent<Text>();
        uiText.text = name;
        uiText.fontSize = 14;
        uiText.alignment = TextAnchor.MiddleLeft;
        uiText.color = color;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Button btn = slotObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.4f, 0.3f, 0.4f);
        colors.pressedColor = new Color(0.3f, 0.2f, 0.3f);
        btn.colors = colors;

        return slotObj;
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
