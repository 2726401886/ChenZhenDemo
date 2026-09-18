using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板控制器 - 游戏设置界面
/// 包含：音量滑块、鼠标灵敏度调节
/// 使用PlayerPrefs保存设置
/// WebGL平台兼容
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("UI组件")]
    [Tooltip("设置面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("主音量滑块")]
    [SerializeField] private Slider masterVolumeSlider;

    [Tooltip("主音量数值文本")]
    [SerializeField] private Text masterVolumeText;

    [Tooltip("音乐音量滑块")]
    [SerializeField] private Slider musicVolumeSlider;

    [Tooltip("音乐音量数值文本")]
    [SerializeField] private Text musicVolumeText;

    [Tooltip("音效音量滑块")]
    [SerializeField] private Slider sfxVolumeSlider;

    [Tooltip("音效音量数值文本")]
    [SerializeField] private Text sfxVolumeText;

    [Tooltip("鼠标灵敏度滑块")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Tooltip("鼠标灵敏度数值文本")]
    [SerializeField] private Text mouseSensitivityText;

    [Header("返回按钮")]
    [Tooltip("返回按钮")]
    [SerializeField] private Button backButton;

    [Header("默认值")]
    [Tooltip("默认主音量")]
    [SerializeField] private float defaultMasterVolume = 1f;

    [Tooltip("默认音乐音量")]
    [SerializeField] private float defaultMusicVolume = 0.8f;

    [Tooltip("默认音效音量")]
    [SerializeField] private float defaultSFXVolume = 1f;

    [Tooltip("默认鼠标灵敏度")]
    [SerializeField] private float defaultMouseSensitivity = 10f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region PlayerPrefs键名

    /// <summary>主音量键名</summary>
    private const string MASTER_VOLUME_KEY = "Settings_MasterVolume";

    /// <summary>音乐音量键名</summary>
    private const string MUSIC_VOLUME_KEY = "Settings_MusicVolume";

    /// <summary>音效音量键名</summary>
    private const string SFX_VOLUME_KEY = "Settings_SFXVolume";

    /// <summary>鼠标灵敏度键名</summary>
    private const string MOUSE_SENSITIVITY_KEY = "Settings_MouseSensitivity";

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>主音量（0-1）</summary>
    public float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, defaultMasterVolume);
        set
        {
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>音乐音量（0-1）</summary>
    public float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicVolume);
        set
        {
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>音效音量（0-1）</summary>
    public float SFXVolume
    {
        get => PlayerPrefs.GetFloat(SFX_VOLUME_KEY, defaultSFXVolume);
        set
        {
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>鼠标灵敏度</summary>
    public float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, defaultMouseSensitivity);
        set
        {
            PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, Mathf.Clamp(value, 1f, 30f));
            PlayerPrefs.Save();
        }
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeSettingsPanel();
    }

    /// <summary>
    /// 销毁时保存设置
    /// </summary>
    private void OnDestroy()
    {
        SaveSettings();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化设置面板
    /// </summary>
    private void InitializeSettingsPanel()
    {
        if (isInitialized) return;

        // 绑定滑块事件
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            masterVolumeSlider.value = MasterVolume;
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            musicVolumeSlider.value = MusicVolume;
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            sfxVolumeSlider.value = SFXVolume;
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            mouseSensitivitySlider.value = MouseSensitivity;
        }

        // 绑定返回按钮
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        // 初始隐藏面板
        HidePanel();

        isInitialized = true;
        DebugLog("[SettingsPanel] 初始化完成");
    }

    #endregion

    #region UI显示控制

    /// <summary>
    /// 显示设置面板
    /// </summary>
    public void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        DebugLog("[SettingsPanel] 显示设置面板");
    }

    /// <summary>
    /// 隐藏设置面板
    /// </summary>
    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        DebugLog("[SettingsPanel] 隐藏设置面板");
    }

    #endregion

    #region 滑块事件处理

    /// <summary>
    /// 主音量滑块变化
    /// </summary>
    /// <param name="value">新的音量值</param>
    private void OnMasterVolumeChanged(float value)
    {
        MasterVolume = value;
        UpdateVolumeText(masterVolumeText, value);

        // 应用音量到AudioListener
        AudioListener.volume = value;

        DebugLog($"[SettingsPanel] 主音量设置为: {value:F2}");
    }

    /// <summary>
    /// 音乐音量滑块变化
    /// </summary>
    /// <param name="value">新的音量值</param>
    private void OnMusicVolumeChanged(float value)
    {
        MusicVolume = value;
        UpdateVolumeText(musicVolumeText, value);

        DebugLog($"[SettingsPanel] 音乐音量设置为: {value:F2}");
    }

    /// <summary>
    /// 音效音量滑块变化
    /// </summary>
    /// <param name="value">新的音量值</param>
    private void OnSFXVolumeChanged(float value)
    {
        SFXVolume = value;
        UpdateVolumeText(sfxVolumeText, value);

        DebugLog($"[SettingsPanel] 音效音量设置为: {value:F2}");
    }

    /// <summary>
    /// 鼠标灵敏度滑块变化
    /// </summary>
    /// <param name="value">新的灵敏度值</param>
    private void OnMouseSensitivityChanged(float value)
    {
        MouseSensitivity = value;
        UpdateSensitivityText(mouseSensitivityText, value);

        DebugLog($"[SettingsPanel] 鼠标灵敏度设置为: {value:F1}");
    }

    #endregion

    #region UI更新

    /// <summary>
    /// 更新音量文本
    /// </summary>
    /// <param name="text">文本组件</param>
    /// <param name="value">音量值</param>
    private void UpdateVolumeText(Text text, float value)
    {
        if (text != null)
        {
            text.text = $"{value * 100:F0}%";
        }
    }

    /// <summary>
    /// 更新灵敏度文本
    /// </summary>
    /// <param name="text">文本组件</param>
    /// <param name="value">灵敏度值</param>
    private void UpdateSensitivityText(Text text, float value)
    {
        if (text != null)
        {
            text.text = $"{value:F0}";
        }
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 返回按钮点击
    /// </summary>
    private void OnBackClicked()
    {
        DebugLog("[SettingsPanel] 点击返回");

        // 保存设置
        SaveSettings();

        // 隐藏设置面板
        HidePanel();
    }

    #endregion

    #region 设置保存/加载

    /// <summary>
    /// 保存所有设置到PlayerPrefs
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, MasterVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, MusicVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, SFXVolume);
        PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, MouseSensitivity);
        PlayerPrefs.Save();

        DebugLog("[SettingsPanel] 设置已保存");
    }

    /// <summary>
    /// 从PlayerPrefs加载设置
    /// </summary>
    public void LoadSettings()
    {
        // 加载设置（PlayerPrefs.GetFloat已有默认值）
        float masterVol = MasterVolume;
        float musicVol = MusicVolume;
        float sfxVol = SFXVolume;
        float sensitivity = MouseSensitivity;

        // 更新UI
        if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
        if (musicVolumeSlider != null) musicVolumeSlider.value = musicVol;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = sensitivity;

        // 应用音量
        AudioListener.volume = masterVol;

        DebugLog("[SettingsPanel] 设置已加载");
    }

    /// <summary>
    /// 重置为默认设置
    /// </summary>
    public void ResetToDefaults()
    {
        MasterVolume = defaultMasterVolume;
        MusicVolume = defaultMusicVolume;
        SFXVolume = defaultSFXVolume;
        MouseSensitivity = defaultMouseSensitivity;

        // 更新UI
        if (masterVolumeSlider != null) masterVolumeSlider.value = MasterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = MusicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = SFXVolume;
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = MouseSensitivity;

        DebugLog("[SettingsPanel] 设置已重置为默认值");
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
