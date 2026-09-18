using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 伤害飘字管理器 - 对象池管理伤害数字
/// 单例模式，自动创建Canvas和伤害数字预制体
/// 订阅EventBus事件：ON_PLAYER_HIT、ON_ENEMY_HIT
/// 在受击位置弹出伤害数字，支持对象池复用
/// WebGL平台兼容
/// </summary>
public class DamageNumberManager : Singleton<DamageNumberManager>
{
    #region Inspector可配置参数

    [Header("对象池设置")]
    [Tooltip("初始对象池大小")]
    [SerializeField] private int initialPoolSize = 20;

    [Tooltip("对象池最大大小")]
    [SerializeField] private int maxPoolSize = 50;

    [Header("伤害数字设置")]
    [Tooltip("玩家受伤颜色")]
    [SerializeField] private Color playerHitColor = new Color(1f, 0.3f, 0.3f);

    [Tooltip("敌人受伤颜色")]
    [SerializeField] private Color enemyHitColor = new Color(1f, 0.9f, 0.2f);

    [Tooltip("暴击颜色")]
    [SerializeField] private Color criticalColor = new Color(1f, 0.1f, 0.1f);

    [Tooltip("治疗颜色")]
    [SerializeField] private Color healColor = new Color(0.2f, 1f, 0.2f);

    [Header("Canvas设置")]
    [Tooltip("Canvas缩放系数")]
    [SerializeField] private float canvasScale = 0.005f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>伤害数字对象池</summary>
    private List<DamageNumber> objectPool = new List<DamageNumber>();

    /// <summary>伤害数字父Canvas</summary>
    private Canvas damageCanvas;

    /// <summary>伤害数字预制体模板</summary>
    private GameObject damageNumberPrefab;

    /// <summary>Phase10: 缓存的玩家CombatSystem</summary>
    private CombatSystem cachedPlayerCombat;

    /// <summary>Phase10: 缓存的主相机</summary>
    private Camera cachedMainCamera;

    #endregion

    #region 属性

    /// <summary>是否输出调试日志</summary>
    public bool EnableDebugLog => enableDebugLog;

    #endregion

    #region Singleton重写

    /// <summary>不需要跨场景保留</summary>
    protected override bool ShouldDontDestroyOnLoad => true;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    protected override void OnSingletonAwake()
    {
        InitializeManager();
    }

    /// <summary>
    /// 销毁时取消订阅
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化管理器
    /// </summary>
    private void InitializeManager()
    {
        // Phase10: 缓存引用
        cachedMainCamera = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            cachedPlayerCombat = player.GetComponent<CombatSystem>();
        }

        CreateDamageCanvas();
        CreateDamageNumberPrefab();
        InitializeObjectPool();
        SubscribeEvents();

