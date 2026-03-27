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
            var reservedEdges = new Dictionary<UndirectedEdgeKey, EdgeReservation>();
            var reservedMovedEntities = new HashSet<int>();

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

                if (RequiresEdgeReservation(candidate) &&
                    TryGetConflictingEdge(candidate, reservedEdges, out var conflictingEdge))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=EdgeReserved|From=({conflictingEdge.First.x},{conflictingEdge.First.y})|To=({conflictingEdge.Second.x},{conflictingEdge.Second.y})");
                    continue;
                }

                if (TryGetSharedMovedEntity(candidate, reservedMovedEntities, out var sharedEntityId))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=SharedMovedEntity|Entity={sharedEntityId}");
                    continue;
                }

                buffer.Add(candidate);
                selectedIntentIds.Add(candidate.IntentId);

                for (var moveIndex = 0; moveIndex < candidate.Moves.Count; moveIndex++)
                {
                    var move = candidate.Moves[moveIndex];
                    reservedDestinations.Add(move.Destination);
                    reservedMovedEntities.Add(move.EntityId);

                    if (RequiresEdgeReservation(candidate) && move.Source != move.Destination)
                    {
                        var edgeReservation = new EdgeReservation(
                            move.EntityId,
                            move.Source,
                            move.Destination,
                            candidate.GroupId);
                        reservedEdges[UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To)] = edgeReservation;
                    }
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

        private static bool TryGetConflictingEdge(
            ActionGroup candidate,
            IReadOnlyDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            out UndirectedEdgeKey conflictingEdge)
        {
            conflictingEdge = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var move = candidate.Moves[i];
                if (move.Source == move.Destination)
                {
                    continue;
                }

                var edge = UndirectedEdgeKey.Create(move.Source, move.Destination);
                if (reservedEdges.ContainsKey(edge))
                {
                    conflictingEdge = edge;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSharedMovedEntity(
            ActionGroup candidate,
            HashSet<int> reservedMovedEntities,
            out int sharedEntityId)
        {
            sharedEntityId = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var entityId = candidate.Moves[i].EntityId;
                if (reservedMovedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            return false;
        }

        private static bool RequiresEdgeReservation(ActionGroup candidate)
        {
            return candidate.GroupKind != ActionGroupKind.Throw;
        }

        private readonly struct EdgeReservation
        {
            public EdgeReservation(int entityId, Vector2Int from, Vector2Int to, int groupId)
            {
                EntityId = entityId;
                From = from;
                To = to;
                GroupId = groupId;
            }

            public int EntityId { get; }

            public Vector2Int From { get; }

            public Vector2Int To { get; }

            public int GroupId { get; }
        }

        private readonly struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
        {
            public UndirectedEdgeKey(Vector2Int first, Vector2Int second)
            {
                First = first;
                Second = second;
            }

            public Vector2Int First { get; }

            public Vector2Int Second { get; }

            public static UndirectedEdgeKey Create(Vector2Int from, Vector2Int to)
            {
                return CompareCells(from, to) <= 0
                    ? new UndirectedEdgeKey(from, to)
                    : new UndirectedEdgeKey(to, from);
            }

            public bool Equals(UndirectedEdgeKey other)
            {
                return First == other.First && Second == other.Second;
            }

            public override bool Equals(object obj)
            {
                return obj is UndirectedEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + First.x;
                    hash = (hash * 31) + First.y;
                    hash = (hash * 31) + Second.x;
                    hash = (hash * 31) + Second.y;
                    return hash;
                }
            }

            private static int CompareCells(Vector2Int left, Vector2Int right)
            {
                var result = left.x.CompareTo(right.x);
                if (result != 0)
                {
                    return result;
                }

                return left.y.CompareTo(right.y);
            }
        }
    }
}
