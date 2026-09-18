using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 特效管理器 - 全局粒子特效控制中心
/// 继承自Singleton泛型单例基类，管理游戏中所有粒子特效
/// 采用对象池复用预制体实例，禁止频繁Instantiate/Destroy，减少GC开销
/// 通过EventBus监听战斗事件，自动播放对应特效
/// 支持游戏暂停时冻结所有粒子播放
/// WebGL平台兼容，纯单线程实现
/// </summary>
public class VFXManager : Singleton<VFXManager>
{
    #region 特效字典配置

    /// <summary>
    /// 特效条目 - 定义一种特效的配置信息
    /// </summary>
    [System.Serializable]
    public class VFXEntry
    {
        [Tooltip("特效唯一标识名称")]
        public string name;

        [Tooltip("特效预制体")]
        public GameObject prefab;

        [Tooltip("特效持续时长（秒），0表示使用粒子系统默认时长")]
        public float duration = 0f;

        [Tooltip("默认缩放倍率")]
        public float scale = 1f;
    }

    #endregion

    #region 活跃特效追踪

    /// <summary>
    /// 活跃特效数据 - 追踪正在播放的特效实例
    /// </summary>
    private class ActiveVFX
    {
        public GameObject gameObject;       // 特效游戏对象
        public string vfxName;              // 特效名称（用于回收到对应池）
        public ParticleSystem particleSystem; // 粒子系统组件
        public float duration;              // 总持续时长
        public float elapsedTime;           // 已播放时间
    }

    #endregion

    #region Inspector可配置参数

    [Header("特效字典配置")]
    [Tooltip("特效条目列表 - 配置所有可用特效的预制体和参数")]
    [SerializeField] private List<VFXEntry> vfxEntries = new List<VFXEntry>();

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    /// <summary>特效字典 - 按名称快速查找特效配置</summary>
    private Dictionary<string, VFXEntry> vfxDictionary = new Dictionary<string, VFXEntry>();

    /// <summary>活跃特效列表 - 追踪所有正在播放的特效</summary>
    private List<ActiveVFX> activeVFXList = new List<ActiveVFX>();

    /// <summary>是否已初始化</summary>
    private bool isInitialized = false;

    /// <summary>游戏是否暂停中</summary>
    private bool isPaused = false;

    /// <summary>自动回收协程引用</summary>
    private Coroutine autoReturnCoroutine;

    #endregion

    #region 公共属性

    /// <summary>当前正在播放的特效数量</summary>
    public int ActiveVFXCount => activeVFXList.Count;

