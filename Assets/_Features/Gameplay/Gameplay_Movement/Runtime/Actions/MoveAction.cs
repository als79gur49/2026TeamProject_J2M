using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Actions
{
    public readonly struct MoveAction
    {
        public MoveAction(int entityId, Vector2Int destination)
        {
            EntityId = entityId;
            Destination = destination;
        }

        public int EntityId { get; }

        public Vector2Int Destination { get; }
    }
}
