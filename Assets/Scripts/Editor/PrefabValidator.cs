using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 预制体快速配置校验脚本（Editor专用）
/// 一键校验PlayerPrefab和EnemyPrefab的组件配置
/// 检查：组件缺失、引用丢失、Layer配置、Animator参数
/// 在Inspector面板输出校验报告
/// 仅编辑器运行，不打包进游戏
/// </summary>
[CustomEditor(typeof(PrefabValidatorTarget))]
public class PrefabValidator : Editor
{
    #region 校验结果数据

    /// <summary>
    /// 校验结果条目
    /// </summary>
    private class ValidationEntry
    {
        /// <summary>校验类型（Info/Warning/Error）</summary>
        public LogLevel Level;
        /// <summary>校验消息</summary>
        public string Message;
    }

    /// <summary>日志级别枚举</summary>
    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    #endregion

    #region 私有变量

    /// <summary>校验结果列表</summary>
    private List<ValidationEntry> validationResults = new List<ValidationEntry>();

    /// <summary>是否已执行校验</summary>
    private bool hasValidated = false;

    /// <summary>滚动位置</summary>
    private Vector2 scrollPosition;

    #endregion

    #region Inspector绘制

    /// <summary>
    /// 绘制Inspector面板
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PrefabValidatorTarget validatorTarget = (PrefabValidatorTarget)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("预制体校验工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("点击下方按钮校验预制体配置是否完整", MessageType.Info);

        EditorGUILayout.Space();

        // 校验Player预制体
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("校验 Player Prefab", GUILayout.Height(30)))
        {
            ValidatePrefab(validatorTarget.playerPrefab, "Player");
        }
        EditorGUILayout.EndHorizontal();

