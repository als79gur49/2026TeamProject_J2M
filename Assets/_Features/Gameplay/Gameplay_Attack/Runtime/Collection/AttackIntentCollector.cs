using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Attack.Collection
{
    internal sealed class AttackIntentCollector
    {
        public void Collect(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IAttackEntityLogic> entityLogics,
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
                entityLogics[i].CollectAttackIntents(snapshot, in input, buffer);
            }

            FilterDeadSources(snapshot, buffer);
            ValidateUniqueIntentKeysPerSource(buffer);
        }

        private static void FilterDeadSources(WorldSnapshot snapshot, List<RawAttackIntent> buffer)
        {
            var writeIndex = 0;

            for (var i = 0; i < buffer.Count; i++)
            {
                if (!snapshot.TryGetEntity(buffer[i].SourceId, out var entity))
                {
                    continue;
                }

                if (entity.hp <= 0 || entity.markedForDeath)
                {
                    continue;
                }

                buffer[writeIndex++] = buffer[i];
            }

            if (writeIndex < buffer.Count)
            {
                buffer.RemoveRange(writeIndex, buffer.Count - writeIndex);
            }
        }

        private static void ValidateUniqueIntentKeysPerSource(List<RawAttackIntent> buffer)
        {
            var seenKeys = new HashSet<(int SourceId, int LocalSequence)>();

            for (var i = 0; i < buffer.Count; i++)
            {
                var key = (buffer[i].SourceId, buffer[i].LocalSequence);
                if (!seenKeys.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Each source must emit unique attack intent local sequences per phase. Source={key.SourceId}|LocalSequence={key.LocalSequence}");
                }
            }
        }
    }
}
