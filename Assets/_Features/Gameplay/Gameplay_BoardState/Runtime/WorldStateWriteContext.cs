using System;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.BoardState
{
    internal sealed class WorldStateWriteContext : IWorldWriteContext
    {
        private readonly IWorldStateMutationPort _port;

        internal WorldStateWriteContext(IWorldStateMutationPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        public void MoveEntity(int entityId, SurfaceCell destination)
        {
            _port.MoveEntityTo(entityId, destination);
        }

        public void ApplyDamage(int entityId, int amount)
        {
            _port.ApplyDamage(entityId, amount);
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            _port.ApplyStateChange(entityId, state, stateTimer);
        }

        public void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer)
        {
            _port.ApplyEnemyAiState(entityId, aiMode, aiStateTimer);
        }

        public void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks)
        {
            _port.SetEnemyLocomotionCooldown(entityId, cooldownTicks);
        }

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            _port.SetEnemyActionState(entityId, state);
        }

        public void AddPendingCellImpact(PendingCellImpact impact)
        {
            _port.AddPendingCellImpact(impact);
        }

        public void RemovePendingCellImpact(int impactId)
        {
            _port.RemovePendingCellImpact(impactId);
        }

        public void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state)
        {
            _port.SetEnemyPatrolState(entityId, state);
        }

        public void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state)
        {
            _port.SetEnemyChargeState(entityId, state);
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            _port.SetEnemyJumpState(entityId, state);
        }

        public void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state)
        {
            _port.SetEnemyGlideState(entityId, state);
        }

        public void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state)
        {
            _port.SetEnemyUtilityState(entityId, state);
        }

        public void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state)
        {
            _port.SetEnemyFrontFaceSupportState(entityId, state);
        }

        public void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state)
        {
            _port.SetBoxInteractionLockState(entityId, state);
        }

        public void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks)
        {
            _port.SetGravityFieldState(entityId, phase, timerTicks);
        }

        public void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state)
        {
            _port.SetUnitKinematicState(entityId, state);
        }

        public void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state)
        {
            _port.SetUnitContinuousLocomotionState(entityId, state);
        }

        public void SetSummonedEntityState(int entityId, SummonedEntityState state)
        {
            _port.SetSummonedEntityState(entityId, state);
        }

        public void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state)
        {
            _port.SetEnemyDefinitionBindingState(entityId, state);
        }

        public void SetPhasedState(int entityId, PhasedRuntimeState state)
        {
            _port.SetPhasedState(entityId, state);
        }

        public void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state)
        {
            _port.SetEntityExecutionLockState(entityId, state);
        }

        public void MoveEnemyJumpEntity(int entityId, SurfaceCell destination)
        {
            _port.MoveEntityTo(entityId, destination);
        }

        public void SetEnemyJumpBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _port.SetBoardPresence(entityId, boardPresence);
        }

        public void SetFacing(int entityId, Direction facing)
        {
            _port.SetFacing(entityId, facing);
        }

        public void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            _port.SetBoxKineticOwner(entityId, instigatorEntityId, instigatorTeamId);
        }

        public void MarkDestroy(int entityId)
        {
            _port.MarkDestroy(entityId);
        }

        public void SpawnEntity(EntityState entity)
        {
            _port.SpawnEntity(entity);
        }

        public void RemoveEntity(int entityId)
        {
            _port.RemoveEntity(entityId);
        }

        public void RemoveBoxInteractionLockState(int entityId)
        {
            _port.RemoveBoxInteractionLockState(entityId);
        }

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _port.SetBoardPresence(entityId, boardPresence);
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state)
        {
            _port.SetPlayerControlState(entityId, state);
        }

        public void SetPlayerDamageState(int entityId, PlayerDamageState state)
        {
            _port.SetPlayerDamageState(entityId, state);
        }

        public void SetTopology(CubeTopologyState topology)
        {
            _port.SetTopology(topology);
        }

        public void AddTileFeature(TileFeatureState state)
        {
            _port.AddTileFeature(state);
        }

        public void UpdateTileFeature(TileFeatureState state)
        {
            _port.UpdateTileFeature(state);
        }

        public void RemoveTileFeature(int tileId)
        {
            _port.RemoveTileFeature(tileId);
        }

        public void EmitEnemyUtilityTriggerIntent(EnemyUtilityTriggerIntent intent)
        {
        }
    }
}
