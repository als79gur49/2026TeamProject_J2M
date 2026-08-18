using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimeTraversalLegalityPolicy
    {
        public static LegalityResult EvaluateDestination(
            TraverseContext context,
            TileFeatureTraversalEvidence tileFeatureEvidence)
        {
            var tileFeatureDefinitions = tileFeatureEvidence.Definitions;
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            var capabilities = ModifierQuery.GetTraversalCapabilities(context.Actor);

            var topologyTileFeatureGuard = EvaluateTopologyTransitionTileFeatureGuard(context);
            if (topologyTileFeatureGuard.Verdict == LegalityVerdict.Blocked)
            {
                return topologyTileFeatureGuard;
            }

            if (!context.EvaluationTopology.IsFaceActive(context.CandidateCell.face))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    context.CandidateCell,
                    context.EvaluationTopology,
                    RuntimeLegalityBlockerFactory.CreateBoardEdge(),
                    context.ReservationStatus,
                    context.TransitionRequirement);
            }

            if (!context.Snapshot.TryGetPlacementBlocker(
                    context.EvaluationTopology,
                    context.Actor.EntityType,
                    context.CandidateCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                if (TryGetUnitTileFeatureBlocker(
                        context,
                        tileFeatureDefinitions,
                        out var tileFeatureBlocker))
                {
                    return LegalityResult.Blocked(
                        LegalityDomain.Traversal,
                        context.CandidateCell,
                        context.EvaluationTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
                        context.ReservationStatus,
                        context.TransitionRequirement);
                }

                if (context.Actor.GlideState.IsActive)
                {
                    return LegalityResult.Allowed(
                        LegalityDomain.Traversal,
                        context.CandidateCell,
                        context.EvaluationTopology,
                        context.ReservationStatus,
                        context.TransitionRequirement);
                }

                return LegalityResult.Allowed(
                    LegalityDomain.Traversal,
                    context.CandidateCell,
                    context.EvaluationTopology,
                    context.ReservationStatus,
                    context.TransitionRequirement);
            }

            var blockers = RuntimeLegalityBlockerFactory.Create(context.Snapshot.EntitiesById, blocker);
            if (ModifierQuery.IgnoresTraversalBlocker(capabilities, blockers[0]))
            {
                if (TryGetUnitTileFeatureBlocker(
                        context,
                        tileFeatureDefinitions,
                        out var tileFeatureBlocker))
                {
                    return LegalityResult.Blocked(
                        LegalityDomain.Traversal,
                        context.CandidateCell,
                        context.EvaluationTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
                        context.ReservationStatus,
                        context.TransitionRequirement);
                }

                if (context.Actor.GlideState.IsActive)
                {
                    return LegalityResult.Allowed(
                        LegalityDomain.Traversal,
                        context.CandidateCell,
                        context.EvaluationTopology,
                        context.ReservationStatus,
                        context.TransitionRequirement);
                }

                return LegalityResult.Allowed(
                    LegalityDomain.Traversal,
                    context.CandidateCell,
                    context.EvaluationTopology,
                    context.ReservationStatus,
                    context.TransitionRequirement);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Traversal,
                context.CandidateCell,
                context.EvaluationTopology,
                blockers,
                context.ReservationStatus,
                context.TransitionRequirement);
        }

        public static LegalityResult EvaluateTopologyTransitionTileFeatureGuard(
            TraverseContext context,
            TileFeatureTraversalEvidence tileFeatureEvidence)
        {
            _ = tileFeatureEvidence.Definitions;
            return EvaluateTopologyTransitionTileFeatureGuard(context);
        }

        public static LegalityResult EvaluateDestination(
            WorldSnapshot snapshot,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            CubeTopologyState evaluatedTopology,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            TileFeatureTraversalEvidence tileFeatureEvidence,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _ = tileFeatureEvidence.Definitions;

            return EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    BuildActorRef(snapshot, ignoredEntityId, entityType),
                    originCell: ignoredEntityId > 0 && snapshot.TryGetEntity(ignoredEntityId, out var actor)
                        ? actor.position
                        : default,
                    candidateCell: cell,
                    evaluationTopology: evaluatedTopology,
                    transitionRequirement: rotationKind == CubeRotationKind.None
                        ? TransitionRequirement.None
                        : TransitionRequirement.TopologyUpdate(rotationKind, updatedTopology),
                    reservationStatus: reservationStatus),
                tileFeatureEvidence);
        }

        // Charge movement ignores overlapping units and only stops on hard board blockers.
        public static LegalityResult EvaluateChargeSolidOnlyStopCell(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            TileFeatureTraversalEvidence tileFeatureEvidence,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var tileFeatureDefinitions = tileFeatureEvidence.Definitions;

            if (!snapshot.IsInsideBoard(cell))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateBoardEdge(),
                    reservationStatus);
            }

            if (snapshot.TryGetSolidSemanticAt(cell, out var solidSemantic))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    RuntimeLegalityBlockerFactory.Create(solidSemantic.Entity),
                    reservationStatus);
            }

            if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                    snapshot,
                    tileFeatureDefinitions,
                    cell,
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.GroundStep,
                    out var barricade))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateTileFeature(barricade),
                    reservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Traversal,
                cell,
                snapshot.Topology,
                reservationStatus);
        }

        private static LegalityActorRef BuildActorRef(
            WorldSnapshot snapshot,
            int entityId,
            EntityType entityType)
        {
            return StateQuery.BuildActorRef(snapshot, entityId, entityType);
        }

        private static LegalityResult EvaluateTopologyTransitionTileFeatureGuard(TraverseContext context)
        {
            if (context.TransitionRequirement.Kind == TransitionRequirementKind.TopologyUpdate &&
                TileFeatureMovementBlockerQuery.TryGetTopologyTransitionTileFeatureBlocker(
                    context.Snapshot,
                    context.CandidateCell,
                    out var topologyTransitionBlocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    context.CandidateCell,
                    context.EvaluationTopology,
                    RuntimeLegalityBlockerFactory.CreateTileFeature(topologyTransitionBlocker),
                    context.ReservationStatus,
                    context.TransitionRequirement);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Traversal,
                context.CandidateCell,
                context.EvaluationTopology,
                context.ReservationStatus,
                context.TransitionRequirement);
        }

        private static bool TryGetUnitTileFeatureBlocker(
            in TraverseContext context,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out TileFeatureState tileFeatureBlocker)
        {
            if (context.Actor.EntityType != EntityType.Unit)
            {
                tileFeatureBlocker = default;
                return false;
            }

            var movementKind = context.TransitionRequirement.Kind == TransitionRequirementKind.TopologyUpdate
                ? TileFeatureMovementKind.Free2DTopologyTransition
                : TileFeatureMovementKind.GroundStep;
            return TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                context.Snapshot,
                tileFeatureDefinitions,
                context.CandidateCell,
                TileFeatureBlockerSubject.Unit,
                movementKind,
                out tileFeatureBlocker,
                context.EvaluationTopology,
                context.Actor.EntityId);
        }
    }
}
