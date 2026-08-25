using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;

public static class WindowsReleaseBuildPolicy
{
    public const string MetadataSchemaVersion = "4.0";
    public const string BuildReportSummarySchemaVersion = "4.0";
    public const string BuildReportDetailsSchemaVersion = "3.0";
    public const string StoreConfigurationSchema = "1.0";
    public const string StoreConfigurationId =
        "windows-x64-store-mono-logon-v1";
    public const string LogPolicyId = "local-player-log-no-auto-upload-v1";
    public const string PayloadAudience = "StoreDistributable";
    public const string ConfigurationName = "Windows-x64-Store-Mono-LogOn";
    public const string Architecture = "x86_64";
    public const string ExpectedUnityVersion = "6000.3.11f1";
    public const string ProductName = "VectorQuake";
    public const string CompanyName = "J2M";
    public const string MainMenuScene = "Assets/Scenes/MainMenuScene.unity";
    public const string UiAudioScene = "Assets/Scenes/UIAudioScene.unity";
    public const bool PlayerLogEnabled = true;
    public const bool AutomaticLogUpload = false;
    public const bool IncrementalGcRequired = true;
    public const ScriptingImplementation Backend = ScriptingImplementation.Mono2x;
    public const ManagedStrippingLevel Stripping = ManagedStrippingLevel.Disabled;
    public const Il2CppCompilerConfiguration Il2CppCompiler =
        Il2CppCompilerConfiguration.Release;
    public const StackTraceLogType WarningStackTrace = StackTraceLogType.ScriptOnly;
    public const string CollectionsPackageVersion = "2.6.2";

    public static readonly string[] ForbiddenManagedAssemblies =
    {
        WindowsDistributionTargetPolicy.SystemIoHashingArtifact,
        WindowsDistributionTargetPolicy.UnsafeArtifact,
    };

    public static readonly string[] TestOnlyManagedPluginPaths =
    {
        "Packages/com.unity.collections/Unity.Collections.Tests/" +
        "System.IO.Hashing/System.IO.Hashing.dll",
        "Packages/com.unity.collections/Unity.Collections.Tests/" +
        "System.Runtime.CompilerServices.Unsafe/" +
        "System.Runtime.CompilerServices.Unsafe.dll",
    };

    public static readonly string[] Scenes = { MainMenuScene, UiAudioScene };

    public static WindowsReleaseBackendConfiguration DefaultConfiguration =>
        WindowsReleaseBackendConfiguration.CanonicalStoreMono;

    public static bool TryResolveConfiguration(
        string intentValue,
        string backendValue,
        out WindowsReleaseBackendConfiguration configuration)
    {
        var intent = WindowsReleaseBuildIntent.CanonicalStore;
        if (!string.IsNullOrEmpty(intentValue) &&
            !Enum.TryParse(intentValue, false, out intent))
        {
            configuration = null;
            return false;
        }

        var backend = StoreBackendCandidate.Mono;
        if (!string.IsNullOrEmpty(backendValue) &&
            !Enum.TryParse(backendValue, false, out backend))
        {
            configuration = null;
            return false;
        }

        if (intent == WindowsReleaseBuildIntent.CanonicalStore)
        {
            configuration = backend == StoreBackendCandidate.Mono
                ? WindowsReleaseBackendConfiguration.CanonicalStoreMono
                : null;
            return configuration != null;
        }

        configuration = backend == StoreBackendCandidate.Mono
            ? WindowsReleaseBackendConfiguration.ComparisonMono
            : WindowsReleaseBackendConfiguration.IL2CPP;
        return true;
    }

    public static bool TryResolveBackend(
        string value,
        out WindowsReleaseBackendConfiguration configuration)
    {
        if (string.IsNullOrEmpty(value) ||
            string.Equals(value, StoreBackendCandidate.Mono.ToString(),
                StringComparison.Ordinal))
        {
            configuration = WindowsReleaseBackendConfiguration.CanonicalStoreMono;
            return true;
        }

        configuration = null;
        return false;
    }

