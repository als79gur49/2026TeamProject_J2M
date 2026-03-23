using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal sealed class WorldStateWriteContext : IWorldWriteContext
    {
        private readonly IWorldStateMutationPort _port;

        internal WorldStateWriteContext(IWorldStateMutationPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        public void MoveEntity(int entityId, Vector2Int destination)
        {
            if (!_port.TryGetEntity(entityId, out var entity))
            {
                return;
            }

            _port.ClearOccupancy(entity);
            entity.position = destination;
            _port.UpdateEntity(entity);
            _port.SetOccupancy(entity);
        }

        public void ApplyDamage(int entityId, int amount)
        {
            if (!_port.TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.hp -= amount;
            _port.UpdateEntity(entity);
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            if (!_port.TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.state = state;
            entity.stateTimer = stateTimer;
            _port.UpdateEntity(entity);
        }

        public void MarkDestroy(int entityId)
        {
            if (!_port.TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.markedForDeath = true;
            _port.UpdateEntity(entity);
        }

        public void SpawnEntity(EntityState entity)
        {
            _port.AddNewEntity(entity);
        }

        public void RemoveEntity(int entityId)
        {
            if (!_port.TryGetEntity(entityId, out var entity))
            {
                return;
            }

            _port.ClearOccupancy(entity);
            _port.RemoveEntityRecord(entityId);
        }

        public void SetFacing(int entityId, Direction facing)
        {
            if (!_port.TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.facing = facing;
            _port.UpdateEntity(entity);
        }
    }
}
