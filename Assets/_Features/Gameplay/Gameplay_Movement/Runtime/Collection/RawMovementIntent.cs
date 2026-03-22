using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Collection
{
    public readonly struct RawMovementIntent
    {
        public RawMovementIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination)
        {
            SourceId = sourceId;
            Priority = priority;
            Destination = destination;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public Vector2Int Destination { get; }
    }
}
