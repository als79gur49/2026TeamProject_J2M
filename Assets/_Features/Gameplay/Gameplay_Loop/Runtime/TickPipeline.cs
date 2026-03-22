using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickPipeline
    {
        private readonly IReadOnlyList<IEntityLogic> _entityLogics;
        private readonly MovementIntentCollector _movementIntentCollector = new();
        private readonly AttackIntentCollector _attackIntentCollector = new();
        private readonly MovementCommitter _movementCommitter = new();
        private readonly AttackCommitter _attackCommitter = new();
        private readonly CleanupProcessor _cleanupProcessor = new();
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
            var completedPhases = new List<TickPhase>(3);
            var phaseTrace = new List<string>(6);
            var transientBuffer = new PhaseTransientBuffer();
            var writeContext = _worldState.CreateWriteContext();

            var movementSnapshot = SnapshotBuilder.Create(_worldState);
            RunMovementPhase(movementSnapshot, in input, transientBuffer, writeContext, completedPhases, phaseTrace);

            var attackSnapshot = SnapshotBuilder.Create(_worldState);
            RunAttackPhase(attackSnapshot, in input, transientBuffer, writeContext, completedPhases, phaseTrace);

            RunCleanupPhase(in input, writeContext, completedPhases, phaseTrace);

            return new TickResult(input.TickIndex, completedPhases, phaseTrace);
        }

        private void RunMovementPhase(
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
            _movementCommitter.Commit(writeContext, transientBuffer);
            phaseTrace.Add("Movement:Exit");
            completedPhases.Add(TickPhase.Movement);
        }

        private void RunAttackPhase(
            WorldSnapshot snapshot,
            in TickInput input,
            PhaseTransientBuffer transientBuffer,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Attack:Enter");
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, _entityLogics, rawAttackIntents);
            _attackCommitter.Commit(writeContext, transientBuffer);
            phaseTrace.Add("Attack:Exit");
            completedPhases.Add(TickPhase.Attack);
        }

        private void RunCleanupPhase(
            in TickInput input,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Cleanup:Enter");
            _cleanupProcessor.Process(writeContext, input.TickIndex);
            phaseTrace.Add("Cleanup:Exit");
            completedPhases.Add(TickPhase.Cleanup);
        }
    }
}
