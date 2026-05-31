using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class PoseMutationArchitectureGovernanceTests
    {
        private const string GameplayRoot = "Assets/_Features/Gameplay";
        private const string TickPipelinePath = "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs";
        private const string TickResultBuilderPath = "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs";

        private static readonly HashSet<string> DirectFacingWriteAllowlist = new(StringComparer.Ordinal)
        {
            NormalizePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldStateWriteContext.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldWriteContext.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldStateMutationPort.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.FinalizationBatch.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TileEffectResolution.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit/MovementCommitter.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs"),
            NormalizePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs"),
        };

        [Test]
        [Category("Core")]
        public void Governance_NoDirectSetFacingOutsidePoseMutationAuthority()
        {
            var violations = new List<string>();
            foreach (var sourcePath in EnumerateProductionSources())
            {
                var normalizedPath = NormalizePath(sourcePath);
                var source = File.ReadAllText(sourcePath);
                if (!source.Contains(".SetFacing(", StringComparison.Ordinal) &&
                    !source.Contains("WithFacing(", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!DirectFacingWriteAllowlist.Contains(normalizedPath))
                {
                    violations.Add(normalizedPath);
                }
            }

            Assert.That(
                string.Join("\n", violations),
                Is.Empty,
                "새 writer는 EntityPoseMutationAuthority를 거쳐야 한다.");
        }

        [Test]
        [Category("Core")]
        public void Governance_MovementDerivedProbeOrIntent_DoesNotDirectlyWriteFacing()
        {
            foreach (var sourcePath in EnumerateProductionSources())
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("MovementDerivedProbe"), sourcePath);
                Assert.That(source, Does.Not.Contain("MovementDerivedIntent"), sourcePath);

                if (!source.Contains("MovementProbe", StringComparison.Ordinal) &&
                    !source.Contains("MovementIntent", StringComparison.Ordinal))
                {
                    continue;
                }

                var normalizedPath = NormalizePath(sourcePath);
                if (DirectFacingWriteAllowlist.Contains(normalizedPath))
                {
                    continue;
                }

                Assert.That(source, Does.Not.Contain(".SetFacing("), $"새 writer는 EntityPoseMutationAuthority를 거쳐야 한다. {sourcePath}");
                Assert.That(source, Does.Not.Contain("WithFacing("), $"새 writer는 EntityPoseMutationAuthority를 거쳐야 한다. {sourcePath}");
            }
        }

        [Test]
        [Category("Core")]
        public void Governance_NoEntityMotionsFromKinematicOnly()
        {
            var tickResultBuilder = File.ReadAllText(GetAbsolutePath(TickResultBuilderPath));
            var movementCarrierBody = ExtractMethodBody(tickResultBuilder, "private static void AppendMovementPresentationRecords");
            var kinematicCarrierBody = ExtractMethodBody(tickResultBuilder, "private static void BuildKinematicMotionPresentation");

            Assert.That(movementCarrierBody, Does.Contain("MovementPresentationRecords"));
            Assert.That(movementCarrierBody, Does.Not.Contain("KinematicOnly"));
            Assert.That(movementCarrierBody, Does.Not.Contain("KinematicSettle"));
            Assert.That(movementCarrierBody, Does.Not.Contain("KinematicRelease"));
            Assert.That(kinematicCarrierBody, Does.Contain("TickKinematicMotionTrack"));
            Assert.That(kinematicCarrierBody, Does.Not.Contain("entityMotions"));
            Assert.That(kinematicCarrierBody, Does.Not.Contain("new TickEntityMotion"));
        }

        [Test]
        [Category("Core")]
        public void Governance_NoPresentationPositionDiffMotionSynthesis()
        {
            var violations = new List<string>();
            foreach (var sourcePath in EnumerateProductionSources())
            {
                var normalizedPath = NormalizePath(sourcePath);
                if (normalizedPath == NormalizePath(TickResultBuilderPath))
                {
                    continue;
                }

                var source = File.ReadAllText(sourcePath);
                if (source.Contains("new TickEntityMotion", StringComparison.Ordinal))
                {
                    violations.Add(normalizedPath);
                }
            }

            Assert.That(
                string.Join("\n", violations),
                Is.Empty,
                "Presentation must not synthesize fake movement tracks from previous/final position diffs.");
        }

        [Test]
        [Category("Core")]
        public void Governance_MovementFacingResolution_IsNotExplicitRotateFallback()
        {
            var tickPipeline = File.ReadAllText(GetAbsolutePath(TickPipelinePath));
            var methodBody = ExtractMethodBody(tickPipeline, "private static EntityPoseMutationRequest CreateFacingOnlyMovementRejectRequest");

            Assert.That(methodBody, Does.Contain("Reason = \"MovementFacingResolutionIsNotExplicitRotate\""));
            Assert.That(methodBody, Does.Contain("HasExplicitRotateAction = false"));
            Assert.That(methodBody, Does.Not.Contain("PoseMutationSource.ExplicitRotateAction"));
            Assert.That(methodBody, Does.Not.Contain("HasExplicitRotateAction = true"));
        }

        private static IEnumerable<string> EnumerateProductionSources()
        {
            return Directory
                .GetFiles(GetAbsolutePath(GameplayRoot), "*.cs", SearchOption.AllDirectories)
                .Where(path => !NormalizePath(path).Contains("/Gameplay_Tests/", StringComparison.Ordinal));
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(relativePath);
        }

        private static string NormalizePath(string path)
        {
            var normalized = path.Replace('\\', '/');
            var assetsIndex = normalized.IndexOf("Assets/", StringComparison.Ordinal);
            return assetsIndex >= 0
                ? normalized.Substring(assetsIndex)
                : normalized;
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature '{signature}'.");

            var bodyStart = source.IndexOf('{', signatureIndex);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Missing method body for '{signature}'.");

            var depth = 0;
            for (var i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(bodyStart, i - bodyStart + 1);
                    }
                }
            }

            Assert.Fail($"Could not extract method body for '{signature}'.");
            return string.Empty;
        }
    }
}
