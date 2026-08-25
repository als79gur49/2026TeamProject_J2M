using System;
using System.Collections.Generic;
using System.IO;
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
        public void CanonicalStore_OmittedIntentAndBackendDefaultsToMono()
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveConfiguration(
                string.Empty, string.Empty, out var configuration), Is.True);
            Assert.That(configuration.Intent,
                Is.EqualTo(WindowsReleaseBuildIntent.CanonicalStore));
            Assert.That(configuration.Candidate, Is.EqualTo(StoreBackendCandidate.Mono));
            Assert.That(configuration.Backend, Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(configuration.ComparisonRole,
                Is.EqualTo(StoreBackendComparisonRole.CanonicalStore));
        }

        [TestCase("Mono", StoreBackendCandidate.Mono, ScriptingImplementation.Mono2x)]
        [TestCase("IL2CPP", StoreBackendCandidate.IL2CPP, ScriptingImplementation.IL2CPP)]
        public void BackendComparison_ExplicitValueCreatesExactCandidate(
            string value,
            StoreBackendCandidate candidate,
            ScriptingImplementation backend)
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveConfiguration(
                WindowsReleaseBuildIntent.BackendComparison.ToString(),
                value, out var configuration), Is.True);
            Assert.That(configuration.Candidate, Is.EqualTo(candidate));
            Assert.That(configuration.Backend, Is.EqualTo(backend));
            Assert.That(configuration.StoreConfigurationId,
                Is.Not.EqualTo(WindowsReleaseBuildPolicy.StoreConfigurationId));
        }

        [Test]
        public void CanonicalStore_Il2CppIsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveConfiguration(
                WindowsReleaseBuildIntent.CanonicalStore.ToString(),
                StoreBackendCandidate.IL2CPP.ToString(), out _), Is.False);
        }

        [Test]
        public void BackendPolicy_UnknownValueOrIntentIsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.TryResolveConfiguration(
                "CanonicalStore", "il2cpp", out _), Is.False);
            Assert.That(WindowsReleaseBuildPolicy.TryResolveConfiguration(
                "Unknown", "Mono", out _), Is.False);
        }

        [Test]
        public void BackendIntent_UsesSeparatedConfigurationNames()
        {
            Assert.That(WindowsReleaseBackendConfiguration.CanonicalStoreMono.ConfigurationName,
                Is.EqualTo("Windows-x64-Store-Mono-LogOn"));
            Assert.That(WindowsReleaseBackendConfiguration.ComparisonMono.ConfigurationName,
                Is.EqualTo("Windows-x64-NonDevelopment-Mono"));
            Assert.That(WindowsReleaseBackendConfiguration.IL2CPP.ConfigurationName,
                Is.EqualTo("Windows-x64-NonDevelopment-IL2CPP"));
        }

        [Test]
        public void BackendPolicy_UsesBackendSpecificStripping()
        {
            Assert.That(WindowsReleaseBackendConfiguration.CanonicalStoreMono.Stripping,
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
        public void CanonicalStore_IdentityAndLoggingPolicyAreFrozen()
        {
            var configuration = WindowsReleaseBuildPolicy.DefaultConfiguration;
            Assert.That(WindowsReleaseBuildPolicy.StoreConfigurationSchema,
                Is.EqualTo("1.0"));
            Assert.That(WindowsReleaseBuildPolicy.StoreConfigurationId,
                Is.EqualTo("windows-x64-store-mono-logon-v1"));
            Assert.That(configuration.Intent,
                Is.EqualTo(WindowsReleaseBuildIntent.CanonicalStore));
            Assert.That(configuration.Backend, Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(configuration.Stripping, Is.EqualTo(ManagedStrippingLevel.Disabled));
            Assert.That(WindowsReleaseBuildPolicy.PlayerLogEnabled, Is.True);
            Assert.That(WindowsReleaseBuildPolicy.AutomaticLogUpload, Is.False);
            Assert.That(configuration.LogPolicyId,
                Is.EqualTo("local-player-log-no-auto-upload-v1"));
            Assert.That(configuration.PayloadAudience, Is.EqualTo("StoreDistributable"));
        }

        [Test]
        public void DistributionTargets_ResolveDirectAndSteamContracts()
        {
            Assert.That(WindowsDistributionTargetPolicy.TryResolve(
                "direct-windows", out var direct), Is.True);
            Assert.That(direct.ProviderSelectionMode,
                Is.EqualTo(ProviderSelectionMode.DefaultWhenUnspecified));
            Assert.That(direct.ExpectedProviderId, Is.EqualTo("local"));
            Assert.That(direct.ExpectedLaunchArguments, Is.Empty);
            Assert.That(direct.ArtifactDirectoryName, Is.EqualTo("DirectWindows"));
            Assert.That(direct.RequiredArtifacts,
                Is.EqualTo(new[]
                {
                    WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact,
                    WindowsDistributionTargetPolicy.UnityPlayerThirdPartyNoticesArtifact,
                }));

            Assert.That(WindowsDistributionTargetPolicy.TryResolve(
                "steam-windows", out var steam), Is.True);
            Assert.That(steam.ProviderSelectionMode,
                Is.EqualTo(ProviderSelectionMode.ExternalLaunchArgumentRequired));
            Assert.That(steam.ExpectedProviderId, Is.EqualTo("steam"));
            Assert.That(steam.ExpectedLaunchArguments,
                Is.EqualTo(new[] { "-j2mPlatformProvider", "steam" }));
            Assert.That(steam.RequiredArtifacts,
                Is.EqualTo(new[]
                {
                    WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact,
                    WindowsDistributionTargetPolicy.UnityPlayerThirdPartyNoticesArtifact,
                    WindowsDistributionTargetPolicy.SteamNativeArtifact,
                    WindowsDistributionTargetPolicy.SteamManagedBindingArtifact,
                }));
            Assert.That(steam.ExpectedStoreLaunch,
                Is.EqualTo("VectorQuake.exe -j2mPlatformProvider steam"));
            Assert.That(steam.ArtifactDirectoryName, Is.EqualTo("SteamWindows"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown-windows")]
        [TestCase("Steam-Windows")]
        public void DistributionTarget_MissingUnknownOrNonCanonicalValueIsRejected(
            string targetId)
        {
            Assert.That(WindowsDistributionTargetPolicy.TryResolve(targetId, out _),
                Is.False);
        }

        [Test]
        public void SteamDistributionLaunchArguments_AreExactOrderedTokens()
        {
            var target = WindowsDistributionTargetPolicy.SteamWindows;
            Assert.That(WindowsDistributionTargetPolicy.ValidateLaunchArguments(
                    target, new[] { "-j2mPlatformProvider", "steam" }),
                Is.EqualTo(WindowsDistributionValidationFailure.None));
            Assert.That(WindowsDistributionTargetPolicy.ValidateLaunchArguments(
                    target, new[] { "steam", "-j2mPlatformProvider" }),
                Is.EqualTo(
                    WindowsDistributionValidationFailure.LaunchArgumentContractMismatch));
            Assert.That(WindowsDistributionTargetPolicy.ValidateLaunchArguments(
                    target, new[] { "-j2mPlatformProvider=steam" }),
                Is.EqualTo(
                    WindowsDistributionValidationFailure.LaunchArgumentContractMismatch));
            Assert.That(WindowsDistributionTargetPolicy.ValidateLaunchArguments(
                    target, new[] { "-j2mPlatformProvider", "local" }),
                Is.EqualTo(
                    WindowsDistributionValidationFailure.LaunchArgumentContractMismatch));
        }

        [Test]
        public void SteamPromotedArtifactContract_RequiresNativeAndManagedAndForbidsAppId()
        {
            var target = WindowsDistributionTargetPolicy.SteamWindows;
            var valid = new[]
            {
                "payload/VectorQuake.exe",
                "payload/ThirdPartyNotices.txt",
                "payload/UnityPlayerThirdPartyNotices.pdf",
                "payload/steam_api64.dll",
                "payload/VectorQuake_Data/Managed/com.rlabrecque.steamworks.net.dll",
            };
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, valid),
                Is.EqualTo(WindowsDistributionValidationFailure.None));
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, valid.Where(path => !path.EndsWith("steam_api64.dll"))),
                Is.EqualTo(WindowsDistributionValidationFailure.RequiredArtifactMissing));
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, valid.Where(path =>
                        !path.EndsWith("com.rlabrecque.steamworks.net.dll"))),
                Is.EqualTo(WindowsDistributionValidationFailure.RequiredArtifactMissing));
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, valid.Concat(new[] { "payload/steam_appid.txt" })),
                Is.EqualTo(WindowsDistributionValidationFailure.ForbiddenArtifactPresent));
        }

        [TestCase("payload/steam_api64.dll")]
        [TestCase("payload/VectorQuake_Data/Managed/com.rlabrecque.steamworks.net.dll")]
        [TestCase("payload/steam_appid.txt")]
        public void DirectPromotedArtifactContract_ForbidsSteamDependencies(string artifact)
        {
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    WindowsDistributionTargetPolicy.DirectWindows,
                    new[]
                    {
                        "payload/VectorQuake.exe",
                        "payload/ThirdPartyNotices.txt",
                        "payload/UnityPlayerThirdPartyNotices.pdf",
                        artifact,
                    }),
                Is.EqualTo(WindowsDistributionValidationFailure.ForbiddenArtifactPresent));
        }

        [TestCase("direct-windows", "System.IO.Hashing.dll")]
        [TestCase("direct-windows", "System.Runtime.CompilerServices.Unsafe.dll")]
        [TestCase("steam-windows", "System.IO.Hashing.dll")]
        [TestCase("steam-windows", "System.Runtime.CompilerServices.Unsafe.dll")]
        public void BothTargets_ForbidTestOnlyManagedAssemblies(
            string targetId,
            string artifact)
        {
            Assert.That(WindowsDistributionTargetPolicy.TryResolve(
                targetId, out var target), Is.True);
            var inventory = target.CopyRequiredArtifacts()
                .Concat(new[] { "VectorQuake_Data/Managed/" + artifact });

            Assert.That(
                WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, inventory),
                Is.EqualTo(WindowsDistributionValidationFailure.ForbiddenArtifactPresent));
        }

        [TestCase(WindowsDistributionTargetPolicy.DirectWindowsTargetId)]
        [TestCase(WindowsDistributionTargetPolicy.SteamWindowsTargetId)]
        public void DistributionContract_MissingThirdPartyNoticesIsRejected(string targetId)
        {
            Assert.That(WindowsDistributionTargetPolicy.TryResolve(
                targetId, out var target), Is.True);
            var artifacts = target.RequiredArtifacts
                .Where(artifact => !string.Equals(
                    artifact,
                    WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact,
                    StringComparison.Ordinal))
                .ToArray();

            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, artifacts),
                Is.EqualTo(WindowsDistributionValidationFailure.RequiredArtifactMissing));
        }

        [TestCase(WindowsDistributionTargetPolicy.DirectWindowsTargetId)]
        [TestCase(WindowsDistributionTargetPolicy.SteamWindowsTargetId)]
        public void DistributionContract_MissingUnityPlayerNoticesIsRejected(
            string targetId)
        {
            Assert.That(WindowsDistributionTargetPolicy.TryResolve(
                targetId, out var target), Is.True);
            var artifacts = target.RequiredArtifacts
                .Where(artifact => !string.Equals(
                    artifact,
                    WindowsDistributionTargetPolicy.UnityPlayerThirdPartyNoticesArtifact,
                    StringComparison.Ordinal))
                .ToArray();

            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target, artifacts),
                Is.EqualTo(WindowsDistributionValidationFailure.RequiredArtifactMissing));
        }

        [TestCase("payload/Docs/ThirdPartyNotices.txt")]
        [TestCase("payload/thirdpartynotices.txt")]
        [TestCase("Docs/ThirdPartyNotices.txt")]
        [TestCase("/payload/ThirdPartyNotices.txt")]
        public void DistributionContract_NonCanonicalThirdPartyNoticesPathIsRejected(
            string noticePath)
        {
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    WindowsDistributionTargetPolicy.DirectWindows,
                    new[]
                    {
                        "payload/VectorQuake.exe",
                        noticePath,
                        "payload/UnityPlayerThirdPartyNotices.pdf",
                    }),
                Is.EqualTo(WindowsDistributionValidationFailure.RequiredArtifactMissing));
        }

        [TestCase("payload/Docs/UnityPlayerThirdPartyNotices.pdf")]
        [TestCase("payload/unityplayerthirdpartynotices.pdf")]
        [TestCase("Docs/UnityPlayerThirdPartyNotices.pdf")]
        [TestCase("/payload/UnityPlayerThirdPartyNotices.pdf")]
        public void DistributionContract_NonCanonicalUnityPlayerNoticesPathIsRejected(
            string noticePath)
        {
            Assert.That(WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    WindowsDistributionTargetPolicy.DirectWindows,
                    new[]
                    {
                        "payload/VectorQuake.exe",
                        "payload/ThirdPartyNotices.txt",
                        noticePath,
                    }),
                Is.EqualTo(WindowsDistributionValidationFailure.RequiredArtifactMissing));
        }

        [Test]
        public void DistributionConfiguration_RemainsProviderNeutralForFutureStore()
        {
            var future = new WindowsDistributionTargetConfiguration(
                "future-store-windows",
                "FutureStoreWindows",
                ProviderSelectionMode.ExternalLaunchArgumentRequired,
                "future-store",
                new[] { "-j2mPlatformProvider", "future-store" },
                new[] { "future_store.dll" },
                new[] { "future_store_dev.txt" });

            Assert.That(WindowsDistributionTargetPolicy.ValidateConfiguration(future),
                Is.EqualTo(WindowsDistributionValidationFailure.None));
            Assert.That(future.ExpectedProviderId, Is.EqualTo("future-store"));
        }

        [Test]
        public void DistributionOwnership_DoesNotLeakIntoPlatformCoreOrSteamRuntime()
        {
            var forbiddenTokens = new[]
            {
                WindowsDistributionTargetPolicy.DirectWindowsTargetId,
                WindowsDistributionTargetPolicy.SteamWindowsTargetId,
                nameof(WindowsDistributionTargetConfiguration),
            };
            var productionRoots = new[]
            {
                "Assets/_Core/Runtime/Platform",
                "Packages/com.j2m.platform.steam/Runtime",
                "Packages/com.j2m.platform.steam.steamworksnet/Runtime",
            };
            foreach (var root in productionRoots)
            {
                var source = string.Join("\n", System.IO.Directory.GetFiles(
                        root, "*.cs", System.IO.SearchOption.AllDirectories)
                    .Select(System.IO.File.ReadAllText));
                Assert.That(source, Does.Not.Contain(forbiddenTokens[0]), root);
                Assert.That(source, Does.Not.Contain(forbiddenTokens[1]), root);
                Assert.That(source, Does.Not.Contain(forbiddenTokens[2]), root);
            }
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

            var otherwiseValid = WindowsReleaseBuildCli.RequiredArgumentNames
                .ToDictionary(name => name, _ => "value", StringComparer.Ordinal);
            otherwiseValid[WindowsReleaseBuildCli.OutputPathArgument] =
                "C:/release/.staging-run/VectorQuake.exe";
            otherwiseValid.Remove(WindowsReleaseBuildCli.DistributionTargetArgument);
            Assert.That(WindowsReleaseBuildPolicy.ValidateArguments(otherwiseValid),
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
        public void ManagedPluginApplyFailure_WithoutBuildReport_IsPreserved()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.ManagedPluginApplyFailure,
                    summaryWritten: false,
                    detailsWritten: false,
                    metadataWritten: true,
                    metadataErrorCount: 0,
                    reportErrorCount: -1,
                    structuredErrorCount: -1,
                    evidenceIdentityAndCounts:
                        WindowsReleaseExitCodes.BuildReportIdentityMismatch),
                Is.EqualTo(WindowsReleaseExitCodes.ManagedPluginApplyFailure));
        }

        [Test]
        public void ManagedPluginRestoreExitCode_TakesPrecedenceOverBuildReportFailures()
        {
            Assert.That(WindowsReleaseBuildPolicy.ResolvePostBuildExitCode(
                    WindowsReleaseExitCodes.ManagedPluginRestoreFailure,
                    summaryWritten: false,
                    detailsWritten: false,
                    metadataWritten: true,
                    metadataErrorCount: 0,
                    reportErrorCount: -1,
                    structuredErrorCount: -1,
                    evidenceIdentityAndCounts:
                        WindowsReleaseExitCodes.BuildReportIdentityMismatch),
                Is.EqualTo(WindowsReleaseExitCodes.ManagedPluginRestoreFailure));
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

        [Test]
        public void SettingsTransaction_PlayerLogFalseIsCorrectedAndRestored()
        {
            var settings = new FakeSettings();
            var record = new ReleaseSettingsTransactionRecordV1();
            var result = WindowsReleaseSettingsTransaction.Run(
                settings, () => WindowsReleaseExitCodes.Success, record);

            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(record.originalSettings.playerLog, Is.False);
            Assert.That(record.requiredSettings.playerLog, Is.True);
            Assert.That(record.playerLogChanged, Is.True);
            Assert.That(record.appliedVerification, Is.True);
            Assert.That(record.restoreAttempted, Is.True);
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
        public void ManagedPlugins_AreRestoredOnSuccessAndRecorded()
        {
            var settings = new FakeManagedPluginSettings();
            var record = new ManagedPluginTransactionRecordV1();

            var result = WindowsReleaseManagedPluginTransaction.Run(
                settings,
                () => WindowsReleaseExitCodes.Success,
                record);

            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(settings.ApplyCalled, Is.True);
            Assert.That(settings.RestoreCalled, Is.True);
            Assert.That(record.assetPaths,
                Is.EqualTo(WindowsReleaseBuildPolicy.TestOnlyManagedPluginPaths));
            Assert.That(record.appliedVerification, Is.True);
            Assert.That(record.restoreResult, Is.EqualTo("Restored"));
        }

        [Test]
        public void ManagedPlugins_AreRestoredOnBuildException()
        {
            var settings = new FakeManagedPluginSettings();

            var result = WindowsReleaseManagedPluginTransaction.Run(
                settings,
                () => throw new InvalidOperationException("synthetic"));

            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.InternalException));
            Assert.That(settings.RestoreCalled, Is.True);
        }

        [Test]
        public void ManagedPluginRestoreFailure_TakesPrecedence()
        {
            var settings = new FakeManagedPluginSettings { RestoreValid = false };

            var result = WindowsReleaseManagedPluginTransaction.Run(
                settings,
                () => WindowsReleaseExitCodes.Success);

            Assert.That(result,
                Is.EqualTo(WindowsReleaseExitCodes.ManagedPluginRestoreFailure));
        }

        [Test]
        public void ManagedPlugins_AlreadyExcluded_AreNotMutated()
        {
            var settings = new FakeManagedPluginSettings { RequiredValid = true };

            var result = WindowsReleaseManagedPluginTransaction.Run(
                settings,
                () => WindowsReleaseExitCodes.Success);

            Assert.That(result, Is.EqualTo(WindowsReleaseExitCodes.Success));
            Assert.That(settings.ApplyCalled, Is.False);
            Assert.That(settings.RestoreCalled, Is.False);
        }

        [Test]
        public void ManagedPluginApplyFailure_FailsClosedAndRestores()
        {
            var settings = new FakeManagedPluginSettings { ApplyValid = false };

            var result = WindowsReleaseManagedPluginTransaction.Run(
                settings,
                () => WindowsReleaseExitCodes.Success);

            Assert.That(result,
                Is.EqualTo(WindowsReleaseExitCodes.ManagedPluginApplyFailure));
            Assert.That(settings.RestoreCalled, Is.True);
        }

        [TestCase("System.IO.Hashing.dll")]
        [TestCase("System.Runtime.CompilerServices.Unsafe.dll")]
        public void PlayerOutput_ForbiddenManagedAssemblyFailsClosed(string artifact)
        {
            WithPlayerOutput((output, dataRoot, managedRoot) =>
            {
                File.WriteAllText(Path.Combine(managedRoot, artifact), "fixture");
                Assert.That(
                    WindowsReleaseBuildPolicy.ValidateForbiddenManagedAssemblies(output),
                    Is.EqualTo(
                        WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent));
            });
        }

        [TestCase("System.IO.Hashing.dll")]
        [TestCase("System.Runtime.CompilerServices.Unsafe.dll")]
        public void ScriptingAssemblyInventory_ForbiddenNameFailsClosed(string artifact)
        {
            WithPlayerOutput((output, dataRoot, _) =>
            {
                File.WriteAllText(
                    Path.Combine(dataRoot, "ScriptingAssemblies.json"),
                    "{\"names\":[\"" + artifact + "\"]}");
                Assert.That(
                    WindowsReleaseBuildPolicy.ValidateForbiddenManagedAssemblies(output),
                    Is.EqualTo(
                        WindowsReleaseExitCodes.ForbiddenManagedAssemblyPresent));
            });
        }

        [Test]
        public void PlayerOutput_WithoutForbiddenManagedAssembliesPasses()
        {
            WithPlayerOutput((output, _, __) =>
                Assert.That(
                    WindowsReleaseBuildPolicy.ValidateForbiddenManagedAssemblies(output),
                    Is.EqualTo(WindowsReleaseExitCodes.Success)));
        }

        [Test]
        public void MetadataSchemaV4_ContainsAllRequiredFields()
        {
            var required = new[]
            {
                "schemaVersion", "runId", "artifactId", "sourceSha", "sourceTree", "branch",
                "headDetached", "originMainSha", "ahead", "behind", "sourceDirty",
                "unityVersion", "unityRevision", "buildTarget", "architecture", "configuration",
                "buildIntent", "storeConfigurationSchema", "storeConfigurationId",
                "backend", "scriptingBackend", "managedStrippingLevel",
                "il2cppCompilerConfiguration",
                "nativeCompilerIdentity", "windowsSdkIdentity", "backendComparisonId",
                "comparisonRole", "development", "connectWithProfiler",
                "deepProfiling", "allowDebugging", "scriptDebugging",
                "waitForPlayerConnection", "waitForDebugger", "forceAssertions",
                "effectiveScenes", "playerLogEnabled", "logPolicyId",
                "automaticLogUpload", "payloadAudience", "distributionTargetId",
                "providerSelectionMode", "expectedProviderId", "expectedLaunchArguments",
                "requiredArtifacts", "forbiddenArtifacts", "expectedStoreLaunch",
                "stackTracePolicy",
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
            Assert.That(WindowsReleaseBuildPolicy.MetadataSchemaVersion, Is.EqualTo("4.0"));
        }

        [Test]
        public void StructuredBuildReportSchemaV3_ContainsRequiredFields()
        {
            Assert.That(typeof(BuildReportDetailsV1).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[]
                {
                    "schemaVersion", "runId", "artifactId", "sourceSha", "sourceTree",
                    "unityVersion", "configuration", "buildIntent",
                    "storeConfigurationSchema", "storeConfigurationId", "backend",
                    "scriptingBackend", "managedStrippingLevel", "playerLogEnabled",
                    "logPolicyId", "automaticLogUpload", "payloadAudience",
                    "distributionTargetId", "providerSelectionMode", "expectedProviderId",
                    "expectedLaunchArguments", "requiredArtifacts", "forbiddenArtifacts",
                    "expectedStoreLaunch",
                    "backendComparisonId",
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
                Is.EqualTo("3.0"));
        }

        [Test]
        public void BuildReportSummarySchemaV4_BindsCanonicalIdentityAndCounts()
        {
            Assert.That(typeof(BuildReportSummaryV2).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[]
                {
                    "schemaVersion", "runId", "artifactId", "sourceSha", "sourceTree",
                    "configuration", "buildIntent", "storeConfigurationSchema",
                    "storeConfigurationId", "backend", "scriptingBackend",
                    "managedStrippingLevel", "playerLogEnabled", "logPolicyId",
                    "automaticLogUpload", "payloadAudience", "backendComparisonId",
                    "distributionTargetId", "providerSelectionMode", "expectedProviderId",
                    "expectedLaunchArguments", "requiredArtifacts", "forbiddenArtifacts",
                    "expectedStoreLaunch", "comparisonRole",
                    "result", "totalErrors", "totalWarnings", "totalSize",
                    "totalTimeSeconds", "outputPath", "detailsFile", "detailsSha256",
                    "errorRecordCount", "warningRecordCount", "distinctErrorMessageHashes",
                }));
            Assert.That(typeof(BuildReportSummaryV2).GetFields().Select(field => field.Name),
                Does.Not.Contain("steps"));
            Assert.That(WindowsReleaseBuildPolicy.BuildReportSummarySchemaVersion,
                Is.EqualTo("4.0"));
            Assert.That(WindowsReleaseBuildPolicy.BuildReportDetailsSchemaVersion,
                Is.EqualTo("3.0"));
        }

        [Test]
        public void BuildReportIdentityAndCounts_ExactEvidence_IsAccepted()
        {
            var evidence = CreateMatchingEvidence();

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details,
                    WindowsReleaseBackendConfiguration.CanonicalStoreMono,
                    WindowsDistributionTargetPolicy.DirectWindows),
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
                    evidence.Metadata, evidence.Summary, evidence.Details,
                    WindowsReleaseBackendConfiguration.CanonicalStoreMono,
                    WindowsDistributionTargetPolicy.DirectWindows),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [TestCase("backend")]
        [TestCase("storeConfigurationId")]
        [TestCase("managedStrippingLevel")]
        [TestCase("playerLogEnabled")]
        [TestCase("logPolicyId")]
        [TestCase("automaticLogUpload")]
        [TestCase("payloadAudience")]
        [TestCase("backendComparisonId")]
        [TestCase("comparisonRole")]
        [TestCase("distributionTargetId")]
        [TestCase("providerSelectionMode")]
        [TestCase("expectedProviderId")]
        [TestCase("expectedLaunchArguments")]
        public void BuildReportIdentityAndCounts_ConfigurationIdentityMismatch_IsRejected(
            string field)
        {
            var evidence = CreateMatchingEvidence();
            switch (field)
            {
                case "backend":
                    evidence.Summary.backend = StoreBackendCandidate.IL2CPP.ToString();
                    break;
                case "storeConfigurationId":
                    evidence.Summary.storeConfigurationId = "other-store-configuration";
                    break;
                case "managedStrippingLevel":
                    evidence.Summary.managedStrippingLevel =
                        ManagedStrippingLevel.Minimal.ToString();
                    break;
                case "playerLogEnabled":
                    evidence.Summary.playerLogEnabled = false;
                    break;
                case "logPolicyId":
                    evidence.Summary.logPolicyId = "other-log-policy";
                    break;
                case "automaticLogUpload":
                    evidence.Summary.automaticLogUpload = true;
                    break;
                case "payloadAudience":
                    evidence.Summary.payloadAudience = "InternalRc";
                    break;
                case "backendComparisonId":
                    evidence.Summary.backendComparisonId = "other-comparison";
                    break;
                case "comparisonRole":
                    evidence.Summary.comparisonRole =
                        StoreBackendComparisonRole.IL2CPPCandidate.ToString();
                    break;
                case "distributionTargetId":
                    evidence.Summary.distributionTargetId =
                        WindowsDistributionTargetPolicy.SteamWindowsTargetId;
                    break;
                case "providerSelectionMode":
                    evidence.Summary.providerSelectionMode =
                        ProviderSelectionMode.ExternalLaunchArgumentRequired.ToString();
                    break;
                case "expectedProviderId":
                    evidence.Summary.expectedProviderId = "steam";
                    break;
                case "expectedLaunchArguments":
                    evidence.Summary.expectedLaunchArguments =
                        new[] { "-j2mPlatformProvider", "steam" };
                    break;
            }

            Assert.That(WindowsReleaseBuildPolicy.ValidateBuildReportIdentityAndCounts(
                    evidence.Metadata, evidence.Summary, evidence.Details,
                    WindowsReleaseBackendConfiguration.CanonicalStoreMono,
                    WindowsDistributionTargetPolicy.DirectWindows),
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
                    evidence.Metadata, evidence.Summary, evidence.Details,
                    WindowsReleaseBackendConfiguration.CanonicalStoreMono,
                    WindowsDistributionTargetPolicy.DirectWindows),
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
                    evidence.Metadata, evidence.Summary, evidence.Details,
                    WindowsReleaseBackendConfiguration.CanonicalStoreMono,
                    WindowsDistributionTargetPolicy.DirectWindows),
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

        [TestCase(WindowsDistributionTargetPolicy.DirectWindowsTargetId)]
        [TestCase(WindowsDistributionTargetPolicy.SteamWindowsTargetId)]
        public void ArtifactIdentity_UniformDistributionTargetsAreAccepted(string targetId)
        {
            Assert.That(WindowsDistributionTargetPolicy.TryResolve(
                targetId, out var distribution), Is.True);

            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(
                    CreateCanonicalStoreIdentity(distribution),
                    CreateCanonicalStoreIdentity(distribution),
                    CreateCanonicalStoreIdentity(distribution)),
                Is.EqualTo(WindowsReleaseExitCodes.Success));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ArtifactIdentity_MixedDistributionTargetsAreRejected(bool directFirst)
        {
            var direct = CreateCanonicalStoreIdentity(
                WindowsDistributionTargetPolicy.DirectWindows);
            var steam = CreateCanonicalStoreIdentity(
                WindowsDistributionTargetPolicy.SteamWindows);

            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(
                    directFirst ? direct : steam,
                    directFirst ? steam : direct),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [Test]
        public void ArtifactIdentity_ThreeCarriersWithOneMixedTargetAreRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(
                    CreateCanonicalStoreIdentity(
                        WindowsDistributionTargetPolicy.SteamWindows),
                    CreateCanonicalStoreIdentity(
                        WindowsDistributionTargetPolicy.SteamWindows),
                    CreateCanonicalStoreIdentity(
                        WindowsDistributionTargetPolicy.DirectWindows)),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [Test]
        public void ArtifactIdentity_SameSteamTargetWithProviderMismatchIsRejected()
        {
            var matching = CreateCanonicalStoreIdentity(
                WindowsDistributionTargetPolicy.SteamWindows);
            var mismatch = CreateCanonicalStoreIdentity(
                WindowsDistributionTargetPolicy.SteamWindows);
            mismatch.expectedProviderId = WindowsDistributionTargetPolicy.LocalProviderId;

            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(
                    matching, mismatch),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [Test]
        public void ArtifactIdentity_NullEmptyOrNullCarrierIsRejected()
        {
            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(null),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(
                    (ReleaseStoreIdentityV1)null),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [Test]
        public void ArtifactIdentity_InvalidTargetIsRejected()
        {
            var identity = CreateCanonicalStoreIdentity();
            identity.distributionTargetId = "unknown-windows";

            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(identity),
                Is.EqualTo(WindowsReleaseExitCodes.BuildReportIdentityMismatch));
        }

        [TestCase("metadata")]
        [TestCase("provenance")]
        [TestCase("SUCCESS")]
        public void ArtifactIdentity_LoggingMismatchIsRejected(string carrier)
        {
            var metadata = CreateCanonicalStoreIdentity();
            var provenance = CreateCanonicalStoreIdentity();
            var success = CreateCanonicalStoreIdentity();
            var target = carrier == "metadata"
                ? metadata
                : carrier == "provenance" ? provenance : success;
            target.automaticLogUpload = true;

            Assert.That(WindowsReleaseBuildPolicy.ValidateCanonicalArtifactIdentity(
                    metadata, provenance, success),
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
        public void InitialStoreConfiguration_HasNoAutomaticDiagnosticsInitialization()
        {
            var manifest = System.IO.File.ReadAllText("Packages/manifest.json");
            Assert.That(manifest, Does.Not.Contain("\"com.unity.services.analytics\""));
            Assert.That(manifest, Does.Not.Contain("\"com.unity.services.core\""));
            Assert.That(manifest, Does.Not.Contain("sentry"));
            Assert.That(manifest, Does.Not.Contain("backtrace"));
            Assert.That(manifest, Does.Not.Contain("bugsnag"));

            var connect = System.IO.File.ReadAllText(
                "ProjectSettings/UnityConnectSettings.asset");
            Assert.That(connect, Does.Contain("m_EnableCloudDiagnosticsReporting: 0"));
            Assert.That(connect, Does.Contain("UnityAnalyticsSettings:"));
            Assert.That(connect, Does.Match(
                @"(?s)UnityAnalyticsSettings:\s+.*?m_Enabled: 0"));
            Assert.That(connect, Does.Match(
                @"(?s)PerformanceReportingSettings:\s+.*?m_Enabled: 0"));

            var productionSources = System.IO.Directory.GetFiles(
                    "Assets", "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(path =>
                    (path.Replace('\\', '/').StartsWith("Assets/_Features/",
                         StringComparison.Ordinal) ||
                     path.Replace('\\', '/').StartsWith("Assets/_Shared/",
                         StringComparison.Ordinal)) &&
                    path.Replace('\\', '/').IndexOf("/Editor/",
                        StringComparison.Ordinal) < 0 &&
                    path.Replace('\\', '/').IndexOf("/Tests/",
                        StringComparison.Ordinal) < 0)
                .Select(System.IO.File.ReadAllText);
            var runtimeSource = string.Join("\n", productionSources);
            Assert.That(runtimeSource, Does.Not.Contain("CrashReportHandler"));
            Assert.That(runtimeSource, Does.Not.Contain("AnalyticsService"));
            Assert.That(runtimeSource, Does.Not.Contain("RecordEvent("));
            Assert.That(runtimeSource, Does.Not.Match(
                @"(?i)(Upload.*Player\.log|Player\.log.*Upload)"));
        }

        [Test]
        public void WindowsPlayerLogSupportPolicy_DocumentsPrivateLocalSubmission()
        {
            const string path = "Docs/Support/Windows-Player-Log-Policy.md";
            Assert.That(System.IO.File.Exists(path), Is.True);
            var document = System.IO.File.ReadAllText(path);
            Assert.That(document, Does.Contain(
                "windows-x64-store-mono-logon-v1"));
            Assert.That(document, Does.Contain(
                @"%USERPROFILE%\AppData\LocalLow\J2M\VectorQuake\Player.log"));
            Assert.That(document, Does.Contain("private support channel"));
            Assert.That(document, Does.Contain("자동 업로드: 없음"));
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
            var configuration = WindowsReleaseBackendConfiguration.CanonicalStoreMono;
            var distribution = WindowsDistributionTargetPolicy.DirectWindows;

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
                    buildIntent = configuration.Intent.ToString(),
                    storeConfigurationSchema = configuration.StoreConfigurationSchema,
                    storeConfigurationId = configuration.StoreConfigurationId,
                    backend = configuration.Candidate.ToString(),
                    scriptingBackend = configuration.Backend.ToString(),
                    managedStrippingLevel = configuration.Stripping.ToString(),
                    playerLogEnabled = true,
                    logPolicyId = configuration.LogPolicyId,
                    automaticLogUpload = false,
                    payloadAudience = configuration.PayloadAudience,
                    distributionTargetId = distribution.TargetId,
                    providerSelectionMode = distribution.ProviderSelectionMode.ToString(),
                    expectedProviderId = distribution.ExpectedProviderId,
                    expectedLaunchArguments = distribution.CopyExpectedLaunchArguments(),
                    requiredArtifacts = distribution.CopyRequiredArtifacts(),
                    forbiddenArtifacts = distribution.CopyForbiddenArtifacts(),
                    expectedStoreLaunch = distribution.ExpectedStoreLaunch,
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
                    buildIntent = configuration.Intent.ToString(),
                    storeConfigurationSchema = configuration.StoreConfigurationSchema,
                    storeConfigurationId = configuration.StoreConfigurationId,
                    backend = configuration.Candidate.ToString(),
                    scriptingBackend = configuration.Backend.ToString(),
                    managedStrippingLevel = configuration.Stripping.ToString(),
                    playerLogEnabled = true,
                    logPolicyId = configuration.LogPolicyId,
                    automaticLogUpload = false,
                    payloadAudience = configuration.PayloadAudience,
                    distributionTargetId = distribution.TargetId,
                    providerSelectionMode = distribution.ProviderSelectionMode.ToString(),
                    expectedProviderId = distribution.ExpectedProviderId,
                    expectedLaunchArguments = distribution.CopyExpectedLaunchArguments(),
                    requiredArtifacts = distribution.CopyRequiredArtifacts(),
                    forbiddenArtifacts = distribution.CopyForbiddenArtifacts(),
                    expectedStoreLaunch = distribution.ExpectedStoreLaunch,
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
                    buildIntent = configuration.Intent.ToString(),
                    storeConfigurationSchema = configuration.StoreConfigurationSchema,
                    storeConfigurationId = configuration.StoreConfigurationId,
                    backend = configuration.Candidate.ToString(),
                    scriptingBackend = configuration.Backend.ToString(),
                    managedStrippingLevel = configuration.Stripping.ToString(),
                    playerLogEnabled = true,
                    logPolicyId = configuration.LogPolicyId,
                    automaticLogUpload = false,
                    payloadAudience = configuration.PayloadAudience,
                    distributionTargetId = distribution.TargetId,
                    providerSelectionMode = distribution.ProviderSelectionMode.ToString(),
                    expectedProviderId = distribution.ExpectedProviderId,
                    expectedLaunchArguments = distribution.CopyExpectedLaunchArguments(),
                    requiredArtifacts = distribution.CopyRequiredArtifacts(),
                    forbiddenArtifacts = distribution.CopyForbiddenArtifacts(),
                    expectedStoreLaunch = distribution.ExpectedStoreLaunch,
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

        private static ReleaseStoreIdentityV1 CreateCanonicalStoreIdentity(
            WindowsDistributionTargetConfiguration distribution = null)
        {
            distribution = distribution ?? WindowsDistributionTargetPolicy.DirectWindows;
            return new ReleaseStoreIdentityV1
            {
                distributionTargetId = distribution.TargetId,
                providerSelectionMode = distribution.ProviderSelectionMode.ToString(),
                expectedProviderId = distribution.ExpectedProviderId,
                expectedLaunchArguments = distribution.CopyExpectedLaunchArguments(),
                requiredArtifacts = distribution.CopyRequiredArtifacts(),
                forbiddenArtifacts = distribution.CopyForbiddenArtifacts(),
                expectedStoreLaunch = distribution.ExpectedStoreLaunch,
                storeConfigurationSchema =
                    WindowsReleaseBuildPolicy.StoreConfigurationSchema,
                storeConfigurationId = WindowsReleaseBuildPolicy.StoreConfigurationId,
                buildIntent = WindowsReleaseBuildIntent.CanonicalStore.ToString(),
                backend = StoreBackendCandidate.Mono.ToString(),
                scriptingBackend = ScriptingImplementation.Mono2x.ToString(),
                managedStrippingLevel = ManagedStrippingLevel.Disabled.ToString(),
                playerLogEnabled = true,
                logPolicyId = WindowsReleaseBuildPolicy.LogPolicyId,
                automaticLogUpload = false,
                payloadAudience = WindowsReleaseBuildPolicy.PayloadAudience,
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

        private sealed class FakeManagedPluginSettings :
            IWindowsReleaseManagedPluginSettings
        {
            public bool ApplyCalled { get; private set; }
            public bool RestoreCalled { get; private set; }
            public bool ApplyValid { get; set; } = true;
            public bool RestoreValid { get; set; } = true;
            public bool RequiredValid { get; set; }

            public ManagedPluginSettingsSnapshot Capture()
            {
                return new ManagedPluginSettingsSnapshot
                {
                    plugins = WindowsReleaseBuildPolicy.TestOnlyManagedPluginPaths
                        .Select(path => new ManagedPluginState { assetPath = path })
                        .ToArray(),
                };
            }

            public void ApplyRequired()
            {
                ApplyCalled = true;
                RequiredValid = ApplyValid;
            }

            public bool IsRequired() => RequiredValid;

            public void Restore(ManagedPluginSettingsSnapshot snapshot)
            {
                RestoreCalled = true;
                RequiredValid = false;
            }

            public bool IsRestored(ManagedPluginSettingsSnapshot snapshot) => RestoreValid;
        }

        private static void WithPlayerOutput(
            Action<string, string, string> assertion)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "windows-release-managed-" + Guid.NewGuid().ToString("N"));
            var output = Path.Combine(root, "VectorQuake.exe");
            var dataRoot = Path.Combine(root, "VectorQuake_Data");
            var managedRoot = Path.Combine(dataRoot, "Managed");
            Directory.CreateDirectory(managedRoot);
            File.WriteAllText(
                Path.Combine(dataRoot, "ScriptingAssemblies.json"),
                "{\"names\":[\"Game.Feature.Gameplay.dll\"]}");
            try
            {
                assertion(output, dataRoot, managedRoot);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
