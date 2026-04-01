using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.PlayerControl
{
    internal sealed class PlayerControlStateLogic : IPreMovementStateLogic, IEntityLogicSourceBinding
    {
        private readonly int _entityId;

        public PlayerControlStateLogic(int entityId)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Player control state logic requires a positive entity ID.");
            }

            _entityId = entityId;
        }

        public int ControlledEntityId => _entityId;

        public void CommitPreMovementState(
            WorldSnapshot snapshot,
            in TickInput input,
            IPlayerControlCommitContext writeContext,
            List<string> updates)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            if (!snapshot.TryGetEntity(_entityId, out var entity) ||
                entity.hp <= 0 ||
                entity.markedForDeath)
            {
                return;
            }

            var nextState = snapshot.TryGetPlayerControlState(_entityId, out var controlState)
                ? controlState
                : default;

            if (nextState.moveCooldownTicks > 0)
            {
                nextState.moveCooldownTicks--;
            }

            if (input.PlayerCommand.FlipPressed ||
                input.PlayerCommand.MoveDirection == Direction.None ||
                !PlayerControlQueries.TryResolvePushContact(snapshot, entity, input.PlayerCommand.MoveDirection, out var contact))
            {
                nextState = PlayerControlQueries.ResetContact(nextState);
            }
            else if (nextState.pushTargetEntityId == contact.TargetEntityId &&
                     nextState.pushDirection == contact.Direction)
            {
                nextState.pushContactTicks++;
            }
            else
            {
                nextState.pushContactTicks = 1;
                nextState.pushTargetEntityId = contact.TargetEntityId;
                nextState.pushDirection = contact.Direction;
            }

            writeContext.SetPlayerControlState(_entityId, nextState);
            updates.Add(
                $"PlayerControlUpdated|E={_entityId}|Cooldown={nextState.moveCooldownTicks}|PushTicks={nextState.pushContactTicks}|Target={nextState.pushTargetEntityId}|Direction={nextState.pushDirection}");
        }
    }
}
