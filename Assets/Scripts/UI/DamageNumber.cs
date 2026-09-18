using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 伤害飘字组件 - 单个伤害数字的显示和动画
/// 挂载在伤害数字UI对象上，控制上浮、渐隐、缩放动画
/// 由DamageNumberManager对象池管理，支持复用
/// WebGL平台兼容
/// </summary>
public class DamageNumber : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("动画设置")]
    [Tooltip("上浮速度（单位/秒）")]
    [SerializeField] private float floatSpeed = 2f;

    [Tooltip("上浮距离后开始淡出")]
    [SerializeField] private float fadeStartDistance = 1f;

    [Tooltip("总生命周期（秒）")]
    [SerializeField] private float lifetime = 1.2f;

    [Tooltip("弹出缩放动画时间（秒）")]
    [SerializeField] private float popDuration = 0.15f;

    [Tooltip("弹出缩放倍率")]
    [SerializeField] private float popScale = 1.5f;

    [Header("随机偏移")]
    [Tooltip("水平随机偏移范围")]
    [SerializeField] private float randomOffsetX = 0.5f;

    [Tooltip("垂直随机偏移范围")]
    [SerializeField] private float randomOffsetY = 0.3f;

    [Header("组件引用")]
    [Tooltip("Text组件")]
    [SerializeField] private Text damageText;

    #endregion

    #region 私有变量

    /// <summary>起始位置（世界坐标）</summary>
    private Vector3 startPosition;

    /// <summary>起始时间</summary>
    private float startTime;

    /// <summary>当前生命周期进度（0-1）</summary>
    private float lifeProgress = 0f;

    /// <summary>是否正在播放动画</summary>
    private bool isPlaying = false;

    /// <summary>原始颜色</summary>
    private Color originalColor;

    /// <summary>是否是暴击（用于特殊显示）</summary>
    private bool isCritical = false;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 每帧更新动画
    /// </summary>
    private void Update()
    {
        if (!isPlaying) return;

        UpdateFloatAnimation();
        UpdateFadeAnimation();
        UpdatePopAnimation();

        lifeProgress = (Time.time - startTime) / lifetime;

        if (lifeProgress >= 1f)
        {
            ReturnToPool();
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 显示伤害数字
    /// </summary>
    /// <param name="damage">伤害值</param>
    /// <param name="worldPosition">世界坐标位置</param>
    /// <param name="color">文字颜色</param>
    /// <param name="critical">是否暴击</param>
    public void Show(int damage, Vector3 worldPosition, Color color, bool critical = false)
    {
        // 设置伤害文本
        if (damageText != null)
        {
            damageText.text = damage.ToString();
            damageText.color = color;
            originalColor = color;
        }

        // 设置起始位置（世界坐标）
        startPosition = worldPosition;
        startPosition.x += Random.Range(-randomOffsetX, randomOffsetX);
        startPosition.y += Random.Range(-randomOffsetY, randomOffsetY);

        // 暴击特殊处理
        isCritical = critical;
        if (isCritical && damageText != null)
        {
            damageText.fontSize = 24;
            damageText.text = damage.ToString() + "!";
        }
        else if (damageText != null)
        {
            damageText.fontSize = 18;
        }

        // 重置状态
        startTime = Time.time;
        lifeProgress = 0f;
        isPlaying = true;

        // 初始缩放
        transform.localScale = Vector3.one * popScale;

        // 确保可见
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 停止动画并返回对象池
    /// </summary>
    public void ReturnToPool()
    {
        isPlaying = false;
        gameObject.SetActive(false);

        // 通知管理器回收
        DamageNumberManager.Instance?.Recycle(this);
    }

    #endregion

    #region 动画更新

    /// <summary>
    /// 更新上浮动画
    /// </summary>
    private void UpdateFloatAnimation()
    {
        // 上浮移动
        Vector3 currentPos = transform.position;
        currentPos.y += floatSpeed * Time.deltaTime;
        transform.position = currentPos;
    }

    /// <summary>
    /// 更新渐隐动画
    /// </summary>
    private void UpdateFadeAnimation()
    {
        // 当生命周期超过一半后开始淡出
        float fadeProgress = Mathf.InverseLerp(0.5f, 1f, lifeProgress);

        if (damageText != null)
        {
            Color c = originalColor;
            c.a = 1f - fadeProgress;
            damageText.color = c;
        }
    }

    /// <summary>
    /// 更新弹出缩放动画
    /// </summary>
    private void UpdatePopAnimation()
    {
        if (lifeProgress < popDuration / lifetime)
        {
            // 弹出阶段：从大到正常
            float t = lifeProgress / (popDuration / lifetime);
            float scale = Mathf.Lerp(popScale, 1f, t);
            transform.localScale = Vector3.one * scale;
        }
    }

    #endregion
}
