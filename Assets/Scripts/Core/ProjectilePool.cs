using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 投射物对象池管理器（已废弃 - 请使用ObjectPoolManager）
/// 此类保留用于兼容Arrow.cs中的ProjectilePool.RecycleArrow调用
/// 所有池化管理已迁移至ObjectPoolManager统一管理
/// WebGL平台兼容
/// </summary>
public class ProjectilePool : Singleton<ProjectilePool>
{
    #region Inspector可配置参数

    [Header("对象池设置")]
    [Tooltip("每种投射物的初始池大小")]
    [SerializeField] private int initialPoolSize = 10;

    [Tooltip("每种投射物的最大池大小")]
    [SerializeField] private int maxPoolSize = 30;

    [Header("投射物预制体")]
    [Tooltip("箭矢预制体")]
    [SerializeField] private GameObject arrowPrefab;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>投射物对象池字典（按类型名分组）</summary>
    private Dictionary<string, List<GameObject>> pools = new Dictionary<string, List<GameObject>>();

    /// <summary>投射物预制体字典</summary>
    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();

    #endregion

    #region Singleton重写

    /// <summary>不需要跨场景保留</summary>
    protected override bool ShouldDontDestroyOnLoad => true;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    protected override void OnSingletonAwake()
    {
        InitializePool();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化对象池
    /// </summary>
    private void InitializePool()
    {
        // 注册箭矢预制体
        if (arrowPrefab != null)
        {
            RegisterPrefab("Arrow", arrowPrefab);
            InitializePoolForType("Arrow");
        }

        DebugLog("[ProjectilePool] 初始化完成");
    }

    /// <summary>
    /// 注册投射物预制体
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    /// <param name="prefab">预制体</param>
    public void RegisterPrefab(string typeName, GameObject prefab)
    {
        if (string.IsNullOrEmpty(typeName) || prefab == null)
        {
            Debug.LogWarning("[ProjectilePool] 注册预制体失败：参数无效");
            return;
        }

        prefabDict[typeName] = prefab;
        DebugLog($"[ProjectilePool] 注册预制体: {typeName}");
    }

    /// <summary>
    /// 为指定类型初始化对象池
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    private void InitializePoolForType(string typeName)
    {
        if (!pools.ContainsKey(typeName))
        {
            pools[typeName] = new List<GameObject>();
        }

        GameObject prefab;
        if (!prefabDict.TryGetValue(typeName, out prefab))
        {
            Debug.LogWarning($"[ProjectilePool] 未找到类型 '{typeName}' 的预制体");
            return;
        }

        // 创建初始对象
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePoolObject(typeName, prefab);
        }

        DebugLog($"[ProjectilePool] 类型 '{typeName}' 对象池初始化完成，数量: {initialPoolSize}");
    }

    /// <summary>
    /// 创建对象池对象
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    /// <param name="prefab">预制体</param>
    /// <returns>新建的GameObject</returns>
    private GameObject CreatePoolObject(string typeName, GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.name = $"{typeName}_Pool";
        obj.SetActive(false);

        if (pools.ContainsKey(typeName))
        {
            pools[typeName].Add(obj);
        }

        return obj;
    }

    #endregion

    #region 对象获取

    /// <summary>
    /// 从对象池获取投射物
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    /// <param name="position">生成位置</param>
    /// <param name="rotation">生成旋转</param>
    /// <returns>获取到的GameObject，如果没有可用对象则返回null</returns>
    public GameObject Get(string typeName, Vector3 position, Quaternion rotation)
    {
        // 确保对象池已初始化
        if (!pools.ContainsKey(typeName))
        {
            InitializePoolForType(typeName);
        }

        List<GameObject> pool;
        if (!pools.TryGetValue(typeName, out pool))
        {
            Debug.LogWarning($"[ProjectilePool] 未找到类型 '{typeName}' 的对象池");
            return null;
        }

        // 查找未激活的对象
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].activeSelf)
            {
                GameObject obj = pool[i];
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                return obj;
            }
        }

        // 对象池已满，创建新对象（如果未超过最大限制）
        if (pool.Count < maxPoolSize)
        {
            GameObject prefab;
            if (prefabDict.TryGetValue(typeName, out prefab))
            {
                GameObject newObj = CreatePoolObject(typeName, prefab);
                newObj.transform.position = position;
                newObj.transform.rotation = rotation;
                newObj.SetActive(true);
                return newObj;
            }
        }

        Debug.LogWarning($"[ProjectilePool] 对象池 '{typeName}' 已满，无法获取新对象");
        return null;
    }

    /// <summary>
    /// 获取箭矢
    /// </summary>
    /// <param name="position">生成位置</param>
    /// <param name="rotation">生成旋转</param>
    /// <returns>箭矢GameObject</returns>
    public GameObject GetArrow(Vector3 position, Quaternion rotation)
    {
        return Get("Arrow", position, rotation);
    }

    #endregion

    #region 对象回收

    /// <summary>
    /// 回收投射物到对象池
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    /// <param name="obj">要回收的GameObject</param>
    public void Recycle(string typeName, GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        DebugLog($"[ProjectilePool] 回收投射物: {typeName}");
    }

    /// <summary>
    /// 回收箭矢
    /// </summary>
    /// <param name="arrow">要回收的箭矢</param>
    public void RecycleArrow(GameObject arrow)
    {
        Recycle("Arrow", arrow);
    }

    #endregion

    #region 对象池管理

    /// <summary>
    /// 清空指定类型对象池
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    public void ClearPool(string typeName)
    {
        if (pools.ContainsKey(typeName))
        {
            foreach (var obj in pools[typeName])
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            pools[typeName].Clear();
            DebugLog($"[ProjectilePool] 清空对象池: {typeName}");
        }
    }

    /// <summary>
    /// 清空所有对象池
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var pool in pools)
        {
            foreach (var obj in pool.Value)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }
        pools.Clear();
        DebugLog("[ProjectilePool] 清空所有对象池");
    }

    /// <summary>
    /// 获取指定类型的对象池大小
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    /// <returns>对象池大小</returns>
    public int GetPoolSize(string typeName)
    {
        if (pools.ContainsKey(typeName))
        {
            return pools[typeName].Count;
        }
        return 0;
    }

    /// <summary>
    /// 获取指定类型的可用对象数量
    /// </summary>
    /// <param name="typeName">投射物类型名</param>
    /// <returns>可用对象数量</returns>
    public int GetAvailableCount(string typeName)
    {
        if (pools.ContainsKey(typeName))
        {
            int count = 0;
            foreach (var obj in pools[typeName])
            {
                if (obj != null && !obj.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }
        return 0;
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
