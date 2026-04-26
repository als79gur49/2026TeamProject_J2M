using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class FinalizeNoRecheckArchitectureTests
    {
        private static readonly string[] ForbiddenFinalizeTokens =
        {
            "RuntimePlacementValidityPolicy",
            "RuntimeTraversalLegalityPolicy",
            "RuntimeSettlementLegalityPolicy",
            "TryGetPlacementBlocker(",
            "TryGetAuthoritativePlacementBlocker(",
            "TryPickImpactTargetAt(",
            "CanAcceptImpactFollowThrough",
            "CanAcceptJumpLandingCell",
        };

        [Test]
        [Category("Extended")]
        public void TickPipeline_RunFinalizePhase_And_FinalizationBatchApplyTo_DoNotReevaluateLegality()
        {
            var tickPipelineSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var runFinalizePhaseBody = ExtractMethodBody(tickPipelineSource, "private void RunFinalizePhase(");
            var applyToBody = ExtractMethodBody(tickPipelineSource, "public void ApplyTo(");

            AssertContainsNoForbiddenTokens(runFinalizePhaseBody, ForbiddenFinalizeTokens);
            AssertContainsNoForbiddenTokens(applyToBody, ForbiddenFinalizeTokens);
        }

        [Test]
        [Category("Core")]
        public void RespawnProcessor_Process_UsesCanonicalAuthoritativePlacementLegality()
        {
            var tickPipelineSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var respawnProcessBody = ExtractMethodBody(tickPipelineSource, "public RespawnPhaseResult Process(");

            Assert.That(
                respawnProcessBody,
                Does.Contain("RuntimePlacementValidityPolicy.EvaluateAuthoritativePlacement("));
            Assert.That(respawnProcessBody, Does.Not.Contain("TryGetAuthoritativePlacementBlocker("));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_WritePath_UsesRepresentableGuard_NotRuntimeLegalityPolicy()
        {
            var worldStateSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs");

            Assert.That(worldStateSource, Does.Contain("EnsurePlacementIsRepresentable"));
            Assert.That(worldStateSource, Does.Not.Contain("RuntimePlacementValidityPolicy"));
        }

        private static void AssertContainsNoForbiddenTokens(string source, string[] forbiddenTokens)
        {
            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), $"Forbidden finalize token found: {forbiddenTokens[i]}");
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, System.StringComparison.Ordinal);
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
