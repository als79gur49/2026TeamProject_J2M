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
        SetEntityExecutionLockState = 6,
        SetTopology = 7,
        SetPlayerControlState = 8,
        SetPlayerDamageState = 9,
        ApplyEnemyAiState = 10,
        SetEnemyActionState = 11,
        SetEnemyPatrolState = 12,
        SetEnemyJumpState = 13,
        SetEnemyChargeState = 14,
        SpawnEntity = 15,
        ApplyDamage = 16,
        MarkDestroy = 17,
        EnqueueDelayedAttackEffect = 18,
        SetPhasedState = 19,
        SetEnemyUtilityState = 20,
        SetBoxInteractionLockState = 21,
        RemoveBoxInteractionLockState = 22,
        SetEnemyGlideState = 23,
        SetEnemyFrontFaceSupportState = 24,
        SetUnitKinematicState = 25,
        SetUnitContinuousLocomotionState = 26,
        SetGravityFieldState = 27,
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

    internal enum MovementSemanticKind
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
            string boundaryReason = null)
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

        public MovementExecutionBoundaryKind MovementExecutionBoundaryKind { get; }

        public string BoundaryReason { get; }
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
            GravityFieldPhase gravityFieldPhase = default,
            int gravityFieldTimerTicks = 0,
            UnitKinematicRuntimeState unitKinematicState = default,
            UnitContinuousLocomotionState unitContinuousLocomotionState = default,
            PhasedRuntimeState phasedState = default,
            EntityState spawnEntity = default,
            bool hasSpawnedEntitySummonedState = false,
            SummonedEntityState spawnedEntitySummonedState = default,
            bool hasSpawnedEntityEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState spawnedEntityEnemyDefinitionBindingState = default,
            DelayedAttackEffectRecord delayedAttackEffect = default)
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
            GravityFieldPhase = gravityFieldPhase;
            GravityFieldTimerTicks = gravityFieldTimerTicks;
            UnitKinematicState = unitKinematicState;
            UnitContinuousLocomotionState = unitContinuousLocomotionState;
            PhasedState = phasedState;
            SpawnedEntity = spawnEntity;
            HasSpawnedEntitySummonedState = hasSpawnedEntitySummonedState;
            SpawnedEntitySummonedState = spawnedEntitySummonedState;
            HasSpawnedEntityEnemyDefinitionBindingState = hasSpawnedEntityEnemyDefinitionBindingState;
            SpawnedEntityEnemyDefinitionBindingState = spawnedEntityEnemyDefinitionBindingState;
            DelayedAttackEffect = delayedAttackEffect;
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

        public EntityExecutionLockState ExecutionLockState { get; }

        public CubeTopologyState Topology { get; }

        public PlayerControlState PlayerControlState { get; }

        public PlayerDamageState PlayerDamageState { get; }

        public EnemyAiMode EnemyAiMode { get; }

        public int EnemyAiStateTimer { get; }

        public EnemyActionRuntimeState EnemyActionState { get; }

        public EnemyPatrolRuntimeState EnemyPatrolState { get; }

        public EnemyJumpRuntimeState EnemyJumpState { get; }

        public EnemyGlideRuntimeState EnemyGlideState { get; }

        public EnemyChargeRuntimeState EnemyChargeState { get; }

        public EnemyUtilityRuntimeState EnemyUtilityState { get; }

        public EnemyFrontFaceSupportRuntimeState EnemyFrontFaceSupportState { get; }

        public BoxInteractionLockState BoxInteractionLockState { get; }

        public GravityFieldPhase GravityFieldPhase { get; }

        public int GravityFieldTimerTicks { get; }

        public UnitKinematicRuntimeState UnitKinematicState { get; }

        public UnitContinuousLocomotionState UnitContinuousLocomotionState { get; }

        public PhasedRuntimeState PhasedState { get; }

        public EntityState SpawnedEntity { get; }

        public bool HasSpawnedEntitySummonedState { get; }

        public SummonedEntityState SpawnedEntitySummonedState { get; }

        public bool HasSpawnedEntityEnemyDefinitionBindingState { get; }

        public EnemyDefinitionBindingState SpawnedEntityEnemyDefinitionBindingState { get; }

        public DelayedAttackEffectRecord DelayedAttackEffect { get; }

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
                GravityFieldPhase,
                GravityFieldTimerTicks,
                UnitKinematicState,
                UnitContinuousLocomotionState,
                PhasedState,
                SpawnedEntity,
                HasSpawnedEntitySummonedState,
                SpawnedEntitySummonedState,
                HasSpawnedEntityEnemyDefinitionBindingState,
                SpawnedEntityEnemyDefinitionBindingState,
                DelayedAttackEffect);
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
    }

    internal sealed class FinalizationBatch
    {
        private readonly List<FinalizationOperation> _operations = new();
        private readonly List<TileFeatureOperation> _tileFeatureOperations = new();
        private long _nextSequence = 1;

        public IReadOnlyList<FinalizationOperation> Operations => _operations;

        public IReadOnlyList<TileFeatureOperation> TileFeatureOperations => _tileFeatureOperations;

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
            _operations.Add(FinalizationOperation.SetFacing(_nextSequence++, entityId, facing, metadata));
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

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            _batch.SetEnemyActionState(entityId, state);
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

        public void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state)
        {
            _batch.SetUnitKinematicState(entityId, state);
        }

        public void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state)
        {
            _batch.SetUnitContinuousLocomotionState(entityId, state);
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