    /// <summary>对象池中空闲特效总数</summary>
    public int PooledVFXCount
    {
        get
        {
            int count = 0;
            foreach (var entry in vfxEntries)
            {
                if (!string.IsNullOrEmpty(entry.name) && ObjectPoolManager.HasInstance)
                    count += ObjectPoolManager.Instance.GetAvailableCount(entry.name);
            }
            return count;
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 单例初始化回调 - 由Singleton基类在Awake时调用
    /// </summary>
    protected override void OnSingletonAwake()
    {
        InitializeManager();
    }

    /// <summary>
    /// 初始化特效管理器
    /// </summary>
    private void InitializeManager()
    {
        if (isInitialized) return;

        // 构建特效字典
        BuildVFXDictionary();

        // 订阅EventBus事件
        SubscribeEvents();

        // 启动自动回收协程
        StartAutoReturnCoroutine();

        isInitialized = true;
        DebugLog("[VFXManager] 特效管理器初始化完成");
    }

    /// <summary>
    /// 构建特效字典 - 将Inspector配置的列表转换为字典，便于快速查找
    /// </summary>
    private void BuildVFXDictionary()
    {
        vfxDictionary.Clear();

        foreach (var entry in vfxEntries)
        {
            if (string.IsNullOrEmpty(entry.name) || entry.prefab == null)
                continue;

            if (vfxDictionary.ContainsKey(entry.name))
            {
                Debug.LogWarning("[VFXManager] 特效名称重复: " + entry.name);
                continue;
            }

            vfxDictionary[entry.name] = entry;
        }

        DebugLog("[VFXManager] 已加载 " + vfxDictionary.Count + " 个特效配置");
    }

    #endregion

    #region EventBus事件订阅

    /// <summary>
    /// 订阅所有需要监听的EventBus事件
    /// </summary>
    private void SubscribeEvents()
    {
        // 战斗事件
        EventBus.Subscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Subscribe("ON_ENEMY_HIT", OnEnemyHit);
        EventBus.Subscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Subscribe("ON_ATTACK_ANIM_START", OnAttackAnimStart);
        EventBus.Subscribe("ON_ATTACK_ANIM_END", OnAttackAnimEnd);

        // 游戏状态事件
        EventBus.Subscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Subscribe("ON_GAME_RESUME", OnGameResume);

        // Phase6: 扩展特效事件
        EventBus.Subscribe("ON_WEAPON_CHANGE", OnWeaponChange);
        EventBus.Subscribe("ON_ARROW_FIRE", OnArrowFire);
        EventBus.Subscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);

        // Phase7: 技能特效事件
        EventBus.Subscribe("ON_SKILL_CAST", OnSkillCast);

        // Phase9: Buff特效事件
        EventBus.Subscribe("ON_BUFF_ADD", OnBuffAdd);

        // Phase11: Boss事件
        EventBus.Subscribe(BossAI.ON_BOSS_PHASE_CHANGE, OnBossPhaseChange);
        EventBus.Subscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);

        // Phase13: 物品事件
        EventBus.Subscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Subscribe(Inventory.ON_ITEM_USE, OnItemUse);

        // Phase14: 新敌人事件
        EventBus.Subscribe("ON_ENEMY_EXPLODE", OnEnemyExplode);
        EventBus.Subscribe("ON_MAGE_CAST", OnMageCast);

        // Phase15: 装备事件
        EventBus.Subscribe(EquipmentManager.ON_EQUIP, OnEquip);
        EventBus.Subscribe(EquipmentManager.ON_UNEQUIP, OnUnequip);

        // Phase16: 商店事件
        EventBus.Subscribe("ON_SHOP_BUY", OnShopBuy);
        EventBus.Subscribe("ON_SHOP_SELL", OnShopSell);

        // Phase17: 强化事件
        EventBus.Subscribe(EnhanceManager.ON_EQUIP_ENHANCE_SUCCESS, OnEnhanceSuccess);
        EventBus.Subscribe(EnhanceManager.ON_EQUIP_ENHANCE_FAIL, OnEnhanceFail);

        // Phase18: 成就事件
        EventBus.Subscribe(AchievementManager.ON_ACHIEVEMENT_UNLOCK, OnAchievementUnlock);

        // Phase19: 任务事件
        EventBus.Subscribe(QuestManager.ON_QUEST_COMPLETE, OnQuestComplete);
    }

    /// <summary>
    /// 取消所有EventBus事件订阅 - 防止内存泄漏
    /// </summary>
    private void UnsubscribeEvents()
    {
        EventBus.Unsubscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Unsubscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Unsubscribe("ON_ENEMY_HIT", OnEnemyHit);
        EventBus.Unsubscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Unsubscribe("ON_ATTACK_ANIM_START", OnAttackAnimStart);
        EventBus.Unsubscribe("ON_ATTACK_ANIM_END", OnAttackAnimEnd);

        EventBus.Unsubscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Unsubscribe("ON_GAME_RESUME", OnGameResume);

        EventBus.Unsubscribe("ON_WEAPON_CHANGE", OnWeaponChange);
        EventBus.Unsubscribe("ON_ARROW_FIRE", OnArrowFire);
        EventBus.Unsubscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);

        EventBus.Unsubscribe("ON_SKILL_CAST", OnSkillCast);

        EventBus.Unsubscribe("ON_BUFF_ADD", OnBuffAdd);

