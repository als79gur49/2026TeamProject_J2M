using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickResult
    {
        private readonly ReadOnlyCollection<TickPhase> _completedPhases;
        private readonly ReadOnlyCollection<string> _eventLog;
        private readonly ReadOnlyCollection<EntityState> _finalEntities;
        private readonly ReadOnlyCollection<string> _phaseTrace;

        public TickResult(int tickIndex, IEnumerable<TickPhase> completedPhases, IEnumerable<string> phaseTrace)
            : this(
                tickIndex,
                completedPhases,
                phaseTrace,
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                TickPresentationData.Empty,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective)
        {
        }

        internal TickResult(
            int tickIndex,
            IEnumerable<TickPhase> completedPhases,
            IEnumerable<string> phaseTrace,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            IEnumerable<EntityState> finalEntities,
            IEnumerable<string> eventLog,
            CubeTopologyState finalTopology,
            string determinismHash,
            TickTrace trace,
            StageObjectiveTickResult objectiveResult = null)
            : this(
                tickIndex,
                completedPhases,
                phaseTrace,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                finalEntities,
                eventLog,
                finalTopology,
                TickPresentationData.Empty,
                determinismHash,
                trace,
                objectiveResult)
        {
        }

        internal TickResult(
            int tickIndex,
            IEnumerable<TickPhase> completedPhases,
            IEnumerable<string> phaseTrace,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            IEnumerable<EntityState> finalEntities,
            IEnumerable<string> eventLog,
            CubeTopologyState finalTopology,
            TickPresentationData presentationData,
            string determinismHash,
            TickTrace trace,
            StageObjectiveTickResult objectiveResult = null)
        {
            if (completedPhases == null)
            {
                throw new ArgumentNullException(nameof(completedPhases));
            }

            if (phaseTrace == null)
            {
                throw new ArgumentNullException(nameof(phaseTrace));
            }

            if (finalEntities == null)
            {
                throw new ArgumentNullException(nameof(finalEntities));
            }

            if (eventLog == null)
            {
                throw new ArgumentNullException(nameof(eventLog));
            }

            if (determinismHash == null)
            {
                throw new ArgumentNullException(nameof(determinismHash));
            }

            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            CleanupPhaseResult = cleanupPhaseResult ?? throw new ArgumentNullException(nameof(cleanupPhaseResult));
            PresentationData = presentationData;
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            ObjectiveResult = objectiveResult ?? StageObjectiveTickResult.NoObjective;
            TickIndex = tickIndex;
            _completedPhases = new ReadOnlyCollection<TickPhase>(new List<TickPhase>(completedPhases));
            _phaseTrace = new ReadOnlyCollection<string>(new List<string>(phaseTrace));
            _finalEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities));
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
            FinalTopology = finalTopology;
            DeterminismHash = determinismHash;
        }

        public int TickIndex { get; }

        internal MovementPhaseResult MovementPhaseResult { get; }

        internal AttackPhaseResult AttackPhaseResult { get; }

        internal CleanupPhaseResult CleanupPhaseResult { get; }

        public IReadOnlyList<TickPhase> CompletedPhases => _completedPhases;

        public IReadOnlyList<string> PhaseTrace => _phaseTrace;

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<string> EventLog => _eventLog;

        internal CubeTopologyState FinalTopology { get; }

        public TickPresentationData PresentationData { get; }

        public string DeterminismHash { get; }

        public TickTrace Trace { get; }

        public StageObjectiveTickResult ObjectiveResult { get; }

        public bool CompletedAllPhases =>
            _completedPhases.Count == 4 &&
            _completedPhases[0] == TickPhase.Movement &&
            _completedPhases[1] == TickPhase.Attack &&
            _completedPhases[2] == TickPhase.Cleanup &&
            _completedPhases[3] == TickPhase.Respawn;
    }
}
