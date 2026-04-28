using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public enum TickEntityMotionKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        ProjectileMove = 4,
        BoxSlide = 5,
        ChargeMove = 6,
    }

    public enum TickVisibilityChangeKind
    {
        None = 0,
        Spawn = 1,
        Detach = 2,
        Remove = 3,
    }

    public enum TickTransitionVisibilityMode
    {
        None = 0,
        RetainUntilTransitionComplete = 1,
        ShowAtTransitionStart = 2,
    }

    public readonly struct TickEntityMotion
    {
        // Motion records describe a render transition between already-committed logical cells.
        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
            : this(
                entityId,
                motionKind,
                sourceCell,
                destinationCell,
                sourceTopology: null,
                destinationTopology: null,
                sourceFacing: null,
                destinationFacing: null)
        {
        }

        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            CubeTopologyState? sourceTopology,
            CubeTopologyState? destinationTopology,
            Direction? sourceFacing,
            Direction? destinationFacing)
        {
            EntityId = entityId;
            MotionKind = motionKind;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            SourceFacing = sourceFacing;
            DestinationFacing = destinationFacing;
        }

        public int EntityId { get; }

        public TickEntityMotionKind MotionKind { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public CubeTopologyState? SourceTopology { get; }

        public CubeTopologyState? DestinationTopology { get; }

        public Direction? SourceFacing { get; }

        public Direction? DestinationFacing { get; }
    }

    public enum TickKinematicMotionTerminalKind
    {
        None = 0,
        Interrupted = 1,
        Removed = 2,
    }

    public readonly struct TickKinematicMotionTrack
    {
        public TickKinematicMotionTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell destinationAnchorCell,
            KinematicOffset2 destinationLocalOffset,
            MotionMode motionMode,
            ForcedMotionOp forcedMotionOp,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None)
        {
            EntityId = entityId;
            SourceAnchorCell = sourceAnchorCell;
            SourceLocalOffset = sourceLocalOffset;
            DestinationAnchorCell = destinationAnchorCell;
            DestinationLocalOffset = destinationLocalOffset;
            MotionMode = motionMode;
            ForcedMotionOp = forcedMotionOp;
            TerminalKind = terminalKind;
        }

        public int EntityId { get; }

        public SurfaceCell SourceAnchorCell { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public SurfaceCell DestinationAnchorCell { get; }

        public KinematicOffset2 DestinationLocalOffset { get; }

        public MotionMode MotionMode { get; }

        public ForcedMotionOp ForcedMotionOp { get; }

        public TickKinematicMotionTerminalKind TerminalKind { get; }
    }

    public readonly struct TickTopologyMotion
    {
        public TickTopologyMotion(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
        }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }
    }

    public readonly struct TickVisibilityChange
    {
        public TickVisibilityChange(
            int entityId,
            TickVisibilityChangeKind changeKind,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing)
        {
            EntityId = entityId;
            ChangeKind = changeKind;
            Cell = cell;
            Topology = topology;
            Facing = facing;
        }

        public int EntityId { get; }

        public TickVisibilityChangeKind ChangeKind { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }
    }

    public readonly struct TickTransitionVisibilityChange
    {
        public TickTransitionVisibilityChange(
            int entityId,
            TickTransitionVisibilityMode mode,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing)
        {
            EntityId = entityId;
            Mode = mode;
            Cell = cell;
            Topology = topology;
            Facing = facing;
        }

        public int EntityId { get; }

        public TickTransitionVisibilityMode Mode { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }
    }

    public readonly struct TickSummonedEnemyPresentationBinding
    {
        public TickSummonedEnemyPresentationBinding(
            int entityId,
            bool hasEnemyDefinitionBinding,
            EnemyUnitArchetypeId archetypeId)
        {
            EntityId = entityId;
            HasEnemyDefinitionBinding = hasEnemyDefinitionBinding;
            ArchetypeId = archetypeId;
        }

        public int EntityId { get; }

        public bool HasEnemyDefinitionBinding { get; }

        public EnemyUnitArchetypeId ArchetypeId { get; }
    }

    public readonly struct TickSummonWindupWarningSignal
    {
        public TickSummonWindupWarningSignal(
            int sourceEntityId,
            int effectIndex,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            Direction facing,
            int windupStartTick,
            int windupEndTick,
            int activationSequence,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            EffectIndex = effectIndex;
            SourceCell = sourceCell;
            Topology = topology;
            Facing = facing;
            WindupStartTick = windupStartTick;
            WindupEndTick = windupEndTick;
            ActivationSequence = activationSequence;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public int EffectIndex { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public int WindupStartTick { get; }

        public int WindupEndTick { get; }

        public int ActivationSequence { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public enum FrontFaceShieldBlockMovementKind
    {
        Unknown = 0,
        PushStart = 1,
        SlidingContinuation = 2,
    }

    public readonly struct TickFrontFaceShieldSourceSignal
    {
        public TickFrontFaceShieldSourceSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            SourceCell = sourceCell;
            Topology = topology;
            Radius = radius;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public int Radius { get; }

        public bool IncludeSourceCell { get; }

        public FrontFaceShieldTargetPattern TargetPattern { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public readonly struct TickFrontFaceShieldWindupWarningSignal
    {
        public TickFrontFaceShieldWindupWarningSignal(
            int sourceEntityId,
            int effectIndex,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int windupStartTick,
            int windupEndTick,
            int activationSequence,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            EffectIndex = effectIndex;
            SourceCell = sourceCell;
            Topology = topology;
            Radius = radius;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            WindupStartTick = windupStartTick;
            WindupEndTick = windupEndTick;
            ActivationSequence = activationSequence;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public int EffectIndex { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public int Radius { get; }

        public bool IncludeSourceCell { get; }

        public FrontFaceShieldTargetPattern TargetPattern { get; }

        public int WindupStartTick { get; }

        public int WindupEndTick { get; }

        public int ActivationSequence { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public readonly struct TickFrontFaceShieldBlockSignal
    {
        public TickFrontFaceShieldBlockSignal(
            int shieldSourceEntityId,
            int boxEntityId,
            int actorEntityId,
            SurfaceCell blockedCell,
            SurfaceCell shieldSourceCell,
            FrontFaceShieldBlockMovementKind movementKind,
            CubeTopologyState topology,
            int tickIndex,
            int presentationSeed)
        {
            ShieldSourceEntityId = shieldSourceEntityId;
            BoxEntityId = boxEntityId;
            ActorEntityId = actorEntityId;
            BlockedCell = blockedCell;
            ShieldSourceCell = shieldSourceCell;
            MovementKind = movementKind;
            Topology = topology;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int ShieldSourceEntityId { get; }

        public int BoxEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell BlockedCell { get; }

        public SurfaceCell ShieldSourceCell { get; }

        public FrontFaceShieldBlockMovementKind MovementKind { get; }

        public CubeTopologyState Topology { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public readonly struct TickPlayerActionPresentationSignal
    {
        public TickPlayerActionPresentationSignal(
            int entityId,
            PlayerActionKind activeActionKind,
            int activeActionSequence,
            bool startedThisTick,
            bool completedThisTick,
            bool canceledThisTick,
            bool executedThisTick = false,
            bool isRecoveryPhase = false,
            TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.None,
            int targetEntityId = 0,
            Direction direction = Direction.None,
            int actionPlanId = 0,
            TickPlayerFlipOutcomeKind flipOutcome = TickPlayerFlipOutcomeKind.None,
            bool hasFlipImpactContactTiming = false,
            int flipTargetBoxEntityId = 0)
        {
            EntityId = entityId;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            StartedThisTick = startedThisTick;
            ExecutedThisTick = executedThisTick;
            IsRecoveryPhase = isRecoveryPhase;
            ResolutionKind = resolutionKind;
            CompletedThisTick = completedThisTick;
            CanceledThisTick = canceledThisTick;
            TargetEntityId = targetEntityId;
            Direction = direction;
            ActionPlanId = actionPlanId;
            FlipOutcome = flipOutcome;
            HasFlipImpactContactTiming = hasFlipImpactContactTiming;
            FlipTargetBoxEntityId = flipTargetBoxEntityId;
        }

        public int EntityId { get; }

        public PlayerActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public bool StartedThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool IsRecoveryPhase { get; }

        public TickPlayerActionResolutionKind ResolutionKind { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }

        public int ActionPlanId { get; }

        public TickPlayerFlipOutcomeKind FlipOutcome { get; }

        public bool HasFlipImpactContactTiming { get; }

        public int FlipTargetBoxEntityId { get; }
    }

    public enum TickPlayerActionResolutionKind
    {
        None = 0,
        Success = 1,
        Impact = 2,
        Blocked = 3,
    }

    public enum TickPlayerFlipOutcomeKind
    {
        None = 0,
        FollowThrough = 1,
        DestroySelf = 2,
        Stay = 3,
        Blocked = 4,
    }

    public readonly struct TickPlayerLocomotionPresentationSignal
    {
        public TickPlayerLocomotionPresentationSignal(
            int entityId,
            bool shouldPlayWalkLoop,
            bool moveMotionGeneratedThisTick,
            bool waitingForNextMoveCadence,
            Direction inputDirection = Direction.None,
            bool inputIsBuffered = false)
        {
            EntityId = entityId;
            ShouldPlayWalkLoop = shouldPlayWalkLoop;
            MoveMotionGeneratedThisTick = moveMotionGeneratedThisTick;
            WaitingForNextMoveCadence = waitingForNextMoveCadence;
            InputDirection = inputDirection;
            InputIsBuffered = inputIsBuffered;
        }

        public int EntityId { get; }

        public bool ShouldPlayWalkLoop { get; }

        public bool MoveMotionGeneratedThisTick { get; }

        public bool WaitingForNextMoveCadence { get; }

        public Direction InputDirection { get; }

        public bool InputIsBuffered { get; }
    }

    public readonly struct TickPlayerDamagePresentationSignal
    {
        public TickPlayerDamagePresentationSignal(
            int entityId,
            bool tookDamageThisTick,
            int damageAmount)
        {
            EntityId = entityId;
            TookDamageThisTick = tookDamageThisTick;
            DamageAmount = damageAmount;
        }

        public int EntityId { get; }

        public bool TookDamageThisTick { get; }

        public int DamageAmount { get; }
    }

    public enum DeathDirectionHintKind
    {
        AttackerReverse = 1,
        FacingReverse = 2,
        Unknown = 3,
    }

    public readonly struct TickPlayerDeathPresentationSignal
    {
        public TickPlayerDeathPresentationSignal(
            int entityId,
            bool didDieThisTick,
            int sourceEntityId,
            Direction fallbackFacing,
            bool resolvedDamageSourceAvailable,
            int damageAmountAtFatalHit,
            DeathDirectionHintKind deathDirectionHintKind)
        {
            EntityId = entityId;
            DidDieThisTick = didDieThisTick;
            SourceEntityId = sourceEntityId;
            FallbackFacing = fallbackFacing;
            ResolvedDamageSourceAvailable = resolvedDamageSourceAvailable;
            DamageAmountAtFatalHit = damageAmountAtFatalHit;
            DeathDirectionHintKind = deathDirectionHintKind;
        }

        public int EntityId { get; }

        public bool DidDieThisTick { get; }

        public int SourceEntityId { get; }

        public Direction FallbackFacing { get; }

        public bool ResolvedDamageSourceAvailable { get; }

        public int DamageAmountAtFatalHit { get; }

        public DeathDirectionHintKind DeathDirectionHintKind { get; }
    }

    public readonly struct TickEnemyDamagePresentationSignal
    {
        public TickEnemyDamagePresentationSignal(
            int entityId,
            bool tookDamageThisTick,
            int damageAmount)
        {
            EntityId = entityId;
            TookDamageThisTick = tookDamageThisTick;
            DamageAmount = damageAmount;
        }

        public int EntityId { get; }

        public bool TookDamageThisTick { get; }

        public int DamageAmount { get; }
    }

    public readonly struct TickEnemyActionPresentationSignal
    {
        public TickEnemyActionPresentationSignal(
            int entityId,
            EnemyActionKind activeActionKind,
            int activeActionSequence,
            bool startedThisTick,
            bool canceledThisTick,
            bool executedThisTick,
            bool startedRecoveryThisTick)
        {
            EntityId = entityId;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            StartedThisTick = startedThisTick;
            CanceledThisTick = canceledThisTick;
            ExecutedThisTick = executedThisTick;
            StartedRecoveryThisTick = startedRecoveryThisTick;
        }

        public int EntityId { get; }

        public EnemyActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public bool StartedThisTick { get; }

        public bool CanceledThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool StartedRecoveryThisTick { get; }
    }

    public enum TickEnemyJumpPresentationOutcome
    {
        None = 0,
        WindupStarted = 1,
        AirborneStarted = 2,
        Landed = 3,
        Retried = 4,
        CrushedBoxAndLanded = 5,
    }

    public readonly struct TickEnemyJumpPresentationSignal
    {
        public TickEnemyJumpPresentationSignal(
            int entityId,
            int sequence,
            EnemyJumpPhase phase,
            bool startedWindupThisTick,
            bool startedAirborneThisTick,
            bool landedThisTick,
            bool retryThisTick,
            SurfaceCell sourceCell = default,
            SurfaceCell lockedTargetCell = default,
            SurfaceCell presentationTargetCell = default,
            Direction facing = Direction.None,
            int landingTick = 0,
            int remainingAirborneTicks = 0,
            int retryCount = 0,
            TickEnemyJumpPresentationOutcome outcome = TickEnemyJumpPresentationOutcome.None)
        {
            var resolvedOutcome = outcome == TickEnemyJumpPresentationOutcome.None
                ? ResolveOutcome(startedWindupThisTick, startedAirborneThisTick, landedThisTick, retryThisTick)
                : outcome;
            EntityId = entityId;
            Sequence = sequence;
            Phase = phase;
            StartedWindupThisTick = startedWindupThisTick;
            StartedAirborneThisTick = startedAirborneThisTick;
            LandedThisTick = landedThisTick || resolvedOutcome == TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded;
            RetryThisTick = retryThisTick || resolvedOutcome == TickEnemyJumpPresentationOutcome.Retried;
            SourceCell = sourceCell;
            LockedTargetCell = lockedTargetCell;
            PresentationTargetCell = presentationTargetCell;
            Facing = facing;
            LandingTick = landingTick;
            RemainingAirborneTicks = remainingAirborneTicks;
            RetryCount = retryCount;
            Outcome = resolvedOutcome;
        }

        public int EntityId { get; }

        public int Sequence { get; }

        public EnemyJumpPhase Phase { get; }

        public bool StartedWindupThisTick { get; }

        public bool StartedAirborneThisTick { get; }

        public bool LandedThisTick { get; }

        public bool RetryThisTick { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell LockedTargetCell { get; }

        public SurfaceCell PresentationTargetCell { get; }

        public Direction Facing { get; }

        public int LandingTick { get; }

        public int RemainingAirborneTicks { get; }

        public int RetryCount { get; }

        public TickEnemyJumpPresentationOutcome Outcome { get; }

        private static TickEnemyJumpPresentationOutcome ResolveOutcome(
            bool startedWindupThisTick,
            bool startedAirborneThisTick,
            bool landedThisTick,
            bool retryThisTick)
        {
            if (startedWindupThisTick)
            {
                return TickEnemyJumpPresentationOutcome.WindupStarted;
            }

            if (startedAirborneThisTick)
            {
                return TickEnemyJumpPresentationOutcome.AirborneStarted;
            }

            if (landedThisTick)
            {
                return TickEnemyJumpPresentationOutcome.Landed;
            }

            return retryThisTick
                ? TickEnemyJumpPresentationOutcome.Retried
                : TickEnemyJumpPresentationOutcome.None;
        }
    }

    public readonly struct TickEnemyChargePresentationSignal
    {
        public TickEnemyChargePresentationSignal(
            int entityId,
            int sequence,
            EnemyChargePhase phase,
            bool startedWindupThisTick,
            bool startedActiveThisTick,
            bool startedRecoverThisTick,
            Direction lockedDirection = Direction.None)
        {
            EntityId = entityId;
            Sequence = sequence;
            Phase = phase;
            StartedWindupThisTick = startedWindupThisTick;
            StartedActiveThisTick = startedActiveThisTick;
            StartedRecoverThisTick = startedRecoverThisTick;
            LockedDirection = lockedDirection;
        }

        public int EntityId { get; }

        public int Sequence { get; }

        public EnemyChargePhase Phase { get; }

        public bool StartedWindupThisTick { get; }

        public bool StartedActiveThisTick { get; }

        public bool StartedRecoverThisTick { get; }

        public Direction LockedDirection { get; }
    }

    public enum TickEntityExitCause
    {
        None = 0,
        ItemConsume = 1,
        BoxDestroy = 2,
        EnemyDeath = 3,
        OutOfBounds = 4,
        DestroyedByImpact = BoxDestroy,
        Killed = EnemyDeath,
    }

    // Exit signals transfer visual ownership away from the authoritative entity view.
    // Once an exit is committed, the original entity view must not remain visible in
    // the scene just to support a lingering effect; any echo is transient-only.
    public readonly struct TickEntityExitPresentationSignal
    {
        public TickEntityExitPresentationSignal(
            int exitedEntityId,
            TickEntityExitCause exitCause,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            Direction facing,
            EntityType entityType,
            int? sourceActorEntityId = null,
            int? anchorEntityId = null,
            int presentationSeed = 0)
        {
            ExitedEntityId = exitedEntityId;
            ExitCause = exitCause;
            SourceCell = sourceCell;
            Topology = topology;
            Facing = facing;
            EntityType = entityType;
            SourceActorEntityId = sourceActorEntityId;
            AnchorEntityId = anchorEntityId;
            PresentationSeed = presentationSeed;
        }

        public int ExitedEntityId { get; }

        public TickEntityExitCause ExitCause { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public EntityType EntityType { get; }

        public int? SourceActorEntityId { get; }

        public int? AnchorEntityId { get; }

        public int PresentationSeed { get; }
    }

    // Presentation-only transient for current Flip nonlethal destroy-self impact.
    // This signal is render metadata and must not be treated as gameplay truth.
    internal readonly struct TickImpactTransientPresentationSignal
    {
        public TickImpactTransientPresentationSignal(
            int entityId,
            EntityType entityType,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            CubeTopologyState topology,
            Direction facing,
            int presentationSeed)
        {
            EntityId = entityId;
            EntityType = entityType;
            SourceCell = sourceCell;
            ImpactCell = impactCell;
            Topology = topology;
            Facing = facing;
            PresentationSeed = presentationSeed;
        }

        public int EntityId { get; }

        public EntityType EntityType { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ImpactCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public int PresentationSeed { get; }
    }

    public enum FlipImpactPresentationDisposition
    {
        DestroySelf = 1,
        Stay = 2,
    }

    public readonly struct FlipImpactPresentationSignal
    {
        public FlipImpactPresentationSignal(
            int sourceActionPlanId,
            int boxEntityId,
            int impactTargetEntityId,
            int actorEntityId,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            CubeTopologyState topology,
            Direction sourceFacing,
            Direction impactFacing,
            FlipImpactPresentationDisposition disposition,
            bool hasLandingCell = false,
            SurfaceCell landingCell = default)
        {
            SourceActionPlanId = sourceActionPlanId;
            BoxEntityId = boxEntityId;
            ImpactTargetEntityId = impactTargetEntityId;
            ActorEntityId = actorEntityId;
            SourceCell = sourceCell;
            ImpactCell = impactCell;
            Topology = topology;
            SourceFacing = sourceFacing;
            ImpactFacing = impactFacing;
            HasLandingCell = hasLandingCell;
            LandingCell = landingCell;
            Disposition = disposition;
        }

        public int SourceActionPlanId { get; }

        public int BoxEntityId { get; }

        public int ImpactTargetEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ImpactCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction SourceFacing { get; }

        public Direction ImpactFacing { get; }

        public bool HasLandingCell { get; }

        public SurfaceCell LandingCell { get; }

        public FlipImpactPresentationDisposition Disposition { get; }
    }

    public sealed class TickPresentationData
    {
        public static readonly TickPresentationData Empty = new(
            Array.Empty<TickEntityMotion>(),
            topologyMotion: null,
            Array.Empty<TickVisibilityChange>(),
            Array.Empty<TickTransitionVisibilityChange>(),
            Array.Empty<TickPlayerActionPresentationSignal>(),
            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
            Array.Empty<TickPlayerDamagePresentationSignal>(),
            Array.Empty<TickPlayerDeathPresentationSignal>(),
            Array.Empty<TickEnemyDamagePresentationSignal>(),
            Array.Empty<TickEnemyActionPresentationSignal>(),
            Array.Empty<TickEnemyJumpPresentationSignal>(),
            Array.Empty<TickEnemyChargePresentationSignal>(),
            Array.Empty<TickEntityExitPresentationSignal>(),
            Array.Empty<FlipImpactPresentationSignal>());

        private readonly ReadOnlyCollection<TickEntityExitPresentationSignal> _entityExitSignals;
        private ReadOnlyCollection<TickImpactTransientPresentationSignal> _impactTransientSignals;
        private readonly ReadOnlyCollection<FlipImpactPresentationSignal> _flipImpactSignals;
        private readonly ReadOnlyCollection<TickEnemyActionPresentationSignal> _enemyActionSignals;
        private readonly ReadOnlyCollection<TickEnemyDamagePresentationSignal> _enemyDamageSignals;
        private readonly ReadOnlyCollection<TickEnemyJumpPresentationSignal> _enemyJumpSignals;
        private readonly ReadOnlyCollection<TickEnemyChargePresentationSignal> _enemyChargeSignals;
        private readonly ReadOnlyCollection<TickEntityMotion> _entityMotions;
        private readonly ReadOnlyCollection<TickKinematicMotionTrack> _kinematicMotionTracks;
        private ReadOnlyCollection<TickFrontFaceShieldBlockSignal> _frontFaceShieldBlocks;
        private ReadOnlyCollection<TickFrontFaceShieldSourceSignal> _frontFaceShieldSources;
        private ReadOnlyCollection<TickFrontFaceShieldWindupWarningSignal> _frontFaceShieldWindupWarnings;
        private readonly ReadOnlyCollection<TickPlayerActionPresentationSignal> _playerActionSignals;
        private readonly ReadOnlyCollection<TickPlayerDamagePresentationSignal> _playerDamageSignals;
        private readonly ReadOnlyCollection<TickPlayerDeathPresentationSignal> _playerDeathSignals;
        private readonly ReadOnlyCollection<TickPlayerLocomotionPresentationSignal> _playerLocomotionSignals;
        private ReadOnlyCollection<TickSummonedEnemyPresentationBinding> _summonedEnemyPresentationBindings;
        private ReadOnlyCollection<TickSummonWindupWarningSignal> _summonWindupWarnings;
        private readonly TickTopologyMotion? _topologyMotion;
        private readonly ReadOnlyCollection<TickTransitionVisibilityChange> _transitionVisibilityChanges;
        private readonly ReadOnlyCollection<TickVisibilityChange> _visibilityChanges;

        // Presentation data is render-only metadata layered on top of authoritative gameplay state.
        public TickPresentationData(IEnumerable<TickEntityMotion> entityMotions)
            : this(
                entityMotions,
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                Array.Empty<TickTransitionVisibilityChange>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                Array.Empty<TickPlayerActionPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickEnemyActionPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                impactTransientSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                impactTransientSignals,
                flipImpactSignals)
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TickFrontFaceShieldSourceSignal> frontFaceShieldSources = null,
            IEnumerable<TickFrontFaceShieldBlockSignal> frontFaceShieldBlocks = null,
            IEnumerable<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IEnumerable<TickFrontFaceShieldWindupWarningSignal> frontFaceShieldWindupWarnings = null,
            IEnumerable<TickKinematicMotionTrack> kinematicMotionTracks = null)
        {
            if (entityMotions == null)
            {
                throw new ArgumentNullException(nameof(entityMotions));
            }

            if (visibilityChanges == null)
            {
                throw new ArgumentNullException(nameof(visibilityChanges));
            }

            if (transitionVisibilityChanges == null)
            {
                throw new ArgumentNullException(nameof(transitionVisibilityChanges));
            }

            if (playerActionSignals == null)
            {
                throw new ArgumentNullException(nameof(playerActionSignals));
            }

            if (enemyActionSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyActionSignals));
            }

            if (playerLocomotionSignals == null)
            {
                throw new ArgumentNullException(nameof(playerLocomotionSignals));
            }

            if (playerDamageSignals == null)
            {
                throw new ArgumentNullException(nameof(playerDamageSignals));
            }

            if (playerDeathSignals == null)
            {
                throw new ArgumentNullException(nameof(playerDeathSignals));
            }

            if (enemyDamageSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyDamageSignals));
            }

            if (enemyJumpSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyJumpSignals));
            }

            if (enemyChargeSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyChargeSignals));
            }

            if (entityExitSignals == null)
            {
                throw new ArgumentNullException(nameof(entityExitSignals));
            }

            if (flipImpactSignals == null)
            {
                throw new ArgumentNullException(nameof(flipImpactSignals));
            }

            _entityMotions = new ReadOnlyCollection<TickEntityMotion>(new List<TickEntityMotion>(entityMotions));
            _kinematicMotionTracks = new ReadOnlyCollection<TickKinematicMotionTrack>(
                new List<TickKinematicMotionTrack>(
                    kinematicMotionTracks ?? Array.Empty<TickKinematicMotionTrack>()));
            _topologyMotion = topologyMotion;
            _visibilityChanges = new ReadOnlyCollection<TickVisibilityChange>(new List<TickVisibilityChange>(visibilityChanges));
            _transitionVisibilityChanges = new ReadOnlyCollection<TickTransitionVisibilityChange>(
                new List<TickTransitionVisibilityChange>(transitionVisibilityChanges));
            _playerActionSignals = new ReadOnlyCollection<TickPlayerActionPresentationSignal>(
                new List<TickPlayerActionPresentationSignal>(playerActionSignals));
            _playerLocomotionSignals = new ReadOnlyCollection<TickPlayerLocomotionPresentationSignal>(
                new List<TickPlayerLocomotionPresentationSignal>(playerLocomotionSignals));
            _playerDamageSignals = new ReadOnlyCollection<TickPlayerDamagePresentationSignal>(
                new List<TickPlayerDamagePresentationSignal>(playerDamageSignals));
            _playerDeathSignals = new ReadOnlyCollection<TickPlayerDeathPresentationSignal>(
                new List<TickPlayerDeathPresentationSignal>(playerDeathSignals));
            _enemyDamageSignals = new ReadOnlyCollection<TickEnemyDamagePresentationSignal>(
                new List<TickEnemyDamagePresentationSignal>(enemyDamageSignals));
            _enemyActionSignals = new ReadOnlyCollection<TickEnemyActionPresentationSignal>(
                new List<TickEnemyActionPresentationSignal>(enemyActionSignals));
            _enemyJumpSignals = new ReadOnlyCollection<TickEnemyJumpPresentationSignal>(
                new List<TickEnemyJumpPresentationSignal>(enemyJumpSignals));
            _enemyChargeSignals = new ReadOnlyCollection<TickEnemyChargePresentationSignal>(
                new List<TickEnemyChargePresentationSignal>(enemyChargeSignals));
            _entityExitSignals = new ReadOnlyCollection<TickEntityExitPresentationSignal>(
                new List<TickEntityExitPresentationSignal>(entityExitSignals));
            _flipImpactSignals = new ReadOnlyCollection<FlipImpactPresentationSignal>(
                new List<FlipImpactPresentationSignal>(flipImpactSignals));
            _impactTransientSignals = new ReadOnlyCollection<TickImpactTransientPresentationSignal>(
                new List<TickImpactTransientPresentationSignal>());
            _summonedEnemyPresentationBindings = new ReadOnlyCollection<TickSummonedEnemyPresentationBinding>(
                new List<TickSummonedEnemyPresentationBinding>());
            _frontFaceShieldSources = new ReadOnlyCollection<TickFrontFaceShieldSourceSignal>(
                new List<TickFrontFaceShieldSourceSignal>(
                    frontFaceShieldSources ?? Array.Empty<TickFrontFaceShieldSourceSignal>()));
            _frontFaceShieldBlocks = new ReadOnlyCollection<TickFrontFaceShieldBlockSignal>(
                new List<TickFrontFaceShieldBlockSignal>(
                    frontFaceShieldBlocks ?? Array.Empty<TickFrontFaceShieldBlockSignal>()));
            _summonWindupWarnings = new ReadOnlyCollection<TickSummonWindupWarningSignal>(
                new List<TickSummonWindupWarningSignal>(
                    summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>()));
            _frontFaceShieldWindupWarnings = new ReadOnlyCollection<TickFrontFaceShieldWindupWarningSignal>(
                new List<TickFrontFaceShieldWindupWarningSignal>(
                    frontFaceShieldWindupWarnings ?? Array.Empty<TickFrontFaceShieldWindupWarningSignal>()));
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                flipImpactSignals)
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                enemyChargeSignals,
                entityExitSignals,
                impactTransientSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TickKinematicMotionTrack> kinematicMotionTracks = null)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                enemyChargeSignals,
                entityExitSignals,
                flipImpactSignals,
                kinematicMotionTracks: kinematicMotionTracks)
        {
            if (impactTransientSignals == null)
            {
                throw new ArgumentNullException(nameof(impactTransientSignals));
            }

            _impactTransientSignals = new ReadOnlyCollection<TickImpactTransientPresentationSignal>(
                new List<TickImpactTransientPresentationSignal>(impactTransientSignals));
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TickSummonedEnemyPresentationBinding> summonedEnemyPresentationBindings,
            IEnumerable<TickFrontFaceShieldSourceSignal> frontFaceShieldSources = null,
            IEnumerable<TickFrontFaceShieldBlockSignal> frontFaceShieldBlocks = null,
            IEnumerable<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IEnumerable<TickFrontFaceShieldWindupWarningSignal> frontFaceShieldWindupWarnings = null,
            IEnumerable<TickKinematicMotionTrack> kinematicMotionTracks = null)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                enemyChargeSignals,
                entityExitSignals,
                impactTransientSignals,
                flipImpactSignals,
                kinematicMotionTracks)
        {
            if (summonedEnemyPresentationBindings == null)
            {
                throw new ArgumentNullException(nameof(summonedEnemyPresentationBindings));
            }

            _summonedEnemyPresentationBindings = new ReadOnlyCollection<TickSummonedEnemyPresentationBinding>(
                new List<TickSummonedEnemyPresentationBinding>(summonedEnemyPresentationBindings));
            _frontFaceShieldSources = new ReadOnlyCollection<TickFrontFaceShieldSourceSignal>(
                new List<TickFrontFaceShieldSourceSignal>(
                    frontFaceShieldSources ?? Array.Empty<TickFrontFaceShieldSourceSignal>()));
            _frontFaceShieldBlocks = new ReadOnlyCollection<TickFrontFaceShieldBlockSignal>(
                new List<TickFrontFaceShieldBlockSignal>(
                    frontFaceShieldBlocks ?? Array.Empty<TickFrontFaceShieldBlockSignal>()));
            _summonWindupWarnings = new ReadOnlyCollection<TickSummonWindupWarningSignal>(
                new List<TickSummonWindupWarningSignal>(
                    summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>()));
            _frontFaceShieldWindupWarnings = new ReadOnlyCollection<TickFrontFaceShieldWindupWarningSignal>(
                new List<TickFrontFaceShieldWindupWarningSignal>(
                    frontFaceShieldWindupWarnings ?? Array.Empty<TickFrontFaceShieldWindupWarningSignal>()));
        }

        public IReadOnlyList<TickEntityMotion> EntityMotions => _entityMotions;

        public IReadOnlyList<TickKinematicMotionTrack> KinematicMotionTracks => _kinematicMotionTracks;

        public TickTopologyMotion? TopologyMotion => _topologyMotion;

        public IReadOnlyList<TickVisibilityChange> VisibilityChanges => _visibilityChanges;

        public IReadOnlyList<TickTransitionVisibilityChange> TransitionVisibilityChanges => _transitionVisibilityChanges;

        public IReadOnlyList<TickPlayerActionPresentationSignal> PlayerActionSignals => _playerActionSignals;

        public IReadOnlyList<TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignals => _playerLocomotionSignals;

        public IReadOnlyList<TickPlayerDamagePresentationSignal> PlayerDamageSignals => _playerDamageSignals;

        public IReadOnlyList<TickPlayerDeathPresentationSignal> PlayerDeathSignals => _playerDeathSignals;

        public IReadOnlyList<TickEnemyDamagePresentationSignal> EnemyDamageSignals => _enemyDamageSignals;

        public IReadOnlyList<TickEnemyActionPresentationSignal> EnemyActionSignals => _enemyActionSignals;

        public IReadOnlyList<TickEnemyJumpPresentationSignal> EnemyJumpSignals => _enemyJumpSignals;

        public IReadOnlyList<TickEnemyChargePresentationSignal> EnemyChargeSignals => _enemyChargeSignals;

        public IReadOnlyList<TickEntityExitPresentationSignal> EntityExitSignals => _entityExitSignals;

        public IReadOnlyList<FlipImpactPresentationSignal> FlipImpactSignals => _flipImpactSignals;

        internal IReadOnlyList<TickImpactTransientPresentationSignal> ImpactTransientSignals => _impactTransientSignals;

        public IReadOnlyList<TickSummonedEnemyPresentationBinding> SummonedEnemyPresentationBindings =>
            _summonedEnemyPresentationBindings;

        public IReadOnlyList<TickFrontFaceShieldSourceSignal> FrontFaceShieldSources => _frontFaceShieldSources;

        public IReadOnlyList<TickFrontFaceShieldBlockSignal> FrontFaceShieldBlocks => _frontFaceShieldBlocks;

        public IReadOnlyList<TickSummonWindupWarningSignal> SummonWindupWarnings => _summonWindupWarnings;

        public IReadOnlyList<TickFrontFaceShieldWindupWarningSignal> FrontFaceShieldWindupWarnings =>
            _frontFaceShieldWindupWarnings;
    }
}
