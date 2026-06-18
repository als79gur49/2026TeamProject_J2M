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

        public TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            PlayerFree2DLocomotionSettings playerFree2DLocomotionSettings,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            bool allowPlayerRespawn = true,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<MoonBlockRespawnDefinition> moonBlockRespawnDefinitions = null)
        {
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
                playerFree2DLocomotionSettings,
                tileFeatureDefinitions,
                moonBlockRespawnDefinitions,
                tileEffectResolver: null);
        }

        public TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            PlayerFree2DLocomotionSettings playerFree2DLocomotionSettings,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            int startTickIndex = 1,
            bool allowPlayerRespawn = true,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
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
                    playerFree2DLocomotionSettings,
                    playerRespawnDelayTicks,
                    objectiveDefinition,
                    allowPlayerRespawn,
                    runtimeFeatureFlags,
                    unitKinematicLocomotionTiming,
                    tileFeatureDefinitions,
                    moonBlockRespawnDefinitions),
                inputBuffer,
                startTickIndex,
                demoGameplayOverrideSnapshotSource);
        }
    }
}
