using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimeTraversalLegalityPolicy
    {
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

            var transitionRequirement = rotationKind == CubeRotationKind.None
                ? TransitionRequirement.None
                : TransitionRequirement.TopologyUpdate(rotationKind, updatedTopology);

            if (!snapshot.TryGetPlacementBlocker(
                    evaluatedTopology,
                    entityType,
                    cell,
                    ignoredEntityId,
                    out var blocker))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Traversal,
                    cell,
                    evaluatedTopology,
                    reservationStatus,
                    transitionRequirement);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Traversal,
                cell,
                evaluatedTopology,
                RuntimeLegalityBlockerFactory.Create(snapshot.EntitiesById, blocker),
                reservationStatus,
                transitionRequirement);
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
                    new[] { new LegalityBlocker(LegalityBlockerKind.BoardEdge) },
                    reservationStatus);
            }

            if (snapshot.IsTerrainBlockedForUnit(cell))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Traversal,
                    cell,
                    snapshot.Topology,
                    new[]
                    {
                        new LegalityBlocker(
                            LegalityBlockerKind.Terrain,
                            terrainFlags: TerrainFlags.BlocksGroundTraversal),
                    },
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
                if (occupant.boardPresence != EntityBoardPresence.Occupying ||
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
    }
}
