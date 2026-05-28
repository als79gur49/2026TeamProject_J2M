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
        private readonly int _pushRecoveryTicks;
        private readonly int _pushWindupTicks;

        public PlayerControlStateLogic(int entityId)
            : this(
                entityId,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 1,
                flipRecoveryTicks: 0)
        {
        }

        internal PlayerControlStateLogic(
            int entityId,
            int pushWindupTicks,
            int pushRecoveryTicks,
            int flipWindupTicks,
            int flipRecoveryTicks)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Player control state logic requires a positive entity ID.");
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

            var hasPreviousControlState = snapshot.TryGetPlayerControlState(_entityId, out var previousControlState);
            var nextState = hasPreviousControlState
                ? previousControlState
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
            var isSettledAtAnchor = UnitSpatialQuery.IsSettledAtAnchor(snapshot, _entityId);
            var canStartSettledAction = canStartAction && isSettledAtAnchor;
            var canUseMoveDirectionForActionState =
                !input.PlayerCommand.IsMoveBuffered ||
                input.PlayerCommand.PushPressed ||
                input.PlayerCommand.FlipPressed;
            var flipResultTurnTransition = default(PlayerFlipResultTurnTransition);

            if (!previousAction.IsActive &&
                canStartSettledAction &&
                canUseMoveDirectionForActionState &&
                input.PlayerCommand.MoveDirection != Direction.None)
            {
                writeContext.SetFacing(_entityId, input.PlayerCommand.MoveDirection);
            }

            if (previousAction.IsActive)
            {
                if (!previousAction.executionAttempted &&
                    !PlayerControlQueries.CanPendingActionStillExecute(snapshot, entity, previousAction, input.TickIndex))
                {
                    nextState.activeAction = default;
                    nextState.nextMoveAllowedTick = Math.Max(nextState.nextMoveAllowedTick, input.TickIndex + 1);
                }
                else
                {
                    nextState = PlayerControlQueries.AdvanceActiveAction(nextState, input.TickIndex);
                    CommitFlipResultFacingOnExecute(
                        previousAction,
                        nextState.activeAction,
                        writeContext,
                        updates);
                }
            }
            else if (PlayerControlQueries.HasQueuedFree2DAction(nextState))
            {
                if (!isSettledAtAnchor)
                {
                    updates.Add(
                        $"Free2DActionAssistQueued|Stage=PreMovement|Source={_entityId}|State=AwaitingSettled|Kind={nextState.queuedFree2DAction.kind}|Direction={nextState.queuedFree2DAction.direction}|RequestedTick={nextState.queuedFree2DAction.requestedTick}|Anchor={entity.position}");
                }
                else if (!canStartAction)
                {
                    updates.Add(
                        $"Free2DActionAssistRejected|Stage=PreMovement|Source={_entityId}|Reason=ActionNotStartable|Kind={nextState.queuedFree2DAction.kind}|Direction={nextState.queuedFree2DAction.direction}|RequestedTick={nextState.queuedFree2DAction.requestedTick}");
                    nextState = PlayerControlQueries.ClearQueuedFree2DAction(nextState);
                    updates.Add(
                        $"Free2DActionAssistCleared|Stage=PreMovement|Source={_entityId}|Reason=ActionNotStartable");
                }
                else if (TryStartQueuedFree2DAction(
                             snapshot,
                             entity,
                             nextState,
                             input.TickIndex,
                             writeContext,
                             updates,
                             out var queuedFlipResultTurnTransition,
                             out var queuedStartState))
                {
                    nextState = queuedStartState;
                    flipResultTurnTransition = queuedFlipResultTurnTransition;
                }
                else
                {
                    updates.Add(
                        $"Free2DActionAssistRejected|Stage=PreMovement|Source={_entityId}|Reason=NoCurrentTarget|Kind={nextState.queuedFree2DAction.kind}|Direction={nextState.queuedFree2DAction.direction}|RequestedTick={nextState.queuedFree2DAction.requestedTick}");
                    nextState = PlayerControlQueries.ClearQueuedFree2DAction(nextState);
                    updates.Add(
                        $"Free2DActionAssistCleared|Stage=PreMovement|Source={_entityId}|Reason=NoCurrentTarget");
                }
            }
            else if (input.PlayerCommand.PushPressed)
            {
                if (input.PlayerCommand.MoveDirection != Direction.None &&
                    canStartSettledAction &&
                    PlayerControlQueries.TryResolvePushContact(snapshot, entity, input.PlayerCommand.MoveDirection, input.TickIndex, out var pushTarget))
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
                else if (input.PlayerCommand.MoveDirection != Direction.None &&
                         canStartSettledAction &&
                         PlayerControlQueries.TryResolveAdjacentPushTarget(snapshot, entity, input.PlayerCommand.MoveDirection, out var adjacentTarget))
                {
                    updates.Add(
                        $"MovementRejected|Stage=PreMovement|Source={_entityId}|Reason=ExplicitPushNotStartable|Direction={input.PlayerCommand.MoveDirection}|Target={adjacentTarget.TargetEntityId}");
                }
                else if (!isSettledAtAnchor)
                {
                    updates.Add(
                        $"MovementRejected|Stage=PreMovement|Source={_entityId}|Reason=UnitKinematicNotSettled|Anchor={entity.position}");
                }
            }
            else if (input.PlayerCommand.FlipPressed)
            {

                if (canStartSettledAction &&
                    PlayerControlQueries.TryResolveFlipTarget(snapshot, entity, input.PlayerCommand.MoveDirection, input.TickIndex, out var flipTarget))
                {
                    nextState = PlayerControlQueries.StartAction(
                        nextState,
                        PlayerActionKind.Flip,
                        flipTarget.Direction,
                        flipTarget.TargetEntityId,
                        input.TickIndex,
                        _flipWindupTicks,
                        _flipRecoveryTicks);
                    flipResultTurnTransition = CreateFlipResultTurnTransition(
                        nextState.activeAction,
                        PlayerFlipResultTurnStartReason.ImmediateFlip);
                }
                else if (!isSettledAtAnchor)
                {
                    updates.Add(
                        $"MovementRejected|Stage=PreMovement|Source={_entityId}|Reason=UnitKinematicNotSettled|Anchor={entity.position}");
                }
            }

            UpdateMovementOwnedPhasedState(
                snapshot,
                input.TickIndex,
                nextState.activeAction,
                writeContext,
                updates);
            if (hasPreviousControlState &&
                AreEqual(previousControlState, nextState))
            {
                SnapshotMaterializationDiagnostics.RecordPlayerControlStateSameStateSkipped();
            }
            else
            {
                writeContext.SetPlayerControlState(_entityId, nextState);
                SnapshotMaterializationDiagnostics.RecordPlayerControlStateWritten();
            }

            actionTransitions.Add(
                new PlayerActionTransition(
                    _entityId,
                    previousAction,
                    nextState.activeAction,
                    flipResultTurnTransition));
            updates.Add(
                $"PlayerControlUpdated|E={_entityId}|Cooldown={nextState.moveCooldownTicks}|NextMoveAllowed={nextState.nextMoveAllowedTick}|Action={nextState.activeAction.kind}|ActionSeq={nextState.activeAction.sequence}|ActionDirection={nextState.activeAction.direction}|ActionTarget={nextState.activeAction.targetEntityId}|Start={nextState.activeAction.startTick}|Execute={nextState.activeAction.executeTick}|Recovery={nextState.activeAction.recoveryEndTick}|Attempted={(nextState.activeAction.executionAttempted ? 1 : 0)}|QueuedKinematicTurn={nextState.queuedKinematicTurnDirection}|QueuedFree2DAction={nextState.queuedFree2DAction.kind}|QueuedFree2DActionDirection={nextState.queuedFree2DAction.direction}|QueuedFree2DActionTick={nextState.queuedFree2DAction.requestedTick}");
        }

        private bool TryStartQueuedFree2DAction(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState state,
            int tickIndex,
            IPreMovementStateCommitContext writeContext,
            List<string> updates,
            out PlayerFlipResultTurnTransition flipResultTurnTransition,
            out PlayerControlState nextState)
        {
            nextState = state;
            flipResultTurnTransition = default;
            var queuedAction = state.queuedFree2DAction;
            switch (queuedAction.kind)
            {
                case PlayerQueuedFree2DActionKind.Push:
                    if (!PlayerControlQueries.TryResolvePushContact(snapshot, entity, queuedAction.direction, tickIndex, out var pushTarget))
                    {
                        return false;
                    }

                    if (entity.facing != queuedAction.direction)
                    {
                        writeContext.SetFacing(_entityId, queuedAction.direction);
                    }

                    nextState = PlayerControlQueries.StartAction(
                        state,
                        PlayerActionKind.Push,
                        pushTarget.Direction,
                        pushTarget.TargetEntityId,
                        tickIndex,
                        _pushWindupTicks,
                        _pushRecoveryTicks);
                    updates.Add(
                        $"Free2DActionAssistExecute|Stage=PreMovement|Source={_entityId}|Kind=Push|Direction={pushTarget.Direction}|Target={pushTarget.TargetEntityId}|RequestedTick={queuedAction.requestedTick}|ExecuteTick={tickIndex}");
                    return true;

                case PlayerQueuedFree2DActionKind.Flip:
                    if (!PlayerControlQueries.TryResolveFlipTarget(snapshot, entity, queuedAction.direction, tickIndex, out var flipTarget))
                    {
                        return false;
                    }

                    if (entity.facing != queuedAction.direction)
                    {
                        writeContext.SetFacing(_entityId, queuedAction.direction);
                    }

                    nextState = PlayerControlQueries.StartAction(
                        state,
                        PlayerActionKind.Flip,
                        flipTarget.Direction,
                        flipTarget.TargetEntityId,
                        tickIndex,
                        _flipWindupTicks,
                        _flipRecoveryTicks);
                    flipResultTurnTransition = CreateFlipResultTurnTransition(
                        nextState.activeAction,
                        PlayerFlipResultTurnStartReason.QueuedFlip);
                    updates.Add(
                        $"Free2DActionAssistExecute|Stage=PreMovement|Source={_entityId}|Kind=Flip|Direction={flipTarget.Direction}|Target={flipTarget.TargetEntityId}|RequestedTick={queuedAction.requestedTick}|ExecuteTick={tickIndex}");
                    return true;

                default:
                    return false;
            }
        }

        private PlayerFlipResultTurnTransition CreateFlipResultTurnTransition(
            in PlayerActionRuntimeState action,
            PlayerFlipResultTurnStartReason reason)
        {
            if (!action.IsActive ||
                action.kind != PlayerActionKind.Flip ||
                !DirectionUtility.IsCardinal(action.direction))
            {
                return default;
            }

            var contactFacing = action.direction;
            var resultFacing = DirectionUtility.Opposite(action.direction);
            if (!DirectionUtility.IsCardinal(resultFacing) ||
                contactFacing == resultFacing)
            {
                return default;
            }

            return new PlayerFlipResultTurnTransition(
                _entityId,
                action.sequence,
                action.direction,
                contactFacing,
                resultFacing,
                action.startTick,
                reason);
        }

        private void CommitFlipResultFacingOnExecute(
            in PlayerActionRuntimeState previousAction,
            in PlayerActionRuntimeState currentAction,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (previousAction.kind != PlayerActionKind.Flip ||
                previousAction.executionAttempted ||
                !currentAction.IsActive ||
                currentAction.sequence != previousAction.sequence ||
                !currentAction.executionAttempted)
            {
                return;
            }

            var resultFacing = DirectionUtility.Opposite(previousAction.direction);
            if (!DirectionUtility.IsCardinal(resultFacing))
            {
                return;
            }

            writeContext.SetFacing(_entityId, resultFacing);
            updates.Add(
                $"FlipResultFacingCommitted|Stage=PreMovement|Source={_entityId}|ActionSeq={previousAction.sequence}|ActionDirection={previousAction.direction}|ResultFacing={resultFacing}");
        }

        private void UpdateMovementOwnedPhasedState(
            WorldSnapshot snapshot,
            int tickIndex,
            in PlayerActionRuntimeState action,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (writeContext is not IPhasedStateCommitContext phasedWriteContext)
            {
                throw new InvalidOperationException("Pre-movement write contexts must support phased runtime writes.");
            }

            var hasCurrentPhasedState = snapshot.TryGetPhasedState(_entityId, out var currentPhasedState) &&
                                        currentPhasedState.IsActive;
            var ownsMovementPhasedState = hasCurrentPhasedState &&
                                          currentPhasedState.ownerKind == PhasedRuntimeStateOwnerKind.MovementPreMovement;
            var shouldOwnMovementPhase = ShouldOwnMovementPhase(action);

            if (shouldOwnMovementPhase)
            {
                if (hasCurrentPhasedState &&
                    !ownsMovementPhasedState)
                {
                    throw new InvalidOperationException(
                        $"Entity {_entityId} cannot enter movement-owned phased state while owner {currentPhasedState.ownerKind} is still active.");
                }

                if (ownsMovementPhasedState)
                {
                    return;
                }

                phasedWriteContext.SetPhasedState(
                    _entityId,
                    PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex));
                updates.Add(
                    $"PhaseEnter|Entity={_entityId}|Tick={tickIndex}|Owner={PhasedRuntimeStateOwnerKind.MovementPreMovement}|Rule=PlayerFlipWindup");
                return;
            }

            if (!ownsMovementPhasedState)
            {
                return;
            }

            phasedWriteContext.SetPhasedState(_entityId, PhasedRuntimeStateQueries.Clear());
            updates.Add(
                $"PhaseExit|Entity={_entityId}|Tick={tickIndex}|Owner={PhasedRuntimeStateOwnerKind.MovementPreMovement}|Reason=PlayerFlipWindupEnded");
        }

        private static bool ShouldOwnMovementPhase(in PlayerActionRuntimeState action)
        {
            return action.kind == PlayerActionKind.Flip &&
                   action.IsActive &&
                   !action.executionAttempted;
        }

        private static bool AreEqual(
            in PlayerControlState left,
            in PlayerControlState right)
        {
            return left.moveCooldownTicks == right.moveCooldownTicks &&
                   left.nextMoveAllowedTick == right.nextMoveAllowedTick &&
                   left.actionSequenceCounter == right.actionSequenceCounter &&
                   AreEqual(left.activeAction, right.activeAction) &&
                   left.queuedKinematicTurnDirection == right.queuedKinematicTurnDirection &&
                   AreEqual(left.queuedFree2DAction, right.queuedFree2DAction);
        }

        private static bool AreEqual(
            in PlayerActionRuntimeState left,
            in PlayerActionRuntimeState right)
        {
            return left.kind == right.kind &&
                   left.sequence == right.sequence &&
                   left.direction == right.direction &&
                   left.targetEntityId == right.targetEntityId &&
                   left.startTick == right.startTick &&
                   left.executeTick == right.executeTick &&
                   left.recoveryEndTick == right.recoveryEndTick &&
                   left.executionAttempted == right.executionAttempted;
        }

        private static bool AreEqual(
            in PlayerQueuedFree2DActionState left,
            in PlayerQueuedFree2DActionState right)
        {
            return left.kind == right.kind &&
                   left.direction == right.direction &&
                   left.requestedTick == right.requestedTick;
        }
    }
}
