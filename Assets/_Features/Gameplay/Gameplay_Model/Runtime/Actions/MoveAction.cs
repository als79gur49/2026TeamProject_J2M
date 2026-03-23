using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Model.Actions
{
    public readonly struct MoveAction
    {
        public MoveAction(int entityId, Vector2Int source, Vector2Int destination, Direction facing)
        {
            EntityId = entityId;
            Source = source;
            Destination = destination;
            Facing = facing;
        }

        public int EntityId { get; }

        public Vector2Int Source { get; }

        public Vector2Int Destination { get; }

        public Direction Facing { get; }
    }
}
