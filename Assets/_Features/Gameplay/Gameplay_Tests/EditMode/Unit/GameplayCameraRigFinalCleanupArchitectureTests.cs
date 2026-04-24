using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayCameraRigFinalCleanupArchitectureTests
    {
        private const string GameplayCameraRigRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs";

        private static readonly string[] ForbiddenFragments =
        {
            "EvaluateMotionBlurIntensity(",
            "EvaluateDistortionIntensity(",
            "ApplyMotionBlurDefaults(",
            "ApplyDistortionDefaults(",
            "EvaluatePulse(",
            "ResolveSignedPositionAmplitude(",
            "ResolveSignedRotationAmplitude(",
            "Camera.main",
            "GetRootGameObjects(",
            "GetComponentsInChildren<CinemachineCamera>(",
            "GetComponentsInChildren<CinemachineBrain>(",
            "GetUniversalAdditionalCameraData(",
            "TopologyTransitionPostFxController",
            "GameplayHostTopologyVisualRuntimeBootstrap",
            "GameplayShowcaseSceneCameraBootstrap",
        };

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_Source_DoesNotReclaim_ShakePostFx_OrBootstrapOwnership()
        {
            var source = ReadRepoFile(GameplayCameraRigRelativePath);

            foreach (var forbiddenFragment in ForbiddenFragments)
            {
                Assert.That(
                    source.Contains(forbiddenFragment),
                    Is.False,
                    $"GameplayCameraRig must not reclaim forbidden fragment '{forbiddenFragment}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_Source_Retains_CanonicalAuthoredBaselineOwnership()
        {
            var source = ReadRepoFile(GameplayCameraRigRelativePath);

            Assert.That(source, Does.Contain("CaptureAuthoredSceneCameraPose("));
            Assert.That(source, Does.Contain("ResolveConfiguredSettings("));
            Assert.That(source, Does.Contain("_authoredSceneCameraBaselineLocalPosition"));
            Assert.That(source, Does.Contain("_authoredSceneCameraBaselineLocalRotation"));
            Assert.That(source, Does.Contain("_useAuthoredSceneCameraPoseAsBaseline"));
            Assert.That(source, Does.Contain("PrepareAuthoredSceneCameraBaseline("));
            Assert.That(source, Does.Contain("ApplyAuthoredSceneCameraLens("));
            Assert.That(source, Does.Contain("ResolveAuthoredSceneCameraBaselinePose()"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_Source_Retains_FinalApplyOwnerPipeline()
        {
            var source = ReadRepoFile(GameplayCameraRigRelativePath);

            Assert.That(source, Does.Contain("private void ApplyCameraPose()"));
            Assert.That(source, Does.Contain("private void ApplyPresentedPoseToHierarchy("));
            Assert.That(source, Does.Contain("private void ApplyCachedShakeToHierarchy()"));
            Assert.That(source, Does.Contain("private void ApplyDirectCameraPose("));
            Assert.That(source, Does.Contain("_viewCamera"));
            Assert.That(source, Does.Contain("_cameraPoseRoot"));
            Assert.That(source, Does.Contain("_cameraEffectsRoot"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_ApplyCameraPose_PreservesSemanticStageOrder()
        {
            var body = ExtractMethodBody(
                ReadRepoFile(GameplayCameraRigRelativePath),
                "private void ApplyCameraPose()");
            var refreshIndex = body.IndexOf("ResolvePoseHierarchy();", StringComparison.Ordinal);
            var resolveIndex = body.IndexOf("ResolveUnshakenPresentedPose()", StringComparison.Ordinal);
            var hierarchyApplyIndex = body.IndexOf("ApplyPresentedPoseToHierarchy(", StringComparison.Ordinal);
            var shakeApplyIndex = body.IndexOf("ApplyCachedShakeToHierarchy()", StringComparison.Ordinal);
            var directApplyIndex = body.IndexOf("ApplyDirectCameraPose(", StringComparison.Ordinal);

            Assert.That(refreshIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(hierarchyApplyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(shakeApplyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(directApplyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(refreshIndex, Is.LessThan(resolveIndex));
            Assert.That(resolveIndex, Is.LessThan(hierarchyApplyIndex));
            Assert.That(hierarchyApplyIndex, Is.LessThan(shakeApplyIndex));
            Assert.That(shakeApplyIndex, Is.LessThan(directApplyIndex));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_Source_SharesOnly_NonBaselinePoseMath()
        {
            var source = ReadRepoFile(GameplayCameraRigRelativePath);
            var applySettingsToCameraBody = ExtractMethodBody(source, "public static void ApplySettingsToCamera(");

            Assert.That(source, Does.Contain("private static UnshakenCameraPose ResolveOrbitDistanceCameraPose("));
            Assert.That(source, Does.Contain("private UnshakenCameraPose ResolveAuthoredSceneCameraBaselinePose()"));
            Assert.That(applySettingsToCameraBody, Does.Contain("ResolveOrbitDistanceCameraPose("));
            Assert.That(applySettingsToCameraBody, Does.Not.Contain("ResolveAuthoredSceneCameraBaselinePose("));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_Source_UsesShakeController_AsCacheInputOnly()
        {
            var source = ReadRepoFile(GameplayCameraRigRelativePath);
            var applyVisualStateBody = ExtractMethodBody(
                source,
                "internal void ApplyTopologyTransitionVisualState(in TopologyTransitionVisualState visualState)");

            Assert.That(source, Does.Contain("_topologyTransitionCameraShakeController.Evaluate("));
            Assert.That(applyVisualStateBody, Does.Contain("CacheTopologyTransitionShakeResult("));
            Assert.That(applyVisualStateBody, Does.Not.Contain("ApplyCachedShakeToHierarchy("));
            Assert.That(applyVisualStateBody, Does.Not.Contain("ApplyDirectCameraPose("));
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
