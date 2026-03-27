using System;

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

        public void SetFacing(int entityId, Direction facing)
        {
            _port.SetFacing(entityId, facing);
        }
    }
}
