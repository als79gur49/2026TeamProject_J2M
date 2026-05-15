using System;
using System.Collections.Generic;
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
        private const int LeashReturnBaseStepCost = 10;
        private const int LeashReturnSameDistancePenalty = 5;
        private const int LeashReturnDistanceIncreasePenalty = 20;
        private const int LeashReturnLethalHazardPenalty = 1000;
        private const int LeashReturnImmediateBacktrackPenalty = 3;
        private const int LeashReturnDetourAllowance = 8;
        private const int LeashReturnMaxSearchDepth = 48;
        private const int LeashReturnMaxVisitedCells = 256;

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
            in PatrolSettings patrolSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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

            if (requireDistanceReduction)
            {
                return TryBuildLeashReturnPlan(
                    snapshot,
                    source,
                    effectiveHomeCell,
                    currentDistanceToHome,
                    patrolSettings.LeashRadius,
                    patrolState.lastCommittedDirection,
                    shouldInitializeState,
                    tileFeatureDefinitions,
                    out var leashReturnPlan)
                    ? leashReturnPlan
                    : new EnemyRandomWalkPatrolPlan(
                        hasDirection: false,
                        Direction.None,
                        source.facing,
                        candidateMask: 0,
                        shouldInitializeState);
            }

            var traversableMask = 0;
            var eligibleMask = 0;
            var eligibleCandidateCount = 0;
            var candidateCells = new SurfaceCell[DirectionOrder.Length];
            var hasCandidateCell = new bool[DirectionOrder.Length];
            var oppositeDirection = ResolveOppositeDirection(patrolState.lastCommittedDirection);
            var hasBacktrackCandidate = false;

            for (var i = 0; i < DirectionOrder.Length; i++)
            {
                if (!EnemyMovementStrategyShared.TryEvaluateRandomWalkCandidate(
                        snapshot,
                        source,
                        DirectionOrder[i],
                        tileFeatureDefinitions,
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
                candidateCells[i] = candidate.DestinationCell;
                hasCandidateCell[i] = true;
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

            var neutralCandidateMask = 0;
            for (var i = 0; i < DirectionOrder.Length; i++)
            {
                var direction = DirectionOrder[i];
                var maskBit = GetCandidateMaskBit(direction);
                if ((finalCandidateMask & maskBit) == 0 ||
                    !hasCandidateCell[i])
                {
                    continue;
                }

                if (TileFeatureHazardQueries.EvaluateTileApproachRisk(
                        snapshot,
                        tileFeatureDefinitions,
                        source,
                        candidateCells[i]) == TileApproachRisk.Neutral)
                {
                    neutralCandidateMask |= maskBit;
                }
            }

            var selectionCandidateMask = neutralCandidateMask != 0
                ? neutralCandidateMask
                : finalCandidateMask;

            var totalWeight = 0;
            for (var i = 0; i < DirectionOrder.Length; i++)
            {
                if ((selectionCandidateMask & GetCandidateMaskBit(DirectionOrder[i])) == 0)
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
                if ((selectionCandidateMask & GetCandidateMaskBit(direction)) == 0)
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

        private readonly struct LeashReturnNode
        {
            public LeashReturnNode(
                SurfaceCell cell,
                Direction firstDirection,
                Direction lastDirection,
                int totalCost,
                int pathLength,
                int distanceToHome,
                int firstDirectionOrder,
                int sequence)
            {
                Cell = cell;
                FirstDirection = firstDirection;
                LastDirection = lastDirection;
                TotalCost = totalCost;
                PathLength = pathLength;
                DistanceToHome = distanceToHome;
                FirstDirectionOrder = firstDirectionOrder;
                Sequence = sequence;
            }

            public SurfaceCell Cell { get; }

            public Direction FirstDirection { get; }

            public Direction LastDirection { get; }

            public int TotalCost { get; }

            public int PathLength { get; }

            public int DistanceToHome { get; }

            public int FirstDirectionOrder { get; }

            public int Sequence { get; }

            public LeashReturnPriority Priority => new(
                TotalCost,
                PathLength,
                DistanceToHome,
                FirstDirectionOrder,
                Sequence);
        }

        private readonly struct LeashReturnPriority : IComparable<LeashReturnPriority>
        {
            public LeashReturnPriority(
                int totalCost,
                int pathLength,
                int finalDistanceToHome,
                int firstDirectionOrder,
                int sequence)
            {
                TotalCost = totalCost;
                PathLength = pathLength;
                FinalDistanceToHome = finalDistanceToHome;
                FirstDirectionOrder = firstDirectionOrder;
                Sequence = sequence;
            }

            public int TotalCost { get; }

            public int PathLength { get; }

            public int FinalDistanceToHome { get; }

            public int FirstDirectionOrder { get; }

            public int Sequence { get; }

            public int CompareTo(LeashReturnPriority other)
            {
                var result = TotalCost.CompareTo(other.TotalCost);
                if (result != 0)
                {
                    return result;
                }

                result = PathLength.CompareTo(other.PathLength);
                if (result != 0)
                {
                    return result;
                }

                result = FinalDistanceToHome.CompareTo(other.FinalDistanceToHome);
                if (result != 0)
                {
                    return result;
                }

                result = FirstDirectionOrder.CompareTo(other.FirstDirectionOrder);
                if (result != 0)
                {
                    return result;
                }

                return Sequence.CompareTo(other.Sequence);
            }
        }

        private readonly struct LeashReturnSearchResult
        {
            public LeashReturnSearchResult(Direction firstDirection)
            {
                FirstDirection = firstDirection;
            }

            public Direction FirstDirection { get; }
        }

        private static bool TryBuildLeashReturnPlan(
            WorldSnapshot snapshot,
            in EntityState source,
            SurfaceCell homeCell,
            int currentDistanceToHome,
            int leashRadius,
            Direction lastCommittedDirection,
            bool shouldInitializeState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out EnemyRandomWalkPatrolPlan plan)
        {
            plan = new EnemyRandomWalkPatrolPlan(
                hasDirection: false,
                Direction.None,
                source.facing,
                candidateMask: 0,
                shouldInitializeState);

            if (source.position.face != homeCell.face ||
                currentDistanceToHome <= leashRadius ||
                !TrySearchLeashReturn(
                    snapshot,
                    source,
                    homeCell,
                    currentDistanceToHome,
                    leashRadius,
                    lastCommittedDirection,
                    tileFeatureDefinitions,
                    out var result))
            {
                return false;
            }

            plan = new EnemyRandomWalkPatrolPlan(
                hasDirection: true,
                result.FirstDirection,
                result.FirstDirection,
                GetCandidateMaskBit(result.FirstDirection),
                shouldInitializeState);
            return true;
        }

        private static bool TrySearchLeashReturn(
            WorldSnapshot snapshot,
            in EntityState source,
            SurfaceCell homeCell,
            int currentDistanceToHome,
            int leashRadius,
            Direction lastCommittedDirection,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out LeashReturnSearchResult result)
        {
            result = default;

            var directReturnDistance = currentDistanceToHome - leashRadius;
            var maxSearchDepth = Math.Min(
                LeashReturnMaxSearchDepth,
                directReturnDistance + LeashReturnDetourAllowance);
            if (maxSearchDepth <= 0)
            {
                return false;
            }

            var nodes = new List<LeashReturnNode>(LeashReturnMaxVisitedCells);
            var openNodeIndices = new List<int>(LeashReturnMaxVisitedCells);
            var bestNodeIndexByCell = new Dictionary<SurfaceCell, int>(LeashReturnMaxVisitedCells);
            var closedCells = new HashSet<SurfaceCell>();
            var nextSequence = 0;

            var startNode = new LeashReturnNode(
                source.position,
                Direction.None,
                lastCommittedDirection,
                totalCost: 0,
                pathLength: 0,
                distanceToHome: currentDistanceToHome,
                firstDirectionOrder: DirectionOrder.Length,
                sequence: nextSequence++);
            nodes.Add(startNode);
            openNodeIndices.Add(0);
            bestNodeIndexByCell[source.position] = 0;

            while (openNodeIndices.Count > 0 &&
                   closedCells.Count < LeashReturnMaxVisitedCells)
            {
                var openListIndex = SelectBestOpenListIndex(openNodeIndices, nodes);
                var nodeIndex = openNodeIndices[openListIndex];
                openNodeIndices.RemoveAt(openListIndex);
                var node = nodes[nodeIndex];

                if (closedCells.Contains(node.Cell))
                {
                    continue;
                }

                closedCells.Add(node.Cell);
                if (node.PathLength > 0 &&
                    node.DistanceToHome <= leashRadius)
                {
                    result = new LeashReturnSearchResult(node.FirstDirection);
                    return true;
                }

                if (node.PathLength >= maxSearchDepth)
                {
                    continue;
                }

                for (var directionOrder = 0; directionOrder < DirectionOrder.Length; directionOrder++)
                {
                    var direction = DirectionOrder[directionOrder];
                    var nodeSource = source;
                    nodeSource.position = node.Cell;

                    if (!EnemyMovementStrategyShared.TryEvaluateRandomWalkCandidate(
                            snapshot,
                            nodeSource,
                            direction,
                            tileFeatureDefinitions,
                            out var candidate) ||
                        closedCells.Contains(candidate.DestinationCell))
                    {
                        continue;
                    }

                    var nextDistanceToHome = GetPlanarDistance(candidate.DestinationCell, homeCell);
                    var firstDirection = node.PathLength == 0
                        ? direction
                        : node.FirstDirection;
                    var firstDirectionOrder = node.PathLength == 0
                        ? directionOrder
                        : node.FirstDirectionOrder;
                    var stepCost = CalculateLeashReturnStepCost(
                        snapshot,
                        tileFeatureDefinitions,
                        nodeSource,
                        candidate.DestinationCell,
                        node.DistanceToHome,
                        nextDistanceToHome,
                        direction,
                        node.LastDirection);
                    var candidateNode = new LeashReturnNode(
                        candidate.DestinationCell,
                        firstDirection,
                        direction,
                        node.TotalCost + stepCost,
                        node.PathLength + 1,
                        nextDistanceToHome,
                        firstDirectionOrder,
                        nextSequence++);

                    if (bestNodeIndexByCell.TryGetValue(candidateNode.Cell, out var existingNodeIndex) &&
                        nodes[existingNodeIndex].Priority.CompareTo(candidateNode.Priority) <= 0)
                    {
                        continue;
                    }

                    nodes.Add(candidateNode);
                    var candidateNodeIndex = nodes.Count - 1;
                    bestNodeIndexByCell[candidateNode.Cell] = candidateNodeIndex;
                    openNodeIndices.Add(candidateNodeIndex);
                }
            }

            return false;
        }

        private static int SelectBestOpenListIndex(
            List<int> openNodeIndices,
            List<LeashReturnNode> nodes)
        {
            var bestOpenListIndex = 0;
            var bestPriority = nodes[openNodeIndices[0]].Priority;
            for (var i = 1; i < openNodeIndices.Count; i++)
            {
                var priority = nodes[openNodeIndices[i]].Priority;
                if (priority.CompareTo(bestPriority) >= 0)
                {
                    continue;
                }

                bestOpenListIndex = i;
                bestPriority = priority;
            }

            return bestOpenListIndex;
        }

        private static int CalculateLeashReturnStepCost(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            in EntityState source,
            SurfaceCell candidateCell,
            int currentDistanceToHome,
            int nextDistanceToHome,
            Direction direction,
            Direction lastDirection)
        {
            var cost = LeashReturnBaseStepCost;
            if (nextDistanceToHome == currentDistanceToHome)
            {
                cost += LeashReturnSameDistancePenalty;
            }
            else if (nextDistanceToHome > currentDistanceToHome)
            {
                cost += LeashReturnDistanceIncreasePenalty;
            }

            if (lastDirection != Direction.None &&
                direction == ResolveOppositeDirection(lastDirection))
            {
                cost += LeashReturnImmediateBacktrackPenalty;
            }

            if (TileFeatureHazardQueries.EvaluateTileApproachRisk(
                    snapshot,
                    tileFeatureDefinitions,
                    source,
                    candidateCell) == TileApproachRisk.LethalOnEnter)
            {
                cost += LeashReturnLethalHazardPenalty;
            }

            return cost;
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
