using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Actions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Movement.Actions;
using Game.Feature.Gameplay.Movement.Groups;

namespace Game.Feature.Gameplay.Movement.Sorting
{
    public sealed class ActionGroupComparer : IComparer<ActionGroup>
    {
        public static readonly ActionGroupComparer Instance = new();

        public int Compare(ActionGroup left, ActionGroup right)
        {
            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.IntentId.CompareTo(right.IntentId);
            if (result != 0)
            {
                return result;
            }

            result = left.GroupId.CompareTo(right.GroupId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.GroupKind).CompareTo((int)right.GroupKind);
            if (result != 0)
            {
                return result;
            }

            result = CompareLists(left.Moves, right.Moves, CompareMoveActions);
            if (result != 0)
            {
                return result;
            }

            result = CompareLists(left.Damages, right.Damages, CompareDamageActions);
            if (result != 0)
            {
                return result;
            }

            result = CompareLists(left.Spawns, right.Spawns, CompareSpawnActions);
            if (result != 0)
            {
                return result;
            }

            result = CompareLists(left.Destroys, right.Destroys, CompareDestroyActions);
            if (result != 0)
            {
                return result;
            }

            return CompareLists(left.StateChanges, right.StateChanges, CompareStateChangeActions);
        }

        private static int CompareMoveActions(MoveAction left, MoveAction right)
        {
            var result = left.EntityId.CompareTo(right.EntityId);
            if (result != 0)
            {
                return result;
            }

            result = left.Destination.x.CompareTo(right.Destination.x);
            if (result != 0)
            {
                return result;
            }

            return left.Destination.y.CompareTo(right.Destination.y);
        }

        private static int CompareDamageActions(DamageAction left, DamageAction right)
        {
            var result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            return left.Amount.CompareTo(right.Amount);
        }

        private static int CompareSpawnActions(SpawnAction left, SpawnAction right)
        {
            var result = left.SpawnId.CompareTo(right.SpawnId);
            if (result != 0)
            {
                return result;
            }

            return CompareEntityStates(left.Entity, right.Entity);
        }

        private static int CompareDestroyActions(DestroyAction left, DestroyAction right)
        {
            return left.TargetId.CompareTo(right.TargetId);
        }

        private static int CompareStateChangeActions(StateChangeAction left, StateChangeAction right)
        {
            var result = left.EntityId.CompareTo(right.EntityId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.State).CompareTo((int)right.State);
            if (result != 0)
            {
                return result;
            }

            return left.StateTimer.CompareTo(right.StateTimer);
        }

        private static int CompareEntityStates(EntityState left, EntityState right)
        {
            var result = left.entityId.CompareTo(right.entityId);
            if (result != 0)
            {
                return result;
            }

            result = left.position.x.CompareTo(right.position.x);
            if (result != 0)
            {
                return result;
            }

            result = left.position.y.CompareTo(right.position.y);
            if (result != 0)
            {
                return result;
            }

            result = left.hp.CompareTo(right.hp);
            if (result != 0)
            {
                return result;
            }

            result = left.maxHp.CompareTo(right.maxHp);
            if (result != 0)
            {
                return result;
            }

            result = left.teamId.CompareTo(right.teamId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.type).CompareTo((int)right.type);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.state).CompareTo((int)right.state);
            if (result != 0)
            {
                return result;
            }

            result = left.stateTimer.CompareTo(right.stateTimer);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.facing).CompareTo((int)right.facing);
            if (result != 0)
            {
                return result;
            }

            result = left.markedForDeath.CompareTo(right.markedForDeath);
            if (result != 0)
            {
                return result;
            }

            return left.spawnTick.CompareTo(right.spawnTick);
        }

        private static int CompareLists<T>(
            IReadOnlyList<T> left,
            IReadOnlyList<T> right,
            Comparison<T> comparison)
        {
            var result = left.Count.CompareTo(right.Count);
            if (result != 0)
            {
                return result;
            }

            for (var i = 0; i < left.Count; i++)
            {
                result = comparison(left[i], right[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }
    }
}
