using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入管理器
/// 单例模式，统一管理所有玩家输入
/// 基于Unity Input System，支持键盘/鼠标/手柄
/// 通过EventBus分发输入事件，实现输入与业务逻辑解耦
/// WebGL平台兼容
/// </summary>
public class InputManager : Singleton<InputManager>
{
    #region 事件定义

    /// <summary>
    /// 输入事件常量
    /// </summary>
    public static class InputEvents
    {
        /// <summary>攻击输入</summary>
        public const string ON_ATTACK = "Input_Attack";
        /// <summary>攻击释放</summary>
        public const string ON_ATTACK_RELEASE = "Input_AttackRelease";
        /// <summary>暂停输入</summary>
        public const string ON_PAUSE = "Input_Pause";
        /// <summary>跳跃输入</summary>
        public const string ON_JUMP = "Input_Jump";
        /// <summary>交互输入</summary>
        public const string ON_INTERACT = "Input_Interact";
    }

    #endregion

    #region Inspector设置

    [Header("Input Actions")]
    [Tooltip("移动输入动作")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("视角输入动作")]
    [SerializeField] private InputActionReference lookAction;

    [Tooltip("攻击输入动作")]
    [SerializeField] private InputActionReference attackAction;

    [Tooltip("暂停输入动作")]
    [SerializeField] private InputActionReference pauseAction;

    [Tooltip("跳跃输入动作")]
    [SerializeField] private InputActionReference jumpAction;

