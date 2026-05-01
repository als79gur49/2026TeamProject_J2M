using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests
{
    internal static class LegacyMovementBoundaryAssert
    {
        public static void NoLegacyOrdinaryUnitMove(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                NoLegacyOrdinaryUnitMove(result, entityIds[i]);
            }
        }

        public static void NoLegacyOrdinaryUnitMove(TickResult result, int entityId)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    (motion.MotionKind == TickEntityMotionKind.Move ||
                     motion.MotionKind == TickEntityMotionKind.ChargeMove)),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LegacyFallback &&
                    operation.Metadata.MovementSemanticKind == MovementSemanticKind.Move),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", System.StringComparison.Ordinal) &&
                    reason.Contains($"E={entityId}", System.StringComparison.Ordinal)),
                Is.False,
                BuildDebug(result, entityId));
        }

        public static void NoUnexpectedLegacyOrdinaryDiagnostics(TickResult result)
        {
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", System.StringComparison.Ordinal)),
                Is.False,
                BuildDebug(result));
        }

        public static void NoCoveredLocomotionLegacyFallback(TickResult result, params int[] entityIds)
        {
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
            NoLegacyOrdinaryUnitMove(result, entityIds);
        }

        public static void NoCoveredFallbackInDefaultGameplayLocomotion(TickResult result, params int[] entityIds)
        {
            NoCoveredLocomotionLegacyFallback(result, entityIds);
        }

        public static void NoPlayerLegacyOrdinaryFallback(TickResult result, int playerEntityId)
        {
            NoLegacyOrdinaryUnitMove(result, playerEntityId);
        }

        public static void AllowsPlayerFlagOffLegacyOrdinaryFallback(TickResult result, int playerEntityId)
        {
            AllowsOnlyFlagOffCoveredFallback(result, playerEntityId);
        }

        public static void NoEnemyLegacyOrdinaryFallback(TickResult result, int enemyEntityId)
        {
            NoLegacyOrdinaryUnitMove(result, enemyEntityId);
        }

        public static void AllowsEnemyFlagOffLegacyOrdinaryFallback(TickResult result, int enemyEntityId)
        {
            AllowsOnlyFlagOffCoveredFallback(result, enemyEntityId);
        }

        public static void NoChargeActiveLegacyFallback(TickResult result, int chargeEntityId)
        {
            NoLegacyOrdinaryUnitMove(result, chargeEntityId);
        }

        public static void AllowsChargeFlagOffLegacyFallback(TickResult result, int chargeEntityId)
        {
            AllowsOnlyFlagOffCoveredFallback(result, chargeEntityId, chargeMove: true);
        }

        public static void AllowsRetainedGlideFallback(TickResult result, int entityId)
        {
            HasLegacyFallbackMoveEntity(result, entityId);
            HasLegacyFallbackMove(result, entityId);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        public static void AllowsRetainedGlideFallbackOnlyWhenGlideFlagOff(
            TickResult result,
            int entityId,
            bool glideFlagEnabled)
        {
            if (glideFlagEnabled)
            {
                NoLegacyOrdinaryUnitMove(result, entityId);
                return;
            }

            AllowsRetainedGlideFallback(result, entityId);
        }

        public static void AllowsFlagOffLegacyFallback(TickResult result, int entityId, bool chargeMove = false)
        {
            HasLegacyFallbackMoveEntity(result, entityId);
            if (chargeMove)
            {
                HasLegacyChargeMove(result, entityId);
            }
            else
            {
                HasLegacyFallbackMove(result, entityId);
            }
        }

        public static void AllowsOnlyFlagOffCoveredFallback(TickResult result, int entityId, bool chargeMove = false)
        {
            AllowsFlagOffLegacyFallback(result, entityId, chargeMove);
            NoUnexpectedLegacyOrdinaryDiagnostics(result);
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

        public static void NoLegacyUnitPresentationForCoveredEntities(TickResult result, params int[] entityIds)
        {
            for (var i = 0; i < entityIds.Length; i++)
            {
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == entityIds[i] &&
                        (motion.MotionKind == TickEntityMotionKind.Move ||
                         motion.MotionKind == TickEntityMotionKind.ChargeMove)),
                    Is.False,
                    BuildDebug(result, entityIds[i]));
            }
        }

        public static void LegacyFallbackIsOnlyForAllowedEntities(TickResult result, params int[] allowedEntityIds)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LegacyFallback &&
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

        public static void NoLegacyChargeMoveForEntitiesExcept(TickResult result, params int[] exceptEntityIds)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    !IsExceptedEntity(motion.EntityId, exceptEntityIds) &&
                    motion.MotionKind == TickEntityMotionKind.ChargeMove),
                Is.False,
                BuildDebug(result));
        }

        public static void HasLegacyFallbackMove(TickResult result, int entityId)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    motion.MotionKind == TickEntityMotionKind.Move),
                Is.True,
                BuildDebug(result, entityId));
        }

        public static void HasLegacyChargeMove(TickResult result, int entityId)
        {
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    motion.MotionKind == TickEntityMotionKind.ChargeMove),
                Is.True,
                BuildDebug(result, entityId));
        }

        public static void HasLegacyFallbackMoveEntity(TickResult result, int entityId)
        {
            HasMoveEntityBoundary(result, entityId, MovementExecutionBoundaryKind.LegacyFallback);
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
            NoLegacyOrdinaryUnitMove(result, entityIds);
        }

        public static void NoLegacyOrdinaryUnitMoveOperationOrDiagnostic(TickResult result, int entityId)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LegacyFallback &&
                    operation.Metadata.MovementSemanticKind == MovementSemanticKind.Move),
                Is.False,
                BuildDebug(result, entityId));

            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", System.StringComparison.Ordinal) &&
                    reason.Contains($"E={entityId}", System.StringComparison.Ordinal)),
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
