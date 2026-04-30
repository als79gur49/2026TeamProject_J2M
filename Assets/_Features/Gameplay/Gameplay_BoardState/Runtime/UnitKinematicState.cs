using System;

namespace Game.Feature.Gameplay.BoardState
{
    public enum MotionMode
    {
        Settled = 0,
        Voluntary = 1,
        Forced = 2,
        Interrupted = 3,
        InteractionLocked = 4,
        LegacyDiscrete = 5,
        Charge = 6,
    }

    public enum ForcedMotionOp
    {
        None = 0,
        Knockback = 1,
        Rebound = 2,
        Pull = 3,
        Dash = 4,
        Charge = 5,
    }

    public enum MotionInterruptPolicy
    {
        None = 0,
        FreezeCurrentPose = 1,
        QueueForcedMotionNextTick = 2,
        RejectVoluntaryMotion = 3,
    }

    public readonly struct KinematicFixed : IEquatable<KinematicFixed>, IComparable<KinematicFixed>
    {
        public const int UnitsPerCell = 4096;
        public const int HalfCellUnits = UnitsPerCell / 2;
        public const int DefaultPlayerUnitsPerTick = UnitsPerCell / 4;
        public const int MaxPositiveLocalOffset = HalfCellUnits - 1;
        public const int MinLocalOffset = -HalfCellUnits;

        public KinematicFixed(int rawValue)
        {
            RawValue = rawValue;
        }

        public int RawValue { get; }

        public static KinematicFixed Zero => default;

        public bool IsZero => RawValue == 0;

        public static KinematicFixed FromRaw(int rawValue)
        {
            return new KinematicFixed(rawValue);
        }

        public static KinematicFixed ClampToLocalOffsetRange(KinematicFixed value)
        {
            return new KinematicFixed(Math.Max(MinLocalOffset, Math.Min(MaxPositiveLocalOffset, value.RawValue)));
        }

        public static bool IsRepresentableLocalOffset(KinematicFixed value)
        {
            return value.RawValue >= MinLocalOffset && value.RawValue <= MaxPositiveLocalOffset;
        }

        public int CompareTo(KinematicFixed other)
        {
            return RawValue.CompareTo(other.RawValue);
        }

        public bool Equals(KinematicFixed other)
        {
            return RawValue == other.RawValue;
        }

        public override bool Equals(object obj)
        {
            return obj is KinematicFixed other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RawValue;
        }

        public override string ToString()
        {
            return RawValue.ToString();
        }

        public static KinematicFixed operator +(KinematicFixed left, KinematicFixed right)
        {
            return new KinematicFixed(checked(left.RawValue + right.RawValue));
        }

        public static KinematicFixed operator -(KinematicFixed left, KinematicFixed right)
        {
            return new KinematicFixed(checked(left.RawValue - right.RawValue));
        }

        public static bool operator ==(KinematicFixed left, KinematicFixed right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KinematicFixed left, KinematicFixed right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct KinematicOffset2 : IEquatable<KinematicOffset2>
    {
        public KinematicOffset2(KinematicFixed x, KinematicFixed y)
        {
            X = x;
            Y = y;
        }

        public KinematicFixed X { get; }

        public KinematicFixed Y { get; }

        public static KinematicOffset2 Zero => default;

        public bool IsZero => X.IsZero && Y.IsZero;

        public bool IsRepresentableLocalOffset =>
            KinematicFixed.IsRepresentableLocalOffset(X) &&
            KinematicFixed.IsRepresentableLocalOffset(Y);

        public KinematicOffset2 ClampToLocalOffsetRange()
        {
            return new KinematicOffset2(
                KinematicFixed.ClampToLocalOffsetRange(X),
                KinematicFixed.ClampToLocalOffsetRange(Y));
        }

        public bool Equals(KinematicOffset2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is KinematicOffset2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({X.RawValue},{Y.RawValue})";
        }
    }

    public readonly struct KinematicVelocity2 : IEquatable<KinematicVelocity2>
    {
        public KinematicVelocity2(KinematicFixed x, KinematicFixed y)
        {
            X = x;
            Y = y;
        }

        public KinematicFixed X { get; }

        public KinematicFixed Y { get; }

        public static KinematicVelocity2 Zero => default;

        public bool IsZero => X.IsZero && Y.IsZero;

        public bool Equals(KinematicVelocity2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is KinematicVelocity2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({X.RawValue},{Y.RawValue})";
        }
    }

    public struct UnitKinematicRuntimeState : IEquatable<UnitKinematicRuntimeState>
    {
        public KinematicOffset2 localOffset;
        public KinematicVelocity2 velocity;
        public MotionMode mode;
        public ForcedMotionOp forcedOp;
        public int remainingDistanceUnits;
        public int remainingTicks;
        public int speedScalePermille;
        public int sequenceId;
        public int elapsedTicks;
        public int totalTicks;
        public int commitTick;
        public int startedTick;
        public int stepDirectionX;
        public int stepDirectionY;

        public static UnitKinematicRuntimeState SettledZero => default;

        public bool IsSettledZero =>
            localOffset.IsZero &&
            velocity.IsZero &&
            mode == MotionMode.Settled &&
            forcedOp == ForcedMotionOp.None &&
            remainingDistanceUnits == 0 &&
            remainingTicks == 0 &&
            speedScalePermille == 0 &&
            sequenceId == 0 &&
            elapsedTicks == 0 &&
            totalTicks == 0 &&
            commitTick == 0 &&
            startedTick == 0 &&
            stepDirectionX == 0 &&
            stepDirectionY == 0;

        public bool IsSettledAtAnchor =>
            localOffset.IsZero &&
            (mode == MotionMode.Settled || mode == MotionMode.LegacyDiscrete);

        public UnitKinematicRuntimeState NormalizedForStorage()
        {
            var normalized = this;
            normalized.localOffset = normalized.localOffset.ClampToLocalOffsetRange();
            normalized.remainingDistanceUnits = Math.Max(0, normalized.remainingDistanceUnits);
            normalized.remainingTicks = Math.Max(0, normalized.remainingTicks);
            normalized.speedScalePermille = Math.Max(0, normalized.speedScalePermille);
            normalized.sequenceId = Math.Max(0, normalized.sequenceId);
            normalized.elapsedTicks = Math.Max(0, normalized.elapsedTicks);
            normalized.totalTicks = Math.Max(0, normalized.totalTicks);
            normalized.commitTick = Math.Max(0, normalized.commitTick);
            normalized.startedTick = Math.Max(0, normalized.startedTick);
            normalized.stepDirectionX = Math.Max(-1, Math.Min(1, normalized.stepDirectionX));
            normalized.stepDirectionY = Math.Max(-1, Math.Min(1, normalized.stepDirectionY));
            if (normalized.mode == MotionMode.Settled)
            {
                normalized.velocity = KinematicVelocity2.Zero;
                normalized.forcedOp = ForcedMotionOp.None;
                normalized.remainingDistanceUnits = 0;
                normalized.remainingTicks = 0;
                normalized.elapsedTicks = 0;
                normalized.totalTicks = 0;
                normalized.commitTick = 0;
                normalized.startedTick = 0;
                normalized.stepDirectionX = 0;
                normalized.stepDirectionY = 0;
            }
            else if (normalized.mode == MotionMode.Interrupted)
            {
                normalized.velocity = KinematicVelocity2.Zero;
                normalized.forcedOp = ForcedMotionOp.None;
                normalized.remainingDistanceUnits = 0;
                normalized.remainingTicks = 0;
                normalized.speedScalePermille = 0;
                normalized.elapsedTicks = 0;
                normalized.totalTicks = 0;
                normalized.commitTick = 0;
                normalized.startedTick = 0;
                normalized.stepDirectionX = 0;
                normalized.stepDirectionY = 0;
            }

            return normalized;
        }

        public static UnitKinematicRuntimeState CreateInterruptedFreeze(UnitKinematicRuntimeState sourceState)
        {
            var normalizedSource = sourceState.NormalizedForStorage();
            return new UnitKinematicRuntimeState
            {
                localOffset = normalizedSource.localOffset,
                velocity = KinematicVelocity2.Zero,
                mode = MotionMode.Interrupted,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = 0,
                remainingTicks = 0,
                speedScalePermille = 0,
                sequenceId = normalizedSource.sequenceId + 1,
                elapsedTicks = 0,
                totalTicks = 0,
                commitTick = 0,
                startedTick = 0,
                stepDirectionX = 0,
                stepDirectionY = 0,
            }.NormalizedForStorage();
        }

        public bool Equals(UnitKinematicRuntimeState other)
        {
            return localOffset.Equals(other.localOffset) &&
                   velocity.Equals(other.velocity) &&
                   mode == other.mode &&
                   forcedOp == other.forcedOp &&
                   remainingDistanceUnits == other.remainingDistanceUnits &&
                   remainingTicks == other.remainingTicks &&
                   speedScalePermille == other.speedScalePermille &&
                   sequenceId == other.sequenceId &&
                   elapsedTicks == other.elapsedTicks &&
                   totalTicks == other.totalTicks &&
                   commitTick == other.commitTick &&
                   startedTick == other.startedTick &&
                   stepDirectionX == other.stepDirectionX &&
                   stepDirectionY == other.stepDirectionY;
        }

        public override bool Equals(object obj)
        {
            return obj is UnitKinematicRuntimeState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = localOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ velocity.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)mode;
                hashCode = (hashCode * 397) ^ (int)forcedOp;
                hashCode = (hashCode * 397) ^ remainingDistanceUnits;
                hashCode = (hashCode * 397) ^ remainingTicks;
                hashCode = (hashCode * 397) ^ speedScalePermille;
                hashCode = (hashCode * 397) ^ sequenceId;
                hashCode = (hashCode * 397) ^ elapsedTicks;
                hashCode = (hashCode * 397) ^ totalTicks;
                hashCode = (hashCode * 397) ^ commitTick;
                hashCode = (hashCode * 397) ^ startedTick;
                hashCode = (hashCode * 397) ^ stepDirectionX;
                hashCode = (hashCode * 397) ^ stepDirectionY;
                return hashCode;
            }
        }
    }

    public readonly struct UnitKinematicPose
    {
        public UnitKinematicPose(SurfaceCell anchorCell, UnitKinematicRuntimeState state, bool hasAuthoritativeState)
        {
            AnchorCell = anchorCell;
            State = state.NormalizedForStorage();
            HasAuthoritativeState = hasAuthoritativeState && !State.IsSettledZero;
        }

        public SurfaceCell AnchorCell { get; }

        public UnitKinematicRuntimeState State { get; }

        public bool HasAuthoritativeState { get; }

        public KinematicOffset2 LocalOffset => State.localOffset;

        public MotionMode Mode => State.mode;

        public bool IsSettledAtAnchor => State.IsSettledAtAnchor;
    }

    internal readonly struct UnitKinematicSnapshotEntry
    {
        public UnitKinematicSnapshotEntry(int entityId, UnitKinematicRuntimeState state)
        {
            EntityId = entityId;
            State = state.NormalizedForStorage();
        }

        public int EntityId { get; }

        public UnitKinematicRuntimeState State { get; }
    }
}
