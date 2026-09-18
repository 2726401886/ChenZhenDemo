using UnityEngine;

/// <summary>
/// 远程弓箭手敌人AI - 有限状态机驱动远程敌人行为
/// 继承EnemyAI基础逻辑，增加远程攻击行为
/// 状态：Idle（巡逻）→ Chase（保持距离）→ Attack（射箭）→ Retreat（后撤）→ Dead（死亡）
/// 使用NavMeshAgent寻路，支持保持距离、射箭、后撤行为
/// 新增事件：ON_ARROW_FIRE（射箭事件）
/// WebGL平台兼容
/// </summary>
public class RangeEnemyAI : EnemyAI
{
    #region Inspector可配置参数

    [Header("远程攻击设置")]
    [Tooltip("射箭冷却时间（秒）")]
    [SerializeField] private float arrowCooldown = 2f;

    [Tooltip("箭矢伤害值")]
    [SerializeField] private float arrowDamage = 15f;

    [Tooltip("箭矢飞行速度")]
    [SerializeField] private float arrowSpeed = 15f;

    [Tooltip("箭矢发射点偏移")]
    [SerializeField] private Vector3 arrowSpawnOffset = new Vector3(0f, 1.5f, 0.5f);

    [Header("距离控制")]
    [Tooltip("理想攻击距离")]
    [SerializeField] private float idealAttackDistance = 8f;

    [Tooltip("最小攻击距离（贴脸会后撤）")]
    [SerializeField] private float minAttackDistance = 3f;

    [Tooltip("最大追击距离")]
    [SerializeField] private float maxChaseDistance = 20f;

    [Header("后撤设置")]
    [Tooltip("后撤移动速度")]
    [SerializeField] private float retreatSpeed = 3f;

    [Tooltip("后撤持续时间（秒）")]
    [SerializeField] private float retreatDuration = 1.5f;

    [Tooltip("后撤方向（相对于玩家的方向）")]
    [SerializeField] private float retreatDirectionAngle = 180f;

    [Header("组件引用")]
    [Tooltip("EnemyNavAgent组件（自动获取）")]
    [SerializeField] private EnemyNavAgent navAgent;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLogRange = false;

    #endregion

    #region 私有变量

    /// <summary>射箭冷却计时器</summary>
    private float arrowCooldownTimer = 0f;

    /// <summary>后撤计时器</summary>
    private float retreatTimer = 0f;

    /// <summary>是否正在后撤</summary>
    private bool isRetreating = false;

    /// <summary>后撤目标位置</summary>
    private Vector3 retreatTarget;

    /// <summary>远程AI状态枚举</summary>
    public new enum AIState
    {
        Idle,       // 巡逻状态
        Chase,      // 保持距离追击
        Attack,     // 射箭攻击
        Retreat,    // 后撤状态
        Dead        // 死亡状态
    }

    /// <summary>当前远程AI状态</summary>
    private AIState currentRangeState = AIState.Idle;

    /// <summary>Phase10: 帧节流计数器</summary>
    private int frameThrottleCounter = 0;

    /// <summary>Phase10: 检测频率（每N帧执行一次）</summary>
    private const int DETECTION_FRAME_INTERVAL = 3;

    #endregion

    #region 公共属性

    /// <summary>当前远程AI状态</summary>
    public AIState CurrentRangeState => currentRangeState;

    /// <summary>是否正在后撤</summary>
    public bool IsRetreating => isRetreating;

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化远程敌人AI
    /// </summary>
    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();

        // 获取NavMeshAgent组件
        if (navAgent == null)
        {
            navAgent = GetComponent<EnemyNavAgent>();
        }

