using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备UI面板 - 显示4个装备槽位，支持穿戴/卸下
/// 按键O打开/关闭，脏标记节流刷新
/// WebGL平台兼容
/// </summary>
public class EquipmentHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI设置")]
    [Tooltip("装备面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("组件引用")]
    [Tooltip("玩家EquipmentManager组件")]
    [SerializeField] private EquipmentManager equipmentManager;

    [Tooltip("玩家Inventory组件")]
    [SerializeField] private Inventory inventory;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isInitialized = false;
    private bool isDirty = false;
    private float refreshInterval = 0.1f;
    private float refreshTimer = 0f;
    private bool isPanelOpen = false;

    /// <summary>槽位UI引用</summary>
    private GameObject[] slotUIs;

    /// <summary>稀有度颜色</summary>
    private static readonly Color[] rarityColors = {
        Color.white,                           // Common
        Color.green,                           // Uncommon
        Color.cyan,                            // Rare
        new Color(0.7f, 0.3f, 1f)             // Epic
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

        if (Input.GetKeyDown(KeyCode.O))
        {
            TogglePanel();
        }

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
        EventBus.Unsubscribe(EquipmentManager.ON_EQUIP, OnEquipChange);
        EventBus.Unsubscribe(EquipmentManager.ON_UNEQUIP, OnEquipChange);
    }

    #endregion

    #region 初始化

    private void InitializeHUD()
    {
        if (isInitialized) return;

        if (equipmentManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                equipmentManager = player.GetComponent<EquipmentManager>();
                inventory = player.GetComponent<Inventory>();
            }
        }

        // 创建装备面板UI
        CreatePanelUI();

        // 订阅装备事件
        EventBus.Subscribe(EquipmentManager.ON_EQUIP, OnEquipChange);
        EventBus.Subscribe(EquipmentManager.ON_UNEQUIP, OnEquipChange);

        // 初始隐藏
        if (panelRoot != null)
            panelRoot.SetActive(false);

        isInitialized = true;
        DebugLog("[EquipmentHUD] 装备UI初始化完成");
    }

    private void CreatePanelUI()
    {
        // 面板根对象
        if (panelRoot == null)
        {
            panelRoot = new GameObject("EquipmentPanel");
            panelRoot.transform.SetParent(transform);
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.15f);
        panelRect.anchorMax = new Vector2(0.75f, 0.85f);
        panelRect.sizeDelta = Vector2.zero;

        Image bg = panelRoot.GetComponent<Image>();
        if (bg == null) bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

        // 标题
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelRoot.transform);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.9f);
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "装备栏 (O键关闭)";
        titleText.fontSize = 22;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.9f, 0.6f);
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 4个装备槽位
        string[] slotNames = { "武器", "头盔", "胸甲", "鞋子" };
        EquipmentConfig.EquipSlot[] slotTypes = {
            EquipmentConfig.EquipSlot.Weapon,
            EquipmentConfig.EquipSlot.Helmet,
            EquipmentConfig.EquipSlot.Chest,
            EquipmentConfig.EquipSlot.Boots
        };

        slotUIs = new GameObject[4];

        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = new GameObject("Slot_" + slotNames[i]);
            slotObj.transform.SetParent(panelRoot.transform);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();

            // 2x2布局
            float x = (i % 2 == 0) ? 0.1f : 0.55f;
            float y = (i < 2) ? 0.55f : 0.1f;
            slotRect.anchorMin = new Vector2(x, y);
            slotRect.anchorMax = new Vector2(x + 0.35f, y + 0.3f);
            slotRect.sizeDelta = Vector2.zero;

            Image slotBg = slotObj.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            // 槽位名称标签
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(slotObj.transform);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.7f);
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = slotNames[i];
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.7f, 0.7f, 0.7f);
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 装备图标
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.05f);
            iconRect.anchorMax = new Vector2(0.9f, 0.65f);
            iconRect.sizeDelta = Vector2.zero;
            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);

            // 装备名称文本
            GameObject nameObj = new GameObject("EquipName");
            nameObj.transform.SetParent(slotObj.transform);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0.08f);
            nameRect.sizeDelta = Vector2.zero;
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = "空";
            nameText.fontSize = 12;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.gray;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 点击卸下按钮
            Button btn = slotObj.AddComponent<Button>();
            int slotIndex = i;
            btn.onClick.AddListener(() => OnSlotClick(slotIndex));

            slotUIs[i] = slotObj;
        }

        // 属性汇总区域
        GameObject statsObj = new GameObject("StatsSummary");
        statsObj.transform.SetParent(panelRoot.transform);
        RectTransform statsRect = statsObj.AddComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.05f, 0f);
        statsRect.anchorMax = new Vector2(0.95f, 0.08f);
        statsRect.sizeDelta = Vector2.zero;
        Text statsText = statsObj.AddComponent<Text>();
        statsText.name = "StatsText";
        statsText.text = "属性加成: 攻击+0 防御+0";
        statsText.fontSize = 14;
        statsText.alignment = TextAnchor.MiddleCenter;
        statsText.color = new Color(0.8f, 0.8f, 0.6f);
        statsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    #endregion

    #region 面板操作

    public void TogglePanel()
    {
        isPanelOpen = !isPanelOpen;
        if (panelRoot != null)
            panelRoot.SetActive(isPanelOpen);

        if (isPanelOpen)
        {
            RefreshUI();
            EventBus.Publish("ON_GAME_PAUSE");
        }
        else
        {
            EventBus.Publish("ON_GAME_RESUME");
        }
    }

    #endregion

    #region UI刷新

    private void OnEquipChange(object data)
    {
        isDirty = true;
    }

    private void RefreshUI()
    {
        if (equipmentManager == null || slotUIs == null) return;

        var slots = equipmentManager.GetAllSlots;

        for (int i = 0; i < slotUIs.Length && i < slots.Length; i++)
        {
            var equip = slots[i];
            var slotUI = slotUIs[i];
            if (slotUI == null) continue;

            // 图标
            Transform iconTransform = slotUI.transform.Find("Icon");
            if (iconTransform != null)
            {
                Image iconImage = iconTransform.GetComponent<Image>();
                if (iconImage != null)
                {
                    if (equip != null && !string.IsNullOrEmpty(equip.equipId) && equip.Icon != null)
                    {
                        iconImage.sprite = equip.Icon;
                        iconImage.color = Color.white;
                    }
                    else
                    {
                        iconImage.sprite = null;
                        iconImage.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                    }
                }
            }

            // 装备名称
            Transform nameTransform = slotUI.transform.Find("EquipName");
            if (nameTransform != null)
            {
                Text nameText = nameTransform.GetComponent<Text>();
                if (nameText != null)
                {
                    if (equip != null && !string.IsNullOrEmpty(equip.equipId))
                    {
                        nameText.text = equip.DisplayName;
                        nameText.color = EquipmentConfig.GetRarityColor(equip.Rarity);
                    }
                    else
                    {
                        nameText.text = "空";
                        nameText.color = Color.gray;
                    }
                }
            }
        }

        // 更新属性汇总
        Transform statsTransform = panelRoot.transform.Find("StatsSummary");
        if (statsTransform != null)
        {
            Text statsText = statsTransform.GetComponent<Text>();
            if (statsText != null)
            {
                statsText.text = $"属性加成: 攻击+{equipmentManager.TotalAttackBonus:F0}  防御+{equipmentManager.TotalDefenseBonus:F0}  耐力+{equipmentManager.TotalStaminaBonus:F0}  速度+{equipmentManager.TotalSpeedBonus:F1}";
            }
        }
    }

    private void OnSlotClick(int slotIndex)
    {
        if (equipmentManager == null) return;

        EquipmentConfig.EquipSlot slot = (EquipmentConfig.EquipSlot)slotIndex;
        if (equipmentManager.IsSlotOccupied(slot))
        {
            equipmentManager.Unequip(slot);
            RefreshUI();
        }
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
