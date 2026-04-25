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
        void SetEnemyActionState(int entityId, EnemyActionRuntimeState state);
        void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state);
        void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state);
        void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state);
        void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state);
        void SetSummonedEntityState(int entityId, SummonedEntityState state);
        void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state);
        void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state);
        void RemoveBoxInteractionLockState(int entityId);
        void SetPhasedState(int entityId, PhasedRuntimeState state);
        void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state);
        void MarkDestroy(int entityId);
        void SetFacing(int entityId, Direction facing);
        void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId);
        void SetBoardPresence(int entityId, EntityBoardPresence boardPresence);
        void SetPlayerControlState(int entityId, PlayerControlState state);
        void SetPlayerDamageState(int entityId, PlayerDamageState state);
        void SetTopology(CubeTopologyState topology);
    }
}
