using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyJumpPhase
    {
        None = 0,
        Windup = 1,
        Airborne = 2,
        Cooldown = 3,
    }

    public struct EnemyJumpRuntimeState
    {
        public EnemyJumpPhase phase;
        public int sequence;
        public SurfaceCell sourceCell;
        public SurfaceCell lockedTargetCell;
        public int windupEndTick;
        public int landingTick;
        public int cooldownRemainingTicks;
        public int retryCount;

        public bool IsActive => phase != EnemyJumpPhase.None;
    }

    internal readonly struct EnemyJumpSnapshotEntry
    {
        public EnemyJumpSnapshotEntry(int entityId, EnemyJumpRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemyJumpRuntimeState State { get; }
    }

    internal static class EnemyJumpQueries
    {
        public static EnemyJumpRuntimeState StartJump(
            in EnemyJumpRuntimeState previousState,
            SurfaceCell sourceCell,
            SurfaceCell lockedTargetCell,
            int tickIndex,
            in EnemyJumpTimingSettings timingSettings)
        {
            timingSettings.Validate(nameof(timingSettings));

            return new EnemyJumpRuntimeState
            {
                phase = EnemyJumpPhase.Windup,
                sequence = Math.Max(1, previousState.sequence + 1),
                sourceCell = sourceCell,
                lockedTargetCell = lockedTargetCell,
                windupEndTick = tickIndex + timingSettings.WindupTicks,
                landingTick = tickIndex + timingSettings.WindupTicks + timingSettings.AirborneTicks,
                cooldownRemainingTicks = 0,
                retryCount = 0,
            };
        }

        public static EnemyJumpRuntimeState BeginAirborne(in EnemyJumpRuntimeState state)
        {
            var updatedState = state;
            updatedState.phase = EnemyJumpPhase.Airborne;
            return updatedState;
        }

        public static EnemyJumpRuntimeState ScheduleRetry(in EnemyJumpRuntimeState state, int nextLandingTick)
        {
            var updatedState = state;
            updatedState.phase = EnemyJumpPhase.Airborne;
            updatedState.landingTick = nextLandingTick;
            updatedState.retryCount = Mathf.Max(0, updatedState.retryCount) + 1;
            return updatedState;
        }

        public static EnemyJumpRuntimeState EnterCooldown(
            in EnemyJumpRuntimeState state,
            int cooldownTicks)
        {
            var updatedState = state;
            updatedState.phase = EnemyJumpPhase.Cooldown;
            updatedState.cooldownRemainingTicks = Mathf.Max(0, cooldownTicks);
            updatedState.retryCount = 0;
            return updatedState;
        }

        public static EnemyJumpRuntimeState TickCooldown(in EnemyJumpRuntimeState state)
        {
            if (state.phase != EnemyJumpPhase.Cooldown)
            {
                return state;
            }

            if (state.cooldownRemainingTicks <= 0)
            {
                return Clear(state);
            }

            var nextCooldown = Mathf.Max(0, state.cooldownRemainingTicks - 1);
            if (nextCooldown == 0)
            {
                return Clear(state);
            }

            var updatedState = state;
            updatedState.cooldownRemainingTicks = nextCooldown;
            return updatedState;
        }

        public static EnemyJumpRuntimeState Clear(in EnemyJumpRuntimeState state)
        {
            return new EnemyJumpRuntimeState
            {
                sequence = state.sequence,
            };
        }

        internal static bool TryResolveLandingCell(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyJumpRuntimeState jumpState,
            out SurfaceCell landingCell,
            out string landingRule)
        {
            landingCell = default;
            landingRule = string.Empty;

            if (RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    snapshot,
                    EntityType.Unit,
                    jumpState.lockedTargetCell,
                    source.entityId).Verdict == LegalityVerdict.Allowed)
            {
                landingCell = jumpState.lockedTargetCell;
                landingRule = "TargetExact";
                return true;
            }

            var basisFacing = ResolveJumpBasisFacing(jumpState.sourceCell, jumpState.lockedTargetCell, source.facing);
            var orderedOffsets = BuildOrderedJumpFallbackOffsets(basisFacing);

            if (TryFindLandingInFallbackOffsets(
                    snapshot,
                    jumpState.lockedTargetCell,
                    source.entityId,
                    orderedOffsets,
                    "Target",
                    out landingCell,
                    out landingRule))
            {
                return true;
            }

            if (RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    snapshot,
                    EntityType.Unit,
                    jumpState.sourceCell,
                    source.entityId).Verdict == LegalityVerdict.Allowed)
            {
                landingCell = jumpState.sourceCell;
                landingRule = "SourceExact";
                return true;
            }

            return TryFindLandingInFallbackOffsets(
                snapshot,
                jumpState.sourceCell,
                source.entityId,
                orderedOffsets,
                "Source",
                out landingCell,
                out landingRule);
        }

        private static bool TryFindLandingInFallbackOffsets(
            WorldSnapshot snapshot,
            SurfaceCell centerCell,
            int entityId,
            IReadOnlyList<(Vector2Int Offset, string Rule)> orderedOffsets,
            string prefix,
            out SurfaceCell landingCell,
            out string landingRule)
        {
            for (var i = 0; i < orderedOffsets.Count; i++)
            {
                var candidate = centerCell + orderedOffsets[i].Offset;
                if (RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                        snapshot,
                        EntityType.Unit,
                        candidate,
                        entityId).Verdict != LegalityVerdict.Allowed)
                {
                    continue;
                }

                landingCell = candidate;
                landingRule = $"{prefix}{orderedOffsets[i].Rule}";
                return true;
            }

            landingCell = default;
            landingRule = string.Empty;
            return false;
        }
        private static List<(Vector2Int Offset, string Rule)> BuildOrderedJumpFallbackOffsets(Direction basisFacing)
        {
            var forward = ResolveJumpDelta(basisFacing);
            var left = ResolveJumpDelta(TurnLeft(basisFacing));
            var right = ResolveJumpDelta(TurnRight(basisFacing));
            var back = ResolveJumpDelta(TurnBack(basisFacing));

            return new List<(Vector2Int Offset, string Rule)>
            {
                (forward, "Ring1Forward"),
                (left, "Ring1Left"),
                (right, "Ring1Right"),
                (back, "Ring1Back"),
                (forward * 2, "Ring2Forward2"),
                (forward + left, "Ring2ForwardLeft"),
                (forward + right, "Ring2ForwardRight"),
                (left * 2, "Ring2Left2"),
                (right * 2, "Ring2Right2"),
                (back + left, "Ring2BackLeft"),
                (back + right, "Ring2BackRight"),
                (back * 2, "Ring2Back2"),
            };
        }

        private static Direction ResolveJumpBasisFacing(
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            Direction fallbackFacing)
        {
            var planarDelta = targetCell.PlanarPosition - sourceCell.PlanarPosition;
            var absX = Mathf.Abs(planarDelta.x);
            var absY = Mathf.Abs(planarDelta.y);

            if (absX > absY && planarDelta.x != 0)
            {
                return planarDelta.x > 0
                    ? Direction.Right
                    : Direction.Left;
            }

            if (absY > absX && planarDelta.y != 0)
            {
                return planarDelta.y > 0
                    ? Direction.Up
                    : Direction.Down;
            }

            return fallbackFacing;
        }

        private static Vector2Int ResolveJumpDelta(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Right => Vector2Int.right,
                Direction.Down => Vector2Int.down,
                Direction.Left => Vector2Int.left,
                _ => Vector2Int.zero,
            };
        }

        private static Direction TurnRight(Direction facing)
        {
            return facing switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnLeft(Direction facing)
        {
            return facing switch
            {
                Direction.Up => Direction.Left,
                Direction.Left => Direction.Down,
                Direction.Down => Direction.Right,
                Direction.Right => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnBack(Direction facing)
        {
            return facing switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }
    }
}
