using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal readonly struct EnemyPatrolDecisionProposal
    {
        public EnemyPatrolDecisionProposal(
            bool hasDirection,
            Direction plannedDirection,
            Direction plannedFacing,
            int candidateMask,
            bool shouldInitializeState)
        {
            HasDirection = hasDirection;
            PlannedDirection = plannedDirection;
            PlannedFacing = plannedFacing;
            CandidateMask = candidateMask;
            ShouldInitializeState = shouldInitializeState;
        }

        public bool HasDirection { get; }

        public Direction PlannedDirection { get; }

        public Direction PlannedFacing { get; }

        public int CandidateMask { get; }

        public bool ShouldInitializeState { get; }
    }

    internal static class EnemyPatrolDecisionPlanner
    {
        public static bool TryBuildProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            PatrolStrategyKind patrolKind,
            in EnemyPatrolRuntimeState patrolState,
            in PatrolSettings patrolSettings,
            out EnemyPatrolDecisionProposal proposal)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            switch (patrolKind)
            {
                case PatrolStrategyKind.Forward:
                    proposal = BuildForwardProposal(snapshot, source, patrolSettings);
                    return true;

                case PatrolStrategyKind.RandomWalk:
                    proposal = BuildRandomWalkProposal(snapshot, source, tickIndex, patrolState, patrolSettings);
                    return true;

                default:
                    proposal = default;
                    return false;
            }
        }

        private static EnemyPatrolDecisionProposal BuildForwardProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings patrolSettings)
        {
            var plannedFacing = source.facing;
            var forwardDelta = EnemyMovementStrategyShared.ResolveDelta(source.facing);
            if (!forwardDelta.HasValue)
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: false,
                    plannedDirection: Direction.None,
                    plannedFacing,
                    candidateMask: 0,
                    shouldInitializeState: false);
            }

            if (EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, forwardDelta.Value))
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: true,
                    plannedDirection: source.facing,
                    plannedFacing,
                    candidateMask: GetCandidateMaskBit(source.facing),
                    shouldInitializeState: false);
            }

            if (patrolSettings.StopWhenForwardBlocked)
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: false,
                    plannedDirection: Direction.None,
                    plannedFacing,
                    candidateMask: 0,
                    shouldInitializeState: false);
            }

            var backwardDirection = ResolveOppositeDirection(source.facing);
            var backwardDelta = EnemyMovementStrategyShared.ResolveDelta(backwardDirection);
            if (backwardDirection == Direction.None ||
                !backwardDelta.HasValue ||
                !EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, backwardDelta.Value))
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: false,
                    plannedDirection: Direction.None,
                    plannedFacing,
                    candidateMask: 0,
                    shouldInitializeState: false);
            }

            return new EnemyPatrolDecisionProposal(
                hasDirection: true,
                plannedDirection: backwardDirection,
                plannedFacing,
                candidateMask: GetCandidateMaskBit(backwardDirection),
                shouldInitializeState: false);
        }

        private static EnemyPatrolDecisionProposal BuildRandomWalkProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            in EnemyPatrolRuntimeState patrolState,
            in PatrolSettings patrolSettings)
        {
            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex,
                patrolState,
                patrolSettings);
            return new EnemyPatrolDecisionProposal(
                plan.HasDirection,
                plan.PlannedDirection,
                plan.PlannedFacing,
                plan.CandidateMask,
                plan.ShouldInitializeState);
        }

        private static Direction ResolveOppositeDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }

        private static int GetCandidateMaskBit(Direction direction)
        {
            return direction switch
            {
                Direction.Up => 1 << 0,
                Direction.Right => 1 << 1,
                Direction.Down => 1 << 2,
                Direction.Left => 1 << 3,
                _ => 0,
            };
        }
    }
}
