using UnityEngine;

/// <summary>
/// 物品实例数据 - 记录物品ID和当前堆叠数
/// 纯数据类，可序列化，用于背包存储
/// WebGL平台兼容
/// </summary>
[System.Serializable]
public class Item
{
    [Tooltip("物品配置ID")]
    public string itemId = "";

    [Tooltip("当前堆叠数量")]
    public int stackCount = 1;

    /// <summary>无参构造（JsonUtility需要）</summary>
    public Item() { }

    /// <summary>带参构造</summary>
    /// <param name="id">物品ID</param>
    /// <param name="count">初始堆叠数</param>
    public Item(string id, int count = 1)
    {
        itemId = id;
        stackCount = count;
    }

    /// <summary>获取物品配置</summary>
    public ItemConfig GetConfig()
    {
        return ItemConfig.Get(itemId);
    }

    /// <summary>是否可以再堆叠</summary>
    public bool CanStack => GetConfig() != null && stackCount < GetConfig().maxStack;

    /// <summary>获取最大堆叠数</summary>
    public int MaxStack => GetConfig() != null ? GetConfig().maxStack : 1;

    /// <summary>获取显示名称</summary>
    public string DisplayName => GetConfig() != null ? GetConfig().displayName : itemId;

    /// <summary>获取物品图标</summary>
    public Sprite Icon => GetConfig() != null ? GetConfig().icon : null;
}