    public static int ValidateStripping(
        WindowsReleaseBackendConfiguration configuration,
        ManagedStrippingLevel stripping)
    {
        return configuration != null && stripping == configuration.Stripping
            ? WindowsReleaseExitCodes.Success
            : WindowsReleaseExitCodes.UnsupportedConfiguration;
    }

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

    public static int MapBuildResult(BuildResult result, int totalErrors)
    {
        switch (result)
        {
            case BuildResult.Succeeded:
                return totalErrors == 0
                    ? WindowsReleaseExitCodes.Success
                    : WindowsReleaseExitCodes.BuildErrorsRecorded;
            case BuildResult.Failed:
                return WindowsReleaseExitCodes.BuildFailed;
            case BuildResult.Cancelled:
                return WindowsReleaseExitCodes.BuildCancelled;
            default:
                return WindowsReleaseExitCodes.BuildUnknownResult;
        }
    }

    public static int ValidateForbiddenManagedAssemblies(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent;
        }

        var playerRoot = Path.GetDirectoryName(outputPath);
        var executableName = Path.GetFileNameWithoutExtension(outputPath);
        if (string.IsNullOrEmpty(playerRoot) || string.IsNullOrEmpty(executableName))
        {
            return WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent;
        }

        var dataRoot = Path.Combine(playerRoot, executableName + "_Data");
        var managedRoot = Path.Combine(dataRoot, "Managed");
        foreach (var artifact in ForbiddenManagedAssemblies)
        {
            if (File.Exists(Path.Combine(managedRoot, artifact)))
            {
                return WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent;
            }
        }

        var scriptingAssembliesPath = Path.Combine(dataRoot, "ScriptingAssemblies.json");
        if (!File.Exists(scriptingAssembliesPath))
        {
            return WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent;
        }

