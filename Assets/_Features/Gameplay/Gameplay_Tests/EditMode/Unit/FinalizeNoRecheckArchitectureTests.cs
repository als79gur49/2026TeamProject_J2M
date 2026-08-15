using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class FinalizeNoRecheckArchitectureTests
    {
        private static readonly string[] ForbiddenFinalizeTokens =
        {
            "CanPlace",
            "CanTraverse",
            "CanSettle",
            "RuntimePlacementValidityPolicy",
            "RuntimeTraversalLegalityPolicy",
            "RuntimeSettlementLegalityPolicy",
            "WorldPlacementPolicy",
            "EvaluateTraversal",
            "EvaluateSettlement",
            "EvaluateGameplayPlacement",
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
            var finalizationBatchSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.FinalizationBatch.cs");
            var runFinalizePhaseBody = ExtractMethodBody(tickPipelineSource, "private void RunFinalizePhase(");
            var applyToBody = ExtractMethodBody(finalizationBatchSource, "public void ApplyTo(");

            AssertContainsNoForbiddenTokens(runFinalizePhaseBody, ForbiddenFinalizeTokens);
            AssertContainsNoForbiddenTokens(applyToBody, ForbiddenFinalizeTokens);
        }

        [Test]
        [Category("Extended")]
        public void FinalizeNoRecheckArchitectureTests_CoversTickPipelineFinalizationBatchPartial()
        {
            var tickPipelineSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var finalizationBatchSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.FinalizationBatch.cs");

            Assert.That(
                finalizationBatchSource,
                Does.Contain("internal sealed class FinalizationBatch"),
                "This guard must cover the TickPipeline.FinalizationBatch.cs partial file.");

            var runFinalizePhaseBody = ExtractMethodBody(tickPipelineSource, "private void RunFinalizePhase(");
            var applyToBody = ExtractMethodBody(finalizationBatchSource, "public void ApplyTo(");
            var applyTileFeatureOperationsBody = ExtractMethodBody(finalizationBatchSource, "private void ApplyTileFeatureOperations(");
            var applyBucketBody = ExtractMethodBody(finalizationBatchSource, "private void ApplyBucket(");

            AssertContainsNoForbiddenTokens(runFinalizePhaseBody, ForbiddenFinalizeTokens);
            AssertContainsNoForbiddenTokens(applyToBody, ForbiddenFinalizeTokens);
            AssertContainsNoForbiddenTokens(applyTileFeatureOperationsBody, ForbiddenFinalizeTokens);
            AssertContainsNoForbiddenTokens(applyBucketBody, ForbiddenFinalizeTokens);
        }

        [Test]
        [Category("Core")]
        public void RespawnProcessor_Process_UsesCanonicalAuthoritativePlacementLegality()
        {
            var respawnProcessorSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.RespawnProcessor.cs");
            var respawnProcessBody = ExtractMethodBody(respawnProcessorSource, "public RespawnPhaseResult Process(");

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

        [Test]
        [Category("Extended")]
        public void TickPipeline_CleanupRespawnDirectWritePath_IsDocumentedAndBounded()
        {
            var tickPipelineSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var finalizationBatchSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.FinalizationBatch.cs");
            var cleanupProcessorSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/CleanupProcessor.cs");
            var respawnProcessorSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.RespawnProcessor.cs");
            var moonBlockRespawnProcessorSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.MoonBlockGeneratorRespawnProcessor.cs");
            var canonicalSpec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");

            var runTickWrapperBody = ExtractMethodBody(
                tickPipelineSource,
                "public TickResult RunTick(in TickInput input)");
            var runTickBody = ExtractMethodBody(
                tickPipelineSource,
                "DemoGameplayOverrideSnapshot demoGameplayOverrideSnapshot)");
            Assert.That(
                runTickWrapperBody,
                Does.Contain("return RunTick(input, DemoGameplayOverrideSnapshot.None);"));
            Assert.That(CountOccurrences(tickPipelineSource, "_worldState.CreateWriteContext("), Is.EqualTo(1));
            Assert.That(runTickBody, Does.Contain("var writeContext = _worldState.CreateWriteContext();"));
            AssertAppearsInOrder(
                runTickBody,
                "RunFinalizePhase(resolvePhaseResult.FinalizationBatch, writeContext, completedPhases, phaseTrace);",
                "var postFinalizeSnapshot = SnapshotBuilder.Create(_worldState);",
                "var cleanupPhaseResult = RunCleanupPhase(",
                "postFinalizeSnapshot,",
                "writeContext,",
                "var postCleanupSnapshot = SnapshotBuilder.Create(_worldState);");
            Assert.That(runTickBody, Does.Contain("RunRespawnPhase("));

            var runPlanPhaseBody = ExtractMethodBody(tickPipelineSource, "private PlanPhaseResult RunPlanPhase(");
            var runResolvePhaseBody = ExtractMethodBody(tickPipelineSource, "private ResolvePhaseResult RunResolvePhase(");
            Assert.That(runPlanPhaseBody, Does.Not.Contain("CreateWriteContext("));
            Assert.That(runPlanPhaseBody, Does.Not.Contain("writeContext"));
            Assert.That(runResolvePhaseBody, Does.Not.Contain("CreateWriteContext("));
            Assert.That(runResolvePhaseBody, Does.Not.Contain("writeContext"));

            var runCleanupPhaseBody = ExtractMethodBody(tickPipelineSource, "private CleanupPhaseResult RunCleanupPhase(");
            var expireBoxInteractionLocksBody = ExtractMethodBody(tickPipelineSource, "private static CleanupPhaseResult ExpireBoxInteractionLocks(");
            var expireEnemyGravityFieldAuraFieldsBody = ExtractMethodBody(
                tickPipelineSource,
                "private static CleanupPhaseResult ExpireEnemyGravityFieldAuraFields(");
            var expirePendingEnemyBlockedReactionsBody = ExtractMethodBody(
                tickPipelineSource,
                "private static CleanupPhaseResult ExpirePendingEnemyBlockedReactions(");
            var runRespawnPhaseBody = ExtractMethodBody(tickPipelineSource, "private RespawnPhaseResult RunRespawnPhase(");
            AssertAppearsInOrder(
                runCleanupPhaseBody,
                "_cleanupProcessor.Process(snapshot, writeContext, tickIndex)",
                "ExpireBoxInteractionLocks(snapshot, writeContext, tickIndex, cleanupPhaseResult)",
                "ExpireEnemyGravityFieldAuraFields(snapshot, writeContext, tickIndex, cleanupPhaseResult)",
                "ExpirePendingEnemyBlockedReactions(snapshot, writeContext, tickIndex, cleanupPhaseResult)");
            Assert.That(
                CountOccurrences(runCleanupPhaseBody, "_cleanupProcessor.Process(snapshot, writeContext, tickIndex)"),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(runCleanupPhaseBody, "ExpireBoxInteractionLocks(snapshot, writeContext, tickIndex, cleanupPhaseResult)"),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(runCleanupPhaseBody, "ExpireEnemyGravityFieldAuraFields(snapshot, writeContext, tickIndex, cleanupPhaseResult)"),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(runCleanupPhaseBody, "ExpirePendingEnemyBlockedReactions(snapshot, writeContext, tickIndex, cleanupPhaseResult)"),
                Is.EqualTo(1));
            Assert.That(CountOccurrences(runCleanupPhaseBody, "writeContext"), Is.EqualTo(4));
            Assert.That(runCleanupPhaseBody, Does.Not.Contain("writeContext."));
            Assert.That(runCleanupPhaseBody, Does.Not.Contain("_worldState."));
            Assert.That(expireBoxInteractionLocksBody, Does.Contain("writeContext.RemoveBoxInteractionLockState("));
            Assert.That(
                expireBoxInteractionLocksBody,
                Does.Contain("snapshot.EnumerateBoxInteractionLockStatesOrdered("));
            Assert.That(
                expireEnemyGravityFieldAuraFieldsBody,
                Does.Contain("writeContext.RemoveEnemyGravityFieldAuraFieldState("));
            Assert.That(
                expireEnemyGravityFieldAuraFieldsBody,
                Does.Contain("snapshot.EnumerateEnemyGravityFieldAuraFieldStatesOrdered("));
            Assert.That(
                expirePendingEnemyBlockedReactionsBody,
                Does.Contain("writeContext.ClearPendingEnemyBlockedReaction("));
            Assert.That(
                expirePendingEnemyBlockedReactionsBody,
                Does.Contain("snapshot.EnumeratePendingEnemyBlockedReactionsOrdered("));
            Assert.That(runRespawnPhaseBody, Does.Contain("_respawnProcessor.Process("));
            Assert.That(runRespawnPhaseBody, Does.Contain("_moonBlockGeneratorRespawnProcessor.Process("));
            Assert.That(runRespawnPhaseBody, Does.Contain("writeContext);"));

            Assert.That(cleanupProcessorSource, Does.Contain("public CleanupPhaseResult Process("));
            Assert.That(cleanupProcessorSource, Does.Contain("ICleanupCommitContext writeContext"));
            Assert.That(respawnProcessorSource, Does.Contain("public RespawnPhaseResult Process("));
            Assert.That(respawnProcessorSource, Does.Contain("IWorldWriteContext writeContext"));
            Assert.That(moonBlockRespawnProcessorSource, Does.Contain("public MoonBlockGeneratorRespawnProcessorResult Process("));
            Assert.That(moonBlockRespawnProcessorSource, Does.Contain("IWorldWriteContext writeContext"));

            Assert.That(CountOccurrences(tickPipelineSource, "IWorldWriteContext writeContext"), Is.EqualTo(2));
            Assert.That(CountOccurrences(tickPipelineSource, "ICleanupCommitContext"), Is.EqualTo(4));
            Assert.That(CountOccurrences(respawnProcessorSource, "IWorldWriteContext writeContext"), Is.EqualTo(1));
            Assert.That(CountOccurrences(moonBlockRespawnProcessorSource, "IWorldWriteContext writeContext"), Is.EqualTo(1));
            Assert.That(CountOccurrences(cleanupProcessorSource, "ICleanupCommitContext writeContext"), Is.EqualTo(1));

            Assert.That(finalizationBatchSource, Does.Not.Contain("CleanupProcessor"));
            Assert.That(finalizationBatchSource, Does.Not.Contain("RespawnProcessor"));
            Assert.That(finalizationBatchSource, Does.Not.Contain("MoonBlockGeneratorRespawnProcessor"));
            Assert.That(finalizationBatchSource, Does.Not.Contain("ExpireBoxInteractionLocks"));
            Assert.That(finalizationBatchSource, Does.Not.Contain("ExpireEnemyGravityFieldAuraFields"));
            Assert.That(finalizationBatchSource, Does.Not.Contain("ExpirePendingEnemyBlockedReactions"));

            Assert.That(canonicalSpec, Does.Contain("Cleanup/Respawn direct write path"));
            Assert.That(canonicalSpec, Does.Contain("bounded exception"));
            AssertContainsExactTrimmedLine(
                canonicalSpec,
                "- 허용된 Cleanup direct write entrypoint는 `CleanupProcessor.Process`, `ExpireBoxInteractionLocks`, `ExpireEnemyGravityFieldAuraFields`, `ExpirePendingEnemyBlockedReactions`다.");
            AssertContainsExactTrimmedLine(
                canonicalSpec,
                "- 허용된 Respawn direct write entrypoint는 `RespawnProcessor.Process`, `MoonBlockGeneratorRespawnProcessor.Process`다.");
            Assert.That(canonicalSpec, Does.Contain("SRP 작업 중 몰래 `FinalizationBatch`로 옮기지 않는다"));
        }

        [Test]
        [Category("Extended")]
        public void TickVocabulary_DoesNotMergeSemanticAxisAndExecutionStages()
        {
            var tickPhaseSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Model/Runtime/Phases/TickPhase.cs");
            var movementPhaseResultSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/MovementPhaseResult.cs");
            var attackPhaseResultSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/AttackPhaseResult.cs");
            var phaseResultsSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.PhaseResults.cs");
            var canonicalSpec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");

            var tickPhaseBody = ExtractEnumBody(tickPhaseSource, "public enum TickPhase");
            Assert.That(
                NormalizeWhitespace(tickPhaseBody),
                Is.EqualTo("Plan = 0, Resolve = 1, Finalize = 2, Cleanup = 3, Respawn = 4,"));
            Assert.That(tickPhaseBody, Does.Not.Contain("Movement"));
            Assert.That(tickPhaseBody, Does.Not.Contain("Attack"));
            Assert.That(tickPhaseSource, Does.Not.Contain("MovementPlan"));
            Assert.That(tickPhaseSource, Does.Not.Contain("AttackResolve"));

            Assert.That(movementPhaseResultSource, Does.Contain("internal sealed class MovementPhaseResult"));
            Assert.That(attackPhaseResultSource, Does.Contain("internal sealed class AttackPhaseResult"));
            Assert.That(movementPhaseResultSource, Does.Not.Contain("enum MovementPhaseResult"));
            Assert.That(attackPhaseResultSource, Does.Not.Contain("enum AttackPhaseResult"));
            Assert.That(movementPhaseResultSource, Does.Not.Contain("TickPhase."));
            Assert.That(attackPhaseResultSource, Does.Not.Contain("TickPhase."));

            Assert.That(phaseResultsSource, Does.Contain("internal sealed class PreMovementStatePhaseResult"));
            Assert.That(phaseResultsSource, Does.Not.Contain("public sealed class PreMovementStatePhaseResult"));
            Assert.That(phaseResultsSource, Does.Not.Contain("public enum PreMovementState"));

            Assert.That(canonicalSpec, Does.Contain("semantic axis와 execution-stage axis는 분리한다"));
            Assert.That(canonicalSpec, Does.Contain("`TickPhase`는 현재 코드에서 runtime stage enum이다"));
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

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static void AssertAppearsInOrder(string source, params string[] tokens)
        {
            var previousIndex = -1;
            for (var i = 0; i < tokens.Length; i++)
            {
                var currentIndex = source.IndexOf(tokens[i], previousIndex + 1, StringComparison.Ordinal);
                Assert.That(
                    currentIndex,
                    Is.GreaterThan(previousIndex),
                    $"Expected token in order: {tokens[i]}");
                previousIndex = currentIndex;
            }
        }

        private static void AssertContainsExactTrimmedLine(string source, string expectedLine)
        {
            var lines = source.Replace("\r\n", "\n").Split('\n');
            var matchCount = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i].Trim(), expectedLine, StringComparison.Ordinal))
                {
                    matchCount++;
                }
            }

            Assert.That(matchCount, Is.EqualTo(1), $"Expected exactly one line: {expectedLine}");
        }

        private static string NormalizeWhitespace(string source)
        {
            return string.Join(" ", source.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string ExtractEnumBody(string source, string signature)
        {
            var delimitedBody = ExtractDelimitedBody(source, signature);
            return delimitedBody.Substring(1, delimitedBody.Length - 2);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            return ExtractDelimitedBody(source, signature);
        }

        private static string ExtractDelimitedBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing signature: {signature}");

            var openBraceIndex = source.IndexOf('{', signatureIndex);
            Assert.That(openBraceIndex, Is.GreaterThanOrEqualTo(0), $"Missing body for: {signature}");

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

            Assert.Fail($"Unbalanced braces for signature: {signature}");
            return string.Empty;
        }
    }
}
