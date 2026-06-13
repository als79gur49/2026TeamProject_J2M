using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldPlacementPolicy
    {
        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetAuthoritativePlacementBlocker(
                entitiesById,
                CreateStackedUnitQueryView(stackedUnitsByCell),
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetAuthoritativePlacementBlocker(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId: null,
                phasedStatesByEntityId: null,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                default,
                PlacementQueryMode.Authoritative,
                PlacementGlideQueryMode.Normal,
                out blocker);
        }

        public static bool TryGetGameplayPlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetGameplayPlacementBlocker(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId: null,
                phasedStatesByEntityId: null,
                solidOccupancyByCell,
                topology,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetGameplayPlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                topology,
                PlacementQueryMode.Gameplay,
                PlacementGlideQueryMode.Normal,
                out blocker);
        }

        public static bool TryGetBoxSlidePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId,
                solidOccupancyByCell,
                boardBounds,
                EntityType.Box,
                cell,
                ignoredEntityId: 0,
                topology,
                PlacementQueryMode.Gameplay,
                PlacementGlideQueryMode.BoxSlide,
                out blocker);
        }

        public static bool TryGetBoxFlipPlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId,
                solidOccupancyByCell,
                boardBounds,
                EntityType.Box,
                cell,
                ignoredEntityId: 0,
                topology,
                PlacementQueryMode.Gameplay,
                PlacementGlideQueryMode.BoxFlip,
                out blocker);
        }

        public static bool TryGetRepresentablePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetRepresentablePlacementBlocker(
                entitiesById,
                CreateStackedUnitQueryView(stackedUnitsByCell),
                enemyGlideStatesByEntityId: null,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetRepresentablePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId: null,
                phasedStatesByEntityId: null,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                default,
                PlacementQueryMode.Representable,
                PlacementGlideQueryMode.Normal,
                out blocker);
        }

        public static bool TryGetRepresentablePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetRepresentablePlacementBlocker(
                entitiesById,
                CreateStackedUnitQueryView(stackedUnitsByCell),
                enemyGlideStatesByEntityId,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetRepresentablePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId: null,
                solidOccupancyByCell,
                boardBounds,
                entityType,
                cell,
                ignoredEntityId,
                default,
                PlacementQueryMode.Representable,
                PlacementGlideQueryMode.Normal,
                out blocker);
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
            return TryGetUnitBlocker(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId: null,
                phasedStatesByEntityId: null,
                solidOccupancyByCell,
                topology,
                boardBounds,
                cell,
                out blocker);
        }

        public static bool TryGetUnitBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
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
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId,
                solidOccupancyByCell,
                topology,
                boardBounds,
                EntityType.Unit,
                cell,
                ignoredEntityId: 0,
                out blocker);
        }

        private static bool TryGetPlacementBlockerCore(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            BoardBounds boardBounds,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            PlacementGlideQueryMode glideQueryMode,
            out SlideStopper blocker)
        {
            ValidateQueryDictionaries(entitiesById, solidOccupancyByCell);
            EnsureEntityTypeSupported(entityType);

            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }

            if (queryMode == PlacementQueryMode.Gameplay && !topology.IsFaceActive(cell.face))
            {
                blocker = default;
                return false;
            }

            if (!boardBounds.Contains(cell.PlanarPosition))
            {
                blocker = SlideStopper.CreateBoardEdge(cell);
                return true;
            }

            if (TryGetBlockingPlacementEntity(
                    entitiesById,
                    stackedUnitsByCell,
                    enemyJumpStatesByEntityId,
                    enemyGlideStatesByEntityId,
                    phasedStatesByEntityId,
                    solidOccupancyByCell,
                    topology,
                    queryMode,
                    entityType,
                    cell,
                    ignoredEntityId,
                    glideQueryMode,
                    out var blockingEntity))
            {
                blocker = SlideStopper.CreateEntity(blockingEntity);
                return true;
            }

            blocker = default;
            return false;
        }

        private static bool TryGetBlockingPlacementEntity(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            PlacementGlideQueryMode glideQueryMode,
            out EntityState entity)
        {
            switch (entityType)
            {
                case EntityType.Unit:
                    return TryGetPlacementOccupant(
                        entitiesById,
                        solidOccupancyByCell,
                        enemyJumpStatesByEntityId,
                        enemyGlideStatesByEntityId,
                        phasedStatesByEntityId,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        entityType,
                        glideQueryMode,
                        out entity);

                case EntityType.Box:
                case EntityType.None:
                    if (TryGetPlacementOccupant(
                            entitiesById,
                            solidOccupancyByCell,
                            enemyJumpStatesByEntityId,
                            enemyGlideStatesByEntityId,
                            phasedStatesByEntityId,
                            topology,
                            queryMode,
                            cell,
                            ignoredEntityId,
                            entityType,
                            glideQueryMode,
                            out entity))
                    {
                        return true;
                    }

                    return TryGetPlacementStackedUnit(
                        entitiesById,
                        stackedUnitsByCell,
                        enemyJumpStatesByEntityId,
                        enemyGlideStatesByEntityId,
                        phasedStatesByEntityId,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        glideQueryMode,
                        out entity);

                default:
                    throw new InvalidOperationException($"Unsupported placement entity type: {entityType}");
            }
        }

        private static void EnsureEntityTypeSupported(EntityType entityType)
        {
            if (entityType == EntityType.Projectile)
            {
                throw new NotSupportedException(
                    "EntityType.Projectile is reserved for legacy serialized values and cannot be placed at runtime.");
            }
        }

        private static IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> CreateStackedUnitQueryView(
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell)
        {
            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }

            var view = new Dictionary<SurfaceCell, IReadOnlyCollection<int>>(stackedUnitsByCell.Count);
            foreach (var pair in stackedUnitsByCell)
            {
                view.Add(pair.Key, pair.Value);
            }

            return view;
        }

        private static bool TryGetPlacementOccupant(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            SurfaceCell cell,
            int ignoredEntityId,
            EntityType movingEntityType,
            PlacementGlideQueryMode glideQueryMode,
            out EntityState entity)
        {
            if (TryGetStoredOccupant(entitiesById, occupancyByCell, cell, out entity) &&
                entity.entityId != ignoredEntityId &&
                !ShouldAllowActiveGliderSolidOverlap(
                    enemyGlideStatesByEntityId,
                    movingEntityType,
                    ignoredEntityId) &&
                GameplayEntityQueryPolicy.IsBlockingPlacementEntity(
                    ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology),
                    requireGameplayVisibility: queryMode == PlacementQueryMode.Gameplay))
            {
                return true;
            }

            entity = default;
            return false;
        }

        private static bool TryGetPlacementStackedUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            SurfaceCell cell,
            int ignoredEntityId,
            PlacementGlideQueryMode glideQueryMode,
            out EntityState entity)
        {
            if (!stackedUnitsByCell.TryGetValue(cell, out var entityIds))
            {
                entity = default;
                return false;
            }

            foreach (var entityId in entityIds)
            {
                if (entityId == ignoredEntityId ||
                    !entitiesById.TryGetValue(entityId, out var candidate) ||
                    ShouldIgnoreStackedUnitForGlide(
                        enemyGlideStatesByEntityId,
                        candidate.entityId,
                        queryMode,
                        glideQueryMode))
                {
                    continue;
                }

                var spatialState = ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, candidate, topology);
                if (GameplayEntityQueryPolicy.IsBlockingPlacementEntity(
                        spatialState,
                        requireGameplayVisibility: queryMode == PlacementQueryMode.Gameplay))
                {
                    entity = candidate;
                    return true;
                }
            }

            entity = default;
            return false;
        }

        private static bool ShouldAllowActiveGliderSolidOverlap(
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            EntityType movingEntityType,
            int ignoredEntityId)
        {
            if (movingEntityType != EntityType.Unit ||
                ignoredEntityId <= 0 ||
                enemyGlideStatesByEntityId == null ||
                !enemyGlideStatesByEntityId.TryGetValue(ignoredEntityId, out var glideState))
            {
                return false;
            }

            if (glideState.IsActive)
            {
                return true;
            }

            return false;
        }

        private static bool ShouldIgnoreStackedUnitForGlide(
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            int entityId,
            PlacementQueryMode queryMode,
            PlacementGlideQueryMode glideQueryMode)
        {
            if (enemyGlideStatesByEntityId == null ||
                !enemyGlideStatesByEntityId.TryGetValue(entityId, out var glideState) ||
                !glideState.IsActive)
            {
                return false;
            }

            return glideQueryMode == PlacementGlideQueryMode.BoxSlide ||
                   glideQueryMode == PlacementGlideQueryMode.BoxFlip ||
                   queryMode == PlacementQueryMode.Representable;
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

        private static ResolvedSpatialState ResolveSpatialState(
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            in EntityState entity,
            CubeTopologyState topology)
        {
            var jumpState = default(EnemyJumpRuntimeState);
            var hasJumpState = enemyJumpStatesByEntityId != null &&
                               enemyJumpStatesByEntityId.TryGetValue(entity.entityId, out jumpState);
            var phasedState = default(PhasedRuntimeState);
            var hasPhasedState = phasedStatesByEntityId != null &&
                                 phasedStatesByEntityId.TryGetValue(entity.entityId, out phasedState);
            return SpatialStateResolver.Resolve(
                entity,
                topology,
                hasJumpState ? jumpState : (EnemyJumpRuntimeState?)null,
                hasPhasedState ? phasedState : (PhasedRuntimeState?)null);
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

        private enum PlacementQueryMode
        {
            Authoritative = 0,
            Gameplay = 1,
            Representable = 2,
        }

        private enum PlacementGlideQueryMode
        {
            Normal = 0,
            BoxSlide = 1,
            BoxFlip = 2,
        }
    }
}