        var scriptingAssemblies = File.ReadAllText(scriptingAssembliesPath);
        return ForbiddenManagedAssemblies.Any(artifact =>
                scriptingAssemblies.IndexOf(
                    "\"" + artifact + "\"", StringComparison.Ordinal) >= 0)
            ? WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent
            : WindowsReleaseExitCodes.Success;
    }

    public static int ValidateBuildReportEvidence(
        int metadataErrorCount, int reportErrorCount, int structuredErrorCount)
    {
        return metadataErrorCount == reportErrorCount &&
               structuredErrorCount == reportErrorCount
            ? WindowsReleaseExitCodes.Success
            : WindowsReleaseExitCodes.BuildReportCountMismatch;
    }

    public static int ValidateCanonicalArtifactIdentity(
        params ReleaseStoreIdentityV1[] identities)
    {
        if (identities == null || identities.Length == 0)
        {
            return WindowsReleaseExitCodes.BuildReportIdentityMismatch;
        }

        WindowsDistributionTargetConfiguration canonicalDistribution = null;
        foreach (var identity in identities)
        {
            if (identity == null ||
                !WindowsDistributionTargetPolicy.TryResolve(
                    identity.distributionTargetId, out var distribution) ||
                WindowsDistributionTargetPolicy.ValidateConfiguration(distribution) !=
                    WindowsDistributionValidationFailure.None)
            {
                return WindowsReleaseExitCodes.BuildReportIdentityMismatch;
            }

            if (canonicalDistribution == null)
            {
                canonicalDistribution = distribution;
            }
            else if (!string.Equals(
                         distribution.TargetId,
                         canonicalDistribution.TargetId,
                         StringComparison.Ordinal))
            {
                return WindowsReleaseExitCodes.BuildReportIdentityMismatch;
            }

            if (!DistributionIdentityMatches(identity, canonicalDistribution) ||
                !string.Equals(identity.storeConfigurationSchema,
                    StoreConfigurationSchema, StringComparison.Ordinal) ||
                !string.Equals(identity.storeConfigurationId,
                    StoreConfigurationId, StringComparison.Ordinal) ||
                !string.Equals(identity.buildIntent,
                    WindowsReleaseBuildIntent.CanonicalStore.ToString(),
                    StringComparison.Ordinal) ||
                !string.Equals(identity.backend, StoreBackendCandidate.Mono.ToString(),
                    StringComparison.Ordinal) ||
                !string.Equals(identity.scriptingBackend, Backend.ToString(),
                    StringComparison.Ordinal) ||
                !string.Equals(identity.managedStrippingLevel, Stripping.ToString(),
                    StringComparison.Ordinal) ||
                !identity.playerLogEnabled ||
                !string.Equals(identity.logPolicyId, LogPolicyId,
                    StringComparison.Ordinal) ||
                identity.automaticLogUpload ||
                !string.Equals(identity.payloadAudience, PayloadAudience,
                    StringComparison.Ordinal))
            {
                return WindowsReleaseExitCodes.BuildReportIdentityMismatch;
            }
        }

        return WindowsReleaseExitCodes.Success;
    }

    internal static int ValidateBuildReportIdentityAndCounts(
        WindowsReleaseMetadataV2 metadata,
        BuildReportSummaryV2 summary,
        BuildReportDetailsV1 details,
        WindowsReleaseBackendConfiguration configuration,
        WindowsDistributionTargetConfiguration distribution)
    {
        if (configuration == null || distribution == null ||
            WindowsDistributionTargetPolicy.ValidateConfiguration(distribution) !=
                WindowsDistributionValidationFailure.None ||
            metadata == null || summary == null || details == null ||
            !string.Equals(metadata.schemaVersion, MetadataSchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(summary.schemaVersion, BuildReportSummarySchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(details.schemaVersion, BuildReportDetailsSchemaVersion,
                StringComparison.Ordinal) ||
            !Same(metadata.runId, summary.runId, details.runId) ||
            !Same(metadata.artifactId, summary.artifactId, details.artifactId) ||
            !Same(metadata.sourceSha, summary.sourceSha, details.sourceSha) ||
            !Same(metadata.sourceTree, summary.sourceTree, details.sourceTree) ||
            !Same(metadata.configuration, summary.configuration, details.configuration) ||
            !string.Equals(summary.configuration, configuration.ConfigurationName,
                StringComparison.Ordinal) ||
            !Same(metadata.buildIntent, summary.buildIntent, details.buildIntent) ||
            !string.Equals(summary.buildIntent, configuration.Intent.ToString(),
                StringComparison.Ordinal) ||
            !Same(metadata.storeConfigurationSchema, summary.storeConfigurationSchema,
                details.storeConfigurationSchema) ||
            !string.Equals(summary.storeConfigurationSchema,
                configuration.StoreConfigurationSchema, StringComparison.Ordinal) ||
            !Same(metadata.storeConfigurationId, summary.storeConfigurationId,
                details.storeConfigurationId) ||
            !string.Equals(summary.storeConfigurationId,
                configuration.StoreConfigurationId, StringComparison.Ordinal) ||
            !Same(metadata.backend, summary.backend, details.backend) ||
            !string.Equals(summary.backend, configuration.Candidate.ToString(),
                StringComparison.Ordinal) ||
            !Same(metadata.scriptingBackend, summary.scriptingBackend,
                details.scriptingBackend) ||
            !string.Equals(summary.scriptingBackend, configuration.Backend.ToString(),
                StringComparison.Ordinal) ||
            !Same(metadata.managedStrippingLevel, summary.managedStrippingLevel,
                details.managedStrippingLevel) ||
            !string.Equals(summary.managedStrippingLevel,
                configuration.Stripping.ToString(), StringComparison.Ordinal) ||
            metadata.playerLogEnabled != summary.playerLogEnabled ||
            summary.playerLogEnabled != details.playerLogEnabled ||
            metadata.playerLogEnabled != PlayerLogEnabled ||
            !Same(metadata.logPolicyId, summary.logPolicyId, details.logPolicyId) ||
            !string.Equals(summary.logPolicyId, configuration.LogPolicyId,
                StringComparison.Ordinal) ||
            metadata.automaticLogUpload != summary.automaticLogUpload ||
            summary.automaticLogUpload != details.automaticLogUpload ||
            metadata.automaticLogUpload != AutomaticLogUpload ||
            !Same(metadata.payloadAudience, summary.payloadAudience,
                details.payloadAudience) ||
            !string.Equals(summary.payloadAudience, configuration.PayloadAudience,
                StringComparison.Ordinal) ||
            !Same(metadata.distributionTargetId, summary.distributionTargetId,
                details.distributionTargetId) ||
            !string.Equals(summary.distributionTargetId, distribution.TargetId,
                StringComparison.Ordinal) ||
            !Same(metadata.providerSelectionMode, summary.providerSelectionMode,
                details.providerSelectionMode) ||
            !string.Equals(summary.providerSelectionMode,
                distribution.ProviderSelectionMode.ToString(), StringComparison.Ordinal) ||
            !Same(metadata.expectedProviderId, summary.expectedProviderId,
                details.expectedProviderId) ||
            !string.Equals(summary.expectedProviderId, distribution.ExpectedProviderId,
                StringComparison.Ordinal) ||
            !SameSequence(metadata.expectedLaunchArguments,
                summary.expectedLaunchArguments, details.expectedLaunchArguments,
                distribution.ExpectedLaunchArguments) ||
            !SameSequence(metadata.requiredArtifacts,
                summary.requiredArtifacts, details.requiredArtifacts,
                distribution.RequiredArtifacts) ||
            !SameSequence(metadata.forbiddenArtifacts,
                summary.forbiddenArtifacts, details.forbiddenArtifacts,
                distribution.ForbiddenArtifacts) ||
            !Same(metadata.expectedStoreLaunch, summary.expectedStoreLaunch,
                details.expectedStoreLaunch) ||
            !string.Equals(summary.expectedStoreLaunch, distribution.ExpectedStoreLaunch,
                StringComparison.Ordinal) ||
            !Same(metadata.backendComparisonId, summary.backendComparisonId,
                details.backendComparisonId) ||
            !Same(metadata.comparisonRole, summary.comparisonRole, details.comparisonRole) ||
            !string.Equals(summary.comparisonRole, configuration.ComparisonRole.ToString(),
                StringComparison.Ordinal) ||
            !Same(metadata.buildResult, summary.result, details.result))
        {
            return WindowsReleaseExitCodes.BuildReportIdentityMismatch;
        }

        return metadata.errorCount == summary.totalErrors &&
               summary.totalErrors == details.totalErrors &&
               summary.errorRecordCount == details.errorRecordCount &&
               details.errorRecordCount == details.totalErrors &&
               metadata.warningCount == summary.totalWarnings &&
               summary.totalWarnings == details.totalWarnings &&
               summary.warningRecordCount == details.warningRecordCount &&
               details.warningRecordCount == details.totalWarnings
            ? WindowsReleaseExitCodes.Success
            : WindowsReleaseExitCodes.BuildReportCountMismatch;
    }

    private static bool Same(string first, string second, string third)
    {
        return !string.IsNullOrWhiteSpace(first) &&
               string.Equals(first, second, StringComparison.Ordinal) &&
               string.Equals(second, third, StringComparison.Ordinal);
    }

    private static bool SameSequence(
        IEnumerable<string> first,
        IEnumerable<string> second,
        IEnumerable<string> third,
        IEnumerable<string> expected)
    {
        if (first == null || second == null || third == null || expected == null)
        {
            return false;
        }

        var canonical = new List<string>(expected);
        return new List<string>(first).SequenceEqual(canonical) &&
               new List<string>(second).SequenceEqual(canonical) &&
               new List<string>(third).SequenceEqual(canonical);
    }

    private static bool DistributionIdentityMatches(
        ReleaseStoreIdentityV1 identity,
        WindowsDistributionTargetConfiguration distribution)
    {
        return string.Equals(identity.providerSelectionMode,
                   distribution.ProviderSelectionMode.ToString(), StringComparison.Ordinal) &&
               string.Equals(identity.expectedProviderId,
                   distribution.ExpectedProviderId, StringComparison.Ordinal) &&
               SameSequence(identity.expectedLaunchArguments,
                   identity.expectedLaunchArguments, identity.expectedLaunchArguments,
                   distribution.ExpectedLaunchArguments) &&
               SameSequence(identity.requiredArtifacts,
                   identity.requiredArtifacts, identity.requiredArtifacts,
                   distribution.RequiredArtifacts) &&
               SameSequence(identity.forbiddenArtifacts,
                   identity.forbiddenArtifacts, identity.forbiddenArtifacts,
                   distribution.ForbiddenArtifacts) &&
               string.Equals(identity.expectedStoreLaunch,
                   distribution.ExpectedStoreLaunch, StringComparison.Ordinal);
    }

    public static int ResolvePostBuildExitCode(
        int buildExitCode,
        bool summaryWritten,
        bool detailsWritten,
        bool metadataWritten,
        int metadataErrorCount,
        int reportErrorCount,
        int structuredErrorCount,
        int evidenceIdentityAndCounts =
            WindowsReleaseExitCodes.Success)
    {
        if (buildExitCode == WindowsReleaseExitCodes.SettingsRestoreFailure ||
            buildExitCode == WindowsReleaseExitCodes.ManagedPluginApplyFailure)
        {
            return buildExitCode;
        }

        if (!detailsWritten)
        {
            return WindowsReleaseExitCodes.BuildReportDetailsWriteFailure;
        }

        if (!summaryWritten)
        {
            return WindowsReleaseExitCodes.BuildReportWriteFailure;
        }

        var evidence = ValidateBuildReportEvidence(
            metadataErrorCount, reportErrorCount, structuredErrorCount);
        if (evidence != WindowsReleaseExitCodes.Success)
        {
            return evidence;
        }

        if (evidenceIdentityAndCounts != WindowsReleaseExitCodes.Success)
        {
            return evidenceIdentityAndCounts;
        }

        if (!metadataWritten)
        {
            return WindowsReleaseExitCodes.MetadataWriteFailure;
        }

        return buildExitCode;
    }
}

public enum WindowsReleaseBuildIntent
{
    CanonicalStore = 0,
    BackendComparison = 1,
}

public enum StoreBackendCandidate
{
    Mono = 0,
    IL2CPP = 1,
}

public enum StoreBackendComparisonRole
{
    CanonicalStore = 0,
    MonoControl = 1,
    IL2CPPCandidate = 2,
}

public sealed class WindowsReleaseBackendConfiguration
{
    public static readonly WindowsReleaseBackendConfiguration CanonicalStoreMono =
        new WindowsReleaseBackendConfiguration(
            WindowsReleaseBuildIntent.CanonicalStore,
            StoreBackendCandidate.Mono,
            ScriptingImplementation.Mono2x,
            ManagedStrippingLevel.Disabled,
            StoreBackendComparisonRole.CanonicalStore,
            WindowsReleaseBuildPolicy.ConfigurationName,
            WindowsReleaseBuildPolicy.StoreConfigurationSchema,
            WindowsReleaseBuildPolicy.StoreConfigurationId,
            WindowsReleaseBuildPolicy.LogPolicyId,
            WindowsReleaseBuildPolicy.PayloadAudience);

    public static readonly WindowsReleaseBackendConfiguration ComparisonMono =
        new WindowsReleaseBackendConfiguration(
            WindowsReleaseBuildIntent.BackendComparison,
            StoreBackendCandidate.Mono,
            ScriptingImplementation.Mono2x,
            ManagedStrippingLevel.Disabled,
            StoreBackendComparisonRole.MonoControl,
            "Windows-x64-NonDevelopment-Mono",
            "comparison-1.0",
            "not-canonical-backend-comparison",
            WindowsReleaseBuildPolicy.LogPolicyId,
            "InternalRc");

    public static readonly WindowsReleaseBackendConfiguration IL2CPP =
        new WindowsReleaseBackendConfiguration(
            WindowsReleaseBuildIntent.BackendComparison,
            StoreBackendCandidate.IL2CPP,
            ScriptingImplementation.IL2CPP,
            ManagedStrippingLevel.Minimal,
            StoreBackendComparisonRole.IL2CPPCandidate,
            "Windows-x64-NonDevelopment-IL2CPP",
            "comparison-1.0",
            "not-canonical-backend-comparison",
            WindowsReleaseBuildPolicy.LogPolicyId,
            "InternalRc");

    private WindowsReleaseBackendConfiguration(
        WindowsReleaseBuildIntent intent,
        StoreBackendCandidate candidate,
        ScriptingImplementation backend,
        ManagedStrippingLevel stripping,
        StoreBackendComparisonRole comparisonRole,
        string configurationName,
        string storeConfigurationSchema,
        string storeConfigurationId,
        string logPolicyId,
        string payloadAudience)
    {
        Intent = intent;
        Candidate = candidate;
        Backend = backend;
        Stripping = stripping;
        ComparisonRole = comparisonRole;
        ConfigurationName = configurationName;
        StoreConfigurationSchema = storeConfigurationSchema;
        StoreConfigurationId = storeConfigurationId;
        LogPolicyId = logPolicyId;
        PayloadAudience = payloadAudience;
    }

    public WindowsReleaseBuildIntent Intent { get; }
    public StoreBackendCandidate Candidate { get; }
    public ScriptingImplementation Backend { get; }
    public ManagedStrippingLevel Stripping { get; }
    public StoreBackendComparisonRole ComparisonRole { get; }
    public string ConfigurationName { get; }
    public string StoreConfigurationSchema { get; }
    public string StoreConfigurationId { get; }
    public string LogPolicyId { get; }
    public string PayloadAudience { get; }
    public Il2CppCompilerConfiguration Il2CppCompilerConfiguration =>
        WindowsReleaseBuildPolicy.Il2CppCompiler;
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

[Serializable]
public sealed class ReleaseStoreIdentityV1
{
    public string distributionTargetId;
    public string providerSelectionMode;
    public string expectedProviderId;
    public string[] expectedLaunchArguments;
    public string[] requiredArtifacts;
    public string[] forbiddenArtifacts;
    public string expectedStoreLaunch;
    public string storeConfigurationSchema;
    public string storeConfigurationId;
    public string buildIntent;
    public string backend;
    public string scriptingBackend;
    public string managedStrippingLevel;
    public bool playerLogEnabled;
    public string logPolicyId;
    public bool automaticLogUpload;
    public string payloadAudience;
}

public static class WindowsReleaseExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 10;
    public const int UnsupportedConfiguration = 11;
    public const int ActiveBuildTargetMismatch = 12;
    public const int UnityVersionMismatch = 13;
    public const int SceneContractMismatch = 14;
    public const int UnsupportedDistributionTarget = 15;
    public const int PlayerSettingsContractMismatch = 20;
    public const int SettingsApplyFailure = 21;
    public const int SettingsRestoreFailure = 22;
    public const int ManagedPluginApplyFailure = 23;
    public const int ManagedPluginRestoreFailure = 24;
    public const int BuildFailed = 30;
    public const int BuildCancelled = 31;
    public const int BuildUnknownResult = 32;
    public const int BuildErrorsRecorded = 33;
    public const int ForbiddenManagedAssemblyPresent = 34;
    public const int MetadataWriteFailure = 40;
    public const int BuildReportWriteFailure = 41;
    public const int BuildReportDetailsWriteFailure = 42;
    public const int BuildReportCountMismatch = 43;
    public const int BuildReportIdentityMismatch = 44;
    public const int InternalException = 50;

    public static int[] All =>
        new[]
        {
            Success, InvalidArguments, UnsupportedConfiguration, ActiveBuildTargetMismatch,
            UnityVersionMismatch, SceneContractMismatch, UnsupportedDistributionTarget,
            PlayerSettingsContractMismatch,
            SettingsApplyFailure, SettingsRestoreFailure,
            ManagedPluginApplyFailure, ManagedPluginRestoreFailure,
            BuildFailed, BuildCancelled, BuildUnknownResult, BuildErrorsRecorded,
            ForbiddenManagedAssemblyPresent, MetadataWriteFailure,
            BuildReportWriteFailure, BuildReportDetailsWriteFailure,
            BuildReportCountMismatch, BuildReportIdentityMismatch, InternalException,
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
    public Il2CppCompilerConfiguration il2cppCompilerConfiguration;
    public bool playerLog;
    public StackTraceLogType warningStackTrace;
}

public static class WindowsReleaseSettingsTransaction
{
    public static int Run(
        IWindowsReleaseSettings settings,
        Func<int> build,
        ReleaseSettingsTransactionRecordV1 record = null)
    {
        return Run(
            settings,
            WindowsReleaseBuildPolicy.DefaultConfiguration,
            build,
            record);
    }

    public static int Run(
        IWindowsReleaseSettings settings,
        WindowsReleaseBackendConfiguration configuration,
        Func<int> build,
        ReleaseSettingsTransactionRecordV1 record = null)
    {
        var snapshot = settings.Capture();
        if (record != null)
        {
            record.originalSettings = snapshot;
            record.requiredSettings = new ReleaseSettingsSnapshot
            {
                backend = configuration.Backend,
                stripping = configuration.Stripping,
                il2cppCompilerConfiguration =
                    configuration.Il2CppCompilerConfiguration,
                playerLog = WindowsReleaseBuildPolicy.PlayerLogEnabled,
                warningStackTrace = WindowsReleaseBuildPolicy.WarningStackTrace,
            };
            record.backendChanged = snapshot.backend != configuration.Backend;
            record.strippingChanged = snapshot.stripping != configuration.Stripping;
            record.il2cppCompilerConfigurationChanged =
                configuration.Candidate == StoreBackendCandidate.IL2CPP &&
                snapshot.il2cppCompilerConfiguration !=
                    configuration.Il2CppCompilerConfiguration;
            record.playerLogChanged =
                snapshot.playerLog != WindowsReleaseBuildPolicy.PlayerLogEnabled;
            record.warningStackTraceChanged =
                snapshot.warningStackTrace != WindowsReleaseBuildPolicy.WarningStackTrace;
        }
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
                if (record != null)
                {
                    record.appliedVerification = settings.IsRequired();
                }
            }
            catch
            {
                result = WindowsReleaseExitCodes.SettingsApplyFailure;
                if (record != null)
                {
                    record.appliedVerification = false;
                }
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
                if (record != null)
                {
                    record.restoreAttempted = true;
                }
                try
                {
                    settings.Restore(snapshot);
                    var restored = settings.IsRestored(snapshot);
                    if (record != null)
                    {
                        record.restoredVerification = restored;
                        record.restoreResult = restored ? "Restored" : "VerificationFailed";
                    }
                    if (!restored)
                    {
                        result = WindowsReleaseExitCodes.SettingsRestoreFailure;
                    }
                }
                catch
                {
                    if (record != null)
                    {
                        record.restoredVerification = false;
                        record.restoreResult = "Exception";
                    }
                    result = WindowsReleaseExitCodes.SettingsRestoreFailure;
                }
            }
            else if (record != null)
            {
                record.restoreAttempted = false;
                record.restoredVerification = true;
                record.restoreResult = "NotRequired";
            }
        }

        return result;
    }
}

