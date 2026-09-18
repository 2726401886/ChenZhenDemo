using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 商店商品配置表 - 存储所有商品的售价、回收价、库存
/// 纯数据类，SceneAutoBuilder初始化时填充
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class ShopConfig
{
    [Header("商品设置")]
    [Tooltip("商品唯一标识ID")]
    public string shopItemId = "";

    [Tooltip("关联物品ID（对应ItemConfig.itemId或EquipmentConfig.equipId）")]
    public string itemId = "";

    [Tooltip("商品显示名称")]
    public string displayName = "未命名商品";

    [Tooltip("商品描述")]
    public string description = "";

    [Header("价格设置")]
    [Tooltip("购买价格（金币）")]
    public int buyPrice = 100;

    [Tooltip("出售回收价（金币）")]
    public int sellPrice = 50;

    [Header("库存设置")]
    [Tooltip("商品库存数量（-1表示无限库存）")]
    public int stock = -1;

    [Tooltip("是否为装备类商品")]
    public bool isEquipment = false;

    /// <summary>商品配置字典</summary>
    private static Dictionary<string, ShopConfig> _configs = new Dictionary<string, ShopConfig>();

    /// <summary>注册商品配置</summary>
    public static void Register(string id, ShopConfig config)
    {
        if (!_configs.ContainsKey(id))
            _configs.Add(id, config);
        else
            _configs[id] = config;
    }

    /// <summary>获取商品配置</summary>
    public static ShopConfig Get(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;
        Debug.LogWarning($"[ShopConfig] 未找到商品: {id}");
        return null;
    }

    /// <summary>获取所有已注册的商品配置</summary>
    public static Dictionary<string, ShopConfig> GetAll()
    {
        return _configs;
    }

    /// <summary>清除所有配置</summary>
    public static void ClearAll()
    {
        _configs.Clear();
    }
}
