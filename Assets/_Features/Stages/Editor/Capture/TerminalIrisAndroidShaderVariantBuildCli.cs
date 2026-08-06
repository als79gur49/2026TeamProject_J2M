using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class TerminalIrisAndroidShaderVariantBuildCli
{
    private const string BuildPathArgument = "-captureBuildPath";
    private const string GraphicsApiArgument = "-terminalIrisAndroidGraphicsApi";

    public static void BuildAndroidDevelopmentPlayer()
    {
        var arguments = Environment.GetCommandLineArgs();
        var buildPath = RequireArgument(arguments, BuildPathArgument);
        var graphicsApi = ParseGraphicsApi(
            RequireArgument(arguments, GraphicsApiArgument));
        var scenes = PlayerProfilerCaptureCli.ResolveBuildScenesForTests(arguments);
        var originalUseDefaultGraphicsApis =
            PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        var originalGraphicsApis =
            PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        try
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { graphicsApi });
            var directory = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Terminal Iris Android {graphicsApi} build failed: " +
                    $"{report.summary.result}");
            }

            Debug.Log(
                $"Terminal Iris Android {graphicsApi} build succeeded: " +
                $"{buildPath}");
        }
        finally
        {
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                originalGraphicsApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(
                BuildTarget.Android,
                originalUseDefaultGraphicsApis);
            AssetDatabase.SaveAssets();
        }
    }

    private static GraphicsDeviceType ParseGraphicsApi(string rawValue)
    {
        if (string.Equals(rawValue, "Vulkan", StringComparison.OrdinalIgnoreCase))
        {
            return GraphicsDeviceType.Vulkan;
        }

        if (string.Equals(rawValue, "OpenGLES3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "GLES3", StringComparison.OrdinalIgnoreCase))
        {
            return GraphicsDeviceType.OpenGLES3;
        }

        throw new ArgumentOutOfRangeException(
            GraphicsApiArgument,
            rawValue,
            "Terminal Iris Android graphics API must be Vulkan or OpenGLES3.");
    }

    private static string RequireArgument(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.Ordinal))
            {
                var value = arguments[index + 1];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        throw new ArgumentException(
            $"Terminal Iris Android build requires {name} <value>.");
    }
}
