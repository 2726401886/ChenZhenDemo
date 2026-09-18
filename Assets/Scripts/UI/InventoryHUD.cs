using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包UI面板 - 显示背包物品，支持点击使用
/// 按键I打开/关闭，格子显示物品图标+堆叠数量
/// 监听ON_INVENTORY_CHANGE事件做脏标记节流刷新
/// WebGL平台兼容
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI设置")]
    [Tooltip("背包面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("格子容器")]
    [SerializeField] private Transform slotContainer;

    [Tooltip("格子预制体模板")]
    [SerializeField] private GameObject slotTemplate;

    [Tooltip("每行显示格子数")]
    [SerializeField] private int slotsPerRow = 5;

    [Header("组件引用")]
    [Tooltip("玩家Inventory组件")]
    [SerializeField] private Inventory inventory;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>UI脏标记（节流刷新）</summary>
    private bool isDirty = false;

    /// <summary>刷新间隔（秒）</summary>
    private float refreshInterval = 0.1f;

    /// <summary>刷新计时器</summary>
    private float refreshTimer = 0f;

    /// <summary>是否面板已打开</summary>
    private bool isPanelOpen = false;

    /// <summary>格子UI列表</summary>
    private System.Collections.Generic.List<GameObject> slotUIs = new System.Collections.Generic.List<GameObject>();

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeHUD();
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 按键I打开/关闭背包
        if (Input.GetKeyDown(KeyCode.I))
        {
            TogglePanel();
        }

        // 脏标记节流刷新
        if (isDirty && isPanelOpen)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshInterval)
            {
                isDirty = false;
                refreshTimer = 0f;
                RefreshSlots();
            }
        }
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe(Inventory.ON_INVENTORY_CHANGE, OnInventoryChange);
    }

    #endregion

    #region 初始化

    private void InitializeHUD()
    {
        if (isInitialized) return;

        // 自动查找玩家Inventory
        if (inventory == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                inventory = player.GetComponent<Inventory>();
        }

        // 创建格子模板（如果未指定）
        if (slotTemplate == null)
        {
            CreateSlotTemplate();
        }

        // 初始隐藏面板
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // 订阅背包变动事件
        EventBus.Subscribe(Inventory.ON_INVENTORY_CHANGE, OnInventoryChange);

        isInitialized = true;
        DebugLog("[InventoryHUD] 背包UI初始化完成");
    }

    private void CreateSlotTemplate()
    {
        // 创建格子容器
        if (slotContainer == null)
        {
            GameObject containerObj = new GameObject("SlotContainer");
            containerObj.transform.SetParent(transform);
            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.1f, 0.1f);
            containerRect.anchorMax = new Vector2(0.9f, 0.9f);
            containerRect.sizeDelta = Vector2.zero;
            slotContainer = containerObj.transform;
        }

        // 创建单个格子模板
        slotTemplate = new GameObject("SlotTemplate");
        slotTemplate.transform.SetParent(slotContainer);
        RectTransform slotRect = slotTemplate.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(80, 80);
        Image slotBg = slotTemplate.AddComponent<Image>();
        slotBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // 物品图标
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slotTemplate.transform);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = Vector2.zero;
        iconObj.AddComponent<Image>();

        // 堆叠数量文本
        GameObject countObj = new GameObject("Count");
        countObj.transform.SetParent(slotTemplate.transform);
        RectTransform countRect = countObj.AddComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0.6f, 0f);
        countRect.anchorMax = Vector2.one;
        countRect.sizeDelta = Vector2.zero;
        Text countText = countObj.AddComponent<Text>();
        countText.text = "";
        countText.fontSize = 14;
        countText.alignment = TextAnchor.LowerRight;
        countText.color = Color.white;
        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 按钮组件（用于点击使用）
        Button btn = slotTemplate.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f);
        colors.pressedColor = new Color(0.3f, 0.3f, 0.3f);
        btn.colors = colors;

        slotTemplate.SetActive(false);
    }

    #endregion

    #region 面板操作

    public void TogglePanel()
    {
        isPanelOpen = !isPanelOpen;
        if (panelRoot != null)
        {
            panelRoot.SetActive(isPanelOpen);
        }

        if (isPanelOpen)
        {
            RefreshSlots();
        }

        // 发布暂停/恢复事件
        if (isPanelOpen)
        {
            EventBus.Publish("ON_GAME_PAUSE");
        }
        else
        {
            EventBus.Publish("ON_GAME_RESUME");
        }
    }

    #endregion

    #region UI刷新

    private void OnInventoryChange(object data)
    {
        isDirty = true;
    }

    private void RefreshSlots()
    {
        if (inventory == null) return;

        // 清除旧格子UI
        foreach (var slotUI in slotUIs)
        {
            if (slotUI != null)
                Destroy(slotUI);
        }
        slotUIs.Clear();

        // 创建新格子UI
        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            GameObject slotUI = Instantiate(slotTemplate, slotContainer);
            slotUI.SetActive(true);

            Item item = inventory.GetItem(i);
            SetupSlot(slotUI, item, i);

            slotUIs.Add(slotUI);
        }
    }

    private void SetupSlot(GameObject slotUI, Item item, int index)
    {
        // 物品图标
        Transform iconTransform = slotUI.transform.Find("Icon");
        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                if (item != null && item.Icon != null)
                {
                    iconImage.sprite = item.Icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                }
            }
        }

        // 堆叠数量
        Transform countTransform = slotUI.transform.Find("Count");
        if (countTransform != null)
        {
            Text countText = countTransform.GetComponent<Text>();
            if (countText != null)
            {
                if (item != null && item.stackCount > 1)
                {
                    countText.text = item.stackCount.ToString();
                }
                else
                {
                    countText.text = "";
                }
            }
        }

        // 点击事件
        Button btn = slotUI.GetComponent<Button>();
        if (btn != null)
        {
            int slotIndex = index;
            btn.onClick.RemoveAllListeners();
            if (item != null)
            {
                btn.onClick.AddListener(() => OnSlotClick(slotIndex));
            }
        }
    }

    private void OnSlotClick(int index)
    {
        if (inventory != null)
        {
            inventory.UseItem(index);
            RefreshSlots();
        }
    }

    #endregion

    #region 调试日志

    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
