using System;

namespace Game.Feature.Gameplay.BoardState
{
    public enum ContinuousLocomotionMode
    {
        Idle = 0,
        Moving = 1,
        AlignToAnchor = 2,
    }

    public struct UnitContinuousLocomotionState : IEquatable<UnitContinuousLocomotionState>
    {
        public KinematicOffset2 localOffset;
        public KinematicVelocity2 velocity;
        public Direction facing;
        public Direction? lastMoveDirection;
        public int speedUnitsPerTick;
        public ContinuousLocomotionMode mode;
        public int sequenceId;
        public int subUnitRemainderX;
        public int subUnitRemainderY;

        public static UnitContinuousLocomotionState SettledZero => default;

        // Idle-zero is canonical absent state; residual, sequence, speed, and facing metadata
        // are progression-only once the pose is exactly settled at the anchor.
        public bool IsOmittableIdleZero =>
            localOffset.IsZero &&
            velocity.IsZero &&
            mode == ContinuousLocomotionMode.Idle;

        public bool IsSettledAtAnchor =>
            localOffset.IsZero &&
            velocity.IsZero &&
            mode == ContinuousLocomotionMode.Idle;

        public UnitContinuousLocomotionState NormalizedForStorage()
        {
            var normalized = this;
            normalized.localOffset = normalized.localOffset.ClampToLocalOffsetRange();
            normalized.speedUnitsPerTick = Math.Max(0, normalized.speedUnitsPerTick);
            normalized.sequenceId = Math.Max(0, normalized.sequenceId);
            normalized.subUnitRemainderX = Math.Max(0, normalized.subUnitRemainderX);
            normalized.subUnitRemainderY = Math.Max(0, normalized.subUnitRemainderY);
            normalized.facing = NormalizeDirection(normalized.facing);
            if (normalized.lastMoveDirection.HasValue)
            {
                var lastMoveDirection = NormalizeDirection(normalized.lastMoveDirection.Value);
                normalized.lastMoveDirection = lastMoveDirection == Direction.None
                    ? null
                    : lastMoveDirection;
            }

            if (normalized.localOffset.IsZero && normalized.velocity.IsZero)
            {
                normalized.mode = ContinuousLocomotionMode.Idle;
            }
            else if (normalized.mode == ContinuousLocomotionMode.Idle)
            {
                normalized.velocity = KinematicVelocity2.Zero;
            }
            else if (normalized.velocity.IsZero)
            {
                normalized.mode = ContinuousLocomotionMode.Idle;
            }

            return normalized;
        }

        public static UnitContinuousLocomotionState CreateIdleFreeze(UnitContinuousLocomotionState sourceState)
        {
            var normalizedSource = sourceState.NormalizedForStorage();
            return new UnitContinuousLocomotionState
            {
                localOffset = normalizedSource.localOffset,
                velocity = KinematicVelocity2.Zero,
                facing = normalizedSource.facing,
                lastMoveDirection = normalizedSource.lastMoveDirection,
                speedUnitsPerTick = normalizedSource.speedUnitsPerTick,
                mode = ContinuousLocomotionMode.Idle,
                sequenceId = normalizedSource.sequenceId + 1,
                subUnitRemainderX = normalizedSource.subUnitRemainderX,
                subUnitRemainderY = normalizedSource.subUnitRemainderY,
            }.NormalizedForStorage();
        }

        public bool Equals(UnitContinuousLocomotionState other)
        {
            return localOffset.Equals(other.localOffset) &&
                   velocity.Equals(other.velocity) &&
                   facing == other.facing &&
                   lastMoveDirection == other.lastMoveDirection &&
                   speedUnitsPerTick == other.speedUnitsPerTick &&
                   mode == other.mode &&
                   sequenceId == other.sequenceId &&
                   subUnitRemainderX == other.subUnitRemainderX &&
                   subUnitRemainderY == other.subUnitRemainderY;
        }

        public override bool Equals(object obj)
        {
            return obj is UnitContinuousLocomotionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = localOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ velocity.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)facing;
                hashCode = (hashCode * 397) ^ (lastMoveDirection.HasValue ? (int)lastMoveDirection.Value : -1);
                hashCode = (hashCode * 397) ^ speedUnitsPerTick;
                hashCode = (hashCode * 397) ^ (int)mode;
                hashCode = (hashCode * 397) ^ sequenceId;
                hashCode = (hashCode * 397) ^ subUnitRemainderX;
                hashCode = (hashCode * 397) ^ subUnitRemainderY;
                return hashCode;
            }
        }

        private static Direction NormalizeDirection(Direction direction)
        {
            return direction == Direction.Up ||
                   direction == Direction.Right ||
                   direction == Direction.Down ||
                   direction == Direction.Left
                ? direction
                : Direction.None;
        }
    }

    public readonly struct UnitContinuousLocomotionPose
    {
        public UnitContinuousLocomotionPose(
            SurfaceCell anchorCell,
            UnitContinuousLocomotionState state,
            bool hasAuthoritativeState)
        {
            AnchorCell = anchorCell;
            State = state.NormalizedForStorage();
            HasAuthoritativeState = hasAuthoritativeState && !State.IsOmittableIdleZero;
        }

        public SurfaceCell AnchorCell { get; }

        public UnitContinuousLocomotionState State { get; }

        public bool HasAuthoritativeState { get; }

        public KinematicOffset2 LocalOffset => State.localOffset;

        public ContinuousLocomotionMode Mode => State.mode;

        public bool IsSettledAtAnchor => State.IsSettledAtAnchor;
    }

    internal readonly struct UnitContinuousLocomotionSnapshotEntry
    {
        public UnitContinuousLocomotionSnapshotEntry(int entityId, UnitContinuousLocomotionState state)
        {
            EntityId = entityId;
            State = state.NormalizedForStorage();
        }

        public int EntityId { get; }

        public UnitContinuousLocomotionState State { get; }
    }
}
