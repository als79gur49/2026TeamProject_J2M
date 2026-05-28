using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Intents
{
    public class MoveIntent : Intent
    {
        public MoveIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero, 0, 0, 0)
        {
        }

        public MoveIntent(int sourceId, int priority, Vector2Int destination)
            : this(sourceId, priority, destination, 0, 0, 0)
        {
        }

        public MoveIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            int localSequence = 0,
            int moveCooldownTicks = 0,
            int ordinaryKinematicMoveTicks = 0,
            int actionStartTick = 0,
            int actionExecuteTick = 0,
            int actionDurationTicks = 0)
            : this(sourceId, priority, destination, MovementCommandKind.Move, localSequence, moveCooldownTicks, ordinaryKinematicMoveTicks, actionStartTick, actionExecuteTick, actionDurationTicks)
        {
        }

        protected MoveIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            MovementCommandKind commandKind,
            int localSequence,
            int moveCooldownTicks,
            int ordinaryKinematicMoveTicks,
            int actionStartTick = 0,
            int actionExecuteTick = 0,
            int actionDurationTicks = 0)
            : base(sourceId, priority, TickPhase.Plan)
        {
            Destination = destination;
            CommandKind = commandKind;
            LocalSequence = localSequence;
            MoveCooldownTicks = moveCooldownTicks;
            OrdinaryKinematicMoveTicks = ordinaryKinematicMoveTicks;
            ActionStartTick = actionStartTick;
            ActionExecuteTick = actionExecuteTick;
            ActionDurationTicks = actionDurationTicks;
        }

        public Vector2Int Destination { get; }

        public MovementCommandKind CommandKind { get; }

        public int LocalSequence { get; }

        public int MoveCooldownTicks { get; }

        public int OrdinaryKinematicMoveTicks { get; }

        public int ActionStartTick { get; }

        public int ActionExecuteTick { get; }

        public int ActionDurationTicks { get; }

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

    public sealed class PushIntent : MoveIntent
    {
        public PushIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            int localSequence = 0,
            int moveCooldownTicks = 0,
            int ordinaryKinematicMoveTicks = 0,
            int actionStartTick = 0,
            int actionExecuteTick = 0,
            int actionDurationTicks = 0)
            : base(sourceId, priority, destination, MovementCommandKind.Push, localSequence, moveCooldownTicks, ordinaryKinematicMoveTicks, actionStartTick, actionExecuteTick, actionDurationTicks)
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
            int localSequence = 0,
            int moveCooldownTicks = 0,
            int ordinaryKinematicMoveTicks = 0,
            int actionStartTick = 0,
            int actionExecuteTick = 0,
            int actionDurationTicks = 0)
            : base(sourceId, priority, destination, MovementCommandKind.Flip, localSequence, moveCooldownTicks, ordinaryKinematicMoveTicks, actionStartTick, actionExecuteTick, actionDurationTicks)
        {
        }

        protected internal override int GetTypeSortKey()
        {
            return 1;
        }
    }
}
