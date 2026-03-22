using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Attack.Collection
{
    internal sealed class AttackIntentCollector
    {
        public void Collect(
            WorldSnapshot snapshot,
            IReadOnlyList<IEntityLogic> entityLogics,
            List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (entityLogics == null)
            {
                throw new ArgumentNullException(nameof(entityLogics));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            for (var i = 0; i < entityLogics.Count; i++)
            {
                entityLogics[i].CollectAttackIntents(snapshot, buffer);
            }

            ValidateSinglePrimaryIntentPerSource(buffer);
        }

        private static void ValidateSinglePrimaryIntentPerSource(List<RawAttackIntent> buffer)
        {
            var seenSourceIds = new HashSet<int>();

            for (var i = 0; i < buffer.Count; i++)
            {
                if (!seenSourceIds.Add(buffer[i].SourceId))
                {
                    throw new InvalidOperationException("Each source may emit at most one attack intent per phase.");
                }
            }
        }
    }
}
