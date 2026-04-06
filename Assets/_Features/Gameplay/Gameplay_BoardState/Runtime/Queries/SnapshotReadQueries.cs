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

        public static bool HasAnyUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell);

            return topology.IsFaceActive(cell.face) &&
                   TryGetStoredStackedUnit(
                       entitiesById,
                       stackedUnitsByCell,
                       topology,
                       cell,
                       requireGameplayVisibility: true,
                       ignoredEntityId: 0,
                       out _);
        }

        public static void EnumerateUnitsAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            List<EntityState> buffer)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell);

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            if (!topology.IsFaceActive(cell.face) ||
                !stackedUnitsByCell.TryGetValue(cell, out var entityIds))
            {
                return;
            }

            foreach (var entityId in entityIds)
            {
                if (!entitiesById.TryGetValue(entityId, out var entity) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology))
                {
                    continue;
                }

                buffer.Add(entity);
            }
        }

        public static bool TryGetPrimaryUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell);

            entity = default;

            if (!topology.IsFaceActive(cell.face))
            {
                return false;
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

        public static bool TryGetSolidOccupantAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return TryGetEntityAt(entitiesById, solidOccupancyByCell, topology, cell, out entity);
        }

        public static bool TryGetBoxAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            if (TryGetSolidOccupantAt(entitiesById, solidOccupancyByCell, topology, cell, out entity) &&
                entity.type == EntityType.Box)
            {
                return true;
            }

            entity = default;
            return false;
        }

        public static bool TryPickImpactTargetAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            out EntityState entity)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell, solidOccupancyByCell);

            entity = default;

            if (!topology.IsFaceActive(cell.face))
            {
                return false;
            }

            if (TryGetSolidOccupantAt(entitiesById, solidOccupancyByCell, topology, cell, out entity))
            {
                return true;
            }

            if (!stackedUnitsByCell.TryGetValue(cell, out var entityIds))
            {
                return false;
            }

            var hasHostile = false;
            var hostile = default(EntityState);
            var hasFallback = false;
            var fallback = default(EntityState);

            foreach (var entityId in entityIds)
            {
                if (!entitiesById.TryGetValue(entityId, out var candidate) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(candidate, topology))
                {
                    continue;
                }

                if (candidate.teamId != sourceTeamId)
                {
                    if (!hasHostile || candidate.entityId < hostile.entityId)
                    {
                        hostile = candidate;
                        hasHostile = true;
                    }

                    continue;
                }

                if (!hasFallback || candidate.entityId < fallback.entityId)
                {
                    fallback = candidate;
                    hasFallback = true;
                }
            }

            if (hasHostile)
            {
                entity = hostile;
                return true;
            }

            if (!hasFallback)
            {
                return false;
            }

            entity = fallback;
            return true;
        }

        // Legacy non-projectile lookup keeps solid-first resolution so existing box/wall callers stay stable.
        // Prefer explicit unit/solid/box/impact queries in new code.
        public static bool TryGetPrimaryNonProjectileOccupantAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
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
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
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

            AppendStackedUnitOccupancyEntries(
                entitiesById,
                stackedUnitsByCell,
                topology,
                buffer);

            buffer.Sort(OccupancyEntryComparer.Instance);
        }

        public static void EnumerateStackedUnitOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            List<SnapshotOccupancyEntry> buffer)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell);

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            AppendStackedUnitOccupancyEntries(
                entitiesById,
                stackedUnitsByCell,
                topology,
                buffer);

            buffer.Sort(OccupancyEntryComparer.Instance);
        }

        private static void AppendStackedUnitOccupancyEntries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            List<SnapshotOccupancyEntry> buffer)
        {
            foreach (var pair in stackedUnitsByCell)
            {
                if (!topology.IsFaceActive(pair.Key.face))
                {
                    continue;
                }

                foreach (var entityId in pair.Value)
                {
                    if (!entitiesById.TryGetValue(entityId, out var entity) ||
                        !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology))
                    {
                        continue;
                    }

                    buffer.Add(new SnapshotOccupancyEntry(pair.Key, entity.entityId));
                }
            }
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
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
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

            foreach (var entityId in entityIds)
            {
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
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }
        }

        private static void ValidateQueryDictionaries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
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
