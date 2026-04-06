using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Model.Actions;

namespace Game.Feature.Gameplay.Model.Groups
{
    public sealed class ActionGroup
    {
        public ActionGroup(int intentId, int sourceId, int priority, ActionGroupKind groupKind)
        {
            if (intentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intentId), "Action groups must reference an assigned intent ID.");
            }

            IntentId = intentId;
            SourceId = sourceId;
            Priority = priority;
            GroupKind = groupKind;
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

        public int ProjectileImpactTargetId { get; private set; }

        public List<MoveAction> Moves { get; }

        public List<DamageAction> Damages { get; }

        public List<SpawnAction> Spawns { get; }

        public List<DestroyAction> Destroys { get; }

        public List<StateChangeAction> StateChanges { get; }

        public List<BoardPresenceChangeAction> BoardPresenceChanges { get; }

        public List<TopologyChangeAction> TopologyChanges { get; }

        public List<DelayedAttackAction> DelayedAttacks { get; }

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

            if (ProjectileImpactTargetId != 0)
            {
                throw new InvalidOperationException("Projectile impact target has already been assigned.");
            }

            ProjectileImpactTargetId = targetId;
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
