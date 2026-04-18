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

        public PatrolSettings(
            PatrolBlockedMovementResponse blockedMovementResponse,
            WallFollowTurnPreference turnPreference = WallFollowTurnPreference.Right,
            bool followWalls = true,
            bool followBoxes = true)
        {
            this.blockedMovementResponse = blockedMovementResponse;
            this.turnPreference = turnPreference;
            this.followWalls = followWalls;
            this.followBoxes = followBoxes;
        }

        public PatrolBlockedMovementResponse BlockedMovementResponse => blockedMovementResponse;

        public WallFollowTurnPreference TurnPreference => turnPreference;

        public bool FollowWalls => followWalls;

        public bool FollowBoxes => followBoxes;

        public bool StopWhenForwardBlocked => blockedMovementResponse == PatrolBlockedMovementResponse.Stop;

        public static PatrolSettings CreateDefault()
        {
            return new PatrolSettings(PatrolBlockedMovementResponse.Stop);
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

    public sealed class AxisPriorityChaseStrategy : IChaseStrategy
    {
        public static readonly AxisPriorityChaseStrategy Instance = new();

        public bool TryBuildMovementIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in ChaseSettings settings,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            if (!TryChooseChaseStep(snapshot, source, target, settings, out var delta))
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

            if (TrySelectCandidate(snapshot, source, horizontalStep, verticalStep, tryHorizontalFirst, out delta))
            {
                return true;
            }

            if (!settings.TrySecondaryAxisWhenBlocked)
            {
                return false;
            }

            return TrySelectCandidate(snapshot, source, horizontalStep, verticalStep, !tryHorizontalFirst, out delta);
        }

        private static bool TrySelectCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int? horizontalStep,
            Vector2Int? verticalStep,
            bool tryHorizontalFirst,
            out Vector2Int delta)
        {
            delta = Vector2Int.zero;

            if (tryHorizontalFirst)
            {
                return EnemyMovementStrategyShared.CanOccupyStep(snapshot, source, horizontalStep, out delta);
            }

            return EnemyMovementStrategyShared.CanOccupyStep(snapshot, source, verticalStep, out delta);
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

            if (!CanOccupyStep(snapshot, source, delta))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                commonSettings.MovementPriority,
                source.position.PlanarPosition + delta);
            return true;
        }

        public static bool CanOccupyStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int? candidate,
            out Vector2Int delta)
        {
            delta = Vector2Int.zero;

            if (!candidate.HasValue || !CanOccupyStep(snapshot, source, candidate.Value))
            {
                return false;
            }

            delta = candidate.Value;
            return true;
        }

        public static bool CanOccupyStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta)
        {
            if (!snapshot.Topology.IsFaceActive(source.position.face))
            {
                return false;
            }

            var hasResolvedStep = snapshot.TryResolveUnitStep(
                source.position,
                delta,
                out var destinationCell,
                out var rotationKind,
                out var updatedTopology);

            if (!hasResolvedStep)
            {
                destinationCell = source.position + delta;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
            }

            if (rotationKind != CubeRotationKind.None)
            {
                return false;
            }

            var movementTopology = rotationKind == CubeRotationKind.None
                ? snapshot.Topology
                : updatedTopology;

            if (snapshot.TryGetPlacementBlocker(
                    movementTopology,
                    source.type,
                    destinationCell,
                    source.entityId,
                    out _))
            {
                return false;
            }

            return IsTraversableUnitDestination(snapshot, source, destinationCell);
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
                !CanOccupyStep(snapshot, source, delta) ||
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

        private static bool TryResolveAdjacentCellWithoutTopologyChange(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta,
            out SurfaceCell adjacentCell)
        {
            return TryResolveAdjacentCellWithoutTopologyChange(snapshot, source.position, delta, out adjacentCell);
        }

        private static bool TryResolveAdjacentCellWithoutTopologyChange(
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

        private static bool IsTraversableUnitDestination(
            WorldSnapshot snapshot,
            in EntityState source,
            SurfaceCell destinationCell)
        {
            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(destinationCell, occupants);

            var hasRelevantOccupant = false;
            var hasHostilePlayer = false;
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == source.entityId ||
                    occupant.boardPresence != EntityBoardPresence.Occupying ||
                    occupant.hp <= 0 ||
                    occupant.markedForDeath)
                {
                    continue;
                }

                hasRelevantOccupant = true;
                if (occupant.teamId == source.teamId)
                {
                    return false;
                }

                if (!EntityRolePolicy.IsPlayerUnit(occupant))
                {
                    return false;
                }

                hasHostilePlayer = true;
            }

            return !hasRelevantOccupant || hasHostilePlayer;
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

            if (!EnemyMovementStrategyShared.CanOccupyStep(snapshot, source, delta))
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
            var delta = EnemyMovementStrategyShared.ResolveDelta(source.facing);
            return delta.HasValue && EnemyMovementStrategyShared.CanOccupyStep(snapshot, source, delta.Value);
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
                if (IsChargeStoppingObstacle(snapshot, nextCell))
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
            var hasResolvedStep = snapshot.TryResolveUnitStep(
                current,
                delta,
                out nextCell,
                out var rotationKind,
                out _);
            if (!hasResolvedStep)
            {
                nextCell = current + delta;
            }

            return rotationKind == CubeRotationKind.None;
        }

        private static bool IsChargeStoppingObstacle(
            WorldSnapshot snapshot,
            SurfaceCell cell)
        {
            if (!snapshot.IsInsideBoard(cell) || snapshot.IsTerrainBlockedForUnit(cell))
            {
                return true;
            }

            if (snapshot.TryGetSolidSemanticAt(cell, out _))
            {
                return true;
            }

            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.boardPresence == EntityBoardPresence.Occupying &&
                    occupant.hp > 0 &&
                    !occupant.markedForDeath)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
