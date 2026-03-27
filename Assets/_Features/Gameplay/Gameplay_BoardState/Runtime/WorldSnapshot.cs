using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldSnapshot
    {
        private readonly BoardBounds _boardBounds;
        private readonly IReadOnlyDictionary<int, EntityState> _entitiesById;
        private readonly IReadOnlyDictionary<SurfaceCell, int> _projectileOccupancy;
        private readonly TerrainData _terrainData;
        private readonly CubeTopologyState _topology;
        private readonly IReadOnlyDictionary<SurfaceCell, int> _unitOccupancy;

        internal WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            Dictionary<SurfaceCell, int> unitOccupancy,
            Dictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            _entitiesById = new ReadOnlyDictionary<int, EntityState>(entitiesById ?? throw new ArgumentNullException(nameof(entitiesById)));
            _unitOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(unitOccupancy ?? throw new ArgumentNullException(nameof(unitOccupancy)));
            _projectileOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(projectileOccupancy ?? throw new ArgumentNullException(nameof(projectileOccupancy)));
            _topology = topology;
            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
        }

        public BoardBounds BoardBounds => _boardBounds;

        public CubeTopologyState Topology => _topology;

        public bool TryGetEntity(int entityId, out EntityState entity)
        {
            return _entitiesById.TryGetValue(entityId, out entity);
        }

        public bool TryGetUnitAt(SurfaceCell cell, out EntityState entity)
        {
            return WorldQueryService.TryGetEntityAt(_entitiesById, _unitOccupancy, _topology, cell, out entity);
        }

        public bool TryGetUnitAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetUnitAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryGetProjectileAt(SurfaceCell cell, out EntityState entity)
        {
            return WorldQueryService.TryGetEntityAt(_entitiesById, _projectileOccupancy, _topology, cell, out entity);
        }

        public bool TryGetProjectileAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetProjectileAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool IsInsideBoard(SurfaceCell cell)
        {
            return WorldQueryService.IsInsideBoard(_boardBounds, cell);
        }

        public bool IsInsideBoard(Vector2Int cell)
        {
            return WorldQueryService.IsInsideBoard(_boardBounds, cell);
        }

        public bool IsTerrainBlockedForUnit(SurfaceCell cell)
        {
            return WorldQueryService.IsTerrainBlockedForUnit(_topology, _terrainData, cell);
        }

        public bool IsTerrainBlockedForUnit(Vector2Int cell)
        {
            return IsTerrainBlockedForUnit(CreateDefaultQueryCell(cell));
        }

        public bool IsBlockedForUnit(SurfaceCell cell)
        {
            return WorldQueryService.IsBlockedForUnit(_entitiesById, _unitOccupancy, _topology, _boardBounds, _terrainData, cell);
        }

        public bool IsBlockedForUnit(Vector2Int cell)
        {
            return IsBlockedForUnit(CreateDefaultQueryCell(cell));
        }

        internal bool TryGetPlacementBlocker(
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldQueryService.TryGetGameplayPlacementBlocker(
                _entitiesById,
                _unitOccupancy,
                _projectileOccupancy,
                _topology,
                _boardBounds,
                _terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        internal bool TryGetPlacementBlocker(
            EntityType entityType,
            Vector2Int cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlocker(entityType, CreateDefaultQueryCell(cell), ignoredEntityId, out blocker);
        }

        public bool TryResolvePlayerStep(
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return WorldQueryService.TryResolvePlayerStep(
                _topology,
                _boardBounds,
                origin,
                direction,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public bool TryResolvePlayerStep(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return WorldQueryService.TryResolvePlayerStep(
                _topology,
                _boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public bool TryGetSurfaceBoxSlideDestination(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            return WorldQueryService.TryGetSurfaceBoxSlideDestination(
                _entitiesById,
                _unitOccupancy,
                _topology,
                _boardBounds,
                _terrainData,
                origin,
                delta,
                out destination,
                out stopper);
        }

        public bool TryGetBoxSlideDestination(
            Vector2Int origin,
            Vector2Int delta,
            out Vector2Int destination,
            out SlideStopper stopper)
        {
            return WorldQueryService.TryGetBoxSlideDestination(
                _entitiesById,
                _unitOccupancy,
                _topology,
                _boardBounds,
                _terrainData,
                origin,
                delta,
                out destination,
                out stopper);
        }

        public bool BlocksMovement(int entityId)
        {
            return WorldQueryService.BlocksMovement(_entitiesById, _topology, entityId);
        }

        public bool CanBeTargetedForNewSelection(int entityId)
        {
            return WorldQueryService.CanBeTargetedForNewSelection(_entitiesById, _topology, entityId);
        }

        public void EnumerateEntitiesOrdered(List<EntityState> buffer)
        {
            WorldQueryService.EnumerateEntitiesOrdered(_entitiesById, buffer);
        }

        internal bool TryGetUnitBlocker(SurfaceCell cell, out SlideStopper blocker)
        {
            return WorldQueryService.TryGetUnitBlocker(_entitiesById, _unitOccupancy, _topology, _boardBounds, _terrainData, cell, out blocker);
        }

        internal bool TryGetUnitBlocker(Vector2Int cell, out SlideStopper blocker)
        {
            return TryGetUnitBlocker(CreateDefaultQueryCell(cell), out blocker);
        }

        internal void EnumerateTerrainBlockedCellsOrdered(List<Vector2Int> buffer)
        {
            WorldQueryService.EnumerateTerrainBlockedCellsOrdered(_terrainData, buffer);
        }

        internal void EnumerateUnitOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            WorldQueryService.EnumerateOccupancyOrdered(_entitiesById, _unitOccupancy, _topology, buffer);
        }

        internal void EnumerateProjectileOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            WorldQueryService.EnumerateOccupancyOrdered(_entitiesById, _projectileOccupancy, _topology, buffer);
        }

        private SurfaceCell CreateDefaultQueryCell(Vector2Int cell)
        {
            return SurfaceCell.FromPlanar(cell, _topology.BottomFace);
        }
    }
}
