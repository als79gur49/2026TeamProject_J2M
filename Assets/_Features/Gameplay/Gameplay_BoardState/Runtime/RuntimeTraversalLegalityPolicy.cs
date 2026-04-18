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
            ReservationStatus reservationStatus = ReservationStatus.None)
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
                    reservationStatus: reservationStatus));
        }

        public static LegalityResult EvaluateChargeStopCell(
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

            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (!snapshot.TryGetResolvedSpatialState(occupant.entityId, out var spatialState) ||
                    !ModifierQuery.ShouldParticipateInTraversalBlocking(spatialState) ||
                    occupant.hp <= 0 ||
                    occupant.markedForDeath)
                {
                    continue;
                }

                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    RuntimeLegalityBlockerFactory.Create(occupant),
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
    }
}
