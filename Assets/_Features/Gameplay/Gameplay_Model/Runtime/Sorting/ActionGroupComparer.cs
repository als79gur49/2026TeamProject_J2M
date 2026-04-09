using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Model.Sorting
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

            result = left.ImpactSourceId.CompareTo(right.ImpactSourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.ImpactTargetId.CompareTo(right.ImpactTargetId);
            if (result != 0)
            {
                return result;
            }

            result = left.BoxKineticTargetId.CompareTo(right.BoxKineticTargetId);
            if (result != 0)
            {
                return result;
            }

            result = left.BoxKineticInstigatorEntityId.CompareTo(right.BoxKineticInstigatorEntityId);
            if (result != 0)
            {
                return result;
            }

            result = left.BoxKineticInstigatorTeamId.CompareTo(right.BoxKineticInstigatorTeamId);
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

            result = CompareLists(left.StateChanges, right.StateChanges, CompareStateChangeActions);
            if (result != 0)
            {
                return result;
            }

            result = CompareLists(left.BoardPresenceChanges, right.BoardPresenceChanges, CompareBoardPresenceChangeActions);
            if (result != 0)
            {
                return result;
            }

            result = CompareLists(left.TopologyChanges, right.TopologyChanges, CompareTopologyChangeActions);
            if (result != 0)
            {
                return result;
            }

            return CompareLists(left.DelayedAttacks, right.DelayedAttacks, CompareDelayedAttackActions);
        }

        private static int CompareMoveActions(MoveAction left, MoveAction right)
        {
            var result = left.EntityId.CompareTo(right.EntityId);
            if (result != 0)
            {
                return result;
            }

            result = CompareSurfaceCells(left.SourceCell, right.SourceCell);
            if (result != 0)
            {
                return result;
            }

            result = CompareSurfaceCells(left.DestinationCell, right.DestinationCell);
            if (result != 0)
            {
                return result;
            }

            return ((int)left.Facing).CompareTo((int)right.Facing);
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
            var result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            return ((int)left.Condition).CompareTo((int)right.Condition);
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

        private static int CompareBoardPresenceChangeActions(BoardPresenceChangeAction left, BoardPresenceChangeAction right)
        {
            var result = left.EntityId.CompareTo(right.EntityId);
            if (result != 0)
            {
                return result;
            }

            return ((int)left.BoardPresence).CompareTo((int)right.BoardPresence);
        }

        private static int CompareTopologyChangeActions(TopologyChangeAction left, TopologyChangeAction right)
        {
            var result = ((int)left.RotationKind).CompareTo((int)right.RotationKind);
            if (result != 0)
            {
                return result;
            }

            return ((int)left.UpdatedTopology.BottomFace).CompareTo((int)right.UpdatedTopology.BottomFace);
        }

        private static int CompareDelayedAttackActions(DelayedAttackAction left, DelayedAttackAction right)
        {
            var result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            return left.Damage.CompareTo(right.Damage);
        }

        private static int CompareEntityStates(EntityState left, EntityState right)
        {
            var result = left.entityId.CompareTo(right.entityId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.position.face).CompareTo((int)right.position.face);
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

            result = ((int)left.boardPresence).CompareTo((int)right.boardPresence);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.boxCapabilities).CompareTo((int)right.boxCapabilities);
            if (result != 0)
            {
                return result;
            }

            result = left.markedForDeath.CompareTo(right.markedForDeath);
            if (result != 0)
            {
                return result;
            }

            result = left.spawnTick.CompareTo(right.spawnTick);
            if (result != 0)
            {
                return result;
            }

            result = left.kineticInstigatorEntityId.CompareTo(right.kineticInstigatorEntityId);
            if (result != 0)
            {
                return result;
            }

            return left.kineticInstigatorTeamId.CompareTo(right.kineticInstigatorTeamId);
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

        private static int CompareSurfaceCells(SurfaceCell left, SurfaceCell right)
        {
            var result = ((int)left.face).CompareTo((int)right.face);
            if (result != 0)
            {
                return result;
            }

            result = left.x.CompareTo(right.x);
            if (result != 0)
            {
                return result;
            }

            return left.y.CompareTo(right.y);
        }
    }
}
