using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 玩家构建入口。CI 和本地共用同一套参数，避免"CI 打出来的和本地不一样"。
///
/// 命令行用法（CI 里就是这么调的）：
///
///   Unity -batchmode -quit -projectPath Project/nameless
///         -executeMethod LabPlayerBuild.Run -buildTarget StandaloneWindows64 -outPath /abs/out
///
/// 不传 -buildTarget 时按当前激活平台构建。
/// </summary>
public static class LabPlayerBuild
{
    /// <summary>发布版包含的场景，第一个必须是主界面。</summary>
    static readonly string[] ReleaseScenes =
    {
        "Assets/Scenes/Home.unity",
        "Assets/Scenes/Levels/REAL FINAL MAP.unity",
    };

    [MenuItem("GameJam/Build Players/Windows")]
    public static void BuildWindowsMenu() => BuildFromMenu(BuildTarget.StandaloneWindows64, "Windows");

    [MenuItem("GameJam/Build Players/macOS")]
    public static void BuildMacMenu() => BuildFromMenu(BuildTarget.StandaloneOSX, "macOS");

    [MenuItem("GameJam/Build Players/Linux")]
    public static void BuildLinuxMenu() => BuildFromMenu(BuildTarget.StandaloneLinux64, "Linux");

    static void BuildFromMenu(BuildTarget target, string label)
    {
        string outPath = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Builds", label);

        Directory.CreateDirectory(outPath);
        var report = Build(target, outPath);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[LabBuild] {label} 成功 → {outPath} " +
                      $"({report.summary.totalSize / (1024f * 1024f):0.0} MB)");
            EditorUtility.RevealInFinder(outPath);
        }
        else
        {
            Debug.LogError($"[LabBuild] {label} 失败：{report.summary.result}，" +
                           $"{report.summary.totalErrors} 个错误");
        }
    }

    /// <summary>
    /// game-ci/unity-builder 的 buildMethod 入口。
    ///
    /// 它自己接管进程退出：方法正常返回就算成功，抛异常就算失败。
    /// 所以这里**不能**调 EditorApplication.Exit —— 那会和 builder 的收尾流程打架。
    /// 平台和输出目录从环境变量读（工作流里设的）。
    /// </summary>
    public static void RunFromCi()
    {
        // unity-builder 通过 customParameters 把这些参数追加到命令行，
        // 用 -labBuildTarget <target> 传进来；没传就按当前激活平台。
        string targetName = ArgValue(Environment.GetCommandLineArgs(), "-labBuildTarget");
        if (string.IsNullOrEmpty(targetName))
            targetName = EditorUserBuildSettings.activeBuildTarget.ToString();

        if (!TryParseTarget(targetName, out BuildTarget target))
            throw new InvalidOperationException($"[LabBuild] 不认识的构建目标：{targetName}");

        string outPath = ArgValue(Environment.GetCommandLineArgs(), "-labBuildOutput");
        if (string.IsNullOrEmpty(outPath))
            outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", targetName));

        Directory.CreateDirectory(outPath);

        var report = Build(target, outPath);
        Debug.Log($"[LabBuild] {target} → {outPath} : {report.summary.result} " +
                  $"({report.summary.totalSize / (1024f * 1024f):0.0} MB)");

        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"[LabBuild] {target} 构建失败：{report.summary.result}，" +
                $"{report.summary.totalErrors} 个错误");
    }

    /// <summary>
    /// 命令行入口（本地手动跑）：
    ///   Unity -batchmode -quit -projectPath Project/nameless
    ///         -executeMethod LabPlayerBuild.Run -buildTarget linux -outPath /abs/out
    ///
    /// 这个会自己调 Exit 设退出码；CI 走 <see cref="RunFromCi"/>。
    /// </summary>
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string targetArg = ArgValue(args, "-buildTarget");
        if (!string.IsNullOrEmpty(targetArg) && !TryParseTarget(targetArg, out target))
        {
            Debug.LogError($"[LabBuild] 不认识的 -buildTarget：{targetArg}");
            EditorApplication.Exit(2);
            return;
        }

        string outPath = ArgValue(args, "-outPath");
        if (string.IsNullOrEmpty(outPath))
            outPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds");

        Directory.CreateDirectory(outPath);

        var report = Build(target, outPath);
        bool ok = report.summary.result == BuildResult.Succeeded;

        Debug.Log($"[LabBuild] {target} → {outPath} : {report.summary.result} " +
                  $"({report.summary.totalSize / (1024f * 1024f):0.0} MB)");

        EditorApplication.Exit(ok ? 0 : 1);
    }

    static BuildReport Build(BuildTarget target, string outPath)
    {
        var scenes = ReleaseScenes
            .Where(p => File.Exists(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), p)))
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("[LabBuild] 一个发布场景都找不到");

        // 主界面必须在第一个：打包后启动就停在这里。
        if (scenes[0] != ReleaseScenes[0])
            throw new InvalidOperationException(
                $"[LabBuild] 主界面 {ReleaseScenes[0]} 缺失，发布包必须从主界面启动");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            target = target,
            locationPathName = ExecutablePath(target, outPath),
            options = BuildOptions.None,
        };

        Debug.Log($"[LabBuild] 构建 {target}，场景：{string.Join(", ", scenes)}");
        return BuildPipeline.BuildPlayer(options);
    }

    /// <summary>各平台的可执行文件命名约定。</summary>
    static string ExecutablePath(BuildTarget target, string outPath)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return Path.Combine(outPath, "Hui.exe");
            case BuildTarget.StandaloneOSX:
                return Path.Combine(outPath, "Hui.app");
            default:
                return Path.Combine(outPath, "Hui");
        }
    }

    static bool TryParseTarget(string name, out BuildTarget target)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "win":
            case "windows":
            case "win64":
            case "standalonewindows64":
                target = BuildTarget.StandaloneWindows64;
                return true;
            case "osx":
            case "mac":
            case "macos":
            case "standaloneosx":
                target = BuildTarget.StandaloneOSX;
                return true;
            case "linux":
            case "linux64":
            case "standalonelinux64":
                target = BuildTarget.StandaloneLinux64;
                return true;
            default:
                target = BuildTarget.NoTarget;
                return false;
        }
    }

    static string ArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
