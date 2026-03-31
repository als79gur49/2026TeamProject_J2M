using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyMovementPolicy
    {
        public static bool TryBuildPatrolMove(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiConfig config,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            var delta = ResolveDelta(source.facing);
            if (!delta.HasValue || !CanOccupyStep(snapshot, source, delta.Value))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                config.MovementPriority,
                source.position.PlanarPosition + delta.Value);
            return true;
        }

        public static bool TryBuildChaseMove(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiConfig config,
            out RawMovementIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            if (!TryChooseChaseStep(snapshot, source, target, out var delta))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                config.MovementPriority,
                source.position.PlanarPosition + delta);
            return true;
        }

        private static bool TryChooseChaseStep(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
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

            var tryHorizontalFirst = ShouldTryHorizontalFirst(planarDelta, source.facing);

            return TrySelectChaseStep(snapshot, source, horizontalStep, verticalStep, tryHorizontalFirst, out delta);
        }

        private static bool TrySelectChaseStep(
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
                return TryCommitChaseStep(snapshot, source, horizontalStep, out delta) ||
                       TryCommitChaseStep(snapshot, source, verticalStep, out delta);
            }

            return TryCommitChaseStep(snapshot, source, verticalStep, out delta) ||
                   TryCommitChaseStep(snapshot, source, horizontalStep, out delta);
        }

        private static bool TryCommitChaseStep(
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

        private static bool CanOccupyStep(
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

        private static bool ShouldTryHorizontalFirst(Vector2Int planarDelta, Direction facing)
        {
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

        private static Vector2Int? ResolveDelta(Direction direction)
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
