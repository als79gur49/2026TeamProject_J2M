using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTopologyTransitionControllerActiveEndpointsArchitectureTests
    {
        private const string ControllerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTopologyTransitionController.cs";

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_DefinesCanonicalActiveTransitionEndpointHelper()
        {
            var source = ReadRepoFile(ControllerRelativePath);

            Assert.That(source, Does.Contain("private readonly struct ActiveTransitionEndpoints"));
            Assert.That(
                source,
                Does.Contain(
                    "private ActiveTransitionEndpoints ConfigureActiveTransitionEndpoints(CubeTopologyState destinationTopology)"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_RefreshTopologyTrack_UsesCanonicalActiveEndpointHelper()
        {
            var refreshTopologyTrackBody = ExtractMethodBody(
                ReadRepoFile(ControllerRelativePath),
                "public void RefreshTopologyTrack(");

            Assert.That(
                refreshTopologyTrackBody,
                Does.Contain(
                    "var activeTransitionEndpoints = ConfigureActiveTransitionEndpoints(topologyMotion.DestinationTopology);"));
            Assert.That(
                refreshTopologyTrackBody,
                Does.Contain("ApplyPresentedRotation(activeTransitionEndpoints.StartRotationXDegrees, forceApply: true);"));
            Assert.That(
                refreshTopologyTrackBody,
                Does.Contain("RecalculateVisualState(activeTransitionEndpoints.StartRotation, deltaTime: 0f);"));
            Assert.That(
                refreshTopologyTrackBody,
                Does.Contain("activeTransitionEndpoints.StartRotationXDegrees,"));
            Assert.That(
                refreshTopologyTrackBody,
                Does.Contain("activeTransitionEndpoints.DestinationRotationXDegrees,"));
            Assert.That(CountMatches(refreshTopologyTrackBody, "_boardSurfaceTransitionStartRotation ="), Is.EqualTo(1));
            Assert.That(CountMatches(refreshTopologyTrackBody, "_boardSurfaceTransitionDestinationRotation ="), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_RefreshBoardSurfaceTransition_UsesCanonicalActiveEndpointHelper()
        {
            var refreshBoardSurfaceTransitionBody = ExtractMethodBody(
                ReadRepoFile(ControllerRelativePath),
                "public void RefreshBoardSurfaceTransition(");

            Assert.That(
                refreshBoardSurfaceTransitionBody,
                Does.Contain("ConfigureActiveTransitionEndpoints(topologyMotion.DestinationTopology);"));
            Assert.That(
                CountMatches(refreshBoardSurfaceTransitionBody, "_boardSurfaceTransitionStartRotation ="),
                Is.EqualTo(1));
            Assert.That(
                CountMatches(refreshBoardSurfaceTransitionBody, "_boardSurfaceTransitionDestinationRotation ="),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTopologyTransitionController_InactiveBoardSurfaceBranch_GuardsOnlyRendererCompletion()
        {
            var refreshBoardSurfaceTransitionBody = ExtractMethodBody(
                ReadRepoFile(ControllerRelativePath),
                "public void RefreshBoardSurfaceTransition(");

            Assert.That(
                refreshBoardSurfaceTransitionBody,
                Does.Contain("if (!_boardSurfaceRenderer.CanSkipCompleteTopologyTransition(committedTopology))"));
            Assert.That(
                refreshBoardSurfaceTransitionBody,
                Does.Contain("_boardSurfaceRenderer.CompleteTopologyTransition(committedTopology);"));
            Assert.That(
                CountMatches(
                    refreshBoardSurfaceTransitionBody,
                    "_boardSurfaceRenderer.CompleteTopologyTransition(committedTopology);"),
                Is.EqualTo(1));
            Assert.That(refreshBoardSurfaceTransitionBody, Does.Contain("_lastCommittedTopology = committedTopology;"));
            Assert.That(refreshBoardSurfaceTransitionBody, Does.Contain("_isBoardSurfaceTransitionActive = false;"));
            Assert.That(refreshBoardSurfaceTransitionBody, Does.Contain("TopologyPresentationCompleted?.Invoke(committedTopology);"));
            Assert.That(refreshBoardSurfaceTransitionBody, Does.Contain("UpdateInactiveVisualState();"));
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

        private static int CountMatches(string source, string value)
        {
            var count = 0;
            var searchStartIndex = 0;
            while (searchStartIndex < source.Length)
            {
                var matchIndex = source.IndexOf(value, searchStartIndex, StringComparison.Ordinal);
                if (matchIndex < 0)
                {
                    return count;
                }

                count++;
                searchStartIndex = matchIndex + value.Length;
            }

            return count;
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
