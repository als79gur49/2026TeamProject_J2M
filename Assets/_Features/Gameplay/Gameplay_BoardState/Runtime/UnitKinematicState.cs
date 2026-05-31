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
        Held = 7,
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
            else if (normalized.mode == MotionMode.Held)
            {
                normalized.velocity = KinematicVelocity2.Zero;
                normalized.forcedOp = ForcedMotionOp.None;
                normalized.speedScalePermille = 0;
            }

            return normalized;
        }

        public static UnitKinematicRuntimeState CreateHeldFreeze(UnitKinematicRuntimeState sourceState)
        {
            var normalizedSource = sourceState.NormalizedForStorage();
            return new UnitKinematicRuntimeState
            {
                localOffset = normalizedSource.localOffset,
                velocity = KinematicVelocity2.Zero,
                mode = MotionMode.Held,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = normalizedSource.remainingDistanceUnits,
                remainingTicks = normalizedSource.remainingTicks,
                speedScalePermille = 0,
                sequenceId = normalizedSource.sequenceId + 1,
                elapsedTicks = normalizedSource.elapsedTicks,
                totalTicks = normalizedSource.totalTicks,
                commitTick = normalizedSource.commitTick,
                startedTick = normalizedSource.startedTick,
                stepDirectionX = normalizedSource.stepDirectionX,
                stepDirectionY = normalizedSource.stepDirectionY,
            }.NormalizedForStorage();
        }

        public static UnitKinematicRuntimeState CreateVoluntaryResumeFromHeld(
            UnitKinematicRuntimeState sourceState,
            KinematicVelocity2 velocity)
        {
            var normalizedSource = sourceState.NormalizedForStorage();
            return new UnitKinematicRuntimeState
            {
                localOffset = normalizedSource.localOffset,
                velocity = velocity,
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = normalizedSource.remainingDistanceUnits,
                remainingTicks = normalizedSource.remainingTicks,
                speedScalePermille = 1000,
                sequenceId = normalizedSource.sequenceId + 1,
                elapsedTicks = normalizedSource.elapsedTicks,
                totalTicks = normalizedSource.totalTicks,
                commitTick = normalizedSource.commitTick,
                startedTick = normalizedSource.startedTick,
                stepDirectionX = normalizedSource.stepDirectionX,
                stepDirectionY = normalizedSource.stepDirectionY,
            }.NormalizedForStorage();
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

    public enum EntityLocomotionLeaseOwnerKind
    {
        None = 0,
        CombatAction = 1,
        MovementSkill = 2,
        UtilityState = 3,
        SystemCleanup = 4,
    }

    public enum EntityLocomotionLeaseStateKind
    {
        None = 0,
        HeldByOwner = 1,
        ReleaseRequested = 2,
        Settling = 3,
        Completed = 4,
        Orphaned = 5,
    }

    public enum EntityLocomotionLeaseReleaseReason
    {
        None = 0,
        ExecuteComplete = 1,
        ForwardCellProjectileExecuteComplete = 2,
        RecoverEntered = 3,
        RecoverComplete = 4,
        ActionTimelineComplete = 5,
        TopologyNonParticipant = 6,
        NoCombatCapability = 7,
        SourceInactive = 8,
        AiModeNonAttack = 9,
        ActionLogicClear = 10,
        EntityRemoved = 11,
        StageReset = 12,
    }

    public enum EntityLocomotionLeaseReleasePolicy
    {
        ResumeCapturedVoluntary = 0,
        SettleToAnchor = 1,
        ForceSettledAtCurrentAnchor = 2,
        ClearStaleHold = 3,
        RemoveWithEntity = 4,
    }

    public struct EntityLocomotionLeaseState : IEquatable<EntityLocomotionLeaseState>
    {
        public int leaseId;
        public int entityId;
        public EntityLocomotionLeaseOwnerKind ownerKind;
        public EntityLocomotionLeaseStateKind stateKind;
        public int ownerActionSequenceId;
        public UnitKinematicRuntimeState capturedKinematic;
        public SurfaceCell anchorAtAcquire;
        public int acquiredTick;
        public int lastReleaseTick;
        public EntityLocomotionLeaseReleaseReason lastReleaseReason;
        public EntityLocomotionLeaseReleaseReason pendingReleaseReason;
        public EntityLocomotionLeaseReleaseReason finalReleaseReason;

        public static EntityLocomotionLeaseState None => default;

        public bool IsOmittable =>
            leaseId <= 0 ||
            entityId <= 0 ||
            ownerKind == EntityLocomotionLeaseOwnerKind.None ||
            stateKind == EntityLocomotionLeaseStateKind.None;

        public bool IsTerminal =>
            stateKind == EntityLocomotionLeaseStateKind.Completed ||
            stateKind == EntityLocomotionLeaseStateKind.Orphaned;

        public bool IsActive =>
            stateKind == EntityLocomotionLeaseStateKind.HeldByOwner ||
            stateKind == EntityLocomotionLeaseStateKind.ReleaseRequested ||
            stateKind == EntityLocomotionLeaseStateKind.Settling;

        public EntityLocomotionLeaseState NormalizedForStorage()
        {
            var normalized = this;
            normalized.leaseId = Math.Max(0, normalized.leaseId);
            normalized.entityId = Math.Max(0, normalized.entityId);
            normalized.ownerActionSequenceId = Math.Max(0, normalized.ownerActionSequenceId);
            normalized.capturedKinematic = normalized.capturedKinematic.NormalizedForStorage();
            normalized.acquiredTick = Math.Max(0, normalized.acquiredTick);
            normalized.lastReleaseTick = Math.Max(0, normalized.lastReleaseTick);
            if (normalized.IsOmittable)
            {
                return default;
            }

            return normalized;
        }

        public bool Equals(EntityLocomotionLeaseState other)
        {
            return leaseId == other.leaseId &&
                   entityId == other.entityId &&
                   ownerKind == other.ownerKind &&
                   stateKind == other.stateKind &&
                   ownerActionSequenceId == other.ownerActionSequenceId &&
                   capturedKinematic.Equals(other.capturedKinematic) &&
                   anchorAtAcquire.Equals(other.anchorAtAcquire) &&
                   acquiredTick == other.acquiredTick &&
                   lastReleaseTick == other.lastReleaseTick &&
                   lastReleaseReason == other.lastReleaseReason &&
                   pendingReleaseReason == other.pendingReleaseReason &&
                   finalReleaseReason == other.finalReleaseReason;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityLocomotionLeaseState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = leaseId;
                hashCode = (hashCode * 397) ^ entityId;
                hashCode = (hashCode * 397) ^ (int)ownerKind;
                hashCode = (hashCode * 397) ^ (int)stateKind;
                hashCode = (hashCode * 397) ^ ownerActionSequenceId;
                hashCode = (hashCode * 397) ^ capturedKinematic.GetHashCode();
                hashCode = (hashCode * 397) ^ anchorAtAcquire.GetHashCode();
                hashCode = (hashCode * 397) ^ acquiredTick;
                hashCode = (hashCode * 397) ^ lastReleaseTick;
                hashCode = (hashCode * 397) ^ (int)lastReleaseReason;
                hashCode = (hashCode * 397) ^ (int)pendingReleaseReason;
                hashCode = (hashCode * 397) ^ (int)finalReleaseReason;
                return hashCode;
            }
        }
    }

    public readonly struct EntityLocomotionLeaseDiagnosticContext
    {
        public EntityLocomotionLeaseDiagnosticContext(
            string operation,
            EntityLocomotionLeaseStateKind stateBefore,
            EntityLocomotionLeaseStateKind stateAfter,
            EntityLocomotionLeaseReleaseReason reason,
            EntityLocomotionLeaseReleaseReason pendingReleaseReason,
            EntityLocomotionLeaseReleaseReason finalReleaseReason,
            string policy,
            int recoverRemainingTicks,
            bool recoverComplete,
            bool actualKinematicReleaseEmitted,
            MotionMode kinematicModeBefore,
            MotionMode kinematicModeAfter,
            bool hasAuthoritativeState,
            bool isSettledAtAnchor,
            int tickIndex)
        {
            HasValue = true;
            Operation = operation ?? string.Empty;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            Reason = reason;
            PendingReleaseReason = pendingReleaseReason;
            FinalReleaseReason = finalReleaseReason;
            Policy = policy ?? string.Empty;
            RecoverRemainingTicks = Math.Max(0, recoverRemainingTicks);
            RecoverComplete = recoverComplete;
            ActualKinematicReleaseEmitted = actualKinematicReleaseEmitted;
            KinematicModeBefore = kinematicModeBefore;
            KinematicModeAfter = kinematicModeAfter;
            HasAuthoritativeState = hasAuthoritativeState;
            IsSettledAtAnchor = isSettledAtAnchor;
            TickIndex = Math.Max(0, tickIndex);
        }

        public bool HasValue { get; }

        public string Operation { get; }

        public EntityLocomotionLeaseStateKind StateBefore { get; }

        public EntityLocomotionLeaseStateKind StateAfter { get; }

        public EntityLocomotionLeaseReleaseReason Reason { get; }

        public EntityLocomotionLeaseReleaseReason PendingReleaseReason { get; }

        public EntityLocomotionLeaseReleaseReason FinalReleaseReason { get; }

        public string Policy { get; }

        public int RecoverRemainingTicks { get; }

        public bool RecoverComplete { get; }

        public bool ActualKinematicReleaseEmitted { get; }

        public MotionMode KinematicModeBefore { get; }

        public MotionMode KinematicModeAfter { get; }

        public bool HasAuthoritativeState { get; }

        public bool IsSettledAtAnchor { get; }

        public int TickIndex { get; }
    }

    internal readonly struct EntityLocomotionLeaseSnapshotEntry
    {
        public EntityLocomotionLeaseSnapshotEntry(int entityId, EntityLocomotionLeaseState state)
        {
            EntityId = entityId;
            State = state.NormalizedForStorage();
        }

        public int EntityId { get; }

        public EntityLocomotionLeaseState State { get; }
    }

    internal static class EntityLocomotionLeaseDiagnostics
    {
        private const string Prefix = "[EntityLocomotionLease]";

        public static string FormatOperation(
            int tickIndex,
            int entityId,
            string operation,
            EntityLocomotionLeaseOwnerKind ownerKind,
            int ownerSequence,
            int leaseId,
            EntityLocomotionLeaseStateKind stateBefore,
            EntityLocomotionLeaseStateKind stateAfter,
            EntityLocomotionLeaseReleaseReason reason,
            EntityLocomotionLeaseReleaseReason pendingReleaseReason,
            EntityLocomotionLeaseReleaseReason finalReleaseReason,
            string policy,
            int recoverRemainingTicks,
            bool recoverComplete,
            bool actualKinematicReleaseEmitted,
            MotionMode kinematicModeBefore,
            MotionMode kinematicModeAfter,
            bool hasAuthoritativeState,
            bool isSettledAtAnchor,
            int lastReleaseTick,
            EntityLocomotionLeaseReleaseReason lastReleaseReason)
        {
            return $"{Prefix} Tick={tickIndex} Entity={entityId} Operation={operation} OwnerKind={ownerKind} OwnerSeq={ownerSequence} LeaseId={leaseId} StateBefore={stateBefore} StateAfter={stateAfter} Reason={reason} PendingReleaseReason={pendingReleaseReason} FinalReleaseReason={finalReleaseReason} Policy={policy ?? string.Empty} RecoverRemainingTicks={recoverRemainingTicks} RecoverComplete={(recoverComplete ? 1 : 0)} ActualKinematicReleaseEmitted={(actualKinematicReleaseEmitted ? 1 : 0)} KinematicModeBefore={kinematicModeBefore} KinematicModeAfter={kinematicModeAfter} HasAuthoritativeState={hasAuthoritativeState} IsSettledAtAnchor={isSettledAtAnchor} LastReleaseTick={lastReleaseTick} LastReleaseReason={lastReleaseReason}";
        }

        public static string BuildKinematicNotSettledDiagnostic(
            WorldSnapshot snapshot,
            int tickIndex,
            int entityId,
            string stage,
            int intentId = 0)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                pose.IsSettledAtAnchor)
            {
                return string.Empty;
            }

            var hasLease = snapshot.TryGetEntityLocomotionLeaseState(entityId, out var lease);
            var hasActiveLeaseLifecycle = hasLease && lease.IsActive;
            var hasActiveKinematicSettleOperation = pose.Mode != MotionMode.Held &&
                                                   pose.Mode != MotionMode.Settled &&
                                                   pose.Mode != MotionMode.LegacyDiscrete;
            if (hasActiveLeaseLifecycle || hasActiveKinematicSettleOperation)
            {
                return string.Empty;
            }

            var ownerKind = hasLease ? lease.ownerKind : EntityLocomotionLeaseOwnerKind.None;
            var ownerSequence = hasLease ? lease.ownerActionSequenceId : 0;
            var leaseId = hasLease ? lease.leaseId : 0;
            var stateBefore = hasLease ? lease.stateKind : EntityLocomotionLeaseStateKind.None;
            var lastReleaseTick = hasLease ? lease.lastReleaseTick : 0;
            var lastReleaseReason = hasLease ? lease.lastReleaseReason : EntityLocomotionLeaseReleaseReason.None;
            var pendingReleaseReason = hasLease ? lease.pendingReleaseReason : EntityLocomotionLeaseReleaseReason.None;
            var finalReleaseReason = hasLease ? lease.finalReleaseReason : EntityLocomotionLeaseReleaseReason.None;
            return FormatOperation(
                tickIndex,
                entityId,
                "OrphanedKinematicNotSettled",
                ownerKind,
                ownerSequence,
                leaseId,
                stateBefore,
                EntityLocomotionLeaseStateKind.Orphaned,
                lastReleaseReason,
                pendingReleaseReason,
                finalReleaseReason,
                stage,
                0,
                false,
                false,
                pose.Mode,
                pose.Mode,
                pose.HasAuthoritativeState,
                pose.IsSettledAtAnchor,
                lastReleaseTick,
                lastReleaseReason) +
                (intentId > 0 ? $" Intent={intentId}" : string.Empty);
        }
    }
}
