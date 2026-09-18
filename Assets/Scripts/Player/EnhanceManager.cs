using UnityEngine;

/// <summary>
/// 装备强化管理器 - 核心强化逻辑
/// 校验金币/材料/等级上限，执行成功/失败判定
/// 发布强化结果事件，供UI/音效/特效响应
/// WebGL平台兼容
/// </summary>
public class EnhanceManager : MonoBehaviour
{
    #region 事件常量

    /// <summary>装备强化成功事件</summary>
    public const string ON_EQUIP_ENHANCE_SUCCESS = "ON_EQUIP_ENHANCE_SUCCESS";

    /// <summary>装备强化失败事件</summary>
    public const string ON_EQUIP_ENHANCE_FAIL = "ON_EQUIP_ENHANCE_FAIL";

    #endregion

    #region Inspector可配置参数

    [Header("组件引用")]
    [Tooltip("玩家CoinManager组件")]
    [SerializeField] private CoinManager coinManager;

    [Tooltip("玩家Inventory组件")]
    [SerializeField] private Inventory inventory;

    [Tooltip("玩家EquipmentManager组件")]
    [SerializeField] private EquipmentManager equipmentManager;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region Unity生命周期

    private void Start()
    {
        if (coinManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                coinManager = player.GetComponent<CoinManager>();
                inventory = player.GetComponent<Inventory>();
                equipmentManager = player.GetComponent<EquipmentManager>();
            }
        }
    }

    #endregion

    #region 强化操作

    /// <summary>
    /// 执行装备强化
    /// </summary>
    /// <param name="slotIndex">装备栏索引</param>
    /// <returns>强化是否执行（成功/失败都算执行）</returns>
    public bool EnhanceEquipment(int slotIndex)
    {
        if (equipmentManager == null || coinManager == null || inventory == null)
        {
            Debug.LogWarning("[EnhanceManager] 组件引用缺失");
            return false;
        }

        // 获取当前装备
        Equipment equip = equipmentManager.GetEquipmentAt(slotIndex);
        if (equip == null)
        {
            Debug.LogWarning("[EnhanceManager] 该位置没有装备");
            return false;
        }

        var equipConfig = equip.GetConfig();
        if (equipConfig == null) return false;

        int currentLv = equip.upgradeLevel;
        DebugLog($"[EnhanceManager] 选中装备【{equipConfig.displayName}】Lv{currentLv}，准备强化{currentLv}→{currentLv + 1}");

        // 检查等级上限
        int maxLevel = EnhanceConfig.GetMaxLevel();
        if (currentLv >= maxLevel)
        {
            Debug.LogWarning($"[EnhanceManager] 已达最高等级+{maxLevel}，无法继续强化");
            return false;
        }

        // 获取强化配置
        var enhanceConfig = EnhanceConfig.Get(currentLv);
        if (enhanceConfig == null)
        {
            Debug.LogWarning("[EnhanceManager] 未找到对应等级强化配置");
            return false;
        }

        // 校验金币
        if (!coinManager.HasEnoughCoins(enhanceConfig.costCoins))
        {
            Debug.LogWarning($"[EnhanceManager] 金币不足：需要{enhanceConfig.costCoins}，当前{coinManager.CurrentCoins}");
            return false;
        }

        // 校验材料
        int materialCount = inventory.GetItemCount(enhanceConfig.costMaterialId);
        if (materialCount < enhanceConfig.costMaterialCount)
        {
            Debug.LogWarning($"[EnhanceManager] 材料不足：需要{enhanceConfig.costMaterialCount}个{enhanceConfig.costMaterialId}，当前{materialCount}");
            return false;
        }

        // 资源校验通过，扣除消耗
        coinManager.SpendCoins(enhanceConfig.costCoins);
        inventory.RemoveItem(enhanceConfig.costMaterialId, enhanceConfig.costMaterialCount);
        DebugLog($"[EnhanceManager] 资源校验通过：金币{enhanceConfig.costCoins}，{enhanceConfig.costMaterialId}x{enhanceConfig.costMaterialCount}");

        // 判定强化结果
        bool isSuccess = Random.value <= enhanceConfig.successRate;

        if (isSuccess)
        {
            // 强化成功：提升等级
            equip.upgradeLevel = currentLv + 1;
            DebugLog($"[EnhanceManager] {equipConfig.displayName}强化等级升级为+{equip.upgradeLevel}，攻击倍率{enhanceConfig.statMultiplier}");

            EventBus.Publish(ON_EQUIP_ENHANCE_SUCCESS);
        }
        else
        {
            // 强化失败：等级不掉，消耗已扣除
            DebugLog($"[EnhanceManager] {equipConfig.displayName}强化失败！等级保持+{currentLv}");

            EventBus.Publish(ON_EQUIP_ENHANCE_FAIL);
        }

        return true;
    }

    #endregion

    #region 查询接口

    /// <summary>
    /// 获取装备下一等级的强化配置
    /// </summary>
    public EnhanceConfig GetNextEnhanceConfig(Equipment equip)
    {
        if (equip == null) return null;
        int nextLv = equip.upgradeLevel;
        if (nextLv >= EnhanceConfig.GetMaxLevel()) return null;
        return EnhanceConfig.Get(nextLv);
    }

    /// <summary>
    /// 计算装备强化后的攻击力（含强化倍率）
    /// </summary>
    public float GetEnhancedAttack(Equipment equip, float baseMultiplier)
    {
        if (equip == null) return 0f;
        var cfg = equip.GetConfig();
        if (cfg == null) return 0f;

        float baseAtk = cfg.attackBonus;
        float enhanceMultiplier = 1f;

        // 应用强化等级的累积倍率
        var config = EnhanceConfig.Get(equip.upgradeLevel);
        if (config != null)
            enhanceMultiplier = config.statMultiplier;

        return baseAtk * enhanceMultiplier;
    }

    /// <summary>
    /// 计算装备强化后的防御力（含强化倍率）
    /// </summary>
    public float GetEnhancedDefense(Equipment equip)
    {
        if (equip == null) return 0f;
        var cfg = equip.GetConfig();
        if (cfg == null) return 0f;

        float baseDef = cfg.defenseBonus;
        float enhanceMultiplier = 1f;

        var config = EnhanceConfig.Get(equip.upgradeLevel);
        if (config != null)
            enhanceMultiplier = config.statMultiplier;

        return baseDef * enhanceMultiplier;
    }

    #endregion

    #region 调试日志

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg);
    }

    #endregion
}
