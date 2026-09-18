using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 商店UI面板 - 分为购买栏和出售栏
/// 按键T打开/关闭（与ShopNPC联动），金币实时显示
/// 订阅ON_COIN_CHANGE、ON_INVENTORY_CHANGE，脏标记节流刷新
/// WebGL平台兼容
/// </summary>
public class ShopHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI设置")]
    [Tooltip("商店面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("组件引用")]
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
    private float refreshInterval = 0.15f;
    private float refreshTimer = 0f;
    private bool isPanelOpen = false;

    private Text coinText;
    private Transform buyContainer;
    private Transform sellContainer;
    private List<GameObject> buySlots = new List<GameObject>();
    private List<GameObject> sellSlots = new List<GameObject>();

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
        EventBus.Unsubscribe(CoinManager.ON_COIN_CHANGE, OnDataChange);
        EventBus.Unsubscribe(Inventory.ON_INVENTORY_CHANGE, OnDataChange);
        EventBus.Unsubscribe(ShopNPC.ON_SHOP_OPEN, OnShopOpen);
        EventBus.Unsubscribe(ShopNPC.ON_SHOP_CLOSE, OnShopClose);
    }

    #endregion

    #region 初始化

    private void InitializeHUD()
    {
        if (isInitialized) return;

        if (coinManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                coinManager = player.GetComponent<CoinManager>();
                inventory = player.GetComponent<Inventory>();
            }
        }

        CreatePanelUI();

        EventBus.Subscribe(CoinManager.ON_COIN_CHANGE, OnDataChange);
        EventBus.Subscribe(Inventory.ON_INVENTORY_CHANGE, OnDataChange);
        EventBus.Subscribe(ShopNPC.ON_SHOP_OPEN, OnShopOpen);
        EventBus.Subscribe(ShopNPC.ON_SHOP_CLOSE, OnShopClose);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        isInitialized = true;
        DebugLog("[ShopHUD] 商店UI初始化完成");
    }

    private void CreatePanelUI()
    {
        if (panelRoot == null)
        {
            panelRoot = new GameObject("ShopPanel");
            panelRoot.transform.SetParent(transform);
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.1f);
        panelRect.anchorMax = new Vector2(0.85f, 0.9f);
        panelRect.sizeDelta = Vector2.zero;

        Image bg = panelRoot.GetComponent<Image>();
        if (bg == null) bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);

        // 标题
        CreateText("Title", panelRoot.transform, new Vector2(0f, 0.93f), Vector2.one,
            "商店", 24, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);

        // 金币显示
        GameObject coinObj = CreateText("CoinText", panelRoot.transform, new Vector2(0.7f, 0.93f), new Vector2(1f, 0.98f),
            "金币: 0", 18, Color.yellow, TextAnchor.MiddleRight);
        coinText = coinObj.GetComponent<Text>();

        // 购买栏标题
        CreateText("BuyTitle", panelRoot.transform, new Vector2(0.02f, 0.88f), new Vector2(0.48f, 0.92f),
            "【购买栏】", 16, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleLeft);

        // 购买栏容器
        GameObject buyObj = new GameObject("BuyContainer");
        buyObj.transform.SetParent(panelRoot.transform);
        RectTransform buyRect = buyObj.AddComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0.02f, 0.15f);
        buyRect.anchorMax = new Vector2(0.48f, 0.87f);
        buyRect.sizeDelta = Vector2.zero;
        buyContainer = buyObj.transform;

        // 出售栏标题
        CreateText("SellTitle", panelRoot.transform, new Vector2(0.52f, 0.88f), new Vector2(0.98f, 0.92f),
            "【出售栏】（背包物品）", 16, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleLeft);

        // 出售栏容器
        GameObject sellObj = new GameObject("SellContainer");
        sellObj.transform.SetParent(panelRoot.transform);
        RectTransform sellRect = sellObj.AddComponent<RectTransform>();
        sellRect.anchorMin = new Vector2(0.52f, 0.15f);
        sellRect.anchorMax = new Vector2(0.98f, 0.87f);
        sellRect.sizeDelta = Vector2.zero;
        sellContainer = sellObj.transform;

        DebugLog("[ShopHUD] 商店面板UI创建完成");
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

    private void OnShopOpen(object data)
    {
        OpenPanel();
        DebugLog("[ShopHUD] 商店面板激活");
    }

    private void OnShopClose(object data)
    {
        ClosePanel();
    }

    #endregion

    #region 购买/出售

    private void OnBuyItem(string shopItemId)
    {
        var shopConfig = ShopConfig.Get(shopItemId);
        if (shopConfig == null) return;

        if (coinManager == null || inventory == null) return;

        // 检查金币
        if (!coinManager.HasEnoughCoins(shopConfig.buyPrice))
        {
            Debug.LogWarning("[ShopHUD] 金币不足！");
            return;
        }

        // 检查库存
        if (shopConfig.stock == 0)
        {
            Debug.LogWarning("[ShopHUD] 商品已售罄！");
            return;
        }

        // 扣除金币
        if (!coinManager.SpendCoins(shopConfig.buyPrice)) return;

        // 添加物品到背包
        bool added = inventory.AddItem(shopConfig.itemId, 1);
        if (!added)
        {
            // 背包满了，退回金币
            coinManager.AddCoins(shopConfig.buyPrice);
            Debug.LogWarning("[ShopHUD] 背包已满，购买失败");
            return;
        }

        // 减少库存（非无限库存）
        if (shopConfig.stock > 0)
        {
            shopConfig.stock--;
        }

        EventBus.Publish("ON_SHOP_BUY");
        DebugLog($"[ShopHUD] 购买成功：{shopConfig.displayName}，花费{shopConfig.buyPrice}金币");
    }

    private void OnSellItem(int slotIndex)
    {
        if (inventory == null || coinManager == null) return;

        Item item = inventory.GetItem(slotIndex);
        if (item == null) return;

        // 获取售价（优先从ShopConfig获取，否则用ItemConfig）
        int sellPrice = 0;
        var shopConfig = ShopConfig.Get(item.itemId);
        if (shopConfig != null)
        {
            sellPrice = shopConfig.sellPrice;
        }
        else
        {
            // 默认售价为购买价的50%
            sellPrice = 10;
        }

        // 从背包移除
        if (!inventory.RemoveItem(item.itemId, 1)) return;

        // 增加金币
        coinManager.AddCoins(sellPrice);

        EventBus.Publish("ON_SHOP_SELL");
        DebugLog($"[ShopHUD] 出售成功：{item.DisplayName}，获得{sellPrice}金币");
    }

    #endregion

    #region UI刷新

    private void OnDataChange(object data)
    {
        isDirty = true;
    }

    private void RefreshUI()
    {
        // 更新金币显示
        if (coinText != null && coinManager != null)
        {
            coinText.text = $"金币: {coinManager.CurrentCoins}";
        }

        // 刷新购买栏
        RefreshBuyPanel();

        // 刷新出售栏
        RefreshSellPanel();
    }

    private void RefreshBuyPanel()
    {
        foreach (var slot in buySlots)
            if (slot != null) Destroy(slot);
        buySlots.Clear();

        var allShopItems = ShopConfig.GetAll();
        foreach (var kvp in allShopItems)
        {
            var config = kvp.Value;
            GameObject slotObj = CreateSlot(buyContainer, config.displayName, $"{config.buyPrice}G",
                config.isEquipment ? EquipmentConfig.GetRarityColor(EquipmentConfig.Rarity.Common) : Color.white,
                true);

            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                string id = config.shopItemId;
                btn.onClick.AddListener(() => OnBuyItem(id));
            }

            buySlots.Add(slotObj);
        }
    }

    private void RefreshSellPanel()
    {
        foreach (var slot in sellSlots)
            if (slot != null) Destroy(slot);
        sellSlots.Clear();

        if (inventory == null) return;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            Item item = inventory.GetItem(i);
            if (item == null) continue;

            var shopConfig = ShopConfig.Get(item.itemId);
            int price = shopConfig != null ? shopConfig.sellPrice : 10;

            GameObject slotObj = CreateSlot(sellContainer, item.DisplayName, $"售价{price}G",
                Color.white, true);

            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                int index = i;
                btn.onClick.AddListener(() => OnSellItem(index));
            }

            sellSlots.Add(slotObj);
        }
    }

    private GameObject CreateSlot(Transform parent, string name, string info, Color color, bool clickable)
    {
        GameObject slotObj = new GameObject("Slot_" + name);
        slotObj.transform.SetParent(parent);
        RectTransform rect = slotObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(0, 35f);
        rect.anchoredPosition = new Vector2(0, -(parent.childCount * 38f));

        Image bg = slotObj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.22f, 0.28f, 0.9f);

        // 物品名
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(slotObj.transform);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.05f, 0f);
        nameRect.anchorMax = new Vector2(0.6f, 1f);
        nameRect.sizeDelta = Vector2.zero;
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = name;
        nameText.fontSize = 13;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = color;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 价格信息
        GameObject infoObj = new GameObject("Info");
        infoObj.transform.SetParent(slotObj.transform);
        RectTransform infoRect = infoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.6f, 0f);
        infoRect.anchorMax = new Vector2(0.95f, 1f);
        infoRect.sizeDelta = Vector2.zero;
        Text infoText = infoObj.AddComponent<Text>();
        infoText.text = info;
        infoText.fontSize = 13;
        infoText.alignment = TextAnchor.MiddleRight;
        infoText.color = Color.yellow;
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (clickable)
        {
            Button btn = slotObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f);
            colors.pressedColor = new Color(0.25f, 0.25f, 0.3f);
            btn.colors = colors;
        }

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
