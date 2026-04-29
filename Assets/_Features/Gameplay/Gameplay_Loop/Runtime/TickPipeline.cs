using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
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
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickPipeline
    {
        private readonly IdAllocator _idAllocator = new();
        private readonly EntityIdAllocator _entityIdAllocator;
        private readonly ISnapshotEntityLogicProvider _entityLogicProvider;
        private readonly IReadOnlyList<IEntityLogic> _staticEntityLogics;
        private readonly IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> _enemySpawnDefaultsByArchetypeId;
        private readonly MovementIntentCollector _movementIntentCollector = new();
        private readonly MovementExpander _movementExpander;
        private readonly AttackIntentCollector _attackIntentCollector = new();
        private readonly AttackInputNormalizer _attackInputNormalizer = new();
        private readonly AttackExpander _attackExpander;
        private readonly CleanupProcessor _cleanupProcessor = new();
        private readonly RespawnProcessor _respawnProcessor = new();
        private readonly TickResultBuilder _tickResultBuilder = new();
        private readonly DeterminismHashBuilder _determinismHashBuilder = new();
        private readonly TickTraceBuilder _tickTraceBuilder = new();
        private readonly DelayedAttackEffectQueue _delayedAttackEffectQueue = new();
        private readonly List<EntityState> _playerRespawnTemplates;
        private readonly StageObjectiveTracker _objectiveTracker;
        private readonly int _moveOccupancyTicks;
        private readonly int _playerMoveCooldownTicks;
        private readonly int _playerDamageCooldownTicks;
        private readonly int _playerRespawnDelayTicks;
        private readonly PlayerKinematicLocomotionTimingSnapshot _playerKinematicLocomotionTiming;
        private readonly bool _allowPlayerRespawn;
        private readonly GameplayRuntimeFeatureFlags _runtimeFeatureFlags;
        private readonly int _slidingStateTimerTicks;
        private readonly WorldState _worldState;

        public TickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            ISnapshotEntityLogicProvider entityLogicProvider,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks = 1,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> enemySpawnDefaultsByArchetypeId = null,
            bool allowPlayerRespawn = true,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            PlayerKinematicLocomotionTimingSnapshot playerKinematicLocomotionTiming = default)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));

            if (entityLogics == null)
            {
                throw new ArgumentNullException(nameof(entityLogics));
            }

            _entityLogicProvider = entityLogicProvider ?? throw new ArgumentNullException(nameof(entityLogicProvider));
            _staticEntityLogics = new List<IEntityLogic>(entityLogics).AsReadOnly();
            _enemySpawnDefaultsByArchetypeId = enemySpawnDefaultsByArchetypeId;
            _entityIdAllocator = EntityIdAllocator.Create(SnapshotBuilder.Create(_worldState));
            var resolvedGeneralTimingProfile = generalTimingProfile ?? throw new ArgumentNullException(nameof(generalTimingProfile));
            if (playerRespawnDelayTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerRespawnDelayTicks),
                    "Player respawn delay ticks must be greater than zero.");
            }

            _movementExpander = new MovementExpander(resolvedGeneralTimingProfile);
            _attackExpander = new AttackExpander(resolvedGeneralTimingProfile);
            _playerMoveCooldownTicks = Math.Max(0, playerControlTiming.MoveCooldownTicks);
            _playerDamageCooldownTicks = Math.Max(0, playerControlTiming.DamageCooldownTicks);
            _playerRespawnDelayTicks = playerRespawnDelayTicks;
            _playerKinematicLocomotionTiming = playerKinematicLocomotionTiming.IsConfigured
                ? playerKinematicLocomotionTiming
                : PlayerKinematicLocomotionTimingSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _allowPlayerRespawn = allowPlayerRespawn;
            _runtimeFeatureFlags = runtimeFeatureFlags;
            _moveOccupancyTicks = resolvedGeneralTimingProfile.MoveOccupancyTicks;
            _playerRespawnTemplates = BuildPlayerRespawnTemplates(
                SnapshotBuilder.Create(_worldState),
                _staticEntityLogics);
            _slidingStateTimerTicks = resolvedGeneralTimingProfile.BoxSlideStepIntervalTicks;
            _objectiveTracker = (objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled).CreateTracker();
        }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _objectiveTracker.ObjectiveDefinition;

        public StageObjectiveTickResult CurrentObjectiveResult => _objectiveTracker.CurrentResult;

        private static bool ShouldEmitTickTrace()
        {
#if UNITY_EDITOR && GAMEPLAY_DEBUG_OUTPUT_FORCE_OFF
            return false;
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        private static bool ShouldEmitDeterminismHash()
        {
#if UNITY_EDITOR && GAMEPLAY_DEBUG_OUTPUT_FORCE_OFF
            return false;
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

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
            var objectiveTickFacts = new StageObjectiveTickFacts(
                input.TickIndex,
                input.PlayerCommand,
                cleanupPhaseResult.RemovedEntityIds,
                BuildObjectiveDamageFacts(attackPhaseResult.DamageResolutions));
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
                input.PlayerCommand,
                resolvePhaseResult.ResolutionRecords);
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
            var determinismHash = ShouldEmitDeterminismHash()
                ? _determinismHashBuilder.Build(input.TickIndex, finalAuthoritativeSnapshot, tickResultData)
                : string.Empty;
            var tickTrace = ShouldEmitTickTrace()
                ? _tickTraceBuilder.Build(
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
                    determinismHash)
                : TickTrace.Empty;

            return new TickResult(
                input.TickIndex,
                completedPhases,
                phaseTrace,
                movementPhaseResult,
                attackPhaseResult,
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

            var rejectedReasons = new List<string>();
            for (var i = 0; i < updates.Count; i++)
            {
                if (updates[i].StartsWith("MovementRejected|", StringComparison.Ordinal))
                {
                    rejectedReasons.Add(updates[i]);
                }
            }

            return new PreMovementStatePhaseResult(updates, actionTransitions, rejectedReasons);
        }

        private static void CloseInterruptedPlayerKinematics(
            WorldSnapshot snapshot,
            FinalizationBatch batch,
            List<string> eventLogEntries)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (eventLogEntries == null)
            {
                throw new ArgumentNullException(nameof(eventLogEntries));
            }

            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.hp <= 0 ||
                    entity.markedForDeath ||
                    !snapshot.TryGetPlayerControlState(entity.entityId, out _) ||
                    !snapshot.TryGetUnitKinematicPose(entity.entityId, out var pose) ||
                    !pose.HasAuthoritativeState ||
                    pose.Mode != MotionMode.Interrupted)
                {
                    continue;
                }

                batch.SetUnitKinematicState(
                    entity.entityId,
                    UnitKinematicRuntimeState.SettledZero,
                    new FinalizationOperationMetadata(
                        TickPhase.Plan,
                        ResolvedActionSemanticKind.Stop,
                        entity.entityId,
                        actionPlanId: 0));
                eventLogEntries.Add(
                    $"KinematicInterruptClosed|E={entity.entityId}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}");
            }
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
            var beforeMovementAiContext = new RecordingFinalizationContext(beforeMovementAiBatch, snapshot, TickPhase.Plan);
            var aiPhaseResult = RunEnemyAiPhase(
                entityLogicsForTick.AiStateLogics,
                snapshot,
                in input,
                beforeMovementAiContext);
            planFinalizationBatch.MergeFrom(beforeMovementAiBatch);
            projectedWorld.ApplyBatch(beforeMovementAiBatch);
            var snapshotAfterEnemyAi = projectedWorld.CreateSnapshot();

            var kinematicClosureBatch = new FinalizationBatch();
            var kinematicClosureEvents = new List<string>();
            if (_runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion)
            {
                CloseInterruptedPlayerKinematics(
                    snapshotAfterEnemyAi,
                    kinematicClosureBatch,
                    kinematicClosureEvents);
                planFinalizationBatch.MergeFrom(kinematicClosureBatch);
                projectedWorld.ApplyBatch(kinematicClosureBatch);
                snapshotAfterEnemyAi = projectedWorld.CreateSnapshot();
            }

            var preMovementBatch = new FinalizationBatch();
            var utilityTriggerIntents = new List<EnemyUtilityTriggerIntent>();
            var preMovementContext = new RecordingFinalizationContext(
                preMovementBatch,
                snapshotAfterEnemyAi,
                TickPhase.Plan,
                utilityTriggerIntents);
            var preMovementStateResult = RunPreMovementStatePhase(
                entityLogicsForTick.PreMovementStateLogics,
                snapshotAfterEnemyAi,
                in input,
                preMovementContext);
            if (kinematicClosureEvents.Count > 0)
            {
                preMovementStateResult.EventLogEntries.InsertRange(0, kinematicClosureEvents);
            }
            utilityTriggerIntents.Sort(EnemyUtilityTriggerIntentComparer.Instance);
            preMovementStateResult.UtilityTriggerIntents.AddRange(utilityTriggerIntents);
            planFinalizationBatch.MergeFrom(preMovementBatch);
            projectedWorld.ApplyBatch(preMovementBatch);
            var preMovementUtilityResolveResult = EnemyUtilityResolver.ResolvePreMovementProjectedEffects(
                projectedWorld.CreateSnapshot(),
                preMovementStateResult.UtilityTriggerIntents,
                input.TickIndex);
            planFinalizationBatch.MergeFrom(preMovementUtilityResolveResult.Batch);
            projectedWorld.ApplyBatch(preMovementUtilityResolveResult.Batch);
            AddRange(preMovementStateResult.EventLogEntries, preMovementUtilityResolveResult.EventLogEntries);
            var nextContestId = 1;
            var jumpLandingPlans = new List<JumpLandingPlan>();
            var jumpLandingSpaceContests = new List<Contest>();
            var jumpLandingEvents = new List<string>();
            var phaseRelocationPlans = new List<PhaseRelocationPlan>();
            var phaseRelocationSpaceContests = new List<Contest>();
            ResolvePlanJumpLandings(
                projectedWorld,
                input.TickIndex,
                entityLogicsForTick.MovementLogics,
                planFinalizationBatch,
                preMovementStateResult.Updates,
                jumpLandingPlans,
                jumpLandingSpaceContests,
                jumpLandingEvents,
                ref nextContestId);
            var planSnapshot = projectedWorld.CreateSnapshot();
            ResolvePlanEnemyPhaseRelocations(
                planSnapshot,
                input.TickIndex,
                phaseRelocationPlans,
                phaseRelocationSpaceContests,
                ref nextContestId);

            var rawMovementIntents = new List<RawMovementIntent>();
            _movementIntentCollector.Collect(planSnapshot, in input, entityLogicsForTick.MovementLogics, rawMovementIntents);
            var rejectedReasons = new List<string>();
            var executableMovementIntents = FilterExecutionLockedMovementIntents(planSnapshot, input.TickIndex, rawMovementIntents, rejectedReasons);
            var sortedIntents = BuildMovementIntents(executableMovementIntents);
            var expansionIntents = sortedIntents;
            var kinematicMovementActionPlanPayloads = new Dictionary<int, MovementActionPlanPayload>();
            if (_runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion)
            {
                expansionIntents = BuildPlayerSameFaceKinematicLocomotionPlans(
                    planSnapshot,
                    sortedIntents,
                    input.TickIndex,
                    rejectedReasons,
                    kinematicMovementActionPlanPayloads);
            }
            if (_runtimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion)
            {
                expansionIntents = BuildEnemySameFaceKinematicLocomotionPlans(
                    planSnapshot,
                    expansionIntents,
                    input.TickIndex,
                    rejectedReasons,
                    kinematicMovementActionPlanPayloads);
            }

            var playerTraversalSourceIds = CollectPlayerTraversalSourceIds(entityLogicsForTick.MovementLogics);
            var frontFaceSupportContributors = CollectFrontFaceSupportContributors(
                entityLogicsForTick.FrontFaceSupportLogics,
                planSnapshot,
                in input);
            var frontFaceShieldSourceExports = BuildFrontFaceShieldSourcePresentationExports(
                frontFaceSupportContributors,
                planSnapshot.Topology,
                input.TickIndex);
            var frontFaceShieldBlockExports = new List<FrontFaceShieldBlockPresentationExport>();
            var expandedCandidates = new List<ActionGroup>();
            var preExpansionRejectedReasons = new List<string>(rejectedReasons);
            _movementExpander.Expand(
                planSnapshot,
                input.TickIndex,
                expansionIntents,
                playerTraversalSourceIds,
                frontFaceSupportContributors,
                expandedCandidates,
                rejectedReasons,
                frontFaceShieldBlockExports);
            if (preExpansionRejectedReasons.Count > 0)
            {
                rejectedReasons.InsertRange(0, preExpansionRejectedReasons);
            }

            expandedCandidates.Sort(ActionGroupComparer.Instance);
            AssignMovementGroupIds(expandedCandidates);
            var movementActionPlanPayloads = BuildMovementActionPlanPayloads(
                planSnapshot,
                expansionIntents,
                expandedCandidates,
                rejectedReasons,
                input.TickIndex);
            MergeMovementActionPlanPayloads(movementActionPlanPayloads, kinematicMovementActionPlanPayloads);
            var orderedMovementActionPlanIds = BuildOrderedMovementActionPlanIds(planSnapshot, movementActionPlanPayloads);
            var jumpLandingActionPlanPayloads = BuildJumpLandingActionPlanPayloads(jumpLandingPlans);
            var orderedJumpLandingActionPlanIds = BuildOrderedJumpLandingActionPlanIds(jumpLandingPlans);
            var phaseRelocationActionPlanPayloads = BuildPhaseRelocationActionPlanPayloads(phaseRelocationPlans);
            var orderedPhaseRelocationActionPlanIds = BuildOrderedPhaseRelocationActionPlanIds(phaseRelocationPlans);
            var spaceContests = BuildSpaceContests(
                movementActionPlanPayloads,
                orderedMovementActionPlanIds,
                ref nextContestId);
            phaseTrace.Add("Plan:Exit");
            completedPhases.Add(TickPhase.Plan);

            return new PlanPhaseResult(
                rawMovementIntents,
                sortedIntents,
                expandedCandidates,
                movementActionPlanPayloads,
                orderedMovementActionPlanIds,
                rejectedReasons,
                spaceContests,
                jumpLandingSpaceContests,
                jumpLandingPlans,
                jumpLandingActionPlanPayloads,
                orderedJumpLandingActionPlanIds,
                jumpLandingEvents,
                phaseRelocationSpaceContests,
                phaseRelocationPlans,
                phaseRelocationActionPlanPayloads,
                orderedPhaseRelocationActionPlanIds,
                frontFaceShieldSourceExports,
                frontFaceShieldBlockExports,
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
            var contests = new List<Contest>(planPhaseResult.SpaceContests.Count);
            AddRange(contests, planPhaseResult.SpaceContests);
            AddRange(contests, planPhaseResult.JumpLandingSpaceContests);
            AddRange(contests, planPhaseResult.PhaseRelocationSpaceContests);
            var resolutionRecords = new List<ResolutionRecord>();
            var nextContestId = planPhaseResult.NextContestId;
            var movementResolutionRecords = new List<ResolutionRecord>();
            var impactDispositionRecords = new List<ImpactDispositionResolutionRecord>();
            var movementReservationBook = new MovementReservationBook();

            var movementRejectedReasons = new List<string>(planPhaseResult.RejectedReasons);
            AddRange(movementRejectedReasons, planPhaseResult.PreMovementStatePhaseResult.RejectedReasons);
            ResolveMovementActionPlansCanonical(
                planSnapshot,
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                planPhaseResult.SpaceContests,
                movementReservationBook,
                movementResolutionRecords,
                movementRejectedReasons);
            var movementImpactReservations = ResolveMovementImpactReservationsCanonical(
                tickIndex,
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                movementResolutionRecords);
            var plannedImpactReservations = SortImpactReservations(movementImpactReservations);
            var impactContests = BuildImpactContests(plannedImpactReservations, ref nextContestId);
            AddRange(contests, impactContests);
            RecordAcceptedResolutionRecords(impactContests, movementResolutionRecords);
            var impactSpaceContests = BuildImpactSpaceContestsCanonical(
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                movementResolutionRecords,
                ref nextContestId);
            AddRange(contests, impactSpaceContests);

            var movementDestroyContests = BuildMovementDestroyContestsCanonical(
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                movementResolutionRecords,
                ref nextContestId);
            AddRange(contests, movementDestroyContests);
            var movementDestroyResolutions = ResolveMovementDestroyResolutionsCanonical(
                planSnapshot,
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                movementResolutionRecords);
            RecordDestroyResolutionRecords(
                movementDestroyContests,
                movementDestroyResolutions,
                movementResolutionRecords);
            var movementCommitEvents = CreateMovementCommitEventBuffer(planPhaseResult.PreMovementStatePhaseResult.EventLogEntries);
            var movementStageBatch = MaterializeMovementOperations(
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                movementResolutionRecords,
                impactDispositionRecords,
                movementImpactReservations,
                movementCommitEvents);

            var finalizationBatch = new FinalizationBatch();
            finalizationBatch.MergeFrom(planPhaseResult.PlanFinalizationBatch);
            finalizationBatch.MergeFrom(movementStageBatch);
            var projectedWorld = new ProjectedWorld(planSnapshot);
            projectedWorld.ApplyBatch(planPhaseResult.PlanFinalizationBatch);
            projectedWorld.ApplyBatch(movementStageBatch);
            // postMovementSnapshot is the movement-visible resolve surface. Accepted
            // impact follow-through writes are materialized here before jump landing.
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
            var attackPlanResult = BuildAttackPlan(
                attackSnapshot,
                in input,
                entityLogicsForTick.AttackLogics,
                plannedImpactReservations,
                drainedDelayedAttackEffects,
                tickIndex);

            var rawAttackIntents = attackPlanResult.RawAttackIntents;
            var attackRejectedReasons = attackPlanResult.RejectedReasons;
            var attackResolutionRecords = new List<ResolutionRecord>();
            var attackPlanContests = BuildAttackPlanContestsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                ref nextContestId);
            AddRange(contests, attackPlanContests);
            ResolveAttackActionPlansCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackPlanContests,
                attackResolutionRecords,
                attackRejectedReasons);
            var damageContests = BuildDamageContestsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                ref nextContestId);
            AddRange(contests, damageContests);
            var attackDestroyContests = BuildAttackDestroyContestsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                ref nextContestId);
            AddRange(contests, attackDestroyContests);
            var damageResolutions = ResolveDamageResolutionsCanonical(
                attackSnapshot,
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                tickIndex);
            RecordDamageResolutionRecords(
                damageContests,
                damageResolutions,
                attackResolutionRecords);
            var destroyResolutions = ResolveDestroyResolutionsCanonical(
                attackSnapshot,
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                damageResolutions);
            RecordDestroyResolutionRecords(
                attackDestroyContests,
                destroyResolutions,
                attackResolutionRecords);
            var damageProjectionSnapshot = CreateCompositeDamageProjectionSnapshot(
                attackSnapshot,
                attackResolutionRecords,
                attackPlanResult.ActionPlanPayloads);
            ResolveImpactSpaceContestsCanonical(
                attackSnapshot,
                destroyResolutions,
                planPhaseResult.OrderedMovementActionPlanIds,
                planPhaseResult.MovementActionPlanPayloads,
                impactSpaceContests,
                movementReservationBook,
                movementResolutionRecords,
                impactDispositionRecords);

            if (HasImpactDispositionRematerialization(impactDispositionRecords))
            {
                movementCommitEvents = CreateMovementCommitEventBuffer(planPhaseResult.PreMovementStatePhaseResult.EventLogEntries);
                movementStageBatch = MaterializeMovementOperations(
                    planPhaseResult.OrderedMovementActionPlanIds,
                    planPhaseResult.MovementActionPlanPayloads,
                    movementResolutionRecords,
                    impactDispositionRecords,
                    movementImpactReservations,
                    movementCommitEvents);

                finalizationBatch = new FinalizationBatch();
                finalizationBatch.MergeFrom(planPhaseResult.PlanFinalizationBatch);
                finalizationBatch.MergeFrom(movementStageBatch);
                projectedWorld = new ProjectedWorld(planSnapshot);
                projectedWorld.ApplyBatch(planPhaseResult.PlanFinalizationBatch);
                projectedWorld.ApplyBatch(movementStageBatch);
                postMovementSnapshot = projectedWorld.CreateSnapshot();

                beforeAttackAiBatch = new FinalizationBatch();
                beforeAttackAiContext = new RecordingFinalizationContext(beforeAttackAiBatch);
                CommitEnemyAiTransitions(
                    postMovementSnapshot,
                    in input,
                    entityLogicsForTick.AiStateLogics,
                    EnemyAiTransitionStage.BeforeAttack,
                    beforeAttackAiContext,
                    aiPhaseResult.BeforeAttackTransitions);
                finalizationBatch.MergeFrom(beforeAttackAiBatch);
                projectedWorld.ApplyBatch(beforeAttackAiBatch);

                enemyActionBeforeAttackBatch = new FinalizationBatch();
                enemyActionBeforeAttackContext = new RecordingFinalizationContext(enemyActionBeforeAttackBatch);
                enemyActionPhaseResult = RunEnemyActionPhase(
                    entityLogicsForTick.EnemyActionStateLogics,
                    projectedWorld.CreateSnapshot(),
                    in input,
                    EnemyActionStage.BeforeAttackCollection,
                    enemyActionBeforeAttackContext,
                    new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()));
                finalizationBatch.MergeFrom(enemyActionBeforeAttackBatch);
                projectedWorld.ApplyBatch(enemyActionBeforeAttackBatch);
            }

            var jumpLandingResolveBatch = ResolveJumpLandingSpaceContestsCanonical(
                postMovementSnapshot,
                damageProjectionSnapshot,
                planPhaseResult.OrderedJumpLandingActionPlanIds,
                planPhaseResult.JumpLandingActionPlanPayloads,
                planPhaseResult.JumpLandingSpaceContests,
                movementReservationBook,
                movementResolutionRecords,
                movementCommitEvents);
            if (jumpLandingResolveBatch.Operations.Count > 0)
            {
                finalizationBatch = new FinalizationBatch();
                finalizationBatch.MergeFrom(planPhaseResult.PlanFinalizationBatch);
                finalizationBatch.MergeFrom(movementStageBatch);
                finalizationBatch.MergeFrom(jumpLandingResolveBatch);
                projectedWorld = new ProjectedWorld(planSnapshot);
                projectedWorld.ApplyBatch(planPhaseResult.PlanFinalizationBatch);
                projectedWorld.ApplyBatch(movementStageBatch);
                projectedWorld.ApplyBatch(jumpLandingResolveBatch);
                postMovementSnapshot = projectedWorld.CreateSnapshot();

                beforeAttackAiBatch = new FinalizationBatch();
                beforeAttackAiContext = new RecordingFinalizationContext(beforeAttackAiBatch);
                CommitEnemyAiTransitions(
                    postMovementSnapshot,
                    in input,
                    entityLogicsForTick.AiStateLogics,
                    EnemyAiTransitionStage.BeforeAttack,
                    beforeAttackAiContext,
                    aiPhaseResult.BeforeAttackTransitions);
                finalizationBatch.MergeFrom(beforeAttackAiBatch);
                projectedWorld.ApplyBatch(beforeAttackAiBatch);

                enemyActionBeforeAttackBatch = new FinalizationBatch();
                enemyActionBeforeAttackContext = new RecordingFinalizationContext(enemyActionBeforeAttackBatch);
                enemyActionPhaseResult = RunEnemyActionPhase(
                    entityLogicsForTick.EnemyActionStateLogics,
                    projectedWorld.CreateSnapshot(),
                    in input,
                    EnemyActionStage.BeforeAttackCollection,
                    enemyActionBeforeAttackContext,
                    new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()));
                finalizationBatch.MergeFrom(enemyActionBeforeAttackBatch);
                projectedWorld.ApplyBatch(enemyActionBeforeAttackBatch);
            }

            var phaseRelocationResolveBatch = ResolveEnemyPhaseRelocationSpaceContestsCanonical(
                postMovementSnapshot,
                planPhaseResult.OrderedPhaseRelocationActionPlanIds,
                planPhaseResult.PhaseRelocationActionPlanPayloads,
                planPhaseResult.PhaseRelocationSpaceContests,
                movementReservationBook,
                movementResolutionRecords,
                movementCommitEvents);
            if (phaseRelocationResolveBatch.Operations.Count > 0)
            {
                finalizationBatch.MergeFrom(phaseRelocationResolveBatch);
                projectedWorld.ApplyBatch(phaseRelocationResolveBatch);
                postMovementSnapshot = projectedWorld.CreateSnapshot();
            }

            var finalImpactReservations = MergeImpactReservations(
                movementImpactReservations,
                ResolveDeferredMovementImpactReservationsAgainstSnapshot(
                    postMovementSnapshot,
                    tickIndex,
                    planPhaseResult.OrderedMovementActionPlanIds,
                    planPhaseResult.MovementActionPlanPayloads,
                    movementResolutionRecords));
            var frozenMovementReservationExport = movementReservationBook.Freeze(finalImpactReservations);
            attackSnapshot = projectedWorld.CreateSnapshot();
            attackPlanResult = BuildAttackPlan(
                attackSnapshot,
                in input,
                entityLogicsForTick.AttackLogics,
                frozenMovementReservationExport,
                drainedDelayedAttackEffects,
                tickIndex);
            rawAttackIntents = attackPlanResult.RawAttackIntents;
            attackRejectedReasons = attackPlanResult.RejectedReasons;
            attackResolutionRecords = new List<ResolutionRecord>();

            var finalAttackPlanContests = BuildAttackPlanContestsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                ref nextContestId);
            ResolveAttackActionPlansCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                finalAttackPlanContests,
                attackResolutionRecords,
                attackRejectedReasons);

            var finalDamageContests = BuildDamageContestsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                ref nextContestId);
            damageResolutions = ResolveDamageResolutionsCanonical(
                attackSnapshot,
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                tickIndex);
            RecordDamageResolutionRecords(
                finalDamageContests,
                damageResolutions,
                attackResolutionRecords);

            var finalAttackDestroyContests = BuildAttackDestroyContestsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                ref nextContestId);
            destroyResolutions = ResolveDestroyResolutionsCanonical(
                attackSnapshot,
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                damageResolutions);
            RecordDestroyResolutionRecords(
                finalAttackDestroyContests,
                destroyResolutions,
                attackResolutionRecords);

            AddRange(resolutionRecords, movementResolutionRecords);
            var delayedAttackEffects = ResolveDelayedAttackEffectsCanonical(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords);
            var attackCommitEvents = new List<string>();
            var delayedAttackDrainEvents = BuildDelayedAttackDrainEvents(tickIndex, drainedDelayedAttackEffects);
            var delayedAttackEnqueueEvents = new List<string>();
            var attackStageBatch = MaterializeAttackOperations(
                attackPlanResult.OrderedActionPlanIds,
                attackPlanResult.ActionPlanPayloads,
                attackResolutionRecords,
                damageResolutions,
                delayedAttackEffects,
                attackCommitEvents,
                delayedAttackEnqueueEvents);
            var motionInterruptRecords = _runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion
                ? MaterializePlayerKinematicMotionInterrupts(
                    attackSnapshot,
                    damageResolutions,
                    destroyResolutions,
                    attackStageBatch,
                    attackCommitEvents)
                : new List<MotionInterruptRecord>();
            finalizationBatch.MergeFrom(attackStageBatch);
            projectedWorld.ApplyBatch(attackStageBatch);
            AddRange(resolutionRecords, attackResolutionRecords);

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
            var utilityResolveResult = EnemyUtilityResolver.ResolvePostAttackEffects(
                projectedWorld.CreateSnapshot(),
                planPhaseResult.PreMovementStatePhaseResult.UtilityTriggerIntents,
                tickIndex,
                _entityIdAllocator,
                _enemySpawnDefaultsByArchetypeId);
            finalizationBatch.MergeFrom(utilityResolveResult.Batch);
            projectedWorld.ApplyBatch(utilityResolveResult.Batch);
            var postAttackSnapshot = projectedWorld.CreateSnapshot();

            phaseTrace.Add("Resolve:Exit");
            completedPhases.Add(TickPhase.Resolve);

            // MovementPhaseResult owns movement-visible resolve effects, including
            // accepted impact follow-through detach/move/facing/state writes.
            var movementResolvedOperations = new List<FinalizationOperation>();
            for (var planOperationIndex = 0; planOperationIndex < planPhaseResult.PlanFinalizationBatch.Operations.Count; planOperationIndex++)
            {
                var planOperation = planPhaseResult.PlanFinalizationBatch.Operations[planOperationIndex];
                if ((planOperation.Kind == FinalizationOperationKind.SetEnemyJumpState &&
                     planOperation.Metadata.JumpPresentationKind != JumpPresentationKind.None) ||
                    planOperation.Kind == FinalizationOperationKind.SetPhasedState)
                {
                    movementResolvedOperations.Add(planOperation);
                }
            }
            AddRange(movementResolvedOperations, movementStageBatch.Operations);
            AddRange(movementResolvedOperations, jumpLandingResolveBatch.Operations);
            AddRange(movementResolvedOperations, phaseRelocationResolveBatch.Operations);
            AppendBoxInteractionLockBlockedEvents(movementRejectedReasons, movementCommitEvents, tickIndex);
            AppendFrontFaceShieldBlockedEvents(movementRejectedReasons, movementCommitEvents, tickIndex);
            var movementPhaseResult = new MovementPhaseResult(
                planPhaseResult.RawIntents,
                planPhaseResult.SortedIntents,
                movementResolutionRecords,
                impactDispositionRecords,
                movementResolvedOperations,
                movementCommitEvents,
                movementRejectedReasons,
                planPhaseResult.FrontFaceShieldSourceExports,
                planPhaseResult.FrontFaceShieldBlockExports);

            AddRange(attackCommitEvents, utilityResolveResult.EventLogEntries);
            var attackResolvedOperations = new List<FinalizationOperation>(attackStageBatch.Operations.Count + utilityResolveResult.Batch.Operations.Count);
            AddRange(attackResolvedOperations, attackStageBatch.Operations);
            AddRange(attackResolvedOperations, utilityResolveResult.Batch.Operations);

            var attackEventLogEntries = new List<string>(delayedAttackDrainEvents.Count + attackCommitEvents.Count + delayedAttackEnqueueEvents.Count);
            AddRange(attackEventLogEntries, delayedAttackDrainEvents);
            AddRange(attackEventLogEntries, attackCommitEvents);
            AddRange(attackEventLogEntries, delayedAttackEnqueueEvents);

            var attackPhaseResult = new AttackPhaseResult(
                rawAttackIntents,
                frozenMovementReservationExport.ImpactReservations,
                drainedDelayedAttackEffects,
                damageResolutions,
                attackResolutionRecords,
                attackResolvedOperations,
                delayedAttackEffects,
                attackCommitEvents,
                attackEventLogEntries,
                attackRejectedReasons,
                frozenMovementReservationExport,
                motionInterruptRecords);

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

        private static List<FrontFaceSupportContributor> CollectFrontFaceSupportContributors(
            IReadOnlyList<IFrontFaceSupportLogic> entityLogics,
            WorldSnapshot snapshot,
            in TickInput input)
        {
            var contributors = new List<FrontFaceSupportContributor>(entityLogics?.Count ?? 0);
            if (entityLogics == null)
            {
                return contributors;
            }

            for (var i = 0; i < entityLogics.Count; i++)
            {
                entityLogics[i].CollectFrontFaceSupportContributors(snapshot, input, contributors);
            }

            contributors.Sort(FrontFaceSupportContributorComparer.Instance);
            return contributors;
        }

        private static List<FrontFaceShieldSourcePresentationExport> BuildFrontFaceShieldSourcePresentationExports(
            IReadOnlyList<FrontFaceSupportContributor> contributors,
            CubeTopologyState topology,
            int tickIndex)
        {
            var exports = new List<FrontFaceShieldSourcePresentationExport>(contributors?.Count ?? 0);
            if (contributors == null)
            {
                return exports;
            }

            for (var i = 0; i < contributors.Count; i++)
            {
                var contributor = contributors[i];
                if (contributor.EffectRuntime.Kind != EnemyFrontFaceSupportEffectKind.BoxSlideShield)
                {
                    continue;
                }

                var shield = contributor.EffectRuntime.BoxSlideShield;
                exports.Add(
                    new FrontFaceShieldSourcePresentationExport(
                        contributor.SourceEntityId,
                        contributor.SourceCell,
                        topology,
                        shield.Radius,
                        shield.IncludeSourceCell,
                        shield.TargetPattern,
                        tickIndex,
                        MovementExpander.BuildFrontFaceShieldPresentationSeed(
                            tickIndex,
                            contributor.SourceEntityId,
                            0,
                            contributor.SourceCell,
                            contributor.EffectIndex)));
            }

            return exports;
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
            cleanupPhaseResult = ExpireBoxInteractionLocks(snapshot, writeContext, tickIndex, cleanupPhaseResult);
            phaseTrace.Add("Cleanup:Exit");
            completedPhases.Add(TickPhase.Cleanup);

            return cleanupPhaseResult;
        }

        private static CleanupPhaseResult ExpireBoxInteractionLocks(
            WorldSnapshot snapshot,
            ICleanupCommitContext writeContext,
            int tickIndex,
            CleanupPhaseResult cleanupPhaseResult)
        {
            var expiredEventLogEntries = new List<string>();
            var removedEntityIdsThisTick = cleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(cleanupPhaseResult.RemovedEntityIds)
                : null;
            var lockEntries = new List<BoxInteractionLockSnapshotEntry>();
            snapshot.EnumerateBoxInteractionLockStatesOrdered(lockEntries);

            for (var i = 0; i < lockEntries.Count; i++)
            {
                var entry = lockEntries[i];
                if ((removedEntityIdsThisTick != null && removedEntityIdsThisTick.Contains(entry.EntityId)) ||
                    BoxInteractionLockQueries.IsActive(entry.State, tickIndex))
                {
                    continue;
                }

                writeContext.RemoveBoxInteractionLockState(entry.EntityId);
                expiredEventLogEntries.Add(
                    $"BoxInteractionLockExpired|Box={entry.EntityId}|Expires={entry.State.ExpiresTickExclusive}|Tick={tickIndex}");
            }

            if (expiredEventLogEntries.Count == 0)
            {
                return cleanupPhaseResult;
            }

            var eventLogEntries = new List<string>(cleanupPhaseResult.EventLogEntries.Count + expiredEventLogEntries.Count);
            AddRange(eventLogEntries, cleanupPhaseResult.EventLogEntries);
            AddRange(eventLogEntries, expiredEventLogEntries);

            return new CleanupPhaseResult(
                cleanupPhaseResult.RemovedEntityIds,
                cleanupPhaseResult.TimerChanges,
                cleanupPhaseResult.StateTransitions,
                eventLogEntries,
                cleanupPhaseResult.RemovedUnitKinematicPoses);
        }

        private RespawnPhaseResult RunRespawnPhase(
            WorldSnapshot tickStartSnapshot,
            WorldSnapshot postCleanupSnapshot,
            CleanupPhaseResult cleanupPhaseResult,
            int tickIndex,
            IWorldWriteContext writeContext,
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
                _allowPlayerRespawn,
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

        private List<MoveIntent> BuildPlayerSameFaceKinematicLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            List<string> rejectedReasons,
            Dictionary<int, MovementActionPlanPayload> kinematicPayloads)
        {
            var legacyIntents = new List<MoveIntent>(sortedIntents.Count);
            var continuingPlayerIds = new HashSet<int>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!snapshot.TryGetPlayerControlState(entity.entityId, out _) ||
                    !TryBuildPlayerKinematicContinuationPayload(
                        snapshot,
                        entity.entityId,
                        tickIndex,
                        rejectedReasons,
                        out var continuationPayload))
                {
                    continue;
                }

                kinematicPayloads.Add(continuationPayload.ActionPlanId, continuationPayload);
                continuingPlayerIds.Add(entity.entityId);
            }

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (continuingPlayerIds.Contains(intent.SourceId))
                {
                    if (intent.CommandKind == Movement.MovementCommandKind.Move)
                    {
                        continue;
                    }

                    legacyIntents.Add(intent);
                    continue;
                }

                if (TryBuildPlayerKinematicStartPayload(
                        snapshot,
                        intent,
                        tickIndex,
                        rejectedReasons,
                        out var handledByKinematic,
                        out var startPayload))
                {
                    if (startPayload != null)
                    {
                        kinematicPayloads.Add(startPayload.ActionPlanId, startPayload);
                    }

                    if (handledByKinematic)
                    {
                        continue;
                    }
                }

                legacyIntents.Add(intent);
            }

            return legacyIntents;
        }

        private List<MoveIntent> BuildEnemySameFaceKinematicLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            List<string> rejectedReasons,
            Dictionary<int, MovementActionPlanPayload> kinematicPayloads)
        {
            var legacyIntents = new List<MoveIntent>(sortedIntents.Count);
            var continuingEnemyIds = new HashSet<int>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!IsEnemyLogicParticipant(entity) ||
                    !TryBuildEnemyKinematicContinuationPayload(
                        snapshot,
                        entity.entityId,
                        tickIndex,
                        rejectedReasons,
                        out var continuationPayload))
                {
                    continue;
                }

                kinematicPayloads.Add(continuationPayload.ActionPlanId, continuationPayload);
                continuingEnemyIds.Add(entity.entityId);
            }

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (continuingEnemyIds.Contains(intent.SourceId))
                {
                    if (intent.CommandKind == Movement.MovementCommandKind.Move)
                    {
                        continue;
                    }

                    legacyIntents.Add(intent);
                    continue;
                }

                if (TryBuildEnemyKinematicStartPayload(
                        snapshot,
                        intent,
                        tickIndex,
                        rejectedReasons,
                        out var handledByKinematic,
                        out var startPayload))
                {
                    if (startPayload != null)
                    {
                        kinematicPayloads.Add(startPayload.ActionPlanId, startPayload);
                    }

                    if (handledByKinematic)
                    {
                        continue;
                    }
                }

                legacyIntents.Add(intent);
            }

            return legacyIntents;
        }

        private bool TryBuildEnemyKinematicStartPayload(
            WorldSnapshot snapshot,
            MoveIntent intent,
            int tickIndex,
            List<string> rejectedReasons,
            out bool handledByKinematic,
            out MovementActionPlanPayload payload)
        {
            handledByKinematic = false;
            payload = null;

            if (!TryResolveEnemyKinematicStartScope(
                    snapshot,
                    intent,
                    out var entity,
                    out var pose,
                    out var delta,
                    out var destination,
                    out var facing))
            {
                return false;
            }

            handledByKinematic = true;
            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destination,
                entity.entityId,
                snapshot.Topology,
                CubeRotationKind.None,
                snapshot.Topology);
            if (legality.Verdict != LegalityVerdict.Allowed)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyKinematicTraversalBlocked|Cell={FormatCell(destination)}|{LegalityDiagnosticsFormatter.FormatStableSummary(legality)}");
                return true;
            }

            if (!TryResolveKinematicVelocity(delta, out var velocity, out _))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyKinematicSweepRejected|RejectedBy={KinematicSweepRejectionReason.NonCardinalDelta}|Anchor={FormatCell(entity.position)}");
                return true;
            }

            if (!SurfaceKinematicSweepQueries.TryResolveSameFaceDelta(
                    snapshot,
                    entity.entityId,
                    velocity,
                    out var sweep) ||
                sweep.Blocked)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyKinematicSweepRejected|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
                return true;
            }

            var outcome = CreateKinematicMotionOutcome(
                sweep,
                entity.position,
                sourceState: pose.State,
                stepDirectionX: delta.x,
                stepDirectionY: delta.y,
                elapsedTicks: 1,
                totalTicks: _playerKinematicLocomotionTiming.TicksPerCell,
                startedTick: tickIndex);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                intent.IntentId,
                entity.entityId,
                intent.Priority,
                outcome,
                facing,
                writeFacing: true,
                enemyLocomotionWrites: CreateEnemyKinematicLocomotionWrites(entity, intent),
                enemyPatrolWrites: CreateEnemyKinematicPatrolWrites(snapshot, entity, delta),
                executionLockWrites: CreateEnemyKinematicExecutionLockWrites(snapshot, entity, tickIndex));
            return true;
        }

        private bool TryBuildEnemyKinematicContinuationPayload(
            WorldSnapshot snapshot,
            int entityId,
            int tickIndex,
            List<string> rejectedReasons,
            out MovementActionPlanPayload payload)
        {
            payload = null;
            if (!snapshot.TryGetEntity(entityId, out var entity) ||
                !IsEnemyOrdinaryKinematicParticipant(snapshot, entity) ||
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                pose.IsSettledAtAnchor ||
                pose.Mode != MotionMode.Voluntary ||
                !TryResolveStepDirection(pose.State, out var stepDirectionX, out var stepDirectionY, out var facing))
            {
                return false;
            }

            var nextElapsedTicks = pose.State.elapsedTicks + 1;
            if (pose.State.totalTicks < 2 ||
                (pose.State.totalTicks % 2) != 0 ||
                nextElapsedTicks <= 0)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entityId}|Reason=EnemyKinematicContinuationCorrupt|Anchor={FormatCell(entity.position)}");
                return false;
            }

            var outcome = CreateKinematicMotionOutcome(
                entityId,
                pose.AnchorCell,
                pose.LocalOffset,
                pose.State,
                stepDirectionX,
                stepDirectionY,
                nextElapsedTicks,
                pose.State.totalTicks,
                pose.State.startedTick);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                _idAllocator.AllocateIntentId(),
                entityId,
                priority: 100,
                outcome: outcome,
                facing: facing,
                writeFacing: true);

            return true;
        }

        private static bool TryResolveEnemyKinematicStartScope(
            WorldSnapshot snapshot,
            MoveIntent intent,
            out EntityState entity,
            out UnitKinematicPose pose,
            out Vector2Int delta,
            out SurfaceCell destination,
            out Direction facing)
        {
            entity = default;
            pose = default;
            delta = Vector2Int.zero;
            destination = default;
            facing = Direction.None;

            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                !snapshot.TryGetEntity(intent.SourceId, out entity) ||
                !IsEnemyOrdinaryKinematicParticipant(snapshot, entity) ||
                !snapshot.TryGetUnitKinematicPose(intent.SourceId, out pose) ||
                !pose.IsSettledAtAnchor)
            {
                return false;
            }

            delta = intent.Destination - entity.position.PlanarPosition;
            if (!TryResolveKinematicVelocity(delta, out _, out facing) ||
                !snapshot.TryResolveUnitStep(
                    entity.position,
                    delta,
                    out destination,
                    out var rotationKind,
                    out _) ||
                rotationKind != CubeRotationKind.None ||
                destination.face != entity.position.face ||
                destination.PlanarPosition != intent.Destination)
            {
                return false;
            }

            return true;
        }

        private static bool IsEnemyOrdinaryKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) ||
                entity.aiMode == EnemyAiMode.Charge)
            {
                return false;
            }

            if (snapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState) &&
                jumpState.IsActive)
            {
                return false;
            }

            if (snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                glideState.HasAuthoritativeRecord &&
                glideState.Phase != EnemyGlidePhase.Ready)
            {
                return false;
            }

            if (snapshot.TryGetEnemyChargeState(entity.entityId, out var chargeState) &&
                chargeState.IsActive)
            {
                return false;
            }

            return !snapshot.TryGetPhasedState(entity.entityId, out var phasedState) ||
                   !phasedState.IsActive;
        }

        private static bool IsEnemyLogicParticipant(in EntityState entity)
        {
            return entity.type == EntityType.Unit &&
                   EnemyParticipationPolicy.IsEnemyLogicEntity(entity);
        }

        private static IReadOnlyList<EnemyLocomotionWritePayload> CreateEnemyKinematicLocomotionWrites(
            in EntityState entity,
            MoveIntent intent)
        {
            if (entity.aiMode != EnemyAiMode.Patrol &&
                entity.aiMode != EnemyAiMode.Chase)
            {
                return Array.Empty<EnemyLocomotionWritePayload>();
            }

            return new[]
            {
                new EnemyLocomotionWritePayload(entity.entityId, intent.MoveCooldownTicks),
            };
        }

        private List<ExecutionLockWritePayload> CreateEnemyKinematicExecutionLockWrites(
            WorldSnapshot snapshot,
            in EntityState entity,
            int tickIndex)
        {
            snapshot.TryGetEntityExecutionLockState(entity.entityId, out var previousState);
            return new List<ExecutionLockWritePayload>
            {
                new ExecutionLockWritePayload(
                    entity.entityId,
                    EntityExecutionLockQueries.StartMoveLock(previousState, tickIndex, _moveOccupancyTicks)),
            };
        }

        private static IReadOnlyList<EnemyPatrolWritePayload> CreateEnemyKinematicPatrolWrites(
            WorldSnapshot snapshot,
            in EntityState entity,
            Vector2Int delta)
        {
            if (entity.aiMode != EnemyAiMode.Patrol ||
                !snapshot.TryGetEnemyPatrolState(entity.entityId, out var enemyPatrolState) ||
                !enemyPatrolState.IsInitialized ||
                !EnemyMovementStrategyShared.TryResolveDirection(delta, out var patrolDirection))
            {
                return Array.Empty<EnemyPatrolWritePayload>();
            }

            return new[]
            {
                new EnemyPatrolWritePayload(
                    entity.entityId,
                    EnemyPatrolQueries.CommitMove(enemyPatrolState, patrolDirection)),
            };
        }

        private bool TryBuildPlayerKinematicStartPayload(
            WorldSnapshot snapshot,
            MoveIntent intent,
            int tickIndex,
            List<string> rejectedReasons,
            out bool handledByKinematic,
            out MovementActionPlanPayload payload)
        {
            handledByKinematic = false;
            payload = null;

            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                !snapshot.TryGetPlayerControlState(intent.SourceId, out _) ||
                !snapshot.TryGetEntity(intent.SourceId, out var entity) ||
                entity.type != EntityType.Unit ||
                !snapshot.TryGetUnitKinematicPose(intent.SourceId, out var pose) ||
                !pose.IsSettledAtAnchor)
            {
                return false;
            }

            var delta = intent.Destination - entity.position.PlanarPosition;
            if (!TryResolveKinematicVelocity(delta, out var velocity, out var facing))
            {
                return false;
            }

            if (!snapshot.TryResolvePlayerStep(
                    entity.position,
                    delta,
                    out var destination,
                    out var rotationKind,
                    out var updatedTopology))
            {
                return false;
            }

            if (rotationKind != CubeRotationKind.None ||
                destination.face != entity.position.face)
            {
                return false;
            }

            handledByKinematic = true;
            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destination,
                entity.entityId,
                snapshot.Topology,
                CubeRotationKind.None,
                updatedTopology);
            if (legality.Verdict != LegalityVerdict.Allowed)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=KinematicTraversalBlocked|Cell={FormatCell(destination)}|{LegalityDiagnosticsFormatter.FormatStableSummary(legality)}");
                return true;
            }

            if (!SurfaceKinematicSweepQueries.TryResolveSameFaceDelta(
                    snapshot,
                    entity.entityId,
                    velocity,
                    out var sweep) ||
                sweep.Blocked)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=KinematicSweepRejected|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
                return true;
            }

            var outcome = CreateKinematicMotionOutcome(
                sweep,
                entity.position,
                sourceState: pose.State,
                stepDirectionX: delta.x,
                stepDirectionY: delta.y,
                elapsedTicks: 1,
                totalTicks: _playerKinematicLocomotionTiming.TicksPerCell,
                startedTick: tickIndex);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                intent.IntentId,
                entity.entityId,
                intent.Priority,
                outcome,
                facing,
                writeFacing: true);
            return true;
        }

        private bool TryBuildPlayerKinematicContinuationPayload(
            WorldSnapshot snapshot,
            int entityId,
            int tickIndex,
            List<string> rejectedReasons,
            out MovementActionPlanPayload payload)
        {
            payload = null;
            if (!snapshot.TryGetPlayerControlState(entityId, out _) ||
                !snapshot.TryGetEntity(entityId, out var entity) ||
                entity.type != EntityType.Unit ||
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                pose.IsSettledAtAnchor ||
                pose.Mode != MotionMode.Voluntary ||
                !TryResolveStepDirection(pose.State, out var stepDirectionX, out var stepDirectionY, out var facing))
            {
                return false;
            }

            var nextElapsedTicks = pose.State.elapsedTicks + 1;
            if (pose.State.totalTicks < 2 ||
                (pose.State.totalTicks % 2) != 0 ||
                nextElapsedTicks <= 0)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entityId}|Reason=KinematicContinuationCorrupt|Anchor={FormatCell(entity.position)}");
                return false;
            }

            var outcome = CreateKinematicMotionOutcome(
                entityId,
                pose.AnchorCell,
                pose.LocalOffset,
                pose.State,
                stepDirectionX,
                stepDirectionY,
                nextElapsedTicks,
                pose.State.totalTicks,
                pose.State.startedTick);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                _idAllocator.AllocateIntentId(),
                entityId,
                priority: 100,
                outcome: outcome,
                facing: facing,
                writeFacing: true);

            return true;
        }

        private static bool TryResolveKinematicVelocity(
            Vector2Int delta,
            out KinematicVelocity2 velocity,
            out Direction facing)
        {
            if (delta == Vector2Int.right)
            {
                velocity = new KinematicVelocity2(KinematicFixed.FromRaw(KinematicFixed.DefaultPlayerUnitsPerTick), KinematicFixed.Zero);
                facing = Direction.Right;
                return true;
            }

            if (delta == Vector2Int.left)
            {
                velocity = new KinematicVelocity2(KinematicFixed.FromRaw(-KinematicFixed.DefaultPlayerUnitsPerTick), KinematicFixed.Zero);
                facing = Direction.Left;
                return true;
            }

            if (delta == Vector2Int.up)
            {
                velocity = new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(KinematicFixed.DefaultPlayerUnitsPerTick));
                facing = Direction.Up;
                return true;
            }

            if (delta == Vector2Int.down)
            {
                velocity = new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(-KinematicFixed.DefaultPlayerUnitsPerTick));
                facing = Direction.Down;
                return true;
            }

            velocity = KinematicVelocity2.Zero;
            facing = Direction.None;
            return false;
        }

        private static bool TryResolveFacing(KinematicVelocity2 velocity, out Direction facing)
        {
            if (velocity.X.RawValue > 0 && velocity.Y.RawValue == 0)
            {
                facing = Direction.Right;
                return true;
            }

            if (velocity.X.RawValue < 0 && velocity.Y.RawValue == 0)
            {
                facing = Direction.Left;
                return true;
            }

            if (velocity.Y.RawValue > 0 && velocity.X.RawValue == 0)
            {
                facing = Direction.Up;
                return true;
            }

            if (velocity.Y.RawValue < 0 && velocity.X.RawValue == 0)
            {
                facing = Direction.Down;
                return true;
            }

            facing = Direction.None;
            return false;
        }

        private static bool TryResolveStepDirection(
            UnitKinematicRuntimeState state,
            out int stepDirectionX,
            out int stepDirectionY,
            out Direction facing)
        {
            stepDirectionX = state.stepDirectionX;
            stepDirectionY = state.stepDirectionY;
            if (stepDirectionX == 1 && stepDirectionY == 0)
            {
                facing = Direction.Right;
                return true;
            }

            if (stepDirectionX == -1 && stepDirectionY == 0)
            {
                facing = Direction.Left;
                return true;
            }

            if (stepDirectionY == 1 && stepDirectionX == 0)
            {
                facing = Direction.Up;
                return true;
            }

            if (stepDirectionY == -1 && stepDirectionX == 0)
            {
                facing = Direction.Down;
                return true;
            }

            if (!TryResolveFacing(state.velocity, out facing))
            {
                return false;
            }

            switch (facing)
            {
                case Direction.Right:
                    stepDirectionX = 1;
                    stepDirectionY = 0;
                    return true;
                case Direction.Left:
                    stepDirectionX = -1;
                    stepDirectionY = 0;
                    return true;
                case Direction.Up:
                    stepDirectionX = 0;
                    stepDirectionY = 1;
                    return true;
                case Direction.Down:
                    stepDirectionX = 0;
                    stepDirectionY = -1;
                    return true;
                default:
                    return false;
            }
        }

        private static KinematicMotionOutcome CreateKinematicMotionOutcome(KinematicSweepResult sweep)
        {
            return new KinematicMotionOutcome(
                sweep.EntityId,
                sweep.SourcePose.AnchorCell,
                sweep.SourcePose.LocalOffset,
                sweep.ResolvedAnchorCell,
                sweep.ResolvedLocalOffset,
                sweep.ResolvedVelocity,
                CreateVoluntaryKinematicState(sweep.SourcePose.State, sweep.ResolvedLocalOffset, sweep.ResolvedVelocity),
                sweep.AnchorChanged,
                blocked: false,
                rejectedBy: sweep.RejectedBy);
        }

        private static KinematicMotionOutcome CreateKinematicMotionOutcome(
            KinematicSweepResult sweep,
            SurfaceCell currentAnchor,
            UnitKinematicRuntimeState sourceState,
            int stepDirectionX,
            int stepDirectionY,
            int elapsedTicks,
            int totalTicks,
            int startedTick)
        {
            return CreateKinematicMotionOutcome(
                sweep.EntityId,
                sweep.SourcePose.AnchorCell,
                sweep.SourcePose.LocalOffset,
                sourceState,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks,
                startedTick,
                currentAnchor);
        }

        private static KinematicMotionOutcome CreateKinematicMotionOutcome(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            UnitKinematicRuntimeState sourceState,
            int stepDirectionX,
            int stepDirectionY,
            int elapsedTicks,
            int totalTicks,
            int startedTick,
            SurfaceCell? currentAnchorOverride = null)
        {
            var currentAnchor = currentAnchorOverride ?? sourceAnchorCell;
            var resolution = KinematicProgressResolver.ResolvePose(
                currentAnchor,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks);
            var resolvedState = CreateVoluntaryKinematicState(
                sourceState,
                resolution,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks,
                startedTick);

            return new KinematicMotionOutcome(
                entityId,
                sourceAnchorCell,
                sourceLocalOffset,
                resolution.AnchorCell,
                resolution.LocalOffset,
                CreateDebugKinematicVelocity(stepDirectionX, stepDirectionY, totalTicks),
                resolvedState,
                resolution.IsAnchorCommitTick,
                blocked: false,
                rejectedBy: KinematicSweepRejectionReason.None);
        }

        private static KinematicMotionOutcome CreateBlockedKinematicMotionOutcome(KinematicSweepResult sweep)
        {
            return new KinematicMotionOutcome(
                sweep.EntityId,
                sweep.SourcePose.AnchorCell,
                sweep.SourcePose.LocalOffset,
                sweep.SourcePose.AnchorCell,
                KinematicOffset2.Zero,
                KinematicVelocity2.Zero,
                UnitKinematicRuntimeState.SettledZero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: sweep.RejectedBy);
        }

        private static UnitKinematicRuntimeState CreateVoluntaryKinematicState(
            UnitKinematicRuntimeState sourceState,
            KinematicOffset2 localOffset,
            KinematicVelocity2 velocity)
        {
            if (localOffset.IsZero)
            {
                return UnitKinematicRuntimeState.SettledZero;
            }

            var remainingDistanceUnits = ResolveRemainingVoluntaryDistanceUnits(localOffset, velocity);
            if (remainingDistanceUnits <= 0)
            {
                return UnitKinematicRuntimeState.SettledZero;
            }

            return new UnitKinematicRuntimeState
            {
                localOffset = localOffset,
                velocity = velocity,
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = remainingDistanceUnits,
                remainingTicks = (remainingDistanceUnits + KinematicFixed.DefaultPlayerUnitsPerTick - 1) /
                                 KinematicFixed.DefaultPlayerUnitsPerTick,
                speedScalePermille = 1000,
                sequenceId = sourceState.sequenceId + 1,
            }.NormalizedForStorage();
        }

        private static UnitKinematicRuntimeState CreateVoluntaryKinematicState(
            UnitKinematicRuntimeState sourceState,
            KinematicProgressResolution resolution,
            int stepDirectionX,
            int stepDirectionY,
            int elapsedTicks,
            int totalTicks,
            int startedTick)
        {
            if (resolution.IsSettled)
            {
                return UnitKinematicRuntimeState.SettledZero;
            }

            return new UnitKinematicRuntimeState
            {
                localOffset = resolution.LocalOffset,
                velocity = CreateDebugKinematicVelocity(stepDirectionX, stepDirectionY, totalTicks),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = resolution.RemainingDistanceUnits,
                remainingTicks = resolution.RemainingTicks,
                speedScalePermille = 1000,
                sequenceId = sourceState.sequenceId + 1,
                elapsedTicks = elapsedTicks,
                totalTicks = totalTicks,
                commitTick = totalTicks / 2,
                startedTick = startedTick,
                stepDirectionX = stepDirectionX,
                stepDirectionY = stepDirectionY,
            }.NormalizedForStorage();
        }

        private static KinematicVelocity2 CreateDebugKinematicVelocity(
            int stepDirectionX,
            int stepDirectionY,
            int totalTicks)
        {
            var unitsPerTick = Math.Max(
                1,
                KinematicProgressResolver.ResolveProgressUnits(
                    1,
                    Math.Max(2, totalTicks),
                    KinematicFixed.UnitsPerCell));
            return new KinematicVelocity2(
                KinematicFixed.FromRaw(stepDirectionX * unitsPerTick),
                KinematicFixed.FromRaw(stepDirectionY * unitsPerTick));
        }

        private static int ResolveRemainingVoluntaryDistanceUnits(
            KinematicOffset2 localOffset,
            KinematicVelocity2 velocity)
        {
            if (velocity.X.RawValue > 0)
            {
                return localOffset.X.RawValue < 0
                    ? -localOffset.X.RawValue
                    : KinematicFixed.UnitsPerCell - localOffset.X.RawValue;
            }

            if (velocity.X.RawValue < 0)
            {
                return localOffset.X.RawValue > 0
                    ? localOffset.X.RawValue
                    : KinematicFixed.UnitsPerCell + localOffset.X.RawValue;
            }

            if (velocity.Y.RawValue > 0)
            {
                return localOffset.Y.RawValue < 0
                    ? -localOffset.Y.RawValue
                    : KinematicFixed.UnitsPerCell - localOffset.Y.RawValue;
            }

            if (velocity.Y.RawValue < 0)
            {
                return localOffset.Y.RawValue > 0
                    ? localOffset.Y.RawValue
                    : KinematicFixed.UnitsPerCell + localOffset.Y.RawValue;
            }

            return 0;
        }

        private static MovementActionPlanPayload CreateKinematicMovementPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            KinematicMotionOutcome outcome,
            Direction facing,
            bool writeFacing,
            IReadOnlyList<EnemyLocomotionWritePayload> enemyLocomotionWrites = null,
            IReadOnlyList<EnemyPatrolWritePayload> enemyPatrolWrites = null,
            IReadOnlyList<ExecutionLockWritePayload> executionLockWrites = null)
        {
            var destinationCell = outcome.AnchorChanged
                ? outcome.ResolvedAnchorCell
                : outcome.SourceAnchorCell;
            var facingWrites = writeFacing
                ? new[] { new FacingWritePayload(sourceActorEntityId, facing) }
                : Array.Empty<FacingWritePayload>();

            return new MovementActionPlanPayload(
                actionPlanId,
                intentId,
                sourceActorEntityId,
                priority,
                ResolvedActionSemanticKind.Move,
                MovementCandidateKind.Move,
                outcome.SourceAnchorCell,
                destinationCell,
                outcome.AnchorChanged,
                outcome.AnchorChanged
                    ? new MovementEdge(outcome.SourceAnchorCell, outcome.ResolvedAnchorCell)
                    : default,
                new[] { sourceActorEntityId },
                outcome.AnchorChanged ? MovementReservationKind.Edge : MovementReservationKind.None,
                MovementBlockingType.NonBlocking,
                Array.Empty<StateChangeWritePayload>(),
                Array.Empty<MoveWritePayload>(),
                Array.Empty<BoardPresenceWritePayload>(),
                facingWrites,
                Array.Empty<BoxKineticOwnerWritePayload>(),
                Array.Empty<TopologyWritePayload>(),
                executionLockWrites ?? Array.Empty<ExecutionLockWritePayload>(),
                enemyLocomotionWrites ?? Array.Empty<EnemyLocomotionWritePayload>(),
                enemyPatrolWrites ?? Array.Empty<EnemyPatrolWritePayload>(),
                Array.Empty<PlayerControlWritePayload>(),
                Array.Empty<DestroyWritePayload>(),
                hasImpactReservationPayload: false,
                impactReservationPayload: default,
                hasDeferredImpactPayload: false,
                deferredImpactPayload: default,
                kinematicMotionOutcomes: new[] { outcome });
        }

        private static void MergeMovementActionPlanPayloads(
            Dictionary<int, MovementActionPlanPayload> destination,
            IReadOnlyDictionary<int, MovementActionPlanPayload> source)
        {
            foreach (var pair in source)
            {
                destination.Add(pair.Key, pair.Value);
            }
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

        private List<AttackIntent> NormalizeAttackInputs(
            List<RawAttackIntent> rawAttackIntents,
            FrozenMovementReservationExport movementReservationExport,
            List<DelayedAttackEffectRecord> delayedAttackEffects)
        {
            var sortedInputs = new List<AttackIntent>(
                rawAttackIntents.Count + movementReservationExport.ImpactReservations.Count + delayedAttackEffects.Count);
            _attackInputNormalizer.Normalize(rawAttackIntents, movementReservationExport, delayedAttackEffects, sortedInputs);

            for (var i = 0; i < sortedInputs.Count; i++)
            {
                sortedInputs[i].AssignIntentId(_idAllocator.AllocateIntentId());
            }

            return sortedInputs;
        }

        private AttackPlanBuildResult BuildAttackPlan(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IAttackEntityLogic> entityLogics,
            List<ImpactReservation> impactReservations,
            List<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            int tickIndex)
        {
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, in input, entityLogics, rawAttackIntents);
            var rejectedReasons = new List<string>();
            var executableAttackIntents = FilterExecutionLockedAttackIntents(snapshot, tickIndex, rawAttackIntents, rejectedReasons);
            var sortedInputs = NormalizeAttackInputs(executableAttackIntents, impactReservations, drainedDelayedAttackEffects);
            var expandedAttackCandidates = new List<ActionGroup>();
            _attackExpander.Expand(snapshot, sortedInputs, expandedAttackCandidates, rejectedReasons);
            expandedAttackCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedAttackCandidates);
            var actionPlanPayloads = BuildAttackActionPlanPayloads(snapshot, expandedAttackCandidates, tickIndex);
            var orderedActionPlanIds = BuildOrderedAttackActionPlanIds(snapshot, expandedAttackCandidates);
            return new AttackPlanBuildResult(rawAttackIntents, expandedAttackCandidates, actionPlanPayloads, orderedActionPlanIds, rejectedReasons);
        }

        private AttackPlanBuildResult BuildAttackPlan(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IAttackEntityLogic> entityLogics,
            FrozenMovementReservationExport movementReservationExport,
            List<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            int tickIndex)
        {
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, in input, entityLogics, rawAttackIntents);
            var rejectedReasons = new List<string>();
            var executableAttackIntents = FilterExecutionLockedAttackIntents(snapshot, tickIndex, rawAttackIntents, rejectedReasons);
            var sortedInputs = NormalizeAttackInputs(executableAttackIntents, movementReservationExport, drainedDelayedAttackEffects);
            var expandedAttackCandidates = new List<ActionGroup>();
            _attackExpander.Expand(snapshot, sortedInputs, expandedAttackCandidates, rejectedReasons);
            expandedAttackCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedAttackCandidates);
            var actionPlanPayloads = BuildAttackActionPlanPayloads(snapshot, expandedAttackCandidates, tickIndex);
            var orderedActionPlanIds = BuildOrderedAttackActionPlanIds(snapshot, expandedAttackCandidates);
            return new AttackPlanBuildResult(rawAttackIntents, expandedAttackCandidates, actionPlanPayloads, orderedActionPlanIds, rejectedReasons);
        }

        private Dictionary<int, MovementActionPlanPayload> BuildMovementActionPlanPayloads(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            IReadOnlyList<ActionGroup> expandedCandidates,
            List<string> rejectedReasons,
            int tickIndex)
        {
            var payloads = new Dictionary<int, MovementActionPlanPayload>(expandedCandidates.Count);

            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                var group = expandedCandidates[i];
                var stateChangeWrites = new List<StateChangeWritePayload>(group.StateChanges.Count);
                for (var stateIndex = 0; stateIndex < group.StateChanges.Count; stateIndex++)
                {
                    var stateChange = group.StateChanges[stateIndex];
                    stateChangeWrites.Add(new StateChangeWritePayload(stateChange.EntityId, stateChange.State, stateChange.StateTimer));
                }

                var moveWrites = new List<MoveWritePayload>(group.Moves.Count);
                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    moveWrites.Add(new MoveWritePayload(move.EntityId, move.SourceCell, move.DestinationCell, move.Facing));
                }

                var boardPresenceWrites = new List<BoardPresenceWritePayload>(group.BoardPresenceChanges.Count);
                for (var changeIndex = 0; changeIndex < group.BoardPresenceChanges.Count; changeIndex++)
                {
                    var change = group.BoardPresenceChanges[changeIndex];
                    boardPresenceWrites.Add(
                        new BoardPresenceWritePayload(
                            change.EntityId,
                            change.BoardPresence,
                            group.GroupKind == ActionGroupKind.Item ? TickEntityExitCause.ItemConsume : TickEntityExitCause.None));
                }

                var facingWrites = new List<FacingWritePayload>();
                if (group.GroupKind == ActionGroupKind.Flip)
                {
                    facingWrites.Add(new FacingWritePayload(group.SourceId, ResolveFlipSourceFacing(snapshot, sortedIntents, group)));
                }

                var boxKineticOwnerWrites = new List<BoxKineticOwnerWritePayload>();
                if (group.BoxKineticTargetId > 0)
                {
                    boxKineticOwnerWrites.Add(
                        new BoxKineticOwnerWritePayload(
                            group.BoxKineticTargetId,
                            group.BoxKineticInstigatorEntityId,
                            group.BoxKineticInstigatorTeamId));
                }

                var topologyWrites = new List<TopologyWritePayload>(group.TopologyChanges.Count);
                for (var topologyIndex = 0; topologyIndex < group.TopologyChanges.Count; topologyIndex++)
                {
                    var topologyChange = group.TopologyChanges[topologyIndex];
                    topologyWrites.Add(new TopologyWritePayload(topologyChange.UpdatedTopology, topologyChange.RotationKind));
                }

                var executionLockWrites = new List<ExecutionLockWritePayload>();
                if (group.GroupKind == ActionGroupKind.Move ||
                    group.GroupKind == ActionGroupKind.Item)
                {
                    for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                    {
                        var move = group.Moves[moveIndex];
                        if (!snapshot.TryGetEntity(move.EntityId, out var movedEntity) ||
                            movedEntity.type != EntityType.Unit)
                        {
                            continue;
                        }

                        snapshot.TryGetEntityExecutionLockState(move.EntityId, out var previousState);
                        executionLockWrites.Add(
                            new ExecutionLockWritePayload(
                                move.EntityId,
                                EntityExecutionLockQueries.StartMoveLock(previousState, tickIndex, _moveOccupancyTicks)));
                    }
                }

                var enemyLocomotionWrites = new List<EnemyLocomotionWritePayload>();
                if (snapshot.TryGetEntity(group.SourceId, out var sourceEntity) &&
                    sourceEntity.type == EntityType.Unit &&
                    (sourceEntity.aiMode == EnemyAiMode.Patrol ||
                     sourceEntity.aiMode == EnemyAiMode.Chase ||
                     sourceEntity.aiMode == EnemyAiMode.Charge))
                {
                    var intent = FindMovementIntent(sortedIntents, group.IntentId);
                    if (intent != null &&
                        intent.CommandKind == Movement.MovementCommandKind.Move)
                    {
                        enemyLocomotionWrites.Add(new EnemyLocomotionWritePayload(group.SourceId, intent.MoveCooldownTicks));
                    }
                }

                var enemyPatrolWrites = new List<EnemyPatrolWritePayload>();
                if (sourceEntity.type == EntityType.Unit &&
                    sourceEntity.aiMode == EnemyAiMode.Patrol)
                {
                    var intent = FindMovementIntent(sortedIntents, group.IntentId);
                    if (intent != null &&
                        intent.CommandKind == Movement.MovementCommandKind.Move &&
                        snapshot.TryGetEnemyPatrolState(group.SourceId, out var enemyPatrolState) &&
                        enemyPatrolState.IsInitialized &&
                        EnemyMovementStrategyShared.TryResolveDirection(
                            intent.Destination - sourceEntity.position.PlanarPosition,
                            out var patrolDirection))
                    {
                        enemyPatrolWrites.Add(
                            new EnemyPatrolWritePayload(
                                group.SourceId,
                                EnemyPatrolQueries.CommitMove(enemyPatrolState, patrolDirection)));
                    }
                }

                var playerControlWrites = new List<PlayerControlWritePayload>();
                if (snapshot.TryGetPlayerControlState(group.SourceId, out var playerControlState))
                {
                    var intent = FindMovementIntent(sortedIntents, group.IntentId);
                    if (intent != null &&
                        intent.CommandKind == Movement.MovementCommandKind.Move)
                    {
                        playerControlWrites.Add(
                            new PlayerControlWritePayload(
                                group.SourceId,
                                PlayerControlQueries.ConsumeMoveCooldown(playerControlState, _playerMoveCooldownTicks, tickIndex)));
                    }
                }

                var sourceCell = TryResolveMovementSourceCell(snapshot, group, moveWrites, out var resolvedSourceCell)
                    ? resolvedSourceCell
                    : default;
                var destinationCell = TryResolveMovementDestinationCell(group, moveWrites, out var resolvedDestinationCell)
                    ? resolvedDestinationCell
                    : default;
                var hasMovementEdge = TryResolveMovementEdge(group, moveWrites, out var movementEdge);
                var affectedEntityIds = BuildAffectedEntityIds(group);
                var reservationKind = ResolveMovementReservationKind(snapshot, group);
                var blockingType = ResolveMovementBlockingType(snapshot, group);
                var destroyWrites = BuildDestroyWritePayloads(group.Destroys, snapshot, group, TickEntityExitCause.None);
                var hasImpactReservationPayload = TryBuildImpactReservationPayload(
                    snapshot,
                    group,
                    out var impactReservationPayload,
                    out var impactRejectedReason);
                if (!string.IsNullOrEmpty(impactRejectedReason))
                {
                    rejectedReasons.Add(impactRejectedReason);
                }
                var hasDeferredImpactPayload = TryBuildDeferredImpactPayload(
                    group,
                    out var deferredImpactPayload);

                payloads[group.GroupId] = new MovementActionPlanPayload(
                    group.GroupId,
                    group.IntentId,
                    group.SourceId,
                    group.Priority,
                    ResolveMovementSemanticKind(snapshot, group),
                    ResolveMovementCandidateKind(group),
                    sourceCell,
                    destinationCell,
                    hasMovementEdge,
                    movementEdge,
                    affectedEntityIds,
                    reservationKind,
                    blockingType,
                    stateChangeWrites,
                    moveWrites,
                    boardPresenceWrites,
                    facingWrites,
                    boxKineticOwnerWrites,
                    topologyWrites,
                    executionLockWrites,
                    enemyLocomotionWrites,
                    enemyPatrolWrites,
                    playerControlWrites,
                    destroyWrites,
                    hasImpactReservationPayload,
                    impactReservationPayload,
                    hasDeferredImpactPayload,
                    deferredImpactPayload);
            }

            return payloads;
        }

        private static bool TryBuildDeferredImpactPayload(
            ActionGroup group,
            out MovementDeferredImpactPayload deferredImpactPayload)
        {
            if (!group.HasDeferredImpact)
            {
                deferredImpactPayload = default;
                return false;
            }

            deferredImpactPayload = new MovementDeferredImpactPayload(
                group.DeferredImpactSourceId,
                group.DeferredImpactCell,
                damageAmount: 1,
                sequence: 0);
            return true;
        }

        private Dictionary<int, AttackActionPlanPayload> BuildAttackActionPlanPayloads(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> expandedCandidates,
            int tickIndex)
        {
            var payloads = new Dictionary<int, AttackActionPlanPayload>(expandedCandidates.Count);

            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                var group = expandedCandidates[i];
                var stateChangeWrites = new List<StateChangeWritePayload>(group.StateChanges.Count);
                for (var stateIndex = 0; stateIndex < group.StateChanges.Count; stateIndex++)
                {
                    var stateChange = group.StateChanges[stateIndex];
                    stateChangeWrites.Add(new StateChangeWritePayload(stateChange.EntityId, stateChange.State, stateChange.StateTimer));
                }

                var damageWrites = new List<DamageWritePayload>(group.Damages.Count);
                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    var hasPlayerDamageState = false;
                    var playerDamageState = default(PlayerDamageState);
                    if (snapshot.TryGetPlayerDamageState(damage.TargetId, out var previousPlayerDamageState) &&
                        PlayerDamageQueries.CanAcceptDamage(previousPlayerDamageState, tickIndex))
                    {
                        hasPlayerDamageState = true;
                        playerDamageState = PlayerDamageQueries.AcceptDamage(
                            previousPlayerDamageState,
                            tickIndex,
                            _playerDamageCooldownTicks);
                    }

                    damageWrites.Add(
                        new DamageWritePayload(
                            damage.TargetId,
                            damage.Amount,
                            hasPlayerDamageState,
                            playerDamageState,
                            group.AttackSourceKind));
                }

                var spawnWrites = new List<SpawnWritePayload>(group.Spawns.Count);
                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    spawnWrites.Add(new SpawnWritePayload(group.Spawns[spawnIndex].Entity, SpawnSourceKind.Attack));
                }

                var delayedEnqueueWrites = new List<DelayedEnqueueWritePayload>(group.DelayedAttacks.Count);
                for (var delayedIndex = 0; delayedIndex < group.DelayedAttacks.Count; delayedIndex++)
                {
                    var delayedAttack = group.DelayedAttacks[delayedIndex];
                    delayedEnqueueWrites.Add(
                        new DelayedEnqueueWritePayload(
                            delayedAttack.TargetId,
                            delayedAttack.Damage,
                            tickGenerated: tickIndex,
                            executeAtTick: tickIndex + 1,
                            effectSequence: delayedIndex + 1));
                }

                payloads[group.GroupId] = new AttackActionPlanPayload(
                    group.GroupId,
                    group.IntentId,
                    group.SourceId,
                    group.Priority,
                    ResolvedActionSemanticKind.Attack,
                    stateChangeWrites,
                    damageWrites,
                    spawnWrites,
                    BuildDestroyWritePayloads(group.Destroys, snapshot, group, TickEntityExitCause.Killed),
                    delayedEnqueueWrites);
            }

            return payloads;
        }

        private static Dictionary<int, JumpLandingActionPlanPayload> BuildJumpLandingActionPlanPayloads(
            IReadOnlyList<JumpLandingPlan> jumpLandingPlans)
        {
            var payloads = new Dictionary<int, JumpLandingActionPlanPayload>(jumpLandingPlans.Count);

            for (var i = 0; i < jumpLandingPlans.Count; i++)
            {
                var plan = jumpLandingPlans[i];
                payloads[plan.ActionPlanId] = new JumpLandingActionPlanPayload(
                    plan.ActionPlanId,
                    plan.SourceId,
                    plan.Priority,
                    plan.LandingKind,
                    plan.DestinationCell,
                    plan.TargetId,
                    plan.LandingRule,
                    plan.SuccessState,
                    plan.RetryState);
            }

            return payloads;
        }

        private static Dictionary<int, PhaseRelocationActionPlanPayload> BuildPhaseRelocationActionPlanPayloads(
            IReadOnlyList<PhaseRelocationPlan> phaseRelocationPlans)
        {
            var payloads = new Dictionary<int, PhaseRelocationActionPlanPayload>(phaseRelocationPlans.Count);

            for (var i = 0; i < phaseRelocationPlans.Count; i++)
            {
                var plan = phaseRelocationPlans[i];
                payloads[plan.ActionPlanId] = new PhaseRelocationActionPlanPayload(
                    plan.ActionPlanId,
                    plan.SourceId,
                    plan.Priority,
                    plan.LockedTargetEntityId,
                    plan.Direction,
                    plan.DestinationCell,
                    plan.RuleLabel);
            }

            return payloads;
        }

        private static List<DestroyWritePayload> BuildDestroyWritePayloads(
            IReadOnlyList<DestroyAction> destroys,
            WorldSnapshot snapshot,
            ActionGroup group,
            TickEntityExitCause exitCauseHint)
        {
            var payloads = new List<DestroyWritePayload>(destroys.Count);
            for (var i = 0; i < destroys.Count; i++)
            {
                payloads.Add(
                    new DestroyWritePayload(
                        destroys[i].TargetId,
                        destroys[i].Condition,
                        ResolveMovementDestroyExitCause(snapshot, group, destroys[i].TargetId) == TickEntityExitCause.None
                            ? exitCauseHint
                            : ResolveMovementDestroyExitCause(snapshot, group, destroys[i].TargetId)));
            }

            return payloads;
        }

        private static List<int> BuildOrderedJumpLandingActionPlanIds(IReadOnlyList<JumpLandingPlan> jumpLandingPlans)
        {
            var orderedIds = new List<int>(jumpLandingPlans.Count);
            for (var i = 0; i < jumpLandingPlans.Count; i++)
            {
                orderedIds.Add(jumpLandingPlans[i].ActionPlanId);
            }

            return orderedIds;
        }

        private static List<int> BuildOrderedPhaseRelocationActionPlanIds(IReadOnlyList<PhaseRelocationPlan> phaseRelocationPlans)
        {
            var orderedIds = new List<int>(phaseRelocationPlans.Count);
            for (var i = 0; i < phaseRelocationPlans.Count; i++)
            {
                orderedIds.Add(phaseRelocationPlans[i].ActionPlanId);
            }

            return orderedIds;
        }

        private List<int> BuildOrderedMovementActionPlanIds(
            WorldSnapshot snapshot,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads)
        {
            var ordered = new List<MovementActionPlanPayload>(payloads.Count);
            foreach (var pair in payloads)
            {
                ordered.Add(pair.Value);
            }

            ordered.Sort((left, right) => CompareMovementCanonicalOrder(snapshot, left, right));

            var orderedIds = new List<int>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                orderedIds.Add(ordered[i].ActionPlanId);
            }

            return orderedIds;
        }

        private List<int> BuildOrderedAttackActionPlanIds(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> expandedCandidates)
        {
            var ordered = new List<ActionGroup>(expandedCandidates.Count);
            AddRange(ordered, expandedCandidates);
            ordered.Sort((left, right) => CompareAttackCanonicalOrder(snapshot, left, right));

            var orderedIds = new List<int>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                orderedIds.Add(ordered[i].GroupId);
            }

            return orderedIds;
        }

        private static int CompareMovementCanonicalOrder(WorldSnapshot snapshot, ActionGroup left, ActionGroup right)
        {
            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = ResolveMovementSourceRank(snapshot, left).CompareTo(ResolveMovementSourceRank(snapshot, right));
            if (result != 0)
            {
                return result;
            }

            result = ResolveMovementIntentPriority(left).CompareTo(ResolveMovementIntentPriority(right));
            if (result != 0)
            {
                return result;
            }

            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.IntentId.CompareTo(right.IntentId);
            if (result != 0)
            {
                return result;
            }

            return left.GroupId.CompareTo(right.GroupId);
        }

        private static int CompareMovementCanonicalOrder(
            WorldSnapshot snapshot,
            MovementActionPlanPayload left,
            MovementActionPlanPayload right)
        {
            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = ResolveMovementSourceRank(snapshot, left).CompareTo(ResolveMovementSourceRank(snapshot, right));
            if (result != 0)
            {
                return result;
            }

            result = ResolveMovementIntentPriority(left).CompareTo(ResolveMovementIntentPriority(right));
            if (result != 0)
            {
                return result;
            }

            result = left.SourceActorEntityId.CompareTo(right.SourceActorEntityId);
            if (result != 0)
            {
                return result;
            }

            result = left.IntentId.CompareTo(right.IntentId);
            if (result != 0)
            {
                return result;
            }

            return left.ActionPlanId.CompareTo(right.ActionPlanId);
        }

        private static int CompareAttackCanonicalOrder(WorldSnapshot snapshot, ActionGroup left, ActionGroup right)
        {
            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = ResolveAttackSourceRank(snapshot, left).CompareTo(ResolveAttackSourceRank(snapshot, right));
            if (result != 0)
            {
                return result;
            }

            result = ResolveAttackIntentPriority(left).CompareTo(ResolveAttackIntentPriority(right));
            if (result != 0)
            {
                return result;
            }

            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.IntentId.CompareTo(right.IntentId);
            if (result != 0)
            {
                return result;
            }

            return left.GroupId.CompareTo(right.GroupId);
        }

        private static int ResolveMovementSourceRank(WorldSnapshot snapshot, ActionGroup group)
        {
            if (group.GroupKind == ActionGroupKind.BoxImpact)
            {
                return 2;
            }

            if (group.GroupKind == ActionGroupKind.ProjectileImpact)
            {
                return 3;
            }

            if (snapshot.TryGetPlayerControlState(group.SourceId, out _))
            {
                return 0;
            }

            if (snapshot.TryGetEntity(group.SourceId, out var entity) &&
                entity.type == EntityType.Unit)
            {
                return 1;
            }

            if (snapshot.TryGetEntity(group.SourceId, out entity) &&
                entity.type == EntityType.Box)
            {
                return 2;
            }

            return 3;
        }

        private static int ResolveMovementSourceRank(WorldSnapshot snapshot, MovementActionPlanPayload payload)
        {
            if (payload.MovementCandidateKind == MovementCandidateKind.BoxImpact)
            {
                return 2;
            }

            if (payload.MovementCandidateKind == MovementCandidateKind.ProjectileImpact)
            {
                return 3;
            }

            if (snapshot.TryGetPlayerControlState(payload.SourceActorEntityId, out _))
            {
                return 0;
            }

            if (snapshot.TryGetEntity(payload.SourceActorEntityId, out var entity) &&
                entity.type == EntityType.Unit)
            {
                return 1;
            }

            if (snapshot.TryGetEntity(payload.SourceActorEntityId, out entity) &&
                entity.type == EntityType.Box)
            {
                return 2;
            }

            return 3;
        }

        private static int ResolveAttackSourceRank(WorldSnapshot snapshot, ActionGroup group)
        {
            if (group.AttackSourceKind == AttackSourceKind.DelayedEffect)
            {
                return 3;
            }

            if (group.AttackSourceKind == AttackSourceKind.ImpactReservation)
            {
                return 2;
            }

            if (snapshot.TryGetPlayerControlState(group.SourceId, out _))
            {
                return 0;
            }

            if (snapshot.TryGetEntity(group.SourceId, out var entity) &&
                entity.type == EntityType.Unit)
            {
                return 1;
            }

            return 3;
        }

        private static int ResolveMovementIntentPriority(ActionGroup group)
        {
            return group.GroupKind switch
            {
                ActionGroupKind.Move => 0,
                ActionGroupKind.Item => 1,
                ActionGroupKind.Flip => 2,
                ActionGroupKind.Push => 3,
                ActionGroupKind.Stop => 4,
                ActionGroupKind.BoxImpact => 5,
                ActionGroupKind.ProjectileImpact => 6,
                _ => 99,
            };
        }

        private static int ResolveMovementIntentPriority(MovementActionPlanPayload payload)
        {
            return payload.MovementCandidateKind switch
            {
                MovementCandidateKind.Move => 0,
                MovementCandidateKind.Item => 1,
                MovementCandidateKind.Flip => 2,
                MovementCandidateKind.Push => 3,
                MovementCandidateKind.Stop => 4,
                MovementCandidateKind.BoxImpact => 5,
                MovementCandidateKind.ProjectileImpact => 6,
                _ => 99,
            };
        }

        private static int ResolveAttackIntentPriority(ActionGroup group)
        {
            return group.AttackSourceKind switch
            {
                AttackSourceKind.Combat => 0,
                AttackSourceKind.PassiveContact => 1,
                AttackSourceKind.ImpactReservation => 2,
                AttackSourceKind.DelayedEffect => 3,
                _ => 99,
            };
        }

        private static MovementCandidateKind ResolveMovementCandidateKind(ActionGroup group)
        {
            return group.GroupKind switch
            {
                ActionGroupKind.Move => MovementCandidateKind.Move,
                ActionGroupKind.Push => MovementCandidateKind.Push,
                ActionGroupKind.Flip => MovementCandidateKind.Flip,
                ActionGroupKind.BoxImpact => MovementCandidateKind.BoxImpact,
                ActionGroupKind.ProjectileImpact => MovementCandidateKind.ProjectileImpact,
                ActionGroupKind.Stop => MovementCandidateKind.Stop,
                ActionGroupKind.Item => MovementCandidateKind.Item,
                _ => MovementCandidateKind.Move,
            };
        }

        private static ResolvedActionSemanticKind ResolveMovementSemanticKind(WorldSnapshot snapshot, ActionGroup group)
        {
            switch (group.GroupKind)
            {
                case ActionGroupKind.Push:
                    if (TryResolveMovementEntity(snapshot, group, out var pushEntity) &&
                        pushEntity.type == EntityType.Box &&
                        (pushEntity.boxCapabilities & BoxCapabilities.Push) == BoxCapabilities.Push &&
                        (pushEntity.entityId != group.SourceId ||
                         pushEntity.state == EntityPhaseState.Sliding))
                    {
                        return ResolvedActionSemanticKind.Slide;
                    }

                    return ResolvedActionSemanticKind.Push;

                case ActionGroupKind.Flip:
                    return ResolvedActionSemanticKind.Flip;

                case ActionGroupKind.BoxImpact:
                case ActionGroupKind.ProjectileImpact:
                    return ResolvedActionSemanticKind.Impact;

                case ActionGroupKind.Item:
                    return ResolvedActionSemanticKind.Item;

                case ActionGroupKind.Move:
                    if (TryResolveMovementEntity(snapshot, group, out var entity))
                    {
                        if (entity.type == EntityType.Projectile)
                        {
                            return ResolvedActionSemanticKind.ProjectileMove;
                        }

                        if (entity.type == EntityType.Box &&
                            entity.state == EntityPhaseState.Sliding &&
                            (entity.boxCapabilities & BoxCapabilities.Push) == BoxCapabilities.Push)
                        {
                            return ResolvedActionSemanticKind.Slide;
                        }
                    }

                    return ResolvedActionSemanticKind.Move;

                case ActionGroupKind.Stop:
                    return ResolvedActionSemanticKind.Stop;

                default:
                    return ResolvedActionSemanticKind.None;
            }
        }

        private static bool TryResolveMovementEntity(
            WorldSnapshot snapshot,
            ActionGroup group,
            out EntityState entity)
        {
            for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
            {
                if (snapshot.TryGetEntity(group.Moves[moveIndex].EntityId, out entity))
                {
                    return true;
                }
            }

            if (snapshot.TryGetEntity(group.SourceId, out entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        private static bool TryResolveMovementSourceCell(
            WorldSnapshot snapshot,
            ActionGroup group,
            IReadOnlyList<MoveWritePayload> moveWrites,
            out SurfaceCell sourceCell)
        {
            if (moveWrites.Count > 0)
            {
                sourceCell = moveWrites[0].SourceCell;
                return true;
            }

            if (snapshot.TryGetEntity(group.SourceId, out var sourceEntity))
            {
                sourceCell = sourceEntity.position;
                return true;
            }

            sourceCell = default;
            return false;
        }

        private static bool TryResolveMovementDestinationCell(
            ActionGroup group,
            IReadOnlyList<MoveWritePayload> moveWrites,
            out SurfaceCell destinationCell)
        {
            if (moveWrites.Count > 0)
            {
                destinationCell = moveWrites[0].DestinationCell;
                return true;
            }

            if (group.HasResolvedImpact)
            {
                destinationCell = default;
                return false;
            }

            destinationCell = default;
            return false;
        }

        private static bool TryResolveMovementEdge(
            ActionGroup group,
            IReadOnlyList<MoveWritePayload> moveWrites,
            out MovementEdge movementEdge)
        {
            movementEdge = default;
            if (moveWrites.Count == 0 ||
                group.GroupKind == ActionGroupKind.Flip)
            {
                return false;
            }

            movementEdge = new MovementEdge(moveWrites[0].SourceCell, moveWrites[0].DestinationCell);
            return true;
        }

        private static List<int> BuildAffectedEntityIds(ActionGroup group)
        {
            var affectedEntityIds = new List<int>();
            var visited = new HashSet<int>();

            for (var i = 0; i < group.Moves.Count; i++)
            {
                if (visited.Add(group.Moves[i].EntityId))
                {
                    affectedEntityIds.Add(group.Moves[i].EntityId);
                }
            }

            for (var i = 0; i < group.StateChanges.Count; i++)
            {
                if (visited.Add(group.StateChanges[i].EntityId))
                {
                    affectedEntityIds.Add(group.StateChanges[i].EntityId);
                }
            }

            for (var i = 0; i < group.BoardPresenceChanges.Count; i++)
            {
                if (visited.Add(group.BoardPresenceChanges[i].EntityId))
                {
                    affectedEntityIds.Add(group.BoardPresenceChanges[i].EntityId);
                }
            }

            for (var i = 0; i < group.Destroys.Count; i++)
            {
                if (visited.Add(group.Destroys[i].TargetId))
                {
                    affectedEntityIds.Add(group.Destroys[i].TargetId);
                }
            }

            if (group.BoxKineticTargetId > 0 &&
                visited.Add(group.BoxKineticTargetId))
            {
                affectedEntityIds.Add(group.BoxKineticTargetId);
            }

            return affectedEntityIds;
        }

        private static MovementReservationKind ResolveMovementReservationKind(WorldSnapshot snapshot, ActionGroup group)
        {
            var reservationMode = ResolveReservationMode(snapshot, group);
            if (group.Moves.Count == 0)
            {
                return MovementReservationKind.None;
            }

            return RequiresEdgeReservation(group)
                ? MovementReservationKind.Edge
                : MovementReservationKind.Vertex;
        }

        private static MovementBlockingType ResolveMovementBlockingType(WorldSnapshot snapshot, ActionGroup group)
        {
            if (group.GroupKind == ActionGroupKind.ProjectileImpact)
            {
                return MovementBlockingType.PassThrough;
            }

            return ResolveReservationMode(snapshot, group) == ReservationMode.UnitSharedMove
                ? MovementBlockingType.NonBlocking
                : MovementBlockingType.Blocking;
        }

        private bool TryBuildImpactReservationPayload(
            WorldSnapshot snapshot,
            ActionGroup group,
            out MovementImpactReservationPayload impactReservationPayload,
            out string rejectedReason)
        {
            if (!group.HasResolvedImpact ||
                !snapshot.TryGetEntity(group.ImpactSourceId, out var impactSourceEntity))
            {
                impactReservationPayload = default;
                rejectedReason = string.Empty;
                return false;
            }

            var impactCell = FindImpactCell(snapshot, group);
            if (!ImpactGeometryResolver.TryResolve(
                    impactSourceEntity.position,
                    impactCell,
                    out var geometry,
                    out var rejectReason))
            {
                impactReservationPayload = default;
                rejectedReason = BuildImpactReservationRejectedReason(
                    group,
                    impactSourceEntity.position,
                    impactCell,
                    rejectReason);
                return false;
            }

            impactReservationPayload = new MovementImpactReservationPayload(
                impactSourceEntity.entityId,
                impactSourceEntity.entityId,
                impactSourceEntity.position,
                group.ImpactTargetId,
                impactCell,
                ResolveImpactDamageAmount(group),
                sequence: 0,
                contingentDestinationCell: geometry.ImpactCell,
                contingentSourceCell: impactSourceEntity.position,
                contingentFacing: geometry.MoveFacing,
                hasContingentStateChange: !geometry.IsFlipImpact,
                contingentState: EntityPhaseState.Sliding,
                contingentStateTimer: !geometry.IsFlipImpact ? _slidingStateTimerTicks : 0,
                hasSourceFacing: geometry.HasSourceFacing,
                sourceFacingEntityId: geometry.HasSourceFacing ? group.SourceId : 0,
                sourceFacing: geometry.SourceFacing,
                dispositionPolicyKind: geometry.IsFlipImpact
                    ? ImpactDispositionPolicyKind.Flip
                    : ImpactDispositionPolicyKind.PushLike,
                contingentSemanticKind: geometry.IsFlipImpact
                    ? ResolvedActionSemanticKind.Flip
                    : ResolveImpactContingentSemanticKind(impactSourceEntity, group.SourceId));
            rejectedReason = string.Empty;
            return true;
        }

        private static string BuildImpactReservationRejectedReason(
            ActionGroup group,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            ImpactGeometryRejectReason rejectReason)
        {
            return
                $"ImpactReservationRejected|Stage=Plan|G={group.GroupId}|I={group.IntentId}|Source={group.ImpactSourceId}|Target={group.ImpactTargetId}|Reason={rejectReason}|SourceCell={FormatCell(sourceCell)}|ImpactCell={FormatCell(impactCell)}";
        }

        private static ResolvedActionSemanticKind ResolveImpactContingentSemanticKind(
            EntityState impactSourceEntity,
            int attackSourceEntityId)
        {
            return impactSourceEntity.type == EntityType.Box &&
                   (impactSourceEntity.boxCapabilities & BoxCapabilities.Push) == BoxCapabilities.Push &&
                   (impactSourceEntity.entityId != attackSourceEntityId ||
                    impactSourceEntity.state == EntityPhaseState.Sliding)
                ? ResolvedActionSemanticKind.Slide
                : ResolvedActionSemanticKind.Push;
        }

        private static SurfaceCell FindImpactCell(WorldSnapshot snapshot, ActionGroup group)
        {
            if (group.HasResolvedImpact &&
                snapshot.TryGetEntity(group.ImpactTargetId, out var target))
            {
                return target.position;
            }

            return default;
        }

        private static int ResolveImpactDamageAmount(ActionGroup group)
        {
            return group.HasResolvedImpact ? 1 : 0;
        }

        private void ResolvePlanEnemyPhaseRelocations(
            WorldSnapshot snapshot,
            int tickIndex,
            List<PhaseRelocationPlan> phaseRelocationPlans,
            List<Contest> phaseRelocationSpaceContests,
            ref int nextContestId)
        {
            var phasedEntries = new List<PhasedSnapshotEntry>();
            snapshot.EnumeratePhasedStatesOrdered(phasedEntries);

            for (var i = 0; i < phasedEntries.Count; i++)
            {
                var phasedEntry = phasedEntries[i];
                if (phasedEntry.State.ownerKind != PhasedRuntimeStateOwnerKind.EnemyPreMovement ||
                    !snapshot.TryGetEntity(phasedEntry.EntityId, out var source) ||
                    !snapshot.TryGetEnemyActionState(phasedEntry.EntityId, out var actionState) ||
                    !actionState.IsActive ||
                    !EnemyActionQueries.CanExecute(actionState, tickIndex) ||
                    !snapshot.TryGetEntity(actionState.lockedTargetEntityId, out var lockedTarget) ||
                    !EnemyPhaseThroughLockedTargetQueries.TryResolveCurrentTerminalCell(
                        source,
                        lockedTarget,
                        actionState.direction,
                        out var destinationCell))
                {
                    continue;
                }

                var actionPlanId = _idAllocator.AllocateGroupId();
                var contestId = nextContestId++;
                phaseRelocationPlans.Add(
                    new PhaseRelocationPlan(
                        actionPlanId,
                        contestId,
                        phasedEntry.EntityId,
                        priority: 0,
                        actionState.lockedTargetEntityId,
                        actionState.direction,
                        destinationCell,
                        EnemyPhaseThroughLockedTargetQueries.RuleLabel));
                phaseRelocationSpaceContests.Add(
                    new Contest(
                        contestId,
                        ContestKind.Space,
                        actionPlanId,
                        phasedEntry.EntityId,
                        priority: 0,
                        affectedEntityId: 0,
                        affectedCell: destinationCell,
                        hasAffectedCell: true,
                        localActionIndex: 0));
            }
        }

        private void ResolvePlanJumpLandings(
            ProjectedWorld projectedWorld,
            int tickIndex,
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            FinalizationBatch planFinalizationBatch,
            List<string> preMovementUpdates,
            List<JumpLandingPlan> jumpLandingPlans,
            List<Contest> jumpLandingSpaceContests,
            List<string> jumpLandingEvents,
            ref int nextContestId)
        {
            var snapshot = projectedWorld.CreateSnapshot();
            var jumpEntries = new List<EnemyJumpSnapshotEntry>();
            snapshot.EnumerateEnemyJumpStatesOrdered(jumpEntries);

            for (var i = 0; i < jumpEntries.Count; i++)
            {
                var jumpEntry = jumpEntries[i];
                var jumpState = jumpEntry.State;
                if (jumpState.phase != EnemyJumpPhase.Airborne ||
                    tickIndex < jumpState.landingTick ||
                    !snapshot.TryGetEntity(jumpEntry.EntityId, out var source) ||
                    !TryResolveJumpCooldownTicks(movementLogics, jumpEntry.EntityId, out var cooldownTicks))
                {
                    continue;
                }

                var crushEvaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                    new SettlementContext(
                        snapshot,
                        BuildLegalityActorRef(snapshot, jumpEntry.EntityId, EntityType.Unit),
                        jumpState.lockedTargetCell,
                        snapshot.Topology,
                        SpatialState.Anchored),
                    new JumpLandingEvidence(
                        snapshot,
                        jumpState.lockedTargetCell));
                if (crushEvaluation.LegalityResult.Verdict == LegalityVerdict.Allowed &&
                    crushEvaluation.CrushedBoxEntityId > 0)
                {
                    var actionPlanId = _idAllocator.AllocateGroupId();
                    var contestId = nextContestId++;
                    var successState = EnemyJumpQueries.EnterCooldown(jumpState, cooldownTicks);
                    var retryState = EnemyJumpQueries.ScheduleRetry(jumpState, tickIndex + 1);
                    jumpLandingPlans.Add(
                        new JumpLandingPlan(
                            actionPlanId,
                            contestId,
                            jumpEntry.EntityId,
                            priority: 0,
                            targetId: 0,
                            destinationCell: jumpState.lockedTargetCell,
                            landingRule: "TargetCrushBox",
                            successState,
                            retryState,
                            JumpLandingKind.CrushBoxAndLand));
                    jumpLandingSpaceContests.Add(
                        new Contest(
                            contestId,
                            ContestKind.Space,
                            actionPlanId,
                            jumpEntry.EntityId,
                            priority: 0,
                            affectedEntityId: 0,
                            affectedCell: jumpState.lockedTargetCell,
                            hasAffectedCell: true,
                            localActionIndex: 0));
                    continue;
                }

                if (!EnemyJumpQueries.TryResolveLandingCell(snapshot, source, jumpState, out var landingCell, out var landingRule))
                {
                    var retryState = EnemyJumpQueries.ScheduleRetry(jumpState, tickIndex + 1);
                    var actionPlanId = _idAllocator.AllocateGroupId();
                    var contestId = nextContestId++;
                    jumpLandingPlans.Add(
                        new JumpLandingPlan(
                            actionPlanId,
                            contestId,
                            jumpEntry.EntityId,
                            priority: 0,
                            targetId: 0,
                            destinationCell: source.position,
                            landingRule: "RetryOnly",
                            successState: default,
                            retryState,
                            JumpLandingKind.RetryOnly));
                    jumpLandingSpaceContests.Add(
                        new Contest(
                            contestId,
                            ContestKind.Space,
                            actionPlanId,
                            jumpEntry.EntityId,
                            priority: 0,
                            affectedEntityId: jumpEntry.EntityId,
                            affectedCell: source.position,
                            hasAffectedCell: true,
                            localActionIndex: 0));
                    continue;
                }

                var occupants = new List<EntityState>();
                snapshot.EnumerateUnitsAt(landingCell, occupants);
                var isExactLockedPlayerStack = landingCell == jumpState.lockedTargetCell &&
                                               IsExclusiveLockedPlayerStack(snapshot, landingCell, jumpEntry.EntityId);
                if (TryResolveContestedJumpLandingTarget(occupants, jumpEntry.EntityId, out var impactTargetId) &&
                    !isExactLockedPlayerStack)
                {
                    var actionPlanId = _idAllocator.AllocateGroupId();
                    var contestId = nextContestId++;
                    var successState = EnemyJumpQueries.EnterCooldown(jumpState, cooldownTicks);
                    var retryState = EnemyJumpQueries.ScheduleRetry(jumpState, tickIndex + 1);
                    jumpLandingPlans.Add(
                        new JumpLandingPlan(
                            actionPlanId,
                            contestId,
                            jumpEntry.EntityId,
                            priority: 0,
                            impactTargetId,
                            landingCell,
                            landingRule,
                            successState,
                            retryState,
                            JumpLandingKind.Contested));
                    jumpLandingSpaceContests.Add(
                        new Contest(
                            contestId,
                            ContestKind.Space,
                            actionPlanId,
                            jumpEntry.EntityId,
                            priority: 0,
                            impactTargetId,
                            landingCell,
                            hasAffectedCell: true,
                            localActionIndex: 0));
                    continue;
                }

                var openLandingActionPlanId = _idAllocator.AllocateGroupId();
                var openLandingContestId = nextContestId++;
                var landedState = EnemyJumpQueries.EnterCooldown(jumpState, cooldownTicks);
                jumpLandingPlans.Add(
                    new JumpLandingPlan(
                        openLandingActionPlanId,
                        openLandingContestId,
                        jumpEntry.EntityId,
                        priority: 0,
                        targetId: 0,
                        destinationCell: landingCell,
                        landingRule,
                        landedState,
                        EnemyJumpQueries.ScheduleRetry(jumpState, tickIndex + 1),
                        isExactLockedPlayerStack ? JumpLandingKind.ExactStack : JumpLandingKind.Contested));
                jumpLandingSpaceContests.Add(
                    new Contest(
                        openLandingContestId,
                        ContestKind.Space,
                        openLandingActionPlanId,
                        jumpEntry.EntityId,
                        priority: 0,
                        affectedEntityId: isExactLockedPlayerStack ? jumpEntry.EntityId : 0,
                        affectedCell: landingCell,
                        hasAffectedCell: true,
                        localActionIndex: 0));
            }
        }

        private bool TryResolveJumpCooldownTicks(
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            int entityId,
            out int cooldownTicks)
        {
            for (var i = 0; i < movementLogics.Count; i++)
            {
                if (movementLogics[i] is IEnemyJumpTimingBinding jumpTimingBinding &&
                    jumpTimingBinding.ControlledEntityId == entityId &&
                    jumpTimingBinding.TryGetJumpCooldownTicks(out cooldownTicks))
                {
                    return true;
                }
            }

            cooldownTicks = 0;
            return false;
        }

        internal static bool IsExclusiveLockedPlayerStack(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            int sourceEntityId)
        {
            // Exact stack is a gameplay-visible, live, exclusive player stack on the locked cell.
            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, occupants);
            var sawLockedPlayer = false;

            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (!ShouldCountJumpLandingOccupant(occupant, sourceEntityId))
                {
                    continue;
                }

                if (!snapshot.TryGetPlayerControlState(occupant.entityId, out _))
                {
                    return false;
                }

                if (sawLockedPlayer)
                {
                    return false;
                }

                sawLockedPlayer = true;
            }

            return sawLockedPlayer;
        }

        private static bool ShouldCountJumpLandingOccupant(
            in EntityState occupant,
            int sourceEntityId)
        {
            return occupant.entityId != sourceEntityId &&
                   occupant.boardPresence == EntityBoardPresence.Occupying &&
                   occupant.hp > 0 &&
                   !occupant.markedForDeath;
        }

        private static bool TryResolveContestedJumpLandingTarget(
            IReadOnlyList<EntityState> occupants,
            int sourceEntityId,
            out int targetId)
        {
            for (var i = 0; i < occupants.Count; i++)
            {
                if (!ShouldCountJumpLandingOccupant(occupants[i], sourceEntityId))
                {
                    continue;
                }

                targetId = occupants[i].entityId;
                return true;
            }

            targetId = 0;
            return false;
        }

        private FinalizationBatch ResolveJumpLandingSpaceContestsCanonical(
            WorldSnapshot movementSnapshot,
            WorldSnapshot damageProjectionSnapshot,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, JumpLandingActionPlanPayload> jumpLandingActionPlanPayloads,
            IReadOnlyList<Contest> jumpLandingSpaceContests,
            MovementReservationBook reservationBook,
            List<ResolutionRecord> movementResolutionRecords,
            List<string> movementCommitEvents)
        {
            var batch = new FinalizationBatch();
            var contestsByActionPlanId = BuildContestLookup(jumpLandingSpaceContests);

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!jumpLandingActionPlanPayloads.TryGetValue(actionPlanId, out var payload) ||
                    !contestsByActionPlanId.TryGetValue(actionPlanId, out var contest))
                {
                    continue;
                }

                var reservationStatus = reservationBook.GetCellStatus(payload.DestinationCell);
                var settlementContext = new SettlementContext(
                    movementSnapshot,
                    BuildLegalityActorRef(movementSnapshot, payload.SourceActorEntityId, EntityType.Unit),
                    payload.DestinationCell,
                    movementSnapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus);
                var jumpLandingEvidence = new JumpLandingEvidence(
                    damageProjectionSnapshot,
                    payload.SuccessJumpState.lockedTargetCell);
                var resolvedCrushedBoxEntityId = 0;
                var landingLegality = default(LegalityResult);
                if (payload.LandingKind == JumpLandingKind.CrushBoxAndLand)
                {
                    var crushEvaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                        settlementContext,
                        jumpLandingEvidence);
                    landingLegality = crushEvaluation.LegalityResult;
                    resolvedCrushedBoxEntityId = crushEvaluation.CrushedBoxEntityId;
                }
                else
                {
                    landingLegality = RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                        settlementContext,
                        jumpLandingEvidence);
                }

                var accepted = payload.LandingKind != JumpLandingKind.RetryOnly &&
                               landingLegality.Verdict == LegalityVerdict.Allowed;
                var resolvedTargetId = accepted &&
                                       payload.LandingKind == JumpLandingKind.CrushBoxAndLand
                    ? resolvedCrushedBoxEntityId
                    : accepted &&
                      payload.LandingKind != JumpLandingKind.ExactStack &&
                      payload.ContestedTargetEntityId > 0 &&
                      IsImpactTargetSurviving(
                          damageProjectionSnapshot,
                          payload.ContestedTargetEntityId)
                        ? payload.ContestedTargetEntityId
                        : 0;

                var resolutionRecord = CreateResolutionRecord(contest, accepted);
                movementResolutionRecords.Add(resolutionRecord);
                var jumpPresentationKind = accepted
                    ? payload.LandingKind == JumpLandingKind.CrushBoxAndLand && resolvedCrushedBoxEntityId > 0
                        ? JumpPresentationKind.CrushedBoxAndLanded
                        : JumpPresentationKind.LandingSuccess
                    : JumpPresentationKind.LandingRetry;
                var metadata = CreateJumpLandingMetadata(
                    payload,
                    resolutionRecord,
                    jumpPresentationKind);

                if (accepted)
                {
                    reservationBook.ReserveJumpLanding(payload.SourceActorEntityId, payload.DestinationCell);
                    if (payload.LandingKind == JumpLandingKind.CrushBoxAndLand &&
                        resolvedCrushedBoxEntityId > 0)
                    {
                        var boxDestroyMetadata = CreateJumpLandingMetadata(
                            payload,
                            resolutionRecord,
                            JumpPresentationKind.None,
                            TickEntityExitCause.BoxDestroy);
                        batch.SetBoardPresence(resolvedCrushedBoxEntityId, EntityBoardPresence.Detached, boxDestroyMetadata);
                        batch.MarkDestroy(resolvedCrushedBoxEntityId, boxDestroyMetadata);
                    }

                    batch.MoveEntity(payload.SourceActorEntityId, payload.DestinationCell, metadata);
                    batch.SetBoardPresence(payload.SourceActorEntityId, EntityBoardPresence.Occupying, metadata);
                    batch.SetEnemyJumpState(payload.SourceActorEntityId, payload.SuccessJumpState, metadata);
                    movementCommitEvents.Add(
                        BuildJumpLandingUpdate(
                            payload.SourceActorEntityId,
                            "Landing",
                            payload.SuccessJumpState,
                            $"Cell={payload.DestinationCell}|Rule={payload.LandingRule}|Target={resolvedTargetId}"));
                }
                else
                {
                    batch.SetEnemyJumpState(payload.SourceActorEntityId, payload.RetryJumpState, metadata);
                    movementCommitEvents.Add(
                        BuildJumpLandingUpdate(
                            payload.SourceActorEntityId,
                            "Retry",
                            payload.RetryJumpState,
                            $"Rule={payload.LandingRule}|Target={resolvedTargetId}|Reason=ResolveRejected"));
                }
            }

            return batch;
        }

        private FinalizationBatch ResolveEnemyPhaseRelocationSpaceContestsCanonical(
            WorldSnapshot movementSnapshot,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, PhaseRelocationActionPlanPayload> phaseRelocationActionPlanPayloads,
            IReadOnlyList<Contest> phaseRelocationSpaceContests,
            MovementReservationBook reservationBook,
            List<ResolutionRecord> movementResolutionRecords,
            List<string> movementCommitEvents)
        {
            var batch = new FinalizationBatch();
            var contestsByActionPlanId = BuildContestLookup(phaseRelocationSpaceContests);

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!phaseRelocationActionPlanPayloads.TryGetValue(actionPlanId, out var payload) ||
                    !contestsByActionPlanId.TryGetValue(actionPlanId, out var contest))
                {
                    continue;
                }

                var accepted = false;
                var rejectionReason = "ResolveRejected";
                var reservationStatus = ReservationStatus.None;

                if (!movementSnapshot.TryGetPhasedState(payload.SourceActorEntityId, out var phasedState) ||
                    !phasedState.IsActive ||
                    phasedState.ownerKind != PhasedRuntimeStateOwnerKind.EnemyPreMovement)
                {
                    rejectionReason = "OwnerInactive";
                }
                else if (!movementSnapshot.TryGetEntity(payload.SourceActorEntityId, out var source) ||
                         !movementSnapshot.TryGetEntity(payload.LockedTargetEntityId, out var lockedTarget))
                {
                    rejectionReason = "SourceOrTargetMissing";
                }
                else if (!EnemyPhaseThroughLockedTargetQueries.TryResolveCurrentTerminalCell(
                             source,
                             lockedTarget,
                             payload.Direction,
                             out var currentDestination) ||
                         currentDestination != payload.DestinationCell)
                {
                    rejectionReason = "GeometryChanged";
                }
                else
                {
                    var actor = BuildLegalityActorRef(movementSnapshot, payload.SourceActorEntityId, EntityType.Unit);
                    var traverseToLockedTarget = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                        new TraverseContext(
                            movementSnapshot,
                            actor,
                            source.position,
                            lockedTarget.position,
                            movementSnapshot.Topology,
                            TransitionRequirement.None));
                    if (traverseToLockedTarget.Verdict != LegalityVerdict.Allowed)
                    {
                        rejectionReason = "TraverseLockedTargetBlocked";
                    }
                    else
                    {
                        var traverseToDestination = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                            new TraverseContext(
                                movementSnapshot,
                                actor,
                                lockedTarget.position,
                                payload.DestinationCell,
                                movementSnapshot.Topology,
                                TransitionRequirement.None));
                        if (traverseToDestination.Verdict != LegalityVerdict.Allowed)
                        {
                            rejectionReason = "TraverseTerminalBlocked";
                        }
                        else
                        {
                            reservationStatus = ReadPhaseRelocationTerminalReservationStatus(
                                reservationBook,
                                payload.DestinationCell);
                            var landingLegality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                                new SettlementContext(
                                    movementSnapshot,
                                    actor,
                                    payload.DestinationCell,
                                    movementSnapshot.Topology,
                                    SpatialState.Phased,
                                    reservationStatus));
                            accepted = landingLegality.Verdict == LegalityVerdict.Allowed;
                            if (!accepted)
                            {
                                rejectionReason = "SettleBlocked";
                            }
                        }
                    }
                }

                var resolutionRecord = CreateResolutionRecord(contest, accepted);
                movementResolutionRecords.Add(resolutionRecord);
                if (!accepted)
                {
                    movementCommitEvents.Add(
                        BuildPhaseRelocationUpdate(
                            payload.SourceActorEntityId,
                            "Rejected",
                            payload,
                            reservationStatus,
                            rejectionReason));
                    continue;
                }

                var metadata = CreatePhaseRelocationMetadata(payload, resolutionRecord);
                reservationBook.ReservePhaseRelocation(payload.SourceActorEntityId, payload.DestinationCell);
                batch.MoveEntity(payload.SourceActorEntityId, payload.DestinationCell, metadata);
                movementCommitEvents.Add(
                    BuildPhaseRelocationUpdate(
                        payload.SourceActorEntityId,
                        "Committed",
                        payload,
                        reservationStatus,
                        "Allowed"));
            }

            return batch;
        }

        private static ReservationStatus ReadPhaseRelocationTerminalReservationStatus(
            MovementReservationBook reservationBook,
            SurfaceCell terminalCell)
        {
            // Semantic contract: read exactly one fixed terminal cell and do not inspect
            // edge/entity/topology reservation detail from this validator consumer.
            return reservationBook.GetCellStatus(terminalCell);
        }

        private static Contest TryFindJumpLandingContest(
            IReadOnlyList<Contest> jumpLandingSpaceContests,
            int contestId)
        {
            for (var i = 0; i < jumpLandingSpaceContests.Count; i++)
            {
                if (jumpLandingSpaceContests[i].ContestId == contestId)
                {
                    return jumpLandingSpaceContests[i];
                }
            }

            throw new InvalidOperationException($"Missing jump landing contest {contestId}.");
        }

        private static Dictionary<int, Contest> BuildContestLookup(IReadOnlyList<Contest> contests, int localActionIndex = 0)
        {
            var lookup = new Dictionary<int, Contest>(contests.Count);
            for (var i = 0; i < contests.Count; i++)
            {
                if (contests[i].LocalActionIndex == localActionIndex)
                {
                    lookup[contests[i].ActionPlanId] = contests[i];
                }
            }

            return lookup;
        }

        private static bool HasImpactDispositionRematerialization(
            IReadOnlyList<ImpactDispositionResolutionRecord> impactDispositionRecords)
        {
            for (var i = 0; i < impactDispositionRecords.Count; i++)
            {
                var disposition = impactDispositionRecords[i].DispositionKind;
                if (disposition == ImpactDispositionKind.FollowThrough ||
                    disposition == ImpactDispositionKind.DestroySelf)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAcceptedImpactDestroy(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int sourceEntityId,
            int targetEntityId)
        {
            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].Accepted &&
                    destroyResolutions[i].SourceId == sourceEntityId &&
                    destroyResolutions[i].TargetId == targetEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static MovementSemanticKind ResolveMovementPresentationSemanticKind(ResolvedActionSemanticKind semanticKind)
        {
            return semanticKind switch
            {
                ResolvedActionSemanticKind.Move => MovementSemanticKind.Move,
                ResolvedActionSemanticKind.Push => MovementSemanticKind.Push,
                ResolvedActionSemanticKind.Flip => MovementSemanticKind.Flip,
                ResolvedActionSemanticKind.Slide => MovementSemanticKind.Slide,
                ResolvedActionSemanticKind.Impact => MovementSemanticKind.Impact,
                ResolvedActionSemanticKind.JumpLanding => MovementSemanticKind.JumpLanding,
                ResolvedActionSemanticKind.ProjectileMove => MovementSemanticKind.ProjectileMove,
                ResolvedActionSemanticKind.Item => MovementSemanticKind.Item,
                ResolvedActionSemanticKind.Stop => MovementSemanticKind.Stop,
                _ => MovementSemanticKind.None,
            };
        }

        private static DamageSourceType ResolveDamageSourceType(AttackSourceKind attackSourceKind)
        {
            return attackSourceKind switch
            {
                AttackSourceKind.Combat => DamageSourceType.Attack,
                AttackSourceKind.DelayedEffect => DamageSourceType.Attack,
                AttackSourceKind.ImpactReservation => DamageSourceType.Impact,
                AttackSourceKind.PassiveContact => DamageSourceType.Environmental,
                _ => DamageSourceType.None,
            };
        }

        private static FinalizationOperationMetadata CreateMovementMetadata(
            MovementActionPlanPayload payload,
            ResolutionRecord resolutionRecord,
            int localActionIndex,
            ResolvedActionSemanticKind? semanticKindOverride = null,
            CubeRotationKind rotationKind = CubeRotationKind.None,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None)
        {
            var resolvedSemanticKind = semanticKindOverride ?? payload.SemanticKind;
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                resolvedSemanticKind,
                payload.SourceActorEntityId,
                payload.ActionPlanId,
                payload.IntentId,
                resolutionRecord.ContestId,
                localActionIndex,
                payload.Priority,
                rotationKind,
                exitCauseHint,
                movementSemanticKind: ResolveMovementPresentationSemanticKind(resolvedSemanticKind),
                damageSourceType: DamageSourceType.None);
        }

        private static FinalizationOperationMetadata CreateAttackMetadata(
            AttackActionPlanPayload payload,
            ResolutionRecord resolutionRecord,
            int localActionIndex,
            AttackSourceKind attackSourceKind = AttackSourceKind.Combat,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                payload.SemanticKind,
                payload.SourceActorEntityId,
                payload.ActionPlanId,
                payload.IntentId,
                resolutionRecord.ContestId,
                localActionIndex,
                payload.Priority,
                attackSourceKind: attackSourceKind,
                exitCauseHint: exitCauseHint,
                movementSemanticKind: MovementSemanticKind.None,
                damageSourceType: ResolveDamageSourceType(attackSourceKind));
        }

        private static FinalizationOperationMetadata CreateJumpLandingMetadata(
            JumpLandingActionPlanPayload payload,
            ResolutionRecord resolutionRecord,
            JumpPresentationKind jumpPresentationKind,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.JumpLanding,
                payload.SourceActorEntityId,
                payload.ActionPlanId,
                payload.IntentId,
                resolutionRecord.ContestId,
                resolutionRecord.LocalActionIndex,
                payload.Priority,
                exitCauseHint: exitCauseHint,
                movementSemanticKind: MovementSemanticKind.JumpLanding,
                damageSourceType: DamageSourceType.None,
                jumpPresentationKind: jumpPresentationKind,
                presentationTargetCell: payload.DestinationCell);
        }

        private static FinalizationOperationMetadata CreatePhaseRelocationMetadata(
            PhaseRelocationActionPlanPayload payload,
            ResolutionRecord resolutionRecord)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Move,
                payload.SourceActorEntityId,
                payload.ActionPlanId,
                payload.IntentId,
                resolutionRecord.ContestId,
                resolutionRecord.LocalActionIndex,
                payload.Priority,
                movementSemanticKind: MovementSemanticKind.Move,
                damageSourceType: DamageSourceType.None,
                presentationTargetCell: payload.DestinationCell);
        }

        private static void AddOperationsBySemanticKind(
            IReadOnlyList<FinalizationOperation> source,
            ResolvedActionSemanticKind semanticKind,
            List<FinalizationOperation> destination)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i].Metadata.SemanticKind == semanticKind)
                {
                    destination.Add(source[i]);
                }
            }
        }

        private FinalizationBatch MaterializeMovementOperations(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            IReadOnlyList<ImpactDispositionResolutionRecord> impactDispositionRecords,
            IReadOnlyList<ImpactReservation> impactReservations,
            List<string> commitEvents)
        {
            var batch = new FinalizationBatch();
            var impactReservationsByActionPlanId = new Dictionary<int, ImpactReservation>(impactReservations.Count);
            var impactDispositionByActionPlanId = new Dictionary<int, ImpactDispositionResolutionRecord>(impactDispositionRecords.Count);
            for (var i = 0; i < impactReservations.Count; i++)
            {
                impactReservationsByActionPlanId[impactReservations[i].SourceActionPlanId] = impactReservations[i];
            }

            for (var i = 0; i < impactDispositionRecords.Count; i++)
            {
                impactDispositionByActionPlanId[impactDispositionRecords[i].ActionPlanId] = impactDispositionRecords[i];
            }

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!payloads.TryGetValue(actionPlanId, out var payload) ||
                    !TryFindResolutionRecord(resolutionRecords, ContestKind.Space, actionPlanId, 0, out var baseResolution) ||
                    !baseResolution.Accepted)
                {
                    continue;
                }

                if (impactReservationsByActionPlanId.TryGetValue(actionPlanId, out var impactReservation))
                {
                    commitEvents.Add(
                        $"ImpactReservationCreated|G={actionPlanId}|I={payload.IntentId}|Source={impactReservation.SourceId}|Target={impactReservation.TargetId}|At={FormatCell(impactReservation.ImpactCell)}|Damage={impactReservation.Damage}|Sequence={impactReservation.LocalActionIndex}");
                }

                var hasContingentResolution = TryFindResolutionRecord(resolutionRecords, ContestKind.Space, actionPlanId, 1, out var contingentResolution) &&
                                             contingentResolution.Accepted;
                var hasImpactDisposition = impactDispositionByActionPlanId.TryGetValue(actionPlanId, out var impactDisposition);
                var hasImpactFollowThrough = hasImpactDisposition &&
                                             payload.HasImpactReservationPayload &&
                                             impactDisposition.DispositionKind == ImpactDispositionKind.FollowThrough;
                var hasDestroySelfDisposition = hasImpactDisposition &&
                                               payload.HasImpactReservationPayload &&
                                               impactDisposition.DispositionKind == ImpactDispositionKind.DestroySelf;

                if (hasImpactFollowThrough &&
                    payload.ImpactReservationPayload.HasSourceFacing)
                {
                    var contingentMetadata = CreateMovementMetadata(
                        payload,
                        contingentResolution,
                        localActionIndex: 1,
                        semanticKindOverride: payload.ImpactReservationPayload.ContingentSemanticKind);
                    batch.SetFacing(
                        payload.ImpactReservationPayload.SourceFacingEntityId,
                        payload.ImpactReservationPayload.SourceFacing,
                        contingentMetadata);
                    commitEvents.Add(
                        $"FacingCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.SourceFacingEntityId}|Facing={payload.ImpactReservationPayload.SourceFacing}");
                }

                if (!hasImpactFollowThrough)
                {
                    for (var stateIndex = 0; stateIndex < payload.StateChangeWrites.Count; stateIndex++)
                    {
                        var stateChangeWrite = payload.StateChangeWrites[stateIndex];
                        batch.ApplyStateChange(
                            stateChangeWrite.EntityId,
                            stateChangeWrite.State,
                            stateChangeWrite.StateTimer,
                            CreateMovementMetadata(payload, baseResolution, stateIndex));
                        commitEvents.Add(
                            $"StateChanged|G={actionPlanId}|I={payload.IntentId}|E={stateChangeWrite.EntityId}|State={stateChangeWrite.State}|Timer={stateChangeWrite.StateTimer}");
                    }
                }

                for (var presenceIndex = 0; presenceIndex < payload.BoardPresenceWrites.Count; presenceIndex++)
                {
                    var boardPresenceWrite = payload.BoardPresenceWrites[presenceIndex];
                    batch.SetBoardPresence(
                        boardPresenceWrite.EntityId,
                        boardPresenceWrite.BoardPresence,
                        CreateMovementMetadata(payload, baseResolution, presenceIndex, exitCauseHint: boardPresenceWrite.ExitCauseHint));
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={actionPlanId}|I={payload.IntentId}|E={boardPresenceWrite.EntityId}|Presence={boardPresenceWrite.BoardPresence}");
                }

                for (var topologyIndex = 0; topologyIndex < payload.TopologyWrites.Count; topologyIndex++)
                {
                    var topologyWrite = payload.TopologyWrites[topologyIndex];
                    batch.SetTopology(
                        topologyWrite.Topology,
                        CreateMovementMetadata(payload, baseResolution, topologyIndex, rotationKind: topologyWrite.RotationKind));
                    commitEvents.Add(
                        $"TopologyCommitted|G={actionPlanId}|I={payload.IntentId}|Rotation={topologyWrite.RotationKind}|Bottom={topologyWrite.Topology.BottomFace}|Front={topologyWrite.Topology.FrontFace}");
                }

                for (var facingIndex = 0; facingIndex < payload.FacingWrites.Count; facingIndex++)
                {
                    var facingWrite = payload.FacingWrites[facingIndex];
                    batch.SetFacing(facingWrite.EntityId, facingWrite.Facing, CreateMovementMetadata(payload, baseResolution, facingIndex));
                    commitEvents.Add(
                        $"FacingCommitted|G={actionPlanId}|I={payload.IntentId}|E={facingWrite.EntityId}|Facing={facingWrite.Facing}");
                }

                if (hasImpactFollowThrough)
                {
                    var contingentMetadata = CreateMovementMetadata(
                        payload,
                        contingentResolution,
                        localActionIndex: 1,
                        semanticKindOverride: payload.ImpactReservationPayload.ContingentSemanticKind);
                    // Keep the local vacate ahead of MoveEntity so the replay sees a
                    // free destination cell without changing global cleanup semantics.
                    batch.SetBoardPresence(
                        payload.ImpactReservationPayload.TargetEntityId,
                        EntityBoardPresence.Detached,
                        contingentMetadata);
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.TargetEntityId}|Presence={EntityBoardPresence.Detached}");
                    batch.MoveEntity(
                        payload.ImpactReservationPayload.SourceEntityId,
                        payload.ImpactReservationPayload.ContingentDestinationCell,
                        contingentMetadata);
                    batch.SetFacing(
                        payload.ImpactReservationPayload.SourceEntityId,
                        payload.ImpactReservationPayload.ContingentFacing,
                        contingentMetadata);
                    commitEvents.Add(
                        $"MoveCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.SourceEntityId}|To={FormatCell(payload.ImpactReservationPayload.ContingentDestinationCell)}|Facing={payload.ImpactReservationPayload.ContingentFacing}");
                    if (payload.ImpactReservationPayload.HasContingentStateChange)
                    {
                        batch.ApplyStateChange(
                            payload.ImpactReservationPayload.SourceEntityId,
                            payload.ImpactReservationPayload.ContingentState,
                            payload.ImpactReservationPayload.ContingentStateTimer,
                            contingentMetadata);
                        commitEvents.Add(
                            $"StateChanged|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.SourceEntityId}|State={payload.ImpactReservationPayload.ContingentState}|Timer={payload.ImpactReservationPayload.ContingentStateTimer}");
                    }
                }
                else
                {
                    for (var moveIndex = 0; moveIndex < payload.MoveWrites.Count; moveIndex++)
                    {
                        var moveWrite = payload.MoveWrites[moveIndex];
                        batch.MoveEntity(moveWrite.EntityId, moveWrite.DestinationCell, CreateMovementMetadata(payload, baseResolution, moveIndex));
                        batch.SetFacing(moveWrite.EntityId, moveWrite.FacingAfterMove, CreateMovementMetadata(payload, baseResolution, moveIndex));
                        commitEvents.Add(
                            $"MoveCommitted|G={actionPlanId}|I={payload.IntentId}|E={moveWrite.EntityId}|To={FormatCell(moveWrite.DestinationCell)}|Facing={moveWrite.FacingAfterMove}");
                    }
                }

                for (var kinematicIndex = 0; kinematicIndex < payload.KinematicMotionOutcomes.Count; kinematicIndex++)
                {
                    var kinematicOutcome = payload.KinematicMotionOutcomes[kinematicIndex];
                    var kinematicMetadata = CreateMovementMetadata(payload, baseResolution, kinematicIndex);
                    if (kinematicOutcome.AnchorChanged)
                    {
                        batch.MoveEntity(
                            kinematicOutcome.EntityId,
                            kinematicOutcome.ResolvedAnchorCell,
                            kinematicMetadata);
                        commitEvents.Add(
                            $"KinematicAnchorCommitted|G={actionPlanId}|I={payload.IntentId}|E={kinematicOutcome.EntityId}|From={FormatCell(kinematicOutcome.SourceAnchorCell)}|To={FormatCell(kinematicOutcome.ResolvedAnchorCell)}");
                    }

                    batch.SetUnitKinematicState(
                        kinematicOutcome.EntityId,
                        kinematicOutcome.ResolvedState,
                        kinematicMetadata);
                    commitEvents.Add(
                        $"KinematicPoseCommitted|G={actionPlanId}|I={payload.IntentId}|E={kinematicOutcome.EntityId}|Anchor={FormatCell(kinematicOutcome.ResolvedAnchorCell)}|Offset={kinematicOutcome.ResolvedLocalOffset}|Mode={kinematicOutcome.ResolvedState.mode}|Blocked={(kinematicOutcome.Blocked ? 1 : 0)}|RejectedBy={kinematicOutcome.RejectedBy}");
                }

                if (hasDestroySelfDisposition)
                {
                    var destroySelfMetadata = CreateMovementMetadata(
                        payload,
                        baseResolution,
                        localActionIndex: 1,
                        semanticKindOverride: payload.ImpactReservationPayload.ContingentSemanticKind,
                        exitCauseHint: TickEntityExitCause.DestroyedByImpact);
                    batch.SetBoardPresence(
                        payload.ImpactReservationPayload.SourceEntityId,
                        EntityBoardPresence.Detached,
                        destroySelfMetadata);
                    batch.MarkDestroy(
                        payload.ImpactReservationPayload.SourceEntityId,
                        destroySelfMetadata);
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.SourceEntityId}|Presence={EntityBoardPresence.Detached}");
                    commitEvents.Add(
                        $"DestroyMarked|G={actionPlanId}|I={payload.IntentId}|Target={payload.ImpactReservationPayload.SourceEntityId}|Reason=ImpactDestroySelf");
                }

                for (var kineticIndex = 0; kineticIndex < payload.BoxKineticOwnerWrites.Count; kineticIndex++)
                {
                    var boxKineticOwnerWrite = payload.BoxKineticOwnerWrites[kineticIndex];
                    batch.SetBoxKineticOwner(
                        boxKineticOwnerWrite.EntityId,
                        boxKineticOwnerWrite.InstigatorEntityId,
                        boxKineticOwnerWrite.InstigatorTeamId,
                        CreateMovementMetadata(payload, baseResolution, kineticIndex));
                }

                for (var executionIndex = 0; executionIndex < payload.ExecutionLockWrites.Count; executionIndex++)
                {
                    var executionLockWrite = payload.ExecutionLockWrites[executionIndex];
                    batch.SetEntityExecutionLockState(
                        executionLockWrite.EntityId,
                        executionLockWrite.ExecutionLockState,
                        CreateMovementMetadata(payload, baseResolution, executionIndex));
                }

                for (var locomotionIndex = 0; locomotionIndex < payload.EnemyLocomotionWrites.Count; locomotionIndex++)
                {
                    var locomotionWrite = payload.EnemyLocomotionWrites[locomotionIndex];
                    batch.SetEnemyLocomotionCooldown(
                        locomotionWrite.EntityId,
                        locomotionWrite.CooldownTicks,
                        CreateMovementMetadata(payload, baseResolution, locomotionIndex));
                }

                for (var patrolIndex = 0; patrolIndex < payload.EnemyPatrolWrites.Count; patrolIndex++)
                {
                    var enemyPatrolWrite = payload.EnemyPatrolWrites[patrolIndex];
                    batch.SetEnemyPatrolState(
                        enemyPatrolWrite.EntityId,
                        enemyPatrolWrite.EnemyPatrolState,
                        CreateMovementMetadata(payload, baseResolution, patrolIndex));
                    commitEvents.Add(
                        $"EnemyPatrolStateUpdated|G={actionPlanId}|I={payload.IntentId}|E={enemyPatrolWrite.EntityId}|Label=CommittedMove|Seq={enemyPatrolWrite.EnemyPatrolState.sequence}|Home={enemyPatrolWrite.EnemyPatrolState.homeCell}|LastDirection={enemyPatrolWrite.EnemyPatrolState.lastCommittedDirection}");
                }

                for (var controlIndex = 0; controlIndex < payload.PlayerControlWrites.Count; controlIndex++)
                {
                    var playerControlWrite = payload.PlayerControlWrites[controlIndex];
                    batch.SetPlayerControlState(
                        playerControlWrite.EntityId,
                        playerControlWrite.PlayerControlState,
                        CreateMovementMetadata(payload, baseResolution, controlIndex));
                }

                for (var destroyIndex = 0; destroyIndex < payload.DestroyWrites.Count; destroyIndex++)
                {
                    if (!TryFindResolutionRecord(resolutionRecords, ContestKind.Destroy, actionPlanId, destroyIndex, out var destroyResolution) ||
                        !destroyResolution.Accepted)
                    {
                        continue;
                    }

                    var destroyWrite = payload.DestroyWrites[destroyIndex];
                    batch.MarkDestroy(
                        destroyWrite.TargetEntityId,
                        CreateMovementMetadata(payload, destroyResolution, destroyIndex, exitCauseHint: destroyWrite.ExitCauseHint));
                    commitEvents.Add(
                        $"DestroyMarked|G={actionPlanId}|I={payload.IntentId}|Target={destroyWrite.TargetEntityId}|Condition={destroyWrite.DestroyCondition}");
                }
            }

            return batch;
        }

        private FinalizationBatch MaterializeAttackOperations(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            IReadOnlyList<DelayedAttackEffectRecord> delayedAttackEffects,
            List<string> commitEvents,
            List<string> delayedAttackEnqueueEvents)
        {
            var batch = new FinalizationBatch();
            commitEvents.Clear();
            delayedAttackEnqueueEvents.Clear();

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!payloads.TryGetValue(actionPlanId, out var payload) ||
                    !TryFindResolutionRecord(resolutionRecords, ContestKind.Plan, actionPlanId, 0, out var planResolution) ||
                    !planResolution.Accepted)
                {
                    continue;
                }

                for (var stateIndex = 0; stateIndex < payload.StateChangeWrites.Count; stateIndex++)
                {
                    var stateChangeWrite = payload.StateChangeWrites[stateIndex];
                    batch.ApplyStateChange(
                        stateChangeWrite.EntityId,
                        stateChangeWrite.State,
                        stateChangeWrite.StateTimer,
                        CreateAttackMetadata(payload, planResolution, stateIndex));
                    commitEvents.Add(
                        $"StateChanged|G={actionPlanId}|I={payload.IntentId}|E={stateChangeWrite.EntityId}|State={stateChangeWrite.State}|Timer={stateChangeWrite.StateTimer}");
                }

                for (var damageIndex = 0; damageIndex < payload.DamageWrites.Count; damageIndex++)
                {
                    var damageWrite = payload.DamageWrites[damageIndex];
                    var damageResolution = FindDamageResolution(damageResolutions, actionPlanId, damageWrite.TargetEntityId, damageIndex);
                    if (!TryFindResolutionRecord(resolutionRecords, ContestKind.Damage, actionPlanId, damageIndex, out var damageContestResolution) ||
                        !damageContestResolution.Accepted)
                    {
                        var sourceKindSuffix = BuildSourceKindSuffix(damageWrite.AttackSourceKind);
                        commitEvents.Add(
                            $"DamageRejected|G={actionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}{sourceKindSuffix}|Target={damageWrite.TargetEntityId}|Amount={damageWrite.Amount}|Reason={damageResolution.RejectReason}");
                        continue;
                    }

                    if (damageWrite.HasPlayerDamageState)
                    {
                        batch.SetPlayerDamageState(
                            damageWrite.TargetEntityId,
                            damageWrite.PlayerDamageState,
                            CreateAttackMetadata(payload, damageContestResolution, damageIndex, attackSourceKind: damageWrite.AttackSourceKind));
                    }

                    batch.ApplyDamage(
                        damageWrite.TargetEntityId,
                        damageWrite.Amount,
                        CreateAttackMetadata(payload, damageContestResolution, damageIndex, attackSourceKind: damageWrite.AttackSourceKind));
                    var committedSourceKindSuffix = BuildSourceKindSuffix(damageWrite.AttackSourceKind);
                    commitEvents.Add(
                        $"DamageCommitted|G={actionPlanId}|I={payload.IntentId}{committedSourceKindSuffix}|Target={damageWrite.TargetEntityId}|Amount={damageWrite.Amount}");
                }

                for (var spawnIndex = 0; spawnIndex < payload.SpawnWrites.Count; spawnIndex++)
                {
                    var finalizedSpawn = FinalizeSpawn(new SpawnAction(0, payload.SpawnWrites[spawnIndex].EntityTemplate));
                    batch.SpawnEntity(finalizedSpawn.Entity, CreateAttackMetadata(payload, planResolution, spawnIndex));
                    commitEvents.Add(
                        $"SpawnCommitted|G={actionPlanId}|I={payload.IntentId}|SpawnId={finalizedSpawn.SpawnId}|E={finalizedSpawn.Entity.entityId}|Pos=({finalizedSpawn.Entity.position.x},{finalizedSpawn.Entity.position.y})|Type={finalizedSpawn.Entity.type}|SpawnTick={finalizedSpawn.Entity.spawnTick}");
                }

                for (var destroyIndex = 0; destroyIndex < payload.DestroyWrites.Count; destroyIndex++)
                {
                    if (!TryFindResolutionRecord(resolutionRecords, ContestKind.Destroy, actionPlanId, destroyIndex, out var destroyResolution) ||
                        !destroyResolution.Accepted)
                    {
                        continue;
                    }

                    var destroyWrite = payload.DestroyWrites[destroyIndex];
                    batch.MarkDestroy(
                        destroyWrite.TargetEntityId,
                        CreateAttackMetadata(payload, destroyResolution, destroyIndex, exitCauseHint: destroyWrite.ExitCauseHint));
                    commitEvents.Add(
                        $"DestroyMarked|G={actionPlanId}|I={payload.IntentId}|Target={destroyWrite.TargetEntityId}|Condition={destroyWrite.DestroyCondition}");
                }

                for (var delayedIndex = 0; delayedIndex < payload.DelayedEnqueueWrites.Count; delayedIndex++)
                {
                    var delayedWrite = payload.DelayedEnqueueWrites[delayedIndex];
                    var effectRecord = new DelayedAttackEffectRecord(
                        payload.SourceActorEntityId,
                        delayedWrite.TargetEntityId,
                        delayedWrite.Damage,
                        payload.Priority,
                        delayedWrite.TickGenerated,
                        delayedWrite.ExecuteAtTick,
                        payload.ActionPlanId,
                        delayedWrite.EffectSequence);
                    batch.EnqueueDelayedAttackEffect(
                        effectRecord,
                        CreateAttackMetadata(payload, planResolution, delayedIndex, attackSourceKind: AttackSourceKind.DelayedEffect));
                    delayedAttackEnqueueEvents.Add(
                        $"DelayedAttackEnqueued|G={payload.ActionPlanId}|I={payload.IntentId}|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|ExecuteTick={effectRecord.ExecuteAtTick}|Sequence={effectRecord.EffectSequence}");
                }
            }

            return batch;
        }

        private static List<MotionInterruptRecord> MaterializePlayerKinematicMotionInterrupts(
            WorldSnapshot attackSnapshot,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            FinalizationBatch attackStageBatch,
            List<string> commitEvents)
        {
            if (attackSnapshot == null)
            {
                throw new ArgumentNullException(nameof(attackSnapshot));
            }

            if (damageResolutions == null)
            {
                throw new ArgumentNullException(nameof(damageResolutions));
            }

            if (destroyResolutions == null)
            {
                throw new ArgumentNullException(nameof(destroyResolutions));
            }

            if (attackStageBatch == null)
            {
                throw new ArgumentNullException(nameof(attackStageBatch));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            var interruptRecords = new List<MotionInterruptRecord>();
            var interruptedEntityIds = new HashSet<int>();
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                var damageResolution = damageResolutions[i];
                if (!damageResolution.Accepted)
                {
                    continue;
                }

                TryMaterializePlayerKinematicMotionInterrupt(
                    attackSnapshot,
                    attackStageBatch,
                    commitEvents,
                    interruptRecords,
                    interruptedEntityIds,
                    damageResolution.TargetId,
                    damageResolution.SourceId,
                    damageResolution.ActionPlanId,
                    damageResolution.LocalActionIndex,
                    damageResolution.SourceKind);
            }

            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                var destroyResolution = destroyResolutions[i];
                if (!destroyResolution.Accepted)
                {
                    continue;
                }

                TryMaterializePlayerKinematicMotionInterrupt(
                    attackSnapshot,
                    attackStageBatch,
                    commitEvents,
                    interruptRecords,
                    interruptedEntityIds,
                    destroyResolution.TargetId,
                    destroyResolution.SourceId,
                    destroyResolution.ActionPlanId,
                    destroyResolution.LocalActionIndex,
                    AttackSourceKind.Combat);
            }

            return interruptRecords;
        }

        private static bool TryMaterializePlayerKinematicMotionInterrupt(
            WorldSnapshot attackSnapshot,
            FinalizationBatch attackStageBatch,
            List<string> commitEvents,
            List<MotionInterruptRecord> interruptRecords,
            HashSet<int> interruptedEntityIds,
            int targetEntityId,
            int sourceEntityId,
            int actionPlanId,
            int localActionIndex,
            AttackSourceKind sourceKind)
        {
            if (interruptedEntityIds.Contains(targetEntityId) ||
                !attackSnapshot.TryGetPlayerControlState(targetEntityId, out _) ||
                !attackSnapshot.TryGetUnitKinematicPose(targetEntityId, out var pose) ||
                !pose.HasAuthoritativeState ||
                pose.IsSettledAtAnchor ||
                pose.Mode != MotionMode.Voluntary)
            {
                return false;
            }

            var interruptedState = UnitKinematicRuntimeState.CreateInterruptedFreeze(pose.State);
            interruptedEntityIds.Add(targetEntityId);
            attackStageBatch.SetUnitKinematicState(
                targetEntityId,
                interruptedState,
                new FinalizationOperationMetadata(
                    TickPhase.Resolve,
                    ResolvedActionSemanticKind.Attack,
                    sourceEntityId,
                    actionPlanId,
                    localActionIndex: localActionIndex,
                    attackSourceKind: sourceKind,
                    damageSourceType: ResolveDamageSourceType(sourceKind)));

            interruptRecords.Add(
                new MotionInterruptRecord(
                    targetEntityId,
                    MotionInterruptPolicy.FreezeCurrentPose,
                    sourceEntityId));
            commitEvents.Add(
                $"KinematicMotionInterrupted|E={targetEntityId}|Source={sourceEntityId}|SourceKind={sourceKind}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}|Mode={interruptedState.mode}");
            return true;
        }

        private static WorldSnapshot CreateCompositeDamageProjectionSnapshot(
            WorldSnapshot baseSnapshot,
            IReadOnlyList<ResolutionRecord> attackResolutionRecords,
            IReadOnlyDictionary<int, AttackActionPlanPayload> attackActionPlanPayloads)
        {
            var projectionBatch = new FinalizationBatch();

            for (var i = 0; i < attackResolutionRecords.Count; i++)
            {
                var resolutionRecord = attackResolutionRecords[i];
                if (!resolutionRecord.Accepted ||
                    !attackActionPlanPayloads.TryGetValue(resolutionRecord.ActionPlanId, out var payload))
                {
                    continue;
                }

                switch (resolutionRecord.Kind)
                {
                    case ContestKind.Damage:
                        if (resolutionRecord.LocalActionIndex < 0 ||
                            resolutionRecord.LocalActionIndex >= payload.DamageWrites.Count)
                        {
                            continue;
                        }

                        var damageWrite = payload.DamageWrites[resolutionRecord.LocalActionIndex];
                        if (damageWrite.HasPlayerDamageState)
                        {
                            projectionBatch.SetPlayerDamageState(damageWrite.TargetEntityId, damageWrite.PlayerDamageState);
                        }

                        projectionBatch.ApplyDamage(damageWrite.TargetEntityId, damageWrite.Amount);
                        break;

                    case ContestKind.Destroy:
                        if (resolutionRecord.LocalActionIndex < 0 ||
                            resolutionRecord.LocalActionIndex >= payload.DestroyWrites.Count)
                        {
                            continue;
                        }

                        projectionBatch.MarkDestroy(payload.DestroyWrites[resolutionRecord.LocalActionIndex].TargetEntityId);
                        break;
                }
            }

            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyBatch(projectionBatch);
            return projectedWorld.CreateSnapshot();
        }

        private static bool TryFindResolutionRecord(
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            ContestKind contestKind,
            int actionPlanId,
            int localActionIndex,
            out ResolutionRecord resolutionRecord)
        {
            for (var i = 0; i < resolutionRecords.Count; i++)
            {
                if (resolutionRecords[i].Kind == contestKind &&
                    resolutionRecords[i].ActionPlanId == actionPlanId &&
                    resolutionRecords[i].LocalActionIndex == localActionIndex)
                {
                    resolutionRecord = resolutionRecords[i];
                    return true;
                }
            }

            resolutionRecord = default;
            return false;
        }

        private static bool HasAcceptedResolution(
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            ContestKind contestKind,
            int actionPlanId,
            int localActionIndex)
        {
            return TryFindResolutionRecord(resolutionRecords, contestKind, actionPlanId, localActionIndex, out var resolutionRecord) &&
                   resolutionRecord.Accepted;
        }

        private void ResolveMovementActionPlansCanonical(
            WorldSnapshot snapshot,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<Contest> spaceContests,
            MovementReservationBook reservationBook,
            List<ResolutionRecord> resolutionRecords,
            List<string> rejectedReasons)
        {
            var selectedIntentIds = new HashSet<int>();
            var contestsByActionPlanId = BuildContestLookup(spaceContests);

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!payloads.TryGetValue(actionPlanId, out var payload) ||
                    !contestsByActionPlanId.TryGetValue(actionPlanId, out var contest))
                {
                    continue;
                }

                var accepted = false;
                if (selectedIntentIds.Contains(payload.IntentId))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Resolve|G={payload.ActionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}|Reason=IntentAlreadySelected");
                }
                else if (payload.MovementCandidateKind == MovementCandidateKind.BoxImpact ||
                         payload.MovementCandidateKind == MovementCandidateKind.ProjectileImpact)
                {
                    accepted = true;
                    selectedIntentIds.Add(payload.IntentId);
                }
                else
                {
                    if (reservationBook.TryAcceptPayload(payload, out var conflict))
                    {
                        accepted = true;
                        selectedIntentIds.Add(payload.IntentId);
                    }
                    else
                    {
                        switch (conflict.Kind)
                        {
                            case MovementReservationConflictKind.TopologyExclusive:
                                rejectedReasons.Add(
                                    $"MovementRejected|Stage=Resolve|G={payload.ActionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}|Reason=TopologyExclusive|BlockedBy={conflict.BlockingActionPlanId}|BlockingKind={conflict.BlockingCandidateKind}|BlockingTopologyChange={conflict.BlockingTopologyChange}");
                                break;

                            case MovementReservationConflictKind.Destination:
                                rejectedReasons.Add(
                                    $"MovementRejected|Stage=Resolve|G={payload.ActionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}|Reason=DestinationReserved|Cell={FormatCell(conflict.Destination)}");
                                break;

                            case MovementReservationConflictKind.Edge:
                                rejectedReasons.Add(
                                    $"MovementRejected|Stage=Resolve|G={payload.ActionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}|Reason=EdgeReserved|From={FormatCell(conflict.Edge.First)}|To={FormatCell(conflict.Edge.Second)}");
                                break;

                            case MovementReservationConflictKind.AffectedEntity:
                                rejectedReasons.Add(
                                    $"MovementRejected|Stage=Resolve|G={payload.ActionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}|Reason=SharedMovedEntity|Entity={conflict.EntityId}");
                                break;
                        }
                    }
                }

                resolutionRecords.Add(CreateResolutionRecord(contest, accepted));
            }
        }

        private List<ImpactReservation> ResolveMovementImpactReservationsCanonical(
            int tickIndex,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords)
        {
            var impactReservations = new List<ImpactReservation>();
            var reservationSequence = 1;

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Space, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload) ||
                    !payload.HasImpactReservationPayload)
                {
                    continue;
                }

                impactReservations.Add(
                    new ImpactReservation(
                        payload.ImpactReservationPayload.SourceEntityId,
                        payload.ImpactReservationPayload.TargetEntityId,
                        payload.ImpactReservationPayload.ImpactCell,
                        payload.ImpactReservationPayload.DamageAmount,
                        tickIndex,
                        payload.ActionPlanId,
                        reservationSequence++));
            }

            return impactReservations;
        }

        private static List<ImpactReservation> MergeImpactReservations(
            IReadOnlyList<ImpactReservation> existingReservations,
            IReadOnlyList<ImpactReservation> deferredReservations)
        {
            var merged = new List<ImpactReservation>(existingReservations.Count + deferredReservations.Count);
            AddRange(merged, existingReservations);
            AddRange(merged, deferredReservations);
            return merged;
        }

        private List<ImpactReservation> ResolveDeferredMovementImpactReservationsAgainstSnapshot(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords)
        {
            var impactReservations = new List<ImpactReservation>();
            var reservationSequence = 1;

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Space, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload) ||
                    !payload.HasDeferredImpactPayload ||
                    !TryResolveImpactReservationAgainstSnapshot(snapshot, payload, out var impactCell, out var damageAmount, out var targetEntityId))
                {
                    continue;
                }

                impactReservations.Add(
                    new ImpactReservation(
                        payload.DeferredImpactPayload.SourceEntityId,
                        targetEntityId,
                        impactCell,
                        damageAmount,
                        tickIndex,
                        payload.ActionPlanId,
                        reservationSequence++));
            }

            return impactReservations;
        }

        private static bool TryResolveImpactReservationAgainstSnapshot(
            WorldSnapshot snapshot,
            MovementActionPlanPayload payload,
            out SurfaceCell impactCell,
            out int damageAmount,
            out int targetEntityId)
        {
            impactCell = default;
            damageAmount = 0;
            targetEntityId = 0;

            var sourceEntityId = 0;
            if (payload.HasImpactReservationPayload)
            {
                sourceEntityId = payload.ImpactReservationPayload.SourceEntityId;
                impactCell = payload.ImpactReservationPayload.ImpactCell;
                damageAmount = payload.ImpactReservationPayload.DamageAmount;
            }
            else if (payload.HasDeferredImpactPayload)
            {
                sourceEntityId = payload.DeferredImpactPayload.SourceEntityId;
                impactCell = payload.DeferredImpactPayload.ImpactCell;
                damageAmount = payload.DeferredImpactPayload.DamageAmount;
            }
            else
            {
                return false;
            }

            if (!snapshot.TryGetEntity(sourceEntityId, out var impactSourceEntity))
            {
                return false;
            }

            var sourceTeamId = ResolveImpactReservationSourceTeamId(
                snapshot,
                payload.SourceActorEntityId,
                impactSourceEntity);
            var skipActiveGlideTargets =
                payload.HasDeferredImpactPayload ||
                (payload.HasImpactReservationPayload &&
                 payload.ImpactReservationPayload.DispositionPolicyKind == ImpactDispositionPolicyKind.PushLike);
            var hasTarget = skipActiveGlideTargets
                ? snapshot.TryPickHostileUnitImpactTargetAtForBoxSlide(impactCell, sourceTeamId, out var target)
                : snapshot.TryPickHostileUnitImpactTargetAt(impactCell, sourceTeamId, out target);
            if (sourceTeamId <= 0 ||
                !hasTarget)
            {
                return false;
            }

            targetEntityId = target.entityId;
            return true;
        }

        private static int ResolveImpactReservationSourceTeamId(
            WorldSnapshot snapshot,
            int sourceActorEntityId,
            in EntityState impactSourceEntity)
        {
            if (impactSourceEntity.kineticInstigatorTeamId > 0)
            {
                return impactSourceEntity.kineticInstigatorTeamId;
            }

            if (impactSourceEntity.teamId > 0)
            {
                return impactSourceEntity.teamId;
            }

            return snapshot.TryGetEntity(sourceActorEntityId, out var sourceActorEntity) &&
                   sourceActorEntity.type == EntityType.Unit &&
                   sourceActorEntity.teamId > 0
                ? sourceActorEntity.teamId
                : 0;
        }

        private static List<Contest> BuildImpactSpaceContestsCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            ref int nextContestId)
        {
            var contests = new List<Contest>();

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Space, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload) ||
                    !payload.HasImpactReservationPayload ||
                    payload.MovementCandidateKind != MovementCandidateKind.BoxImpact)
                {
                    continue;
                }

                contests.Add(
                    new Contest(
                        nextContestId++,
                        ContestKind.Space,
                        payload.ActionPlanId,
                        payload.SourceActorEntityId,
                        payload.Priority,
                        payload.ImpactReservationPayload.SourceEntityId,
                        payload.ImpactReservationPayload.ContingentDestinationCell,
                        hasAffectedCell: true,
                        localActionIndex: 1));
            }

            return contests;
        }

        private static List<Contest> BuildAttackPlanContestsCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            ref int nextContestId)
        {
            var contests = new List<Contest>(orderedActionPlanIds.Count);
            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                if (!payloads.TryGetValue(orderedActionPlanIds[i], out var payload))
                {
                    continue;
                }

                contests.Add(
                    new Contest(
                        nextContestId++,
                        ContestKind.Plan,
                        payload.ActionPlanId,
                        payload.SourceActorEntityId,
                        payload.Priority,
                        payload.SourceActorEntityId,
                        default,
                        hasAffectedCell: false,
                        localActionIndex: 0));
            }

            return contests;
        }

        private void ResolveAttackActionPlansCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<Contest> planContests,
            List<ResolutionRecord> resolutionRecords,
            List<string> rejectedReasons)
        {
            var selectedIntentIds = new HashSet<int>();
            var contestsByActionPlanId = BuildContestLookup(planContests);

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!payloads.TryGetValue(actionPlanId, out var payload) ||
                    !contestsByActionPlanId.TryGetValue(actionPlanId, out var contest))
                {
                    continue;
                }

                var accepted = selectedIntentIds.Add(payload.IntentId);
                if (!accepted)
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Resolve|G={payload.ActionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}|Reason=IntentAlreadySelected");
                }
                else
                {
                }

                resolutionRecords.Add(CreateResolutionRecord(contest, accepted));
            }
        }

        private static List<Contest> BuildDamageContestsCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            ref int nextContestId)
        {
            var contests = new List<Contest>();
            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Plan, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var damageIndex = 0; damageIndex < payload.DamageWrites.Count; damageIndex++)
                {
                    var damageWrite = payload.DamageWrites[damageIndex];
                    contests.Add(
                        new Contest(
                            nextContestId++,
                            ContestKind.Damage,
                            payload.ActionPlanId,
                            payload.SourceActorEntityId,
                            payload.Priority,
                            damageWrite.TargetEntityId,
                            default,
                            hasAffectedCell: false,
                            localActionIndex: damageIndex));
                }
            }

            return contests;
        }

        private static List<Contest> BuildMovementDestroyContestsCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            ref int nextContestId)
        {
            var contests = new List<Contest>();
            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Space, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var destroyIndex = 0; destroyIndex < payload.DestroyWrites.Count; destroyIndex++)
                {
                    var destroyWrite = payload.DestroyWrites[destroyIndex];
                    contests.Add(
                        new Contest(
                            nextContestId++,
                            ContestKind.Destroy,
                            payload.ActionPlanId,
                            payload.SourceActorEntityId,
                            payload.Priority,
                            destroyWrite.TargetEntityId,
                            default,
                            hasAffectedCell: false,
                            localActionIndex: destroyIndex,
                            destroyCondition: destroyWrite.DestroyCondition));
                }
            }

            return contests;
        }

        private static List<Contest> BuildAttackDestroyContestsCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            ref int nextContestId)
        {
            var contests = new List<Contest>();
            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Plan, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var destroyIndex = 0; destroyIndex < payload.DestroyWrites.Count; destroyIndex++)
                {
                    var destroyWrite = payload.DestroyWrites[destroyIndex];
                    contests.Add(
                        new Contest(
                            nextContestId++,
                            ContestKind.Destroy,
                            payload.ActionPlanId,
                            payload.SourceActorEntityId,
                            payload.Priority,
                            destroyWrite.TargetEntityId,
                            default,
                            hasAffectedCell: false,
                            localActionIndex: destroyIndex,
                            destroyCondition: destroyWrite.DestroyCondition));
                }
            }

            return contests;
        }

        private List<DamageResolutionRecord> ResolveDamageResolutionsCanonical(
            WorldSnapshot snapshot,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            int tickIndex)
        {
            var damageResolutions = new List<DamageResolutionRecord>();
            var playerDamageStatesByEntityId = new Dictionary<int, PlayerDamageState>();

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Plan, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var damageIndex = 0; damageIndex < payload.DamageWrites.Count; damageIndex++)
                {
                    var damageWrite = payload.DamageWrites[damageIndex];
                    if (!snapshot.TryGetEntity(damageWrite.TargetEntityId, out var target) ||
                        !EntityRolePolicy.IsPlayerUnit(target))
                    {
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                payload.ActionPlanId,
                                payload.IntentId,
                                payload.SourceActorEntityId,
                                damageWrite.AttackSourceKind,
                                damageWrite.TargetEntityId,
                                damageWrite.Amount,
                                accepted: true,
                                DamageRejectReason.None,
                                localActionIndex: damageIndex,
                                hasPlayerDamageState: damageWrite.HasPlayerDamageState,
                                playerDamageState: damageWrite.PlayerDamageState));
                        continue;
                    }

                    if (!playerDamageStatesByEntityId.TryGetValue(damageWrite.TargetEntityId, out var damageState))
                    {
                        damageState = snapshot.TryGetPlayerDamageState(damageWrite.TargetEntityId, out var storedState)
                            ? storedState
                            : default;
                    }

                    if (!PlayerDamageQueries.CanAcceptDamage(damageState, tickIndex))
                    {
                        playerDamageStatesByEntityId[damageWrite.TargetEntityId] = damageState;
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                payload.ActionPlanId,
                                payload.IntentId,
                                payload.SourceActorEntityId,
                                damageWrite.AttackSourceKind,
                                damageWrite.TargetEntityId,
                                damageWrite.Amount,
                                accepted: false,
                                DamageRejectReason.ReceiverCooldown,
                                localActionIndex: damageIndex));
                        continue;
                    }

                    var updatedState = PlayerDamageQueries.AcceptDamage(damageState, tickIndex, _playerDamageCooldownTicks);
                    playerDamageStatesByEntityId[damageWrite.TargetEntityId] = updatedState;
                    damageResolutions.Add(
                        new DamageResolutionRecord(
                            payload.ActionPlanId,
                            payload.IntentId,
                            payload.SourceActorEntityId,
                            damageWrite.AttackSourceKind,
                            damageWrite.TargetEntityId,
                            damageWrite.Amount,
                            accepted: true,
                            DamageRejectReason.None,
                            localActionIndex: damageIndex,
                            hasPlayerDamageState: true,
                            playerDamageState: updatedState));
                }
            }

            return damageResolutions;
        }

        private static List<DestroyResolutionRecord> ResolveMovementDestroyResolutionsCanonical(
            WorldSnapshot snapshot,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords)
        {
            var destroyResolutions = new List<DestroyResolutionRecord>();
            var destroyMarkedTargets = new HashSet<int>();

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Space, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var destroyIndex = 0; destroyIndex < payload.DestroyWrites.Count; destroyIndex++)
                {
                    var destroyWrite = payload.DestroyWrites[destroyIndex];
                    var accepted = false;
                    if (!destroyMarkedTargets.Contains(destroyWrite.TargetEntityId) &&
                        snapshot.TryGetEntity(destroyWrite.TargetEntityId, out var target) &&
                        !target.markedForDeath)
                    {
                        if (destroyWrite.DestroyCondition != DestroyCondition.WhenHpDepleted || target.hp <= 0)
                        {
                            accepted = true;
                            destroyMarkedTargets.Add(destroyWrite.TargetEntityId);
                        }
                    }

                    destroyResolutions.Add(
                        new DestroyResolutionRecord(
                            payload.ActionPlanId,
                            payload.IntentId,
                            payload.SourceActorEntityId,
                            destroyWrite.TargetEntityId,
                            destroyWrite.DestroyCondition,
                            snapshot.TryGetEntity(destroyWrite.TargetEntityId, out var existing) ? existing.hp : 0,
                            accepted,
                            destroyIndex));
                }
            }

            return destroyResolutions;
        }

        private static List<DestroyResolutionRecord> ResolveDestroyResolutionsCanonical(
            WorldSnapshot snapshot,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            IReadOnlyList<DamageResolutionRecord> damageResolutions)
        {
            var accumulatedDamageByTarget = new Dictionary<int, int>();
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                if (!damageResolutions[i].Accepted)
                {
                    continue;
                }

                accumulatedDamageByTarget[damageResolutions[i].TargetId] =
                    accumulatedDamageByTarget.TryGetValue(damageResolutions[i].TargetId, out var existingDamage)
                        ? existingDamage + damageResolutions[i].Amount
                        : damageResolutions[i].Amount;
            }

            var destroyResolutions = new List<DestroyResolutionRecord>();
            var destroyMarkedTargets = new HashSet<int>();

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Plan, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var destroyIndex = 0; destroyIndex < payload.DestroyWrites.Count; destroyIndex++)
                {
                    var destroyWrite = payload.DestroyWrites[destroyIndex];
                    var accepted = false;
                    var finalHp = 0;

                    if (!destroyMarkedTargets.Contains(destroyWrite.TargetEntityId) &&
                        snapshot.TryGetEntity(destroyWrite.TargetEntityId, out var target) &&
                        !target.markedForDeath)
                    {
                        var accumulatedDamage = accumulatedDamageByTarget.TryGetValue(destroyWrite.TargetEntityId, out var damage)
                            ? damage
                            : 0;
                        finalHp = target.hp - accumulatedDamage;
                        if (destroyWrite.DestroyCondition != DestroyCondition.WhenHpDepleted || finalHp <= 0)
                        {
                            destroyMarkedTargets.Add(destroyWrite.TargetEntityId);
                            accepted = true;
                        }
                    }

                    destroyResolutions.Add(
                        new DestroyResolutionRecord(
                            payload.ActionPlanId,
                            payload.IntentId,
                            payload.SourceActorEntityId,
                            destroyWrite.TargetEntityId,
                            destroyWrite.DestroyCondition,
                            finalHp,
                            accepted,
                            destroyIndex));
                }
            }

            return destroyResolutions;
        }

        private static List<DelayedAttackEffectRecord> ResolveDelayedAttackEffectsCanonical(
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords)
        {
            var delayedAttackEffects = new List<DelayedAttackEffectRecord>();

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                var actionPlanId = orderedActionPlanIds[i];
                if (!HasAcceptedResolution(resolutionRecords, ContestKind.Plan, actionPlanId, localActionIndex: 0) ||
                    !payloads.TryGetValue(actionPlanId, out var payload))
                {
                    continue;
                }

                for (var delayedIndex = 0; delayedIndex < payload.DelayedEnqueueWrites.Count; delayedIndex++)
                {
                    var delayedWrite = payload.DelayedEnqueueWrites[delayedIndex];
                    delayedAttackEffects.Add(
                        new DelayedAttackEffectRecord(
                            payload.SourceActorEntityId,
                            delayedWrite.TargetEntityId,
                            delayedWrite.Damage,
                            payload.Priority,
                            delayedWrite.TickGenerated,
                            delayedWrite.ExecuteAtTick,
                            payload.ActionPlanId,
                            delayedWrite.EffectSequence));
                }
            }

            return delayedAttackEffects;
        }

        private void ResolveImpactSpaceContestsCanonical(
            WorldSnapshot attackSnapshot,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<Contest> impactSpaceContests,
            MovementReservationBook reservationBook,
            List<ResolutionRecord> resolutionRecords,
            List<ImpactDispositionResolutionRecord> impactDispositionRecords)
        {
            if (impactSpaceContests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < impactSpaceContests.Count; i++)
            {
                var contest = impactSpaceContests[i];
                var accepted = false;
                var followThroughLegalityChecked = false;
                var followThroughAccepted = false;
                var targetDestroyed = false;
                var dispositionKind = ImpactDispositionKind.Stay;
                if (payloads.TryGetValue(contest.ActionPlanId, out var payload) &&
                    payload.HasImpactReservationPayload)
                {
                    targetDestroyed = HasAcceptedImpactDestroy(
                        destroyResolutions,
                        payload.ImpactReservationPayload.AttackSourceEntityId,
                        payload.ImpactReservationPayload.TargetEntityId);

                    switch (payload.ImpactReservationPayload.DispositionPolicyKind)
                    {
                        case ImpactDispositionPolicyKind.PushLike:
                            if (targetDestroyed)
                            {
                                followThroughLegalityChecked = true;
                                var reservationStatus = reservationBook.GetImpactPayloadStatus(payload.ImpactReservationPayload);
                                var impactLegality = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                                    new SettlementContext(
                                        attackSnapshot,
                                        BuildLegalityActorRef(attackSnapshot, payload.SourceActorEntityId, EntityType.Unit),
                                        payload.ImpactReservationPayload.ContingentDestinationCell,
                                        attackSnapshot.Topology,
                                        SpatialState.Anchored,
                                        reservationStatus),
                                    new ImpactFollowThroughEvidence(
                                        payload.ImpactReservationPayload.AttackSourceEntityId,
                                        payload.ImpactReservationPayload.TargetEntityId,
                                        destroyResolutions));
                                followThroughAccepted = impactLegality.Verdict == LegalityVerdict.Allowed;
                                if (followThroughAccepted)
                                {
                                    accepted = true;
                                    dispositionKind = ImpactDispositionKind.FollowThrough;
                                    reservationBook.ReserveImpactPayload(payload.ImpactReservationPayload, contest.ActionPlanId);
                                }
                            }

                            break;

                        case ImpactDispositionPolicyKind.Flip:
                            if (!targetDestroyed)
                            {
                                dispositionKind = ImpactDispositionKind.DestroySelf;
                                break;
                            }

                            followThroughLegalityChecked = true;
                            var flipReservationStatus = reservationBook.GetImpactPayloadStatus(payload.ImpactReservationPayload);
                            var flipImpactLegality = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                                new SettlementContext(
                                    attackSnapshot,
                                    BuildLegalityActorRef(attackSnapshot, payload.SourceActorEntityId, EntityType.Unit),
                                    payload.ImpactReservationPayload.ContingentDestinationCell,
                                    attackSnapshot.Topology,
                                    SpatialState.Anchored,
                                    flipReservationStatus),
                                new ImpactFollowThroughEvidence(
                                    payload.ImpactReservationPayload.AttackSourceEntityId,
                                    payload.ImpactReservationPayload.TargetEntityId,
                                    destroyResolutions));
                            followThroughAccepted = flipImpactLegality.Verdict == LegalityVerdict.Allowed;
                            if (followThroughAccepted)
                            {
                                accepted = true;
                                dispositionKind = ImpactDispositionKind.FollowThrough;
                                reservationBook.ReserveImpactPayload(payload.ImpactReservationPayload, contest.ActionPlanId);
                            }

                            break;
                    }

                    impactDispositionRecords.Add(
                        new ImpactDispositionResolutionRecord(
                            contest.ActionPlanId,
                            payload.ImpactReservationPayload.SourceEntityId,
                            payload.ImpactReservationPayload.TargetEntityId,
                            payload.ImpactReservationPayload.ImpactCell,
                            payload.ImpactReservationPayload.DispositionPolicyKind,
                            dispositionKind,
                            targetDestroyed,
                            followThroughLegalityChecked,
                            followThroughAccepted));
                }

                resolutionRecords.Add(CreateResolutionRecord(contest, accepted));
            }
        }

        private static List<ImpactReservation> SortImpactReservations(IReadOnlyList<ImpactReservation> impactReservations)
        {
            var sortedReservations = new List<ImpactReservation>(impactReservations);
            sortedReservations.Sort(ImpactReservationComparer.Instance);
            return sortedReservations;
        }

        private static TickEntityExitCause ResolveMovementExitCause(
            WorldSnapshot snapshot,
            ActionGroup group,
            int entityId)
        {
            if (group.GroupKind == ActionGroupKind.Item)
            {
                return TickEntityExitCause.ItemConsume;
            }

            if (snapshot.TryGetEntity(group.SourceId, out var sourceEntity) &&
                sourceEntity.entityId == entityId &&
                sourceEntity.type == EntityType.Box)
            {
                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    if (group.Destroys[destroyIndex].TargetId == entityId)
                    {
                        return TickEntityExitCause.DestroyedByImpact;
                    }
                }
            }

            return TickEntityExitCause.None;
        }

        private static TickEntityExitCause ResolveMovementDestroyExitCause(
            WorldSnapshot snapshot,
            ActionGroup group,
            int targetEntityId)
        {
            return snapshot.TryGetEntity(targetEntityId, out var target) &&
                   target.type == EntityType.Box
                ? TickEntityExitCause.DestroyedByImpact
                : ResolveMovementExitCause(snapshot, group, targetEntityId);
        }

        private static MoveIntent FindMovementIntent(IReadOnlyList<MoveIntent> sortedIntents, int intentId)
        {
            for (var i = 0; i < sortedIntents.Count; i++)
            {
                if (sortedIntents[i].IntentId == intentId)
                {
                    return sortedIntents[i];
                }
            }

            return null;
        }

        private static Direction ResolveFlipSourceFacing(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ActionGroup group)
        {
            if (!snapshot.TryGetEntity(group.SourceId, out var source))
            {
                throw new InvalidOperationException(
                    $"Flip group references a missing source entity. Source={group.SourceId}, Intent={group.IntentId}");
            }

            var intent = FindMovementIntent(sortedIntents, group.IntentId);
            if (intent == null)
            {
                throw new InvalidOperationException(
                    $"Flip group is missing its movement intent. Source={group.SourceId}, Intent={group.IntentId}");
            }

            var delta = intent.Destination - source.position;
            if (delta.x == 0 && delta.y == 1)
            {
                return Direction.Up;
            }

            if (delta.x == 1 && delta.y == 0)
            {
                return Direction.Right;
            }

            if (delta.x == 0 && delta.y == -1)
            {
                return Direction.Down;
            }

            if (delta.x == -1 && delta.y == 0)
            {
                return Direction.Left;
            }

            throw new InvalidOperationException(
                $"Flip group requires an orthogonal adjacent direction. Source={group.SourceId}, Intent={group.IntentId}");
        }

        private static string BuildSourceKindSuffix(AttackSourceKind sourceKind)
        {
            return sourceKind == AttackSourceKind.PassiveContact
                ? $"|SourceKind={sourceKind}"
                : string.Empty;
        }

        private static string BuildJumpLandingUpdate(
            int entityId,
            string label,
            in EnemyJumpRuntimeState state,
            string extra)
        {
            var builder = new System.Text.StringBuilder();
            builder
                .Append("EnemyJumpStateUpdated|E=").Append(entityId)
                .Append("|Label=").Append(label ?? string.Empty)
                .Append("|Phase=").Append(state.phase)
                .Append("|Seq=").Append(state.sequence)
                .Append("|Source=").Append(state.sourceCell)
                .Append("|Locked=").Append(state.lockedTargetCell)
                .Append("|WindupEnd=").Append(state.windupEndTick)
                .Append("|Landing=").Append(state.landingTick)
                .Append("|Cooldown=").Append(state.cooldownRemainingTicks)
                .Append("|Retry=").Append(state.retryCount);

            if (!string.IsNullOrEmpty(extra))
            {
                builder.Append('|').Append(extra);
            }

            return builder.ToString();
        }

        private static string BuildPhaseRelocationUpdate(
            int entityId,
            string label,
            PhaseRelocationActionPlanPayload payload,
            ReservationStatus reservationStatus,
            string result)
        {
            return $"EnemyPhaseRelocation|E={entityId}|Label={label}|Target={payload.LockedTargetEntityId}|Direction={payload.Direction}|Destination={FormatCell(payload.DestinationCell)}|Rule={payload.RuleLabel}|Reservation={reservationStatus}|Result={result}";
        }

        private static DamageResolutionRecord FindDamageResolution(
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            int actionPlanId,
            int targetId,
            int localActionIndex)
        {
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                if (damageResolutions[i].ActionPlanId == actionPlanId &&
                    damageResolutions[i].TargetId == targetId &&
                    damageResolutions[i].LocalActionIndex == localActionIndex)
                {
                    return damageResolutions[i];
                }
            }

            throw new InvalidOperationException(
                $"Missing damage resolution for action plan {actionPlanId}, target {targetId}, action {localActionIndex}.");
        }

        private static IReadOnlyList<StageObjectiveDamageFact> BuildObjectiveDamageFacts(
            IReadOnlyList<DamageResolutionRecord> damageResolutions)
        {
            if (damageResolutions == null || damageResolutions.Count == 0)
            {
                return Array.Empty<StageObjectiveDamageFact>();
            }

            var facts = new StageObjectiveDamageFact[damageResolutions.Count];
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                var damageResolution = damageResolutions[i];
                facts[i] = new StageObjectiveDamageFact(
                    damageResolution.SourceId,
                    damageResolution.SourceKind,
                    damageResolution.TargetId,
                    damageResolution.Amount,
                    damageResolution.Accepted,
                    damageResolution.RejectReason,
                    damageResolution.LocalActionIndex,
                    damageResolution.HasPlayerDamageState,
                    damageResolution.PlayerDamageState);
            }

            return facts;
        }

        private static DestroyResolutionRecord FindDestroyResolution(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int actionPlanId,
            int targetId,
            int localActionIndex)
        {
            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].ActionPlanId == actionPlanId &&
                    destroyResolutions[i].TargetId == targetId &&
                    destroyResolutions[i].LocalActionIndex == localActionIndex)
                {
                    return destroyResolutions[i];
                }
            }

            throw new InvalidOperationException(
                $"Missing destroy resolution for action plan {actionPlanId}, target {targetId}, action {localActionIndex}.");
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
                    $"DelayedAttackDrained|Tick={tickIndex}|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|GeneratedTick={effectRecord.TickGenerated}|ExecuteTick={effectRecord.ExecuteAtTick}|Group={effectRecord.SourceActionPlanId}|Sequence={effectRecord.EffectSequence}");
            }

            return events;
        }

        private static List<Contest> BuildSpaceContests(
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<int> orderedActionPlanIds,
            ref int nextContestId)
        {
            var contests = new List<Contest>(orderedActionPlanIds.Count);

            for (var i = 0; i < orderedActionPlanIds.Count; i++)
            {
                if (!payloads.TryGetValue(orderedActionPlanIds[i], out var payload))
                {
                    continue;
                }

                var hasAffectedCell = payload.MoveWrites.Count > 0 ||
                                      PayloadHasKinematicAnchorTransition(payload);
                var affectedCell = hasAffectedCell
                    ? payload.DestinationCell
                    : default;
                contests.Add(
                    new Contest(
                        nextContestId++,
                        ContestKind.Space,
                        payload.ActionPlanId,
                        payload.SourceActorEntityId,
                        payload.Priority,
                        payload.SourceActorEntityId,
                        affectedCell,
                        hasAffectedCell,
                        localActionIndex: 0));
            }

            return contests;
        }

        private static bool PayloadHasKinematicAnchorTransition(MovementActionPlanPayload payload)
        {
            for (var i = 0; i < payload.KinematicMotionOutcomes.Count; i++)
            {
                if (payload.KinematicMotionOutcomes[i].AnchorChanged)
                {
                    return true;
                }
            }

            return false;
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
                        reservation.SourceActionPlanId,
                        reservation.SourceId,
                        priority: 0,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        hasAffectedCell: true,
                        localActionIndex: reservation.LocalActionIndex));
            }

            return contests;
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
                    if (damageResolution.ActionPlanId != contest.ActionPlanId ||
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
                    if (destroyResolution.ActionPlanId != contest.ActionPlanId ||
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

        private static WorldSnapshot CreateDamageProjectedSnapshot(
            WorldSnapshot baseSnapshot,
            IReadOnlyList<DamageResolutionRecord> damageResolutions)
        {
            var damageProjectionBatch = new FinalizationBatch();
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                var damageResolution = damageResolutions[i];
                if (!damageResolution.Accepted)
                {
                    continue;
                }

                if (damageResolution.HasPlayerDamageState)
                {
                    damageProjectionBatch.SetPlayerDamageState(damageResolution.TargetId, damageResolution.PlayerDamageState);
                }

                damageProjectionBatch.ApplyDamage(damageResolution.TargetId, damageResolution.Amount);
            }

            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyBatch(damageProjectionBatch);
            return projectedWorld.CreateSnapshot();
        }

        private static bool IsImpactTargetSurviving(
            WorldSnapshot snapshot,
            int targetId)
        {
            return snapshot.TryGetEntity(targetId, out var target) &&
                   !target.markedForDeath &&
                   target.hp > 0;
        }

        private static LegalityActorRef BuildLegalityActorRef(
            WorldSnapshot snapshot,
            int entityId,
            EntityType entityType)
        {
            return StateQuery.BuildActorRef(snapshot, entityId, entityType);
        }

        private static bool TryGetConflictingImpactDestination(
            MovementImpactSpaceResolutionRecord resolution,
            HashSet<SurfaceCell> reservedDestinations,
            out SurfaceCell conflictingDestination)
        {
            conflictingDestination = default;
            if (!reservedDestinations.Contains(resolution.DestinationCell))
            {
                return false;
            }

            conflictingDestination = resolution.DestinationCell;
            return true;
        }

        private static bool TryGetConflictingImpactEdge(
            MovementImpactSpaceResolutionRecord resolution,
            IReadOnlyDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            out UndirectedEdgeKey conflictingEdge)
        {
            conflictingEdge = default;

            if (Math.Abs(resolution.DestinationCell.x - resolution.SourceCell.x) +
                Math.Abs(resolution.DestinationCell.y - resolution.SourceCell.y) > 1)
            {
                return false;
            }

            if (resolution.SourceCell == resolution.DestinationCell)
            {
                return false;
            }

            var edge = UndirectedEdgeKey.Create(resolution.SourceCell, resolution.DestinationCell);
            if (!reservedEdges.ContainsKey(edge))
            {
                return false;
            }

            conflictingEdge = edge;
            return true;
        }

        private static bool TryGetSharedImpactAffectedEntity(
            MovementImpactSpaceResolutionRecord resolution,
            ISet<int> reservedAffectedEntities,
            out int sharedEntityId)
        {
            sharedEntityId = default;
            if (!reservedAffectedEntities.Contains(resolution.EntityId))
            {
                return false;
            }

            sharedEntityId = resolution.EntityId;
            return true;
        }

        private static void ReserveImpactSpace(
            MovementImpactSpaceResolutionRecord resolution,
            HashSet<SurfaceCell> reservedDestinations,
            ISet<SurfaceCell> reservedBlockingDestinations,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedBlockingEdges,
            ISet<int> reservedAffectedEntities,
            int groupId)
        {
            reservedDestinations.Add(resolution.DestinationCell);
            reservedBlockingDestinations.Add(resolution.DestinationCell);
            reservedAffectedEntities.Add(resolution.EntityId);

            if (Math.Abs(resolution.DestinationCell.x - resolution.SourceCell.x) +
                Math.Abs(resolution.DestinationCell.y - resolution.SourceCell.y) > 1 ||
                resolution.SourceCell == resolution.DestinationCell)
            {
                return;
            }

            var edgeReservation = new EdgeReservation(
                resolution.EntityId,
                resolution.SourceCell,
                resolution.DestinationCell,
                groupId);
            var edgeKey = UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To);
            reservedEdges[edgeKey] = edgeReservation;
            reservedBlockingEdges[edgeKey] = edgeReservation;
        }

        private static void ReserveCandidate(
            ActionGroup candidate,
            HashSet<SurfaceCell> reservedDestinations,
            ISet<SurfaceCell> reservedBlockingDestinations,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            IDictionary<UndirectedEdgeKey, EdgeReservation> reservedBlockingEdges,
            ISet<int> reservedAffectedEntities,
            ReservationMode reservationMode,
            ref ExclusiveGroupReservation? firstSelectedReservation,
            ref ExclusiveGroupReservation? topologyExclusiveReservation)
        {
            var blocksSharedUnitMoves = reservationMode == ReservationMode.Conservative;

            for (var moveIndex = 0; moveIndex < candidate.Moves.Count; moveIndex++)
            {
                var move = candidate.Moves[moveIndex];
                reservedDestinations.Add(move.DestinationCell);
                if (blocksSharedUnitMoves)
                {
                    reservedBlockingDestinations.Add(move.DestinationCell);
                }

                reservedAffectedEntities.Add(move.EntityId);

                if (RequiresEdgeReservation(candidate) && move.SourceCell != move.DestinationCell)
                {
                    var edgeReservation = new EdgeReservation(
                        move.EntityId,
                        move.SourceCell,
                        move.DestinationCell,
                        candidate.GroupId);
                    var edgeKey = UndirectedEdgeKey.Create(edgeReservation.From, edgeReservation.To);
                    reservedEdges[edgeKey] = edgeReservation;
                    if (blocksSharedUnitMoves)
                    {
                        reservedBlockingEdges[edgeKey] = edgeReservation;
                    }
                }
            }

            for (var destroyIndex = 0; destroyIndex < candidate.Destroys.Count; destroyIndex++)
            {
                reservedAffectedEntities.Add(candidate.Destroys[destroyIndex].TargetId);
            }

            for (var stateChangeIndex = 0; stateChangeIndex < candidate.StateChanges.Count; stateChangeIndex++)
            {
                reservedAffectedEntities.Add(candidate.StateChanges[stateChangeIndex].EntityId);
            }

            for (var presenceIndex = 0; presenceIndex < candidate.BoardPresenceChanges.Count; presenceIndex++)
            {
                reservedAffectedEntities.Add(candidate.BoardPresenceChanges[presenceIndex].EntityId);
            }

            if (!firstSelectedReservation.HasValue)
            {
                firstSelectedReservation = new ExclusiveGroupReservation(
                    candidate.GroupId,
                    candidate.GroupKind,
                    HasTopologyChange(candidate));
            }

            if (!topologyExclusiveReservation.HasValue && HasTopologyChange(candidate))
            {
                topologyExclusiveReservation = new ExclusiveGroupReservation(
                    candidate.GroupId,
                    candidate.GroupKind,
                    hasTopologyChange: true);
            }
        }

        private static bool TryGetConflictingDestination(
            ActionGroup candidate,
            HashSet<SurfaceCell> reservedDestinations,
            out SurfaceCell conflictingDestination)
        {
            conflictingDestination = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var destination = candidate.Moves[i].DestinationCell;
                if (reservedDestinations.Contains(destination))
                {
                    conflictingDestination = destination;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConflictingEdge(
            ActionGroup candidate,
            IReadOnlyDictionary<UndirectedEdgeKey, EdgeReservation> reservedEdges,
            out UndirectedEdgeKey conflictingEdge)
        {
            conflictingEdge = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var move = candidate.Moves[i];
                if (move.SourceCell == move.DestinationCell)
                {
                    continue;
                }

                var edge = UndirectedEdgeKey.Create(move.SourceCell, move.DestinationCell);
                if (reservedEdges.ContainsKey(edge))
                {
                    conflictingEdge = edge;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSharedAffectedEntity(
            ActionGroup candidate,
            ISet<int> reservedAffectedEntities,
            out int sharedEntityId)
        {
            sharedEntityId = default;

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                var entityId = candidate.Moves[i].EntityId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            for (var i = 0; i < candidate.Destroys.Count; i++)
            {
                var entityId = candidate.Destroys[i].TargetId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            for (var i = 0; i < candidate.StateChanges.Count; i++)
            {
                var entityId = candidate.StateChanges[i].EntityId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            for (var i = 0; i < candidate.BoardPresenceChanges.Count; i++)
            {
                var entityId = candidate.BoardPresenceChanges[i].EntityId;
                if (reservedAffectedEntities.Contains(entityId))
                {
                    sharedEntityId = entityId;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetTopologyExclusiveConflict(
            ActionGroup candidate,
            ExclusiveGroupReservation? firstSelectedReservation,
            ExclusiveGroupReservation? topologyExclusiveReservation,
            out ExclusiveGroupReservation conflictingReservation)
        {
            conflictingReservation = default;

            if (HasTopologyChange(candidate))
            {
                if (!firstSelectedReservation.HasValue)
                {
                    return false;
                }

                conflictingReservation = firstSelectedReservation.Value;
                return true;
            }

            if (!topologyExclusiveReservation.HasValue)
            {
                return false;
            }

            conflictingReservation = topologyExclusiveReservation.Value;
            return true;
        }

        private static bool RequiresEdgeReservation(ActionGroup candidate)
        {
            return candidate.GroupKind != ActionGroupKind.Flip;
        }

        private static ReservationMode ResolveReservationMode(WorldSnapshot snapshot, ActionGroup candidate)
        {
            if (snapshot == null ||
                candidate.GroupKind != ActionGroupKind.Move ||
                HasTopologyChange(candidate) ||
                candidate.Moves.Count == 0)
            {
                return ReservationMode.Conservative;
            }

            for (var i = 0; i < candidate.Moves.Count; i++)
            {
                if (!snapshot.TryGetEntity(candidate.Moves[i].EntityId, out var entity) ||
                    entity.type != EntityType.Unit)
                {
                    return ReservationMode.Conservative;
                }
            }

            return ReservationMode.UnitSharedMove;
        }

        private static bool HasTopologyChange(ActionGroup candidate)
        {
            return candidate.TopologyChanges.Count > 0;
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
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
                if (snapshot.CanExecuteIntent(rawIntent.SourceId, tickIndex) ||
                    CanExecuteExecutionLockedPassiveContact(snapshot, rawIntent, tickIndex) ||
                    CanExecuteExecutionLockedImmediateCombat(snapshot, rawIntent, tickIndex) ||
                    CanExecuteExecutionLockedWindupCombat(snapshot, rawIntent, tickIndex))
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

        private static bool CanExecuteExecutionLockedPassiveContact(
            WorldSnapshot snapshot,
            in RawAttackIntent rawIntent,
            int tickIndex)
        {
            if (rawIntent.SourceKind != AttackSourceKind.PassiveContact ||
                !snapshot.TryGetEntityExecutionLockState(rawIntent.SourceId, out var lockState))
            {
                return false;
            }

            return lockState.phase == EntityExecutionPhase.Move &&
                   EntityExecutionLockQueries.IsLocked(lockState, tickIndex);
        }

        private static bool CanExecuteExecutionLockedImmediateCombat(
            WorldSnapshot snapshot,
            in RawAttackIntent rawIntent,
            int tickIndex)
        {
            if (rawIntent.SourceKind != AttackSourceKind.Combat ||
                !snapshot.TryGetEntityExecutionLockState(rawIntent.SourceId, out var lockState) ||
                lockState.phase != EntityExecutionPhase.Move ||
                !EntityExecutionLockQueries.IsLocked(lockState, tickIndex) ||
                !snapshot.TryGetEnemyActionState(rawIntent.SourceId, out var actionState))
            {
                return false;
            }

            return actionState.IsActive &&
                   actionState.startTick == tickIndex &&
                   actionState.executeTick == tickIndex;
        }

        private static bool CanExecuteExecutionLockedWindupCombat(
            WorldSnapshot snapshot,
            in RawAttackIntent rawIntent,
            int tickIndex)
        {
            if (rawIntent.SourceKind != AttackSourceKind.Combat ||
                !snapshot.TryGetEntityExecutionLockState(rawIntent.SourceId, out var lockState) ||
                lockState.phase != EntityExecutionPhase.Move ||
                !EntityExecutionLockQueries.IsLocked(lockState, tickIndex) ||
                !snapshot.TryGetEnemyActionState(rawIntent.SourceId, out var actionState))
            {
                return false;
            }

            return actionState.IsActive &&
                   actionState.startTick < tickIndex &&
                   actionState.executeTick == tickIndex;
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

        private static List<string> CreateMovementCommitEventBuffer(IReadOnlyList<string> preMovementEventLogEntries)
        {
            var movementCommitEvents = new List<string>(preMovementEventLogEntries?.Count ?? 0);
            if (preMovementEventLogEntries != null)
            {
                AddRange(movementCommitEvents, preMovementEventLogEntries);
            }

            return movementCommitEvents;
        }

        private static void AppendBoxInteractionLockBlockedEvents(
            IReadOnlyList<string> movementRejectedReasons,
            List<string> movementCommitEvents,
            int tickIndex)
        {
            if (movementRejectedReasons == null || movementCommitEvents == null)
            {
                return;
            }

            for (var i = 0; i < movementRejectedReasons.Count; i++)
            {
                if (!TryBuildBoxInteractionLockBlockedEvent(movementRejectedReasons[i], tickIndex, out var blockedEvent))
                {
                    continue;
                }

                movementCommitEvents.Add(blockedEvent);
            }
        }

        private static void AppendFrontFaceShieldBlockedEvents(
            IReadOnlyList<string> movementRejectedReasons,
            List<string> movementCommitEvents,
            int tickIndex)
        {
            if (movementRejectedReasons == null || movementCommitEvents == null)
            {
                return;
            }

            for (var i = 0; i < movementRejectedReasons.Count; i++)
            {
                if (!TryBuildFrontFaceShieldBlockedEvents(
                        movementRejectedReasons[i],
                        tickIndex,
                        out var blockedEvent,
                        out var playerBlockedEvent))
                {
                    continue;
                }

                movementCommitEvents.Add(blockedEvent);
                if (!string.IsNullOrEmpty(playerBlockedEvent))
                {
                    movementCommitEvents.Add(playerBlockedEvent);
                }
            }
        }

        private static bool TryBuildBoxInteractionLockBlockedEvent(
            string movementRejectedReason,
            int tickIndex,
            out string blockedEvent)
        {
            blockedEvent = null;
            if (string.IsNullOrEmpty(movementRejectedReason) ||
                !movementRejectedReason.StartsWith("MovementRejected|", StringComparison.Ordinal))
            {
                return false;
            }

            var actionKind = default(PlayerActionKind);
            if (movementRejectedReason.Contains("Reason=PushTargetLocked", StringComparison.Ordinal))
            {
                actionKind = PlayerActionKind.Push;
            }
            else if (movementRejectedReason.Contains("Reason=FlipTargetLocked", StringComparison.Ordinal))
            {
                actionKind = PlayerActionKind.Flip;
            }
            else
            {
                return false;
            }

            if (!TryGetStructuredLogValue(movementRejectedReason, "Source", out var sourceText) ||
                !TryGetStructuredLogValue(movementRejectedReason, "Target", out var targetText) ||
                !TryGetStructuredLogValue(movementRejectedReason, "Cell", out var cellText))
            {
                return false;
            }

            blockedEvent =
                $"PlayerActionBlockedByBoxInteractionLock|Action={actionKind}|Actor={sourceText}|Box={targetText}|Cell={cellText}|Tick={tickIndex}";
            return true;
        }

        private static bool TryBuildFrontFaceShieldBlockedEvents(
            string movementRejectedReason,
            int tickIndex,
            out string blockedEvent,
            out string playerBlockedEvent)
        {
            blockedEvent = null;
            playerBlockedEvent = null;
            if (string.IsNullOrEmpty(movementRejectedReason) ||
                !movementRejectedReason.StartsWith("MovementRejected|", StringComparison.Ordinal) ||
                !movementRejectedReason.Contains("Reason=BoxSlideBlockedByFrontFaceShield", StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetStructuredLogValue(movementRejectedReason, "MovementKind", out var movementKindText) ||
                !TryGetStructuredLogValue(movementRejectedReason, "Box", out var boxText) ||
                !TryGetStructuredLogValue(movementRejectedReason, "Cell", out var cellText) ||
                !TryGetStructuredLogValue(movementRejectedReason, "ShieldSource", out var shieldSourceText))
            {
                return false;
            }

            blockedEvent =
                $"BoxSlideBlockedByFrontFaceShield|MovementKind={movementKindText}|Box={boxText}|Cell={cellText}|ShieldSource={shieldSourceText}|Tick={tickIndex}";

            if (string.Equals(movementKindText, nameof(BoxSlideMovementKind.PushStart), StringComparison.Ordinal) &&
                TryGetStructuredLogValue(movementRejectedReason, "Source", out var sourceText))
            {
                playerBlockedEvent =
                    $"PlayerActionBlockedByFrontFaceShield|Action={PlayerActionKind.Push}|Actor={sourceText}|Box={boxText}|Cell={cellText}|ShieldSource={shieldSourceText}|Tick={tickIndex}";
            }

            return true;
        }

        private static bool TryGetStructuredLogValue(string line, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            var token = key + "=";
            var startIndex = line.IndexOf(token, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return false;
            }

            startIndex += token.Length;
            var endIndex = line.IndexOf('|', startIndex);
            value = endIndex >= 0
                ? line.Substring(startIndex, endIndex - startIndex)
                : line.Substring(startIndex);
            return true;
        }

        private void AssignAttackGroupIds(List<ActionGroup> expandedCandidates)
        {
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(_idAllocator.AllocateGroupId());
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
        private readonly Dictionary<int, PlayerRespawnDelayState> _respawnDelayStatesByEntityId = new();

        public RespawnPhaseResult Process(
            WorldSnapshot tickStartSnapshot,
            WorldSnapshot postCleanupSnapshot,
            CleanupPhaseResult cleanupPhaseResult,
            IReadOnlyList<EntityState> respawnTemplates,
            int tickIndex,
            int respawnDelayTicks,
            bool allowRespawn,
            IWorldWriteContext writeContext)
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
            var delayRecords = new List<PlayerRespawnDelayRecord>();
            RespawnTopologyResetRequest? topologyResetRequest = null;
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
                    _respawnDelayStatesByEntityId.Remove(entityId);
                    continue;
                }

                if (removedThisTick)
                {
                    var startedState = new PlayerRespawnDelayState(
                        tickIndex,
                        tickIndex + respawnDelayTicks,
                        respawnDelayTicks,
                        elapsedEventEmitted: false);
                    _respawnDelayStatesByEntityId[entityId] = startedState;
                    delayRecords.Add(CreateDelayRecord(entityId, startedState, tickIndex, startedThisTick: true, elapsedThisTick: false));
                    eventLogEntries.Add(
                        $"PlayerRespawnDelayStarted|E={entityId}|StartTick={startedState.StartTick}|EligibleTick={startedState.EligibleTick}|DelayTicks={startedState.DelayTicks}");
                }
                else if (existedAtTickStart)
                {
                    _respawnDelayStatesByEntityId.Remove(entityId);
                    continue;
                }
                else if (!_respawnDelayStatesByEntityId.ContainsKey(entityId))
                {
                    _respawnDelayStatesByEntityId[entityId] = new PlayerRespawnDelayState(
                        tickIndex,
                        tickIndex,
                        delayTicks: 0,
                        elapsedEventEmitted: false);
                }

                var delayState = _respawnDelayStatesByEntityId[entityId];
                if (tickIndex < delayState.EligibleTick)
                {
                    if (!removedThisTick)
                    {
                        delayRecords.Add(CreateDelayRecord(entityId, delayState, tickIndex, startedThisTick: false, elapsedThisTick: false));
                        eventLogEntries.Add(
                            $"PlayerRespawnDelayTicking|E={entityId}|StartTick={delayState.StartTick}|EligibleTick={delayState.EligibleTick}|RemainingTicks={Math.Max(0, delayState.EligibleTick - tickIndex)}|Tick={tickIndex}");
                    }

                    continue;
                }

                if (!delayState.ElapsedEventEmitted)
                {
                    delayState = delayState.WithElapsedEventEmitted();
                    _respawnDelayStatesByEntityId[entityId] = delayState;
                    delayRecords.Add(CreateDelayRecord(entityId, delayState, tickIndex, startedThisTick: false, elapsedThisTick: true));
                    eventLogEntries.Add(
                        $"PlayerRespawnDelayElapsed|E={entityId}|StartTick={delayState.StartTick}|EligibleTick={delayState.EligibleTick}|Tick={tickIndex}");
                }

                if (!allowRespawn)
                {
                    _respawnDelayStatesByEntityId.Remove(entityId);
                    eventLogEntries.Add($"RespawnSuppressed|E={entityId}|Reason=PolicyDisabled|Tick={tickIndex}");
                    continue;
                }

                var respawnEntity = BuildRespawnEntity(template, tickIndex);
                if (postCleanupSnapshot.Topology.BottomFace != respawnEntity.position.face)
                {
                    if (!TryResolveRespawnTopologyReset(
                            postCleanupSnapshot.Topology,
                            respawnEntity.position.face,
                            out var resetTopology,
                            out var rotationKind))
                    {
                        throw new InvalidOperationException(
                            $"Respawn topology reset could not resolve a bottom-face topology step for entity {respawnEntity.entityId} on face {respawnEntity.position.face} from {postCleanupSnapshot.Topology}.");
                    }

                    writeContext.SetTopology(resetTopology);
                    topologyResetRequest = new RespawnTopologyResetRequest(
                        respawnEntity.entityId,
                        respawnEntity.position.face,
                        postCleanupSnapshot.Topology,
                        resetTopology,
                        rotationKind);
                    eventLogEntries.Add(FormatRespawnDeferredEvent(respawnEntity, tickIndex));
                    eventLogEntries.Add(
                        FormatRespawnTopologyResetRequestedEvent(
                            respawnEntity,
                            postCleanupSnapshot.Topology,
                            resetTopology,
                            rotationKind,
                            tickIndex));
                    continue;
                }

                var respawnLegality = RuntimePlacementValidityPolicy.EvaluateAuthoritativePlacement(
                    postCleanupSnapshot,
                    respawnEntity.type,
                    respawnEntity.position,
                    ignoredEntityId: 0);
                if (respawnLegality.Verdict == LegalityVerdict.Blocked)
                {
                    eventLogEntries.Add(FormatRespawnSkippedEvent(respawnEntity, respawnLegality, tickIndex));
                    continue;
                }

                writeContext.SpawnEntity(respawnEntity);
                writeContext.SetPlayerControlState(respawnEntity.entityId, default);
                writeContext.SetPlayerDamageState(respawnEntity.entityId, default);
                respawnedEntities.Add(respawnEntity);
                _respawnDelayStatesByEntityId.Remove(entityId);
                eventLogEntries.Add(
                    $"RespawnCommitted|E={respawnEntity.entityId}|Pos=({respawnEntity.position.x},{respawnEntity.position.y})|Face={respawnEntity.position.face}|Facing={respawnEntity.facing}|Tick={tickIndex}");
            }

            return new RespawnPhaseResult(respawnedEntities, eventLogEntries, delayRecords, topologyResetRequest);
        }

        private static PlayerRespawnDelayRecord CreateDelayRecord(
            int entityId,
            PlayerRespawnDelayState state,
            int currentTick,
            bool startedThisTick,
            bool elapsedThisTick)
        {
            return new PlayerRespawnDelayRecord(
                entityId,
                state.StartTick,
                state.EligibleTick,
                state.DelayTicks,
                currentTick,
                startedThisTick,
                elapsedThisTick);
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

        private readonly struct PlayerRespawnDelayState
        {
            public PlayerRespawnDelayState(
                int startTick,
                int eligibleTick,
                int delayTicks,
                bool elapsedEventEmitted)
            {
                StartTick = startTick;
                EligibleTick = eligibleTick;
                DelayTicks = delayTicks;
                ElapsedEventEmitted = elapsedEventEmitted;
            }

            public int StartTick { get; }

            public int EligibleTick { get; }

            public int DelayTicks { get; }

            public bool ElapsedEventEmitted { get; }

            public PlayerRespawnDelayState WithElapsedEventEmitted()
            {
                return new PlayerRespawnDelayState(
                    StartTick,
                    EligibleTick,
                    DelayTicks,
                    elapsedEventEmitted: true);
            }
        }

        private static string FormatRespawnSkippedEvent(
            EntityState entity,
            LegalityResult legality,
            int tickIndex)
        {
            return LegalityDiagnosticsFormatter.FormatRespawnSkippedEvent(entity, legality, tickIndex);
        }

        private static string FormatRespawnDeferredEvent(EntityState entity, int tickIndex)
        {
            return
                $"RespawnDeferred|E={entity.entityId}|Reason=TopologyResetRequired|TargetFace={entity.position.face}|Tick={tickIndex}";
        }

        private static string FormatRespawnTopologyResetRequestedEvent(
            EntityState entity,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            int tickIndex)
        {
            return
                $"RespawnTopologyResetRequested|E={entity.entityId}|From={sourceTopology.BottomFace}|To={destinationTopology.BottomFace}|Rotation={rotationKind}|TargetFace={entity.position.face}|Tick={tickIndex}";
        }

        private static bool TryResolveRespawnTopologyReset(
            CubeTopologyState currentTopology,
            FaceId targetFace,
            out CubeTopologyState resetTopology,
            out CubeRotationKind rotationKind)
        {
            var forwardTopology = currentTopology.Rotate(CubeRotationKind.Forward);
            if (forwardTopology.BottomFace == targetFace)
            {
                resetTopology = forwardTopology;
                rotationKind = CubeRotationKind.Forward;
                return true;
            }

            var backwardTopology = currentTopology.Rotate(CubeRotationKind.Backward);
            if (backwardTopology.BottomFace == targetFace)
            {
                resetTopology = backwardTopology;
                rotationKind = CubeRotationKind.Backward;
                return true;
            }

            resetTopology = forwardTopology;
            rotationKind = CubeRotationKind.Forward;
            return true;
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
            : this(
                updates,
                new List<PlayerActionTransition>(),
                new List<string>(),
                new List<EnemyUtilityTriggerIntent>(),
                new List<string>())
        {
        }

        public PreMovementStatePhaseResult(
            List<string> updates,
            List<PlayerActionTransition> playerActionTransitions,
            List<string> rejectedReasons = null,
            List<EnemyUtilityTriggerIntent> utilityTriggerIntents = null,
            List<string> eventLogEntries = null)
        {
            Updates = updates ?? throw new ArgumentNullException(nameof(updates));
            PlayerActionTransitions = playerActionTransitions ?? throw new ArgumentNullException(nameof(playerActionTransitions));
            RejectedReasons = rejectedReasons ?? new List<string>();
            UtilityTriggerIntents = utilityTriggerIntents ?? new List<EnemyUtilityTriggerIntent>();
            EventLogEntries = eventLogEntries ?? new List<string>();
        }

        public List<string> Updates { get; }

        public List<PlayerActionTransition> PlayerActionTransitions { get; }

        public List<string> RejectedReasons { get; }

        public List<EnemyUtilityTriggerIntent> UtilityTriggerIntents { get; }

        public List<string> EventLogEntries { get; }
    }

    internal sealed class PlanPhaseResult
    {
        public PlanPhaseResult(
            List<RawMovementIntent> rawIntents,
            List<MoveIntent> sortedIntents,
            List<ActionGroup> expandedCandidates,
            Dictionary<int, MovementActionPlanPayload> movementActionPlanPayloads,
            List<int> orderedMovementActionPlanIds,
            List<string> rejectedReasons,
            List<Contest> spaceContests,
            List<Contest> jumpLandingSpaceContests,
            List<JumpLandingPlan> jumpLandingPlans,
            Dictionary<int, JumpLandingActionPlanPayload> jumpLandingActionPlanPayloads,
            List<int> orderedJumpLandingActionPlanIds,
            List<string> jumpLandingEvents,
            List<Contest> phaseRelocationSpaceContests,
            List<PhaseRelocationPlan> phaseRelocationPlans,
            Dictionary<int, PhaseRelocationActionPlanPayload> phaseRelocationActionPlanPayloads,
            List<int> orderedPhaseRelocationActionPlanIds,
            List<FrontFaceShieldSourcePresentationExport> frontFaceShieldSourceExports,
            List<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports,
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
            MovementActionPlanPayloads = movementActionPlanPayloads ?? throw new ArgumentNullException(nameof(movementActionPlanPayloads));
            OrderedMovementActionPlanIds = orderedMovementActionPlanIds ?? throw new ArgumentNullException(nameof(orderedMovementActionPlanIds));
            RejectedReasons = rejectedReasons ?? throw new ArgumentNullException(nameof(rejectedReasons));
            SpaceContests = spaceContests ?? throw new ArgumentNullException(nameof(spaceContests));
            JumpLandingSpaceContests = jumpLandingSpaceContests ?? throw new ArgumentNullException(nameof(jumpLandingSpaceContests));
            JumpLandingPlans = jumpLandingPlans ?? throw new ArgumentNullException(nameof(jumpLandingPlans));
            JumpLandingActionPlanPayloads = jumpLandingActionPlanPayloads ?? throw new ArgumentNullException(nameof(jumpLandingActionPlanPayloads));
            OrderedJumpLandingActionPlanIds = orderedJumpLandingActionPlanIds ?? throw new ArgumentNullException(nameof(orderedJumpLandingActionPlanIds));
            JumpLandingEvents = jumpLandingEvents ?? throw new ArgumentNullException(nameof(jumpLandingEvents));
            PhaseRelocationSpaceContests = phaseRelocationSpaceContests ?? throw new ArgumentNullException(nameof(phaseRelocationSpaceContests));
            PhaseRelocationPlans = phaseRelocationPlans ?? throw new ArgumentNullException(nameof(phaseRelocationPlans));
            PhaseRelocationActionPlanPayloads = phaseRelocationActionPlanPayloads ?? throw new ArgumentNullException(nameof(phaseRelocationActionPlanPayloads));
            OrderedPhaseRelocationActionPlanIds = orderedPhaseRelocationActionPlanIds ?? throw new ArgumentNullException(nameof(orderedPhaseRelocationActionPlanIds));
            FrontFaceShieldSourceExports = frontFaceShieldSourceExports ?? throw new ArgumentNullException(nameof(frontFaceShieldSourceExports));
            FrontFaceShieldBlockExports = frontFaceShieldBlockExports ?? throw new ArgumentNullException(nameof(frontFaceShieldBlockExports));
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

        public Dictionary<int, MovementActionPlanPayload> MovementActionPlanPayloads { get; }

        public List<int> OrderedMovementActionPlanIds { get; }

        public List<string> RejectedReasons { get; }

        public List<Contest> SpaceContests { get; }

        public List<Contest> JumpLandingSpaceContests { get; }

        public List<JumpLandingPlan> JumpLandingPlans { get; }

        public Dictionary<int, JumpLandingActionPlanPayload> JumpLandingActionPlanPayloads { get; }

        public List<int> OrderedJumpLandingActionPlanIds { get; }

        public List<string> JumpLandingEvents { get; }

        public List<Contest> PhaseRelocationSpaceContests { get; }

        public List<PhaseRelocationPlan> PhaseRelocationPlans { get; }

        public Dictionary<int, PhaseRelocationActionPlanPayload> PhaseRelocationActionPlanPayloads { get; }

        public List<int> OrderedPhaseRelocationActionPlanIds { get; }

        public List<FrontFaceShieldSourcePresentationExport> FrontFaceShieldSourceExports { get; }

        public List<FrontFaceShieldBlockPresentationExport> FrontFaceShieldBlockExports { get; }

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

    internal sealed class AttackPlanBuildResult
    {
        public AttackPlanBuildResult(
            List<RawAttackIntent> rawAttackIntents,
            List<ActionGroup> expandedCandidates,
            Dictionary<int, AttackActionPlanPayload> actionPlanPayloads,
            List<int> orderedActionPlanIds,
            List<string> rejectedReasons)
        {
            RawAttackIntents = rawAttackIntents ?? throw new ArgumentNullException(nameof(rawAttackIntents));
            ExpandedCandidates = expandedCandidates ?? throw new ArgumentNullException(nameof(expandedCandidates));
            ActionPlanPayloads = actionPlanPayloads ?? throw new ArgumentNullException(nameof(actionPlanPayloads));
            OrderedActionPlanIds = orderedActionPlanIds ?? throw new ArgumentNullException(nameof(orderedActionPlanIds));
            RejectedReasons = rejectedReasons ?? throw new ArgumentNullException(nameof(rejectedReasons));
        }

        public List<RawAttackIntent> RawAttackIntents { get; }

        public List<ActionGroup> ExpandedCandidates { get; }

        public Dictionary<int, AttackActionPlanPayload> ActionPlanPayloads { get; }

        public List<int> OrderedActionPlanIds { get; }

        public List<string> RejectedReasons { get; }
    }

    internal sealed class EnemyUtilityResolveResult
    {
        public EnemyUtilityResolveResult(FinalizationBatch batch, List<string> eventLogEntries)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            EventLogEntries = eventLogEntries ?? throw new ArgumentNullException(nameof(eventLogEntries));
        }

        public FinalizationBatch Batch { get; }

        public List<string> EventLogEntries { get; }
    }

    internal static class EnemyUtilityResolver
    {
        private readonly struct SourceEffectKey : IEquatable<SourceEffectKey>
        {
            public SourceEffectKey(int sourceEntityId, int effectIndex)
            {
                SourceEntityId = sourceEntityId;
                EffectIndex = effectIndex;
            }

            public int SourceEntityId { get; }

            public int EffectIndex { get; }

            public bool Equals(SourceEffectKey other)
            {
                return SourceEntityId == other.SourceEntityId &&
                       EffectIndex == other.EffectIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is SourceEffectKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceEntityId * 397) ^ EffectIndex;
                }
            }
        }

        private enum SummonSkipReason
        {
            SourceInvalid = 0,
            MaxAliveReached = 1,
            NoCandidateCell = 2,
        }

        private enum LockNearbyBoxesSkipReason
        {
            SourceInvalid = 0,
            NoTargetBoxes = 1,
        }

        public static EnemyUtilityResolveResult ResolvePreMovementProjectedEffects(
            WorldSnapshot projectedSnapshot,
            IReadOnlyList<EnemyUtilityTriggerIntent> triggerIntents,
            int tickIndex)
        {
            if (projectedSnapshot == null)
            {
                throw new ArgumentNullException(nameof(projectedSnapshot));
            }

            if (triggerIntents == null)
            {
                throw new ArgumentNullException(nameof(triggerIntents));
            }

            var batch = new FinalizationBatch();
            var eventLogEntries = new List<string>();
            if (triggerIntents.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries);
            }

            var plannedStatesByBoxEntityId = new Dictionary<int, BoxInteractionLockState>();
            for (var intentIndex = 0; intentIndex < triggerIntents.Count; intentIndex++)
            {
                var triggerIntent = triggerIntents[intentIndex];
                if (triggerIntent.EffectKind != EnemyUtilityEffectKind.LockNearbyBoxes)
                {
                    continue;
                }

                ResolveLockNearbyBoxes(
                    projectedSnapshot,
                    triggerIntent,
                    tickIndex,
                    plannedStatesByBoxEntityId,
                    eventLogEntries);
            }

            if (plannedStatesByBoxEntityId.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries);
            }

            var orderedBoxEntityIds = new List<int>(plannedStatesByBoxEntityId.Keys);
            orderedBoxEntityIds.Sort();
            for (var boxIndex = 0; boxIndex < orderedBoxEntityIds.Count; boxIndex++)
            {
                var boxEntityId = orderedBoxEntityIds[boxIndex];
                var mergedState = plannedStatesByBoxEntityId[boxEntityId];
                if (projectedSnapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out var existingState) &&
                    AreBoxInteractionLockStatesEqual(existingState, mergedState))
                {
                    continue;
                }

                batch.SetBoxInteractionLockState(
                    boxEntityId,
                    mergedState,
                    new FinalizationOperationMetadata(
                        TickPhase.Plan,
                        ResolvedActionSemanticKind.None,
                        mergedState.SourceEntityId,
                        actionPlanId: 0));
                if (projectedSnapshot.TryGetEntity(boxEntityId, out var box))
                {
                    eventLogEntries.Add(
                        $"BoxInteractionLockApplied|Source={mergedState.SourceEntityId}|Effect={mergedState.SourceEffectIndex}|Box={boxEntityId}|Cell={box.position}|Expires={mergedState.ExpiresTickExclusive}|BlocksPush={(mergedState.BlocksPush ? 1 : 0)}|BlocksFlip={(mergedState.BlocksFlip ? 1 : 0)}");
                }
            }

            return new EnemyUtilityResolveResult(batch, eventLogEntries);
        }

        public static EnemyUtilityResolveResult ResolvePostAttackEffects(
            WorldSnapshot postAttackSnapshot,
            IReadOnlyList<EnemyUtilityTriggerIntent> triggerIntents,
            int tickIndex,
            EntityIdAllocator entityIdAllocator,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            if (postAttackSnapshot == null)
            {
                throw new ArgumentNullException(nameof(postAttackSnapshot));
            }

            if (triggerIntents == null)
            {
                throw new ArgumentNullException(nameof(triggerIntents));
            }

            if (entityIdAllocator == null)
            {
                throw new ArgumentNullException(nameof(entityIdAllocator));
            }

            var batch = new FinalizationBatch();
            var eventLogEntries = new List<string>();
            if (triggerIntents.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries);
            }

            var reservedSpawnCells = new HashSet<SurfaceCell>();
            var summonedEntries = new List<SummonedEntitySnapshotEntry>();
            postAttackSnapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
            var plannedChildrenBySource = new Dictionary<SourceEffectKey, int>();

            for (var intentIndex = 0; intentIndex < triggerIntents.Count; intentIndex++)
            {
                var triggerIntent = triggerIntents[intentIndex];
                if (triggerIntent.EffectKind != EnemyUtilityEffectKind.SummonMinion)
                {
                    continue;
                }

                ResolveSummonMinion(
                    postAttackSnapshot,
                    triggerIntent,
                    tickIndex,
                    entityIdAllocator,
                    spawnDefaultsByArchetypeId,
                    summonedEntries,
                    plannedChildrenBySource,
                    reservedSpawnCells,
                    batch,
                    eventLogEntries);
            }

            return new EnemyUtilityResolveResult(batch, eventLogEntries);
        }

        private static void ResolveLockNearbyBoxes(
            WorldSnapshot snapshot,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedStatesByBoxEntityId,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, LockNearbyBoxesSkipReason.SourceInvalid, tickIndex);
                return;
            }

            var targetEntityIds = new HashSet<int>();
            var targetCellOffsets = BuildTargetOffsets(source.facing, triggerIntent.EffectRuntime.LockNearbyBoxes);
            for (var offsetIndex = 0; offsetIndex < targetCellOffsets.Count; offsetIndex++)
            {
                var candidateCell = source.position + targetCellOffsets[offsetIndex];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    !TryResolveLockTargetBox(snapshot, candidateCell, out var box))
                {
                    continue;
                }

                targetEntityIds.Add(box.entityId);
            }

            if (targetEntityIds.Count == 0)
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, LockNearbyBoxesSkipReason.NoTargetBoxes, tickIndex);
                return;
            }

            var newExpiresTickExclusive = tickIndex + triggerIntent.EffectRuntime.LockNearbyBoxes.DurationTicks;
            var newState = new BoxInteractionLockState(
                triggerIntent.SourceEntityId,
                triggerIntent.EffectIndex,
                newExpiresTickExclusive,
                triggerIntent.EffectRuntime.LockNearbyBoxes.BlocksPush,
                triggerIntent.EffectRuntime.LockNearbyBoxes.BlocksFlip);
            var orderedTargetEntityIds = new List<int>(targetEntityIds);
            orderedTargetEntityIds.Sort();

            for (var targetIndex = 0; targetIndex < orderedTargetEntityIds.Count; targetIndex++)
            {
                var boxEntityId = orderedTargetEntityIds[targetIndex];
                var hasExistingPlannedState = plannedStatesByBoxEntityId.TryGetValue(boxEntityId, out var plannedState);
                var hasExistingSnapshotState = snapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out var existingState);
                var mergedState = !hasExistingPlannedState && !hasExistingSnapshotState
                    ? newState
                    : MergeBoxInteractionLockStates(
                        hasExistingPlannedState ? plannedState : existingState,
                        newState);
                plannedStatesByBoxEntityId[boxEntityId] = mergedState;
            }
        }

        private static void ResolveSummonMinion(
            WorldSnapshot snapshot,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            EntityIdAllocator entityIdAllocator,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            IDictionary<SourceEffectKey, int> plannedChildrenBySource,
            ISet<SurfaceCell> reservedSpawnCells,
            FinalizationBatch batch,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex: 0, SummonSkipReason.SourceInvalid);
                return;
            }

            var summonRuntime = triggerIntent.EffectRuntime.Summon;
            var spawnDefaults = ResolveArchetypeSpawnDefaults(summonRuntime.SummonedArchetypeId, spawnDefaultsByArchetypeId);
            var minionHp = summonRuntime.OverrideHp
                ? summonRuntime.HpOverride
                : spawnDefaults.Hp;
            var initialAiMode = spawnDefaults.InitialAiMode;
            var enemyDefinitionBindingState = new EnemyDefinitionBindingState(summonRuntime.SummonedArchetypeId);
            var sourceKey = new SourceEffectKey(triggerIntent.SourceEntityId, triggerIntent.EffectIndex);
            for (var spawnIndex = 0; spawnIndex < summonRuntime.SpawnCountPerTrigger; spawnIndex++)
            {
                var aliveChildren = CountAliveChildren(
                    snapshot,
                    summonedEntries,
                    triggerIntent.SourceEntityId,
                    triggerIntent.EffectIndex);
                var plannedChildren = plannedChildrenBySource.TryGetValue(sourceKey, out var currentPlannedChildren)
                    ? currentPlannedChildren
                    : 0;
                if (aliveChildren + plannedChildren >= summonRuntime.MaxAliveChildren)
                {
                    AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex, SummonSkipReason.MaxAliveReached);
                    continue;
                }

                if (!TrySelectCandidateCell(
                        snapshot,
                        source,
                        summonRuntime,
                        reservedSpawnCells,
                        out var spawnCell))
                {
                    AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex, SummonSkipReason.NoCandidateCell);
                    continue;
                }

                var summonedEntityState = new SummonedEntityState(triggerIntent.SourceEntityId, triggerIntent.EffectIndex);
                var spawnedEntity = CreateSummonedMinionEntity(
                    entityIdAllocator.AllocateEntityId(),
                    source,
                    spawnCell,
                    minionHp,
                    initialAiMode,
                    tickIndex);
                batch.SpawnEntity(
                    spawnedEntity,
                    new FinalizationOperationMetadata(
                        TickPhase.Resolve,
                        ResolvedActionSemanticKind.None,
                        triggerIntent.SourceEntityId,
                        actionPlanId: 0),
                    hasSummonedEntityState: true,
                    summonedEntityState: summonedEntityState,
                    hasEnemyDefinitionBindingState: true,
                    enemyDefinitionBindingState: enemyDefinitionBindingState);
                reservedSpawnCells.Add(spawnCell);
                plannedChildrenBySource[sourceKey] = plannedChildren + 1;
                AppendCommittedEvent(
                    eventLogEntries,
                    triggerIntent,
                    tickIndex,
                    spawnIndex,
                    spawnedEntity,
                    summonRuntime);
            }
        }

        private static bool TryGetValidSource(
            WorldSnapshot snapshot,
            int sourceEntityId,
            out EntityState source)
        {
            if (!snapshot.TryGetEntity(sourceEntityId, out source))
            {
                return false;
            }

            return EnemyParticipationPolicy.IsControllableParticipant(snapshot, source) &&
                   source.position.face == snapshot.Topology.BottomFace;
        }

        private static int CountAliveChildren(
            WorldSnapshot snapshot,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            int sourceEntityId,
            int effectIndex)
        {
            var aliveCount = 0;
            for (var i = 0; i < summonedEntries.Count; i++)
            {
                var entry = summonedEntries[i];
                if (entry.State.SourceEntityId != sourceEntityId ||
                    entry.State.SourceEffectIndex != effectIndex ||
                    !snapshot.TryGetEntity(entry.EntityId, out var child) ||
                    child.hp <= 0 ||
                    child.markedForDeath ||
                    child.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                aliveCount++;
            }

            return aliveCount;
        }

        private static bool TrySelectCandidateCell(
            WorldSnapshot snapshot,
            in EntityState source,
            in SummonMinionRuntime summonRuntime,
            ISet<SurfaceCell> reservedSpawnCells,
            out SurfaceCell spawnCell)
        {
            var candidateOffsets = BuildCandidateOffsets(source.facing);
            for (var i = 0; i < candidateOffsets.Count; i++)
            {
                var candidateCell = source.position + candidateOffsets[i];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    snapshot.IsTerrainBlockedForUnit(candidateCell))
                {
                    continue;
                }

                if (summonRuntime.RequireNoSolidAtSpawnCell &&
                    snapshot.TryGetSolidSemanticAt(candidateCell, out _))
                {
                    continue;
                }

                if (snapshot.TryGetPlacementBlocker(EntityType.Unit, candidateCell, ignoredEntityId: 0, out _))
                {
                    continue;
                }

                if (summonRuntime.RequireNoUnitAtSpawnCell &&
                    snapshot.HasAnyUnitAt(candidateCell))
                {
                    continue;
                }

                if (reservedSpawnCells.Contains(candidateCell))
                {
                    continue;
                }

                spawnCell = candidateCell;
                return true;
            }

            spawnCell = default;
            return false;
        }

        private static List<Vector2Int> BuildCandidateOffsets(Direction facing)
        {
            if (!EnemyMovementStrategyShared.TryResolveDelta(facing, out var forward) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnRight(facing), out var right) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnLeft(facing), out var left) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnBack(facing), out var back))
            {
                return new List<Vector2Int>
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, -1),
                };
            }

            return new List<Vector2Int>
            {
                forward,
                right,
                left,
                back,
            };
        }

        private static List<Vector2Int> BuildTargetOffsets(
            Direction facing,
            in LockNearbyBoxesRuntime lockRuntime)
        {
            switch (lockRuntime.TargetPattern)
            {
                case BoxLockTargetPattern.OrthogonalAdjacent4:
                    return BuildCandidateOffsets(facing);

                case BoxLockTargetPattern.ManhattanRadius:
                    return BuildManhattanOffsets(lockRuntime.Radius, lockRuntime.IncludeSourceCell);

                default:
                    throw new ArgumentOutOfRangeException(nameof(lockRuntime), lockRuntime.TargetPattern, "Unsupported lock nearby boxes target pattern.");
            }
        }

        private static List<Vector2Int> BuildManhattanOffsets(int radius, bool includeSourceCell)
        {
            var offsets = new List<Vector2Int>();
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (distance > radius ||
                        (!includeSourceCell && dx == 0 && dy == 0))
                    {
                        continue;
                    }

                    offsets.Add(new Vector2Int(dx, dy));
                }
            }

            offsets.Sort(CompareManhattanOffsets);
            return offsets;
        }

        private static int CompareManhattanOffsets(Vector2Int left, Vector2Int right)
        {
            var distanceComparison = (Mathf.Abs(left.x) + Mathf.Abs(left.y)).CompareTo(Mathf.Abs(right.x) + Mathf.Abs(right.y));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var yComparison = right.y.CompareTo(left.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return left.x.CompareTo(right.x);
        }

        private static bool TryResolveLockTargetBox(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            out EntityState box)
        {
            if (!snapshot.TryGetSolidSemanticAt(cell, out var solidSemantic) ||
                solidSemantic.Kind != SolidKind.Box)
            {
                box = default;
                return false;
            }

            box = solidSemantic.Entity;
            return box.type == EntityType.Box &&
                   box.hp > 0 &&
                   !box.markedForDeath &&
                   box.boardPresence == EntityBoardPresence.Occupying;
        }

        private static BoxInteractionLockState MergeBoxInteractionLockStates(
            in BoxInteractionLockState existingState,
            in BoxInteractionLockState newState)
        {
            var mergedExpires = Math.Max(existingState.ExpiresTickExclusive, newState.ExpiresTickExclusive);
            var useNewSource =
                newState.ExpiresTickExclusive > existingState.ExpiresTickExclusive ||
                (newState.ExpiresTickExclusive == existingState.ExpiresTickExclusive &&
                 (newState.SourceEntityId < existingState.SourceEntityId ||
                  (newState.SourceEntityId == existingState.SourceEntityId &&
                   newState.SourceEffectIndex < existingState.SourceEffectIndex)));

            return new BoxInteractionLockState(
                useNewSource ? newState.SourceEntityId : existingState.SourceEntityId,
                useNewSource ? newState.SourceEffectIndex : existingState.SourceEffectIndex,
                mergedExpires,
                existingState.BlocksPush || newState.BlocksPush,
                existingState.BlocksFlip || newState.BlocksFlip);
        }

        private static bool AreBoxInteractionLockStatesEqual(
            in BoxInteractionLockState left,
            in BoxInteractionLockState right)
        {
            return left.SourceEntityId == right.SourceEntityId &&
                   left.SourceEffectIndex == right.SourceEffectIndex &&
                   left.ExpiresTickExclusive == right.ExpiresTickExclusive &&
                   left.BlocksPush == right.BlocksPush &&
                   left.BlocksFlip == right.BlocksFlip;
        }

        private static Direction TurnRight(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnLeft(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Left,
                Direction.Left => Direction.Down,
                Direction.Down => Direction.Right,
                Direction.Right => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnBack(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }

        private static EnemyUnitSpawnDefaultsRuntime ResolveArchetypeSpawnDefaults(
            EnemyUnitArchetypeId archetypeId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            archetypeId.Validate(nameof(archetypeId));
            if (spawnDefaultsByArchetypeId != null &&
                spawnDefaultsByArchetypeId.TryGetValue(archetypeId, out var spawnDefaults))
            {
                return spawnDefaults;
            }

            throw new InvalidOperationException(
                $"Missing enemy unit spawn defaults for archetype '{archetypeId}'.");
        }

        private static EntityState CreateSummonedMinionEntity(
            int entityId,
            in EntityState source,
            SurfaceCell spawnCell,
            int minionHp,
            EnemyAiMode initialAiMode,
            int tickIndex)
        {
            return new EntityState
            {
                entityId = entityId,
                position = spawnCell,
                hp = minionHp,
                maxHp = minionHp,
                teamId = source.teamId,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = source.facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = tickIndex,
                aiMode = initialAiMode,
                aiStateTimer = 0,
                enemyLocomotionCooldownTicks = 0,
            };
        }

        private static void AppendCommittedEvent(
            List<string> eventLogEntries,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            int spawnIndex,
            in EntityState spawnedEntity,
            in SummonMinionRuntime summonRuntime)
        {
            eventLogEntries.Add(
                $"SummonCommitted|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|SpawnIndex={spawnIndex}|Spawned={spawnedEntity.entityId}|Pos=({spawnedEntity.position.x},{spawnedEntity.position.y})|Archetype={summonRuntime.SummonedArchetypeId}|Tick={tickIndex}");
        }

        private static void AppendSkipEvent(
            List<string> eventLogEntries,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            int spawnIndex,
            SummonSkipReason reason)
        {
            eventLogEntries.Add(
                $"SummonSkipped|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|SpawnIndex={spawnIndex}|Reason={reason}|Tick={tickIndex}");
        }

        private static void AppendLockSkipEvent(
            List<string> eventLogEntries,
            in EnemyUtilityTriggerIntent triggerIntent,
            LockNearbyBoxesSkipReason reason,
            int tickIndex)
        {
            eventLogEntries.Add(
                $"BoxInteractionLockSkipped|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|Reason={reason}|Tick={tickIndex}");
        }
    }

    internal sealed class JumpLandingPlan
    {
        public JumpLandingPlan(
            int actionPlanId,
            int contestId,
            int sourceId,
            int priority,
            int targetId,
            SurfaceCell destinationCell,
            string landingRule,
            EnemyJumpRuntimeState successState,
            EnemyJumpRuntimeState retryState,
            JumpLandingKind landingKind)
        {
            ActionPlanId = actionPlanId;
            ContestId = contestId;
            SourceId = sourceId;
            Priority = priority;
            TargetId = targetId;
            DestinationCell = destinationCell;
            LandingRule = landingRule ?? string.Empty;
            SuccessState = successState;
            RetryState = retryState;
            LandingKind = landingKind;
        }

        public int ActionPlanId { get; }

        public int ContestId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int TargetId { get; }

        public SurfaceCell DestinationCell { get; }

        public string LandingRule { get; }

        public EnemyJumpRuntimeState SuccessState { get; }

        public EnemyJumpRuntimeState RetryState { get; }

        public JumpLandingKind LandingKind { get; }
    }

    internal sealed class PhaseRelocationPlan
    {
        public PhaseRelocationPlan(
            int actionPlanId,
            int contestId,
            int sourceId,
            int priority,
            int lockedTargetEntityId,
            Direction direction,
            SurfaceCell destinationCell,
            string ruleLabel)
        {
            ActionPlanId = actionPlanId;
            ContestId = contestId;
            SourceId = sourceId;
            Priority = priority;
            LockedTargetEntityId = lockedTargetEntityId;
            Direction = direction;
            DestinationCell = destinationCell;
            RuleLabel = ruleLabel ?? string.Empty;
        }

        public int ActionPlanId { get; }

        public int ContestId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int LockedTargetEntityId { get; }

        public Direction Direction { get; }

        public SurfaceCell DestinationCell { get; }

        public string RuleLabel { get; }
    }

    internal enum SpawnSourceKind
    {
        Attack = 0,
    }

    internal enum JumpLandingKind
    {
        ExactStack = 0,
        Contested = 1,
        RetryOnly = 2,
        CrushBoxAndLand = 3,
    }

    internal abstract class ActionPlanPayload
    {
        protected ActionPlanPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            ResolvedActionSemanticKind semanticKind)
        {
            ActionPlanId = actionPlanId;
            IntentId = intentId;
            SourceActorEntityId = sourceActorEntityId;
            Priority = priority;
            SemanticKind = semanticKind;
        }

        public int ActionPlanId { get; }

        public int IntentId { get; }

        public int SourceActorEntityId { get; }

        public int Priority { get; }

        public ResolvedActionSemanticKind SemanticKind { get; }
    }

    internal readonly struct MoveWritePayload
    {
        public MoveWritePayload(int entityId, SurfaceCell sourceCell, SurfaceCell destinationCell, Direction facingAfterMove)
        {
            EntityId = entityId;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            FacingAfterMove = facingAfterMove;
        }

        public int EntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public Direction FacingAfterMove { get; }
    }

    internal readonly struct MovementEdge
    {
        public MovementEdge(SurfaceCell fromCell, SurfaceCell toCell)
        {
            FromCell = fromCell;
            ToCell = toCell;
        }

        public SurfaceCell FromCell { get; }

        public SurfaceCell ToCell { get; }
    }

    internal enum MovementReservationKind
    {
        None = 0,
        Vertex = 1,
        Edge = 2,
    }

    internal enum MovementBlockingType
    {
        Blocking = 0,
        NonBlocking = 1,
        PassThrough = 2,
    }

    internal enum MovementCandidateKind
    {
        Move = 0,
        Push = 1,
        Flip = 2,
        BoxImpact = 3,
        ProjectileImpact = 4,
        Stop = 5,
        Item = 6,
    }

    // Narrow internal contract for current Push / Sliding Push / Flip box-impact
    // resolve only. This is not a generalized impact framework seed.
    internal enum ImpactDispositionPolicyKind
    {
        PushLike = 0,
        Flip = 1,
    }

    internal enum ImpactDispositionKind
    {
        Stay = 0,
        FollowThrough = 1,
        DestroySelf = 2,
    }

    internal readonly struct ImpactDispositionResolutionRecord
    {
        public ImpactDispositionResolutionRecord(
            int actionPlanId,
            int impactSourceEntityId,
            int impactTargetEntityId,
            SurfaceCell impactCell,
            ImpactDispositionPolicyKind policyKind,
            ImpactDispositionKind dispositionKind,
            bool targetDestroyed,
            bool followThroughLegalityChecked,
            bool followThroughAccepted)
        {
            ActionPlanId = actionPlanId;
            ImpactSourceEntityId = impactSourceEntityId;
            ImpactTargetEntityId = impactTargetEntityId;
            ImpactCell = impactCell;
            PolicyKind = policyKind;
            DispositionKind = dispositionKind;
            TargetDestroyed = targetDestroyed;
            FollowThroughLegalityChecked = followThroughLegalityChecked;
            FollowThroughAccepted = followThroughAccepted;
        }

        public int ActionPlanId { get; }

        public int ImpactSourceEntityId { get; }

        public int ImpactTargetEntityId { get; }

        public SurfaceCell ImpactCell { get; }

        public ImpactDispositionPolicyKind PolicyKind { get; }

        public ImpactDispositionKind DispositionKind { get; }

        public bool TargetDestroyed { get; }

        public bool FollowThroughLegalityChecked { get; }

        public bool FollowThroughAccepted { get; }
    }

    internal readonly struct BoardPresenceWritePayload
    {
        public BoardPresenceWritePayload(int entityId, EntityBoardPresence boardPresence, TickEntityExitCause exitCauseHint)
        {
            EntityId = entityId;
            BoardPresence = boardPresence;
            ExitCauseHint = exitCauseHint;
        }

        public int EntityId { get; }

        public EntityBoardPresence BoardPresence { get; }

        public TickEntityExitCause ExitCauseHint { get; }
    }

    internal readonly struct FacingWritePayload
    {
        public FacingWritePayload(int entityId, Direction facing)
        {
            EntityId = entityId;
            Facing = facing;
        }

        public int EntityId { get; }

        public Direction Facing { get; }
    }

    internal readonly struct BoxKineticOwnerWritePayload
    {
        public BoxKineticOwnerWritePayload(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            EntityId = entityId;
            InstigatorEntityId = instigatorEntityId;
            InstigatorTeamId = instigatorTeamId;
        }

        public int EntityId { get; }

        public int InstigatorEntityId { get; }

        public int InstigatorTeamId { get; }
    }

    internal readonly struct TopologyWritePayload
    {
        public TopologyWritePayload(CubeTopologyState topology, CubeRotationKind rotationKind)
        {
            Topology = topology;
            RotationKind = rotationKind;
        }

        public CubeTopologyState Topology { get; }

        public CubeRotationKind RotationKind { get; }
    }

    internal readonly struct ExecutionLockWritePayload
    {
        public ExecutionLockWritePayload(int entityId, EntityExecutionLockState executionLockState)
        {
            EntityId = entityId;
            ExecutionLockState = executionLockState;
        }

        public int EntityId { get; }

        public EntityExecutionLockState ExecutionLockState { get; }
    }

    internal readonly struct EnemyLocomotionWritePayload
    {
        public EnemyLocomotionWritePayload(int entityId, int cooldownTicks)
        {
            EntityId = entityId;
            CooldownTicks = cooldownTicks;
        }

        public int EntityId { get; }

        public int CooldownTicks { get; }
    }

    internal readonly struct EnemyPatrolWritePayload
    {
        public EnemyPatrolWritePayload(int entityId, EnemyPatrolRuntimeState enemyPatrolState)
        {
            EntityId = entityId;
            EnemyPatrolState = enemyPatrolState;
        }

        public int EntityId { get; }

        public EnemyPatrolRuntimeState EnemyPatrolState { get; }
    }

    internal readonly struct PlayerControlWritePayload
    {
        public PlayerControlWritePayload(int entityId, PlayerControlState playerControlState)
        {
            EntityId = entityId;
            PlayerControlState = playerControlState;
        }

        public int EntityId { get; }

        public PlayerControlState PlayerControlState { get; }
    }

    internal readonly struct DestroyWritePayload
    {
        public DestroyWritePayload(int targetEntityId, DestroyCondition destroyCondition, TickEntityExitCause exitCauseHint)
        {
            TargetEntityId = targetEntityId;
            DestroyCondition = destroyCondition;
            ExitCauseHint = exitCauseHint;
        }

        public int TargetEntityId { get; }

        public DestroyCondition DestroyCondition { get; }

        public TickEntityExitCause ExitCauseHint { get; }
    }

    internal readonly struct MovementImpactReservationPayload
    {
        public MovementImpactReservationPayload(
            int sourceEntityId,
            int attackSourceEntityId,
            SurfaceCell sourceCell,
            int targetEntityId,
            SurfaceCell impactCell,
            int damageAmount,
            int sequence,
            SurfaceCell contingentDestinationCell,
            SurfaceCell contingentSourceCell,
            Direction contingentFacing,
            bool hasContingentStateChange,
            EntityPhaseState contingentState,
            int contingentStateTimer,
            bool hasSourceFacing,
            int sourceFacingEntityId,
            Direction sourceFacing,
            ImpactDispositionPolicyKind dispositionPolicyKind,
            ResolvedActionSemanticKind contingentSemanticKind)
        {
            SourceEntityId = sourceEntityId;
            AttackSourceEntityId = attackSourceEntityId;
            SourceCell = sourceCell;
            TargetEntityId = targetEntityId;
            ImpactCell = impactCell;
            DamageAmount = damageAmount;
            Sequence = sequence;
            ContingentDestinationCell = contingentDestinationCell;
            ContingentSourceCell = contingentSourceCell;
            ContingentFacing = contingentFacing;
            HasContingentStateChange = hasContingentStateChange;
            ContingentState = contingentState;
            ContingentStateTimer = contingentStateTimer;
            HasSourceFacing = hasSourceFacing;
            SourceFacingEntityId = sourceFacingEntityId;
            SourceFacing = sourceFacing;
            DispositionPolicyKind = dispositionPolicyKind;
            ContingentSemanticKind = contingentSemanticKind;
        }

        public int SourceEntityId { get; }

        public int AttackSourceEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public int TargetEntityId { get; }

        public SurfaceCell ImpactCell { get; }

        public int DamageAmount { get; }

        public int Sequence { get; }

        public SurfaceCell ContingentDestinationCell { get; }

        public SurfaceCell ContingentSourceCell { get; }

        public Direction ContingentFacing { get; }

        public bool HasContingentStateChange { get; }

        public EntityPhaseState ContingentState { get; }

        public int ContingentStateTimer { get; }

        public bool HasSourceFacing { get; }

        public int SourceFacingEntityId { get; }

        public Direction SourceFacing { get; }

        public ImpactDispositionPolicyKind DispositionPolicyKind { get; }

        public ResolvedActionSemanticKind ContingentSemanticKind { get; }
    }

    internal readonly struct MovementDeferredImpactPayload
    {
        public MovementDeferredImpactPayload(
            int sourceEntityId,
            SurfaceCell impactCell,
            int damageAmount,
            int sequence)
        {
            SourceEntityId = sourceEntityId;
            ImpactCell = impactCell;
            DamageAmount = damageAmount;
            Sequence = sequence;
        }

        public int SourceEntityId { get; }

        public SurfaceCell ImpactCell { get; }

        public int DamageAmount { get; }

        public int Sequence { get; }
    }

    internal readonly struct KinematicMotionOutcome
    {
        public KinematicMotionOutcome(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell resolvedAnchorCell,
            KinematicOffset2 resolvedLocalOffset,
            KinematicVelocity2 resolvedVelocity,
            UnitKinematicRuntimeState resolvedState,
            bool anchorChanged,
            bool blocked,
            KinematicSweepRejectionReason rejectedBy)
        {
            EntityId = entityId;
            SourceAnchorCell = sourceAnchorCell;
            SourceLocalOffset = sourceLocalOffset;
            ResolvedAnchorCell = resolvedAnchorCell;
            ResolvedLocalOffset = resolvedLocalOffset;
            ResolvedVelocity = resolvedVelocity;
            ResolvedState = resolvedState;
            AnchorChanged = anchorChanged;
            Blocked = blocked;
            RejectedBy = rejectedBy;
        }

        public int EntityId { get; }

        public SurfaceCell SourceAnchorCell { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public SurfaceCell ResolvedAnchorCell { get; }

        public KinematicOffset2 ResolvedLocalOffset { get; }

        public KinematicVelocity2 ResolvedVelocity { get; }

        public UnitKinematicRuntimeState ResolvedState { get; }

        public bool AnchorChanged { get; }

        public bool Blocked { get; }

        public KinematicSweepRejectionReason RejectedBy { get; }
    }

    internal readonly struct UnitLocomotionIntent
    {
        public UnitLocomotionIntent(
            int entityId,
            KinematicVelocity2 requestedDelta,
            MotionMode requestedMode,
            ForcedMotionOp forcedOp = ForcedMotionOp.None)
        {
            EntityId = entityId;
            RequestedDelta = requestedDelta;
            RequestedMode = requestedMode;
            ForcedOp = forcedOp;
        }

        public int EntityId { get; }

        public KinematicVelocity2 RequestedDelta { get; }

        public MotionMode RequestedMode { get; }

        public ForcedMotionOp ForcedOp { get; }
    }

    internal readonly struct ForcedMotionOpRequest
    {
        public ForcedMotionOpRequest(
            int entityId,
            ForcedMotionOp forcedOp,
            KinematicVelocity2 requestedDelta,
            int startTick)
        {
            EntityId = entityId;
            ForcedOp = forcedOp;
            RequestedDelta = requestedDelta;
            StartTick = startTick;
        }

        public int EntityId { get; }

        public ForcedMotionOp ForcedOp { get; }

        public KinematicVelocity2 RequestedDelta { get; }

        public int StartTick { get; }
    }

    internal readonly struct MotionInterruptRecord
    {
        public MotionInterruptRecord(int entityId, MotionInterruptPolicy policy, int sourceEntityId)
        {
            EntityId = entityId;
            Policy = policy;
            SourceEntityId = sourceEntityId;
        }

        public int EntityId { get; }

        public MotionInterruptPolicy Policy { get; }

        public int SourceEntityId { get; }
    }

    internal sealed class MovementActionPlanPayload : ActionPlanPayload
    {
        public MovementActionPlanPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            ResolvedActionSemanticKind semanticKind,
            MovementCandidateKind movementCandidateKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            bool hasMovementEdge,
            MovementEdge movementEdge,
            IReadOnlyList<int> affectedEntityIds,
            MovementReservationKind reservationKind,
            MovementBlockingType blockingType,
            IReadOnlyList<StateChangeWritePayload> stateChangeWrites,
            IReadOnlyList<MoveWritePayload> moveWrites,
            IReadOnlyList<BoardPresenceWritePayload> boardPresenceWrites,
            IReadOnlyList<FacingWritePayload> facingWrites,
            IReadOnlyList<BoxKineticOwnerWritePayload> boxKineticOwnerWrites,
            IReadOnlyList<TopologyWritePayload> topologyWrites,
            IReadOnlyList<ExecutionLockWritePayload> executionLockWrites,
            IReadOnlyList<EnemyLocomotionWritePayload> enemyLocomotionWrites,
            IReadOnlyList<EnemyPatrolWritePayload> enemyPatrolWrites,
            IReadOnlyList<PlayerControlWritePayload> playerControlWrites,
            IReadOnlyList<DestroyWritePayload> destroyWrites,
            bool hasImpactReservationPayload,
            MovementImpactReservationPayload impactReservationPayload,
            bool hasDeferredImpactPayload,
            MovementDeferredImpactPayload deferredImpactPayload,
            IReadOnlyList<KinematicMotionOutcome> kinematicMotionOutcomes = null)
            : base(actionPlanId, intentId, sourceActorEntityId, priority, semanticKind)
        {
            MovementCandidateKind = movementCandidateKind;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            HasMovementEdge = hasMovementEdge;
            MovementEdge = movementEdge;
            AffectedEntityIds = affectedEntityIds ?? throw new ArgumentNullException(nameof(affectedEntityIds));
            ReservationKind = reservationKind;
            BlockingType = blockingType;
            StateChangeWrites = stateChangeWrites ?? throw new ArgumentNullException(nameof(stateChangeWrites));
            MoveWrites = moveWrites ?? throw new ArgumentNullException(nameof(moveWrites));
            BoardPresenceWrites = boardPresenceWrites ?? throw new ArgumentNullException(nameof(boardPresenceWrites));
            FacingWrites = facingWrites ?? throw new ArgumentNullException(nameof(facingWrites));
            BoxKineticOwnerWrites = boxKineticOwnerWrites ?? throw new ArgumentNullException(nameof(boxKineticOwnerWrites));
            TopologyWrites = topologyWrites ?? throw new ArgumentNullException(nameof(topologyWrites));
            ExecutionLockWrites = executionLockWrites ?? throw new ArgumentNullException(nameof(executionLockWrites));
            EnemyLocomotionWrites = enemyLocomotionWrites ?? throw new ArgumentNullException(nameof(enemyLocomotionWrites));
            EnemyPatrolWrites = enemyPatrolWrites ?? throw new ArgumentNullException(nameof(enemyPatrolWrites));
            PlayerControlWrites = playerControlWrites ?? throw new ArgumentNullException(nameof(playerControlWrites));
            DestroyWrites = destroyWrites ?? throw new ArgumentNullException(nameof(destroyWrites));
            HasImpactReservationPayload = hasImpactReservationPayload;
            ImpactReservationPayload = impactReservationPayload;
            HasDeferredImpactPayload = hasDeferredImpactPayload;
            DeferredImpactPayload = deferredImpactPayload;
            KinematicMotionOutcomes = kinematicMotionOutcomes ?? Array.Empty<KinematicMotionOutcome>();
        }

        public MovementCandidateKind MovementCandidateKind { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public bool HasMovementEdge { get; }

        public MovementEdge MovementEdge { get; }

        public IReadOnlyList<int> AffectedEntityIds { get; }

        public MovementReservationKind ReservationKind { get; }

        public MovementBlockingType BlockingType { get; }

        public IReadOnlyList<StateChangeWritePayload> StateChangeWrites { get; }

        public IReadOnlyList<MoveWritePayload> MoveWrites { get; }

        public IReadOnlyList<BoardPresenceWritePayload> BoardPresenceWrites { get; }

        public IReadOnlyList<FacingWritePayload> FacingWrites { get; }

        public IReadOnlyList<BoxKineticOwnerWritePayload> BoxKineticOwnerWrites { get; }

        public IReadOnlyList<TopologyWritePayload> TopologyWrites { get; }

        public IReadOnlyList<ExecutionLockWritePayload> ExecutionLockWrites { get; }

        public IReadOnlyList<EnemyLocomotionWritePayload> EnemyLocomotionWrites { get; }

        public IReadOnlyList<EnemyPatrolWritePayload> EnemyPatrolWrites { get; }

        public IReadOnlyList<PlayerControlWritePayload> PlayerControlWrites { get; }

        public IReadOnlyList<DestroyWritePayload> DestroyWrites { get; }

        public bool HasImpactReservationPayload { get; }

        public MovementImpactReservationPayload ImpactReservationPayload { get; }

        public bool HasDeferredImpactPayload { get; }

        public MovementDeferredImpactPayload DeferredImpactPayload { get; }

        public IReadOnlyList<KinematicMotionOutcome> KinematicMotionOutcomes { get; }
    }

    internal readonly struct StateChangeWritePayload
    {
        public StateChangeWritePayload(int entityId, EntityPhaseState state, int stateTimer)
        {
            EntityId = entityId;
            State = state;
            StateTimer = stateTimer;
        }

        public int EntityId { get; }

        public EntityPhaseState State { get; }

        public int StateTimer { get; }
    }

    internal readonly struct DamageWritePayload
    {
        public DamageWritePayload(
            int targetEntityId,
            int amount,
            bool hasPlayerDamageState,
            PlayerDamageState playerDamageState,
            AttackSourceKind attackSourceKind)
        {
            TargetEntityId = targetEntityId;
            Amount = amount;
            HasPlayerDamageState = hasPlayerDamageState;
            PlayerDamageState = playerDamageState;
            AttackSourceKind = attackSourceKind;
        }

        public int TargetEntityId { get; }

        public int Amount { get; }

        public bool HasPlayerDamageState { get; }

        public PlayerDamageState PlayerDamageState { get; }

        public AttackSourceKind AttackSourceKind { get; }
    }

    internal readonly struct SpawnWritePayload
    {
        public SpawnWritePayload(EntityState entityTemplate, SpawnSourceKind spawnSourceKind)
        {
            EntityTemplate = entityTemplate;
            SpawnSourceKind = spawnSourceKind;
        }

        public EntityState EntityTemplate { get; }

        public SpawnSourceKind SpawnSourceKind { get; }
    }

    internal readonly struct DelayedEnqueueWritePayload
    {
        public DelayedEnqueueWritePayload(
            int targetEntityId,
            int damage,
            int tickGenerated,
            int executeAtTick,
            int effectSequence)
        {
            TargetEntityId = targetEntityId;
            Damage = damage;
            TickGenerated = tickGenerated;
            ExecuteAtTick = executeAtTick;
            EffectSequence = effectSequence;
        }

        public int TargetEntityId { get; }

        public int Damage { get; }

        public int TickGenerated { get; }

        public int ExecuteAtTick { get; }

        public int EffectSequence { get; }
    }

    internal sealed class AttackActionPlanPayload : ActionPlanPayload
    {
        public AttackActionPlanPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            ResolvedActionSemanticKind semanticKind,
            IReadOnlyList<StateChangeWritePayload> stateChangeWrites,
            IReadOnlyList<DamageWritePayload> damageWrites,
            IReadOnlyList<SpawnWritePayload> spawnWrites,
            IReadOnlyList<DestroyWritePayload> destroyWrites,
            IReadOnlyList<DelayedEnqueueWritePayload> delayedEnqueueWrites)
            : base(actionPlanId, intentId, sourceActorEntityId, priority, semanticKind)
        {
            StateChangeWrites = stateChangeWrites ?? throw new ArgumentNullException(nameof(stateChangeWrites));
            DamageWrites = damageWrites ?? throw new ArgumentNullException(nameof(damageWrites));
            SpawnWrites = spawnWrites ?? throw new ArgumentNullException(nameof(spawnWrites));
            DestroyWrites = destroyWrites ?? throw new ArgumentNullException(nameof(destroyWrites));
            DelayedEnqueueWrites = delayedEnqueueWrites ?? throw new ArgumentNullException(nameof(delayedEnqueueWrites));
        }

        public IReadOnlyList<StateChangeWritePayload> StateChangeWrites { get; }

        public IReadOnlyList<DamageWritePayload> DamageWrites { get; }

        public IReadOnlyList<SpawnWritePayload> SpawnWrites { get; }

        public IReadOnlyList<DestroyWritePayload> DestroyWrites { get; }

        public IReadOnlyList<DelayedEnqueueWritePayload> DelayedEnqueueWrites { get; }
    }

    internal sealed class JumpLandingActionPlanPayload : ActionPlanPayload
    {
        public JumpLandingActionPlanPayload(
            int actionPlanId,
            int sourceActorEntityId,
            int priority,
            JumpLandingKind landingKind,
            SurfaceCell destinationCell,
            int contestedTargetEntityId,
            string landingRule,
            EnemyJumpRuntimeState successJumpState,
            EnemyJumpRuntimeState retryJumpState)
            : base(actionPlanId, intentId: 0, sourceActorEntityId, priority, ResolvedActionSemanticKind.JumpLanding)
        {
            LandingKind = landingKind;
            DestinationCell = destinationCell;
            ContestedTargetEntityId = contestedTargetEntityId;
            LandingRule = landingRule ?? string.Empty;
            SuccessJumpState = successJumpState;
            RetryJumpState = retryJumpState;
        }

        public JumpLandingKind LandingKind { get; }

        public SurfaceCell DestinationCell { get; }

        public int ContestedTargetEntityId { get; }

        public string LandingRule { get; }

        public EnemyJumpRuntimeState SuccessJumpState { get; }

        public EnemyJumpRuntimeState RetryJumpState { get; }
    }

    internal sealed class PhaseRelocationActionPlanPayload : ActionPlanPayload
    {
        public PhaseRelocationActionPlanPayload(
            int actionPlanId,
            int sourceActorEntityId,
            int priority,
            int lockedTargetEntityId,
            Direction direction,
            SurfaceCell destinationCell,
            string ruleLabel)
            : base(actionPlanId, intentId: 0, sourceActorEntityId, priority, ResolvedActionSemanticKind.Move)
        {
            LockedTargetEntityId = lockedTargetEntityId;
            Direction = direction;
            DestinationCell = destinationCell;
            RuleLabel = ruleLabel ?? string.Empty;
        }

        public int LockedTargetEntityId { get; }

        public Direction Direction { get; }

        public SurfaceCell DestinationCell { get; }

        public string RuleLabel { get; }
    }

    internal enum ContestKind
    {
        Plan = 0,
        Space = 1,
        Impact = 2,
        Damage = 3,
        Destroy = 4,
    }

    internal readonly struct EdgeReservation
    {
        public EdgeReservation(int entityId, SurfaceCell from, SurfaceCell to, int groupId)
        {
            EntityId = entityId;
            From = from;
            To = to;
            GroupId = groupId;
        }

        public int EntityId { get; }

        public SurfaceCell From { get; }

        public SurfaceCell To { get; }

        public int GroupId { get; }
    }

    internal readonly struct ExclusiveGroupReservation
    {
        public ExclusiveGroupReservation(int groupId, ActionGroupKind groupKind, bool hasTopologyChange)
        {
            GroupId = groupId;
            GroupKind = groupKind;
            HasTopologyChange = hasTopologyChange;
        }

        public int GroupId { get; }

        public ActionGroupKind GroupKind { get; }

        public bool HasTopologyChange { get; }
    }

    internal readonly struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
    {
        public UndirectedEdgeKey(SurfaceCell first, SurfaceCell second)
        {
            First = first;
            Second = second;
        }

        public SurfaceCell First { get; }

        public SurfaceCell Second { get; }

        public static UndirectedEdgeKey Create(SurfaceCell from, SurfaceCell to)
        {
            return CompareCells(from, to) <= 0
                ? new UndirectedEdgeKey(from, to)
                : new UndirectedEdgeKey(to, from);
        }

        public bool Equals(UndirectedEdgeKey other)
        {
            return First == other.First && Second == other.Second;
        }

        public override bool Equals(object obj)
        {
            return obj is UndirectedEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (int)First.face;
                hash = (hash * 31) + First.x;
                hash = (hash * 31) + First.y;
                hash = (hash * 31) + (int)Second.face;
                hash = (hash * 31) + Second.x;
                hash = (hash * 31) + Second.y;
                return hash;
            }
        }

        private static int CompareCells(SurfaceCell left, SurfaceCell right)
        {
            var result = ((int)left.face).CompareTo((int)right.face);
            if (result != 0)
            {
                return result;
            }

            result = left.x.CompareTo(right.x);
            if (result != 0)
            {
                return result;
            }

            return left.y.CompareTo(right.y);
        }
    }

    internal enum ReservationMode
    {
        Conservative = 0,
        UnitSharedMove = 1,
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
        private readonly int _actionPlanId;
        private readonly int _intentId;

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
            _actionPlanId = groupId;
            _intentId = intentId;
            SourceId = sourceId;
            TargetId = targetId;
            Condition = condition;
            FinalHp = finalHp;
            Accepted = accepted;
            LocalActionIndex = localActionIndex;
        }

        public int ActionPlanId => _actionPlanId;

        [Obsolete("Legacy alias for ActionPlanId. Prefer ActionPlanId for correlation and semantic fields such as SourceId, TargetId, Condition, FinalHp, and Accepted.")]
        public int GroupId => _actionPlanId;

        [Obsolete("IR metadata only. Prefer ActionPlanId for plan correlation and semantic fields such as SourceId, TargetId, Condition, FinalHp, and Accepted.")]
        public int IntentId => _intentId;

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
        DelayedEnqueue = 4,
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
        SetEnemyPatrolState = 12,
        SetEnemyJumpState = 13,
        SetEnemyChargeState = 14,
        SpawnEntity = 15,
        ApplyDamage = 16,
        MarkDestroy = 17,
        EnqueueDelayedAttackEffect = 18,
        SetPhasedState = 19,
        SetEnemyUtilityState = 20,
        SetBoxInteractionLockState = 21,
        RemoveBoxInteractionLockState = 22,
        SetEnemyGlideState = 23,
        SetEnemyFrontFaceSupportState = 24,
        SetUnitKinematicState = 25,
    }

    internal enum ResolvedActionSemanticKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        Slide = 4,
        Impact = 5,
        Item = 6,
        ProjectileMove = 7,
        Attack = 8,
        Stop = 9,
        JumpLanding = 10,
    }

    internal enum MovementSemanticKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        Slide = 4,
        Impact = 5,
        JumpLanding = 6,
        ProjectileMove = 7,
        Item = 8,
        Stop = 9,
    }

    internal enum DamageSourceType
    {
        None = 0,
        Attack = 1,
        Impact = 2,
        Environmental = 3,
    }

    internal enum JumpPresentationKind
    {
        None = 0,
        WindupStart = 1,
        AirborneStart = 2,
        LandingSuccess = 3,
        LandingRetry = 4,
        CrushedBoxAndLanded = 5,
    }

    internal readonly struct FinalizationOperationMetadata
    {
        public FinalizationOperationMetadata(
            TickPhase originPhase,
            ResolvedActionSemanticKind semanticKind,
            int sourceActorEntityId,
            int actionPlanId,
            int intentId = 0,
            int contestId = 0,
            int localActionIndex = 0,
            int priority = 0,
            CubeRotationKind rotationKind = CubeRotationKind.None,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None,
            AttackSourceKind attackSourceKind = AttackSourceKind.Combat,
            MovementSemanticKind movementSemanticKind = MovementSemanticKind.None,
            DamageSourceType damageSourceType = DamageSourceType.None,
            JumpPresentationKind jumpPresentationKind = JumpPresentationKind.None,
            SurfaceCell presentationTargetCell = default)
        {
            OriginPhase = originPhase;
            SemanticKind = semanticKind;
            SourceActorEntityId = sourceActorEntityId;
            ActionPlanId = actionPlanId;
            IntentId = intentId;
            ContestId = contestId;
            LocalActionIndex = localActionIndex;
            Priority = priority;
            RotationKind = rotationKind;
            ExitCauseHint = exitCauseHint;
            AttackSourceKind = attackSourceKind;
            MovementSemanticKind = movementSemanticKind;
            DamageSourceType = damageSourceType;
            JumpPresentationKind = jumpPresentationKind;
            PresentationTargetCell = presentationTargetCell;
        }

        public TickPhase OriginPhase { get; }

        public ResolvedActionSemanticKind SemanticKind { get; }

        public int SourceActorEntityId { get; }

        public int ActionPlanId { get; }

        public int IntentId { get; }

        public int ContestId { get; }

        public int LocalActionIndex { get; }

        public int Priority { get; }

        public CubeRotationKind RotationKind { get; }

        public TickEntityExitCause ExitCauseHint { get; }

        public AttackSourceKind AttackSourceKind { get; }

        public MovementSemanticKind MovementSemanticKind { get; }

        public DamageSourceType DamageSourceType { get; }

        public JumpPresentationKind JumpPresentationKind { get; }

        public SurfaceCell PresentationTargetCell { get; }
    }

    internal sealed class FinalizationOperation
    {
        private FinalizationOperation(
            long sequence,
            FinalizationOperationBucket bucket,
            FinalizationOperationKind kind,
            FinalizationOperationMetadata metadata = default,
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
            EnemyPatrolRuntimeState enemyPatrolState = default,
            EnemyJumpRuntimeState enemyJumpState = default,
            EnemyGlideRuntimeState enemyGlideState = default,
            EnemyChargeRuntimeState enemyChargeState = default,
            EnemyUtilityRuntimeState enemyUtilityState = null,
            EnemyFrontFaceSupportRuntimeState enemyFrontFaceSupportState = null,
            BoxInteractionLockState boxInteractionLockState = default,
            UnitKinematicRuntimeState unitKinematicState = default,
            PhasedRuntimeState phasedState = default,
            EntityState spawnEntity = default,
            bool hasSpawnedEntitySummonedState = false,
            SummonedEntityState spawnedEntitySummonedState = default,
            bool hasSpawnedEntityEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState spawnedEntityEnemyDefinitionBindingState = default,
            DelayedAttackEffectRecord delayedAttackEffect = default)
        {
            Sequence = sequence;
            Bucket = bucket;
            Kind = kind;
            Metadata = metadata;
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
            EnemyPatrolState = enemyPatrolState;
            EnemyJumpState = enemyJumpState;
            EnemyGlideState = enemyGlideState;
            EnemyChargeState = enemyChargeState;
            EnemyUtilityState = enemyUtilityState;
            EnemyFrontFaceSupportState = enemyFrontFaceSupportState;
            BoxInteractionLockState = boxInteractionLockState;
            UnitKinematicState = unitKinematicState;
            PhasedState = phasedState;
            SpawnedEntity = spawnEntity;
            HasSpawnedEntitySummonedState = hasSpawnedEntitySummonedState;
            SpawnedEntitySummonedState = spawnedEntitySummonedState;
            HasSpawnedEntityEnemyDefinitionBindingState = hasSpawnedEntityEnemyDefinitionBindingState;
            SpawnedEntityEnemyDefinitionBindingState = spawnedEntityEnemyDefinitionBindingState;
            DelayedAttackEffect = delayedAttackEffect;
        }

        public long Sequence { get; }

        public FinalizationOperationBucket Bucket { get; }

        public FinalizationOperationKind Kind { get; }

        public FinalizationOperationMetadata Metadata { get; }

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

        public EnemyPatrolRuntimeState EnemyPatrolState { get; }

        public EnemyJumpRuntimeState EnemyJumpState { get; }

        public EnemyGlideRuntimeState EnemyGlideState { get; }

        public EnemyChargeRuntimeState EnemyChargeState { get; }

        public EnemyUtilityRuntimeState EnemyUtilityState { get; }

        public EnemyFrontFaceSupportRuntimeState EnemyFrontFaceSupportState { get; }

        public BoxInteractionLockState BoxInteractionLockState { get; }

        public UnitKinematicRuntimeState UnitKinematicState { get; }

        public PhasedRuntimeState PhasedState { get; }

        public EntityState SpawnedEntity { get; }

        public bool HasSpawnedEntitySummonedState { get; }

        public SummonedEntityState SpawnedEntitySummonedState { get; }

        public bool HasSpawnedEntityEnemyDefinitionBindingState { get; }

        public EnemyDefinitionBindingState SpawnedEntityEnemyDefinitionBindingState { get; }

        public DelayedAttackEffectRecord DelayedAttackEffect { get; }

        public FinalizationOperation WithSequence(long sequence)
        {
            return new FinalizationOperation(
                sequence,
                Bucket,
                Kind,
                Metadata,
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
                EnemyPatrolState,
                EnemyJumpState,
                EnemyGlideState,
                EnemyChargeState,
                EnemyUtilityState,
                EnemyFrontFaceSupportState,
                BoxInteractionLockState,
                UnitKinematicState,
                PhasedState,
                SpawnedEntity,
                HasSpawnedEntitySummonedState,
                SpawnedEntitySummonedState,
                HasSpawnedEntityEnemyDefinitionBindingState,
                SpawnedEntityEnemyDefinitionBindingState,
                DelayedAttackEffect);
        }

        public static FinalizationOperation MoveEntity(long sequence, int entityId, SurfaceCell destination, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(sequence, FinalizationOperationBucket.NonHpState, FinalizationOperationKind.MoveEntity, metadata, entityId, destination);
        }

        public static FinalizationOperation ApplyStateChange(long sequence, int entityId, EntityPhaseState state, int stateTimer, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ApplyStateChange,
                metadata,
                entityId: entityId,
                phaseState: state,
                stateTimer: stateTimer);
        }

        public static FinalizationOperation SetFacing(long sequence, int entityId, Direction facing, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetFacing,
                metadata,
                entityId: entityId,
                facing: facing);
        }

        public static FinalizationOperation SetBoxKineticOwner(long sequence, int entityId, int instigatorEntityId, int instigatorTeamId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoxKineticOwner,
                metadata,
                entityId: entityId,
                instigatorEntityId: instigatorEntityId,
                instigatorTeamId: instigatorTeamId);
        }

        public static FinalizationOperation SetBoardPresence(long sequence, int entityId, EntityBoardPresence boardPresence, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoardPresence,
                metadata,
                entityId: entityId,
                boardPresence: boardPresence);
        }

        public static FinalizationOperation SetEnemyLocomotionCooldown(long sequence, int entityId, int cooldownTicks, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyLocomotionCooldown,
                metadata,
                entityId: entityId,
                cooldownTicks: cooldownTicks);
        }

        public static FinalizationOperation SetEntityExecutionLockState(long sequence, int entityId, EntityExecutionLockState executionLockState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEntityExecutionLockState,
                metadata,
                entityId: entityId,
                executionLockState: executionLockState);
        }

        public static FinalizationOperation SetTopology(long sequence, CubeTopologyState topology, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetTopology,
                metadata,
                topology: topology);
        }

        public static FinalizationOperation SetPlayerControlState(long sequence, int entityId, PlayerControlState playerControlState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetPlayerControlState,
                metadata,
                entityId: entityId,
                playerControlState: playerControlState);
        }

        public static FinalizationOperation SetPlayerDamageState(long sequence, int entityId, PlayerDamageState playerDamageState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DamageState,
                FinalizationOperationKind.SetPlayerDamageState,
                metadata,
                entityId: entityId,
                playerDamageState: playerDamageState);
        }

        public static FinalizationOperation ApplyEnemyAiState(long sequence, int entityId, EnemyAiMode enemyAiMode, int enemyAiStateTimer, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.ApplyEnemyAiState,
                metadata,
                entityId: entityId,
                enemyAiMode: enemyAiMode,
                enemyAiStateTimer: enemyAiStateTimer);
        }

        public static FinalizationOperation SetEnemyActionState(long sequence, int entityId, EnemyActionRuntimeState enemyActionState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyActionState,
                metadata,
                entityId: entityId,
                enemyActionState: enemyActionState);
        }

        public static FinalizationOperation SetEnemyPatrolState(long sequence, int entityId, EnemyPatrolRuntimeState enemyPatrolState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyPatrolState,
                metadata,
                entityId: entityId,
                enemyPatrolState: enemyPatrolState);
        }

        public static FinalizationOperation SetEnemyJumpState(long sequence, int entityId, EnemyJumpRuntimeState enemyJumpState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyJumpState,
                metadata,
                entityId: entityId,
                enemyJumpState: enemyJumpState);
        }

        public static FinalizationOperation SetEnemyGlideState(long sequence, int entityId, EnemyGlideRuntimeState enemyGlideState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyGlideState,
                metadata,
                entityId: entityId,
                enemyGlideState: enemyGlideState);
        }

        public static FinalizationOperation SetEnemyChargeState(long sequence, int entityId, EnemyChargeRuntimeState enemyChargeState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyChargeState,
                metadata,
                entityId: entityId,
                enemyChargeState: enemyChargeState);
        }

        public static FinalizationOperation SetEnemyUtilityState(long sequence, int entityId, EnemyUtilityRuntimeState enemyUtilityState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyUtilityState,
                metadata,
                entityId: entityId,
                enemyUtilityState: enemyUtilityState);
        }

        public static FinalizationOperation SetEnemyFrontFaceSupportState(
            long sequence,
            int entityId,
            EnemyFrontFaceSupportRuntimeState enemyFrontFaceSupportState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetEnemyFrontFaceSupportState,
                metadata,
                entityId: entityId,
                enemyFrontFaceSupportState: enemyFrontFaceSupportState);
        }

        public static FinalizationOperation SetBoxInteractionLockState(long sequence, int entityId, BoxInteractionLockState boxInteractionLockState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetBoxInteractionLockState,
                metadata,
                entityId: entityId,
                boxInteractionLockState: boxInteractionLockState);
        }

        public static FinalizationOperation RemoveBoxInteractionLockState(long sequence, int entityId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.RemoveBoxInteractionLockState,
                metadata,
                entityId: entityId);
        }

        public static FinalizationOperation SetUnitKinematicState(
            long sequence,
            int entityId,
            UnitKinematicRuntimeState unitKinematicState,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetUnitKinematicState,
                metadata,
                entityId: entityId,
                unitKinematicState: unitKinematicState);
        }

        public static FinalizationOperation SetPhasedState(long sequence, int entityId, PhasedRuntimeState phasedState, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.NonHpState,
                FinalizationOperationKind.SetPhasedState,
                metadata,
                entityId: entityId,
                phasedState: phasedState);
        }

        public static FinalizationOperation SpawnEntity(
            long sequence,
            EntityState entity,
            FinalizationOperationMetadata metadata = default,
            bool hasSpawnedEntitySummonedState = false,
            SummonedEntityState spawnedEntitySummonedState = default,
            bool hasSpawnedEntityEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState spawnedEntityEnemyDefinitionBindingState = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Spawn,
                FinalizationOperationKind.SpawnEntity,
                metadata,
                spawnEntity: entity,
                hasSpawnedEntitySummonedState: hasSpawnedEntitySummonedState,
                spawnedEntitySummonedState: spawnedEntitySummonedState,
                hasSpawnedEntityEnemyDefinitionBindingState: hasSpawnedEntityEnemyDefinitionBindingState,
                spawnedEntityEnemyDefinitionBindingState: spawnedEntityEnemyDefinitionBindingState);
        }

        public static FinalizationOperation ApplyDamage(long sequence, int entityId, int amount, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DamageState,
                FinalizationOperationKind.ApplyDamage,
                metadata,
                entityId: entityId,
                amount: amount);
        }

        public static FinalizationOperation MarkDestroy(long sequence, int entityId, FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.Destroy,
                FinalizationOperationKind.MarkDestroy,
                metadata,
                entityId: entityId);
        }

        public static FinalizationOperation EnqueueDelayedAttackEffect(
            long sequence,
            DelayedAttackEffectRecord delayedAttackEffect,
            FinalizationOperationMetadata metadata = default)
        {
            return new FinalizationOperation(
                sequence,
                FinalizationOperationBucket.DelayedEnqueue,
                FinalizationOperationKind.EnqueueDelayedAttackEffect,
                metadata,
                delayedAttackEffect: delayedAttackEffect);
        }
    }

    internal sealed class FinalizationBatch
    {
        private readonly List<FinalizationOperation> _operations = new();
        private long _nextSequence = 1;

        public IReadOnlyList<FinalizationOperation> Operations => _operations;

        public void MoveEntity(int entityId, SurfaceCell destination, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.MoveEntity(_nextSequence++, entityId, destination, metadata));
        }

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ApplyStateChange(_nextSequence++, entityId, state, stateTimer, metadata));
        }

        public void SetFacing(int entityId, Direction facing, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetFacing(_nextSequence++, entityId, facing, metadata));
        }

        public void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetBoxKineticOwner(_nextSequence++, entityId, instigatorEntityId, instigatorTeamId, metadata));
        }

        public void SetBoardPresence(int entityId, EntityBoardPresence boardPresence, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetBoardPresence(_nextSequence++, entityId, boardPresence, metadata));
        }

        public void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyLocomotionCooldown(_nextSequence++, entityId, cooldownTicks, metadata));
        }

        public void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEntityExecutionLockState(_nextSequence++, entityId, state, metadata));
        }

        public void SetTopology(CubeTopologyState topology, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetTopology(_nextSequence++, topology, metadata));
        }

        public void SetPlayerControlState(int entityId, PlayerControlState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPlayerControlState(_nextSequence++, entityId, state, metadata));
        }

        public void SetPlayerDamageState(int entityId, PlayerDamageState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPlayerDamageState(_nextSequence++, entityId, state, metadata));
        }

        public void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ApplyEnemyAiState(_nextSequence++, entityId, aiMode, aiStateTimer, metadata));
        }

        public void SetEnemyActionState(int entityId, EnemyActionRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyActionState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyPatrolState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyJumpState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyGlideState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyChargeState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyUtilityState(_nextSequence++, entityId, state, metadata));
        }

        public void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetEnemyFrontFaceSupportState(_nextSequence++, entityId, state, metadata));
        }

        public void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetBoxInteractionLockState(_nextSequence++, entityId, state, metadata));
        }

        public void RemoveBoxInteractionLockState(int entityId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.RemoveBoxInteractionLockState(_nextSequence++, entityId, metadata));
        }

        public void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetUnitKinematicState(_nextSequence++, entityId, state, metadata));
        }

        public void SetPhasedState(int entityId, PhasedRuntimeState state, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.SetPhasedState(_nextSequence++, entityId, state, metadata));
        }

        public void SpawnEntity(
            EntityState entity,
            FinalizationOperationMetadata metadata = default,
            bool hasSummonedEntityState = false,
            SummonedEntityState summonedEntityState = default,
            bool hasEnemyDefinitionBindingState = false,
            EnemyDefinitionBindingState enemyDefinitionBindingState = default)
        {
            _operations.Add(
                FinalizationOperation.SpawnEntity(
                    _nextSequence++,
                    entity,
                    metadata,
                    hasSummonedEntityState,
                    summonedEntityState,
                    hasEnemyDefinitionBindingState,
                    enemyDefinitionBindingState));
        }

        public void ApplyDamage(int entityId, int amount, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.ApplyDamage(_nextSequence++, entityId, amount, metadata));
        }

        public void MarkDestroy(int entityId, FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.MarkDestroy(_nextSequence++, entityId, metadata));
        }

        public void EnqueueDelayedAttackEffect(
            DelayedAttackEffectRecord effectRecord,
            FinalizationOperationMetadata metadata = default)
        {
            _operations.Add(FinalizationOperation.EnqueueDelayedAttackEffect(_nextSequence++, effectRecord, metadata));
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
            ApplyBucket(writeContext, FinalizationOperationBucket.DelayedEnqueue, delayedAttackEffectSink);
        }

        private void ApplyBucket(
            IWorldWriteContext writeContext,
            FinalizationOperationBucket bucket,
            IDelayedAttackEffectSink delayedAttackEffectSink = null)
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

                    case FinalizationOperationKind.SetEnemyPatrolState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyPatrolState(operation.EntityId, operation.EnemyPatrolState);
                        break;

                    case FinalizationOperationKind.SetEnemyJumpState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyJumpState(operation.EntityId, operation.EnemyJumpState);
                        break;

                    case FinalizationOperationKind.SetEnemyGlideState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyGlideState(operation.EntityId, operation.EnemyGlideState);
                        break;

                    case FinalizationOperationKind.SetEnemyChargeState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyChargeState(operation.EntityId, operation.EnemyChargeState);
                        break;

                    case FinalizationOperationKind.SetEnemyUtilityState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyUtilityState(operation.EntityId, operation.EnemyUtilityState);
                        break;

                    case FinalizationOperationKind.SetEnemyFrontFaceSupportState:
                        ((IPreMovementStateCommitContext)writeContext).SetEnemyFrontFaceSupportState(operation.EntityId, operation.EnemyFrontFaceSupportState);
                        break;

                    case FinalizationOperationKind.SetBoxInteractionLockState:
                        writeContext.SetBoxInteractionLockState(operation.EntityId, operation.BoxInteractionLockState);
                        break;

                    case FinalizationOperationKind.RemoveBoxInteractionLockState:
                        writeContext.RemoveBoxInteractionLockState(operation.EntityId);
                        break;

                    case FinalizationOperationKind.SetUnitKinematicState:
                        writeContext.SetUnitKinematicState(operation.EntityId, operation.UnitKinematicState);
                        break;

                    case FinalizationOperationKind.SetPhasedState:
                        ((IPhasedStateCommitContext)writeContext).SetPhasedState(operation.EntityId, operation.PhasedState);
                        break;

                    case FinalizationOperationKind.SpawnEntity:
                        ((IAttackCommitContext)writeContext).SpawnEntity(operation.SpawnedEntity);
                        if (operation.HasSpawnedEntitySummonedState)
                        {
                            writeContext.SetSummonedEntityState(operation.SpawnedEntity.entityId, operation.SpawnedEntitySummonedState);
                        }
                        if (operation.HasSpawnedEntityEnemyDefinitionBindingState)
                        {
                            writeContext.SetEnemyDefinitionBindingState(
                                operation.SpawnedEntity.entityId,
                                operation.SpawnedEntityEnemyDefinitionBindingState);
                        }
                        break;

                    case FinalizationOperationKind.ApplyDamage:
                        ((IAttackCommitContext)writeContext).ApplyDamage(operation.EntityId, operation.Amount);
                        break;

                    case FinalizationOperationKind.MarkDestroy:
                        ((IAttackCommitContext)writeContext).MarkDestroy(operation.EntityId);
                        break;

                    case FinalizationOperationKind.EnqueueDelayedAttackEffect:
                        if (delayedAttackEffectSink != null)
                        {
                            delayedAttackEffectSink.Enqueue(operation.DelayedAttackEffect);
                        }
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
        private readonly TickPhase _originPhase;
        private readonly WorldSnapshot _referenceSnapshot;
        private readonly List<EnemyUtilityTriggerIntent> _utilityTriggerIntents;

        public RecordingFinalizationContext(
            FinalizationBatch batch,
            WorldSnapshot referenceSnapshot = null,
            TickPhase originPhase = TickPhase.Resolve,
            List<EnemyUtilityTriggerIntent> utilityTriggerIntents = null)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
            _referenceSnapshot = referenceSnapshot;
            _originPhase = originPhase;
            _utilityTriggerIntents = utilityTriggerIntents;
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

        public void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state)
        {
            _batch.SetEnemyPatrolState(entityId, state);
        }

        public void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            _batch.SetEnemyJumpState(entityId, state, CreateJumpStateMetadata(entityId, state));
        }

        public void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state)
        {
            _batch.SetEnemyGlideState(entityId, state);
        }

        public void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state)
        {
            _batch.SetEnemyUtilityState(entityId, state);
        }

        public void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state)
        {
            _batch.SetEnemyFrontFaceSupportState(entityId, state);
        }

        public void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state)
        {
            _batch.SetBoxInteractionLockState(entityId, state);
        }

        public void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state)
        {
            _batch.SetEnemyChargeState(entityId, state);
        }

        public void SetPhasedState(int entityId, PhasedRuntimeState state)
        {
            _batch.SetPhasedState(entityId, state, CreatePhasedStateMetadata(entityId));
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

        public void SetSummonedEntityState(int entityId, SummonedEntityState state)
        {
        }

        public void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state)
        {
        }

        public void RemoveEntity(int entityId)
        {
            throw new NotSupportedException("Finalize recording does not support cleanup removes.");
        }

        public void RemoveBoxInteractionLockState(int entityId)
        {
            _batch.RemoveBoxInteractionLockState(entityId);
        }

        public void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state)
        {
            _batch.SetUnitKinematicState(entityId, state);
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

        public void EmitEnemyUtilityTriggerIntent(EnemyUtilityTriggerIntent intent)
        {
            _utilityTriggerIntents?.Add(intent);
        }

        private FinalizationOperationMetadata CreateJumpStateMetadata(int entityId, in EnemyJumpRuntimeState state)
        {
            var jumpPresentationKind = JumpPresentationKind.None;
            if (_referenceSnapshot == null ||
                !_referenceSnapshot.TryGetEnemyJumpState(entityId, out var previousState))
            {
                if (state.phase == EnemyJumpPhase.Windup)
                {
                    jumpPresentationKind = JumpPresentationKind.WindupStart;
                }
                else if (state.phase == EnemyJumpPhase.Airborne)
                {
                    jumpPresentationKind = JumpPresentationKind.AirborneStart;
                }
            }
            else if (state.phase == EnemyJumpPhase.Windup &&
                     previousState.phase != EnemyJumpPhase.Windup)
            {
                jumpPresentationKind = JumpPresentationKind.WindupStart;
            }
            else if (state.phase == EnemyJumpPhase.Airborne &&
                     previousState.phase != EnemyJumpPhase.Airborne)
            {
                jumpPresentationKind = JumpPresentationKind.AirborneStart;
            }

            return new FinalizationOperationMetadata(
                _originPhase,
                ResolvedActionSemanticKind.JumpLanding,
                entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.JumpLanding,
                damageSourceType: DamageSourceType.None,
                jumpPresentationKind: jumpPresentationKind,
                presentationTargetCell: state.lockedTargetCell);
        }

        private FinalizationOperationMetadata CreatePhasedStateMetadata(int entityId)
        {
            return new FinalizationOperationMetadata(
                _originPhase,
                ResolvedActionSemanticKind.None,
                entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.None,
                damageSourceType: DamageSourceType.None);
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
            entities.Sort(CompareProjectedMaterializationOrder);
            var enemyGlideStates = new List<EnemyGlideSnapshotEntry>();
            snapshot.EnumerateEnemyGlideStatesOrdered(enemyGlideStates);
            var enemyGlideStatesByEntityId = new Dictionary<int, EnemyGlideRuntimeState>(enemyGlideStates.Count);
            for (var i = 0; i < enemyGlideStates.Count; i++)
            {
                enemyGlideStatesByEntityId[enemyGlideStates[i].EntityId] = enemyGlideStates[i].State;
            }

            var worldState = new WorldState(
                entities,
                snapshot.BoardBounds,
                snapshot.TerrainData,
                snapshot.Topology,
                enemyGlideStatesByEntityId);
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

                if (snapshot.TryGetEnemyPatrolState(entityId, out var enemyPatrolState))
                {
                    writeContext.SetEnemyPatrolState(entityId, enemyPatrolState);
                }

                if (snapshot.TryGetEntityExecutionLockState(entityId, out var executionLockState))
                {
                    writeContext.SetEntityExecutionLockState(entityId, executionLockState);
                }

                if (snapshot.TryGetEnemyJumpState(entityId, out var enemyJumpState))
                {
                    writeContext.SetEnemyJumpState(entityId, enemyJumpState);
                }

                if (snapshot.TryGetEnemyGlideState(entityId, out var enemyGlideState))
                {
                    writeContext.SetEnemyGlideState(entityId, enemyGlideState);
                }

                if (snapshot.TryGetEnemyUtilityState(entityId, out var enemyUtilityState))
                {
                    writeContext.SetEnemyUtilityState(entityId, enemyUtilityState);
                }

                if (snapshot.TryGetEnemyFrontFaceSupportState(entityId, out var enemyFrontFaceSupportState))
                {
                    writeContext.SetEnemyFrontFaceSupportState(entityId, enemyFrontFaceSupportState);
                }

                if (snapshot.TryGetBoxInteractionLockState(entityId, out var boxInteractionLockState))
                {
                    writeContext.SetBoxInteractionLockState(entityId, boxInteractionLockState);
                }

                if (snapshot.TryGetUnitKinematicState(entityId, out var unitKinematicState))
                {
                    writeContext.SetUnitKinematicState(entityId, unitKinematicState);
                }

                if (snapshot.TryGetEnemyChargeState(entityId, out var enemyChargeState))
                {
                    writeContext.SetEnemyChargeState(entityId, enemyChargeState);
                }

                if (snapshot.TryGetPhasedState(entityId, out var phasedState))
                {
                    writeContext.SetPhasedState(entityId, phasedState);
                }

                if (snapshot.TryGetSummonedEntityState(entityId, out var summonedEntityState))
                {
                    writeContext.SetSummonedEntityState(entityId, summonedEntityState);
                }

                if (snapshot.TryGetEnemyDefinitionBindingState(entityId, out var enemyDefinitionBindingState))
                {
                    writeContext.SetEnemyDefinitionBindingState(entityId, enemyDefinitionBindingState);
                }
            }

            return worldState;
        }

        private static int CompareProjectedMaterializationOrder(EntityState left, EntityState right)
        {
            var leftPriority = ResolveProjectedMaterializationPriority(left);
            var rightPriority = ResolveProjectedMaterializationPriority(right);
            if (leftPriority != rightPriority)
            {
                return leftPriority.CompareTo(rightPriority);
            }

            return left.entityId.CompareTo(right.entityId);
        }

        private static int ResolveProjectedMaterializationPriority(EntityState entity)
        {
            // Projection rehydrates already-authoritative snapshots. Occupying projectiles
            // must materialize ahead of solids so box/projectile overlap states that are
            // legal in the live world can be reconstructed without relaxing placement
            // invariants for normal world writes.
            if (entity.boardPresence != EntityBoardPresence.Occupying)
            {
                return 2;
            }

            if (entity.type == EntityType.Projectile)
            {
                return 0;
            }

            return entity.type == EntityType.Unit ? 2 : 1;
        }
    }
}
