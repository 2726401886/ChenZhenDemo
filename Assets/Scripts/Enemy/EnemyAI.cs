using UnityEngine;

/// <summary>
/// 敌人AI控制器 - 有限状态机驱动敌人行为
/// 四状态FSM：Idle（巡逻）→ Chase（追击）→ Attack（攻击）→ Dead（死亡）
/// 感知机制：视野检测（角度+距离），超出范围丢失目标回到巡逻
/// 攻击判定：目标进入攻击距离才触发攻击；攻击冷却锁
/// 通过EventBus发布事件驱动EnemyAnimation
/// WebGL平台兼容
/// </summary>
public class EnemyAI : MonoBehaviour
{
    #region FSM状态枚举

    /// <summary>
    /// 敌人AI状态枚举
    /// </summary>
    public enum AIState
    {
        Idle,       // 巡逻状态
        Chase,      // 追击状态
        Attack,     // 攻击状态
        Dead        // 死亡状态
    }

    #endregion

    #region Inspector可配置参数

    [Header("巡逻设置")]
    [Tooltip("巡逻半径（以出生点为中心）")]
    [SerializeField] private float patrolRadius = 8f;

    [Tooltip("巡逻移动速度")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("到达巡逻点后的等待时间")]
    [SerializeField] private float patrolWaitTime = 1.5f;

    [Header("感知设置")]
    [Tooltip("视野检测距离")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("视野检测角度（锥形视野）")]
    [SerializeField] private float detectionAngle = 120f;

    [Tooltip("目标丢失距离（超出此距离回到巡逻）")]
    [SerializeField] private float loseTargetDistance = 15f;

    [Header("追击设置")]
    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 4f;

    [Header("攻击设置")]
    [Tooltip("攻击触发距离")]
    [SerializeField] private float attackRange = 1.5f;

    [Tooltip("攻击冷却时间（秒）")]
    [SerializeField] private float attackCooldown = 1f;

    [Tooltip("攻击伤害值")]
    [SerializeField] private float attackDamage = 10f;

    [Header("组件引用（自动获取）")]
    [Tooltip("CharacterController组件")]
    [SerializeField] private CharacterController characterController;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有状态变量

    /// <summary>当前AI状态</summary>
    private AIState currentState = AIState.Idle;

    /// <summary>当前追击目标</summary>
    private Transform target;

    /// <summary>出生点位置（巡逻中心点）</summary>
    private Vector3 spawnPosition;

    /// <summary>当前巡逻目标点</summary>
    private Vector3 currentPatrolTarget;

    /// <summary>巡逻等待计时器</summary>
    private float patrolWaitTimer = 0f;

    /// <summary>是否正在等待</summary>
    private bool isWaiting = false;

    /// <summary>攻击冷却计时器</summary>
    private float attackCooldownTimer = 0f;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>重力速度</summary>
    private float verticalVelocity = 0f;

    /// <summary>Phase10: 缓存的玩家Transform（避免每帧FindGameObjectWithTag）</summary>
    private static Transform cachedPlayerTransform;

    /// <summary>Phase10: 帧节流计数器</summary>
    private int frameThrottleCounter = 0;

    /// <summary>Phase10: 检测频率（每N帧执行一次检测）</summary>
    private const int DETECTION_FRAME_INTERVAL = 3;

    #endregion

    #region 公共属性

    /// <summary>当前AI状态</summary>
    public AIState CurrentState => currentState;

    /// <summary>当前追击目标</summary>
    public Transform Target => target;

