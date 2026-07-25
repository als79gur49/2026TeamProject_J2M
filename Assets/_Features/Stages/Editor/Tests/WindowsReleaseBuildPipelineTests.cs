using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class WindowsReleaseBuildPipelineTests
    {
        [TestCase(BuildOptions.Development)]
        [TestCase(BuildOptions.ConnectWithProfiler)]
        [TestCase(BuildOptions.AllowDebugging)]
        [TestCase(BuildOptions.WaitForPlayerConnection)]
        [TestCase(BuildOptions.ForceEnableAssertions)]
        public void BuildOptions_ExcludeDevelopmentAndDebugFlags(BuildOptions forbidden)
        {
            var options = WindowsReleaseBuildPolicy.CreateBuildOptions("out.exe");
            Assert.That(options.options & forbidden, Is.EqualTo(BuildOptions.None));
        }

        [Test]
        public void BuildOptions_TargetWindows64AndExactOrderedScenes()
        {
            var options = WindowsReleaseBuildPolicy.CreateBuildOptions("out.exe");
            Assert.That(options.target, Is.EqualTo(BuildTarget.StandaloneWindows64));
            Assert.That(options.scenes, Is.EqualTo(new[]
            {
                "Assets/Scenes/MainMenuScene.unity",
                "Assets/Scenes/UIAudioScene.unity",
            }));
            Assert.That(options.options, Is.EqualTo(BuildOptions.None));
        }

        [Test]
        public void Policy_FixesMonoStrippingPlayerLogAndStackTrace()
        {
            Assert.That(WindowsReleaseBuildPolicy.Backend,
                Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(WindowsReleaseBuildPolicy.Stripping,
                Is.EqualTo(ManagedStrippingLevel.Disabled));
            Assert.That(WindowsReleaseBuildPolicy.PlayerLogEnabled, Is.True);
            Assert.That(WindowsReleaseBuildPolicy.WarningStackTrace,
                Is.EqualTo(StackTraceLogType.ScriptOnly));
        }

        [Test]
        public void Policy_BackendIsMono()
        {
            Assert.That(WindowsReleaseBuildPolicy.Backend,
                Is.EqualTo(ScriptingImplementation.Mono2x));
        }

        [Test]
        public void BackendPolicy_OmittedValueDefaultsToMono()
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveBackend(
                string.Empty, out var configuration), Is.True);
            Assert.That(configuration.Candidate, Is.EqualTo(StoreBackendCandidate.Mono));
            Assert.That(configuration.Backend, Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(configuration.ComparisonRole,
                Is.EqualTo(StoreBackendComparisonRole.MonoControl));
        }

        [TestCase("Mono", StoreBackendCandidate.Mono, ScriptingImplementation.Mono2x)]
        [TestCase("IL2CPP", StoreBackendCandidate.IL2CPP, ScriptingImplementation.IL2CPP)]
        public void BackendPolicy_ExplicitValueCreatesExactPolicy(
            string value,
            StoreBackendCandidate candidate,
            ScriptingImplementation backend)
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveBackend(
                value, out var configuration), Is.True);
            Assert.That(configuration.Candidate, Is.EqualTo(candidate));
            Assert.That(configuration.Backend, Is.EqualTo(backend));
        }

        [Test]
        public void BackendPolicy_UnknownValueIsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveBackend(
                "il2cpp", out _), Is.False);
            Assert.That(WindowsReleaseBuildPolicy.TryResolveBackend(
                "Unknown", out _), Is.False);
        }

        [Test]
        public void BackendPolicy_UsesBackendSpecificConfigurationNames()
        {
            Assert.That(WindowsReleaseBackendConfiguration.Mono.ConfigurationName,
                Is.EqualTo("Windows-x64-NonDevelopment-Mono"));
            Assert.That(WindowsReleaseBackendConfiguration.IL2CPP.ConfigurationName,
                Is.EqualTo("Windows-x64-NonDevelopment-IL2CPP"));
        }

        [Test]
        public void BackendPolicy_UsesBackendSpecificStripping()
        {
            Assert.That(WindowsReleaseBackendConfiguration.Mono.Stripping,
                Is.EqualTo(ManagedStrippingLevel.Disabled));
            Assert.That(WindowsReleaseBackendConfiguration.IL2CPP.Stripping,
                Is.EqualTo(ManagedStrippingLevel.Minimal));
            Assert.That(WindowsReleaseBackendConfiguration.IL2CPP.Il2CppCompilerConfiguration,
                Is.EqualTo(Il2CppCompilerConfiguration.Release));
        }

        [Test]
        public void BackendPolicy_RejectsUnsupportedIl2CppStripping()
        {
            Assert.That(WindowsReleaseBuildPolicy.ValidateStripping(
                    WindowsReleaseBackendConfiguration.IL2CPP,
                    ManagedStrippingLevel.Disabled),
                Is.EqualTo(WindowsReleaseExitCodes.UnsupportedConfiguration));
            Assert.That(WindowsReleaseBuildPolicy.ValidateStripping(
                    WindowsReleaseBackendConfiguration.IL2CPP,
                    ManagedStrippingLevel.Minimal),
                Is.EqualTo(WindowsReleaseExitCodes.Success));
        }

        [Test]
        public void Policy_ManagedStrippingIsDisabled()
        {
            Assert.That(WindowsReleaseBuildPolicy.Stripping,
                Is.EqualTo(ManagedStrippingLevel.Disabled));
        }

        [Test]
        public void Policy_PlayerLogIsEnabled()
        {
            Assert.That(WindowsReleaseBuildPolicy.PlayerLogEnabled, Is.True);
        }

        [Test]
        public void Policy_WarningStackTraceIsScriptOnly()
        {
            Assert.That(WindowsReleaseBuildPolicy.WarningStackTrace,
                Is.EqualTo(StackTraceLogType.ScriptOnly));
        }

        [Test]
        public void BuildOptions_TargetIsStandaloneWindows64()
        {
            Assert.That(WindowsReleaseBuildPolicy.CreateBuildOptions("out.exe").target,
                Is.EqualTo(BuildTarget.StandaloneWindows64));
        }

        [Test]
        public void BuildOptions_ScenesAreExactAndOrdered()
        {
            Assert.That(WindowsReleaseBuildPolicy.CreateBuildOptions("out.exe").scenes,
                Is.EqualTo(WindowsReleaseBuildPolicy.Scenes));
        }

        [Test]
        public void ActiveTargetMismatch_IsRejected()
        {
            Assert.That(
                WindowsReleaseBuildPolicy.ValidateActiveTarget(BuildTarget.StandaloneLinux64),
                Is.EqualTo(WindowsReleaseExitCodes.ActiveBuildTargetMismatch));
            Assert.That(
                WindowsReleaseBuildPolicy.ValidateActiveTarget(BuildTarget.StandaloneWindows64),
                Is.EqualTo(WindowsReleaseExitCodes.Success));
        }

        [Test]
        public void UnityVersionMismatch_IsRejected()
        {
            Assert.That(
                WindowsReleaseBuildPolicy.ValidateUnityVersion("other", "6000.3.11f1"),
                Is.EqualTo(WindowsReleaseExitCodes.UnityVersionMismatch));
            Assert.That(
                WindowsReleaseBuildPolicy.ValidateUnityVersion("6000.3.11f1", "6000.3.11f1"),
                Is.EqualTo(WindowsReleaseExitCodes.Success));
        }

        [Test]
        public void SceneMismatch_IsRejected()
        {
            var valid = new[]
            {
                new ReleaseSceneDescriptor(WindowsReleaseBuildPolicy.MainMenuScene, true, true),
                new ReleaseSceneDescriptor(WindowsReleaseBuildPolicy.UiAudioScene, true, true),
            };
            Assert.That(WindowsReleaseBuildPolicy.ValidateScenes(valid),
                Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(WindowsReleaseBuildPolicy.ValidateScenes(valid.Reverse()),
                Is.EqualTo(WindowsReleaseExitCodes.SceneContractMismatch));
            Assert.That(WindowsReleaseBuildPolicy.ValidateScenes(new[]
            {
                new ReleaseSceneDescriptor(WindowsReleaseBuildPolicy.MainMenuScene, false, true),
                valid[1],
            }), Is.EqualTo(WindowsReleaseExitCodes.SceneContractMismatch));
        }

        [Test]
        public void InvalidArguments_AreRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ValidateArguments(
                    new Dictionary<string, string>()),
                Is.EqualTo(WindowsReleaseExitCodes.InvalidArguments));
        }

        [TestCase(BuildResult.Succeeded, 0, WindowsReleaseExitCodes.Success)]
        [TestCase(BuildResult.Succeeded, 1, WindowsReleaseExitCodes.BuildErrorsRecorded)]
        [TestCase(BuildResult.Succeeded, 3, WindowsReleaseExitCodes.BuildErrorsRecorded)]
        [TestCase(BuildResult.Failed, 2, WindowsReleaseExitCodes.BuildFailed)]
        [TestCase(BuildResult.Cancelled, 0, WindowsReleaseExitCodes.BuildCancelled)]
        [TestCase(BuildResult.Unknown, 0, WindowsReleaseExitCodes.BuildUnknownResult)]
        public void BuildResultAndErrorCount_HaveExactMapping(
            BuildResult result, int totalErrors, int expected)
        {
            Assert.That(WindowsReleaseBuildPolicy.MapBuildResult(result, totalErrors),
                Is.EqualTo(expected));
        }

        [Test]
        public void BuildReportEvidence_MetadataAndSummaryMismatch_IsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportEvidence(1, 0, 0),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportCountMismatch));
        }

        [Test]
        public void BuildReportEvidence_MissingSummary_IsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.Success,
                    summaryWritten: false,
                    detailsWritten: true,
                    metadataWritten: true,
                    metadataErrorCount: 0,
                    reportErrorCount: 0,
                    structuredErrorCount: 0),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportWriteFailure));
        }

        [Test]
        public void BuildReportEvidence_MissingStructuredDetails_IsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.Success,
                    summaryWritten: true,
                    detailsWritten: false,
                    metadataWritten: true,
                    metadataErrorCount: 0,
                    reportErrorCount: 0,
                    structuredErrorCount: 0),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportDetailsWriteFailure));
        }

        [Test]
        public void BuildReportEvidence_StructuredCountMismatch_IsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportEvidence(2, 2, 1),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportCountMismatch));
        }

        [Test]
        public void ZeroErrorGate_IsAppliedAfterEvidenceWrites()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.BuildErrorsRecorded,
                    summaryWritten: true,
                    detailsWritten: true,
                    metadataWritten: true,
                    metadataErrorCount: 1,
                    reportErrorCount: 1,
                    structuredErrorCount: 1),
                Is.EqualTo(WindowsReleaseExitCodes.BuildErrorsRecorded));
        }

        [Test]
        public void Settings_AreRestoredOnSuccess()
        {
            var settings = new FakeSettings();
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => WindowsReleaseExitCodes.Success);
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(settings.RestoreCalled, Is.True);
        }

        [Test]
        public void Settings_AreRestoredOnBuildException()
        {
            var settings = new FakeSettings();
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => throw new InvalidOperationException("synthetic"));
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.InternalException));
            Assert.That(settings.RestoreCalled, Is.True);
        }

        [Test]
        public void SettingsRestoreFailure_TakesPrecedence()
        {
            var settings = new FakeSettings { RestoreValid = false };
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => WindowsReleaseExitCodes.Success);
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.SettingsRestoreFailure));
        }

        [Test]
        public void Settings_AreRestoredOnBuildErrorsRecorded()
        {
            var settings = new FakeSettings();
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => WindowsReleaseExitCodes.BuildErrorsRecorded);
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.BuildErrorsRecorded));
            Assert.That(settings.RestoreCalled, Is.True);
        }

        [Test]
        public void SettingsTransaction_RecordsApplyAndRestoreVerification()
        {
            var settings = new FakeSettings();
            var record = new ReleaseSettingsTransactionRecordV1();
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => WindowsReleaseExitCodes.Success, record);
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(record.schemaVersion, Is.EqualTo("1.0"));
            Assert.That(record.appliedVerification, Is.True);
            Assert.That(record.restoreAttempted, Is.True);
            Assert.That(record.restoreResult, Is.EqualTo("Restored"));
            Assert.That(record.restoredVerification, Is.True);
        }

        [TestCase(WindowsReleaseExitCodes.BuildErrorsRecorded)]
        [TestCase(WindowsReleaseExitCodes.BuildFailed)]
        public void SettingsRestoreFailure_TakesPrecedenceOverBuildOutcome(int buildOutcome)
        {
            var settings = new FakeSettings { RestoreValid = false };
            var result = WindowsReleaseSettingsTransaction.Run(settings, () => buildOutcome);
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.SettingsRestoreFailure));
        }

        [Test]
        public void Settings_AlreadyRequired_AreNotAppliedOrRestored()
        {
            var settings = new FakeSettings { RequiredValid = true };
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => WindowsReleaseExitCodes.Success);
            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(settings.ApplyCalled, Is.False);
            Assert.That(settings.RestoreCalled, Is.False);
        }

        [Test]
        public void MetadataSchemaV2_ContainsAllRequiredFields()
        {
            var required = new[]
            {
                "schemaVersion", "runId", "artifactId", "sourceSha", "sourceTree", "branch",
                "headDetached", "originMainSha", "ahead", "behind", "sourceDirty",
                "unityVersion", "unityRevision", "buildTarget", "architecture", "configuration",
                "backend", "managedStrippingLevel", "il2cppCompilerConfiguration",
                "nativeCompilerIdentity", "windowsSdkIdentity", "backendComparisonId",
                "comparisonRole", "development", "connectWithProfiler",
                "deepProfiling", "allowDebugging", "scriptDebugging", "waitForDebugger",
                "forceAssertions", "effectiveScenes", "playerLogEnabled", "stackTracePolicy",
                "incrementalGC", "productName", "companyName", "productVersion", "buildNumber",
                "applicationIdentifier", "buildEntry", "entrySourceSha256", "policySourceSha256",
                "wrapperSourceSha256", "buildResult", "warningCount", "errorCount",
                "totalSizeBytes", "durationSeconds", "buildReportSummaryFile",
                "buildReportDetailsFile", "zeroErrorGatePassed",
                "metadataReportCountMatched", "structuredErrorCountMatched",
                "cSharpExitCode", "cSharpExitName", "buildStartedUtc", "buildCompletedUtc",
            };
            var fields = typeof(WindowsReleaseMetadataV2).GetFields()
                .Select(field => field.Name).ToArray();
            Assert.That(fields, Is.EquivalentTo(required));
            Assert.That(WindowsReleaseBuildPolicy.MetadataSchemaVersion, Is.EqualTo("2.0"));
        }

        [Test]
        public void StructuredBuildReportSchemaV1_ContainsRequiredFields()
        {
            Assert.That(typeof(BuildReportDetailsV1).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[]
                {
                    "schemaVersion", "runId", "artifactId", "sourceSha", "sourceTree",
                    "unityVersion", "configuration", "backend", "backendComparisonId",
                    "comparisonRole", "result", "totalErrors", "totalWarnings",
                    "errorRecordCount", "warningRecordCount", "captureLimitation", "steps",
                }));
            Assert.That(typeof(BuildReportStepV1).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[]
                {
                    "index", "name", "depth", "durationSeconds", "messages",
                }));
            Assert.That(typeof(BuildReportMessageV1).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[]
                {
                    "index", "type", "content", "normalizedMessage", "messageSha256",
                    "stackTrace",
                }));
            Assert.That(WindowsReleaseBuildPolicy.BuildReportDetailsSchemaVersion,
                Is.EqualTo("1.0"));
        }

        [Test]
        public void BuildReportSummarySchemaV2_BindsCanonicalIdentityAndCounts()
        {
            Assert.That(typeof(BuildReportSummaryV2).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[]
                {
                    "schemaVersion", "runId", "artifactId", "sourceSha", "sourceTree",
                    "configuration", "backend", "backendComparisonId", "comparisonRole",
                    "result", "totalErrors", "totalWarnings", "totalSize",
                    "totalTimeSeconds", "outputPath", "detailsFile", "detailsSha256",
                    "errorRecordCount", "warningRecordCount", "distinctErrorMessageHashes",
                }));
            Assert.That(typeof(BuildReportSummaryV2).GetFields().Select(field => field.Name),
                Does.Not.Contain("steps"));
            Assert.That(WindowsReleaseBuildPolicy.BuildReportSummarySchemaVersion,
                Is.EqualTo("2.0"));
            Assert.That(WindowsReleaseBuildPolicy.BuildReportDetailsSchemaVersion,
                Is.EqualTo("1.0"));
        }

        [Test]
        public void BuildReportIdentityAndCounts_ExactEvidence_IsAccepted()
        {
            var evidence = CreateMatchingEvidence();

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details),
                Is.EqualTo(WindowsReleaseExitCodes.Success));
        }

        [TestCase("runId")]
        [TestCase("artifactId")]
        [TestCase("sourceSha")]
        [TestCase("sourceTree")]
        [TestCase("configuration")]
        [TestCase("result")]
        public void BuildReportIdentityAndCounts_IdentityMismatch_IsRejected(string field)
        {
            var evidence = CreateMatchingEvidence();
            switch (field)
            {
                case "runId":
                    evidence.Summary.runId = "other-run";
                    break;
                case "artifactId":
                    evidence.Summary.artifactId = "other-artifact";
                    break;
                case "sourceSha":
                    evidence.Summary.sourceSha = "other-sha";
                    break;
                case "sourceTree":
                    evidence.Summary.sourceTree = "other-tree";
                    break;
                case "configuration":
                    evidence.Summary.configuration = "other-configuration";
                    break;
                case "result":
                    evidence.Summary.result = BuildResult.Failed.ToString();
                    break;
            }

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [TestCase("backend")]
        [TestCase("backendComparisonId")]
        [TestCase("comparisonRole")]
        public void BuildReportIdentityAndCounts_BackendIdentityMismatch_IsRejected(
            string field)
        {
            var evidence = CreateMatchingEvidence();
            switch (field)
            {
                case "backend":
                    evidence.Summary.backend = ScriptingImplementation.IL2CPP.ToString();
                    break;
                case "backendComparisonId":
                    evidence.Summary.backendComparisonId = "other-comparison";
                    break;
                case "comparisonRole":
                    evidence.Summary.comparisonRole =
                        StoreBackendComparisonRole.IL2CPPCandidate.ToString();
                    break;
            }

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [Test]
        public void SettingsTransaction_RecordsIl2CppApplyAndRestoreContract()
        {
            var settings = new FakeSettings();
            var record = new ReleaseSettingsTransactionRecordV1();
            var result = WindowsReleaseSettingsTransaction.Run(
                settings,
                WindowsReleaseBackendConfiguration.IL2CPP,
                () => WindowsReleaseExitCodes.Success,
                record);

            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(record.requiredSettings.backend,
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(record.requiredSettings.stripping,
                Is.EqualTo(ManagedStrippingLevel.Minimal));
            Assert.That(record.requiredSettings.il2cppCompilerConfiguration,
                Is.EqualTo(Il2CppCompilerConfiguration.Release));
            Assert.That(record.restoreAttempted, Is.True);
            Assert.That(record.restoredVerification, Is.True);
        }

        [Test]
        public void BuildReportIdentityAndCounts_EmptyCanonicalIdentity_IsRejected()
        {
            var evidence = CreateMatchingEvidence();
            evidence.Metadata.runId = string.Empty;
            evidence.Summary.runId = string.Empty;
            evidence.Details.runId = string.Empty;

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [TestCase("metadataErrors")]
        [TestCase("summaryErrors")]
        [TestCase("structuredErrors")]
        [TestCase("metadataWarnings")]
        [TestCase("summaryWarnings")]
        [TestCase("structuredWarnings")]
        public void BuildReportIdentityAndCounts_CountMismatch_IsRejected(string field)
        {
            var evidence = CreateMatchingEvidence();
            switch (field)
            {
                case "metadataErrors":
                    evidence.Metadata.errorCount++;
                    break;
                case "summaryErrors":
                    evidence.Summary.totalErrors++;
                    break;
                case "structuredErrors":
                    evidence.Details.errorRecordCount++;
                    break;
                case "metadataWarnings":
                    evidence.Metadata.warningCount++;
                    break;
                case "summaryWarnings":
                    evidence.Summary.totalWarnings++;
                    break;
                case "structuredWarnings":
                    evidence.Details.warningRecordCount++;
                    break;
            }

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportCountMismatch));
        }

        [Test]
        public void BuildReportIdentityMismatch_IsReturnedAfterEvidenceWrites()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.Success,
                    summaryWritten: true,
                    detailsWritten: true,
                    metadataWritten: true,
                    metadataErrorCount: 0,
                    reportErrorCount: 0,
                    structuredErrorCount: 0,
                    evidenceIdentityAndCounts:
                        WindowsReleaseExitCodes.BuildReportIdentityMismatch),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [Test]
        public void SettingsRestoreFailure_TakesPrecedenceOverIdentityMismatch()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.SettingsRestoreFailure,
                    summaryWritten: true,
                    detailsWritten: true,
                    metadataWritten: true,
                    metadataErrorCount: 0,
                    reportErrorCount: 0,
                    structuredErrorCount: 0,
                    evidenceIdentityAndCounts:
                        WindowsReleaseExitCodes.BuildReportIdentityMismatch),
                Is.EqualTo(WindowsReleaseExitCodes.SettingsRestoreFailure));
        }

        [Test]
        public void KnownUrpError_NormalizedMessageHash_IsStable()
        {
            const string message =
                "Host type is not matching any asset type at Path " +
                "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/" +
                "RenderingLayerMask/TraceRenderingLayerMask.urtshader.";
            Assert.That(WindowsReleaseBuildCli.ComputeMessageSha256(message),
                Is.EqualTo("24c8a11af0bbff70740d553e9748c1aa6d7f3c0a0d689c625037019e759d3951"));
        }

        [Test]
        public void UrpApvRenderingLayerShader_ImportsBothExpectedHostTypes()
        {
            const string path =
                "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/" +
                "RenderingLayerMask/TraceRenderingLayerMask.urtshader";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Assert.That(assets.OfType<ComputeShader>().Count(), Is.EqualTo(1));
            Assert.That(assets.Count(asset => asset.GetType().Name == "RayTracingShader"),
                Is.EqualTo(1));
        }

        [Test]
        public void UrpGlobalSettings_RenderingLayerReferencesUseExpectedSubassetTypes()
        {
            var yaml = System.IO.File.ReadAllText(
                "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            Assert.That(yaml, Does.Contain(
                "renderingLayerCS: {fileID: -6772857160820960102, " +
                "guid: 94a070d33e408384bafc1dea4a565df9, type: 3}"));
            Assert.That(yaml, Does.Contain(
                "renderingLayerRT: {fileID: -5126288278712620388, " +
                "guid: 94a070d33e408384bafc1dea4a565df9, type: 3}"));
        }

        [Test]
        public void ExitCodes_AreUniqueAndCSharpScoped()
        {
            Assert.That(WindowsReleaseExitCodes.All.Distinct().Count(),
                Is.EqualTo(WindowsReleaseExitCodes.All.Length));
            Assert.That(WindowsReleaseExitCodes.All, Has.All.LessThan(100));
        }

        private static EvidenceSet CreateMatchingEvidence()
        {
            const string runId = "20260725T000000000Z";
            const string artifactId = "sha-run";
            const string sourceSha = "source-sha";
            const string sourceTree = "source-tree";
            const string result = "Succeeded";
            const int errors = 0;
            const int warnings = 7;
            const string comparisonId = "comparison";
            var configuration = WindowsReleaseBackendConfiguration.Mono;

            return new EvidenceSet
            {
                Metadata = new WindowsReleaseMetadataV2
                {
                    schemaVersion = WindowsReleaseBuildPolicy.MetadataSchemaVersion,
                    runId = runId,
                    artifactId = artifactId,
                    sourceSha = sourceSha,
                    sourceTree = sourceTree,
                    configuration = configuration.ConfigurationName,
                    backend = configuration.Backend.ToString(),
                    backendComparisonId = comparisonId,
                    comparisonRole = configuration.ComparisonRole.ToString(),
                    buildResult = result,
                    errorCount = errors,
                    warningCount = warnings,
                },
                Summary = new BuildReportSummaryV2
                {
                    runId = runId,
                    artifactId = artifactId,
                    sourceSha = sourceSha,
                    sourceTree = sourceTree,
                    configuration = configuration.ConfigurationName,
                    backend = configuration.Backend.ToString(),
                    backendComparisonId = comparisonId,
                    comparisonRole = configuration.ComparisonRole.ToString(),
                    result = result,
                    totalErrors = errors,
                    totalWarnings = warnings,
                    errorRecordCount = errors,
                    warningRecordCount = warnings,
                },
                Details = new BuildReportDetailsV1
                {
                    runId = runId,
                    artifactId = artifactId,
                    sourceSha = sourceSha,
                    sourceTree = sourceTree,
                    configuration = configuration.ConfigurationName,
                    backend = configuration.Backend.ToString(),
                    backendComparisonId = comparisonId,
                    comparisonRole = configuration.ComparisonRole.ToString(),
                    result = result,
                    totalErrors = errors,
                    totalWarnings = warnings,
                    errorRecordCount = errors,
                    warningRecordCount = warnings,
                },
            };
        }

        private sealed class EvidenceSet
        {
            public WindowsReleaseMetadataV2 Metadata;
            public BuildReportSummaryV2 Summary;
            public BuildReportDetailsV1 Details;
        }

        private sealed class FakeSettings : IWindowsReleaseSettings
        {
            public bool ApplyCalled { get; private set; }
            public bool RestoreCalled { get; private set; }
            public bool RestoreValid { get; set; } = true;
            public bool RequiredValid { get; set; }
            public ReleaseSettingsSnapshot Capture() => default;
            public void ApplyRequired()
            {
                ApplyCalled = true;
                RequiredValid = true;
            }
            public bool IsRequired() => RequiredValid;
            public void Restore(ReleaseSettingsSnapshot snapshot) => RestoreCalled = true;
            public bool IsRestored(ReleaseSettingsSnapshot snapshot) => RestoreValid;
        }
    }
}
