using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 游戏管理器 - 全局游戏状态控制
/// 继承自Singleton泛型单例基类，负责管理游戏的核心状态和流程
/// 包含：游戏状态管理、时间控制、场景加载、事件系统接口
/// </summary>
public class GameManager : Singleton<GameManager>
{
    #region 游戏状态枚举

    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        /// <summary>游戏未开始（主菜单/加载界面）</summary>
        Menu,
        /// <summary>游戏进行中</summary>
        Playing,
        /// <summary>游戏暂停</summary>
        Paused,
        /// <summary>游戏结束（胜利/失败）</summary>
        GameOver
    }

    #endregion

    #region 序列化字段

    /// <summary>
    /// 默认游戏时间缩放（正常速度为1，暂停为0）
    /// </summary>
    [Header("时间设置")]
    [Tooltip("默认游戏时间缩放（正常速度为1，暂停为0）")]
    [SerializeField] private float defaultTimeScale = 1f;

    [Tooltip("暂停时的时间缩放")]
    [SerializeField] private float pauseTimeScale = 0f;

    #endregion

    #region 私有字段

    /// <summary>
    /// 当前游戏状态
    /// </summary>
    private GameState currentState = GameState.Menu;

    /// <summary>
    /// 游戏开始时间（用于计时）
    /// </summary>
    private float gameStartTime;

    /// <summary>
    /// 游戏总时长（用于记录游戏时长）
    /// </summary>
    private float totalGameTime;

    /// <summary>
    /// 游戏是否已初始化
    /// </summary>
    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取当前游戏状态
    /// </summary>
    public GameState CurrentState => currentState;

    /// <summary>
    /// 游戏是否正在运行（Playing状态）
    /// </summary>
    public bool IsPlaying => currentState == GameState.Playing;

    /// <summary>
    /// 游戏是否暂停
    /// </summary>
    public bool IsPaused => currentState == GameState.Paused;

    /// <summary>
    /// 游戏是否结束
    /// </summary>
    public bool IsGameOver => currentState == GameState.GameOver;

    /// <summary>
    /// 获取当前游戏时长（秒）
    /// </summary>
    public float GameTime => currentState == GameState.Playing 
        ? Time.time - gameStartTime + totalGameTime 
        : totalGameTime;

    /// <summary>
    /// 获取当前时间缩放值
    /// </summary>
    public float CurrentTimeScale => Time.timeScale;

    #endregion

    #region 事件系统

    /// <summary>
    /// 游戏状态改变事件
    /// 参数：新状态，旧状态
    /// </summary>
    public event Action<GameState, GameState> OnGameStateChanged;

    /// <summary>
    /// 游戏开始事件
    /// </summary>
    public event Action OnGameStarted;

    /// <summary>
    /// 游戏暂停事件
    /// </summary>
    public event Action OnGamePaused;

    /// <summary>
    /// 游戏恢复事件
    /// </summary>
    public event Action OnGameResumed;

    /// <summary>
    /// 游戏结束事件
    /// 参数：是否胜利
    /// </summary>
    public event Action<bool> OnGameEnded;

    /// <summary>
    /// 场景加载开始事件
    /// 参数：场景名称
    /// </summary>
    public event Action<string> OnSceneLoadStarted;

    /// <summary>
    /// 场景加载完成事件
    /// 参数：场景名称
    /// </summary>
    public event Action<string> OnSceneLoadCompleted;

    /// <summary>
    /// 时间缩放改变事件
    /// 参数：新的时间缩放值
    /// </summary>
    public event Action<float> OnTimeScaleChanged;

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 单例初始化回调
    /// 由Singleton基类在Awake时调用
    /// </summary>
    protected override void OnSingletonAwake()
    {
        InitializeGameManager();
    }

    /// <summary>
    /// 游戏更新循环
    /// 用于检测暂停按键（ESC键）
    /// </summary>
    private void Update()
    {
        // 检测暂停切换按键（ESC键）
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    /// <summary>
    /// 应用程序获得/失去焦点时的处理
    /// WebGL平台自动暂停/恢复
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        // WebGL平台自动处理
        #if UNITY_WEBGL
        if (!hasFocus && currentState == GameState.Playing)
        {
            PauseGame();
        }
        #endif
    }

    /// <summary>
    /// 应用程序暂停/恢复处理（移动端Home键等）
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        // WebGL平台自动处理
        #if UNITY_WEBGL
        if (pauseStatus && currentState == GameState.Playing)
        {
            PauseGame();
        }
        #endif
    }

    /// <summary>
    /// GameManager销毁时清理
    /// </summary>
    protected override void OnDestroy()
    {
        // 取消注册场景加载回调
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        // 清理事件监听
        ClearAllEvents();
        base.OnDestroy();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化游戏管理器
    /// </summary>
    private void InitializeGameManager()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[GameManager] 游戏管理器已初始化，跳过重复初始化");
            return;
        }

        // 设置初始时间缩放
        Time.timeScale = defaultTimeScale;

        // 记录游戏开始时间
        gameStartTime = Time.time;
        totalGameTime = 0f;

        // 初始化为菜单状态
        currentState = GameState.Menu;

        // 注册场景加载回调
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        isInitialized = true;
        Debug.Log("[GameManager] 游戏管理器初始化完成");
    }

    #endregion

    #region 游戏状态管理

    /// <summary>
    /// 开始游戏
    /// 从菜单状态切换到游戏进行中状态
    /// </summary>
    public void StartGame()
    {
        if (currentState != GameState.Menu)
        {
            Debug.LogWarning("[GameManager] 只能在菜单状态下开始游戏");
            return;
        }

        // 记录开始时间
        gameStartTime = Time.time;
        totalGameTime = 0f;

        // 设置时间缩放为正常值
        Time.timeScale = defaultTimeScale;

        // 切换状态
        ChangeState(GameState.Playing);

        Debug.Log("[GameManager] 游戏开始");
    }

    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void PauseGame()
    {
        if (currentState != GameState.Playing)
        {
            Debug.LogWarning("[GameManager] 只能在游戏进行中暂停");
            return;
        }

        // 记录当前游戏时长
        totalGameTime += Time.time - gameStartTime;

        // 设置时间缩放为0（暂停）
        Time.timeScale = pauseTimeScale;

        // 切换状态
        ChangeState(GameState.Paused);

        Debug.Log("[GameManager] 游戏暂停");
    }

    /// <summary>
    /// 恢复游戏
    /// </summary>
    public void ResumeGame()
    {
        if (currentState != GameState.Paused)
        {
            Debug.LogWarning("[GameManager] 只能在暂停状态恢复游戏");
            return;
        }

        // 设置时间缩放为正常值
        Time.timeScale = defaultTimeScale;

        // 重新记录开始时间
        gameStartTime = Time.time;

        // 切换状态
        ChangeState(GameState.Playing);

        Debug.Log("[GameManager] 游戏恢复");
    }

    /// <summary>
    /// 切换暂停/恢复状态
    /// </summary>
    public void TogglePause()
    {
        if (currentState == GameState.Playing)
        {
            PauseGame();
        }
        else if (currentState == GameState.Paused)
        {
            ResumeGame();
        }
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    /// <param name="isVictory">是否胜利</param>
    public void EndGame(bool isVictory)
    {
        if (currentState == GameState.GameOver)
        {
            Debug.LogWarning("[GameManager] 游戏已经结束");
            return;
        }

        // 记录最终游戏时长
        if (currentState == GameState.Playing)
        {
            totalGameTime += Time.time - gameStartTime;
        }

        // 设置时间缩放为正常值（方便显示结算界面）
        Time.timeScale = defaultTimeScale;

        // 切换状态
        ChangeState(GameState.GameOver);

        // 触发游戏结束事件
        OnGameEnded?.Invoke(isVictory);

        Debug.Log($"[GameManager] 游戏结束 - {(isVictory ? "胜利" : "失败")}");
    }

    /// <summary>
    /// 返回菜单
    /// </summary>
    public void ReturnToMenu()
    {
        // 重置时间缩放
        Time.timeScale = defaultTimeScale;

        // 重置游戏时间
        totalGameTime = 0f;

        // 切换状态
        ChangeState(GameState.Menu);

        Debug.Log("[GameManager] 返回菜单");
    }

    /// <summary>
    /// 切换游戏状态（内部方法）
    /// </summary>
    /// <param name="newState">新状态</param>
    private void ChangeState(GameState newState)
    {
        GameState oldState = currentState;
        currentState = newState;

        // 触发状态改变事件
        OnGameStateChanged?.Invoke(newState, oldState);

        // 触发特定状态事件
        switch (newState)
        {
            case GameState.Playing:
                if (oldState == GameState.Menu || oldState == GameState.GameOver)
                {
                    OnGameStarted?.Invoke();
                }
                else if (oldState == GameState.Paused)
                {
                    OnGameResumed?.Invoke();
                }
                break;

            case GameState.Paused:
                OnGamePaused?.Invoke();
                break;

            case GameState.GameOver:
                // OnGameEnded已在EndGame方法中触发
                break;

            case GameState.Menu:
                // 返回菜单，无需特殊处理
                break;
        }
    }

    #endregion

    #region 时间控制

    /// <summary>
    /// 设置时间缩放
    /// </summary>
    /// <param name="newTimeScale">新的时间缩放值</param>
    public void SetTimeScale(float newTimeScale)
    {
        // 限制时间缩放范围
        newTimeScale = Mathf.Clamp(newTimeScale, 0f, 10f);

        Time.timeScale = newTimeScale;

        // 触发时间缩放改变事件
        OnTimeScaleChanged?.Invoke(newTimeScale);

        Debug.Log($"[GameManager] 时间缩放设置为: {newTimeScale}");
    }

    /// <summary>
    /// 重置时间缩放为默认值
    /// </summary>
    public void ResetTimeScale()
    {
        SetTimeScale(defaultTimeScale);
    }

    /// <summary>
    /// 获取游戏时长的格式化字符串
    /// </summary>
    /// <returns>格式化的时间字符串（分:秒）</returns>
    public string GetFormattedGameTime()
    {
        float time = GameTime;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    #endregion

    #region 场景管理

    /// <summary>
    /// 加载场景（异步）
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameManager] 场景名称不能为空");
            return;
        }

        // 触发场景加载开始事件
        OnSceneLoadStarted?.Invoke(sceneName);

        // 异步加载场景
        SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// 加载场景（带加载进度）
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <param name="onProgress">进度回调（0-1）</param>
    /// <param name="onComplete">加载完成回调</param>
    public void LoadSceneAsync(string sceneName, Action<float> onProgress = null, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameManager] 场景名称不能为空");
            return;
        }

        // 触发场景加载开始事件
        OnSceneLoadStarted?.Invoke(sceneName);

        // 启动协程异步加载
        StartCoroutine(LoadSceneCoroutine(sceneName, onProgress, onComplete));
    }

    /// <summary>
    /// 场景加载协程
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <param name="onProgress">进度回调</param>
    /// <param name="onComplete">完成回调</param>
    /// <returns></returns>
    private System.Collections.IEnumerator LoadSceneCoroutine(
        string sceneName, 
        Action<float> onProgress, 
        Action onComplete)
    {
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);

        if (asyncOperation == null)
        {
            Debug.LogError($"[GameManager] 无法加载场景: {sceneName}");
            yield break;
        }

        // 等待场景加载
        while (!asyncOperation.isDone)
        {
            // 返回加载进度
            onProgress?.Invoke(asyncOperation.progress);

            yield return null;
        }

        // 加载完成
        onProgress?.Invoke(1f);
        onComplete?.Invoke();

        // 触发场景加载完成事件
        OnSceneLoadCompleted?.Invoke(sceneName);
    }

    /// <summary>
    /// 场景加载完成回调
    /// </summary>
    /// <param name="scene">加载的场景</param>
    /// <param name="mode">加载模式</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] 场景加载完成: {scene.name}");
    }

    /// <summary>
    /// 场景卸载完成回调
    /// </summary>
    /// <param name="scene">卸载的场景</param>
    private void OnSceneUnloaded(Scene scene)
    {
        Debug.Log($"[GameManager] 场景卸载完成: {scene.name}");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 清理所有事件监听
    /// </summary>
    private void ClearAllEvents()
    {
        OnGameStateChanged = null;
        OnGameStarted = null;
        OnGamePaused = null;
        OnGameResumed = null;
        OnGameEnded = null;
        OnSceneLoadStarted = null;
        OnSceneLoadCompleted = null;
        OnTimeScaleChanged = null;
    }

    /// <summary>
    /// 重置游戏管理器
    /// 用于重新开始游戏或重新初始化
    /// </summary>
    public void ResetGameManager()
    {
        // 重置状态
        currentState = GameState.Menu;

        // 重置时间
        Time.timeScale = defaultTimeScale;
        totalGameTime = 0f;
        gameStartTime = Time.time;

        // 清理事件
        ClearAllEvents();

        Debug.Log("[GameManager] 游戏管理器已重置");
    }

    #endregion
}
