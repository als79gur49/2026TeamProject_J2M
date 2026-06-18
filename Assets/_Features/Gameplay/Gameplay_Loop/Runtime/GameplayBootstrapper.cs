using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class GameplayBootstrapper
    {
        private readonly ISnapshotEntityLogicProvider _entityLogicProvider;
        private readonly IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> _spawnDefaultsByArchetypeId;

        public GameplayBootstrapper(ISnapshotEntityLogicProvider entityLogicProvider)
            : this(entityLogicProvider, null)
        {
        }

        public GameplayBootstrapper(
            ISnapshotEntityLogicProvider entityLogicProvider,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            _entityLogicProvider = entityLogicProvider ?? throw new ArgumentNullException(nameof(entityLogicProvider));
            _spawnDefaultsByArchetypeId = spawnDefaultsByArchetypeId;
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
            var playerFree2DLocomotion = CreateDefaultPlayerFree2DLocomotionSettings(generalTimingProfile);
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
            var resolvedPlayerFree2DLocomotion = playerFree2DLocomotion.IsConfigured
                ? playerFree2DLocomotion
                : CreateDefaultPlayerFree2DLocomotionSettings(generalTimingProfile);

            return new TickPipeline(
                worldState,
                entityLogics,
                _entityLogicProvider,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition,
                _spawnDefaultsByArchetypeId,
                allowPlayerRespawn,
                runtimeFeatureFlags,
                unitKinematicLocomotionTiming,
                resolvedPlayerFree2DLocomotion,
                tileFeatureDefinitions,
                moonBlockRespawnDefinitions,
                tileEffectResolver: null);
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
            var playerFree2DLocomotion = CreateDefaultPlayerFree2DLocomotionSettings(generalTimingProfile);
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
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            return new TickRunner(
                CreateTickPipeline(
                    worldState,
                    entityLogics,
                    generalTimingProfile,
                    playerControlTiming,
                    playerRespawnDelayTicks,
                    objectiveDefinition,
                    allowPlayerRespawn,
                    runtimeFeatureFlags,
                    unitKinematicLocomotionTiming,
                    playerFree2DLocomotion,
                    tileFeatureDefinitions,
                    moonBlockRespawnDefinitions),
                inputBuffer,
                startTickIndex,
                demoGameplayOverrideSnapshotSource);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile generalTimingProfile)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static UnitKinematicLocomotionTimingSnapshot CreateDefaultUnitKinematicLocomotionTimingSnapshot(
            GameplayTimingProfile generalTimingProfile)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return UnitKinematicLocomotionTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond);
        }

        private static PlayerFree2DLocomotionSettings CreateDefaultPlayerFree2DLocomotionSettings(
            GameplayTimingProfile generalTimingProfile)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(generalTimingProfile.SimulationTicksPerSecond);
        }

        private static int CreateDefaultPlayerRespawnDelayTicks(
            GameplayTimingProfile generalTimingProfile)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return GameplayTimingProfile.SecondsToCeilTicks(
                GameplayTimingProfile.DefaultPlayerRespawnDelaySeconds,
                generalTimingProfile.SimulationTicksPerSecond);
        }
    }
}
