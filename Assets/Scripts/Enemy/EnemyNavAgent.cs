using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人NavMesh导航组件 - 封装NavMeshAgent
/// 提供NavMesh寻路能力，替代原有简易距离追踪
/// 支持巡逻点、追击、攻击、返回巡逻点等行为
/// 自动处理NavMeshAgent的启用/禁用
/// WebGL平台兼容
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyNavAgent : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("NavMesh设置")]
    [Tooltip("NavMeshAgent组件（自动获取）")]
    [SerializeField] private NavMeshAgent navMeshAgent;

    [Tooltip("是否启用NavMesh寻路（关闭则使用原有简易移动）")]
    [SerializeField] private bool enableNavMesh = true;

    [Tooltip("到达目标的停止距离")]
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("移动设置")]
    [Tooltip("巡逻移动速度")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 4f;

    [Tooltip("攻击移动速度")]
    [SerializeField] private float attackSpeed = 3f;

    [Header("旋转设置")]
    [Tooltip("旋转速度")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>当前目标位置</summary>
    private Vector3 currentDestination;

    /// <summary>是否正在寻路</summary>
    private bool isNavigating = false;

    #endregion

    #region 公共属性

    /// <summary>是否启用NavMesh</summary>
    public bool EnableNavMesh
    {
        get => enableNavMesh;
        set
        {
            enableNavMesh = value;
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = value;
            }
        }
    }

    /// <summary>是否正在寻路</summary>
    public bool IsNavigating => isNavigating;

    /// <summary>是否到达目标</summary>
    public bool HasReachedDestination => !navMeshAgent.pathPending && 
        navMeshAgent.remainingDistance <= stoppingDistance;

    /// <summary>当前速度</summary>
    public float CurrentSpeed => navMeshAgent != null ? navMeshAgent.velocity.magnitude : 0f;

    /// <summary>NavMeshAgent组件</summary>
    public NavMeshAgent Agent => navMeshAgent;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Awake()
    {
        InitializeNavAgent();
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        // 禁用NavMeshAgent
        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化NavMesh导航组件
    /// </summary>
    private void InitializeNavAgent()
    {
        if (isInitialized) return;

        // 获取NavMeshAgent组件
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        // 验证NavMeshAgent
        if (navMeshAgent == null)
        {
            Debug.LogError("[EnemyNavAgent] 未找到NavMeshAgent组件！");
            enabled = false;
            return;
        }

        // 配置NavMeshAgent参数
        navMeshAgent.stoppingDistance = stoppingDistance;
        navMeshAgent.speed = patrolSpeed;
        navMeshAgent.angularSpeed = rotationSpeed * 100f;
        navMeshAgent.acceleration = 8f;

        // 启用/禁用NavMesh
        navMeshAgent.enabled = enableNavMesh;

        isInitialized = true;
        DebugLog("[EnemyNavAgent] 初始化完成");
    }

    #endregion

    #region 移动控制

    /// <summary>
    /// 移动到指定位置（使用NavMesh寻路）
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="speed">移动速度</param>
    public void MoveTo(Vector3 targetPosition, float speed = -1f)
    {
        if (!isInitialized || !enableNavMesh) return;

        // 设置速度
        if (speed > 0)
        {
            navMeshAgent.speed = speed;
        }

        // 设置目标位置
        navMeshAgent.SetDestination(targetPosition);
        currentDestination = targetPosition;
        isNavigating = true;

        DebugLog($"[EnemyNavAgent] 移动到: {targetPosition}, 速度: {navMeshAgent.speed}");
    }

    /// <summary>
    /// 停止移动
    /// </summary>
    public void Stop()
    {
        if (!isInitialized || !enableNavMesh) return;

        navMeshAgent.ResetPath();
        isNavigating = false;

        DebugLog("[EnemyNavAgent] 停止移动");
    }

    /// <summary>
    /// 设置巡逻速度
    /// </summary>
    public void SetPatrolSpeed()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = patrolSpeed;
        }
    }

    /// <summary>
    /// 设置追击速度
    /// </summary>
    public void SetChaseSpeed()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = chaseSpeed;
        }
    }

    /// <summary>
    /// 设置攻击速度
    /// </summary>
    public void SetAttackSpeed()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = attackSpeed;
        }
    }

    /// <summary>
    /// 设置停止距离
    /// </summary>
    /// <param name="distance">新的停止距离</param>
    public void SetStoppingDistance(float distance)
    {
        stoppingDistance = distance;
        if (navMeshAgent != null)
        {
            navMeshAgent.stoppingDistance = distance;
        }
    }

    /// <summary>
    /// 检查是否可以到达指定位置
    /// </summary>
    /// <param name="position">目标位置</param>
    /// <returns>是否可达</returns>
    public bool IsReachable(Vector3 position)
    {
        if (!enableNavMesh) return true;

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, position, NavMesh.AllAreas, path))
        {
            return path.status == NavMeshPathStatus.PathComplete;
        }
        return false;
    }

    /// <summary>
    /// 获取到目标的距离
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <returns>距离</returns>
    public float GetDistanceTo(Vector3 targetPosition)
    {
        if (!enableNavMesh)
        {
            return Vector3.Distance(transform.position, targetPosition);
        }

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, targetPosition, NavMesh.AllAreas, path))
        {
            float distance = 0f;
            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                distance += Vector3.Distance(corners[i], corners[i + 1]);
            }
            return distance;
        }

        return Vector3.Distance(transform.position, targetPosition);
    }

    /// <summary>
    /// 面向目标位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    public void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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

    #endregion
}
