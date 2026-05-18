using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class MovementReservationBook
    {
        private readonly HashSet<int> _reservedAffectedEntities = new();
        private readonly Dictionary<UndirectedEdgeKey, EdgeReservation> _reservedBlockingEdges = new();
        private readonly HashSet<SurfaceCell> _reservedBlockingDestinations = new();
        private readonly Dictionary<SurfaceCell, CellReservationInfo> _reservedDestinationInfos = new();
        private readonly Dictionary<UndirectedEdgeKey, EdgeReservation> _reservedEdges = new();
        private readonly HashSet<SurfaceCell> _reservedDestinations = new();
        private bool _isFrozen;
        private int _freezeVersion;
        private (int ActionPlanId, MovementCandidateKind CandidateKind, bool HasTopologyChange, bool HasValue) _firstSelectedReservation;
        private (int ActionPlanId, MovementCandidateKind CandidateKind, bool HasTopologyChange, bool HasValue) _topologyExclusiveReservation;

        public bool TryAcceptPayload(
            WorldSnapshot snapshot,
            MovementActionPlanPayload payload,
            out MovementReservationConflict conflict)
        {
            ThrowIfFrozen();

            if (TryGetPayloadTopologyExclusiveConflict(
                    payload,
                    _firstSelectedReservation,
                    _topologyExclusiveReservation,
                    out var exclusiveConflict))
            {
                conflict = MovementReservationConflict.ForTopologyExclusive(
                    exclusiveConflict.ActionPlanId,
                    exclusiveConflict.CandidateKind,
                    exclusiveConflict.HasTopologyChange);
                return false;
            }

            var destinationsToCheck = payload.BlockingType == MovementBlockingType.NonBlocking
                ? _reservedBlockingDestinations
                : _reservedDestinations;
            var edgesToCheck = payload.BlockingType == MovementBlockingType.NonBlocking
                ? _reservedBlockingEdges
                : _reservedEdges;

            if (TryGetConflictingPayloadDestination(payload, destinationsToCheck, out var conflictingDestination))
            {
                conflict = MovementReservationConflict.ForDestination(conflictingDestination);
                return false;
            }

            if (TryGetConflictingPayloadEdge(payload, edgesToCheck, out var conflictingEdge))
            {
                conflict = MovementReservationConflict.ForEdge(conflictingEdge);
                return false;
            }

            if (TryGetSharedPayloadAffectedEntity(payload, _reservedAffectedEntities, out var sharedEntityId))
            {
                conflict = MovementReservationConflict.ForAffectedEntity(sharedEntityId);
                return false;
            }

            ReservePayload(snapshot, payload);
            conflict = default;
            return true;
        }

        public ReservationStatus GetImpactPayloadStatus(MovementImpactReservationPayload payload)
        {
            if (TryGetConflictingImpactPayloadDestination(payload, _reservedDestinations, out _) ||
                TryGetConflictingImpactPayloadEdge(payload, _reservedEdges, out _))
            {
                return ReservationStatus.Conflicted;
            }

            return ReservationStatus.None;
        }

        public ReservationStatus GetCellStatus(SurfaceCell cell)
        {
            // Pre-settle runtime consumers treat an already-selected destination as a conflict.
            return _reservedDestinations.Contains(cell)
                ? ReservationStatus.Conflicted
                : ReservationStatus.None;
        }

        public CellReservationInfo GetCellReservationInfo(SurfaceCell cell)
        {
            if (!_reservedDestinations.Contains(cell))
            {
                return CellReservationInfo.None;
            }

            return _reservedDestinationInfos.TryGetValue(cell, out var info)
                ? info
                : CellReservationInfo.BlockingConflict;
        }

        public ReservationStatus GetEdgeStatus(SurfaceCell from, SurfaceCell to)
        {
            return _reservedEdges.ContainsKey(UndirectedEdgeKey.Create(from, to))
                ? ReservationStatus.Reserved
                : ReservationStatus.None;
        }

        public ReservationStatus GetEntityStatus(int entityId)
        {
            return _reservedAffectedEntities.Contains(entityId)
                ? ReservationStatus.Reserved
                : ReservationStatus.None;
        }

        public void ReserveImpactPayload(MovementImpactReservationPayload payload, int actionPlanId)
        {
            ThrowIfFrozen();
            ReserveImpactPayloadCore(payload, actionPlanId);
        }

        public void ReserveJumpLanding(int entityId, SurfaceCell destinationCell)
        {
            ThrowIfFrozen();
            AddDestinationReservation(
                destinationCell,
                entityId,
                EntityType.Unit,
                blocksUnitSharedSettlement: true);
            _reservedBlockingDestinations.Add(destinationCell);
            _reservedAffectedEntities.Add(entityId);
        }

        public void ReservePhaseRelocation(int entityId, SurfaceCell destinationCell)
        {
            ThrowIfFrozen();
            AddDestinationReservation(
                destinationCell,
                entityId,
                EntityType.Unit,
                blocksUnitSharedSettlement: true);
            _reservedBlockingDestinations.Add(destinationCell);
            _reservedAffectedEntities.Add(entityId);
        }

        public FrozenMovementReservationExport Freeze(IReadOnlyList<ImpactReservation> impactReservations)
        {
            if (impactReservations == null)
            {
                throw new ArgumentNullException(nameof(impactReservations));
            }

            ThrowIfFrozen();
            _isFrozen = true;
            _freezeVersion++;

            var sortedReservations = new List<ImpactReservation>(impactReservations.Count);
            for (var i = 0; i < impactReservations.Count; i++)
            {
                sortedReservations.Add(impactReservations[i]);
            }

            sortedReservations.Sort(ImpactReservationComparer.Instance);
            return new FrozenMovementReservationExport(
                _freezeVersion,
                sortedReservations,
                new HashSet<SurfaceCell>(_reservedDestinations),
                new Dictionary<UndirectedEdgeKey, EdgeReservation>(_reservedEdges),
                new HashSet<int>(_reservedAffectedEntities));
        }

        private void ThrowIfFrozen()
        {
            if (_isFrozen)
            {
                throw new InvalidOperationException("Movement reservation book is frozen.");
            }
        }

        private void ReservePayload(WorldSnapshot snapshot, MovementActionPlanPayload payload)
        {
            var blocksSharedUnitMoves = payload.BlockingType == MovementBlockingType.Blocking;
            var destinationReservationEntityId = GetDestinationReservationEntityId(payload);
            var destinationReservationEntityType = snapshot.TryGetEntity(destinationReservationEntityId, out var destinationReservationEntity)
                ? destinationReservationEntity.type
                : EntityType.None;
            var blocksUnitSharedSettlement = !IsUnitSharedSettlementCompatibleReservation(
                payload,
                destinationReservationEntityType);

            if (PayloadReservesDestination(payload))
            {
                AddDestinationReservation(
                    payload.DestinationCell,
                    destinationReservationEntityId,
                    destinationReservationEntityType,
                    blocksUnitSharedSettlement);
                if (blocksSharedUnitMoves)
                {
                    _reservedBlockingDestinations.Add(payload.DestinationCell);
                }

                if (payload.ReservationKind == MovementReservationKind.Edge &&
                    payload.HasMovementEdge &&
                    payload.MovementEdge.FromCell != payload.MovementEdge.ToCell)
                {
                    var reservedEntityId = payload.MoveWrites.Count > 0
                        ? payload.MoveWrites[0].EntityId
                        : payload.SourceActorEntityId;
                    var edgeReservation = new EdgeReservation(
                        reservedEntityId,
                        payload.MovementEdge.FromCell,
                        payload.MovementEdge.ToCell,
                        payload.ActionPlanId);
                    var edgeKey = UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To);
                    _reservedEdges[edgeKey] = edgeReservation;
                    if (blocksSharedUnitMoves)
                    {
                        _reservedBlockingEdges[edgeKey] = edgeReservation;
                    }
                }
            }

            for (var i = 0; i < payload.AffectedEntityIds.Count; i++)
            {
                _reservedAffectedEntities.Add(payload.AffectedEntityIds[i]);
            }

            if (!_firstSelectedReservation.HasValue)
            {
                _firstSelectedReservation = (
                    payload.ActionPlanId,
                    payload.MovementCandidateKind,
                    PayloadHasTopologyChange(payload),
                    true);
            }

            if (!_topologyExclusiveReservation.HasValue && PayloadHasTopologyChange(payload))
            {
                _topologyExclusiveReservation = (
                    payload.ActionPlanId,
                    payload.MovementCandidateKind,
                    true,
                    true);
            }
        }

        private void ReserveImpactPayloadCore(MovementImpactReservationPayload payload, int actionPlanId)
        {
            AddDestinationReservation(
                payload.ContingentDestinationCell,
                payload.SourceEntityId,
                EntityType.None,
                blocksUnitSharedSettlement: true);
            _reservedBlockingDestinations.Add(payload.ContingentDestinationCell);
            _reservedAffectedEntities.Add(payload.SourceEntityId);

            if (Math.Abs(payload.ContingentDestinationCell.x - payload.ContingentSourceCell.x) +
                Math.Abs(payload.ContingentDestinationCell.y - payload.ContingentSourceCell.y) > 1 ||
                payload.ContingentDestinationCell == payload.ContingentSourceCell)
            {
                return;
            }

            var edgeReservation = new EdgeReservation(
                payload.SourceEntityId,
                payload.ContingentSourceCell,
                payload.ContingentDestinationCell,
                actionPlanId);
            var edgeKey = UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To);
            _reservedEdges[edgeKey] = edgeReservation;
            _reservedBlockingEdges[edgeKey] = edgeReservation;
        }

        private static bool PayloadHasTopologyChange(MovementActionPlanPayload payload)
        {
            return payload.TopologyWrites.Count > 0;
        }

        private static int GetDestinationReservationEntityId(MovementActionPlanPayload payload)
        {
            if (payload.MoveWrites.Count > 0)
            {
                return payload.MoveWrites[0].EntityId;
            }

            if (payload.KinematicMotionOutcomes.Count > 0)
            {
                return payload.KinematicMotionOutcomes[0].EntityId;
            }

            return payload.SourceActorEntityId;
        }

        private void AddDestinationReservation(
            SurfaceCell cell,
            int reservedEntityId,
            EntityType reservedEntityType,
            bool blocksUnitSharedSettlement)
        {
            _reservedDestinations.Add(cell);
            var nextInfo = new CellReservationInfo(
                ReservationStatus.Conflicted,
                reservedEntityId,
                reservedEntityType,
                blocksUnitSharedSettlement);

            if (!_reservedDestinationInfos.TryGetValue(cell, out var existingInfo) ||
                blocksUnitSharedSettlement ||
                !existingInfo.BlocksUnitSharedSettlement)
            {
                _reservedDestinationInfos[cell] = nextInfo;
            }
        }

        private static bool IsUnitSharedSettlementCompatibleReservation(
            MovementActionPlanPayload payload,
            EntityType sourceEntityType)
        {
            return sourceEntityType == EntityType.Unit &&
                   payload.BlockingType == MovementBlockingType.NonBlocking &&
                   !PayloadHasTopologyChange(payload);
        }

        private static bool TryGetPayloadTopologyExclusiveConflict(
            MovementActionPlanPayload payload,
            (int ActionPlanId, MovementCandidateKind CandidateKind, bool HasTopologyChange, bool HasValue) firstSelectedReservation,
            (int ActionPlanId, MovementCandidateKind CandidateKind, bool HasTopologyChange, bool HasValue) topologyExclusiveReservation,
            out (int ActionPlanId, MovementCandidateKind CandidateKind, bool HasTopologyChange) conflictingReservation)
        {
            conflictingReservation = default;
            if (PayloadHasTopologyChange(payload))
            {
                if (!firstSelectedReservation.HasValue)
                {
                    return false;
                }

                conflictingReservation = (
                    firstSelectedReservation.ActionPlanId,
                    firstSelectedReservation.CandidateKind,
                    firstSelectedReservation.HasTopologyChange);
                return true;
            }

            if (!topologyExclusiveReservation.HasValue)
            {
                return false;
            }

            conflictingReservation = (
                topologyExclusiveReservation.ActionPlanId,
                topologyExclusiveReservation.CandidateKind,
                topologyExclusiveReservation.HasTopologyChange);
            return true;
        }

        private static bool TryGetConflictingPayloadDestination(
            MovementActionPlanPayload payload,
            HashSet<SurfaceCell> reservedDestinations,
            out SurfaceCell conflictingDestination)
        {
            conflictingDestination = default;
            if (!PayloadReservesDestination(payload))
            {
                return false;
            }

            if (!reservedDestinations.Contains(payload.DestinationCell))
            {
                return false;
            }

            conflictingDestination = payload.DestinationCell;
            return true;
        }

        private static bool PayloadReservesDestination(MovementActionPlanPayload payload)
        {
            if (payload.MoveWrites.Count > 0)
            {
                return true;
            }

            for (var i = 0; i < payload.KinematicMotionOutcomes.Count; i++)
            {
                if (payload.KinematicMotionOutcomes[i].AnchorChanged)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConflictingPayloadEdge(
            MovementActionPlanPayload payload,
            IReadOnlyDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            out UndirectedEdgeKey conflictingEdge)
        {
            conflictingEdge = default;
            if (payload.ReservationKind != MovementReservationKind.Edge ||
                !payload.HasMovementEdge ||
                payload.MovementEdge.FromCell == payload.MovementEdge.ToCell)
            {
                return false;
            }

            var edge = UndirectedEdgeKey.Create(payload.MovementEdge.FromCell, payload.MovementEdge.ToCell);
            if (!reservedEdges.ContainsKey(edge))
            {
                return false;
            }

            conflictingEdge = edge;
            return true;
        }

        private static bool TryGetSharedPayloadAffectedEntity(
            MovementActionPlanPayload payload,
            ISet<int> reservedAffectedEntities,
            out int sharedEntityId)
        {
            sharedEntityId = default;
            for (var i = 0; i < payload.AffectedEntityIds.Count; i++)
            {
                if (reservedAffectedEntities.Contains(payload.AffectedEntityIds[i]))
                {
                    sharedEntityId = payload.AffectedEntityIds[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConflictingImpactPayloadDestination(
            MovementImpactReservationPayload payload,
            ISet<SurfaceCell> reservedDestinations,
            out SurfaceCell conflictingDestination)
        {
            conflictingDestination = default;
            if (!reservedDestinations.Contains(payload.ContingentDestinationCell))
            {
                return false;
            }

            conflictingDestination = payload.ContingentDestinationCell;
            return true;
        }

        private static bool TryGetConflictingImpactPayloadEdge(
            MovementImpactReservationPayload payload,
            IReadOnlyDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            out UndirectedEdgeKey conflictingEdge)
        {
            conflictingEdge = default;
            if (Math.Abs(payload.ContingentDestinationCell.x - payload.ContingentSourceCell.x) +
                Math.Abs(payload.ContingentDestinationCell.y - payload.ContingentSourceCell.y) > 1 ||
                payload.ContingentDestinationCell == payload.ContingentSourceCell)
            {
                return false;
            }

            var edge = UndirectedEdgeKey.Create(payload.ContingentSourceCell, payload.ContingentDestinationCell);
            if (!reservedEdges.ContainsKey(edge))
            {
                return false;
            }

            conflictingEdge = edge;
            return true;
        }
    }

    internal readonly struct MovementReservationConflict
    {
        private MovementReservationConflict(
            MovementReservationConflictKind kind,
            SurfaceCell destination,
            UndirectedEdgeKey edge,
            int entityId,
            int blockingActionPlanId,
            MovementCandidateKind blockingCandidateKind,
            bool blockingTopologyChange)
        {
            Kind = kind;
            Destination = destination;
            Edge = edge;
            EntityId = entityId;
            BlockingActionPlanId = blockingActionPlanId;
            BlockingCandidateKind = blockingCandidateKind;
            BlockingTopologyChange = blockingTopologyChange;
        }

        public MovementReservationConflictKind Kind { get; }

        public SurfaceCell Destination { get; }

        public UndirectedEdgeKey Edge { get; }

        public int EntityId { get; }

        public int BlockingActionPlanId { get; }

        public MovementCandidateKind BlockingCandidateKind { get; }

        public bool BlockingTopologyChange { get; }

        public static MovementReservationConflict ForDestination(SurfaceCell destination)
        {
            return new MovementReservationConflict(
                MovementReservationConflictKind.Destination,
                destination,
                default,
                0,
                0,
                default,
                false);
        }

        public static MovementReservationConflict ForEdge(UndirectedEdgeKey edge)
        {
            return new MovementReservationConflict(
                MovementReservationConflictKind.Edge,
                default,
                edge,
                0,
                0,
                default,
                false);
        }

        public static MovementReservationConflict ForAffectedEntity(int entityId)
        {
            return new MovementReservationConflict(
                MovementReservationConflictKind.AffectedEntity,
                default,
                default,
                entityId,
                0,
                default,
                false);
        }

        public static MovementReservationConflict ForTopologyExclusive(
            int blockingActionPlanId,
            MovementCandidateKind blockingCandidateKind,
            bool blockingTopologyChange)
        {
            return new MovementReservationConflict(
                MovementReservationConflictKind.TopologyExclusive,
                default,
                default,
                0,
                blockingActionPlanId,
                blockingCandidateKind,
                blockingTopologyChange);
        }
    }

    internal enum MovementReservationConflictKind
    {
        None = 0,
        Destination = 1,
        Edge = 2,
        AffectedEntity = 3,
        TopologyExclusive = 4,
    }

    internal readonly struct CellReservationInfo
    {
        public static readonly CellReservationInfo None = new(
            ReservationStatus.None,
            0,
            EntityType.None,
            blocksUnitSharedSettlement: false);

        public static readonly CellReservationInfo BlockingConflict = new(
            ReservationStatus.Conflicted,
            0,
            EntityType.None,
            blocksUnitSharedSettlement: true);

        public CellReservationInfo(
            ReservationStatus status,
            int reservedEntityId,
            EntityType reservedEntityType,
            bool blocksUnitSharedSettlement)
        {
            Status = status;
            ReservedEntityId = reservedEntityId;
            ReservedEntityType = reservedEntityType;
            BlocksUnitSharedSettlement = blocksUnitSharedSettlement;
        }

        public ReservationStatus Status { get; }

        public int ReservedEntityId { get; }

        public EntityType ReservedEntityType { get; }

        public bool BlocksUnitSharedSettlement { get; }

        public bool IsUnitSharedSettlementCompatible =>
            Status == ReservationStatus.Conflicted &&
            ReservedEntityType == EntityType.Unit &&
            !BlocksUnitSharedSettlement;
    }

    internal sealed class FrozenMovementReservationExport
    {
        public static readonly FrozenMovementReservationExport Empty = new(
            0,
            Array.Empty<ImpactReservation>(),
            new HashSet<SurfaceCell>(),
            new Dictionary<UndirectedEdgeKey, EdgeReservation>(),
            new HashSet<int>());

        private readonly ReadOnlyCollection<ImpactReservation> _impactReservations;
        private readonly HashSet<int> _reservedEntities;
        private readonly Dictionary<UndirectedEdgeKey, EdgeReservation> _reservedEdges;
        private readonly HashSet<SurfaceCell> _reservedCells;

        public FrozenMovementReservationExport(
            int freezeVersion,
            IReadOnlyList<ImpactReservation> impactReservations,
            HashSet<SurfaceCell> reservedCells,
            Dictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            HashSet<int> reservedEntities)
        {
            FreezeVersion = freezeVersion;
            _impactReservations = new ReadOnlyCollection<ImpactReservation>(
                new List<ImpactReservation>(impactReservations ?? throw new ArgumentNullException(nameof(impactReservations))));
            _reservedCells = reservedCells ?? throw new ArgumentNullException(nameof(reservedCells));
            _reservedEdges = reservedEdges ?? throw new ArgumentNullException(nameof(reservedEdges));
            _reservedEntities = reservedEntities ?? throw new ArgumentNullException(nameof(reservedEntities));
        }

        public int FreezeVersion { get; }

        public IReadOnlyList<ImpactReservation> ImpactReservations => _impactReservations;

        public ReservationStatus GetCellStatus(SurfaceCell cell)
        {
            return _reservedCells.Contains(cell) ? ReservationStatus.Reserved : ReservationStatus.None;
        }

        public ReservationStatus GetEdgeStatus(SurfaceCell from, SurfaceCell to)
        {
            return _reservedEdges.ContainsKey(UndirectedEdgeKey.Create(from, to))
                ? ReservationStatus.Reserved
                : ReservationStatus.None;
        }

        public ReservationStatus GetEntityStatus(int entityId)
        {
            return _reservedEntities.Contains(entityId) ? ReservationStatus.Reserved : ReservationStatus.None;
        }
    }
}
