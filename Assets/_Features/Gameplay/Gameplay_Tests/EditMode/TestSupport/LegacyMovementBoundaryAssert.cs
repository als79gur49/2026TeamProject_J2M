using System.Linq;
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
