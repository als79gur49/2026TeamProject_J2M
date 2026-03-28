using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldQueryService
    {
        private static readonly IReadOnlyDictionary<SurfaceCell, int> EmptyOccupancy = new Dictionary<SurfaceCell, int>();

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
                   ShouldEntityParticipateInActiveQueries(entity, topology);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, SurfaceCell cell)
        {
            return boardBounds.Contains(cell.PlanarPosition);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, Vector2Int cell)
        {
            return boardBounds.Contains(cell);
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

        public static bool IsBlockedForUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell cell)
        {
            return TryGetUnitBlocker(entitiesById, unitOccupancy, topology, boardBounds, terrainData, cell, out _);
        }

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                unitOccupancy,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                default,
                PlacementQueryMode.Authoritative,
                out blocker);
        }

        public static bool TryGetGameplayPlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                unitOccupancy,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                topology,
                PlacementQueryMode.Gameplay,
                out blocker);
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
                   ShouldEntityParticipateInActiveQueries(entity, topology);
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
                   ShouldEntityParticipateInActiveQueries(entity, topology);
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
                    !ShouldEntityParticipateInActiveQueries(entity, topology))
                {
                    continue;
                }

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
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            if (!topology.IsFaceActive(cell.face))
            {
                blocker = default;
                return false;
            }

            return TryGetGameplayPlacementBlocker(
                entitiesById,
                unitOccupancy,
                EmptyOccupancy,
                topology,
                boardBounds,
                terrainData,
                EntityType.Unit,
                cell,
                ignoredEntityId: 0,
                out blocker);
        }

        public static bool TryResolvePlayerStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return TryResolvePlayerStep(
                topology,
                boardBounds,
                origin,
                DirectionToDelta(direction),
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolvePlayerStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            ValidateSlideDelta(delta);

            destination = default;
            rotationKind = CubeRotationKind.None;
            updatedTopology = topology;

            if (!topology.IsFaceActive(origin.face))
            {
                return false;
            }

            if (TryResolveBottomFaceRotation(topology, boardBounds, origin, delta, out destination, out rotationKind, out updatedTopology))
            {
                return true;
            }

            var candidate = origin + delta;
            if (!IsInsideBoard(boardBounds, candidate))
            {
                return false;
            }

            destination = candidate;
            return true;
        }

        public static bool TryResolveLocalFlipCells(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell actorCell,
            Vector2Int delta,
            out SurfaceCell targetCell,
            out SurfaceCell landingCell)
        {
            ValidateSlideDelta(delta);

            targetCell = default;
            landingCell = default;

            if (!topology.IsFaceActive(actorCell.face))
            {
                return false;
            }

            var adjacentTarget = actorCell + delta;
            var oppositeLanding = actorCell - delta;
            if (adjacentTarget.face != actorCell.face ||
                oppositeLanding.face != actorCell.face ||
                !IsInsideBoard(boardBounds, adjacentTarget) ||
                !IsInsideBoard(boardBounds, oppositeLanding))
            {
                return false;
            }

            targetCell = adjacentTarget;
            landingCell = oppositeLanding;
            return true;
        }

        public static bool TryGetSurfaceBoxSlideDestination(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            ValidateQueryDictionaries(entitiesById, unitOccupancy);

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            ValidateSlideDelta(delta);

            if (!topology.IsFaceActive(origin.face))
            {
                destination = default;
                stopper = default;
                return false;
            }

            if (!boardBounds.IsBounded)
            {
                return TryGetUnboundedSurfaceBoxSlideDestination(
                    entitiesById,
                    unitOccupancy,
                    topology,
                    terrainData,
                    origin,
                    delta,
                    out destination,
                    out stopper);
            }

            if (!IsInsideBoard(boardBounds, origin))
            {
                throw new InvalidOperationException(
                    $"Slide origin {origin} must be inside the configured board bounds.");
            }

            var current = origin;

            while (true)
            {
                if (!TryGetNextSurfaceBoxSlideCell(topology, boardBounds, current, delta, out var next, out stopper))
                {
                    destination = current;
                    return true;
                }

                if (terrainData.BlocksUnitMovement(next.PlanarPosition))
                {
                    destination = current;
                    stopper = SlideStopper.CreateTerrain(next);
                    return true;
                }

                if (TryGetEntityAt(entitiesById, unitOccupancy, topology, next, out var entity) &&
                    entity.type != EntityType.Projectile)
                {
                    destination = current;
                    stopper = SlideStopper.CreateEntity(entity);
                    return true;
                }

                current = next;
            }
        }

        public static bool TryGetBoxSlideDestination(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            Vector2Int origin,
            Vector2Int delta,
            out Vector2Int destination,
            out SlideStopper stopper)
        {
            if (TryGetLegacyPlanarBoxSlideDestination(
                    entitiesById,
                    unitOccupancy,
                    topology,
                    boardBounds,
                    terrainData,
                    CreateDefaultQueryCell(topology, origin),
                    delta,
                    out var surfaceDestination,
                    out stopper))
            {
                destination = surfaceDestination.PlanarPosition;
                return true;
            }

            destination = default;
            return false;
        }

        private static bool TryGetUnboundedSurfaceBoxSlideDestination(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            TerrainData terrainData,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            var bestDistance = int.MaxValue;
            stopper = default;

            var terrainCells = terrainData.OrderedUnitBlockingCells;
            for (var i = 0; i < terrainCells.Count; i++)
            {
                if (!IsOnPositiveRay(terrainCells[i] - origin.PlanarPosition, delta, out var distance) ||
                    distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                stopper = SlideStopper.CreateTerrain(new SurfaceCell(origin.face, terrainCells[i].x, terrainCells[i].y));
            }

            foreach (var pair in unitOccupancy)
            {
                if (pair.Key.face != origin.face ||
                    !IsOnPositiveRay(pair.Key.PlanarPosition - origin.PlanarPosition, delta, out var distance) ||
                    distance >= bestDistance)
                {
                    continue;
                }

                if (!entitiesById.TryGetValue(pair.Value, out var entity) ||
                    entity.type == EntityType.Projectile ||
                    !ShouldEntityParticipateInActiveQueries(entity, topology))
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

        private static bool TryGetLegacyPlanarBoxSlideDestination(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            ValidateQueryDictionaries(entitiesById, unitOccupancy);

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            ValidateSlideDelta(delta);

            var bestDistance = int.MaxValue;
            stopper = default;

            if (boardBounds.IsBounded)
            {
                var boardEdgeDistance = GetBoardEdgeDistance(boardBounds, origin.PlanarPosition, delta);
                bestDistance = boardEdgeDistance;
                stopper = SlideStopper.CreateBoardEdge(Offset(origin, delta, boardEdgeDistance));
            }

            var terrainCells = terrainData.OrderedUnitBlockingCells;
            for (var i = 0; i < terrainCells.Count; i++)
            {
                if (!IsOnPositiveRay(terrainCells[i] - origin.PlanarPosition, delta, out var distance) ||
                    distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                stopper = SlideStopper.CreateTerrain(new SurfaceCell(origin.face, terrainCells[i].x, terrainCells[i].y));
            }

            foreach (var pair in unitOccupancy)
            {
                if (pair.Key.face != origin.face ||
                    !IsOnPositiveRay(pair.Key.PlanarPosition - origin.PlanarPosition, delta, out var distance) ||
                    distance >= bestDistance)
                {
                    continue;
                }

                if (!entitiesById.TryGetValue(pair.Value, out var entity) ||
                    entity.type == EntityType.Projectile ||
                    !ShouldEntityParticipateInActiveQueries(entity, topology))
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

        private static bool TryResolveBottomFaceRotation(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            destination = default;
            rotationKind = CubeRotationKind.None;
            updatedTopology = topology;

            if (!boardBounds.IsBounded || origin.face != topology.BottomFace)
            {
                return false;
            }

            if (delta == Vector2Int.up && origin.y == boardBounds.MaxInclusive.y)
            {
                rotationKind = CubeRotationKind.Forward;
                updatedTopology = topology.Rotate(rotationKind);
                destination = new SurfaceCell(updatedTopology.BottomFace, origin.x, boardBounds.MinInclusive.y);
                return true;
            }

            if (delta == Vector2Int.down && origin.y == boardBounds.MinInclusive.y)
            {
                rotationKind = CubeRotationKind.Backward;
                updatedTopology = topology.Rotate(rotationKind);
                destination = new SurfaceCell(updatedTopology.BottomFace, origin.x, boardBounds.MaxInclusive.y);
                return true;
            }

            return false;
        }

        internal static bool TryGetNextSurfaceBoxSlideCell(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell current,
            Vector2Int delta,
            out SurfaceCell next,
            out SlideStopper stopper)
        {
            if (current.face == topology.BottomFace &&
                delta == Vector2Int.up &&
                current.y == boardBounds.MaxInclusive.y)
            {
                next = new SurfaceCell(topology.FrontFace, current.x, boardBounds.MinInclusive.y);
                stopper = default;
                return true;
            }

            if (current.face == topology.FrontFace &&
                delta == Vector2Int.down &&
                current.y == boardBounds.MinInclusive.y)
            {
                next = new SurfaceCell(topology.BottomFace, current.x, boardBounds.MaxInclusive.y);
                stopper = default;
                return true;
            }

            next = current + delta;
            if (IsInsideBoard(boardBounds, next))
            {
                stopper = default;
                return true;
            }

            stopper = SlideStopper.CreateBoardEdge(next);
            next = default;
            return false;
        }

        private static SurfaceCell CreateDefaultQueryCell(CubeTopologyState topology, Vector2Int cell)
        {
            return SurfaceCell.FromPlanar(cell, topology.BottomFace);
        }

        private static Vector2Int DirectionToDelta(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Right => Vector2Int.right,
                Direction.Down => Vector2Int.down,
                Direction.Left => Vector2Int.left,
                _ => throw new InvalidOperationException("Surface step queries require a cardinal direction."),
            };
        }

        private static bool TryGetBlockingPlacementEntity(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out EntityState entity)
        {
            if (entityType == EntityType.Projectile)
            {
                if (TryGetPlacementOccupant(
                        entitiesById,
                        projectileOccupancy,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity))
                {
                    return true;
                }

                if (TryGetPlacementOccupant(
                        entitiesById,
                        unitOccupancy,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity))
                {
                    return true;
                }
            }
            else if (TryGetPlacementOccupant(
                         entitiesById,
                         unitOccupancy,
                         topology,
                         queryMode,
                         cell,
                         ignoredEntityId,
                         out entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        private static bool TryGetPlacementOccupant(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            SurfaceCell cell,
            int ignoredEntityId,
            out EntityState entity)
        {
            if (TryGetStoredOccupant(entitiesById, occupancyByCell, cell, out entity) &&
                entity.entityId != ignoredEntityId &&
                IsBlockingPlacementEntity(entity, topology, queryMode))
            {
                return true;
            }

            entity = default;
            return false;
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

        private static bool TerrainBlocksPlacement(
            EntityType entityType,
            TerrainData terrainData,
            Vector2Int cell)
        {
            switch (entityType)
            {
                case EntityType.None:
                case EntityType.Unit:
                case EntityType.Projectile:
                case EntityType.Box:
                    return terrainData.BlocksUnitMovement(cell);

                default:
                    throw new InvalidOperationException($"Unsupported placement entity type: {entityType}");
            }
        }

        private static bool IsEntityOnActiveFace(EntityState entity, CubeTopologyState topology)
        {
            return topology.IsFaceActive(entity.position.face);
        }

        private static bool IsEntityOccupyingBoard(EntityState entity)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying;
        }

        private static bool ShouldEntityParticipateInActiveQueries(EntityState entity, CubeTopologyState topology)
        {
            return IsEntityOccupyingBoard(entity) && IsEntityOnActiveFace(entity, topology);
        }

        private static bool IsBlockingPlacementEntity(
            EntityState entity,
            CubeTopologyState topology,
            PlacementQueryMode queryMode)
        {
            return queryMode == PlacementQueryMode.Gameplay
                ? ShouldEntityParticipateInActiveQueries(entity, topology)
                : IsEntityOccupyingBoard(entity);
        }

        private static bool TryGetPlacementBlockerCore(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            out SlideStopper blocker)
        {
            ValidateQueryDictionaries(entitiesById, unitOccupancy);

            if (projectileOccupancy == null)
            {
                throw new ArgumentNullException(nameof(projectileOccupancy));
            }

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            if (queryMode == PlacementQueryMode.Gameplay && !topology.IsFaceActive(cell.face))
            {
                blocker = default;
                return false;
            }

            if (!IsInsideBoard(boardBounds, cell))
            {
                blocker = SlideStopper.CreateBoardEdge(cell);
                return true;
            }

            if (TerrainBlocksPlacement(entityType, terrainData, cell.PlanarPosition))
            {
                blocker = SlideStopper.CreateTerrain(cell);
                return true;
            }

            if (TryGetBlockingPlacementEntity(
                    entitiesById,
                    unitOccupancy,
                    projectileOccupancy,
                    topology,
                    queryMode,
                    entityType,
                    cell,
                    ignoredEntityId,
                    out var blockingEntity))
            {
                blocker = SlideStopper.CreateEntity(blockingEntity);
                return true;
            }

            blocker = default;
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

        private static SurfaceCell Offset(SurfaceCell origin, Vector2Int delta, int distance)
        {
            return new SurfaceCell(origin.face, origin.x + (delta.x * distance), origin.y + (delta.y * distance));
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

        private enum PlacementQueryMode
        {
            Authoritative = 0,
            Gameplay = 1,
        }
    }
}
