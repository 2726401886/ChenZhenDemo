using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 攻击碰撞盒组件 - 通用近战攻击判定
/// 挂载在攻击空物体（子物体）上，碰撞盒默认关闭
/// 订阅动画帧事件控制碰撞盒开关
/// 检测碰撞并调用目标CombatSystem的TakeDamage方法
/// 支持Layer过滤，只打击敌方目标
/// 防重复击中锁：一次攻击只能命中同一个目标一次
/// WebGL平台兼容
/// </summary>
public class AttackHitbox : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("攻击参数")]
    [Tooltip("本次攻击的伤害值")]
    [SerializeField] private float damageValue = 10f;

    [Tooltip("攻击所属的攻击者GameObject（通常是挂载此组件的角色）")]
    [SerializeField] private GameObject owner;

    [Tooltip("攻击目标Layer（只打击这些Layer的对象）")]
    [SerializeField] private LayerMask targetLayerMask;

    [Header("碰撞盒设置")]
    [Tooltip("碰撞盒Collider组件（自动获取本对象的Collider）")]
    [SerializeField] private Collider hitboxCollider;

    [Tooltip("攻击持续时间（秒）- 自动关闭碰撞盒的备用计时器")]
    [SerializeField] private float attackDuration = 0.5f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>本次攻击已命中的目标列表（防重复击中锁）</summary>
    private List<GameObject> hitTargets = new List<GameObject>();

    /// <summary>是否正在检测碰撞</summary>
    private bool isActive = false;

    /// <summary>攻击计时器</summary>
    private float attackTimer = 0f;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>是否正在检测碰撞</summary>
    public bool IsActive => isActive;

    /// <summary>本次攻击命中目标数量</summary>
    public int HitCount => hitTargets.Count;

    /// <summary>攻击伤害值（可运行时修改）</summary>
    public float DamageValue
    {
        get => damageValue;
        set => damageValue = Mathf.Max(0, value);
    }

    /// <summary>攻击者GameObject</summary>
    public GameObject Owner
    {
        get => owner;
        set => owner = value;
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeHitbox();
    }

    /// <summary>
    /// 每帧更新 - 备用计时器自动关闭
    /// </summary>
    private void Update()
    {
        if (!isActive || !isInitialized) return;

        // 备用计时器：超时自动关闭碰撞盒
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackDuration)
        {
            DisableHitbox();
        }
    }

    /// <summary>
    /// 销毁时取消EventBus订阅，防止内存泄漏
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化攻击碰撞盒
    /// </summary>
    private void InitializeHitbox()
    {
        if (isInitialized) return;

        // 自动获取Collider组件
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        // 确保Collider是触发器模式
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false; // 默认关闭
        }
        else
        {
            Debug.LogError("[AttackHitbox] 未找到Collider组件！");
        }

        // 如果未指定攻击者，尝试从父对象获取
        if (owner == null)
        {
            owner = transform.parent?.gameObject;
        }

        // 订阅动画帧事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[AttackHitbox] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅动画帧事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 订阅伤害帧事件：开启碰撞检测
        EventBus.Subscribe("ON_ATTACK_DAMAGE_FRAME", OnAttackDamageFrame);

        // 订阅攻击动画结束事件：关闭碰撞盒
        EventBus.Subscribe("ON_ATTACK_ANIM_END", OnAttackAnimEnd);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_ATTACK_DAMAGE_FRAME", OnAttackDamageFrame);
        EventBus.Unsubscribe("ON_ATTACK_ANIM_END", OnAttackAnimEnd);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 攻击伤害帧事件处理 - 开启碰撞检测
    /// </summary>
    private void OnAttackDamageFrame()
    {
        EnableHitbox();
    }

    /// <summary>
    /// 攻击动画结束事件处理 - 关闭碰撞盒
    /// </summary>
    private void OnAttackAnimEnd()
    {
        DisableHitbox();
    }

    #endregion

    #region 碰撞盒控制

    /// <summary>
    /// 启用碰撞盒 - 开始检测碰撞
    /// </summary>
    public void EnableHitbox()
    {
        if (!isInitialized) return;

        // 清空本次攻击的命中列表（新的攻击开始）
        hitTargets.Clear();

        // 重置计时器
        attackTimer = 0f;

        // 启用碰撞检测
        isActive = true;
        if (hitboxCollider != null)
            hitboxCollider.enabled = true;

        DebugLog("[AttackHitbox] 碰撞盒已启用");
    }

    /// <summary>
    /// 禁用碰撞盒 - 停止检测碰撞
    /// </summary>
    public void DisableHitbox()
    {
        if (!isInitialized) return;

        // 禁用碰撞检测
        isActive = false;
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;

        DebugLog("[AttackHitbox] 碰撞盒已禁用，本次命中: " + hitTargets.Count + " 个目标");
    }

    #endregion

    #region 碰撞检测

    /// <summary>
    /// 触发器碰撞检测
    /// </summary>
    /// <param name="other">碰撞到的对象</param>
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        // 检查目标是否在目标Layer上
        if (((1 << other.gameObject.layer) & targetLayerMask) == 0)
        {
            return; // 不在目标Layer，跳过
        }

        // 防重复击中检查
        if (hitTargets.Contains(other.gameObject))
        {
            return; // 已经击中过，跳过
        }

        // 获取目标的CombatSystem组件
        CombatSystem targetCombat = other.GetComponent<CombatSystem>();
        if (targetCombat == null)
        {
            // 尝试从父对象获取
            targetCombat = other.GetComponentInParent<CombatSystem>();
        }

        // 检查目标是否有效
        if (targetCombat == null)
        {
            return; // 目标没有CombatSystem，跳过
        }

        // 检查目标是否已死亡
        if (targetCombat.IsDead)
        {
            return; // 目标已死亡，跳过
        }

        // 检查是否攻击自己（防止友军伤害）
        if (owner != null && other.gameObject == owner)
        {
            return; // 攻击自己，跳过
        }

        // 记录已命中（防重复击中锁）
        hitTargets.Add(other.gameObject);

        // 调用目标的TakeDamage方法
        Vector3 hitPos = other.ClosestPoint(transform.position);
        targetCombat.TakeDamage(owner, damageValue, hitPos);

        DebugLog("[AttackHitbox] 命中目标: " + other.gameObject.name + "，伤害: " + damageValue);
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 重置命中列表（可手动调用）
    /// </summary>
    public void ResetHitTargets()
    {
        hitTargets.Clear();
    }

    /// <summary>
    /// 设置攻击者
    /// </summary>
    /// <param name="newOwner">新的攻击者</param>
    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
    }

    /// <summary>
    /// 设置伤害值
    /// </summary>
    /// <param name="newDamage">新的伤害值</param>
    public void SetDamage(float newDamage)
    {
        damageValue = Mathf.Max(0, newDamage);
    }

    /// <summary>
    /// 设置目标Layer
    /// </summary>
    /// <param name="newLayerMask">新的Layer掩码</param>
    public void SetTargetLayer(LayerMask newLayerMask)
    {
        targetLayerMask = newLayerMask;
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
