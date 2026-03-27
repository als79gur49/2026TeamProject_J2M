using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Model.Actions
{
    public readonly struct MoveAction
    {
        public MoveAction(int entityId, SurfaceCell source, SurfaceCell destination, Direction facing)
        {
            EntityId = entityId;
            SourceCell = source;
            DestinationCell = destination;
            Facing = facing;
        }

        public MoveAction(int entityId, Vector2Int source, Vector2Int destination, Direction facing)
            : this(entityId, SurfaceCell.FromPlanar(source), SurfaceCell.FromPlanar(destination), facing)
        {
        }

        public int EntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public Vector2Int Source => SourceCell.PlanarPosition;

        public Vector2Int Destination => DestinationCell.PlanarPosition;

        public Direction Facing { get; }
    }
}
