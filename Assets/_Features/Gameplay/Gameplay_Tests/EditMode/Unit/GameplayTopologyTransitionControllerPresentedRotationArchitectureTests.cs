using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTopologyTransitionControllerPresentedRotationArchitectureTests
    {
        private const string ControllerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTopologyTransitionController.cs";

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_ApplyPresentedRotation_RemainsCanonicalBoardAndCameraApplyPath()
        {
            var applyBody = ExtractMethodBody(
                ReadRepoFile(ControllerRelativePath),
                "private void ApplyPresentedRotation(");
            var rotationIndex = applyBody.IndexOf("_presentedBoardRotation = presentedRotation;", StringComparison.Ordinal);
            var xDegreesIndex = applyBody.IndexOf(
                "_presentedBoardRotationXDegrees = presentedRotationXDegrees;",
                StringComparison.Ordinal);
            var boardApplyIndex = applyBody.IndexOf(
                "_boardRoot?.ApplyPresentationRotation(presentedRotation, _resolveCubeCenter());",
                StringComparison.Ordinal);
            var cameraApplyIndex = applyBody.IndexOf("_cameraRig?.SetPresentedTopologyOrbit(", StringComparison.Ordinal);

            Assert.That(rotationIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(xDegreesIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(boardApplyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(cameraApplyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(rotationIndex, Is.LessThan(boardApplyIndex));
            Assert.That(xDegreesIndex, Is.LessThan(boardApplyIndex));
            Assert.That(boardApplyIndex, Is.LessThan(cameraApplyIndex));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_StartBoardRotationTween_RoutesTweenUpdatesThroughApplyPresentedRotation()
        {
            var startTweenBody = ExtractMethodBody(
                ReadRepoFile(ControllerRelativePath),
                "private void StartBoardRotationTween(");

            Assert.That(startTweenBody, Does.Contain("ApplyPresentedRotation(tweenedRotationXDegrees);"));
            Assert.That(
                startTweenBody,
                Does.Contain(".OnComplete(() => ApplyPresentedRotation(destinationRotationXDegrees, forceApply: true))"));
            Assert.That(startTweenBody, Does.Not.Contain("_presentedBoardRotation ="));
            Assert.That(startTweenBody, Does.Not.Contain("_presentedBoardRotationXDegrees ="));
            Assert.That(startTweenBody, Does.Not.Contain("_boardRoot?.ApplyPresentationRotation("));
            Assert.That(startTweenBody, Does.Not.Contain("_cameraRig?.SetPresentedTopologyOrbit("));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_AttachCameraRig_And_Reset_RouteThroughApplyPresentedRotation()
        {
            var source = ReadRepoFile(ControllerRelativePath);
            var attachCameraRigBody = ExtractMethodBody(source, "public void AttachCameraRig(GameplayCameraRig cameraRig)");
            var resetBody = ExtractMethodBody(source, "public void Reset()");

            Assert.That(
                attachCameraRigBody,
                Does.Contain("ApplyPresentedRotation(_presentedBoardRotationXDegrees, forceApply: true);"));
            Assert.That(resetBody, Does.Contain("ApplyPresentedRotation(0f, forceApply: true);"));
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
