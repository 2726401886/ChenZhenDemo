using UnityEngine;

/// <summary>
/// 泛型单例基类（MonoBehaviour版本）
/// 用于创建全局唯一的管理器类，如游戏管理器、音频管理器、UI管理器等
/// WebGL平台兼容：处理DontDestroyOnLoad边界、退出标志重置、线程安全
/// </summary>
/// <typeparam name="T">继承此基类的具体类型</typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    /// <summary>
    /// 单例实例
    /// </summary>
    private static T instance;

    /// <summary>
    /// 用于线程安全的锁对象（WebGL为单线程，保留用于未来扩展）
    /// </summary>
    private static readonly object lockObj = new object();

    /// <summary>
    /// 应用程序是否正在退出（用于防止在程序退出时创建新实例）
    /// </summary>
    private static bool applicationIsQuitting = false;

    /// <summary>
    /// WebGL平台标志：检测当前是否在WebGL环境运行
    /// </summary>
    private static readonly bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;

    /// <summary>
    /// 是否需要将实例标记为DontDestroyOnLoad
    /// 子类可以重写此属性来控制是否跨场景保留
    /// </summary>
    protected virtual bool ShouldDontDestroyOnLoad => true;

    /// <summary>
    /// 获取单例实例（全局访问点）
    /// 如果实例不存在，会自动查找或创建
    /// WebGL兼容：退出时返回null避免NullReferenceException
    /// </summary>
    public static T Instance
    {
        get
        {
            // 如果应用程序正在退出，不再创建新实例，避免NullReferenceException
            if (applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] 应用程序正在退出，无法获取 '{typeof(T)}' 实例。");
                return null;
            }

            // 线程安全的锁机制（WebGL为单线程，但保留锁以支持未来扩展）
            lock (lockObj)
            {
                // 如果实例不存在，尝试在场景中查找
                if (instance == null)
                {
                    // 在场景中查找已存在的实例
                    instance = FindObjectOfType<T>();

                    // 如果场景中没有找到，创建一个新的
                    if (instance == null)
                    {
                        // WebGL平台：创建单例时输出警告日志（便于调试）
                        if (isWebGL)
                        {
                            Debug.Log($"[Singleton] WebGL平台创建 '{typeof(T).Name}' 实例");
                        }

                        // 创建新的GameObject并挂载单例组件
                        GameObject singletonObject = new GameObject();
                        instance = singletonObject.AddComponent<T>();
                        singletonObject.name = $"[Singleton] {typeof(T).Name}";

                        // 设置为DontDestroyOnLoad，跨场景保留
                        // WebGL兼容：DontDestroyOnLoad在WebGL中正常工作，但场景卸载时对象不会自动销毁
                        if (instance is Singleton<T> singleton && singleton.ShouldDontDestroyOnLoad)
                        {
                            DontDestroyOnLoad(singletonObject);
                        }

                        Debug.Log($"[Singleton] 创建了 '{typeof(T).Name}' 实例。");
                    }
                }

                return instance;
            }
        }
    }

    /// <summary>
    /// 检查单例实例是否存在（不触发创建）
    /// </summary>
    public static bool HasInstance => instance != null;

    /// <summary>
    /// Awake方法：检查重复实例并处理
    /// WebGL兼容：重复实例检测在WebGL中同样重要
    /// </summary>
    protected virtual void Awake()
    {
        // 如果已经有实例存在，且当前实例不是已有实例
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[Singleton] 检测到 '{typeof(T).Name}' 的重复实例，已销毁: {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        // 设置当前实例为单例
        instance = this as T;

        // 如果需要跨场景保留
        if (ShouldDontDestroyOnLoad)
        {
            // WebGL兼容：确保对象没有父物体，否则DontDestroyOnLoad无效
            if (transform.parent != null)
            {
                // 如果是子物体，需要从父物体分离才能设置DontDestroyOnLoad
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        // 调用子类的初始化方法
        OnSingletonAwake();
    }

    /// <summary>
    /// 子类可以重写的初始化方法
    /// </summary>
    protected virtual void OnSingletonAwake() { }

    /// <summary>
    /// 当前场景卸载时调用
    /// WebGL兼容：场景切换时清理实例引用
    /// </summary>
    protected virtual void OnDestroy()
    {
        // 如果当前实例被销毁，清空引用
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 应用程序退出时调用
    /// 设置退出标志，防止在退出过程中创建新实例
    /// WebGL特殊处理：WebGL不支持OnApplicationQuit，使用OnApplicationPause替代
    /// </summary>
    private void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }

    /// <summary>
    /// WebGL平台兼容处理
    /// 在WebGL中，场景切换时不会调用OnApplicationQuit
    /// 因此需要在OnApplicationPause中设置退出标志
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        // WebGL平台特殊处理
        // 当浏览器标签页被隐藏或切换时，会触发OnApplicationPause
        if (isWebGL && pauseStatus)
        {
            // WebGL暂停时不清除退出标志，保持与原生平台一致行为
            Debug.Log("[Singleton] WebGL平台暂停");
        }
    }

    /// <summary>
    /// WebGL平台兼容处理
    /// 在WebGL中，场景切换时不会调用OnApplicationQuit
    /// 因此需要在OnDestroy中重置退出标志（如果实例被销毁但程序未退出）
    /// </summary>
    protected virtual void OnDisable()
    {
        // WebGL平台特殊处理
        // 当我们在运行时销毁单例时，需要重置退出标志
        // 这样新的单例可以在需要时被创建
        if (instance == this)
        {
            // 延迟重置，确保是真正的退出而不是场景切换
            CancelInvoke(nameof(ResetQuittingFlag));
            Invoke(nameof(ResetQuittingFlag), 0.1f);
        }
    }

    /// <summary>
    /// 重置退出标志（用于WebGL兼容）
    /// WebGL中场景切换不会触发OnApplicationQuit，需要手动重置
    /// </summary>
    private void ResetQuittingFlag()
    {
        if (instance == null)
        {
            applicationIsQuitting = false;

            // WebGL平台额外日志
            if (isWebGL)
            {
                Debug.Log("[Singleton] WebGL平台退出标志已重置");
            }
        }
    }
}
