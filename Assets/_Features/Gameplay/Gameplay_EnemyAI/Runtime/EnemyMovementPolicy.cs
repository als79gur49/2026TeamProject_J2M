using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public enum PatrolBlockedMovementResponse
    {
        Stop = 0,
        TryStepBackward = 1,
    }

    public enum WallFollowTurnPreference
    {
        Left = 0,
        Right = 1,
    }

    public enum ChaseAxisPriorityMode
    {
        GreatestDistanceThenFacingTieBreak = 0,
        HorizontalFirst = 1,
        VerticalFirst = 2,
    }

    [Serializable]
    public struct PatrolSettings
    {
        [SerializeField] private PatrolBlockedMovementResponse blockedMovementResponse;
        [SerializeField] private WallFollowTurnPreference turnPreference;
        [SerializeField] private bool followWalls;
        [SerializeField] private bool followBoxes;
        [SerializeField] private int leashRadius;
        [SerializeField] private int forwardWeight;
        [SerializeField] private int sideWeight;
        [SerializeField] private int backwardWeight;
        [SerializeField] private bool preventImmediateBacktrack;

        public PatrolSettings(
            PatrolBlockedMovementResponse blockedMovementResponse,
            WallFollowTurnPreference turnPreference = WallFollowTurnPreference.Right,
            bool followWalls = true,
            bool followBoxes = true,
            int leashRadius = 0,
            int forwardWeight = 0,
            int sideWeight = 0,
            int backwardWeight = 0,
            bool preventImmediateBacktrack = false)
        {
            this.blockedMovementResponse = blockedMovementResponse;
            this.turnPreference = turnPreference;
            this.followWalls = followWalls;
            this.followBoxes = followBoxes;
            this.leashRadius = leashRadius;
            this.forwardWeight = forwardWeight;
            this.sideWeight = sideWeight;
            this.backwardWeight = backwardWeight;
            this.preventImmediateBacktrack = preventImmediateBacktrack;
        }

        public PatrolBlockedMovementResponse BlockedMovementResponse => blockedMovementResponse;

        public WallFollowTurnPreference TurnPreference => turnPreference;

        public bool FollowWalls => followWalls;

        public bool FollowBoxes => followBoxes;

        public int LeashRadius => leashRadius;

        public int ForwardWeight => forwardWeight;

        public int SideWeight => sideWeight;

        public int BackwardWeight => backwardWeight;

        public bool PreventImmediateBacktrack => preventImmediateBacktrack;

        public bool StopWhenForwardBlocked => blockedMovementResponse == PatrolBlockedMovementResponse.Stop;

        public void ValidateRandomWalk(string paramName)
        {
            if (leashRadius < 0)
            {
                throw new ArgumentException("Random walk patrol settings require a non-negative leash radius.", paramName);
            }

            if (forwardWeight < 0 || sideWeight < 0 || backwardWeight < 0)
            {
                throw new ArgumentException("Random walk patrol weights must be zero or greater.", paramName);
            }

            if (forwardWeight + sideWeight + backwardWeight <= 0)
            {
                throw new ArgumentException("Random walk patrol settings require at least one positive movement weight.", paramName);
            }
        }

        public static PatrolSettings CreateDefault()
        {
            return new PatrolSettings(PatrolBlockedMovementResponse.Stop);
        }

        public static PatrolSettings CreateDefaultRandomWalk()
        {
            return new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 2,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
        }
    }

    [Serializable]
    public struct ChaseSettings
    {
        [SerializeField] private ChaseAxisPriorityMode axisPriority;
        [SerializeField] private bool trySecondaryAxisWhenBlocked;
        [SerializeField] private int desiredChaseDistance;

        public ChaseSettings(
            ChaseAxisPriorityMode axisPriority,
            bool trySecondaryAxisWhenBlocked,
            int desiredChaseDistance = 0)
        {
            this.axisPriority = axisPriority;
            this.trySecondaryAxisWhenBlocked = trySecondaryAxisWhenBlocked;
            this.desiredChaseDistance = desiredChaseDistance;
        }

        public ChaseAxisPriorityMode AxisPriority => axisPriority;

        public bool TrySecondaryAxisWhenBlocked => trySecondaryAxisWhenBlocked;

        public int DesiredChaseDistance => desiredChaseDistance;

        public void Validate(string paramName)
        {
            if (desiredChaseDistance < 0)
            {
                throw new ArgumentException("Enemy chase settings require a non-negative desired chase distance.", paramName);
            }
        }

        public static ChaseSettings CreateDefault()
        {
            return new ChaseSettings(
                ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak,
                trySecondaryAxisWhenBlocked: true,
                desiredChaseDistance: 0);
        }
    }

    public interface IPatrolStrategy
    {
        bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            in PatrolSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent);
    }

    public interface IPatrolFacingStrategy
    {
        bool TryResolveFacing(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            out Direction facing);
    }

    public interface IChaseStrategy
    {
        bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in ChaseSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent);
    }

    public sealed class ForwardPatrolStrategy : IPatrolStrategy
    {
        public static readonly ForwardPatrolStrategy Instance = new();

        public bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            in PatrolSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            var forwardDelta = EnemyMovementStrategyShared.ResolveDelta(source.facing);
            if (!forwardDelta.HasValue)
            {
                return false;
            }

            if (EnemyMovementStrategyShared.TryBuildMoveIntent(snapshot, source, commonSettings, forwardDelta.Value, out intent))
            {
                return true;
            }

            if (settings.StopWhenForwardBlocked)
            {
                return false;
            }

            return EnemyMovementStrategyShared.TryBuildMoveIntent(
                snapshot,
                source,
                commonSettings,
                -forwardDelta.Value,
                out intent);
        }
    }

    public sealed class WallFollowPatrolStrategy : IPatrolStrategy, IPatrolFacingStrategy
    {
        public static readonly WallFollowPatrolStrategy Instance = new();

        public bool TryResolveFacing(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            out Direction facing)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            facing = source.facing;

            if (!EnemyMovementStrategyShared.TryChooseWallFollowFacing(snapshot, source, settings, out var nextFacing))
            {
                return false;
            }

            if (nextFacing == source.facing)
            {
                return false;
            }

            facing = nextFacing;
            return true;
        }

        public bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            in PatrolSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            if (!EnemyMovementStrategyShared.TryChooseWallFollowDirection(snapshot, source, settings, out var direction) ||
                !EnemyMovementStrategyShared.TryResolveDelta(direction, out var delta))
            {
                return false;
            }

            return EnemyMovementStrategyShared.TryBuildMoveIntent(
                snapshot,
                source,
                commonSettings,
                delta,
                out intent);
        }
    }

    public sealed class StationaryPatrolStrategy : IPatrolStrategy
    {
        public static readonly StationaryPatrolStrategy Instance = new();

        public bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            in PatrolSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;
            return false;
        }
    }

    public sealed class RandomWalkPatrolStrategy : IPatrolStrategy
    {
        public static readonly RandomWalkPatrolStrategy Instance = new();

        public bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            in PatrolSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent)
        {
            throw new InvalidOperationException(
                "RandomWalk patrol dispatch is owned by EnemyLogic in the Phase 1 bounded rollout.");
        }
    }

    public sealed class AxisPriorityChaseStrategy : IChaseStrategy
    {
        public static readonly AxisPriorityChaseStrategy Instance = new();

        private static readonly Direction[] LocalAvoidanceDirectionOrder =
        {
            Direction.Up,
            Direction.Right,
            Direction.Down,
            Direction.Left,
        };

        private enum ChaseCandidateAxis
        {
            Horizontal = 0,
            Vertical = 1,
        }

        private readonly struct ChaseStepCandidate
        {
            public ChaseStepCandidate(Vector2Int delta, ChaseCandidateAxis axis)
            {
                Delta = delta;
                Axis = axis;
            }

            public Vector2Int Delta { get; }

            public ChaseCandidateAxis Axis { get; }
        }

        public bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in ChaseSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            if (!TryChooseChaseStep(snapshot, source, target, settings, tileFeatureDefinitions, out var delta))
            {
                return false;
            }

            return EnemyMovementStrategyShared.TryBuildMoveIntent(
                snapshot,
                source,
                commonSettings,
                delta,
                out intent);
        }

        private static bool TryChooseChaseStep(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in ChaseSettings settings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out Vector2Int delta)
        {
            delta = Vector2Int.zero;

            if (source.position.face != target.position.face)
            {
                return false;
            }

            var planarDelta = target.position - source.position;
            settings.Validate(nameof(settings));

            if (Math.Abs(planarDelta.x) + Math.Abs(planarDelta.y) <= settings.DesiredChaseDistance)
            {
                return false;
            }

            var horizontalStep = planarDelta.x == 0
                ? (Vector2Int?)null
                : new Vector2Int(Math.Sign(planarDelta.x), 0);
            var verticalStep = planarDelta.y == 0
                ? (Vector2Int?)null
                : new Vector2Int(0, Math.Sign(planarDelta.y));

            var tryHorizontalFirst = ShouldTryHorizontalFirst(planarDelta, source.facing, settings.AxisPriority);

            return TrySelectCandidate(
                snapshot,
                source,
                horizontalStep,
                verticalStep,
                tryHorizontalFirst,
                settings.TrySecondaryAxisWhenBlocked,
                target.position,
                tileFeatureDefinitions,
                out delta);
        }

        private static bool TrySelectCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int? horizontalStep,
            Vector2Int? verticalStep,
            bool tryHorizontalFirst,
            bool includeSecondaryAxis,
            SurfaceCell targetCell,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out Vector2Int delta)
        {
            delta = Vector2Int.zero;

            var primary = tryHorizontalFirst ? horizontalStep : verticalStep;
            var secondary = tryHorizontalFirst ? verticalStep : horizontalStep;
            var primaryAxis = tryHorizontalFirst ? ChaseCandidateAxis.Horizontal : ChaseCandidateAxis.Vertical;
            var secondaryAxis = tryHorizontalFirst ? ChaseCandidateAxis.Vertical : ChaseCandidateAxis.Horizontal;
            var legalCandidates = new List<ChaseStepCandidate>(2);
            AddLegalCandidate(snapshot, source, primary, primaryAxis, legalCandidates);
            if (includeSecondaryAxis)
            {
                AddLegalCandidate(snapshot, source, secondary, secondaryAxis, legalCandidates);
            }

            if (legalCandidates.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < legalCandidates.Count; i++)
            {
                var candidate = legalCandidates[i];
                if (EvaluateCandidateRisk(snapshot, tileFeatureDefinitions, source, candidate.Delta) == TileApproachRisk.Neutral)
                {
                    delta = candidate.Delta;
                    return true;
                }
            }

            for (var i = 0; i < legalCandidates.Count; i++)
            {
                var candidate = legalCandidates[i];
                if (EvaluateCandidateRisk(snapshot, tileFeatureDefinitions, source, candidate.Delta) != TileApproachRisk.LethalOnEnter)
                {
                    continue;
                }

                return TrySelectLocalHazardAvoidanceStep(
                    snapshot,
                    source,
                    targetCell,
                    candidate.Axis,
                    tileFeatureDefinitions,
                    out delta);
            }

            return false;
        }

        private static void AddLegalCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int? candidate,
            ChaseCandidateAxis axis,
            List<ChaseStepCandidate> legalCandidates)
        {
            if (EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, candidate, out var delta))
            {
                legalCandidates.Add(new ChaseStepCandidate(delta, axis));
            }
        }

        private static bool TrySelectLocalHazardAvoidanceStep(
            WorldSnapshot snapshot,
            in EntityState source,
            SurfaceCell targetCell,
            ChaseCandidateAxis blockedAxis,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out Vector2Int delta)
        {
            delta = Vector2Int.zero;
            var hasSelection = false;
            var selectedDistance = int.MaxValue;
            var selectedPriority = int.MaxValue;

            for (var i = 0; i < LocalAvoidanceDirectionOrder.Length; i++)
            {
                var direction = LocalAvoidanceDirectionOrder[i];
                if (!IsPerpendicularAvoidanceDirection(blockedAxis, direction) ||
                    !EnemyMovementStrategyShared.TryResolveDelta(direction, out var candidateDelta) ||
                    !EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, candidateDelta) ||
                    EvaluateCandidateRisk(snapshot, tileFeatureDefinitions, source, candidateDelta) != TileApproachRisk.Neutral)
                {
                    continue;
                }

                var candidateCell = ResolveCandidateCell(source, candidateDelta);
                var candidateDistance = GetPlanarDistance(candidateCell, targetCell);
                if (hasSelection &&
                    (candidateDistance > selectedDistance ||
                     candidateDistance == selectedDistance && i >= selectedPriority))
                {
                    continue;
                }

                hasSelection = true;
                selectedDistance = candidateDistance;
                selectedPriority = i;
                delta = candidateDelta;
            }

            return hasSelection;
        }

        private static bool IsPerpendicularAvoidanceDirection(
            ChaseCandidateAxis blockedAxis,
            Direction direction)
        {
            return blockedAxis switch
            {
                ChaseCandidateAxis.Horizontal => direction == Direction.Up || direction == Direction.Down,
                ChaseCandidateAxis.Vertical => direction == Direction.Right || direction == Direction.Left,
                _ => false,
            };
        }

        private static TileApproachRisk EvaluateCandidateRisk(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            in EntityState source,
            Vector2Int delta)
        {
            return TileFeatureHazardQueries.EvaluateTileApproachRisk(
                snapshot,
                tileFeatureDefinitions,
                source,
                ResolveCandidateCell(source, delta));
        }

        private static SurfaceCell ResolveCandidateCell(in EntityState source, Vector2Int delta)
        {
            return new SurfaceCell(
                source.position.face,
                source.position.x + delta.x,
                source.position.y + delta.y);
        }

        private static int GetPlanarDistance(SurfaceCell sourceCell, SurfaceCell targetCell)
        {
            var source = sourceCell.PlanarPosition;
            var target = targetCell.PlanarPosition;
            return Math.Abs(source.x - target.x) + Math.Abs(source.y - target.y);
        }

        private static bool ShouldTryHorizontalFirst(
            Vector2Int planarDelta,
            Direction facing,
            ChaseAxisPriorityMode axisPriority)
        {
            switch (axisPriority)
            {
                case ChaseAxisPriorityMode.HorizontalFirst:
                    return true;

                case ChaseAxisPriorityMode.VerticalFirst:
                    return false;

                case ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak:
                default:
                    var absX = Math.Abs(planarDelta.x);
                    var absY = Math.Abs(planarDelta.y);

                    if (absX != absY)
                    {
                        return absX > absY;
                    }

                    return facing == Direction.Left ||
                           facing == Direction.Right ||
                           planarDelta.x != 0;
            }
        }
    }

    internal static class EnemyMovementStrategyShared
    {
        internal readonly struct RandomWalkPatrolCandidate
        {
            public RandomWalkPatrolCandidate(Direction direction, SurfaceCell destinationCell)
            {
                Direction = direction;
                DestinationCell = destinationCell;
            }

            public Direction Direction { get; }

            public SurfaceCell DestinationCell { get; }

            public int CandidateMaskBit => Direction switch
            {
                Direction.Up => 1 << 0,
                Direction.Right => 1 << 1,
                Direction.Down => 1 << 2,
                Direction.Left => 1 << 3,
                _ => 0,
            };
        }

        private enum RelativeDirection
        {
            Forward = 0,
            Right = 1,
            Left = 2,
            Back = 3,
        }

        private enum WallFollowMovementChoice
        {
            Forward = 0,
            PreferredTurn = 1,
            OppositeTurn = 2,
        }

        private enum WallFollowAnchorFilter
        {
            PreferredOnly = 0,
            WeakOnly = 1,
        }

        private enum WallFollowAnchorKind
        {
            None = 0,
            BoardEdge = 1,
            Wall = 2,
            Box = 3,
        }

        public static bool TryBuildMoveIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            Vector2Int delta,
            out RawMovementIntent intent)
        {
            intent = default;

            if (!CanTraverseStep(snapshot, source, delta))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                commonSettings.MovementPriority,
                source.position.PlanarPosition + delta);
            return true;
        }

        public static bool TryBuildChargeMoveIntentIgnoringUnits(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings,
            Vector2Int delta,
            out RawMovementIntent intent)
        {
            intent = default;

            if (!CanTraverseChargeStepIgnoringUnits(snapshot, source, delta))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                commonSettings.MovementPriority,
                source.position.PlanarPosition + delta);
            return true;
        }

        public static bool CanTraverseStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int? candidate,
            out Vector2Int delta)
        {
            delta = Vector2Int.zero;

            if (!candidate.HasValue || !CanTraverseStep(snapshot, source, candidate.Value))
            {
                return false;
            }

            delta = candidate.Value;
            return true;
        }

        public static bool CanTraverseStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta)
        {
            if (!TryResolveStep(snapshot, source.position, delta, out var destinationCell, out var rotationKind, out var updatedTopology))
            {
                return false;
            }

            var evaluationTopology = rotationKind == CubeRotationKind.None ? snapshot.Topology : updatedTopology;
            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    source.position,
                    destinationCell,
                    evaluationTopology,
                    rotationKind == CubeRotationKind.None
                        ? TransitionRequirement.None
                        : TransitionRequirement.TopologyUpdate(rotationKind, updatedTopology)));
            if (legality.Verdict != LegalityVerdict.Allowed ||
                legality.TransitionRequirement.Kind != TransitionRequirementKind.None)
            {
                return false;
            }

            return true;
        }

        public static bool CanTraverseChargeStepIgnoringUnits(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta)
        {
            if (!TryResolveStep(snapshot, source.position, delta, out var destinationCell, out var rotationKind, out _))
            {
                return false;
            }

            if (rotationKind != CubeRotationKind.None)
            {
                return false;
            }

            return RuntimeTraversalLegalityPolicy.EvaluateChargeSolidOnlyStopCell(snapshot, destinationCell).Verdict ==
                   LegalityVerdict.Allowed;
        }

        public static Vector2Int? ResolveDelta(Direction direction)
        {
            return TryResolveDelta(direction, out var delta)
                ? delta
                : (Vector2Int?)null;
        }

        public static bool TryResolveDelta(Direction direction, out Vector2Int delta)
        {
            switch (direction)
            {
                case Direction.Up:
                    delta = Vector2Int.up;
                    return true;

                case Direction.Right:
                    delta = Vector2Int.right;
                    return true;

                case Direction.Down:
                    delta = Vector2Int.down;
                    return true;

                case Direction.Left:
                    delta = Vector2Int.left;
                    return true;

                default:
                    delta = Vector2Int.zero;
                    return false;
            }
        }

        public static bool TryResolveDirection(Vector2Int delta, out Direction direction)
        {
            if (delta == Vector2Int.up)
            {
                direction = Direction.Up;
                return true;
            }

            if (delta == Vector2Int.right)
            {
                direction = Direction.Right;
                return true;
            }

            if (delta == Vector2Int.down)
            {
                direction = Direction.Down;
                return true;
            }

            if (delta == Vector2Int.left)
            {
                direction = Direction.Left;
                return true;
            }

            direction = Direction.None;
            return false;
        }

        internal static bool TryEvaluateRandomWalkCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            Direction direction,
            out RandomWalkPatrolCandidate candidate)
        {
            candidate = default;

            if (!TryResolveDelta(direction, out var delta) ||
                !CanTraverseStep(snapshot, source, delta) ||
                !TryResolveAdjacentCellWithoutTopologyChange(snapshot, source, delta, out var destinationCell))
            {
                return false;
            }

            candidate = new RandomWalkPatrolCandidate(direction, destinationCell);
            return true;
        }

        internal static bool TryResolveStep(
            WorldSnapshot snapshot,
            SurfaceCell sourceCell,
            Vector2Int delta,
            out SurfaceCell destinationCell,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            destinationCell = default;
            rotationKind = CubeRotationKind.None;
            updatedTopology = snapshot?.Topology ?? default;

            if (snapshot == null || !snapshot.Topology.IsFaceActive(sourceCell.face))
            {
                return false;
            }

            var hasResolvedStep = snapshot.TryResolveUnitStep(
                sourceCell,
                delta,
                out destinationCell,
                out rotationKind,
                out updatedTopology);

            if (!hasResolvedStep)
            {
                destinationCell = sourceCell + delta;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
            }

            return true;
        }

        internal static bool HasWallFollowAnchor(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return HasWallFollowAnchor(
                snapshot,
                source.type,
                source.entityId,
                source.position,
                source.facing,
                settings);
        }

        internal static bool TryChooseWallFollowDirection(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            out Direction direction)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            direction = Direction.None;

            if (TryChooseWallFollowDirectionWithDestinationAnchor(
                    snapshot,
                    source,
                    settings,
                    WallFollowAnchorFilter.PreferredOnly,
                    WallFollowMovementChoice.PreferredTurn,
                    WallFollowMovementChoice.Forward,
                    WallFollowMovementChoice.OppositeTurn,
                    out direction))
            {
                return true;
            }

            if (HasWallFollowAnchor(snapshot, source, settings))
            {
                return TryChooseFirstAvailableWallFollowDirection(
                    snapshot,
                    source,
                    settings,
                    WallFollowMovementChoice.Forward,
                    WallFollowMovementChoice.PreferredTurn,
                    WallFollowMovementChoice.OppositeTurn,
                    out direction);
            }

            if (TryChooseWallFollowDirectionWithDestinationAnchor(
                    snapshot,
                    source,
                    settings,
                    WallFollowAnchorFilter.WeakOnly,
                    WallFollowMovementChoice.PreferredTurn,
                    WallFollowMovementChoice.Forward,
                    WallFollowMovementChoice.OppositeTurn,
                    out direction))
            {
                return true;
            }

            return TryChooseFirstAvailableWallFollowDirection(
                    snapshot,
                    source,
                    settings,
                    WallFollowMovementChoice.PreferredTurn,
                    WallFollowMovementChoice.Forward,
                    WallFollowMovementChoice.OppositeTurn,
                    out direction);
        }

        internal static bool TryChooseWallFollowFacing(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            out Direction direction)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (TryChooseWallFollowDirection(snapshot, source, settings, out direction) &&
                direction != source.facing)
            {
                return true;
            }

            direction = Direction.None;
            return false;
        }

        internal static bool TryChooseWallFollowRotateOnlyFacing(
            Direction facing,
            WallFollowTurnPreference turnPreference,
            out Direction direction)
        {
            return TryChooseFacingOnlyWallFollowDirection(
                facing,
                turnPreference,
                WallFollowMovementChoice.PreferredTurn,
                WallFollowMovementChoice.OppositeTurn,
                WallFollowMovementChoice.Forward,
                out direction);
        }

        private static bool TryChooseWallFollowDirectionWithDestinationAnchor(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            WallFollowAnchorFilter filter,
            WallFollowMovementChoice firstChoice,
            WallFollowMovementChoice secondChoice,
            WallFollowMovementChoice thirdChoice,
            out Direction direction)
        {
            direction = Direction.None;

            if (TryChooseWallFollowDirectionWithDestinationAnchor(snapshot, source, settings, filter, firstChoice, out direction))
            {
                return true;
            }

            if (TryChooseWallFollowDirectionWithDestinationAnchor(snapshot, source, settings, filter, secondChoice, out direction))
            {
                return true;
            }

            return TryChooseWallFollowDirectionWithDestinationAnchor(snapshot, source, settings, filter, thirdChoice, out direction);
        }

        private static bool TryChooseWallFollowDirectionWithDestinationAnchor(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            WallFollowAnchorFilter filter,
            WallFollowMovementChoice choice,
            out Direction direction)
        {
            direction = Direction.None;

            if (!TryEvaluateWallFollowCandidate(snapshot, source, settings, choice, out var candidateDirection, out _, out var destinationCell))
            {
                return false;
            }

            var anchorKind = GetWallFollowAnchorKind(
                snapshot,
                source.type,
                source.entityId,
                destinationCell,
                candidateDirection,
                settings);

            if (!MatchesWallFollowAnchorFilter(anchorKind, filter))
            {
                return false;
            }

            direction = candidateDirection;
            return true;
        }

        private static bool TryChooseFirstAvailableWallFollowDirection(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            WallFollowMovementChoice firstChoice,
            WallFollowMovementChoice secondChoice,
            WallFollowMovementChoice thirdChoice,
            out Direction direction)
        {
            direction = Direction.None;

            if (TryChooseFirstAvailableWallFollowDirection(snapshot, source, settings, firstChoice, out direction))
            {
                return true;
            }

            if (TryChooseFirstAvailableWallFollowDirection(snapshot, source, settings, secondChoice, out direction))
            {
                return true;
            }

            return TryChooseFirstAvailableWallFollowDirection(snapshot, source, settings, thirdChoice, out direction);
        }

        private static bool TryChooseFirstAvailableWallFollowDirection(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            WallFollowMovementChoice choice,
            out Direction direction)
        {
            direction = Direction.None;

            if (!TryEvaluateWallFollowCandidate(snapshot, source, settings, choice, out var candidateDirection, out _, out _))
            {
                return false;
            }

            direction = candidateDirection;
            return true;
        }

        private static bool TryChooseFacingOnlyWallFollowDirection(
            Direction facing,
            WallFollowTurnPreference turnPreference,
            WallFollowMovementChoice firstChoice,
            WallFollowMovementChoice secondChoice,
            WallFollowMovementChoice thirdChoice,
            out Direction direction)
        {
            direction = Direction.None;

            if (TryChooseFacingOnlyWallFollowDirection(facing, turnPreference, firstChoice, out direction))
            {
                return true;
            }

            if (TryChooseFacingOnlyWallFollowDirection(facing, turnPreference, secondChoice, out direction))
            {
                return true;
            }

            return TryChooseFacingOnlyWallFollowDirection(facing, turnPreference, thirdChoice, out direction);
        }

        private static bool TryChooseFacingOnlyWallFollowDirection(
            Direction facing,
            WallFollowTurnPreference turnPreference,
            WallFollowMovementChoice choice,
            out Direction direction)
        {
            return TryResolveWallFollowDirection(facing, turnPreference, choice, out direction, out _);
        }

        private static bool TryEvaluateWallFollowCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            in PatrolSettings settings,
            WallFollowMovementChoice choice,
            out Direction direction,
            out Vector2Int delta,
            out SurfaceCell destinationCell)
        {
            direction = Direction.None;
            delta = Vector2Int.zero;
            destinationCell = default;

            if (!TryResolveWallFollowDirection(source.facing, settings.TurnPreference, choice, out direction, out delta) ||
                !CanTraverseStep(snapshot, source, delta) ||
                !TryResolveAdjacentCellWithoutTopologyChange(snapshot, source.position, delta, out destinationCell))
            {
                direction = Direction.None;
                delta = Vector2Int.zero;
                destinationCell = default;
                return false;
            }

            return true;
        }

        private static bool TryResolveWallFollowDirection(
            Direction facing,
            WallFollowTurnPreference turnPreference,
            WallFollowMovementChoice choice,
            out Direction direction,
            out Vector2Int delta)
        {
            var relativeDirection = choice switch
            {
                WallFollowMovementChoice.Forward => RelativeDirection.Forward,
                WallFollowMovementChoice.PreferredTurn => GetHandSide(turnPreference),
                WallFollowMovementChoice.OppositeTurn => GetOppositeHandSide(turnPreference),
                _ => RelativeDirection.Forward,
            };

            return TryResolveRelativeDirection(facing, relativeDirection, out direction, out delta);
        }

        private static bool TryResolveRelativeDirection(
            Direction facing,
            RelativeDirection relativeDirection,
            out Direction direction,
            out Vector2Int delta)
        {
            direction = relativeDirection switch
            {
                RelativeDirection.Forward => facing,
                RelativeDirection.Right => TurnRight(facing),
                RelativeDirection.Left => TurnLeft(facing),
                RelativeDirection.Back => TurnBack(facing),
                _ => Direction.None,
            };

            return TryResolveDelta(direction, out delta);
        }

        internal static bool TryResolveAdjacentCellWithoutTopologyChange(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta,
            out SurfaceCell adjacentCell)
        {
            return TryResolveAdjacentCellWithoutTopologyChange(snapshot, source.position, delta, out adjacentCell);
        }

        internal static bool TryResolveAdjacentCellWithoutTopologyChange(
            WorldSnapshot snapshot,
            SurfaceCell sourceCell,
            Vector2Int delta,
            out SurfaceCell adjacentCell)
        {
            adjacentCell = default;

            var hasResolvedStep = snapshot.TryResolveUnitStep(
                sourceCell,
                delta,
                out adjacentCell,
                out var rotationKind,
                out _);

            if (!hasResolvedStep)
            {
                adjacentCell = sourceCell + delta;
                rotationKind = CubeRotationKind.None;
            }

            return rotationKind == CubeRotationKind.None;
        }

        private static bool HasWallFollowAnchor(
            WorldSnapshot snapshot,
            EntityType sourceType,
            int sourceEntityId,
            SurfaceCell originCell,
            Direction facing,
            in PatrolSettings settings)
        {
            return GetWallFollowAnchorKind(
                       snapshot,
                       sourceType,
                       sourceEntityId,
                       originCell,
                       facing,
                       settings) != WallFollowAnchorKind.None;
        }

        private static WallFollowAnchorKind GetWallFollowAnchorKind(
            WorldSnapshot snapshot,
            EntityType sourceType,
            int sourceEntityId,
            SurfaceCell originCell,
            Direction facing,
            in PatrolSettings settings)
        {
            if (!TryResolveRelativeDirection(facing, GetHandSide(settings.TurnPreference), out _, out var delta) ||
                !TryResolveAdjacentCellWithoutTopologyChange(snapshot, originCell, delta, out var adjacentCell))
            {
                return WallFollowAnchorKind.None;
            }

            if (!snapshot.TryGetPlacementBlocker(
                    snapshot.Topology,
                    sourceType,
                    adjacentCell,
                    sourceEntityId,
                    out var blocker))
            {
                return WallFollowAnchorKind.None;
            }

            return GetWallFollowAnchorKind(snapshot, adjacentCell, blocker, settings);
        }

        private static WallFollowAnchorKind GetWallFollowAnchorKind(
            WorldSnapshot snapshot,
            SurfaceCell adjacentCell,
            SlideStopper blocker,
            in PatrolSettings settings)
        {
            if (blocker.Kind == SlideStopperKind.BoardEdge)
            {
                return WallFollowAnchorKind.BoardEdge;
            }

            if (blocker.Kind != SlideStopperKind.Entity ||
                !snapshot.TryGetSolidSemanticAt(adjacentCell, out var adjacentSolid))
            {
                return WallFollowAnchorKind.None;
            }

            if (settings.FollowWalls && adjacentSolid.Kind == SolidKind.Wall)
            {
                return WallFollowAnchorKind.Wall;
            }

            if (settings.FollowBoxes && adjacentSolid.Kind == SolidKind.Box)
            {
                return WallFollowAnchorKind.Box;
            }

            return WallFollowAnchorKind.None;
        }

        private static bool MatchesWallFollowAnchorFilter(
            WallFollowAnchorKind anchorKind,
            WallFollowAnchorFilter filter)
        {
            return filter switch
            {
                WallFollowAnchorFilter.PreferredOnly => anchorKind == WallFollowAnchorKind.Wall ||
                                                        anchorKind == WallFollowAnchorKind.Box,
                WallFollowAnchorFilter.WeakOnly => anchorKind == WallFollowAnchorKind.BoardEdge,
                _ => false,
            };
        }

        private static RelativeDirection GetHandSide(WallFollowTurnPreference turnPreference)
        {
            return turnPreference == WallFollowTurnPreference.Right
                ? RelativeDirection.Right
                : RelativeDirection.Left;
        }

        private static RelativeDirection GetOppositeHandSide(WallFollowTurnPreference turnPreference)
        {
            return turnPreference == WallFollowTurnPreference.Right
                ? RelativeDirection.Left
                : RelativeDirection.Right;
        }

        private static Direction TurnRight(Direction facing)
        {
            return facing switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnLeft(Direction facing)
        {
            return facing switch
            {
                Direction.Up => Direction.Left,
                Direction.Left => Direction.Down,
                Direction.Down => Direction.Right,
                Direction.Right => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnBack(Direction facing)
        {
            return facing switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }

    }

    internal static class EnemyChargeStrategyShared
    {
        public static bool TryResolveChargeStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            out Direction direction,
            out int reachableSteps)
        {
            reachableSteps = 0;

            if (!TryResolveChargeDirection(source, target, out direction, out var delta))
            {
                return false;
            }

            if (!EnemyMovementStrategyShared.CanTraverseChargeStepIgnoringUnits(snapshot, source, delta))
            {
                return false;
            }

            return TryCountReachableChargeSteps(snapshot, source, delta, out reachableSteps) &&
                   reachableSteps > 0;
        }

        public static bool CanAdvanceChargeStep(
            WorldSnapshot snapshot,
            in EntityState source)
        {
            return CanAdvanceChargeStep(snapshot, source, source.facing);
        }

        public static bool CanAdvanceChargeStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Direction direction)
        {
            var delta = EnemyMovementStrategyShared.ResolveDelta(direction);
            return delta.HasValue && EnemyMovementStrategyShared.CanTraverseChargeStepIgnoringUnits(snapshot, source, delta.Value);
        }

        private static bool TryResolveChargeDirection(
            in EntityState source,
            in EntityState target,
            out Direction direction,
            out Vector2Int delta)
        {
            direction = Direction.None;
            delta = Vector2Int.zero;

            if (source.position.face != target.position.face)
            {
                return false;
            }

            var planarDelta = target.position - source.position;
            if (planarDelta.x != 0 && planarDelta.y != 0)
            {
                return false;
            }

            if (planarDelta.x == 0 && planarDelta.y == 0)
            {
                return false;
            }

            if (planarDelta.x != 0)
            {
                direction = planarDelta.x > 0 ? Direction.Right : Direction.Left;
                delta = new Vector2Int(Math.Sign(planarDelta.x), 0);
                return true;
            }

            direction = planarDelta.y > 0 ? Direction.Up : Direction.Down;
            delta = new Vector2Int(0, Math.Sign(planarDelta.y));
            return true;
        }

        private static bool TryCountReachableChargeSteps(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta,
            out int reachableSteps)
        {
            reachableSteps = 0;

            if (!snapshot.Topology.IsFaceActive(source.position.face))
            {
                return false;
            }

            var current = source.position;
            while (TryResolveChargeScanStep(snapshot, current, delta, out var nextCell))
            {
                if (RuntimeTraversalLegalityPolicy.EvaluateChargeSolidOnlyStopCell(
                        snapshot,
                        nextCell).Verdict == LegalityVerdict.Blocked)
                {
                    return reachableSteps > 0;
                }

                reachableSteps++;
                current = nextCell;
            }

            return reachableSteps > 0;
        }

        private static bool TryResolveChargeScanStep(
            WorldSnapshot snapshot,
            SurfaceCell current,
            Vector2Int delta,
            out SurfaceCell nextCell)
        {
            if (!EnemyMovementStrategyShared.TryResolveStep(snapshot, current, delta, out nextCell, out var rotationKind, out _))
            {
                return false;
            }

            return rotationKind == CubeRotationKind.None;
        }
    }
}
