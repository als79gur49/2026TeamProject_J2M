using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests
{
    internal static class MovementExecutionOwnershipAssert
    {
        public const string ExplicitGenericExpansionOwnedRequiredReason = "GenericOrdinaryExpansionRequiresExplicitOwner";
        public const string PlayerGenericExpansionOwnedRemovedReason = "PlayerGenericExpansionRemovedFromRuntime";
        public const string EnemyGenericExpansionOwnedRemovedReason = "EnemyGenericExpansionRemovedFromRuntime";
        public const string ChargeGenericExpansionOwnedRemovedReason = "ChargeGenericExpansionRemovedFromRuntime";

        public static void NoGenericExpansionOrdinaryUnitMove(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                NoGenericExpansionOrdinaryUnitMove(result, entityIds[i]);
            }
        }

        public static void NoGenericExpansionOrdinaryUnitMove(TickResult result, int entityId)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    motion.MotionKind == TickEntityMotionKind.Move),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned &&
                    operation.Metadata.MovementSemanticKind == MovementSemanticKind.Move),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("GenericUnitOrdinaryMovementDetected", System.StringComparison.Ordinal) &&
                    reason.Contains($"E={entityId}", System.StringComparison.Ordinal)),
                Is.False,
                BuildDebug(result, entityId));
        }

        public static void NoUnexpectedLegacyOrdinaryDiagnostics(TickResult result)
        {
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("GenericUnitOrdinaryMovementDetected", System.StringComparison.Ordinal)),
                Is.False,
                BuildDebug(result));
        }

        public static void NoCoveredLocomotionGenericExpansionOwned(TickResult result, params int[] entityIds)
        {
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
            NoGenericExpansionOrdinaryUnitMove(result, entityIds);
        }

        public static void NoCoveredFallbackInDefaultGameplayLocomotion(TickResult result, params int[] entityIds)
        {
            NoCoveredLocomotionGenericExpansionOwned(result, entityIds);
        }

        public static void NoCoveredFallbackUnderDefaultGameplayLocomotion(TickResult result, params int[] entityIds)
        {
            NoCoveredFallbackInDefaultGameplayLocomotion(result, entityIds);
        }

        public static void NoCoveredFallbackUnderNone(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                NoLegacyOrdinaryUnitOperationOrPresentation(result, entityIds[i]);
            }
        }

        public static void RequiresExplicitGenericExpansionOwnedBaseline(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                NoLegacyOrdinaryUnitOperationOrPresentation(result, entityIds[i]);
                Assert.That(
                    result.MovementPhaseResult.RejectedReasons.Any(reason =>
                        reason.Contains("GenericUnitOrdinaryMovementDetected", System.StringComparison.Ordinal) &&
                        reason.Contains($"E={entityIds[i]}", System.StringComparison.Ordinal) &&
                        reason.Contains(ExplicitGenericExpansionOwnedRequiredReason, System.StringComparison.Ordinal)),
                    Is.True,
                    BuildDebug(result, entityIds[i]));
            }
        }

        public static void PlayerGenericExpansionRemovedFromRuntime(TickResult result, int playerEntityId)
        {
            NoLegacyOrdinaryUnitOperationOrPresentation(result, playerEntityId);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        public static void EnemyGenericExpansionRemovedFromRuntime(TickResult result, int enemyEntityId)
        {
            HasGenericExpansionOwnedMoveEntity(result, enemyEntityId);
            HasGenericExpansionOwnedMove(result, enemyEntityId);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        public static void ChargeGenericExpansionRemovedFromRuntime(TickResult result, int chargeEntityId)
        {
            NoLegacyOrdinaryUnitOperationOrPresentation(result, chargeEntityId);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        public static void AssertCoveredFallbackCurrentOwnerships(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                var entityId = entityIds[i];
                NoLegacyOrdinaryUnitOperationOrPresentation(result, entityId);
                NoUnexpectedLegacyOrdinaryDiagnostics(result);
            }
        }

        public static void AssertPlayerFallbackRemovedFromRuntime(TickResult result, int playerEntityId)
        {
            PlayerGenericExpansionRemovedFromRuntime(result, playerEntityId);
        }

        public static void AssertEnemyFallbackRemovedFromRuntime(TickResult result, int enemyEntityId)
        {
            EnemyGenericExpansionRemovedFromRuntime(result, enemyEntityId);
        }

        public static void AssertChargeFallbackRemovedFromRuntime(TickResult result, int chargeEntityId)
        {
            ChargeGenericExpansionRemovedFromRuntime(result, chargeEntityId);
        }

        public static void NoPlayerLegacyOrdinaryFallback(TickResult result, int playerEntityId)
        {
            NoGenericExpansionOrdinaryUnitMove(result, playerEntityId);
        }

        public static void NoEnemyGenericExpansionOrdinaryFallback(TickResult result, int enemyEntityId)
        {
            NoGenericExpansionOrdinaryUnitMove(result, enemyEntityId);
        }

        public static void NoChargeActiveGenericExpansionOwned(TickResult result, int chargeEntityId)
        {
            NoGenericExpansionOrdinaryUnitMove(result, chargeEntityId);
        }

        public static void AllowsRetainedGlideFallback(TickResult result, int entityId)
        {
            HasGenericExpansionOwnedMoveEntity(result, entityId);
            HasGenericExpansionOwnedMove(result, entityId);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        public static void AllowsRetainedGlideFallbackOnlyWhenGlideFlagOff(
            TickResult result,
            int entityId,
            bool glideFlagEnabled)
        {
            if (glideFlagEnabled)
            {
                NoGenericExpansionOrdinaryUnitMove(result, entityId);
                return;
            }

            AllowsRetainedGlideFallback(result, entityId);
        }

        public static void GridTransactionsRemainAllowed(
            TickResult result,
            int entityId,
            MovementExecutionBoundaryKind boundaryKind)
        {
            HasMoveEntityBoundary(result, entityId, boundaryKind);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        public static void GridTransactionBranchesRemainAllowed(
            TickResult result,
            int entityId,
            MovementExecutionBoundaryKind boundaryKind)
        {
            GridTransactionsRemainAllowed(result, entityId, boundaryKind);
        }

        public static void GridTransactionsRemainAllowedWithoutGenericExpansionOwned(
            TickResult result,
            int entityId,
            MovementExecutionBoundaryKind boundaryKind)
        {
            GridTransactionsRemainAllowed(result, entityId, boundaryKind);
            GenericExpansionOwnedIsOnlyForAllowedEntities(result);
        }

        public static void NoLegacyUnitPresentationForCoveredEntities(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == entityIds[i] &&
                        motion.MotionKind == TickEntityMotionKind.Move),
                    Is.False,
                    BuildDebug(result, entityIds[i]));
            }
        }

        public static void GenericExpansionOwnedIsOnlyForAllowedEntities(TickResult result, params int[] allowedEntityIds)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned &&
                    !IsExceptedEntity(operation.EntityId, allowedEntityIds)),
                Is.False,
                BuildDebug(result));
        }

        public static void NoLegacyOrdinaryMoveForEntitiesExcept(TickResult result, params int[] exceptEntityIds)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    !IsExceptedEntity(motion.EntityId, exceptEntityIds) &&
                    motion.MotionKind == TickEntityMotionKind.Move),
                Is.False,
                BuildDebug(result));
        }

        public static void HasGenericExpansionOwnedMove(TickResult result, int entityId)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    motion.MotionKind == TickEntityMotionKind.Move),
                Is.True,
                BuildDebug(result, entityId));
        }

        public static void HasGenericExpansionOwnedMoveEntity(TickResult result, int entityId)
        {
            HasMoveEntityBoundary(result, entityId, MovementExecutionBoundaryKind.GenericExpansionOwned);
        }

        public static void HasMoveEntityBoundary(
            TickResult result,
            int entityId,
            MovementExecutionBoundaryKind boundaryKind)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == boundaryKind),
                Is.True,
                BuildDebug(result, entityId));
        }

        public static void HasMoveEntityBoundaryReason(
            TickResult result,
            int entityId,
            MovementExecutionBoundaryKind boundaryKind,
            string boundaryReason)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == boundaryKind &&
                    operation.Metadata.BoundaryReason == boundaryReason),
                Is.True,
                BuildDebug(result, entityId));
        }

        public static void HasOperationBoundary(
            TickResult result,
            int entityId,
            FinalizationOperationKind operationKind,
            MovementExecutionBoundaryKind boundaryKind)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == operationKind &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == boundaryKind),
                Is.True,
                BuildDebug(result, entityId));
        }

        public static void NoUnexpectedUnknownMovementBoundary(TickResult result)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Unknown),
                Is.False,
                BuildDebug(result));
            Assert.That(result.Trace.Text, Does.Not.Contain("Boundary=Unknown"));
        }

        public static void NoUnexpectedUnknownMovementBoundaryAllowingStateOnly(TickResult result)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(IsUnexpectedUnknownMovementBoundary),
                Is.False,
                BuildDebug(result));
        }

        public static void NoFlagOnLegacyOrdinaryReadinessLeaks(TickResult result, params int[] entityIds)
        {
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
            NoGenericExpansionOrdinaryUnitMove(result, entityIds);
        }

        public static void NoGenericExpansionOrdinaryUnitMoveOperationOrDiagnostic(TickResult result, int entityId)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned &&
                    operation.Metadata.MovementSemanticKind == MovementSemanticKind.Move),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("GenericUnitOrdinaryMovementDetected", System.StringComparison.Ordinal) &&
                    reason.Contains($"E={entityId}", System.StringComparison.Ordinal)),
                Is.False,
                BuildDebug(result, entityId));
        }

        private static void NoLegacyOrdinaryUnitOperationOrPresentation(TickResult result, int entityId)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    motion.MotionKind == TickEntityMotionKind.Move),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned &&
                    operation.Metadata.MovementSemanticKind == MovementSemanticKind.Move),
                Is.False,
                BuildDebug(result, entityId));
        }

        public static void NoForcedKinematicProducer(TickResult result, params int[] entityIds)
        {
            Assert.That(
                result.PresentationData.KinematicMotionTracks.Any(track =>
                    IsTrackedEntity(track.EntityId, entityIds) &&
                    (track.MotionMode == MotionMode.Forced ||
                     track.ForcedMotionOp == ForcedMotionOp.Knockback)),
                Is.False,
                BuildDebug(result));

            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetUnitKinematicState &&
                    IsTrackedEntity(operation.EntityId, entityIds) &&
                    (operation.UnitKinematicState.mode == MotionMode.Forced ||
                     operation.UnitKinematicState.forcedOp == ForcedMotionOp.Knockback)),
                Is.False,
                BuildDebug(result));
        }

        private static bool IsUnexpectedUnknownMovementBoundary(FinalizationOperation operation)
        {
            if (operation.Metadata.MovementExecutionBoundaryKind != MovementExecutionBoundaryKind.Unknown)
            {
                return false;
            }

            return operation.Kind == FinalizationOperationKind.MoveEntity ||
                   operation.Kind == FinalizationOperationKind.SetTopology ||
                   operation.Kind == FinalizationOperationKind.SetBoardPresence ||
                   operation.Kind == FinalizationOperationKind.SpawnEntity ||
                   operation.Metadata.MovementSemanticKind != MovementSemanticKind.None ||
                   operation.Metadata.SemanticKind == ResolvedActionSemanticKind.Move ||
                   operation.Metadata.SemanticKind == ResolvedActionSemanticKind.JumpLanding ||
                   operation.Metadata.JumpPresentationKind != JumpPresentationKind.None;
        }

        private static bool IsTrackedEntity(int entityId, int[] entityIds)
        {
            return entityIds == null ||
                   entityIds.Length == 0 ||
                   entityIds.Contains(entityId);
        }

        private static bool IsExceptedEntity(int entityId, int[] entityIds)
        {
            return entityIds != null && entityIds.Contains(entityId);
        }

        private static string BuildDebug(TickResult result, int entityId)
        {
            var motions = string.Join(
                "\n",
                result.PresentationData.EntityMotions
                    .Where(motion => motion.EntityId == entityId)
                    .Select(motion => $"Motion|E={motion.EntityId}|Kind={motion.MotionKind}|From={motion.SourceCell}|To={motion.DestinationCell}"));
            var operations = string.Join(
                "\n",
                result.MovementPhaseResult.ResolvedOperations
                    .Where(operation => operation.EntityId == entityId)
                    .Select(operation => $"Op|E={operation.EntityId}|Kind={operation.Kind}|Boundary={operation.Metadata.MovementExecutionBoundaryKind}|Semantic={operation.Metadata.MovementSemanticKind}|Reason={operation.Metadata.BoundaryReason}"));
            var rejected = string.Join("\n", result.MovementPhaseResult.RejectedReasons);
            return $"Legacy movement boundary debug for E={entityId}\n{motions}\n{operations}\n{rejected}";
        }

        private static string BuildDebug(TickResult result)
        {
            var motions = string.Join(
                "\n",
                result.PresentationData.EntityMotions
                    .Select(motion => $"Motion|E={motion.EntityId}|Kind={motion.MotionKind}|From={motion.SourceCell}|To={motion.DestinationCell}"));
            var operations = string.Join(
                "\n",
                result.MovementPhaseResult.ResolvedOperations
                    .Select(operation => $"Op|E={operation.EntityId}|Kind={operation.Kind}|Boundary={operation.Metadata.MovementExecutionBoundaryKind}|Semantic={operation.Metadata.MovementSemanticKind}|Reason={operation.Metadata.BoundaryReason}"));
            var rejected = string.Join("\n", result.MovementPhaseResult.RejectedReasons);
            return $"Legacy movement boundary debug\n{motions}\n{operations}\n{rejected}";
        }
    }
}
