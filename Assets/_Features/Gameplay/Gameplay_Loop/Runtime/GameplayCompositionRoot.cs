using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Objectives;
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
                new CubeTopologyState(FaceId.Floor));
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology)
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
                topology);
        }

        public static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            return worldState.CreateSnapshot();
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
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition,
                runtimeFeatureFlags: runtimeFeatureFlags);
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
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            int startTickIndex = 1,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default)
        {
            return CreateDefaultBootstrapper().CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition,
                startTickIndex,
                runtimeFeatureFlags: runtimeFeatureFlags);
        }
    }
}
