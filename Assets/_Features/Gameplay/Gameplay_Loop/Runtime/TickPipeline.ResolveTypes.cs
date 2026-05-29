using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal enum ContestKind
    {
        Plan = 0,
        Space = 1,
        Impact = 2,
        Damage = 3,
        Destroy = 4,
    }

    internal readonly struct EdgeReservation
    {
        public EdgeReservation(int entityId, SurfaceCell from, SurfaceCell to, int groupId)
        {
            EntityId = entityId;
            From = from;
            To = to;
            GroupId = groupId;
        }

        public int EntityId { get; }

        public SurfaceCell From { get; }

        public SurfaceCell To { get; }

        public int GroupId { get; }
    }

    internal readonly struct ExclusiveGroupReservation
    {
        public ExclusiveGroupReservation(int groupId, ActionGroupKind groupKind, bool hasTopologyChange)
        {
            GroupId = groupId;
            GroupKind = groupKind;
            HasTopologyChange = hasTopologyChange;
        }

        public int GroupId { get; }

        public ActionGroupKind GroupKind { get; }

        public bool HasTopologyChange { get; }
    }

    internal readonly struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
    {
        public UndirectedEdgeKey(SurfaceCell first, SurfaceCell second)
        {
            First = first;
            Second = second;
        }

        public SurfaceCell First { get; }

        public SurfaceCell Second { get; }

        public static UndirectedEdgeKey Create(SurfaceCell from, SurfaceCell to)
        {
            return CompareCells(from, to) <= 0
                ? new UndirectedEdgeKey(from, to)
                : new UndirectedEdgeKey(to, from);
        }

        public bool Equals(UndirectedEdgeKey other)
        {
            return First == other.First && Second == other.Second;
        }

        public override bool Equals(object obj)
        {
            return obj is UndirectedEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (int)First.face;
                hash = (hash * 31) + First.x;
                hash = (hash * 31) + First.y;
                hash = (hash * 31) + (int)Second.face;
                hash = (hash * 31) + Second.x;
                hash = (hash * 31) + Second.y;
                return hash;
            }
        }

        private static int CompareCells(SurfaceCell left, SurfaceCell right)
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

    internal enum ReservationMode
    {
        Conservative = 0,
        UnitSharedMove = 1,
    }

    internal readonly struct Contest
    {
        public Contest(
            int contestId,
            ContestKind kind,
            int actionPlanId,
            int sourceId,
            int priority,
            int affectedEntityId,
            SurfaceCell affectedCell,
            bool hasAffectedCell,
            int localActionIndex,
            DestroyCondition destroyCondition = DestroyCondition.WhenHpDepleted)
        {
            ContestId = contestId;
            Kind = kind;
            ActionPlanId = actionPlanId;
            SourceId = sourceId;
            Priority = priority;
            AffectedEntityId = affectedEntityId;
            AffectedCell = affectedCell;
            HasAffectedCell = hasAffectedCell;
            LocalActionIndex = localActionIndex;
            DestroyCondition = destroyCondition;
        }

        public int ContestId { get; }

        public ContestKind Kind { get; }

        public int ActionPlanId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int AffectedEntityId { get; }

        public SurfaceCell AffectedCell { get; }

        public bool HasAffectedCell { get; }

        public int LocalActionIndex { get; }

        public DestroyCondition DestroyCondition { get; }
    }

    internal readonly struct ResolutionRecord
    {
        public ResolutionRecord(
            int contestId,
            ContestKind kind,
            bool accepted,
            int sourceId,
            int priority,
            int actionPlanId,
            int affectedEntityId,
            int localActionIndex)
        {
            ContestId = contestId;
            Kind = kind;
            Accepted = accepted;
            SourceId = sourceId;
            Priority = priority;
            ActionPlanId = actionPlanId;
            AffectedEntityId = affectedEntityId;
            LocalActionIndex = localActionIndex;
        }

        public int ContestId { get; }

        public ContestKind Kind { get; }

        public bool Accepted { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int ActionPlanId { get; }

        public int AffectedEntityId { get; }

        public int LocalActionIndex { get; }
    }

    internal readonly struct DestroyResolutionRecord
    {
        private readonly int _actionPlanId;
        private readonly int _intentId;

        public DestroyResolutionRecord(
            int actionPlanId,
            int intentId,
            int sourceId,
            int targetId,
            DestroyCondition condition,
            int finalHp,
            bool accepted,
            int localActionIndex)
        {
            _actionPlanId = actionPlanId;
            _intentId = intentId;
            SourceId = sourceId;
            TargetId = targetId;
            Condition = condition;
            FinalHp = finalHp;
            Accepted = accepted;
            LocalActionIndex = localActionIndex;
        }

        public int ActionPlanId => _actionPlanId;

        [Obsolete("Legacy alias for ActionPlanId. Prefer ActionPlanId for correlation and semantic fields such as SourceId, TargetId, Condition, FinalHp, and Accepted.")]
        public int GroupId => _actionPlanId;

        [Obsolete("IR metadata only. Prefer ActionPlanId for plan correlation and semantic fields such as SourceId, TargetId, Condition, FinalHp, and Accepted.")]
        public int IntentId => _intentId;

        public int SourceId { get; }

        public int TargetId { get; }

        public DestroyCondition Condition { get; }

        public int FinalHp { get; }

        public bool Accepted { get; }

        public int LocalActionIndex { get; }
    }

}