        DebugLog("[DamageNumberManager] 初始化完成");
    }

    /// <summary>
    /// 创建伤害数字Canvas（World Space模式）
    /// </summary>
    private void CreateDamageCanvas()
    {
        GameObject canvasObj = new GameObject("DamageNumberCanvas");
        canvasObj.transform.SetParent(transform);

        damageCanvas = canvasObj.AddComponent<Canvas>();
        damageCanvas.renderMode = RenderMode.WorldSpace;
        damageCanvas.sortingOrder = 200;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000f, 1000f);
        canvasRect.localScale = Vector3.one * canvasScale;

        // 禁用GraphicRaycaster（不需要交互）
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        canvasObj.GetComponent<GraphicRaycaster>().enabled = false;
    }

    /// <summary>
    /// 创建伤害数字预制体模板
    /// </summary>
    private void CreateDamageNumberPrefab()
    {
        damageNumberPrefab = new GameObject("DamageNumberPrefab");
        damageNumberPrefab.transform.SetParent(transform);

        // 添加RectTransform
        RectTransform rectTransform = damageNumberPrefab.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100f, 50f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 添加Text组件
        Text text = damageNumberPrefab.AddComponent<Text>();
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "0";
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // 添加Shadow效果增加可读性
        Shadow shadow = damageNumberPrefab.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(1f, -1f);

        // 添加Outline效果
        Outline outline = damageNumberPrefab.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        // 添加DamageNumber组件
        DamageNumber damageNumber = damageNumberPrefab.AddComponent<DamageNumber>();

        // 预制体初始不激活
        damageNumberPrefab.SetActive(false);

        // 隐藏在Hierarchy中
        damageNumberPrefab.hideFlags = HideFlags.HideInHierarchy;
    }

    /// <summary>
    /// 初始化对象池
    /// </summary>
    private void InitializeObjectPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePoolObject();
        }

        DebugLog($"[DamageNumberManager] 对象池初始化完成，数量: {objectPool.Count}");
    }

    /// <summary>
    /// 创建对象池对象
    /// </summary>
    /// <returns>新建的DamageNumber</returns>
    private DamageNumber CreatePoolObject()
    {
        GameObject obj = Instantiate(damageNumberPrefab, damageCanvas.transform);
        obj.name = "DamageNumber";

        DamageNumber damageNumber = obj.GetComponent<DamageNumber>();
        objectPool.Add(damageNumber);

        return damageNumber;
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 玩家受伤事件
        EventBus.Subscribe("ON_PLAYER_HIT", OnPlayerHit);

        // 敌人受伤事件
        EventBus.Subscribe("ON_ENEMY_HIT", OnEnemyHit);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Unsubscribe("ON_ENEMY_HIT", OnEnemyHit);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家受伤事件处理 - 显示红色伤害数字
    /// </summary>
    private void OnPlayerHit()
    {
        // Phase10: 使用缓存引用
        if (cachedPlayerCombat == null)
        {
            cachedPlayerCombat = FindObjectOfType<CombatSystem>();
        }

        if (cachedPlayerCombat != null && cachedPlayerCombat.IsPlayer)
        {
            int damage = cachedPlayerCombat.MaxHealth - cachedPlayerCombat.CurrentHealth;
            if (damage > 0)
            {
                ShowDamage(damage, cachedPlayerCombat.transform.position + Vector3.up * 2.2f, playerHitColor);
            }
        }
    }

    private void OnEnemyHit()
    {
        // Phase10: 使用缓存的Camera
        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
        }

        CombatSystem[] allCombats = FindObjectsOfType<CombatSystem>();
        CombatSystem latestHit = null;
        float latestDistance = float.MaxValue;

        foreach (var combat in allCombats)
        {
            if (!combat.IsPlayer && !combat.IsDead)
            {
                float dist = Vector3.Distance(cachedMainCamera.transform.position, combat.transform.position);
                if (dist < latestDistance)
                {
                    latestDistance = dist;
                    latestHit = combat;
                }
            }
        }

        if (latestHit != null)
        {
            int damage = latestHit.MaxHealth - latestHit.CurrentHealth;
            if (damage > 0)
            {
                ShowDamage(damage, latestHit.transform.position + Vector3.up * 2.2f, enemyHitColor);
            }
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 显示伤害数字（指定颜色）
    /// </summary>
    /// <param name="damage">伤害值</param>
    /// <param name="worldPosition">世界坐标位置</param>
    /// <param name="color">显示颜色</param>
    /// <param name="critical">是否暴击</param>
    public void ShowDamage(int damage, Vector3 worldPosition, Color color, bool critical = false)
    {
        // 从对象池获取
        DamageNumber damageNumber = GetFromPool();
        if (damageNumber == null)
        {
            // 对象池已满，创建新的
            if (objectPool.Count < maxPoolSize)
            {
                damageNumber = CreatePoolObject();
            }
            else
            {
                DebugLog("[DamageNumberManager] 对象池已满，无法创建新的伤害数字");
                return;
            }
        }

        // 设置伤害数字位置（World Space Canvas下）
        // 将世界坐标转换为Canvas下的局部坐标
        Vector3 canvasPos = damageCanvas.transform.InverseTransformPoint(worldPosToScreenPos(worldPosition));
        canvasPos.z = 0f;
        damageNumber.transform.localPosition = canvasPos;

        // 显示伤害数字
        damageNumber.Show(damage, worldPosition, color, critical);

        DebugLog($"[DamageNumberManager] 显示伤害数字: {damage}, 位置: {worldPosition}");
    }

    /// <summary>
    /// 显示治疗数字（绿色）
    /// </summary>
    /// <param name="heal">治疗量</param>
    /// <param name="worldPosition">世界坐标位置</param>
    public void ShowHeal(int heal, Vector3 worldPosition)
    {
        ShowDamage(heal, worldPosition, healColor);
    }

    /// <summary>
    /// 回收伤害数字到对象池
    /// </summary>
    /// <param name="damageNumber">要回收的伤害数字</param>
    public void Recycle(DamageNumber damageNumber)
    {
        if (damageNumber != null && !objectPool.Contains(damageNumber))
        {
            objectPool.Add(damageNumber);
        }
    }

    #endregion

    #region 对象池管理

    /// <summary>
    /// 从对象池获取对象
    /// </summary>
    /// <returns>可用的DamageNumber，如果没有则返回null</returns>
    private DamageNumber GetFromPool()
    {
        // 查找未激活的对象
        for (int i = objectPool.Count - 1; i >= 0; i--)
        {
            if (objectPool[i] != null && !objectPool[i].gameObject.activeSelf)
            {
                return objectPool[i];
            }
        }

        return null;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 世界坐标转屏幕坐标
    /// </summary>
    /// <param name="worldPos">世界坐标</param>
    /// <returns>屏幕坐标</returns>
    private Vector3 worldPosToScreenPos(Vector3 worldPos)
    {
        if (Camera.main != null)
        {
            return Camera.main.WorldToScreenPoint(worldPos);
        }
        return worldPos;
    }

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
