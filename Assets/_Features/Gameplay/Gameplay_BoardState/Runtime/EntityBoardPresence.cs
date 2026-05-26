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

    public readonly struct ScheduledFlipContact : IEquatable<ScheduledFlipContact>
    {
        public ScheduledFlipContact(
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
            int executeTick,
            int dueTick,
            int orderingKey,
            FlipContactCancellationPolicy cancellationPolicy,
            FlipContactDispositionPolicy dispositionPolicy)
        {
            if (actionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Scheduled flip contact action id must be positive.");
            }

            if (actorEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actorEntityId), "Scheduled flip contact actor id must be positive.");
            }

            if (sourceBoxEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceBoxEntityId), "Scheduled flip contact source box id must be positive.");
            }

            if (dueTick < executeTick)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTick), "Scheduled flip contact due tick cannot precede execute tick.");
            }

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
            ExecuteTick = executeTick;
            DueTick = dueTick;
            OrderingKey = orderingKey;
            CancellationPolicy = cancellationPolicy;
            DispositionPolicy = dispositionPolicy;
        }

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

        public int ExecuteTick { get; }

        public int DueTick { get; }

        public int OrderingKey { get; }

        public FlipContactCancellationPolicy CancellationPolicy { get; }

        public FlipContactDispositionPolicy DispositionPolicy { get; }

        public bool Equals(ScheduledFlipContact other)
        {
            return ActionId == other.ActionId &&
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
                   ExecuteTick == other.ExecuteTick &&
                   DueTick == other.DueTick &&
                   OrderingKey == other.OrderingKey &&
                   CancellationPolicy == other.CancellationPolicy &&
                   DispositionPolicy == other.DispositionPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is ScheduledFlipContact other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ActionId;
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
                hashCode = (hashCode * 397) ^ ExecuteTick;
                hashCode = (hashCode * 397) ^ DueTick;
                hashCode = (hashCode * 397) ^ OrderingKey;
                hashCode = (hashCode * 397) ^ (int)CancellationPolicy;
                hashCode = (hashCode * 397) ^ (int)DispositionPolicy;
                return hashCode;
            }
        }
    }

    internal readonly struct ScheduledFlipContactSnapshotEntry
    {
        public ScheduledFlipContactSnapshotEntry(int actionId, ScheduledFlipContact contact)
        {
            ActionId = actionId;
            Contact = contact;
        }

        public int ActionId { get; }

        public ScheduledFlipContact Contact { get; }
    }

    internal sealed class ScheduledFlipContactComparer :
        IComparer<ScheduledFlipContact>,
        IComparer<ScheduledFlipContactSnapshotEntry>
    {
        public static readonly ScheduledFlipContactComparer Instance = new ScheduledFlipContactComparer();

        private ScheduledFlipContactComparer()
        {
        }

        public int Compare(ScheduledFlipContact x, ScheduledFlipContact y)
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

            return x.OrderingKey.CompareTo(y.OrderingKey);
        }

        public int Compare(ScheduledFlipContactSnapshotEntry x, ScheduledFlipContactSnapshotEntry y)
        {
            var contactComparison = Compare(x.Contact, y.Contact);
            if (contactComparison != 0)
            {
                return contactComparison;
            }

            return x.ActionId.CompareTo(y.ActionId);
        }
    }
}
