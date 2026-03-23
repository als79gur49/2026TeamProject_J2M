using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Attack.Resolution
{
    internal sealed class AttackResolver
    {
        public void Resolve(
            IReadOnlyList<ActionGroup> sortedCandidates,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (sortedCandidates == null)
            {
                throw new ArgumentNullException(nameof(sortedCandidates));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            buffer.Clear();

            var selectedIntentIds = new HashSet<int>();

            for (var i = 0; i < sortedCandidates.Count; i++)
            {
                var candidate = sortedCandidates[i];
                if (!selectedIntentIds.Add(candidate.IntentId))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=IntentAlreadySelected");
                    continue;
                }

                buffer.Add(candidate);
            }
        }
    }
}
