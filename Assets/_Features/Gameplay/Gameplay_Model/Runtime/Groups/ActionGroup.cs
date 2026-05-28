using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;

namespace Game.Feature.Gameplay.Model.Groups
{
    internal readonly struct ScheduledFlipContactDraft
    {
        public ScheduledFlipContactDraft(
            int actorEntityId,
            int sourceBoxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell contactCell,
            SurfaceCell landingCell,
            Direction flipDirection,
            FaceId sourceFace,
            BoxCapabilities sourceCapabilitiesSnapshot,
            int damageAmount,
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
            ActorEntityId = actorEntityId;
            SourceBoxEntityId = sourceBoxEntityId;
            SourceCell = sourceCell;
            ContactCell = contactCell;
            LandingCell = landingCell;
            FlipDirection = flipDirection;
            SourceFace = sourceFace;
            SourceCapabilitiesSnapshot = sourceCapabilitiesSnapshot;
            DamageAmount = damageAmount;
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

        public int ActorEntityId { get; }

        public int SourceBoxEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ContactCell { get; }

        public SurfaceCell LandingCell { get; }

        public Direction FlipDirection { get; }

        public FaceId SourceFace { get; }

        public BoxCapabilities SourceCapabilitiesSnapshot { get; }

        public int DamageAmount { get; }

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

        public ScheduledFlipContact ToScheduledContact(int actionId)
        {
            return new ScheduledFlipContact(
                actionId,
                ActorEntityId,
                SourceBoxEntityId,
                SourceCell,
                ContactCell,
                LandingCell,
                FlipDirection,
                SourceFace,
                SourceCapabilitiesSnapshot,
                new FlipImpactDamageSpec(
                    DamageAmount,
                    FlipImpactDamageKind.Impact,
                    AttackSourceKind.B1ScheduledContactDue,
                    actionId),
                KineticInstigatorEntityId,
                KineticInstigatorTeamId,
                ActionStartTick,
                ActionVisualImpactTick,
                FlipExecuteDelayTicks,
                FlipInputLockDurationTicks,
                ExecuteTick,
                DueTick,
                OrderingKey,
                CancellationPolicy,
                DispositionPolicy);
        }
    }

    internal sealed class ActionGroup
    {
        public ActionGroup(
            int intentId,
            int sourceId,
            int priority,
            ActionGroupKind groupKind,
            AttackSourceKind attackSourceKind = AttackSourceKind.Combat)
        {
            if (intentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intentId), "Action groups must reference an assigned intent ID.");
            }

            IntentId = intentId;
            SourceId = sourceId;
            Priority = priority;
            GroupKind = groupKind;
            AttackSourceKind = attackSourceKind;
            ImpactTargetIds = new List<int>();
            Moves = new List<MoveAction>();
            Damages = new List<DamageAction>();
            Spawns = new List<SpawnAction>();
            Destroys = new List<DestroyAction>();
            StateChanges = new List<StateChangeAction>();
            BoardPresenceChanges = new List<BoardPresenceChangeAction>();
            TopologyChanges = new List<TopologyChangeAction>();
            DelayedAttacks = new List<DelayedAttackAction>();
        }

        public int GroupId { get; private set; }

        public int IntentId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public ActionGroupKind GroupKind { get; }

        public AttackSourceKind AttackSourceKind { get; }

        public int ImpactSourceId { get; private set; }

        public int ImpactTargetId => ImpactTargetIds.Count > 0 ? ImpactTargetIds[0] : 0;

        public IReadOnlyList<int> ImpactTargetIds { get; }

        public int DeferredImpactSourceId { get; private set; }

        public SurfaceCell DeferredImpactCell { get; private set; }

        public int ProjectileImpactTargetId => GroupKind == ActionGroupKind.ProjectileImpact ? ImpactTargetId : 0;

        public int BoxKineticTargetId { get; private set; }

        public int BoxKineticInstigatorEntityId { get; private set; }

        public int BoxKineticInstigatorTeamId { get; private set; }

        public bool HasScheduledFlipContact { get; private set; }

        public ScheduledFlipContactDraft ScheduledFlipContactDraft { get; private set; }

        public List<MoveAction> Moves { get; }

        public List<DamageAction> Damages { get; }

