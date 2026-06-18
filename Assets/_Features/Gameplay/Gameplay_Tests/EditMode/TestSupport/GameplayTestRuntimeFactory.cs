using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Tests
{
    public static class PlayerFree2DTestSettingsFactory
    {
        public static PlayerFree2DLocomotionSettings CreateDefault(
            int simulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond)
        {
            return PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(simulationTicksPerSecond);
        }

        public static PlayerFree2DLocomotionSettings CreateOrDefault(
            PlayerFree2DLocomotionSettings playerFree2DLocomotion,
            int simulationTicksPerSecond)
        {
            return playerFree2DLocomotion.IsConfigured
                ? playerFree2DLocomotion
                : CreateDefault(simulationTicksPerSecond);
        }
    }

    public static class GameplayTestRuntimeFactory
    {
        public static GameplayTestBootstrapper CreateDefaultBootstrapper()
        {
            return new GameplayTestBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault());
        }

        public static GameplayTestBootstrapper CreateDefaultBootstrapper(EnemyAiProfile enemyAiProfile)
        {
            return new GameplayTestBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault(enemyAiProfile));
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
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            PlayerFree2DLocomotionSettings playerFree2DLocomotion = default)
        {
            return CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition,
                runtimeFeatureFlags: runtimeFeatureFlags,
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming,
                playerFree2DLocomotion: playerFree2DLocomotion);
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
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            PlayerFree2DLocomotionSettings playerFree2DLocomotion = default,
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
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming,
                playerFree2DLocomotion: playerFree2DLocomotion,
                demoGameplayOverrideSnapshotSource: demoGameplayOverrideSnapshotSource);
        }
    }

    public sealed class GameplayTestBootstrapper
    {
        private readonly GameplayBootstrapper _bootstrapper;

        public GameplayTestBootstrapper(ISnapshotEntityLogicProvider entityLogicProvider)
            : this(entityLogicProvider, null)
        {
        }

        public GameplayTestBootstrapper(
            ISnapshotEntityLogicProvider entityLogicProvider,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            _bootstrapper = new GameplayBootstrapper(entityLogicProvider, spawnDefaultsByArchetypeId);
        }

        public TickPipeline CreateTickPipeline(WorldState worldState)
        {
            return CreateTickPipeline(worldState, Array.Empty<IEntityLogic>());
        }

        public TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics)
        {
            var generalTimingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = CreateDefaultPlayerControlTimingSnapshot(generalTimingProfile);
            var unitKinematicLocomotionTiming = CreateDefaultUnitKinematicLocomotionTimingSnapshot(generalTimingProfile);
            var playerFree2DLocomotion = PlayerFree2DTestSettingsFactory.CreateDefault(
                generalTimingProfile.SimulationTicksPerSecond);
            var playerRespawnDelayTicks = CreateDefaultPlayerRespawnDelayTicks(generalTimingProfile);
            return CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming,
                playerFree2DLocomotion: playerFree2DLocomotion);
        }

        public TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            bool allowPlayerRespawn = true,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            PlayerFree2DLocomotionSettings playerFree2DLocomotion = default,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<MoonBlockRespawnDefinition> moonBlockRespawnDefinitions = null)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return _bootstrapper.CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                PlayerFree2DTestSettingsFactory.CreateOrDefault(
                    playerFree2DLocomotion,
                    generalTimingProfile.SimulationTicksPerSecond),
                playerRespawnDelayTicks,
                objectiveDefinition,
                allowPlayerRespawn,
                runtimeFeatureFlags,
                unitKinematicLocomotionTiming,
                tileFeatureDefinitions,
                moonBlockRespawnDefinitions);
        }

        public TickRunner CreateTickRunner(
            WorldState worldState,
            TickInputBuffer inputBuffer)
        {
            return CreateTickRunner(worldState, Array.Empty<IEntityLogic>(), inputBuffer);
        }

        public TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            int startTickIndex = 1,
            IDemoGameplayOverrideSnapshotSource demoGameplayOverrideSnapshotSource = null)
        {
            var generalTimingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = CreateDefaultPlayerControlTimingSnapshot(generalTimingProfile);
            var unitKinematicLocomotionTiming = CreateDefaultUnitKinematicLocomotionTimingSnapshot(generalTimingProfile);
            var playerFree2DLocomotion = PlayerFree2DTestSettingsFactory.CreateDefault(
                generalTimingProfile.SimulationTicksPerSecond);
            var playerRespawnDelayTicks = CreateDefaultPlayerRespawnDelayTicks(generalTimingProfile);
            return CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition: null,
                startTickIndex: startTickIndex,
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming,
                playerFree2DLocomotion: playerFree2DLocomotion,
                demoGameplayOverrideSnapshotSource: demoGameplayOverrideSnapshotSource);
        }

        public TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            int startTickIndex = 1,
            bool allowPlayerRespawn = true,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            PlayerFree2DLocomotionSettings playerFree2DLocomotion = default,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<MoonBlockRespawnDefinition> moonBlockRespawnDefinitions = null,
            IDemoGameplayOverrideSnapshotSource demoGameplayOverrideSnapshotSource = null)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return _bootstrapper.CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                PlayerFree2DTestSettingsFactory.CreateOrDefault(
                    playerFree2DLocomotion,
                    generalTimingProfile.SimulationTicksPerSecond),
                playerRespawnDelayTicks,
                objectiveDefinition,
                startTickIndex,
                allowPlayerRespawn,
                runtimeFeatureFlags,
                unitKinematicLocomotionTiming,
                tileFeatureDefinitions,
                moonBlockRespawnDefinitions,
                demoGameplayOverrideSnapshotSource);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile generalTimingProfile)
        {
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static UnitKinematicLocomotionTimingSnapshot CreateDefaultUnitKinematicLocomotionTimingSnapshot(
            GameplayTimingProfile generalTimingProfile)
        {
            return UnitKinematicLocomotionTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond);
        }

        private static int CreateDefaultPlayerRespawnDelayTicks(GameplayTimingProfile generalTimingProfile)
        {
            return GameplayTimingProfile.SecondsToCeilTicks(
                GameplayTimingProfile.DefaultPlayerRespawnDelaySeconds,
                generalTimingProfile.SimulationTicksPerSecond);
        }
    }
}
