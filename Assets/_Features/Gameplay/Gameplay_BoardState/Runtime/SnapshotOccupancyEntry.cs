using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct SnapshotOccupancyEntry
    {
        public SnapshotOccupancyEntry(SurfaceCell cell, int entityId)
        {
            Cell = cell;
            EntityId = entityId;
        }

        public SnapshotOccupancyEntry(Vector2Int cell, int entityId)
            : this(SurfaceCell.FromPlanar(cell), entityId)
        {
        }

        public SurfaceCell Cell { get; }

        public int EntityId { get; }
    }
}
