using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class SnapshotReadQueries
    {
        // Snapshot queries always read committed logical occupancy. Presenter interpolation never changes these answers.
        public static bool TryGetEntityAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            ValidateQueryDictionaries(entitiesById, occupancyByCell);

            entity = default;

            if (!topology.IsFaceActive(cell.face))
            {
                return false;
            }

            return TryGetStoredOccupant(entitiesById, occupancyByCell, cell, out entity) &&
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology);
        }

        // Legacy non-projectile lookup keeps solid-first resolution so existing box/wall callers stay stable
        // until the explicit unit/solid APIs land in the next stage.
        public static bool TryGetPrimaryNonProjectileOccupantAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyList<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell, solidOccupancyByCell);

            entity = default;

            if (!topology.IsFaceActive(cell.face))
            {
                return false;
            }

            if (TryGetStoredOccupant(entitiesById, solidOccupancyByCell, cell, out entity) &&
                GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology))
            {
                return true;
            }

            return TryGetStoredStackedUnit(
                entitiesById,
                stackedUnitsByCell,
                topology,
                cell,
                requireGameplayVisibility: true,
                ignoredEntityId: 0,
                out entity);
        }

        public static bool IsTerrainBlockedForUnit(
            CubeTopologyState topology,
            TerrainData terrainData,
            SurfaceCell cell)
        {
            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            return topology.IsFaceActive(cell.face) && terrainData.BlocksUnitMovement(cell.PlanarPosition);
        }

        public static bool BlocksMovement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            return entitiesById.TryGetValue(entityId, out var entity) &&
                   entity.type != EntityType.Projectile &&
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology);
        }

        public static bool CanBeTargetedForNewSelection(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            return entitiesById.TryGetValue(entityId, out var entity) &&
                   !entity.markedForDeath &&
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology);
        }

        public static void EnumerateEntitiesOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            List<EntityState> buffer)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var entity in entitiesById.Values)
            {
                buffer.Add(entity);
            }

            buffer.Sort(EntityIdComparer.Instance);
        }

        public static void EnumerateOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            List<SnapshotOccupancyEntry> buffer)
        {
            ValidateQueryDictionaries(entitiesById, occupancyByCell);

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in occupancyByCell)
            {
                if (!topology.IsFaceActive(pair.Key.face))
                {
                    continue;
                }

                if (!entitiesById.TryGetValue(pair.Value, out var entity) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology))
                {
                    continue;
                }

                buffer.Add(new SnapshotOccupancyEntry(pair.Key, pair.Value));
            }

            buffer.Sort(OccupancyEntryComparer.Instance);
        }

        public static void EnumerateCombinedOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyList<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            List<SnapshotOccupancyEntry> buffer)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell, solidOccupancyByCell);

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in solidOccupancyByCell)
            {
                if (!topology.IsFaceActive(pair.Key.face))
                {
                    continue;
                }

                if (!entitiesById.TryGetValue(pair.Value, out var entity) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology))
                {
                    continue;
                }

                buffer.Add(new SnapshotOccupancyEntry(pair.Key, pair.Value));
            }

            foreach (var pair in stackedUnitsByCell)
            {
                if (!topology.IsFaceActive(pair.Key.face))
                {
                    continue;
                }

                var entityIds = pair.Value;
                for (var i = 0; i < entityIds.Count; i++)
                {
                    if (!entitiesById.TryGetValue(entityIds[i], out var entity) ||
                        !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology))
                    {
                        continue;
                    }

                    buffer.Add(new SnapshotOccupancyEntry(pair.Key, entity.entityId));
                }
            }

            buffer.Sort(OccupancyEntryComparer.Instance);
        }

        public static void EnumerateTerrainBlockedCellsOrdered(
            TerrainData terrainData,
            List<Vector2Int> buffer)
        {
            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            var orderedCells = terrainData.OrderedUnitBlockingCells;
            for (var i = 0; i < orderedCells.Count; i++)
            {
                buffer.Add(orderedCells[i]);
            }
        }

        private static bool TryGetStoredOccupant(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            SurfaceCell cell,
            out EntityState entity)
        {
            entity = default;

            return occupancyByCell.TryGetValue(cell, out var entityId) &&
                   entitiesById.TryGetValue(entityId, out entity);
        }

        internal static bool TryGetStoredStackedUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyList<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            bool requireGameplayVisibility,
            int ignoredEntityId,
            out EntityState entity)
        {
            entity = default;

            if (!stackedUnitsByCell.TryGetValue(cell, out var entityIds))
            {
                return false;
            }

            for (var i = 0; i < entityIds.Count; i++)
            {
                var entityId = entityIds[i];
                if (entityId == ignoredEntityId ||
                    !entitiesById.TryGetValue(entityId, out var candidate))
                {
                    continue;
                }

                if (requireGameplayVisibility
                        ? GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(candidate, topology)
                        : GameplayEntityQueryPolicy.IsEntityOccupyingBoard(candidate))
                {
                    entity = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void ValidateQueryDictionaries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (occupancyByCell == null)
            {
                throw new ArgumentNullException(nameof(occupancyByCell));
            }
        }

        private static void ValidateQueryDictionaries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyList<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }

            if (solidOccupancyByCell == null)
            {
                throw new ArgumentNullException(nameof(solidOccupancyByCell));
            }
        }

        private sealed class EntityIdComparer : IComparer<EntityState>
        {
            internal static readonly EntityIdComparer Instance = new();

            public int Compare(EntityState left, EntityState right)
            {
                return left.entityId.CompareTo(right.entityId);
            }
        }

        private sealed class OccupancyEntryComparer : IComparer<SnapshotOccupancyEntry>
        {
            internal static readonly OccupancyEntryComparer Instance = new();

            public int Compare(SnapshotOccupancyEntry left, SnapshotOccupancyEntry right)
            {
                var result = left.Cell.face.CompareTo(right.Cell.face);
                if (result != 0)
                {
                    return result;
                }

                result = left.Cell.x.CompareTo(right.Cell.x);
                if (result != 0)
                {
                    return result;
                }

                result = left.Cell.y.CompareTo(right.Cell.y);
                if (result != 0)
                {
                    return result;
                }

                return left.EntityId.CompareTo(right.EntityId);
            }
        }
    }
}
