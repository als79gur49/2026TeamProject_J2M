using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    public const string SourceShaArgument = "-releaseSourceSha";
    public const string SourceTreeArgument = "-releaseSourceTree";
    public const string MetadataPathArgument = "-releaseIntermediateMetadataPath";
    public const string BuildReportPathArgument = "-releaseBuildReportPath";
    public const string BuildReportDetailsPathArgument = "-releaseBuildReportDetailsPath";
    public const string SettingsTransactionPathArgument = "-releaseSettingsTransactionPath";
    public const string BackendArgument = "-releaseBackend";
    public const string BackendComparisonIdArgument = "-releaseBackendComparisonId";

    public static readonly string[] RequiredArgumentNames =
    {
        OutputPathArgument, RunIdArgument, ArtifactIdArgument,
        SourceShaArgument, SourceTreeArgument, MetadataPathArgument,
        BuildReportPathArgument, BuildReportDetailsPathArgument,
        SettingsTransactionPathArgument,
    };

    public static readonly string[] KnownArgumentNames =
        RequiredArgumentNames
            .Concat(new[] { BackendArgument, BackendComparisonIdArgument })
            .ToArray();

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

        if (!WindowsReleaseBuildPolicy.TryResolveBackend(
                arguments.TryGetValue(BackendArgument, out var backendValue)
                    ? backendValue
                    : string.Empty,
                out var configuration))
        {
            Debug.LogError("UNSUPPORTED_STORE_BACKEND");
            return WindowsReleaseExitCodes.UnsupportedConfiguration;
        }

        var comparisonId =
            arguments.TryGetValue(BackendComparisonIdArgument, out var suppliedComparisonId) &&
            !string.IsNullOrWhiteSpace(suppliedComparisonId)
                ? suppliedComparisonId
                : arguments[SourceShaArgument];

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

        var metadata = CreateMetadata(arguments, configuration, comparisonId);
        var settings = new UnityWindowsReleaseSettings(configuration);
        var settingsTransaction = new ReleaseSettingsTransactionRecordV1();
        var started = DateTime.UtcNow;
        BuildReport report = null;
        var result = WindowsReleaseSettingsTransaction.Run(
            settings,
            configuration,
            () =>
            {
                var outputPath = arguments[OutputPathArgument];
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                report = BuildPipeline.BuildPlayer(
                    WindowsReleaseBuildPolicy.CreateBuildOptions(outputPath));
                return WindowsReleaseBuildPolicy.MapBuildResult(
                    report.summary.result, (int)report.summary.totalErrors);
            },
            settingsTransaction);
        try
        {
            WriteJson(arguments[SettingsTransactionPathArgument], settingsTransaction);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        var summaryWritten = false;
        var detailsWritten = false;
        var metadataWritten = false;
        var reportErrorCount = -1;
        var structuredErrorCount = -1;
        BuildReportDetailsV1 details = null;
        BuildReportSummaryV2 summary = null;
        if (report != null)
        {
            PopulateReport(metadata, report, started);
            reportErrorCount = (int)report.summary.totalErrors;
            details = new BuildReportDetailsV1(
                report,
                arguments[RunIdArgument],
                arguments[ArtifactIdArgument],
                arguments[SourceShaArgument],
                arguments[SourceTreeArgument],
                configuration,
                comparisonId);
            structuredErrorCount = details.errorRecordCount;
            var detailsHash = string.Empty;
            try
            {
                WriteJson(arguments[BuildReportDetailsPathArgument], details);
                detailsHash = ComputeSha256(arguments[BuildReportDetailsPathArgument]);
                detailsWritten = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            try
            {
                summary = new BuildReportSummaryV2(
                    report,
                    details,
                    Path.GetFileName(arguments[BuildReportDetailsPathArgument]),
                    detailsHash,
                    configuration,
                    comparisonId);
                WriteJson(arguments[BuildReportPathArgument], summary);
                summaryWritten = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        metadata.zeroErrorGatePassed =
            report != null &&
            report.summary.result == BuildResult.Succeeded &&
            report.summary.totalErrors == 0;
        metadata.metadataReportCountMatched =
            report != null &&
            metadata.errorCount == reportErrorCount &&
            metadata.warningCount == (int)report.summary.totalWarnings;
        metadata.structuredErrorCountMatched =
            report != null && structuredErrorCount == reportErrorCount;
        var evidenceIdentityAndCounts =
            WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                metadata, summary, details, configuration);
        var intendedResult = WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
            result,
            summaryWritten,
            detailsWritten,
            metadataWritten: true,
            metadata.errorCount,
            reportErrorCount,
            structuredErrorCount,
            evidenceIdentityAndCounts);
        metadata.cSharpExitCode = intendedResult;
        metadata.cSharpExitName = ExitCodeName(intendedResult);
        metadata.buildCompletedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        try
        {
            WriteJson(arguments[MetadataPathArgument], metadata);
            metadataWritten = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        return WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
            result,
            summaryWritten,
            detailsWritten,
            metadataWritten,
            metadata.errorCount,
            reportErrorCount,
            structuredErrorCount,
            evidenceIdentityAndCounts);
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
            foreach (var name in KnownArgumentNames)
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

    private static WindowsReleaseMetadataV2 CreateMetadata(
        IReadOnlyDictionary<string, string> arguments,
        WindowsReleaseBackendConfiguration configuration,
        string comparisonId)
    {
        return new WindowsReleaseMetadataV2
        {
            schemaVersion = WindowsReleaseBuildPolicy.MetadataSchemaVersion,
            runId = arguments[RunIdArgument],
            artifactId = arguments[ArtifactIdArgument],
            sourceSha = arguments[SourceShaArgument],
            sourceTree = arguments[SourceTreeArgument],
            unityVersion = Application.unityVersion,
            unityRevision = ReadUnityRevision(),
            buildTarget = BuildTarget.StandaloneWindows64.ToString(),
            architecture = WindowsReleaseBuildPolicy.Architecture,
            configuration = configuration.ConfigurationName,
            backend = configuration.Backend.ToString(),
            managedStrippingLevel = configuration.Stripping.ToString(),
            il2cppCompilerConfiguration =
                configuration.Il2CppCompilerConfiguration.ToString(),
            nativeCompilerIdentity = string.Empty,
            windowsSdkIdentity = string.Empty,
            backendComparisonId = comparisonId,
            comparisonRole = configuration.ComparisonRole.ToString(),
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
            buildResult = "NotProduced",
            warningCount = -1,
            errorCount = -1,
            buildReportSummaryFile = Path.GetFileName(arguments[BuildReportPathArgument]),
            buildReportDetailsFile =
                Path.GetFileName(arguments[BuildReportDetailsPathArgument]),
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
        WindowsReleaseMetadataV2 metadata, BuildReport report, DateTime started)
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

    private static string ComputeSha256(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha256 = SHA256.Create())
        {
            return ToHex(sha256.ComputeHash(stream));
        }
    }

    internal static string ComputeMessageSha256(string value)
    {
        using (var sha256 = SHA256.Create())
        {
            return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }
    }

    private static string ToHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string ExitCodeName(int code)
    {
        switch (code)
        {
            case WindowsReleaseExitCodes.Success:
                return "Success";
            case WindowsReleaseExitCodes.BuildFailed:
                return "BuildFailed";
            case WindowsReleaseExitCodes.BuildCancelled:
                return "BuildCancelled";
            case WindowsReleaseExitCodes.BuildUnknownResult:
                return "BuildUnknownResult";
            case WindowsReleaseExitCodes.BuildErrorsRecorded:
                return "BuildErrorsRecorded";
            case WindowsReleaseExitCodes.MetadataWriteFailure:
                return "MetadataWriteFailure";
            case WindowsReleaseExitCodes.BuildReportWriteFailure:
                return "BuildReportWriteFailure";
            case WindowsReleaseExitCodes.BuildReportDetailsWriteFailure:
                return "BuildReportDetailsWriteFailure";
            case WindowsReleaseExitCodes.BuildReportCountMismatch:
                return "BuildReportCountMismatch";
            case WindowsReleaseExitCodes.BuildReportIdentityMismatch:
                return "BuildReportIdentityMismatch";
            case WindowsReleaseExitCodes.SettingsRestoreFailure:
                return "SettingsRestoreFailure";
            default:
                return "ExitCode-" + code.ToString(CultureInfo.InvariantCulture);
        }
    }
}

internal sealed class UnityWindowsReleaseSettings : IWindowsReleaseSettings
{
    private readonly WindowsReleaseBackendConfiguration configuration;

    public UnityWindowsReleaseSettings()
        : this(WindowsReleaseBuildPolicy.DefaultConfiguration)
    {
    }

    public UnityWindowsReleaseSettings(
        WindowsReleaseBackendConfiguration configuration)
    {
        this.configuration = configuration ??
                             WindowsReleaseBuildPolicy.DefaultConfiguration;
    }

    public ReleaseSettingsSnapshot Capture()
    {
        return new ReleaseSettingsSnapshot
        {
            backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone),
            stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone),
            il2cppCompilerConfiguration =
                PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone),
            playerLog = PlayerSettings.usePlayerLog,
            warningStackTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning),
        };
    }

    public void ApplyRequired()
    {
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Standalone, configuration.Backend);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Standalone, configuration.Stripping);
        if (configuration.Candidate == StoreBackendCandidate.IL2CPP)
        {
            PlayerSettings.SetIl2CppCompilerConfiguration(
                NamedBuildTarget.Standalone,
                configuration.Il2CppCompilerConfiguration);
        }
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SetStackTraceLogType(
            LogType.Warning, WindowsReleaseBuildPolicy.WarningStackTrace);
    }

    public bool IsRequired()
    {
        return PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) ==
                   configuration.Backend &&
               PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone) ==
                   configuration.Stripping &&
               (configuration.Candidate != StoreBackendCandidate.IL2CPP ||
                PlayerSettings.GetIl2CppCompilerConfiguration(
                    NamedBuildTarget.Standalone) ==
                configuration.Il2CppCompilerConfiguration) &&
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
        PlayerSettings.SetIl2CppCompilerConfiguration(
            NamedBuildTarget.Standalone, snapshot.il2cppCompilerConfiguration);
        PlayerSettings.usePlayerLog = snapshot.playerLog;
        PlayerSettings.SetStackTraceLogType(LogType.Warning, snapshot.warningStackTrace);
    }

    public bool IsRestored(ReleaseSettingsSnapshot snapshot)
    {
        var current = Capture();
        return current.backend == snapshot.backend &&
               current.stripping == snapshot.stripping &&
               current.il2cppCompilerConfiguration ==
                   snapshot.il2cppCompilerConfiguration &&
               current.playerLog == snapshot.playerLog &&
               current.warningStackTrace == snapshot.warningStackTrace;
    }
}

