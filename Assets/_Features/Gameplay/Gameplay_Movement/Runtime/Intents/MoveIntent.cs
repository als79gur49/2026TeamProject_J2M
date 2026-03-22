using Game.Feature.Gameplay.Loop;

using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Intents
{
    public sealed class MoveIntent : Intent
    {
        public MoveIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero)
        {
        }

        public MoveIntent(int sourceId, int priority, Vector2Int destination)
            : base(sourceId, priority, TickPhase.Movement)
        {
            Destination = destination;
        }

        public Vector2Int Destination { get; }
    }
}
