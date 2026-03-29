using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Resolution;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
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
        private readonly EntityIdAllocator _entityIdAllocator;
        private readonly ISnapshotEntityLogicProvider _entityLogicProvider;
        private readonly IReadOnlyList<IEntityLogic> _staticEntityLogics;
        private readonly MovementIntentCollector _movementIntentCollector = new();
        private readonly MovementExpander _movementExpander;
        private readonly MovementResolver _movementResolver = new();
        private readonly AttackIntentCollector _attackIntentCollector = new();
        private readonly AttackInputNormalizer _attackInputNormalizer = new();
        private readonly AttackExpander _attackExpander;
        private readonly AttackResolver _attackResolver = new();
        private readonly MovementCommitter _movementCommitter = new();
        private readonly AttackCommitter _attackCommitter = new();
        private readonly CleanupProcessor _cleanupProcessor = new();
        private readonly TickResultBuilder _tickResultBuilder = new();
        private readonly DeterminismHashBuilder _determinismHashBuilder = new();
        private readonly TickTraceBuilder _tickTraceBuilder = new();
        private readonly DelayedAttackEffectQueue _delayedAttackEffectQueue = new();
        private readonly int _projectileStepIntervalTicks;
        private readonly WorldState _worldState;
        private bool _hasInitializedProjectileCadence;

        public TickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            ISnapshotEntityLogicProvider entityLogicProvider)
            : this(
                worldState,
                entityLogics,
                entityLogicProvider,
                GameplayTimingProfile.CreateDefault())
        {
        }

        internal TickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            ISnapshotEntityLogicProvider entityLogicProvider,
            GameplayTimingProfile timingProfile)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));

            if (entityLogics == null)
            {
                throw new ArgumentNullException(nameof(entityLogics));
            }

            _entityLogicProvider = entityLogicProvider ?? throw new ArgumentNullException(nameof(entityLogicProvider));
            _staticEntityLogics = new List<IEntityLogic>(entityLogics).AsReadOnly();
            _entityIdAllocator = EntityIdAllocator.Create(SnapshotBuilder.Create(_worldState));
            var resolvedTimingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _projectileStepIntervalTicks = resolvedTimingProfile.ProjectileStepIntervalTicks;
            _movementExpander = new MovementExpander(resolvedTimingProfile);
            _attackExpander = new AttackExpander(resolvedTimingProfile);
        }

        public TickResult RunTick(in TickInput input)
        {
            InitializeProjectileCadenceForSessionStart();
            _idAllocator.ResetForTick(input.TickIndex);

            var completedPhases = new List<TickPhase>(3);
            var phaseTrace = new List<string>(6);
            var transientBuffer = new PhaseTransientBuffer();
            var writeContext = _worldState.CreateWriteContext();
            var drainedDelayedAttackEffects = _delayedAttackEffectQueue.Drain(input.TickIndex);

            var movementSnapshot = SnapshotBuilder.Create(_worldState);
            var entityLogicsForTick = BuildEntityLogicsForTick(movementSnapshot);
            var movementPhaseResult = RunMovementPhase(
                movementSnapshot,
                in input,
                entityLogicsForTick,
                transientBuffer,
                writeContext,
                completedPhases,
                phaseTrace);

            var attackSnapshot = SnapshotBuilder.Create(_worldState);
            var attackPhaseResult = RunAttackPhase(
                attackSnapshot,
                in input,
                entityLogicsForTick,
                transientBuffer,
                drainedDelayedAttackEffects,
                input.TickIndex,
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
            var pendingDelayedAttackEffects = _delayedAttackEffectQueue.Snapshot();
            var tickResultData = _tickResultBuilder.Build(
                finalSnapshot,
                pendingDelayedAttackEffects,
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
                finalSnapshot.Topology,
                tickResultData.PresentationData,
                determinismHash,
                tickTrace);
        }

        private void InitializeProjectileCadenceForSessionStart()
        {
            if (_hasInitializedProjectileCadence)
            {
                return;
            }

            _hasInitializedProjectileCadence = true;

            var snapshot = SnapshotBuilder.Create(_worldState);
            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            if (orderedEntities.Count == 0)
            {
                return;
            }

            var writeContext = _worldState.CreateWriteContext();

            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var entity = orderedEntities[i];
                if (entity.type != EntityType.Projectile ||
                    entity.spawnTick != 0 ||
                    entity.stateTimer > 0 ||
                    entity.hp <= 0 ||
                    entity.markedForDeath)
                {
                    continue;
                }

                writeContext.ApplyStateChange(entity.entityId, entity.state, _projectileStepIntervalTicks);
            }
        }

        private MovementPhaseResult RunMovementPhase(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IEntityLogic> entityLogics,
            PhaseTransientBuffer transientBuffer,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Movement:Enter");
            var rawMovementIntents = new List<RawMovementIntent>();
            _movementIntentCollector.Collect(snapshot, in input, entityLogics, rawMovementIntents);
            var sortedIntents = BuildMovementIntents(rawMovementIntents);
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            _movementExpander.Expand(snapshot, sortedIntents, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            AssignMovementGroupIds(expandedCandidates);

            var selectedGroups = new List<ActionGroup>();
            _movementResolver.Resolve(expandedCandidates, selectedGroups, rejectedReasons);

            var commitEvents = new List<string>();
            _movementCommitter.Commit(
                snapshot,
                sortedIntents,
                input.TickIndex,
                writeContext,
                transientBuffer,
                selectedGroups,
                commitEvents);
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
            in TickInput input,
            IReadOnlyList<IEntityLogic> entityLogics,
            PhaseTransientBuffer transientBuffer,
            List<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            int tickIndex,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Attack:Enter");
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, in input, entityLogics, rawAttackIntents);
            var drainedImpactReservations = transientBuffer.DrainImpacts();
            var sortedInputs = NormalizeAttackInputs(rawAttackIntents, drainedImpactReservations, drainedDelayedAttackEffects);
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            _attackExpander.Expand(snapshot, sortedInputs, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedCandidates);

            var selectedGroups = new List<ActionGroup>();
            _attackResolver.Resolve(expandedCandidates, selectedGroups, rejectedReasons);
            FinalizeAttackSpawns(selectedGroups);

            var commitEvents = new List<string>();
            var delayedAttackDrainEvents = BuildDelayedAttackDrainEvents(tickIndex, drainedDelayedAttackEffects);
            var delayedAttackEnqueueEvents = new List<string>();
            _attackCommitter.Commit(
                snapshot,
                writeContext,
                tickIndex,
                _delayedAttackEffectQueue,
                selectedGroups,
                commitEvents,
                delayedAttackEnqueueEvents);
            phaseTrace.Add("Attack:Exit");
            completedPhases.Add(TickPhase.Attack);

            var eventLogEntries = new List<string>(delayedAttackDrainEvents.Count + commitEvents.Count + delayedAttackEnqueueEvents.Count);
            AddRange(eventLogEntries, delayedAttackDrainEvents);
            AddRange(eventLogEntries, commitEvents);
            AddRange(eventLogEntries, delayedAttackEnqueueEvents);

            return new AttackPhaseResult(
                rawAttackIntents,
                drainedImpactReservations,
                drainedDelayedAttackEffects,
                sortedInputs,
                expandedCandidates,
                selectedGroups,
                commitEvents,
                eventLogEntries,
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
                MoveIntent moveIntent = rawIntent.CommandKind switch
                {
                    Movement.MovementCommandKind.Push => new PushIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                    Movement.MovementCommandKind.Flip => new FlipIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                    _ => new MoveIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                };
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

        private List<AttackIntent> NormalizeAttackInputs(
            List<RawAttackIntent> rawAttackIntents,
            List<ImpactReservation> impactReservations,
            List<DelayedAttackEffectRecord> delayedAttackEffects)
        {
            var sortedInputs = new List<AttackIntent>(rawAttackIntents.Count + impactReservations.Count + delayedAttackEffects.Count);
            _attackInputNormalizer.Normalize(rawAttackIntents, impactReservations, delayedAttackEffects, sortedInputs);

            for (var i = 0; i < sortedInputs.Count; i++)
            {
                sortedInputs[i].AssignIntentId(_idAllocator.AllocateIntentId());
            }

            return sortedInputs;
        }

        internal void EnqueueDelayedAttackEffect(DelayedAttackEffectRecord effectRecord)
        {
            _delayedAttackEffectQueue.Enqueue(effectRecord);
        }

        private static List<string> BuildDelayedAttackDrainEvents(
            int tickIndex,
            IReadOnlyList<DelayedAttackEffectRecord> drainedDelayedAttackEffects)
        {
            var events = new List<string>(drainedDelayedAttackEffects.Count);

            for (var i = 0; i < drainedDelayedAttackEffects.Count; i++)
            {
                var effectRecord = drainedDelayedAttackEffects[i];
                events.Add(
                    $"DelayedAttackDrained|Tick={tickIndex}|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|GeneratedTick={effectRecord.TickGenerated}|ExecuteTick={effectRecord.ExecuteAtTick}|Group={effectRecord.SourceActionGroupId}|Sequence={effectRecord.EffectSequence}");
            }

            return events;
        }

        private static void AddRange(List<string> destination, IReadOnlyList<string> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }

        private void AssignAttackGroupIds(List<ActionGroup> expandedCandidates)
        {
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(_idAllocator.AllocateGroupId());
            }
        }

        private void FinalizeAttackSpawns(IReadOnlyList<ActionGroup> selectedGroups)
        {
            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    group.Spawns[spawnIndex] = FinalizeSpawn(group.Spawns[spawnIndex]);
                }
            }
        }

        private SpawnAction FinalizeSpawn(SpawnAction template)
        {
            var entity = template.Entity;
            entity.entityId = _entityIdAllocator.AllocateEntityId();
            entity.spawnTick = _idAllocator.CurrentTickIndex;
            return new SpawnAction(_idAllocator.AllocateSpawnId(), entity);
        }

        private List<IEntityLogic> BuildEntityLogicsForTick(WorldSnapshot snapshot)
        {
            return new List<IEntityLogic>(_entityLogicProvider.Build(snapshot, _staticEntityLogics));
        }
    }
}
