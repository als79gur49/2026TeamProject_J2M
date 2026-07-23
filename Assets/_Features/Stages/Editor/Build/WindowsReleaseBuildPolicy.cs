using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;

public static class WindowsReleaseBuildPolicy
{
    public const string SchemaVersion = "1.0";
    public const string ConfigurationName = "Windows-x64-NonDevelopment-Mono-RC";
    public const string Architecture = "x86_64";
    public const string ExpectedUnityVersion = "6000.3.11f1";
    public const string ProductName = "VectorQuake";
    public const string CompanyName = "J2M";
    public const string MainMenuScene = "Assets/Scenes/MainMenuScene.unity";
    public const string UiAudioScene = "Assets/Scenes/UIAudioScene.unity";
    public const bool PlayerLogEnabled = true;
    public const bool IncrementalGcRequired = true;
    public const ScriptingImplementation Backend = ScriptingImplementation.Mono2x;
    public const ManagedStrippingLevel Stripping = ManagedStrippingLevel.Disabled;
    public const StackTraceLogType WarningStackTrace = StackTraceLogType.ScriptOnly;

    public static readonly string[] Scenes = { MainMenuScene, UiAudioScene };

    public static BuildPlayerOptions CreateBuildOptions(string outputPath)
    {
        return new BuildPlayerOptions
        {
            scenes = (string[])Scenes.Clone(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
    }

    public static int ValidateArguments(IReadOnlyDictionary<string, string> arguments)
    {
        foreach (var name in WindowsReleaseBuildCli.RequiredArgumentNames)
        {
            if (arguments == null ||
                !arguments.TryGetValue(name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                return WindowsReleaseExitCodes.InvalidArguments;
            }
        }

        var output = arguments[WindowsReleaseBuildCli.OutputPathArgument]
            .Replace('\\', '/');
        if (output.IndexOf("/.staging-", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return WindowsReleaseExitCodes.InvalidArguments;
        }

        return WindowsReleaseExitCodes.Success;
    }

    public static int ValidateActiveTarget(BuildTarget target)
    {
        return target == BuildTarget.StandaloneWindows64
            ? WindowsReleaseExitCodes.Success
            : WindowsReleaseExitCodes.ActiveBuildTargetMismatch;
    }

    public static int ValidateUnityVersion(string runtimeVersion, string projectVersion)
    {
        return string.Equals(runtimeVersion, ExpectedUnityVersion, StringComparison.Ordinal) &&
               string.Equals(projectVersion, ExpectedUnityVersion, StringComparison.Ordinal)
            ? WindowsReleaseExitCodes.Success
            : WindowsReleaseExitCodes.UnityVersionMismatch;
    }

    public static int ValidateScenes(IEnumerable<ReleaseSceneDescriptor> scenes)
    {
        if (scenes == null)
        {
            return WindowsReleaseExitCodes.SceneContractMismatch;
        }

        var actual = new List<ReleaseSceneDescriptor>(scenes);
        if (actual.Count != Scenes.Length)
        {
            return WindowsReleaseExitCodes.SceneContractMismatch;
        }

        for (var index = 0; index < Scenes.Length; index++)
        {
            if (!actual[index].Enabled ||
                !actual[index].Exists ||
                !string.Equals(actual[index].Path, Scenes[index], StringComparison.Ordinal))
            {
                return WindowsReleaseExitCodes.SceneContractMismatch;
            }
        }

        return WindowsReleaseExitCodes.Success;
    }

    public static int MapBuildResult(BuildResult result)
    {
        switch (result)
        {
            case BuildResult.Succeeded:
                return WindowsReleaseExitCodes.Success;
            case BuildResult.Failed:
                return WindowsReleaseExitCodes.BuildFailed;
            case BuildResult.Cancelled:
                return WindowsReleaseExitCodes.BuildCancelled;
            default:
                return WindowsReleaseExitCodes.BuildUnknownResult;
        }
    }
}

public readonly struct ReleaseSceneDescriptor
{
    public ReleaseSceneDescriptor(string path, bool enabled, bool exists)
    {
        Path = path;
        Enabled = enabled;
        Exists = exists;
    }

    public string Path { get; }
    public bool Enabled { get; }
    public bool Exists { get; }
}

public static class WindowsReleaseExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 10;
    public const int UnsupportedConfiguration = 11;
    public const int ActiveBuildTargetMismatch = 12;
    public const int UnityVersionMismatch = 13;
    public const int SceneContractMismatch = 14;
    public const int PlayerSettingsContractMismatch = 20;
    public const int SettingsApplyFailure = 21;
    public const int SettingsRestoreFailure = 22;
    public const int BuildFailed = 30;
    public const int BuildCancelled = 31;
    public const int BuildUnknownResult = 32;
    public const int MetadataWriteFailure = 40;
    public const int BuildReportWriteFailure = 41;
    public const int InternalException = 50;

    public static int[] All =>
        new[]
        {
            Success, InvalidArguments, UnsupportedConfiguration, ActiveBuildTargetMismatch,
            UnityVersionMismatch, SceneContractMismatch, PlayerSettingsContractMismatch,
            SettingsApplyFailure, SettingsRestoreFailure, BuildFailed, BuildCancelled,
            BuildUnknownResult, MetadataWriteFailure, BuildReportWriteFailure, InternalException,
        };
}

public interface IWindowsReleaseSettings
{
    ReleaseSettingsSnapshot Capture();
    void ApplyRequired();
    bool IsRequired();
    void Restore(ReleaseSettingsSnapshot snapshot);
    bool IsRestored(ReleaseSettingsSnapshot snapshot);
}

[Serializable]
public struct ReleaseSettingsSnapshot
{
    public ScriptingImplementation backend;
    public ManagedStrippingLevel stripping;
    public bool playerLog;
    public StackTraceLogType warningStackTrace;
}

public static class WindowsReleaseSettingsTransaction
{
    public static int Run(IWindowsReleaseSettings settings, Func<int> build)
    {
        var snapshot = settings.Capture();
        var result = WindowsReleaseExitCodes.InternalException;
        var restoreRequired = true;
        try
        {
            try
            {
                restoreRequired = !settings.IsRequired();
                if (restoreRequired)
                {
                    settings.ApplyRequired();
                }

                if (!settings.IsRequired())
                {
                    result = WindowsReleaseExitCodes.SettingsApplyFailure;
                }
            }
            catch
            {
                result = WindowsReleaseExitCodes.SettingsApplyFailure;
            }

            if (result != WindowsReleaseExitCodes.SettingsApplyFailure)
            {
                result = build();
            }
        }
        catch
        {
            result = WindowsReleaseExitCodes.InternalException;
        }
        finally
        {
            if (restoreRequired)
            {
                try
                {
                    settings.Restore(snapshot);
                    if (!settings.IsRestored(snapshot))
                    {
                        result = WindowsReleaseExitCodes.SettingsRestoreFailure;
                    }
                }
                catch
                {
                    result = WindowsReleaseExitCodes.SettingsRestoreFailure;
                }
            }
        }

        return result;
    }
}

[Serializable]
public sealed class WindowsReleaseMetadataV1
{
    public string schemaVersion;
    public string runId;
    public string artifactId;
    public string sourceSha;
    public string sourceTree;
    public string branch;
    public bool headDetached;
    public string originMainSha;
    public int ahead;
    public int behind;
    public bool sourceDirty;
    public string unityVersion;
    public string unityRevision;
    public string buildTarget;
    public string architecture;
    public string configuration;
    public string backend;
    public string managedStrippingLevel;
    public bool development;
    public bool connectWithProfiler;
    public bool deepProfiling;
    public bool allowDebugging;
    public bool scriptDebugging;
    public bool waitForDebugger;
    public bool forceAssertions;
    public string[] effectiveScenes;
    public bool playerLogEnabled;
    public string stackTracePolicy;
    public bool incrementalGC;
    public string productName;
    public string companyName;
    public string productVersion;
    public string buildNumber;
    public string applicationIdentifier;
    public string buildEntry;
    public string entrySourceSha256;
    public string policySourceSha256;
    public string wrapperSourceSha256;
    public string buildResult;
    public int warningCount;
    public int errorCount;
    public ulong totalSizeBytes;
    public double durationSeconds;
    public string payloadManifest;
    public string payloadManifestSha256;
    public int payloadFileCount;
    public string buildStartedUtc;
    public string buildCompletedUtc;
}
