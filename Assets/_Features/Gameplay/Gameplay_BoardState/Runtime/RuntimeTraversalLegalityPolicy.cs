using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimeTraversalLegalityPolicy
    {
        public static LegalityResult EvaluateDestination(TraverseContext context)
        {
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            var capabilities = ModifierQuery.GetTraversalCapabilities(context.Actor);

            if (!context.Snapshot.TryGetPlacementBlocker(
                    context.EvaluationTopology,
                    context.Actor.EntityType,
                    context.CandidateCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                if (TryGetUnitTileFeatureBlocker(context, out var tileFeatureBlocker))
                {
                    return LegalityResult.Blocked(
                        LegalityDomain.Traversal,
                        context.CandidateCell,
                        context.EvaluationTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
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
                if (TryGetUnitTileFeatureBlocker(context, out var tileFeatureBlocker))
                {
                    return LegalityResult.Blocked(
                        LegalityDomain.Traversal,
                        context.CandidateCell,
                        context.EvaluationTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
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

        public static LegalityResult EvaluateDestination(
            WorldSnapshot snapshot,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            CubeTopologyState evaluatedTopology,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            ReservationStatus reservationStatus = ReservationStatus.None,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

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
                    reservationStatus: reservationStatus,
                    tileFeatureDefinitions: tileFeatureDefinitions));
        }

        // Charge movement ignores overlapping units and only stops on hard board blockers.
        public static LegalityResult EvaluateChargeSolidOnlyStopCell(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.IsInsideBoard(cell))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateBoardEdge(),
                    reservationStatus);
            }

            if (snapshot.IsTerrainBlockedForUnit(cell))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    RuntimeLegalityBlockerFactory.CreateTerrain(TerrainFlags.BlocksGroundTraversal),
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

        private static bool TryGetUnitTileFeatureBlocker(
            in TraverseContext context,
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
                context.TileFeatureDefinitions,
                context.CandidateCell,
                TileFeatureBlockerSubject.Unit,
                movementKind,
                out tileFeatureBlocker);
        }
    }
}
