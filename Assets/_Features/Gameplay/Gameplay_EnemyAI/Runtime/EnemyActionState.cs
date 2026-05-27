using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyActionKind
    {
        None = 0,
        Melee = 1,
        ForwardCellProjectile = 2,
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
        public bool hasLockedForwardCellImpact;
        public SurfaceCell lockedAttackBaseCell;
        public SurfaceCell lockedTargetCell;
        public Direction lockedAttackDirection;

        public bool IsActive => kind != EnemyActionKind.None;
    }

    public readonly struct PendingCellImpact : IEquatable<PendingCellImpact>
    {
        public PendingCellImpact(
            int impactId,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            Direction direction,
            int damage,
            int createdTick,
            int releaseTick,
            int impactTick)
        {
            if (impactId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactId), "Pending cell impacts require a positive impact ID.");
            }

            if (ownerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), "Pending cell impacts require a positive owner ID.");
            }

            if (sourceEnemyId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceEnemyId), "Pending cell impacts require a positive source enemy ID.");
            }

            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Pending cell impacts require positive damage.");
            }

            if (impactTick <= releaseTick)
            {
                throw new ArgumentOutOfRangeException(nameof(impactTick), "Pending cell impacts must resolve after release.");
            }

            ImpactId = impactId;
            OwnerId = ownerId;
            SourceEnemyId = sourceEnemyId;
            TargetCell = targetCell;
            Direction = direction;
            Damage = damage;
            CreatedTick = createdTick;
            ReleaseTick = releaseTick;
            ImpactTick = impactTick;
        }

        public int ImpactId { get; }

        public int OwnerId { get; }

        public int SourceEnemyId { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public int Damage { get; }

        public int CreatedTick { get; }

        public int ReleaseTick { get; }

        public int ImpactTick { get; }

        public bool Equals(PendingCellImpact other)
        {
            return ImpactId == other.ImpactId &&
                   OwnerId == other.OwnerId &&
                   SourceEnemyId == other.SourceEnemyId &&
                   TargetCell.Equals(other.TargetCell) &&
                   Direction == other.Direction &&
                   Damage == other.Damage &&
                   CreatedTick == other.CreatedTick &&
                   ReleaseTick == other.ReleaseTick &&
                   ImpactTick == other.ImpactTick;
        }

        public override bool Equals(object obj)
        {
            return obj is PendingCellImpact other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ImpactId;
                hashCode = (hashCode * 397) ^ OwnerId;
                hashCode = (hashCode * 397) ^ SourceEnemyId;
                hashCode = (hashCode * 397) ^ TargetCell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Direction;
                hashCode = (hashCode * 397) ^ Damage;
                hashCode = (hashCode * 397) ^ CreatedTick;
                hashCode = (hashCode * 397) ^ ReleaseTick;
                hashCode = (hashCode * 397) ^ ImpactTick;
                return hashCode;
            }
        }
    }

    internal readonly struct PendingCellImpactSnapshotEntry
    {
        public PendingCellImpactSnapshotEntry(int impactId, PendingCellImpact impact)
        {
            ImpactId = impactId;
            Impact = impact;
        }

        public int ImpactId { get; }

        public PendingCellImpact Impact { get; }
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
            CombatOriginAnchor? lockedCombatAnchor = null,
            SurfaceCell? lockedTargetCell = null)
        {
            if (kind == EnemyActionKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Enemy actions must start with a concrete action kind.");
            }

            if (windupTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windupTicks), "Enemy action wind-up ticks must be zero or greater.");
            }

            var state = new EnemyActionRuntimeState
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

            if (lockedCombatAnchor.HasValue && lockedTargetCell.HasValue)
            {
                state.hasLockedForwardCellImpact = true;
                state.lockedAttackBaseCell = lockedCombatAnchor.Value.AnchorCell;
                state.lockedAttackDirection = direction;
                state.lockedTargetCell = lockedTargetCell.Value;
            }

            return state;
        }

        public static Direction ResolveAuthoritativeFacing(in EnemyActionRuntimeState state)
        {
            if (state.kind == EnemyActionKind.ForwardCellProjectile &&
                state.hasLockedForwardCellImpact &&
                state.lockedAttackDirection != Direction.None)
            {
                return state.lockedAttackDirection;
            }

            return state.direction;
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
