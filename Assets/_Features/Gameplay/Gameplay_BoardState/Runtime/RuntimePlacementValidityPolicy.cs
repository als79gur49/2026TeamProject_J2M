using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimePlacementValidityPolicy
    {
        public static LegalityResult EvaluateGameplayPlacement(
            WorldSnapshot snapshot,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            CubeTopologyState? evaluatedTopology = null,
            ReservationStatus reservationStatus = ReservationStatus.None,
            TransitionRequirement transitionRequirement = default)
        {
            if (snapshot == null)
            {
                throw new System.ArgumentNullException(nameof(snapshot));
            }

            var topology = evaluatedTopology ?? snapshot.Topology;
            if (!snapshot.TryGetPlacementBlocker(
                    topology,
                    entityType,
                    cell,
                    ignoredEntityId,
                    out var blocker))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Placement,
                    cell,
                    topology,
                    reservationStatus,
                    transitionRequirement);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Placement,
                cell,
                topology,
                RuntimeLegalityBlockerFactory.Create(snapshot.EntitiesById, blocker),
                reservationStatus,
                transitionRequirement);
        }

        public static LegalityResult EvaluateAuthoritativePlacement(
            WorldSnapshot snapshot,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (snapshot == null)
            {
                throw new System.ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.TryGetAuthoritativePlacementBlocker(
                    entityType,
                    cell,
                    ignoredEntityId,
                    out var blocker))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Placement,
                    cell,
                    snapshot.Topology,
                    reservationStatus);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Placement,
                cell,
                snapshot.Topology,
                RuntimeLegalityBlockerFactory.Create(snapshot.EntitiesById, blocker),
                reservationStatus);
        }

        public static LegalityResult EvaluateAuthoritativePlacement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            if (!WorldPlacementPolicy.TryGetAuthoritativePlacementBlocker(
                    entitiesById,
                    stackedUnitsByCell,
                    solidOccupancyByCell,
                    projectileOccupancy,
                    boardBounds,
                    entityType,
                    cell,
                    ignoredEntityId,
                    out blocker))
            {
                return LegalityResult.Allowed(
                    LegalityDomain.Placement,
                    cell,
                    default);
            }

            return LegalityResult.Blocked(
                LegalityDomain.Placement,
                cell,
                default,
                RuntimeLegalityBlockerFactory.Create(entitiesById, blocker));
        }
    }
}
