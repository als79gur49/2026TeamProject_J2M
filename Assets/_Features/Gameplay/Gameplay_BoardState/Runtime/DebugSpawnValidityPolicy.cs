using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class DebugSpawnValidityPolicy
    {
        public static void EnsureRepresentable(
            BoardBounds boardBounds,
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

                if (WorldPlacementPolicy.TryGetRepresentablePlacementBlocker(
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
                ReserveEntityOccupancy(entity, stackedUnitsByCell, solidOccupancyByCell);
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

        private static void ReserveEntityOccupancy(
            EntityState entity,
            IDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IDictionary<SurfaceCell, int> solidOccupancyByCell)
        {
            if (entity.boardPresence != EntityBoardPresence.Occupying)
            {
                return;
            }

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
                    solidOccupancyByCell[entity.position] = entity.entityId;
                    break;

                case EntityType.Projectile:
                    throw new NotSupportedException(
                        "EntityType.Projectile is reserved for legacy serialized values and cannot be used as a debug spawn.");
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
