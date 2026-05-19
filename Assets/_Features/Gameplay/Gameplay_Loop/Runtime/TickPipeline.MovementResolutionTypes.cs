using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal readonly struct MoveWritePayload
    {
        public MoveWritePayload(int entityId, SurfaceCell sourceCell, SurfaceCell destinationCell, Direction facingAfterMove)
        {
            EntityId = entityId;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            FacingAfterMove = facingAfterMove;
        }

        public int EntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public Direction FacingAfterMove { get; }
    }

    internal readonly struct MovementEdge
    {
        public MovementEdge(SurfaceCell fromCell, SurfaceCell toCell)
        {
            FromCell = fromCell;
            ToCell = toCell;
        }

        public SurfaceCell FromCell { get; }

        public SurfaceCell ToCell { get; }
    }

    internal enum MovementReservationKind
    {
        None = 0,
        Vertex = 1,
        Edge = 2,
    }

    internal enum MovementBlockingType
    {
        Blocking = 0,
        NonBlocking = 1,
        PassThrough = 2,
    }

    internal enum MovementCandidateKind
    {
        Move = 0,
        Push = 1,
        Flip = 2,
        BoxImpact = 3,
        ProjectileImpact = 4,
        Stop = 5,
        Item = 6,
    }

    internal enum MovementExecutionBoundaryKind
    {
        Unknown = 0,
        UnitOrdinaryLocomotion = 1,
        UnitSpecialLocomotion = 2,
        LocomotionAnchorCommit = 3,
        BoxActionMovement = 4,
        TopologyMaterialization = 5,
        SpawnRespawnPlacement = 6,
        CleanupRemoval = 7,
        ScriptedRelocation = 8,
        LegacyFallback = 9,
        Free2DTopologyTransition = 10,
    }

    // Narrow internal contract for current Push / Sliding Push / Flip box-impact
    // resolve only. This is not a generalized impact framework seed.
    internal enum ImpactDispositionPolicyKind
    {
        PushLike = 0,
        Flip = 1,
    }

    internal enum ImpactDispositionKind
    {
        Stay = 0,
        FollowThrough = 1,
        DestroySelf = 2,
    }

    internal readonly struct ImpactDispositionResolutionRecord
    {
        public ImpactDispositionResolutionRecord(
            int actionPlanId,
            int impactSourceEntityId,
            int impactTargetEntityId,
            SurfaceCell impactCell,
            ImpactDispositionPolicyKind policyKind,
            ImpactDispositionKind dispositionKind,
            bool targetDestroyed,
            bool followThroughLegalityChecked,
            bool followThroughAccepted)
        {
            ActionPlanId = actionPlanId;
            ImpactSourceEntityId = impactSourceEntityId;
            ImpactTargetEntityId = impactTargetEntityId;
            ImpactTargetEntityIds = new[] { impactTargetEntityId };
            ImpactCell = impactCell;
            PolicyKind = policyKind;
            DispositionKind = dispositionKind;
            TargetDestroyed = targetDestroyed;
            AllTargetsDestroyed = targetDestroyed;
            FollowThroughLegalityChecked = followThroughLegalityChecked;
            FollowThroughAccepted = followThroughAccepted;
        }

        public ImpactDispositionResolutionRecord(
            int actionPlanId,
            int impactSourceEntityId,
            IReadOnlyList<int> impactTargetEntityIds,
            SurfaceCell impactCell,
            ImpactDispositionPolicyKind policyKind,
            ImpactDispositionKind dispositionKind,
            bool allTargetsDestroyed,
            bool followThroughLegalityChecked,
            bool followThroughAccepted)
        {
            ActionPlanId = actionPlanId;
            ImpactSourceEntityId = impactSourceEntityId;
            ImpactTargetEntityIds = impactTargetEntityIds ?? throw new ArgumentNullException(nameof(impactTargetEntityIds));
            ImpactTargetEntityId = ImpactTargetEntityIds.Count > 0 ? ImpactTargetEntityIds[0] : 0;
            ImpactCell = impactCell;
            PolicyKind = policyKind;
            DispositionKind = dispositionKind;
            TargetDestroyed = allTargetsDestroyed;
            AllTargetsDestroyed = allTargetsDestroyed;
            FollowThroughLegalityChecked = followThroughLegalityChecked;
            FollowThroughAccepted = followThroughAccepted;
        }

        public int ActionPlanId { get; }

        public int ImpactSourceEntityId { get; }

        public int ImpactTargetEntityId { get; }

        public IReadOnlyList<int> ImpactTargetEntityIds { get; }

        public SurfaceCell ImpactCell { get; }

        public ImpactDispositionPolicyKind PolicyKind { get; }

        public ImpactDispositionKind DispositionKind { get; }

        public bool TargetDestroyed { get; }

        public bool AllTargetsDestroyed { get; }

        public bool FollowThroughLegalityChecked { get; }

        public bool FollowThroughAccepted { get; }
    }

    internal readonly struct BoardPresenceWritePayload
    {
        public BoardPresenceWritePayload(int entityId, EntityBoardPresence boardPresence, TickEntityExitCause exitCauseHint)
        {
            EntityId = entityId;
            BoardPresence = boardPresence;
            ExitCauseHint = exitCauseHint;
        }

        public int EntityId { get; }

        public EntityBoardPresence BoardPresence { get; }

        public TickEntityExitCause ExitCauseHint { get; }
    }

    internal readonly struct FacingWritePayload
    {
        public FacingWritePayload(int entityId, Direction facing)
        {
            EntityId = entityId;
            Facing = facing;
        }

        public int EntityId { get; }

        public Direction Facing { get; }
    }

    internal readonly struct BoxKineticOwnerWritePayload
    {
        public BoxKineticOwnerWritePayload(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            EntityId = entityId;
            InstigatorEntityId = instigatorEntityId;
            InstigatorTeamId = instigatorTeamId;
        }

        public int EntityId { get; }

        public int InstigatorEntityId { get; }

        public int InstigatorTeamId { get; }
    }

    internal readonly struct TopologyWritePayload
    {
        public TopologyWritePayload(CubeTopologyState topology, CubeRotationKind rotationKind)
        {
            Topology = topology;
            RotationKind = rotationKind;
        }

        public CubeTopologyState Topology { get; }

        public CubeRotationKind RotationKind { get; }
    }

    internal readonly struct ExecutionLockWritePayload
    {
        public ExecutionLockWritePayload(int entityId, EntityExecutionLockState executionLockState)
        {
            EntityId = entityId;
            ExecutionLockState = executionLockState;
        }

        public int EntityId { get; }

        public EntityExecutionLockState ExecutionLockState { get; }
    }

    internal readonly struct EnemyLocomotionWritePayload
    {
        public EnemyLocomotionWritePayload(int entityId, int cooldownTicks)
        {
            EntityId = entityId;
            CooldownTicks = cooldownTicks;
        }

        public int EntityId { get; }

        public int CooldownTicks { get; }
    }

    internal readonly struct EnemyPatrolWritePayload
    {
        public EnemyPatrolWritePayload(int entityId, EnemyPatrolRuntimeState enemyPatrolState)
        {
            EntityId = entityId;
            EnemyPatrolState = enemyPatrolState;
        }

        public int EntityId { get; }

        public EnemyPatrolRuntimeState EnemyPatrolState { get; }
    }

    internal readonly struct EnemyChargeWritePayload
    {
        public EnemyChargeWritePayload(int entityId, EnemyChargeRuntimeState enemyChargeState, string label)
        {
            EntityId = entityId;
            EnemyChargeState = enemyChargeState;
            Label = label ?? string.Empty;
        }

        public int EntityId { get; }

        public EnemyChargeRuntimeState EnemyChargeState { get; }

        public string Label { get; }
    }

    internal readonly struct PlayerControlWritePayload
    {
        public PlayerControlWritePayload(int entityId, PlayerControlState playerControlState)
        {
            EntityId = entityId;
            PlayerControlState = playerControlState;
        }

        public int EntityId { get; }

        public PlayerControlState PlayerControlState { get; }
    }

    internal readonly struct DestroyWritePayload
    {
        public DestroyWritePayload(int targetEntityId, DestroyCondition destroyCondition, TickEntityExitCause exitCauseHint)
        {
            TargetEntityId = targetEntityId;
            DestroyCondition = destroyCondition;
            ExitCauseHint = exitCauseHint;
        }

        public int TargetEntityId { get; }

        public DestroyCondition DestroyCondition { get; }

        public TickEntityExitCause ExitCauseHint { get; }
    }

    internal readonly struct MovementImpactReservationPayload
    {
        public MovementImpactReservationPayload(
            int sourceEntityId,
            int attackSourceEntityId,
            SurfaceCell sourceCell,
            int targetEntityId,
            SurfaceCell impactCell,
            int damageAmount,
            int sequence,
            SurfaceCell contingentDestinationCell,
            SurfaceCell contingentSourceCell,
            Direction contingentFacing,
            bool hasContingentStateChange,
            EntityPhaseState contingentState,
            int contingentStateTimer,
            bool hasSourceFacing,
            int sourceFacingEntityId,
            Direction sourceFacing,
            ImpactDispositionPolicyKind dispositionPolicyKind,
            ResolvedActionSemanticKind contingentSemanticKind)
            : this(
                sourceEntityId,
                attackSourceEntityId,
                sourceCell,
                new[] { targetEntityId },
                impactCell,
                damageAmount,
                sequence,
                contingentDestinationCell,
                contingentSourceCell,
                contingentFacing,
                hasContingentStateChange,
                contingentState,
                contingentStateTimer,
                hasSourceFacing,
                sourceFacingEntityId,
                sourceFacing,
                dispositionPolicyKind,
                contingentSemanticKind)
        {
        }

        public MovementImpactReservationPayload(
            int sourceEntityId,
            int attackSourceEntityId,
            SurfaceCell sourceCell,
            IReadOnlyList<int> targetEntityIds,
            SurfaceCell impactCell,
            int damageAmount,
            int sequence,
            SurfaceCell contingentDestinationCell,
            SurfaceCell contingentSourceCell,
            Direction contingentFacing,
            bool hasContingentStateChange,
            EntityPhaseState contingentState,
            int contingentStateTimer,
            bool hasSourceFacing,
            int sourceFacingEntityId,
            Direction sourceFacing,
            ImpactDispositionPolicyKind dispositionPolicyKind,
            ResolvedActionSemanticKind contingentSemanticKind)
        {
            SourceEntityId = sourceEntityId;
            AttackSourceEntityId = attackSourceEntityId;
            SourceCell = sourceCell;
            TargetEntityIds = targetEntityIds ?? throw new ArgumentNullException(nameof(targetEntityIds));
            TargetEntityId = TargetEntityIds.Count > 0 ? TargetEntityIds[0] : 0;
            ImpactCell = impactCell;
            DamageAmount = damageAmount;
            Sequence = sequence;
            ContingentDestinationCell = contingentDestinationCell;
            ContingentSourceCell = contingentSourceCell;
            ContingentFacing = contingentFacing;
            HasContingentStateChange = hasContingentStateChange;
            ContingentState = contingentState;
            ContingentStateTimer = contingentStateTimer;
            HasSourceFacing = hasSourceFacing;
            SourceFacingEntityId = sourceFacingEntityId;
            SourceFacing = sourceFacing;
            DispositionPolicyKind = dispositionPolicyKind;
            ContingentSemanticKind = contingentSemanticKind;
        }

        public int SourceEntityId { get; }

        public int AttackSourceEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public int TargetEntityId { get; }

        public IReadOnlyList<int> TargetEntityIds { get; }

        public SurfaceCell ImpactCell { get; }

        public int DamageAmount { get; }

        public int Sequence { get; }

        public SurfaceCell ContingentDestinationCell { get; }

        public SurfaceCell ContingentSourceCell { get; }

        public Direction ContingentFacing { get; }

        public bool HasContingentStateChange { get; }

        public EntityPhaseState ContingentState { get; }

        public int ContingentStateTimer { get; }

        public bool HasSourceFacing { get; }

        public int SourceFacingEntityId { get; }

        public Direction SourceFacing { get; }

        public ImpactDispositionPolicyKind DispositionPolicyKind { get; }

        public ResolvedActionSemanticKind ContingentSemanticKind { get; }
    }

    internal readonly struct MovementDeferredImpactPayload
    {
        public MovementDeferredImpactPayload(
            int sourceEntityId,
            SurfaceCell impactCell,
            int damageAmount,
            int sequence)
        {
            SourceEntityId = sourceEntityId;
            ImpactCell = impactCell;
            DamageAmount = damageAmount;
            Sequence = sequence;
        }

        public int SourceEntityId { get; }

        public SurfaceCell ImpactCell { get; }

        public int DamageAmount { get; }

        public int Sequence { get; }
    }

    internal readonly struct KinematicMotionOutcome
    {
        public KinematicMotionOutcome(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell resolvedAnchorCell,
            KinematicOffset2 resolvedLocalOffset,
            KinematicVelocity2 resolvedVelocity,
            UnitKinematicRuntimeState resolvedState,
            bool anchorChanged,
            bool blocked,
            KinematicSweepRejectionReason rejectedBy)
        {
            EntityId = entityId;
            SourceAnchorCell = sourceAnchorCell;
            SourceLocalOffset = sourceLocalOffset;
            ResolvedAnchorCell = resolvedAnchorCell;
            ResolvedLocalOffset = resolvedLocalOffset;
            ResolvedVelocity = resolvedVelocity;
            ResolvedState = resolvedState;
            AnchorChanged = anchorChanged;
            Blocked = blocked;
            RejectedBy = rejectedBy;
        }

        public int EntityId { get; }

        public SurfaceCell SourceAnchorCell { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public SurfaceCell ResolvedAnchorCell { get; }

        public KinematicOffset2 ResolvedLocalOffset { get; }

        public KinematicVelocity2 ResolvedVelocity { get; }

        public UnitKinematicRuntimeState ResolvedState { get; }

        public bool AnchorChanged { get; }

        public bool Blocked { get; }

        public KinematicSweepRejectionReason RejectedBy { get; }
    }

    internal readonly struct UnitLocomotionIntent
    {
        public UnitLocomotionIntent(
            int entityId,
            KinematicVelocity2 requestedDelta,
            MotionMode requestedMode,
            ForcedMotionOp forcedOp = ForcedMotionOp.None)
        {
            EntityId = entityId;
            RequestedDelta = requestedDelta;
            RequestedMode = requestedMode;
            ForcedOp = forcedOp;
        }

        public int EntityId { get; }

        public KinematicVelocity2 RequestedDelta { get; }

        public MotionMode RequestedMode { get; }

        public ForcedMotionOp ForcedOp { get; }
    }

    internal readonly struct ForcedMotionOpRequest
    {
        public ForcedMotionOpRequest(
            int entityId,
            ForcedMotionOp forcedOp,
            KinematicVelocity2 requestedDelta,
            int startTick)
        {
            EntityId = entityId;
            ForcedOp = forcedOp;
            RequestedDelta = requestedDelta;
            StartTick = startTick;
        }

        public int EntityId { get; }

        public ForcedMotionOp ForcedOp { get; }

        public KinematicVelocity2 RequestedDelta { get; }

        public int StartTick { get; }
    }

    internal readonly struct MotionInterruptRecord
    {
        public MotionInterruptRecord(int entityId, MotionInterruptPolicy policy, int sourceEntityId)
        {
            EntityId = entityId;
            Policy = policy;
            SourceEntityId = sourceEntityId;
        }

        public int EntityId { get; }

        public MotionInterruptPolicy Policy { get; }

        public int SourceEntityId { get; }
    }

    internal sealed class MovementActionPlanPayload : ActionPlanPayload
    {
        public MovementActionPlanPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            ResolvedActionSemanticKind semanticKind,
            MovementCandidateKind movementCandidateKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            bool hasMovementEdge,
            MovementEdge movementEdge,
            IReadOnlyList<int> affectedEntityIds,
            MovementReservationKind reservationKind,
            MovementBlockingType blockingType,
            IReadOnlyList<StateChangeWritePayload> stateChangeWrites,
            IReadOnlyList<MoveWritePayload> moveWrites,
            IReadOnlyList<BoardPresenceWritePayload> boardPresenceWrites,
            IReadOnlyList<FacingWritePayload> facingWrites,
            IReadOnlyList<BoxKineticOwnerWritePayload> boxKineticOwnerWrites,
            IReadOnlyList<TopologyWritePayload> topologyWrites,
            IReadOnlyList<ExecutionLockWritePayload> executionLockWrites,
            IReadOnlyList<EnemyLocomotionWritePayload> enemyLocomotionWrites,
            IReadOnlyList<EnemyPatrolWritePayload> enemyPatrolWrites,
            IReadOnlyList<EnemyChargeWritePayload> enemyChargeWrites,
            IReadOnlyList<PlayerControlWritePayload> playerControlWrites,
            IReadOnlyList<DestroyWritePayload> destroyWrites,
            bool hasImpactReservationPayload,
            MovementImpactReservationPayload impactReservationPayload,
            bool hasDeferredImpactPayload,
            MovementDeferredImpactPayload deferredImpactPayload,
            IReadOnlyList<KinematicMotionOutcome> kinematicMotionOutcomes = null,
            MovementExecutionBoundaryKind executionBoundaryKind = MovementExecutionBoundaryKind.Unknown,
            string boundaryReason = null)
            : base(actionPlanId, intentId, sourceActorEntityId, priority, semanticKind)
        {
            MovementCandidateKind = movementCandidateKind;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            HasMovementEdge = hasMovementEdge;
            MovementEdge = movementEdge;
            AffectedEntityIds = affectedEntityIds ?? throw new ArgumentNullException(nameof(affectedEntityIds));
            ReservationKind = reservationKind;
            BlockingType = blockingType;
            StateChangeWrites = stateChangeWrites ?? throw new ArgumentNullException(nameof(stateChangeWrites));
            MoveWrites = moveWrites ?? throw new ArgumentNullException(nameof(moveWrites));
            BoardPresenceWrites = boardPresenceWrites ?? throw new ArgumentNullException(nameof(boardPresenceWrites));
            FacingWrites = facingWrites ?? throw new ArgumentNullException(nameof(facingWrites));
            BoxKineticOwnerWrites = boxKineticOwnerWrites ?? throw new ArgumentNullException(nameof(boxKineticOwnerWrites));
            TopologyWrites = topologyWrites ?? throw new ArgumentNullException(nameof(topologyWrites));
            ExecutionLockWrites = executionLockWrites ?? throw new ArgumentNullException(nameof(executionLockWrites));
            EnemyLocomotionWrites = enemyLocomotionWrites ?? throw new ArgumentNullException(nameof(enemyLocomotionWrites));
            EnemyPatrolWrites = enemyPatrolWrites ?? throw new ArgumentNullException(nameof(enemyPatrolWrites));
            EnemyChargeWrites = enemyChargeWrites ?? throw new ArgumentNullException(nameof(enemyChargeWrites));
            PlayerControlWrites = playerControlWrites ?? throw new ArgumentNullException(nameof(playerControlWrites));
            DestroyWrites = destroyWrites ?? throw new ArgumentNullException(nameof(destroyWrites));
            HasImpactReservationPayload = hasImpactReservationPayload;
            ImpactReservationPayload = impactReservationPayload;
            HasDeferredImpactPayload = hasDeferredImpactPayload;
            DeferredImpactPayload = deferredImpactPayload;
            KinematicMotionOutcomes = kinematicMotionOutcomes ?? Array.Empty<KinematicMotionOutcome>();
            ExecutionBoundaryKind = executionBoundaryKind;
            BoundaryReason = boundaryReason ?? string.Empty;
        }

        public MovementCandidateKind MovementCandidateKind { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public bool HasMovementEdge { get; }

        public MovementEdge MovementEdge { get; }

        public IReadOnlyList<int> AffectedEntityIds { get; }

        public MovementReservationKind ReservationKind { get; }

        public MovementBlockingType BlockingType { get; }

        public IReadOnlyList<StateChangeWritePayload> StateChangeWrites { get; }

        public IReadOnlyList<MoveWritePayload> MoveWrites { get; }

        public IReadOnlyList<BoardPresenceWritePayload> BoardPresenceWrites { get; }

        public IReadOnlyList<FacingWritePayload> FacingWrites { get; }

        public IReadOnlyList<BoxKineticOwnerWritePayload> BoxKineticOwnerWrites { get; }

        public IReadOnlyList<TopologyWritePayload> TopologyWrites { get; }

        public IReadOnlyList<ExecutionLockWritePayload> ExecutionLockWrites { get; }

        public IReadOnlyList<EnemyLocomotionWritePayload> EnemyLocomotionWrites { get; }

        public IReadOnlyList<EnemyPatrolWritePayload> EnemyPatrolWrites { get; }

        public IReadOnlyList<EnemyChargeWritePayload> EnemyChargeWrites { get; }

        public IReadOnlyList<PlayerControlWritePayload> PlayerControlWrites { get; }

        public IReadOnlyList<DestroyWritePayload> DestroyWrites { get; }

        public bool HasImpactReservationPayload { get; }

        public MovementImpactReservationPayload ImpactReservationPayload { get; }

        public bool HasDeferredImpactPayload { get; }

        public MovementDeferredImpactPayload DeferredImpactPayload { get; }

        public IReadOnlyList<KinematicMotionOutcome> KinematicMotionOutcomes { get; }

        public MovementExecutionBoundaryKind ExecutionBoundaryKind { get; }

        public string BoundaryReason { get; }
    }

}
