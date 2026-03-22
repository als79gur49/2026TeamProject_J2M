using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Movement.Groups;

namespace Game.Feature.Gameplay.Attack.Resolution
{
    internal sealed class AttackResolver
    {
        public void Resolve(
            IReadOnlyList<ActionGroup> sortedCandidates,
            List<ActionGroup> buffer)
        {
            if (sortedCandidates == null)
            {
                throw new ArgumentNullException(nameof(sortedCandidates));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            var selectedIntentIds = new HashSet<int>();

            for (var i = 0; i < sortedCandidates.Count; i++)
            {
                var candidate = sortedCandidates[i];
                if (!selectedIntentIds.Add(candidate.IntentId))
                {
                    continue;
                }

                buffer.Add(candidate);
            }
        }
    }
}
