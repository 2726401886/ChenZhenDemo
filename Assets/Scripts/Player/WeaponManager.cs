using UnityEngine;

/// <summary>
/// 武器管理器 - 管理玩家武器切换
/// 挂载在玩家对象上，持有武器数组
/// 支持1/2数字键切换武器，切换时发布ON_WEAPON_CHANGE事件
/// 武器切换过程中禁止攻击，硬直/无敌帧状态下不能切换武器
/// WebGL平台兼容
/// </summary>
public class WeaponManager : MonoBehaviour
{
    #region Inspector可配置参数

    [Header("武器设置")]
    [Tooltip("武器数组（挂载在子物体上的Weapon组件）")]
    [SerializeField] private Weapon[] weapons;

    [Tooltip("默认激活的武器索引")]
    [SerializeField] private int defaultWeaponIndex = 0;

    [Header("切换设置")]
    [Tooltip("切换武器时是否禁止攻击")]
    [SerializeField] private bool blockAttackDuringSwitch = true;

    [Tooltip("切换武器动画时间（秒）")]
    [SerializeField] private float switchDuration = 0.3f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>当前激活的武器索引</summary>
    private int currentWeaponIndex = -1;

    /// <summary>当前激活的武器</summary>
    private Weapon currentWeapon;

    /// <summary>是否正在切换武器</summary>
    private bool isSwitching = false;

    /// <summary>切换计时器</summary>
    private float switchTimer = 0f;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>玩家CombatSystem引用</summary>
    private CombatSystem combatSystem;

    #endregion

    #region 公共属性

    /// <summary>当前激活的武器</summary>
    public Weapon CurrentWeapon => currentWeapon;

    /// <summary>当前武器索引</summary>
    public int CurrentWeaponIndex => currentWeaponIndex;

    /// <summary>是否正在切换武器</summary>
    public bool IsSwitching => isSwitching;

