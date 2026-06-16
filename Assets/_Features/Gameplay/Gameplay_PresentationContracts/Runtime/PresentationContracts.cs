using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.PresentationContracts
{
    public enum PresentationFactKind
    {
        None = 0,
        Movement = 1,
        Action = 2,
        Combat = 3,
        EntityLifecycle = 4,
        Topology = 5,
        Objective = 6,
        Stage = 7,
        Tile = 8,
        Gravity = 9,
    }

    public enum PresentationDomain
    {
        None = 0,
        Motion = 1,
        Animation = 2,
        Vfx = 3,
        Sfx = 4,
        Camera = 5,
        Topology = 6,
        UiBridge = 7,
        Stage = 8,
    }

    public enum PresentationSemanticSource
    {
        None = 0,
        TickPresentationData = 1,
        EntityMotion = 2,
        PlayerAction = 3,
        PlayerActionAttempt = 4,
        PlayerLocomotion = 5,
        PlayerDamage = 6,
        PlayerDeath = 7,
        EnemyDamage = 8,
        EnemyAction = 9,
        EnemyJump = 10,
        EnemyCharge = 11,
        EnemyGlide = 12,
        EnemyUtility = 13,
        EntityExit = 14,
        EntitySpawn = 15,
        TileEvent = 16,
        GravityField = 17,
        TopologyMotion = 18,
        ObjectiveResult = 19,
        StageOutcome = 20,
    }

    public enum PresentationTargetKind
    {
        None = 0,
        Entity = 1,
        SurfaceCell = 2,
        Topology = 3,
        Stage = 4,
        Global = 5,
    }

    public enum PresentationAnchorKind
    {
        None = 0,
        EntityCenter = 1,
        EntityVisualRoot = 2,
        SurfaceCellCenter = 3,
        Camera = 4,
        TopologyOrbit = 5,
        StageGlobal = 6,
        Global = 7,
    }

    public readonly struct PresentationSource : IEquatable<PresentationSource>
    {
        public PresentationSource(
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int sourceEntityId = 0,
            int sourceActionKind = 0,
            int sourceSequence = 0)
        {
            TickIndex = tickIndex;
            SemanticSource = semanticSource;
            SourceEntityId = sourceEntityId;
            SourceActionKind = sourceActionKind;
            SourceSequence = sourceSequence;
        }

        public int TickIndex { get; }

        public PresentationSemanticSource SemanticSource { get; }

        public int SourceEntityId { get; }

        public int SourceActionKind { get; }

        public int SourceSequence { get; }

        public bool Equals(PresentationSource other)
        {
            return TickIndex == other.TickIndex &&
                   SemanticSource == other.SemanticSource &&
                   SourceEntityId == other.SourceEntityId &&
                   SourceActionKind == other.SourceActionKind &&
                   SourceSequence == other.SourceSequence;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)SemanticSource;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ SourceActionKind;
                hash = (hash * 397) ^ SourceSequence;
                return hash;
            }
        }
    }

    public readonly struct PresentationTarget : IEquatable<PresentationTarget>
    {
        private PresentationTarget(
            PresentationTargetKind kind,
            int entityId,
            SurfaceCell cell,
            int stageKey)
        {
            Kind = kind;
            EntityId = entityId;
            Cell = cell;
            StageKey = stageKey;
        }

        public PresentationTargetKind Kind { get; }

        public int EntityId { get; }

        public SurfaceCell Cell { get; }

        public int StageKey { get; }

        public static PresentationTarget None()
        {
            return default;
        }

        public static PresentationTarget Global()
        {
            return new PresentationTarget(PresentationTargetKind.Global, 0, default, 0);
        }

        public static PresentationTarget Topology()
        {
            return new PresentationTarget(PresentationTargetKind.Topology, 0, default, 0);
        }

        public static PresentationTarget Stage(int stageKey)
        {
            return new PresentationTarget(PresentationTargetKind.Stage, 0, default, stageKey);
        }

        public static PresentationTarget Entity(int entityId)
        {
            return entityId > 0
                ? new PresentationTarget(PresentationTargetKind.Entity, entityId, default, 0)
                : default;
        }

        public static PresentationTarget SurfaceCell(SurfaceCell cell)
        {
            return new PresentationTarget(PresentationTargetKind.SurfaceCell, 0, cell, 0);
        }

        public bool Equals(PresentationTarget other)
        {
            return Kind == other.Kind &&
                   EntityId == other.EntityId &&
                   Cell.Equals(other.Cell) &&
                   StageKey == other.StageKey;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ Cell.GetHashCode();
                hash = (hash * 397) ^ StageKey;
                return hash;
            }
        }
    }

    public readonly struct PresentationAnchor : IEquatable<PresentationAnchor>
    {
        private PresentationAnchor(PresentationAnchorKind kind, PresentationTarget target)
        {
            Kind = kind;
            Target = target;
        }

        public PresentationAnchorKind Kind { get; }

        public PresentationTarget Target { get; }

        public static PresentationAnchor None()
        {
            return default;
        }

        public static PresentationAnchor ForEntityCenter(int entityId)
        {
            return new PresentationAnchor(
                PresentationAnchorKind.EntityCenter,
                PresentationTarget.Entity(entityId));
        }

        public static PresentationAnchor ForEntityVisualRoot(int entityId)
        {
            return new PresentationAnchor(
                PresentationAnchorKind.EntityVisualRoot,
                PresentationTarget.Entity(entityId));
        }

        public static PresentationAnchor ForSurfaceCellCenter(SurfaceCell cell)
        {
            return new PresentationAnchor(
                PresentationAnchorKind.SurfaceCellCenter,
                PresentationTarget.SurfaceCell(cell));
        }

        public static PresentationAnchor ForTopologyOrbit()
        {
            return new PresentationAnchor(
                PresentationAnchorKind.TopologyOrbit,
                PresentationTarget.Topology());
        }

        public static PresentationAnchor ForStageGlobal(int stageKey = 0)
        {
            return new PresentationAnchor(
                PresentationAnchorKind.StageGlobal,
                PresentationTarget.Stage(stageKey));
        }

        public static PresentationAnchor ForGlobal()
        {
            return new PresentationAnchor(
                PresentationAnchorKind.Global,
                PresentationTarget.Global());
        }

        public bool Equals(PresentationAnchor other)
        {
            return Kind == other.Kind && Target.Equals(other.Target);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationAnchor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ Target.GetHashCode();
            }
        }
    }

    public readonly struct PresentationFactPayload : IEquatable<PresentationFactPayload>
    {
        public PresentationFactPayload(
            int primaryValue = 0,
            int secondaryValue = 0,
            int tertiaryValue = 0,
            SurfaceCell primaryCell = default,
            SurfaceCell secondaryCell = default,
            bool hasPrimaryCell = false,
            bool hasSecondaryCell = false)
        {
            PrimaryValue = primaryValue;
            SecondaryValue = secondaryValue;
            TertiaryValue = tertiaryValue;
            PrimaryCell = primaryCell;
            SecondaryCell = secondaryCell;
            HasPrimaryCell = hasPrimaryCell;
            HasSecondaryCell = hasSecondaryCell;
        }

        public int PrimaryValue { get; }

        public int SecondaryValue { get; }

        public int TertiaryValue { get; }

        public SurfaceCell PrimaryCell { get; }

        public SurfaceCell SecondaryCell { get; }

        public bool HasPrimaryCell { get; }

        public bool HasSecondaryCell { get; }

        public PresentationAnchor PrimaryCellCenterAnchorOrEntityCenter(int entityId)
        {
            return HasPrimaryCell
                ? PresentationAnchor.ForSurfaceCellCenter(PrimaryCell)
                : PresentationAnchor.ForEntityCenter(entityId);
        }

        public bool Equals(PresentationFactPayload other)
        {
            return PrimaryValue == other.PrimaryValue &&
                   SecondaryValue == other.SecondaryValue &&
                   TertiaryValue == other.TertiaryValue &&
                   PrimaryCell.Equals(other.PrimaryCell) &&
                   SecondaryCell.Equals(other.SecondaryCell) &&
                   HasPrimaryCell == other.HasPrimaryCell &&
                   HasSecondaryCell == other.HasSecondaryCell;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationFactPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PrimaryValue;
                hash = (hash * 397) ^ SecondaryValue;
                hash = (hash * 397) ^ TertiaryValue;
                hash = (hash * 397) ^ PrimaryCell.GetHashCode();
                hash = (hash * 397) ^ SecondaryCell.GetHashCode();
                hash = (hash * 397) ^ HasPrimaryCell.GetHashCode();
                hash = (hash * 397) ^ HasSecondaryCell.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct PresentationTopologyTransitionPayload : IEquatable<PresentationTopologyTransitionPayload>
    {
        public PresentationTopologyTransitionPayload(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            int sourceTickIndex,
            bool hasSourceMetadata = false,
            int sourceMetadataKey = 0)
        {
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
            SourceTickIndex = sourceTickIndex;
            HasSourceMetadata = hasSourceMetadata;
            SourceMetadataKey = Math.Max(0, sourceMetadataKey);
        }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public int SourceTickIndex { get; }

        public bool HasSourceMetadata { get; }

        public int SourceMetadataKey { get; }

        public bool Equals(PresentationTopologyTransitionPayload other)
        {
            return SourceTopology.Equals(other.SourceTopology) &&
                   DestinationTopology.Equals(other.DestinationTopology) &&
                   RotationKind == other.RotationKind &&
                   SourceTickIndex == other.SourceTickIndex &&
                   HasSourceMetadata == other.HasSourceMetadata &&
                   SourceMetadataKey == other.SourceMetadataKey;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationTopologyTransitionPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SourceTopology.GetHashCode();
                hash = (hash * 397) ^ DestinationTopology.GetHashCode();
                hash = (hash * 397) ^ (int)RotationKind;
                hash = (hash * 397) ^ SourceTickIndex;
                hash = (hash * 397) ^ HasSourceMetadata.GetHashCode();
                hash = (hash * 397) ^ SourceMetadataKey;
                return hash;
            }
        }
    }

    public readonly struct PresentationFact : IEquatable<PresentationFact>
    {
        public PresentationFact(
            PresentationFactKind kind,
            PresentationSource source,
            PresentationTarget target,
            PresentationFactPayload payload = default,
            PresentationTopologyTransitionPayload topologyPayload = default)
        {
            Kind = kind;
            Source = source;
            Target = target;
            Payload = payload;
            TopologyPayload = topologyPayload;
        }

        public PresentationFactKind Kind { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationFactPayload Payload { get; }

        public PresentationTopologyTransitionPayload TopologyPayload { get; }

        public bool Equals(PresentationFact other)
        {
            return Kind == other.Kind &&
                   Source.Equals(other.Source) &&
                   Target.Equals(other.Target) &&
                   Payload.Equals(other.Payload) &&
                   TopologyPayload.Equals(other.TopologyPayload);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationFact other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ Target.GetHashCode();
                hash = (hash * 397) ^ Payload.GetHashCode();
                hash = (hash * 397) ^ TopologyPayload.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct PresentationFactFrameDiagnostics
    {
        public PresentationFactFrameDiagnostics(
            int extractedFactCount,
            int topologyFactCount,
            int combatFactCount,
            int lifecycleFactCount,
            int movementFactCount,
            int tileFactCount,
            int gravityFactCount,
            int objectiveFactCount,
            int stageFactCount)
        {
            ExtractedFactCount = Math.Max(0, extractedFactCount);
            TopologyFactCount = Math.Max(0, topologyFactCount);
            CombatFactCount = Math.Max(0, combatFactCount);
            LifecycleFactCount = Math.Max(0, lifecycleFactCount);
            MovementFactCount = Math.Max(0, movementFactCount);
            TileFactCount = Math.Max(0, tileFactCount);
            GravityFactCount = Math.Max(0, gravityFactCount);
            ObjectiveFactCount = Math.Max(0, objectiveFactCount);
            StageFactCount = Math.Max(0, stageFactCount);
        }

        public int ExtractedFactCount { get; }

        public int TopologyFactCount { get; }

        public int CombatFactCount { get; }

        public int LifecycleFactCount { get; }

        public int MovementFactCount { get; }

        public int TileFactCount { get; }

        public int GravityFactCount { get; }

        public int ObjectiveFactCount { get; }

        public int StageFactCount { get; }
    }

    public sealed class PresentationFactFrame
    {
        private static readonly IReadOnlyList<PresentationFact> EmptyFacts =
            new ReadOnlyCollection<PresentationFact>(new List<PresentationFact>());

        private readonly IReadOnlyList<PresentationFact> _facts;

        public PresentationFactFrame(
            int tickIndex,
            IReadOnlyList<PresentationFact> facts,
            PresentationFactFrameDiagnostics diagnostics)
        {
            TickIndex = tickIndex;
            _facts = new ReadOnlyCollection<PresentationFact>(
                new List<PresentationFact>(facts ?? EmptyFacts));
            Diagnostics = diagnostics;
        }

        public int TickIndex { get; }

        public IReadOnlyList<PresentationFact> Facts => _facts;

        public PresentationFactFrameDiagnostics Diagnostics { get; }

        public static PresentationFactFrame Empty(int tickIndex)
        {
            return new PresentationFactFrame(
                tickIndex,
                EmptyFacts,
                new PresentationFactFrameDiagnostics(0, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}
