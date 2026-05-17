using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyActionKind
    {
        None = 0,
        Melee = 1,
    }

    public readonly struct CombatOriginAnchor : IEquatable<CombatOriginAnchor>
    {
        public CombatOriginAnchor(
            SurfaceCell anchorCell,
            KinematicOffset2 localOffset,
            int tileSpaceX,
            int tileSpaceY,
            Direction facing)
        {
            AnchorCell = anchorCell;
            LocalOffset = localOffset;
            TileSpaceX = tileSpaceX;
            TileSpaceY = tileSpaceY;
            Facing = facing;
        }

        public SurfaceCell AnchorCell { get; }

        public KinematicOffset2 LocalOffset { get; }

        public int TileSpaceX { get; }

        public int TileSpaceY { get; }

        public Direction Facing { get; }

        public bool Equals(CombatOriginAnchor other)
        {
            return AnchorCell.Equals(other.AnchorCell) &&
                   LocalOffset.Equals(other.LocalOffset) &&
                   TileSpaceX == other.TileSpaceX &&
                   TileSpaceY == other.TileSpaceY &&
                   Facing == other.Facing;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatOriginAnchor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = AnchorCell.GetHashCode();
                hashCode = (hashCode * 397) ^ LocalOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ TileSpaceX;
                hashCode = (hashCode * 397) ^ TileSpaceY;
                hashCode = (hashCode * 397) ^ (int)Facing;
                return hashCode;
            }
        }
    }

    public struct EnemyActionRuntimeState
    {
        public EnemyActionKind kind;
        public int sequence;
        public int lockedTargetEntityId;
        public Direction direction;
        public int startTick;
        public int executeTick;
        public bool executionAttempted;
        public bool hasLockedCombatAnchor;
        public CombatOriginAnchor lockedCombatAnchor;

        public bool IsActive => kind != EnemyActionKind.None;
    }

    public readonly struct EnemyActionTransition
    {
        public EnemyActionTransition(
            int entityId,
            in EnemyActionRuntimeState previousAction,
            in EnemyActionRuntimeState currentAction)
        {
            EntityId = entityId;
            PreviousKind = previousAction.kind;
            CurrentKind = currentAction.kind;
            PreviousSequence = previousAction.sequence;
            CurrentSequence = currentAction.sequence;
            StartedThisTick = currentAction.kind != EnemyActionKind.None &&
                              (previousAction.kind != currentAction.kind ||
                               previousAction.sequence != currentAction.sequence);
            CanceledThisTick = previousAction.kind != EnemyActionKind.None &&
                               currentAction.kind == EnemyActionKind.None &&
                               !previousAction.executionAttempted;
        }

        public int EntityId { get; }

        public EnemyActionKind PreviousKind { get; }

        public EnemyActionKind CurrentKind { get; }

        public int PreviousSequence { get; }

        public int CurrentSequence { get; }

        public bool StartedThisTick { get; }

        public bool CanceledThisTick { get; }
    }

    internal readonly struct EnemyActionSnapshotEntry
    {
        public EnemyActionSnapshotEntry(int entityId, EnemyActionRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemyActionRuntimeState State { get; }
    }

    internal static class EnemyActionQueries
    {
        public static EnemyActionRuntimeState StartAction(
            in EnemyActionRuntimeState previousState,
            EnemyActionKind kind,
            int lockedTargetEntityId,
            Direction direction,
            int startTick,
            int windupTicks,
            CombatOriginAnchor? lockedCombatAnchor = null)
        {
            if (kind == EnemyActionKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Enemy actions must start with a concrete action kind.");
            }

            if (windupTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windupTicks), "Enemy action wind-up ticks must be zero or greater.");
            }

            return new EnemyActionRuntimeState
            {
                kind = kind,
                sequence = Math.Max(1, previousState.sequence + 1),
                lockedTargetEntityId = lockedTargetEntityId,
                direction = direction,
                startTick = startTick,
                executeTick = startTick + windupTicks,
                executionAttempted = false,
                hasLockedCombatAnchor = lockedCombatAnchor.HasValue,
                lockedCombatAnchor = lockedCombatAnchor.GetValueOrDefault(),
            };
        }

        public static EnemyActionRuntimeState Clear(in EnemyActionRuntimeState state)
        {
            return new EnemyActionRuntimeState
            {
                sequence = state.sequence,
            };
        }

        public static EnemyActionRuntimeState MarkExecutionAttempted(
            in EnemyActionRuntimeState state,
            int tickIndex)
        {
            if (!state.IsActive ||
                state.executionAttempted ||
                tickIndex < state.executeTick)
            {
                return state;
            }

            var updatedState = state;
            updatedState.executionAttempted = true;
            return updatedState;
        }

        public static bool CanExecute(in EnemyActionRuntimeState state, int tickIndex)
        {
            return state.IsActive &&
                   !state.executionAttempted &&
                   state.executeTick == tickIndex;
        }
    }
}
