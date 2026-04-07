using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    // Read-only view over committed gameplay state. Entity positions and occupancy are the only gameplay coordinates.
    public sealed class WorldSnapshot
    {
        private readonly BoardBounds _boardBounds;
        private readonly IReadOnlyDictionary<int, EnemyActionRuntimeState> _enemyActionStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyJumpRuntimeState> _enemyJumpStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EntityState> _entitiesById;
        private readonly IReadOnlyDictionary<int, PlayerControlState> _playerControlStatesByEntityId;
        private readonly IReadOnlyDictionary<SurfaceCell, int> _projectileOccupancy;
        private readonly IReadOnlyDictionary<SurfaceCell, int> _solidOccupancy;
        private readonly IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> _stackedUnitsByCell;
        private readonly TerrainData _terrainData;
        private readonly CubeTopologyState _topology;

        internal WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            Dictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            Dictionary<SurfaceCell, int> solidOccupancy,
            Dictionary<SurfaceCell, int> projectileOccupancy,
            Dictionary<int, EnemyActionRuntimeState> enemyActionStatesByEntityId,
            Dictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            Dictionary<int, PlayerControlState> playerControlStatesByEntityId,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            _entitiesById = new ReadOnlyDictionary<int, EntityState>(entitiesById ?? throw new ArgumentNullException(nameof(entitiesById)));
            _stackedUnitsByCell = CreateReadonlyStackedUnitsByCell(stackedUnitsByCell ?? throw new ArgumentNullException(nameof(stackedUnitsByCell)));
            _solidOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(solidOccupancy ?? throw new ArgumentNullException(nameof(solidOccupancy)));
            _projectileOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(projectileOccupancy ?? throw new ArgumentNullException(nameof(projectileOccupancy)));
            _enemyActionStatesByEntityId = new ReadOnlyDictionary<int, EnemyActionRuntimeState>(enemyActionStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyActionStatesByEntityId)));
            _enemyJumpStatesByEntityId = new ReadOnlyDictionary<int, EnemyJumpRuntimeState>(enemyJumpStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyJumpStatesByEntityId)));
            _playerControlStatesByEntityId = new ReadOnlyDictionary<int, PlayerControlState>(playerControlStatesByEntityId ?? throw new ArgumentNullException(nameof(playerControlStatesByEntityId)));
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

        public bool TryGetPlayerControlState(int entityId, out PlayerControlState state)
        {
            return _playerControlStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyActionState(int entityId, out EnemyActionRuntimeState state)
        {
            return _enemyActionStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyJumpState(int entityId, out EnemyJumpRuntimeState state)
        {
            return _enemyJumpStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool HasAnyUnitAt(SurfaceCell cell)
        {
            return HasAnyUnitAt(_topology, cell);
        }

        public bool HasAnyUnitAt(Vector2Int cell)
        {
            return HasAnyUnitAt(CreateDefaultQueryCell(cell));
        }

        public void EnumerateUnitsAt(SurfaceCell cell, List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateUnitsAt(_entitiesById, _stackedUnitsByCell, _topology, cell, buffer);
        }

        public void EnumerateUnitsAt(Vector2Int cell, List<EntityState> buffer)
        {
            EnumerateUnitsAt(CreateDefaultQueryCell(cell), buffer);
        }

        public bool TryGetPrimaryUnitAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetPrimaryUnitAt(_topology, cell, out entity);
        }

        public bool TryGetPrimaryUnitAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetPrimaryUnitAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryGetBoxAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetBoxAt(_topology, cell, out entity);
        }

        public bool TryGetBoxAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetBoxAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryGetSolidOccupantAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetSolidOccupantAt(_topology, cell, out entity);
        }

        public bool TryGetSolidOccupantAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetSolidOccupantAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryPickImpactTargetAt(SurfaceCell cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickImpactTargetAt(_topology, cell, sourceTeamId, out entity);
        }

        public bool TryPickImpactTargetAt(Vector2Int cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickImpactTargetAt(CreateDefaultQueryCell(cell), sourceTeamId, out entity);
        }

        // Cell queries always resolve against committed authoritative occupancy, not render-time motion tracks.
        // Legacy compatibility API: this returns the primary non-projectile occupant, not "unit only".
        // Prefer TryGetPrimaryUnitAt/TryGetBoxAt/TryGetSolidOccupantAt/TryPickImpactTargetAt in new code.
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
            return WorldPlacementPolicy.IsBlockedForUnit(
                _entitiesById,
                _stackedUnitsByCell,
                _solidOccupancy,
                _topology,
                _boardBounds,
                _terrainData,
                cell);
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
                _stackedUnitsByCell,
                _solidOccupancy,
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

        public bool TryResolveUnitStep(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolveUnitStep(
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
                _stackedUnitsByCell,
                _solidOccupancy,
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
                _stackedUnitsByCell,
                _solidOccupancy,
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
            SnapshotReadQueries.EnumerateStackedUnitOccupancyOrdered(
                _entitiesById,
                _stackedUnitsByCell,
                _topology,
                buffer);
        }

        internal void EnumerateSolidOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(_entitiesById, _solidOccupancy, _topology, buffer);
        }

        internal void EnumeratePlayerControlStatesOrdered(List<PlayerControlSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _playerControlStatesByEntityId)
            {
                buffer.Add(new PlayerControlSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyActionStatesOrdered(List<EnemyActionSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyActionStatesByEntityId)
            {
                buffer.Add(new EnemyActionSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyJumpStatesOrdered(List<EnemyJumpSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyJumpStatesByEntityId)
            {
                buffer.Add(new EnemyJumpSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateProjectileOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(_entitiesById, _projectileOccupancy, _topology, buffer);
        }

        internal bool HasAnyUnitAt(CubeTopologyState topology, SurfaceCell cell)
        {
            return SnapshotReadQueries.HasAnyUnitAt(_entitiesById, _stackedUnitsByCell, topology, cell);
        }

        internal bool TryGetPrimaryUnitAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetPrimaryUnitAt(_entitiesById, _stackedUnitsByCell, topology, cell, out entity);
        }

        internal bool TryGetBoxAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetBoxAt(_entitiesById, _solidOccupancy, topology, cell, out entity);
        }

        internal bool TryGetSolidOccupantAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetSolidOccupantAt(_entitiesById, _solidOccupancy, topology, cell, out entity);
        }

        internal bool TryPickImpactTargetAt(
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryPickImpactTargetAt(
                _entitiesById,
                _stackedUnitsByCell,
                _solidOccupancy,
                topology,
                cell,
                sourceTeamId,
                out entity);
        }

        internal bool TryGetUnitAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetPrimaryNonProjectileOccupantAt(
                _entitiesById,
                _stackedUnitsByCell,
                _solidOccupancy,
                topology,
                cell,
                out entity);
        }

        internal bool TryGetProjectileAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetEntityAt(_entitiesById, _projectileOccupancy, topology, cell, out entity);
        }

        private SurfaceCell CreateDefaultQueryCell(Vector2Int cell)
        {
            return SurfaceCell.FromPlanar(cell, _topology.BottomFace);
        }

        private static ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> CreateReadonlyStackedUnitsByCell(
            Dictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell)
        {
            var buffer = new Dictionary<SurfaceCell, IReadOnlyCollection<int>>(stackedUnitsByCell.Count);

            foreach (var pair in stackedUnitsByCell)
            {
                if (pair.Value == null)
                {
                    throw new ArgumentNullException(nameof(stackedUnitsByCell));
                }

                var orderedEntityIds = new List<int>(pair.Value.Count);
                foreach (var entityId in pair.Value)
                {
                    orderedEntityIds.Add(entityId);
                }

                buffer.Add(pair.Key, orderedEntityIds.AsReadOnly());
            }

            return new ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>>(buffer);
        }
    }
}
