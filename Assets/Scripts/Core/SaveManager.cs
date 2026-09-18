using UnityEngine;
using System;
using System.IO;

/// <summary>
/// 存档管理器 - 全局存档控制中心
/// 单例模式，管理游戏存档的保存、读取、删除
/// 使用PlayerPrefs存储（WebGL兼容），支持JSON序列化
/// 通过EventBus发布存档事件
/// WebGL平台兼容，纯单线程实现
/// </summary>
public class SaveManager : Singleton<SaveManager>
{
    #region 存档数据结构

    /// <summary>
    /// 游戏存档数据
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        /// <summary>存档版本号</summary>
        public int version = 1;

        /// <summary>当前关卡编号</summary>
        public int currentLevel = 1;

        /// <summary>玩家血量</summary>
        public float playerHealth = 100f;

        /// <summary>玩家最大血量</summary>
        public float playerMaxHealth = 100f;

        /// <summary>当前武器索引</summary>
        public int currentWeaponIndex = 0;

        /// <summary>总击杀数</summary>
        public int totalKills = 0;

        /// <summary>当前关卡击杀数</summary>
        public int levelKills = 0;

        /// <summary>游戏时间（秒）</summary>
        public float playTime = 0f;

        /// <summary>已解锁关卡列表</summary>
        public int unlockedLevels = 1;

        /// <summary>Phase13: 背包物品列表（序列化用）</summary>
        public System.Collections.Generic.List<Item> inventoryItems = new System.Collections.Generic.List<Item>();

        /// <summary>Phase15: 装备栏数据（序列化用）</summary>
        public System.Collections.Generic.List<Equipment> equipmentSlots = new System.Collections.Generic.List<Equipment>();

        /// <summary>Phase16: 玩家金币数量</summary>
        public int playerCoins = 500;

        /// <summary>Phase18: 成就数据列表（序列化用）</summary>
        public System.Collections.Generic.List<AchievementData> achievementDatas = new System.Collections.Generic.List<AchievementData>();

        /// <summary>Phase19: 任务数据列表（序列化用）</summary>
        public System.Collections.Generic.List<QuestData> questDatas = new System.Collections.Generic.List<QuestData>();

        /// <summary>存档时间戳</summary>
        public string saveTime = "";

