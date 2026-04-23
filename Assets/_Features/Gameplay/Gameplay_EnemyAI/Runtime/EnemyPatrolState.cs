using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    public struct EnemyPatrolRuntimeState
    {
        public int sequence;
        public SurfaceCell homeCell;
        public Direction lastCommittedDirection;

        public bool IsInitialized => sequence > 0;
    }

    internal readonly struct EnemyPatrolSnapshotEntry
    {
        public EnemyPatrolSnapshotEntry(int entityId, EnemyPatrolRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemyPatrolRuntimeState State { get; }
    }

    internal static class EnemyPatrolQueries
    {
        public static EnemyPatrolRuntimeState Initialize(
            in EnemyPatrolRuntimeState previousState,
            SurfaceCell homeCell)
        {
            if (previousState.IsInitialized)
            {
                return previousState;
            }

            return new EnemyPatrolRuntimeState
            {
                sequence = Math.Max(1, previousState.sequence),
                homeCell = homeCell,
                lastCommittedDirection = Direction.None,
            };
        }

        public static EnemyPatrolRuntimeState CommitMove(
            in EnemyPatrolRuntimeState previousState,
            Direction direction)
        {
            if (direction == Direction.None)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "Committed patrol moves require a concrete direction.");
            }

            if (!previousState.IsInitialized)
            {
                throw new ArgumentException("Enemy patrol state must be initialized before committing movement.", nameof(previousState));
            }

            return new EnemyPatrolRuntimeState
            {
                sequence = Math.Max(1, previousState.sequence + 1),
                homeCell = previousState.homeCell,
                lastCommittedDirection = direction,
            };
        }
    }
}