[Serializable]
internal sealed class BuildReportSummaryV2
{
    public string schemaVersion = WindowsReleaseBuildPolicy.BuildReportSummarySchemaVersion;
    public string runId;
    public string artifactId;
    public string sourceSha;
    public string sourceTree;
    public string configuration;
    public string backend;
    public string backendComparisonId;
    public string comparisonRole;
    public string result;
    public int totalErrors;
    public int totalWarnings;
    public ulong totalSize;
    public double totalTimeSeconds;
    public string outputPath;
    public string detailsFile;
    public string detailsSha256;
    public int errorRecordCount;
    public int warningRecordCount;
    public string[] distinctErrorMessageHashes;

    internal BuildReportSummaryV2()
    {
    }

    public BuildReportSummaryV2(
        BuildReport report,
        BuildReportDetailsV1 details,
        string detailsFileName,
        string detailsHash,
        WindowsReleaseBackendConfiguration configuration,
        string comparisonId)
    {
        runId = details.runId;
        artifactId = details.artifactId;
        sourceSha = details.sourceSha;
        sourceTree = details.sourceTree;
        this.configuration = configuration.ConfigurationName;
        backend = configuration.Backend.ToString();
        backendComparisonId = comparisonId;
        comparisonRole = configuration.ComparisonRole.ToString();
        result = report.summary.result.ToString();
        totalErrors = report.summary.totalErrors;
        totalWarnings = report.summary.totalWarnings;
        totalSize = report.summary.totalSize;
        totalTimeSeconds = report.summary.totalTime.TotalSeconds;
        outputPath = Path.GetFileName(report.summary.outputPath);
        detailsFile = detailsFileName;
        detailsSha256 = detailsHash;
        errorRecordCount = details.errorRecordCount;
        warningRecordCount = details.warningRecordCount;
        distinctErrorMessageHashes = details.steps
            .SelectMany(step => step.messages)
            .Where(message => BuildReportMessageV1.IsErrorType(message.type))
            .Select(message => message.messageSha256)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(hash => hash, StringComparer.Ordinal)
            .ToArray();
    }
}

