using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;
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

        protected internal override int GetTypeSortKey()
        {
            return 0;
        }

        protected internal override int CompareSameType(Intent other)
        {
            var otherMove = (MoveIntent)other;

            var result = Destination.x.CompareTo(otherMove.Destination.x);
            if (result != 0)
            {
                return result;
            }

            return Destination.y.CompareTo(otherMove.Destination.y);
        }
    }
}
