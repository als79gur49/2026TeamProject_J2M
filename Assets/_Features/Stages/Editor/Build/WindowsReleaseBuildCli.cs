using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class WindowsReleaseBuildCli
{
    public const string OutputPathArgument = "-releaseOutputPath";
    public const string RunIdArgument = "-releaseRunId";
    public const string ArtifactIdArgument = "-releaseArtifactId";
    public const string MetadataPathArgument = "-releaseIntermediateMetadataPath";
    public const string BuildReportPathArgument = "-releaseBuildReportPath";

    public static readonly string[] RequiredArgumentNames =
    {
        OutputPathArgument, RunIdArgument, ArtifactIdArgument,
        MetadataPathArgument, BuildReportPathArgument,
    };

    public static void BuildWindowsX64NonDevelopment()
    {
        var exitCode = WindowsReleaseExitCodes.InternalException;
        try
        {
            exitCode = Run(Environment.GetCommandLineArgs());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            exitCode = WindowsReleaseExitCodes.InternalException;
        }
        finally
        {
            Debug.Log($"WindowsReleaseBuildCli exiting with code {exitCode}.");
            EditorApplication.Exit(exitCode);
        }
    }

    internal static int Run(string[] commandLine)
    {
        var arguments = ParseArguments(commandLine);
        var validation = WindowsReleaseBuildPolicy.ValidateArguments(arguments);
        if (validation != WindowsReleaseExitCodes.Success)
        {
            return validation;
        }

        validation = WindowsReleaseBuildPolicy.ValidateActiveTarget(
            EditorUserBuildSettings.activeBuildTarget);
        if (validation != WindowsReleaseExitCodes.Success)
        {
            Debug.LogError("ACTIVE_BUILD_TARGET_MISMATCH");
            return validation;
        }

        var projectVersion = ReadProjectVersion();
        validation = WindowsReleaseBuildPolicy.ValidateUnityVersion(
            Application.unityVersion, projectVersion);
        if (validation != WindowsReleaseExitCodes.Success)
        {
            Debug.LogError("UNITY_VERSION_MISMATCH");
            return validation;
        }

        var scenes = EditorBuildSettings.scenes.Select(scene =>
            new ReleaseSceneDescriptor(
                scene.path.Replace('\\', '/'),
                scene.enabled,
                File.Exists(Path.GetFullPath(scene.path))));
        validation = WindowsReleaseBuildPolicy.ValidateScenes(scenes);
        if (validation != WindowsReleaseExitCodes.Success)
        {
            Debug.LogError("RELEASE_SCENE_LIST_MISMATCH");
            return validation;
        }

        Debug.Log("ACTIVE_WINDOWS64_TARGET_CONFIRMED");
        Debug.Log("UNITY_VERSION_CONTRACT_CONFIRMED");
        Debug.Log("EXACT_RELEASE_SCENE_LIST_CONFIRMED");

        var metadata = CreateMetadata(arguments);
        var settings = new UnityWindowsReleaseSettings();
        var started = DateTime.UtcNow;
        BuildReport report = null;
        var result = WindowsReleaseSettingsTransaction.Run(settings, () =>
        {
            var outputPath = arguments[OutputPathArgument];
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            report = BuildPipeline.BuildPlayer(
                WindowsReleaseBuildPolicy.CreateBuildOptions(outputPath));
            return WindowsReleaseBuildPolicy.MapBuildResult(report.summary.result);
        });

        if (report != null)
        {
            PopulateReport(metadata, report, started);
            try
            {
                WriteJson(arguments[BuildReportPathArgument], new BuildReportSummaryV1(report));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (result == WindowsReleaseExitCodes.Success)
                {
                    result = WindowsReleaseExitCodes.BuildReportWriteFailure;
                }
            }
        }

        metadata.buildResult = ExitCodeName(result);
        metadata.buildCompletedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        try
        {
            WriteJson(arguments[MetadataPathArgument], metadata);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (result == WindowsReleaseExitCodes.Success)
            {
                result = WindowsReleaseExitCodes.MetadataWriteFailure;
            }
        }

        return result;
    }

    internal static Dictionary<string, string> ParseArguments(IReadOnlyList<string> args)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args == null)
        {
            return parsed;
        }

        for (var index = 0; index < args.Count; index++)
        {
            var current = args[index] ?? string.Empty;
            foreach (var name in RequiredArgumentNames)
            {
                if (current.StartsWith(name + "=", StringComparison.Ordinal))
                {
                    parsed[name] = current.Substring(name.Length + 1);
                }
                else if (string.Equals(current, name, StringComparison.Ordinal) &&
                         index + 1 < args.Count)
                {
                    parsed[name] = args[index + 1] ?? string.Empty;
                }
            }
        }

        return parsed;
    }

    private static string ReadProjectVersion()
    {
        const string prefix = "m_EditorVersion:";
        var line = File.ReadLines("ProjectSettings/ProjectVersion.txt")
            .FirstOrDefault(value => value.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        return line == null ? string.Empty : line.Substring(line.IndexOf(':') + 1).Trim();
    }

    private static WindowsReleaseMetadataV1 CreateMetadata(
        IReadOnlyDictionary<string, string> arguments)
    {
        return new WindowsReleaseMetadataV1
        {
            schemaVersion = WindowsReleaseBuildPolicy.SchemaVersion,
            runId = arguments[RunIdArgument],
            artifactId = arguments[ArtifactIdArgument],
            unityVersion = Application.unityVersion,
            unityRevision = ReadUnityRevision(),
            buildTarget = BuildTarget.StandaloneWindows64.ToString(),
            architecture = WindowsReleaseBuildPolicy.Architecture,
            configuration = WindowsReleaseBuildPolicy.ConfigurationName,
            backend = WindowsReleaseBuildPolicy.Backend.ToString(),
            managedStrippingLevel = WindowsReleaseBuildPolicy.Stripping.ToString(),
            development = false,
            connectWithProfiler = false,
            deepProfiling = false,
            allowDebugging = false,
            scriptDebugging = false,
            waitForDebugger = false,
            forceAssertions = false,
            effectiveScenes = (string[])WindowsReleaseBuildPolicy.Scenes.Clone(),
            playerLogEnabled = true,
            stackTracePolicy = WindowsReleaseBuildPolicy.WarningStackTrace.ToString(),
            incrementalGC = PlayerSettings.gcIncremental,
            productName = PlayerSettings.productName,
            companyName = PlayerSettings.companyName,
            productVersion = PlayerSettings.bundleVersion,
            buildNumber = string.Empty,
            applicationIdentifier =
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone),
            buildEntry = nameof(WindowsReleaseBuildCli) + "." +
                         nameof(BuildWindowsX64NonDevelopment),
            payloadManifest = "files.sha256",
            buildStartedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };
    }

    private static string ReadUnityRevision()
    {
        var line = File.ReadLines("ProjectSettings/ProjectVersion.txt")
            .FirstOrDefault(value => value.StartsWith("m_EditorVersionWithRevision:",
                StringComparison.Ordinal));
        if (line == null)
        {
            return string.Empty;
        }

        var open = line.LastIndexOf('(');
        var close = line.LastIndexOf(')');
        return open >= 0 && close > open ? line.Substring(open + 1, close - open - 1) : string.Empty;
    }

    private static void PopulateReport(
        WindowsReleaseMetadataV1 metadata, BuildReport report, DateTime started)
    {
        metadata.buildResult = report.summary.result.ToString();
        metadata.warningCount = (int)report.summary.totalWarnings;
        metadata.errorCount = (int)report.summary.totalErrors;
        metadata.totalSizeBytes = report.summary.totalSize;
        metadata.durationSeconds = report.summary.totalTime.TotalSeconds;
        metadata.buildStartedUtc = started.ToString("o", CultureInfo.InvariantCulture);
    }

    private static void WriteJson(string path, object value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonUtility.ToJson(value, true));
    }

    private static string ExitCodeName(int code)
    {
        return code == 0 ? "Succeeded" : "ExitCode-" + code.ToString(CultureInfo.InvariantCulture);
    }
}

