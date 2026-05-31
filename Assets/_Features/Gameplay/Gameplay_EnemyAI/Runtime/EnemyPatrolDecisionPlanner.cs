using System;
using System.Collections.Generic;
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
            bool shouldInitializeState,
            bool shouldCaptureOriginBeforeLeavingPatrol)
        {
            HasDirection = hasDirection;
            PlannedDirection = plannedDirection;
            PlannedFacing = plannedFacing;
            CandidateMask = candidateMask;
            ShouldInitializeState = shouldInitializeState;
            ShouldCaptureOriginBeforeLeavingPatrol = shouldCaptureOriginBeforeLeavingPatrol;
        }

        public bool HasDirection { get; }

        public Direction PlannedDirection { get; }

        public Direction PlannedFacing { get; }

        public int CandidateMask { get; }

        public bool ShouldInitializeState { get; }

        public bool ShouldCaptureOriginBeforeLeavingPatrol { get; }
    }

    internal static class EnemyPatrolDecisionPlanner
    {
        public static bool TryResolveBlockedReactionFacingOverride(
            PatrolStrategyKind patrolKind,
            in PendingEnemyBlockedReaction reaction,
            out Direction facing)
        {
            facing = Direction.None;
            if (patrolKind != PatrolStrategyKind.Forward ||
                !DirectionUtility.IsCardinal(reaction.BlockedDirection))
            {
                return false;
            }

            facing = ResolveOppositeDirection(reaction.BlockedDirection);
            return facing != Direction.None;
        }

        public static bool TryBuildProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            PatrolStrategyKind patrolKind,
            in EnemyPatrolRuntimeState patrolState,
            in PatrolSettings patrolSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out EnemyPatrolDecisionProposal proposal)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            switch (patrolKind)
            {
                case PatrolStrategyKind.Forward:
                    proposal = BuildForwardProposal(snapshot, source, patrolSettings, tileFeatureDefinitions);
                    return true;

                case PatrolStrategyKind.RandomWalk:
                    proposal = BuildRandomWalkProposal(
                        snapshot,
                        source,
                        tickIndex,
                        patrolState,
                        patrolSettings,
                        tileFeatureDefinitions);
                    return true;

                default:
                    proposal = default;
                    return false;
            }
        }

        private static EnemyPatrolDecisionProposal BuildForwardProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings patrolSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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
                    shouldInitializeState: false,
                    shouldCaptureOriginBeforeLeavingPatrol: false);
            }

            if (EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, forwardDelta.Value, tileFeatureDefinitions))
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: true,
                    plannedDirection: source.facing,
                    plannedFacing,
                    candidateMask: GetCandidateMaskBit(source.facing),
                    shouldInitializeState: false,
                    shouldCaptureOriginBeforeLeavingPatrol: false);
            }

            if (patrolSettings.StopWhenForwardBlocked)
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: false,
                    plannedDirection: Direction.None,
                    plannedFacing,
                    candidateMask: 0,
                    shouldInitializeState: false,
                    shouldCaptureOriginBeforeLeavingPatrol: false);
            }

            var backwardDirection = ResolveOppositeDirection(source.facing);
            var backwardDelta = EnemyMovementStrategyShared.ResolveDelta(backwardDirection);
            if (backwardDirection == Direction.None ||
                !backwardDelta.HasValue ||
                !EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, backwardDelta.Value, tileFeatureDefinitions))
            {
                return new EnemyPatrolDecisionProposal(
                    hasDirection: false,
                    plannedDirection: Direction.None,
                    plannedFacing,
                    candidateMask: 0,
                    shouldInitializeState: false,
                    shouldCaptureOriginBeforeLeavingPatrol: false);
            }

            return new EnemyPatrolDecisionProposal(
                hasDirection: true,
                plannedDirection: backwardDirection,
                plannedFacing,
                candidateMask: GetCandidateMaskBit(backwardDirection),
                shouldInitializeState: false,
                shouldCaptureOriginBeforeLeavingPatrol: false);
        }

        private static EnemyPatrolDecisionProposal BuildRandomWalkProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            in EnemyPatrolRuntimeState patrolState,
            in PatrolSettings patrolSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex,
                patrolState,
                patrolSettings,
                tileFeatureDefinitions);
            return new EnemyPatrolDecisionProposal(
                plan.HasDirection,
                plan.PlannedDirection,
                plan.PlannedFacing,
                plan.CandidateMask,
                plan.ShouldInitializeState,
                shouldCaptureOriginBeforeLeavingPatrol: true);
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
