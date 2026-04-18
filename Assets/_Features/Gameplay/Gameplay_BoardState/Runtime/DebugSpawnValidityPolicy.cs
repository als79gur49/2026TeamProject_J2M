using System;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class DebugSpawnValidityPolicy
    {
        public static void EnsureRepresentable(BoardBounds boardBounds, TerrainData terrainData, EntityState entity)
        {
            if (entity.entityId <= 0)
            {
                throw new InvalidOperationException("Debug spawns must use a positive entity id.");
            }

            if (boardBounds.IsBounded && !boardBounds.Contains(entity.position.PlanarPosition))
            {
                throw new InvalidOperationException(
                    $"Debug spawn entity {entity.entityId} is outside the configured board bounds at {entity.position}.");
            }

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }
        }
    }
}
