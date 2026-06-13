using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
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
            return TryGetEntityAt(
                entitiesById,
                occupancyByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                out entity);
        }

        public static bool TryGetEntityAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                       ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology));
        }

        public static bool HasAnyUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell)
        {
            return HasAnyUnitAt(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell);
        }

        public static bool HasAnyUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            CubeTopologyState topology,
            SurfaceCell cell)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell);

            return topology.IsFaceActive(cell.face) &&
                   TryGetStoredStackedUnit(
                       entitiesById,
                       stackedUnitsByCell,
                       enemyJumpStatesByEntityId,
                       phasedStatesByEntityId,
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
            EnumerateUnitsAt(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                buffer);
        }

        public static void EnumerateUnitsAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                        ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology)))
                {
                    continue;
                }

                buffer.Add(entity);
            }
        }

        public static void EnumerateUnitImpactTargetsAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                    !GameplayEntityQueryPolicy.ShouldParticipateInTargetSelection(
                        ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology)))
                {
                    continue;
                }

                buffer.Add(entity);
            }

            buffer.Sort((left, right) => left.entityId.CompareTo(right.entityId));
        }

        public static bool TryGetPrimaryUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return TryGetPrimaryUnitAt(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                out entity);
        }

        public static bool TryGetPrimaryUnitAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                enemyJumpStatesByEntityId,
                phasedStatesByEntityId,
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

        public static bool TryGetSolidSemanticAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out SolidSemantic semantic)
        {
            semantic = default;

            if (!TryGetSolidOccupantAt(entitiesById, solidOccupancyByCell, topology, cell, out var entity))
            {
                return false;
            }

            semantic = new SolidSemantic(entity, ResolveSolidKind(entity));
            return true;
        }

        public static bool IsWallAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell)
        {
            return TryGetSolidSemanticAt(entitiesById, solidOccupancyByCell, topology, cell, out var semantic) &&
                   semantic.Kind == SolidKind.Wall;
        }

        public static bool IsBoxAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell)
        {
            return TryGetSolidSemanticAt(entitiesById, solidOccupancyByCell, topology, cell, out var semantic) &&
                   semantic.Kind == SolidKind.Box;
        }

        public static bool TryGetBoxAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            if (TryGetSolidSemanticAt(entitiesById, solidOccupancyByCell, topology, cell, out var semantic) &&
                semantic.Kind == SolidKind.Box)
            {
                entity = semantic.Entity;
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
            return TryPickImpactTargetAt(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                sourceTeamId,
                out entity);
        }

        public static bool TryPickImpactTargetAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                    !GameplayEntityQueryPolicy.ShouldParticipateInTargetSelection(
                        ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, candidate, topology)))
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

        public static bool TryPickHostileUnitImpactTargetAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            out EntityState entity)
        {
            return TryPickHostileUnitImpactTargetAt(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                enemyJumpStatesByEntityId: null,
                enemyGlideStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                sourceTeamId,
                skipActiveGlideTargets: false,
                allowGlideTargetsOverSolid: false,
                out entity);
        }

        public static bool TryPickHostileUnitImpactTargetAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            bool skipActiveGlideTargets,
            bool allowGlideTargetsOverSolid,
            out EntityState entity)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell, solidOccupancyByCell);

            entity = default;

            if (!topology.IsFaceActive(cell.face) ||
                sourceTeamId <= 0 ||
                !stackedUnitsByCell.TryGetValue(cell, out var entityIds))
            {
                return false;
            }

            var hasSolidOccupant = TryGetSolidOccupantAt(entitiesById, solidOccupancyByCell, topology, cell, out _);
            if (hasSolidOccupant && !allowGlideTargetsOverSolid)
            {
                return false;
            }

            var hasHostile = false;
            var hostile = default(EntityState);

            foreach (var entityId in entityIds)
            {
                if (!entitiesById.TryGetValue(entityId, out var candidate) ||
                    (skipActiveGlideTargets && IsActiveGlideTarget(enemyGlideStatesByEntityId, candidate.entityId)) ||
                    (hasSolidOccupant && !IsGlideTargetOverSolid(enemyGlideStatesByEntityId, candidate.entityId)) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInTargetSelection(
                        ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, candidate, topology)) ||
                    candidate.teamId == sourceTeamId)
                {
                    continue;
                }

                if (!hasHostile || candidate.entityId < hostile.entityId)
                {
                    hostile = candidate;
                    hasHostile = true;
                }
            }

            if (!hasHostile)
            {
                return false;
            }

            entity = hostile;
            return true;
        }

        private static bool IsActiveGlideTarget(
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            int entityId)
        {
            return enemyGlideStatesByEntityId != null &&
                   enemyGlideStatesByEntityId.TryGetValue(entityId, out var state) &&
                   state.IsActive;
        }

        private static bool IsGlideTargetOverSolid(
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            int entityId)
        {
            return enemyGlideStatesByEntityId != null &&
                   enemyGlideStatesByEntityId.TryGetValue(entityId, out var state) &&
                   state.IsActive;
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
            return TryGetPrimaryNonProjectileOccupantAt(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                out entity);
        }

        public static bool TryGetPrimaryNonProjectileOccupantAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                    ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology)))
            {
                return true;
            }

            return TryGetStoredStackedUnit(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                phasedStatesByEntityId,
                topology,
                cell,
                requireGameplayVisibility: true,
                ignoredEntityId: 0,
                out entity);
        }

        public static bool BlocksMovement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            return BlocksMovement(entitiesById, enemyJumpStatesByEntityId: null, phasedStatesByEntityId: null, topology, entityId);
        }

        public static bool BlocksMovement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            CubeTopologyState topology,
            int entityId)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            return entitiesById.TryGetValue(entityId, out var entity) &&
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                       ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology));
        }

        public static bool CanBeTargetedForNewSelection(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            return CanBeTargetedForNewSelection(
                entitiesById,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                entityId);
        }

        public static bool CanBeTargetedForNewSelection(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            CubeTopologyState topology,
            int entityId)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            return entitiesById.TryGetValue(entityId, out var entity) &&
                   !entity.markedForDeath &&
                   GameplayEntityQueryPolicy.ShouldParticipateInTargetSelection(
                       ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology));
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
            EnumerateOccupancyOrdered(
                entitiesById,
                occupancyByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                buffer);
        }

        public static void EnumerateOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                        ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology)))
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
            EnumerateCombinedOccupancyOrdered(
                entitiesById,
                stackedUnitsByCell,
                solidOccupancyByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                buffer);
        }

        public static void EnumerateCombinedOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                        ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology)))
                {
                    continue;
                }

                buffer.Add(new SnapshotOccupancyEntry(pair.Key, pair.Value));
            }

            AppendStackedUnitOccupancyEntries(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                phasedStatesByEntityId,
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
            EnumerateStackedUnitOccupancyOrdered(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                buffer);
        }

        public static void EnumerateStackedUnitOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                enemyJumpStatesByEntityId,
                phasedStatesByEntityId,
                topology,
                buffer);

            buffer.Sort(OccupancyEntryComparer.Instance);
        }

        private static void AppendStackedUnitOccupancyEntries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                        !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                            ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, entity, topology)))
                    {
                        continue;
                    }

                    buffer.Add(new SnapshotOccupancyEntry(pair.Key, entity.entityId));
                }
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
            return TryGetStoredStackedUnit(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId: null,
                phasedStatesByEntityId: null,
                topology,
                cell,
                requireGameplayVisibility,
                ignoredEntityId,
                out entity);
        }

        internal static bool TryGetStoredStackedUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
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
                        ? GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(
                            ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, candidate, topology))
                        : GameplayEntityQueryPolicy.IsEntityOccupyingBoard(
                            ResolveSpatialState(enemyJumpStatesByEntityId, phasedStatesByEntityId, candidate, topology)))
                {
                    entity = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static SolidKind ResolveSolidKind(EntityState entity)
        {
            return entity.type == EntityType.Box ? SolidKind.Box : SolidKind.Wall;
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
