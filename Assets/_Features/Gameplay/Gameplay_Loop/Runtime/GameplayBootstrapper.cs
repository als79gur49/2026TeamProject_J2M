using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

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
            return new TickPipeline(worldState, entityLogics, _entityLogicProvider);
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
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            return new TickRunner(
                CreateTickPipeline(worldState, entityLogics),
                inputBuffer,
                startTickIndex);
        }
    }
}
