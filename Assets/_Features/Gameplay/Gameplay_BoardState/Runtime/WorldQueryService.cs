using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldQueryService
    {
        // Service wrappers preserve authoritative snapshot semantics; callers must not substitute render positions.
        public static bool TryGetEntityAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryGetEntityAt(entitiesById, occupancyByCell, topology, cell, out entity);
        }

        public static bool HasAnyUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell)
        {
            return SnapshotReadQueries.HasAnyUnitAt(entitiesById, stackedUnitsByCell, topology, cell);
        }

        public static void EnumerateUnitsAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateUnitsAt(entitiesById, stackedUnitsByCell, topology, cell, buffer);
        }

        public static bool TryGetPrimaryUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryGetPrimaryUnitAt(entitiesById, stackedUnitsByCell, topology, cell, out entity);
        }

        public static bool TryGetBoxAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryGetBoxAt(entitiesById, solidOccupancyByCell, topology, cell, out entity);
        }

        public static bool TryGetSolidSemanticAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out SolidSemantic semantic)
        {
            return SnapshotReadQueries.TryGetSolidSemanticAt(
                entitiesById,
                solidOccupancyByCell,
                topology,
                cell,
                out semantic);
        }

        public static bool TryGetSolidOccupantAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryGetSolidOccupantAt(entitiesById, solidOccupancyByCell, topology, cell, out entity);
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
            return SnapshotReadQueries.TryPickImpactTargetAt(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                topology,
                cell,
                sourceTeamId,
                out entity);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, SurfaceCell cell)
        {
            return boardBounds.Contains(cell.PlanarPosition);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, Vector2Int cell)
        {
            return boardBounds.Contains(cell);
        }

        public static bool TryGetUnitTraversalBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetUnitBlocker(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                topology,
                boardBounds,
                cell,
                out blocker);
        }

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetAuthoritativePlacementBlocker(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                projectileOccupancy,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetGameplayPlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetGameplayPlacementBlocker(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                projectileOccupancy,
                topology,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool CanBeTargetedForNewSelection(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            return SnapshotReadQueries.CanBeTargetedForNewSelection(entitiesById, topology, entityId);
        }

        public static void EnumerateEntitiesOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateEntitiesOrdered(entitiesById, buffer);
        }

        public static void EnumerateOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(entitiesById, occupancyByCell, topology, buffer);
        }

        public static bool TryGetUnitBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetUnitBlocker(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                topology,
                boardBounds,
                cell,
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
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                topology,
                boardBounds,
                origin,
                direction,
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
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                topology,
                boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolveUnitStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolveUnitStep(
                topology,
                boardBounds,
                origin,
                direction,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolveUnitStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolveUnitStep(
                topology,
                boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolveLocalFlipCells(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell actorCell,
            Vector2Int delta,
            out SurfaceCell targetCell,
            out SurfaceCell landingCell)
        {
            return SurfaceTraversalQueries.TryResolveLocalFlipCells(
                topology,
                boardBounds,
                actorCell,
                delta,
                out targetCell,
                out landingCell);
        }

        public static bool TryResolveNextSurfaceBoxSlideStep(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            return SurfaceSlideQueries.TryResolveNextSurfaceBoxSlideStep(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId: null,
                phasedStatesByEntityId: null,
                solidOccupancyByCell,
                topology,
                boardBounds,
                origin,
                delta,
                out destination,
                out stopper);
        }
    }
}
