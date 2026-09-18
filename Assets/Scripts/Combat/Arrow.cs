using UnityEngine;

/// <summary>
/// 箭矢组件 - 远程投射物逻辑
/// 挂载在箭矢预制体上，控制飞行、碰撞、伤害、回收
/// 由ProjectilePool对象池管理
/// WebGL平台兼容
/// </summary>
public class Arrow : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("飞行设置")]
    [Tooltip("飞行速度（单位/秒）")]
    [SerializeField] private float flySpeed = 15f;

    [Tooltip("最大飞行距离")]
    [SerializeField] private float maxFlyDistance = 30f;

    [Tooltip("重力影响系数（0为无重力）")]
    [SerializeField] private float gravityScale = 0.5f;

    [Header("伤害设置")]
    [Tooltip("箭矢伤害值")]
    [SerializeField] private float damageValue = 15f;

    [Tooltip("伤害范围（碰撞盒大小）")]
    [SerializeField] private Vector3 damageBoxSize = new Vector3(0.1f, 0.1f, 0.5f);

    [Header("碰撞设置")]
    [Tooltip("碰撞后自动回收时间（秒）")]
    [SerializeField] private float autoRecycleTime = 3f;

    [Tooltip("碰撞后停留时间（秒）")]
    [SerializeField] private float stickTime = 1.5f;

    [Header("特效设置")]
    [Tooltip("命中特效预制体（可选）")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>飞行方向</summary>
    private Vector3 flyDirection;

    /// <summary>是否正在飞行</summary>
    private bool isFlying = false;

    /// <summary>已飞行距离</summary>
    private float flownDistance = 0f;

    /// <summary>飞行起始位置</summary>
    private Vector3 startPosition;

    /// <summary>是否已命中</summary>
    private bool hasHit = false;

    /// <summary>命中计时器</summary>
    private float hitTimer = 0f;

    /// <summary>垂直速度（用于重力）</summary>
    private float verticalVelocity = 0f;

    #endregion

    #region 公共属性

    /// <summary>是否正在飞行</summary>
    public bool IsFlying => isFlying;

    /// <summary>箭矢伤害值</summary>
    public float DamageValue
    {
        get => damageValue;
        set => damageValue = value;
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 每帧更新
    /// </summary>
    private void Update()
    {
        if (isFlying)
        {
            UpdateFlight();
        }
        else if (hasHit)
        {
            UpdateHitState();
        }
    }

    #endregion

    #region 飞行逻辑

    /// <summary>
    /// 发射箭矢
    /// </summary>
    /// <param name="direction">飞行方向</param>
    /// <param name="speed">飞行速度（可选，使用默认值）</param>
    /// <param name="damage">伤害值（可选，使用默认值）</param>
    public void Fire(Vector3 direction, float speed = -1f, float damage = -1f)
    {
        // 设置参数
        flyDirection = direction.normalized;
        startPosition = transform.position;
        isFlying = true;
        hasHit = false;
        hitTimer = 0f;
        flownDistance = 0f;
        verticalVelocity = 0f;

        // 使用自定义速度
        if (speed > 0)
        {
            flySpeed = speed;
        }

        // 使用自定义伤害
        if (damage > 0)
        {
            damageValue = damage;
        }

        // 发布箭矢发射事件
        EventBus.Publish("ON_ARROW_FIRE");

        DebugLog($"[Arrow] 箭矢发射，方向: {flyDirection}, 伤害: {damageValue}");
    }

    /// <summary>
    /// 更新飞行逻辑
    /// </summary>
    private void UpdateFlight()
    {
        // 计算移动
        Vector3 moveVector = flyDirection * flySpeed * Time.deltaTime;

        // 应用重力
        if (gravityScale > 0)
        {
            verticalVelocity += Physics.gravity.y * gravityScale * Time.deltaTime;
            moveVector.y += verticalVelocity * Time.deltaTime;
        }

        // 移动箭矢
        transform.position += moveVector;
        transform.forward = flyDirection;

        // 更新飞行距离
        flownDistance = Vector3.Distance(transform.position, startPosition);

        // 检查是否超过最大飞行距离
        if (flownDistance >= maxFlyDistance)
        {
            DebugLog("[Arrow] 箭矢超过最大飞行距离，自动回收");
            RecycleArrow();
            return;
        }

        // 检查碰撞
        CheckCollision();
    }

    /// <summary>
    /// 检查碰撞
    /// </summary>
    private void CheckCollision()
    {
        // 使用BoxCast检测碰撞
        RaycastHit hit;
        Vector3 boxCenter = transform.position;
        if (Physics.BoxCast(boxCenter, damageBoxSize * 0.5f, flyDirection, out hit, 
            transform.rotation, flySpeed * Time.deltaTime + 0.5f))
        {
            // 检查是否击中有效目标
            if (hit.collider != null)
            {
                HandleHit(hit.collider, hit.point, hit.normal);
            }
        }
    }

    /// <summary>
    /// 处理命中
    /// </summary>
    /// <param name="collider">碰撞到的Collider</param>
    /// <param name="hitPoint">碰撞点</param>
    /// <param name="hitNormal">碰撞法线</param>
    private void HandleHit(Collider collider, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hasHit) return;

        hasHit = true;
        isFlying = false;
        hitTimer = 0f;

        // 禁用碰撞盒
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            myCollider.enabled = false;
        }

        // 检查是否击中角色
        CombatSystem combat = collider.GetComponent<CombatSystem>();
        if (combat == null)
        {
            combat = collider.GetComponentInParent<CombatSystem>();
        }

        if (combat != null)
        {
            // 对角色造成伤害
            combat.TakeDamage(null, damageValue, hitPoint);
            DebugLog($"[Arrow] 命中角色: {collider.name}，造成 {damageValue} 点伤害");
        }

        // 检查是否击中地面（Layer 0为Default）
        if (collider.gameObject.layer == 0)
        {
            // 插入地面效果
            transform.position = hitPoint;
            transform.forward = hitNormal;
            DebugLog("[Arrow] 命中地面");
        }

        // 生成命中特效
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(effect, 2f);
        }

        // 开始命中计时器
        hitTimer = 0f;
    }

    /// <summary>
    /// 更新命中状态
    /// </summary>
    private void UpdateHitState()
    {
        hitTimer += Time.deltaTime;

        // 命中后停留时间结束，自动回收
        if (hitTimer >= stickTime)
        {
            RecycleArrow();
        }
    }

    #endregion

    #region 回收逻辑

    /// <summary>
    /// 回收箭矢到对象池
    /// </summary>
    public void RecycleArrow()
    {
        isFlying = false;
        hasHit = false;
        hitTimer = 0f;
        flownDistance = 0f;
        verticalVelocity = 0f;

        // 通知对象池回收
        if (ProjectilePool.HasInstance)
        {
            ProjectilePool.Instance.RecycleArrow(gameObject);
        }
        else
        {
            // 对象池不存在，直接销毁
            Destroy(gameObject);
        }

        DebugLog("[Arrow] 箭矢回收");
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
