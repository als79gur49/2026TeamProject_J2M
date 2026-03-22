using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class CleanupPhaseResult
    {
        public static readonly CleanupPhaseResult Empty = new(
            Array.Empty<int>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<int> _removedEntityIds;
        private readonly ReadOnlyCollection<string> _stateTransitions;
        private readonly ReadOnlyCollection<string> _timerChanges;

        public CleanupPhaseResult(
            IEnumerable<int> removedEntityIds,
            IEnumerable<string> timerChanges,
            IEnumerable<string> stateTransitions)
        {
            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            if (timerChanges == null)
            {
                throw new ArgumentNullException(nameof(timerChanges));
            }

            if (stateTransitions == null)
            {
                throw new ArgumentNullException(nameof(stateTransitions));
            }

            _removedEntityIds = new ReadOnlyCollection<int>(new List<int>(removedEntityIds));
            _timerChanges = new ReadOnlyCollection<string>(new List<string>(timerChanges));
            _stateTransitions = new ReadOnlyCollection<string>(new List<string>(stateTransitions));
        }

        public IReadOnlyList<int> RemovedEntityIds => _removedEntityIds;

        public IReadOnlyList<string> TimerChanges => _timerChanges;

        public IReadOnlyList<string> StateTransitions => _stateTransitions;
    }
}
