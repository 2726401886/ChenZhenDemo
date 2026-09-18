using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 技能HUD控制器 - 显示5个技能图标、冷却遮罩、耐力条、Buff显示区
/// 挂载在Canvas下SkillHUD根物体
/// 订阅ON_SKILL_CAST/ON_SKILL_END/ON_STAMINA_CHANGE/ON_BUFF_ADD/ON_BUFF_REMOVE事件
/// 每帧刷新冷却剩余时间文字与遮罩填充
/// WebGL平台兼容
/// </summary>
public class SkillHUD : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("技能图标槽位")]
    [Tooltip("技能图标数组（按Q/E/R/F/T顺序）")]
    [SerializeField] private Image[] skillIcons = new Image[5];

    [Tooltip("技能冷却遮罩数组")]
    [SerializeField] private Image[] skillCooldownMasks = new Image[5];

    [Tooltip("技能冷却倒计时文字数组")]
    [SerializeField] private Text[] skillCooldownTexts = new Text[5];

    [Tooltip("技能名称文字数组")]
    [SerializeField] private Text[] skillNameTexts = new Text[5];

    [Header("耐力条")]
    [Tooltip("耐力条Slider")]
    [SerializeField] private Slider staminaSlider;

    [Tooltip("耐力条填充Image")]
    [SerializeField] private Image staminaFillImage;

    [Tooltip("耐力文字")]
    [SerializeField] private Text staminaText;

    [Header("Buff显示区")]
    [Tooltip("Buff图标容器")]
    [SerializeField] private Transform buffContainer;

    [Tooltip("Buff图标预制体（运行时实例化）")]
    [SerializeField] private GameObject buffIconPrefab;

    [Header("颜色设置")]
    [Tooltip("技能可用颜色")]
    [SerializeField] private Color readyColor = Color.white;

    [Tooltip("技能冷却中颜色")]
    [SerializeField] private Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

    [Tooltip("耐力条满颜色")]
    [SerializeField] private Color staminaFullColor = new Color(0.9f, 0.8f, 0.2f);

    [Tooltip("耐力条低颜色")]
    [SerializeField] private Color staminaLowColor = new Color(0.9f, 0.3f, 0.2f);

    [Tooltip("低耐力阈值")]
    [Range(0f, 1f)]
    [SerializeField] private float lowStaminaThreshold = 0.3f;

    [Header("组件引用")]
    [Tooltip("SkillManager组件（自动查找Player）")]
    [SerializeField] private SkillManager skillManager;

    [Tooltip("BuffManager组件（自动查找Player）")]
    [SerializeField] private BuffManager buffManager;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private bool isInitialized = false;
    private Skill[] skills = new Skill[5];
    private Dictionary<string, GameObject> buffIconInstances = new Dictionary<string, GameObject>();

    /// <summary>Phase10: 是否有任何技能在冷却中</summary>
    private bool anySkillOnCooldown = false;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeSkillHUD();
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Phase10优化：仅在有技能冷却时才刷新
        anySkillOnCooldown = false;
        for (int i = 0; i < 5; i++)
        {
            if (skills[i] != null && skills[i].IsOnCooldown)
            {
                anySkillOnCooldown = true;
                break;
            }
        }

        if (anySkillOnCooldown)
        {
            UpdateCooldownDisplay();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    private void InitializeSkillHUD()
    {
        if (isInitialized) return;

        // 自动查找组件
        if (skillManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                skillManager = player.GetComponent<SkillManager>();
                buffManager = player.GetComponent<BuffManager>();
            }
        }

        if (skillManager == null)
        {
            Debug.LogError("[SkillHUD] 未找到SkillManager组件！");
            enabled = false;
            return;
        }

        // 获取所有技能引用
        for (int i = 0; i < 5; i++)
        {
            skills[i] = skillManager.GetSkill(i);
        }

        // 初始化耐力条
        UpdateStaminaBarImmediate(skillManager.StaminaPercent);

        // 初始化冷却遮罩
        for (int i = 0; i < 5; i++)
        {
            if (skillCooldownMasks[i] != null)
                skillCooldownMasks[i].fillAmount = 0f;
        }

        SubscribeEvents();

        isInitialized = true;
        DebugLog("[SkillHUD] 初始化完成（5技能 + Buff区）");
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_SKILL_CAST", OnSkillCast);
        EventBus.Subscribe("ON_SKILL_END", OnSkillEnd);
        EventBus.Subscribe("ON_STAMINA_CHANGE", OnStaminaChange);
        EventBus.Subscribe("ON_BUFF_ADD", OnBuffChange);
        EventBus.Subscribe("ON_BUFF_REMOVE", OnBuffChange);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_SKILL_CAST", OnSkillCast);
        EventBus.Unsubscribe("ON_SKILL_END", OnSkillEnd);
        EventBus.Unsubscribe("ON_STAMINA_CHANGE", OnStaminaChange);
        EventBus.Unsubscribe("ON_BUFF_ADD", OnBuffChange);
        EventBus.Unsubscribe("ON_BUFF_REMOVE", OnBuffChange);
    }

    private void OnSkillCast(object data)
    {
        DebugLog("[SkillHUD] 技能释放");
    }

    private void OnSkillEnd(object data)
    {
        DebugLog("[SkillHUD] 技能结束");
    }

    private void OnStaminaChange()
    {
        if (skillManager != null)
        {
            UpdateStaminaBarImmediate(skillManager.StaminaPercent);
        }
    }

    private void OnBuffChange()
    {
        UpdateBuffDisplay();
    }

    #endregion

    #region 冷却显示更新

    private void UpdateCooldownDisplay()
    {
        for (int i = 0; i < 5; i++)
        {
            UpdateSkillCooldown(skills[i],
                i < skillCooldownMasks.Length ? skillCooldownMasks[i] : null,
                i < skillCooldownTexts.Length ? skillCooldownTexts[i] : null,
                i < skillIcons.Length ? skillIcons[i] : null);
        }
    }

    private void UpdateSkillCooldown(Skill skill, Image mask, Text cooldownText, Image icon)
    {
        if (skill == null) return;

        if (skill.IsOnCooldown)
        {
            float percent = skill.GetCooldownPercent();
            if (mask != null)
                mask.fillAmount = percent;

            if (cooldownText != null)
            {
                cooldownText.text = Mathf.CeilToInt(skill.currentCooldown).ToString();
                cooldownText.gameObject.SetActive(true);
            }

            if (icon != null)
                icon.color = cooldownColor;
        }
        else
        {
            if (mask != null)
                mask.fillAmount = 0f;

            if (cooldownText != null)
                cooldownText.gameObject.SetActive(false);

            if (icon != null)
                icon.color = readyColor;
        }
    }

    #endregion

    #region 耐力条更新

    private void UpdateStaminaBarImmediate(float percent)
    {
        percent = Mathf.Clamp01(percent);

        if (staminaSlider != null)
            staminaSlider.value = percent;

        if (staminaText != null && skillManager != null)
        {
            float current = percent * skillManager.MaxStamina;
            staminaText.text = Mathf.RoundToInt(current) + " / " + skillManager.MaxStamina;
        }

        if (staminaFillImage != null)
            staminaFillImage.color = percent <= lowStaminaThreshold ? staminaLowColor : staminaFullColor;
    }

    #endregion

    #region Buff显示更新

    private void UpdateBuffDisplay()
    {
        if (buffManager == null || buffContainer == null) return;

        // 清除旧的Buff图标
        foreach (var kvp in buffIconInstances)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        buffIconInstances.Clear();

        // 获取当前Buff列表
        List<Buff> activeBuffs = buffManager.GetActiveBuffs();

        // 创建新的Buff图标
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            Buff buff = activeBuffs[i];
            GameObject buffIcon = CreateBuffIcon(buff, i);
            buffIconInstances[buff.buffName] = buffIcon;
        }
    }

    private GameObject CreateBuffIcon(Buff buff, int index)
    {
        GameObject iconObj = new GameObject("Buff_" + buff.buffName);
        iconObj.transform.SetParent(buffContainer, false);

        RectTransform rect = iconObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(index * 35f, 0f);
        rect.sizeDelta = new Vector2(30f, 30f);

        // 背景
        Image bg = iconObj.AddComponent<Image>();
        bg.color = buff.isPositive ? new Color(0.2f, 0.6f, 0.2f, 0.8f) : new Color(0.6f, 0.2f, 0.2f, 0.8f);

        // Buff名称文字
        GameObject textObj = new GameObject("BuffName");
        textObj.transform.SetParent(iconObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textObj.AddComponent<Text>();
        text.text = buff.displayName.Length > 2 ? buff.displayName.Substring(0, 2) : buff.displayName;
        text.fontSize = 10;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 持续时间进度条
        GameObject durationBar = new GameObject("DurationBar");
        durationBar.transform.SetParent(iconObj.transform, false);
        RectTransform durationRect = durationBar.AddComponent<RectTransform>();
        durationRect.anchorMin = Vector2.zero;
        durationRect.anchorMax = new Vector2(1f, 0.15f);
        durationRect.sizeDelta = Vector2.zero;
        Image durationImg = durationBar.AddComponent<Image>();
        durationImg.color = new Color(1f, 1f, 1f, 0.6f);

        // 设置持续时间进度
        if (!buff.IsPermanent)
        {
            float pct = buff.GetDurationPercent();
            durationRect.anchorMax = new Vector2(pct, 0.15f);
        }

        return iconObj;
    }

    #endregion

    #region 公共API

    public void SetSkillManager(SkillManager manager)
    {
        skillManager = manager;
        isInitialized = false;
        InitializeSkillHUD();
    }

    public void SetBuffManager(BuffManager manager)
    {
        buffManager = manager;
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
