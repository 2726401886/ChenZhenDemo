using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

/// <summary>
/// WebGL打包脚本 - 适配GitHub Pages部署
/// 菜单栏：Build → Build WebGL (GitHub Pages)
/// 输出到项目根目录 docs 文件夹
/// 压缩模式：Disabled（防止GitHub Pages MIME报错）
/// </summary>
public class WebGLBuilder
{
    private const string OUTPUT_DIR = "../docs";

    [MenuItem("Build/Build WebGL (GitHub Pages)")]
    public static void BuildWebGL()
    {
        // 输出路径
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, OUTPUT_DIR));
        Debug.Log($"[WebGLBuilder] 输出目录: {outputPath}");

        // 清理旧构建
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);

        // 收集所有已启用场景
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "BuildSettings中没有场景，请先添加场景", "OK");
            return;
        }

        string[] scenePaths = new string[scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenePaths[i] = scenes[i].path;
            Debug.Log($"[WebGLBuilder] 场景{i}: {scenes[i].path}");
        }

        // 设置WebGL压缩为Disabled（GitHub Pages兼容）
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;

        // 打包选项
        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = scenePaths,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log("[WebGLBuilder] 开始打包WebGL...");

        // 执行打包
        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            // 生成.nojekyll文件（GitHub Pages需要）
            File.WriteAllText(Path.Combine(outputPath, ".nojekyll"), "");
            Debug.Log("[WebGLBuilder] .nojekyll文件已生成");

            // 生成说明文件
            string readme = $"ChenZhenDemo WebGL Build\n" +
                           $"构建时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                           $"输出大小: {summary.totalSize / 1024 / 1024:F2} MB\n" +
                           $"编译时间: {summary.totalTime.TotalSeconds:F1} 秒";
            File.WriteAllText(Path.Combine(outputPath, "BUILD_INFO.txt"), readme);

            string msg = $"WebGL打包成功！\n" +
                        $"输出: {outputPath}\n" +
                        $"大小: {summary.totalSize / 1024 / 1024:F2} MB\n" +
                        $"耗时: {summary.totalTime.TotalSeconds:F1} 秒";
            EditorUtility.DisplayDialog("打包成功", msg, "OK");
            Debug.Log($"[WebGLBuilder] {msg}");
        }
        else
        {
            string error = $"打包失败！\n错误数: {summary.totalErrors}\n" +
                          $"请查看Console日志排查";
            EditorUtility.DisplayDialog("打包失败", error, "OK");
            Debug.LogError($"[WebGLBuilder] 打包失败，错误数: {summary.totalErrors}");
        }
    }

    [MenuItem("Build/打开输出目录")]
    public static void OpenOutputDir()
    {
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, OUTPUT_DIR));
        if (Directory.Exists(outputPath))
            EditorUtility.RevealInFinder(outputPath);
        else
            EditorUtility.DisplayDialog("提示", "输出目录不存在，请先打包", "OK");
    }
}
