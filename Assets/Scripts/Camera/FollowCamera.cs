using UnityEngine;

/// <summary>
/// 第三人称跟随相机控制器
/// 挂载在主相机GameObject，跟随Player目标
/// 读取InputManager的视角增量控制相机旋转
/// 水平旋转跟随角色，俯仰限制在最小/最大角度之间
/// 平滑跟随逻辑，支持距离偏移、高度偏移
/// 碰撞检测：遇到障碍物自动拉近相机，避免穿墙
/// WebGL平台兼容
/// </summary>
public class FollowCamera : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("跟随目标")]
    [Tooltip("跟随的目标Transform（通常是Player）")]
    [SerializeField] private Transform target;

    [Tooltip("相机看向的目标偏移点（相对于目标位置）")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("相机位置设置")]
    [Tooltip("相机与目标的默认距离")]
    [SerializeField] private float cameraDistance = 5f;

    [Tooltip("相机的高度偏移")]
    [SerializeField] private float cameraHeight = 2f;

    [Tooltip("相机最小距离（碰撞检测用）")]
    [SerializeField] private float minDistance = 0.5f;

    [Header("旋转设置")]
    [Tooltip("俯仰角最小值（负数为向下看）")]
    [SerializeField] private float minPitch = -30f;

    [Tooltip("俯仰角最大值（正数为向上看）")]
    [SerializeField] private float maxPitch = 60f;

    [Tooltip("水平旋转灵敏度")]
    [SerializeField] private float yawSensitivity = 3f;

    [Tooltip("俯仰旋转灵敏度")]
    [SerializeField] private float pitchSensitivity = 3f;

    [Header("平滑设置")]
    [Tooltip("旋转平滑速度（越大越平滑）")]
    [SerializeField] private float rotationSmooth = 10f;

    [Tooltip("位置平滑速度（越大越平滑）")]
    [SerializeField] private float positionSmooth = 10f;

    [Header("碰撞检测")]
    [Tooltip("碰撞检测Layer（相机不能穿过的层）")]
    [SerializeField] private LayerMask collisionLayerMask = ~0;

    [Tooltip("碰撞检测球体半径")]
    [SerializeField] private float collisionSphereRadius = 0.2f;

    [Tooltip("碰撞后相机与障碍物的安全距离")]
    [SerializeField] private float collisionPadding = 0.3f;

    [Header("组件引用（自动获取）")]
    [Tooltip("Camera组件")]
    [SerializeField] private Camera cameraComponent;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>当前水平旋转角（Yaw）</summary>
    private float currentYaw = 0f;

    /// <summary>当前俯仰角（Pitch）</summary>
    private float currentPitch = 20f;

    /// <summary>目标位置（用于平滑插值）</summary>
    private Vector3 smoothPosition = Vector3.zero;

    /// <summary>当前相机距离（碰撞检测用）</summary>
    private float currentDistance = 0f;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>游戏是否暂停</summary>
    private bool isPaused = false;

    #endregion

    #region 公共属性

    /// <summary>当前俯仰角</summary>
    public float CurrentPitch => currentPitch;

    /// <summary>当前水平旋转角</summary>
    public float CurrentYaw => currentYaw;

    /// <summary>当前相机距离</summary>
    public float CurrentDistance => currentDistance;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeFollowCamera();
    }

    /// <summary>
    /// 每帧更新 - 处理旋转和跟随
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized) return;

        // 暂停时冻结相机
        if (isPaused) return;

        UpdateRotation();
        UpdatePosition();
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
    /// 初始化跟随相机
    /// </summary>
    private void InitializeFollowCamera()
    {
        if (isInitialized) return;

        // 获取Camera组件
        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        // 如果没有指定目标，尝试自动查找Player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        // 验证目标
        if (target == null)
        {
            Debug.LogError("[FollowCamera] 未找到跟随目标！");
            enabled = false;
            return;
        }

        // 初始化旋转角度（基于目标当前朝向）
        Vector3 targetEuler = target.eulerAngles;
        currentYaw = targetEuler.y;
        currentPitch = 20f; // 默认初始俯仰角

        // 初始化距离
        currentDistance = cameraDistance;

        // 初始化平滑位置
        smoothPosition = CalculateDesiredPosition();

        // 订阅EventBus事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[FollowCamera] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Subscribe("ON_GAME_RESUME", OnGameResume);
    }

    /// <summary>
    /// 取消所有EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Unsubscribe("ON_GAME_RESUME", OnGameResume);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 游戏暂停事件处理
    /// </summary>
    private void OnGamePause()
    {
        isPaused = true;
        DebugLog("[FollowCamera] 游戏暂停，相机冻结");
    }

    /// <summary>
    /// 游戏恢复事件处理
    /// </summary>
    private void OnGameResume()
    {
        isPaused = false;
        DebugLog("[FollowCamera] 游戏恢复，相机解冻");
    }

    #endregion

    #region 旋转逻辑

    /// <summary>
    /// 更新相机旋转
    /// </summary>
    private void UpdateRotation()
    {
        if (!InputManager.HasInstance) return;

        // 获取视角增量
        Vector2 lookDelta = InputManager.Instance.LookDelta;

        // 更新水平旋转（Yaw）
        currentYaw += lookDelta.x * yawSensitivity;

        // 归一化角度到0-360
        if (currentYaw > 360f) currentYaw -= 360f;
        if (currentYaw < 0f) currentYaw += 360f;

        // 更新俯仰角（Pitch）
        currentPitch -= lookDelta.y * pitchSensitivity;

        // 限制俯仰角范围，防止镜头翻转
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    #endregion

    #region 位置逻辑

    /// <summary>
    /// 更新相机位置
    /// </summary>
    private void UpdatePosition()
    {
        // 计算目标位置
        Vector3 targetPosition = target.position + targetOffset;

        // 计算期望的相机位置
        Vector3 desiredPosition = CalculateDesiredPosition();

        // 碰撞检测，自动拉近相机
        desiredPosition = CheckCollision(targetPosition, desiredPosition);

        // 平滑插值位置
        smoothPosition = Vector3.Lerp(smoothPosition, desiredPosition, positionSmooth * Time.deltaTime);

        // 应用位置和旋转
        transform.position = smoothPosition;

        // 计算并应用旋转（看向目标）
        Quaternion desiredRotation = Quaternion.LookRotation(targetPosition - smoothPosition);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmooth * Time.deltaTime);
    }

    /// <summary>
    /// 计算期望的相机位置
    /// </summary>
    /// <returns>期望的相机位置</returns>
    private Vector3 CalculateDesiredPosition()
    {
        // 计算旋转四元数
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        // 计算偏移向量
        Vector3 offset = rotation * new Vector3(0f, cameraHeight, -currentDistance);

        // 返回期望位置
        return target.position + targetOffset + offset;
    }

    #endregion

    #region 碰撞检测

    /// <summary>
    /// 碰撞检测 - 自动拉近相机避免穿墙
    /// </summary>
    /// <param name="targetPosition">目标位置（相机看向的点）</param>
    /// <param name="desiredPosition">期望的相机位置</param>
    /// <returns>碰撞检测后的相机位置</returns>
    private Vector3 CheckCollision(Vector3 targetPosition, Vector3 desiredPosition)
    {
        // 计算从目标到期望位置的方向
        Vector3 direction = desiredPosition - targetPosition;
        float desiredDistance = direction.magnitude;

        if (desiredDistance < 0.01f) return desiredPosition;

        // 射线检测
        if (Physics.SphereCast(
            targetPosition,
            collisionSphereRadius,
            direction.normalized,
            out RaycastHit hit,
            desiredDistance,
            collisionLayerMask,
            QueryTriggerInteraction.Ignore))
        {
            // 计算碰撞后的安全距离
            float safeDistance = hit.distance - collisionPadding;

            // 确保距离不小于最小值
            safeDistance = Mathf.Max(safeDistance, minDistance);

            // 返回碰撞后的位置
            return targetPosition + direction.normalized * safeDistance;
        }

        return desiredPosition;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 设置跟随目标
    /// </summary>
    /// <param name="newTarget">新的跟随目标</param>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            // 重置旋转角度
            currentYaw = target.eulerAngles.y;
        }
    }

    /// <summary>
    /// 设置相机距离
    /// </summary>
    /// <param name="distance">新的相机距离</param>
    public void SetCameraDistance(float distance)
    {
        cameraDistance = Mathf.Max(minDistance, distance);
    }

    /// <summary>
    /// 设置相机高度
    /// </summary>
    /// <param name="height">新的相机高度</param>
    public void SetCameraHeight(float height)
    {
        cameraHeight = height;
    }

    /// <summary>
    /// 立即移动相机到目标位置（不使用平滑）
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + targetOffset;
        Vector3 desiredPosition = CalculateDesiredPosition();

        smoothPosition = desiredPosition;
        transform.position = desiredPosition;
        transform.LookAt(targetPosition);
    }

    /// <summary>
    /// 重置相机到默认状态
    /// </summary>
    public void ResetCamera()
    {
        currentYaw = target != null ? target.eulerAngles.y : 0f;
        currentPitch = 20f;
        currentDistance = cameraDistance;
        smoothPosition = CalculateDesiredPosition();
    }

    #endregion

    #region 编辑器辅助

    /// <summary>
    /// 在Scene视图中绘制辅助线
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        // 绘制目标位置
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position + targetOffset, 0.3f);

        // 绘制相机期望位置
        if (isInitialized)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(smoothPosition, 0.2f);

            // 绘制连线
            Gizmos.color = Color.white;
            Gizmos.DrawLine(target.position + targetOffset, smoothPosition);
        }
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
