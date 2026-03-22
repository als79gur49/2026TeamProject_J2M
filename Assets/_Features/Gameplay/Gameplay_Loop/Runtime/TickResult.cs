using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickResult
    {
        private readonly ReadOnlyCollection<TickPhase> _completedPhases;
        private readonly ReadOnlyCollection<string> _phaseTrace;

        public TickResult(int tickIndex, IEnumerable<TickPhase> completedPhases, IEnumerable<string> phaseTrace)
        {
            if (completedPhases == null)
            {
                throw new ArgumentNullException(nameof(completedPhases));
            }

            if (phaseTrace == null)
            {
                throw new ArgumentNullException(nameof(phaseTrace));
            }

            TickIndex = tickIndex;
            _completedPhases = new ReadOnlyCollection<TickPhase>(new List<TickPhase>(completedPhases));
            _phaseTrace = new ReadOnlyCollection<string>(new List<string>(phaseTrace));
        }

        public int TickIndex { get; }

        public IReadOnlyList<TickPhase> CompletedPhases => _completedPhases;

        public IReadOnlyList<string> PhaseTrace => _phaseTrace;

        public bool CompletedAllPhases =>
            _completedPhases.Count == 3 &&
            _completedPhases[0] == TickPhase.Movement &&
            _completedPhases[1] == TickPhase.Attack &&
            _completedPhases[2] == TickPhase.Cleanup;
    }
}
