using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 敌人世界空间血条控制器
/// 挂载在敌人身上，显示敌人血量信息
/// 跟随敌人移动，始终朝向主相机
/// 距离过远自动隐藏，优化渲染性能
/// WebGL平台兼容
/// </summary>
public class EnemyHUD : MonoBehaviour
{
    #region 事件定义

    /// <summary>
    /// 敌人HUD事件常量
    /// </summary>
    public static class EnemyHUDEvents
    {
        /// <summary>敌人血条显示</summary>
        public const string ON_HUD_SHOWN = "EnemyHUD_Shown";
        /// <summary>敌人血条隐藏</summary>
        public const string ON_HUD_HIDDEN = "EnemyHUD_Hidden";
        /// <summary>敌人血条更新</summary>
        public const string ON_HUD_UPDATED = "EnemyHUD_Updated";
    }

    #endregion

    #region UI引用设置

    [Header("UI引用")]
    [Tooltip("血条Canvas（World Space模式）")]
    [SerializeField] private Canvas worldCanvas;

    [Tooltip("血条Fill Image（使用Filled模式）")]
    [SerializeField] private Image healthBarFill;

    [Tooltip("血条背景Image（可选）")]
    [SerializeField] private Image healthBarBackground;

    [Tooltip("血量数值文本（可选）")]
    [SerializeField] private Text healthText;

    [Tooltip("敌人名称文本（可选）")]
    [SerializeField] private Text enemyNameText;

    #endregion

    #region 跟随设置

    [Header("跟随设置")]
    [Tooltip("血条在敌人头顶的偏移量")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

    [Tooltip("血条缩放大小")]
    [SerializeField] private Vector3 hudScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Tooltip("是否始终朝向相机")]
    [SerializeField] private bool faceCamera = true;

    [Tooltip("朝向平滑度（值越大越平滑）")]
    [SerializeField] private float faceCameraSmoothSpeed = 10f;

    #endregion

    #region 距离检测设置

    [Header("距离检测")]
    [Tooltip("最大显示距离")]
    [SerializeField] private float maxDisplayDistance = 15f;

    [Tooltip("最小显示距离（太近时隐藏，防止穿插）")]
    [SerializeField] private float minDisplayDistance = 0.5f;

    [Tooltip("距离检测间隔（秒，优化性能）")]
    [SerializeField] private float distanceCheckInterval = 0.2f;

    [Tooltip("淡入淡出速度")]
    [SerializeField] private float fadeSpeed = 5f;

    #endregion

    #region 颜色渐变设置

    [Header("颜色渐变")]
    [Tooltip("满血颜色")]
    [SerializeField] private Color fullHealthColor = Color.green;

    [Tooltip("中等血量颜色")]
    [SerializeField] private Color mediumHealthColor = Color.yellow;

    [Tooltip("低血量颜色")]
    [SerializeField] private Color lowHealthColor = Color.red;