        // 如果没有NavMeshAgent，添加一个
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<EnemyNavAgent>();
        }

        DebugLog("[RangeEnemyAI] 远程敌人AI初始化完成");
    }

    #endregion

    #region 状态更新（重写父类）

    /// <summary>
    /// 更新远程敌人AI状态机
    /// </summary>
    protected new void Update()
    {
        if (CurrentState == EnemyAI.AIState.Dead) return;

        // Phase10优化：帧节流
        frameThrottleCounter++;
        if (frameThrottleCounter % DETECTION_FRAME_INTERVAL != 0)
        {
            UpdateArrowCooldown();
            return;
        }

        UpdateArrowCooldown();

        // 更新后撤状态
        if (isRetreating)
        {
            UpdateRetreatState();
            return;
        }

        // 远程AI状态机执行
        switch (currentRangeState)
        {
            case AIState.Idle:
                UpdateRangeIdleState();
                break;
            case AIState.Chase:
                UpdateRangeChaseState();
                break;
            case AIState.Attack:
                UpdateRangeAttackState();
                break;
            case AIState.Retreat:
                UpdateRetreatState();
                break;
        }
    }

    #endregion

    #region 远程AI状态更新

    /// <summary>
    /// 巡逻状态更新（远程版本）
    /// </summary>
    private void UpdateRangeIdleState()
    {
        // 检测玩家
        if (DetectPlayerInRange())
        {
            // 发现玩家，切换到保持距离追击
            ChangeRangeState(AIState.Chase);
            return;
        }

        // 巡逻逻辑（使用NavMesh）
        if (navAgent != null && navAgent.EnableNavMesh)
        {
            // NavMesh巡逻
            UpdatePatrolWithNavMesh();
        }
        else
        {
            // 简易巡逻（备用）
            UpdateSimplePatrol();
        }
    }

    /// <summary>
    /// 保持距离追击状态更新
    /// </summary>
    private void UpdateRangeChaseState()
    {
        // 检测玩家是否还在视野内
        if (!DetectPlayerInRange())
        {
            // 丢失目标，回到巡逻
            ChangeRangeState(AIState.Idle);
            return;
        }

        // 获取目标距离
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 超出最大追击距离，回到巡逻
        if (distanceToPlayer > maxChaseDistance)
        {
            ChangeRangeState(AIState.Idle);
            return;
        }

        // 贴脸了，后撤
        if (distanceToPlayer < minAttackDistance)
        {
            StartRetreat();
            return;
        }

        // 进入理想攻击距离，射箭
        if (distanceToPlayer <= idealAttackDistance && arrowCooldownTimer <= 0)
        {
            ChangeRangeState(AIState.Attack);
            return;
        }

        // 移动到理想距离
        if (navAgent != null && navAgent.EnableNavMesh)
        {
            // 计算理想位置（保持距离）
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            Vector3 idealPosition = player.position - directionToPlayer * idealAttackDistance;

            // 面向玩家
            navAgent.FaceTarget(player.position);

            // 移动到理想位置
            navAgent.MoveTo(idealPosition, navAgent.Agent.speed);
        }
        else
        {
            // 简易移动
            MoveToPositionSimple(player.position, retreatSpeed);
        }
    }

    /// <summary>
    /// 射箭攻击状态更新
    /// </summary>
    private void UpdateRangeAttackState()
    {
        // 获取目标
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            ChangeRangeState(AIState.Idle);
            return;
        }

        // 面向玩家
        if (navAgent != null)
        {
            navAgent.FaceTarget(player.position);
        }
        else
        {
            FaceTarget(player.position);
        }

        // 射箭
        if (arrowCooldownTimer <= 0)
        {
            FireArrow();
        }

        // 射箭后切换到保持距离状态
        ChangeRangeState(AIState.Chase);
    }

    /// <summary>
    /// 后撤状态更新
    /// </summary>
    private void UpdateRetreatState()
    {
        retreatTimer -= Time.deltaTime;

        if (retreatTimer <= 0)
        {
            // 后撤结束
            isRetreating = false;
            currentRangeState = AIState.Chase;
            return;
        }

        // 移动到后撤目标
        if (navAgent != null && navAgent.EnableNavMesh)
        {
            navAgent.MoveTo(retreatTarget, retreatSpeed);
        }
        else
        {
            MoveToPositionSimple(retreatTarget, retreatSpeed);
        }
    }

    #endregion

    #region 远程攻击逻辑

    /// <summary>
    /// 射箭
    /// </summary>
    private void FireArrow()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) return;

        // 计算发射位置
        Vector3 spawnPosition = transform.position + transform.TransformDirection(arrowSpawnOffset);

        // 计算发射方向（指向玩家）
        Vector3 direction = (player.position + Vector3.up * 1f - spawnPosition).normalized;

        // 从对象池获取箭矢
        if (ProjectilePool.HasInstance)
        {
            GameObject arrowObj = ProjectilePool.Instance.GetArrow(spawnPosition, Quaternion.LookRotation(direction));
            if (arrowObj != null)
            {
                Arrow arrow = arrowObj.GetComponent<Arrow>();
                if (arrow != null)
                {
                    arrow.Fire(direction, arrowSpeed, arrowDamage);
                }
            }
        }

        // 启动射箭冷却
        arrowCooldownTimer = arrowCooldown;

        DebugLog($"[RangeEnemyAI] 射箭，伤害: {arrowDamage}");
    }

    /// <summary>
    /// 更新射箭冷却
    /// </summary>
    private void UpdateArrowCooldown()
    {
        if (arrowCooldownTimer > 0)
        {
            arrowCooldownTimer -= Time.deltaTime;
        }
    }

    #endregion

    #region 后撤逻辑

    /// <summary>
    /// 开始后撤
    /// </summary>
    private void StartRetreat()
    {
        isRetreating = true;
        retreatTimer = retreatDuration;

        // 计算后撤目标位置
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            Vector3 directionFromPlayer = (transform.position - player.position).normalized;
            retreatTarget = transform.position + directionFromPlayer * 5f;

            // 确保后撤位置在地面上
            RaycastHit hit;
            if (Physics.Raycast(retreatTarget + Vector3.up * 10f, Vector3.down, out hit, 20f))
            {
                retreatTarget = hit.point;
            }
        }
        else
        {
            // 没有玩家，向后撤退
            retreatTarget = transform.position - transform.forward * 5f;
        }

        currentRangeState = AIState.Retreat;

        DebugLog("[RangeEnemyAI] 开始后撤");
    }

    #endregion

    #region 感知机制（远程版本）

    /// <summary>
    /// 检测玩家（远程版本，角度更大）
    /// </summary>
    /// <returns>是否检测到玩家</returns>
    private bool DetectPlayerInRange()
    {
        // Phase10优化：使用父类缓存的玩家引用
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
        float sqrMaxChase = maxChaseDistance * maxChaseDistance;

        if (sqrDistance > sqrMaxChase)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > 150f / 2f)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region 状态切换

    /// <summary>
    /// 切换远程AI状态
    /// </summary>
    /// <param name="newState">新状态</param>
    private void ChangeRangeState(AIState newState)
    {
        if (currentRangeState == newState) return;

        AIState oldState = currentRangeState;
        currentRangeState = newState;

        DebugLog($"[RangeEnemyAI] 状态切换: {oldState} → {newState}");
    }

    #endregion

    #region 移动逻辑（远程版本）

    /// <summary>
    /// 使用NavMesh巡逻
    /// </summary>
    private void UpdatePatrolWithNavMesh()
    {
        // 简化巡逻逻辑：随机移动
        if (navAgent.HasReachedDestination || !navAgent.IsNavigating)
        {
            // 生成新的巡逻目标
            Vector3 randomPoint = GetRandomPatrolPoint();
            navAgent.MoveTo(randomPoint, navAgent.Agent.speed);
        }
    }

    /// <summary>
    /// 简易巡逻（备用）
    /// </summary>
    private void UpdateSimplePatrol()
    {
        // 简化处理：随机移动
        if (!navAgent.IsNavigating)
        {
            Vector3 randomPoint = GetRandomPatrolPoint();
            navAgent.MoveTo(randomPoint, 2f);
        }
    }

    /// <summary>
    /// 获取随机巡逻点
    /// </summary>
    /// <returns>随机巡逻点</returns>
    private Vector3 GetRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 8f;
        randomDirection += transform.position;

        RaycastHit hit;
        if (Physics.Raycast(randomDirection + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            return hit.point;
        }

        return randomDirection;
    }

    /// <summary>
    /// 简易移动到位置（备用）
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="speed">移动速度</param>
    private void MoveToPositionSimple(Vector3 targetPosition, float speed)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.magnitude > 0.1f)
        {
            transform.forward = direction.normalized;
            Vector3 moveVector = direction.normalized * speed * Time.deltaTime;
            cc.Move(moveVector);
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
        if (enableDebugLogRange)
        {
            Debug.Log(message);
        }
    }

    #endregion
}
