using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class GameplayBootstrapper
    {
        private readonly ISnapshotEntityLogicProvider _entityLogicProvider;

        public GameplayBootstrapper(ISnapshotEntityLogicProvider entityLogicProvider)
        {
            _entityLogicProvider = entityLogicProvider ?? throw new ArgumentNullException(nameof(entityLogicProvider));
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
            var playerRespawnDelayTicks = CreateDefaultPlayerRespawnDelayTicks(generalTimingProfile);
            return CreateTickPipeline(
                worldState,
                entityLogics,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks);
        }

        public TickPipeline CreateTickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1)
        {
            return new TickPipeline(
                worldState,
                entityLogics,
                _entityLogicProvider,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks);
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
            int startTickIndex = 1)
        {
            var generalTimingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = CreateDefaultPlayerControlTimingSnapshot(generalTimingProfile);
            var playerRespawnDelayTicks = CreateDefaultPlayerRespawnDelayTicks(generalTimingProfile);
            return CreateTickRunner(
                worldState,
                entityLogics,
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                startTickIndex);
        }

        public TickRunner CreateTickRunner(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            TickInputBuffer inputBuffer,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1,
            int startTickIndex = 1)
        {
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            return new TickRunner(
                CreateTickPipeline(worldState, entityLogics, generalTimingProfile, playerControlTiming, playerRespawnDelayTicks),
                inputBuffer,
                startTickIndex);
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

        private static int CreateDefaultPlayerRespawnDelayTicks(
            GameplayTimingProfile generalTimingProfile)
        {
            if (generalTimingProfile == null)
            {
                throw new ArgumentNullException(nameof(generalTimingProfile));
            }

            return 1;
        }
    }
}
