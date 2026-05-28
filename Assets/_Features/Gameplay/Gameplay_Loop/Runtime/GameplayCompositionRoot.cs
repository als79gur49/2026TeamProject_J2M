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
            return CreateWorldState(
                initialEntities,
                boardBounds,
                terrainData,
                topology,
                initialTileFeatures: null);
        }

        public static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
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
                terrainData ?? throw new ArgumentNullException(nameof(terrainData)),
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
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            PlayerKinematicLocomotionTimingSnapshot playerKinematicLocomotionTiming = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition,
                runtimeFeatureFlags: runtimeFeatureFlags,
                playerKinematicLocomotionTiming: playerKinematicLocomotionTiming,
                playerContinuousLocomotion: playerContinuousLocomotion);
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
            int startTickIndex = 1,
            IDemoGameplayOverrideSnapshotSource demoGameplayOverrideSnapshotSource = null)
        {
            return CreateDefaultBootstrapper().CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                startTickIndex,
                demoGameplayOverrideSnapshotSource);
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
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            PlayerKinematicLocomotionTimingSnapshot playerKinematicLocomotionTiming = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default,
            IDemoGameplayOverrideSnapshotSource demoGameplayOverrideSnapshotSource = null)
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
                runtimeFeatureFlags: runtimeFeatureFlags,
                playerKinematicLocomotionTiming: playerKinematicLocomotionTiming,
                playerContinuousLocomotion: playerContinuousLocomotion,
                demoGameplayOverrideSnapshotSource: demoGameplayOverrideSnapshotSource);
        }
    }
}
