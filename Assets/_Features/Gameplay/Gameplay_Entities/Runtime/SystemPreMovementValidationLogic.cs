using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class SystemPreMovementValidationLogic : IPreMovementStateLogic, IEntityLogicSourceBinding
    {
        internal const string RuleLabel = "SystemPreMovementValidationWindow";

        private readonly int _entityId;
        private readonly int _enterTick;
        private readonly int _exitTickExclusive;

        public SystemPreMovementValidationLogic(int entityId, int enterTick, int exitTickExclusive)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Validation owner requires a positive entity ID.");
            }

            if (enterTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enterTick), "Validation enter tick must be non-negative.");
            }

            if (exitTickExclusive <= enterTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exitTickExclusive),
                    "Validation exit tick must be greater than the enter tick.");
            }

            _entityId = entityId;
            _enterTick = enterTick;
            _exitTickExclusive = exitTickExclusive;
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

            if (writeContext is not IPhasedStateCommitContext phasedWriteContext)
            {
                throw new InvalidOperationException("Pre-movement write contexts must support phased runtime writes.");
            }

            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            if (actionTransitions == null)
            {
                throw new ArgumentNullException(nameof(actionTransitions));
            }

            var hasCurrentPhasedState = snapshot.TryGetPhasedState(_entityId, out var currentPhasedState) &&
                                        currentPhasedState.IsActive;
            var ownsValidationPhase = hasCurrentPhasedState &&
                                      currentPhasedState.ownerKind == PhasedRuntimeStateOwnerKind.SystemPreMovementValidation;

            var shouldOwnValidationPhase = ShouldOwnValidationPhase(snapshot, input.TickIndex, out var exitReason);
            if (shouldOwnValidationPhase)
            {
                if (hasCurrentPhasedState &&
                    !ownsValidationPhase)
                {
                    throw new InvalidOperationException(
                        $"Entity {_entityId} cannot enter system validation phased state while owner {currentPhasedState.ownerKind} is still active.");
                }

                if (ownsValidationPhase)
                {
                    return;
                }

                phasedWriteContext.SetPhasedState(
                    _entityId,
                    PhasedRuntimeStateQueries.BeginSystemPreMovementValidation(default, input.TickIndex));
                updates.Add(
                    $"PhaseEnter|Entity={_entityId}|Tick={input.TickIndex}|Owner={PhasedRuntimeStateOwnerKind.SystemPreMovementValidation}|Rule={RuleLabel}");
                return;
            }

            if (!ownsValidationPhase)
            {
                return;
            }

            phasedWriteContext.SetPhasedState(_entityId, PhasedRuntimeStateQueries.Clear());
            updates.Add(
                $"PhaseExit|Entity={_entityId}|Tick={input.TickIndex}|Owner={PhasedRuntimeStateOwnerKind.SystemPreMovementValidation}|Reason={exitReason}");
        }

        private bool ShouldOwnValidationPhase(
            WorldSnapshot snapshot,
            int tickIndex,
            out string exitReason)
        {
            exitReason = "ValidationWindowClosed";

            if (!snapshot.TryGetEntity(_entityId, out var source))
            {
                exitReason = "SourceMissing";
                return false;
            }

            if (source.hp <= 0)
            {
                exitReason = "ForcedCancelHpZero";
                return false;
            }

            if (source.markedForDeath)
            {
                exitReason = "ForcedCancelMarkedForDeath";
                return false;
            }

            if (source.boardPresence != EntityBoardPresence.Occupying)
            {
                exitReason = "ForcedCancelBoardPresence";
                return false;
            }

            if (snapshot.TryGetEnemyJumpState(_entityId, out var jumpState) &&
                jumpState.phase != EnemyJumpPhase.None)
            {
                exitReason = "ForcedCancelMutuallyExclusiveState";
                return false;
            }

            if (tickIndex < _enterTick ||
                tickIndex >= _exitTickExclusive)
            {
                exitReason = "ValidationWindowClosed";
                return false;
            }

            return true;
        }
    }
}
