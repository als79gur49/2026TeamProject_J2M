using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.BoardState
{
    public interface IEnemyAiCommitContext
    {
        void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer);

        void SetFacing(int entityId, Direction facing);
    }

    public interface IEnemyActionCommitContext : IEnemyAiCommitContext
    {
        void SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks);

        void SetEnemyActionState(int entityId, EnemyActionRuntimeState state);

        void AddPendingCellImpact(PendingCellImpact impact);

        void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state);

        void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state);
    }

    public interface IPlayerControlCommitContext
    {
        void SetPlayerControlState(int entityId, PlayerControlState state);
    }

    public interface IPlayerDamageCommitContext
    {
        void SetPlayerDamageState(int entityId, PlayerDamageState state);
    }

    public interface IPreMovementStateCommitContext : IPlayerControlCommitContext
    {
        void SetFacing(int entityId, Direction facing);

        void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks);

        void SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks);

        void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state);

        void SetPendingEnemyBlockedReaction(int entityId, PendingEnemyBlockedReaction reaction);

        void ClearPendingEnemyBlockedReaction(int entityId);

        void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state);

        void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state);

        void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state);

        void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state);

        void SetEnemySummonBehaviorState(int entityId, EnemySummonBehaviorRuntimeState state);

        void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state);

        void SetEnemyGravityFieldAuraFieldState(int fieldId, EnemyGravityFieldAuraFieldState state);

        void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks);
    }

    internal interface IEnemyJumpCommitContext
    {
        void MoveEnemyJumpEntity(int entityId, SurfaceCell destination);

        void SetEnemyJumpBoardPresence(int entityId, EntityBoardPresence boardPresence);
    }

    internal interface IPhasedStateCommitContext
    {
        void SetPhasedState(int entityId, PhasedRuntimeState state);
    }

    internal interface IMovementCommitContext : IPlayerControlCommitContext
    {
        void MoveEntity(int entityId, SurfaceCell destination);

        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void MarkDestroy(int entityId);

        void SetFacing(int entityId, Direction facing);

        void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId);

        void SetBoardPresence(int entityId, EntityBoardPresence boardPresence);

        void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks);

        void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state);

        void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state);

        void SetTopology(CubeTopologyState topology);
    }

    internal interface IAttackCommitContext : IPlayerDamageCommitContext
    {
        void ApplyDamage(int entityId, int amount);

        void RemovePendingCellImpact(int impactId);

        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void MarkDestroy(int entityId);

        void SpawnEntity(EntityState entity);
    }

    internal interface ICleanupCommitContext
    {
        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);

        void RemoveEntity(int entityId);

        void RemoveBoxInteractionLockState(int entityId);

        void RemoveEnemyGravityFieldAuraFieldState(int fieldId);

        void ClearPendingEnemyBlockedReaction(int entityId);
    }

    internal interface IEnemyUtilityTriggerSink
    {
        void EmitEnemyUtilityTriggerIntent(EnemyUtilityTriggerIntent intent);

        void EmitEnemySummonBehaviorTriggerIntent(EnemySummonBehaviorTriggerIntent intent);
    }

    internal interface IRespawnCommitContext : IAttackCommitContext, IPlayerControlCommitContext
    {
    }

    internal interface IWorldWriteContext : IPreMovementStateCommitContext, IEnemyJumpCommitContext, IPhasedStateCommitContext, IMovementCommitContext, IAttackCommitContext, ICleanupCommitContext, IRespawnCommitContext, IEnemyActionCommitContext, IEnemyUtilityTriggerSink
    {
        new void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state);

        void SetSummonedEntityState(int entityId, SummonedEntityState state);

        void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state);

        new void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state);

        new void SetEnemyGravityFieldAuraFieldState(int fieldId, EnemyGravityFieldAuraFieldState state);

        new void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks);

        new void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state);

        new void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state);

        new void RemoveBoxInteractionLockState(int entityId);

        new void RemoveEnemyGravityFieldAuraFieldState(int fieldId);

        void AddTileFeature(TileFeatureState state);

        void UpdateTileFeature(TileFeatureState state);

        void RemoveTileFeature(int tileId);
    }
}
