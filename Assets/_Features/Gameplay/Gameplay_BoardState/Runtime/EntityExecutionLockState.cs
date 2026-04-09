using System;

namespace Game.Feature.Gameplay.BoardState
{
    public enum EntityExecutionPhase
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        Attack = 4,
    }

    public struct EntityExecutionLockState
    {
        public EntityExecutionPhase phase;
        public int sequence;
        public int unlockTickExclusive;

        public bool IsLocked => phase != EntityExecutionPhase.None && unlockTickExclusive > 0;
    }

    internal readonly struct EntityExecutionLockSnapshotEntry
    {
        public EntityExecutionLockSnapshotEntry(int entityId, EntityExecutionLockState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EntityExecutionLockState State { get; }
    }

    internal static class EntityExecutionLockQueries
    {
        public static EntityExecutionLockState StartMoveLock(
            in EntityExecutionLockState previousState,
            int tickIndex,
            int moveOccupancyTicks)
        {
            if (moveOccupancyTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moveOccupancyTicks), "Move occupancy ticks must be zero or greater.");
            }

            return new EntityExecutionLockState
            {
                phase = EntityExecutionPhase.Move,
                sequence = Math.Max(1, previousState.sequence + 1),
                unlockTickExclusive = tickIndex + moveOccupancyTicks + 1,
            };
        }

        public static bool CanStartAction(in EntityExecutionLockState state, int tickIndex)
        {
            return !IsLocked(state, tickIndex);
        }

        public static bool CanExecuteIntent(in EntityExecutionLockState state, int tickIndex)
        {
            return !IsLocked(state, tickIndex);
        }

        public static bool CanExecuteMovementIntent(in EntityExecutionLockState state, int tickIndex)
        {
            return state.phase == EntityExecutionPhase.Move ||
                   !IsLocked(state, tickIndex);
        }

        public static bool IsLocked(in EntityExecutionLockState state, int tickIndex)
        {
            return state.phase != EntityExecutionPhase.None &&
                   tickIndex < state.unlockTickExclusive;
        }
    }
}
