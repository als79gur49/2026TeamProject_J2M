using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public sealed class PlayerLogic : IMovementEntityLogic, IEntityLogicSourceBinding
    {
        private const int DefaultCommandPriority = 100;

        private readonly int _entityId;
        private readonly int _pushContactThresholdTicks;

        public PlayerLogic(int entityId)
            : this(entityId, GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks)
        {
        }

        public PlayerLogic(int entityId, int pushContactThresholdTicks)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Player logic requires a positive entity ID.");
            }

            if (pushContactThresholdTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pushContactThresholdTicks), "Push contact threshold must be greater than zero.");
            }

            _entityId = entityId;
            _pushContactThresholdTicks = pushContactThresholdTicks;
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

            if (!snapshot.TryGetEntity(_entityId, out var entity))
            {
                return;
            }

            if (entity.hp <= 0 || entity.markedForDeath)
            {
                return;
            }

            var hasControlState = snapshot.TryGetPlayerControlState(_entityId, out var controlState);
            if (hasControlState &&
                controlState.interactionLockTicks > 0)
            {
                return;
            }

            if (!TryResolveDelta(input.PlayerCommand.MoveDirection, out var delta))
            {
                return;
            }

            if (input.PlayerCommand.FlipPressed)
            {
                buffer.Add(
                    new RawMovementIntent(
                        entity.entityId,
                        DefaultCommandPriority,
                        entity.position + delta,
                        MovementCommandKind.Flip,
                        localSequence: 0));
                return;
            }

            if (hasControlState &&
                controlState.moveCooldownTicks > 0)
            {
                return;
            }

            var hasMatchingPushContact =
                hasControlState &&
                controlState.pushTargetEntityId > 0 &&
                controlState.pushDirection == input.PlayerCommand.MoveDirection;

            if (hasMatchingPushContact)
            {
                if (controlState.pushContactTicks >= _pushContactThresholdTicks)
                {
                    buffer.Add(
                        new RawMovementIntent(
                            entity.entityId,
                            DefaultCommandPriority,
                            entity.position + delta,
                            MovementCommandKind.Push,
                            localSequence: 0));
                }

                return;
            }

            buffer.Add(
                new RawMovementIntent(
                    entity.entityId,
                    DefaultCommandPriority,
                    entity.position + delta,
                    MovementCommandKind.Move,
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
}
