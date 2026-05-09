using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Movement.Collection
{
    internal sealed class MovementIntentCollector
    {
        public void Collect(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IMovementEntityLogic> entityLogics,
            List<RawMovementIntent> buffer,
            List<string> debugEvents = null)
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
                entityLogics[i].CollectMovementIntents(snapshot, in input, buffer);
            }

            ValidateSinglePrimaryIntentPerSource(buffer);

            if (debugEvents == null)
            {
                return;
            }

            for (var i = 0; i < entityLogics.Count; i++)
            {
                if (entityLogics[i] is IMovementEntityDebugLogic debugLogic)
                {
                    debugLogic.CollectMovementDebugEvents(snapshot, in input, buffer, debugEvents);
                }
            }
        }

        private static void ValidateSinglePrimaryIntentPerSource(List<RawMovementIntent> buffer)
        {
            var seenSourceIds = new HashSet<int>();

            for (var i = 0; i < buffer.Count; i++)
            {
                if (!seenSourceIds.Add(buffer[i].SourceId))
                {
                    throw new InvalidOperationException("Each source may emit at most one movement intent per phase.");
                }
            }
        }
    }
}
