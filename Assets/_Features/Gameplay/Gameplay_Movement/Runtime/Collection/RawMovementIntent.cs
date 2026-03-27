using UnityEngine;

namespace Game.Feature.Gameplay.Movement
{
    public enum MovementCommandKind
    {
        Interact = 0,
        Throw = 1,
        Move = 2,
    }
}

namespace Game.Feature.Gameplay.Movement.Collection
{
    using Game.Feature.Gameplay.Movement;

    public readonly struct RawMovementIntent
    {
        public RawMovementIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero, MovementCommandKind.Move, 0)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination)
            : this(sourceId, priority, destination, MovementCommandKind.Move, 0)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination, MovementCommandKind commandKind)
            : this(sourceId, priority, destination, commandKind, 0)
        {
        }

        public RawMovementIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            MovementCommandKind commandKind,
            int localSequence)
        {
            SourceId = sourceId;
            Priority = priority;
            Destination = destination;
            CommandKind = commandKind;
            LocalSequence = localSequence;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public Vector2Int Destination { get; }

        public MovementCommandKind CommandKind { get; }

        public int LocalSequence { get; }
    }
}
