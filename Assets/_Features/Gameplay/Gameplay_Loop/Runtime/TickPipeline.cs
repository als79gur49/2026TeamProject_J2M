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
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;

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
        private readonly MovementCommitter _movementCommitter;
        private readonly AttackCommitter _attackCommitter;
        private readonly CleanupProcessor _cleanupProcessor = new();
        private readonly RespawnProcessor _respawnProcessor = new();
        private readonly TickResultBuilder _tickResultBuilder = new();
        private readonly DeterminismHashBuilder _determinismHashBuilder = new();
        private readonly TickTraceBuilder _tickTraceBuilder = new();
        private readonly DelayedAttackEffectQueue _delayedAttackEffectQueue = new();
        private readonly List<EntityState> _playerRespawnTemplates;
        private readonly StageObjectiveTracker _objectiveTracker;
        private readonly int _playerDamageCooldownTicks;
        private readonly int _playerRespawnDelayTicks;
        private readonly WorldState _worldState;

        public TickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            ISnapshotEntityLogicProvider entityLogicProvider,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));

            if (entityLogics == null)
            {
                throw new ArgumentNullException(nameof(entityLogics));
            }

            _entityLogicProvider = entityLogicProvider ?? throw new ArgumentNullException(nameof(entityLogicProvider));
            _staticEntityLogics = new List<IEntityLogic>(entityLogics).AsReadOnly();
            _entityIdAllocator = EntityIdAllocator.Create(SnapshotBuilder.Create(_worldState));
            var resolvedGeneralTimingProfile = generalTimingProfile ?? throw new ArgumentNullException(nameof(generalTimingProfile));
            if (playerRespawnDelayTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerRespawnDelayTicks),
                    "Player respawn delay ticks must be greater than zero.");
            }

            _movementExpander = new MovementExpander(resolvedGeneralTimingProfile);
            _movementCommitter = new MovementCommitter(playerControlTiming, resolvedGeneralTimingProfile);
            _attackExpander = new AttackExpander(resolvedGeneralTimingProfile);
            _attackCommitter = new AttackCommitter(playerControlTiming);
            _playerDamageCooldownTicks = Math.Max(0, playerControlTiming.DamageCooldownTicks);
            _playerRespawnDelayTicks = playerRespawnDelayTicks;
            _playerRespawnTemplates = BuildPlayerRespawnTemplates(
                SnapshotBuilder.Create(_worldState),
                _staticEntityLogics);
            _objectiveTracker = (objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled).CreateTracker();
        }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _objectiveTracker.ObjectiveDefinition;

        public StageObjectiveTickResult CurrentObjectiveResult => _objectiveTracker.CurrentResult;

        public TickResult RunTick(in TickInput input)
        {
            _idAllocator.ResetForTick(input.TickIndex);

            var completedPhases = new List<TickPhase>(5);
            var phaseTrace = new List<string>(10);
            var drainedDelayedAttackEffects = _delayedAttackEffectQueue.Drain(input.TickIndex);

            var initialSnapshot = SnapshotBuilder.Create(_worldState);
            var entityLogicsForTick = _entityLogicProvider.Build(initialSnapshot, _staticEntityLogics);
            var planPhaseResult = RunPlanPhase(
                initialSnapshot,
                in input,
                entityLogicsForTick,
                completedPhases,
                phaseTrace);
            var aiPhaseResult = planPhaseResult.EnemyAiPhaseResult;
            var snapshotAfterEnemyAi = planPhaseResult.PostEnemyAiSnapshot;
            var preMovementStateResult = planPhaseResult.PreMovementStatePhaseResult;
            var preMovementSnapshot = planPhaseResult.PlanSnapshot;
            var resolvePhaseResult = RunResolvePhase(
                preMovementSnapshot,
                in input,
                entityLogicsForTick,
                aiPhaseResult,
                planPhaseResult,
                drainedDelayedAttackEffects,
                input.TickIndex,
                completedPhases,
                phaseTrace);
            var writeContext = _worldState.CreateWriteContext();
            RunFinalizePhase(resolvePhaseResult.FinalizationBatch, writeContext, completedPhases, phaseTrace);
            var postFinalizeSnapshot = SnapshotBuilder.Create(_worldState);
            var cleanupPhaseResult = RunCleanupPhase(
                postFinalizeSnapshot,
                input.TickIndex,
                writeContext,
                completedPhases,
                phaseTrace);
            var postCleanupSnapshot = SnapshotBuilder.Create(_worldState);
            var respawnPhaseResult = RunRespawnPhase(
                initialSnapshot,
                postCleanupSnapshot,
                cleanupPhaseResult,
                input.TickIndex,
                writeContext,
                completedPhases,
                phaseTrace);
            var finalAuthoritativeSnapshot = SnapshotBuilder.Create(_worldState);
            var movementPhaseResult = resolvePhaseResult.MovementPhaseResult;
            var attackPhaseResult = resolvePhaseResult.AttackPhaseResult;
            var objectiveExtensions = new Dictionary<Type, object>
            {
                { typeof(CleanupPhaseResult), cleanupPhaseResult },
                { typeof(AttackPhaseResult), attackPhaseResult },
            };
            var objectiveTickFacts = new StageObjectiveTickFacts(
                input.TickIndex,
                input.PlayerCommand,
                cleanupPhaseResult.RemovedEntityIds,
                attackPhaseResult.DamageResolutions,
                objectiveExtensions);
            var objectiveResult = _objectiveTracker.Advance(finalAuthoritativeSnapshot, in objectiveTickFacts);
            var presentationBuildContext = new TickPresentationBuildContext(
                preMovementSnapshot,
                resolvePhaseResult.PostMovementSnapshot,
                resolvePhaseResult.PostAttackSnapshot,
                finalAuthoritativeSnapshot,
                preMovementStateResult,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                respawnPhaseResult,
                input.TickIndex,
                snapshotAfterEnemyAi,
                input.PlayerCommand);
            var pendingDelayedAttackEffects = _delayedAttackEffectQueue.Snapshot();
            var tickResultData = _tickResultBuilder.Build(
                finalAuthoritativeSnapshot,
                pendingDelayedAttackEffects,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                respawnPhaseResult,
                objectiveResult,
                presentationBuildContext);
            var determinismHash = _determinismHashBuilder.Build(input.TickIndex, finalAuthoritativeSnapshot, tickResultData);
            var tickTrace = _tickTraceBuilder.Build(
                input.TickIndex,
                preMovementSnapshot,
                aiPhaseResult,
                resolvePhaseResult.EnemyActionPhaseResult,
                preMovementStateResult,
                movementPhaseResult,
                resolvePhaseResult.PostMovementSnapshot,
                attackPhaseResult,
                cleanupPhaseResult,
                respawnPhaseResult,
                finalAuthoritativeSnapshot,
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
                finalAuthoritativeSnapshot.Topology,
                tickResultData.PresentationData,
                determinismHash,
                tickTrace,
                objectiveResult);
        }

        private EnemyAiPhaseResult RunEnemyAiPhase(
            IReadOnlyList<IEnemyAiStateLogic> entityLogics,
            WorldSnapshot snapshot,
            in TickInput input,
            IEnemyAiCommitContext writeContext)
        {
            var beforeMovementTransitions = new List<string>();
            CommitEnemyAiTransitions(
                snapshot,
                in input,
                entityLogics,
                EnemyAiTransitionStage.BeforeMovement,
                writeContext,
                beforeMovementTransitions);

            return new EnemyAiPhaseResult(
                beforeMovementTransitions,
                new List<string>(),
                new List<string>());
        }

        private static EnemyActionPhaseResult RunEnemyActionPhase(
            IReadOnlyList<IEnemyActionStateLogic> entityLogics,
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyActionStage stage,
            IEnemyActionCommitContext writeContext,
            EnemyActionPhaseResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var transitions = stage == EnemyActionStage.BeforeAttackCollection
                ? result.BeforeAttackCollectionTransitions
                : result.AfterAttackTransitions;
            CommitEnemyActionState(snapshot, in input, entityLogics, stage, writeContext, transitions);
            return result;
        }

        private static void CommitEnemyAiTransitions(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IEnemyAiStateLogic> entityLogics,
            EnemyAiTransitionStage stage,
            IEnemyAiCommitContext writeContext,
            List<string> transitions)
        {
            transitions.Clear();

            for (var i = 0; i < entityLogics.Count; i++)
            {
                entityLogics[i].CommitAiTransitions(snapshot, in input, stage, writeContext, transitions);
            }
        }

        private static void CommitEnemyActionState(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IEnemyActionStateLogic> entityLogics,
            EnemyActionStage stage,
            IEnemyActionCommitContext writeContext,
            List<EnemyActionTransition> transitions)
        {
            transitions.Clear();

            for (var i = 0; i < entityLogics.Count; i++)
            {
                entityLogics[i].CommitEnemyActionState(snapshot, in input, stage, writeContext, transitions);
            }
        }

        private static PreMovementStatePhaseResult RunPreMovementStatePhase(
            IReadOnlyList<IPreMovementStateLogic> entityLogics,
            WorldSnapshot snapshot,
            in TickInput input,
            IPreMovementStateCommitContext writeContext)
        {
            var updates = new List<string>();
            var actionTransitions = new List<PlayerActionTransition>();

            for (var i = 0; i < entityLogics.Count; i++)
            {
                entityLogics[i].CommitPreMovementState(snapshot, in input, writeContext, updates, actionTransitions);
            }

            return new PreMovementStatePhaseResult(updates, actionTransitions);
        }

        private PlanPhaseResult RunPlanPhase(
            WorldSnapshot snapshot,
            in TickInput input,
            EntityLogicSet entityLogicsForTick,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Plan:Enter");
            var planFinalizationBatch = new FinalizationBatch();
            var projectedWorld = new ProjectedWorld(snapshot);

            var beforeMovementAiBatch = new FinalizationBatch();
            var beforeMovementAiContext = new RecordingFinalizationContext(beforeMovementAiBatch);
            var aiPhaseResult = RunEnemyAiPhase(
                entityLogicsForTick.AiStateLogics,
                snapshot,
                in input,
                beforeMovementAiContext);
            planFinalizationBatch.MergeFrom(beforeMovementAiBatch);
            projectedWorld.ApplyBatch(beforeMovementAiBatch);
            var snapshotAfterEnemyAi = projectedWorld.CreateSnapshot();

            var preMovementBatch = new FinalizationBatch();
            var preMovementContext = new RecordingFinalizationContext(preMovementBatch);
            var preMovementStateResult = RunPreMovementStatePhase(
                entityLogicsForTick.PreMovementStateLogics,
                snapshotAfterEnemyAi,
                in input,
                preMovementContext);
            planFinalizationBatch.MergeFrom(preMovementBatch);
            projectedWorld.ApplyBatch(preMovementBatch);
            var planSnapshot = projectedWorld.CreateSnapshot();

            var rawMovementIntents = new List<RawMovementIntent>();
            _movementIntentCollector.Collect(planSnapshot, in input, entityLogicsForTick.MovementLogics, rawMovementIntents);
            var rejectedReasons = new List<string>();
            var executableMovementIntents = FilterExecutionLockedMovementIntents(planSnapshot, input.TickIndex, rawMovementIntents, rejectedReasons);
            var sortedIntents = BuildMovementIntents(executableMovementIntents);
            var playerTraversalSourceIds = CollectPlayerTraversalSourceIds(entityLogicsForTick.MovementLogics);
            var expandedCandidates = new List<ActionGroup>();
            _movementExpander.Expand(planSnapshot, sortedIntents, playerTraversalSourceIds, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            AssignMovementGroupIds(expandedCandidates);
            var nextContestId = 1;
            var spaceContests = BuildSpaceContests(expandedCandidates, ref nextContestId);
            phaseTrace.Add("Plan:Exit");
            completedPhases.Add(TickPhase.Plan);

            return new PlanPhaseResult(
                rawMovementIntents,
                sortedIntents,
                expandedCandidates,
                rejectedReasons,
                spaceContests,
                nextContestId,
                aiPhaseResult,
                preMovementStateResult,
                snapshotAfterEnemyAi,
                planSnapshot,
                planFinalizationBatch);
        }

        private ResolvePhaseResult RunResolvePhase(
            WorldSnapshot planSnapshot,
            in TickInput input,
            EntityLogicSet entityLogicsForTick,
            EnemyAiPhaseResult aiPhaseResult,
            PlanPhaseResult planPhaseResult,
            List<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            int tickIndex,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Resolve:Enter");
            var projectedWorld = new ProjectedWorld(planSnapshot);
            var finalizationBatch = new FinalizationBatch();
            finalizationBatch.MergeFrom(planPhaseResult.PlanFinalizationBatch);
            var contests = new List<Contest>(planPhaseResult.SpaceContests.Count);
            AddRange(contests, planPhaseResult.SpaceContests);
            var resolutionRecords = new List<ResolutionRecord>();
            var nextContestId = planPhaseResult.NextContestId;

            var movementRejectedReasons = new List<string>(planPhaseResult.RejectedReasons);
            var selectedMovementGroups = new List<ActionGroup>();
            _movementResolver.Resolve(planSnapshot, planPhaseResult.ExpandedCandidates, selectedMovementGroups, movementRejectedReasons);
            RecordSpaceResolutionRecords(
                planPhaseResult.SpaceContests,
                selectedMovementGroups,
                resolutionRecords);
            var movementImpactReservations = _movementCommitter.ResolveImpactReservations(planSnapshot, tickIndex, selectedMovementGroups);
            var drainedImpactReservations = MovementCommitter.SortImpactReservations(movementImpactReservations);
            var impactContests = BuildImpactContests(drainedImpactReservations, ref nextContestId);
            AddRange(contests, impactContests);
            RecordAcceptedResolutionRecords(impactContests, resolutionRecords);
            var movementDestroyContests = BuildDestroyContests(selectedMovementGroups, ref nextContestId);
            AddRange(contests, movementDestroyContests);
            var movementDestroyResolutions = _movementCommitter.ResolveDestroyResolutions(planSnapshot, selectedMovementGroups);
            RecordDestroyResolutionRecords(
                movementDestroyContests,
                movementDestroyResolutions,
                resolutionRecords);
            var movementFacingResolutions = _movementCommitter.ResolveFacingResolutions(
                planSnapshot,
                planPhaseResult.SortedIntents,
                selectedMovementGroups);
            var movementExecutionLockResolutions = _movementCommitter.ResolveExecutionLockResolutions(
                planSnapshot,
                tickIndex,
                selectedMovementGroups);
            var movementEnemyLocomotionResolutions = _movementCommitter.ResolveEnemyLocomotionResolutions(
                planSnapshot,
                planPhaseResult.SortedIntents,
                selectedMovementGroups);
            var movementPlayerControlResolutions = _movementCommitter.ResolvePlayerControlResolutions(
                planSnapshot,
                planPhaseResult.SortedIntents,
                tickIndex,
                selectedMovementGroups);

            var movementStageBatch = new FinalizationBatch();
            var movementStageContext = new RecordingFinalizationContext(movementStageBatch);
            var movementCommitEvents = new List<string>();
            _movementCommitter.CommitResolved(
                movementStageContext,
                selectedMovementGroups,
                movementImpactReservations,
                movementDestroyResolutions,
                movementFacingResolutions,
                movementExecutionLockResolutions,
                movementEnemyLocomotionResolutions,
                movementPlayerControlResolutions,
                movementCommitEvents);
            finalizationBatch.MergeFrom(movementStageBatch);
            projectedWorld.ApplyBatch(movementStageBatch);
            var postMovementSnapshot = projectedWorld.CreateSnapshot();

            var beforeAttackAiBatch = new FinalizationBatch();
            var beforeAttackAiContext = new RecordingFinalizationContext(beforeAttackAiBatch);
            CommitEnemyAiTransitions(
                postMovementSnapshot,
                in input,
                entityLogicsForTick.AiStateLogics,
                EnemyAiTransitionStage.BeforeAttack,
                beforeAttackAiContext,
                aiPhaseResult.BeforeAttackTransitions);
            finalizationBatch.MergeFrom(beforeAttackAiBatch);
            projectedWorld.ApplyBatch(beforeAttackAiBatch);

            var enemyActionBeforeAttackBatch = new FinalizationBatch();
            var enemyActionBeforeAttackContext = new RecordingFinalizationContext(enemyActionBeforeAttackBatch);
            var enemyActionPhaseResult = RunEnemyActionPhase(
                entityLogicsForTick.EnemyActionStateLogics,
                projectedWorld.CreateSnapshot(),
                in input,
                EnemyActionStage.BeforeAttackCollection,
                enemyActionBeforeAttackContext,
                new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()));
            finalizationBatch.MergeFrom(enemyActionBeforeAttackBatch);
            projectedWorld.ApplyBatch(enemyActionBeforeAttackBatch);

            var attackSnapshot = projectedWorld.CreateSnapshot();
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(attackSnapshot, in input, entityLogicsForTick.AttackLogics, rawAttackIntents);
            var attackRejectedReasons = new List<string>();
            var executableAttackIntents = FilterExecutionLockedAttackIntents(attackSnapshot, tickIndex, rawAttackIntents, attackRejectedReasons);
            var sortedInputs = NormalizeAttackInputs(executableAttackIntents, drainedImpactReservations, drainedDelayedAttackEffects);
            var expandedAttackCandidates = new List<ActionGroup>();
            _attackExpander.Expand(attackSnapshot, sortedInputs, expandedAttackCandidates, attackRejectedReasons);
            expandedAttackCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedAttackCandidates);

            var selectedAttackGroups = new List<ActionGroup>();
            _attackResolver.Resolve(expandedAttackCandidates, selectedAttackGroups, attackRejectedReasons);
            FinalizeAttackSpawns(selectedAttackGroups);
            var damageContests = BuildDamageContests(selectedAttackGroups, ref nextContestId);
            AddRange(contests, damageContests);
            var attackDestroyContests = BuildDestroyContests(selectedAttackGroups, ref nextContestId);
            AddRange(contests, attackDestroyContests);
            var damageResolutions = ResolveDamageResolutions(attackSnapshot, selectedAttackGroups, tickIndex);
            var destroyResolutions = ResolveDestroyResolutions(attackSnapshot, selectedAttackGroups, damageResolutions);
            var delayedAttackEffects = ResolveDelayedAttackEffects(selectedAttackGroups, tickIndex);
            RecordDamageResolutionRecords(
                damageContests,
                damageResolutions,
                resolutionRecords);
            RecordDestroyResolutionRecords(
                attackDestroyContests,
                destroyResolutions,
                resolutionRecords);

            var attackStageBatch = new FinalizationBatch();
            var attackStageContext = new RecordingFinalizationContext(attackStageBatch);
            var attackDelayedSink = new RecordingDelayedAttackEffectSink(attackStageBatch);
            var attackCommitEvents = new List<string>();
            var delayedAttackDrainEvents = BuildDelayedAttackDrainEvents(tickIndex, drainedDelayedAttackEffects);
            var delayedAttackEnqueueEvents = new List<string>();
            _attackCommitter.CommitResolved(
                attackStageContext,
                attackDelayedSink,
                selectedAttackGroups,
                damageResolutions,
                destroyResolutions,
                delayedAttackEffects,
                attackCommitEvents,
                delayedAttackEnqueueEvents);
            finalizationBatch.MergeFrom(attackStageBatch);
            projectedWorld.ApplyBatch(attackStageBatch);

            var enemyActionAfterAttackBatch = new FinalizationBatch();
            var enemyActionAfterAttackContext = new RecordingFinalizationContext(enemyActionAfterAttackBatch);
            CommitEnemyActionState(
                projectedWorld.CreateSnapshot(),
                in input,
                entityLogicsForTick.EnemyActionStateLogics,
                EnemyActionStage.AfterAttack,
                enemyActionAfterAttackContext,
                enemyActionPhaseResult.AfterAttackTransitions);
            finalizationBatch.MergeFrom(enemyActionAfterAttackBatch);
            projectedWorld.ApplyBatch(enemyActionAfterAttackBatch);

            var afterAttackAiBatch = new FinalizationBatch();
            var afterAttackAiContext = new RecordingFinalizationContext(afterAttackAiBatch);
            CommitEnemyAiTransitions(
                projectedWorld.CreateSnapshot(),
                in input,
                entityLogicsForTick.AiStateLogics,
                EnemyAiTransitionStage.AfterAttack,
                afterAttackAiContext,
                aiPhaseResult.AfterAttackTransitions);
            finalizationBatch.MergeFrom(afterAttackAiBatch);
            projectedWorld.ApplyBatch(afterAttackAiBatch);
            var postAttackSnapshot = projectedWorld.CreateSnapshot();

            phaseTrace.Add("Resolve:Exit");
            completedPhases.Add(TickPhase.Resolve);

            var movementPhaseResult = new MovementPhaseResult(
                planPhaseResult.RawIntents,
                planPhaseResult.SortedIntents,
                planPhaseResult.ExpandedCandidates,
                selectedMovementGroups,
                movementCommitEvents,
                movementRejectedReasons);

            var attackEventLogEntries = new List<string>(delayedAttackDrainEvents.Count + attackCommitEvents.Count + delayedAttackEnqueueEvents.Count);
            AddRange(attackEventLogEntries, delayedAttackDrainEvents);
            AddRange(attackEventLogEntries, attackCommitEvents);
            AddRange(attackEventLogEntries, delayedAttackEnqueueEvents);

            var attackPhaseResult = new AttackPhaseResult(
                rawAttackIntents,
                drainedImpactReservations,
                drainedDelayedAttackEffects,
                damageResolutions,
                sortedInputs,
                expandedAttackCandidates,
                selectedAttackGroups,
                attackCommitEvents,
                attackEventLogEntries,
                attackRejectedReasons);

            return new ResolvePhaseResult(
                movementPhaseResult,
                attackPhaseResult,
                enemyActionPhaseResult,
                finalizationBatch,
                postMovementSnapshot,
                postAttackSnapshot,
                contests,
                resolutionRecords);
        }

        private void RunFinalizePhase(
            FinalizationBatch finalizationBatch,
            IWorldWriteContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Finalize:Enter");
            finalizationBatch.ApplyTo(writeContext, _delayedAttackEffectQueue);
            phaseTrace.Add("Finalize:Exit");
            completedPhases.Add(TickPhase.Finalize);
        }

        private static HashSet<int> CollectPlayerTraversalSourceIds(IReadOnlyList<IMovementEntityLogic> entityLogics)
        {
            var playerTraversalSourceIds = new HashSet<int>();

            for (var i = 0; i < entityLogics.Count; i++)
            {
                if (entityLogics[i] is PlayerLogic &&
                    entityLogics[i] is IEntityLogicSourceBinding binding)
                {
                    playerTraversalSourceIds.Add(binding.ControlledEntityId);
                }
            }

            return playerTraversalSourceIds;
        }


        private CleanupPhaseResult RunCleanupPhase(
            WorldSnapshot snapshot,
            int tickIndex,
            ICleanupCommitContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Cleanup:Enter");
            var cleanupPhaseResult = _cleanupProcessor.Process(snapshot, writeContext, tickIndex);
            phaseTrace.Add("Cleanup:Exit");
            completedPhases.Add(TickPhase.Cleanup);

            return cleanupPhaseResult;
        }

        private RespawnPhaseResult RunRespawnPhase(
            WorldSnapshot tickStartSnapshot,
            WorldSnapshot postCleanupSnapshot,
            CleanupPhaseResult cleanupPhaseResult,
            int tickIndex,
            IRespawnCommitContext writeContext,
            List<TickPhase> completedPhases,
            List<string> phaseTrace)
        {
            phaseTrace.Add("Respawn:Enter");
            var respawnPhaseResult = _respawnProcessor.Process(
                tickStartSnapshot,
                postCleanupSnapshot,
                cleanupPhaseResult,
                _playerRespawnTemplates,
                tickIndex,
                _playerRespawnDelayTicks,
                writeContext);
            phaseTrace.Add("Respawn:Exit");
            completedPhases.Add(TickPhase.Respawn);
            return respawnPhaseResult;
        }

        private static List<EntityState> BuildPlayerRespawnTemplates(
            WorldSnapshot snapshot,
            IReadOnlyList<IEntityLogic> entityLogics)
        {
            var playerEntityIds = new HashSet<int>();
            for (var i = 0; i < entityLogics.Count; i++)
            {
                if (entityLogics[i] is PlayerLogic &&
                    entityLogics[i] is IEntityLogicSourceBinding binding)
                {
                    playerEntityIds.Add(binding.ControlledEntityId);
                }
            }

            var templates = new List<EntityState>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].unitRole == UnitRole.Player ||
                    playerEntityIds.Contains(entities[i].entityId))
                {
                    templates.Add(entities[i]);
                }
            }

            return templates;
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
                        rawIntent.LocalSequence,
                        rawIntent.MoveCooldownTicks),
                    Movement.MovementCommandKind.Flip => new FlipIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence,
                        rawIntent.MoveCooldownTicks),
                    _ => new MoveIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence,
                        rawIntent.MoveCooldownTicks),
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

        private List<DamageResolutionRecord> ResolveDamageResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> selectedGroups,
            int tickIndex)
        {
            var damageResolutions = new List<DamageResolutionRecord>();
            var playerDamageStatesByEntityId = new Dictionary<int, PlayerDamageState>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    if (!snapshot.TryGetEntity(damage.TargetId, out var target) ||
                        !EntityRolePolicy.IsPlayerUnit(target))
                    {
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                group.GroupId,
                                group.IntentId,
                                group.SourceId,
                                group.AttackSourceKind,
                                damage.TargetId,
                                damage.Amount,
                                accepted: true,
                                DamageRejectReason.None,
                                localActionIndex: damageIndex));
                        continue;
                    }

                    if (!playerDamageStatesByEntityId.TryGetValue(damage.TargetId, out var damageState))
                    {
                        damageState = snapshot.TryGetPlayerDamageState(damage.TargetId, out var storedState)
                            ? storedState
                            : default;
                    }

                    if (!PlayerDamageQueries.CanAcceptDamage(damageState, tickIndex))
                    {
                        playerDamageStatesByEntityId[damage.TargetId] = damageState;
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                group.GroupId,
                                group.IntentId,
                                group.SourceId,
                                group.AttackSourceKind,
                                damage.TargetId,
                                damage.Amount,
                                accepted: false,
                                DamageRejectReason.ReceiverCooldown,
                                localActionIndex: damageIndex));
                        continue;
                    }

                    var updatedState = PlayerDamageQueries.AcceptDamage(
                        damageState,
                        tickIndex,
                        _playerDamageCooldownTicks);
                    playerDamageStatesByEntityId[damage.TargetId] = updatedState;
                    damageResolutions.Add(
                        new DamageResolutionRecord(
                            group.GroupId,
                            group.IntentId,
                            group.SourceId,
                            group.AttackSourceKind,
                            damage.TargetId,
                            damage.Amount,
                            accepted: true,
                            DamageRejectReason.None,
                            localActionIndex: damageIndex,
                            hasPlayerDamageState: true,
                            playerDamageState: updatedState));
                }
            }

            return damageResolutions;
        }

        private static List<DestroyResolutionRecord> ResolveDestroyResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> selectedGroups,
            IReadOnlyList<DamageResolutionRecord> damageResolutions)
        {
            var accumulatedDamageByTarget = new Dictionary<int, int>();
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                if (!damageResolutions[i].Accepted)
                {
                    continue;
                }

                if (accumulatedDamageByTarget.TryGetValue(damageResolutions[i].TargetId, out var existingDamage))
                {
                    accumulatedDamageByTarget[damageResolutions[i].TargetId] = existingDamage + damageResolutions[i].Amount;
                    continue;
                }

                accumulatedDamageByTarget[damageResolutions[i].TargetId] = damageResolutions[i].Amount;
            }

            var destroyResolutions = new List<DestroyResolutionRecord>();
            var destroyMarkedTargets = new HashSet<int>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    var accepted = false;
                    var finalHp = 0;

                    if (!destroyMarkedTargets.Contains(destroy.TargetId) &&
                        snapshot.TryGetEntity(destroy.TargetId, out var target) &&
                        !target.markedForDeath)
                    {
                        var accumulatedDamage = accumulatedDamageByTarget.TryGetValue(destroy.TargetId, out var damage)
                            ? damage
                            : 0;
                        finalHp = target.hp - accumulatedDamage;
                        if (destroy.Condition != DestroyCondition.WhenHpDepleted || finalHp <= 0)
                        {
                            destroyMarkedTargets.Add(destroy.TargetId);
                            accepted = true;
                        }
                    }

                    destroyResolutions.Add(
                        new DestroyResolutionRecord(
                            group.GroupId,
                            group.IntentId,
                            group.SourceId,
                            destroy.TargetId,
                            destroy.Condition,
                            finalHp,
                            accepted,
                            destroyIndex));
                }
            }

            return destroyResolutions;
        }

        private static List<DelayedAttackEffectRecord> ResolveDelayedAttackEffects(
            IReadOnlyList<ActionGroup> selectedGroups,
            int tickIndex)
        {
            var delayedAttackEffects = new List<DelayedAttackEffectRecord>();
            var delayedAttackSequence = 1;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var delayedIndex = 0; delayedIndex < group.DelayedAttacks.Count; delayedIndex++)
                {
                    var delayedAttack = group.DelayedAttacks[delayedIndex];
                    delayedAttackEffects.Add(
                        new DelayedAttackEffectRecord(
                            group.SourceId,
                            delayedAttack.TargetId,
                            delayedAttack.Damage,
                            group.Priority,
                            tickIndex,
                            tickIndex + 1,
                            group.GroupId,
                            delayedAttackSequence));
                    delayedAttackSequence++;
                }
            }

            return delayedAttackEffects;
        }

        private static List<Contest> BuildSpaceContests(
            IReadOnlyList<ActionGroup> candidates,
            ref int nextContestId)
        {
            var contests = new List<Contest>();

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Moves.Count == 0 &&
                    candidate.BoardPresenceChanges.Count == 0 &&
                    candidate.TopologyChanges.Count == 0)
                {
                    continue;
                }

                var hasAffectedCell = candidate.Moves.Count > 0;
                var affectedCell = hasAffectedCell
                    ? candidate.Moves[0].DestinationCell
                    : default;
                contests.Add(
                    new Contest(
                        nextContestId++,
                        ContestKind.Space,
                        candidate.GroupId,
                        candidate.SourceId,
                        candidate.Priority,
                        candidate.SourceId,
                        affectedCell,
                        hasAffectedCell,
                        localActionIndex: 0));
            }

            return contests;
        }

        private static List<Contest> BuildImpactContests(
            IReadOnlyList<ImpactReservation> impactReservations,
            ref int nextContestId)
        {
            var contests = new List<Contest>(impactReservations.Count);

            for (var i = 0; i < impactReservations.Count; i++)
            {
                var reservation = impactReservations[i];
                contests.Add(
                    new Contest(
                        nextContestId++,
                        ContestKind.Impact,
                        reservation.SourceActionGroupId,
                        reservation.SourceId,
                        priority: 0,
                        reservation.TargetId,
                        new SurfaceCell(FaceId.Floor, reservation.Position.x, reservation.Position.y),
                        hasAffectedCell: true,
                        localActionIndex: reservation.ReservationSequence));
            }

            return contests;
        }

        private static List<Contest> BuildDamageContests(
            IReadOnlyList<ActionGroup> selectedGroups,
            ref int nextContestId)
        {
            var contests = new List<Contest>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    contests.Add(
                        new Contest(
                            nextContestId++,
                            ContestKind.Damage,
                            group.GroupId,
                            group.SourceId,
                            group.Priority,
                            damage.TargetId,
                            default,
                            hasAffectedCell: false,
                            localActionIndex: damageIndex));
                }
            }

            return contests;
        }

        private static List<Contest> BuildDestroyContests(
            IReadOnlyList<ActionGroup> selectedGroups,
            ref int nextContestId)
        {
            var contests = new List<Contest>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    contests.Add(
                        new Contest(
                            nextContestId++,
                            ContestKind.Destroy,
                            group.GroupId,
                            group.SourceId,
                            group.Priority,
                            destroy.TargetId,
                            default,
                            hasAffectedCell: false,
                            localActionIndex: destroyIndex,
                            destroyCondition: destroy.Condition));
                }
            }

            return contests;
        }

        private static void RecordSpaceResolutionRecords(
            IReadOnlyList<Contest> spaceContests,
            IReadOnlyList<ActionGroup> selectedGroups,
            List<ResolutionRecord> resolutionRecords)
        {
            var selectedGroupIds = new HashSet<int>();
            for (var i = 0; i < selectedGroups.Count; i++)
            {
                selectedGroupIds.Add(selectedGroups[i].GroupId);
            }

            for (var i = 0; i < spaceContests.Count; i++)
            {
                resolutionRecords.Add(
                    CreateResolutionRecord(
                        spaceContests[i],
                        selectedGroupIds.Contains(spaceContests[i].ActionPlanId)));
            }
        }

        private static void RecordAcceptedResolutionRecords(
            IReadOnlyList<Contest> contests,
            List<ResolutionRecord> resolutionRecords)
        {
            for (var i = 0; i < contests.Count; i++)
            {
                resolutionRecords.Add(CreateResolutionRecord(contests[i], accepted: true));
            }
        }

        private static void RecordDamageResolutionRecords(
            IReadOnlyList<Contest> damageContests,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            List<ResolutionRecord> resolutionRecords)
        {
            var consumed = new bool[damageResolutions.Count];

            for (var contestIndex = 0; contestIndex < damageContests.Count; contestIndex++)
            {
                var contest = damageContests[contestIndex];
                var accepted = false;

                for (var resolutionIndex = 0; resolutionIndex < damageResolutions.Count; resolutionIndex++)
                {
                    if (consumed[resolutionIndex])
                    {
                        continue;
                    }

                    var damageResolution = damageResolutions[resolutionIndex];
                    if (damageResolution.GroupId != contest.ActionPlanId ||
                        damageResolution.TargetId != contest.AffectedEntityId ||
                        damageResolution.LocalActionIndex != contest.LocalActionIndex)
                    {
                        continue;
                    }

                    accepted = damageResolution.Accepted;
                    consumed[resolutionIndex] = true;
                    break;
                }

                resolutionRecords.Add(CreateResolutionRecord(contest, accepted));
            }
        }

        private static void RecordDestroyResolutionRecords(
            IReadOnlyList<Contest> destroyContests,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            List<ResolutionRecord> resolutionRecords)
        {
            var consumed = new bool[destroyResolutions.Count];

            for (var contestIndex = 0; contestIndex < destroyContests.Count; contestIndex++)
            {
                var contest = destroyContests[contestIndex];
                var accepted = false;

                for (var resolutionIndex = 0; resolutionIndex < destroyResolutions.Count; resolutionIndex++)
                {
                    if (consumed[resolutionIndex])
                    {
                        continue;
                    }

                    var destroyResolution = destroyResolutions[resolutionIndex];
                    if (destroyResolution.GroupId != contest.ActionPlanId ||
                        destroyResolution.TargetId != contest.AffectedEntityId ||
                        destroyResolution.LocalActionIndex != contest.LocalActionIndex)
                    {
                        continue;
                    }

                    accepted = destroyResolution.Accepted;
                    consumed[resolutionIndex] = true;
                    break;
                }

                resolutionRecords.Add(CreateResolutionRecord(contest, accepted));
            }
        }

        private static ResolutionRecord CreateResolutionRecord(Contest contest, bool accepted)
        {
            return new ResolutionRecord(
                contest.ContestId,
                contest.Kind,
                accepted,
                contest.SourceId,
                contest.Priority,
                contest.ActionPlanId,
                contest.AffectedEntityId,
                contest.LocalActionIndex);
        }

        private static List<RawMovementIntent> FilterExecutionLockedMovementIntents(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<RawMovementIntent> rawMovementIntents,
            List<string> rejectedReasons)
        {
            var filteredIntents = new List<RawMovementIntent>(rawMovementIntents.Count);

            for (var i = 0; i < rawMovementIntents.Count; i++)
            {
                var rawIntent = rawMovementIntents[i];
                if (snapshot.CanExecuteMovementIntent(rawIntent.SourceId, tickIndex))
                {
                    filteredIntents.Add(rawIntent);
                    continue;
                }

                rejectedReasons.Add(
                    BuildExecutionLockRejectedReason(
                        "MovementRejected",
                        rawIntent.SourceId,
                        tickIndex,
                        snapshot));
            }

            return filteredIntents;
        }

        private static List<RawAttackIntent> FilterExecutionLockedAttackIntents(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<RawAttackIntent> rawAttackIntents,
            List<string> rejectedReasons)
        {
            var filteredIntents = new List<RawAttackIntent>(rawAttackIntents.Count);

            for (var i = 0; i < rawAttackIntents.Count; i++)
            {
                var rawIntent = rawAttackIntents[i];
                if (snapshot.CanExecuteIntent(rawIntent.SourceId, tickIndex))
                {
                    filteredIntents.Add(rawIntent);
                    continue;
                }

                rejectedReasons.Add(
                    BuildExecutionLockRejectedReason(
                        "AttackRejected",
                        rawIntent.SourceId,
                        tickIndex,
                        snapshot));
            }

            return filteredIntents;
        }

        private static string BuildExecutionLockRejectedReason(
            string prefix,
            int sourceId,
            int tickIndex,
            WorldSnapshot snapshot)
        {
            snapshot.TryGetEntityExecutionLockState(sourceId, out var lockState);
            return $"{prefix}|Stage=ExecutionLock|Source={sourceId}|Reason=Busy|Phase={lockState.phase}|Sequence={lockState.sequence}|UnlockTickExclusive={lockState.unlockTickExclusive}|Tick={tickIndex}";
        }

        private static void AddRange<T>(List<T> destination, IReadOnlyList<T> source)
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

    }

    internal sealed class RespawnProcessor
    {
        private readonly Dictionary<int, int> _eligibleRespawnTicksByEntityId = new();

        public RespawnPhaseResult Process(
            WorldSnapshot tickStartSnapshot,
            WorldSnapshot postCleanupSnapshot,
            CleanupPhaseResult cleanupPhaseResult,
            IReadOnlyList<EntityState> respawnTemplates,
            int tickIndex,
            int respawnDelayTicks,
            IRespawnCommitContext writeContext)
        {
            if (tickStartSnapshot == null)
            {
                throw new ArgumentNullException(nameof(tickStartSnapshot));
            }

            if (postCleanupSnapshot == null)
            {
                throw new ArgumentNullException(nameof(postCleanupSnapshot));
            }

            if (cleanupPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(cleanupPhaseResult));
            }

            if (respawnTemplates == null)
            {
                throw new ArgumentNullException(nameof(respawnTemplates));
            }

            if (respawnDelayTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(respawnDelayTicks),
                    "Respawn delay ticks must be greater than zero.");
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            var respawnedEntities = new List<EntityState>();
            var eventLogEntries = new List<string>();
            var removedEntityIdsThisTick = cleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(cleanupPhaseResult.RemovedEntityIds)
                : null;

            for (var i = 0; i < respawnTemplates.Count; i++)
            {
                var template = respawnTemplates[i];
                var entityId = template.entityId;
                var existedAtTickStart = tickStartSnapshot.TryGetEntity(entityId, out _);
                var existsAfterCleanup = postCleanupSnapshot.TryGetEntity(entityId, out _);
                var removedThisTick = removedEntityIdsThisTick != null &&
                    removedEntityIdsThisTick.Contains(entityId);

                if (existsAfterCleanup)
                {
                    _eligibleRespawnTicksByEntityId.Remove(entityId);
                    continue;
                }

                if (removedThisTick)
                {
                    _eligibleRespawnTicksByEntityId[entityId] = tickIndex + respawnDelayTicks;
                }
                else if (existedAtTickStart)
                {
                    _eligibleRespawnTicksByEntityId.Remove(entityId);
                    continue;
                }
                else if (!_eligibleRespawnTicksByEntityId.ContainsKey(entityId))
                {
                    _eligibleRespawnTicksByEntityId[entityId] = tickIndex;
                }

                if (tickIndex < _eligibleRespawnTicksByEntityId[entityId])
                {
                    continue;
                }

                var respawnEntity = BuildRespawnEntity(template, tickIndex);
                if (postCleanupSnapshot.TryGetPlacementBlocker(
                        respawnEntity.type,
                        respawnEntity.position,
                        ignoredEntityId: 0,
                        out var blocker))
                {
                    eventLogEntries.Add(FormatRespawnSkippedEvent(respawnEntity, blocker, tickIndex));
                    continue;
                }

                writeContext.SpawnEntity(respawnEntity);
                writeContext.SetPlayerControlState(respawnEntity.entityId, default);
                writeContext.SetPlayerDamageState(respawnEntity.entityId, default);
                respawnedEntities.Add(respawnEntity);
                _eligibleRespawnTicksByEntityId.Remove(entityId);
                eventLogEntries.Add(
                    $"RespawnCommitted|E={respawnEntity.entityId}|Pos=({respawnEntity.position.x},{respawnEntity.position.y})|Face={respawnEntity.position.face}|Facing={respawnEntity.facing}|Tick={tickIndex}");
            }

            return new RespawnPhaseResult(respawnedEntities, eventLogEntries);
        }

        private static EntityState BuildRespawnEntity(EntityState template, int tickIndex)
        {
            var maxHp = template.maxHp > 0 ? template.maxHp : template.hp;
            var respawnEntity = template;
            respawnEntity.hp = maxHp;
            respawnEntity.maxHp = maxHp;
            respawnEntity.state = EntityPhaseState.Idle;
            respawnEntity.stateTimer = 0;
            respawnEntity.boardPresence = EntityBoardPresence.Occupying;
            respawnEntity.markedForDeath = false;
            respawnEntity.spawnTick = tickIndex;
            respawnEntity.kineticInstigatorEntityId = 0;
            respawnEntity.kineticInstigatorTeamId = 0;
            respawnEntity.aiStateTimer = 0;
            respawnEntity.enemyLocomotionCooldownTicks = 0;
            return respawnEntity;
        }

        private static string FormatRespawnSkippedEvent(
            EntityState entity,
            SlideStopper blocker,
            int tickIndex)
        {
            var prefix =
                $"RespawnSkipped|E={entity.entityId}|Pos=({entity.position.x},{entity.position.y})|Face={entity.position.face}|Tick={tickIndex}|Reason={blocker.Kind}";
            return blocker.Kind == SlideStopperKind.Entity
                ? $"{prefix}|BlockerEntity={blocker.EntityId}|BlockerType={blocker.EntityType}"
                : prefix;
        }
    }

    internal sealed class EnemyAiPhaseResult
    {
        public EnemyAiPhaseResult(
            List<string> beforeMovementTransitions,
            List<string> beforeAttackTransitions,
            List<string> afterAttackTransitions)
        {
            BeforeMovementTransitions = beforeMovementTransitions ?? throw new ArgumentNullException(nameof(beforeMovementTransitions));
            BeforeAttackTransitions = beforeAttackTransitions ?? throw new ArgumentNullException(nameof(beforeAttackTransitions));
            AfterAttackTransitions = afterAttackTransitions ?? throw new ArgumentNullException(nameof(afterAttackTransitions));
        }

        public List<string> BeforeMovementTransitions { get; }

        public List<string> BeforeAttackTransitions { get; }

        public List<string> AfterAttackTransitions { get; }
    }

    internal sealed class EnemyActionPhaseResult
    {
        public EnemyActionPhaseResult(
            List<EnemyActionTransition> beforeAttackCollectionTransitions,
            List<EnemyActionTransition> afterAttackTransitions)
        {
            BeforeAttackCollectionTransitions = beforeAttackCollectionTransitions ?? throw new ArgumentNullException(nameof(beforeAttackCollectionTransitions));
            AfterAttackTransitions = afterAttackTransitions ?? throw new ArgumentNullException(nameof(afterAttackTransitions));
        }

        public List<EnemyActionTransition> BeforeAttackCollectionTransitions { get; }

        public List<EnemyActionTransition> AfterAttackTransitions { get; }
    }

    internal sealed class PreMovementStatePhaseResult
    {
        public PreMovementStatePhaseResult(List<string> updates)
            : this(updates, new List<PlayerActionTransition>())
        {
        }

        public PreMovementStatePhaseResult(
            List<string> updates,
            List<PlayerActionTransition> playerActionTransitions)
        {
            Updates = updates ?? throw new ArgumentNullException(nameof(updates));
            PlayerActionTransitions = playerActionTransitions ?? throw new ArgumentNullException(nameof(playerActionTransitions));
        }

        public List<string> Updates { get; }

        public List<PlayerActionTransition> PlayerActionTransitions { get; }
    }

    internal sealed class PlanPhaseResult
    {
        public PlanPhaseResult(
            List<RawMovementIntent> rawIntents,
            List<MoveIntent> sortedIntents,
            List<ActionGroup> expandedCandidates,
            List<string> rejectedReasons,
            List<Contest> spaceContests,
            int nextContestId,
            EnemyAiPhaseResult enemyAiPhaseResult,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            WorldSnapshot postEnemyAiSnapshot,
            WorldSnapshot planSnapshot,
            FinalizationBatch planFinalizationBatch)
        {
            RawIntents = rawIntents ?? throw new ArgumentNullException(nameof(rawIntents));
            SortedIntents = sortedIntents ?? throw new ArgumentNullException(nameof(sortedIntents));
            ExpandedCandidates = expandedCandidates ?? throw new ArgumentNullException(nameof(expandedCandidates));
            RejectedReasons = rejectedReasons ?? throw new ArgumentNullException(nameof(rejectedReasons));
            SpaceContests = spaceContests ?? throw new ArgumentNullException(nameof(spaceContests));
            NextContestId = nextContestId;
            EnemyAiPhaseResult = enemyAiPhaseResult ?? throw new ArgumentNullException(nameof(enemyAiPhaseResult));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            PostEnemyAiSnapshot = postEnemyAiSnapshot ?? throw new ArgumentNullException(nameof(postEnemyAiSnapshot));
            PlanSnapshot = planSnapshot ?? throw new ArgumentNullException(nameof(planSnapshot));
            PlanFinalizationBatch = planFinalizationBatch ?? throw new ArgumentNullException(nameof(planFinalizationBatch));
        }

        public List<RawMovementIntent> RawIntents { get; }

        public List<MoveIntent> SortedIntents { get; }

        public List<ActionGroup> ExpandedCandidates { get; }

        public List<string> RejectedReasons { get; }

        public List<Contest> SpaceContests { get; }

        public int NextContestId { get; }

        public EnemyAiPhaseResult EnemyAiPhaseResult { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public WorldSnapshot PostEnemyAiSnapshot { get; }

        public WorldSnapshot PlanSnapshot { get; }

        public FinalizationBatch PlanFinalizationBatch { get; }
    }

    internal sealed class ResolvePhaseResult
    {
        public ResolvePhaseResult(
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            EnemyActionPhaseResult enemyActionPhaseResult,
            FinalizationBatch finalizationBatch,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            List<Contest> contests,
            List<ResolutionRecord> resolutionRecords)
        {
            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            EnemyActionPhaseResult = enemyActionPhaseResult ?? throw new ArgumentNullException(nameof(enemyActionPhaseResult));
            FinalizationBatch = finalizationBatch ?? throw new ArgumentNullException(nameof(finalizationBatch));
            PostMovementSnapshot = postMovementSnapshot ?? throw new ArgumentNullException(nameof(postMovementSnapshot));
            PostAttackSnapshot = postAttackSnapshot ?? throw new ArgumentNullException(nameof(postAttackSnapshot));
            Contests = contests ?? throw new ArgumentNullException(nameof(contests));
            ResolutionRecords = resolutionRecords ?? throw new ArgumentNullException(nameof(resolutionRecords));
        }

        public MovementPhaseResult MovementPhaseResult { get; }

        public AttackPhaseResult AttackPhaseResult { get; }

        public EnemyActionPhaseResult EnemyActionPhaseResult { get; }

        public FinalizationBatch FinalizationBatch { get; }

        public WorldSnapshot PostMovementSnapshot { get; }

        public WorldSnapshot PostAttackSnapshot { get; }

        public List<Contest> Contests { get; }

        public List<ResolutionRecord> ResolutionRecords { get; }
    }

    internal enum ContestKind
    {
        Space = 0,
        Impact = 1,
        Damage = 2,
        Destroy = 3,
    }

    internal readonly struct Contest
    {
        public Contest(
            int contestId,
            ContestKind kind,
            int actionPlanId,
            int sourceId,
            int priority,
            int affectedEntityId,
            SurfaceCell affectedCell,
            bool hasAffectedCell,
            int localActionIndex,
            DestroyCondition destroyCondition = DestroyCondition.WhenHpDepleted)
        {
            ContestId = contestId;
            Kind = kind;
            ActionPlanId = actionPlanId;
            SourceId = sourceId;
            Priority = priority;
            AffectedEntityId = affectedEntityId;
            AffectedCell = affectedCell;
            HasAffectedCell = hasAffectedCell;
            LocalActionIndex = localActionIndex;
            DestroyCondition = destroyCondition;
        }

        public int ContestId { get; }

        public ContestKind Kind { get; }

        public int ActionPlanId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int AffectedEntityId { get; }

        public SurfaceCell AffectedCell { get; }

        public bool HasAffectedCell { get; }

        public int LocalActionIndex { get; }

        public DestroyCondition DestroyCondition { get; }
    }

    internal readonly struct ResolutionRecord
    {
        public ResolutionRecord(
            int contestId,
            ContestKind kind,
            bool accepted,
            int sourceId,
            int priority,
            int actionPlanId,
            int affectedEntityId,
            int localActionIndex)
        {
            ContestId = contestId;
            Kind = kind;
            Accepted = accepted;
            SourceId = sourceId;
            Priority = priority;
            ActionPlanId = actionPlanId;
            AffectedEntityId = affectedEntityId;
            LocalActionIndex = localActionIndex;
        }

        public int ContestId { get; }

        public ContestKind Kind { get; }

        public bool Accepted { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int ActionPlanId { get; }

        public int AffectedEntityId { get; }

        public int LocalActionIndex { get; }
    }

    internal readonly struct DestroyResolutionRecord
    {
        public DestroyResolutionRecord(
            int groupId,
            int intentId,
            int sourceId,
            int targetId,
            DestroyCondition condition,
            int finalHp,
            bool accepted,
            int localActionIndex)
        {
            GroupId = groupId;
            IntentId = intentId;
            SourceId = sourceId;
            TargetId = targetId;
            Condition = condition;
            FinalHp = finalHp;
            Accepted = accepted;
            LocalActionIndex = localActionIndex;
        }

        public int GroupId { get; }

        public int IntentId { get; }

        public int SourceId { get; }

        public int TargetId { get; }

        public DestroyCondition Condition { get; }

        public int FinalHp { get; }

        public bool Accepted { get; }

        public int LocalActionIndex { get; }
    }

    internal enum FinalizationOperationBucket
    {
        NonHpState = 0,
        DamageState = 1,
        Spawn = 2,
        Destroy = 3,
    }

    internal enum FinalizationOperationKind
    {
        MoveEntity = 0,
        ApplyStateChange = 1,
        SetFacing = 2,
        SetBoxKineticOwner = 3,
        SetBoardPresence = 4,
        SetEnemyLocomotionCooldown = 5,
        SetEntityExecutionLockState = 6,
        SetTopology = 7,
        SetPlayerControlState = 8,
        SetPlayerDamageState = 9,
        ApplyEnemyAiState = 10,
        SetEnemyActionState = 11,
        SetEnemyJumpState = 12,
        SpawnEntity = 13,
        ApplyDamage = 14,
        MarkDestroy = 15,
    }

    internal sealed class FinalizationOperation
    {
        private FinalizationOperation(
            long sequence,
            FinalizationOperationBucket bucket,
            FinalizationOperationKind kind,
            int entityId = 0,
            SurfaceCell destination = default,
            int amount = 0,
            EntityPhaseState phaseState = default,
            int stateTimer = 0,
            Direction facing = default,
            int instigatorEntityId = 0,
            int instigatorTeamId = 0,
            EntityBoardPresence boardPresence = default,
            int cooldownTicks = 0,
            EntityExecutionLockState executionLockState = default,
            CubeTopologyState topology = default,
            PlayerControlState playerControlState = default,
            PlayerDamageState playerDamageState = default,
            EnemyAiMode enemyAiMode = default,
            int enemyAiStateTimer = 0,
            EnemyActionRuntimeState enemyActionState = default,
            EnemyJumpRuntimeState enemyJumpState = default,
            EntityState spawnEntity = default)
        {
            Sequence = sequence;
            Bucket = bucket;
            Kind = kind;
            EntityId = entityId;
            Destination = destination;
            Amount = amount;
            PhaseState = phaseState;
            StateTimer = stateTimer;
            Facing = facing;
            InstigatorEntityId = instigatorEntityId;
            InstigatorTeamId = instigatorTeamId;
            BoardPresence = boardPresence;
            CooldownTicks = cooldownTicks;
            ExecutionLockState = executionLockState;
            Topology = topology;
            PlayerControlState = playerControlState;
            PlayerDamageState = playerDamageState;
            EnemyAiMode = enemyAiMode;
            EnemyAiStateTimer = enemyAiStateTimer;
            EnemyActionState = enemyActionState;
            EnemyJumpState = enemyJumpState;
            SpawnedEntity = spawnEntity;
        }

        public long Sequence { get; }

        public FinalizationOperationBucket Bucket { get; }

        public FinalizationOperationKind Kind { get; }

        public int EntityId { get; }

        public SurfaceCell Destination { get; }

        public int Amount { get; }

        public EntityPhaseState PhaseState { get; }

        public int StateTimer { get; }

        public Direction Facing { get; }

        public int InstigatorEntityId { get; }

        public int InstigatorTeamId { get; }

        public EntityBoardPresence BoardPresence { get; }

        public int CooldownTicks { get; }

        public EntityExecutionLockState ExecutionLockState { get; }

        public CubeTopologyState Topology { get; }

        public PlayerControlState PlayerControlState { get; }

        public PlayerDamageState PlayerDamageState { get; }

        public EnemyAiMode EnemyAiMode { get; }

        public int EnemyAiStateTimer { get; }

        public EnemyActionRuntimeState EnemyActionState { get; }

        public EnemyJumpRuntimeState EnemyJumpState { get; }

        public EntityState SpawnedEntity { get; }

        public FinalizationOperation WithSequence(long sequence)
        {
            return new FinalizationOperation(
                sequence,
                Bucket,
                Kind,
                EntityId,
                Destination,
                Amount,
                PhaseState,
                StateTimer,
                Facing,
                InstigatorEntityId,
                InstigatorTeamId,
                BoardPresence,
                CooldownTicks,
                ExecutionLockState,
                Topology,
                PlayerControlState,
                PlayerDamageState,
                EnemyAiMode,
                EnemyAiStateTimer,
                EnemyActionState,
                EnemyJumpState,
                SpawnedEntity);
        }

        public static FinalizationOperation MoveEntity(long sequence, int entityId, SurfaceCell destination)
        {
            return new FinalizationOperation(sequence, FinalizationOperationBucket.NonHpState, FinalizationOperationKind.MoveEntity, entityId, destination);
        }

        public static FinalizationOperation ApplyStateChange(long sequence, int entityId, EntityPhaseState state, int stateTimer)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ApplyStateChange,
                entityId: entityId,
                phaseState: state,
                stateTimer: stateTimer);
        }

        public static FinalizationOperation SetFacing(long sequence, int entityId, Direction facing)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetFacing,
                entityId: entityId,
                facing: facing);
        }

        public static FinalizationOperation SetBoxKineticOwner(long sequence, int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoxKineticOwner,
                entityId: entityId,
                instigatorEntityId: instigatorEntityId,
                instigatorTeamId: instigatorTeamId);
        }

        public static FinalizationOperation SetBoardPresence(long sequence, int entityId, EntityBoardPresence boardPresence)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoardPresence,
                entityId: entityId,
                boardPresence: boardPresence);
        }

        public static FinalizationOperation SetEnemyLocomotionCooldown(long sequence, int entityId, int cooldownTicks)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyLocomotionCooldown,
                entityId: entityId,
                cooldownTicks: cooldownTicks);
        }

        public static FinalizationOperation SetEntityExecutionLockState(long sequence, int entityId, EntityExecutionLockState executionLockState)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEntityExecutionLockState,
                entityId: entityId,
                executionLockState: executionLockState);
        }

        public static FinalizationOperation SetTopology(long sequence, CubeTopologyState topology)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetTopology,
                topology: topology);
        }

        public static FinalizationOperation SetPlayerControlState(long sequence, int entityId, PlayerControlState playerControlState)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetPlayerControlState,
                entityId: entityId,
                playerControlState: playerControlState);
        }

        public static FinalizationOperation SetPlayerDamageState(long sequence, int entityId, PlayerDamageState playerDamageState)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DamageState,
                FinalizationOperationKind.SetPlayerDamageState,
                entityId: entityId,
                playerDamageState: playerDamageState);
        }

        public static FinalizationOperation ApplyEnemyAiState(long sequence, int entityId, EnemyAiMode enemyAiMode, int enemyAiStateTimer)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ApplyEnemyAiState,
                entityId: entityId,
                enemyAiMode: enemyAiMode,
                enemyAiStateTimer: enemyAiStateTimer);
        }

        public static FinalizationOperation SetEnemyActionState(long sequence, int entityId, EnemyActionRuntimeState enemyActionState)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyActionState,
                entityId: entityId,
                enemyActionState: enemyActionState);
        }

        public static FinalizationOperation SetEnemyJumpState(long sequence, int entityId, EnemyJumpRuntimeState enemyJumpState)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyJumpState,
                entityId: entityId,
                enemyJumpState: enemyJumpState);
        }

        public static FinalizationOperation SpawnEntity(long sequence, EntityState entity)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Spawn,
                FinalizationOperationKind.SpawnEntity,
                spawnEntity: entity);
        }

        public static FinalizationOperation ApplyDamage(long sequence, int entityId, int amount)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DamageState,
                FinalizationOperationKind.ApplyDamage,
                entityId: entityId,
                amount: amount);
        }

        public static FinalizationOperation MarkDestroy(long sequence, int entityId)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Destroy,
                FinalizationOperationKind.MarkDestroy,
                entityId: entityId);
        }
    }

    internal sealed class FinalizationBatch
    {
        private readonly List<DelayedAttackEffectRecord> _delayedAttackEffects = new();
        private readonly List<FinalizationOperation> _operations = new();
        private long _nextSequence = 1;

        public IReadOnlyList<FinalizationOperation> Operations => _operations;

        public IReadOnlyList<DelayedAttackEffectRecord> DelayedAttackEffects => _delayedAttackEffects;

        public void MoveEntity(int entityId, SurfaceCell destination)
        {
            _operations.Add(FinalizationOperation.MoveEntity(_nextSequence++, entityId, destination));
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            _operations.Add(FinalizationOperation.ApplyStateChange(_nextSequence++, entityId, state, stateTimer));
        }

        public void SetFacing(int entityId, Direction facing)
        {
            _operations.Add(FinalizationOperation.SetFacing(_nextSequence++, entityId, facing));
        }

        public void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            _operations.Add(FinalizationOperation.SetBoxKineticOwner(_nextSequence++, entityId, instigatorEntityId, instigatorTeamId));
        }

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _operations.Add(FinalizationOperation.SetBoardPresence(_nextSequence++, entityId, boardPresence));
        }

        public void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks)
        {
            _operations.Add(FinalizationOperation.SetEnemyLocomotionCooldown(_nextSequence++, entityId, cooldownTicks));
        }

        public void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state)
        {
            _operations.Add(FinalizationOperation.SetEntityExecutionLockState(_nextSequence++, entityId, state));
        }

        public void SetTopology(CubeTopologyState topology)
        {
            _operations.Add(FinalizationOperation.SetTopology(_nextSequence++, topology));
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state)
        {
            _operations.Add(FinalizationOperation.SetPlayerControlState(_nextSequence++, entityId, state));
        }

        public void SetPlayerDamageState(int entityId, PlayerDamageState state)
        {
            _operations.Add(FinalizationOperation.SetPlayerDamageState(_nextSequence++, entityId, state));
        }

        public void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer)
        {
            _operations.Add(FinalizationOperation.ApplyEnemyAiState(_nextSequence++, entityId, aiMode, aiStateTimer));
        }

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            _operations.Add(FinalizationOperation.SetEnemyActionState(_nextSequence++, entityId, state));
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            _operations.Add(FinalizationOperation.SetEnemyJumpState(_nextSequence++, entityId, state));
        }

        public void SpawnEntity(EntityState entity)
        {
            _operations.Add(FinalizationOperation.SpawnEntity(_nextSequence++, entity));
        }

        public void ApplyDamage(int entityId, int amount)
        {
            _operations.Add(FinalizationOperation.ApplyDamage(_nextSequence++, entityId, amount));
        }

        public void MarkDestroy(int entityId)
        {
            _operations.Add(FinalizationOperation.MarkDestroy(_nextSequence++, entityId));
        }

        public void EnqueueDelayedAttackEffect(DelayedAttackEffectRecord effectRecord)
        {
            _delayedAttackEffects.Add(effectRecord);
        }

        public void MergeFrom(FinalizationBatch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            for (var i = 0; i < batch._operations.Count; i++)
            {
                _operations.Add(batch._operations[i].WithSequence(_nextSequence++));
            }

            for (var i = 0; i < batch._delayedAttackEffects.Count; i++)
            {
                _delayedAttackEffects.Add(batch._delayedAttackEffects[i]);
            }
        }

        public void ApplyTo(IWorldWriteContext writeContext, IDelayedAttackEffectSink delayedAttackEffectSink)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            ApplyBucket(writeContext, FinalizationOperationBucket.NonHpState);
            ApplyBucket(writeContext, FinalizationOperationBucket.DamageState);
            ApplyBucket(writeContext, FinalizationOperationBucket.Spawn);
            ApplyBucket(writeContext, FinalizationOperationBucket.Destroy);

            if (delayedAttackEffectSink == null)
            {
                return;
            }

            for (var i = 0; i < _delayedAttackEffects.Count; i++)
            {
                delayedAttackEffectSink.Enqueue(_delayedAttackEffects[i]);
            }
        }

        private void ApplyBucket(
            IWorldWriteContext writeContext,
            FinalizationOperationBucket bucket)
        {
            for (var i = 0; i < _operations.Count; i++)
            {
                var operation = _operations[i];
                if (operation.Bucket != bucket)
                {
                    continue;
                }

                switch (operation.Kind)
                {
                    case FinalizationOperationKind.MoveEntity:
                        ((IMovementCommitContext)writeContext).MoveEntity(operation.EntityId, operation.Destination);
                        break;

                    case FinalizationOperationKind.ApplyStateChange:
                        ((IMovementCommitContext)writeContext).ApplyStateChange(operation.EntityId, operation.PhaseState, operation.StateTimer);
                        break;

                    case FinalizationOperationKind.SetFacing:
                        ((IEnemyAiCommitContext)writeContext).SetFacing(operation.EntityId, operation.Facing);
                        break;

                    case FinalizationOperationKind.SetBoxKineticOwner:
                        writeContext.SetBoxKineticOwner(
                            operation.EntityId,
                            operation.InstigatorEntityId,
                            operation.InstigatorTeamId);
                        break;

                    case FinalizationOperationKind.SetBoardPresence:
                        ((IMovementCommitContext)writeContext).SetBoardPresence(operation.EntityId, operation.BoardPresence);
                        break;

                    case FinalizationOperationKind.SetEnemyLocomotionCooldown:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyLocomotionCooldown(operation.EntityId, operation.CooldownTicks);
                        break;

                    case FinalizationOperationKind.SetEntityExecutionLockState:
                        ((IMovementCommitContext)writeContext).SetEntityExecutionLockState(operation.EntityId, operation.ExecutionLockState);
                        break;

                    case FinalizationOperationKind.SetTopology:
                        ((IMovementCommitContext)writeContext).SetTopology(operation.Topology);
                        break;

                    case FinalizationOperationKind.SetPlayerControlState:
                        ((IPlayerControlCommitContext)writeContext).SetPlayerControlState(operation.EntityId, operation.PlayerControlState);
                        break;

                    case FinalizationOperationKind.SetPlayerDamageState:
                        ((IPlayerDamageCommitContext)writeContext).SetPlayerDamageState(operation.EntityId, operation.PlayerDamageState);
                        break;

                    case FinalizationOperationKind.ApplyEnemyAiState:
                        ((IEnemyAiCommitContext)writeContext).ApplyEnemyAiState(operation.EntityId, operation.EnemyAiMode, operation.EnemyAiStateTimer);
                        break;

                    case FinalizationOperationKind.SetEnemyActionState:
                        ((IEnemyActionCommitContext)writeContext).SetEnemyActionState(operation.EntityId, operation.EnemyActionState);
                        break;

                    case FinalizationOperationKind.SetEnemyJumpState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyJumpState(operation.EntityId, operation.EnemyJumpState);
                        break;

                    case FinalizationOperationKind.SpawnEntity:
                        ((IAttackCommitContext)writeContext).SpawnEntity(operation.SpawnedEntity);
                        break;

                    case FinalizationOperationKind.ApplyDamage:
                        ((IAttackCommitContext)writeContext).ApplyDamage(operation.EntityId, operation.Amount);
                        break;

                    case FinalizationOperationKind.MarkDestroy:
                        ((IAttackCommitContext)writeContext).MarkDestroy(operation.EntityId);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    internal sealed class RecordingFinalizationContext : IWorldWriteContext
    {
        private readonly FinalizationBatch _batch;

        public RecordingFinalizationContext(FinalizationBatch batch)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
        }

        public void MoveEntity(int entityId, SurfaceCell destination)
        {
            _batch.MoveEntity(entityId, destination);
        }

        public void ApplyDamage(int entityId, int amount)
        {
            _batch.ApplyDamage(entityId, amount);
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            _batch.ApplyStateChange(entityId, state, stateTimer);
        }

        public void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer)
        {
            _batch.ApplyEnemyAiState(entityId, aiMode, aiStateTimer);
        }

        public void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks)
        {
            _batch.SetEnemyLocomotionCooldown(entityId, cooldownTicks);
        }

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            _batch.SetEnemyActionState(entityId, state);
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            _batch.SetEnemyJumpState(entityId, state);
        }

        public void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state)
        {
            _batch.SetEntityExecutionLockState(entityId, state);
        }

        public void MoveEnemyJumpEntity(int entityId, SurfaceCell destination)
        {
            _batch.MoveEntity(entityId, destination);
        }

        public void SetEnemyJumpBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _batch.SetBoardPresence(entityId, boardPresence);
        }

        public void SetFacing(int entityId, Direction facing)
        {
            _batch.SetFacing(entityId, facing);
        }

        public void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            _batch.SetBoxKineticOwner(entityId, instigatorEntityId, instigatorTeamId);
        }

        public void MarkDestroy(int entityId)
        {
            _batch.MarkDestroy(entityId);
        }

        public void SpawnEntity(EntityState entity)
        {
            _batch.SpawnEntity(entity);
        }

        public void RemoveEntity(int entityId)
        {
            throw new NotSupportedException("Finalize recording does not support cleanup removes.");
        }

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            _batch.SetBoardPresence(entityId, boardPresence);
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state)
        {
            _batch.SetPlayerControlState(entityId, state);
        }

        public void SetPlayerDamageState(int entityId, PlayerDamageState state)
        {
            _batch.SetPlayerDamageState(entityId, state);
        }

        public void SetTopology(CubeTopologyState topology)
        {
            _batch.SetTopology(topology);
        }
    }

    internal sealed class RecordingDelayedAttackEffectSink : IDelayedAttackEffectSink
    {
        private readonly FinalizationBatch _batch;

        public RecordingDelayedAttackEffectSink(FinalizationBatch batch)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
        }

        public void Enqueue(DelayedAttackEffectRecord effectRecord)
        {
            _batch.EnqueueDelayedAttackEffect(effectRecord);
        }
    }

    internal sealed class ProjectedWorld
    {
        private readonly WorldSnapshot _baseSnapshot;
        private readonly FinalizationBatch _overlayBatch = new();
        private bool _isDirty = true;
        private WorldSnapshot _materializedSnapshot;

        public ProjectedWorld(WorldSnapshot baseSnapshot)
        {
            _baseSnapshot = baseSnapshot ?? throw new ArgumentNullException(nameof(baseSnapshot));
        }

        public void ApplyBatch(FinalizationBatch batch)
        {
            _overlayBatch.MergeFrom(batch ?? throw new ArgumentNullException(nameof(batch)));
            _isDirty = true;
        }

        public WorldSnapshot CreateSnapshot()
        {
            if (!_isDirty && _materializedSnapshot != null)
            {
                return _materializedSnapshot;
            }

            var projectedWorldState = MaterializeWorldState(_baseSnapshot);
            _overlayBatch.ApplyTo(projectedWorldState.CreateWriteContext(), delayedAttackEffectSink: null);
            _materializedSnapshot = SnapshotBuilder.Create(projectedWorldState);
            _isDirty = false;
            return _materializedSnapshot;
        }

        private static WorldState MaterializeWorldState(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            var worldState = new WorldState(
                entities,
                snapshot.BoardBounds,
                snapshot.TerrainData,
                snapshot.Topology);
            var writeContext = worldState.CreateWriteContext();

            for (var i = 0; i < entities.Count; i++)
            {
                var entityId = entities[i].entityId;
                if (snapshot.TryGetPlayerControlState(entityId, out var playerControlState))
                {
                    writeContext.SetPlayerControlState(entityId, playerControlState);
                }

                if (snapshot.TryGetPlayerDamageState(entityId, out var playerDamageState))
                {
                    writeContext.SetPlayerDamageState(entityId, playerDamageState);
                }

                if (snapshot.TryGetEnemyActionState(entityId, out var enemyActionState))
                {
                    writeContext.SetEnemyActionState(entityId, enemyActionState);
                }

                if (snapshot.TryGetEntityExecutionLockState(entityId, out var executionLockState))
                {
                    writeContext.SetEntityExecutionLockState(entityId, executionLockState);
                }

                if (snapshot.TryGetEnemyJumpState(entityId, out var enemyJumpState))
                {
                    writeContext.SetEnemyJumpState(entityId, enemyJumpState);
                }
            }

            return worldState;
        }
    }
}