internal sealed class UnityWindowsReleaseSettings : IWindowsReleaseSettings
{
    public ReleaseSettingsSnapshot Capture()
    {
        return new ReleaseSettingsSnapshot
        {
            backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone),
            stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone),
            playerLog = PlayerSettings.usePlayerLog,
            warningStackTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning),
        };
    }

    public void ApplyRequired()
    {
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Standalone, WindowsReleaseBuildPolicy.Backend);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Standalone, WindowsReleaseBuildPolicy.Stripping);
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SetStackTraceLogType(
            LogType.Warning, WindowsReleaseBuildPolicy.WarningStackTrace);
    }

    public bool IsRequired()
    {
        return PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) ==
                   WindowsReleaseBuildPolicy.Backend &&
               PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone) ==
                   WindowsReleaseBuildPolicy.Stripping &&
               PlayerSettings.usePlayerLog &&
               PlayerSettings.GetStackTraceLogType(LogType.Warning) ==
                   WindowsReleaseBuildPolicy.WarningStackTrace &&
               PlayerSettings.gcIncremental &&
               string.Equals(PlayerSettings.productName, WindowsReleaseBuildPolicy.ProductName,
                   StringComparison.Ordinal) &&
               string.Equals(PlayerSettings.companyName, WindowsReleaseBuildPolicy.CompanyName,
                   StringComparison.Ordinal);
    }

    public void Restore(ReleaseSettingsSnapshot snapshot)
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, snapshot.backend);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, snapshot.stripping);
        PlayerSettings.usePlayerLog = snapshot.playerLog;
        PlayerSettings.SetStackTraceLogType(LogType.Warning, snapshot.warningStackTrace);
    }

    public bool IsRestored(ReleaseSettingsSnapshot snapshot)
    {
        var current = Capture();
        return current.backend == snapshot.backend &&
               current.stripping == snapshot.stripping &&
               current.playerLog == snapshot.playerLog &&
               current.warningStackTrace == snapshot.warningStackTrace;
    }
}

[Serializable]
internal sealed class BuildReportSummaryV1
{
    public string schemaVersion = WindowsReleaseBuildPolicy.SchemaVersion;
    public string result;
    public int totalErrors;
    public int totalWarnings;
    public ulong totalSize;
    public double totalTimeSeconds;
    public string outputPath;

    public BuildReportSummaryV1(BuildReport report)
    {
        result = report.summary.result.ToString();
        totalErrors = report.summary.totalErrors;
        totalWarnings = report.summary.totalWarnings;
        totalSize = report.summary.totalSize;
        totalTimeSeconds = report.summary.totalTime.TotalSeconds;
        outputPath = Path.GetFileName(report.summary.outputPath);
    }
}
