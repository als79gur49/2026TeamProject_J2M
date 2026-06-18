using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public static class GameplayCompositionRoot
    {
        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds)
        {
            return CreateWorldState(
                initialEntities,
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology)
        {
            return CreateWorldState(
                initialEntities,
                boardBounds,
                topology,
                initialTileFeatures: null);
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException(
                    "Runtime world creation requires bounded board bounds. Unbounded boards are no longer supported by the composition root.");
            }

            return new WorldState(
                initialEntities,
                boardBounds,
                topology,
                initialTileFeatures);
        }

        public static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            return worldState.CreateSnapshot();
        }
    }
}
