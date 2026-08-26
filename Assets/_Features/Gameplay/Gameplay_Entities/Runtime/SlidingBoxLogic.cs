using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class SlidingBoxLogic : IMovementEntityLogic, IEntityLogicSourceBinding
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

        public int ControlledEntityId => _sourceId;

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
                source.stateTimer > 0 ||
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

    internal sealed class SlidingBoxEntityLogicFactory : IEntityLogicFactory, IEntityLogicFactoryEntityTypePrefilter
    {
        public bool MayCreateForEntityType(EntityType entityType) => entityType == EntityType.Box;

        public bool CanCreate(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return entity.type == EntityType.Box &&
                   (entity.boxCapabilities & BoxCapabilities.Push) == BoxCapabilities.Push;
        }

        public IEntityLogic Create(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return new SlidingBoxLogic(entity.entityId);
        }
    }
}
