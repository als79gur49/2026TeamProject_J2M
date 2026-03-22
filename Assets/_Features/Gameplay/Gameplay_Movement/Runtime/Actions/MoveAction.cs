using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Actions
{
    public readonly struct MoveAction
    {
        public MoveAction(int entityId, Vector2Int destination, Direction facing)
        {
            EntityId = entityId;
            Destination = destination;
            Facing = facing;
        }

        public int EntityId { get; }

        public Vector2Int Destination { get; }

        public Direction Facing { get; }
    }
}
