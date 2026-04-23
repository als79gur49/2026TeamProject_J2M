using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyChargePhase
    {
        None = 0,
        Windup = 1,
        Active = 2,
        Recover = 3,
    }

    public struct EnemyChargeRuntimeState
    {
        public EnemyChargePhase phase;
        public int sequence;
        public Direction lockedDirection;
        public int windupEndTick;
        public int remainingActiveSteps;
        public int recoverRemainingTicks;

        public bool IsActive => phase != EnemyChargePhase.None;
    }

    internal readonly struct EnemyChargeSnapshotEntry
    {
        public EnemyChargeSnapshotEntry(int entityId, EnemyChargeRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemyChargeRuntimeState State { get; }
    }

    internal static class EnemyChargeQueries
    {
        public static EnemyChargeRuntimeState StartCharge(
            in EnemyChargeRuntimeState previousState,
            Direction lockedDirection,
            int tickIndex,
            in EnemyChargeTimingSettings timingSettings,
            int reachableSteps)
        {
            timingSettings.Validate(nameof(timingSettings));

            return new EnemyChargeRuntimeState
            {
                phase = timingSettings.WindupTicks == 0
                    ? EnemyChargePhase.Active
                    : EnemyChargePhase.Windup,
                sequence = Math.Max(1, previousState.sequence + 1),
                lockedDirection = lockedDirection,
                windupEndTick = tickIndex + timingSettings.WindupTicks,
                remainingActiveSteps = Math.Max(0, reachableSteps),
                recoverRemainingTicks = 0,
            };
        }

        public static EnemyChargeRuntimeState BeginActive(in EnemyChargeRuntimeState state)
        {
            var updatedState = state;
            updatedState.phase = EnemyChargePhase.Active;
            return updatedState;
        }

        public static EnemyChargeRuntimeState ConsumeActiveStep(in EnemyChargeRuntimeState state)
        {
            var updatedState = state;
            updatedState.remainingActiveSteps = Math.Max(0, updatedState.remainingActiveSteps - 1);
            return updatedState;
        }

        public static EnemyChargeRuntimeState EnterRecover(in EnemyChargeRuntimeState state, int recoverTicks)
        {
            var updatedState = state;
            updatedState.phase = EnemyChargePhase.Recover;
            updatedState.recoverRemainingTicks = Math.Max(0, recoverTicks);
            return updatedState;
        }

        public static EnemyChargeRuntimeState TickRecover(in EnemyChargeRuntimeState state)
        {
            var updatedState = state;
            updatedState.recoverRemainingTicks = Math.Max(0, updatedState.recoverRemainingTicks - 1);
            return updatedState;
        }

        public static EnemyChargeRuntimeState Clear(in EnemyChargeRuntimeState state)
        {
            return new EnemyChargeRuntimeState
            {
                sequence = state.sequence,
            };
        }
    }
}
