using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;

namespace Game.Feature.Gameplay.BoardState
{
    public enum EntityBoardPresence
    {
        Occupying = 0,
        Detached = 1,
        InFlight = 2,
    }

    public enum FlipImpactDamageKind
    {
        Impact = 0,
    }

    public readonly struct FlipImpactDamageSpec : IEquatable<FlipImpactDamageSpec>
    {
        public FlipImpactDamageSpec(
            int damageAmount,
            FlipImpactDamageKind damageKind,
            AttackSourceKind sourceKind,
            int sourceActionId)
        {
            if (damageAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damageAmount), "Flip impact damage must be positive.");
            }

            DamageAmount = damageAmount;
            DamageKind = damageKind;
            SourceKind = sourceKind;
            SourceActionId = sourceActionId;
        }

        public int DamageAmount { get; }

        public FlipImpactDamageKind DamageKind { get; }

        public AttackSourceKind SourceKind { get; }

        public int SourceActionId { get; }

        public bool Equals(FlipImpactDamageSpec other)
        {
            return DamageAmount == other.DamageAmount &&
                   DamageKind == other.DamageKind &&
                   SourceKind == other.SourceKind &&
                   SourceActionId == other.SourceActionId;
        }

        public override bool Equals(object obj)
        {
            return obj is FlipImpactDamageSpec other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = DamageAmount;
                hashCode = (hashCode * 397) ^ (int)DamageKind;
                hashCode = (hashCode * 397) ^ (int)SourceKind;
                hashCode = (hashCode * 397) ^ SourceActionId;
                return hashCode;
            }
        }
    }

    public enum FlipContactCancellationPolicy
    {
        SafeReturnOrDestroy = 0,
        CancelSilently = 1,
    }

    public enum FlipContactDispositionPolicy
    {
        DefaultB1HostileImpact = 0,
        DefaultB1OrdinaryLanding = 1,
    }

    public enum ScheduledFlipResolutionKind
    {
        HostileImpact = 0,
        OrdinaryLanding = 1,
    }

    public enum FlipContactResolutionKind
    {
        EmptyLand = 0,
        Whiff = 1,
        HitHostileSurvived = 2,
        HitHostileDiedSettlementAllowed = 3,
        HitHostileDiedSettlementDenied = 4,
        BlockedByFriendly = 5,
        BlockedBySolid = 6,
        BlockedByTerrain = 7,
        BlockedByBoardEdge = 8,
        CancelledActorGone = 9,
        CancelledBoxGone = 10,
        CancelledTopologyChanged = 11,
        CancelledStageTerminal = 12,
        OrdinaryLandingHostileSurvivedDestroySelf = 13,
        OrdinaryLandingHostileKilledFollowThrough = 14,
        OrdinaryLandingHostileKilledSourceFallback = 15,
        OrdinaryLandingHostileKilledDestroySelf = 16,
        OrdinaryLandingNoDamageBlockSourceFallback = 17,
        OrdinaryLandingNoDamageBlockDestroySelf = 18,
        OrdinaryLandingSolidBlockSourceFallback = 19,
        OrdinaryLandingSolidBlockDestroySelf = 20,
        OrdinaryLandingInvalidSourceFallback = 21,
        OrdinaryLandingInvalidDestroySelf = 22,
        OrdinaryLandingSettlementDeniedSourceFallback = 23,
        OrdinaryLandingSettlementDeniedDestroySelf = 24,
        OrdinaryLandingTokenNoOp = 25,
    }

    public enum FlipBoxDisposition
    {
        None = 0,
        MaterializeAtLanding = 1,
        MaterializeAtSource = 2,
        DestroySelf = 3,
        StayAtContact = 4,
        Cancelled = 5,
    }

    public readonly struct FlipContactResolution
    {
        public FlipContactResolution(
            FlipContactResolutionKind kind,
            int? hitEntityId,
            int sourceBoxEntityId,
            SurfaceCell contactCell,
            SurfaceCell? materializeCell,
            FlipBoxDisposition disposition)
        {
            Kind = kind;
            HitEntityId = hitEntityId;
            SourceBoxEntityId = sourceBoxEntityId;
            ContactCell = contactCell;
            MaterializeCell = materializeCell.GetValueOrDefault();
            HasMaterializeCell = materializeCell.HasValue;
            Disposition = disposition;
        }

        public FlipContactResolutionKind Kind { get; }

        public int? HitEntityId { get; }

        public int SourceBoxEntityId { get; }

        public SurfaceCell ContactCell { get; }

        public SurfaceCell MaterializeCell { get; }

        public bool HasMaterializeCell { get; }

        public FlipBoxDisposition Disposition { get; }
    }

    public readonly struct ScheduledFlipResolution : IEquatable<ScheduledFlipResolution>
    {
        public ScheduledFlipResolution(
            ScheduledFlipResolutionKind kind,
            int actionId,
            int actorEntityId,
            int sourceBoxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell contactCell,
            SurfaceCell landingCell,
            Direction flipDirection,
            FaceId sourceFace,
            BoxCapabilities sourceCapabilitiesSnapshot,
            FlipImpactDamageSpec damageSpec,
            int kineticInstigatorEntityId,
            int kineticInstigatorTeamId,
            int actionStartTick,
            int actionVisualImpactTick,
            int flipExecuteDelayTicks,
            int flipInputLockDurationTicks,
            int executeTick,
            int dueTick,
            int orderingKey,
            FlipContactCancellationPolicy cancellationPolicy,
            FlipContactDispositionPolicy dispositionPolicy)
        {
            if (actionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Scheduled flip resolution action id must be positive.");
            }

            if (actorEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actorEntityId), "Scheduled flip resolution actor id must be positive.");
            }

            if (sourceBoxEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceBoxEntityId), "Scheduled flip resolution source box id must be positive.");
            }

            if (dueTick < executeTick)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTick), "Scheduled flip resolution due tick cannot precede execute tick.");
            }

            if (actionStartTick > executeTick)
            {
                throw new ArgumentOutOfRangeException(nameof(actionStartTick), "Scheduled flip resolution action start tick cannot follow execute tick.");
            }

            if (actionVisualImpactTick < actionStartTick)
            {
                throw new ArgumentOutOfRangeException(nameof(actionVisualImpactTick), "Scheduled flip resolution visual impact tick cannot precede action start tick.");
            }

            if (flipExecuteDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flipExecuteDelayTicks), "Scheduled flip resolution execute delay ticks must be zero or greater.");
            }

            if (flipInputLockDurationTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flipInputLockDurationTicks), "Scheduled flip resolution input lock duration ticks must be greater than zero.");
            }

            Kind = kind;
            ActionId = actionId;
            ActorEntityId = actorEntityId;
            SourceBoxEntityId = sourceBoxEntityId;
            SourceCell = sourceCell;
            ContactCell = contactCell;
            LandingCell = landingCell;
            FlipDirection = flipDirection;
            SourceFace = sourceFace;
            SourceCapabilitiesSnapshot = sourceCapabilitiesSnapshot;
            DamageSpec = damageSpec;
            KineticInstigatorEntityId = kineticInstigatorEntityId;
            KineticInstigatorTeamId = kineticInstigatorTeamId;
            ActionStartTick = actionStartTick;
            ActionVisualImpactTick = actionVisualImpactTick;
            FlipExecuteDelayTicks = flipExecuteDelayTicks;
            FlipInputLockDurationTicks = flipInputLockDurationTicks;
            ExecuteTick = executeTick;
            DueTick = dueTick;
            OrderingKey = orderingKey;
            CancellationPolicy = cancellationPolicy;
            DispositionPolicy = dispositionPolicy;
        }

        public ScheduledFlipResolutionKind Kind { get; }

        public int ActionId { get; }

        public int ActorEntityId { get; }

        public int SourceBoxEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ContactCell { get; }

        public SurfaceCell LandingCell { get; }

        public Direction FlipDirection { get; }

        public FaceId SourceFace { get; }

        public BoxCapabilities SourceCapabilitiesSnapshot { get; }

        public FlipImpactDamageSpec DamageSpec { get; }

        public int KineticInstigatorEntityId { get; }

        public int KineticInstigatorTeamId { get; }

        public int ActionStartTick { get; }

        public int ActionVisualImpactTick { get; }

        public int FlipExecuteDelayTicks { get; }

        public int FlipInputLockDurationTicks { get; }

        public int ExecuteTick { get; }

        public int DueTick { get; }

        public int OrderingKey { get; }

        public FlipContactCancellationPolicy CancellationPolicy { get; }

        public FlipContactDispositionPolicy DispositionPolicy { get; }

        public bool Equals(ScheduledFlipResolution other)
        {
            return Kind == other.Kind &&
                   ActionId == other.ActionId &&
                   ActorEntityId == other.ActorEntityId &&
                   SourceBoxEntityId == other.SourceBoxEntityId &&
                   SourceCell.Equals(other.SourceCell) &&
                   ContactCell.Equals(other.ContactCell) &&
                   LandingCell.Equals(other.LandingCell) &&
                   FlipDirection == other.FlipDirection &&
                   SourceFace == other.SourceFace &&
                   SourceCapabilitiesSnapshot == other.SourceCapabilitiesSnapshot &&
                   DamageSpec.Equals(other.DamageSpec) &&
                   KineticInstigatorEntityId == other.KineticInstigatorEntityId &&
                   KineticInstigatorTeamId == other.KineticInstigatorTeamId &&
                   ActionStartTick == other.ActionStartTick &&
                   ActionVisualImpactTick == other.ActionVisualImpactTick &&
                   FlipExecuteDelayTicks == other.FlipExecuteDelayTicks &&
                   FlipInputLockDurationTicks == other.FlipInputLockDurationTicks &&
                   ExecuteTick == other.ExecuteTick &&
                   DueTick == other.DueTick &&
                   OrderingKey == other.OrderingKey &&
                   CancellationPolicy == other.CancellationPolicy &&
                   DispositionPolicy == other.DispositionPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is ScheduledFlipResolution other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ ActionId;
                hashCode = (hashCode * 397) ^ ActorEntityId;
                hashCode = (hashCode * 397) ^ SourceBoxEntityId;
                hashCode = (hashCode * 397) ^ SourceCell.GetHashCode();
                hashCode = (hashCode * 397) ^ ContactCell.GetHashCode();
                hashCode = (hashCode * 397) ^ LandingCell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)FlipDirection;
                hashCode = (hashCode * 397) ^ (int)SourceFace;
                hashCode = (hashCode * 397) ^ (int)SourceCapabilitiesSnapshot;
                hashCode = (hashCode * 397) ^ DamageSpec.GetHashCode();
                hashCode = (hashCode * 397) ^ KineticInstigatorEntityId;
                hashCode = (hashCode * 397) ^ KineticInstigatorTeamId;
                hashCode = (hashCode * 397) ^ ActionStartTick;
                hashCode = (hashCode * 397) ^ ActionVisualImpactTick;
                hashCode = (hashCode * 397) ^ FlipExecuteDelayTicks;
                hashCode = (hashCode * 397) ^ FlipInputLockDurationTicks;
                hashCode = (hashCode * 397) ^ ExecuteTick;
                hashCode = (hashCode * 397) ^ DueTick;
                hashCode = (hashCode * 397) ^ OrderingKey;
                hashCode = (hashCode * 397) ^ (int)CancellationPolicy;
                hashCode = (hashCode * 397) ^ (int)DispositionPolicy;
                return hashCode;
            }
        }
    }

    internal readonly struct ScheduledFlipResolutionSnapshotEntry
    {
        public ScheduledFlipResolutionSnapshotEntry(int actionId, ScheduledFlipResolution resolution)
        {
            ActionId = actionId;
            Resolution = resolution;
        }

        public int ActionId { get; }

        public ScheduledFlipResolution Resolution { get; }
    }

    internal sealed class ScheduledFlipResolutionComparer :
        IComparer<ScheduledFlipResolution>,
        IComparer<ScheduledFlipResolutionSnapshotEntry>
    {
        public static readonly ScheduledFlipResolutionComparer Instance = new ScheduledFlipResolutionComparer();

        private ScheduledFlipResolutionComparer()
        {
        }

        public int Compare(ScheduledFlipResolution x, ScheduledFlipResolution y)
        {
            var dueTickComparison = x.DueTick.CompareTo(y.DueTick);
            if (dueTickComparison != 0)
            {
                return dueTickComparison;
            }

            var executeTickComparison = x.ExecuteTick.CompareTo(y.ExecuteTick);
            if (executeTickComparison != 0)
            {
                return executeTickComparison;
            }

            var actionIdComparison = x.ActionId.CompareTo(y.ActionId);
            if (actionIdComparison != 0)
            {
                return actionIdComparison;
            }

            var sourceBoxComparison = x.SourceBoxEntityId.CompareTo(y.SourceBoxEntityId);
            if (sourceBoxComparison != 0)
            {
                return sourceBoxComparison;
            }

            var kindComparison = x.Kind.CompareTo(y.Kind);
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            return x.OrderingKey.CompareTo(y.OrderingKey);
        }

        public int Compare(ScheduledFlipResolutionSnapshotEntry x, ScheduledFlipResolutionSnapshotEntry y)
        {
            var contactComparison = Compare(x.Resolution, y.Resolution);
            if (contactComparison != 0)
            {
                return contactComparison;
            }

            return x.ActionId.CompareTo(y.ActionId);
        }
    }
}
