using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Loop
{
    public static class GameplayCompositionRoot
    {
        public static GameplayBootstrapper CreateDefaultBootstrapper()
        {
            return new GameplayBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault());
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException(
                    "Runtime world creation requires bounded board bounds. Use the legacy unbounded helper only from tests or compatibility paths.");
            }

            return new WorldState(initialEntities, boardBounds, terrainData ?? throw new ArgumentNullException(nameof(terrainData)));
        }

        internal static WorldState CreateLegacyUnboundedWorldState(IEnumerable<EntityState> initialEntities)
        {
            return CreateLegacyUnboundedWorldState(initialEntities, TerrainData.Empty);
        }

        internal static WorldState CreateLegacyUnboundedWorldState(
            IEnumerable<EntityState> initialEntities,
            TerrainData terrainData)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            return new WorldState(initialEntities, BoardBounds.Unbounded, terrainData ?? throw new ArgumentNullException(nameof(terrainData)));
        }

        public static TickPipeline CreateTickPipeline(WorldState worldState)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(worldState);
        }

        public static TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(worldState, entityLogics);
        }

        public static TickRunner CreateTickRunner(
            WorldState worldState,
            TickInputBuffer inputBuffer)
        {
            return CreateDefaultBootstrapper().CreateTickRunner(worldState, inputBuffer);
        }

        public static TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            int startTickIndex = 1)
        {
            return CreateDefaultBootstrapper().CreateTickRunner(worldState, entityLogics, inputBuffer, startTickIndex);
        }
    }
}
