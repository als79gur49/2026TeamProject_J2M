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
    internal enum FinalizationOperationBucket
    {
        NonHpState = 0,
        DamageState = 1,
        Spawn = 2,
        Destroy = 3,
        DelayedEnqueue = 4,
    }

    internal enum FinalizationOperationKind
    {
        MoveEntity = 0,
        ApplyStateChange = 1,
        SetFacing = 2,
        SetBoxKineticOwner = 3,
        SetBoardPresence = 4,
        SetEnemyLocomotionCooldown = 5,
        SetEnemyAttackCooldown = 6,
        SetEntityExecutionLockState = 7,
        SetTopology = 8,
        SetPlayerControlState = 9,
        SetPlayerDamageState = 10,
        ApplyEnemyAiState = 11,
        SetEnemyActionState = 12,
        SetEnemyPatrolState = 13,
        SetEnemyJumpState = 14,
        SetEnemyChargeState = 15,
        SpawnEntity = 16,
        ApplyDamage = 17,
        MarkDestroy = 18,
        EnqueueDelayedAttackEffect = 19,
        SetPhasedState = 20,
        SetEnemyUtilityState = 21,
        SetBoxInteractionLockState = 22,
        RemoveBoxInteractionLockState = 23,
        SetEnemyGlideState = 24,
        SetEnemyFrontFaceSupportState = 25,
        SetUnitKinematicState = 26,
        SetUnitContinuousLocomotionState = 27,
        SetGravityFieldState = 28,
        AddPendingCellImpact = 29,
        RemovePendingCellImpact = 30,
        SetEnemyGravityFieldAuraFieldState = 31,
        RemoveEnemyGravityFieldAuraFieldState = 32,
        SetPendingEnemyBlockedReaction = 33,
        ClearPendingEnemyBlockedReaction = 34,
        PoseMutation = 35,
        SetEntityLocomotionLeaseState = 36,
    }

    internal enum ResolvedActionSemanticKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        Slide = 4,
        Impact = 5,
        Item = 6,
        ProjectileMove = 7,
        Attack = 8,
        Stop = 9,
        JumpLanding = 10,
    }

    public enum MovementSemanticKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        Slide = 4,
        Impact = 5,
        JumpLanding = 6,
        ProjectileMove = 7,
        Item = 8,
        Stop = 9,
    }

    internal enum DamageSourceType
    {
        None = 0,
        Attack = 1,
        Impact = 2,
        Environmental = 3,
    }

    internal enum JumpPresentationKind
    {
        None = 0,
        WindupStart = 1,
        AirborneStart = 2,
        LandingSuccess = 3,
        LandingRetry = 4,
        CrushedBoxAndLanded = 5,
    }

    internal readonly struct FinalizationOperationMetadata
    {
        public FinalizationOperationMetadata(
            TickPhase originPhase,
            ResolvedActionSemanticKind semanticKind,
            int sourceActorEntityId,
            int actionPlanId,
            int intentId = 0,
            int contestId = 0,
            int localActionIndex = 0,
            int priority = 0,
            CubeRotationKind rotationKind = CubeRotationKind.None,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None,
            AttackSourceKind attackSourceKind = AttackSourceKind.Combat,
            MovementSemanticKind movementSemanticKind = MovementSemanticKind.None,
            DamageSourceType damageSourceType = DamageSourceType.None,
            JumpPresentationKind jumpPresentationKind = JumpPresentationKind.None,
            SurfaceCell presentationTargetCell = default,
            MovementExecutionBoundaryKind movementExecutionBoundaryKind = MovementExecutionBoundaryKind.Unknown,
            string boundaryReason = null,
            EntityExitPresentationTiming exitPresentationTiming = EntityExitPresentationTiming.Immediate,
            bool hasPresentationTargetCell = false)
        {
            OriginPhase = originPhase;
            SemanticKind = semanticKind;
            SourceActorEntityId = sourceActorEntityId;
            ActionPlanId = actionPlanId;
            IntentId = intentId;
            ContestId = contestId;
            LocalActionIndex = localActionIndex;
            Priority = priority;
            RotationKind = rotationKind;
            ExitCauseHint = exitCauseHint;
            AttackSourceKind = attackSourceKind;
            MovementSemanticKind = movementSemanticKind;
            DamageSourceType = damageSourceType;
            JumpPresentationKind = jumpPresentationKind;
            PresentationTargetCell = presentationTargetCell;
            MovementExecutionBoundaryKind = movementExecutionBoundaryKind;
            BoundaryReason = boundaryReason ?? string.Empty;
            ExitPresentationTiming = exitPresentationTiming;
            HasPresentationTargetCell = hasPresentationTargetCell || !presentationTargetCell.Equals(default(SurfaceCell));
        }

        public TickPhase OriginPhase { get; }

        public ResolvedActionSemanticKind SemanticKind { get; }

        public int SourceActorEntityId { get; }

        public int ActionPlanId { get; }

        public int IntentId { get; }

        public int ContestId { get; }

        public int LocalActionIndex { get; }

        public int Priority { get; }

        public CubeRotationKind RotationKind { get; }

        public TickEntityExitCause ExitCauseHint { get; }

        public AttackSourceKind AttackSourceKind { get; }

        public MovementSemanticKind MovementSemanticKind { get; }

        public DamageSourceType DamageSourceType { get; }

        public JumpPresentationKind JumpPresentationKind { get; }

        public SurfaceCell PresentationTargetCell { get; }

        public bool HasPresentationTargetCell { get; }

        public MovementExecutionBoundaryKind MovementExecutionBoundaryKind { get; }

        public string BoundaryReason { get; }

        public EntityExitPresentationTiming ExitPresentationTiming { get; }
    }

    internal sealed class FinalizationOperation
    {
        private FinalizationOperation(
            long sequence,
            FinalizationOperationBucket bucket,
            FinalizationOperationKind kind,
            FinalizationOperationMetadata metadata = default,
            int entityId = 0,
            SurfaceCell destination = default,
            int amount = 0,
            EntityPhaseState phaseState = default,
            int stateTimer = 0,
            Direction facing = default,
            int instigatorEntityId = 0,
            int instigatorTeamId = 0,
            EntityBoardPresence boardPresence = default,
            int cooldownTicks = 0,
            int cooldownTotalTicks = 0,
            EntityExecutionLockState executionLockState = default,
            CubeTopologyState topology = default,
            PlayerControlState playerControlState = default,
            PlayerDamageState playerDamageState = default,
            EnemyAiMode enemyAiMode = default,
            int enemyAiStateTimer = 0,
            EnemyActionRuntimeState enemyActionState = default,
            EnemyPatrolRuntimeState enemyPatrolState = default,
            EnemyJumpRuntimeState enemyJumpState = default,
            EnemyGlideRuntimeState enemyGlideState = default,
            EnemyChargeRuntimeState enemyChargeState = default,
            EnemyUtilityRuntimeState enemyUtilityState = null,
            EnemyFrontFaceSupportRuntimeState enemyFrontFaceSupportState = null,
            BoxInteractionLockState boxInteractionLockState = default,
            EnemyGravityFieldAuraFieldState enemyGravityFieldAuraFieldState = default,
            PendingEnemyBlockedReaction pendingEnemyBlockedReaction = default,
            GravityFieldPhase gravityFieldPhase = default,
            int gravityFieldTimerTicks = 0,
            UnitKinematicRuntimeState unitKinematicState = default,
            UnitContinuousLocomotionState unitContinuousLocomotionState = default,
            EntityLocomotionLeaseState entityLocomotionLeaseState = default,
            PhasedRuntimeState phasedState = default,
            EntityState spawnEntity = default,
            bool hasSpawnedEntitySummonedState = false,
            SummonedEntityState spawnedEntitySummonedState = default,
            bool hasSpawnedEntityEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState spawnedEntityEnemyDefinitionBindingState = default,
            DelayedAttackEffectRecord delayedAttackEffect = default,
            PendingCellImpact pendingCellImpact = default,
            EntityPoseMutationOperation poseMutationOperation = default)
        {
            Sequence = sequence;
            Bucket = bucket;
            Kind = kind;
            Metadata = metadata;
            EntityId = entityId;
            Destination = destination;
            Amount = amount;
            PhaseState = phaseState;
            StateTimer = stateTimer;
            Facing = facing;
            InstigatorEntityId = instigatorEntityId;
            InstigatorTeamId = instigatorTeamId;
            BoardPresence = boardPresence;
            CooldownTicks = cooldownTicks;
            CooldownTotalTicks = cooldownTotalTicks;
            ExecutionLockState = executionLockState;
            Topology = topology;
            PlayerControlState = playerControlState;
            PlayerDamageState = playerDamageState;
            EnemyAiMode = enemyAiMode;
            EnemyAiStateTimer = enemyAiStateTimer;
            EnemyActionState = enemyActionState;
            EnemyPatrolState = enemyPatrolState;
            EnemyJumpState = enemyJumpState;
            EnemyGlideState = enemyGlideState;
            EnemyChargeState = enemyChargeState;
            EnemyUtilityState = enemyUtilityState;
            EnemyFrontFaceSupportState = enemyFrontFaceSupportState;
            BoxInteractionLockState = boxInteractionLockState;
            EnemyGravityFieldAuraFieldState = enemyGravityFieldAuraFieldState;
            PendingEnemyBlockedReaction = pendingEnemyBlockedReaction;
            GravityFieldPhase = gravityFieldPhase;
            GravityFieldTimerTicks = gravityFieldTimerTicks;
            UnitKinematicState = unitKinematicState;
            UnitContinuousLocomotionState = unitContinuousLocomotionState;
            EntityLocomotionLeaseState = entityLocomotionLeaseState;
            PhasedState = phasedState;
            SpawnedEntity = spawnEntity;
            HasSpawnedEntitySummonedState = hasSpawnedEntitySummonedState;
            SpawnedEntitySummonedState = spawnedEntitySummonedState;
            HasSpawnedEntityEnemyDefinitionBindingState = hasSpawnedEntityEnemyDefinitionBindingState;
            SpawnedEntityEnemyDefinitionBindingState = spawnedEntityEnemyDefinitionBindingState;
            DelayedAttackEffect = delayedAttackEffect;
            PendingCellImpact = pendingCellImpact;
            PoseMutationOperation = poseMutationOperation;
        }

        public long Sequence { get; }

        public FinalizationOperationBucket Bucket { get; }

        public FinalizationOperationKind Kind { get; }

        public FinalizationOperationMetadata Metadata { get; }

        public int EntityId { get; }

        public SurfaceCell Destination { get; }

        public int Amount { get; }

        public EntityPhaseState PhaseState { get; }

        public int StateTimer { get; }

        public Direction Facing { get; }

        public int InstigatorEntityId { get; }

        public int InstigatorTeamId { get; }

        public EntityBoardPresence BoardPresence { get; }

        public int CooldownTicks { get; }

        public int CooldownTotalTicks { get; }

        public EntityExecutionLockState ExecutionLockState { get; }

        public CubeTopologyState Topology { get; }

        public PlayerControlState PlayerControlState { get; }

        public PlayerDamageState PlayerDamageState { get; }

        public EnemyAiMode EnemyAiMode { get; }

        public int EnemyAiStateTimer { get; }

        public EnemyActionRuntimeState EnemyActionState { get; }

        public PendingCellImpact PendingCellImpact { get; }

        public EnemyPatrolRuntimeState EnemyPatrolState { get; }

        public EnemyJumpRuntimeState EnemyJumpState { get; }

        public EnemyGlideRuntimeState EnemyGlideState { get; }

        public EnemyChargeRuntimeState EnemyChargeState { get; }

        public EnemyUtilityRuntimeState EnemyUtilityState { get; }

        public EnemyFrontFaceSupportRuntimeState EnemyFrontFaceSupportState { get; }

        public BoxInteractionLockState BoxInteractionLockState { get; }

        public EnemyGravityFieldAuraFieldState EnemyGravityFieldAuraFieldState { get; }

        public PendingEnemyBlockedReaction PendingEnemyBlockedReaction { get; }

        public GravityFieldPhase GravityFieldPhase { get; }

        public int GravityFieldTimerTicks { get; }

        public UnitKinematicRuntimeState UnitKinematicState { get; }

        public UnitContinuousLocomotionState UnitContinuousLocomotionState { get; }

        public EntityLocomotionLeaseState EntityLocomotionLeaseState { get; }

        public PhasedRuntimeState PhasedState { get; }

        public EntityState SpawnedEntity { get; }

        public bool HasSpawnedEntitySummonedState { get; }

        public SummonedEntityState SpawnedEntitySummonedState { get; }

        public bool HasSpawnedEntityEnemyDefinitionBindingState { get; }

        public EnemyDefinitionBindingState SpawnedEntityEnemyDefinitionBindingState { get; }

        public DelayedAttackEffectRecord DelayedAttackEffect { get; }

        public EntityPoseMutationOperation PoseMutationOperation { get; }

        public FinalizationOperation WithSequence(long sequence)
        {
            return new FinalizationOperation(
                sequence,
                Bucket,
                Kind,
                Metadata,
                EntityId,
                Destination,
                Amount,
                PhaseState,
                StateTimer,
                Facing,
                InstigatorEntityId,
                InstigatorTeamId,
                BoardPresence,
                CooldownTicks,
                CooldownTotalTicks,
                ExecutionLockState,
                Topology,
                PlayerControlState,
                PlayerDamageState,
                EnemyAiMode,
                EnemyAiStateTimer,
                EnemyActionState,
                EnemyPatrolState,
                EnemyJumpState,
                EnemyGlideState,
                EnemyChargeState,
                EnemyUtilityState,
                EnemyFrontFaceSupportState,
                BoxInteractionLockState,
                EnemyGravityFieldAuraFieldState,
                PendingEnemyBlockedReaction,
                GravityFieldPhase,
                GravityFieldTimerTicks,
                UnitKinematicState,
                UnitContinuousLocomotionState,
                EntityLocomotionLeaseState,
                PhasedState,
                SpawnedEntity,
                HasSpawnedEntitySummonedState,
                SpawnedEntitySummonedState,
                HasSpawnedEntityEnemyDefinitionBindingState,
                SpawnedEntityEnemyDefinitionBindingState,
                DelayedAttackEffect,
                PendingCellImpact,
                PoseMutationOperation);
        }

        public static FinalizationOperation MoveEntity(long sequence, int entityId, SurfaceCell destination, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(sequence, FinalizationOperationBucket.NonHpState, FinalizationOperationKind.MoveEntity, metadata, entityId, destination);
        }

        public static FinalizationOperation ApplyStateChange(long sequence, int entityId, EntityPhaseState state, int stateTimer, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ApplyStateChange,
                metadata,
                entityId: entityId,
                phaseState: state,
                stateTimer: stateTimer);
        }

        public static FinalizationOperation SetFacing(long sequence, int entityId, Direction facing, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetFacing,
                metadata,
                entityId: entityId,
                facing: facing);
        }

        public static FinalizationOperation SetBoxKineticOwner(long sequence, int entityId, int instigatorEntityId, int instigatorTeamId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoxKineticOwner,
                metadata,
                entityId: entityId,
                instigatorEntityId: instigatorEntityId,
                instigatorTeamId: instigatorTeamId);
        }

        public static FinalizationOperation SetBoardPresence(long sequence, int entityId, EntityBoardPresence boardPresence, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoardPresence,
                metadata,
                entityId: entityId,
                boardPresence: boardPresence);
        }

        public static FinalizationOperation SetEnemyLocomotionCooldown(long sequence, int entityId, int cooldownTicks, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyLocomotionCooldown,
                metadata,
                entityId: entityId,
                cooldownTicks: cooldownTicks);
        }

        public static FinalizationOperation SetEnemyAttackCooldown(long sequence, int entityId, int cooldownTicks, int totalTicks, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyAttackCooldown,
                metadata,
                entityId: entityId,
                cooldownTicks: cooldownTicks,
                cooldownTotalTicks: totalTicks);
        }

        public static FinalizationOperation SetEntityExecutionLockState(long sequence, int entityId, EntityExecutionLockState executionLockState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEntityExecutionLockState,
                metadata,
                entityId: entityId,
                executionLockState: executionLockState);
        }

        public static FinalizationOperation SetTopology(long sequence, CubeTopologyState topology, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetTopology,
                metadata,
                topology: topology);
        }

        public static FinalizationOperation SetPlayerControlState(long sequence, int entityId, PlayerControlState playerControlState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetPlayerControlState,
                metadata,
                entityId: entityId,
                playerControlState: playerControlState);
        }

        public static FinalizationOperation SetPlayerDamageState(long sequence, int entityId, PlayerDamageState playerDamageState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DamageState,
                FinalizationOperationKind.SetPlayerDamageState,
                metadata,
                entityId: entityId,
                playerDamageState: playerDamageState);
        }

        public static FinalizationOperation ApplyEnemyAiState(long sequence, int entityId, EnemyAiMode enemyAiMode, int enemyAiStateTimer, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ApplyEnemyAiState,
                metadata,
                entityId: entityId,
                enemyAiMode: enemyAiMode,
                enemyAiStateTimer: enemyAiStateTimer);
        }

        public static FinalizationOperation SetEnemyActionState(long sequence, int entityId, EnemyActionRuntimeState enemyActionState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyActionState,
                metadata,
                entityId: entityId,
                enemyActionState: enemyActionState);
        }

        public static FinalizationOperation AddPendingCellImpact(long sequence, PendingCellImpact pendingCellImpact, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.AddPendingCellImpact,
                metadata,
                pendingCellImpact: pendingCellImpact);
        }

        public static FinalizationOperation RemovePendingCellImpact(long sequence, int impactId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Destroy,
                FinalizationOperationKind.RemovePendingCellImpact,
                metadata,
                entityId: impactId);
        }

        public static FinalizationOperation SetEnemyPatrolState(long sequence, int entityId, EnemyPatrolRuntimeState enemyPatrolState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyPatrolState,
                metadata,
                entityId: entityId,
                enemyPatrolState: enemyPatrolState);
        }

        public static FinalizationOperation SetEnemyJumpState(long sequence, int entityId, EnemyJumpRuntimeState enemyJumpState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyJumpState,
                metadata,
                entityId: entityId,
                enemyJumpState: enemyJumpState);
        }

        public static FinalizationOperation SetEnemyGlideState(long sequence, int entityId, EnemyGlideRuntimeState enemyGlideState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyGlideState,
                metadata,
                entityId: entityId,
                enemyGlideState: enemyGlideState);
        }

        public static FinalizationOperation SetEnemyChargeState(long sequence, int entityId, EnemyChargeRuntimeState enemyChargeState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyChargeState,
                metadata,
                entityId: entityId,
                enemyChargeState: enemyChargeState);
        }

        public static FinalizationOperation SetEnemyUtilityState(long sequence, int entityId, EnemyUtilityRuntimeState enemyUtilityState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyUtilityState,
                metadata,
                entityId: entityId,
                enemyUtilityState: enemyUtilityState);
        }

        public static FinalizationOperation SetEnemyFrontFaceSupportState(
            long sequence,
            int entityId,
            EnemyFrontFaceSupportRuntimeState enemyFrontFaceSupportState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyFrontFaceSupportState,
                metadata,
                entityId: entityId,
                enemyFrontFaceSupportState: enemyFrontFaceSupportState);
        }

        public static FinalizationOperation SetBoxInteractionLockState(long sequence, int entityId, BoxInteractionLockState boxInteractionLockState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoxInteractionLockState,
                metadata,
                entityId: entityId,
                boxInteractionLockState: boxInteractionLockState);
        }

        public static FinalizationOperation RemoveBoxInteractionLockState(long sequence, int entityId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.RemoveBoxInteractionLockState,
                metadata,
                entityId: entityId);
        }

        public static FinalizationOperation SetEnemyGravityFieldAuraFieldState(
            long sequence,
            int fieldId,
            EnemyGravityFieldAuraFieldState enemyGravityFieldAuraFieldState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyGravityFieldAuraFieldState,
                metadata,
                entityId: fieldId,
                enemyGravityFieldAuraFieldState: enemyGravityFieldAuraFieldState);
        }

        public static FinalizationOperation RemoveEnemyGravityFieldAuraFieldState(
            long sequence,
            int fieldId,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.RemoveEnemyGravityFieldAuraFieldState,
                metadata,
                entityId: fieldId);
        }

        public static FinalizationOperation SetPendingEnemyBlockedReaction(
            long sequence,
            int entityId,
            PendingEnemyBlockedReaction reaction,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetPendingEnemyBlockedReaction,
                metadata,
                entityId: entityId,
                pendingEnemyBlockedReaction: reaction);
        }

        public static FinalizationOperation ClearPendingEnemyBlockedReaction(
            long sequence,
            int entityId,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ClearPendingEnemyBlockedReaction,
                metadata,
                entityId: entityId);
        }

        public static FinalizationOperation SetGravityFieldState(
            long sequence,
            int entityId,
            GravityFieldPhase phase,
            int timerTicks,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetGravityFieldState,
                metadata,
                entityId: entityId,
                gravityFieldPhase: phase,
                gravityFieldTimerTicks: timerTicks);
        }

        public static FinalizationOperation SetUnitKinematicState(
            long sequence,
            int entityId,
            UnitKinematicRuntimeState unitKinematicState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetUnitKinematicState,
                metadata,
                entityId: entityId,
                unitKinematicState: unitKinematicState);
        }

        public static FinalizationOperation SetUnitContinuousLocomotionState(
            long sequence,
            int entityId,
            UnitContinuousLocomotionState unitContinuousLocomotionState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetUnitContinuousLocomotionState,
                metadata,
                entityId: entityId,
                unitContinuousLocomotionState: unitContinuousLocomotionState);
        }

        public static FinalizationOperation SetEntityLocomotionLeaseState(
            long sequence,
            int entityId,
            EntityLocomotionLeaseState entityLocomotionLeaseState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEntityLocomotionLeaseState,
                metadata,
                entityId: entityId,
                entityLocomotionLeaseState: entityLocomotionLeaseState);
        }

        public static FinalizationOperation SetPhasedState(long sequence, int entityId, PhasedRuntimeState phasedState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetPhasedState,
                metadata,
                entityId: entityId,
                phasedState: phasedState);
        }

        public static FinalizationOperation SpawnEntity(
            long sequence,
            EntityState entity,
            FinalizationOperationMetadata metadata = default,
            bool hasSpawnedEntitySummonedState = false,
            SummonedEntityState spawnedEntitySummonedState = default,
            bool hasSpawnedEntityEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState spawnedEntityEnemyDefinitionBindingState = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Spawn,
                FinalizationOperationKind.SpawnEntity,
                metadata,
                spawnEntity: entity,
                hasSpawnedEntitySummonedState: hasSpawnedEntitySummonedState,
                spawnedEntitySummonedState: spawnedEntitySummonedState,
                hasSpawnedEntityEnemyDefinitionBindingState: hasSpawnedEntityEnemyDefinitionBindingState,
                spawnedEntityEnemyDefinitionBindingState: spawnedEntityEnemyDefinitionBindingState);
        }

        public static FinalizationOperation ApplyDamage(long sequence, int entityId, int amount, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DamageState,
                FinalizationOperationKind.ApplyDamage,
                metadata,
                entityId: entityId,
                amount: amount);
        }

        public static FinalizationOperation MarkDestroy(long sequence, int entityId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Destroy,
                FinalizationOperationKind.MarkDestroy,
                metadata,
                entityId: entityId);
        }

        public static FinalizationOperation EnqueueDelayedAttackEffect(
            long sequence,
            DelayedAttackEffectRecord delayedAttackEffect,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DelayedEnqueue,
                FinalizationOperationKind.EnqueueDelayedAttackEffect,
                metadata,
                delayedAttackEffect: delayedAttackEffect);
        }

        public static FinalizationOperation PoseMutation(
            long sequence,
            EntityPoseMutationOperation operation,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.PoseMutation,
                metadata,
                entityId: operation.Request.EntityId,
                poseMutationOperation: operation);
        }
    }

    internal sealed class FinalizationBatch
    {
        private const string MovementPresentationRecordSource = "MovementCommit";

        private readonly List<FinalizationOperation> _operations = new();
        private readonly List<MovementPresentationRecord> _movementPresentationRecords = new();
        private readonly List<string> _movementPresentationDiagnostics = new();
        private readonly List<TileFeatureOperation> _tileFeatureOperations = new();
        private readonly List<string> _poseMutationDiagnostics = new();
        private readonly List<string> _entityLocomotionLeaseDiagnostics = new();
        private long _nextSequence = 1;

        public IReadOnlyList<FinalizationOperation> Operations => _operations;

        public IReadOnlyList<MovementPresentationRecord> MovementPresentationRecords => _movementPresentationRecords;

        public IReadOnlyList<string> MovementPresentationDiagnostics => _movementPresentationDiagnostics;

        public IReadOnlyList<TileFeatureOperation> TileFeatureOperations => _tileFeatureOperations;

        public IReadOnlyList<string> PoseMutationDiagnostics => _poseMutationDiagnostics;

        public IReadOnlyList<string> EntityLocomotionLeaseDiagnostics => _entityLocomotionLeaseDiagnostics;

        public void MoveEntity(int entityId, SurfaceCell destination, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.MoveEntity(_nextSequence++, entityId, destination, metadata));
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ApplyStateChange(_nextSequence++, entityId, state, stateTimer, metadata));
        }

        public void SetFacing(int entityId, Direction facing, FinalizationOperationMetadata metadata = default)
        {
            RecordDirectPoseWriteWarningIfNeeded(entityId, "SetFacing", metadata);
            _operations.Add(FinalizationOperation.SetFacing(_nextSequence++, entityId, facing, metadata));
        }

        public void AddPoseMutation(EntityPoseMutationOperation operation, FinalizationOperationMetadata metadata = default)
        {
            var request = EnsureOperationId(operation.Request, metadata);
            var resolvedOperation = new EntityPoseMutationOperation(request, operation.UnitKinematicState);
            var decision = EntityPoseMutationAuthority.Decide(request);
            _poseMutationDiagnostics.Add(FormatPoseMutationDiagnostic(request, decision));
            if (request.Source == PoseMutationSource.MovementCommit)
            {
                var created = TryCreateMovementPresentationRecord(
                    request,
                    decision,
                    out var movementPresentationRecord,
                    out var movementPresentationReason);
                _movementPresentationDiagnostics.Add(
                    FormatMovementPresentationDiagnostic(
                        request,
                        created,
                        movementPresentationReason));
                if (created)
                {
                    _movementPresentationRecords.Add(movementPresentationRecord);
                }
            }

            if (!decision.Allowed)
            {
                return;
            }

            RecordDirectPoseWriteWarningIfNeeded(request.EntityId, "AddPoseMutation", metadata);
            _operations.Add(FinalizationOperation.PoseMutation(_nextSequence++, resolvedOperation, metadata));
        }

        public void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetBoxKineticOwner(_nextSequence++, entityId, instigatorEntityId, instigatorTeamId, metadata));
        }

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetBoardPresence(_nextSequence++, entityId, boardPresence, metadata));
        }

        public void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyLocomotionCooldown(_nextSequence++, entityId, cooldownTicks, metadata));
        }

        public void SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyAttackCooldown(_nextSequence++, entityId, cooldownTicks, totalTicks, metadata));
        }

        public void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEntityExecutionLockState(_nextSequence++, entityId, state, metadata));
        }

        public void SetTopology(CubeTopologyState topology, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetTopology(_nextSequence++, topology, metadata));
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPlayerControlState(_nextSequence++, entityId, state, metadata));
        }

        public void SetPlayerDamageState(int entityId, PlayerDamageState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPlayerDamageState(_nextSequence++, entityId, state, metadata));
        }

        public void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ApplyEnemyAiState(_nextSequence++, entityId, aiMode, aiStateTimer, metadata));
        }

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyActionState(_nextSequence++, entityId, state, metadata));
        }

        public void AddPendingCellImpact(PendingCellImpact impact, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.AddPendingCellImpact(_nextSequence++, impact, metadata));
        }

        public void RemovePendingCellImpact(int impactId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.RemovePendingCellImpact(_nextSequence++, impactId, metadata));
        }

        public void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyPatrolState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyJumpState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyGlideState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyChargeState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyUtilityState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyFrontFaceSupportState(_nextSequence++, entityId, state, metadata));
        }

        public void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetBoxInteractionLockState(_nextSequence++, entityId, state, metadata));
        }

        public void RemoveBoxInteractionLockState(int entityId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.RemoveBoxInteractionLockState(_nextSequence++, entityId, metadata));
        }

        public void SetEnemyGravityFieldAuraFieldState(
            int fieldId,
            EnemyGravityFieldAuraFieldState state,
            FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyGravityFieldAuraFieldState(_nextSequence++, fieldId, state, metadata));
        }

        public void RemoveEnemyGravityFieldAuraFieldState(
            int fieldId,
            FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.RemoveEnemyGravityFieldAuraFieldState(_nextSequence++, fieldId, metadata));
        }

        public void SetPendingEnemyBlockedReaction(
            int entityId,
            PendingEnemyBlockedReaction reaction,
            FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPendingEnemyBlockedReaction(_nextSequence++, entityId, reaction, metadata));
        }

        public void ClearPendingEnemyBlockedReaction(int entityId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ClearPendingEnemyBlockedReaction(_nextSequence++, entityId, metadata));
        }

        public void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetGravityFieldState(_nextSequence++, entityId, phase, timerTicks, metadata));
        }

        public void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetUnitKinematicState(_nextSequence++, entityId, state, metadata));
        }

        public void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetUnitContinuousLocomotionState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEntityLocomotionLeaseState(int entityId, EntityLocomotionLeaseState state, FinalizationOperationMetadata metadata = default)
        {
            _entityLocomotionLeaseDiagnostics.Add(FormatEntityLocomotionLeaseDiagnostic(entityId, state));
            _operations.Add(FinalizationOperation.SetEntityLocomotionLeaseState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEntityLocomotionLeaseState(
            int entityId,
            EntityLocomotionLeaseState state,
            EntityLocomotionLeaseDiagnosticContext diagnosticContext,
            FinalizationOperationMetadata metadata = default)
        {
            _entityLocomotionLeaseDiagnostics.Add(
                diagnosticContext.HasValue
                    ? FormatEntityLocomotionLeaseDiagnostic(entityId, state, diagnosticContext)
                    : FormatEntityLocomotionLeaseDiagnostic(entityId, state));
            _operations.Add(FinalizationOperation.SetEntityLocomotionLeaseState(_nextSequence++, entityId, state, metadata));
        }

        public void SetPhasedState(int entityId, PhasedRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPhasedState(_nextSequence++, entityId, state, metadata));
        }

        public void SpawnEntity(
            EntityState entity,
            FinalizationOperationMetadata metadata = default,
            bool hasSummonedEntityState = false,
            SummonedEntityState summonedEntityState = default,
            bool hasEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState enemyDefinitionBindingState = default)
        {
            _operations.Add(
                FinalizationOperation.SpawnEntity(
                    _nextSequence++,
                    entity,
                    metadata,
                    hasSummonedEntityState,
                    summonedEntityState,
                    hasEnemyDefinitionBindingState,
                    enemyDefinitionBindingState));
        }

        public void ApplyDamage(int entityId, int amount, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ApplyDamage(_nextSequence++, entityId, amount, metadata));
        }

        public void MarkDestroy(int entityId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.MarkDestroy(_nextSequence++, entityId, metadata));
        }

        public void EnqueueDelayedAttackEffect(
            DelayedAttackEffectRecord effectRecord,
            FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.EnqueueDelayedAttackEffect(_nextSequence++, effectRecord, metadata));
        }

        public void ApplyTileFeatureOperations(TileFeatureOperationBatch batch)
        {
            var resolvedBatch = batch ?? throw new ArgumentNullException(nameof(batch));
            if (resolvedBatch.IsEmpty)
            {
                return;
            }

            var operations = resolvedBatch.Operations;
            for (var i = 0; i < operations.Count; i++)
            {
                _tileFeatureOperations.Add(operations[i]);
            }
        }

        public void MergeFrom(FinalizationBatch batch, bool includeTileFeatureOperations = true)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            for (var i = 0; i < batch._operations.Count; i++)
            {
                _operations.Add(batch._operations[i].WithSequence(_nextSequence++));
            }

            for (var i = 0; i < batch._poseMutationDiagnostics.Count; i++)
            {
                _poseMutationDiagnostics.Add(batch._poseMutationDiagnostics[i]);
            }

            for (var i = 0; i < batch._movementPresentationRecords.Count; i++)
            {
                _movementPresentationRecords.Add(batch._movementPresentationRecords[i]);
            }

            for (var i = 0; i < batch._movementPresentationDiagnostics.Count; i++)
            {
                _movementPresentationDiagnostics.Add(batch._movementPresentationDiagnostics[i]);
            }

            for (var i = 0; i < batch._entityLocomotionLeaseDiagnostics.Count; i++)
            {
                _entityLocomotionLeaseDiagnostics.Add(batch._entityLocomotionLeaseDiagnostics[i]);
            }

            if (!includeTileFeatureOperations)
            {
                return;
            }

            for (var i = 0; i < batch._tileFeatureOperations.Count; i++)
            {
                _tileFeatureOperations.Add(batch._tileFeatureOperations[i]);
            }
        }

        public void ApplyTo(IWorldWriteContext writeContext, IDelayedAttackEffectSink delayedAttackEffectSink)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            ApplyBucket(writeContext, FinalizationOperationBucket.NonHpState);
            ApplyTileFeatureOperations(writeContext);
            ApplyBucket(writeContext, FinalizationOperationBucket.DamageState);
            ApplyBucket(writeContext, FinalizationOperationBucket.Spawn);
            ApplyBucket(writeContext, FinalizationOperationBucket.Destroy);
            ApplyBucket(writeContext, FinalizationOperationBucket.DelayedEnqueue, delayedAttackEffectSink);
        }

        private void ApplyTileFeatureOperations(IWorldWriteContext writeContext)
        {
            for (var i = 0; i < _tileFeatureOperations.Count; i++)
            {
                var operation = _tileFeatureOperations[i];
                switch (operation.Kind)
                {
                    case TileFeatureOperationKind.Add:
                        writeContext.AddTileFeature(operation.State);
                        break;

                    case TileFeatureOperationKind.Update:
                        writeContext.UpdateTileFeature(operation.State);
                        break;

                    case TileFeatureOperationKind.Remove:
                        writeContext.RemoveTileFeature(operation.TileId);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void ApplyBucket(
            IWorldWriteContext writeContext,
            FinalizationOperationBucket bucket,
            IDelayedAttackEffectSink delayedAttackEffectSink = null)
        {
            for (var i = 0; i < _operations.Count; i++)
            {
                var operation = _operations[i];
                if (operation.Bucket != bucket)
                {
                    continue;
                }

                switch (operation.Kind)
                {
                    case FinalizationOperationKind.MoveEntity:
                        ((IMovementCommitContext)writeContext).MoveEntity(operation.EntityId, operation.Destination);
                        break;

                    case FinalizationOperationKind.ApplyStateChange:
                        ((IMovementCommitContext)writeContext).ApplyStateChange(operation.EntityId, operation.PhaseState, operation.StateTimer);
                        break;

                    case FinalizationOperationKind.SetFacing:
                        ((IEnemyAiCommitContext)writeContext).SetFacing(operation.EntityId, operation.Facing);
                        break;

                    case FinalizationOperationKind.SetBoxKineticOwner:
                        writeContext.SetBoxKineticOwner(
                            operation.EntityId,
                            operation.InstigatorEntityId,
                            operation.InstigatorTeamId);
                        break;

                    case FinalizationOperationKind.SetBoardPresence:
                        ((IMovementCommitContext)writeContext).SetBoardPresence(operation.EntityId, operation.BoardPresence);
                        break;

                    case FinalizationOperationKind.SetEnemyLocomotionCooldown:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyLocomotionCooldown(operation.EntityId, operation.CooldownTicks);
                        break;

                    case FinalizationOperationKind.SetEnemyAttackCooldown:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyAttackCooldown(
                            operation.EntityId,
                            operation.CooldownTicks,
                            operation.CooldownTotalTicks);
                        break;

                    case FinalizationOperationKind.SetEntityExecutionLockState:
                        ((IMovementCommitContext)writeContext).SetEntityExecutionLockState(operation.EntityId, operation.ExecutionLockState);
                        break;

                    case FinalizationOperationKind.SetTopology:
                        ((IMovementCommitContext)writeContext).SetTopology(operation.Topology);
                        break;

                    case FinalizationOperationKind.SetPlayerControlState:
                        ((IPlayerControlCommitContext)writeContext).SetPlayerControlState(operation.EntityId, operation.PlayerControlState);
                        break;

                    case FinalizationOperationKind.SetPlayerDamageState:
                        ((IPlayerDamageCommitContext)writeContext).SetPlayerDamageState(operation.EntityId, operation.PlayerDamageState);
                        break;

                    case FinalizationOperationKind.ApplyEnemyAiState:
                        ((IEnemyAiCommitContext)writeContext).ApplyEnemyAiState(operation.EntityId, operation.EnemyAiMode, operation.EnemyAiStateTimer);
                        break;

                    case FinalizationOperationKind.SetEnemyActionState:
                        ((IEnemyActionCommitContext)writeContext).SetEnemyActionState(operation.EntityId, operation.EnemyActionState);
                        break;

                    case FinalizationOperationKind.AddPendingCellImpact:
                        ((IEnemyActionCommitContext)writeContext).AddPendingCellImpact(operation.PendingCellImpact);
                        break;

                    case FinalizationOperationKind.SetEnemyPatrolState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyPatrolState(operation.EntityId, operation.EnemyPatrolState);
                        break;

                    case FinalizationOperationKind.SetEnemyJumpState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyJumpState(operation.EntityId, operation.EnemyJumpState);
                        break;

                    case FinalizationOperationKind.SetEnemyGlideState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyGlideState(operation.EntityId, operation.EnemyGlideState);
                        break;

                    case FinalizationOperationKind.SetEnemyChargeState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyChargeState(operation.EntityId, operation.EnemyChargeState);
                        break;

                    case FinalizationOperationKind.SetEnemyUtilityState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyUtilityState(operation.EntityId, operation.EnemyUtilityState);
                        break;

                    case FinalizationOperationKind.SetEnemyFrontFaceSupportState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyFrontFaceSupportState(operation.EntityId, operation.EnemyFrontFaceSupportState);
                        break;

                    case FinalizationOperationKind.SetBoxInteractionLockState:
                        writeContext.SetBoxInteractionLockState(operation.EntityId, operation.BoxInteractionLockState);
                        break;

                    case FinalizationOperationKind.RemoveBoxInteractionLockState:
                        writeContext.RemoveBoxInteractionLockState(operation.EntityId);
                        break;

                    case FinalizationOperationKind.SetEnemyGravityFieldAuraFieldState:
                        writeContext.SetEnemyGravityFieldAuraFieldState(
                            operation.EntityId,
                            operation.EnemyGravityFieldAuraFieldState);
                        break;

                    case FinalizationOperationKind.RemoveEnemyGravityFieldAuraFieldState:
                        writeContext.RemoveEnemyGravityFieldAuraFieldState(operation.EntityId);
                        break;

                    case FinalizationOperationKind.SetPendingEnemyBlockedReaction:
                        ((IPreMovementStateCommitContext)writeContext).SetPendingEnemyBlockedReaction(
                            operation.EntityId,
                            operation.PendingEnemyBlockedReaction);
                        break;

                    case FinalizationOperationKind.ClearPendingEnemyBlockedReaction:
                        ((IPreMovementStateCommitContext)writeContext).ClearPendingEnemyBlockedReaction(operation.EntityId);
                        break;

                    case FinalizationOperationKind.SetGravityFieldState:
                        ((IPreMovementStateCommitContext)writeContext).SetGravityFieldState(
                            operation.EntityId,
                            operation.GravityFieldPhase,
                            operation.GravityFieldTimerTicks);
                        break;

                    case FinalizationOperationKind.SetUnitKinematicState:
                        writeContext.SetUnitKinematicState(operation.EntityId, operation.UnitKinematicState);
                        break;

                    case FinalizationOperationKind.SetUnitContinuousLocomotionState:
                        writeContext.SetUnitContinuousLocomotionState(operation.EntityId, operation.UnitContinuousLocomotionState);
                        break;

                    case FinalizationOperationKind.SetEntityLocomotionLeaseState:
                        writeContext.SetEntityLocomotionLeaseState(operation.EntityId, operation.EntityLocomotionLeaseState);
                        break;

                    case FinalizationOperationKind.PoseMutation:
                        ApplyPoseMutation(writeContext, operation.PoseMutationOperation);
                        break;

                    case FinalizationOperationKind.SetPhasedState:
                        ((IPhasedStateCommitContext)writeContext).SetPhasedState(operation.EntityId, operation.PhasedState);
                        break;

                    case FinalizationOperationKind.SpawnEntity:
                        ((IAttackCommitContext)writeContext).SpawnEntity(operation.SpawnedEntity);
                        if (operation.HasSpawnedEntitySummonedState)
                        {
                            writeContext.SetSummonedEntityState(operation.SpawnedEntity.entityId, operation.SpawnedEntitySummonedState);
                        }
                        if (operation.HasSpawnedEntityEnemyDefinitionBindingState)
                        {
                            writeContext.SetEnemyDefinitionBindingState(
                                operation.SpawnedEntity.entityId,
                                operation.SpawnedEntityEnemyDefinitionBindingState);
                        }
                        break;

                    case FinalizationOperationKind.ApplyDamage:
                        ((IAttackCommitContext)writeContext).ApplyDamage(operation.EntityId, operation.Amount);
                        break;

                    case FinalizationOperationKind.MarkDestroy:
                        ((IAttackCommitContext)writeContext).MarkDestroy(operation.EntityId);
                        break;

                    case FinalizationOperationKind.RemovePendingCellImpact:
                        ((IAttackCommitContext)writeContext).RemovePendingCellImpact(operation.EntityId);
                        break;

                    case FinalizationOperationKind.EnqueueDelayedAttackEffect:
                        if (delayedAttackEffectSink != null)
                        {
                            delayedAttackEffectSink.Enqueue(operation.DelayedAttackEffect);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void ApplyPoseMutation(
            IWorldWriteContext writeContext,
            EntityPoseMutationOperation operation)
        {
            var decision = EntityPoseMutationAuthority.Decide(operation.Request);
            if (!decision.Allowed)
            {
                return;
            }

            if (decision.AppliesPosition)
            {
                ((IMovementCommitContext)writeContext).MoveEntity(operation.Request.EntityId, operation.Request.ToCell);
            }

            if (decision.AppliesFacing)
            {
                ((IEnemyAiCommitContext)writeContext).SetFacing(operation.Request.EntityId, operation.Request.FacingAfter);
            }

            if (decision.AppliesKinematic)
            {
                writeContext.SetUnitKinematicState(operation.Request.EntityId, operation.UnitKinematicState);
            }
        }

        private EntityPoseMutationRequest EnsureOperationId(
            EntityPoseMutationRequest request,
            FinalizationOperationMetadata metadata)
        {
            if (request.OperationId != 0)
            {
                return request;
            }

            return new EntityPoseMutationRequest
            {
                EntityId = request.EntityId,
                Source = request.Source,
                Kind = request.Kind,
                FromCell = request.FromCell,
                ToCell = request.ToCell,
                PositionChanged = request.PositionChanged,
                FacingBefore = request.FacingBefore,
                FacingAfter = request.FacingAfter,
                MovementDirection = request.MovementDirection,
                MovementIntentExists = request.MovementIntentExists,
                MovementAccepted = request.MovementAccepted,
                MovementSuppressed = request.MovementSuppressed,
                HasExplicitActionFacing = request.HasExplicitActionFacing,
                HasExplicitSkillFacing = request.HasExplicitSkillFacing,
                HasExplicitRotateAction = request.HasExplicitRotateAction,
                KinematicMutation = request.KinematicMutation,
                KinematicDirection = request.KinematicDirection,
                HasKinematicDirection = request.HasKinematicDirection,
                KinematicDirectionKind = request.KinematicDirectionKind,
                KinematicFacingPolicy = request.KinematicFacingPolicy,
                ShouldUpdateFacing = request.ShouldUpdateFacing,
                TickIndex = request.TickIndex,
                OperationId = unchecked((request.TickIndex * 397) ^ (request.EntityId * 31) ^ ((int)request.Source * 17) ^ (int)_nextSequence),
                MovementIntentId = request.MovementIntentId != 0 ? request.MovementIntentId : metadata.IntentId,
                MovementResolutionId = request.MovementResolutionId != 0 ? request.MovementResolutionId : metadata.ContestId,
                ActionSequenceId = request.ActionSequenceId,
                Writer = request.Writer,
                Reason = request.Reason,
                UsesSyntheticOperationId = true,
            };
        }

        private static string FormatPoseMutationDiagnostic(
            in EntityPoseMutationRequest request,
            in EntityPoseMutationDecision decision)
        {
            return
                "[EntityPoseMutation]" +
                $"Tick={request.TickIndex}" +
                $"|Entity={request.EntityId}" +
                $"|Source={request.Source}" +
                $"|Kind={request.Kind}" +
                $"|Writer={request.Writer}" +
                $"|Reason={request.Reason}" +
                $"|OperationId={request.OperationId}" +
                $"|SyntheticOperationId={(request.UsesSyntheticOperationId ? 1 : 0)}" +
                $"|MovementIntentId={request.MovementIntentId}" +
                $"|MovementResolutionId={request.MovementResolutionId}" +
                $"|ActionSeq={request.ActionSequenceId}" +
                $"|FromCell={request.FromCell}" +
                $"|ToCell={request.ToCell}" +
                $"|PositionChanged={(request.PositionChanged ? 1 : 0)}" +
                $"|FacingBefore={request.FacingBefore}" +
                $"|FacingAfter={request.FacingAfter}" +
                $"|MovementDirection={request.MovementDirection}" +
                $"|MovementAccepted={(request.MovementAccepted ? 1 : 0)}" +
                $"|MovementSuppressed={(request.MovementSuppressed ? 1 : 0)}" +
                $"|HasExplicitActionFacing={(request.HasExplicitActionFacing ? 1 : 0)}" +
                $"|HasExplicitSkillFacing={(request.HasExplicitSkillFacing ? 1 : 0)}" +
                $"|HasExplicitRotateAction={(request.HasExplicitRotateAction ? 1 : 0)}" +
                $"|KinematicMutation={request.KinematicMutation}" +
                $"|KinematicDirection={request.KinematicDirection}" +
                $"|HasKinematicDirection={(request.HasKinematicDirection ? 1 : 0)}" +
                $"|DirectionKind={request.KinematicDirectionKind}" +
                $"|FacingPolicy={request.KinematicFacingPolicy}" +
                $"|ShouldUpdateFacing={(request.ShouldUpdateFacing ? 1 : 0)}" +
                $"|Allowed={(decision.Allowed ? 1 : 0)}" +
                $"|RejectReason={decision.RejectReason}";
        }

        private static string FormatEntityLocomotionLeaseDiagnostic(
            int entityId,
            in EntityLocomotionLeaseState state)
        {
            var normalized = state.NormalizedForStorage();
            var operation = ResolveLeaseDiagnosticOperation(normalized);
            var policy = ResolveLeaseDiagnosticPolicy(normalized);
            var tickIndex = operation == "Acquire"
                ? normalized.acquiredTick
                : normalized.lastReleaseTick;
            var stateBefore = operation == "Acquire"
                ? EntityLocomotionLeaseStateKind.None
                : EntityLocomotionLeaseStateKind.HeldByOwner;
            var kinematicModeBefore = operation == "Acquire"
                ? normalized.capturedKinematic.mode
                : MotionMode.Held;
            var kinematicModeAfter = ResolveLeaseDiagnosticKinematicModeAfter(operation, policy);
            var isSettledAtAnchor = kinematicModeAfter == MotionMode.Settled ||
                                    kinematicModeAfter == MotionMode.LegacyDiscrete;
            return Game.Feature.Gameplay.BoardState.EntityLocomotionLeaseDiagnostics.FormatOperation(
                tickIndex,
                entityId,
                operation,
                normalized.ownerKind,
                normalized.ownerActionSequenceId,
                normalized.leaseId,
                stateBefore,
                normalized.stateKind,
                normalized.lastReleaseReason,
                normalized.pendingReleaseReason,
                normalized.finalReleaseReason,
                policy,
                0,
                normalized.IsTerminal,
                normalized.IsTerminal,
                kinematicModeBefore,
                kinematicModeAfter,
                hasAuthoritativeState: true,
                isSettledAtAnchor: isSettledAtAnchor,
                lastReleaseTick: normalized.lastReleaseTick,
                lastReleaseReason: normalized.lastReleaseReason);
        }

        private static string FormatEntityLocomotionLeaseDiagnostic(
            int entityId,
            in EntityLocomotionLeaseState state,
            in EntityLocomotionLeaseDiagnosticContext diagnosticContext)
        {
            var normalized = state.NormalizedForStorage();
            return Game.Feature.Gameplay.BoardState.EntityLocomotionLeaseDiagnostics.FormatOperation(
                diagnosticContext.TickIndex,
                entityId,
                diagnosticContext.Operation,
                normalized.ownerKind,
                normalized.ownerActionSequenceId,
                normalized.leaseId,
                diagnosticContext.StateBefore,
                diagnosticContext.StateAfter,
                diagnosticContext.Reason,
                diagnosticContext.PendingReleaseReason,
                diagnosticContext.FinalReleaseReason,
                diagnosticContext.Policy,
                diagnosticContext.RecoverRemainingTicks,
                diagnosticContext.RecoverComplete,
                diagnosticContext.ActualKinematicReleaseEmitted,
                diagnosticContext.KinematicModeBefore,
                diagnosticContext.KinematicModeAfter,
                diagnosticContext.HasAuthoritativeState,
                diagnosticContext.IsSettledAtAnchor,
                normalized.lastReleaseTick,
                normalized.lastReleaseReason);
        }

        private static string ResolveLeaseDiagnosticOperation(in EntityLocomotionLeaseState state)
        {
            if (state.stateKind == EntityLocomotionLeaseStateKind.HeldByOwner &&
                state.lastReleaseTick == 0)
            {
                return "Acquire";
            }

            if (state.stateKind == EntityLocomotionLeaseStateKind.Orphaned)
            {
                return "OrphanedKinematicNotSettled";
            }

            if (!state.IsTerminal)
            {
                return state.stateKind == EntityLocomotionLeaseStateKind.ReleaseRequested
                    ? "RequestRelease"
                    : "SkipRelease";
            }

            return IsTerminalLeaseReason(state.lastReleaseReason)
                ? "TerminateImmediate"
                : "FinalizeRelease";
        }

        private static string ResolveLeaseDiagnosticPolicy(in EntityLocomotionLeaseState state)
        {
            if (!state.IsTerminal)
            {
                return string.Empty;
            }

            if (IsTerminalLeaseReason(state.lastReleaseReason))
            {
                return state.lastReleaseReason == EntityLocomotionLeaseReleaseReason.TopologyNonParticipant
                    ? EntityLocomotionLeaseReleasePolicy.ForceSettledAtCurrentAnchor.ToString()
                    : EntityLocomotionLeaseReleasePolicy.ClearStaleHold.ToString();
            }

            if (TryResolveLeaseDiagnosticResumeVelocity(state.capturedKinematic, out _))
            {
                return EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary.ToString();
            }

            return EntityLocomotionLeaseReleasePolicy.ClearStaleHold.ToString();
        }

        private static bool IsTerminalLeaseReason(EntityLocomotionLeaseReleaseReason reason)
        {
            return reason == EntityLocomotionLeaseReleaseReason.TopologyNonParticipant ||
                   reason == EntityLocomotionLeaseReleaseReason.NoCombatCapability ||
                   reason == EntityLocomotionLeaseReleaseReason.SourceInactive ||
                   reason == EntityLocomotionLeaseReleaseReason.AiModeNonAttack ||
                   reason == EntityLocomotionLeaseReleaseReason.ActionLogicClear ||
                   reason == EntityLocomotionLeaseReleaseReason.EntityRemoved ||
                   reason == EntityLocomotionLeaseReleaseReason.StageReset;
        }

        private static MotionMode ResolveLeaseDiagnosticKinematicModeAfter(string operation, string policy)
        {
            if (operation == "Acquire")
            {
                return MotionMode.Held;
            }

            return policy == EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary.ToString()
                ? MotionMode.Voluntary
                : MotionMode.Settled;
        }

        private static bool TryResolveLeaseDiagnosticResumeVelocity(
            in UnitKinematicRuntimeState state,
            out KinematicVelocity2 velocity)
        {
            velocity = default;
            if (state.totalTicks <= 0 ||
                (state.stepDirectionX == 0 && state.stepDirectionY == 0))
            {
                return false;
            }

            velocity = new KinematicVelocity2(
                KinematicFixed.FromRaw(state.stepDirectionX * KinematicFixed.UnitsPerCell / state.totalTicks),
                KinematicFixed.FromRaw(state.stepDirectionY * KinematicFixed.UnitsPerCell / state.totalTicks));
            return true;
        }

        private static bool TryCreateMovementPresentationRecord(
            in EntityPoseMutationRequest request,
            in EntityPoseMutationDecision decision,
            out MovementPresentationRecord record,
            out string reason)
        {
            if (!decision.Allowed)
            {
                record = default;
                reason = string.IsNullOrEmpty(decision.RejectReason)
                    ? "RejectedByPoseAuthority"
                    : decision.RejectReason;
                return false;
            }

            if (request.Source != PoseMutationSource.MovementCommit)
            {
                record = default;
                reason = "NotMovementCommit";
                return false;
            }

            if (request.Kind != PoseMutationKind.PositionAndFacing)
            {
                record = default;
                reason = request.Kind.ToString();
                return false;
            }

            if (!request.PositionChanged)
            {
                record = default;
                reason = "NoPositionChange";
                return false;
            }

            if (!request.MovementAccepted)
            {
                record = default;
                reason = "MovementRejected";
                return false;
            }

            if (request.MovementSuppressed)
            {
                record = default;
                reason = "MovementSuppressed";
                return false;
            }

            record = new MovementPresentationRecord(
                request.EntityId,
                request.TickIndex,
                request.OperationId,
                request.MovementIntentId,
                request.MovementResolutionId,
                request.FromCell,
                request.ToCell,
                request.MovementDirection,
                request.PositionChanged,
                request.MovementAccepted,
                MovementPresentationRecordSource,
                request.UsesSyntheticOperationId);
            reason = "Created";
            return true;
        }

        private static string FormatMovementPresentationDiagnostic(
            in EntityPoseMutationRequest request,
            bool created,
            string reason)
        {
            return
                "[MovementPresentationRecord]" +
                $"Tick={request.TickIndex}" +
                $"|Entity={request.EntityId}" +
                $"|OperationId={request.OperationId}" +
                $"|SyntheticOperationId={(request.UsesSyntheticOperationId ? 1 : 0)}" +
                $"|MovementIntentId={request.MovementIntentId}" +
                $"|MovementResolutionId={request.MovementResolutionId}" +
                $"|FromCell={request.FromCell}" +
                $"|ToCell={request.ToCell}" +
                $"|Direction={request.MovementDirection}" +
                $"|Created={(created ? 1 : 0)}" +
                $"|Reason={reason}";
        }

        private void RecordDirectPoseWriteWarningIfNeeded(
            int entityId,
            string writer,
            FinalizationOperationMetadata metadata)
        {
            for (var i = 0; i < _operations.Count; i++)
            {
                var operation = _operations[i];
                if (operation.EntityId != entityId ||
                    operation.Kind != FinalizationOperationKind.PoseMutation)
                {
                    continue;
                }

                _poseMutationDiagnostics.Add(
                    "[EntityPoseMutation]" +
                    $"Tick=0|Entity={entityId}|Source=None|Kind=None|Writer={writer}|Reason=DirectPoseWriteSharesEntityWithPoseMutation" +
                    $"|OperationId=0|SyntheticOperationId=0|MovementIntentId={metadata.IntentId}|MovementResolutionId={metadata.ContestId}" +
                    "|ActionSeq=0|PositionChanged=0|MovementAccepted=0|MovementSuppressed=0|Allowed=0|RejectReason=DirectPoseWriteSharesEntityWithPoseMutation");
                return;
            }
        }
    }

    internal sealed class RecordingFinalizationContext : IWorldWriteContext
    {
        private readonly FinalizationBatch _batch;
        private readonly TickPhase _originPhase;
        private readonly WorldSnapshot _referenceSnapshot;
        private readonly List<EnemyUtilityTriggerIntent> _utilityTriggerIntents;

        public RecordingFinalizationContext(
            FinalizationBatch batch,
            WorldSnapshot referenceSnapshot = null,
            TickPhase originPhase = TickPhase.Resolve,
            List<EnemyUtilityTriggerIntent> utilityTriggerIntents = null)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
            _referenceSnapshot = referenceSnapshot;
            _originPhase = originPhase;
            _utilityTriggerIntents = utilityTriggerIntents;
        }

        public void MoveEntity(int entityId, SurfaceCell destination)
        {
            _batch.MoveEntity(entityId, destination);
        }

        public void ApplyDamage(int entityId, int amount)
        {
            _batch.ApplyDamage(entityId, amount);
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            _batch.ApplyStateChange(entityId, state, stateTimer);
        }

        public void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer)
        {
            _batch.ApplyEnemyAiState(entityId, aiMode, aiStateTimer);
        }

        public void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks)
        {
            _batch.SetEnemyLocomotionCooldown(entityId, cooldownTicks);
        }

        public void SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks)
        {
            _batch.SetEnemyAttackCooldown(entityId, cooldownTicks, totalTicks);
        }

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            _batch.SetEnemyActionState(entityId, state);
        }

        public void AddPendingCellImpact(PendingCellImpact impact)
        {
            _batch.AddPendingCellImpact(impact);
        }

        public void RemovePendingCellImpact(int impactId)
        {
            _batch.RemovePendingCellImpact(impactId);
        }

        public void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state)
        {
            _batch.SetEnemyPatrolState(entityId, state);
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            _batch.SetEnemyJumpState(entityId, state, CreateJumpStateMetadata(entityId, state));
        }

        public void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state)
        {
            _batch.SetEnemyGlideState(entityId, state);
        }

        public void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state)
        {
            _batch.SetEnemyUtilityState(entityId, state);
        }

        public void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state)
        {
            _batch.SetEnemyFrontFaceSupportState(entityId, state);
        }

        public void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state)
        {
            _batch.SetBoxInteractionLockState(entityId, state);
        }

        public void SetEnemyGravityFieldAuraFieldState(int fieldId, EnemyGravityFieldAuraFieldState state)
        {
            _batch.SetEnemyGravityFieldAuraFieldState(fieldId, state);
        }

        public void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks)
        {
            _batch.SetGravityFieldState(entityId, phase, timerTicks);
        }

        public void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state)
        {
            _batch.SetEnemyChargeState(entityId, state);
        }

        public void SetPhasedState(int entityId, PhasedRuntimeState state)
        {
            _batch.SetPhasedState(entityId, state, CreatePhasedStateMetadata(entityId));
        }

        public void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state)
        {
            _batch.SetEntityExecutionLockState(entityId, state);
        }

        public void MoveEnemyJumpEntity(int entityId, SurfaceCell destination)
        {
            _batch.MoveEntity(entityId, destination);
        }

        public void SetEnemyJumpBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _batch.SetBoardPresence(entityId, boardPresence);
        }

        public void SetFacing(int entityId, Direction facing)
        {
            _batch.SetFacing(entityId, facing);
        }

        public void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            _batch.SetBoxKineticOwner(entityId, instigatorEntityId, instigatorTeamId);
        }

        public void MarkDestroy(int entityId)
        {
            _batch.MarkDestroy(entityId);
        }

        public void SpawnEntity(EntityState entity)
        {
            _batch.SpawnEntity(entity);
        }

        public void SetSummonedEntityState(int entityId, SummonedEntityState state)
        {
        }

        public void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state)
        {
        }

        public void RemoveEntity(int entityId)
        {
            throw new NotSupportedException("Finalize recording does not support cleanup removes.");
        }

        public void RemoveBoxInteractionLockState(int entityId)
        {
            _batch.RemoveBoxInteractionLockState(entityId);
        }

        public void RemoveEnemyGravityFieldAuraFieldState(int fieldId)
        {
            _batch.RemoveEnemyGravityFieldAuraFieldState(fieldId);
        }

        public void SetPendingEnemyBlockedReaction(int entityId, PendingEnemyBlockedReaction reaction)
        {
            _batch.SetPendingEnemyBlockedReaction(entityId, reaction);
        }

        public void ClearPendingEnemyBlockedReaction(int entityId)
        {
            _batch.ClearPendingEnemyBlockedReaction(entityId);
        }

        public void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state)
        {
            _batch.SetUnitKinematicState(entityId, state);
        }

        public void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state)
        {
            _batch.SetUnitContinuousLocomotionState(entityId, state);
        }

        public void SetEntityLocomotionLeaseState(int entityId, EntityLocomotionLeaseState state)
        {
            _batch.SetEntityLocomotionLeaseState(entityId, state);
        }

        public void SetEntityLocomotionLeaseState(
            int entityId,
            EntityLocomotionLeaseState state,
            EntityLocomotionLeaseDiagnosticContext diagnosticContext)
        {
            _batch.SetEntityLocomotionLeaseState(entityId, state, diagnosticContext);
        }

        public void AddPoseMutation(EntityPoseMutationOperation operation)
        {
            _batch.AddPoseMutation(operation);
        }

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _batch.SetBoardPresence(entityId, boardPresence);
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state)
        {
            _batch.SetPlayerControlState(entityId, state);
        }

        public void SetPlayerDamageState(int entityId, PlayerDamageState state)
        {
            _batch.SetPlayerDamageState(entityId, state);
        }

        public void SetTopology(CubeTopologyState topology)
        {
            _batch.SetTopology(topology);
        }

        public void AddTileFeature(TileFeatureState state)
        {
            _batch.ApplyTileFeatureOperations(new TileFeatureOperationBatch(new[] { TileFeatureOperation.Add(state) }));
        }

        public void UpdateTileFeature(TileFeatureState state)
        {
            _batch.ApplyTileFeatureOperations(new TileFeatureOperationBatch(new[] { TileFeatureOperation.Update(state) }));
        }

        public void RemoveTileFeature(int tileId)
        {
            _batch.ApplyTileFeatureOperations(new TileFeatureOperationBatch(new[] { TileFeatureOperation.Remove(tileId) }));
        }

        public void EmitEnemyUtilityTriggerIntent(EnemyUtilityTriggerIntent intent)
        {
            _utilityTriggerIntents?.Add(intent);
        }

        private FinalizationOperationMetadata CreateJumpStateMetadata(int entityId, in EnemyJumpRuntimeState state)
        {
            var jumpPresentationKind = JumpPresentationKind.None;
            if (_referenceSnapshot == null ||
                !_referenceSnapshot.TryGetEnemyJumpState(entityId, out var previousState))
            {
                if (state.phase == EnemyJumpPhase.Windup)
                {
                    jumpPresentationKind = JumpPresentationKind.WindupStart;
                }
                else if (state.phase == EnemyJumpPhase.Airborne)
                {
                    jumpPresentationKind = JumpPresentationKind.AirborneStart;
                }
            }
            else if (state.phase == EnemyJumpPhase.Windup &&
                     previousState.phase != EnemyJumpPhase.Windup)
            {
                jumpPresentationKind = JumpPresentationKind.WindupStart;
            }
            else if (state.phase == EnemyJumpPhase.Airborne &&
                     previousState.phase != EnemyJumpPhase.Airborne)
            {
                jumpPresentationKind = JumpPresentationKind.AirborneStart;
            }

            return new FinalizationOperationMetadata(
                _originPhase,
                ResolvedActionSemanticKind.JumpLanding,
                entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.JumpLanding,
                damageSourceType: DamageSourceType.None,
                jumpPresentationKind: jumpPresentationKind,
                presentationTargetCell: state.lockedTargetCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion,
                boundaryReason: "EnemyJumpState");
        }

        private FinalizationOperationMetadata CreatePhasedStateMetadata(int entityId)
        {
            return new FinalizationOperationMetadata(
                _originPhase,
                ResolvedActionSemanticKind.None,
                entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.None,
                damageSourceType: DamageSourceType.None);
        }
    }

    internal sealed class RecordingDelayedAttackEffectSink : IDelayedAttackEffectSink
    {
        private readonly FinalizationBatch _batch;

        public RecordingDelayedAttackEffectSink(FinalizationBatch batch)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
        }

        public void Enqueue(DelayedAttackEffectRecord effectRecord)
        {
            _batch.EnqueueDelayedAttackEffect(effectRecord);
        }
    }

}