    /// <summary>是否存活</summary>
    public bool IsAlive => currentState != AIState.Dead;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeEnemyAI();
    }

    /// <summary>
    /// 每帧更新 - 状态机逻辑
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        if (currentState == AIState.Dead) return;

        // Phase10优化：帧节流，每DETECTION_FRAME_INTERVAL帧执行一次完整逻辑
        frameThrottleCounter++;
        if (frameThrottleCounter % DETECTION_FRAME_INTERVAL != 0)
        {
            // 非检测帧只更新攻击冷却和移动（不执行感知检测）
            UpdateAttackCooldown();
            return;
        }

        UpdateAttackCooldown();

        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdleState();
                break;
            case AIState.Chase:
                UpdateChaseState();
                break;
            case AIState.Attack:
                UpdateAttackState();
                break;
        }
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
    /// 初始化敌人AI
    /// </summary>
    private void InitializeEnemyAI()
    {
        if (isInitialized) return;

        // 获取组件
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        // Phase12: 从EnemyConfig读取AI参数
        var cfg = EnemyConfig.Get(gameObject.name);
        patrolRadius = cfg.patrolRadius;
        patrolSpeed = cfg.patrolSpeed;
        patrolWaitTime = cfg.patrolWaitTime;
        detectionRange = cfg.detectionRange;
        detectionAngle = cfg.detectionAngle;
        loseTargetDistance = cfg.loseTargetDistance;
        chaseSpeed = cfg.chaseSpeed;
        attackRange = cfg.attackRange;
        attackCooldown = cfg.attackCooldown;
        attackDamage = cfg.attackDamage;

        // 记录出生点
        spawnPosition = transform.position;

        // 生成初始巡逻点
        SetNewPatrolTarget();

        // 订阅EventBus事件
        SubscribeEvents();

        // 发布状态事件
        PublishStateEvent("ON_ENEMY_STATE_IDLE");

        isInitialized = true;
        DebugLog("[EnemyAI] 初始化完成（配置驱动）");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Subscribe("ON_ENEMY_RESPAWN", OnEnemyRespawn);
    }

    /// <summary>
    /// 取消所有EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Unsubscribe("ON_ENEMY_RESPAWN", OnEnemyRespawn);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 敌人死亡事件处理
    /// </summary>
    private void OnEnemyDie()
    {
        // 只处理自己（通过距离判断或对象判断）
        // 这里简化处理：如果当前状态不是Dead则切换
        if (currentState != AIState.Dead)
        {
            ChangeState(AIState.Dead);
        }
    }

    /// <summary>
    /// 敌人重生事件处理
    /// </summary>
    private void OnEnemyRespawn()
    {
        // 重置状态回到Idle
        ResetEnemyAI();
    }

    #endregion

    #region 状态切换

    /// <summary>
    /// 切换AI状态
    /// </summary>
    /// <param name="newState">新状态</param>
    private void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        AIState oldState = currentState;
        currentState = newState;

        // 发布状态切换事件
        switch (newState)
        {
            case AIState.Idle:
                PublishStateEvent("ON_ENEMY_STATE_IDLE");
                break;
            case AIState.Chase:
                PublishStateEvent("ON_ENEMY_STATE_CHASE");
                break;
            case AIState.Attack:
                PublishStateEvent("ON_ENEMY_STATE_ATTACK");
                break;
            case AIState.Dead:
                PublishStateEvent("ON_ENEMY_STATE_DEAD");
                break;
        }

        DebugLog("[EnemyAI] 状态切换: " + oldState + " → " + newState);
    }

    #endregion

    #region 状态更新逻辑

    /// <summary>
    /// 巡逻状态更新
    /// </summary>
    private void UpdateIdleState()
    {
        // 检测玩家
        if (DetectPlayer())
        {
            // 发现玩家，切换到追击状态
            ChangeState(AIState.Chase);
            return;
        }

        // 巡逻逻辑
        if (isWaiting)
        {
            // 等待中
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0)
            {
                isWaiting = false;
                SetNewPatrolTarget();
            }
        }
        else
        {
            // 移动到巡逻点
            MoveToPosition(currentPatrolTarget, patrolSpeed);

            // 检查是否到达巡逻点
            float distance = Vector3.Distance(transform.position, currentPatrolTarget);
            if (distance < 0.5f)
            {
                // 到达巡逻点，开始等待
                isWaiting = true;
                patrolWaitTimer = patrolWaitTime;
            }
        }
    }

    /// <summary>
    /// 追击状态更新
    /// </summary>
    private void UpdateChaseState()
    {
        // 检测玩家是否还在视野内
        if (!DetectPlayer())
        {
            // 丢失目标，回到巡逻
            target = null;
            ChangeState(AIState.Idle);
            return;
        }

        // 检查目标距离
        if (target != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            // 超出丢失距离
            if (distanceToTarget > loseTargetDistance)
            {
                target = null;
                ChangeState(AIState.Idle);
                return;
            }

            // 进入攻击范围
            if (distanceToTarget <= attackRange)
            {
                ChangeState(AIState.Attack);
                return;
            }

            // 追击移动
            MoveToPosition(target.position, chaseSpeed);
        }
    }

    /// <summary>
    /// 攻击状态更新
    /// </summary>
    private void UpdateAttackState()
    {
        // 检查目标是否有效
        if (target == null)
        {
            ChangeState(AIState.Idle);
            return;
        }

        // 检查目标距离
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // 目标离开攻击范围，回到追击
        if (distanceToTarget > attackRange * 1.2f)
        {
            ChangeState(AIState.Chase);
            return;
        }

        // 面向目标
        FaceTarget(target.position);

        // 检查攻击冷却
        if (attackCooldownTimer <= 0)
        {
            // 执行攻击
            PerformAttack();
        }
    }

    #endregion

    #region 感知机制

    /// <summary>
    /// 检测玩家
    /// </summary>
    /// <returns>是否检测到玩家</returns>
    private bool DetectPlayer()
    {
        // Phase10优化：使用缓存的玩家引用，避免每帧FindGameObjectWithTag
        if (cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return false;
            cachedPlayerTransform = player.transform;
        }

        Transform playerTransform = cachedPlayerTransform;
        Vector3 directionToPlayer = playerTransform.position - transform.position;

        // Phase10优化：使用sqrMagnitude避免开方
        float sqrDistance = directionToPlayer.sqrMagnitude;
        float sqrDetectionRange = detectionRange * detectionRange;

        if (sqrDistance > sqrDetectionRange)
        {
            return false;
        }

        // 角度检测
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > detectionAngle / 2f)
        {
            return false;
        }

        target = playerTransform;
        return true;
    }

    #endregion

    #region 移动逻辑

    /// <summary>
    /// 移动到指定位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="speed">移动速度</param>
    private void MoveToPosition(Vector3 targetPosition, float speed)
    {
        if (characterController == null) return;

        // 计算移动方向（水平面）
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.magnitude > 0.1f)
        {
            // 面向移动方向
            transform.forward = direction.normalized;

            // 计算移动向量
            Vector3 moveVector = direction.normalized * speed * Time.deltaTime;

            // 应用重力
            ApplyGravity();

            // 应用移动
            characterController.Move(moveVector + Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }

    /// <summary>
    /// 面向目标
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }

    /// <summary>
    /// 应用重力
    /// </summary>
    private void ApplyGravity()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }
    }

    #endregion

    #region 攻击逻辑

    /// <summary>
    /// 执行攻击
    /// </summary>
    private void PerformAttack()
    {
        // 启动攻击冷却
        attackCooldownTimer = attackCooldown;

        // 发布攻击事件
        EventBus.Publish("ON_ENEMY_ATTACK");

        DebugLog("[EnemyAI] 执行攻击");
    }

    /// <summary>
    /// 更新攻击冷却
    /// </summary>
    private void UpdateAttackCooldown()
    {
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }
    }

    #endregion

    #region 巡逻逻辑

    /// <summary>
    /// 设置新的巡逻目标点
    /// </summary>
    private void SetNewPatrolTarget()
    {
        // 在出生点周围随机生成巡逻点
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        currentPatrolTarget = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    #endregion

    #region 状态重置

    /// <summary>
    /// 重置敌人AI状态
    /// </summary>
    public void ResetEnemyAI()
    {
        currentState = AIState.Idle;
        target = null;
        isWaiting = false;
        patrolWaitTimer = 0f;
        attackCooldownTimer = 0f;
        verticalVelocity = 0f;

        // 重置位置到出生点
        transform.position = spawnPosition;

        // 生成新的巡逻点
        SetNewPatrolTarget();

        // 发布状态事件
        PublishStateEvent("ON_ENEMY_STATE_IDLE");

        DebugLog("[EnemyAI] 状态已重置");
    }

    /// <summary>
    /// 设置追击目标
    /// </summary>
    /// <param name="targetTransform">目标Transform</param>
    public void SetTarget(Transform targetTransform)
    {
        target = targetTransform;

        if (target != null && currentState == AIState.Idle)
        {
            ChangeState(AIState.Chase);
        }
    }

    #endregion

    #region 事件发布

    /// <summary>
    /// 发布状态事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    private void PublishStateEvent(string eventName)
    {
        EventBus.Publish(eventName);
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

    #region 编辑器辅助

    /// <summary>
    /// 在Scene视图中绘制感知范围
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 绘制视野范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制视野角度
        Gizmos.color = Color.cyan;
        Vector3 leftDir = Quaternion.Euler(0, -detectionAngle / 2, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, detectionAngle / 2, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, leftDir * detectionRange);
        Gizmos.DrawRay(transform.position, rightDir * detectionRange);

        // 绘制攻击范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 绘制巡逻范围
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, patrolRadius);
    }

    #endregion
}