        // 校验Enemy预制体
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("校验 Enemy Prefab", GUILayout.Height(30)))
        {
            ValidatePrefab(validatorTarget.enemyPrefab, "Enemy");
        }
        EditorGUILayout.EndHorizontal();

        // 一键校验全部
        EditorGUILayout.Space();
        if (GUILayout.Button("一键校验全部预制体", GUILayout.Height(35)))
        {
            validationResults.Clear();
            ValidatePrefab(validatorTarget.playerPrefab, "Player");
            ValidatePrefab(validatorTarget.enemyPrefab, "Enemy");
            hasValidated = true;
        }

        // 清除结果
        if (hasValidated && GUILayout.Button("清除校验结果", GUILayout.Height(25)))
        {
            validationResults.Clear();
            hasValidated = false;
        }

        // 显示校验结果
        if (hasValidated && validationResults.Count > 0)
        {
            EditorGUILayout.Space();
            DrawValidationResults();
        }
    }

    #endregion

    #region 校验逻辑

    /// <summary>
    /// 校验单个预制体
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="prefabType">预制体类型名称</param>
    private void ValidatePrefab(GameObject prefab, string prefabType)
    {
        if (prefab == null)
        {
            AddResult(LogLevel.Error, $"[{prefabType}] 预制体未设置！");
            return;
        }

        AddResult(LogLevel.Info, $"========== 校验 {prefabType} 预制体: {prefab.name} ==========");

        // 1. 检查基础组件
        ValidateBasicComponents(prefab, prefabType);

        // 2. 检查战斗系统组件
        ValidateCombatSystem(prefab, prefabType);

        // 3. 检查动画组件
        ValidateAnimator(prefab, prefabType);

        // 4. 检查Layer配置
        ValidateLayer(prefab, prefabType);

        // 5. 检查Tag配置
        ValidateTag(prefab, prefabType);

        // 6. 检查特定组件
        if (prefabType == "Player")
        {
            ValidatePlayerComponents(prefab);
        }
        else if (prefabType == "Enemy")
        {
            ValidateEnemyComponents(prefab);
        }

        AddResult(LogLevel.Info, $"========== {prefabType} 校验完成 ==========");
    }

    /// <summary>
    /// 校验基础组件
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="prefabType">预制体类型</param>
    private void ValidateBasicComponents(GameObject prefab, string prefabType)
    {
        // CharacterController
        CharacterController cc = prefab.GetComponent<CharacterController>();
        if (cc == null)
        {
            AddResult(LogLevel.Error, $"[{prefabType}] 缺少 CharacterController 组件");
        }
        else
        {
            AddResult(LogLevel.Info, $"[{prefabType}] CharacterController ✓ (高度:{cc.height}, 半径:{cc.radius})");
        }
    }

    /// <summary>
    /// 校验战斗系统组件
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="prefabType">预制体类型</param>
    private void ValidateCombatSystem(GameObject prefab, string prefabType)
    {
        CombatSystem combat = prefab.GetComponent<CombatSystem>();
        if (combat == null)
        {
            AddResult(LogLevel.Error, $"[{prefabType}] 缺少 CombatSystem 组件");
        }
        else
        {
            AddResult(LogLevel.Info, $"[{prefabType}] CombatSystem ✓ (最大生命值:{combat.MaxHealth})");
        }
    }

    /// <summary>
    /// 校验动画组件
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="prefabType">预制体类型</param>
    private void ValidateAnimator(GameObject prefab, string prefabType)
    {
        Animator animator = prefab.GetComponent<Animator>();
        if (animator == null)
        {
            AddResult(LogLevel.Warning, $"[{prefabType}] 缺少 Animator 组件（动画将不可用）");
        }
        else
        {
            AddResult(LogLevel.Info, $"[{prefabType}] Animator ✓");

            // 检查Animator Controller
            if (animator.runtimeAnimatorController == null)
            {
                AddResult(LogLevel.Warning, $"[{prefabType}] Animator 未设置 AnimatorController");
            }
            else
            {
                AddResult(LogLevel.Info, $"[{prefabType}] AnimatorController: {animator.runtimeAnimatorController.name}");
            }

            // 检查常用动画参数
            ValidateAnimatorParameters(animator, prefabType);
        }
    }

    /// <summary>
    /// 校验Animator参数
    /// </summary>
    /// <param name="animator">Animator组件</param>
    /// <param name="prefabType">预制体类型</param>
    private void ValidateAnimatorParameters(Animator animator, string prefabType)
    {
        // 期望的参数列表
        string[] expectedParams = { "Speed", "Hurt", "Die" };

        foreach (string paramName in expectedParams)
        {
            bool hasParam = HasAnimatorParameter(animator, paramName);
            if (hasParam)
            {
                AddResult(LogLevel.Info, $"[{prefabType}] Animator参数 '{paramName}' ✓");
            }
            else
            {
                AddResult(LogLevel.Warning, $"[{prefabType}] Animator缺少参数 '{paramName}'");
            }
        }
    }

    /// <summary>
    /// 检查Animator是否有指定参数
    /// </summary>
    /// <param name="animator">Animator组件</param>
    /// <param name="parameterName">参数名称</param>
    /// <returns>是否存在</returns>
    private bool HasAnimatorParameter(Animator animator, string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == parameterName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 校验Layer配置
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="prefabType">预制体类型</param>
    private void ValidateLayer(GameObject prefab, string prefabType)
    {
        int layerIndex = prefab.layer;
        string layerName = LayerMask.LayerToName(layerIndex);

        if (string.IsNullOrEmpty(layerName))
        {
            AddResult(LogLevel.Warning, $"[{prefabType}] Layer未设置（使用默认Layer）");
        }
        else
        {
            AddResult(LogLevel.Info, $"[{prefabType}] Layer: {layerName} (索引:{layerIndex})");
        }

        // 检查是否在合理的Layer上
        if (prefabType == "Player" && layerName != "Player" && layerIndex != 0)
        {
            AddResult(LogLevel.Warning, $"[{prefabType}] 玩家建议使用 'Player' Layer");
        }
        else if (prefabType == "Enemy" && layerName != "Enemy" && layerIndex != 0)
        {
            AddResult(LogLevel.Warning, $"[{prefabType}] 敌人建议使用 'Enemy' Layer");
        }
    }

    /// <summary>
    /// 校验Tag配置
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="prefabType">预制体类型</param>
    private void ValidateTag(GameObject prefab, string prefabType)
    {
        string tag = prefab.tag;

        if (prefabType == "Player")
        {
            if (tag == "Player")
            {
                AddResult(LogLevel.Info, $"[{prefabType}] Tag: Player ✓");
            }
            else
            {
                AddResult(LogLevel.Error, $"[{prefabType}] Tag应为 'Player'，当前为 '{tag}'");
            }
        }
        else if (prefabType == "Enemy")
        {
            if (tag == "Untagged" || tag == "Enemy")
            {
                AddResult(LogLevel.Info, $"[{prefabType}] Tag: {tag}");
            }
            else
            {
                AddResult(LogLevel.Warning, $"[{prefabType}] 敌人Tag建议为 'Untagged' 或 'Enemy'，当前为 '{tag}'");
            }
        }
    }

    /// <summary>
    /// 校验Player特定组件
    /// </summary>
    /// <param name="prefab">预制体</param>
    private void ValidatePlayerComponents(GameObject prefab)
    {
        // PlayerController
        PlayerController pc = prefab.GetComponent<PlayerController>();
        if (pc == null)
        {
            AddResult(LogLevel.Error, "[Player] 缺少 PlayerController 组件");
        }
        else
        {
            AddResult(LogLevel.Info, "[Player] PlayerController ✓");
        }

        // PlayerAnimation
        PlayerAnimation pa = prefab.GetComponent<PlayerAnimation>();
        if (pa == null)
        {
            AddResult(LogLevel.Warning, "[Player] 缺少 PlayerAnimation 组件（动画将不可用）");
        }
        else
        {
            AddResult(LogLevel.Info, "[Player] PlayerAnimation ✓");
        }
    }

    /// <summary>
    /// 校验Enemy特定组件
    /// </summary>
    /// <param name="prefab">预制体</param>
    private void ValidateEnemyComponents(GameObject prefab)
    {
        // EnemyAI
        EnemyAI ai = prefab.GetComponent<EnemyAI>();
        if (ai == null)
        {
            AddResult(LogLevel.Error, "[Enemy] 缺少 EnemyAI 组件");
        }
        else
        {
            AddResult(LogLevel.Info, "[Enemy] EnemyAI ✓");
        }

        // EnemyAnimation
        EnemyAnimation ea = prefab.GetComponent<EnemyAnimation>();
        if (ea == null)
        {
            AddResult(LogLevel.Warning, "[Enemy] 缺少 EnemyAnimation 组件（动画将不可用）");
        }
        else
        {
            AddResult(LogLevel.Info, "[Enemy] EnemyAnimation ✓");
        }

        // EnemyHUD
        EnemyHUD hud = prefab.GetComponentInChildren<EnemyHUD>();
        if (hud == null)
        {
            AddResult(LogLevel.Warning, "[Enemy] 缺少 EnemyHUD 组件（血条将不可用）");
        }
        else
        {
            AddResult(LogLevel.Info, "[Enemy] EnemyHUD ✓");
        }
    }

    #endregion

    #region 结果管理

    /// <summary>
    /// 添加校验结果
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息内容</param>
    private void AddResult(LogLevel level, string message)
    {
        validationResults.Add(new ValidationEntry { Level = level, Message = message });

        // 同时输出到Unity控制台
        switch (level)
        {
            case LogLevel.Error:
                Debug.LogError($"[PrefabValidator] {message}");
                break;
            case LogLevel.Warning:
                Debug.LogWarning($"[PrefabValidator] {message}");
                break;
            case LogLevel.Info:
                Debug.Log($"[PrefabValidator] {message}");
                break;
        }
    }

    /// <summary>
    /// 绘制校验结果列表
    /// </summary>
    private void DrawValidationResults()
    {
        EditorGUILayout.LabelField("校验结果", EditorStyles.boldLabel);

        // 统计
        int errorCount = 0;
        int warningCount = 0;
        int infoCount = 0;

        foreach (var entry in validationResults)
        {
            switch (entry.Level)
            {
                case LogLevel.Error: errorCount++; break;
                case LogLevel.Warning: warningCount++; break;
                case LogLevel.Info: infoCount++; break;
            }
        }

        EditorGUILayout.HelpBox(
            $"校验完成: {infoCount} 信息, {warningCount} 警告, {errorCount} 错误",
            errorCount > 0 ? MessageType.Error : (warningCount > 0 ? MessageType.Warning : MessageType.Info)
        );

        EditorGUILayout.Space();

        // 结果列表
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));

        foreach (var entry in validationResults)
        {
            MessageType msgType;
            switch (entry.Level)
            {
                case LogLevel.Error:
                    msgType = MessageType.Error;
                    break;
                case LogLevel.Warning:
                    msgType = MessageType.Warning;
                    break;
                default:
                    msgType = MessageType.Info;
                    break;
            }

            EditorGUILayout.HelpBox(entry.Message, msgType);
        }

        EditorGUILayout.EndScrollView();
    }

    #endregion
}

/// <summary>
/// PrefabValidator的目标组件
/// 挂载到空GameObject上，用于持有要校验的预制体引用
/// </summary>
public class PrefabValidatorTarget : MonoBehaviour
{
    [Tooltip("要校验的Player预制体")]
    public GameObject playerPrefab;

    [Tooltip("要校验的Enemy预制体")]
    public GameObject enemyPrefab;
}
