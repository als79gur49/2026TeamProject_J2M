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

        [TestCase(BuildResult.Succeeded, WindowsReleaseExitCodes.Success)]
        [TestCase(BuildResult.Failed, WindowsReleaseExitCodes.BuildFailed)]
        [TestCase(BuildResult.Cancelled, WindowsReleaseExitCodes.BuildCancelled)]
        [TestCase(BuildResult.Unknown, WindowsReleaseExitCodes.BuildUnknownResult)]
        public void BuildResult_HasExactMapping(BuildResult result, int expected)
        {
            Assert.That(WindowsReleaseBuildPolicy.MapBuildResult(result), Is.EqualTo(expected));
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
        public void MetadataSchemaV1_ContainsAllRequiredFields()
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
                "totalSizeBytes", "durationSeconds", "payloadManifest",
                "payloadManifestSha256", "payloadFileCount", "buildStartedUtc",
                "buildCompletedUtc",
            };
            var fields = typeof(WindowsReleaseMetadataV1).GetFields()
                .Select(field => field.Name).ToArray();
            Assert.That(fields, Is.EquivalentTo(required));
            Assert.That(WindowsReleaseBuildPolicy.SchemaVersion, Is.EqualTo("1.0"));
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
            public bool RestoreCalled { get; private set; }
            public bool RestoreValid { get; set; } = true;
            public ReleaseSettingsSnapshot Capture() => default;
            public void ApplyRequired() { }
            public bool IsRequired() => true;
            public void Restore(ReleaseSettingsSnapshot snapshot) => RestoreCalled = true;
            public bool IsRestored(ReleaseSettingsSnapshot snapshot) => RestoreValid;
        }
    }
}
