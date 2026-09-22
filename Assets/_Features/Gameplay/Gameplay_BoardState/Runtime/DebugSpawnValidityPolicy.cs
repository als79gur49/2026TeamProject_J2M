using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class DebugSpawnValidityPolicy
    {
        public static void EnsureRepresentable(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            IEnumerable<EntityState> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            var entitiesById = new Dictionary<int, EntityState>();
            var stackedUnitsByCell = new Dictionary<SurfaceCell, SortedSet<int>>();
            var solidOccupancyByCell = new Dictionary<SurfaceCell, int>();

            foreach (var entity in entities)
            {
                EnsureRepresentable(boardBounds, entity);

                if (entitiesById.ContainsKey(entity.entityId))
                {
                    throw new InvalidOperationException(
                        $"Debug spawns must use unique entity ids. Duplicate entity id {entity.entityId}.");
                }

                EnsureSupportedEntityType(entity.type);

                var spatialState = SpatialStateResolver.Resolve(
                    entity,
                    topology,
                    jumpState: null);
                var claimsAuthoritativeOccupancy = spatialState.ClaimsAuthoritativeOccupancy;

                if (claimsAuthoritativeOccupancy &&
                    WorldPlacementPolicy.TryGetRepresentablePlacementBlocker(
                        entitiesById,
                        stackedUnitsByCell,
                        solidOccupancyByCell,
                        boardBounds,
                        entity.type,
                        entity.position,
                        ignoredEntityId: 0,
                        out var blocker))
                {
                    throw new InvalidOperationException(
                        $"Debug spawn entity {entity.entityId} is not representable at {entity.position}: {FormatBlocker(blocker)}.");
                }

                entitiesById.Add(entity.entityId, entity);
                if (claimsAuthoritativeOccupancy)
                {
                    ReserveClaimedEntityOccupancy(entity, stackedUnitsByCell, solidOccupancyByCell);
                }
            }
        }

        public static void EnsureRepresentable(BoardBounds boardBounds, EntityState entity)
        {
            if (entity.entityId <= 0)
            {
                throw new InvalidOperationException("Debug spawns must use a positive entity id.");
            }

            if (boardBounds.IsBounded && !boardBounds.Contains(entity.position.PlanarPosition))
            {
                throw new InvalidOperationException(
                    $"Debug spawn entity {entity.entityId} is outside the configured board bounds at {entity.position}.");
            }
        }

        private static void ReserveClaimedEntityOccupancy(
            EntityState entity,
            IDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IDictionary<SurfaceCell, int> solidOccupancyByCell)
        {
            switch (entity.type)
            {
                case EntityType.Unit:
                    if (!stackedUnitsByCell.TryGetValue(entity.position, out var occupants))
                    {
                        occupants = new SortedSet<int>();
                        stackedUnitsByCell[entity.position] = occupants;
                    }

                    occupants.Add(entity.entityId);
                    break;

                case EntityType.Box:
                case EntityType.None:
                case EntityType.Wall:
                    solidOccupancyByCell[entity.position] = entity.entityId;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(entity.type),
                        entity.type,
                        "Unsupported debug spawn entity type.");
            }
        }

        private static void EnsureSupportedEntityType(EntityType entityType)
        {
            switch (entityType)
            {
                case EntityType.None:
                case EntityType.Unit:
                case EntityType.Box:
                case EntityType.Wall:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(entityType),
                        entityType,
                        "Unsupported debug spawn entity type.");
            }
        }

        private static string FormatBlocker(SlideStopper blocker)
        {
            if (blocker.Kind != SlideStopperKind.Entity)
            {
                return blocker.Kind.ToString();
            }

            return blocker.EntityId > 0
                ? $"{blocker.Kind}|BlockerEntity={blocker.EntityId}|BlockerType={blocker.EntityType}"
                : blocker.Kind.ToString();
        }
    }
}
