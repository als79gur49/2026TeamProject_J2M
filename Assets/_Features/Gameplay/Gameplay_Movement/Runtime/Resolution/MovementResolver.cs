using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Model.Groups;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Resolution
{
    internal sealed class MovementResolver
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
            var reservedDestinations = new HashSet<Vector2Int>();

            for (var i = 0; i < sortedCandidates.Count; i++)
            {
                var candidate = sortedCandidates[i];
                if (selectedIntentIds.Contains(candidate.IntentId))
                {
                    continue;
                }

                if (Conflicts(candidate, reservedDestinations))
                {
                    continue;
                }

                buffer.Add(candidate);
                selectedIntentIds.Add(candidate.IntentId);

                for (var moveIndex = 0; moveIndex < candidate.Moves.Count; moveIndex++)
                {
                    reservedDestinations.Add(candidate.Moves[moveIndex].Destination);
                }
            }
        }

        private static bool Conflicts(ActionGroup candidate, HashSet<Vector2Int> reservedDestinations)
        {
            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                if (reservedDestinations.Contains(candidate.Moves[i].Destination))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
