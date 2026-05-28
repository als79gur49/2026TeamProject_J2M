using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal enum WindupMeleeStartBlockReason
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
    }

    internal readonly struct WindupMeleeStartQueryResult
    {
        public WindupMeleeStartQueryResult(
            bool canStart,
            bool shouldApproach,
            WindupMeleeStartBlockReason blockReason,
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

        public WindupMeleeStartBlockReason BlockReason { get; }

        public int DistanceFixedUnits { get; }

        public int ThresholdFixedUnits { get; }

        public CombatOriginAnchor EnemyOrigin { get; }

        public static WindupMeleeStartQueryResult Block(WindupMeleeStartBlockReason reason)
        {
            return new WindupMeleeStartQueryResult(
                canStart: false,
                shouldApproach: reason == WindupMeleeStartBlockReason.OutsideSimulationStartRange,
                reason,
                distanceFixedUnits: -1,
                thresholdFixedUnits: -1,
                default);
        }
    }

    internal static class WindupMeleeCombatPoseQueries
    {
        private const int ProvisionalPlayerCombatRadiusUnits = KinematicFixed.UnitsPerCell * 3 / 16;

        public static bool CanStartWindupMeleeA(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            IAttackDecisionStrategy attackDecisionStrategy,
            in AttackDecisionSettings attackDecisionSettings,
            in WindupMeleeSettings windupMeleeSettings,
            out CombatOriginAnchor enemyOrigin)
        {
            var result = QueryStartWindupMeleeA(
                snapshot,
                enemy,
                player,
                attackDecisionStrategy,
                attackDecisionSettings,
                windupMeleeSettings);
            enemyOrigin = result.EnemyOrigin;
            return result.CanStart;
        }

        public static WindupMeleeStartQueryResult QueryStartWindupMeleeA(
            WorldSnapshot snapshot,
            in EntityState enemy,
            in EntityState player,
            IAttackDecisionStrategy attackDecisionStrategy,
            in AttackDecisionSettings attackDecisionSettings,
            in WindupMeleeSettings windupMeleeSettings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            attackDecisionSettings.Validate(nameof(attackDecisionSettings));
            windupMeleeSettings.Validate(nameof(windupMeleeSettings));

            if (!IsShortRangeWindupStrategy(attackDecisionStrategy))
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.TargetInvalid);
            }

            if (!attackDecisionStrategy.IsTargetInRange(enemy, player, attackDecisionSettings))
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.OutsideLogicRange);
            }

            if (IsInSevereCombatOriginTransition(snapshot, enemy) ||
                IsInSevereCombatOriginTransition(snapshot, player))
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.SevereTransition);
            }

            if (!TryResolveSimulationCombatOrigin(snapshot, enemy, out var enemyOrigin) ||
                !TryResolveSimulationCombatOrigin(snapshot, player, out var playerOrigin))
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.MissingSimulationPose);
            }

            if (enemyOrigin.AnchorCell.face != playerOrigin.AnchorCell.face)
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.DifferentFace);
            }

            var thresholdUnits = checked((attackDecisionSettings.AttackRange * KinematicFixed.UnitsPerCell) +
                                         windupMeleeSettings.VisualRangeSlackUnits);
            var distanceUnits = GetManhattanDistanceUnits(enemyOrigin, playerOrigin);
            return distanceUnits <= thresholdUnits
                ? new WindupMeleeStartQueryResult(
                    canStart: true,
                    shouldApproach: false,
                    WindupMeleeStartBlockReason.None,
                    distanceUnits,
                    thresholdUnits,
                    enemyOrigin)
                : new WindupMeleeStartQueryResult(
                    canStart: false,
                    shouldApproach: true,
                    WindupMeleeStartBlockReason.OutsideSimulationStartRange,
                    distanceUnits,
                    thresholdUnits,
                    enemyOrigin);
        }

        public static WindupMeleeStartQueryResult QueryStartWindupForwardCellProjectile(
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

        public static WindupMeleeStartQueryResult QueryStartWindupForwardCellProjectile(
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

            var startQuery = QueryStartWindupMeleeA(
                snapshot,
                enemy,
                player,
                attackDecisionStrategy,
                attackDecisionSettings,
                settings.ToWindupStartSettings());
            if (!startQuery.CanStart)
            {
                return startQuery;
            }

            if (snapshot.CountPendingCellImpactsForOwner(enemy.entityId) >= settings.ActivePendingImpactLimitPerOwner)
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.ActivePendingImpactLimitReached);
            }

            var attackDirection = EnemyActionStateTargeting.ResolveFacing(enemy, player);
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
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.InvalidForwardTargetCell);
            }

            if (settings.RequireValidForwardCell &&
                IsForwardProjectilePathBlockedByActiveBarricade(
                    snapshot,
                    startQuery.EnemyOrigin.AnchorCell,
                    targetCell,
                    tileFeatureDefinitions))
            {
                return WindupMeleeStartQueryResult.Block(WindupMeleeStartBlockReason.ForwardPathBlockedByTileFeature);
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

        public static WindupMeleeStartQueryResult QueryShortRangeWindupStart(
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

        public static WindupMeleeStartQueryResult QueryShortRangeWindupStart(
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

            return QueryStartWindupMeleeA(
                snapshot,
                enemy,
                player,
                combatCapability.AttackDecisionStrategy,
                combatCapability.AttackDecisionSettings,
                combatCapability.WindupMeleeSettings);
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

        private static bool IsForwardProjectilePathBlockedByActiveBarricade(
            WorldSnapshot snapshot,
            SurfaceCell baseCell,
            SurfaceCell targetCell,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            if (snapshot == null ||
                baseCell.face != targetCell.face ||
                tileFeatureDefinitions == null ||
                tileFeatureDefinitions.Count == 0)
            {
                return false;
            }

            var dx = targetCell.x - baseCell.x;
            var dy = targetCell.y - baseCell.y;
            var distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance <= 0 || (dx != 0 && dy != 0))
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

        private static bool IsShortRangeWindupStrategy(IAttackDecisionStrategy attackDecisionStrategy)
        {
            return attackDecisionStrategy is MeleeAttackDecisionStrategy ||
                   attackDecisionStrategy is WindupForwardCellProjectileAttackDecisionStrategy;
        }

        public static bool CanExecuteHitFromLockedCombatAnchor(
            WorldSnapshot snapshot,
            in EnemyActionRuntimeState actionState,
            in EntityState player,
            in AttackDecisionSettings attackDecisionSettings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            attackDecisionSettings.Validate(nameof(attackDecisionSettings));
            if (!actionState.hasLockedCombatAnchor ||
                !TryResolveSimulationCombatOrigin(snapshot, player, out var playerOrigin) ||
                actionState.lockedCombatAnchor.AnchorCell.face != playerOrigin.AnchorCell.face)
            {
                return false;
            }

            // Temporary hurtbox approximation: runtime has no combat hurtbox surface yet,
            // so WindupMelee execute uses the player simulation combat point plus a small radius.
            return IsPointWithinForwardMeleeShape(
                actionState.lockedCombatAnchor,
                actionState.direction,
                attackDecisionSettings.AttackRange * KinematicFixed.UnitsPerCell,
                playerOrigin.TileSpaceX,
                playerOrigin.TileSpaceY,
                ProvisionalPlayerCombatRadiusUnits);
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
            // WindupMelee gate/execute must not fall back to renderer or presentation state.
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
            KinematicOffset2 localOffset,
            Direction facing)
        {
            return new CombatOriginAnchor(
                anchorCell,
                localOffset,
                checked((anchorCell.x * KinematicFixed.UnitsPerCell) + localOffset.X.RawValue),
                checked((anchorCell.y * KinematicFixed.UnitsPerCell) + localOffset.Y.RawValue),
                facing);
        }

        private static int GetManhattanDistanceUnits(CombatOriginAnchor source, CombatOriginAnchor target)
        {
            return Math.Abs(target.TileSpaceX - source.TileSpaceX) +
                   Math.Abs(target.TileSpaceY - source.TileSpaceY);
        }

        private static bool IsPointWithinForwardMeleeShape(
            CombatOriginAnchor anchor,
            Direction direction,
            int rangeUnits,
            int pointX,
            int pointY,
            int radiusUnits)
        {
            var deltaX = ResolveDirectionX(direction);
            var deltaY = ResolveDirectionY(direction);
            var endX = anchor.TileSpaceX + (deltaX * rangeUnits);
            var endY = anchor.TileSpaceY + (deltaY * rangeUnits);
            if (deltaX == 0 && deltaY == 0)
            {
                endX = anchor.TileSpaceX;
                endY = anchor.TileSpaceY;
            }

            return DistanceSquaredPointToSegment(
                       pointX,
                       pointY,
                       anchor.TileSpaceX,
                       anchor.TileSpaceY,
                       endX,
                       endY) <= (long)radiusUnits * radiusUnits;
        }

        private static int ResolveDirectionX(Direction direction)
        {
            return direction switch
            {
                Direction.Right => 1,
                Direction.Left => -1,
                _ => 0,
            };
        }

        private static int ResolveDirectionY(Direction direction)
        {
            return direction switch
            {
                Direction.Up => 1,
                Direction.Down => -1,
                _ => 0,
            };
        }

        private static long DistanceSquaredPointToSegment(
            int pointX,
            int pointY,
            int segmentStartX,
            int segmentStartY,
            int segmentEndX,
            int segmentEndY)
        {
            var segmentX = segmentEndX - segmentStartX;
            var segmentY = segmentEndY - segmentStartY;
            var pointDeltaX = pointX - segmentStartX;
            var pointDeltaY = pointY - segmentStartY;
            var segmentLengthSquared = ((long)segmentX * segmentX) + ((long)segmentY * segmentY);
            if (segmentLengthSquared == 0)
            {
                return ((long)pointDeltaX * pointDeltaX) + ((long)pointDeltaY * pointDeltaY);
            }

            var projectedNumerator = ((long)pointDeltaX * segmentX) + ((long)pointDeltaY * segmentY);
            if (projectedNumerator <= 0)
            {
                return ((long)pointDeltaX * pointDeltaX) + ((long)pointDeltaY * pointDeltaY);
            }

            if (projectedNumerator >= segmentLengthSquared)
            {
                var endDeltaX = pointX - segmentEndX;
                var endDeltaY = pointY - segmentEndY;
                return ((long)endDeltaX * endDeltaX) + ((long)endDeltaY * endDeltaY);
            }

            var cross = ((long)pointDeltaX * segmentY) - ((long)pointDeltaY * segmentX);
            return (cross * cross) / segmentLengthSquared;
        }
    }
}
