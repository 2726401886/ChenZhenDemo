using UnityEngine;

/// <summary>
/// 武器基础组件 - 挂载在武器子物体上
/// 定义武器属性：名称、伤害、冷却、攻击范围、碰撞盒偏移
/// 武器启用/禁用时自动开关攻击碰撞盒
/// 由WeaponManager管理武器切换
/// WebGL平台兼容
/// </summary>
public class Weapon : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("武器基础属性")]
    [Tooltip("武器名称")]
    [SerializeField] private string weaponName = "Unknown Weapon";

    [Tooltip("武器伤害值")]
    [SerializeField] private float weaponDamage = 10f;

    [Tooltip("攻击冷却时间（秒）")]
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("攻击持续时间（秒）")]
    [SerializeField] private float attackDuration = 0.4f;

    [Tooltip("攻击范围（碰撞盒长度）")]
    [SerializeField] private float attackRange = 1.8f;

    [Header("碰撞盒设置")]
    [Tooltip("攻击碰撞盒偏移（相对于武器位置）")]
    [SerializeField] private Vector3 hitboxOffset = new Vector3(0f, 0f, 0.5f);

    [Tooltip("攻击碰撞盒大小")]
    [SerializeField] private Vector3 hitboxSize = new Vector3(0.6f, 1.2f, 1f);

    [Header("武器外观")]
    [Tooltip("武器颜色（用于运行时材质设置）")]
    [SerializeField] private Color weaponColor = Color.white;

    [Tooltip("武器缩放")]
    [SerializeField] private Vector3 weaponScale = new Vector3(0.15f, 0.15f, 0.8f);

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>武器所属的攻击者GameObject</summary>
    private GameObject owner;

    /// <summary>武器的AttackHitbox组件</summary>
    private AttackHitbox attackHitbox;

    /// <summary>武器的Collider组件</summary>
    private Collider weaponCollider;

    /// <summary>武器的Renderer组件</summary>
    private Renderer weaponRenderer;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>是否已激活</summary>
    private bool isActive = false;

    #endregion

    #region 公共属性

    /// <summary>武器名称</summary>
    public string WeaponName => weaponName;

    /// <summary>武器伤害值</summary>
    public float WeaponDamage => weaponDamage;

    /// <summary>攻击冷却时间</summary>
    public float AttackCooldown => attackCooldown;

    /// <summary>攻击持续时间</summary>
    public float AttackDuration => attackDuration;

    /// <summary>攻击范围</summary>
    public float AttackRange => attackRange;

    /// <summary>武器所属的攻击者</summary>
    public GameObject Owner
    {
        get => owner;
        set => owner = value;
    }

    /// <summary>是否已激活</summary>
    public bool IsActive => isActive;

    /// <summary>攻击碰撞盒偏移</summary>
    public Vector3 HitboxOffset => hitboxOffset;

    /// <summary>攻击碰撞盒大小</summary>
    public Vector3 HitboxSize => hitboxSize;

    /// <summary>武器颜色</summary>
    public Color WeaponColor => weaponColor;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// Awake时初始化
    /// </summary>
    private void Awake()
    {
        InitializeWeapon();
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        CleanupWeapon();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化武器
    /// </summary>
    private void InitializeWeapon()
    {
        if (isInitialized) return;

        // 获取组件引用
        weaponCollider = GetComponent<Collider>();
        weaponRenderer = GetComponent<Renderer>();
        attackHitbox = GetComponentInChildren<AttackHitbox>();

        // 如果没有AttackHitbox，创建一个
        if (attackHitbox == null)
        {
            CreateAttackHitbox();
        }

        // 设置武器外观
        SetupWeaponAppearance();

        isInitialized = true;
        DebugLog($"[Weapon] 初始化完成: {weaponName}");
    }

    /// <summary>
    /// 创建攻击碰撞盒
    /// </summary>
    private void CreateAttackHitbox()
    {
        GameObject hitboxObj = new GameObject("AttackHitbox");
        hitboxObj.transform.SetParent(transform);
        hitboxObj.transform.localPosition = hitboxOffset;
        hitboxObj.transform.localRotation = Quaternion.identity;
        hitboxObj.transform.localScale = hitboxSize;

        // 添加BoxCollider作为触发器
        BoxCollider collider = hitboxObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = Vector3.one;

        // 添加Rigidbody（用于触发器检测）
        Rigidbody rb = hitboxObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 添加AttackHitbox组件
        attackHitbox = hitboxObj.AddComponent<AttackHitbox>();

        // 设置攻击伤害值
        attackHitbox.DamageValue = weaponDamage;

        DebugLog($"[Weapon] 创建攻击碰撞盒: {hitboxObj.name}");
    }

    /// <summary>
    /// 设置武器外观
    /// </summary>
    private void SetupWeaponAppearance()
    {
        // 设置武器缩放
        transform.localScale = weaponScale;

        // 设置材质颜色
        if (weaponRenderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = weaponColor;
            mat.SetFloat("_Metallic", 0.4f);
            mat.SetFloat("_Glossiness", 0.6f);
            weaponRenderer.material = mat;
        }
    }

    #endregion

    #region 武器激活/禁用

    /// <summary>
    /// 激活武器（启用碰撞盒和渲染）
    /// </summary>
    public void Activate()
    {
        isActive = true;

        // 显示武器
        gameObject.SetActive(true);

        // 启用碰撞盒
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }

        DebugLog($"[Weapon] 武器已激活: {weaponName}");
    }

    /// <summary>
    /// 禁用武器（禁用碰撞盒和渲染）
    /// </summary>
    public void Deactivate()
    {
        isActive = false;

        // 禁用攻击碰撞盒
        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }

        // 隐藏武器
        gameObject.SetActive(false);

        DebugLog($"[Weapon] 武器已禁用: {weaponName}");
    }

    /// <summary>
    /// 设置攻击者
    /// </summary>
    /// <param name="attacker">攻击者GameObject</param>
    public void SetOwner(GameObject attacker)
    {
        owner = attacker;

        // 更新AttackHitbox的owner
        if (attackHitbox != null)
        {
            attackHitbox.Owner = attacker;
        }
    }

    /// <summary>
    /// 更新攻击伤害值
    /// </summary>
    /// <param name="damage">新的伤害值</param>
    public void SetDamage(float damage)
    {
        weaponDamage = damage;

        // 更新AttackHitbox的伤害值
        if (attackHitbox != null)
        {
            attackHitbox.DamageValue = damage;
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

    /// <summary>
    /// 清理武器
    /// </summary>
    private void CleanupWeapon()
    {
        // 清理材质
        if (weaponRenderer != null && weaponRenderer.material != null)
        {
            Destroy(weaponRenderer.material);
        }
    }

    #endregion
}
