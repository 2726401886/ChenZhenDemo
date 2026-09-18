using UnityEngine;

/// <summary>
/// 场景掉落物实体 - 场景中的可拾取道具
/// 碰撞检测：玩家靠近后自动拾取
/// 拾取后回收到ObjectPoolManager
/// 通过EventBus发布拾取事件
/// WebGL平台兼容
/// </summary>
public class PickupItem : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("掉落物设置")]
    [Tooltip("物品ID")]
    [SerializeField] private string itemId = "";

    [Tooltip("拾取数量")]
    [SerializeField] private int amount = 1;

    [Tooltip("拾取检测半径")]
    [SerializeField] private float pickupRadius = 1.5f;

    [Tooltip("拾取后淡出时间")]
    [SerializeField] private float fadeTime = 0.3f;

    [Header("视觉设置")]
    [Tooltip("浮动效果幅度")]
    [SerializeField] private float floatAmplitude = 0.2f;

    [Tooltip("浮动效果速度")]
    [SerializeField] private float floatSpeed = 2f;

    [Tooltip("旋转速度")]
    [SerializeField] private float rotateSpeed = 90f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>初始Y坐标</summary>
    private float startY;

    /// <summary>是否已被拾取</summary>
    private bool isPickedUp = false;

    /// <summary>淡出计时器</summary>
    private float fadeTimer = 0f;

    /// <summary>是否正在淡出</summary>
    private bool isFading = false;

    /// <summary>Renderer组件（用于淡出）</summary>
    private Renderer cachedRenderer;

    /// <summary>原始材质颜色</summary>
    private Color originalColor;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        startY = transform.position.y;
        cachedRenderer = GetComponentInChildren<Renderer>();
        if (cachedRenderer != null)
        {
            originalColor = cachedRenderer.material.color;
        }
    }

    private void Update()
    {
        if (isPickedUp) return;

        // 浮动效果
        Vector3 pos = transform.position;
        pos.y = startY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = pos;

        // 旋转效果
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

        // 检测玩家距离
        CheckPlayerDistance();

        // 淡出效果
        if (isFading)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeTimer / fadeTime);
            if (cachedRenderer != null)
            {
                Color c = originalColor;
                c.a = alpha;
                cachedRenderer.material.color = c;
            }

            if (fadeTimer >= fadeTime)
            {
                ReturnToPool();
            }
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化掉落物（从对象池取出时调用）
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="amount">数量</param>
    public void Initialize(string itemId, int amount)
    {
        this.itemId = itemId;
        this.amount = amount;
        isPickedUp = false;
        isFading = false;
        fadeTimer = 0f;
        startY = transform.position.y;

        // 恢复材质透明度
        if (cachedRenderer != null)
        {
            Color c = originalColor;
            c.a = 1f;
            cachedRenderer.material.color = c;
        }
    }

    #endregion

    #region 拾取逻辑

    private void CheckPlayerDistance()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= pickupRadius)
        {
            TryPickup(player);
        }
    }

    private void TryPickup(GameObject player)
    {
        if (isPickedUp) return;

        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogWarning("[PickupItem] 玩家没有Inventory组件");
            return;
        }

        // 尝试添加物品
        bool success = inventory.AddItem(itemId, amount);
        if (success)
        {
            isPickedUp = true;
            DebugLog($"[PickupItem] 拾取成功: {itemId} x{amount}");
            StartFade();
        }
    }

    private void StartFade()
    {
        isFading = true;
        fadeTimer = 0f;
    }

    private void ReturnToPool()
    {
        // 回收到对象池
        if (ObjectPoolManager.HasInstance)
        {
            ObjectPoolManager.Instance.ReturnToPool("PickupItem", gameObject);
        }
        else
        {
            gameObject.SetActive(false);
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
