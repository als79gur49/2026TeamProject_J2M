using UnityEngine;

namespace Game.Feature.Gameplay.Movement
{
    public enum MovementCommandKind
    {
        Push = 0,
        Flip = 1,
        Move = 2,
    }
}

namespace Game.Feature.Gameplay.Movement.Collection
{
    using Game.Feature.Gameplay.Movement;

    public readonly struct RawMovementIntent
    {
        public RawMovementIntent(int sourceId, int priority)
            : this(sourceId, priority, Vector2Int.zero, MovementCommandKind.Move, 0, 0, 0)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination)
            : this(sourceId, priority, destination, MovementCommandKind.Move, 0, 0, 0)
        {
        }

        public RawMovementIntent(int sourceId, int priority, Vector2Int destination, MovementCommandKind commandKind)
            : this(sourceId, priority, destination, commandKind, 0, 0, 0)
        {
        }

        public RawMovementIntent(
            int sourceId,
            int priority,
            Vector2Int destination,
            MovementCommandKind commandKind,
            int localSequence,
            int moveCooldownTicks = 0,
            int ordinaryKinematicMoveTicks = 0,
            int actionStartTick = 0,
            int actionExecuteTick = 0,
            int actionDurationTicks = 0)
        {
            SourceId = sourceId;
            Priority = priority;
            Destination = destination;
            CommandKind = commandKind;
            LocalSequence = localSequence;
            MoveCooldownTicks = moveCooldownTicks;
            OrdinaryKinematicMoveTicks = ordinaryKinematicMoveTicks;
            ActionStartTick = actionStartTick;
            ActionExecuteTick = actionExecuteTick;
            ActionDurationTicks = actionDurationTicks;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public Vector2Int Destination { get; }

        public MovementCommandKind CommandKind { get; }

        public int LocalSequence { get; }

        public int MoveCooldownTicks { get; }

        public int OrdinaryKinematicMoveTicks { get; }

        public int ActionStartTick { get; }

        public int ActionExecuteTick { get; }

        public int ActionDurationTicks { get; }
    }
}
