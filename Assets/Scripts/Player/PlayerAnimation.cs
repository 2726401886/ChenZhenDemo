using UnityEngine;

/// <summary>
/// 玩家动画控制器 - 驱动玩家Animator动画状态机
/// 挂载在Player根物体，持有Animator组件引用
/// 订阅EventBus事件：移动、受击、死亡、重生
/// 根据移动向量设置Animator的Speed参数
/// 只负责动画驱动，不处理移动、战斗逻辑
/// 动画帧事件回调只做EventBus发布
/// WebGL平台兼容
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("组件引用")]
    [Tooltip("Animator组件（自动获取本对象的Animator）")]
    [SerializeField] private Animator animator;

    [Header("动画参数名")]
    [Tooltip("移动速度参数名")]
    [SerializeField] private string speedParam = "Speed";

    [Tooltip("受击触发器参数名")]
    [SerializeField] private string hurtTrigger = "Hurt";

    [Tooltip("死亡布尔参数名")]
    [SerializeField] private string dieBool = "Die";

    [Tooltip("重生触发器参数名")]
    [SerializeField] private string respawnTrigger = "Respawn";

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>缓存的移动速度值</summary>
    private float cachedSpeed = 0f;

    /// <summary>移动速度平滑过渡目标值</summary>
    private float targetSpeed = 0f;

    /// <summary>速度平滑过渡速度</summary>
    private float speedSmoothVelocity = 0f;

    #endregion

    #region 公共属性

    /// <summary>当前移动速度</summary>
    public float CurrentSpeed => cachedSpeed;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializePlayerAnimation();
    }

    /// <summary>
    /// 每帧更新 - 平滑过渡动画参数
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        UpdateAnimationParameters();
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
    /// 初始化玩家动画控制器
    /// </summary>
    private void InitializePlayerAnimation()
    {
        if (isInitialized) return;

        // 获取Animator组件
        if (animator == null)
            animator = GetComponent<Animator>();

        // 验证Animator组件
        if (animator == null)
        {
            Debug.LogError("[PlayerAnimation] 未找到Animator组件！");
            enabled = false;
            return;
        }

        // 订阅EventBus事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog("[PlayerAnimation] 初始化完成");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 移动事件
        EventBus.Subscribe("ON_PLAYER_MOVE", OnPlayerMove);

        // 受击事件
        EventBus.Subscribe("ON_PLAYER_HIT", OnPlayerHit);

        // 硬直事件
        EventBus.Subscribe("ON_PLAYER_STUN", OnPlayerStun);

        // 死亡事件
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);

        // 重生事件
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    /// <summary>
    /// 取消所有EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_MOVE", OnPlayerMove);
        EventBus.Unsubscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Unsubscribe("ON_PLAYER_STUN", OnPlayerStun);
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家移动事件处理
    /// </summary>
    private void OnPlayerMove()
    {
        // 获取PlayerController的移动向量
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            // 将移动向量的水平分量作为速度值
            Vector3 moveVector = playerController.CurrentMoveVector;
            targetSpeed = new Vector3(moveVector.x, 0, moveVector.z).magnitude;
        }
        else
        {
            // 备用方案：使用InputManager
            if (InputManager.HasInstance)
            {
                Vector2 moveInput = InputManager.Instance.MoveInput;
                targetSpeed = moveInput.magnitude;
            }
        }

        DebugLog("[PlayerAnimation] 移动事件，目标速度: " + targetSpeed);
    }

    /// <summary>
    /// 玩家受击事件处理
    /// </summary>
    private void OnPlayerHit()
    {
        // 触发受击动画
        if (animator != null && !string.IsNullOrEmpty(hurtTrigger))
        {
            animator.SetTrigger(hurtTrigger);
            DebugLog("[PlayerAnimation] 触发受击动画");
        }
    }

    /// <summary>
    /// 玩家硬直事件处理
    /// </summary>
    private void OnPlayerStun()
    {
        // 硬直时减速移动（通过设置Speed参数为0）
        targetSpeed = 0f;

        // 硬直时触发受击动画（如果需要不同的硬直动画，可扩展）
        if (animator != null && !string.IsNullOrEmpty(hurtTrigger))
        {
            animator.SetTrigger(hurtTrigger);
            DebugLog("[PlayerAnimation] 触发硬直动画");
        }
    }

    /// <summary>
    /// 玩家死亡事件处理
    /// </summary>
    private void OnPlayerDie()
    {
        // 设置死亡状态
        if (animator != null && !string.IsNullOrEmpty(dieBool))
        {
            animator.SetBool(dieBool, true);
            DebugLog("[PlayerAnimation] 触发死亡动画");
        }
    }

    /// <summary>
    /// 玩家重生事件处理
    /// </summary>
    private void OnPlayerRespawn()
    {
        // 重置动画状态
        ResetAnimationState();

        DebugLog("[PlayerAnimation] 重生，动画状态已重置");
    }

    #endregion

    #region 动画参数更新

    /// <summary>
    /// 更新动画参数（每帧调用）
    /// </summary>
    private void UpdateAnimationParameters()
    {
        if (animator == null) return;

        // 平滑过渡速度值
        cachedSpeed = Mathf.SmoothDamp(cachedSpeed, targetSpeed, ref speedSmoothVelocity, 0.1f);

        // 当目标速度接近0时，直接归零
        if (targetSpeed < 0.01f)
        {
            cachedSpeed = 0f;
        }

        // 设置Animator的Speed参数
        if (!string.IsNullOrEmpty(speedParam))
        {
            animator.SetFloat(speedParam, cachedSpeed);
        }
    }

    #endregion

    #region 动画状态重置

    /// <summary>
    /// 重置动画状态
    /// </summary>
    public void ResetAnimationState()
    {
        if (animator == null) return;

        // 重置速度
        targetSpeed = 0f;
        cachedSpeed = 0f;

        // 重置所有参数
        animator.SetFloat(speedParam, 0f);
        animator.SetBool(dieBool, false);

        // 重置所有触发器
        animator.ResetTrigger(hurtTrigger);
        animator.ResetTrigger(respawnTrigger);

        // 强制刷新Animator
        animator.Update(0f);

        DebugLog("[PlayerAnimation] 动画状态已重置");
    }

    /// <summary>
    /// 强制播放指定动画状态
    /// </summary>
    /// <param name="stateName">动画状态名</param>
    /// <param name="layer">动画层</param>
    public void PlayAnimationState(string stateName, int layer = 0)
    {
        if (animator != null && !string.IsNullOrEmpty(stateName))
        {
            animator.Play(stateName, layer);
        }
    }

    #endregion

    #region 动画帧事件回调

    /// <summary>
    /// 攻击动画开始帧事件回调
    /// 在Animator动画帧事件中配置调用此方法
    /// </summary>
    public void OnAttackAnimStartEvent()
    {
        EventBus.Publish("ON_ATTACK_ANIM_START");
        DebugLog("[PlayerAnimation] 攻击动画开始帧");
    }

    /// <summary>
    /// 攻击伤害判定帧事件回调
    /// 在Animator动画帧事件中配置调用此方法
    /// </summary>
    public void OnAttackDamageFrameEvent()
    {
        EventBus.Publish("ON_ATTACK_DAMAGE_FRAME");
        DebugLog("[PlayerAnimation] 攻击伤害帧");
    }

    /// <summary>
    /// 攻击动画结束帧事件回调
    /// 在Animator动画帧事件中配置调用此方法
    /// </summary>
    public void OnAttackAnimEndEvent()
    {
        EventBus.Publish("ON_ATTACK_ANIM_END");
        DebugLog("[PlayerAnimation] 攻击动画结束帧");
    }

    /// <summary>
    /// 死亡动画结束帧事件回调
    /// 在Animator动画帧事件中配置调用此方法
    /// </summary>
    public void OnDeathAnimEndEvent()
    {
        EventBus.Publish("ON_DEATH_ANIM_END");
        DebugLog("[PlayerAnimation] 死亡动画结束帧");
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 设置Animator组件引用
    /// </summary>
    /// <param name="newAnimator">新的Animator组件</param>
    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
    }

    /// <summary>
    /// 设置移动速度参数名
    /// </summary>
    /// <param name="paramName">参数名</param>
    public void SetSpeedParamName(string paramName)
    {
        speedParam = paramName;
    }

    /// <summary>
    /// 手动设置速度值
    /// </summary>
    /// <param name="speed">速度值</param>
    public void SetSpeed(float speed)
    {
        targetSpeed = speed;
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
