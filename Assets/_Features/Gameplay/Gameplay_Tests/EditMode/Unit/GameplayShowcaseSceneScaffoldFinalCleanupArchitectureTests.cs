using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    /*
     * Phase 7B-A scaffold final cleanup guardrail:
     * - locks GameplayShowcaseSceneScaffold as a wrapper-only scene orchestrator
     * - uses Lane A live-ledger context for no-new-regression sign-off, not full-suite-green gating
     */
    public sealed class GameplayShowcaseSceneScaffoldFinalCleanupArchitectureTests
    {
        private const string ScaffoldRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneScaffold.cs";
        private const string InstallerBaseRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs";
        private const string ConfigureDefaultSceneCameraSignature =
            "public static void ConfigureDefaultSceneCamera(\n" +
            "            Camera camera,\n" +
            "            GameplayCameraSettings cameraSettings,\n" +
            "            Vector3 targetPosition,\n" +
            "            Bounds visibleCubeBounds)";

        private static readonly string[] ForbiddenScaffoldFragments =
        {
            "EnsureCameraRig(",
            "ConfigureSceneOutputCameras(",
            "ConfigureSceneCinemachinePath(",
            "ConfigureCinemachineCamera(",
            "CaptureAuthoredSceneCameraPose(",
            "GetUniversalAdditionalCameraData(",
            "renderPostProcessing = true;",
            "DefaultBlend =",
            "new CameraTarget",
            "Camera.main",
            "TopologyTransitionPostFxController",
        };

        private static readonly string[] ForbiddenWrapperFragments =
        {
            "GameplayShowcaseSceneCameraBootstrap",
            "EnsureCameraRig(",
            "ConfigureSceneOutputCameras(",
            "ConfigureSceneCinemachinePath(",
            "ConfigureCinemachineCamera(",
            "CaptureAuthoredSceneCameraPose(",
            "GetRootGameObjects(",
            "GetComponentsInChildren<Cinemachine",
            "Camera.main",
            "TopologyTransitionPostFxController",
        };

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneScaffold_Source_RetainsSceneOrchestrationAndSceneUtilityOwnership()
        {
            var scaffoldSource = ReadRepoFile(ScaffoldRelativePath);

            Assert.That(scaffoldSource, Does.Contain("GameplayShowcaseSceneCameraBootstrap.Bootstrap("));
            Assert.That(scaffoldSource, Does.Contain("private static void DestroyLegacyWorldLabels("));
            Assert.That(scaffoldSource, Does.Contain("private static GameplayBoardRoot EnsureBoardRoot("));
            Assert.That(scaffoldSource, Does.Contain("private static GameplayBoardRoot FindBoardRoot("));
            Assert.That(scaffoldSource, Does.Contain("public static void ConfigureDefaultSceneCamera("));
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneScaffold_Source_DoesNotReintroduce_MovedCameraBootstrapGlue()
        {
            var scaffoldSource = ReadRepoFile(ScaffoldRelativePath);

            foreach (var forbiddenFragment in ForbiddenScaffoldFragments)
            {
                Assert.That(
                    scaffoldSource.Contains(forbiddenFragment),
                    Is.False,
                    $"GameplayShowcaseSceneScaffold must not reclaim moved camera/bootstrap fragment '{forbiddenFragment}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneScaffold_ConfigureDefaultSceneCamera_DeepestOverload_RemainsWrapperOnly()
        {
            var configureDefaultSceneCameraBody = ExtractMethodBody(
                ReadRepoFile(ScaffoldRelativePath),
                ConfigureDefaultSceneCameraSignature);

            Assert.That(configureDefaultSceneCameraBody, Does.Contain("if (camera == null)"));
            Assert.That(configureDefaultSceneCameraBody, Does.Contain("cameraSettings ?? GameplayCameraSettings.CreateShowcaseDefault()"));
            Assert.That(configureDefaultSceneCameraBody, Does.Contain("GameplayCameraRig.ApplySettingsToCamera("));

            foreach (var forbiddenFragment in ForbiddenWrapperFragments)
            {
                Assert.That(
                    configureDefaultSceneCameraBody.Contains(forbiddenFragment),
                    Is.False,
                    $"ConfigureDefaultSceneCamera must stay wrapper-only and not contain '{forbiddenFragment}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneInstallerBase_Still_ComposesThrough_GameplayShowcaseSceneScaffold_EnsureInstallerScaffold()
        {
            var installerBaseSource = ReadRepoFile(InstallerBaseRelativePath);

            Assert.That(installerBaseSource, Does.Contain("GameplayShowcaseSceneScaffold.EnsureInstallerScaffold("));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature '{signature}'.");

            var bodyStart = source.IndexOf('{', signatureIndex);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Missing body start for '{signature}'.");

            var braceDepth = 0;
            for (var i = bodyStart; i < source.Length; i++)
            {
                switch (source[i])
                {
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        if (braceDepth == 0)
                        {
                            return source.Substring(bodyStart + 1, i - bodyStart - 1);
                        }

                        break;
                }
            }

            throw new InvalidOperationException($"Could not extract method body for '{signature}'.");
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
