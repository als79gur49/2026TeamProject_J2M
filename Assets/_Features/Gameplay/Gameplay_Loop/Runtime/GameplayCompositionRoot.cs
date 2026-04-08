using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public static class GameplayCompositionRoot
    {
        public static GameplayBootstrapper CreateDefaultBootstrapper()
        {
            return CreateDefaultBootstrapper(null);
        }

        public static GameplayBootstrapper CreateDefaultBootstrapper(EnemyAiProfile enemyAiProfile)
        {
            return new GameplayBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault(enemyAiProfile));
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            return CreateWorldState(
                initialEntities,
                boardBounds,
                terrainData,
                new CubeTopologyState(FaceId.Floor),
                BoardTraversalRules.Empty);
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology)
        {
            return CreateWorldState(
                initialEntities,
                boardBounds,
                terrainData,
                topology,
                BoardTraversalRules.Empty);
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology,
            BoardTraversalRules traversalRules)
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
                terrainData ?? throw new ArgumentNullException(nameof(terrainData)),
                topology,
                traversalRules ?? BoardTraversalRules.Empty);
        }

        public static TickPipeline CreateTickPipeline(WorldState worldState)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(worldState);
        }

        public static TickPipeline CreateTickPipeline(
            WorldState worldState,
            EnemyAiProfile enemyAiProfile)
        {
            return CreateDefaultBootstrapper(enemyAiProfile).CreateTickPipeline(worldState);
        }

        public static TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(worldState, entityLogics);
        }

        public static TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming);
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

        public static TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int startTickIndex = 1)
        {
            return CreateDefaultBootstrapper().CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                startTickIndex);
        }
    }
}
