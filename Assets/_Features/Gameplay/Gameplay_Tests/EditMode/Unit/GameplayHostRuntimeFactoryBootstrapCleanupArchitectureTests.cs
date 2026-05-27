using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayHostRuntimeFactoryBootstrapCleanupArchitectureTests
    {
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string BootstrapRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap";
        private const string BootstrapFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap.meta";
        private const string FactoryRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs";
        private const string HelperRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayHostTopologyVisualRuntimeBootstrap.cs";
        private const string SceneHostRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs";
        private const string ShowcaseInstallerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs";
        private const string VfxRuntimeInstallerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxRuntimeInstaller.cs";
        private const string PresentationExtensionRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationExtension.cs";

        private static readonly string[] ForbiddenFactoryFragments =
        {
            "ConfigureViewCameraRig(",
            "ResolveViewCamera(",
            "ResolveOutputCamera(",
            "ResolveOutputCameraBrain(",
            "presenter.AttachOutputCamera(outputCamera);",
            "presenter.AttachCameraRuntime(viewCameraRig, outputCameraBrain);",
            "presenter.AttachTopologyTransitionPostFxController(topologyTransitionPostFxController);",
            "topologyTransitionPostFxController.Initialize(configuration.TopologyTransitionPostFxProfile, outputCamera);",
            "hostObject.GetComponent<TopologyTransitionPostFxController>() ??",
        };

        private static readonly string[] ForbiddenHelperFragments =
        {
            "EvaluateMotionBlurIntensity(",
            "EvaluateDistortionIntensity(",
            "ApplyMotionBlurDefaults(",
            "ApplyDistortionDefaults(",
            "TopologyTransitionDistortionProfile.ApplyDefaults(",
            "TopologyTransitionCameraShakeController",
            "EvaluatePulse(",
            "ResolveSignedPositionAmplitude(",
            "ResolveSignedRotationAmplitude(",
        };

        private static readonly string[] ForbiddenShakeEnvelopeFieldNames =
        {
            "ImpactStart01",
            "ImpactDuration01",
            "ImpactOscillationCycles",
            "ImpactLocalPositionAmplitude",
            "ImpactLocalRotationAmplitudeDegrees",
            "LandingStart01",
            "LandingDuration01",
            "LandingOscillationCycles",
            "LandingLocalPositionAmplitude",
            "LandingLocalRotationAmplitudeDegrees",
        };

        private static readonly string[] ForbiddenPostFxEnvelopeFieldNames =
        {
            "MaxBlurIntensity",
            "CameraClamp",
            "AngularVelocityResponseExponent",
            "LandingFadeStart01",
            "LandingFadeExponent",
            "ImpactIntensity",
            "LandingIntensity",
            "XMultiplier",
            "YMultiplier",
            "Center",
            "Scale",
        };

        [Test]
        [Category("Extended")]
        public void HostBootstrapHelperFile_Exists_InDedicatedBootstrapLane()
        {
            Assert.That(Directory.Exists(GetAbsolutePath(BootstrapRelativeDirectory)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(BootstrapFolderMetaRelativePath)), Is.True);

            var helperSource = ReadRepoFile(HelperRelativePath);

            Assert.That(helperSource, Does.Contain("internal static class GameplayHostTopologyVisualRuntimeBootstrap"));
            Assert.That(helperSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForHostBootstrapHelper()
        {
            var runtimeSource = ReadCombinedSource(RuntimeRelativeDirectory);

            Assert.That(
                CountMatches(runtimeSource, @"\bclass\s+GameplayHostTopologyVisualRuntimeBootstrap\b"),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostRuntimeFactory_DelegatesTopologyVisualRuntimeBootstrap_ToHelper()
        {
            var factorySource = ReadRepoFile(FactoryRelativePath);

            Assert.That(factorySource, Does.Contain("GameplayCameraStartupPlanComposer.Compose("));
            Assert.That(factorySource, Does.Contain("GameplayShowcaseSceneCameraBootstrap.ApplyResolvedStartupLens("));
            Assert.That(factorySource, Does.Contain("GameplayHostTopologyVisualRuntimeBootstrap.Attach("));

            foreach (var forbiddenFragment in ForbiddenFactoryFragments)
            {
                Assert.That(
                    factorySource.Contains(forbiddenFragment),
                    Is.False,
                    $"GameplayHostRuntimeFactory still contains moved inline glue fragment '{forbiddenFragment}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void HostBootstrapHelper_Source_UsesExistingWiringApis_AndAvoidsBehaviorOwnership()
        {
            var helperSource = ReadRepoFile(HelperRelativePath);

            Assert.That(helperSource, Does.Contain("presenter.AttachOutputCamera(startupPlan.OutputCamera);"));
            Assert.That(helperSource, Does.Contain("cameraRig.ConfigureTopologyTransitionCameraShake(startupPlan.TopologyTransitionCameraShakeProfile);"));
            Assert.That(helperSource, Does.Not.Contain("cameraRig.ResolveConfiguredSettings("));
            Assert.That(helperSource, Does.Not.Contain("configuration.CameraBaselineAuthoringPolicy"));
            Assert.That(helperSource, Does.Contain("cameraRig.ApplySettings(startupPlan.ResolvedCameraSettings);"));
            Assert.That(helperSource, Does.Contain("cameraRig.Initialize("));
            Assert.That(helperSource, Does.Contain("topologyTransitionPostFxController.Initialize("));
            Assert.That(helperSource, Does.Contain("startupPlan.TopologyTransitionPostFxProfile"));
            Assert.That(helperSource, Does.Contain("startupPlan.OutputCamera"));
            Assert.That(helperSource, Does.Contain("presenter.AttachCameraRuntime(viewCameraRig, startupPlan.OutputCameraBrain);"));
            Assert.That(helperSource, Does.Contain("presenter.AttachTopologyTransitionPostFxController(topologyTransitionPostFxController);"));

            foreach (var forbiddenFragment in ForbiddenHelperFragments)
            {
                Assert.That(
                    helperSource.Contains(forbiddenFragment),
                    Is.False,
                    $"Host bootstrap helper must stay wiring-only and not contain '{forbiddenFragment}'.");
            }

            foreach (var fieldName in ForbiddenShakeEnvelopeFieldNames.Concat(ForbiddenPostFxEnvelopeFieldNames))
            {
                Assert.That(
                    ContainsIdentifierToken(helperSource, fieldName),
                    Is.False,
                    $"Host bootstrap helper must not directly interpret envelope field '{fieldName}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Still_ComposesThroughGameplayHostRuntimeFactory()
        {
            var sceneHostSource = ReadRepoFile(SceneHostRelativePath);
            var runtimeAssembly = typeof(GameplaySceneHost).Assembly;
            var helperType = runtimeAssembly.GetType("Game.Feature.Gameplay.Host.GameplayHostTopologyVisualRuntimeBootstrap");

            Assert.That(sceneHostSource, Does.Contain("GameplayHostRuntimeFactory.Create(this, configuration);"));
            Assert.That(helperType, Is.Not.Null);
            Assert.That(
                helperType?.GetMethod("Attach", BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void ShowcaseInstaller_InstallsBootstrapServices_BeforeHostInitialize()
        {
            var installerSource = ReadRepoFile(ShowcaseInstallerRelativePath);
            var contractsSource = ReadRepoFile(PresentationExtensionRelativePath);
            var vfxInstallerSource = ReadRepoFile(VfxRuntimeInstallerRelativePath);

            Assert.That(contractsSource, Does.Contain("interface IGameplayBootstrapInstaller"));
            Assert.That(contractsSource, Does.Contain("interface IGameplayBootstrapReadiness"));
            Assert.That(vfxInstallerSource, Does.Contain("IGameplayBootstrapInstaller"));
            Assert.That(vfxInstallerSource, Does.Contain("IGameplayBootstrapReadiness"));
            Assert.That(vfxInstallerSource, Does.Contain("productionRuntime.ConfigureHostDefaultMap(hostDefaultCueMap);"));

            Assert.That(
                installerSource.IndexOf("InstallBootstrapServices(gameObject)", StringComparison.Ordinal),
                Is.LessThan(installerSource.IndexOf("BuildInitialGameplayState()", StringComparison.Ordinal)));
            Assert.That(
                installerSource.IndexOf("ValidateBootstrapReadiness(gameObject)", StringComparison.Ordinal),
                Is.LessThan(installerSource.IndexOf("BuildInitialGameplayState()", StringComparison.Ordinal)));
            Assert.That(
                installerSource.IndexOf("InstallBootstrapServices(gameObject)", StringComparison.Ordinal),
                Is.LessThan(installerSource.IndexOf("host.Initialize(", StringComparison.Ordinal)));
        }

        private static int CountMatches(string source, string pattern)
        {
            return Regex.Matches(source, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
        }

        private static bool ContainsIdentifierToken(string source, string identifier)
        {
            return Regex.IsMatch(
                source,
                $@"\b{Regex.Escape(identifier)}\b",
                RegexOptions.CultureInvariant);
        }

        private static string ReadCombinedSource(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(GetAbsolutePath(relativeDirectory), "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
