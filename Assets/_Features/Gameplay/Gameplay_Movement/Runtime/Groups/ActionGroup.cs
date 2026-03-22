using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Actions;
using Game.Feature.Gameplay.Movement.Actions;

namespace Game.Feature.Gameplay.Movement.Groups
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
        }

        public int GroupId { get; private set; }

        public int IntentId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public ActionGroupKind GroupKind { get; }

        public List<MoveAction> Moves { get; }

        public List<DamageAction> Damages { get; }

        public List<SpawnAction> Spawns { get; }

        public List<DestroyAction> Destroys { get; }

        public List<StateChangeAction> StateChanges { get; }

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