    /// <summary>武器数量</summary>
    public int WeaponCount => weapons != null ? weapons.Length : 0;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeWeaponManager();
    }

    /// <summary>
    /// 每帧更新 - 处理武器切换输入和切换计时器
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        // 更新切换计时器
        if (isSwitching)
        {
            switchTimer -= Time.deltaTime;
            if (switchTimer <= 0)
            {
                isSwitching = false;
                DebugLog("[WeaponManager] 武器切换完成");
            }
            return;
        }

        // 处理武器切换输入
        HandleWeaponSwitchInput();
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
    /// 初始化武器管理器
    /// </summary>
    private void InitializeWeaponManager()
    {
        if (isInitialized) return;

        // 获取CombatSystem组件
        combatSystem = GetComponent<CombatSystem>();

        // 自动查找子物体上的Weapon组件
        if (weapons == null || weapons.Length == 0)
        {
            weapons = GetComponentsInChildren<Weapon>(true);
        }

        // 验证武器数组
        if (weapons == null || weapons.Length == 0)
        {
            Debug.LogWarning("[WeaponManager] 未找到任何武器组件");
            return;
        }

        // 初始化所有武器状态
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                weapons[i].SetOwner(gameObject);
                weapons[i].Deactivate();
            }
        }

        // 激活默认武器
        if (defaultWeaponIndex >= 0 && defaultWeaponIndex < weapons.Length)
        {
            SwitchWeapon(defaultWeaponIndex);
        }
        else if (weapons.Length > 0)
        {
            SwitchWeapon(0);
        }

        // 订阅事件
        SubscribeEvents();

        isInitialized = true;
        DebugLog($"[WeaponManager] 初始化完成，武器数量: {weapons.Length}");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 玩家重生事件 - 重置武器状态
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    /// <summary>
    /// 取消EventBus事件订阅
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家重生事件处理
    /// </summary>
    private void OnPlayerRespawn()
    {
        // 重生时重置到默认武器
        if (defaultWeaponIndex >= 0 && defaultWeaponIndex < weapons.Length)
        {
            SwitchWeapon(defaultWeaponIndex);
        }
    }

    #endregion

    #region 武器切换

    /// <summary>
    /// 处理武器切换输入
    /// </summary>
    private void HandleWeaponSwitchInput()
    {
        // 检查是否在硬直或无敌帧状态
        if (combatSystem != null && (combatSystem.IsInHitstun || combatSystem.IsInInvincibility))
        {
            return;
        }

        // 数字键1切换武器
        if (Input.GetKeyDown(KeyCode.Alpha1) && weapons.Length > 0)
        {
            SwitchWeapon(0);
        }

        // 数字键2切换武器
        if (Input.GetKeyDown(KeyCode.Alpha2) && weapons.Length > 1)
        {
            SwitchWeapon(1);
        }

        // 数字键3切换武器
        if (Input.GetKeyDown(KeyCode.Alpha3) && weapons.Length > 2)
        {
            SwitchWeapon(2);
        }

        // 数字键4切换武器
        if (Input.GetKeyDown(KeyCode.Alpha4) && weapons.Length > 3)
        {
            SwitchWeapon(3);
        }

        // 鼠标滚轮切换武器
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0)
        {
            // 向上滚动 - 下一把武器
            int nextIndex = (currentWeaponIndex + 1) % weapons.Length;
            SwitchWeapon(nextIndex);
        }
        else if (scroll < 0)
        {
            // 向下滚动 - 上一把武器
            int prevIndex = (currentWeaponIndex - 1 + weapons.Length) % weapons.Length;
            SwitchWeapon(prevIndex);
        }
    }

    /// <summary>
    /// 切换武器
    /// </summary>
    /// <param name="weaponIndex">目标武器索引</param>
    public void SwitchWeapon(int weaponIndex)
    {
        // 索引验证
        if (weaponIndex < 0 || weaponIndex >= weapons.Length)
        {
            Debug.LogWarning($"[WeaponManager] 无效的武器索引: {weaponIndex}");
            return;
        }

        // 不能切换到当前武器
        if (weaponIndex == currentWeaponIndex)
        {
            DebugLog("[WeaponManager] 已经是当前武器，跳过切换");
            return;
        }

        // 检查是否正在切换
        if (isSwitching)
        {
            DebugLog("[WeaponManager] 正在切换武器中，无法切换");
            return;
        }

        // 禁用旧武器
        if (currentWeapon != null)
        {
            currentWeapon.Deactivate();
        }

        // 更新当前武器索引
        currentWeaponIndex = weaponIndex;
        currentWeapon = weapons[currentWeaponIndex];

        // 激活新武器
        if (currentWeapon != null)
        {
            currentWeapon.Activate();

            // 发布武器切换事件
            PublishWeaponChangeEvent();

            DebugLog($"[WeaponManager] 切换到武器: {currentWeapon.WeaponName}");
        }

        // 开始切换计时器
        isSwitching = true;
        switchTimer = switchDuration;
    }

    /// <summary>
    /// 切换到下一把武器
    /// </summary>
    public void SwitchToNextWeapon()
    {
        if (weapons.Length == 0) return;

        int nextIndex = (currentWeaponIndex + 1) % weapons.Length;
        SwitchWeapon(nextIndex);
    }

    /// <summary>
    /// 切换到上一把武器
    /// </summary>
    public void SwitchToPreviousWeapon()
    {
        if (weapons.Length == 0) return;

        int prevIndex = (currentWeaponIndex - 1 + weapons.Length) % weapons.Length;
        SwitchWeapon(prevIndex);
    }

    #endregion

    #region 武器管理

    /// <summary>
    /// 添加武器到管理器
    /// </summary>
    /// <param name="weapon">要添加的武器</param>
    /// <returns>添加的武器索引</returns>
    public int AddWeapon(Weapon weapon)
    {
        if (weapon == null)
        {
            Debug.LogWarning("[WeaponManager] 不能添加空武器");
            return -1;
        }

        // 扩展数组
        System.Array.Resize(ref weapons, weapons.Length + 1);
        weapons[weapons.Length - 1] = weapon;

        // 设置武器所有者
        weapon.SetOwner(gameObject);
        weapon.Deactivate();

        DebugLog($"[WeaponManager] 添加武器: {weapon.WeaponName}，索引: {weapons.Length - 1}");
        return weapons.Length - 1;
    }

    /// <summary>
    /// 移除武器
    /// </summary>
    /// <param name="weaponIndex">要移除的武器索引</param>
    public void RemoveWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Length)
        {
            Debug.LogWarning($"[WeaponManager] 无效的武器索引: {weaponIndex}");
            return;
        }

        // 如果要移除的是当前武器，先切换到其他武器
        if (weaponIndex == currentWeaponIndex)
        {
            if (weapons.Length > 1)
            {
                int newIndex = (weaponIndex + 1) % weapons.Length;
                SwitchWeapon(newIndex);
            }
            else
            {
                currentWeapon = null;
                currentWeaponIndex = -1;
            }
        }

        // 移除武器
        Weapon removedWeapon = weapons[weaponIndex];
        weapons[weaponIndex] = null;

        // 重新组织数组
        Weapon[] newWeapons = new Weapon[weapons.Length - 1];
        int newIndex2 = 0;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (i != weaponIndex && weapons[i] != null)
            {
                newWeapons[newIndex2] = weapons[i];
                newIndex2++;
            }
        }
        weapons = newWeapons;

        // 修正当前武器索引
        if (currentWeaponIndex >= weapons.Length)
        {
            currentWeaponIndex = weapons.Length - 1;
        }

        DebugLog($"[WeaponManager] 移除武器: {removedWeapon.WeaponName}");
    }

    /// <summary>
    /// 获取武器信息
    /// </summary>
    /// <param name="weaponIndex">武器索引</param>
    /// <returns>武器信息字符串</returns>
    public string GetWeaponInfo(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Length || weapons[weaponIndex] == null)
        {
            return "无武器";
        }

        Weapon weapon = weapons[weaponIndex];
        return $"{weapon.WeaponName} | 伤害:{weapon.WeaponDamage} | 冷却:{weapon.AttackCooldown}s | 范围:{weapon.AttackRange}";
    }

    #endregion

    #region 事件发布

    /// <summary>
    /// 发布武器切换事件
    /// </summary>
    private void PublishWeaponChangeEvent()
    {
        if (currentWeapon != null)
        {
            // 创建武器切换事件数据
            WeaponChangeEventData data = new WeaponChangeEventData
            {
                weaponName = currentWeapon.WeaponName,
                weaponDamage = currentWeapon.WeaponDamage,
                attackCooldown = currentWeapon.AttackCooldown,
                attackRange = currentWeapon.AttackRange
            };

            EventBus.Publish("ON_WEAPON_CHANGE", data);

            DebugLog($"[WeaponManager] 发布武器切换事件: {currentWeapon.WeaponName}");
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

/// <summary>
/// 武器切换事件数据
/// </summary>
public struct WeaponChangeEventData
{
    /// <summary>武器名称</summary>
    public string weaponName;

    /// <summary>武器伤害值</summary>
    public float weaponDamage;

    /// <summary>攻击冷却时间</summary>
    public float attackCooldown;

    /// <summary>攻击范围</summary>
    public float attackRange;
}
