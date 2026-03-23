using UnityEngine;

namespace Game.Feature.Gameplay.Movement
{
    public enum MovementCommandKind
    {
        Move = 0,
        InteractSlide = 1,
    }
}

namespace Game.Feature.Gameplay.Movement.Collection
{
    using Game.Feature.Gameplay.Movement;

    public readonly struct RawMovementIntent
    {
        public RawMovementIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero, MovementCommandKind.Move)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination)
            : this(sourceId, priority, destination, MovementCommandKind.Move)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination, MovementCommandKind commandKind)
        {
            SourceId = sourceId;
            Priority = priority;
            Destination = destination;
            CommandKind = commandKind;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public Vector2Int Destination { get; }

        public MovementCommandKind CommandKind { get; }
    }
}
