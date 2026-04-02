using System;
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

        public PatrolSettings(PatrolBlockedMovementResponse blockedMovementResponse)
        {
            this.blockedMovementResponse = blockedMovementResponse;
        }

        public PatrolBlockedMovementResponse BlockedMovementResponse => blockedMovementResponse;

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

        public ChaseSettings(
            ChaseAxisPriorityMode axisPriority,
            bool trySecondaryAxisWhenBlocked)
        {
            this.axisPriority = axisPriority;
            this.trySecondaryAxisWhenBlocked = trySecondaryAxisWhenBlocked;
        }

        public ChaseAxisPriorityMode AxisPriority => axisPriority;

        public bool TrySecondaryAxisWhenBlocked => trySecondaryAxisWhenBlocked;

        public static ChaseSettings CreateDefault()
        {
            return new ChaseSettings(
                ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak,
                trySecondaryAxisWhenBlocked: true);
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
            if (Math.Abs(planarDelta.x) + Math.Abs(planarDelta.y) <= 1)
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

            return !snapshot.TryGetPlacementBlocker(
                movementTopology,
                source.type,
                destinationCell,
                source.entityId,
                out _);
        }

        public static Vector2Int? ResolveDelta(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2Int.up;

                case Direction.Right:
                    return Vector2Int.right;

                case Direction.Down:
                    return Vector2Int.down;

                case Direction.Left:
                    return Vector2Int.left;

                default:
                    return null;
            }
        }
    }
}