[Serializable]
public sealed class ReleaseSettingsTransactionRecordV1
{
    public string schemaVersion = "1.0";
    public ReleaseSettingsSnapshot originalSettings;
    public ReleaseSettingsSnapshot requiredSettings;
    public bool backendChanged;
    public bool strippingChanged;
    public bool il2cppCompilerConfigurationChanged;
    public bool playerLogChanged;
    public bool warningStackTraceChanged;
    public bool appliedVerification;
    public bool restoreAttempted;
    public string restoreResult;
    public bool restoredVerification;
    public ManagedPluginTransactionRecordV1 managedPluginTransaction;
}

public interface IWindowsReleaseManagedPluginSettings
{
    ManagedPluginSettingsSnapshot Capture();
    void ApplyRequired();
    bool IsRequired();
    void Restore(ManagedPluginSettingsSnapshot snapshot);
    bool IsRestored(ManagedPluginSettingsSnapshot snapshot);
}

[Serializable]
public struct ManagedPluginState
{
    public string assetPath;
    public bool compatibleWithAnyPlatform;
    public bool compatibleWithStandaloneWindows64;
}

[Serializable]
public struct ManagedPluginSettingsSnapshot
{
    public ManagedPluginState[] plugins;
}

[Serializable]
public sealed class ManagedPluginTransactionRecordV1
{
    public string schemaVersion = "1.0";
    public string[] assetPaths;
    public bool appliedVerification;
    public bool restoreAttempted;
    public string restoreResult;
    public bool restoredVerification;
}

