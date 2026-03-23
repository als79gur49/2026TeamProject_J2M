using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct SnapshotOccupancyEntry
    {
        public SnapshotOccupancyEntry(Vector2Int cell, int entityId)
        {
            Cell = cell;
            EntityId = entityId;
        }

        public Vector2Int Cell { get; }

        public int EntityId { get; }
    }
}
