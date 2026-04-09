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
        private readonly int _flipRecoveryTicks;
        private readonly int _flipWindupTicks;
        private readonly int _pushContactThresholdTicks;
        private readonly int _pushRecoveryTicks;
        private readonly int _pushWindupTicks;

        public PlayerControlStateLogic(int entityId)
            : this(
                entityId,
                GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 1,
                flipRecoveryTicks: 0)
        {
        }

        internal PlayerControlStateLogic(
            int entityId,
            int pushContactThresholdTicks,
            int pushWindupTicks,
            int pushRecoveryTicks,
            int flipWindupTicks,
            int flipRecoveryTicks)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Player control state logic requires a positive entity ID.");
            }

            if (pushContactThresholdTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pushContactThresholdTicks), "Push contact threshold must be greater than zero.");
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
            _pushContactThresholdTicks = pushContactThresholdTicks;
            _pushWindupTicks = pushWindupTicks;
            _pushRecoveryTicks = pushRecoveryTicks;
            _flipWindupTicks = flipWindupTicks;
            _flipRecoveryTicks = flipRecoveryTicks;
        }

        public int ControlledEntityId => _entityId;

        public void CommitPreMovementState(
            WorldSnapshot snapshot,
            in TickInput input,
            IPreMovementStateCommitContext writeContext,
            List<string> updates,
            List<PlayerActionTransition> actionTransitions)
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

            if (actionTransitions == null)
            {
                throw new ArgumentNullException(nameof(actionTransitions));
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
                nextState.moveCooldownTicks = nextState.nextMoveAllowedTick > 0
                    ? Math.Max(0, nextState.nextMoveAllowedTick - input.TickIndex - 1)
                    : nextState.moveCooldownTicks - 1;
            }

            if (nextState.nextMoveAllowedTick > 0 &&
                input.TickIndex >= nextState.nextMoveAllowedTick)
            {
                nextState.nextMoveAllowedTick = 0;
            }

            var previousAction = nextState.activeAction;
            var canStartAction = snapshot.CanStartAction(_entityId, input.TickIndex);
            var canUseMoveDirectionForActionState = !input.PlayerCommand.IsMoveBuffered || input.PlayerCommand.FlipPressed;

            if (!previousAction.IsActive &&
                canStartAction &&
                canUseMoveDirectionForActionState &&
                input.PlayerCommand.MoveDirection != Direction.None)
            {
                writeContext.SetFacing(_entityId, input.PlayerCommand.MoveDirection);
            }

            if (previousAction.IsActive)
            {
                if (!previousAction.executionAttempted &&
                    !PlayerControlQueries.CanPendingActionStillExecute(snapshot, entity, previousAction))
                {
                    nextState = PlayerControlQueries.ResetContact(nextState);
                    nextState.activeAction = default;
                }
                else
                {
                    nextState = PlayerControlQueries.AdvanceActiveAction(nextState, input.TickIndex);
                }
            }
            else if (input.PlayerCommand.FlipPressed)
            {
                nextState = PlayerControlQueries.ResetContact(nextState);

                if (canStartAction &&
                    PlayerControlQueries.TryResolveFlipTarget(snapshot, entity, input.PlayerCommand.MoveDirection, out var flipTarget))
                {
                    nextState = PlayerControlQueries.StartAction(
                        nextState,
                        PlayerActionKind.Flip,
                        flipTarget.Direction,
                        flipTarget.TargetEntityId,
                        input.TickIndex,
                        _flipWindupTicks,
                        _flipRecoveryTicks);
                }
            }
            else if (input.PlayerCommand.IsMoveBuffered)
            {
                nextState = PlayerControlQueries.ResetContact(nextState);
            }
            else if (input.PlayerCommand.MoveDirection == Direction.None)
            {
                nextState = PlayerControlQueries.ResetContact(nextState);
            }
            else if (!canStartAction)
            {
                nextState = PlayerControlQueries.ResetContact(nextState);
            }
            else if (PlayerControlQueries.TryResolvePushContact(snapshot, entity, input.PlayerCommand.MoveDirection, out var pushTarget))
            {
                if (nextState.pushTargetEntityId == pushTarget.TargetEntityId &&
                    nextState.pushDirection == pushTarget.Direction)
                {
                    nextState.pushContactTicks++;
                }
                else
                {
                    nextState.pushContactTicks = 1;
                    nextState.pushTargetEntityId = pushTarget.TargetEntityId;
                    nextState.pushDirection = pushTarget.Direction;
                }

                if (nextState.pushContactTicks >= _pushContactThresholdTicks)
                {
                    nextState = PlayerControlQueries.StartAction(
                        nextState,
                        PlayerActionKind.Push,
                        pushTarget.Direction,
                        pushTarget.TargetEntityId,
                        input.TickIndex,
                        _pushWindupTicks,
                        _pushRecoveryTicks);
                }
            }
            else
            {
                nextState = PlayerControlQueries.ResetContact(nextState);
            }

            writeContext.SetPlayerControlState(_entityId, nextState);
            actionTransitions.Add(new PlayerActionTransition(_entityId, previousAction, nextState.activeAction));
            updates.Add(
                $"PlayerControlUpdated|E={_entityId}|Cooldown={nextState.moveCooldownTicks}|NextMoveAllowed={nextState.nextMoveAllowedTick}|PushTicks={nextState.pushContactTicks}|Target={nextState.pushTargetEntityId}|Direction={nextState.pushDirection}|Action={nextState.activeAction.kind}|ActionSeq={nextState.activeAction.sequence}|ActionDirection={nextState.activeAction.direction}|ActionTarget={nextState.activeAction.targetEntityId}|Start={nextState.activeAction.startTick}|Execute={nextState.activeAction.executeTick}|Recovery={nextState.activeAction.recoveryEndTick}|Attempted={(nextState.activeAction.executionAttempted ? 1 : 0)}");
        }
    }
}
