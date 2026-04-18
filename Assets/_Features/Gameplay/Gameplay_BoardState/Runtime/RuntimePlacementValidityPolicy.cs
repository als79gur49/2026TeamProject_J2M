using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimePlacementValidityPolicy
    {
        public static LegalityResult EvaluateAuthoritativePlacement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
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
                    terrainData,
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

            return new LegalityResult(
                LegalityDomain.Placement,
                LegalityVerdict.Blocked,
                cell,
                default,
                ReservationStatus.None,
                new[]
                {
                    CreateBlockerFact(entitiesById, blocker),
                });
        }

        private static LegalityBlocker CreateBlockerFact(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            SlideStopper blocker)
        {
            switch (blocker.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return new LegalityBlocker(LegalityBlockerKind.BoardEdge);

                case SlideStopperKind.Terrain:
                    return new LegalityBlocker(
                        LegalityBlockerKind.Terrain,
                        terrainFlags: TerrainFlags.BlocksGroundTraversal);

                case SlideStopperKind.Entity:
                    if (blocker.EntityId != 0 &&
                        entitiesById != null &&
                        entitiesById.TryGetValue(blocker.EntityId, out var entity))
                    {
                        if (entity.type == EntityType.Unit)
                        {
                            return new LegalityBlocker(LegalityBlockerKind.Unit, entity.entityId);
                        }

                        return new LegalityBlocker(
                            LegalityBlockerKind.Solid,
                            entity.entityId,
                            SnapshotReadQueries.ResolveSolidKind(entity));
                    }

                    return new LegalityBlocker(LegalityBlockerKind.Solid, blocker.EntityId);

                default:
                    return new LegalityBlocker(LegalityBlockerKind.Solid, blocker.EntityId);
            }
        }
    }
}
