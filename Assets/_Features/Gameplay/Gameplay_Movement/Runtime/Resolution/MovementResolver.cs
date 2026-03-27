using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Groups;

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
            var reservedDestinations = new HashSet<SurfaceCell>();
            var reservedEdges = new Dictionary<UndirectedEdgeKey, EdgeReservation>();
            var reservedAffectedEntities = new HashSet<int>();
            TopologyReservation? topologyReservation = null;

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
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=DestinationReserved|Cell={FormatCell(conflictingDestination)}");
                    continue;
                }

                if (RequiresEdgeReservation(candidate) &&
                    TryGetConflictingEdge(candidate, reservedEdges, out var conflictingEdge))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=EdgeReserved|From={FormatCell(conflictingEdge.First)}|To={FormatCell(conflictingEdge.Second)}");
                    continue;
                }

                if (TryGetSharedAffectedEntity(candidate, reservedAffectedEntities, out var sharedEntityId))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=SharedMovedEntity|Entity={sharedEntityId}");
                    continue;
                }

                if (TryGetConflictingTopologyChange(candidate, topologyReservation, out var conflictingTopologyReservation))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=TopologyReserved|Bottom={conflictingTopologyReservation.BottomFace}|Front={conflictingTopologyReservation.FrontFace}|ReservedBy={conflictingTopologyReservation.GroupId}");
                    continue;
                }

                buffer.Add(candidate);
                selectedIntentIds.Add(candidate.IntentId);
                ReserveCandidate(candidate, reservedDestinations, reservedEdges, reservedAffectedEntities, ref topologyReservation);
            }
        }

        private static void ReserveCandidate(
            ActionGroup candidate,
            HashSet<SurfaceCell> reservedDestinations,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            ISet<int> reservedAffectedEntities,
            ref TopologyReservation? topologyReservation)
        {
            for (var moveIndex = 0; moveIndex < candidate.Moves.Count; moveIndex++)
            {
                var move = candidate.Moves[moveIndex];
                reservedDestinations.Add(move.DestinationCell);
                reservedAffectedEntities.Add(move.EntityId);

                if (RequiresEdgeReservation(candidate) && move.SourceCell != move.DestinationCell)
                {
                    var edgeReservation = new EdgeReservation(
                        move.EntityId,
                        move.SourceCell,
                        move.DestinationCell,
                        candidate.GroupId);
                    reservedEdges[UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To)] = edgeReservation;
                }
            }

            for (var destroyIndex = 0; destroyIndex < candidate.Destroys.Count; destroyIndex++)
            {
                reservedAffectedEntities.Add(candidate.Destroys[destroyIndex].TargetId);
            }

            for (var presenceIndex = 0; presenceIndex < candidate.BoardPresenceChanges.Count; presenceIndex++)
            {
                reservedAffectedEntities.Add(candidate.BoardPresenceChanges[presenceIndex].EntityId);
            }

            if (!topologyReservation.HasValue && candidate.TopologyChanges.Count > 0)
            {
                var topologyChange = candidate.TopologyChanges[0];
                topologyReservation = new TopologyReservation(
                    topologyChange.UpdatedTopology,
                    candidate.GroupId);
            }
        }

        private static bool TryGetConflictingDestination(
            ActionGroup candidate,
            HashSet<SurfaceCell> reservedDestinations,
            out SurfaceCell conflictingDestination)
        {
            conflictingDestination = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var destination = candidate.Moves[i].DestinationCell;
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
                if (move.SourceCell == move.DestinationCell)
                {
                    continue;
                }

                var edge = UndirectedEdgeKey.Create(move.SourceCell, move.DestinationCell);
                if (reservedEdges.ContainsKey(edge))
                {
                    conflictingEdge = edge;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSharedAffectedEntity(
            ActionGroup candidate,
            ISet<int> reservedAffectedEntities,
            out int sharedEntityId)
        {
            sharedEntityId = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var entityId = candidate.Moves[i].EntityId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            for (var i = 0; i < candidate.Destroys.Count; i++)
            {
                var entityId = candidate.Destroys[i].TargetId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            for (var i = 0; i < candidate.BoardPresenceChanges.Count; i++)
            {
                var entityId = candidate.BoardPresenceChanges[i].EntityId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConflictingTopologyChange(
            ActionGroup candidate,
            TopologyReservation? reservedTopologyReservation,
            out TopologyReservation conflictingTopologyReservation)
        {
            conflictingTopologyReservation = default;

            if (candidate.TopologyChanges.Count == 0 || !reservedTopologyReservation.HasValue)
            {
                return false;
            }

            conflictingTopologyReservation = reservedTopologyReservation.Value;
            return true;
        }

        private static bool RequiresEdgeReservation(ActionGroup candidate)
        {
            return candidate.GroupKind != ActionGroupKind.Flip;
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }

        private readonly struct EdgeReservation
        {
            public EdgeReservation(int entityId, SurfaceCell from, SurfaceCell to, int groupId)
            {
                EntityId = entityId;
                From = from;
                To = to;
                GroupId = groupId;
            }

            public int EntityId { get; }

            public SurfaceCell From { get; }

            public SurfaceCell To { get; }

            public int GroupId { get; }
        }

        private readonly struct TopologyReservation
        {
            public TopologyReservation(CubeTopologyState topology, int groupId)
            {
                BottomFace = topology.BottomFace;
                FrontFace = topology.FrontFace;
                GroupId = groupId;
            }

            public FaceId BottomFace { get; }

            public FaceId FrontFace { get; }

            public int GroupId { get; }
        }

        private readonly struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
        {
            public UndirectedEdgeKey(SurfaceCell first, SurfaceCell second)
            {
                First = first;
                Second = second;
            }

            public SurfaceCell First { get; }

            public SurfaceCell Second { get; }

            public static UndirectedEdgeKey Create(SurfaceCell from, SurfaceCell to)
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
                    hash = (hash * 31) + (int)First.face;
                    hash = (hash * 31) + First.x;
                    hash = (hash * 31) + First.y;
                    hash = (hash * 31) + (int)Second.face;
                    hash = (hash * 31) + Second.x;
                    hash = (hash * 31) + Second.y;
                    return hash;
                }
            }

            private static int CompareCells(SurfaceCell left, SurfaceCell right)
            {
                var result = ((int)left.face).CompareTo((int)right.face);
                if (result != 0)
                {
                    return result;
                }

                result = left.x.CompareTo(right.x);
                if (result != 0)
                {
                    return result;
                }

                return left.y.CompareTo(right.y);
            }
        }
    }
}
