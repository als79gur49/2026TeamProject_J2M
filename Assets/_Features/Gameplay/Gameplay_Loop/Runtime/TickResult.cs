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

        internal static TickResult CreateFromOwnedData(
            int tickIndex,
            IEnumerable<TickPhase> completedPhases,
            IEnumerable<string> phaseTrace,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            TickResultData tickResultData,
            CubeTopologyState finalTopology,
            string determinismHash,
            TickTrace trace)
        {
            if (tickResultData == null)
            {
                throw new ArgumentNullException(nameof(tickResultData));
            }

            return new TickResult(
                OwnedFinalEntitiesToken.Instance,
                tickIndex,
                completedPhases,
                phaseTrace,
                movementPhaseResult,
                attackPhaseResult,
                tickResultData.OwnedFinalEntities,
                tickResultData.EventLog,
                finalTopology,
                tickResultData.PresentationData,
                determinismHash,
                trace,
                tickResultData.ObjectiveResult);
        }

        private TickResult(
            OwnedFinalEntitiesToken ownershipToken,
            int tickIndex,
            IEnumerable<TickPhase> completedPhases,
            IEnumerable<string> phaseTrace,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            ReadOnlyCollection<EntityState> ownedFinalEntities,
            IEnumerable<string> eventLog,
            CubeTopologyState finalTopology,
            TickPresentationData presentationData,
            string determinismHash,
            TickTrace trace,
            StageObjectiveTickResult objectiveResult)
        {
            if (!ownershipToken.IsValid)
            {
                throw new ArgumentException("A trusted FinalEntities ownership token is required.", nameof(ownershipToken));
            }

            if (completedPhases == null)
            {
                throw new ArgumentNullException(nameof(completedPhases));
            }

            if (phaseTrace == null)
            {
                throw new ArgumentNullException(nameof(phaseTrace));
            }

            if (ownedFinalEntities == null)
            {
                throw new ArgumentNullException(nameof(ownedFinalEntities));
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
            PresentationData = presentationData;
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            ObjectiveResult = objectiveResult ?? StageObjectiveTickResult.NoObjective;
            TickIndex = tickIndex;
            _completedPhases = new ReadOnlyCollection<TickPhase>(new List<TickPhase>(completedPhases));
            _phaseTrace = new ReadOnlyCollection<string>(new List<string>(phaseTrace));
            _finalEntities = ownedFinalEntities;
            GameplayTickWorkloadDiagnostics.RecordTickResultFinalEntitiesShared(ownedFinalEntities.Count);
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
            FinalTopology = finalTopology;
            DeterminismHash = determinismHash;
        }

        public TickResult(int tickIndex, IEnumerable<TickPhase> completedPhases, IEnumerable<string> phaseTrace)
            : this(
                tickIndex,
                completedPhases,
                phaseTrace,
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
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
            PresentationData = presentationData;
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            ObjectiveResult = objectiveResult ?? StageObjectiveTickResult.NoObjective;
            TickIndex = tickIndex;
            _completedPhases = new ReadOnlyCollection<TickPhase>(new List<TickPhase>(completedPhases));
            _phaseTrace = new ReadOnlyCollection<string>(new List<string>(phaseTrace));
            var copiedFinalEntities = new List<EntityState>(finalEntities);
            _finalEntities = new ReadOnlyCollection<EntityState>(copiedFinalEntities);
            GameplayTickWorkloadDiagnostics.RecordFinalEntityDefensiveCopy(copiedFinalEntities.Count);
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
            FinalTopology = finalTopology;
            DeterminismHash = determinismHash;
        }

        public int TickIndex { get; }

        internal MovementPhaseResult MovementPhaseResult { get; }

        internal AttackPhaseResult AttackPhaseResult { get; }

        public IReadOnlyList<TickPhase> CompletedPhases => _completedPhases;

        public IReadOnlyList<string> PhaseTrace => _phaseTrace;

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<string> EventLog => _eventLog;

        /// <summary>
        /// Authoritative topology after the tick commits. This is a final presentation seam,
        /// not an intermediate phase diagnostic surface.
        /// </summary>
        public CubeTopologyState FinalTopology { get; }

        public TickPresentationData PresentationData { get; }

        public string DeterminismHash { get; }

        public TickTrace Trace { get; }

        public StageObjectiveTickResult ObjectiveResult { get; }

        public bool CompletedAllPhases =>
            _completedPhases.Count == 5 &&
            _completedPhases[0] == TickPhase.Plan &&
            _completedPhases[1] == TickPhase.Resolve &&
            _completedPhases[2] == TickPhase.Finalize &&
            _completedPhases[3] == TickPhase.Cleanup &&
            _completedPhases[4] == TickPhase.MoonBlockGeneration;

        private readonly struct OwnedFinalEntitiesToken
        {
            internal static readonly OwnedFinalEntitiesToken Instance = new(true);

            private OwnedFinalEntitiesToken(bool isValid)
            {
                IsValid = isValid;
            }

            internal bool IsValid { get; }
        }
    }
}