        EventBus.Unsubscribe(BossAI.ON_BOSS_PHASE_CHANGE, OnBossPhaseChange);
        EventBus.Unsubscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);

        EventBus.Unsubscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Unsubscribe(Inventory.ON_ITEM_USE, OnItemUse);

        EventBus.Unsubscribe("ON_ENEMY_EXPLODE", OnEnemyExplode);
        EventBus.Unsubscribe("ON_MAGE_CAST", OnMageCast);

        EventBus.Unsubscribe(EquipmentManager.ON_EQUIP, OnEquip);
        EventBus.Unsubscribe(EquipmentManager.ON_UNEQUIP, OnUnequip);

        EventBus.Unsubscribe("ON_SHOP_BUY", OnShopBuy);
        EventBus.Unsubscribe("ON_SHOP_SELL", OnShopSell);

        EventBus.Unsubscribe(EnhanceManager.ON_EQUIP_ENHANCE_SUCCESS, OnEnhanceSuccess);
        EventBus.Unsubscribe(EnhanceManager.ON_EQUIP_ENHANCE_FAIL, OnEnhanceFail);

        EventBus.Unsubscribe(AchievementManager.ON_ACHIEVEMENT_UNLOCK, OnAchievementUnlock);

        EventBus.Unsubscribe(QuestManager.ON_QUEST_COMPLETE, OnQuestComplete);
    }

    #endregion

    #region EventBus事件处理

    /// <summary>
    /// 玩家受击事件 - 播放受击特效
    /// </summary>
    private void OnPlayerHit()
    {
        // 实际项目中应通过事件参数获取受击位置
        // 此处演示基础框架
        DebugLog("[VFXManager] 玩家受击事件");
    }

    /// <summary>
    /// 玩家死亡事件 - 播放死亡特效
    /// </summary>
    private void OnPlayerDie()
    {
        DebugLog("[VFXManager] 玩家死亡事件");
    }

    /// <summary>
    /// 敌人受击事件 - 播放受击特效
    /// </summary>
    private void OnEnemyHit()
    {
        DebugLog("[VFXManager] 敌人受击事件");
    }

    /// <summary>
    /// 敌人死亡事件 - 播放死亡特效
    /// </summary>
    private void OnEnemyDie()
    {
        DebugLog("[VFXManager] 敌人死亡事件");
    }

    /// <summary>
    /// 攻击动画开始事件 - 可播放武器拖尾等特效
    /// </summary>
    private void OnAttackAnimStart()
    {
        DebugLog("[VFXManager] 攻击动画开始");
    }

    /// <summary>
    /// 攻击动画结束事件 - 停止相关特效
    /// </summary>
    private void OnAttackAnimEnd()
    {
        DebugLog("[VFXManager] 攻击动画结束");
    }

    /// <summary>
    /// 游戏暂停事件 - 暂停所有粒子播放
    /// </summary>
    private void OnGamePause()
    {
        isPaused = true;
        PauseAllParticles();
        DebugLog("[VFXManager] 游戏暂停，暂停所有粒子");
    }

    /// <summary>
    /// 游戏恢复事件 - 恢复所有粒子播放
    /// </summary>
    private void OnGameResume()
    {
        isPaused = false;
        ResumeAllParticles();
        DebugLog("[VFXManager] 游戏恢复，恢复所有粒子");
    }

    // Phase6: 扩展特效事件处理

    /// <summary>
    /// 换武器闪光特效
    /// </summary>
    private void OnWeaponChange()
    {
        PlayFX("WeaponSwitchFlash", Vector3.zero);
        DebugLog("[VFXManager] 换武器闪光特效");
    }

    /// <summary>
    /// 箭矢命中特效
    /// </summary>
    private void OnArrowFire()
    {
        DebugLog("[VFXManager] 箭矢发射事件");
    }

    /// <summary>
    /// 关卡通关特效
    /// </summary>
    private void OnLevelComplete()
    {
        PlayFX("LevelCompleteEffect", Vector3.up * 3f);
        DebugLog("[VFXManager] 关卡通关特效");
    }

    /// <summary>
    /// 玩家重生特效
    /// </summary>
    private void OnPlayerRespawn()
    {
        PlayFX("PlayerRespawnEffect", Vector3.up);
        DebugLog("[VFXManager] 玩家重生特效");
    }

    // Phase7: 技能特效事件处理

    /// <summary>
    /// 技能释放特效
    /// </summary>
    private void OnSkillCast(object data)
    {
        if (data is System.Collections.Generic.Dictionary<string, object> skillData)
        {
            string skillName = skillData.ContainsKey("skillName") ? skillData["skillName"].ToString() : "";
            switch (skillName)
            {
                case "WhirlwindSlash":
                    PlayFX("SkillWhirlwindEffect", transform.position);
                    break;
                case "DashStrike":
                    PlayFX("SkillDashEffect", transform.position);
                    break;
                case "EnergyBall":
                    PlayFX("SkillEnergyBallEffect", transform.position);
                    break;
                default:
                    PlayFX("SkillGenericEffect", transform.position);
                    break;
            }
            DebugLog("[VFXManager] 技能特效: " + skillName);
        }
    }

    // Phase9: Buff特效事件处理

    /// <summary>
    /// Buff添加特效
    /// </summary>
    private void OnBuffAdd()
    {
        PlayFX("BuffApplyEffect", transform.position);
        DebugLog("[VFXManager] Buff添加特效");
    }

    // Phase11: Boss特效事件处理

    private void OnBossPhaseChange(object data)
    {
        PlayFX("BossPhaseChangeEffect", transform.position);
        DebugLog("[VFXManager] Boss阶段切换闪光特效");
    }

    private void OnBossDefeated(object data)
    {
        PlayFX("BossDefeatedEffect", transform.position);
        DebugLog("[VFXManager] Boss击败爆炸特效");
    }

    // Phase13: 物品特效事件处理

    private void OnItemPickup(object data)
    {
        PlayFX("ItemPickupEffect", transform.position);
        DebugLog("[VFXManager] 拾取闪光特效");
    }

    private void OnItemUse(object data)
    {
        PlayFX("ItemUseEffect", transform.position);
        DebugLog("[VFXManager] 使用物品特效");
    }

    // Phase14: 新敌人特效事件处理

    private void OnEnemyExplode(object data)
    {
        PlayFX("EnemyExplodeEffect", transform.position);
        DebugLog("[VFXManager] 自爆范围爆炸特效");
    }

    private void OnMageCast(object data)
    {
        PlayFX("MageCastEffect", transform.position);
        DebugLog("[VFXManager] 法师施法特效");
    }

    // Phase15: 装备特效事件处理

    private void OnEquip(object data)
    {
        PlayFX("EquipEffect", transform.position);
        DebugLog("[VFXManager] 装备穿戴闪光特效");
    }

    private void OnUnequip(object data)
    {
        PlayFX("UnequipEffect", transform.position);
        DebugLog("[VFXManager] 卸下装备特效");
    }

    // Phase16: 商店特效事件处理

    private void OnShopBuy(object data)
    {
        PlayFX("ShopBuyEffect", transform.position);
        DebugLog("[VFXManager] 购买闪光特效");
    }

    private void OnShopSell(object data)
    {
        PlayFX("ShopSellEffect", transform.position);
        DebugLog("[VFXManager] 出售物品特效");
    }

    // Phase17: 强化特效事件处理

    private void OnEnhanceSuccess(object data)
    {
        PlayFX("EnhanceSuccessEffect", transform.position);
        DebugLog("[VFXManager] 金色闪光特效");
    }

    private void OnEnhanceFail(object data)
    {
        PlayFX("EnhanceFailEffect", transform.position);
        DebugLog("[VFXManager] 灰色烟雾特效");
    }

    // Phase18: 成就特效事件处理

    private void OnAchievementUnlock(object data)
    {
        PlayFX("AchievementUnlockEffect", transform.position);
        DebugLog("[VFXManager] 金色弹窗闪光特效");
    }

    // Phase19: 任务特效事件处理

    private void OnQuestComplete(object data)
    {
        PlayFX("QuestCompleteEffect", transform.position);
        DebugLog("[VFXManager] 任务完成闪光特效");
    }

    #endregion

    #region 粒子暂停/恢复

    /// <summary>
    /// 暂停所有活跃粒子的播放
    /// </summary>
    private void PauseAllParticles()
    {
        foreach (var activeVFX in activeVFXList)
        {
            if (activeVFX.particleSystem != null && activeVFX.gameObject.activeInHierarchy)
            {
                activeVFX.particleSystem.Pause(true);
            }
        }
    }

    /// <summary>
    /// 恢复所有活跃粒子的播放
    /// </summary>
    private void ResumeAllParticles()
    {
        foreach (var activeVFX in activeVFXList)
        {
            if (activeVFX.particleSystem != null && activeVFX.gameObject.activeInHierarchy)
            {
                activeVFX.particleSystem.Play(true);
            }
        }
    }

    #endregion

    #region 对外公共API - 播放特效

    /// <summary>
    /// 在指定位置播放特效
    /// </summary>
    /// <param name="vfxName">特效名称（需在特效字典中配置）</param>
    /// <param name="position">播放位置</param>
    /// <param name="rotation">播放旋转</param>
    /// <param name="customScale">自定义缩放（-1表示使用默认缩放）</param>
    /// <returns>播放的特效对象，失败返回null</returns>
    public GameObject PlayFX(string vfxName, Vector3 position, Quaternion rotation, float customScale = -1f)
    {
        // 验证特效是否存在
        if (!vfxDictionary.TryGetValue(vfxName, out VFXEntry entry))
        {
            Debug.LogWarning("[VFXManager] 找不到特效: " + vfxName);
            return null;
        }

        // 检查ObjectPoolManager是否存在
        if (!ObjectPoolManager.HasInstance)
        {
            Debug.LogWarning("[VFXManager] ObjectPoolManager不存在");
            return null;
        }

        // 从ObjectPoolManager获取可用实例
        GameObject vfxObj = ObjectPoolManager.Instance.Get(vfxName, position, rotation);
        if (vfxObj == null)
        {
            Debug.LogWarning("[VFXManager] 对象池获取失败: " + vfxName);
            return null;
        }

        // 设置缩放
        float finalScale = customScale > 0 ? customScale : entry.scale;
        vfxObj.transform.localScale = Vector3.one * finalScale;

        // 播放粒子系统
        ParticleSystem ps = vfxObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(true);

            // 如果游戏暂停中，立即暂停这个粒子
            if (isPaused)
                ps.Pause(true);
        }

        // 计算持续时长
        float duration = entry.duration > 0 ? entry.duration : GetParticleDuration(ps);

        // 添加到活跃列表
        ActiveVFX activeVFX = new ActiveVFX
        {
            gameObject = vfxObj,
            vfxName = vfxName,
            particleSystem = ps,
            duration = duration,
            elapsedTime = 0f
        };
        activeVFXList.Add(activeVFX);

        DebugLog("[VFXManager] 播放特效: " + vfxName);
        return vfxObj;
    }

    /// <summary>
    /// 在指定位置播放特效（使用默认旋转）
    /// </summary>
    /// <param name="vfxName">特效名称</param>
    /// <param name="position">播放位置</param>
    /// <returns>播放的特效对象</returns>
    public GameObject PlayFX(string vfxName, Vector3 position)
    {
        return PlayFX(vfxName, position, Quaternion.identity);
    }

    /// <summary>
    /// 在目标对象上播放特效（带位置偏移）
    /// </summary>
    /// <param name="vfxName">特效名称</param>
    /// <param name="target">目标Transform</param>
    /// <param name="offset">位置偏移量</param>
    /// <returns>播放的特效对象</returns>
    public GameObject PlayFXAtTarget(string vfxName, Transform target, Vector3 offset)
    {
        if (target == null)
        {
            Debug.LogWarning("[VFXManager] 目标对象为空");
            return null;
        }

        Vector3 position = target.position + offset;
        Quaternion rotation = target.rotation;
        return PlayFX(vfxName, position, rotation);
    }

    /// <summary>
    /// 在目标对象上播放特效（无偏移）
    /// </summary>
    /// <param name="vfxName">特效名称</param>
    /// <param name="target">目标Transform</param>
    /// <returns>播放的特效对象</returns>
    public GameObject PlayFXAtTarget(string vfxName, Transform target)
    {
        return PlayFXAtTarget(vfxName, target, Vector3.zero);
    }

    #endregion

    #region 对外公共API - 回收与停止

    /// <summary>
    /// 手动将特效回收到对象池
    /// </summary>
    /// <param name="vfxObj">要回收的特效对象</param>
    public void ReturnVFXToPool(GameObject vfxObj)
    {
        if (vfxObj == null) return;

        // 从活跃列表中移除
        for (int i = activeVFXList.Count - 1; i >= 0; i--)
        {
            if (activeVFXList[i].gameObject == vfxObj)
            {
                activeVFXList.RemoveAt(i);
                break;
            }
        }

        // 停止粒子系统
        ParticleSystem ps = vfxObj.GetComponent<ParticleSystem>();
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 获取特效名称
        string vfxName = ExtractVFXName(vfxObj);

        // 回收到ObjectPoolManager
        if (!string.IsNullOrEmpty(vfxName) && ObjectPoolManager.HasInstance)
        {
            ObjectPoolManager.Instance.Recycle(vfxName, vfxObj);
            DebugLog("[VFXManager] 回收特效到池: " + vfxName);
        }
        else
        {
            Destroy(vfxObj);
        }
    }

    /// <summary>
    /// 停止指定名称的所有特效
    /// </summary>
    /// <param name="vfxName">特效名称</param>
    public void StopVFX(string vfxName)
    {
        for (int i = activeVFXList.Count - 1; i >= 0; i--)
        {
            if (activeVFXList[i].vfxName == vfxName)
            {
                ReturnVFXToPool(activeVFXList[i].gameObject);
            }
        }
    }

    /// <summary>
    /// 清空所有特效 - 回收活跃特效到对象池
    /// </summary>
    public void ClearAllVFX()
    {
        // 回收所有活跃特效
        foreach (var activeVFX in activeVFXList)
        {
            if (activeVFX.gameObject != null)
            {
                if (activeVFX.particleSystem != null)
                    activeVFX.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (ObjectPoolManager.HasInstance && !string.IsNullOrEmpty(activeVFX.vfxName))
                    ObjectPoolManager.Instance.Recycle(activeVFX.vfxName, activeVFX.gameObject);
                else
                    Destroy(activeVFX.gameObject);
            }
        }
        activeVFXList.Clear();

        DebugLog("[VFXManager] 已清空所有特效");
    }

    #endregion

    #region 对外公共API - 动态添加

    /// <summary>
    /// 运行时动态添加新特效到字典
    /// </summary>
    /// <param name="name">特效名称</param>
    /// <param name="prefab">特效预制体</param>
    /// <param name="duration">持续时长（0使用粒子默认）</param>
    /// <param name="scale">缩放倍率</param>
    public void AddVFX(string name, GameObject prefab, float duration = 0f, float scale = 1f)
    {
        if (string.IsNullOrEmpty(name) || prefab == null)
        {
            Debug.LogWarning("[VFXManager] AddVFX 参数无效");
            return;
        }

        VFXEntry entry = new VFXEntry
        {
            name = name,
            prefab = prefab,
            duration = duration,
            scale = scale
        };

        vfxDictionary[name] = entry;
        vfxEntries.Add(entry);

        DebugLog("[VFXManager] 动态添加特效: " + name);
    }

    #endregion

    #region 对象池内部操作

    /// <summary>
    /// 从游戏对象名称中提取特效名称
    /// 命名规则: "VFX_特效名称"
    /// </summary>
    /// <param name="vfxObj">特效对象</param>
    /// <returns>特效名称</returns>
    private string ExtractVFXName(GameObject vfxObj)
    {
        string objName = vfxObj.name;
        if (objName.StartsWith("VFX_"))
            return objName.Substring(4);
        return objName;
    }

    /// <summary>
    /// 获取粒子系统的实际持续时长
    /// </summary>
    /// <param name="ps">粒子系统</param>
    /// <returns>持续时长（秒）</returns>
    private float GetParticleDuration(ParticleSystem ps)
    {
        if (ps == null) return 1f;
        return ps.main.duration + ps.main.startLifetime.constantMax;
    }

    #endregion

    #region 自动回收协程

    /// <summary>
    /// 启动自动回收协程
    /// </summary>
    private void StartAutoReturnCoroutine()
    {
        StopAutoReturnCoroutine();
        autoReturnCoroutine = StartCoroutine(AutoReturnCoroutine());
    }

    /// <summary>
    /// 停止自动回收协程
    /// </summary>
    private void StopAutoReturnCoroutine()
    {
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
            autoReturnCoroutine = null;
        }
    }

    /// <summary>
    /// 自动回收协程 - 定期检查已完成的特效并回收
    /// </summary>
    private System.Collections.IEnumerator AutoReturnCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            // 暂停时不检查，避免时间累积问题
            if (isPaused) continue;

            // 从后往前遍历，安全移除已完成的特效
            for (int i = activeVFXList.Count - 1; i >= 0; i--)
            {
                ActiveVFX activeVFX = activeVFXList[i];

                // 对象已被外部销毁
                if (activeVFX.gameObject == null)
                {
                    activeVFXList.RemoveAt(i);
                    continue;
                }

                // 累加已播放时间
                activeVFX.elapsedTime += 0.5f;

                // 检查粒子系统是否已停止
                bool isFinished = false;
                if (activeVFX.particleSystem != null)
                {
                    // 检查粒子系统是否还在播放
                    isFinished = !activeVFX.particleSystem.IsAlive(true);
                }
                else
                {
                    // 没有粒子系统，使用时间判断
                    isFinished = activeVFX.elapsedTime >= activeVFX.duration;
                }

                // 已完成则回收
                if (isFinished)
                    ReturnVFXToPool(activeVFX.gameObject);
            }
        }
    }

    #endregion

    #region 生命周期清理

    /// <summary>
    /// 销毁时取消所有EventBus订阅，防止内存泄漏
    /// </summary>
    protected override void OnDestroy()
    {
        // 取消所有EventBus订阅
        UnsubscribeEvents();

        // 停止自动回收协程
        StopAutoReturnCoroutine();

        // 清空所有特效
        ClearAllVFX();

        base.OnDestroy();
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 输出调试日志（仅在启用调试时输出）
    /// </summary>
    /// <param name="message">日志内容</param>
    private void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }

    #endregion
}
