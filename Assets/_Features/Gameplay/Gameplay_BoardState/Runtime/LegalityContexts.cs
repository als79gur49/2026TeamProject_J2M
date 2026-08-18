using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct LegalityActorRef
    {
        public LegalityActorRef(int entityId, EntityType entityType, ResolvedSpatialState spatialState)
            : this(entityId, entityType, spatialState, default)
        {
        }

        public LegalityActorRef(
            int entityId,
            EntityType entityType,
            ResolvedSpatialState spatialState,
            EnemyGlideRuntimeState glideState)
        {
            EntityId = entityId;
            EntityType = entityType;
            SpatialState = spatialState;
            GlideState = glideState;
        }

        public int EntityId { get; }

        public EntityType EntityType { get; }

        public ResolvedSpatialState SpatialState { get; }

        public EnemyGlideRuntimeState GlideState { get; }
    }

    internal readonly struct TraverseContext
    {
        public TraverseContext(
            WorldSnapshot snapshot,
            LegalityActorRef actor,
            SurfaceCell originCell,
            SurfaceCell candidateCell,
            CubeTopologyState evaluationTopology,
            TransitionRequirement transitionRequirement,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Actor = actor;
            OriginCell = originCell;
            CandidateCell = candidateCell;
            EvaluationTopology = evaluationTopology;
            TransitionRequirement = transitionRequirement;
            ReservationStatus = reservationStatus;
        }

        public WorldSnapshot Snapshot { get; }

        public LegalityActorRef Actor { get; }

        public SurfaceCell OriginCell { get; }

        public SurfaceCell CandidateCell { get; }

        public CubeTopologyState EvaluationTopology { get; }

        public TransitionRequirement TransitionRequirement { get; }

        public ReservationStatus ReservationStatus { get; }
    }

    internal readonly struct TileFeatureTraversalEvidence
    {
        private readonly IReadOnlyList<TileFeatureRuntimeDefinition> _definitions;

        public TileFeatureTraversalEvidence(IReadOnlyList<TileFeatureRuntimeDefinition> definitions)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public IReadOnlyList<TileFeatureRuntimeDefinition> Definitions =>
            _definitions ?? throw new InvalidOperationException(
                "TileFeature traversal evidence was not specified.");

        public static TileFeatureTraversalEvidence Empty { get; } =
            new TileFeatureTraversalEvidence(Array.Empty<TileFeatureRuntimeDefinition>());
    }

    internal readonly struct SettlementContext
    {
        public SettlementContext(
            WorldSnapshot occupancySnapshot,
            LegalityActorRef actor,
            SurfaceCell terminalCell,
            CubeTopologyState terminalTopology,
            SpatialState requestedTerminalState,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            OccupancySnapshot = occupancySnapshot ?? throw new ArgumentNullException(nameof(occupancySnapshot));
            Actor = actor;
            TerminalCell = terminalCell;
            TerminalTopology = terminalTopology;
            RequestedTerminalState = requestedTerminalState;
            ReservationStatus = reservationStatus;
        }

        public WorldSnapshot OccupancySnapshot { get; }

        public LegalityActorRef Actor { get; }

        public SurfaceCell TerminalCell { get; }

        public CubeTopologyState TerminalTopology { get; }

        public SpatialState RequestedTerminalState { get; }

        public ReservationStatus ReservationStatus { get; }
    }

    internal readonly struct TileFeatureSettlementEvidence
    {
        private readonly IReadOnlyList<TileFeatureRuntimeDefinition> _definitions;

        public TileFeatureSettlementEvidence(IReadOnlyList<TileFeatureRuntimeDefinition> definitions)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public IReadOnlyList<TileFeatureRuntimeDefinition> Definitions =>
            _definitions ?? throw new InvalidOperationException(
                "TileFeature settlement evidence was not specified.");

        public static TileFeatureSettlementEvidence Empty { get; } =
            new TileFeatureSettlementEvidence(Array.Empty<TileFeatureRuntimeDefinition>());
    }

    internal readonly struct JumpLandingEvidence
    {
        public JumpLandingEvidence(WorldSnapshot damageProjectionSnapshot, SurfaceCell lockedTargetCell)
        {
            DamageProjectionSnapshot = damageProjectionSnapshot ?? throw new ArgumentNullException(nameof(damageProjectionSnapshot));
            LockedTargetCell = lockedTargetCell;
        }

        public WorldSnapshot DamageProjectionSnapshot { get; }

        public SurfaceCell LockedTargetCell { get; }
    }

    internal readonly struct JumpCrushLandingEvaluation
    {
        public JumpCrushLandingEvaluation(LegalityResult legalityResult, int crushedBoxEntityId)
        {
            LegalityResult = legalityResult;
            CrushedBoxEntityId = crushedBoxEntityId;
        }

        public LegalityResult LegalityResult { get; }

        public int CrushedBoxEntityId { get; }
    }

    internal readonly struct CurrentEnemyLockRetentionEvidence
    {
        public CurrentEnemyLockRetentionEvidence(int sourceEntityId, int targetEntityId)
        {
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
        }

        public int SourceEntityId { get; }

        public int TargetEntityId { get; }
    }

    internal readonly struct ImpactFollowThroughEvidence
    {
        public ImpactFollowThroughEvidence(
            int attackSourceId,
            int targetId,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            bool ignoreActiveGlideOccupants = false)
            : this(attackSourceId, new[] { targetId }, destroyResolutions, ignoreActiveGlideOccupants)
        {
        }

        public ImpactFollowThroughEvidence(
            int attackSourceId,
            IReadOnlyList<int> targetIds,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            bool ignoreActiveGlideOccupants = false)
        {
            if (targetIds == null)
            {
                throw new ArgumentNullException(nameof(targetIds));
            }

            if (destroyResolutions == null)
            {
                throw new ArgumentNullException(nameof(destroyResolutions));
            }

            AttackSourceId = attackSourceId;
            TargetIds = targetIds;
            TargetId = TargetIds.Count > 0 ? TargetIds[0] : 0;
            DestroyResolutions = destroyResolutions;
            IgnoreActiveGlideOccupants = ignoreActiveGlideOccupants;
        }

        public int AttackSourceId { get; }

        public int TargetId { get; }

        public IReadOnlyList<int> TargetIds { get; }

        public IReadOnlyList<DestroyResolutionRecord> DestroyResolutions { get; }

        public bool IgnoreActiveGlideOccupants { get; }
    }
}
