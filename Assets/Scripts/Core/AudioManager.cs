using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 音频管理器 - 全局音效与背景音乐控制中心
/// 继承自Singleton泛型单例基类，管理游戏中所有音频播放
/// BGM使用独立AudioSource轨道，音效采用ObjectPoolManager统一对象池
/// 通过EventBus监听战斗与游戏状态事件，自动播放对应音效
/// 支持游戏暂停时冻结所有音频播放
/// WebGL平台兼容，纯单线程实现
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    #region 音效条目配置

    /// <summary>
    /// 音效条目 - 定义一种音效的配置信息
    /// </summary>
    [System.Serializable]
    public class SFXEntry
    {
        [Tooltip("音效唯一标识名称")]
        public string name;

        [Tooltip("音频片段")]
        public AudioClip clip;

        [Tooltip("默认音量（0-1）")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("默认音调（0.1-3）")]
        [Range(0.1f, 3f)]
        public float pitch = 1f;

        [Tooltip("是否循环播放")]
        public bool loop = false;
    }

    #endregion

    #region 活跃音效追踪

    /// <summary>
    /// 活跃音效数据 - 追踪正在播放的音效实例
    /// </summary>
    private class ActiveSFX
    {
        public AudioSource audioSource;
        public string sfxName;
        public float duration;
        public float elapsedTime;
    }

    #endregion

    #region Inspector可配置参数

    [Header("BGM背景音乐")]
    [Tooltip("BGM专用AudioSource（独立轨道）")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("BGM淡入时长（秒）")]
    [SerializeField] private float bgmFadeInDuration = 0.5f;

    [Tooltip("BGM淡出时长（秒）")]
    [SerializeField] private float bgmFadeOutDuration = 0.5f;

    [Header("音效字典配置")]
    [Tooltip("音效条目列表 - 配置所有可用音效的音频片段和参数")]
    [SerializeField] private List<SFXEntry> sfxEntries = new List<SFXEntry>();

    [Header("音量控制")]
    [Tooltip("BGM背景音乐音量（0-1）")]
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.7f;

    [Tooltip("SFX音效音量（0-1）")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("调试设置")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = false;

    #endregion

    #region 私有变量

    private Dictionary<string, SFXEntry> sfxDictionary = new Dictionary<string, SFXEntry>();
    private List<ActiveSFX> activeSFXList = new List<ActiveSFX>();
    private bool isInitialized = false;
    private bool isPaused = false;
    private Coroutine autoReturnCoroutine;
    private Coroutine bgmFadeCoroutine;
    private float cachedBGMVolume = 0.7f;

    #endregion

    #region 公共属性

    public float BGMVolume
    {
        get => bgmVolume;
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            cachedBGMVolume = bgmVolume;
            if (bgmSource != null)
                bgmSource.volume = bgmVolume;
        }
    }

    public float SFXVolume
    {
        get => sfxVolume;
        set => sfxVolume = Mathf.Clamp01(value);
    }

    public int ActiveSFXCount => activeSFXList.Count;
    public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;

    #endregion

    #region 初始化

    protected override void OnSingletonAwake()
    {
        InitializeManager();
    }

    private void InitializeManager()
    {
        if (isInitialized) return;

        if (bgmSource == null)
        {
            GameObject bgmObj = new GameObject("BGM_Source");
            bgmObj.transform.SetParent(transform);
            bgmSource = bgmObj.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;

        BuildSFXDictionary();
        SubscribeEvents();
        StartAutoReturnCoroutine();

        isInitialized = true;
        DebugLog("[AudioManager] 音频管理器初始化完成");
    }

    private void BuildSFXDictionary()
    {
        sfxDictionary.Clear();

        foreach (var entry in sfxEntries)
        {
            if (string.IsNullOrEmpty(entry.name) || entry.clip == null)
                continue;

            if (sfxDictionary.ContainsKey(entry.name))
            {
                Debug.LogWarning("[AudioManager] 音效名称重复: " + entry.name);
                continue;
            }

            sfxDictionary[entry.name] = entry;
        }

        DebugLog("[AudioManager] 已加载 " + sfxDictionary.Count + " 个音效配置");
    }

    #endregion

    #region EventBus事件订阅

    private void SubscribeEvents()
    {
        EventBus.Subscribe("ON_PLAYER_HIT", OnPlayerHit);
        EventBus.Subscribe("ON_PLAYER_DIE", OnPlayerDie);
        EventBus.Subscribe("ON_ENEMY_HIT", OnEnemyHit);
        EventBus.Subscribe("ON_ENEMY_DIE", OnEnemyDie);
        EventBus.Subscribe("ON_ATTACK_ANIM_START", OnAttackAnimStart);
        EventBus.Subscribe("ON_ATTACK_ANIM_END", OnAttackAnimEnd);
        EventBus.Subscribe("ON_GAME_PAUSE", OnGamePause);
        EventBus.Subscribe("ON_GAME_RESUME", OnGameResume);

        // Phase6: 扩展音效事件
        EventBus.Subscribe("ON_WEAPON_CHANGE", OnWeaponChange);
        EventBus.Subscribe("ON_ARROW_FIRE", OnArrowFire);
        EventBus.Subscribe("ON_LEVEL_START", OnLevelStart);
        EventBus.Subscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
        EventBus.Subscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);

        // Phase7: 技能音效事件
        EventBus.Subscribe("ON_SKILL_CAST", OnSkillCast);
        EventBus.Subscribe("ON_SKILL_END", OnSkillEnd);

        // Phase9: Buff事件
        EventBus.Subscribe("ON_BUFF_ADD", OnBuffAdd);
        EventBus.Subscribe("ON_BUFF_REMOVE", OnBuffRemove);

        // Phase11: Boss事件
        EventBus.Subscribe(BossAI.ON_BOSS_PHASE_CHANGE, OnBossPhaseChange);
        EventBus.Subscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);

        // Phase13: 物品事件
        EventBus.Subscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Subscribe(Inventory.ON_ITEM_USE, OnItemUse);

        // Phase14: 新敌人事件
        EventBus.Subscribe("ON_ENEMY_EXPLODE", OnEnemyExplode);
        EventBus.Subscribe("ON_ENEMY_EXPLODE_WARNING", OnEnemyExplodeWarning);
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

        // Phase6: 扩展音效事件
        EventBus.Unsubscribe("ON_WEAPON_CHANGE", OnWeaponChange);
        EventBus.Unsubscribe("ON_ARROW_FIRE", OnArrowFire);
        EventBus.Unsubscribe("ON_LEVEL_START", OnLevelStart);
        EventBus.Unsubscribe("ON_LEVEL_COMPLETE", OnLevelComplete);
        EventBus.Unsubscribe("ON_PLAYER_RESPAWN", OnPlayerRespawn);

        // Phase7: 技能音效事件
        EventBus.Unsubscribe("ON_SKILL_CAST", OnSkillCast);
        EventBus.Unsubscribe("ON_SKILL_END", OnSkillEnd);

        // Phase9: Buff事件
        EventBus.Unsubscribe("ON_BUFF_ADD", OnBuffAdd);
        EventBus.Unsubscribe("ON_BUFF_REMOVE", OnBuffRemove);

        EventBus.Unsubscribe(BossAI.ON_BOSS_PHASE_CHANGE, OnBossPhaseChange);
        EventBus.Unsubscribe(BossAI.ON_BOSS_DEFEATED, OnBossDefeated);

        EventBus.Unsubscribe(Inventory.ON_ITEM_PICKUP, OnItemPickup);
        EventBus.Unsubscribe(Inventory.ON_ITEM_USE, OnItemUse);

        EventBus.Unsubscribe("ON_ENEMY_EXPLODE", OnEnemyExplode);
        EventBus.Unsubscribe("ON_ENEMY_EXPLODE_WARNING", OnEnemyExplodeWarning);
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

    private void OnPlayerHit()
    {
        PlayOneShot("PlayerHurt");
        DebugLog("[AudioManager] 玩家受击音效");
    }

    private void OnPlayerDie()
    {
        PlayOneShot("PlayerDeath");
        DebugLog("[AudioManager] 玩家死亡音效");
    }

    private void OnEnemyHit()
    {
        PlayOneShot("EnemyHurt");
        DebugLog("[AudioManager] 敌人受击音效");
    }

    private void OnEnemyDie()
    {
        PlayOneShot("EnemyDeath");
        DebugLog("[AudioManager] 敌人死亡音效");
    }

    private void OnAttackAnimStart()
    {
        PlayOneShot("AttackSwing");
        DebugLog("[AudioManager] 攻击挥舞音效");
    }

    private void OnAttackAnimEnd()
    {
        DebugLog("[AudioManager] 攻击结束");
    }

    private void OnGamePause()
    {
        isPaused = true;
        PauseAllAudio();
        DebugLog("[AudioManager] 游戏暂停，暂停所有音频");
    }

    private void OnGameResume()
    {
        isPaused = false;
        ResumeAllAudio();
        DebugLog("[AudioManager] 游戏恢复，恢复所有音频");
    }

    // Phase6: 扩展音效事件处理

    /// <summary>
    /// 换武器音效
    /// </summary>
    private void OnWeaponChange()
    {
        PlayOneShot("WeaponSwitch");
        DebugLog("[AudioManager] 换武器音效");
    }

    /// <summary>
    /// 射箭音效
    /// </summary>
    private void OnArrowFire()
    {
        PlayOneShot("ArrowFire");
        DebugLog("[AudioManager] 射箭音效");
    }

    /// <summary>
    /// 关卡开始音效
    /// </summary>
    private void OnLevelStart()
    {
        PlayOneShot("LevelStart");
        DebugLog("[AudioManager] 关卡开始音效");
    }

    /// <summary>
    /// 关卡通关音效
    /// </summary>
    private void OnLevelComplete()
    {
        PlayOneShot("LevelComplete");
        DebugLog("[AudioManager] 关卡通关音效");
    }

    /// <summary>
    /// 玩家重生音效
    /// </summary>
    private void OnPlayerRespawn()
    {
        PlayOneShot("PlayerRespawn");
        DebugLog("[AudioManager] 玩家重生音效");
    }

    // Phase7: 技能音效事件处理

    /// <summary>
    /// 技能释放音效
    /// </summary>
    private void OnSkillCast(object data)
    {
        if (data is System.Collections.Generic.Dictionary<string, object> skillData)
        {
            string skillName = skillData.ContainsKey("skillName") ? skillData["skillName"].ToString() : "";
            switch (skillName)
            {
                case "WhirlwindSlash":
                    PlayOneShot("SkillWhirlwind");
                    break;
                case "DashStrike":
                    PlayOneShot("SkillDash");
                    break;
                default:
                    PlayOneShot("SkillGeneric");
                    break;
            }
            DebugLog("[AudioManager] 技能音效: " + skillName);
        }
    }

    /// <summary>
    /// 技能结束音效
    /// </summary>
    private void OnSkillEnd(object data)
    {
        DebugLog("[AudioManager] 技能结束");
    }

    // Phase9: Buff音效事件处理

    /// <summary>
    /// Buff添加音效
    /// </summary>
    private void OnBuffAdd()
    {
        PlayOneShot("BuffApply");
        DebugLog("[AudioManager] Buff添加音效");
    }

    /// <summary>
    /// Buff移除音效
    /// </summary>
    private void OnBuffRemove()
    {
        DebugLog("[AudioManager] Buff移除");
    }

    // Phase11: Boss音效事件处理

    private void OnBossPhaseChange(object data)
    {
        PlayOneShot("BossPhaseChange");
        DebugLog("[AudioManager] Boss阶段切换音效");
    }

    private void OnBossDefeated(object data)
    {
        PlayOneShot("BossDefeated");
        DebugLog("[AudioManager] Boss击败音效");
    }

    // Phase13: 物品音效事件处理

    private void OnItemPickup(object data)
    {
        PlayOneShot("ItemPickup");
        DebugLog("[AudioManager] 拾取物品音效");
    }

    private void OnItemUse(object data)
    {
        PlayOneShot("ItemUse");
        DebugLog("[AudioManager] 使用物品音效");
    }

    // Phase14: 新敌人音效事件处理

    private void OnEnemyExplode(object data)
    {
        PlayOneShot("EnemyExplode");
        DebugLog("[AudioManager] 自爆爆炸音效");
    }

    private void OnEnemyExplodeWarning(object data)
    {
        PlayOneShot("EnemyExplodeWarning");
        DebugLog("[AudioManager] 自爆预警音效");
    }

    private void OnMageCast(object data)
    {
        PlayOneShot("MageCast");
        DebugLog("[AudioManager] 法师施法音效");
    }

    // Phase15: 装备音效事件处理

    private void OnEquip(object data)
    {
        PlayOneShot("EquipItem");
        DebugLog("[AudioManager] 穿戴装备音效");
    }

    private void OnUnequip(object data)
    {
        PlayOneShot("UnequipItem");
        DebugLog("[AudioManager] 卸下装备音效");
    }

    // Phase16: 商店音效事件处理

    private void OnShopBuy(object data)
    {
        PlayOneShot("ShopBuy");
        DebugLog("[AudioManager] 购买物品音效");
    }

    private void OnShopSell(object data)
    {
        PlayOneShot("ShopSell");
        DebugLog("[AudioManager] 出售物品音效");
    }

    // Phase17: 强化音效事件处理

    private void OnEnhanceSuccess(object data)
    {
        PlayOneShot("EnhanceSuccess");
        DebugLog("[AudioManager] 强化成功音效");
    }

    private void OnEnhanceFail(object data)
    {
        PlayOneShot("EnhanceFail");
        DebugLog("[AudioManager] 强化失败音效");
    }

    // Phase18: 成就音效事件处理

    private void OnAchievementUnlock(object data)
    {
        PlayOneShot("AchievementUnlock");
        DebugLog("[AudioManager] 成就解锁音效");
    }

    // Phase19: 任务音效事件处理

    private void OnQuestComplete(object data)
    {
        PlayOneShot("QuestComplete");
        DebugLog("[AudioManager] 任务完成音效");
    }

    #endregion

    #region 对外公共API - 播放音效

    public void PlayOneShot(string sfxName, float volumeScale = 1f)
    {
        if (!sfxDictionary.TryGetValue(sfxName, out SFXEntry entry))
        {
            Debug.LogWarning("[AudioManager] 找不到音效: " + sfxName);
            return;
        }

        if (!ObjectPoolManager.HasInstance)
        {
            Debug.LogWarning("[AudioManager] ObjectPoolManager不存在");
            return;
        }

        GameObject sfxObj = ObjectPoolManager.Instance.Get("AudioSource");
        if (sfxObj == null)
        {
            Debug.LogWarning("[AudioManager] 音效池已满");
            return;
        }

        AudioSource source = sfxObj.GetComponent<AudioSource>();
        if (source == null)
            source = sfxObj.AddComponent<AudioSource>();

        source.clip = entry.clip;
        source.pitch = entry.pitch;
        source.volume = entry.volume * sfxVolume * volumeScale;
        source.loop = entry.loop;

        sfxObj.SetActive(true);
        source.Play();

        float duration = entry.clip.length / entry.pitch;

        ActiveSFX activeSFX = new ActiveSFX
        {
            audioSource = source,
            sfxName = sfxName,
            duration = duration,
            elapsedTime = 0f
        };
        activeSFXList.Add(activeSFX);

        DebugLog("[AudioManager] 播放音效: " + sfxName);
    }

    public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] AudioClip为空");
            return;
        }

        if (!ObjectPoolManager.HasInstance)
        {
            Debug.LogWarning("[AudioManager] ObjectPoolManager不存在");
            return;
        }

        GameObject sfxObj = ObjectPoolManager.Instance.Get("AudioSource");
        if (sfxObj == null)
        {
            Debug.LogWarning("[AudioManager] 音效池已满");
            return;
        }

        AudioSource source = sfxObj.GetComponent<AudioSource>();
        if (source == null)
            source = sfxObj.AddComponent<AudioSource>();

        source.clip = clip;
        source.pitch = 1f;
        source.volume = sfxVolume * volumeScale;
        source.loop = false;

        sfxObj.SetActive(true);
        source.Play();

        ActiveSFX activeSFX = new ActiveSFX
        {
            audioSource = source,
            sfxName = clip.name,
            duration = clip.length,
            elapsedTime = 0f
        };
        activeSFXList.Add(activeSFX);
    }

    public void PlayOneShotAtPosition(string sfxName, Vector3 position, float volumeScale = 1f)
    {
        if (!sfxDictionary.TryGetValue(sfxName, out SFXEntry entry))
        {
            Debug.LogWarning("[AudioManager] 找不到音效: " + sfxName);
            return;
        }

        float finalVolume = entry.volume * sfxVolume * volumeScale;
        AudioSource.PlayClipAtPoint(entry.clip, position, finalVolume);
    }

    #endregion

    #region 对外公共API - BGM控制

    public void PlayBGM(AudioClip clip, float fadeTime = -1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] BGM AudioClip为空");
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;

        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);

        float fadeDuration = fadeTime >= 0 ? fadeTime : bgmFadeInDuration;
        if (fadeDuration > 0)
            bgmFadeCoroutine = StartCoroutine(FadeInBGM(fadeDuration));
        else
            bgmSource.Play();

        DebugLog("[AudioManager] 播放BGM: " + clip.name);
    }

    public void StopBGM(float fadeTime = -1f)
    {
        if (!bgmSource.isPlaying) return;

        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);

        float fadeDuration = fadeTime >= 0 ? fadeTime : bgmFadeOutDuration;
        if (fadeDuration > 0)
            bgmFadeCoroutine = StartCoroutine(FadeOutBGM(fadeDuration));
        else
            bgmSource.Stop();

        DebugLog("[AudioManager] 停止BGM");
    }

    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
            DebugLog("[AudioManager] 暂停BGM");
        }
    }

    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && bgmSource.clip != null)
        {
            bgmSource.UnPause();
            DebugLog("[AudioManager] 恢复BGM");
        }
    }

    #endregion

    #region 对外公共API - 全局控制

    public void PauseAllAudio()
    {
        PauseBGM();
        foreach (var activeSFX in activeSFXList)
        {
            if (activeSFX.audioSource != null && activeSFX.audioSource.isPlaying)
                activeSFX.audioSource.Pause();
        }
    }

    public void ResumeAllAudio()
    {
        ResumeBGM();
        foreach (var activeSFX in activeSFXList)
        {
            if (activeSFX.audioSource != null && !activeSFX.audioSource.isPlaying)
                activeSFX.audioSource.UnPause();
        }
    }

    public void StopAllSFX()
    {
        for (int i = activeSFXList.Count - 1; i >= 0; i--)
        {
            ReturnToPool(activeSFXList[i].audioSource);
        }
        activeSFXList.Clear();
    }

    public void StopSFX(string sfxName)
    {
        for (int i = activeSFXList.Count - 1; i >= 0; i--)
        {
            if (activeSFXList[i].sfxName == sfxName)
            {
                ReturnToPool(activeSFXList[i].audioSource);
                activeSFXList.RemoveAt(i);
            }
        }
    }

    #endregion

    #region 对外公共API - 动态添加

    public void AddSFX(string name, AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(name) || clip == null)
        {
            Debug.LogWarning("[AudioManager] AddSFX 参数无效");
            return;
        }

        SFXEntry entry = new SFXEntry
        {
            name = name,
            clip = clip,
            volume = volume,
            pitch = pitch,
            loop = false
        };

        sfxDictionary[name] = entry;
        sfxEntries.Add(entry);

        DebugLog("[AudioManager] 动态添加音效: " + name);
    }

    #endregion

    #region 对象池内部操作

    private void ReturnToPool(AudioSource source)
    {
        if (source == null) return;

        source.Stop();
        source.clip = null;

        if (ObjectPoolManager.HasInstance)
        {
            ObjectPoolManager.Instance.Recycle("AudioSource", source.gameObject);
        }
        else
        {
            Destroy(source.gameObject);
        }
    }

    #endregion

    #region 音量控制

    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
    }

    public void MuteAll()
    {
        if (bgmSource != null) bgmSource.mute = true;
        foreach (var activeSFX in activeSFXList)
        {
            if (activeSFX.audioSource != null)
                activeSFX.audioSource.mute = true;
        }
    }

    public void UnmuteAll()
    {
        if (bgmSource != null) bgmSource.mute = false;
        foreach (var activeSFX in activeSFXList)
        {
            if (activeSFX.audioSource != null)
                activeSFX.audioSource.mute = false;
        }
    }

    #endregion

    #region BGM淡入淡出协程

    private System.Collections.IEnumerator FadeInBGM(float duration)
    {
        bgmSource.volume = 0f;
        bgmSource.Play();
        float elapsed = 0f;
        float targetVolume = cachedBGMVolume;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }
        bgmSource.volume = targetVolume;
    }

    private System.Collections.IEnumerator FadeOutBGM(float duration)
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Stop();
    }

    #endregion

    #region 自动回收协程

    private void StartAutoReturnCoroutine()
    {
        StopAutoReturnCoroutine();
        autoReturnCoroutine = StartCoroutine(AutoReturnCoroutine());
    }

    private void StopAutoReturnCoroutine()
    {
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
            autoReturnCoroutine = null;
        }
    }

    private System.Collections.IEnumerator AutoReturnCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            if (isPaused) continue;
            for (int i = activeSFXList.Count - 1; i >= 0; i--)
            {
                ActiveSFX activeSFX = activeSFXList[i];
                if (activeSFX.audioSource == null)
                {
                    activeSFXList.RemoveAt(i);
                    continue;
                }
                if (!activeSFX.audioSource.isPlaying)
                {
                    ReturnToPool(activeSFX.audioSource);
                    activeSFXList.RemoveAt(i);
                }
            }
        }
    }

    #endregion

    #region 生命周期清理

    protected override void OnDestroy()
    {
        UnsubscribeEvents();
        StopAutoReturnCoroutine();
        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);
        StopAllSFX();
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
