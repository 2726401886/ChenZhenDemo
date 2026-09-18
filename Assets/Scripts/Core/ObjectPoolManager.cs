using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 通用对象池管理器（Phase10优化版）- 统一管理所有可复用实例
/// 单例模式，管理敌人、VFX特效、AudioSource、投射物等对象池
/// 替代原ProjectilePool、VFXManager内建池、AudioManager内建池
/// Phase10优化：GetActiveCount改为O(1)计数，TryGetValue替代双重查找，移除池内Destroy
/// </summary>
public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    #region 对象池配置

    [System.Serializable]
    public class PoolEntry
    {
        [Tooltip("对象池唯一标识名称")]
        public string poolName;
        [Tooltip("对象池预制体")]
        public GameObject prefab;
        [Tooltip("初始池容量")]
        public int initialSize = 10;
        [Tooltip("最大池容量")]
        public int maxSize = 50;
    }

    #endregion

    #region Inspector可配置参数

    [Header("对象池配置")]
    [Tooltip("对象池条目列表")]
    [SerializeField] private List<PoolEntry> poolEntries = new List<PoolEntry>();

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private Dictionary<string, int> maxSizes = new Dictionary<string, int>();
    private Dictionary<string, Transform> poolContainers = new Dictionary<string, Transform>();

    /// <summary>Phase10新增：各池活跃对象计数，O(1)查询</summary>
    private Dictionary<string, int> activeCounts = new Dictionary<string, int>();

    /// <summary>Phase10新增：总空闲对象缓存计数</summary>
    private int cachedTotalPooledCount = 0;

    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>Phase10优化：O(1)缓存计数</summary>
    public int TotalPooledCount => cachedTotalPooledCount;

    #endregion

    #region Singleton重写

    protected override bool ShouldDontDestroyOnLoad => true;

    #endregion

    #region 初始化

    protected override void OnSingletonAwake()
    {
        InitializeManager();
    }

    private void InitializeManager()
    {
        if (isInitialized) return;

        GameObject rootContainer = new GameObject("ObjectPool_Container");
        rootContainer.transform.SetParent(transform);

        foreach (var entry in poolEntries)
        {
            if (string.IsNullOrEmpty(entry.poolName) || entry.prefab == null)
                continue;
            CreatePool(entry.poolName, entry.prefab, entry.initialSize, entry.maxSize, rootContainer.transform);
        }

        isInitialized = true;
        DebugLog("[ObjectPoolManager] 初始化完成，池数量: " + pools.Count);
    }

    private void CreatePool(string poolName, GameObject prefab, int initialSize, int maxSize, Transform parent)
    {
        GameObject containerObj = new GameObject("Pool_" + poolName);
        containerObj.transform.SetParent(parent);
        poolContainers[poolName] = containerObj.transform;

        prefabCache[poolName] = prefab;
        maxSizes[poolName] = maxSize;
        pools[poolName] = new Queue<GameObject>();
        activeCounts[poolName] = 0;

        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = CreatePooledObject(poolName, prefab, containerObj.transform);
            pools[poolName].Enqueue(obj);
        }

        cachedTotalPooledCount += initialSize;
        DebugLog("[ObjectPoolManager] 创建对象池: " + poolName + ", 初始容量: " + initialSize);
    }

    private GameObject CreatePooledObject(string poolName, GameObject prefab, Transform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.name = poolName + "_Pool";
        obj.SetActive(false);
        return obj;
    }

    #endregion

    #region 对象获取

    /// <summary>
    /// 从对象池获取可用实例（Phase10优化：TryGetValue + O(1)活跃计数）
    /// </summary>
    public GameObject Get(string poolName, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(poolName, out Queue<GameObject> pool))
        {
            Debug.LogWarning("[ObjectPoolManager] 对象池不存在: " + poolName);
            return null;
        }

        // 尝试从池中获取
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            cachedTotalPooledCount--;
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                activeCounts[poolName]++;
                return obj;
            }
        }

        // 池为空，检查是否可扩容
        int currentActive = 0;
        activeCounts.TryGetValue(poolName, out currentActive);

        int maxSize = 0;
        maxSizes.TryGetValue(poolName, out maxSize);

        if (currentActive < maxSize)
        {
            Transform container = null;
            poolContainers.TryGetValue(poolName, out container);
            if (container == null) container = transform;

            GameObject prefab = null;
            prefabCache.TryGetValue(poolName, out prefab);
            if (prefab == null) return null;

            GameObject newObj = CreatePooledObject(poolName, prefab, container);
            newObj.transform.position = position;
            newObj.transform.rotation = rotation;
            newObj.SetActive(true);
            activeCounts[poolName] = currentActive + 1;
            return newObj;
        }

        Debug.LogWarning("[ObjectPoolManager] 对象池已满: " + poolName);
        return null;
    }

    public GameObject Get(string poolName)
    {
        return Get(poolName, Vector3.zero, Quaternion.identity);
    }

    public GameObject GetArrow(Vector3 position, Quaternion rotation)
    {
        return Get("Arrow", position, rotation);
    }

    #endregion

    #region 对象回收

    /// <summary>
    /// 回收对象到对象池（Phase10优化：TryGetValue + O(1)计数 + 移除Destroy）
    /// </summary>
    public void Recycle(string poolName, GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);

        if (poolContainers.TryGetValue(poolName, out Transform container))
        {
            obj.transform.SetParent(container);
        }
        else
        {
            obj.transform.SetParent(transform);
        }

        if (pools.TryGetValue(poolName, out Queue<GameObject> pool))
        {
            int maxSize = 0;
            maxSizes.TryGetValue(poolName, out maxSize);

            if (pool.Count < maxSize)
            {
                pool.Enqueue(obj);
                cachedTotalPooledCount++;
                // Phase10修复：池满时不Destroy，直接丢弃（对象已隐藏）
            }
            else
            {
                // Phase10修复：池满时不调用Destroy，避免WebGL延迟销毁问题
                DebugLog("[ObjectPoolManager] 池已满，丢弃对象: " + poolName);
            }

            // 更新活跃计数
            if (activeCounts.TryGetValue(poolName, out int count) && count > 0)
            {
                activeCounts[poolName] = count - 1;
            }
        }

        DebugLog("[ObjectPoolManager] 回收对象: " + poolName);
    }

    public void RecycleArrow(GameObject arrow)
    {
        Recycle("Arrow", arrow);
    }

    #endregion

    #region 对象池管理

    public void ClearPool(string poolName)
    {
        if (pools.TryGetValue(poolName, out Queue<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                    Destroy(obj);
            }
            cachedTotalPooledCount = 0;
            activeCounts[poolName] = 0;
            DebugLog("[ObjectPoolManager] 清空对象池: " + poolName);
        }
    }

    public void ClearAllPools()
    {
        foreach (var pool in pools.Values)
        {
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                    Destroy(obj);
            }
        }
        pools.Clear();
        activeCounts.Clear();
        cachedTotalPooledCount = 0;
        DebugLog("[ObjectPoolManager] 清空所有对象池");
    }

    /// <summary>
    /// 获取指定池的可用对象数量
    /// </summary>
    public int GetAvailableCount(string poolName)
    {
        if (pools.TryGetValue(poolName, out Queue<GameObject> pool))
            return pool.Count;
        return 0;
    }

    /// <summary>
    /// Phase10优化：O(1)活跃对象数量查询
    /// </summary>
    public int GetActiveCount(string poolName)
    {
        if (activeCounts.TryGetValue(poolName, out int count))
            return count;
        return 0;
    }

    #endregion

    #region 生命周期清理

    protected override void OnDestroy()
    {
        ClearAllPools();
        base.OnDestroy();
    }

    #endregion

    #region 工具方法

    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