public static class WindowsReleaseManagedPluginTransaction
{
    public static int Run(
        IWindowsReleaseManagedPluginSettings settings,
        Func<int> build,
        ManagedPluginTransactionRecordV1 record = null)
    {
        ManagedPluginSettingsSnapshot snapshot;
        try
        {
            snapshot = settings.Capture();
        }
        catch
        {
            return WindowsReleaseExitCodes.ManagedPluginApplyFailure;
        }

        if (record != null)
        {
            record.assetPaths = snapshot.plugins == null
                ? Array.Empty<string>()
                : snapshot.plugins.Select(plugin => plugin.assetPath).ToArray();
        }

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

                var applied = settings.IsRequired();
                if (record != null)
                {
                    record.appliedVerification = applied;
                }
                if (!applied)
                {
                    result = WindowsReleaseExitCodes.ManagedPluginApplyFailure;
                }
            }
            catch
            {
                if (record != null)
                {
                    record.appliedVerification = false;
                }
                result = WindowsReleaseExitCodes.ManagedPluginApplyFailure;
            }

            if (result != WindowsReleaseExitCodes.ManagedPluginApplyFailure)
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
                if (record != null)
                {
                    record.restoreAttempted = true;
                }
                try
                {
                    settings.Restore(snapshot);
                    var restored = settings.IsRestored(snapshot);
                    if (record != null)
                    {
                        record.restoredVerification = restored;
                        record.restoreResult = restored ? "Restored" : "VerificationFailed";
                    }
                    if (!restored)
                    {
                        result = WindowsReleaseExitCodes.ManagedPluginRestoreFailure;
                    }
                }
                catch
                {
                    if (record != null)
                    {
                        record.restoredVerification = false;
                        record.restoreResult = "Exception";
                    }
                    result = WindowsReleaseExitCodes.ManagedPluginRestoreFailure;
                }
            }
            else if (record != null)
            {
                record.restoreAttempted = false;
                record.restoredVerification = true;
                record.restoreResult = "NotRequired";
            }
        }

        return result;
    }
}

