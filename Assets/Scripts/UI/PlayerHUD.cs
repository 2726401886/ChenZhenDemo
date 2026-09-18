using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家HUD控制器 - 管理玩家血条UI显示
/// 挂载在UI画布下玩家血条根物体
/// 订阅EventBus事件：受伤、死亡、重生
/// 血条平滑插值填充，死亡时弹出死亡提示UI
/// WebGL平台兼容
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("血条组件")]
    [Tooltip("血条Slider组件")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("血条填充Image（可选，用于变色）")]
    [SerializeField] private Image healthFillImage;

    [Tooltip("血量文字显示")]
    [SerializeField] private Text healthText;

    [Header("死亡提示UI")]
    [Tooltip("死亡提示面板")]
    [SerializeField] private GameObject deathPanel;

    [Tooltip("死亡提示文字")]
    [SerializeField] private Text deathText;

    [Tooltip("死亡提示显示时长（秒，0为持续显示直到重生）")]
    [SerializeField] private float deathMessageDuration = 3f;

    [Header("平滑设置")]
    [Tooltip("血条平滑过渡时间（秒）")]
    [SerializeField] private float smoothTime = 0.3f;

    [Header("血条颜色设置")]
    [Tooltip("满血颜色")]
    [SerializeField] private Color fullHealthColor = Color.green;

    [Tooltip("半血颜色")]
    [SerializeField] private Color halfHealthColor = Color.yellow;

    [Tooltip("低血颜色")]
    [SerializeField] private Color lowHealthColor = Color.red;

    [Tooltip("低血阈值")]
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Header("组件引用")]
    [Tooltip("CombatSystem组件（自动查找Player对象）")]
    [SerializeField] private CombatSystem combatSystem;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>当前血条显示值（用于平滑插值）</summary>
    private float currentSliderValue = 1f;

    /// <summary>目标血条值</summary>
    private float targetSliderValue = 1f;

    /// <summary>平滑速度</summary>
    private float smoothVelocity = 0f;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>死亡提示计时器</summary>
    private float deathTimer = 0f;

    /// <summary>是否正在显示死亡提示</summary>
    private bool isShowingDeathMessage = false;

    /// <summary>Phase10: 脏标记，血条需要刷新时置true</summary>
    private bool isHealthDirty = false;

    /// <summary>Phase10: 缓存的血量百分比（避免重复设置Image.color）</summary>
    private float cachedHealthPercent = 1f;

    #endregion

    #region 公共属性

    /// <summary>当前血条显示值</summary>
    public float CurrentSliderValue => currentSliderValue;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializePlayerHUD();
    }

    /// <summary>
    /// 每帧更新 - 血条平滑过渡、死亡提示计时
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        // Phase10优化：仅脏标记时更新血条
        if (isHealthDirty)
        {
            UpdateHealthBarSmooth();
            isHealthDirty = false;
        }

        UpdateDeathMessage();
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
    /// 初始化玩家HUD
    /// </summary>
    private void InitializePlayerHUD()
    {
        if (isInitialized) return;

        // 自动查找CombatSystem
        if (combatSystem == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                combatSystem = player.GetComponent<CombatSystem>();
            }
        }

        // 验证组件
        if (healthSlider == null)
        {
            Debug.LogError("[PlayerHUD] 未找到血条Slider组件！");
            enabled = false;
            return;
        }

        // 初始化血条
        if (combatSystem != null)
        {
            UpdateHealthBarImmediate(combatSystem.HealthPercent);
        }
        else
        {
            UpdateHealthBarImmediate(1f);
        }

        // 隐藏死亡提示面板
        HideDeathPanel();

        // 订阅EventBus事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[PlayerHUD] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    /// <summary>
    /// 取消所有EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家受伤事件处理
    /// </summary>
    private void OnPlayerHit()
    {
        if (combatSystem != null)
        {
            targetSliderValue = combatSystem.HealthPercent;
        }
        isHealthDirty = true; // Phase10: 标记脏
        DebugLog("[PlayerHUD] 玩家受伤，血条更新");
    }

    /// <summary>
    /// 玩家死亡事件处理
    /// </summary>
    private void OnPlayerDie()
    {
        // 立即更新血条到0
        targetSliderValue = 0f;
        currentSliderValue = 0f;
        UpdateHealthBarImmediate(0f);

        // 显示死亡提示
        ShowDeathPanel();

        DebugLog("[PlayerHUD] 玩家死亡");
    }

    /// <summary>
    /// 玩家重生事件处理
    /// </summary>
    private void OnPlayerRespawn()
    {
        // 重置血条
        if (combatSystem != null)
        {
            targetSliderValue = combatSystem.HealthPercent;
        }
        else
        {
            targetSliderValue = 1f;
        }

        // 立即更新显示
        UpdateHealthBarImmediate(targetSliderValue);

        // 隐藏死亡提示
        HideDeathPanel();

        DebugLog("[PlayerHUD] 玩家重生，血条重置");
    }

    #endregion

    #region 血条更新

    /// <summary>
    /// 更新血条（平滑过渡）
    /// </summary>
    private void UpdateHealthBarSmooth()
    {
        // 平滑插值
        currentSliderValue = Mathf.SmoothDamp(currentSliderValue, targetSliderValue, ref smoothVelocity, smoothTime);

        // 更新Slider
        if (healthSlider != null)
        {
            healthSlider.value = currentSliderValue;
        }

        // 更新血量文字
        UpdateHealthText(currentSliderValue);

        // 更新血条颜色
        UpdateHealthBarColor(currentSliderValue);
    }

    /// <summary>
    /// 立即更新血条（无平滑）
    /// </summary>
    /// <param name="percent">血量百分比（0-1）</param>
    private void UpdateHealthBarImmediate(float percent)
    {
        percent = Mathf.Clamp01(percent);
        currentSliderValue = percent;
        targetSliderValue = percent;

        // 更新Slider
        if (healthSlider != null)
        {
            healthSlider.value = percent;
        }

        // 更新血量文字
        UpdateHealthText(percent);

        // 更新血条颜色
        UpdateHealthBarColor(percent);
    }

    /// <summary>
    /// 更新血量文字
    /// </summary>
    /// <param name="percent">血量百分比</param>
    private void UpdateHealthText(float percent)
    {
        if (healthText == null || combatSystem == null) return;

        int currentHP = Mathf.RoundToInt(percent * combatSystem.MaxHealth);
        healthText.text = currentHP + " / " + combatSystem.MaxHealth;
    }

    /// <summary>
    /// 更新血条颜色
    /// </summary>
    /// <param name="percent">血量百分比</param>
    private void UpdateHealthBarColor(float percent)
    {
        if (healthFillImage == null) return;

        Color targetColor;
        if (percent <= lowHealthThreshold)
        {
            targetColor = lowHealthColor;
        }
        else if (percent <= 0.6f)
        {
            targetColor = halfHealthColor;
        }
        else
        {
            targetColor = fullHealthColor;
        }

        healthFillImage.color = targetColor;
    }

    #endregion

    #region 死亡提示控制

    /// <summary>
    /// 显示死亡提示面板
    /// </summary>
    private void ShowDeathPanel()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
        }

        if (deathText != null)
        {
            deathText.text = "你已死亡";
        }

        isShowingDeathMessage = true;
        deathTimer = 0f;
    }

    /// <summary>
    /// 隐藏死亡提示面板
    /// </summary>
    private void HideDeathPanel()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }

        isShowingDeathMessage = false;
        deathTimer = 0f;
    }

    /// <summary>
    /// 更新死亡提示计时
    /// </summary>
    private void UpdateDeathMessage()
    {
        if (!isShowingDeathMessage) return;

        // 如果设置了自动隐藏时间
        if (deathMessageDuration > 0)
        {
            deathTimer += Time.deltaTime;
            if (deathTimer >= deathMessageDuration)
            {
                HideDeathPanel();
            }
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 手动更新血条
    /// </summary>
    /// <param name="percent">血量百分比（0-1）</param>
    public void UpdateHealthBar(float percent)
    {
        targetSliderValue = Mathf.Clamp01(percent);
    }

    /// <summary>
    /// 立即设置血条（无平滑）
    /// </summary>
    /// <param name="percent">血量百分比（0-1）</param>
    public void SetHealthBarImmediate(float percent)
    {
        UpdateHealthBarImmediate(percent);
    }

    /// <summary>
    /// 设置CombatSystem引用
    /// </summary>
    /// <param name="combat">CombatSystem组件</param>
    public void SetCombatSystem(CombatSystem combat)
    {
        combatSystem = combat;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 输出调试日志
    /// </summary>
    /// <param name="message">日志内容</param>
    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
