using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldQueryService
    {
        public static bool TryGetEntityAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<Vector2Int, int> occupancyByCell,
            Vector2Int cell,
            out EntityState entity)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (occupancyByCell == null)
            {
                throw new ArgumentNullException(nameof(occupancyByCell));
            }

            entity = default;

            return occupancyByCell.TryGetValue(cell, out var entityId) &&
                   entitiesById.TryGetValue(entityId, out entity);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, Vector2Int cell)
        {
            return boardBounds.Contains(cell);
        }

        public static bool IsTerrainBlockedForUnit(TerrainData terrainData, Vector2Int cell)
        {
            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            return terrainData.BlocksUnitMovement(cell);
        }

        public static bool IsBlockedForUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<Vector2Int, int> unitOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            Vector2Int cell)
        {
            return TryGetUnitBlocker(entitiesById, unitOccupancy, boardBounds, terrainData, cell, out _);
        }

        public static bool BlocksMovement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            int entityId)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            return entitiesById.TryGetValue(entityId, out var entity) && entity.type != EntityType.Projectile;
        }

        public static bool CanBeTargetedForNewSelection(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            int entityId)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            return entitiesById.TryGetValue(entityId, out var entity) && !entity.markedForDeath;
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
            IReadOnlyDictionary<Vector2Int, int> occupancyByCell,
            List<SnapshotOccupancyEntry> buffer)
        {
            if (occupancyByCell == null)
            {
                throw new ArgumentNullException(nameof(occupancyByCell));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in occupancyByCell)
            {
                buffer.Add(new SnapshotOccupancyEntry(pair.Key, pair.Value));
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

        public static bool TryGetUnitBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<Vector2Int, int> unitOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            Vector2Int cell,
            out SlideStopper blocker)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (unitOccupancy == null)
            {
                throw new ArgumentNullException(nameof(unitOccupancy));
            }

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            if (!IsInsideBoard(boardBounds, cell))
            {
                blocker = SlideStopper.CreateBoardEdge(cell);
                return true;
            }

            if (terrainData.BlocksUnitMovement(cell))
            {
                blocker = SlideStopper.CreateTerrain(cell);
                return true;
            }

            if (TryGetBlockingEntityAt(entitiesById, unitOccupancy, cell, out var blockingEntity))
            {
                blocker = SlideStopper.CreateEntity(blockingEntity);
                return true;
            }

            blocker = default;
            return false;
        }

        public static bool TryGetBoxSlideDestination(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<Vector2Int, int> unitOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            Vector2Int origin,
            Vector2Int delta,
            out Vector2Int destination,
            out SlideStopper stopper)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (unitOccupancy == null)
            {
                throw new ArgumentNullException(nameof(unitOccupancy));
            }

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            ValidateSlideDelta(delta);

            var bestDistance = int.MaxValue;
            stopper = default;

            if (boardBounds.IsBounded)
            {
                var boardEdgeDistance = GetBoardEdgeDistance(boardBounds, origin, delta);
                bestDistance = boardEdgeDistance;
                stopper = SlideStopper.CreateBoardEdge(Offset(origin, delta, boardEdgeDistance));
            }

            var terrainCells = terrainData.OrderedUnitBlockingCells;
            for (var i = 0; i < terrainCells.Count; i++)
            {
                if (!IsOnPositiveRay(terrainCells[i] - origin, delta, out var distance))
                {
                    continue;
                }

                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                stopper = SlideStopper.CreateTerrain(terrainCells[i]);
            }

            foreach (var pair in unitOccupancy)
            {
                if (!IsOnPositiveRay(pair.Key - origin, delta, out var distance) || distance >= bestDistance)
                {
                    continue;
                }

                if (!entitiesById.TryGetValue(pair.Value, out var entity) || entity.type == EntityType.Projectile)
                {
                    continue;
                }

                bestDistance = distance;
                stopper = SlideStopper.CreateEntity(entity);
            }

            if (bestDistance == int.MaxValue)
            {
                destination = default;
                stopper = default;
                return false;
            }

            destination = Offset(origin, delta, bestDistance - 1);
            return true;
        }

        private static int GetBoardEdgeDistance(BoardBounds boardBounds, Vector2Int origin, Vector2Int delta)
        {
            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException("Board edge distance requires bounded board data.");
            }

            if (!boardBounds.Contains(origin))
            {
                throw new InvalidOperationException(
                    $"Slide origin ({origin.x},{origin.y}) must be inside the configured board bounds.");
            }

            if (delta == Vector2Int.right)
            {
                return boardBounds.MaxInclusive.x - origin.x + 1;
            }

            if (delta == Vector2Int.left)
            {
                return origin.x - boardBounds.MinInclusive.x + 1;
            }

            if (delta == Vector2Int.up)
            {
                return boardBounds.MaxInclusive.y - origin.y + 1;
            }

            if (delta == Vector2Int.down)
            {
                return origin.y - boardBounds.MinInclusive.y + 1;
            }

            throw new InvalidOperationException("Slide queries require an orthogonal single-cell direction.");
        }

        private static bool IsOnPositiveRay(Vector2Int offset, Vector2Int delta, out int distance)
        {
            distance = 0;

            if (delta.x != 0)
            {
                if (offset.y != 0 || offset.x == 0 || Math.Sign(offset.x) != Math.Sign(delta.x))
                {
                    return false;
                }

                distance = Math.Abs(offset.x);
                return true;
            }

            if (offset.x != 0 || offset.y == 0 || Math.Sign(offset.y) != Math.Sign(delta.y))
            {
                return false;
            }

            distance = Math.Abs(offset.y);
            return true;
        }

        private static Vector2Int Offset(Vector2Int origin, Vector2Int delta, int distance)
        {
            return new Vector2Int(origin.x + (delta.x * distance), origin.y + (delta.y * distance));
        }

        private static bool TryGetBlockingEntityAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<Vector2Int, int> unitOccupancy,
            Vector2Int cell,
            out EntityState entity)
        {
            if (TryGetEntityAt(entitiesById, unitOccupancy, cell, out entity) && entity.type != EntityType.Projectile)
            {
                return true;
            }

            entity = default;
            return false;
        }

        private static void ValidateSlideDelta(Vector2Int delta)
        {
            if ((delta.x == 0 && delta.y == 0) || Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                throw new InvalidOperationException("Slide queries require an orthogonal single-cell direction.");
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
                var result = left.Cell.x.CompareTo(right.Cell.x);
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
