using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.Stages.Editor;
using Game.Feature.UI.Composition;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PlayerProfilerCaptureCli
{
    private const string CaptureBuildPathArg = "-captureBuildPath";
    private const string CaptureScenesArg = "-captureScenes";
    private const string CaptureBackendArg = "-captureBackend";
    private const string CaptureGameplayShellSceneArg = "-captureGameplayShellScene";
    private const string RouteConfigPath =
        "Assets/_Features/UI/UI_Composition/Authoring/GameplayStageLaunchRouteConfig.asset";

    public static void BuildWindowsDevelopmentPlayer()
    {
        var args = Environment.GetCommandLineArgs();
        var buildPath = RequireArgument(args, CaptureBuildPathArg);
        var backend = ReadArgument(args, CaptureBackendArg, "Mono");
        var scenes = ResolveBuildScenes(args);
        var requestedBackend = ParseBackend(backend);

        var originalBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
        var backendWasChanged = originalBackend != requestedBackend;
        try
        {
            if (backendWasChanged)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, requestedBackend);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development |
                          BuildOptions.ConnectWithProfiler |
                          BuildOptions.AllowDebugging,
            };

            var buildDirectory = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrWhiteSpace(buildDirectory))
            {
                Directory.CreateDirectory(buildDirectory);
            }

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"PlayerProfilerCaptureCli build failed: {report.summary.result}");
            }

            Debug.Log($"PlayerProfilerCaptureCli build succeeded: {buildPath}");
            if (TryReadCaptureStage(args, out var stageId))
            {
                Debug.Log(
                    $"Run the capture player with {PlayerCaptureLaunchOptions.CaptureStageArg} {stageId.Value} before collecting gameplay profiler data.");
            }
        }
        finally
        {
            if (backendWasChanged)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, originalBackend);
            }
        }
    }

    public static string[] ResolveBuildScenesForTests(string[] args)
    {
        return ResolveBuildScenes(args);
    }

    private static string[] ResolveBuildScenes(IReadOnlyList<string> args)
    {
        if (TryReadCaptureStage(args, out var stageId))
        {
            var shellScenePath = ResolveGameplayShellScenePath(args);
            if (string.IsNullOrWhiteSpace(shellScenePath))
            {
                throw new InvalidOperationException(
                    $"Capture stage '{stageId.Value}' requires a configured gameplay shell scene.");
            }

            Debug.Log(
                $"PlayerProfilerCaptureCli resolved capture stage '{stageId.Value}' to gameplay shell scene '{shellScenePath}'.");
            return new[] { shellScenePath };
        }

        var scenes = ReadSceneList(args);
        RejectStageBackedDirectScenesWithoutCaptureStage(scenes);
        return scenes.Length > 0
            ? scenes
            : EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => NormalizeScenePath(scene.path))
                .ToArray();
    }

    private static string ResolveGameplayShellScenePath(IReadOnlyList<string> args)
    {
        var explicitScene = ReadArgument(args, CaptureGameplayShellSceneArg, string.Empty);
        if (!string.IsNullOrWhiteSpace(explicitScene))
        {
            return NormalizeScenePath(explicitScene);
        }

        var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);
        return routeConfig != null
            ? NormalizeScenePath(routeConfig.GameplayShellScenePath)
            : string.Empty;
    }

    private static void RejectStageBackedDirectScenesWithoutCaptureStage(IEnumerable<string> scenes)
    {
        var directPlayCatalog = StageEditorDirectPlayCatalog.LoadDefault();
        if (directPlayCatalog == null)
        {
            return;
        }

        foreach (var scene in scenes)
        {
            if (directPlayCatalog.TryResolveScenePath(scene, out var stageId))
            {
                throw new InvalidOperationException(
                    $"Player capture cannot build stage-backed scene '{scene}' directly without {PlayerCaptureLaunchOptions.CaptureStageArg} {stageId.Value}. Use the gameplay shell scene plus a capture stage argument.");
            }
        }
    }

    private static bool TryReadCaptureStage(IReadOnlyList<string> args, out StageId stageId)
    {
        if (PlayerCaptureLaunchOptions.TryParse(args, out var options, out var error))
        {
            stageId = options.StageId;
            return options.HasCaptureStage;
        }

        throw new ArgumentException(error);
    }

    private static string[] ReadSceneList(IReadOnlyList<string> args)
    {
        var rawScenes = ReadArgument(args, CaptureScenesArg, string.Empty);
        if (string.IsNullOrWhiteSpace(rawScenes))
        {
            return Array.Empty<string>();
        }

        return rawScenes
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeScenePath)
            .Where(scene => !string.IsNullOrWhiteSpace(scene))
            .ToArray();
    }

    private static ScriptingImplementation ParseBackend(string backend)
    {
        return string.Equals(backend, "IL2CPP", StringComparison.OrdinalIgnoreCase)
            ? ScriptingImplementation.IL2CPP
            : ScriptingImplementation.Mono2x;
    }

    private static string RequireArgument(IReadOnlyList<string> args, string name)
    {
        var value = ReadArgument(args, name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required command line argument: {name}");
        }

        return value;
    }

    private static string ReadArgument(IReadOnlyList<string> args, string name, string defaultValue)
    {
        if (args == null)
        {
            return defaultValue;
        }

        var inlinePrefix = name + "=";
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i] ?? string.Empty;
            if (arg.StartsWith(inlinePrefix, StringComparison.Ordinal))
            {
                return arg.Substring(inlinePrefix.Length);
            }

            if (string.Equals(arg, name, StringComparison.Ordinal) && i + 1 < args.Count)
            {
                return args[i + 1] ?? defaultValue;
            }
        }

        return defaultValue;
    }

    private static string NormalizeScenePath(string scenePath)
    {
        return (scenePath ?? string.Empty).Replace('\\', '/').Trim();
    }
}