        public List<SpawnAction> Spawns { get; }

        public List<DestroyAction> Destroys { get; }

        public List<StateChangeAction> StateChanges { get; }

        public List<BoardPresenceChangeAction> BoardPresenceChanges { get; }

        public List<TopologyChangeAction> TopologyChanges { get; }

        public List<DelayedAttackAction> DelayedAttacks { get; }

        public bool HasResolvedImpact => ImpactSourceId > 0 && ImpactTargetIds.Count > 0;

        public bool HasDeferredImpact => DeferredImpactSourceId > 0;

        public void AssignProjectileImpactTarget(int targetId)
        {
            if (GroupKind != ActionGroupKind.ProjectileImpact)
            {
                throw new InvalidOperationException("Only projectile impact groups can assign an impact target.");
            }

            if (targetId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetId), "Projectile impact targets must be positive entity IDs.");
            }

            if (HasResolvedImpact)
            {
                throw new InvalidOperationException("Projectile impact target has already been assigned.");
            }

            ImpactSourceId = SourceId;
            ((List<int>)ImpactTargetIds).Add(targetId);
        }

        public void AssignImpactReservation(int impactSourceId, int impactTargetId)
        {
            AssignImpactReservation(impactSourceId, new[] { impactTargetId });
        }

        public void AssignImpactReservation(int impactSourceId, IReadOnlyList<int> impactTargetIds)
        {
            if (impactSourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactSourceId), "Impact source must be a positive entity ID.");
            }

            if (impactTargetIds == null)
            {
                throw new ArgumentNullException(nameof(impactTargetIds));
            }

            if (impactTargetIds.Count == 0)
            {
                throw new ArgumentException("Impact target list must not be empty.", nameof(impactTargetIds));
            }

            for (var i = 0; i < impactTargetIds.Count; i++)
            {
                if (impactTargetIds[i] <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(impactTargetIds), "Impact targets must be positive entity IDs.");
                }
            }

            if (HasResolvedImpact)
            {
                throw new InvalidOperationException("Impact reservation has already been assigned.");
            }

            ImpactSourceId = impactSourceId;
            var targetIds = (List<int>)ImpactTargetIds;
            for (var i = 0; i < impactTargetIds.Count; i++)
            {
                targetIds.Add(impactTargetIds[i]);
            }
        }

        public void AssignDeferredImpact(int impactSourceId, SurfaceCell impactCell)
        {
            if (impactSourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactSourceId), "Deferred impact source must be a positive entity ID.");
            }

            if (HasResolvedImpact || HasDeferredImpact)
            {
                throw new InvalidOperationException("Impact has already been assigned.");
            }

            DeferredImpactSourceId = impactSourceId;
            DeferredImpactCell = impactCell;
        }

        public void AssignBoxKineticOwner(int targetBoxId, int instigatorEntityId, int instigatorTeamId)
        {
            if (targetBoxId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetBoxId), "Kinetic ownership target must be a positive entity ID.");
            }

            if (instigatorEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instigatorEntityId), "Kinetic instigator must be a positive entity ID.");
            }

            if (instigatorTeamId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instigatorTeamId), "Kinetic instigator team must be a positive team ID.");
            }

            if (BoxKineticTargetId != 0)
            {
                throw new InvalidOperationException("Box kinetic ownership has already been assigned.");
            }

            BoxKineticTargetId = targetBoxId;
            BoxKineticInstigatorEntityId = instigatorEntityId;
            BoxKineticInstigatorTeamId = instigatorTeamId;
        }

        public void AssignScheduledFlipContact(ScheduledFlipContactDraft contactDraft)
        {
            if (GroupKind != ActionGroupKind.Flip)
            {
                throw new InvalidOperationException("Only flip groups can schedule flip contacts.");
            }

            if (HasScheduledFlipContact)
            {
                throw new InvalidOperationException("Scheduled flip contact has already been assigned.");
            }

            HasScheduledFlipContact = true;
            ScheduledFlipContactDraft = contactDraft;
        }

        internal void AssignGroupId(int groupId)
        {
            if (groupId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groupId), "Group IDs must be positive.");
            }

            if (GroupId != 0)
            {
                throw new InvalidOperationException("Group ID has already been assigned.");
            }

            GroupId = groupId;
        }
    }
}
