using UnityEngine;

/// <summary>
/// 第三人称跟随相机控制器（备用版本）
/// 使用旧版Input Manager（Input.GetAxis），适用于未配置Unity Input System的项目
/// 主相机逻辑请使用 FollowCamera（基于InputManager + EventBus）
/// 本脚本与FollowCamera功能冲突，不可同时挂载
/// WebGL平台兼容
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    #region 目标设置

    [Header("目标设置")]
    [Tooltip("相机跟随的目标（通常是玩家）")]
    [SerializeField] private Transform target;

    [Tooltip("目标身上的锚点（相机看向的位置，默认为头顶上方）")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("是否自动查找Tag为Player的目标")]
    [SerializeField] private bool autoFindTarget = true;

    #endregion

    #region 旋转参数

    [Header("旋转设置")]
    [Tooltip("水平旋转速度")]
    [SerializeField] private float horizontalRotateSpeed = 3f;

    [Tooltip("垂直旋转速度")]
    [SerializeField] private float verticalRotateSpeed = 3f;

    [Tooltip("垂直旋转最小角度（俯视限制）")]
    [SerializeField] private float minVerticalAngle = -20f;

    [Tooltip("垂直旋转最大角度（仰视限制）")]
    [SerializeField] private float maxVerticalAngle = 80f;

    [Tooltip("初始水平角度")]
    [SerializeField] private float initialHorizontalAngle = 0f;

    [Tooltip("初始垂直角度")]
    [SerializeField] private float initialVerticalAngle = 20f;

    [Tooltip("是否反转鼠标Y轴")]
    [SerializeField] private bool invertYAxis = false;

    #endregion

    #region 距离设置

    [Header("距离设置")]
    [Tooltip("相机与目标的最小距离（最近）")]
    [SerializeField] private float minDistance = 1f;

    [Tooltip("相机与目标的最大距离（最远）")]
    [SerializeField] private float maxDistance = 10f;

    [Tooltip("相机默认距离")]
    [SerializeField] private float defaultDistance = 4f;

    [Tooltip("滚轮缩放速度")]
    [SerializeField] private float scrollSpeed = 5f;

    [Tooltip("距离变化的平滑度（值越大越平滑）")]
    [SerializeField] private float distanceSmoothSpeed = 5f;

    #endregion

    #region 平滑参数

    [Header("平滑设置")]
    [Tooltip("跟随位置的平滑阻尼（值越大越平滑）")]
    [SerializeField] private float positionSmoothDamping = 0.1f;

    [Tooltip("旋转的平滑阻尼")]
    [SerializeField] private float rotationSmoothDamping = 0.1f;

    #endregion

    #region 碰撞设置

    [Header("碰撞检测")]
    [Tooltip("是否启用相机碰撞检测")]
    [SerializeField] private bool enableCollision = true;

    [Tooltip("碰撞检测的层")]
    [SerializeField] private LayerMask collisionLayers = ~0; // 默认所有层

    [Tooltip("碰撞检测球体半径")]
    [SerializeField] private float collisionRadius = 0.2f;

    [Tooltip("碰撞后相机与障碍物的安全距离")]
    [SerializeField] private float collisionPadding = 0.1f;

    [Tooltip("碰撞检测的射线长度偏移")]
    [SerializeField] private float collisionOffset = 0.1f;

    #endregion

    #region 角色联动设置

    [Header("角色联动")]
    [Tooltip("是否通过相机旋转驱动角色朝向")]
    [SerializeField] private bool drivePlayerRotation = true;

    [Tooltip("角色旋转跟随的平滑度")]
    [SerializeField] private float playerRotationSmoothSpeed = 10f;

    [Tooltip("是否在攻击时锁定相机旋转")]
    [SerializeField] private bool lockRotationOnAttack = false;

    #endregion

    #region 私有变量

    /// <summary>当前水平旋转角度</summary>
    private float currentHorizontalAngle;

    /// <summary>当前垂直旋转角度</summary>
    private float currentVerticalAngle;

    /// <summary>当前相机距离</summary>
    private float currentDistance;

    /// <summary>目标距离（用于平滑过渡）</summary>
    private float targetDistance;

    /// <summary>当前相机位置（用于平滑）</summary>
    private Vector3 smoothPosition;

    /// <summary>当前相机旋转（用于平滑）</summary>
    private Quaternion smoothRotation;

    /// <summary>PlayerController组件引用</summary>
    private PlayerController playerController;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>是否锁定旋转</summary>
    private bool isRotationLocked = false;

    #endregion

    #region 公共属性

    /// <summary>获取当前相机距离</summary>
    public float CurrentDistance => currentDistance;

    /// <summary>获取当前水平角度</summary>
    public float CurrentHorizontalAngle => currentHorizontalAngle;

    /// <summary>获取当前垂直角度</summary>
    public float CurrentVerticalAngle => currentVerticalAngle;

    /// <summary>是否锁定旋转</summary>
    public bool IsRotationLocked
    {
        get => isRotationLocked;
        set => isRotationLocked = value;
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// 在所有Update之后执行，确保角色更新后再处理相机
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized || target == null) return;

        // 处理输入
        HandleInput();

        // 计算相机位置和旋转
        UpdateCameraTransform();

        // 处理角色联动
        UpdatePlayerRotation();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化相机控制器
    /// </summary>
    private void Initialize()
    {
        // 自动查找目标
        if (target == null && autoFindTarget)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target == null)
        {
            Debug.LogError("[ThirdPersonCamera] 未设置跟随目标！");
            enabled = false;
            return;
        }

        // 获取PlayerController组件
        playerController = target.GetComponent<PlayerController>();

        // 初始化角度和距离
        currentHorizontalAngle = initialHorizontalAngle;
        currentVerticalAngle = initialVerticalAngle;
        currentDistance = defaultDistance;
        targetDistance = defaultDistance;

        // 初始化平滑变量
        smoothPosition = transform.position;
        smoothRotation = transform.rotation;

        isInitialized = true;
        Debug.Log("[ThirdPersonCamera] 第三人称相机初始化完成");
    }

    #endregion

    #region 输入处理

    /// <summary>
    /// 处理相机输入
    /// </summary>
    private void HandleInput()
    {
        // 如果旋转被锁定，跳过旋转输入
        if (!isRotationLocked)
        {
            HandleRotationInput();
        }

        HandleZoomInput();
    }

    /// <summary>
    /// 处理旋转输入（鼠标右键拖动）
    /// </summary>
    private void HandleRotationInput()
    {
        // 检测鼠标右键按下
        if (Input.GetMouseButton(1))
        {
            // 获取鼠标移动量
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // 水平旋转（左右）
            currentHorizontalAngle += mouseX * horizontalRotateSpeed;

            // 垂直旋转（上下），根据设置决定是否反转Y轴
            float invertFactor = invertYAxis ? 1f : -1f;
            currentVerticalAngle += mouseY * verticalRotateSpeed * invertFactor;

            // 限制垂直角度范围
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);

            // 水平角度循环（0-360度）
            if (currentHorizontalAngle > 360f) currentHorizontalAngle -= 360f;
            if (currentHorizontalAngle < 0f) currentHorizontalAngle += 360f;
        }
    }

    /// <summary>
    /// 处理缩放输入（鼠标滚轮）
    /// </summary>
    private void HandleZoomInput()
    {
        // 获取滚轮输入
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            // 调整目标距离
            targetDistance -= scrollInput * scrollSpeed;

            // 限制距离范围
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }

    #endregion

    #region 相机变换更新

    /// <summary>
    /// 更新相机的位置和旋转
    /// </summary>
    private void UpdateCameraTransform()
    {
        // 平滑过渡距离
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, distanceSmoothSpeed * Time.deltaTime);

        // 计算目标位置的锚点
        Vector3 anchorPosition = target.position + targetOffset;

        // 计算旋转四元数
        Quaternion rotation = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0f);

        // 计算相机相对于目标的偏移位置（未碰撞检测）
        Vector3 desiredPosition = anchorPosition + rotation * (Vector3.back * currentDistance);

        // 如果启用碰撞检测，处理相机碰撞
        if (enableCollision)
        {
            desiredPosition = HandleCameraCollision(anchorPosition, desiredPosition);
        }

        // 平滑插值位置
        smoothPosition = Vector3.Lerp(smoothPosition, desiredPosition, (1f / positionSmoothDamping) * Time.deltaTime);

        // 平滑插值旋转
        smoothRotation = Quaternion.Slerp(smoothRotation, rotation, (1f / rotationSmoothDamping) * Time.deltaTime);

        // 应用位置和旋转
        transform.position = smoothPosition;
        transform.rotation = smoothRotation;
    }

    #endregion

    #region 碰撞检测

    /// <summary>
    /// 处理相机碰撞，防止相机穿入墙壁
    /// </summary>
    /// <param name="anchorPosition">锚点位置</param>
    /// <param name="desiredPosition">期望位置</param>
    /// <returns>碰撞处理后的位置</returns>
    private Vector3 HandleCameraCollision(Vector3 anchorPosition, Vector3 desiredPosition)
    {
        // 计算从锚点到期望位置的方向
        Vector3 direction = desiredPosition - anchorPosition;
        float desiredDistance = direction.magnitude;

        // 如果距离太近，直接返回
        if (desiredDistance < 0.1f)
        {
            return desiredPosition;
        }

        // 归一化方向
        Vector3 normalizedDirection = direction / desiredDistance;

        // 从锚点发射射线检测障碍物
        if (Physics.SphereCast(
            anchorPosition,
            collisionRadius,
            normalizedDirection,
            out RaycastHit hit,
            desiredDistance + collisionOffset,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            // 计算碰撞后的安全距离
            float safeDistance = hit.distance - collisionPadding;

            // 确保距离不小于最小值
            safeDistance = Mathf.Max(safeDistance, minDistance * 0.5f);

            // 返回碰撞后的位置
            return anchorPosition + normalizedDirection * safeDistance;
        }

        return desiredPosition;
    }

    #endregion

    #region 角色联动

    /// <summary>
    /// 更新角色旋转（使角色面朝相机方向）
    /// </summary>
    private void UpdatePlayerRotation()
    {
        if (!drivePlayerRotation || target == null) return;

        // 计算角色目标旋转角度（水平方向）
        float playerTargetAngle = currentHorizontalAngle;

        // 获取角色当前旋转
        Vector3 currentEulerAngles = target.rotation.eulerAngles;

        // 平滑旋转角色
        float smoothAngle = Mathf.LerpAngle(
            currentEulerAngles.y,
            playerTargetAngle,
            playerRotationSmoothSpeed * Time.deltaTime
        );

        // 应用旋转（只改变Y轴）
        target.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 设置相机跟随目标
    /// </summary>
    /// <param name="newTarget">新目标</param>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        // 重新获取PlayerController
        if (target != null)
        {
            playerController = target.GetComponent<PlayerController>();
        }

        Debug.Log($"[ThirdPersonCamera] 相机目标设置为: {target?.name}");
    }

    /// <summary>
    /// 重置相机到默认状态
    /// </summary>
    public void ResetCamera()
    {
        currentHorizontalAngle = initialHorizontalAngle;
        currentVerticalAngle = initialVerticalAngle;
        targetDistance = defaultDistance;
        currentDistance = defaultDistance;

        Debug.Log("[ThirdPersonCamera] 相机已重置");
    }

    /// <summary>
    /// 瞬间移动相机到目标位置（不使用平滑）
    /// </summary>
    /// <param name="position">目标位置</param>
    /// <param name="rotation">目标旋转</param>
    public void SnapToPosition(Vector3 position, Quaternion rotation)
    {
        smoothPosition = position;
        smoothRotation = rotation;
        transform.position = position;
        transform.rotation = rotation;
    }

    /// <summary>
    /// 平滑切换到新的距离
    /// </summary>
    /// <param name="newDistance">新距离</param>
    public void SetDistance(float newDistance)
    {
        targetDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }

    /// <summary>
    /// 平滑切换到新的垂直角度
    /// </summary>
    /// <param name="angle">新垂直角度</param>
    public void SetVerticalAngle(float angle)
    {
        currentVerticalAngle = Mathf.Clamp(angle, minVerticalAngle, maxVerticalAngle);
    }

    /// <summary>
    /// 平滑切换到新的水平角度
    /// </summary>
    /// <param name="angle">新水平角度</param>
    public void SetHorizontalAngle(float angle)
    {
        currentHorizontalAngle = angle;
    }

    #endregion

    #region 事件监听（可选）

    /// <summary>
    /// 订阅游戏事件
    /// </summary>
    private void OnEnable()
    {
        // 可以在这里订阅游戏事件
        // 例如：游戏开始时重置相机
        // EventBus.Subscribe("OnGameStart", ResetCamera);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    private void OnDisable()
    {
        // 取消订阅
        // EventBus.Unsubscribe("OnGameStart", ResetCamera);
    }

    #endregion

    #region 编辑器辅助

    /// <summary>
    /// 在Scene视图中绘制辅助线
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        // 绘制目标锚点
        Vector3 anchorPosition = target.position + targetOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(anchorPosition, 0.3f);

        // 绘制相机范围
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchorPosition, minDistance);
        Gizmos.DrawWireSphere(anchorPosition, maxDistance);

        // 绘制当前相机位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
    }

    #endregion
}
