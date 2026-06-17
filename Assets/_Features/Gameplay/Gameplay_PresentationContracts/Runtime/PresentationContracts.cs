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
        EnemyPresentation = 10,
        ActionAudio = 11,
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
        ActionAudio = 9,
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
        BoxSlideMotion = 21,
        BoxFlipMotion = 22,
        BoxFlipImpactMotion = 23,
        PlayerActionAudio = 24,
        PlayerActionAttemptAudio = 25,
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

    public enum PresentationMotionFactKind
    {
        None = 0,
        BoxSlide = 1,
        BoxFlip = 2,
        BoxFlipImpact = 3,
    }

    public enum PresentationMotionActionKind
    {
        None = 0,
        Push = 1,
        Flip = 2,
    }

    public readonly struct PresentationMotionPayload : IEquatable<PresentationMotionPayload>
    {
        public PresentationMotionPayload(
            PresentationMotionFactKind kind,
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            int actorEntityId = 0,
            PresentationMotionActionKind actionKind = PresentationMotionActionKind.None,
            Direction direction = Direction.None,
            Direction sourceFacing = Direction.None,
            Direction destinationFacing = Direction.None,
            int sourceSequenceId = 0,
            int sourceActionPlanId = 0,
            int impactTargetEntityId = 0,
            int flipDisposition = 0,
            bool hasLandingCell = false,
            SurfaceCell landingCell = default,
            CubeTopologyState topology = default,
            bool hasTopology = false)
        {
            Kind = kind;
            EntityId = Math.Max(0, entityId);
            ActorEntityId = Math.Max(0, actorEntityId);
            ActionKind = actionKind;
            Direction = direction;
            SourceFacing = sourceFacing;
            DestinationFacing = destinationFacing;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            SourceActionPlanId = Math.Max(0, sourceActionPlanId);
            ImpactTargetEntityId = Math.Max(0, impactTargetEntityId);
            FlipDisposition = Math.Max(0, flipDisposition);
            HasLandingCell = hasLandingCell;
            LandingCell = landingCell;
            Topology = topology;
            HasTopology = hasTopology;
        }

        public PresentationMotionFactKind Kind { get; }

        public int EntityId { get; }

        public int ActorEntityId { get; }

        public PresentationMotionActionKind ActionKind { get; }

        public Direction Direction { get; }

        public Direction SourceFacing { get; }

        public Direction DestinationFacing { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public int SourceSequenceId { get; }

        public int SourceActionPlanId { get; }

        public int ImpactTargetEntityId { get; }

        public int FlipDisposition { get; }

        public bool HasLandingCell { get; }

        public SurfaceCell LandingCell { get; }

        public CubeTopologyState Topology { get; }

        public bool HasTopology { get; }

        public bool IsValid => Kind != PresentationMotionFactKind.None && EntityId > 0;

        public bool Equals(PresentationMotionPayload other)
        {
            return Kind == other.Kind &&
                   EntityId == other.EntityId &&
                   ActorEntityId == other.ActorEntityId &&
                   ActionKind == other.ActionKind &&
                   Direction == other.Direction &&
                   SourceFacing == other.SourceFacing &&
                   DestinationFacing == other.DestinationFacing &&
                   SourceCell.Equals(other.SourceCell) &&
                   DestinationCell.Equals(other.DestinationCell) &&
                   SourceSequenceId == other.SourceSequenceId &&
                   SourceActionPlanId == other.SourceActionPlanId &&
                   ImpactTargetEntityId == other.ImpactTargetEntityId &&
                   FlipDisposition == other.FlipDisposition &&
                   HasLandingCell == other.HasLandingCell &&
                   LandingCell.Equals(other.LandingCell) &&
                   Topology.Equals(other.Topology) &&
                   HasTopology == other.HasTopology;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationMotionPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ ActorEntityId;
                hash = (hash * 397) ^ (int)ActionKind;
                hash = (hash * 397) ^ (int)Direction;
                hash = (hash * 397) ^ (int)SourceFacing;
                hash = (hash * 397) ^ (int)DestinationFacing;
                hash = (hash * 397) ^ SourceCell.GetHashCode();
                hash = (hash * 397) ^ DestinationCell.GetHashCode();
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ SourceActionPlanId;
                hash = (hash * 397) ^ ImpactTargetEntityId;
                hash = (hash * 397) ^ FlipDisposition;
                hash = (hash * 397) ^ HasLandingCell.GetHashCode();
                hash = (hash * 397) ^ LandingCell.GetHashCode();
                hash = (hash * 397) ^ Topology.GetHashCode();
                hash = (hash * 397) ^ HasTopology.GetHashCode();
                return hash;
            }
        }
    }

    public enum PresentationAnimationFactKind
    {
        None = 0,
        PlayerAction = 1,
        EnemyPresentation = 2,
    }

    public enum PresentationAnimationActionKind
    {
        None = 0,
        Push = 1,
        Flip = 2,
        EnemyJump = 3,
        EnemyCharge = 4,
        EnemyDeath = 5,
    }

    public enum PresentationAnimationPhaseKind
    {
        None = 0,
        Windup = 1,
        Execute = 2,
        Recovery = 3,
        Failed = 4,
        Airborne = 5,
        Land = 6,
        Active = 7,
        Death = 8,
    }

    public enum PresentationAnimationOutcomeKind
    {
        None = 0,
        Started = 1,
        Executed = 2,
        Blocked = 3,
        Impact = 4,
        Recovery = 5,
        Failed = 6,
        Landed = 7,
        Retried = 8,
        Death = 9,
    }

    public readonly struct PresentationAnimationPayload : IEquatable<PresentationAnimationPayload>
    {
        public PresentationAnimationPayload(
            PresentationAnimationFactKind kind,
            int entityId,
            PresentationAnimationActionKind actionKind,
            PresentationAnimationPhaseKind phaseKind,
            PresentationAnimationOutcomeKind outcomeKind,
            int sourceTickIndex,
            int sourceSequenceId = 0,
            int sourceActionPlanId = 0,
            int targetEntityId = 0,
            Direction direction = Direction.None)
        {
            Kind = kind;
            EntityId = Math.Max(0, entityId);
            ActionKind = actionKind;
            PhaseKind = phaseKind;
            OutcomeKind = outcomeKind;
            SourceTickIndex = Math.Max(0, sourceTickIndex);
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            SourceActionPlanId = Math.Max(0, sourceActionPlanId);
            TargetEntityId = Math.Max(0, targetEntityId);
            Direction = direction;
        }

        public PresentationAnimationFactKind Kind { get; }

        public int EntityId { get; }

        public PresentationAnimationActionKind ActionKind { get; }

        public PresentationAnimationPhaseKind PhaseKind { get; }

        public PresentationAnimationOutcomeKind OutcomeKind { get; }

        public int SourceTickIndex { get; }

        public int SourceSequenceId { get; }

        public int SourceActionPlanId { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }

        public bool IsValid =>
            Kind != PresentationAnimationFactKind.None &&
            EntityId > 0 &&
            ActionKind != PresentationAnimationActionKind.None &&
            PhaseKind != PresentationAnimationPhaseKind.None;

        public bool Equals(PresentationAnimationPayload other)
        {
            return Kind == other.Kind &&
                   EntityId == other.EntityId &&
                   ActionKind == other.ActionKind &&
                   PhaseKind == other.PhaseKind &&
                   OutcomeKind == other.OutcomeKind &&
                   SourceTickIndex == other.SourceTickIndex &&
                   SourceSequenceId == other.SourceSequenceId &&
                   SourceActionPlanId == other.SourceActionPlanId &&
                   TargetEntityId == other.TargetEntityId &&
                   Direction == other.Direction;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationAnimationPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ (int)ActionKind;
                hash = (hash * 397) ^ (int)PhaseKind;
                hash = (hash * 397) ^ (int)OutcomeKind;
                hash = (hash * 397) ^ SourceTickIndex;
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ SourceActionPlanId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ (int)Direction;
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

    public enum PresentationActionAudioOutcomeKind
    {
        None = 0,
        Started = 1,
        AttemptFeedback = 2,
    }

    public readonly struct PresentationActionAudioPayload : IEquatable<PresentationActionAudioPayload>
    {
        public PresentationActionAudioPayload(
            int ownerEntityId,
            int actionKind,
            int moment,
            int sourceTickIndex,
            int sourceSequenceId = 0,
            int sourceActionPlanId = 0,
            int targetEntityId = 0,
            Direction direction = Direction.None,
            PresentationActionAudioOutcomeKind outcomeKind = PresentationActionAudioOutcomeKind.None,
            int sourceFeedbackKind = 0)
        {
            OwnerEntityId = Math.Max(0, ownerEntityId);
            ActionKind = actionKind;
            Moment = moment;
            SourceTickIndex = Math.Max(0, sourceTickIndex);
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            SourceActionPlanId = Math.Max(0, sourceActionPlanId);
            TargetEntityId = Math.Max(0, targetEntityId);
            Direction = direction;
            OutcomeKind = outcomeKind;
            SourceFeedbackKind = Math.Max(0, sourceFeedbackKind);
        }

        public int OwnerEntityId { get; }

        public int ActionKind { get; }

        public int Moment { get; }

        public int SourceTickIndex { get; }

        public int SourceSequenceId { get; }

        public int SourceActionPlanId { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }

        public PresentationActionAudioOutcomeKind OutcomeKind { get; }

        public int SourceFeedbackKind { get; }

        public bool IsValid =>
            OwnerEntityId > 0 &&
            (ActionKind == 0 || ActionKind == 1) &&
            (Moment == 0 || Moment == 6 || Moment == 7 || Moment == 8);

        public bool Equals(PresentationActionAudioPayload other)
        {
            return OwnerEntityId == other.OwnerEntityId &&
                   ActionKind == other.ActionKind &&
                   Moment == other.Moment &&
                   SourceTickIndex == other.SourceTickIndex &&
                   SourceSequenceId == other.SourceSequenceId &&
                   SourceActionPlanId == other.SourceActionPlanId &&
                   TargetEntityId == other.TargetEntityId &&
                   Direction == other.Direction &&
                   OutcomeKind == other.OutcomeKind &&
                   SourceFeedbackKind == other.SourceFeedbackKind;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationActionAudioPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = OwnerEntityId;
                hash = (hash * 397) ^ ActionKind;
                hash = (hash * 397) ^ Moment;
                hash = (hash * 397) ^ SourceTickIndex;
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ SourceActionPlanId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ (int)Direction;
                hash = (hash * 397) ^ (int)OutcomeKind;
                hash = (hash * 397) ^ SourceFeedbackKind;
                return hash;
            }
        }
    }

    public enum PresentationEnemyPresentationKind
    {
        None = 0,
        Jump = 1,
        Charge = 2,
        Death = 3,
    }

    public enum PresentationEnemyPresentationPhase
    {
        None = 0,
        Windup = 1,
        Airborne = 2,
        Land = 3,
        Active = 4,
        Recover = 5,
        Death = 6,
    }

    public enum PresentationEnemyPresentationOutcome
    {
        None = 0,
        Started = 1,
        ActiveStarted = 2,
        Landed = 3,
        Retried = 4,
        Death = 5,
    }

    public readonly struct PresentationEnemyPayload : IEquatable<PresentationEnemyPayload>
    {
        public PresentationEnemyPayload(
            PresentationEnemyPresentationKind kind,
            PresentationEnemyPresentationPhase phase,
            int enemyEntityId,
            int sourceTickIndex,
            int sourceSequenceId = 0,
            PresentationEnemyPresentationOutcome outcome = PresentationEnemyPresentationOutcome.None,
            SurfaceCell sourceCell = default,
            SurfaceCell targetCell = default,
            bool hasSourceCell = false,
            bool hasTargetCell = false,
            Direction direction = Direction.None,
            int sourceCause = 0,
            int timing = 0)
        {
            Kind = kind;
            Phase = phase;
            EnemyEntityId = Math.Max(0, enemyEntityId);
            SourceTickIndex = Math.Max(0, sourceTickIndex);
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            Outcome = outcome;
            SourceCell = sourceCell;
            TargetCell = targetCell;
            HasSourceCell = hasSourceCell;
            HasTargetCell = hasTargetCell;
            Direction = direction;
            SourceCause = sourceCause;
            Timing = timing;
        }

        public PresentationEnemyPresentationKind Kind { get; }

        public PresentationEnemyPresentationPhase Phase { get; }

        public int EnemyEntityId { get; }

        public int SourceTickIndex { get; }

        public int SourceSequenceId { get; }

        public PresentationEnemyPresentationOutcome Outcome { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell TargetCell { get; }

        public bool HasSourceCell { get; }

        public bool HasTargetCell { get; }

        public Direction Direction { get; }

        public int SourceCause { get; }

        public int Timing { get; }

        public bool IsValid =>
            Kind != PresentationEnemyPresentationKind.None &&
            Phase != PresentationEnemyPresentationPhase.None &&
            EnemyEntityId > 0;

        public bool Equals(PresentationEnemyPayload other)
        {
            return Kind == other.Kind &&
                   Phase == other.Phase &&
                   EnemyEntityId == other.EnemyEntityId &&
                   SourceTickIndex == other.SourceTickIndex &&
                   SourceSequenceId == other.SourceSequenceId &&
                   Outcome == other.Outcome &&
                   SourceCell.Equals(other.SourceCell) &&
                   TargetCell.Equals(other.TargetCell) &&
                   HasSourceCell == other.HasSourceCell &&
                   HasTargetCell == other.HasTargetCell &&
                   Direction == other.Direction &&
                   SourceCause == other.SourceCause &&
                   Timing == other.Timing;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationEnemyPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ EnemyEntityId;
                hash = (hash * 397) ^ SourceTickIndex;
                hash = (hash * 397) ^ SourceSequenceId;
                hash = (hash * 397) ^ (int)Outcome;
                hash = (hash * 397) ^ SourceCell.GetHashCode();
                hash = (hash * 397) ^ TargetCell.GetHashCode();
                hash = (hash * 397) ^ HasSourceCell.GetHashCode();
                hash = (hash * 397) ^ HasTargetCell.GetHashCode();
                hash = (hash * 397) ^ (int)Direction;
                hash = (hash * 397) ^ SourceCause;
                hash = (hash * 397) ^ Timing;
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
            PresentationTopologyTransitionPayload topologyPayload = default,
            PresentationMotionPayload motionPayload = default,
            PresentationAnimationPayload animationPayload = default,
            PresentationEnemyPayload enemyPayload = default,
            PresentationActionAudioPayload actionAudioPayload = default)
        {
            Kind = kind;
            Source = source;
            Target = target;
            Payload = payload;
            TopologyPayload = topologyPayload;
            MotionPayload = motionPayload;
            AnimationPayload = animationPayload;
            EnemyPayload = enemyPayload;
            ActionAudioPayload = actionAudioPayload;
        }

        public PresentationFactKind Kind { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationFactPayload Payload { get; }

        public PresentationTopologyTransitionPayload TopologyPayload { get; }

        public PresentationMotionPayload MotionPayload { get; }

        public PresentationAnimationPayload AnimationPayload { get; }

        public PresentationEnemyPayload EnemyPayload { get; }

        public PresentationActionAudioPayload ActionAudioPayload { get; }

        public bool Equals(PresentationFact other)
        {
            return Kind == other.Kind &&
                   Source.Equals(other.Source) &&
                   Target.Equals(other.Target) &&
                   Payload.Equals(other.Payload) &&
                   TopologyPayload.Equals(other.TopologyPayload) &&
                   MotionPayload.Equals(other.MotionPayload) &&
                   AnimationPayload.Equals(other.AnimationPayload) &&
                   EnemyPayload.Equals(other.EnemyPayload) &&
                   ActionAudioPayload.Equals(other.ActionAudioPayload);
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
                hash = (hash * 397) ^ MotionPayload.GetHashCode();
                hash = (hash * 397) ^ AnimationPayload.GetHashCode();
                hash = (hash * 397) ^ EnemyPayload.GetHashCode();
                hash = (hash * 397) ^ ActionAudioPayload.GetHashCode();
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
            int stageFactCount,
            int enemyPresentationFactCount = 0,
            int actionAudioFactCount = 0)
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
            EnemyPresentationFactCount = Math.Max(0, enemyPresentationFactCount);
            ActionAudioFactCount = Math.Max(0, actionAudioFactCount);
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

        public int EnemyPresentationFactCount { get; }

        public int ActionAudioFactCount { get; }
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
