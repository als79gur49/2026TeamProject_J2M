using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class CleanupProcessor
    {
        private readonly RemovalProcessor _removalProcessor = new();
        private readonly StateTimerProcessor _stateTimerProcessor = new();
        private readonly StateTransitionProcessor _stateTransitionProcessor = new();

        public CleanupPhaseResult Process(
            WorldSnapshot snapshot,
            ICleanupCommitContext writeContext,
            int tickIndex)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var survivingEntities = new List<EntityState>(orderedEntities.Count);
            var removedEntityIds = new List<int>();
            _removalProcessor.Process(orderedEntities, writeContext, survivingEntities, removedEntityIds);

            var timerChanges = new List<string>();
            _stateTimerProcessor.Process(survivingEntities, writeContext, tickIndex, timerChanges);

            var stateTransitions = new List<string>();
            _stateTransitionProcessor.Process(survivingEntities, writeContext, stateTransitions);

            return new CleanupPhaseResult(
                removedEntityIds,
                timerChanges,
                stateTransitions);
        }
    }
}
