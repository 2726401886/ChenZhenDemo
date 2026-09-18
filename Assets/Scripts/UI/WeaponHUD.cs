using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器HUD控制器 - 显示当前武器信息
/// 订阅ON_WEAPON_CHANGE事件，在界面显示当前武器名称
/// 支持武器切换时的UI更新
/// WebGL平台兼容
/// </summary>
public class WeaponHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("武器名称文本")]
    [SerializeField] private Text weaponNameText;

    [Tooltip("武器伤害文本")]
    [SerializeField] private Text damageText;

    [Tooltip("武器冷却文本")]
    [SerializeField] private Text cooldownText;

    [Tooltip("武器范围文本")]
    [SerializeField] private Text rangeText;

    [Tooltip("武器图标（可选）")]
    [SerializeField] private Image weaponIcon;

    [Header("UI设置")]
    [Tooltip("武器名称字体大小")]
    [SerializeField] private int weaponNameFontSize = 20;

    [Tooltip("武器信息字体大小")]
    [SerializeField] private int weaponInfoFontSize = 14;

    [Tooltip("显示持续时间（秒，0为持续显示）")]
    [SerializeField] private float displayDuration = 3f;

    [Header("颜色设置")]
    [Tooltip("武器名称颜色")]
    [SerializeField] private Color weaponNameColor = new Color(1f, 0.9f, 0.6f);

    [Tooltip("伤害文本颜色")]
    [SerializeField] private Color damageColor = new Color(1f, 0.4f, 0.4f);

    [Tooltip("冷却文本颜色")]
    [SerializeField] private Color cooldownColor = new Color(0.5f, 0.8f, 1f);

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>显示计时器</summary>
    private float displayTimer = 0f;

    /// <summary>是否正在显示</summary>
    private bool isShowing = false;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>默认武器名称（未装备武器时显示）</summary>
    private string defaultWeaponName = "空手";

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeWeaponHUD();
    }

    /// <summary>
    /// 每帧更新 - 处理显示计时器
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        // 处理显示计时器
        if (isShowing && displayDuration > 0)
        {
            displayTimer -= Time.deltaTime;
            if (displayTimer <= 0)
            {
                HideWeaponInfo();
            }
        }
    }

    /// <summary>
    /// 销毁时取消EventBus订阅
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化武器HUD
    /// </summary>
    private void InitializeWeaponHUD()
    {
        if (isInitialized) return;

        // 如果没有设置文本组件，尝试自动查找
        if (weaponNameText == null)
        {
            weaponNameText = GetComponentInChildren<Text>();
        }

        // 设置默认显示
        if (weaponNameText != null)
        {
            weaponNameText.fontSize = weaponNameFontSize;
            weaponNameText.color = weaponNameColor;
            weaponNameText.text = defaultWeaponName;
        }

        if (damageText != null)
        {
            damageText.fontSize = weaponInfoFontSize;
            damageText.color = damageColor;
        }

        if (cooldownText != null)
        {
            cooldownText.fontSize = weaponInfoFontSize;
            cooldownText.color = cooldownColor;
        }

        if (rangeText != null)
        {
            rangeText.fontSize = weaponInfoFontSize;
        }

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[WeaponHUD] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 武器切换事件
        EventBus.Subscribe<WeaponChangeEventData>("ON_WEAPON_CHANGE", OnWeaponChange);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe<WeaponChangeEventData>("ON_WEAPON_CHANGE", OnWeaponChange);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 武器切换事件处理
    /// </summary>
    /// <param name="data">武器切换数据</param>
    private void OnWeaponChange(WeaponChangeEventData data)
    {
        ShowWeaponInfo(data);
    }

    #endregion

    #region UI显示

    /// <summary>
    /// 显示武器信息
    /// </summary>
    /// <param name="data">武器数据</param>
    public void ShowWeaponInfo(WeaponChangeEventData data)
    {
        // 更新武器名称
        if (weaponNameText != null)
        {
            weaponNameText.text = data.weaponName;
        }

        // 更新伤害信息
        if (damageText != null)
        {
            damageText.text = $"伤害: {data.weaponDamage:F0}";
        }

        // 更新冷却信息
        if (cooldownText != null)
        {
            cooldownText.text = $"冷却: {data.attackCooldown:F1}s";
        }

        // 更新范围信息
        if (rangeText != null)
        {
            rangeText.text = $"范围: {data.attackRange:F1}";
        }

        // 开始显示计时器
        isShowing = true;
        displayTimer = displayDuration;

        DebugLog($"[WeaponHUD] 显示武器信息: {data.weaponName}");
    }

    /// <summary>
    /// 隐藏武器信息
    /// </summary>
    public void HideWeaponInfo()
    {
        isShowing = false;

        // 如果设置了隐藏，可以淡出或隐藏整个HUD
        // 这里选择保持显示，只停止计时器
        DebugLog("[WeaponHUD] 武器信息显示结束");
    }

    /// <summary>
    /// 设置默认武器名称（无武器时显示）
    /// </summary>
    /// <param name="name">默认名称</param>
    public void SetDefaultWeaponName(string name)
    {
        defaultWeaponName = name;

        // 如果当前没有武器显示，更新显示
        if (!isShowing && weaponNameText != null)
        {
            weaponNameText.text = defaultWeaponName;
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 调试日志
    /// </summary>
    /// <param name="message">日志消息</param>
    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message);
        }
    }

    #endregion
}
