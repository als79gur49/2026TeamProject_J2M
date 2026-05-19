using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.BoardState
{
    internal interface IWorldStateMutationPort
    {
        bool TryGetEntity(int entityId, out EntityState entity);
        void MoveEntityTo(int entityId, SurfaceCell destination);
        void SpawnEntity(EntityState entity);
        void RemoveEntity(int entityId);
        void ApplyDamage(int entityId, int amount);
        void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer);
        void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer);
        void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks);
        void SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks);
        void SetEnemyActionState(int entityId, EnemyActionRuntimeState state);
        void AddPendingCellImpact(PendingCellImpact impact);
        void RemovePendingCellImpact(int impactId);
        void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state);
        void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state);
        void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state);
        void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state);
        void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state);
        void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state);
        void SetSummonedEntityState(int entityId, SummonedEntityState state);
        void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state);
        void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state);
        void SetEnemyGravityFieldAuraFieldState(int fieldId, EnemyGravityFieldAuraFieldState state);
        void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks);
        void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state);
        void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state);
        void RemoveBoxInteractionLockState(int entityId);
        void RemoveEnemyGravityFieldAuraFieldState(int fieldId);
        void SetPhasedState(int entityId, PhasedRuntimeState state);
        void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state);
        void MarkDestroy(int entityId);
        void SetFacing(int entityId, Direction facing);
        void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId);
        void SetBoardPresence(int entityId, EntityBoardPresence boardPresence);
        void SetPlayerControlState(int entityId, PlayerControlState state);
        void SetPlayerDamageState(int entityId, PlayerDamageState state);
        void SetTopology(CubeTopologyState topology);
        void AddTileFeature(TileFeatureState state);
        void UpdateTileFeature(TileFeatureState state);
        void RemoveTileFeature(int tileId);
    }
}
