using UnityEngine;
using UnityEditor;

/// <summary>
/// SceneAutoBuilder编辑器扩展
/// 提供一键清理和场景构建的编辑器菜单
/// 仅编辑器运行，不打包进游戏
/// </summary>
[CustomEditor(typeof(SceneAutoBuilder))]
public class SceneAutoBuilderEditor : Editor
{
    #region Inspector绘制

    /// <summary>
    /// 绘制Inspector面板
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SceneAutoBuilder builder = (SceneAutoBuilder)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("场景自动构建工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "将此脚本挂载到场景任意空物体，进入PlayMode自动构建完整测试场景。\n" +
            "无需任何外部美术资源，Play即可跑通战斗。",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // 手动构建按钮
        if (GUILayout.Button("立即构建测试场景（仅PlayMode）", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                builder.BuildScene();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先进入PlayMode再构建场景", "确定");
            }
        }

        EditorGUILayout.Space();

        // 清理按钮
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("清理所有动态生成对象", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认清理", "确定要清理所有动态生成的对象吗？", "确定", "取消"))
            {
                SceneAutoBuilder.CleanupDynamicObjects();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        // 使用说明
        EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 创建空场景（或使用现有场景）\n" +
            "2. 创建空GameObject，挂载SceneAutoBuilder脚本\n" +
            "3. 配置参数（可选）\n" +
            "4. 点击Play进入PlayMode\n" +
            "5. 自动生成完整测试场景\n" +
            "6. 退出PlayMode自动清理所有对象",
            MessageType.Info
        );
    }

    #endregion

    #region 菜单工具

    /// <summary>
    /// 一键构建测试场景
    /// </summary>
    [MenuItem("工具/场景构建/一键构建测试场景")]
    public static void BuildTestScene()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "请先进入PlayMode再构建场景", "确定");
            return;
        }

        // 查找场景中的SceneAutoBuilder
        SceneAutoBuilder builder = FindObjectOfType<SceneAutoBuilder>();

        if (builder == null)
        {
            // 创建新的GameObject并添加组件
            GameObject obj = new GameObject("SceneAutoBuilder");
            builder = obj.AddComponent<SceneAutoBuilder>();
        }

        builder.BuildScene();
    }

    /// <summary>
    /// 一键清理动态对象
    /// </summary>
    [MenuItem("工具/场景构建/一键清理动态对象")]
    public static void CleanupScene()
    {
        if (!EditorUtility.DisplayDialog("确认清理", "确定要清理所有动态生成的对象吗？", "确定", "取消"))
        {
            return;
        }

        SceneAutoBuilder.CleanupDynamicObjects();
    }

    /// <summary>
    /// 创建测试场景（非PlayMode，仅创建空对象和脚本）
    /// </summary>
    [MenuItem("工具/场景构建/创建测试场景Setup")]
    public static void CreateTestSceneSetup()
    {
        // 检查是否已存在
        if (FindObjectOfType<SceneAutoBuilder>() != null)
        {
            EditorUtility.DisplayDialog("提示", "场景中已存在SceneAutoBuilder", "确定");
            return;
        }

        // 创建空GameObject并挂载脚本
        GameObject obj = new GameObject("SceneAutoBuilder");
        obj.AddComponent<SceneAutoBuilder>();

        // 选中对象
        Selection.activeGameObject = obj;

        EditorUtility.DisplayDialog("完成", 
            "已创建SceneAutoBuilder对象。\n" +
            "配置参数后点击Play即可自动构建测试场景。", 
            "确定");
    }

    /// <summary>
    /// 打开快速测试指南
    /// </summary>
    [MenuItem("工具/场景构建/快速测试指南")]
    public static void ShowQuickGuide()
    {
        string guide = @"【极简测试场景使用指南】

前提条件：
- 空白场景，无需提前搭建任何Hierarchy对象
- 只需挂载SceneAutoBuilder脚本

操作步骤：
1. 创建空GameObject
2. 挂载SceneAutoBuilder脚本
3. 点击Play进入PlayMode
4. 自动生成完整测试场景

操作说明：
- WASD：移动
- 鼠标：旋转视角
- 左键：攻击
- P：暂停
- R：恢复
- K：玩家死亡
- L：玩家重生

退出PlayMode：
- 自动销毁所有动态生成对象
- 不会修改保存的场景文件";

        EditorUtility.DisplayDialog("快速测试指南", guide, "确定");
    }

    #endregion
}
