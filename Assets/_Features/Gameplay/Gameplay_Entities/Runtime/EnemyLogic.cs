using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public sealed class EnemyLogic : IMovementEntityLogic, IAttackEntityLogic, IEntityLogicSourceBinding
    {
        private const int DefaultAttackPriority = 50;
        private const int DefaultMovementPriority = 50;
        private const int DefaultSenseRange = 8;

        private readonly int _entityId;

        public EnemyLogic(int entityId)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Enemy logic requires a positive entity ID.");
            }

            _entityId = entityId;
        }

        public int ControlledEntityId => _entityId;

        public void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!TryGetControllableEnemy(snapshot, out var source))
            {
                return;
            }

            switch (source.aiMode)
            {
                case EnemyAiMode.Patrol:
                    if (TryBuildPatrolMove(snapshot, source, out var patrolIntent))
                    {
                        buffer.Add(patrolIntent);
                    }

                    return;

                case EnemyAiMode.Chase:
                    if (!TryFindNearestTarget(snapshot, source, DefaultSenseRange, out var chaseTarget))
                    {
                        return;
                    }

                    if (TryBuildChaseMove(snapshot, source, chaseTarget, out var chaseIntent))
                    {
                        buffer.Add(chaseIntent);
                    }

                    return;

                default:
                    return;
            }
        }

        public void CollectAttackIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!TryGetControllableEnemy(snapshot, out var source) ||
                source.aiMode != EnemyAiMode.Attack ||
                !TryFindNearestTarget(snapshot, source, DefaultSenseRange, out var target) ||
                !IsOrthogonallyAdjacent(source.position, target.position))
            {
                return;
            }

            buffer.Add(new RawAttackIntent(source.entityId, DefaultAttackPriority, target.entityId));
        }

        private bool TryGetControllableEnemy(WorldSnapshot snapshot, out EntityState source)
        {
            if (!snapshot.TryGetEntity(_entityId, out source))
            {
                return false;
            }

            return source.type == EntityType.Unit &&
                   source.hp > 0 &&
                   !source.markedForDeath &&
                   source.boardPresence == EntityBoardPresence.Occupying &&
                   snapshot.Topology.IsFaceActive(source.position.face) &&
                   source.aiMode != EnemyAiMode.None &&
                   source.aiMode != EnemyAiMode.Dead;
        }

        private static bool TryBuildPatrolMove(
            WorldSnapshot snapshot,
            in EntityState source,
            out RawMovementIntent intent)
        {
            intent = default;

            var delta = ResolveDelta(source.facing);
            if (!delta.HasValue || !CanOccupyStep(snapshot, source, delta.Value))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                DefaultMovementPriority,
                source.position.PlanarPosition + delta.Value);
            return true;
        }

        private static bool TryBuildChaseMove(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            out RawMovementIntent intent)
        {
            intent = default;

            if (!TryChooseChaseStep(snapshot, source, target, out var delta))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                DefaultMovementPriority,
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

        private static bool TryFindNearestTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            int maxDistance,
            out EntityState target)
        {
            target = default;

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var bestDistance = int.MaxValue;
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                if (!IsValidTarget(snapshot, source, candidate))
                {
                    continue;
                }

                var distance = GetPlanarDistance(source.position, candidate.position);
                if (!distance.HasValue || distance.Value > maxDistance || distance.Value >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance.Value;
                target = candidate;
            }

            return bestDistance != int.MaxValue;
        }

        private static bool IsValidTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState candidate)
        {
            return candidate.entityId != source.entityId &&
                   candidate.type == EntityType.Unit &&
                   candidate.teamId != source.teamId &&
                   candidate.hp > 0 &&
                   snapshot.CanBeTargetedForNewSelection(candidate.entityId);
        }

        private static bool CanOccupyStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta)
        {
            var hasResolvedStep = snapshot.TryResolvePlayerStep(
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

        private static int? GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            if (source.face != target.face)
            {
                return null;
            }

            var delta = target - source;
            return Math.Abs(delta.x) + Math.Abs(delta.y);
        }

        private static bool IsOrthogonallyAdjacent(SurfaceCell source, SurfaceCell target)
        {
            var distance = GetPlanarDistance(source, target);
            return distance.HasValue && distance.Value == 1;
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
