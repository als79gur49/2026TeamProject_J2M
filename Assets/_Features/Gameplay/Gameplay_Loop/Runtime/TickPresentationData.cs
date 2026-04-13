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
            int targetEntityId = 0,
            Direction direction = Direction.None)
        {
            EntityId = entityId;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            StartedThisTick = startedThisTick;
            ExecutedThisTick = executedThisTick;
            IsRecoveryPhase = isRecoveryPhase;
            CompletedThisTick = completedThisTick;
            CanceledThisTick = canceledThisTick;
            TargetEntityId = targetEntityId;
            Direction = direction;
        }

        public int EntityId { get; }

        public PlayerActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public bool StartedThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool IsRecoveryPhase { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }
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
            int retryCount = 0)
        {
            EntityId = entityId;
            Sequence = sequence;
            Phase = phase;
            StartedWindupThisTick = startedWindupThisTick;
            StartedAirborneThisTick = startedAirborneThisTick;
            LandedThisTick = landedThisTick;
            RetryThisTick = retryThisTick;
            SourceCell = sourceCell;
            LockedTargetCell = lockedTargetCell;
            PresentationTargetCell = presentationTargetCell;
            Facing = facing;
            LandingTick = landingTick;
            RemainingAirborneTicks = remainingAirborneTicks;
            RetryCount = retryCount;
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
            Array.Empty<TickEnemyActionPresentationSignal>(),
            Array.Empty<TickEnemyJumpPresentationSignal>(),
            Array.Empty<TickEntityExitPresentationSignal>());

        private readonly ReadOnlyCollection<TickEntityExitPresentationSignal> _entityExitSignals;
        private readonly ReadOnlyCollection<TickEnemyActionPresentationSignal> _enemyActionSignals;
        private readonly ReadOnlyCollection<TickEnemyDamagePresentationSignal> _enemyDamageSignals;
        private readonly ReadOnlyCollection<TickEnemyJumpPresentationSignal> _enemyJumpSignals;
        private readonly ReadOnlyCollection<TickEntityMotion> _entityMotions;
        private readonly ReadOnlyCollection<TickPlayerActionPresentationSignal> _playerActionSignals;
        private readonly ReadOnlyCollection<TickPlayerDamagePresentationSignal> _playerDamageSignals;
        private readonly ReadOnlyCollection<TickPlayerLocomotionPresentationSignal> _playerLocomotionSignals;
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
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>())
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
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals)
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
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals)
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
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals)
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
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals)
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

            if (enemyDamageSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyDamageSignals));
            }

            if (enemyJumpSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyJumpSignals));
            }

            if (entityExitSignals == null)
            {
                throw new ArgumentNullException(nameof(entityExitSignals));
            }

            _entityMotions = new ReadOnlyCollection<TickEntityMotion>(new List<TickEntityMotion>(entityMotions));
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
            _enemyDamageSignals = new ReadOnlyCollection<TickEnemyDamagePresentationSignal>(
                new List<TickEnemyDamagePresentationSignal>(enemyDamageSignals));
            _enemyActionSignals = new ReadOnlyCollection<TickEnemyActionPresentationSignal>(
                new List<TickEnemyActionPresentationSignal>(enemyActionSignals));
            _enemyJumpSignals = new ReadOnlyCollection<TickEnemyJumpPresentationSignal>(
                new List<TickEnemyJumpPresentationSignal>(enemyJumpSignals));
            _entityExitSignals = new ReadOnlyCollection<TickEntityExitPresentationSignal>(
                new List<TickEntityExitPresentationSignal>(entityExitSignals));
        }

        public IReadOnlyList<TickEntityMotion> EntityMotions => _entityMotions;

        public TickTopologyMotion? TopologyMotion => _topologyMotion;

        public IReadOnlyList<TickVisibilityChange> VisibilityChanges => _visibilityChanges;

        public IReadOnlyList<TickTransitionVisibilityChange> TransitionVisibilityChanges => _transitionVisibilityChanges;

        public IReadOnlyList<TickPlayerActionPresentationSignal> PlayerActionSignals => _playerActionSignals;

        public IReadOnlyList<TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignals => _playerLocomotionSignals;

        public IReadOnlyList<TickPlayerDamagePresentationSignal> PlayerDamageSignals => _playerDamageSignals;

        public IReadOnlyList<TickEnemyDamagePresentationSignal> EnemyDamageSignals => _enemyDamageSignals;

        public IReadOnlyList<TickEnemyActionPresentationSignal> EnemyActionSignals => _enemyActionSignals;

        public IReadOnlyList<TickEnemyJumpPresentationSignal> EnemyJumpSignals => _enemyJumpSignals;

        public IReadOnlyList<TickEntityExitPresentationSignal> EntityExitSignals => _entityExitSignals;
    }
}
