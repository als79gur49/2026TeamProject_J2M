using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Intents
{
    public sealed class MoveIntent : Intent
    {
        public MoveIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero, MovementCommandKind.Move)
        {
        }

        public MoveIntent(int sourceId, int priority, Vector2Int destination)
            : this(sourceId, priority, destination, MovementCommandKind.Move)
        {
        }

        public MoveIntent(int sourceId, int priority, Vector2Int destination, MovementCommandKind commandKind)
            : base(sourceId, priority, TickPhase.Movement)
        {
            Destination = destination;
            CommandKind = commandKind;
        }

        public Vector2Int Destination { get; }

        public MovementCommandKind CommandKind { get; }

        protected internal override int GetTypeSortKey()
        {
            return 0;
        }

        protected internal override int CompareSameType(Intent other)
        {
            var otherMove = (MoveIntent)other;

            var result = ((int)CommandKind).CompareTo((int)otherMove.CommandKind);
            if (result != 0)
            {
                return result;
            }

            result = Destination.x.CompareTo(otherMove.Destination.x);
            if (result != 0)
            {
                return result;
            }

            return Destination.y.CompareTo(otherMove.Destination.y);
        }
    }
}
