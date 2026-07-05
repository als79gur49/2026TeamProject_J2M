using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal enum CombatWindupStartBlockReason
    {
        None = 0,
        TargetInvalid = 1,
        OutsideLogicRange = 2,
        OutsideSimulationStartRange = 3,
        MissingSimulationPose = 4,
        DifferentFace = 5,
        SevereTransition = 6,
        InvalidForwardTargetCell = 7,
        ActivePendingImpactLimitReached = 8,
        ForwardPathBlockedByTileFeature = 9,
        NotSettledAtAnchor = 10,
        ForwardPathBlockedBySolid = 11,
    }

    internal readonly struct CombatWindupStartQueryResult
    {
        public CombatWindupStartQueryResult(
            bool canStart,
            bool shouldApproach,
            CombatWindupStartBlockReason blockReason,
            int distanceFixedUnits,
            int thresholdFixedUnits,
            CombatOriginAnchor enemyOrigin)
        {
            CanStart = canStart;
            ShouldApproach = shouldApproach;
            BlockReason = blockReason;
            DistanceFixedUnits = distanceFixedUnits;
            ThresholdFixedUnits = thresholdFixedUnits;
            EnemyOrigin = enemyOrigin;
        }

        public bool CanStart { get; }

        public bool ShouldApproach { get; }

        public CombatWindupStartBlockReason BlockReason { get; }

        public int DistanceFixedUnits { get; }

        public int ThresholdFixedUnits { get; }

        public CombatOriginAnchor EnemyOrigin { get; }

        public static CombatWindupStartQueryResult Block(CombatWindupStartBlockReason reason)
        {
            return new CombatWindupStartQueryResult(
                canStart: false,
                shouldApproach: reason == CombatWindupStartBlockReason.OutsideSimulationStartRange,
                reason,
                distanceFixedUnits: -1,
                thresholdFixedUnits: -1,
                default);
        }
    }

    internal static class CombatWindupPoseQueries
    {
        private static CombatWindupStartQueryResult QueryStartShortRangeWindupFromSimulationPose(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            in AttackDecisionSettings attackDecisionSettings,
            in ProjectileWindupSettings projectileWindupSettings)
        {
            if (IsInSevereCombatOriginTransition(snapshot, enemy) ||
                IsInSevereCombatOriginTransition(snapshot, player))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.SevereTransition);
            }

            if (!TryResolveSimulationCombatOrigin(snapshot, enemy, out var enemyOrigin) ||
                !TryResolveSimulationCombatOrigin(snapshot, player, out var playerOrigin))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.MissingSimulationPose);
            }

            if (enemyOrigin.AnchorCell.face != playerOrigin.AnchorCell.face)
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.DifferentFace);
            }

            var thresholdUnits = checked((attackDecisionSettings.AttackRange * SimulationFixed.UnitsPerCell) +
                                         projectileWindupSettings.VisualRangeSlackUnits);
            var distanceUnits = GetManhattanDistanceUnits(enemyOrigin, playerOrigin);
            return distanceUnits <= thresholdUnits
                ? new CombatWindupStartQueryResult(
                    canStart: true,
                    shouldApproach: false,
                    CombatWindupStartBlockReason.None,
                    distanceUnits,
                    thresholdUnits,
                    enemyOrigin)
                : new CombatWindupStartQueryResult(
                    canStart: false,
                    shouldApproach: true,
                    CombatWindupStartBlockReason.OutsideSimulationStartRange,
                    distanceUnits,
                    thresholdUnits,
                    enemyOrigin);
        }

        public static CombatWindupStartQueryResult QueryStartWindupForwardCellProjectile(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            IAttackDecisionStrategy attackDecisionStrategy,
            in AttackDecisionSettings attackDecisionSettings,
            in WindupForwardCellProjectileSettings settings,
            out SurfaceCell targetCell)
        {
            return QueryStartWindupForwardCellProjectile(
                snapshot,
                enemy,
                player,
                attackDecisionStrategy,
                attackDecisionSettings,
                settings,
                tileFeatureDefinitions: null,
                out targetCell);
        }

        public static CombatWindupStartQueryResult QueryStartWindupForwardCellProjectile(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            IAttackDecisionStrategy attackDecisionStrategy,
            in AttackDecisionSettings attackDecisionSettings,
            in WindupForwardCellProjectileSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out SurfaceCell targetCell)
        {
            targetCell = default;
            settings.Validate(nameof(settings));

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            attackDecisionSettings.Validate(nameof(attackDecisionSettings));
            var windupStartSettings = settings.ToWindupStartSettings();
            windupStartSettings.Validate(nameof(settings));

            if (attackDecisionStrategy is not WindupForwardCellProjectileAttackDecisionStrategy)
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.TargetInvalid);
            }

            if (!EnemyAttackRangeQueries.IsTargetInRange(enemy, player, attackDecisionSettings))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.OutsideLogicRange);
            }

            var startQuery = QueryStartShortRangeWindupFromSimulationPose(
                snapshot,
                enemy,
                player,
                attackDecisionSettings,
                windupStartSettings);
            if (!startQuery.CanStart)
            {
                if (settings.RequireValidForwardCell &&
                    startQuery.BlockReason == CombatWindupStartBlockReason.OutsideSimulationStartRange &&
                    IsForwardProjectilePathBlockedBySolid(
                        snapshot,
                        startQuery.EnemyOrigin.AnchorCell,
                        player.position,
                        attackDecisionSettings.AttackRange))
                {
                    return CombatWindupStartQueryResult.Block(
                        CombatWindupStartBlockReason.ForwardPathBlockedBySolid);
                }

                if (settings.RequireValidForwardCell &&
                    startQuery.BlockReason == CombatWindupStartBlockReason.OutsideSimulationStartRange &&
                    IsForwardProjectilePathBlockedByActiveBarricade(
                        snapshot,
                        startQuery.EnemyOrigin.AnchorCell,
                        player.position,
                        attackDecisionSettings.AttackRange,
                        tileFeatureDefinitions))
                {
                    return CombatWindupStartQueryResult.Block(
                        CombatWindupStartBlockReason.ForwardPathBlockedByTileFeature);
                }

                return startQuery;
            }

            if (!UnitSpatialQuery.IsSettledAtAnchor(snapshot, enemy.entityId))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.NotSettledAtAnchor);
            }

            if (snapshot.CountPendingCellImpactsForOwner(enemy.entityId) >= settings.ActivePendingImpactLimitPerOwner)
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.ActivePendingImpactLimitReached);
            }

            var attackDirection = EnemyActionStateTargeting.ResolveFacing(enemy, player);
            if (settings.RequireValidForwardCell &&
                IsForwardProjectilePathBlockedBySolid(
                    snapshot,
                    startQuery.EnemyOrigin.AnchorCell,
                    player.position,
                    attackDecisionSettings.AttackRange))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.ForwardPathBlockedBySolid);
            }

            if (settings.RequireValidForwardCell &&
                !TryResolveForwardTargetCell(
                    snapshot,
                    startQuery.EnemyOrigin.AnchorCell,
                    attackDirection,
                    player.position,
                    attackDecisionSettings.AttackRange,
                    tileFeatureDefinitions,
                    out targetCell))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.InvalidForwardTargetCell);
            }

            if (settings.RequireValidForwardCell &&
                IsForwardProjectilePathBlockedByActiveBarricade(
                    snapshot,
                    startQuery.EnemyOrigin.AnchorCell,
                    targetCell,
                    attackDecisionSettings.AttackRange,
                    tileFeatureDefinitions))
            {
                return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.ForwardPathBlockedByTileFeature);
            }

            if (!settings.RequireValidForwardCell)
            {
                TryResolveForwardTargetCell(
                    snapshot,
                    startQuery.EnemyOrigin.AnchorCell,
                    attackDirection,
                    player.position,
                    attackDecisionSettings.AttackRange,
                    tileFeatureDefinitions,
                    out targetCell);
            }

            return startQuery;
        }

        public static CombatWindupStartQueryResult QueryShortRangeWindupStart(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            EnemyCombatCapabilityRuntime combatCapability,
            out SurfaceCell? lockedTargetCell)
        {
            return QueryShortRangeWindupStart(
                snapshot,
                enemy,
                player,
                combatCapability,
                tileFeatureDefinitions: null,
                out lockedTargetCell);
        }

        public static CombatWindupStartQueryResult QueryShortRangeWindupStart(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            EnemyCombatCapabilityRuntime combatCapability,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out SurfaceCell? lockedTargetCell)
        {
            if (combatCapability == null)
            {
                throw new ArgumentNullException(nameof(combatCapability));
            }

            lockedTargetCell = null;
            if (combatCapability.Kind == AttackDecisionStrategyKind.WindupForwardCellProjectile)
            {
                var result = QueryStartWindupForwardCellProjectile(
                    snapshot,
                    enemy,
                    player,
                    combatCapability.AttackDecisionStrategy,
                    combatCapability.AttackDecisionSettings,
                    combatCapability.WindupForwardCellProjectileSettings,
                    tileFeatureDefinitions,
                    out var targetCell);
                if (result.CanStart)
                {
                    lockedTargetCell = targetCell;
                }

                return result;
            }

            return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.TargetInvalid);
        }

        public static bool TryResolveForwardTargetCell(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            Direction direction,
            out SurfaceCell targetCell)
        {
            targetCell = default;
            if (snapshot == null ||
                !EnemyMovementStrategyShared.TryResolveDelta(direction, out var delta))
            {
                return false;
            }

            return TryResolveForwardStep(snapshot, baseCell, delta, out targetCell);
        }

        public static bool TryResolveForwardTargetCell(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            Direction direction,
            SurfaceCell desiredTargetCell,
            int maxRangeCells,
            out SurfaceCell targetCell)
        {
            return TryResolveForwardTargetCell(
                snapshot,
                baseCell,
                direction,
                desiredTargetCell,
                maxRangeCells,
                tileFeatureDefinitions: null,
                out targetCell);
        }

        public static bool TryResolveForwardTargetCell(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            Direction direction,
            SurfaceCell desiredTargetCell,
            int maxRangeCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out SurfaceCell targetCell)
        {
            targetCell = default;
            if (snapshot == null ||
                maxRangeCells <= 0 ||
                desiredTargetCell.face != baseCell.face ||
                !EnemyMovementStrategyShared.TryResolveDelta(direction, out var delta))
            {
                return false;
            }

            var offset = desiredTargetCell.PlanarPosition - baseCell.PlanarPosition;
            var distanceCells = Math.Abs(offset.x) + Math.Abs(offset.y);
            if (distanceCells <= 0 ||
                distanceCells > maxRangeCells ||
                !IsAlignedWithForwardDirection(offset, delta))
            {
                return false;
            }

            var current = baseCell;
            for (var step = 0; step < distanceCells; step++)
            {
                if (!TryResolveForwardStep(snapshot, current, delta, out current))
                {
                    return false;
                }

                if (snapshot.TryGetSolidSemanticAt(current, out _))
                {
                    return false;
                }

                if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                        snapshot,
                        tileFeatureDefinitions,
                        current,
                        TileFeatureBlockerSubject.Unit,
                        TileFeatureMovementKind.GroundStep,
                        out _))
                {
                    return false;
                }
            }

            targetCell = current;
            return targetCell == desiredTargetCell;
        }

        internal static bool IsForwardProjectilePathBlocked(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            SurfaceCell targetCell,
            int maxRangeCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            return IsForwardProjectilePathBlockedBySolid(snapshot, baseCell, targetCell, maxRangeCells) ||
                   IsForwardProjectilePathBlockedByActiveBarricade(
                       snapshot,
                       baseCell,
                       targetCell,
                       maxRangeCells,
                       tileFeatureDefinitions);
        }

        internal static bool IsForwardProjectilePathBlockedBySolid(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            SurfaceCell targetCell,
            int maxRangeCells)
        {
            if (snapshot == null ||
                baseCell.face != targetCell.face ||
                maxRangeCells <= 0)
            {
                return false;
            }

            var dx = targetCell.x - baseCell.x;
            var dy = targetCell.y - baseCell.y;
            var distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance <= 0 ||
                distance > maxRangeCells ||
                (dx != 0 && dy != 0))
            {
                return false;
            }

            var stepX = Math.Sign(dx);
            var stepY = Math.Sign(dy);
            var current = new SurfaceCell(baseCell.face, baseCell.x + stepX, baseCell.y + stepY);
            for (var step = 0; step < distance; step++)
            {
                if (snapshot.TryGetSolidSemanticAt(current, out _))
                {
                    return true;
                }

                current = new SurfaceCell(current.face, current.x + stepX, current.y + stepY);
            }

            return false;
        }

        internal static bool IsForwardProjectilePathBlockedByActiveBarricade(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            SurfaceCell targetCell,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            return IsForwardProjectilePathBlockedByActiveBarricade(
                snapshot,
                baseCell,
                targetCell,
                int.MaxValue,
                tileFeatureDefinitions);
        }

        private static bool IsForwardProjectilePathBlockedByActiveBarricade(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            SurfaceCell targetCell,
            int maxRangeCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            if (snapshot == null ||
                baseCell.face != targetCell.face ||
                maxRangeCells <= 0 ||
                tileFeatureDefinitions == null ||
                tileFeatureDefinitions.Count == 0)
            {
                return false;
            }

            var dx = targetCell.x - baseCell.x;
            var dy = targetCell.y - baseCell.y;
            var distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance <= 0 ||
                distance > maxRangeCells ||
                (dx != 0 && dy != 0))
            {
                return false;
            }

            var stepX = Math.Sign(dx);
            var stepY = Math.Sign(dy);
            var current = new SurfaceCell(baseCell.face, baseCell.x + stepX, baseCell.y + stepY);
            for (var step = 0; step < distance; step++)
            {
                if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                        snapshot,
                        tileFeatureDefinitions,
                        current,
                        TileFeatureBlockerSubject.Unit,
                        TileFeatureMovementKind.GroundStep,
                        out _))
                {
                    return true;
                }

                current = new SurfaceCell(current.face, current.x + stepX, current.y + stepY);
            }

            return false;
        }

        private static bool TryResolveForwardStep(
            WorldSnapshot snapshot,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell targetCell)
        {
            targetCell = default;
            if (!snapshot.TryResolveUnitStep(origin, delta, out targetCell, out _, out var updatedTopology))
            {
                return false;
            }

            return updatedTopology.Equals(snapshot.Topology) &&
                   targetCell.face == origin.face &&
                   snapshot.Topology.IsFaceActive(targetCell.face);
        }

        private static bool IsAlignedWithForwardDirection(Vector2Int offset, Vector2Int delta)
        {
            var distanceCells = Math.Abs(offset.x) + Math.Abs(offset.y);
            return offset.x == delta.x * distanceCells &&
                   offset.y == delta.y * distanceCells;
        }

        public static bool IsMoveLockStartedThisTick(WorldSnapshot snapshot, int entityId, int tickIndex)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return snapshot.TryGetEntityExecutionLockState(entityId, out var executionLockState) &&
                   executionLockState.phase == EntityExecutionPhase.Move &&
                   EntityExecutionLockQueries.IsLocked(executionLockState, tickIndex) &&
                   snapshot.TryGetUnitKinematicPose(entityId, out var pose) &&
                   pose.HasAuthoritativeState &&
                   pose.State.startedTick == tickIndex;
        }

        public static bool TryResolveSimulationCombatOrigin(
            WorldSnapshot snapshot,
            in EntityState entity,
            out CombatOriginAnchor origin)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out var continuousPose) &&
                (continuousPose.HasAuthoritativeState ||
                 !snapshot.TryGetUnitKinematicPose(entity.entityId, out var kinematicPoseForFallback) ||
                 !kinematicPoseForFallback.HasAuthoritativeState))
            {
                origin = CreateOrigin(
                    continuousPose.AnchorCell,
                    continuousPose.LocalOffset,
                    continuousPose.State.facing == Direction.None ? entity.facing : continuousPose.State.facing);
                return true;
            }

            if (snapshot.TryGetUnitKinematicPose(entity.entityId, out var kinematicPose))
            {
                origin = CreateOrigin(
                    kinematicPose.AnchorCell,
                    kinematicPose.LocalOffset,
                    entity.facing);
                return true;
            }

            // Conservative gameplay path: if WorldSnapshot cannot expose a simulation pose,
            // Combat windup gate/execute must not fall back to renderer or presentation state.
            origin = default;
            return false;
        }

        public static bool IsInSevereCombatOriginTransition(WorldSnapshot snapshot, in EntityState entity)
        {
            if (entity.hp <= 0 ||
                entity.markedForDeath ||
                entity.boardPresence != EntityBoardPresence.Occupying ||
                !snapshot.Topology.IsFaceActive(entity.position.face))
            {
                return true;
            }

            if (snapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState) &&
                (jumpState.phase == EnemyJumpPhase.Windup ||
                 jumpState.phase == EnemyJumpPhase.Airborne))
            {
                return true;
            }

            if (snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                glideState.IsActive)
            {
                return true;
            }

            if (snapshot.TryGetEnemyChargeState(entity.entityId, out var chargeState) &&
                chargeState.phase != EnemyChargePhase.None)
            {
                return true;
            }

            if (snapshot.TryGetPhasedState(entity.entityId, out var phasedState) &&
                phasedState.IsActive)
            {
                return true;
            }

            if (!snapshot.TryGetUnitKinematicPose(entity.entityId, out var pose) ||
                !pose.HasAuthoritativeState)
            {
                return false;
            }

            return pose.Mode == MotionMode.Forced ||
                   pose.Mode == MotionMode.Charge ||
                   pose.Mode == MotionMode.Interrupted ||
                   pose.State.forcedOp != ForcedMotionOp.None;
        }

        private static CombatOriginAnchor CreateOrigin(
            SurfaceCell anchorCell,
            SimulationOffset2 localOffset,
            Direction facing)
        {
            return new CombatOriginAnchor(
                anchorCell,
                localOffset,
                checked((anchorCell.x * SimulationFixed.UnitsPerCell) + localOffset.X.RawValue),
                checked((anchorCell.y * SimulationFixed.UnitsPerCell) + localOffset.Y.RawValue),
                facing);
        }

        private static int GetManhattanDistanceUnits(CombatOriginAnchor source, CombatOriginAnchor target)
        {
            return Math.Abs(target.TileSpaceX - source.TileSpaceX) +
                   Math.Abs(target.TileSpaceY - source.TileSpaceY);
        }

    }
}
