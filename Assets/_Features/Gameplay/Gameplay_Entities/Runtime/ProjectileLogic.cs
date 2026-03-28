using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class ProjectileLogic : IEntityLogic, IEntityLogicSourceBinding
    {
        private const int DefaultMovementPriority = 0;

        private readonly int _sourceId;

        public ProjectileLogic(int sourceId)
        {
            if (sourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Projectile logic requires a positive source ID.");
            }

            _sourceId = sourceId;
        }

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

            if (!snapshot.TryGetEntity(_sourceId, out var source))
            {
                return;
            }

            if (source.type != EntityType.Projectile || source.hp <= 0 || source.markedForDeath)
            {
                return;
            }

            var delta = ResolveDelta(source.facing);
            if (!delta.HasValue)
            {
                return;
            }

            buffer.Add(
                new RawMovementIntent(
                    source.entityId,
                    DefaultMovementPriority,
                    source.position + delta.Value));
        }

        public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
        }

        public bool ControlsEntity(int entityId, TickPhase phase)
        {
            return phase == TickPhase.Movement && _sourceId == entityId;
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

    internal sealed class ProjectileEntityLogicFactory : IEntityLogicFactory
    {
        public bool CanCreate(in EntityState entity)
        {
            return entity.type == EntityType.Projectile;
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new ProjectileLogic(entity.entityId);
        }
    }

    internal sealed class SlidingBoxLogic : IEntityLogic, IEntityLogicSourceBinding
    {
        private const int DefaultMovementPriority = 0;

        private readonly int _sourceId;

        public SlidingBoxLogic(int sourceId)
        {
            if (sourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Sliding box logic requires a positive source ID.");
            }

            _sourceId = sourceId;
        }

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

            if (!snapshot.TryGetEntity(_sourceId, out var source))
            {
                return;
            }

            if (source.type != EntityType.Box ||
                source.hp <= 0 ||
                source.markedForDeath ||
                source.boardPresence != EntityBoardPresence.Occupying ||
                source.state != EntityPhaseState.Sliding ||
                (source.boxCapabilities & BoxCapabilities.Push) != BoxCapabilities.Push ||
                !snapshot.Topology.IsFaceActive(source.position.face))
            {
                return;
            }

            if (!TryResolveDelta(source.facing, out var delta))
            {
                return;
            }

            buffer.Add(
                new RawMovementIntent(
                    source.entityId,
                    DefaultMovementPriority,
                    source.position + delta,
                    Movement.MovementCommandKind.Move,
                    localSequence: 0));
        }

        public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
        }

        public bool ControlsEntity(int entityId, TickPhase phase)
        {
            return phase == TickPhase.Movement && _sourceId == entityId;
        }

        private static bool TryResolveDelta(Direction direction, out Vector2Int delta)
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
    }

    internal sealed class SlidingBoxEntityLogicFactory : IEntityLogicFactory
    {
        public bool CanCreate(in EntityState entity)
        {
            return entity.type == EntityType.Box &&
                   (entity.boxCapabilities & BoxCapabilities.Push) == BoxCapabilities.Push;
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new SlidingBoxLogic(entity.entityId);
        }
    }
}
