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
        public int topologySuspendLastTick;
        public bool initialDelayInitialized;
        public int initialDelayTicksRemaining;

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
                topologySuspendLastTick = 0,
                initialDelayInitialized = previousState.initialDelayInitialized,
                initialDelayTicksRemaining = 0,
            };
        }

        public static EnemyJumpRuntimeState TickInitialDelay(
            in EnemyJumpRuntimeState state,
            int initialDelayTicks)
        {
            var updatedState = state;
            updatedState.phase = EnemyJumpPhase.None;
            updatedState.initialDelayInitialized = true;
            if (!state.initialDelayInitialized)
            {
                updatedState.initialDelayTicksRemaining = Mathf.Max(0, initialDelayTicks);
            }

            if (updatedState.initialDelayTicksRemaining > 0)
            {
                updatedState.initialDelayTicksRemaining = Mathf.Max(0, updatedState.initialDelayTicksRemaining - 1);
            }

            return updatedState;
        }

        public static EnemyJumpRuntimeState BeginAirborne(in EnemyJumpRuntimeState state)
        {
            var updatedState = state;
            updatedState.phase = EnemyJumpPhase.Airborne;
            updatedState.topologySuspendLastTick = 0;
            return updatedState;
        }

        public static EnemyJumpRuntimeState ScheduleRetry(in EnemyJumpRuntimeState state, int nextLandingTick)
        {
            var updatedState = state;
            updatedState.phase = EnemyJumpPhase.Airborne;
            updatedState.landingTick = nextLandingTick;
            updatedState.retryCount = Mathf.Max(0, updatedState.retryCount) + 1;
            updatedState.topologySuspendLastTick = 0;
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
            updatedState.topologySuspendLastTick = 0;
            return updatedState;
        }

        public static EnemyJumpRuntimeState SuspendTopologyParticipation(
            in EnemyJumpRuntimeState state,
            int tickIndex)
        {
            if (state.phase != EnemyJumpPhase.Windup &&
                state.phase != EnemyJumpPhase.Airborne)
            {
                return state;
            }

            var suspendTicks = state.topologySuspendLastTick > 0
                ? Mathf.Max(1, tickIndex - state.topologySuspendLastTick)
                : 1;
            var updatedState = state;
            if (updatedState.phase == EnemyJumpPhase.Windup)
            {
                updatedState.windupEndTick += suspendTicks;
                updatedState.landingTick += suspendTicks;
            }
            else
            {
                updatedState.landingTick += suspendTicks;
            }

            updatedState.topologySuspendLastTick = tickIndex;
            return updatedState;
        }

        public static EnemyJumpRuntimeState ResumeTopologyParticipation(in EnemyJumpRuntimeState state)
        {
            if (state.topologySuspendLastTick <= 0)
            {
                return state;
            }

            var updatedState = state;
            updatedState.topologySuspendLastTick = 0;
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
                topologySuspendLastTick = 0,
                initialDelayInitialized = state.initialDelayInitialized,
                initialDelayTicksRemaining = state.initialDelayTicksRemaining,
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
            var actorRef = BuildActorRef(snapshot, source);

            if (RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    new SettlementContext(
                        snapshot,
                        actorRef,
                        jumpState.lockedTargetCell,
                        snapshot.Topology,
                        SpatialState.Anchored)).Verdict == LegalityVerdict.Allowed)
            {
                landingCell = jumpState.lockedTargetCell;
                landingRule = "TargetExact";
                return true;
            }

            var basisFacing = ResolveJumpBasisFacing(
                jumpState.sourceCell,
                jumpState.lockedTargetCell,
                source.facing,
                ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak);
            var orderedOffsets = BuildOrderedJumpFallbackOffsets(basisFacing);

            if (TryFindLandingInFallbackOffsets(
                    snapshot,
                    jumpState.lockedTargetCell,
                    actorRef,
                    orderedOffsets,
                    "Target",
                    out landingCell,
                    out landingRule))
            {
                return true;
            }

            if (RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    new SettlementContext(
                        snapshot,
                        actorRef,
                        jumpState.sourceCell,
                        snapshot.Topology,
                        SpatialState.Anchored)).Verdict == LegalityVerdict.Allowed)
            {
                landingCell = jumpState.sourceCell;
                landingRule = "SourceExact";
                return true;
            }

            return TryFindLandingInFallbackOffsets(
                snapshot,
                jumpState.sourceCell,
                actorRef,
                orderedOffsets,
                "Source",
                out landingCell,
                out landingRule);
        }

        private static bool TryFindLandingInFallbackOffsets(
            WorldSnapshot snapshot,
            SurfaceCell centerCell,
            LegalityActorRef actorRef,
            IReadOnlyList<(Vector2Int Offset, string Rule)> orderedOffsets,
            string prefix,
            out SurfaceCell landingCell,
            out string landingRule)
        {
            for (var i = 0; i < orderedOffsets.Count; i++)
            {
                var candidate = centerCell + orderedOffsets[i].Offset;
                if (RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                        new SettlementContext(
                            snapshot,
                            actorRef,
                            candidate,
                            snapshot.Topology,
                            SpatialState.Anchored)).Verdict != LegalityVerdict.Allowed)
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

        private static LegalityActorRef BuildActorRef(WorldSnapshot snapshot, in EntityState actor)
        {
            return StateQuery.BuildActorRef(snapshot, actor);
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

        public static Direction ResolveJumpBasisFacing(
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            Direction fallbackFacing,
            ChaseAxisPriorityMode axisPriority)
        {
            var planarDelta = targetCell.PlanarPosition - sourceCell.PlanarPosition;
            var absX = Mathf.Abs(planarDelta.x);
            var absY = Mathf.Abs(planarDelta.y);

            if (planarDelta.x == 0 && planarDelta.y == 0)
            {
                return fallbackFacing;
            }

            if (planarDelta.x == 0)
            {
                return planarDelta.y > 0
                    ? Direction.Up
                    : Direction.Down;
            }

            if (planarDelta.y == 0)
            {
                return planarDelta.x > 0
                    ? Direction.Right
                    : Direction.Left;
            }

            var useHorizontal = axisPriority switch
            {
                ChaseAxisPriorityMode.HorizontalFirst => true,
                ChaseAxisPriorityMode.VerticalFirst => false,
                _ => absX >= absY,
            };

            if (useHorizontal)
            {
                return planarDelta.x > 0
                    ? Direction.Right
                    : Direction.Left;
            }

            return planarDelta.y > 0
                ? Direction.Up
                : Direction.Down;
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
