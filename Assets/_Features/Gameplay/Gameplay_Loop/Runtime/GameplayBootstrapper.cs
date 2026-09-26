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
            return CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming);
        }

        public TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<MoonBlockRespawnDefinition> moonBlockRespawnDefinitions = null)
        {
            return new TickPipeline(
                worldState,
                entityLogics,
                _entityLogicProvider,
                generalTimingProfile,
                playerControlTiming,
                objectiveDefinition,
                _spawnDefaultsByArchetypeId,
                runtimeFeatureFlags,
                unitKinematicLocomotionTiming,
                playerContinuousLocomotion,
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
            return CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                objectiveDefinition: null,
                startTickIndex: startTickIndex,
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming,
                demoGameplayOverrideSnapshotSource: demoGameplayOverrideSnapshotSource);
        }

        public TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            int startTickIndex = 1,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default,
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
                    objectiveDefinition,
                    runtimeFeatureFlags,
                    unitKinematicLocomotionTiming,
                    playerContinuousLocomotion,
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

    }
}