        /// <summary>设置数据</summary>
        public SettingsData settings = new SettingsData();
    }

    /// <summary>
    /// 设置存档数据
    /// </summary>
    [System.Serializable]
    public class SettingsData
    {
        /// <summary>主音量</summary>
        public float masterVolume = 1f;

        /// <summary>音乐音量</summary>
        public float musicVolume = 0.8f;

        /// <summary>音效音量</summary>
        public float sfxVolume = 1f;

        /// <summary>鼠标灵敏度</summary>
        public float mouseSensitivity = 10f;
    }

    #endregion

    #region 事件常量

    /// <summary>存档完成事件</summary>
    public const string ON_SAVE_COMPLETE = "ON_SAVE_COMPLETE";

    /// <summary>读档完成事件</summary>
    public const string ON_LOAD_COMPLETE = "ON_LOAD_COMPLETE";

    /// <summary>存档删除事件</summary>
    public const string ON_SAVE_DELETE = "ON_SAVE_DELETE";

    #endregion

    #region PlayerPrefs键名

    /// <summary>存档数据键名</summary>
    private const string SAVE_DATA_KEY = "GameSaveData";

    /// <summary>是否有存档键名</summary>
    private const string HAS_SAVE_KEY = "HasGameSave";

    #endregion

    #region Inspector可配置参数

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>当前存档数据</summary>
    private SaveData currentSaveData;

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    #endregion

    #region 公共属性

    /// <summary>当前存档数据</summary>
    public SaveData CurrentSave => currentSaveData;

    /// <summary>是否有存档</summary>
    public bool HasSave => PlayerPrefs.GetInt(HAS_SAVE_KEY, 0) == 1;

    #endregion

    #region 初始化

    protected override void OnSingletonAwake()
    {
        InitializeManager();
    }

    private void InitializeManager()
    {
        if (isInitialized) return;

        currentSaveData = new SaveData();

        isInitialized = true;
        DebugLog("[SaveManager] 存档管理器初始化完成");
    }

    #endregion

    #region 存档操作

    /// <summary>
    /// 保存游戏存档
    /// </summary>
    public void SaveGame()
    {
        // 更新存档时间
        currentSaveData.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Phase13: 保存背包数据
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory != null)
            {
                currentSaveData.inventoryItems = inventory.SerializeForSave();
            }

            // Phase15: 保存装备栏数据
            EquipmentManager equipMgr = player.GetComponent<EquipmentManager>();
            if (equipMgr != null)
            {
                currentSaveData.equipmentSlots = equipMgr.SerializeForSave();
            }

            // Phase16: 保存金币数据
            CoinManager coinMgr = player.GetComponent<CoinManager>();
            if (coinMgr != null)
            {
                currentSaveData.playerCoins = coinMgr.CurrentCoins;
            }

            // Phase18: 保存成就数据
            AchievementManager achMgr = player.GetComponent<AchievementManager>();
            if (achMgr != null)
            {
                currentSaveData.achievementDatas = achMgr.SerializeForSave();
            }

            // Phase19: 保存任务数据
            QuestManager questMgr = player.GetComponent<QuestManager>();
            if (questMgr != null)
            {
                currentSaveData.questDatas = questMgr.SerializeForSave();
            }
        }

        // 序列化为JSON
        string json = JsonUtility.ToJson(currentSaveData);

        // 保存到PlayerPrefs
        PlayerPrefs.SetString(SAVE_DATA_KEY, json);
        PlayerPrefs.SetInt(HAS_SAVE_KEY, 1);
        PlayerPrefs.Save();

        // 发布存档完成事件
        EventBus.Publish(ON_SAVE_COMPLETE);

        DebugLog($"[SaveManager] 游戏已保存 - 关卡: {currentSaveData.currentLevel}, 击杀: {currentSaveData.totalKills}");
    }

    /// <summary>
    /// 读取游戏存档
    /// </summary>
    /// <returns>是否成功读取</returns>
    public bool LoadGame()
    {
        if (!HasSave)
        {
            DebugLog("[SaveManager] 没有存档数据");
            return false;
        }

        string json = PlayerPrefs.GetString(SAVE_DATA_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            DebugLog("[SaveManager] 存档数据为空");
            return false;
        }

        try
        {
            currentSaveData = JsonUtility.FromJson<SaveData>(json);
            if (currentSaveData == null)
            {
                currentSaveData = new SaveData();
            }

            // 发布读档完成事件
            EventBus.Publish(ON_LOAD_COMPLETE);

            // Phase13: 加载背包数据
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Inventory inventory = player.GetComponent<Inventory>();
                if (inventory != null && currentSaveData.inventoryItems != null)
                {
                    inventory.LoadFromSave(currentSaveData.inventoryItems);
                }

                // Phase15: 加载装备栏数据
                EquipmentManager equipMgr = player.GetComponent<EquipmentManager>();
                if (equipMgr != null && currentSaveData.equipmentSlots != null)
                {
                    equipMgr.LoadFromSave(currentSaveData.equipmentSlots);
                }

                // Phase16: 加载金币数据
                CoinManager coinMgr = player.GetComponent<CoinManager>();
                if (coinMgr != null)
                {
                    coinMgr.SetCoins(currentSaveData.playerCoins);
                }

                // Phase18: 加载成就数据
                AchievementManager achMgr = player.GetComponent<AchievementManager>();
                if (achMgr != null && currentSaveData.achievementDatas != null)
                {
                    achMgr.LoadFromSave(currentSaveData.achievementDatas);
                }

                // Phase19: 加载任务数据
                QuestManager questMgr = player.GetComponent<QuestManager>();
                if (questMgr != null && currentSaveData.questDatas != null)
                {
                    questMgr.LoadFromSave(currentSaveData.questDatas);
                }
            }

            DebugLog($"[SaveManager] 存档已读取 - 关卡: {currentSaveData.currentLevel}, 击杀: {currentSaveData.totalKills}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 读取存档失败: {e.Message}");
            currentSaveData = new SaveData();
            return false;
        }
    }

    /// <summary>
    /// 删除游戏存档
    /// </summary>
    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SAVE_DATA_KEY);
        PlayerPrefs.SetInt(HAS_SAVE_KEY, 0);
        PlayerPrefs.Save();

        currentSaveData = new SaveData();

        // 发布存档删除事件
        EventBus.Publish(ON_SAVE_DELETE);

        DebugLog("[SaveManager] 存档已删除");
    }

    /// <summary>
    /// 检查是否有存档
    /// </summary>
    /// <returns>是否有存档</returns>
    public bool HasSaveData()
    {
        return HasSave;
    }

    #endregion

    #region 存档数据更新

    /// <summary>
    /// 更新当前关卡
    /// </summary>
    /// <param name="level">关卡编号</param>
    public void SetCurrentLevel(int level)
    {
        currentSaveData.currentLevel = level;
    }

    /// <summary>
    /// 更新玩家血量
    /// </summary>
    /// <param name="health">当前血量</param>
    /// <param name="maxHealth">最大血量</param>
    public void SetPlayerHealth(float health, float maxHealth)
    {
        currentSaveData.playerHealth = health;
        currentSaveData.playerMaxHealth = maxHealth;
    }

    /// <summary>
    /// 更新当前武器
    /// </summary>
    /// <param name="weaponIndex">武器索引</param>
    public void SetCurrentWeapon(int weaponIndex)
    {
        currentSaveData.currentWeaponIndex = weaponIndex;
    }

    /// <summary>
    /// 增加击杀数
    /// </summary>
    /// <param name="count">击杀数量</param>
    public void AddKillCount(int count = 1)
    {
        currentSaveData.totalKills += count;
        currentSaveData.levelKills += count;
    }

    /// <summary>
    /// 重置关卡击杀数
    /// </summary>
    public void ResetLevelKills()
    {
        currentSaveData.levelKills = 0;
    }

    /// <summary>
    /// 更新游戏时间
    /// </summary>
    /// <param name="time">游戏时间（秒）</param>
    public void SetPlayTime(float time)
    {
        currentSaveData.playTime = time;
    }

    /// <summary>
    /// 解锁下一关
    /// </summary>
    public void UnlockNextLevel()
    {
        int nextLevel = currentSaveData.currentLevel + 1;
        if (nextLevel > currentSaveData.unlockedLevels)
        {
            currentSaveData.unlockedLevels = nextLevel;
        }
    }

    /// <summary>
    /// 获取已解锁关卡数
    /// </summary>
    /// <returns>已解锁关卡数</returns>
    public int GetUnlockedLevels()
    {
        return currentSaveData.unlockedLevels;
    }

    /// <summary>
    /// 保存设置到存档
    /// </summary>
    /// <param name="settings">设置数据</param>
    public void SaveSettings(SettingsData settings)
    {
        currentSaveData.settings = settings;
    }

    /// <summary>
    /// 获取设置数据
    /// </summary>
    /// <returns>设置数据</returns>
    public SettingsData GetSettings()
    {
        return currentSaveData.settings;
    }

    #endregion

    #region 生命周期清理

    protected override void OnDestroy()
    {
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
