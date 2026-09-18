using UnityEngine;

/// <summary>
/// 金币管理器 - 管理玩家金币的增加/扣除
/// 通过EventBus发布金币变动事件，供UI系统响应
/// WebGL平台兼容
/// </summary>
public class CoinManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>金币变动事件</summary>
    public const string ON_COIN_CHANGE = "ON_COIN_CHANGE";

    #endregion

    #region Inspector可配置参数

    [Header("金币设置")]
    [Tooltip("初始金币数量")]
    [SerializeField] private int initialCoins = 500;

    [Tooltip("当前金币数量")]
    [SerializeField] private int currentCoins = 0;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 公共属性

    /// <summary>当前金币数量</summary>
    public int CurrentCoins => currentCoins;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        InitializeCoinManager();
    }

    #endregion

    #region 初始化

    private void InitializeCoinManager()
    {
        currentCoins = initialCoins;
        DebugLog($"[CoinManager] 玩家初始金币：{currentCoins}");
    }

    #endregion

    #region 金币操作

    /// <summary>
    /// 增加金币
    /// </summary>
    /// <param name="amount">增加数量</param>
    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[CoinManager] 增加金币数量必须大于0");
            return;
        }

        currentCoins += amount;
        DebugLog($"[CoinManager] 金币+{amount}，当前：{currentCoins}");

        EventBus.Publish(ON_COIN_CHANGE);
    }

    /// <summary>
    /// 扣除金币
    /// </summary>
    /// <param name="amount">扣除数量</param>
    /// <returns>是否成功扣除</returns>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[CoinManager] 扣除金币数量必须大于0");
            return false;
        }

        if (currentCoins < amount)
        {
            Debug.LogWarning($"[CoinManager] 金币不足：需要{amount}，当前{currentCoins}");
            return false;
        }

        currentCoins -= amount;
        DebugLog($"[CoinManager] 扣除金币，剩余：{currentCoins}");

        EventBus.Publish(ON_COIN_CHANGE);
        return true;
    }

    /// <summary>
    /// 检查金币是否足够
    /// </summary>
    public bool HasEnoughCoins(int amount)
    {
        return currentCoins >= amount;
    }

    /// <summary>
    /// 设置金币数量（存档加载用）
    /// </summary>
    public void SetCoins(int amount)
    {
        currentCoins = Mathf.Max(0, amount);
        EventBus.Publish(ON_COIN_CHANGE);
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
