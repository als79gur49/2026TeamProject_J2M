using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Intents
{
    public class MoveIntent : Intent
    {
        public MoveIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero, 0)
        {
        }

        public MoveIntent(int sourceId, int priority, Vector2Int destination)
            : this(sourceId, priority, destination, 0)
        {
        }

        public MoveIntent(int sourceId, int priority, Vector2Int destination, int localSequence = 0)
            : this(sourceId, priority, destination, MovementCommandKind.Move, localSequence)
        {
        }

        protected MoveIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            MovementCommandKind commandKind,
            int localSequence)
            : base(sourceId, priority, TickPhase.Movement)
        {
            Destination = destination;
            CommandKind = commandKind;
            LocalSequence = localSequence;
        }

        public Vector2Int Destination { get; }

        public MovementCommandKind CommandKind { get; }

        public int LocalSequence { get; }

        protected internal override int GetTypeSortKey()
        {
            return 2;
        }

        protected internal override bool TryGetTargetCell(out Vector2Int targetCell)
        {
            targetCell = Destination;
            return true;
        }

        protected internal override int GetLocalSequence()
        {
            return LocalSequence;
        }

        protected internal override int CompareSameType(Intent other)
        {
            return 0;
        }
    }

    public sealed class InteractMoveIntent : MoveIntent
    {
        public InteractMoveIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            int localSequence = 0)
            : base(sourceId, priority, destination, MovementCommandKind.Push, localSequence)
        {
        }

        protected internal override int GetTypeSortKey()
        {
            return 0;
        }
    }

    public class FlipIntent : MoveIntent
    {
        public FlipIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            int localSequence = 0)
            : base(sourceId, priority, destination, MovementCommandKind.Flip, localSequence)
        {
        }

        protected internal override int GetTypeSortKey()
        {
            return 1;
        }
    }

    public sealed class ThrowIntent : FlipIntent
    {
        public ThrowIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            int localSequence = 0)
            : base(sourceId, priority, destination, localSequence)
        {
        }
    }
}
