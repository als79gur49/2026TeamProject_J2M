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

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            _port.SetEnemyJumpState(entityId, state);
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

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _port.SetBoardPresence(entityId, boardPresence);
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state)
        {
            _port.SetPlayerControlState(entityId, state);
        }

        public void SetTopology(CubeTopologyState topology)
        {
            _port.SetTopology(topology);
        }
    }
}