    [Tooltip("低血量阈值（百分比）")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;

    [Tooltip("中等血量阈值（百分比）")]
    [SerializeField, Range(0f, 1f)] private float mediumHealthThreshold = 0.6f;

    #endregion

    #region 动画设置

    [Header("动画设置")]
    [Tooltip("血条平滑更新速度")]
    [SerializeField] private float smoothSpeed = 5f;

    [Tooltip("受伤时血条抖动强度")]
    [SerializeField] private float hitShakeIntensity = 0.05f;

    [Tooltip("受伤时血条抖动持续时间")]
    [SerializeField] private float hitShakeDuration = 0.2f;

    [Tooltip("是否启用受伤抖动效果")]
    [SerializeField] private bool enableHitShake = true;

    #endregion

    #region 显示设置

    [Header("显示设置")]
    [Tooltip("敌人名称")]
    [SerializeField] private string enemyName = "敌人";

    [Tooltip("是否显示敌人名称")]
    [SerializeField] private bool showEnemyName = true;

    [Tooltip("是否显示血量数值")]
    [SerializeField] private bool showHealthText = false;

    [Tooltip("血量数值格式")]
    [SerializeField] private string healthTextFormat = "{0}/{1}";

    [Tooltip("是否显示血条背景")]
    [SerializeField] private bool showBackground = true;

    #endregion

    #region 私有变量

    /// <summary>CombatSystem组件引用</summary>
    private CombatSystem combatSystem;

    /// <summary>目标血条Fill Amount</summary>
    private float targetFillAmount = 1f;

    /// <summary>当前血条Fill Amount</summary>
    private float currentFillAmount = 1f;

    /// <summary>是否可见</summary>
    private bool isVisible = true;

    /// <summary>是否存活</summary>
    private bool isAlive = true;

    /// <summary>距离检测计时器</summary>
    private float distanceCheckTimer = 0f;

    /// <summary>当前Canvas Group透明度</summary>
    private float currentAlpha = 1f;

    /// <summary>目标透明度</summary>
    private float targetAlpha = 1f;

    /// <summary>Canvas Group组件（用于控制透明度）</summary>
    private CanvasGroup canvasGroup;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>缓存的主相机Transform</summary>
    private Transform mainCameraTransform;

    /// <summary>是否正在抖动</summary>
    private bool isShaking = false;

    /// <summary>原始血条本地位置</summary>
    private Vector3 originalLocalPosition;

    /// <summary>Phase10: 帧节流计数器</summary>
    private int frameThrottleCounter = 0;

    #endregion

    #region 公共属性

    /// <summary>是否可见</summary>
    public bool IsVisible => isVisible;

    /// <summary>当前血条Fill Amount</summary>
    public float CurrentFillAmount => currentFillAmount;

    /// <summary>敌人名称</summary>
    public string EnemyName
    {
        get => enemyName;
        set
        {
            enemyName = value;
            UpdateEnemyNameText();
        }
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        // Phase10优化：帧节流，朝向和距离检测每2帧执行一次
        frameThrottleCounter++;
        bool shouldThrottle = (frameThrottleCounter % 2 == 0);

        if (faceCamera && shouldThrottle)
        {
            UpdateFaceCamera();
        }

        UpdatePosition();

        if (shouldThrottle)
        {
            UpdateDistanceCheck();
        }

        UpdateAlpha();
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化敌人HUD
    /// </summary>
    private void Initialize()
    {
        // 获取CombatSystem组件
        combatSystem = GetComponent<CombatSystem>();
        if (combatSystem == null)
        {
            combatSystem = GetComponentInParent<CombatSystem>();
        }

        if (combatSystem == null)
        {
            Debug.LogError("[EnemyHUD] 找不到CombatSystem组件！");
            enabled = false;
            return;
        }

        // 获取或创建Canvas
        if (worldCanvas == null)
        {
            worldCanvas = GetComponentInChildren<Canvas>();
        }

        if (worldCanvas == null)
        {
            Debug.LogError("[EnemyHUD] 找不到World Space Canvas！");
            enabled = false;
            return;
        }

        // 确保是World Space模式
        worldCanvas.renderMode = RenderMode.WorldSpace;

        // 设置Canvas大小
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 20f);
        canvasRect.localScale = hudScale;

        // 获取或添加Canvas Group
        canvasGroup = worldCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = worldCanvas.gameObject.AddComponent<CanvasGroup>();
        }

        // 设置血条Image模式
        if (healthBarFill != null)
        {
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        }

        // 初始化血量
        InitializeHealthDisplay();

        // 设置敌人名称
        UpdateEnemyNameText();

        // 缓存主相机
        mainCameraTransform = Camera.main?.transform;

        // 缓存原始位置
        originalLocalPosition = worldCanvas.transform.localPosition;

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        isAlive = true;

        Debug.Log($"[EnemyHUD] 初始化完成 - {enemyName}");
    }

    /// <summary>
    /// 初始化血量显示
    /// </summary>
    private void InitializeHealthDisplay()
    {
        if (combatSystem == null) return;

        float healthPercent = combatSystem.HealthPercent;
        targetFillAmount = healthPercent;
        currentFillAmount = healthPercent;

        // 立即更新血条
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = healthPercent;
        }

