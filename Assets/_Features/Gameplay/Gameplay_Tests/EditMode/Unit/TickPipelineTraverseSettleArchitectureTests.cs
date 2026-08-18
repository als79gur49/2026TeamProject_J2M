using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineTraverseSettleArchitectureTests
    {
        private static readonly string[] JumpLandingForbiddenTokens =
        {
            "TryGetPlacementBlocker(",
            "TryGetAuthoritativePlacementBlocker(",
            "EnumerateUnitsAt(",
            "TryResolveContestedJumpLandingTarget(",
            "EnemyJumpPhase.Airborne",
            "TileFeatureMovementBlockerQuery.",
            "BarricadeEffectiveActivationPolicy.",
        };

        private static readonly string[] ImpactForbiddenTokens =
        {
            "TryGetPlacementBlocker(",
            "TryGetAuthoritativePlacementBlocker(",
            "EnumerateUnitsAt(",
            "TryPickImpactTargetAt(",
            "TryPickHostileUnitImpactTargetAt(",
            "TryGetSolidSemanticAt(",
            "EntityBoardPresence.Detached",
            "EnemyJumpPhase.Airborne",
            "TileFeatureMovementBlockerQuery.",
            "BarricadeEffectiveActivationPolicy.",
        };

        [Test]
        [Category("Extended")]
        public void ResolveJumpLandingSpaceContestsCanonical_UsesSettlementOwner_NotFileLocalLegalityRecomposition()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var body = ExtractMethodBody(source, "private FinalizationBatch ResolveJumpLandingSpaceContestsCanonical(");

            AssertContainsNoForbiddenTokens(body, JumpLandingForbiddenTokens);
            Assert.That(body, Does.Contain("RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell("));
            Assert.That(body, Does.Contain("RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell("));
            Assert.That(body, Does.Contain("new SettlementContext("));
            Assert.That(body, Does.Contain("new JumpLandingEvidence("));
            Assert.That(body, Does.Contain("_tileFeatureSettlementEvidence"));
            Assert.That(body, Does.Not.Contain("payload.CrushedBoxEntityId"));
        }

        [Test]
        [Category("Extended")]
        public void ResolveImpactSpaceContestsCanonical_UsesSettlementOwner_NotFileLocalLegalityRecomposition()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var body = ExtractMethodBody(source, "private void ResolveImpactSpaceContestsCanonical(");

            AssertContainsNoForbiddenTokens(body, ImpactForbiddenTokens);
            Assert.That(body, Does.Contain("RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThroughDetailed("));
            Assert.That(body, Does.Contain("new SettlementContext("));
            Assert.That(body, Does.Contain("new ImpactFollowThroughEvidence("));
            Assert.That(body, Does.Contain("_tileFeatureSettlementEvidence"));
        }

        private static void AssertContainsNoForbiddenTokens(string source, string[] forbiddenTokens)
        {
            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), $"Forbidden orchestration token found: {forbiddenTokens[i]}");
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature: {signature}");

            var openBraceIndex = source.IndexOf('{', signatureIndex);
            Assert.That(openBraceIndex, Is.GreaterThanOrEqualTo(0), $"Missing method body for: {signature}");

            var depth = 0;
            for (var index = openBraceIndex; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(openBraceIndex, index - openBraceIndex + 1);
                    }
                }
            }

            Assert.Fail($"Unbalanced braces for method signature: {signature}");
            return string.Empty;
        }
    }
}
