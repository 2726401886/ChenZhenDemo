using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss血条UI - 独立全屏Boss血条
/// 默认隐藏，Boss出场才显示
/// 显示Boss名称、血量百分比、阶段切换闪烁
/// 订阅ON_BOSS_PHASE_CHANGE、ON_BOSS_DEFEATED事件
/// WebGL平台兼容
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("血条Slider")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("血条填充Image")]
    [SerializeField] private Image healthFillImage;

    [Tooltip("Boss名称文字")]
    [SerializeField] private Text bossNameText;

    [Tooltip("血量百分比文字")]
    [SerializeField] private Text healthPercentText;

    [Tooltip("阶段指示文字")]
    [SerializeField] private Text phaseText;

    [Header("颜色设置")]
    [Tooltip("P1血条颜色")]
    [SerializeField] private Color p1Color = new Color(0.8f, 0.2f, 0.2f);

    [Tooltip("P2血条颜色")]
    [SerializeField] private Color p2Color = new Color(1f, 0.5f, 0.1f);

    [Tooltip("阶段切换闪烁颜色")]
    [SerializeField] private Color flashColor = Color.white;

    [Tooltip("闪烁持续时间")]
    [SerializeField] private float flashDuration = 0.3f;

    [Header("组件引用")]
    [Tooltip("BossAI组件")]
    [SerializeField] private BossAI bossAI;

    [Header("调试设置")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isInitialized = false;
    private GameObject bossRoot;
    private float flashTimer = 0f;
    private bool isFlashing = false;
    private Color originalColor;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeBossHealthBar();
    }

    private void Update()
    {
        if (!isInitialized) return;

        UpdateFlashEffect();
        UpdateHealthDisplay();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    private void InitializeBossHealthBar()
    {
        if (isInitialized) return;

        // 默认隐藏
        gameObject.SetActive(false);

        SubscribeEvents();

        isInitialized = true;
        DebugLog("[BossHealthBar] 初始化完成");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe(BossAI.ON_BOSS_PHASE_CHANGE, OnBossPhaseChange);
        EventBus.Subscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe(BossAI.ON_BOSS_PHASE_CHANGE, OnBossPhaseChange);
        EventBus.Unsubscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);
    }

    private void OnBossPhaseChange(object data)
    {
        // 触发闪烁
        isFlashing = true;
        flashTimer = flashDuration;

        // 更新阶段文字
        if (phaseText != null)
        {
            phaseText.text = "P2 狂暴";
            phaseText.color = p2Color;
        }

        DebugLog("[BossHealthBar] 阶段切换闪烁");
    }

    private void OnBossDefeated(object data)
    {
        // Boss死亡，隐藏血条
        gameObject.SetActive(false);
        bossAI = null;
        DebugLog("[BossHealthBar] Boss血条隐藏");
    }

    #endregion

    #region 显示控制

    /// <summary>
    /// 显示Boss血条
    /// </summary>
    public void ShowBossHealthBar(BossAI boss, string name)
    {
        bossAI = boss;
        bossRoot = boss.gameObject;

        if (bossNameText != null)
        {
            bossNameText.text = name;
        }

        if (phaseText != null)
        {
            phaseText.text = "P1";
            phaseText.color = p1Color;
        }

        if (healthFillImage != null)
        {
            originalColor = p1Color;
            healthFillImage.color = p1Color;
        }

        gameObject.SetActive(true);

        DebugLog("[BossHealthBar] 显示Boss血条: " + name);
    }

    /// <summary>
    /// 隐藏Boss血条
    /// </summary>
    public void HideBossHealthBar()
    {
        gameObject.SetActive(false);
        bossAI = null;
    }

    #endregion

    #region 血量更新

    private void UpdateHealthDisplay()
    {
        if (bossAI == null || bossAI.CombatSystem == null) return;

        CombatSystem combat = bossAI.CombatSystem;
        float healthPercent = (float)combat.CurrentHealth / combat.MaxHealth;

        if (healthSlider != null)
        {
            healthSlider.value = healthPercent;
        }

        if (healthPercentText != null)
        {
            healthPercentText.text = Mathf.RoundToInt(healthPercent * 100) + "%";
        }

        // P2时更新颜色
        if (bossAI.CurrentPhase == BossAI.BossPhase.P2 && !isFlashing)
        {
            if (healthFillImage != null)
            {
                healthFillImage.color = p2Color;
            }
        }
    }

    #endregion

    #region 闪烁效果

    private void UpdateFlashEffect()
    {
        if (!isFlashing) return;

        flashTimer -= Time.deltaTime;

        if (flashTimer > 0f)
        {
            // 闪烁：在原色和白色之间切换
            float t = Mathf.PingPong(Time.time * 10f, 1f);
            if (healthFillImage != null)
            {
                healthFillImage.color = Color.Lerp(originalColor, flashColor, t);
            }
        }
        else
        {
            isFlashing = false;
            if (healthFillImage != null)
            {
                healthFillImage.color = bossAI != null && bossAI.CurrentPhase == BossAI.BossPhase.P2 ? p2Color : p1Color;
            }
        }
    }

    #endregion

    #region 工具方法

    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