[Serializable]
internal sealed class BuildReportDetailsV1
{
    public string schemaVersion = WindowsReleaseBuildPolicy.BuildReportDetailsSchemaVersion;
    public string runId;
    public string artifactId;
    public string sourceSha;
    public string sourceTree;
    public string configuration;
    public string backend;
    public string backendComparisonId;
    public string comparisonRole;
    public string unityVersion;
    public string result;
    public int totalErrors;
    public int totalWarnings;
    public int errorRecordCount;
    public int warningRecordCount;
    public string captureLimitation;
    public BuildReportStepV1[] steps;

    internal BuildReportDetailsV1()
    {
    }

    public BuildReportDetailsV1(
        BuildReport report,
        string releaseRunId,
        string releaseArtifactId,
        string releaseSourceSha,
        string releaseSourceTree,
        WindowsReleaseBackendConfiguration configuration,
        string comparisonId)
    {
        runId = releaseRunId;
        artifactId = releaseArtifactId;
        sourceSha = releaseSourceSha;
        sourceTree = releaseSourceTree;
        this.configuration = configuration.ConfigurationName;
        backend = configuration.Backend.ToString();
        backendComparisonId = comparisonId;
        comparisonRole = configuration.ComparisonRole.ToString();
        unityVersion = Application.unityVersion;
        result = report.summary.result.ToString();
        totalErrors = (int)report.summary.totalErrors;
        totalWarnings = (int)report.summary.totalWarnings;
        steps = report.steps
            .Select((step, index) => new BuildReportStepV1(step, index))
            .ToArray();
        errorRecordCount = steps
            .SelectMany(step => step.messages)
            .Count(message => BuildReportMessageV1.IsErrorType(message.type));
        warningRecordCount = steps
            .SelectMany(step => step.messages)
            .Count(message => string.Equals(
                message.type, LogType.Warning.ToString(), StringComparison.Ordinal));
        captureLimitation =
            errorRecordCount == totalErrors &&
            warningRecordCount == totalWarnings
            ? string.Empty
            : "STRUCTURED_BUILDREPORT_COUNT_LIMITATION";
    }
}