    [Tooltip("交互输入动作")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Settings")]
    [Tooltip("鼠标视角灵敏度")]
    [SerializeField] private float mouseSensitivity = 0.2f;

    [Tooltip("手柄视角灵敏度")]
    [SerializeField] private float gamepadSensitivity = 1f;

    [Tooltip("视角Y轴是否反转")]
    [SerializeField] private bool invertYAxis = false;

    [Tooltip("移动死区（手柄）")]
    [SerializeField] private float moveDeadzone = 0.2f;

    [Tooltip("视角死区（手柄）")]
    [SerializeField] private float lookDeadzone = 0.1f;

    #endregion

    #region 私有变量

    /// <summary>当前移动输入向量</summary>
    private Vector2 currentMoveInput = Vector2.zero;

    /// <summary>当前视角增量</summary>
    private Vector2 currentLookDelta = Vector2.zero;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>输入是否启用</summary>
    private bool inputEnabled = true;

    /// <summary>是否暂停中</summary>
    private bool isPaused = false;

    /// <summary>上一帧攻击状态</summary>
    private bool lastAttackState = false;

    /// <summary>当前是否使用手柄</summary>
    private bool isUsingGamepad = false;

    #endregion

    #region 公共属性

    /// <summary>当前移动输入向量（只读）</summary>
    public Vector2 MoveInput => inputEnabled ? currentMoveInput : Vector2.zero;

    /// <summary>当前视角增量（只读）</summary>
    public Vector2 LookDelta => inputEnabled ? currentLookDelta : Vector2.zero;

    /// <summary>输入是否启用</summary>
    public bool InputEnabled => inputEnabled;

    /// <summary>是否使用手柄</summary>
    public bool IsUsingGamepad => isUsingGamepad;

    /// <summary>移动输入是否有效</summary>
    public bool HasMoveInput => currentMoveInput.sqrMagnitude > 0.01f;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    protected override void OnSingletonAwake()
    {
        InitializeInputManager();
    }

    /// <summary>
    /// 启用时开启输入
    /// </summary>
    private void OnEnable()
    {
        EnableInputActions();
    }

    /// <summary>
    /// 禁用时关闭输入
    /// </summary>
    protected override void OnDisable()
    {
        DisableInputActions();
        base.OnDisable();
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        UpdateInputState();
        DetectInputDevice();
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    protected override void OnDestroy()
    {
        UnsubscribeEvents();
        base.OnDestroy();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化输入管理器
    /// </summary>
    private void InitializeInputManager()
    {
        if (isInitialized) return;

        // 启用输入动作
        EnableInputActions();

        // 注册输入回调
        RegisterInputCallbacks();

        // 订阅游戏事件
        SubscribeEvents();

        isInitialized = true;
        Debug.Log("[InputManager] 初始化完成");
    }

    /// <summary>
    /// 启用所有输入动作
    /// </summary>
    private void EnableInputActions()
    {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Enable();

        if (lookAction != null && lookAction.action != null)
            lookAction.action.Enable();

        if (attackAction != null && attackAction.action != null)
            attackAction.action.Enable();

        if (pauseAction != null && pauseAction.action != null)
            pauseAction.action.Enable();

        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Enable();

        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();
    }

    /// <summary>
    /// 禁用所有输入动作
    /// </summary>
    private void DisableInputActions()
    {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Disable();

        if (lookAction != null && lookAction.action != null)
            lookAction.action.Disable();

        if (attackAction != null && attackAction.action != null)
            attackAction.action.Disable();

        if (pauseAction != null && pauseAction.action != null)
            pauseAction.action.Disable();

        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Disable();

        if (interactAction != null && interactAction.action != null)
            interactAction.action.Disable();
    }

    /// <summary>
    /// 注册输入回调
    /// </summary>
    private void RegisterInputCallbacks()
    {
        // 攻击按下
        if (attackAction != null && attackAction.action != null)
        {
            attackAction.action.started += OnAttackStarted;
            attackAction.action.canceled += OnAttackCanceled;
        }

        // 暂停
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed += OnPausePerformed;
        }

        // 跳跃
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.performed += OnJumpPerformed;
        }

        // 交互
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    /// <summary>
    /// 注销输入回调
    /// </summary>
    private void UnregisterInputCallbacks()
    {
        if (attackAction != null && attackAction.action != null)
        {
            attackAction.action.started -= OnAttackStarted;
            attackAction.action.canceled -= OnAttackCanceled;
        }

        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed -= OnPausePerformed;
        }

        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
        }

        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
        }
    }

    #endregion

    #region 事件订阅

    /// <summary>
    /// 订阅游戏事件
    /// </summary>
    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_GAME_PAUSE", OnGamePaused);
        EventBus.Subscribe("ON_GAME_RESUME", OnGameResumed);
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    /// <summary>
    /// 取消订阅游戏事件
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_GAME_PAUSE", OnGamePaused);
        EventBus.Unsubscribe("ON_GAME_RESUME", OnGameResumed);
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 游戏暂停处理
    /// </summary>
    private void OnGamePaused()
    {
        isPaused = true;
        currentMoveInput = Vector2.zero;
        currentLookDelta = Vector2.zero;
    }

    /// <summary>
    /// 游戏恢复处理
    /// </summary>
    private void OnGameResumed()
    {
        isPaused = false;
    }

    /// <summary>
    /// 玩家死亡处理
    /// </summary>
    private void OnPlayerDie()
    {
        SetInputEnabled(false);
    }

    #endregion

    #region 输入回调

    /// <summary>
    /// 攻击按下回调
    /// </summary>
    private void OnAttackStarted(InputAction.CallbackContext context)
    {
        if (!inputEnabled || isPaused) return;

        EventBus.Publish(InputEvents.ON_ATTACK);
    }

    /// <summary>
    /// 攻击释放回调
    /// </summary>
    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        if (!inputEnabled || isPaused) return;

        EventBus.Publish(InputEvents.ON_ATTACK_RELEASE);
    }

    /// <summary>
    /// 暂停回调
    /// </summary>
    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        EventBus.Publish(InputEvents.ON_PAUSE);
    }

    /// <summary>
    /// 跳跃回调
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (!inputEnabled || isPaused) return;

        EventBus.Publish(InputEvents.ON_JUMP);
    }

    /// <summary>
    /// 交互回调
    /// </summary>
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!inputEnabled || isPaused) return;

        EventBus.Publish(InputEvents.ON_INTERACT);
    }

    #endregion

    #region 输入更新

    /// <summary>
    /// 更新输入状态
    /// </summary>
    private void UpdateInputState()
    {
        if (!inputEnabled || isPaused)
        {
            currentMoveInput = Vector2.zero;
            currentLookDelta = Vector2.zero;
            return;
        }

        // 更新移动输入
        UpdateMoveInput();

        // 更新视角输入
        UpdateLookInput();
    }

    /// <summary>
    /// 更新移动输入
    /// </summary>
    private void UpdateMoveInput()
    {
        if (moveAction != null && moveAction.action != null)
        {
            Vector2 rawInput = moveAction.action.ReadValue<Vector2>();

            // 应用手柄死区
            if (isUsingGamepad)
            {
                rawInput = ApplyDeadzone(rawInput, moveDeadzone);
            }

            currentMoveInput = rawInput;
        }
        else
        {
            currentMoveInput = Vector2.zero;
        }
    }

    /// <summary>
    /// 更新视角输入
    /// </summary>
    private void UpdateLookInput()
    {
        if (lookAction != null && lookAction.action != null)
        {
            Vector2 rawInput = lookAction.action.ReadValue<Vector2>();

            // 根据输入设备应用不同灵敏度
            float sensitivity = isUsingGamepad ? gamepadSensitivity : mouseSensitivity;

            // 应用手柄死区
            if (isUsingGamepad)
            {
                rawInput = ApplyDeadzone(rawInput, lookDeadzone);
            }

            // 应用Y轴反转
            float yMultiplier = invertYAxis ? -1f : 1f;

            currentLookDelta = new Vector2(rawInput.x * sensitivity, rawInput.y * sensitivity * yMultiplier);
        }
        else
        {
            currentLookDelta = Vector2.zero;
        }
    }

    /// <summary>
    /// 检测输入设备类型
    /// </summary>
    private void DetectInputDevice()
    {
        if (Gamepad.current != null && Gamepad.current.isPressed)
        {
            isUsingGamepad = true;
        }
        else if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
        {
            isUsingGamepad = false;
        }
        else if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f)
        {
            isUsingGamepad = false;
        }
    }

    /// <summary>
    /// 应用死区
    /// </summary>
    /// <param name="input">输入向量</param>
    /// <param name="deadzone">死区范围</param>
    /// <returns>处理后的输入</returns>
    private Vector2 ApplyDeadzone(Vector2 input, float deadzone)
    {
        if (input.magnitude < deadzone)
            return Vector2.zero;

        // 重新映射死区外的值到0-1范围
        float remappedMagnitude = (input.magnitude - deadzone) / (1f - deadzone);
        return input.normalized * remappedMagnitude;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 设置输入启用/禁用
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (!enabled)
        {
            currentMoveInput = Vector2.zero;
            currentLookDelta = Vector2.zero;
        }

        Debug.Log($"[InputManager] 输入{(enabled ? "已启用" : "已禁用")}");
    }

    /// <summary>
    /// 切换输入启用状态
    /// </summary>
    public void ToggleInput()
    {
        SetInputEnabled(!inputEnabled);
    }

    /// <summary>
    /// 获取移动输入值（带方向）
    /// </summary>
    /// <returns>移动方向向量</returns>
    public Vector3 GetMoveDirection()
    {
        if (!inputEnabled || isPaused) return Vector3.zero;

        Vector2 input = MoveInput;
        return new Vector3(input.x, 0f, input.y);
    }

    /// <summary>
    /// 获取视角增量
    /// </summary>
    /// <returns>视角增量</returns>
    public Vector2 GetLookDelta()
    {
        if (!inputEnabled || isPaused) return Vector2.zero;
        return LookDelta;
    }

    /// <summary>
    /// 检查是否按下攻击键
    /// </summary>
    /// <returns>是否按下</returns>
    public bool IsAttackPressed()
    {
        if (attackAction == null || attackAction.action == null) return false;
        return attackAction.action.ReadValue<float>() > 0.5f;
    }

    /// <summary>
    /// 检查是否按下跳跃键
    /// </summary>
    /// <returns>是否按下</returns>
    public bool IsJumpPressed()
    {
        if (jumpAction == null || jumpAction.action == null) return false;
        return jumpAction.action.ReadValue<float>() > 0.5f;
    }

    #endregion

    #region GameManager事件桥接

    /// <summary>
    /// GameManager需要的事件定义（补充）
    /// </summary>
    public static class GameManagerEvents
    {
        /// <summary>游戏暂停</summary>
        public const string ON_GAME_PAUSED = "GameManager_Paused";

        /// <summary>游戏恢复</summary>
        public const string ON_GAME_RESUMED = "GameManager_Resumed";
    }

    #endregion
}
