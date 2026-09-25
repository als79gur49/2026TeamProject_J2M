using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemySummonExecutor
    {
        private const int SummonBehaviorCompatibilitySourceEffectIndex = 0;

        public static bool ShouldSuppressActive(
            WorldSnapshot snapshot,
            in EntityState source,
            int entityId,
            EnemySummonBehaviorRuntime summonBehavior,
            int tickIndex)
        {
            if (summonBehavior == null ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, source) ||
                !snapshot.TryGetEnemySummonBehaviorState(entityId, out var state))
            {
                return false;
            }

            return IsSummonBehaviorMovementSuppressionWindowActive(summonBehavior, state, tickIndex);
        }

        public static bool ShouldSuppressImminent(
            WorldSnapshot snapshot,
            in EntityState source,
            int entityId,
            EnemySummonBehaviorRuntime summonBehavior,
            int tickIndex)
        {
            if (summonBehavior == null ||
                !summonBehavior.SuppressMovementDuringWindup ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, source))
            {
                return false;
            }

            var state = GetSummonBehaviorStateForStartPrediction(snapshot, entityId, summonBehavior);
            if (!CanStartDelayedSummonBehaviorWindupThisTick(state))
            {
                return false;
            }

            var summonedEntries = new List<SummonedEntitySnapshotEntry>();
            snapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
            return !EnemySummonChildLimitPolicy.IsMaxAliveReached(
                snapshot,
                summonedEntries,
                source.entityId,
                SummonBehaviorCompatibilitySourceEffectIndex,
                summonBehavior.Summon);
        }

        private static EnemySummonBehaviorRuntimeState GetSummonBehaviorStateForStartPrediction(
            WorldSnapshot snapshot, int entityId, EnemySummonBehaviorRuntime summonBehavior)
        {
            return snapshot.TryGetEnemySummonBehaviorState(entityId, out var state)
                ? state
                : CreateInitialSummonBehaviorState(summonBehavior);
        }

        private static bool CanStartDelayedSummonBehaviorWindupThisTick(
            in EnemySummonBehaviorRuntimeState state)
        {
            if (state.phase != EnemySummonBehaviorPhase.None ||
                state.movementSuppressionUntilTickInclusive > 0)
            {
                return false;
            }

            var nextCooldownTicks = state.cooldownTicksRemaining > 0
                ? Mathf.Max(0, state.cooldownTicksRemaining - 1)
                : 0;
            return nextCooldownTicks == 0;
        }

        public static void Commit(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            int entityId,
            EnemySummonBehaviorRuntime summonBehavior,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            var canParticipateOnCurrentTopology = EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source);
            var isHardInvalidParticipant = EnemyParticipationPolicy.IsHardInvalidParticipant(source);
            var isControllableParticipant = canParticipateOnCurrentTopology && !isHardInvalidParticipant;
            var hasCurrentState = snapshot.TryGetEnemySummonBehaviorState(entityId, out var currentState);
            var initializedState = false;
            if (!hasCurrentState && isControllableParticipant)
            {
                currentState = CreateInitialSummonBehaviorState(summonBehavior);
                hasCurrentState = true;
                initializedState = true;
                updates.Add($"EnemySummonBehaviorInitialized|E={entityId}");
            }

            if (!hasCurrentState)
            {
                return;
            }

            if (!canParticipateOnCurrentTopology &&
                !isHardInvalidParticipant)
            {
                SuspendEnemySummonBehaviorForTopologyParticipationLoss(entityId, currentState, writeContext, updates);
                return;
            }

            if (isHardInvalidParticipant)
            {
                Cancel(currentState, entityId, summonBehavior, writeContext, updates);
                return;
            }

            var previousState = currentState;
            var nextState = previousState;
            var triggered = false;
            if (nextState.movementSuppressionUntilTickInclusive > 0 &&
                input.TickIndex > nextState.movementSuppressionUntilTickInclusive)
            {
                nextState.movementSuppressionUntilTickInclusive = 0;
            }

            if (nextState.phase == EnemySummonBehaviorPhase.Recover)
            {
                AdvanceSummonBehaviorRecover(summonBehavior, input.TickIndex, ref nextState);
                if (!AreEqual(previousState, nextState))
                {
                    updates.Add(
                        $"EnemySummonBehaviorRecoverUpdated|E={entityId}|Phase={nextState.phase}|RecoverStart={nextState.recoverStartTick}|RecoverEnd={nextState.recoverEndTickExclusive}|Cooldown={nextState.cooldownTicksRemaining}");
                }

                WriteSummonBehaviorStateIfChanged(entityId, initializedState, previousState, nextState, writeContext);
                return;
            }

            if (nextState.phase == EnemySummonBehaviorPhase.Windup)
            {
                if (input.TickIndex >= nextState.windupEndTick)
                {
                    triggered = true;
                    EmitEnemySummonBehaviorTriggerIntent(entityId, summonBehavior, writeContext, source, input.TickIndex);
                    EnterSummonBehaviorRecoverOrClear(summonBehavior, input.TickIndex, ref nextState);
                    updates.Add(
                        $"EnemySummonBehaviorWindupCommitted|E={entityId}|Effect={SummonBehaviorCompatibilitySourceEffectIndex}|Sequence={nextState.activationSequence}|Tick={input.TickIndex}");
                }
            }
            else
            {
                if (nextState.cooldownTicksRemaining > 0)
                {
                    nextState.cooldownTicksRemaining = Mathf.Max(0, nextState.cooldownTicksRemaining - 1);
                }

                if (nextState.cooldownTicksRemaining == 0)
                {
                    var summonedEntries = new List<SummonedEntitySnapshotEntry>();
                    snapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
                    if (!EnemySummonChildLimitPolicy.IsMaxAliveReached(
                            snapshot,
                            summonedEntries,
                            source.entityId,
                            SummonBehaviorCompatibilitySourceEffectIndex,
                            summonBehavior.Summon))
                    {
                        nextState.phase = EnemySummonBehaviorPhase.Windup;
                        nextState.windupStartTick = input.TickIndex;
                        nextState.windupEndTick = input.TickIndex + summonBehavior.WindupTicks;
                        nextState.recoverStartTick = 0;
                        nextState.recoverEndTickExclusive = 0;
                        nextState.activationSequence = Math.Max(0, nextState.activationSequence) + 1;
                        if (summonBehavior.SuppressMovementDuringWindup)
                        {
                            nextState.movementSuppressionUntilTickInclusive = nextState.windupEndTick;
                        }

                        updates.Add(
                            $"EnemySummonBehaviorWindupStarted|E={entityId}|Effect={SummonBehaviorCompatibilitySourceEffectIndex}|Sequence={nextState.activationSequence}|Start={nextState.windupStartTick}|End={nextState.windupEndTick}");
                    }
                }
            }

            if (!AreEqual(previousState, nextState))
            {
                updates.Add(
                    $"EnemySummonBehaviorCooldownUpdated|E={entityId}|Effect={SummonBehaviorCompatibilitySourceEffectIndex}|From={previousState.cooldownTicksRemaining}|To={nextState.cooldownTicksRemaining}|Triggered={(triggered ? 1 : 0)}");
            }

            WriteSummonBehaviorStateIfChanged(entityId, initializedState, previousState, nextState, writeContext);
        }

        private static EnemySummonBehaviorRuntimeState CreateInitialSummonBehaviorState(
            EnemySummonBehaviorRuntime summonBehavior)
        {
            return new EnemySummonBehaviorRuntimeState
            {
                cooldownTicksRemaining = summonBehavior.InitialDelayTicks,
            };
        }

        private static void WriteSummonBehaviorStateIfChanged(
            int entityId,
            bool initializedState,
            in EnemySummonBehaviorRuntimeState previousState,
            in EnemySummonBehaviorRuntimeState nextState,
            IPreMovementStateCommitContext writeContext)
        {
            if (initializedState || !AreEqual(previousState, nextState))
            {
                writeContext.SetEnemySummonBehaviorState(entityId, nextState);
            }
        }

        private static void SuspendEnemySummonBehaviorForTopologyParticipationLoss(
            int entityId,
            EnemySummonBehaviorRuntimeState currentState,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            var nextState = currentState;
            if (!ShiftEnemySummonBehaviorSuspendedWindow(ref nextState))
            {
                return;
            }

            updates.Add(
                $"EnemySummonBehaviorTopologySuspended|E={entityId}|Effect={SummonBehaviorCompatibilitySourceEffectIndex}|Phase={nextState.phase}|Sequence={nextState.activationSequence}|WindupEnd={nextState.windupEndTick}|RecoverEnd={nextState.recoverEndTickExclusive}|Cooldown={nextState.cooldownTicksRemaining}");
            writeContext.SetEnemySummonBehaviorState(entityId, nextState);
        }

        private static bool ShiftEnemySummonBehaviorSuspendedWindow(ref EnemySummonBehaviorRuntimeState state)
        {
            var shifted = false;
            switch (state.phase)
            {
                case EnemySummonBehaviorPhase.Windup:
                    if (state.windupEndTick > 0)
                    {
                        state.windupEndTick++;
                        shifted = true;
                    }

                    break;

                case EnemySummonBehaviorPhase.Recover:
                    if (state.recoverEndTickExclusive > 0)
                    {
                        state.recoverEndTickExclusive++;
                        shifted = true;
                    }

                    break;
            }

            if (state.movementSuppressionUntilTickInclusive > 0)
            {
                state.movementSuppressionUntilTickInclusive++;
                shifted = true;
            }

            return shifted;
        }

        public static void Cancel(
            EnemySummonBehaviorRuntimeState currentState,
            int entityId,
            EnemySummonBehaviorRuntime summonBehavior,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (currentState.phase != EnemySummonBehaviorPhase.Windup &&
                currentState.phase != EnemySummonBehaviorPhase.Recover &&
                currentState.movementSuppressionUntilTickInclusive == 0)
            {
                return;
            }

            var nextState = currentState;
            nextState.phase = EnemySummonBehaviorPhase.None;
            nextState.cooldownTicksRemaining = summonBehavior.CooldownTicks;
            nextState.windupStartTick = 0;
            nextState.windupEndTick = 0;
            nextState.recoverStartTick = 0;
            nextState.recoverEndTickExclusive = 0;
            nextState.movementSuppressionUntilTickInclusive = 0;
            updates.Add(
                $"EnemySummonBehaviorWindupCanceled|E={entityId}|Effect={SummonBehaviorCompatibilitySourceEffectIndex}|Sequence={nextState.activationSequence}|Cooldown={nextState.cooldownTicksRemaining}");
            writeContext.SetEnemySummonBehaviorState(entityId, nextState);
        }

        private static void EmitEnemySummonBehaviorTriggerIntent(
            int entityId,
            EnemySummonBehaviorRuntime summonBehavior,
            IPreMovementStateCommitContext writeContext,
            in EntityState source,
            int tickIndex)
        {
            if (writeContext is not IEnemyUtilityTriggerSink triggerSink)
            {
                return;
            }

            triggerSink.EmitEnemySummonBehaviorTriggerIntent(
                new EnemySummonBehaviorTriggerIntent(
                    entityId,
                    SummonBehaviorCompatibilitySourceEffectIndex,
                    tickIndex,
                    source.position,
                    source.facing,
                    source.teamId,
                    summonBehavior.Summon));
        }

        private static void EnterSummonBehaviorRecoverOrClear(
            EnemySummonBehaviorRuntime summonBehavior,
            int tickIndex,
            ref EnemySummonBehaviorRuntimeState state)
        {
            state.windupStartTick = 0;
            state.windupEndTick = 0;

            if (summonBehavior.RecoveryTicks <= 0)
            {
                state.phase = EnemySummonBehaviorPhase.None;
                state.recoverStartTick = 0;
                state.recoverEndTickExclusive = 0;
                state.cooldownTicksRemaining = summonBehavior.CooldownTicks;
                return;
            }

            state.phase = EnemySummonBehaviorPhase.Recover;
            state.recoverStartTick = tickIndex;
            state.recoverEndTickExclusive = tickIndex + summonBehavior.RecoveryTicks;
            state.cooldownTicksRemaining = summonBehavior.CooldownTicks;
            if (summonBehavior.SuppressMovementDuringRecover)
            {
                state.movementSuppressionUntilTickInclusive = Mathf.Max(
                    state.movementSuppressionUntilTickInclusive,
                    state.recoverEndTickExclusive - 1);
            }
        }

        private static void AdvanceSummonBehaviorRecover(
            EnemySummonBehaviorRuntime summonBehavior,
            int tickIndex,
            ref EnemySummonBehaviorRuntimeState state)
        {
            if (tickIndex >= state.recoverEndTickExclusive)
            {
                state.phase = EnemySummonBehaviorPhase.None;
                state.recoverStartTick = 0;
                state.recoverEndTickExclusive = 0;
                return;
            }

            if (state.cooldownTicksRemaining > 0)
            {
                state.cooldownTicksRemaining = Mathf.Max(0, state.cooldownTicksRemaining - 1);
            }
        }

        private static bool IsSummonBehaviorMovementSuppressionWindowActive(
            EnemySummonBehaviorRuntime summonBehavior,
            in EnemySummonBehaviorRuntimeState state,
            int tickIndex)
        {
            if (state.movementSuppressionUntilTickInclusive <= 0 ||
                tickIndex > state.movementSuppressionUntilTickInclusive)
            {
                return false;
            }

            return state.phase switch
            {
                EnemySummonBehaviorPhase.Windup => summonBehavior.SuppressMovementDuringWindup,
                EnemySummonBehaviorPhase.Recover => summonBehavior.SuppressMovementDuringRecover ||
                                                    IsSummonBehaviorWindupSuppressionWindowRemainder(summonBehavior, state, tickIndex),
                EnemySummonBehaviorPhase.None => IsSummonBehaviorWindupSuppressionWindowRemainder(summonBehavior, state, tickIndex),
                _ => false,
            };
        }

        private static bool IsSummonBehaviorWindupSuppressionWindowRemainder(
            EnemySummonBehaviorRuntime summonBehavior,
            in EnemySummonBehaviorRuntimeState state,
            int tickIndex)
        {
            return summonBehavior.SuppressMovementDuringWindup &&
                   tickIndex == state.movementSuppressionUntilTickInclusive;
        }

        private static bool AreEqual(
            EnemySummonBehaviorRuntimeState left,
            EnemySummonBehaviorRuntimeState right)
        {
            return left.cooldownTicksRemaining == right.cooldownTicksRemaining &&
                   left.phase == right.phase &&
                   left.windupStartTick == right.windupStartTick &&
                   left.windupEndTick == right.windupEndTick &&
                   left.recoverStartTick == right.recoverStartTick &&
                   left.recoverEndTickExclusive == right.recoverEndTickExclusive &&
                   left.activationSequence == right.activationSequence &&
                   left.movementSuppressionUntilTickInclusive == right.movementSuppressionUntilTickInclusive;
        }

    }
}
