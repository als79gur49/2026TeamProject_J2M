using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Resolution;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Resolution;
using Game.Feature.Gameplay.Movement.Sorting;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickPipeline
    {
        private readonly IdAllocator _idAllocator = new();
        private readonly IReadOnlyList<IEntityLogic> _entityLogics;
        private readonly MovementIntentCollector _movementIntentCollector = new();
        private readonly MovementExpander _movementExpander = new();
        private readonly MovementResolver _movementResolver = new();
        private readonly AttackIntentCollector _attackIntentCollector = new();
        private readonly AttackExpander _attackExpander = new();
        private readonly AttackResolver _attackResolver = new();
        private readonly MovementCommitter _movementCommitter = new();
        private readonly AttackCommitter _attackCommitter = new();
        private readonly CleanupProcessor _cleanupProcessor = new();
        private readonly TickResultBuilder _tickResultBuilder = new();
        private readonly DeterminismHashBuilder _determinismHashBuilder = new();
        private readonly TickTraceBuilder _tickTraceBuilder = new();
        private readonly WorldState _worldState;

        public TickPipeline(WorldState worldState)
            : this(worldState, Array.Empty<IEntityLogic>())
        {
        }

        public TickPipeline(WorldState worldState, IEnumerable<IEntityLogic> entityLogics)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));

            if (entityLogics == null)
            {
                throw new ArgumentNullException(nameof(entityLogics));
            }

            _entityLogics = new List<IEntityLogic>(entityLogics).AsReadOnly();
        }

        public TickResult RunTick(in TickInput input)
        {
            _idAllocator.ResetForTick(input.TickIndex);

            var completedPhases = new List<TickPhase>(3);
            var phaseTrace = new List<string>(6);
            var transientBuffer = new PhaseTransientBuffer();
            var writeContext = _worldState.CreateWriteContext();

            var movementSnapshot = SnapshotBuilder.Create(_worldState);
            var movementPhaseResult = RunMovementPhase(
                movementSnapshot,
                in input,
                transientBuffer,
                writeContext,
                completedPhases,
                phaseTrace);

            var attackSnapshot = SnapshotBuilder.Create(_worldState);
            var attackPhaseResult = RunAttackPhase(
                attackSnapshot,
                transientBuffer,
                writeContext,
                completedPhases,
                phaseTrace);

            var cleanupSnapshot = SnapshotBuilder.Create(_worldState);
            var cleanupPhaseResult = RunCleanupPhase(
                cleanupSnapshot,
                input.TickIndex,
                writeContext,
                completedPhases,
                phaseTrace);

            var finalSnapshot = SnapshotBuilder.Create(_worldState);
            var tickResultData = _tickResultBuilder.Build(
                finalSnapshot,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult);
            var determinismHash = _determinismHashBuilder.Build(input.TickIndex, finalSnapshot, tickResultData);
            var tickTrace = _tickTraceBuilder.Build(
                input.TickIndex,
                movementSnapshot,
                movementPhaseResult,
                attackSnapshot,
                attackPhaseResult,
                cleanupPhaseResult,
                finalSnapshot,
                tickResultData,
                determinismHash);

            return new TickResult(
                input.TickIndex,
                completedPhases,
                phaseTrace,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                tickResultData.FinalEntities,
                tickResultData.EventLog,
                determinismHash,
                tickTrace);
        }

        private MovementPhaseResult RunMovementPhase(
            WorldSnapshot snapshot,
            in TickInput input,
            PhaseTransientBuffer transientBuffer,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Movement:Enter");
            var rawMovementIntents = new List<RawMovementIntent>();
            _movementIntentCollector.Collect(snapshot, in input, _entityLogics, rawMovementIntents);
            var sortedIntents = BuildMovementIntents(rawMovementIntents);
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            _movementExpander.Expand(snapshot, sortedIntents, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            AssignMovementGroupIds(expandedCandidates);

            var selectedGroups = new List<ActionGroup>();
            _movementResolver.Resolve(expandedCandidates, selectedGroups, rejectedReasons);

            var commitEvents = new List<string>();
            _movementCommitter.Commit(writeContext, transientBuffer, selectedGroups, commitEvents);
            phaseTrace.Add("Movement:Exit");
            completedPhases.Add(TickPhase.Movement);

            return new MovementPhaseResult(
                rawMovementIntents,
                sortedIntents,
                expandedCandidates,
                selectedGroups,
                commitEvents,
                rejectedReasons);
        }

        private AttackPhaseResult RunAttackPhase(
            WorldSnapshot snapshot,
            PhaseTransientBuffer transientBuffer,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Attack:Enter");
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, _entityLogics, rawAttackIntents);
            var drainedImpactReservations = transientBuffer.DrainImpacts();
            var sortedInputs = BuildAttackInputs(rawAttackIntents, drainedImpactReservations);
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            _attackExpander.Expand(snapshot, sortedInputs, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedCandidates);

            var selectedGroups = new List<ActionGroup>();
            _attackResolver.Resolve(expandedCandidates, selectedGroups, rejectedReasons);

            var commitEvents = new List<string>();
            _attackCommitter.Commit(snapshot, writeContext, selectedGroups, commitEvents);
            phaseTrace.Add("Attack:Exit");
            completedPhases.Add(TickPhase.Attack);

            return new AttackPhaseResult(
                rawAttackIntents,
                drainedImpactReservations,
                sortedInputs,
                expandedCandidates,
                selectedGroups,
                commitEvents,
                rejectedReasons);
        }

        private CleanupPhaseResult RunCleanupPhase(
            WorldSnapshot snapshot,
            int tickIndex,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Cleanup:Enter");
            var cleanupPhaseResult = _cleanupProcessor.Process(snapshot, writeContext, tickIndex);
            phaseTrace.Add("Cleanup:Exit");
            completedPhases.Add(TickPhase.Cleanup);

            return cleanupPhaseResult;
        }

        private List<MoveIntent> BuildMovementIntents(List<RawMovementIntent> rawMovementIntents)
        {
            rawMovementIntents.Sort(RawMovementIntentComparer.Instance);

            var sortedIntents = new List<MoveIntent>(rawMovementIntents.Count);

            for (var i = 0; i < rawMovementIntents.Count; i++)
            {
                var rawIntent = rawMovementIntents[i];
                var moveIntent = new MoveIntent(rawIntent.SourceId, rawIntent.Priority, rawIntent.Destination);
                moveIntent.AssignIntentId(_idAllocator.AllocateIntentId());
                sortedIntents.Add(moveIntent);
            }

            return sortedIntents;
        }

        private void AssignMovementGroupIds(List<ActionGroup> expandedCandidates)
        {
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(_idAllocator.AllocateGroupId());
            }
        }

        private List<AttackIntent> BuildAttackInputs(
            List<RawAttackIntent> rawAttackIntents,
            List<ImpactReservation> impactReservations)
        {
            var sortedInputs = new List<AttackIntent>(rawAttackIntents.Count + impactReservations.Count);

            for (var i = 0; i < rawAttackIntents.Count; i++)
            {
                var rawIntent = rawAttackIntents[i];
                sortedInputs.Add(new AttackIntent(rawIntent.SourceId, rawIntent.Priority, rawIntent.TargetId));
            }

            for (var i = 0; i < impactReservations.Count; i++)
            {
                sortedInputs.Add(AttackIntent.FromImpactReservation(impactReservations[i]));
            }

            sortedInputs.Sort(AttackInputComparer.Instance);

            for (var i = 0; i < sortedInputs.Count; i++)
            {
                sortedInputs[i].AssignIntentId(_idAllocator.AllocateIntentId());
            }

            return sortedInputs;
        }

        private void AssignAttackGroupIds(List<ActionGroup> expandedCandidates)
        {
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(_idAllocator.AllocateGroupId());
            }
        }
    }
}
