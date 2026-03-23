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

        public static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            return new WorldState(initialEntities);
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
