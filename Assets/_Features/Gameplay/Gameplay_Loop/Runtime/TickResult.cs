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
            : this(
                tickIndex,
                completedPhases,
                phaseTrace,
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty)
        {
        }

        internal TickResult(
            int tickIndex,
            IEnumerable<TickPhase> completedPhases,
            IEnumerable<string> phaseTrace,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult)
        {
            if (completedPhases == null)
            {
                throw new ArgumentNullException(nameof(completedPhases));
            }

            if (phaseTrace == null)
            {
                throw new ArgumentNullException(nameof(phaseTrace));
            }

            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            CleanupPhaseResult = cleanupPhaseResult ?? throw new ArgumentNullException(nameof(cleanupPhaseResult));
            TickIndex = tickIndex;
            _completedPhases = new ReadOnlyCollection<TickPhase>(new List<TickPhase>(completedPhases));
            _phaseTrace = new ReadOnlyCollection<string>(new List<string>(phaseTrace));
        }

        public int TickIndex { get; }

        internal MovementPhaseResult MovementPhaseResult { get; }

        internal AttackPhaseResult AttackPhaseResult { get; }

        internal CleanupPhaseResult CleanupPhaseResult { get; }

        public IReadOnlyList<TickPhase> CompletedPhases => _completedPhases;

        public IReadOnlyList<string> PhaseTrace => _phaseTrace;

        public bool CompletedAllPhases =>
            _completedPhases.Count == 3 &&
            _completedPhases[0] == TickPhase.Movement &&
            _completedPhases[1] == TickPhase.Attack &&
            _completedPhases[2] == TickPhase.Cleanup;
    }
}
