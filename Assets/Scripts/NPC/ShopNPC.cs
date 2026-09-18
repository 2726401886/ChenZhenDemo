using UnityEngine;

/// <summary>
/// 商店NPC脚本 - 玩家靠近后按键T打开商店面板
/// 复用对象池，交互触发ON_SHOP_OPEN/ON_SHOP_CLOSE事件
/// WebGL平台兼容
/// </summary>
public class ShopNPC : MonoBehaviour
{
    #region 事件常量

    /// <summary>商店打开事件</summary>
    public const string ON_SHOP_OPEN = "ON_SHOP_OPEN";

    /// <summary>商店关闭事件</summary>
    public const string ON_SHOP_CLOSE = "ON_SHOP_CLOSE";

    #endregion

    #region Inspector可配置参数

    [Header("NPC设置")]
    [Tooltip("NPC显示名称")]
    [SerializeField] private string npcName = "商店老板";

    [Tooltip("交互触发距离")]
    [SerializeField] private float interactDistance = 3f;

    [Tooltip("提示文字偏移")]
    [SerializeField] private Vector3 tipOffset = new Vector3(0f, 2.5f, 0f);

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isPlayerNearby = false;
    private bool isShopOpen = false;
    private GameObject tipObject;
    private static Transform cachedPlayerTransform;

    #endregion

    #region Unity生命周期

    private void Update()
    {
        CheckPlayerDistance();

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.T))
        {
            ToggleShop();
        }
    }

    private void OnDestroy()
    {
        if (isShopOpen)
        {
            EventBus.Publish(ON_SHOP_CLOSE);
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

        // 显示/隐藏提示文字
        if (isPlayerNearby && !wasNearby)
        {
            ShowTip("[T] 打开商店");
        }
        else if (!isPlayerNearby && wasNearby)
        {
            HideTip();
            if (isShopOpen)
            {
                ToggleShop();
            }
        }
    }

    private void ToggleShop()
    {
        isShopOpen = !isShopOpen;

        if (isShopOpen)
        {
            EventBus.Publish(ON_SHOP_OPEN);
            DebugLog($"[ShopNPC] 打开商店：{npcName}");
        }
        else
        {
            EventBus.Publish(ON_SHOP_CLOSE);
            DebugLog($"[ShopNPC] 关闭商店：{npcName}");
        }
    }

    #endregion

    #region 提示文字

    private void ShowTip(string text)
    {
        if (tipObject == null)
        {
            tipObject = new GameObject("ShopTip");
            tipObject.transform.SetParent(transform);
            tipObject.transform.localPosition = tipOffset;

            Canvas canvas = tipObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 40f);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            tipObject.AddComponent<UnityEngine.UI.CanvasScaler>();

            GameObject textObj = new GameObject("TipText");
            textObj.transform.SetParent(tipObject.transform);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Text uiText = textObj.AddComponent<UnityEngine.UI.Text>();
            uiText.text = text;
            uiText.fontSize = 18;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = Color.yellow;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        else
        {
            tipObject.SetActive(true);
            var uiText = tipObject.GetComponentInChildren<UnityEngine.UI.Text>();
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
