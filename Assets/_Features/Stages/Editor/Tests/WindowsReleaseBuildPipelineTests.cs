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
                "backend", "managedStrippingLevel", "development", "connectWithProfiler",
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
                    "unityVersion", "result", "totalErrors", "totalWarnings",
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
        public void BuildReportSummarySchemaV1_ExposesOnlyShareableDetailRecord()
        {
            Assert.That(typeof(BuildReportSummaryV1).GetFields().Select(field => field.Name),
                Does.Contain("detailsFile"));
            Assert.That(typeof(BuildReportSummaryV1).GetFields().Select(field => field.Name),
                Does.Contain("detailsSha256"));
            Assert.That(typeof(BuildReportSummaryV1).GetFields().Select(field => field.Name),
                Does.Contain("errorRecordCount"));
            Assert.That(typeof(BuildReportSummaryV1).GetFields().Select(field => field.Name),
                Does.Contain("warningRecordCount"));
            Assert.That(typeof(BuildReportSummaryV1).GetFields().Select(field => field.Name),
                Does.Contain("distinctErrorMessageHashes"));
            Assert.That(typeof(BuildReportSummaryV1).GetFields().Select(field => field.Name),
                Does.Not.Contain("steps"));
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