        // 更新颜色
        UpdateHealthBarColor(healthPercent);
    }

    #endregion

    #region 事件订阅

    /// <summary>
    /// 订阅事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 订阅敌人受击事件
        EventBus.Subscribe("ON_ENEMY_HIT", OnBeingHit);

        // 订阅敌人的死亡事件
        EventBus.Subscribe("ON_ENEMY_DIE", OnDeath);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_ENEMY_HIT", OnBeingHit);
        EventBus.Unsubscribe("ON_ENEMY_DIE", OnDeath);
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 被击中处理
    /// </summary>
    private void OnBeingHit()
    {
        if (!isInitialized || !isAlive) return;

        // 更新血量
        float healthPercent = combatSystem.HealthPercent;
        targetFillAmount = healthPercent;

        // 播放抖动效果
        if (enableHitShake && !isShaking)
        {
            StartCoroutine(HitShakeCoroutine());
        }

        // 更新颜色
        UpdateHealthBarColor(healthPercent);

        // 更新血量文本
        UpdateHealthText();

        // 强制显示血条（受伤时显示）
        if (!isVisible)
        {
            SetVisible(true);
        }

        Debug.Log($"[EnemyHUD] {enemyName} 受击，血量: {healthPercent * 100}%");
    }

    /// <summary>
    /// 死亡处理
    /// </summary>
    private void OnDeath()
    {
        isAlive = false;

        // 设置血条为0
        targetFillAmount = 0f;
        currentFillAmount = 0f;

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = 0f;
        }

        // 延迟隐藏血条
        StartCoroutine(HideAfterDeathCoroutine());

        Debug.Log($"[EnemyHUD] {enemyName} 死亡，血条将隐藏");
    }

    #endregion

    #region 更新逻辑

    /// <summary>
    /// 更新朝向（始终面向相机）
    /// </summary>
    private void UpdateFaceCamera()
    {
        if (mainCameraTransform == null)
        {
            mainCameraTransform = Camera.main?.transform;
            if (mainCameraTransform == null) return;
        }

        // 计算朝向相机的方向
        Vector3 directionToCamera = mainCameraTransform.position - transform.position;
        directionToCamera.y = 0; // 只在水平方向旋转

        if (directionToCamera != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera);
            worldCanvas.transform.rotation = Quaternion.Slerp(
                worldCanvas.transform.rotation,
                targetRotation,
                faceCameraSmoothSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// 更新位置（跟随敌人）
    /// </summary>
    private void UpdatePosition()
    {
        if (!isShaking) // 抖动时保持原位
        {
            worldCanvas.transform.localPosition = offset;
        }
    }

    /// <summary>
    /// 更新距离检测
    /// </summary>
    private void UpdateDistanceCheck()
    {
        distanceCheckTimer += Time.deltaTime;

        if (distanceCheckTimer >= distanceCheckInterval)
        {
            distanceCheckTimer = 0f;

            // 计算与相机的距离
            float distance = CalculateDistanceToCamera();

            // 判断是否应该显示
            bool shouldShow = distance >= minDisplayDistance && distance <= maxDisplayDistance;

            if (shouldShow != isVisible)
            {
                SetVisible(shouldShow);
            }
        }
    }

    /// <summary>
    /// 计算与相机的距离
    /// </summary>
    /// <returns>距离</returns>
    private float CalculateDistanceToCamera()
    {
        if (mainCameraTransform == null)
        {
            mainCameraTransform = Camera.main?.transform;
        }

        if (mainCameraTransform == null) return float.MaxValue;

        return Vector3.Distance(transform.position, mainCameraTransform.position);
    }

    /// <summary>
    /// 更新透明度（淡入淡出）
    /// </summary>
    private void UpdateAlpha()
    {
        if (canvasGroup == null) return;

        if (Mathf.Abs(currentAlpha - targetAlpha) > 0.01f)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            canvasGroup.alpha = currentAlpha;
        }
        else
        {
            currentAlpha = targetAlpha;
            canvasGroup.alpha = currentAlpha;
        }
    }

    #endregion

    #region 血条更新

    /// <summary>
    /// 平滑更新血条（在LateUpdate中调用）
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized || !isAlive) return;

        // 平滑过渡血条
        if (Mathf.Abs(currentFillAmount - targetFillAmount) > 0.001f)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, smoothSpeed * Time.deltaTime);

            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = currentFillAmount;
            }

            // 更新颜色
            UpdateHealthBarColor(currentFillAmount);
        }
    }

    /// <summary>
    /// 更新血条颜色
    /// </summary>
    /// <param name="healthPercentage">血量百分比</param>
    private void UpdateHealthBarColor(float healthPercentage)
    {
        if (healthBarFill == null) return;

        Color targetColor;

        if (healthPercentage <= 0f)
        {
            targetColor = lowHealthColor;
        }
        else if (healthPercentage <= lowHealthThreshold)
        {
            targetColor = lowHealthColor;
        }
        else if (healthPercentage <= mediumHealthThreshold)
        {
            float t = (healthPercentage - lowHealthThreshold) / (mediumHealthThreshold - lowHealthThreshold);
            targetColor = Color.Lerp(lowHealthColor, mediumHealthColor, t);
        }
        else
        {
            float t = (healthPercentage - mediumHealthThreshold) / (1f - mediumHealthThreshold);
            targetColor = Color.Lerp(mediumHealthColor, fullHealthColor, t);
        }

        healthBarFill.color = targetColor;

        // 更新背景颜色
        if (healthBarBackground != null)
        {
            Color bgColor = targetColor * 0.4f;
            bgColor.a = 0.6f;
            healthBarBackground.color = bgColor;
        }
    }

    /// <summary>
    /// 更新血量文本
    /// </summary>
    private void UpdateHealthText()
    {
        if (healthText == null || !showHealthText) return;

        if (combatSystem != null)
        {
            healthText.text = string.Format(healthTextFormat, combatSystem.CurrentHealth, combatSystem.MaxHealth);
        }
    }

    /// <summary>
    /// 更新敌人名称文本
    /// </summary>
    private void UpdateEnemyNameText()
    {
        if (enemyNameText != null)
        {
            enemyNameText.text = enemyName;
            enemyNameText.gameObject.SetActive(showEnemyName);
        }
    }

    #endregion

    #region 显示控制

    /// <summary>
    /// 设置血条可见性
    /// </summary>
    /// <param name="visible">是否可见</param>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        targetAlpha = visible ? 1f : 0f;

        // 直接控制交互（防止隐藏时UI仍然接收输入）
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        // 触发事件
        if (visible)
        {
            EventBus.Publish(EnemyHUDEvents.ON_HUD_SHOWN);
        }
        else
        {
            EventBus.Publish(EnemyHUDEvents.ON_HUD_HIDDEN);
        }
    }

    /// <summary>
    /// 隐藏血条（死亡后）
    /// </summary>
    /// <returns></returns>
    private IEnumerator HideAfterDeathCoroutine()
    {
        // 等待一小段时间后隐藏
        yield return new WaitForSeconds(2f);

        SetVisible(false);

        // 完全隐藏后禁用渲染
        yield return new WaitForSeconds(0.5f);

        // 禁用Canvas渲染
        if (worldCanvas != null)
        {
            worldCanvas.enabled = false;
        }
    }

    #endregion

    #region 特效协程

    /// <summary>
    /// 受伤抖动协程
    /// </summary>
    private IEnumerator HitShakeCoroutine()
    {
        isShaking = true;

        Vector3 startPosition = worldCanvas.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < hitShakeDuration)
        {
            // 计算抖动偏移
            float x = Random.Range(-hitShakeIntensity, hitShakeIntensity);
            float y = Random.Range(-hitShakeIntensity, hitShakeIntensity);

            worldCanvas.transform.localPosition = startPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 恢复原位
        worldCanvas.transform.localPosition = startPosition;
        isShaking = false;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 强制更新血条
    /// </summary>
    public void ForceUpdate()
    {
        if (combatSystem == null) return;

        float healthPercent = combatSystem.HealthPercent;
        targetFillAmount = healthPercent;
        currentFillAmount = healthPercent;

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = healthPercent;
        }

        UpdateHealthBarColor(healthPercent);
        UpdateHealthText();
    }

    /// <summary>
    /// 设置敌人名称
    /// </summary>
    /// <param name="name">新名称</param>
    public void SetEnemyName(string name)
    {
        enemyName = name;
        UpdateEnemyNameText();
    }

    /// <summary>
    /// 设置显示距离
    /// </summary>
    /// <param name="minDist">最小距离</param>
    /// <param name="maxDist">最大距离</param>
    public void SetDisplayDistance(float minDist, float maxDist)
    {
        minDisplayDistance = minDist;
        maxDisplayDistance = maxDist;
    }

    /// <summary>
    /// 重置HUD状态
    /// </summary>
    public void ResetHUD()
    {
        isAlive = true;
        isVisible = true;
        isShaking = false;

        StopAllCoroutines();

        if (worldCanvas != null)
        {
            worldCanvas.enabled = true;
        }

        ForceUpdate();
        SetVisible(true);
    }

    /// <summary>
    /// 获取与相机的距离
    /// </summary>
    /// <returns>距离</returns>
    public float GetDistanceToCamera()
    {
        return CalculateDistanceToCamera();
    }

    #endregion

    #region 编辑器辅助

    /// <summary>
    /// 在Scene视图中绘制显示范围
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 绘制最大显示距离
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDisplayDistance);

        // 绘制最小显示距离
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minDisplayDistance);

        // 绘制血条位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + offset, new Vector3(1f, 0.2f, 0.01f));
    }

    #endregion
}
