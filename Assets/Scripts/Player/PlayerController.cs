using UnityEngine;

/// <summary>
/// 玩家控制器 - 管理玩家移动、跳跃、攻击、旋转
/// 挂载到Player根GameObject，依赖CharacterController组件
/// 通过InputManager读取输入，通过EventBus分发事件
/// 通过CombatSystem执行战斗逻辑
/// 只负责移动、跳跃、攻击、旋转，不处理动画
/// WebGL平台兼容
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("移动设置")]
    [Tooltip("地面移动速度")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("奔跑速度倍率")]
    [SerializeField] private float runMultiplier = 1.5f;

    [Tooltip("重力加速度")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("地面检测距离")]
    [SerializeField] private float groundCheckDistance = 0.3f;

    [Tooltip("地面检测Layer")]
    [SerializeField] private LayerMask groundLayer;

    [Header("跳跃设置")]
    [Tooltip("跳跃高度")]
    [SerializeField] private float jumpHeight = 2f;

    [Header("攻击设置")]
    [Tooltip("攻击冷却时间（秒）")]
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("攻击持续时间（秒）")]
    [SerializeField] private float attackDuration = 0.4f;

    [Header("旋转设置")]
    [Tooltip("角色旋转灵敏度")]
    [SerializeField] private float rotateSensitivity = 10f;

    [Header("组件引用（自动获取，避免重复GetComponent）")]
    [Tooltip("CharacterController组件")]
    [SerializeField] private CharacterController characterController;

    [Tooltip("CombatSystem组件")]
    [SerializeField] private CombatSystem combatSystem;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有状态变量

    /// <summary>角色垂直速度</summary>
    private float verticalVelocity = 0f;

    /// <summary>是否在地面上</summary>
    private bool isGrounded = true;

    /// <summary>是否已死亡</summary>
    private bool isDead = false;

    /// <summary>攻击冷却计时器</summary>
    private float attackCooldownTimer = 0f;

    /// <summary>攻击持续计时器</summary>
    private float attackDurationTimer = 0f;

    /// <summary>是否正在攻击中</summary>
    private bool isAttacking = false;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>当前移动向量（用于事件传递）</summary>
    private Vector3 currentMoveVector = Vector3.zero;

    #endregion

    #region 公共属性（只读）

    /// <summary>是否在地面上</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>是否已死亡</summary>
    public bool IsDead => isDead;

    /// <summary>是否正在攻击</summary>
    public bool IsAttacking => isAttacking;

    /// <summary>当前移动向量</summary>
    public Vector3 CurrentMoveVector => currentMoveVector;

    /// <summary>移动速度</summary>
    public float MoveSpeed => moveSpeed;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializePlayerController();
    }

    /// <summary>
    /// 每帧更新 - 处理旋转、攻击计时
    /// </summary>
    private void Update()
    {
        if (!isInitialized || isDead) return;

        UpdateRotation();
        UpdateAttackTimers();
    }

    /// <summary>
    /// 固定帧更新 - 处理移动、重力、落地检测
    /// </summary>
    private void FixedUpdate()
    {
        if (!isInitialized || isDead) return;

        UpdateGroundCheck();
        UpdateMovement();
        UpdateGravity();
    }

    /// <summary>
    /// 销毁时取消所有EventBus订阅，防止内存泄漏
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化玩家控制器
    /// </summary>
    private void InitializePlayerController()
    {
        if (isInitialized) return;

        // 获取组件引用（只获取一次，避免重复GetComponent）
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (combatSystem == null)
            combatSystem = GetComponent<CombatSystem>();

        // 组件验证
        if (characterController == null)
        {
            Debug.LogError("[PlayerController] 未找到CharacterController组件！");
            enabled = false;
            return;
        }

        if (combatSystem == null)
        {
            Debug.LogWarning("[PlayerController] 未找到CombatSystem组件，战斗功能不可用");
        }

        // Phase12: 从GameConfig读取移动/攻击设置
        var cfg = GameConfig.Instance;
        moveSpeed = cfg.playerBaseMoveSpeed;
        runMultiplier = cfg.playerRunMultiplier;
        gravity = cfg.playerGravity;
        jumpHeight = cfg.playerJumpHeight;
        attackCooldown = cfg.attackCooldown;
        attackDuration = cfg.attackDuration;
        rotateSensitivity = cfg.rotateSensitivity;

        // 订阅EventBus事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[PlayerController] 初始化完成（配置驱动）");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 攻击输入事件
        EventBus.Subscribe("ON_PLAYER_ATTACK_INPUT", OnAttackInput);

        // 跳跃输入事件
        EventBus.Subscribe("ON_PLAYER_JUMP_INPUT", OnJumpInput);

        // 重生事件
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    /// <summary>
    /// 取消所有EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_ATTACK_INPUT", OnAttackInput);
        EventBus.Unsubscribe("ON_PLAYER_JUMP_INPUT", OnJumpInput);
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 攻击输入事件处理
    /// </summary>
    private void OnAttackInput()
    {
        if (isDead) return;

        TryAttack();
    }

    /// <summary>
    /// 跳跃输入事件处理
    /// </summary>
    private void OnJumpInput()
    {
        if (isDead) return;

        TryJump();
    }

    /// <summary>
    /// 玩家重生事件处理
    /// </summary>
    private void OnPlayerRespawn()
    {
        // 重置状态
        isDead = false;
        isAttacking = false;
        attackCooldownTimer = 0f;
        attackDurationTimer = 0f;
        verticalVelocity = 0f;

        DebugLog("[PlayerController] 玩家重生，状态已重置");
    }

    #endregion

    #region 移动逻辑

    /// <summary>
    /// 更新地面检测
    /// </summary>
    private void UpdateGroundCheck()
    {
        // 使用SphereCast检测地面
        Vector3 spherePos = transform.position + Vector3.down * (characterController.height / 2 - characterController.radius + 0.05f);
        isGrounded = Physics.SphereCast(spherePos, characterController.radius * 0.9f, Vector3.down, out _, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);

        // 备用检测：如果角色控制器报告在地面
        if (!isGrounded)
        {
            isGrounded = characterController.isGrounded;
        }
    }

    /// <summary>
    /// 更新移动逻辑
    /// </summary>
    private void UpdateMovement()
    {
        // 从InputManager读取移动输入
        Vector2 moveInput = Vector2.zero;
        if (InputManager.HasInstance)
        {
            moveInput = InputManager.Instance.MoveInput;
        }

        // 计算移动方向（基于角色朝向）
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;

        // 计算最终移动速度
        float currentSpeed = moveSpeed;
        if (InputManager.HasInstance && InputManager.Instance.IsUsingGamepad)
        {
            // 手柄时可以支持奔跑（例如按住左摇杆）
        }

        // 计算移动向量
        Vector3 moveVector = moveDirection * currentSpeed;

        // 如果在地面且没有移动输入，停止水平移动
        if (isGrounded && moveInput.sqrMagnitude < 0.01f)
        {
            moveVector = Vector3.zero;
        }

        // 应用移动
        characterController.Move(moveVector * Time.fixedDeltaTime);

        // 记录当前移动向量（供动画使用）
        currentMoveVector = moveVector;

        // 发布移动事件
        PublishMoveEvent(moveInput);
    }

    /// <summary>
    /// 更新重力逻辑
    /// </summary>
    private void UpdateGravity()
    {
        if (isGrounded)
        {
            // 在地面上：如果垂直速度为负，重置为小的向下力
            if (verticalVelocity < 0)
            {
                verticalVelocity = -2f;
            }
        }
        else
        {
            // 在空中：应用重力
            verticalVelocity += gravity * Time.fixedDeltaTime;
        }

        // 应用垂直速度
        if (Mathf.Abs(verticalVelocity) > 0.01f)
        {
            characterController.Move(Vector3.up * verticalVelocity * Time.fixedDeltaTime);
        }
    }

    #endregion

    #region 旋转逻辑

    /// <summary>
    /// 更新角色旋转（Y轴）
    /// </summary>
    private void UpdateRotation()
    {
        if (!InputManager.HasInstance) return;

        // 读取视角输入
        Vector2 lookDelta = InputManager.Instance.LookDelta;

        // 只处理水平旋转（Y轴）
        float rotateAmount = lookDelta.x * rotateSensitivity;

        if (Mathf.Abs(rotateAmount) > 0.001f)
        {
            transform.Rotate(Vector3.up, rotateAmount);
        }
    }

    #endregion

    #region 跳跃逻辑

    /// <summary>
    /// 尝试跳跃
    /// </summary>
    private void TryJump()
    {
        // 状态锁检查：在地面才能跳跃
        if (!isGrounded)
        {
            DebugLog("[PlayerController] 不在地面，无法跳跃");
            return;
        }

        // 执行跳跃
        Jump();
    }

    /// <summary>
    /// 执行跳跃
    /// </summary>
    private void Jump()
    {
        // 使用公式计算跳跃初速度：v = sqrt(2 * g * h)
        verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);

        DebugLog("[PlayerController] 跳跃");
    }

    #endregion

    #region 攻击逻辑

    /// <summary>
    /// 尝试攻击
    /// </summary>
    private void TryAttack()
    {
        // 攻击冷却锁检查
        if (attackCooldownTimer > 0)
        {
            DebugLog("[PlayerController] 攻击冷却中");
            return;
        }

        // 攻击持续锁检查
        if (isAttacking)
        {
            DebugLog("[PlayerController] 正在攻击中");
            return;
        }

        // 执行攻击
        Attack();
    }

    /// <summary>
    /// 执行攻击
    /// </summary>
    private void Attack()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        attackDurationTimer = attackDuration;

        // 调用CombatSystem发起攻击
        if (combatSystem != null)
        {
            // CombatSystem处理战斗逻辑
            DebugLog("[PlayerController] 发起攻击");
        }

        // 发布攻击事件
        EventBus.Publish("ON_PLAYER_ATTACK");
    }

    /// <summary>
    /// 更新攻击计时器
    /// </summary>
    private void UpdateAttackTimers()
    {
        // 攻击冷却计时
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // 攻击持续计时
        if (isAttacking)
        {
            attackDurationTimer -= Time.deltaTime;
            if (attackDurationTimer <= 0)
            {
                isAttacking = false;
            }
        }
    }

    #endregion

    #region 事件发布

    /// <summary>
    /// 发布移动事件
    /// </summary>
    /// <param name="moveInput">移动输入向量</param>
    private void PublishMoveEvent(Vector2 moveInput)
    {
        // 只在有移动输入时发布事件
        if (moveInput.sqrMagnitude > 0.01f)
        {
            EventBus.Publish("ON_PLAYER_MOVE");
        }
    }

    #endregion

    #region 状态控制

    /// <summary>
    /// 设置死亡状态
    /// </summary>
    /// <param name="dead">是否死亡</param>
    public void SetDead(bool dead)
    {
        isDead = dead;

        if (dead)
        {
            // 死亡时停止所有动作
            isAttacking = false;
            attackCooldownTimer = 0f;
            attackDurationTimer = 0f;
            verticalVelocity = 0f;
            currentMoveVector = Vector3.zero;

            DebugLog("[PlayerController] 玩家死亡");
        }
    }

    /// <summary>
    /// 重置玩家状态
    /// </summary>
    public void ResetPlayer()
    {
        isDead = false;
        isAttacking = false;
        attackCooldownTimer = 0f;
        attackDurationTimer = 0f;
        verticalVelocity = 0f;
        currentMoveVector = Vector3.zero;

        DebugLog("[PlayerController] 玩家状态已重置");
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
