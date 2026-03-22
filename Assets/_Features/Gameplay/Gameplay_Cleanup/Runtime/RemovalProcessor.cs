using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class RemovalProcessor
    {
        public void Process(
            IReadOnlyList<EntityState> orderedEntities,
            IWorldWriteContext writeContext,
            List<EntityState> survivingEntities,
            List<int> removedEntityIds)
        {
            if (orderedEntities == null)
            {
                throw new ArgumentNullException(nameof(orderedEntities));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (survivingEntities == null)
            {
                throw new ArgumentNullException(nameof(survivingEntities));
            }

            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            survivingEntities.Clear();
            removedEntityIds.Clear();

            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var entity = orderedEntities[i];
                if (ShouldRemove(entity))
                {
                    removedEntityIds.Add(entity.entityId);
                    continue;
                }

                survivingEntities.Add(entity);
            }

            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                writeContext.RemoveEntity(removedEntityIds[i]);
            }
        }

        private static bool ShouldRemove(EntityState entity)
        {
            return entity.hp <= 0 || entity.markedForDeath;
        }
    }
}
