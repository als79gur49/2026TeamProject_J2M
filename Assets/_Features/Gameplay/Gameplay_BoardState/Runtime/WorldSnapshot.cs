using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    // Read-only view over committed gameplay state. Entity positions and occupancy are the only gameplay coordinates.
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

        // Cell queries always resolve against committed authoritative occupancy, not render-time motion tracks.
        public bool TryGetUnitAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetUnitAt(_topology, cell, out entity);
        }

        public bool TryGetUnitAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetUnitAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryGetProjectileAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetProjectileAt(_topology, cell, out entity);
        }

        public bool TryGetProjectileAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetProjectileAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool IsInsideBoard(SurfaceCell cell)
        {
            return _boardBounds.Contains(cell.PlanarPosition);
        }

        public bool IsInsideBoard(Vector2Int cell)
        {
            return _boardBounds.Contains(cell);
        }

        public bool IsTerrainBlockedForUnit(SurfaceCell cell)
        {
            return SnapshotReadQueries.IsTerrainBlockedForUnit(_topology, _terrainData, cell);
        }

        public bool IsTerrainBlockedForUnit(Vector2Int cell)
        {
            return IsTerrainBlockedForUnit(CreateDefaultQueryCell(cell));
        }

        public bool IsBlockedForUnit(SurfaceCell cell)
        {
            return WorldPlacementPolicy.IsBlockedForUnit(_entitiesById, _unitOccupancy, _topology, _boardBounds, _terrainData, cell);
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
            return TryGetPlacementBlocker(_topology, entityType, cell, ignoredEntityId, out blocker);
        }

        internal bool TryGetPlacementBlocker(
            CubeTopologyState topology,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetGameplayPlacementBlocker(
                _entitiesById,
                _unitOccupancy,
                _projectileOccupancy,
                topology,
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
            return SurfaceTraversalQueries.TryResolvePlayerStep(
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
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                _topology,
                _boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        internal bool TryResolveLocalFlipCells(
            SurfaceCell actorCell,
            Vector2Int delta,
            out SurfaceCell targetCell,
            out SurfaceCell landingCell)
        {
            return SurfaceTraversalQueries.TryResolveLocalFlipCells(
                _topology,
                _boardBounds,
                actorCell,
                delta,
                out targetCell,
                out landingCell);
        }

        public bool TryResolveNextSurfaceBoxSlideStep(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            return TryResolveNextSurfaceBoxSlideStep(_topology, origin, delta, out destination, out stopper);
        }

        internal bool TryResolveNextSurfaceBoxSlideStep(
            CubeTopologyState topology,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            return SurfaceSlideQueries.TryResolveNextSurfaceBoxSlideStep(
                _entitiesById,
                _unitOccupancy,
                topology,
                _boardBounds,
                _terrainData,
                origin,
                delta,
                out destination,
                out stopper);
        }

        public bool BlocksMovement(int entityId)
        {
            return SnapshotReadQueries.BlocksMovement(_entitiesById, _topology, entityId);
        }

        public bool CanBeTargetedForNewSelection(int entityId)
        {
            return SnapshotReadQueries.CanBeTargetedForNewSelection(_entitiesById, _topology, entityId);
        }

        public void EnumerateEntitiesOrdered(List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateEntitiesOrdered(_entitiesById, buffer);
        }

        internal bool TryGetUnitBlocker(SurfaceCell cell, out SlideStopper blocker)
        {
            return TryGetUnitBlocker(_topology, cell, out blocker);
        }

        internal bool TryGetUnitBlocker(
            CubeTopologyState topology,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetUnitBlocker(
                _entitiesById,
                _unitOccupancy,
                topology,
                _boardBounds,
                _terrainData,
                cell,
                out blocker);
        }

        internal bool TryGetUnitBlocker(Vector2Int cell, out SlideStopper blocker)
        {
            return TryGetUnitBlocker(CreateDefaultQueryCell(cell), out blocker);
        }

        internal void EnumerateTerrainBlockedCellsOrdered(List<Vector2Int> buffer)
        {
            SnapshotReadQueries.EnumerateTerrainBlockedCellsOrdered(_terrainData, buffer);
        }

        internal void EnumerateUnitOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(_entitiesById, _unitOccupancy, _topology, buffer);
        }

        internal void EnumerateProjectileOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(_entitiesById, _projectileOccupancy, _topology, buffer);
        }

        internal bool TryGetUnitAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetEntityAt(_entitiesById, _unitOccupancy, topology, cell, out entity);
        }

        internal bool TryGetProjectileAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetEntityAt(_entitiesById, _projectileOccupancy, topology, cell, out entity);
        }

        private SurfaceCell CreateDefaultQueryCell(Vector2Int cell)
        {
            return SurfaceCell.FromPlanar(cell, _topology.BottomFace);
        }
    }
}
