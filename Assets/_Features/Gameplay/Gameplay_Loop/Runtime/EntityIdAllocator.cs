using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class EntityIdAllocator
    {
        private int _nextEntityId;

        private EntityIdAllocator(int nextEntityId)
        {
            if (nextEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextEntityId), "Entity IDs must remain positive.");
            }

            _nextEntityId = nextEntityId;
        }

        internal static EntityIdAllocator Create(WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            var nextEntityId = entities.Count == 0
                ? 1
                : entities[entities.Count - 1].entityId + 1;

            return new EntityIdAllocator(nextEntityId);
        }

        internal int AllocateEntityId()
        {
            return _nextEntityId++;
        }
    }
}