[Serializable]
internal sealed class BuildReportStepV1
{
    public int index;
    public string name;
    public int depth;
    public double durationSeconds;
    public BuildReportMessageV1[] messages;

    public BuildReportStepV1(BuildStep step, int stepIndex)
    {
        index = stepIndex;
        name = step.name;
        depth = step.depth;
        durationSeconds = step.duration.TotalSeconds;
        messages = step.messages
            .Select((message, messageIndex) =>
                new BuildReportMessageV1(message, messageIndex))
            .ToArray();
    }
}

[Serializable]
internal sealed class BuildReportMessageV1
{
    public int index;
    public string type;
    public string content;
    public string normalizedMessage;
    public string messageSha256;
    public string stackTrace;

    public BuildReportMessageV1(BuildStepMessage message, int messageIndex)
    {
        index = messageIndex;
        type = message.type.ToString();
        content = message.content ?? string.Empty;
        normalizedMessage = NormalizeMessage(content);
        messageSha256 = WindowsReleaseBuildCli.ComputeMessageSha256(normalizedMessage);
        stackTrace = ExtractStackTrace(content);
    }

    internal static bool IsErrorType(string value)
    {
        return string.Equals(value, LogType.Error.ToString(), StringComparison.Ordinal) ||
               string.Equals(value, LogType.Assert.ToString(), StringComparison.Ordinal) ||
               string.Equals(value, LogType.Exception.ToString(), StringComparison.Ordinal);
    }

    private static string NormalizeMessage(string value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();
    }

    private static string ExtractStackTrace(string value)
    {
        var normalized = (value ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var firstStackLine = Array.FindIndex(lines, line =>
            line.StartsWith("at ", StringComparison.Ordinal) ||
            line.StartsWith("  at ", StringComparison.Ordinal));
        return firstStackLine < 0
            ? string.Empty
            : string.Join("\n", lines.Skip(firstStackLine));
    }
}
