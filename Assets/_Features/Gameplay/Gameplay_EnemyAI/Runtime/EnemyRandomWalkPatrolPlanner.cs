using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal readonly struct EnemyRandomWalkPatrolPlan
    {
        public EnemyRandomWalkPatrolPlan(
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

    internal static class EnemyRandomWalkPatrolPlanner
    {
        private static readonly Direction[] DirectionOrder =
        {
            Direction.Up,
            Direction.Right,
            Direction.Down,
            Direction.Left,
        };

        public static EnemyRandomWalkPatrolPlan BuildPlan(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            in EnemyPatrolRuntimeState patrolState,
            in PatrolSettings patrolSettings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            patrolSettings.ValidateRandomWalk(nameof(patrolSettings));

            var shouldInitializeState = !patrolState.IsInitialized;
            var effectiveHomeCell = patrolState.IsInitialized
                ? patrolState.homeCell
                : source.position;
            var effectiveSequence = patrolState.IsInitialized
                ? patrolState.sequence
                : 1;
            var currentDistanceToHome = GetPlanarDistance(source.position, effectiveHomeCell);
            var requireDistanceReduction = currentDistanceToHome > patrolSettings.LeashRadius;

            var traversableMask = 0;
            var eligibleMask = 0;
            var eligibleCandidateCount = 0;
            var oppositeDirection = ResolveOppositeDirection(patrolState.lastCommittedDirection);
            var hasBacktrackCandidate = false;

            for (var i = 0; i < DirectionOrder.Length; i++)
            {
                if (!EnemyMovementStrategyShared.TryEvaluateRandomWalkCandidate(
                        snapshot,
                        source,
                        DirectionOrder[i],
                        out var candidate))
                {
                    continue;
                }

                traversableMask |= candidate.CandidateMaskBit;
                var candidateDistanceToHome = GetPlanarDistance(candidate.DestinationCell, effectiveHomeCell);
                if (requireDistanceReduction)
                {
                    if (candidateDistanceToHome >= currentDistanceToHome)
                    {
                        continue;
                    }
                }
                else if (candidateDistanceToHome > patrolSettings.LeashRadius)
                {
                    continue;
                }

                eligibleMask |= candidate.CandidateMaskBit;
                eligibleCandidateCount++;
                if (candidate.Direction == oppositeDirection)
                {
                    hasBacktrackCandidate = true;
                }
            }

            if (eligibleCandidateCount == 0)
            {
                return new EnemyRandomWalkPatrolPlan(
                    hasDirection: false,
                    Direction.None,
                    source.facing,
                    candidateMask: traversableMask,
                    shouldInitializeState);
            }

            var finalCandidateMask = eligibleMask;
            if (patrolSettings.PreventImmediateBacktrack &&
                hasBacktrackCandidate &&
                eligibleCandidateCount > 1 &&
                oppositeDirection != Direction.None)
            {
                finalCandidateMask &= ~GetCandidateMaskBit(oppositeDirection);
            }

            var totalWeight = 0;
            for (var i = 0; i < DirectionOrder.Length; i++)
            {
                if ((finalCandidateMask & GetCandidateMaskBit(DirectionOrder[i])) == 0)
                {
                    continue;
                }

                totalWeight += ResolveCandidateWeight(source.facing, DirectionOrder[i], patrolSettings);
            }

            if (totalWeight <= 0)
            {
                return new EnemyRandomWalkPatrolPlan(
                    hasDirection: false,
                    Direction.None,
                    source.facing,
                    finalCandidateMask,
                    shouldInitializeState);
            }

            var selection = (int)(BuildDeterministicSeed(source, tickIndex, effectiveHomeCell, effectiveSequence) % (uint)totalWeight);
            for (var i = 0; i < DirectionOrder.Length; i++)
            {
                var direction = DirectionOrder[i];
                if ((finalCandidateMask & GetCandidateMaskBit(direction)) == 0)
                {
                    continue;
                }

                var candidateWeight = ResolveCandidateWeight(source.facing, direction, patrolSettings);
                if (candidateWeight <= 0)
                {
                    continue;
                }

                if (selection < candidateWeight)
                {
                    return new EnemyRandomWalkPatrolPlan(
                        hasDirection: true,
                        direction,
                        direction,
                        finalCandidateMask,
                        shouldInitializeState);
                }

                selection -= candidateWeight;
            }

            return new EnemyRandomWalkPatrolPlan(
                hasDirection: false,
                Direction.None,
                source.facing,
                finalCandidateMask,
                shouldInitializeState);
        }

        private static int ResolveCandidateWeight(
            Direction currentFacing,
            Direction candidateDirection,
            in PatrolSettings patrolSettings)
        {
            if (candidateDirection == currentFacing)
            {
                return patrolSettings.ForwardWeight;
            }

            if (candidateDirection == ResolveOppositeDirection(currentFacing))
            {
                return patrolSettings.BackwardWeight;
            }

            return patrolSettings.SideWeight;
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

        private static int GetPlanarDistance(SurfaceCell sourceCell, SurfaceCell targetCell)
        {
            var source = sourceCell.PlanarPosition;
            var target = targetCell.PlanarPosition;
            return Math.Abs(source.x - target.x) + Math.Abs(source.y - target.y);
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

        private static uint BuildDeterministicSeed(
            in EntityState source,
            int tickIndex,
            SurfaceCell homeCell,
            int effectiveSequence)
        {
            var seed = 2166136261u;
            seed = Mix(seed, source.entityId);
            seed = Mix(seed, tickIndex);
            seed = Mix(seed, (int)source.position.face);
            seed = Mix(seed, source.position.x);
            seed = Mix(seed, source.position.y);
            seed = Mix(seed, (int)homeCell.face);
            seed = Mix(seed, homeCell.x);
            seed = Mix(seed, homeCell.y);
            seed = Mix(seed, effectiveSequence);
            return seed;
        }

        private static uint Mix(uint seed, int value)
        {
            unchecked
            {
                var mixed = seed ^ (uint)value;
                mixed ^= mixed >> 16;
                mixed *= 0x7feb352du;
                mixed ^= mixed >> 15;
                mixed *= 0x846ca68bu;
                mixed ^= mixed >> 16;
                return mixed;
            }
        }
    }
}
