using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Movement.Resolution
{
    internal sealed class MovementResolver
    {
        public void Resolve(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> sortedCandidates,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ResolveCore(snapshot, sortedCandidates, buffer, rejectedReasons);
        }

        public void Resolve(
            IReadOnlyList<ActionGroup> sortedCandidates,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            ResolveCore(null, sortedCandidates, buffer, rejectedReasons);
        }

        private static void ResolveCore(
            WorldSnapshot snapshot,
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
            var reservedBlockingDestinations = new HashSet<SurfaceCell>();
            var reservedEdges = new Dictionary<UndirectedEdgeKey, EdgeReservation>();
            var reservedBlockingEdges = new Dictionary<UndirectedEdgeKey, EdgeReservation>();
            var reservedAffectedEntities = new HashSet<int>();
            ExclusiveGroupReservation? firstSelectedReservation = null;
            ExclusiveGroupReservation? topologyExclusiveReservation = null;

            for (var i = 0; i < sortedCandidates.Count; i++)
            {
                var candidate = sortedCandidates[i];
                if (selectedIntentIds.Contains(candidate.IntentId))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=IntentAlreadySelected");
                    continue;
                }

                if (TryGetTopologyExclusiveConflict(
                        candidate,
                        firstSelectedReservation,
                        topologyExclusiveReservation,
                        out var exclusiveConflict))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=TopologyExclusive|BlockedBy={exclusiveConflict.GroupId}|BlockingKind={exclusiveConflict.GroupKind}|BlockingTopologyChange={exclusiveConflict.HasTopologyChange}");
                    continue;
                }

                var reservationMode = ResolveReservationMode(snapshot, candidate);
                var destinationsToCheck = reservationMode == ReservationMode.UnitSharedMove
                    ? reservedBlockingDestinations
                    : reservedDestinations;
                if (TryGetConflictingDestination(candidate, destinationsToCheck, out var conflictingDestination))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={candidate.GroupId}|I={candidate.IntentId}|Source={candidate.SourceId}|Reason=DestinationReserved|Cell={FormatCell(conflictingDestination)}");
                    continue;
                }

                if (RequiresEdgeReservation(candidate) &&
                    TryGetConflictingEdge(
                        candidate,
                        reservationMode == ReservationMode.UnitSharedMove ? reservedBlockingEdges : reservedEdges,
                        out var conflictingEdge))
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

                buffer.Add(candidate);
                selectedIntentIds.Add(candidate.IntentId);
                ReserveCandidate(
                    candidate,
                    reservedDestinations,
                    reservedBlockingDestinations,
                    reservedEdges,
                    reservedBlockingEdges,
                    reservedAffectedEntities,
                    reservationMode,
                    ref firstSelectedReservation,
                    ref topologyExclusiveReservation);
            }
        }

        private static void ReserveCandidate(
            ActionGroup candidate,
            HashSet<SurfaceCell> reservedDestinations,
            ISet<SurfaceCell> reservedBlockingDestinations,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedBlockingEdges,
            ISet<int> reservedAffectedEntities,
            ReservationMode reservationMode,
            ref ExclusiveGroupReservation? firstSelectedReservation,
            ref ExclusiveGroupReservation? topologyExclusiveReservation)
        {
            var blocksSharedUnitMoves = reservationMode == ReservationMode.Conservative;

            for (var moveIndex = 0; moveIndex < candidate.Moves.Count; moveIndex++)
            {
                var move = candidate.Moves[moveIndex];
                reservedDestinations.Add(move.DestinationCell);
                if (blocksSharedUnitMoves)
                {
                    reservedBlockingDestinations.Add(move.DestinationCell);
                }

                reservedAffectedEntities.Add(move.EntityId);

                if (RequiresEdgeReservation(candidate) && move.SourceCell != move.DestinationCell)
                {
                    var edgeReservation = new EdgeReservation(
                        move.EntityId,
                        move.SourceCell,
                        move.DestinationCell,
                        candidate.GroupId);
                    var edgeKey = UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To);
                    reservedEdges[edgeKey] = edgeReservation;
                    if (blocksSharedUnitMoves)
                    {
                        reservedBlockingEdges[edgeKey] = edgeReservation;
                    }
                }
            }

            for (var destroyIndex = 0; destroyIndex < candidate.Destroys.Count; destroyIndex++)
            {
                reservedAffectedEntities.Add(candidate.Destroys[destroyIndex].TargetId);
            }

            for (var stateChangeIndex = 0; stateChangeIndex < candidate.StateChanges.Count; stateChangeIndex++)
            {
                reservedAffectedEntities.Add(candidate.StateChanges[stateChangeIndex].EntityId);
            }

            for (var presenceIndex = 0; presenceIndex < candidate.BoardPresenceChanges.Count; presenceIndex++)
            {
                reservedAffectedEntities.Add(candidate.BoardPresenceChanges[presenceIndex].EntityId);
            }

            if (!firstSelectedReservation.HasValue)
            {
                firstSelectedReservation = new ExclusiveGroupReservation(
                    candidate.GroupId,
                    candidate.GroupKind,
                    HasTopologyChange(candidate));
            }

            if (!topologyExclusiveReservation.HasValue && HasTopologyChange(candidate))
            {
                topologyExclusiveReservation = new ExclusiveGroupReservation(
                    candidate.GroupId,
                    candidate.GroupKind,
                    hasTopologyChange: true);
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

            for (var i = 0; i < candidate.StateChanges.Count; i++)
            {
                var entityId = candidate.StateChanges[i].EntityId;
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

        private static bool TryGetTopologyExclusiveConflict(
            ActionGroup candidate,
            ExclusiveGroupReservation? firstSelectedReservation,
            ExclusiveGroupReservation? topologyExclusiveReservation,
            out ExclusiveGroupReservation conflictingReservation)
        {
            conflictingReservation = default;

            if (HasTopologyChange(candidate))
            {
                if (!firstSelectedReservation.HasValue)
                {
                    return false;
                }

                conflictingReservation = firstSelectedReservation.Value;
                return true;
            }

            if (!topologyExclusiveReservation.HasValue)
            {
                return false;
            }

            conflictingReservation = topologyExclusiveReservation.Value;
            return true;
        }

        private static bool RequiresEdgeReservation(ActionGroup candidate)
        {
            return candidate.GroupKind != ActionGroupKind.Flip;
        }

        private static ReservationMode ResolveReservationMode(WorldSnapshot snapshot, ActionGroup candidate)
        {
            if (snapshot == null ||
                candidate.GroupKind != ActionGroupKind.Move ||
                HasTopologyChange(candidate) ||
                candidate.Moves.Count == 0)
            {
                return ReservationMode.Conservative;
            }

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                if (!snapshot.TryGetEntity(candidate.Moves[i].EntityId, out var entity) ||
                    entity.type != EntityType.Unit)
                {
                    return ReservationMode.Conservative;
                }
            }

            return ReservationMode.UnitSharedMove;
        }

        private static bool HasTopologyChange(ActionGroup candidate)
        {
            return candidate.TopologyChanges.Count > 0;
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

        private readonly struct ExclusiveGroupReservation
        {
            public ExclusiveGroupReservation(int groupId, ActionGroupKind groupKind, bool hasTopologyChange)
            {
                GroupId = groupId;
                GroupKind = groupKind;
                HasTopologyChange = hasTopologyChange;
            }

            public int GroupId { get; }

            public ActionGroupKind GroupKind { get; }

            public bool HasTopologyChange { get; }
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

        private enum ReservationMode
        {
            Conservative = 0,
            UnitSharedMove = 1,
        }
    }
}
