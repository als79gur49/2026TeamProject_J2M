using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayShowcaseSceneScaffoldBootstrapCleanupArchitectureTests
    {
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ScaffoldRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneScaffold.cs";
        private const string HelperRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayShowcaseSceneCameraBootstrap.cs";
        private const string InstallerBaseRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs";

        private static readonly string[] ForbiddenScaffoldFragments =
        {
            "EnsureCameraRig(",
            "ConfigureSceneOutputCameras(",
            "ConfigureSceneCinemachinePath(",
            "ConfigureCinemachineCamera(",
            "EnsureCameraRig(installerRoot, cameraSettings, topologyTransitionCameraShakeProfile)",
            "ConfigureSceneOutputCameras(installerRoot.scene);",
            "ConfigureSceneCinemachinePath(installerRoot.scene, boardRoot,",
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
        public void ShowcaseSceneCameraBootstrapHelperFile_Exists_InDedicatedBootstrapLane()
        {
            var helperSource = ReadRepoFile(HelperRelativePath);

            Assert.That(helperSource, Does.Contain("internal static class GameplayShowcaseSceneCameraBootstrap"));
            Assert.That(helperSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForShowcaseSceneCameraBootstrapHelper()
        {
            var runtimeSource = ReadCombinedSource(RuntimeRelativeDirectory);

            Assert.That(
                CountMatches(runtimeSource, @"\bclass\s+GameplayShowcaseSceneCameraBootstrap\b"),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneScaffold_DelegatesCameraBootstrap_ToHelper()
        {
            var scaffoldSource = ReadRepoFile(ScaffoldRelativePath);

            Assert.That(scaffoldSource, Does.Contain("GameplayShowcaseSceneCameraBootstrap.Bootstrap("));
            Assert.That(scaffoldSource, Does.Contain("DestroyLegacyWorldLabels("));
            Assert.That(scaffoldSource, Does.Contain("EnsureBoardRoot("));

            foreach (var forbiddenFragment in ForbiddenScaffoldFragments)
            {
                Assert.That(
                    scaffoldSource.Contains(forbiddenFragment),
                    Is.False,
                    $"GameplayShowcaseSceneScaffold still contains moved camera/bootstrap fragment '{forbiddenFragment}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void ShowcaseSceneCameraBootstrapHelper_Source_UsesExistingSceneWiringApis_AndAvoidsBehaviorOwnership()
        {
            var helperSource = ReadRepoFile(HelperRelativePath);

            Assert.That(helperSource, Does.Not.Contain("rig.ApplySettings("));
            Assert.That(helperSource, Does.Not.Contain("rig.ConfigureTopologyTransitionCameraShake("));
            Assert.That(helperSource, Does.Contain("internal static void ApplyResolvedStartupLens("));
            Assert.That(helperSource, Does.Contain("rig?.CaptureAuthoredSceneCameraPose("));
            Assert.That(helperSource, Does.Contain("ApplyResolvedStartupLens(cinemachineCameras[j], startupPlan.ResolvedCameraSettings);"));
            Assert.That(helperSource, Does.Not.Contain("if (!baselineAuthoringPolicy.UseAuthoredSceneCameraLens)"));
            Assert.That(helperSource, Does.Contain("brains[j].DefaultBlend ="));
            Assert.That(helperSource, Does.Contain("cinemachineCamera.Target = new CameraTarget"));
            Assert.That(helperSource, Does.Not.Contain("camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;"));

            foreach (var forbiddenFragment in ForbiddenHelperFragments)
            {
                Assert.That(
                    helperSource.Contains(forbiddenFragment),
                    Is.False,
                    $"Showcase scene camera bootstrap helper must stay wiring-only and not contain '{forbiddenFragment}'.");
            }

            foreach (var fieldName in ForbiddenShakeEnvelopeFieldNames.Concat(ForbiddenPostFxEnvelopeFieldNames))
            {
                Assert.That(
                    ContainsIdentifierToken(helperSource, fieldName),
                    Is.False,
                    $"Showcase scene camera bootstrap helper must not directly interpret envelope field '{fieldName}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneInstallerBase_Still_ComposesThroughGameplayShowcaseSceneScaffold()
        {
            var installerBaseSource = ReadRepoFile(InstallerBaseRelativePath);

            Assert.That(installerBaseSource, Does.Contain("GameplayShowcaseSceneScaffold.EnsureInstallerScaffold("));
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