[Serializable]
public sealed class WindowsReleaseMetadataV2
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
    public string buildIntent;
    public string storeConfigurationSchema;
    public string storeConfigurationId;
    public string backend;
    public string scriptingBackend;
    public string managedStrippingLevel;
    public string il2cppCompilerConfiguration;
    public string nativeCompilerIdentity;
    public string windowsSdkIdentity;
    public string backendComparisonId;
    public string comparisonRole;
    public bool development;
    public bool connectWithProfiler;
    public bool deepProfiling;
    public bool allowDebugging;
    public bool scriptDebugging;
    public bool waitForPlayerConnection;
    public bool waitForDebugger;
    public bool forceAssertions;
    public string[] effectiveScenes;
    public bool playerLogEnabled;
    public string logPolicyId;
    public bool automaticLogUpload;
    public string payloadAudience;
    public string distributionTargetId;
    public string providerSelectionMode;
    public string expectedProviderId;
    public string[] expectedLaunchArguments;
    public string[] requiredArtifacts;
    public string[] forbiddenArtifacts;
    public string expectedStoreLaunch;
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
    public string buildReportSummaryFile;
    public string buildReportDetailsFile;
    public bool zeroErrorGatePassed;
    public bool metadataReportCountMatched;
    public bool structuredErrorCountMatched;
    public int cSharpExitCode;
    public string cSharpExitName;
    public string buildStartedUtc;
    public string buildCompletedUtc;
}
