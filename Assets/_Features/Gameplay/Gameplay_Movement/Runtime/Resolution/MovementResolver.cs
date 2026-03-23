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
            var reservedDestinations = new HashSet<Vector2Int>();

            for (var i = 0; i < sortedCandidates.Count; i++)
            {
                var candidate = sortedCandidates[i];
                if (selectedIntentIds.Contains(candidate.IntentId))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=IntentAlreadySelected");
                    continue;
                }

                if (TryGetConflictingDestination(candidate, reservedDestinations, out var conflictingDestination))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=DestinationReserved|Cell=({conflictingDestination.x},{conflictingDestination.y})");
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

        private static bool TryGetConflictingDestination(
            ActionGroup candidate,
            HashSet<Vector2Int> reservedDestinations,
            out Vector2Int conflictingDestination)
        {
            conflictingDestination = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var destination = candidate.Moves[i].Destination;
                if (reservedDestinations.Contains(destination))
                {
                    conflictingDestination = destination;
                    return true;
                }
            }

            return false;
        }
    }
}
