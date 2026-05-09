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
        private readonly int _flipRecoveryTicks;
        private readonly int _flipWindupTicks;
        private readonly int _pushRecoveryTicks;
        private readonly int _pushWindupTicks;

        public PlayerLogic(int entityId)
            : this(
                entityId,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 1,
                flipRecoveryTicks: 0)
        {
        }

        public PlayerLogic(
            int entityId,
            int pushWindupTicks,
            int pushRecoveryTicks,
            int flipWindupTicks,
            int flipRecoveryTicks)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Player logic requires a positive entity ID.");
            }

            if (pushWindupTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pushWindupTicks), "Push wind-up ticks must be zero or greater.");
            }

            if (pushRecoveryTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pushRecoveryTicks), "Push recovery ticks must be zero or greater.");
            }

            if (flipWindupTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flipWindupTicks), "Flip wind-up ticks must be zero or greater.");
            }

            if (flipRecoveryTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flipRecoveryTicks), "Flip recovery ticks must be zero or greater.");
            }

            _entityId = entityId;
            _pushWindupTicks = pushWindupTicks;
            _pushRecoveryTicks = pushRecoveryTicks;
            _flipWindupTicks = flipWindupTicks;
            _flipRecoveryTicks = flipRecoveryTicks;
        }

        public int ControlledEntityId => _entityId;

        internal int PushWindupTicks => _pushWindupTicks;

        internal int PushRecoveryTicks => _pushRecoveryTicks;

        internal int FlipWindupTicks => _flipWindupTicks;

        internal int FlipRecoveryTicks => _flipRecoveryTicks;

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
                controlState.activeAction.IsActive)
            {
                if (input.TickIndex != controlState.activeAction.executeTick ||
                    !TryResolveDelta(controlState.activeAction.direction, out var actionDelta))
                {
                    return;
                }

                buffer.Add(
                    new RawMovementIntent(
                        entity.entityId,
                        DefaultCommandPriority,
                        entity.position.PlanarPosition + actionDelta,
                        ResolveCommandKind(controlState.activeAction.kind),
                        localSequence: controlState.activeAction.sequence));
                return;
            }

            if (!TryResolveDelta(input.PlayerCommand.MoveDirection, out var delta))
            {
                return;
            }

            if (input.PlayerCommand.PushPressed || input.PlayerCommand.FlipPressed)
            {
                return;
            }

            if (input.PlayerCommand.IsMoveBuffered)
            {
                return;
            }

            if (hasControlState &&
                PlayerControlQueries.IsMoveOnCooldown(controlState, input.TickIndex))
            {
                return;
            }

            if (PlayerControlQueries.ShouldSuppressOrdinaryMoveForPushTarget(
                    snapshot,
                    entity,
                    input.PlayerCommand.MoveDirection))
            {
                return;
            }

            var destination = entity.position + delta;
            buffer.Add(
                new RawMovementIntent(
                    entity.entityId,
                    DefaultCommandPriority,
                    destination,
                    MovementCommandKind.Move,
                    localSequence: 0));
        }

        private static MovementCommandKind ResolveCommandKind(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => MovementCommandKind.Push,
                PlayerActionKind.Flip => MovementCommandKind.Flip,
                _ => MovementCommandKind.Move,
            };
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
