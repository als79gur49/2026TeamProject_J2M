using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Model.Actions;

namespace Game.Feature.Gameplay.Model.Groups
{
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

        public int ImpactTargetId { get; private set; }

        public int ProjectileImpactTargetId => GroupKind == ActionGroupKind.ProjectileImpact ? ImpactTargetId : 0;

        public int BoxKineticTargetId { get; private set; }

        public int BoxKineticInstigatorEntityId { get; private set; }

        public int BoxKineticInstigatorTeamId { get; private set; }

        public List<MoveAction> Moves { get; }

        public List<DamageAction> Damages { get; }

        public List<SpawnAction> Spawns { get; }

        public List<DestroyAction> Destroys { get; }

        public List<StateChangeAction> StateChanges { get; }

        public List<BoardPresenceChangeAction> BoardPresenceChanges { get; }

        public List<TopologyChangeAction> TopologyChanges { get; }

        public List<DelayedAttackAction> DelayedAttacks { get; }

        public bool HasResolvedImpact => ImpactSourceId > 0 && ImpactTargetId > 0;

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
            ImpactTargetId = targetId;
        }

        public void AssignImpactReservation(int impactSourceId, int impactTargetId)
        {
            if (impactSourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactSourceId), "Impact source must be a positive entity ID.");
            }

            if (impactTargetId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactTargetId), "Impact target must be a positive entity ID.");
            }

            if (HasResolvedImpact)
            {
                throw new InvalidOperationException("Impact reservation has already been assigned.");
            }

            ImpactSourceId = impactSourceId;
            ImpactTargetId = impactTargetId;
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
