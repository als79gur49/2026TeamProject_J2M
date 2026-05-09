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
    public sealed partial class TickPipeline
    {
        private readonly IdAllocator _idAllocator = new();
        private readonly EntityIdAllocator _entityIdAllocator;
        private readonly ISnapshotEntityLogicProvider _entityLogicProvider;
        private readonly IEnemyGlidePresentationSettingsResolver _enemyGlidePresentationSettingsResolver;
        private readonly IReadOnlyList<IEntityLogic> _staticEntityLogics;
        private readonly IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> _enemySpawnDefaultsByArchetypeId;
        private readonly MovementIntentCollector _movementIntentCollector = new();
        private readonly MovementExpander _movementExpander;
        private readonly AttackIntentCollector _attackIntentCollector = new();
        private readonly AttackInputNormalizer _attackInputNormalizer = new();
        private readonly AttackExpander _attackExpander;
        private readonly CleanupProcessor _cleanupProcessor = new();
        private readonly RespawnProcessor _respawnProcessor = new();
        private readonly MoonBlockGeneratorRespawnProcessor _moonBlockGeneratorRespawnProcessor = new();
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
        private readonly int _gravityFieldChargeTicks;
        private readonly int _gravityFieldActiveTicks;
        private readonly PlayerKinematicLocomotionTimingSnapshot _playerKinematicLocomotionTiming;
        private readonly PlayerContinuousLocomotionSnapshot _playerContinuousLocomotion;
        private readonly bool _allowPlayerRespawn;
        private readonly GameplayRuntimeFeatureFlags _runtimeFeatureFlags;
        private readonly int _slidingStateTimerTicks;
        private readonly IReadOnlyList<TileFeatureRuntimeDefinition> _tileFeatureDefinitions;
        private readonly IReadOnlyList<MoonBlockRespawnDefinition> _moonBlockRespawnDefinitions;
        private readonly ITileEffectResolver _tileEffectResolver;
        private readonly WorldState _worldState;

        private enum EnemyGlideKinematicKind
        {
            None = 0,
            Active = 1,
            LandingPendingEgress = 2,
        }

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
            PlayerKinematicLocomotionTimingSnapshot playerKinematicLocomotionTiming = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default)
            : this(
                worldState,
                entityLogics,
                entityLogicProvider,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnDelayTicks,
                objectiveDefinition,
                enemySpawnDefaultsByArchetypeId,
                allowPlayerRespawn,
                runtimeFeatureFlags,
                playerKinematicLocomotionTiming,
                playerContinuousLocomotion,
                tileFeatureDefinitions: null,
                tileEffectResolver: null)
        {
        }

        internal TickPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            ISnapshotEntityLogicProvider entityLogicProvider,
            GameplayTimingProfile generalTimingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            int playerRespawnDelayTicks,
            StageObjectiveRuntimeDefinition objectiveDefinition,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> enemySpawnDefaultsByArchetypeId,
            bool allowPlayerRespawn,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            PlayerKinematicLocomotionTimingSnapshot playerKinematicLocomotionTiming,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<MoonBlockRespawnDefinition> moonBlockRespawnDefinitions = null,
            ITileEffectResolver tileEffectResolver = null)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));

            if (entityLogics == null)
            {
                throw new ArgumentNullException(nameof(entityLogics));
            }

            _entityLogicProvider = entityLogicProvider ?? throw new ArgumentNullException(nameof(entityLogicProvider));
            _enemyGlidePresentationSettingsResolver =
                _entityLogicProvider as IEnemyGlidePresentationSettingsResolver;
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
            _gravityFieldChargeTicks = GameplayTimingProfile.SecondsToCeilTicks(
                GravityFieldRuntimePolicy.ChargeDurationSeconds,
                resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _gravityFieldActiveTicks = GameplayTimingProfile.SecondsToCeilTicks(
                GravityFieldRuntimePolicy.ActiveDurationSeconds,
                resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _playerKinematicLocomotionTiming = playerKinematicLocomotionTiming.IsConfigured
                ? playerKinematicLocomotionTiming
                : PlayerKinematicLocomotionTimingSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _playerContinuousLocomotion = playerContinuousLocomotion.IsConfigured
                ? playerContinuousLocomotion
                : PlayerContinuousLocomotionSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _tileFeatureDefinitions = tileFeatureDefinitions == null
                ? Array.Empty<TileFeatureRuntimeDefinition>()
                : new List<TileFeatureRuntimeDefinition>(tileFeatureDefinitions).AsReadOnly();
            _moonBlockRespawnDefinitions = moonBlockRespawnDefinitions == null
                ? Array.Empty<MoonBlockRespawnDefinition>()
                : new List<MoonBlockRespawnDefinition>(moonBlockRespawnDefinitions).AsReadOnly();
            _tileEffectResolver = tileEffectResolver ?? TileFeatureEffectResolver.Instance;
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
                resolvePhaseResult.ResolutionRecords,
                _enemyGlidePresentationSettingsResolver,
                resolvePhaseResult.TilePresentationEvents,
                objectiveResult,
                _objectiveTracker.ObjectiveDefinition,
                _tileFeatureDefinitions,
                resolvePhaseResult.GravityFieldPresentationEvents,
                _gravityFieldChargeTicks,
                _gravityFieldActiveTicks,
                resolvePhaseResult.GravityFieldLockedTargetFacts,
                planPhaseResult.PlayerActionAttemptResolutions);
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

        private static void CloseInterruptedUnitKinematics(
            WorldSnapshot snapshot,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            bool closePlayerKinematics,
            bool closeEnemyGlideKinematics)
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
                    !snapshot.TryGetUnitKinematicPose(entity.entityId, out var pose) ||
                    !pose.HasAuthoritativeState ||
                    pose.Mode != MotionMode.Interrupted)
                {
                    continue;
                }

                var hasPlayerControl = snapshot.TryGetPlayerControlState(entity.entityId, out var playerControlState);
                var canClosePlayer = closePlayerKinematics && hasPlayerControl;
                var canCloseEnemyGlide = closeEnemyGlideKinematics &&
                                         IsEnemyInterruptedGlideKinematicParticipant(snapshot, entity);
                if (!canClosePlayer && !canCloseEnemyGlide)
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
                if (hasPlayerControl &&
                    PlayerControlQueries.HasQueuedKinematicTurn(playerControlState))
                {
                    batch.SetPlayerControlState(
                        entity.entityId,
                        PlayerControlQueries.ClearQueuedKinematicTurn(playerControlState),
                        new FinalizationOperationMetadata(
                            TickPhase.Plan,
                            ResolvedActionSemanticKind.Stop,
                            entity.entityId,
                            actionPlanId: 0));
                }
            }
        }

        private static bool IsEnemyInterruptedGlideKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            return IsEnemyLogicParticipant(entity) &&
                   EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) &&
                   entity.aiMode == EnemyAiMode.Chase &&
                   snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                   glideState.HasAuthoritativeRecord &&
                   (glideState.Phase == EnemyGlidePhase.Active ||
                    glideState.Phase == EnemyGlidePhase.Recovery ||
                    glideState.Phase == EnemyGlidePhase.LandingPending);
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
            if (_runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion ||
                _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion)
            {
                CloseInterruptedUnitKinematics(
                    snapshotAfterEnemyAi,
                    kinematicClosureBatch,
                    kinematicClosureEvents,
                    closePlayerKinematics: _runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion,
                    closeEnemyGlideKinematics: _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion);
                planFinalizationBatch.MergeFrom(kinematicClosureBatch);
                projectedWorld.ApplyBatch(kinematicClosureBatch);
                snapshotAfterEnemyAi = projectedWorld.CreateSnapshot();
            }

            var gravityFieldResult = GravityFieldRuntimeResolver.ResolvePreMovement(
                snapshotAfterEnemyAi,
                input.TickIndex,
                _gravityFieldChargeTicks,
                _gravityFieldActiveTicks);
            var gravityFieldBatch = gravityFieldResult.Batch;
            var gravityFieldEvents = gravityFieldResult.EventLogEntries;
            if (gravityFieldBatch.Operations.Count > 0)
            {
                planFinalizationBatch.MergeFrom(gravityFieldBatch);
                projectedWorld.ApplyBatch(gravityFieldBatch);
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
            if (gravityFieldEvents.Count > 0)
            {
                preMovementStateResult.EventLogEntries.InsertRange(0, gravityFieldEvents);
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

            var rejectedReasons = new List<string>();
            var playerActionAttemptResolutions = new List<PlayerActionAttemptResolution>();
            var consumedPlayerActionAttemptEntityIds = new HashSet<int>();
            var playerActionAttemptBatch = new FinalizationBatch();
            CollectPreMovementPlayerActionAttemptResolutions(
                planSnapshot,
                input.PlayerCommand,
                input.TickIndex,
                rejectedReasons,
                playerActionAttemptBatch,
                playerActionAttemptResolutions,
                consumedPlayerActionAttemptEntityIds);
            if (playerActionAttemptBatch.Operations.Count > 0 ||
                playerActionAttemptBatch.TileFeatureOperations.Count > 0)
            {
                planFinalizationBatch.MergeFrom(playerActionAttemptBatch);
                projectedWorld.ApplyBatch(playerActionAttemptBatch);
                planSnapshot = projectedWorld.CreateSnapshot();
            }

            var rawMovementIntents = new List<RawMovementIntent>();
            _movementIntentCollector.Collect(planSnapshot, in input, entityLogicsForTick.MovementLogics, rawMovementIntents);
            FilterConsumedPlayerActionAttemptMovementIntents(rawMovementIntents, consumedPlayerActionAttemptEntityIds);
            var executableMovementIntents = FilterExecutionLockedMovementIntents(planSnapshot, input.TickIndex, rawMovementIntents, rejectedReasons);
            var sortedIntents = BuildMovementIntents(executableMovementIntents);
            var expansionIntents = sortedIntents;
            var kinematicMovementActionPlanPayloads = new Dictionary<int, MovementActionPlanPayload>();
            if (_runtimeFeatureFlags.EnablePlayerFree2DLocalLocomotion)
            {
                var free2DBatch = new FinalizationBatch();
                expansionIntents = BuildPlayerFree2DLocalLocomotionPlans(
                    planSnapshot,
                    sortedIntents,
                    input.PlayerCommand,
                    input.TickIndex,
                    rejectedReasons,
                    free2DBatch,
                    consumedPlayerActionAttemptEntityIds);
                planFinalizationBatch.MergeFrom(free2DBatch);
                projectedWorld.ApplyBatch(free2DBatch);
                planSnapshot = projectedWorld.CreateSnapshot();
            }
            else if (_runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion)
            {
                expansionIntents = BuildPlayerSameFaceKinematicLocomotionPlans(
                    planSnapshot,
                    sortedIntents,
                    input.PlayerCommand,
                    input.TickIndex,
                    rejectedReasons,
                    consumedPlayerActionAttemptEntityIds,
                    kinematicMovementActionPlanPayloads);
            }
            if (_runtimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion ||
                _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion)
            {
                expansionIntents = BuildEnemySameFaceKinematicLocomotionPlans(
                    planSnapshot,
                    expansionIntents,
                    input.TickIndex,
                    rejectedReasons,
                    kinematicMovementActionPlanPayloads);
            }
            if (_runtimeFeatureFlags.EnableEnemyChargeKinematicLocomotion)
            {
                expansionIntents = BuildEnemyChargeKinematicLocomotionPlans(
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
            var barricadeBlockFacts = new List<BarricadeBlockFact>();
            var expandedCandidates = new List<ActionGroup>();
            var preExpansionRejectedReasons = new List<string>(rejectedReasons);
            var legacyExpansionIntents = ValidateLegacyExpansionIntents(
                planSnapshot,
                expansionIntents,
                preExpansionRejectedReasons);
            var forbiddenLegacyUnitOrdinaryIntentIds = BuildForbiddenLegacyUnitOrdinaryIntentIds(
                expansionIntents,
                legacyExpansionIntents);
            _movementExpander.Expand(
                planSnapshot,
                input.TickIndex,
                legacyExpansionIntents,
                playerTraversalSourceIds,
                frontFaceSupportContributors,
                expandedCandidates,
                rejectedReasons,
                frontFaceShieldBlockExports,
                barricadeBlockFacts,
                forbiddenLegacyUnitOrdinaryIntentIds,
                _tileFeatureDefinitions);
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
                barricadeBlockFacts,
                nextContestId,
                aiPhaseResult,
                preMovementStateResult,
                snapshotAfterEnemyAi,
                planSnapshot,
                planFinalizationBatch,
                gravityFieldResult.PresentationEvents,
                gravityFieldResult.LockedTargetFacts,
                playerActionAttemptResolutions);
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
            var attackReadSnapshot = projectedWorld.CreateSnapshot();
            var tileEffectBoxContacts = BuildTileEffectBoxContacts(
                attackReadSnapshot,
                movementStageBatch,
                jumpLandingResolveBatch,
                phaseRelocationResolveBatch);
            IReadOnlyList<TilePresentationEvent> tilePresentationEvents = Array.Empty<TilePresentationEvent>();
            var tileEffectResult = _tileEffectResolver.Resolve(
                new TileEffectResolutionContext(
                    tickIndex,
                    attackReadSnapshot,
                    _tileFeatureDefinitions,
                    tileEffectBoxContacts,
                    planSnapshot));
            tilePresentationEvents = tileEffectResult.TileEvents;
            if (!tileEffectResult.IsEmpty)
            {
                if (!tileEffectResult.Operations.IsEmpty)
                {
                    finalizationBatch.ApplyTileFeatureOperations(tileEffectResult.Operations);
                    projectedWorld.ApplyTileFeatureOperations(tileEffectResult.Operations);
                }

                if (tileEffectResult.EntityOperations.Operations.Count > 0)
                {
                    finalizationBatch.MergeFrom(tileEffectResult.EntityOperations);
                    projectedWorld.ApplyBatch(tileEffectResult.EntityOperations);
                }

                attackReadSnapshot = projectedWorld.CreateSnapshot();
            }

            attackSnapshot = attackReadSnapshot;
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
            var motionInterruptRecords = _runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion ||
                _runtimeFeatureFlags.EnablePlayerFree2DLocalLocomotion ||
                _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion
                ? MaterializeUnitKinematicMotionInterrupts(
                    attackSnapshot,
                    damageResolutions,
                    destroyResolutions,
                    attackStageBatch,
                    attackCommitEvents,
                    tickIndex,
                    interruptPlayerKinematics: _runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion ||
                                                _runtimeFeatureFlags.EnablePlayerFree2DLocalLocomotion,
                    interruptEnemyGlideKinematics: _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion)
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
                    planOperation.Kind == FinalizationOperationKind.SetPhasedState ||
                    planOperation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition)
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
                planPhaseResult.FrontFaceShieldBlockExports,
                planPhaseResult.BarricadeBlockFacts);

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
                resolutionRecords,
                tilePresentationEvents,
                planPhaseResult.GravityFieldPresentationEvents,
                planPhaseResult.GravityFieldLockedTargetFacts);
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
                cleanupPhaseResult.RemovedUnitKinematicPoses,
                cleanupPhaseResult.RemovedUnitContinuousLocomotionPoses);
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
            var moonBlockGeneratorResult = _moonBlockGeneratorRespawnProcessor.Process(
                postCleanupSnapshot,
                () => SnapshotBuilder.Create(_worldState),
                respawnPhaseResult.RespawnedEntities.Count > 0 ||
                respawnPhaseResult.TopologyResetRequest.HasValue,
                _moonBlockRespawnDefinitions,
                _tileFeatureDefinitions,
                tickIndex,
                writeContext);
            if (moonBlockGeneratorResult.EventLogEntries.Count > 0 ||
                moonBlockGeneratorResult.RespawnFacts.Count > 0)
            {
                var eventLogEntries = new List<string>(
                    respawnPhaseResult.EventLogEntries.Count + moonBlockGeneratorResult.EventLogEntries.Count);
                AddRange(eventLogEntries, respawnPhaseResult.EventLogEntries);
                AddRange(eventLogEntries, moonBlockGeneratorResult.EventLogEntries);
                var respawnFacts = new List<MoonBlockGeneratorRespawnFact>(
                    respawnPhaseResult.MoonBlockGeneratorRespawnFacts.Count +
                    moonBlockGeneratorResult.RespawnFacts.Count);
                AddRange(respawnFacts, respawnPhaseResult.MoonBlockGeneratorRespawnFacts);
                AddRange(respawnFacts, moonBlockGeneratorResult.RespawnFacts);
                respawnPhaseResult = new RespawnPhaseResult(
                    respawnPhaseResult.RespawnedEntities,
                    eventLogEntries,
                    respawnPhaseResult.PlayerRespawnDelayRecords,
                    respawnPhaseResult.RespawnPlacementRecords,
                    respawnPhaseResult.TopologyResetRequest,
                    respawnFacts);
            }

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
                        rawIntent.MoveCooldownTicks,
                        rawIntent.OrdinaryKinematicMoveTicks),
                    Movement.MovementCommandKind.Flip => new FlipIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence,
                        rawIntent.MoveCooldownTicks,
                        rawIntent.OrdinaryKinematicMoveTicks),
                    _ => new MoveIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence,
                        rawIntent.MoveCooldownTicks,
                        rawIntent.OrdinaryKinematicMoveTicks),
                };
                moveIntent.AssignIntentId(_idAllocator.AllocateIntentId());
                sortedIntents.Add(moveIntent);
            }

            return sortedIntents;
        }

        private static void FilterConsumedPlayerActionAttemptMovementIntents(
            List<RawMovementIntent> rawMovementIntents,
            HashSet<int> consumedPlayerActionAttemptEntityIds)
        {
            if (rawMovementIntents == null ||
                consumedPlayerActionAttemptEntityIds == null ||
                consumedPlayerActionAttemptEntityIds.Count == 0)
            {
                return;
            }

            for (var i = rawMovementIntents.Count - 1; i >= 0; i--)
            {
                if (consumedPlayerActionAttemptEntityIds.Contains(rawMovementIntents[i].SourceId))
                {
                    rawMovementIntents.RemoveAt(i);
                }
            }
        }

        private static bool IsConsumedPlayerActionAttemptEntity(
            HashSet<int> consumedPlayerActionAttemptEntityIds,
            int entityId)
        {
            return consumedPlayerActionAttemptEntityIds != null &&
                   consumedPlayerActionAttemptEntityIds.Contains(entityId);
        }

        private List<MoveIntent> ValidateLegacyExpansionIntents(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> expansionIntents,
            List<string> rejectedReasons)
        {
            var filteredIntents = new List<MoveIntent>(expansionIntents.Count);
            for (var i = 0; i < expansionIntents.Count; i++)
            {
                var intent = expansionIntents[i];
                if (TryResolveForbiddenLegacyUnitOrdinaryMovement(
                        snapshot,
                        intent,
                        out var entity,
                        out var reason))
                {
                    rejectedReasons.Add(
                        $"LegacyUnitOrdinaryMovementDetected|E={intent.SourceId}|EntityType={entity.type}|Intent={intent.CommandKind}|Flags={FormatLocomotionFeatureFlags()}|Reason={reason}|I={intent.IntentId}");
                    continue;
                }

                filteredIntents.Add(intent);
            }

            return filteredIntents;
        }

        private static ISet<int> BuildForbiddenLegacyUnitOrdinaryIntentIds(
            IReadOnlyList<MoveIntent> expansionIntents,
            IReadOnlyList<MoveIntent> legacyExpansionIntents)
        {
            if (expansionIntents == null ||
                legacyExpansionIntents == null ||
                expansionIntents.Count == legacyExpansionIntents.Count)
            {
                return null;
            }

            var allowedIntentIds = new HashSet<int>();
            for (var i = 0; i < legacyExpansionIntents.Count; i++)
            {
                allowedIntentIds.Add(legacyExpansionIntents[i].IntentId);
            }

            var forbiddenIntentIds = new HashSet<int>();
            for (var i = 0; i < expansionIntents.Count; i++)
            {
                var intentId = expansionIntents[i].IntentId;
                if (!allowedIntentIds.Contains(intentId))
                {
                    forbiddenIntentIds.Add(intentId);
                }
            }

            return forbiddenIntentIds.Count > 0 ? forbiddenIntentIds : null;
        }

        private bool TryResolveForbiddenLegacyUnitOrdinaryMovement(
            WorldSnapshot snapshot,
            MoveIntent intent,
            out EntityState entity,
            out string reason)
        {
            entity = default;
            reason = string.Empty;
            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                !snapshot.TryGetEntity(intent.SourceId, out entity) ||
                entity.type != EntityType.Unit)
            {
                return false;
            }

            if (IsAllowedLegacyGridTransactionIntent(snapshot, entity, intent))
            {
                return false;
            }

            var isPlayerOrdinaryFallback = snapshot.TryGetPlayerControlState(intent.SourceId, out _);
            var isChargeActiveFallback = TryResolveEnemyChargeKinematicStartScope(snapshot, intent, out _, out _, out _, out _);
            var hasEnemyKinematicScope = TryResolveEnemyKinematicStartScope(
                snapshot,
                intent,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var ordinaryScopeGlideKind);
            var isActiveGlideFallback = (hasEnemyKinematicScope && ordinaryScopeGlideKind != EnemyGlideKinematicKind.None) ||
                                        IsEnemyActiveGlideKinematicParticipant(snapshot, entity);
            var isEnemyOrdinaryFallback = IsEnemyLogicParticipant(entity) &&
                                          !isPlayerOrdinaryFallback &&
                                          !isChargeActiveFallback &&
                                          !isActiveGlideFallback;

            if ((_runtimeFeatureFlags.EnablePlayerFree2DLocalLocomotion ||
                 _runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion) &&
                isPlayerOrdinaryFallback)
            {
                reason = "PlayerCoveredLocomotionReachedLegacyExpansion";
                return true;
            }

            if (_runtimeFeatureFlags.EnableEnemyChargeKinematicLocomotion &&
                isChargeActiveFallback)
            {
                reason = "ChargeCoveredKinematicReachedLegacyExpansion";
                return true;
            }

            if (_runtimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion &&
                isEnemyOrdinaryFallback)
            {
                reason = "EnemyCoveredOrdinaryKinematicReachedLegacyExpansion";
                return true;
            }

            if (_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion &&
                TryResolveEnemyKinematicStartScope(snapshot, intent, out _, out _, out _, out _, out _, out var glideKinematicKind) &&
                glideKinematicKind != EnemyGlideKinematicKind.None)
            {
                reason = glideKinematicKind == EnemyGlideKinematicKind.LandingPendingEgress
                    ? "EnemyGlideLandingPendingKinematicReachedLegacyExpansion"
                    : "EnemyGlideActiveKinematicReachedLegacyExpansion";
                return true;
            }

            if (!_runtimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled &&
                (isPlayerOrdinaryFallback ||
                 isEnemyOrdinaryFallback ||
                 isChargeActiveFallback))
            {
                reason = "LegacyOrdinaryFallbackRequiresExplicitBaseline";
                return true;
            }

            if (isPlayerOrdinaryFallback)
            {
                reason = "PlayerLegacyFallbackRemovedFromRuntime";
                return true;
            }

            if (isEnemyOrdinaryFallback)
            {
                reason = "EnemyLegacyFallbackRemovedFromRuntime";
                return true;
            }

            if (isChargeActiveFallback)
            {
                reason = "ChargeLegacyFallbackRemovedFromRuntime";
                return true;
            }

            return false;
        }

        private static bool IsAllowedLegacyGridTransactionIntent(
            WorldSnapshot snapshot,
            in EntityState entity,
            MoveIntent intent)
        {
            var delta = intent.Destination - entity.position.PlanarPosition;
            if (!snapshot.TryResolveUnitStep(
                    entity.position,
                    delta,
                    out var destination,
                    out var rotationKind,
                    out var updatedTopology))
            {
                return snapshot.TryResolvePlayerStep(
                           entity.position,
                           delta,
                           out _,
                           out var playerRotationKind,
                           out _) &&
                       playerRotationKind != CubeRotationKind.None;
            }

            if (rotationKind != CubeRotationKind.None)
            {
                return true;
            }

            if (!snapshot.TryGetSolidSemanticAt(updatedTopology, destination, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box)
            {
                return false;
            }

            return (targetSemantic.Entity.boxCapabilities & BoxCapabilities.Item) == BoxCapabilities.Item;
        }

        private string FormatLocomotionFeatureFlags()
        {
            return
                $"PlayerFree2D={(_runtimeFeatureFlags.EnablePlayerFree2DLocalLocomotion ? 1 : 0)}," +
                $"PlayerFree2DTopology={(_runtimeFeatureFlags.EnablePlayerFree2DNativeTopologyTransition ? 1 : 0)}," +
                $"PlayerKinematic={(_runtimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion ? 1 : 0)}," +
                $"PlayerStoppable={(_runtimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion ? 1 : 0)}," +
                $"EnemyKinematic={(_runtimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion ? 1 : 0)}," +
                $"ChargeKinematic={(_runtimeFeatureFlags.EnableEnemyChargeKinematicLocomotion ? 1 : 0)}," +
                $"GlideKinematic={(_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion ? 1 : 0)}," +
                $"LegacyFallback={(_runtimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled ? 1 : 0)}";
        }

        private List<MoveIntent> BuildPlayerSameFaceKinematicLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            HashSet<int> consumedPlayerActionAttemptEntityIds,
            Dictionary<int, MovementActionPlanPayload> kinematicPayloads)
        {
            var legacyIntents = new List<MoveIntent>(sortedIntents.Count);
            var kinematicControlledPlayerIds = new HashSet<int>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!snapshot.TryGetPlayerControlState(entity.entityId, out _) ||
                    !snapshot.TryGetUnitKinematicPose(entity.entityId, out var pose) ||
                    pose.IsSettledAtAnchor ||
                    (pose.Mode != MotionMode.Voluntary &&
                     (!_runtimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion ||
                      pose.Mode != MotionMode.Held)))
                {
                    continue;
                }

                if (IsConsumedPlayerActionAttemptEntity(consumedPlayerActionAttemptEntityIds, entity.entityId))
                {
                    kinematicControlledPlayerIds.Add(entity.entityId);
                    continue;
                }

                kinematicControlledPlayerIds.Add(entity.entityId);
                if (!TryBuildPlayerKinematicContinuationPayload(
                        snapshot,
                        entity.entityId,
                        playerCommand,
                        tickIndex,
                        _runtimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion,
                        rejectedReasons,
                        out var continuationPayload))
                {
                    continue;
                }

                kinematicPayloads.Add(continuationPayload.ActionPlanId, continuationPayload);
            }

            if (_runtimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion)
            {
                for (var i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (kinematicControlledPlayerIds.Contains(entity.entityId) ||
                        IsConsumedPlayerActionAttemptEntity(consumedPlayerActionAttemptEntityIds, entity.entityId) ||
                        !TryBuildPlayerQueuedKinematicTurnStartPayload(
                            snapshot,
                            entity.entityId,
                            tickIndex,
                            rejectedReasons,
                            out var queuedTurnPayload))
                    {
                        continue;
                    }

                    kinematicControlledPlayerIds.Add(entity.entityId);
                    kinematicPayloads.Add(queuedTurnPayload.ActionPlanId, queuedTurnPayload);
                }
            }

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (kinematicControlledPlayerIds.Contains(intent.SourceId))
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

        private List<MoveIntent> BuildPlayerFree2DLocalLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            FinalizationBatch batch,
            HashSet<int> consumedPlayerActionAttemptEntityIds)
        {
            var legacyIntents = new List<MoveIntent>(sortedIntents.Count);
            var consumedFree2DIntentIds = new HashSet<int>();
            var topologyHandoffPlayerIds = new HashSet<int>();
            var settledApproachPlayerIds = new HashSet<int>();
            var skipTopologyApproachSettleFallbackPlayerIds = new HashSet<int>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!snapshot.TryGetPlayerControlState(entity.entityId, out var playerControlState) ||
                    entity.type != EntityType.Unit ||
                    entity.hp <= 0 ||
                    entity.markedForDeath)
                {
                    continue;
                }

                var consumedByActionAttempt = IsConsumedPlayerActionAttemptEntity(
                    consumedPlayerActionAttemptEntityIds,
                    entity.entityId);
                for (var intentIndex = 0; intentIndex < sortedIntents.Count; intentIndex++)
                {
                    var intent = sortedIntents[intentIndex];
                    if (intent.SourceId != entity.entityId ||
                        intent.CommandKind != Movement.MovementCommandKind.Move)
                    {
                        continue;
                    }

                    if (consumedByActionAttempt)
                    {
                        consumedFree2DIntentIds.Add(intent.IntentId);
                        continue;
                    }

                    var nativeTopologyDisposition = _runtimeFeatureFlags.EnablePlayerFree2DNativeTopologyTransition
                        ? TryMaterializePlayerFree2DNativeTopologyTransition(
                            snapshot,
                            entity,
                            playerControlState,
                            playerCommand,
                            intent,
                            rejectedReasons,
                            batch)
                        : PlayerFree2DNativeTopologyDisposition.NotCandidate;
                    if (nativeTopologyDisposition == PlayerFree2DNativeTopologyDisposition.Materialized)
                    {
                        consumedFree2DIntentIds.Add(intent.IntentId);
                        settledApproachPlayerIds.Add(entity.entityId);
                        continue;
                    }

                    if (nativeTopologyDisposition == PlayerFree2DNativeTopologyDisposition.TargetRejected)
                    {
                        consumedFree2DIntentIds.Add(intent.IntentId);
                        skipTopologyApproachSettleFallbackPlayerIds.Add(entity.entityId);
                        continue;
                    }

                    var handoffDisposition = ResolvePlayerFree2DTopologyHandoffIntent(
                            snapshot,
                            entity,
                            playerControlState,
                            intent);
                    if (handoffDisposition == PlayerFree2DTopologyHandoffIntentDisposition.LocalZeroHandoff)
                    {
                        topologyHandoffPlayerIds.Add(entity.entityId);
                        continue;
                    }

                    if (handoffDisposition == PlayerFree2DTopologyHandoffIntentDisposition.ApproachSettleAndHandoff ||
                        handoffDisposition == PlayerFree2DTopologyHandoffIntentDisposition.ApproachSettleOnly)
                    {
                        if (settledApproachPlayerIds.Add(entity.entityId))
                        {
                            MaterializePlayerFree2DTopologyApproachSettle(
                                entity,
                                playerCommand.HeldMoveDirection,
                                snapshot,
                                batch);
                        }

                        if (handoffDisposition == PlayerFree2DTopologyHandoffIntentDisposition.ApproachSettleAndHandoff)
                        {
                            topologyHandoffPlayerIds.Add(entity.entityId);
                            continue;
                        }

                        consumedFree2DIntentIds.Add(intent.IntentId);
                        continue;
                    }

                    consumedFree2DIntentIds.Add(intent.IntentId);
                }

                if (consumedByActionAttempt)
                {
                    continue;
                }

                if (topologyHandoffPlayerIds.Contains(entity.entityId) ||
                    settledApproachPlayerIds.Contains(entity.entityId))
                {
                    continue;
                }

                ResolvePlayerFree2DLocalLocomotion(
                    snapshot,
                    entity,
                    playerControlState,
                    playerCommand,
                    tickIndex,
                    rejectedReasons,
                    skipTopologyApproachSettleFallbackPlayerIds.Contains(entity.entityId),
                    batch);
            }

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (consumedFree2DIntentIds.Contains(intent.IntentId))
                {
                    continue;
                }

                legacyIntents.Add(intent);
            }

            return legacyIntents;
        }

        private PlayerFree2DNativeTopologyDisposition TryMaterializePlayerFree2DNativeTopologyTransition(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            MoveIntent intent,
            List<string> rejectedReasons,
            FinalizationBatch batch)
        {
            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                intent.SourceId != entity.entityId)
            {
                return PlayerFree2DNativeTopologyDisposition.NotCandidate;
            }

            if (playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState))
            {
                return PlayerFree2DNativeTopologyDisposition.NotCandidate;
            }

            if (!snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out var pose) ||
                pose.Mode == ContinuousLocomotionMode.AlignToAnchor)
            {
                return PlayerFree2DNativeTopologyDisposition.NotCandidate;
            }

            var directionDelta = intent.Destination - entity.position.PlanarPosition;
            if (Math.Abs(directionDelta.x) + Math.Abs(directionDelta.y) != 1)
            {
                return PlayerFree2DNativeTopologyDisposition.NotCandidate;
            }

            var delta = CreateContinuousDelta(
                pose.State,
                directionDelta,
                out _,
                out _,
                out var facing);
            if (delta.IsZero)
            {
                return PlayerFree2DNativeTopologyDisposition.NotCandidate;
            }

            if (!SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                    snapshot,
                    entity.entityId,
                    directionDelta,
                    delta,
                    _playerContinuousLocomotion.CollisionRadiusUnits,
                    out var transition))
            {
                if (ShouldRecordPlayerFree2DNativeTopologyReject(transition.RejectReason))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Plan|Source={entity.entityId}|I={intent.IntentId}|Reason=Free2DTopologyNativeRejected|RejectedBy={transition.RejectReason}|Anchor={FormatCell(entity.position)}");
                }

                return IsPlayerFree2DNativeTopologyTargetRejected(transition.RejectReason)
                    ? PlayerFree2DNativeTopologyDisposition.TargetRejected
                    : PlayerFree2DNativeTopologyDisposition.NotCandidate;
            }

            var movementMetadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                entity.entityId,
                actionPlanId: 0,
                intentId: intent.IntentId,
                rotationKind: transition.RotationKind,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.Free2DTopologyTransition,
                boundaryReason: "Free2DTopologyNativeTransition");
            var topologyMetadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                entity.entityId,
                actionPlanId: 0,
                intentId: intent.IntentId,
                rotationKind: transition.RotationKind,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.Free2DTopologyTransition,
                boundaryReason: "Free2DTopologyNativeTransition");
            var resolvedState = new UnitContinuousLocomotionState
            {
                localOffset = transition.TargetLocalOffset,
                velocity = transition.TargetVelocity,
                facing = facing,
                lastMoveDirection = playerCommand.HeldMoveDirection == Direction.None
                    ? facing
                    : playerCommand.HeldMoveDirection,
                speedUnitsPerTick = _playerContinuousLocomotion.SpeedUnitsPerTick,
                mode = transition.TargetVelocity.IsZero
                    ? ContinuousLocomotionMode.Idle
                    : ContinuousLocomotionMode.Moving,
                sequenceId = pose.State.sequenceId + 1,
                subUnitRemainderX = transition.TargetResidualX,
                subUnitRemainderY = transition.TargetResidualY,
            }.NormalizedForStorage();

            batch.SetTopology(transition.UpdatedTopology, topologyMetadata);
            batch.MoveEntity(entity.entityId, transition.TargetAnchor, movementMetadata);
            batch.SetUnitContinuousLocomotionState(entity.entityId, resolvedState, movementMetadata);
            if (entity.facing != facing)
            {
                batch.SetFacing(entity.entityId, facing, movementMetadata);
            }

            rejectedReasons.Add(
                $"Free2DTopologyNativeTransition|Stage=Plan|E={entity.entityId}|I={intent.IntentId}|Rot={transition.RotationKind}|From={FormatCell(transition.SourceAnchor)}|To={FormatCell(transition.TargetAnchor)}|SourceOffset={transition.SourceLocalOffset}|TargetOffset={transition.TargetLocalOffset}");
            return PlayerFree2DNativeTopologyDisposition.Materialized;
        }

        private static bool ShouldRecordPlayerFree2DNativeTopologyReject(Free2DTopologyTransitionRejectReason reason)
        {
            return reason != Free2DTopologyTransitionRejectReason.UnsupportedSeam &&
                   reason != Free2DTopologyTransitionRejectReason.CrossingAxisDidNotReachSeam &&
                   reason != Free2DTopologyTransitionRejectReason.NonCardinalDelta &&
                   reason != Free2DTopologyTransitionRejectReason.MissingEntity &&
                   reason != Free2DTopologyTransitionRejectReason.NonUnit &&
                   reason != Free2DTopologyTransitionRejectReason.MissingContinuousPose;
        }

        private static bool IsPlayerFree2DNativeTopologyTargetRejected(Free2DTopologyTransitionRejectReason reason)
        {
            return reason == Free2DTopologyTransitionRejectReason.TopologyTransitionUnavailable ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTerrain ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedByUnit ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedByReservation ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked ||
                   reason == Free2DTopologyTransitionRejectReason.RemapInvalid;
        }

        private enum PlayerFree2DTopologyHandoffIntentDisposition
        {
            None = 0,
            LocalZeroHandoff = 1,
            ApproachSettleAndHandoff = 2,
            ApproachSettleOnly = 3,
        }

        private enum PlayerFree2DNativeTopologyDisposition
        {
            NotCandidate = 0,
            Materialized = 1,
            TargetRejected = 2,
        }

        private PlayerFree2DTopologyHandoffIntentDisposition ResolvePlayerFree2DTopologyHandoffIntent(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            MoveIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                intent.SourceId != entity.entityId)
            {
                return PlayerFree2DTopologyHandoffIntentDisposition.None;
            }

            var delta = intent.Destination - entity.position.PlanarPosition;
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                return PlayerFree2DTopologyHandoffIntentDisposition.None;
            }

            if (!TryResolvePlayerFree2DTopologyTransition(snapshot, entity.position, delta))
            {
                return PlayerFree2DTopologyHandoffIntentDisposition.None;
            }

            if (IsPlayerFree2DTopologyLocalZeroHandoffEligible(snapshot, entity.entityId, playerControlState))
            {
                return PlayerFree2DTopologyHandoffIntentDisposition.LocalZeroHandoff;
            }

            if (!IsPlayerFree2DTopologyApproachSettleEligible(
                    snapshot,
                    entity,
                    playerControlState,
                    delta,
                    out var disposition))
            {
                return PlayerFree2DTopologyHandoffIntentDisposition.None;
            }

            return disposition;
        }

        private static bool TryResolvePlayerFree2DTopologyTransition(
            WorldSnapshot snapshot,
            SurfaceCell source,
            Vector2Int delta)
        {
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                return false;
            }

            if (!snapshot.TryResolvePlayerStep(
                    source,
                    delta,
                    out var destination,
                    out var rotationKind,
                    out var updatedTopology))
            {
                return false;
            }

            return rotationKind != CubeRotationKind.None ||
                   !updatedTopology.Equals(snapshot.Topology) ||
                   destination.face != source.face;
        }

        private static bool IsPlayerFree2DTopologyLocalZeroHandoffEligible(
            WorldSnapshot snapshot,
            int entityId,
            in PlayerControlState playerControlState)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState))
            {
                return false;
            }

            if (!snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var continuousPose) ||
                !continuousPose.IsSettledAtAnchor ||
                continuousPose.Mode != ContinuousLocomotionMode.Idle)
            {
                return false;
            }

            return snapshot.TryGetUnitKinematicPose(entityId, out var kinematicPose) &&
                   kinematicPose.IsSettledAtAnchor;
        }

        private bool IsPlayerFree2DTopologyApproachSettleEligible(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            Vector2Int directionDelta,
            out PlayerFree2DTopologyHandoffIntentDisposition disposition)
        {
            disposition = PlayerFree2DTopologyHandoffIntentDisposition.None;
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState))
            {
                return false;
            }

            if (!snapshot.TryGetUnitKinematicPose(entity.entityId, out var kinematicPose) ||
                !kinematicPose.IsSettledAtAnchor ||
                !snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out var continuousPose) ||
                continuousPose.Mode == ContinuousLocomotionMode.AlignToAnchor ||
                continuousPose.IsSettledAtAnchor ||
                continuousPose.AnchorCell != entity.position)
            {
                return false;
            }

            var axisOffset = directionDelta.x != 0
                ? continuousPose.LocalOffset.X.RawValue
                : continuousPose.LocalOffset.Y.RawValue;
            var perpendicularOffset = directionDelta.x != 0
                ? continuousPose.LocalOffset.Y.RawValue
                : continuousPose.LocalOffset.X.RawValue;
            if (perpendicularOffset != 0)
            {
                return false;
            }

            var directionSign = directionDelta.x != 0 ? Math.Sign(directionDelta.x) : Math.Sign(directionDelta.y);
            if (directionSign == 0)
            {
                return false;
            }

            var delta = CreateContinuousDelta(
                continuousPose.State,
                directionDelta,
                out _,
                out _,
                out _);
            var axisDelta = directionDelta.x != 0 ? delta.X.RawValue : delta.Y.RawValue;
            if (axisDelta == 0 || Math.Sign(axisDelta) != directionSign)
            {
                return false;
            }

            var projectedOffset = axisOffset + axisDelta;
            var reachesCenterThisTick = directionSign > 0
                ? axisOffset < 0 && projectedOffset >= 0
                : axisOffset > 0 && projectedOffset <= 0;
            if (reachesCenterThisTick &&
                Math.Abs(axisOffset) <= Math.Abs(axisDelta) + 1)
            {
                disposition = PlayerFree2DTopologyHandoffIntentDisposition.ApproachSettleAndHandoff;
                return true;
            }

            var seamSideNonZero = directionSign > 0
                ? axisOffset > 0
                : axisOffset < 0;
            if (seamSideNonZero)
            {
                disposition = PlayerFree2DTopologyHandoffIntentDisposition.ApproachSettleOnly;
                return true;
            }

            return false;
        }

        private void MaterializePlayerFree2DTopologyApproachSettle(
            in EntityState entity,
            Direction direction,
            WorldSnapshot snapshot,
            FinalizationBatch batch)
        {
            if (!snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out var pose))
            {
                return;
            }

            var facing = direction == Direction.Up ||
                         direction == Direction.Right ||
                         direction == Direction.Down ||
                         direction == Direction.Left
                ? direction
                : entity.facing;
            var settledState = new UnitContinuousLocomotionState
            {
                localOffset = KinematicOffset2.Zero,
                velocity = KinematicVelocity2.Zero,
                facing = facing,
                lastMoveDirection = facing == Direction.None ? null : facing,
                speedUnitsPerTick = _playerContinuousLocomotion.SpeedUnitsPerTick,
                mode = ContinuousLocomotionMode.Idle,
                sequenceId = pose.State.sequenceId + 1,
            }.NormalizedForStorage();
            var metadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                entity.entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitOrdinaryLocomotion,
                boundaryReason: "PlayerFree2DTopologyApproachSettle");
            batch.SetUnitContinuousLocomotionState(entity.entityId, settledState, metadata);
            if (facing != Direction.None && entity.facing != facing)
            {
                batch.SetFacing(entity.entityId, facing, metadata);
            }
        }

        private void ResolvePlayerFree2DLocalLocomotion(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            bool skipTopologyApproachSettleFallback,
            FinalizationBatch batch)
        {
            if (!snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out var pose))
            {
                return;
            }

            var effectivePlayerControlState = playerControlState;
            if (_runtimeFeatureFlags.EnablePlayerFree2DActionAssist &&
                TryQueueFree2DActionAssist(
                    snapshot,
                    entity,
                    pose,
                    playerControlState,
                    playerCommand,
                    tickIndex,
                    rejectedReasons,
                    batch,
                    out var queuedPlayerControlState))
            {
                effectivePlayerControlState = queuedPlayerControlState;
            }

            if (_runtimeFeatureFlags.EnablePlayerFree2DActionAssist &&
                PlayerControlQueries.HasQueuedFree2DAction(effectivePlayerControlState))
            {
                ResolvePlayerFree2DActionAssistAlign(
                    snapshot,
                    entity,
                    pose,
                    effectivePlayerControlState,
                    tickIndex,
                    rejectedReasons,
                    batch);
                return;
            }

            var hasDirection = TryResolveDirectionDelta(playerCommand.HeldMoveDirection, out var directionDelta);
            var actionInputBlocksFree2DMovement =
                (playerCommand.PushPressed || playerCommand.FlipPressed) &&
                playerCommand.HeldMoveDirection == Direction.None;
            var canMove =
                !actionInputBlocksFree2DMovement &&
                !effectivePlayerControlState.activeAction.IsActive &&
                !PlayerControlQueries.IsMoveOnCooldown(effectivePlayerControlState, tickIndex) &&
                hasDirection;
            if (!canMove)
            {
                if (pose.HasAuthoritativeState &&
                    (!pose.State.velocity.IsZero || pose.State.mode != ContinuousLocomotionMode.Idle))
                {
                    batch.SetUnitContinuousLocomotionState(
                        entity.entityId,
                        UnitContinuousLocomotionState.CreateIdleFreeze(pose.State),
                        new FinalizationOperationMetadata(
                            TickPhase.Plan,
                            ResolvedActionSemanticKind.Stop,
                            entity.entityId,
                            actionPlanId: 0));
                }

                return;
            }

            var delta = CreateContinuousDelta(
                pose.State,
                directionDelta,
                out var nextRemainderX,
                out var nextRemainderY,
                out var facing);
            if (delta.IsZero)
            {
                return;
            }

            if (!SurfaceContinuousLocomotionQueries.TryResolveSameFaceAxisMove(
                    snapshot,
                    entity.entityId,
                    delta,
                    _playerContinuousLocomotion.CollisionRadiusUnits,
                    out var sweep))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entity.entityId}|Reason=Free2DContinuousSweepRejected|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
                return;
            }

            if (sweep.Blocked &&
                sweep.RejectedBy == ContinuousLocomotionRejectionReason.TopologySeam &&
                !skipTopologyApproachSettleFallback &&
                TryResolvePlayerFree2DTopologyTransition(snapshot, entity.position, directionDelta) &&
                IsPlayerFree2DTopologyApproachSettleEligible(
                    snapshot,
                    entity,
                    effectivePlayerControlState,
                    directionDelta,
                    out _))
            {
                MaterializePlayerFree2DTopologyApproachSettle(
                    entity,
                    playerCommand.HeldMoveDirection,
                    snapshot,
                    batch);
                return;
            }

            var resolvedState = CreateContinuousLocomotionState(
                pose.State,
                sweep,
                facing,
                playerCommand.HeldMoveDirection,
                nextRemainderX,
                nextRemainderY);
            var stateMetadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                entity.entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitOrdinaryLocomotion,
                boundaryReason: "PlayerFree2DLocalLocomotion");
            var anchorMetadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                entity.entityId,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                boundaryReason: "PlayerFree2DAnchorNormalization");

            if (sweep.AnchorChanged)
            {
                batch.MoveEntity(entity.entityId, sweep.ResolvedAnchorCell, anchorMetadata);
            }

            batch.SetUnitContinuousLocomotionState(entity.entityId, resolvedState, stateMetadata);
            if (entity.facing != facing)
            {
                batch.SetFacing(entity.entityId, facing, stateMetadata);
            }

            if (sweep.Blocked)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entity.entityId}|Reason=Free2DContinuousBlocked|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
            }
        }

        private void CollectPreMovementPlayerActionAttemptResolutions(
            WorldSnapshot snapshot,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            FinalizationBatch batch,
            List<PlayerActionAttemptResolution> playerActionAttemptResolutions)
        {
            CollectPreMovementPlayerActionAttemptResolutions(
                snapshot,
                playerCommand,
                tickIndex,
                rejectedReasons,
                batch,
                playerActionAttemptResolutions,
                consumedPlayerActionAttemptEntityIds: null);
        }

        private void CollectPreMovementPlayerActionAttemptResolutions(
            WorldSnapshot snapshot,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            FinalizationBatch batch,
            List<PlayerActionAttemptResolution> playerActionAttemptResolutions,
            HashSet<int> consumedPlayerActionAttemptEntityIds)
        {
            if (!TryResolveAttemptActionKind(playerCommand, out _))
            {
                return;
            }

            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!snapshot.TryGetPlayerControlState(entity.entityId, out var playerControlState) ||
                    entity.type != EntityType.Unit ||
                    entity.hp <= 0 ||
                    entity.markedForDeath ||
                    HasPlayerActionAttemptResolution(playerActionAttemptResolutions, entity.entityId))
                {
                    continue;
                }

                if (_runtimeFeatureFlags.EnablePlayerFree2DActionAssist &&
                    snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out var free2DPose) &&
                    TryQueueFree2DActionAssist(
                        snapshot,
                        entity,
                        free2DPose,
                        playerControlState,
                        playerCommand,
                        tickIndex,
                        rejectedReasons,
                        batch,
                        out _))
                {
                    continue;
                }

                if (!TryCreatePlayerActionAttemptResolution(
                        snapshot,
                        entity,
                        playerControlState,
                        playerCommand,
                        snapshot.TryGetUnitContinuousLocomotionPose(entity.entityId, out free2DPose)
                            ? free2DPose
                            : null,
                        out var attemptResolution))
                {
                    continue;
                }

                AddPlayerActionAttemptResolution(playerActionAttemptResolutions, attemptResolution);
                if (attemptResolution.ConsumesMovement)
                {
                    consumedPlayerActionAttemptEntityIds?.Add(entity.entityId);
                }
            }
        }

        private static bool TryCreatePlayerActionAttemptResolution(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            UnitContinuousLocomotionPose? free2DPose,
            out PlayerActionAttemptResolution resolution)
        {
            resolution = default;
            if (!TryResolveAttemptActionKind(playerCommand, out var actionKind) ||
                playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState))
            {
                return false;
            }

            var direction = ResolvePlayerActionAttemptFeedbackDirection(playerCommand, entity);
            var queuedActionKind = PlayerControlQueries.ToQueuedFree2DActionKind(actionKind);
            var feedbackKind = PlayerActionAttemptFeedbackKind.NoTarget;
            var targetEntityId = 0;
            var hasTarget = false;

            if (queuedActionKind == PlayerQueuedFree2DActionKind.None ||
                direction == Direction.None)
            {
                feedbackKind = PlayerActionAttemptFeedbackKind.Invalid;
            }
            else
            {
                var anchor = free2DPose.HasValue
                    ? free2DPose.Value.AnchorCell
                    : entity.position;
                if (PlayerControlQueries.TryResolveFree2DActionAssistCandidate(
                        snapshot,
                        entity,
                        anchor,
                        queuedActionKind,
                        direction,
                        out var target))
                {
                    targetEntityId = target.TargetEntityId;
                    hasTarget = true;
                    feedbackKind = free2DPose.HasValue &&
                                   !free2DPose.Value.State.localOffset.IsZero &&
                                   !IsWithinFree2DActionAssistSettleWindow(
                                       free2DPose.Value.State.localOffset,
                                       PlayerContinuousLocomotionSettings.CreateDefault()
                                           .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                                           .ActionAssistSettleWindowUnits)
                        ? PlayerActionAttemptFeedbackKind.AssistOutOfRange
                        : PlayerActionAttemptFeedbackKind.Invalid;
                }
                else if (actionKind == PlayerActionKind.Push &&
                         PlayerControlQueries.TryResolveAdjacentPushTarget(
                             snapshot,
                             entity,
                             direction,
                             out var adjacentTarget))
                {
                    targetEntityId = adjacentTarget.TargetEntityId;
                    hasTarget = true;
                    feedbackKind = PlayerActionAttemptFeedbackKind.Invalid;
                }
            }

            resolution = new PlayerActionAttemptResolution(
                entity.entityId,
                actionKind,
                direction,
                feedbackKind,
                consumesMovement: true,
                emitsFakePresentation: true,
                targetEntityId,
                hasTarget);
            return true;
        }

        private static bool IsWithinFree2DActionAssistSettleWindow(
            KinematicOffset2 localOffset,
            int actionAssistSettleWindowUnits)
        {
            var windowUnits = Math.Max(0, actionAssistSettleWindowUnits);
            return Math.Abs(localOffset.X.RawValue) <= windowUnits &&
                Math.Abs(localOffset.Y.RawValue) <= windowUnits;
        }

        private bool TryCreatePlayerActionAttemptResolution(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            UnitContinuousLocomotionPose free2DPose,
            out PlayerActionAttemptResolution resolution)
        {
            resolution = default;
            if (!TryResolveAttemptActionKind(playerCommand, out var actionKind) ||
                playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState))
            {
                return false;
            }

            var direction = ResolvePlayerActionAttemptFeedbackDirection(playerCommand, entity);
            var queuedActionKind = PlayerControlQueries.ToQueuedFree2DActionKind(actionKind);
            var feedbackKind = PlayerActionAttemptFeedbackKind.NoTarget;
            var targetEntityId = 0;
            var hasTarget = false;

            if (queuedActionKind == PlayerQueuedFree2DActionKind.None ||
                direction == Direction.None)
            {
                feedbackKind = PlayerActionAttemptFeedbackKind.Invalid;
            }
            else if (PlayerControlQueries.TryResolveFree2DActionAssistCandidate(
                         snapshot,
                         entity,
                         free2DPose.AnchorCell,
                         queuedActionKind,
                         direction,
                         out var target))
            {
                targetEntityId = target.TargetEntityId;
                hasTarget = true;
                feedbackKind = !free2DPose.State.localOffset.IsZero &&
                               !IsWithinFree2DActionAssistSettleWindow(free2DPose.State.localOffset)
                    ? PlayerActionAttemptFeedbackKind.AssistOutOfRange
                    : PlayerActionAttemptFeedbackKind.Invalid;
            }
            else if (actionKind == PlayerActionKind.Push &&
                     PlayerControlQueries.TryResolveAdjacentPushTarget(
                         snapshot,
                         entity,
                         direction,
                         out var adjacentTarget))
            {
                targetEntityId = adjacentTarget.TargetEntityId;
                hasTarget = true;
                feedbackKind = PlayerActionAttemptFeedbackKind.Invalid;
            }

            resolution = new PlayerActionAttemptResolution(
                entity.entityId,
                actionKind,
                direction,
                feedbackKind,
                consumesMovement: true,
                emitsFakePresentation: true,
                targetEntityId,
                hasTarget);
            return true;
        }

        private static bool TryResolveAttemptActionKind(
            PlayerTickCommand playerCommand,
            out PlayerActionKind actionKind)
        {
            if (playerCommand.PushPressed)
            {
                actionKind = PlayerActionKind.Push;
                return true;
            }

            if (playerCommand.FlipPressed)
            {
                actionKind = PlayerActionKind.Flip;
                return true;
            }

            actionKind = PlayerActionKind.None;
            return false;
        }

        private static Direction ResolvePlayerActionAttemptFeedbackDirection(
            PlayerTickCommand playerCommand,
            in EntityState entity)
        {
            if (TryResolveDirectionDelta(playerCommand.MoveDirection, out _))
            {
                return playerCommand.MoveDirection;
            }

            if (TryResolveDirectionDelta(playerCommand.HeldMoveDirection, out _))
            {
                return playerCommand.HeldMoveDirection;
            }

            return TryResolveDirectionDelta(entity.facing, out _)
                ? entity.facing
                : Direction.Up;
        }

        private static void AddPlayerActionAttemptResolution(
            List<PlayerActionAttemptResolution> playerActionAttemptResolutions,
            in PlayerActionAttemptResolution resolution)
        {
            if (!resolution.HasAttempt ||
                HasPlayerActionAttemptResolution(playerActionAttemptResolutions, resolution.EntityId))
            {
                return;
            }

            playerActionAttemptResolutions.Add(resolution);
        }

        private static bool HasPlayerActionAttemptResolution(
            IReadOnlyList<PlayerActionAttemptResolution> playerActionAttemptResolutions,
            int entityId)
        {
            for (var i = 0; i < playerActionAttemptResolutions.Count; i++)
            {
                if (playerActionAttemptResolutions[i].EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryQueueFree2DActionAssist(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitContinuousLocomotionPose pose,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            FinalizationBatch batch,
            out PlayerControlState queuedPlayerControlState)
        {
            queuedPlayerControlState = playerControlState;
            if (playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState) ||
                pose.State.localOffset.IsZero ||
                !TryResolveQueuedFree2DActionKind(playerCommand, out var actionKind) ||
                !TryResolveDirectionDelta(playerCommand.MoveDirection, out _))
            {
                return false;
            }

            if (!IsWithinFree2DActionAssistSettleWindow(pose.State.localOffset))
            {
                rejectedReasons.Add(
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=OutsideSettleWindow|Source={entity.entityId}|Kind={actionKind}|Direction={playerCommand.MoveDirection}|Offset={pose.LocalOffset}|Window={_playerContinuousLocomotion.ActionAssistSettleWindowUnits}");
                return false;
            }

            if (!PlayerControlQueries.HasFree2DActionAssistCandidate(
                    snapshot,
                    entity,
                    pose.AnchorCell,
                    actionKind,
                    playerCommand.MoveDirection))
            {
                rejectedReasons.Add(
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=NoActionCandidate|Source={entity.entityId}|Kind={actionKind}|Direction={playerCommand.MoveDirection}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}");
                return false;
            }

            queuedPlayerControlState = PlayerControlQueries.QueueFree2DAction(
                playerControlState,
                actionKind,
                playerCommand.MoveDirection,
                tickIndex);
            batch.SetPlayerControlState(
                entity.entityId,
                queuedPlayerControlState,
                new FinalizationOperationMetadata(
                    TickPhase.Plan,
                    ResolvedActionSemanticKind.Stop,
                    entity.entityId,
                    actionPlanId: 0));
            rejectedReasons.Add(
                $"Free2DActionAssistQueued|Stage=Plan|Source={entity.entityId}|Kind={actionKind}|Direction={playerCommand.MoveDirection}|RequestedTick={tickIndex}|Anchor={FormatCell(entity.position)}|Offset={pose.LocalOffset}");
            return true;
        }

        private bool IsWithinFree2DActionAssistSettleWindow(KinematicOffset2 localOffset)
        {
            var windowUnits = Math.Max(0, _playerContinuousLocomotion.ActionAssistSettleWindowUnits);
            return Math.Abs(localOffset.X.RawValue) <= windowUnits &&
                Math.Abs(localOffset.Y.RawValue) <= windowUnits;
        }

        private void ResolvePlayerFree2DActionAssistAlign(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitContinuousLocomotionPose pose,
            in PlayerControlState playerControlState,
            int tickIndex,
            List<string> rejectedReasons,
            FinalizationBatch batch)
        {
            if (pose.State.localOffset.IsZero)
            {
                rejectedReasons.Add(
                    $"Free2DActionAssistAlign|Stage=Plan|Source={entity.entityId}|State=Settled|Kind={playerControlState.queuedFree2DAction.kind}|Direction={playerControlState.queuedFree2DAction.direction}|RequestedTick={playerControlState.queuedFree2DAction.requestedTick}|Anchor={FormatCell(entity.position)}");
                return;
            }

            var queuedAction = playerControlState.queuedFree2DAction;
            if (!PlayerControlQueries.HasFree2DActionAssistCandidate(
                    snapshot,
                    entity,
                    pose.AnchorCell,
                    queuedAction.kind,
                    queuedAction.direction))
            {
                batch.SetPlayerControlState(
                    entity.entityId,
                    PlayerControlQueries.ClearQueuedFree2DAction(playerControlState),
                    new FinalizationOperationMetadata(
                        TickPhase.Plan,
                        ResolvedActionSemanticKind.Stop,
                        entity.entityId,
                        actionPlanId: 0));
                if (pose.HasAuthoritativeState &&
                    (!pose.State.velocity.IsZero || pose.State.mode != ContinuousLocomotionMode.Idle))
                {
                    batch.SetUnitContinuousLocomotionState(
                        entity.entityId,
                        UnitContinuousLocomotionState.CreateIdleFreeze(pose.State),
                        new FinalizationOperationMetadata(
                            TickPhase.Plan,
                            ResolvedActionSemanticKind.Stop,
                            entity.entityId,
                            actionPlanId: 0));
                }

                rejectedReasons.Add(
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=NoActionCandidate|Source={entity.entityId}|Kind={queuedAction.kind}|Direction={queuedAction.direction}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}|RequestedTick={queuedAction.requestedTick}");
                rejectedReasons.Add(
                    $"Free2DActionAssistCleared|Stage=Plan|Source={entity.entityId}|Reason=NoActionCandidate");
                return;
            }

            var alignedState = CreateAlignToAnchorState(pose.State);
            batch.SetUnitContinuousLocomotionState(
                entity.entityId,
                alignedState,
                new FinalizationOperationMetadata(
                    TickPhase.Plan,
                    ResolvedActionSemanticKind.Move,
                    entity.entityId,
                    actionPlanId: 0,
                    movementSemanticKind: MovementSemanticKind.Move));
            rejectedReasons.Add(
                $"Free2DActionAssistAlign|Stage=Plan|Source={entity.entityId}|Kind={playerControlState.queuedFree2DAction.kind}|Direction={playerControlState.queuedFree2DAction.direction}|RequestedTick={playerControlState.queuedFree2DAction.requestedTick}|Anchor={FormatCell(entity.position)}|From={pose.LocalOffset}|To={alignedState.localOffset}|Mode={alignedState.mode}");
        }

        private UnitContinuousLocomotionState CreateAlignToAnchorState(UnitContinuousLocomotionState sourceState)
        {
            var normalizedSource = sourceState.NormalizedForStorage();
            var nextX = normalizedSource.localOffset.X.RawValue;
            var nextY = normalizedSource.localOffset.Y.RawValue;
            var nextRemainderX = normalizedSource.subUnitRemainderX;
            var nextRemainderY = normalizedSource.subUnitRemainderY;
            var deltaX = 0;
            var deltaY = 0;
            var alignOnX = SelectAlignAxisX(normalizedSource.localOffset);
            if (alignOnX)
            {
                deltaX = CreateAlignAxisDelta(
                    nextX,
                    ref nextRemainderX);
                nextX += deltaX;
                if (nextX == 0)
                {
                    nextRemainderX = 0;
                }
            }
            else
            {
                deltaY = CreateAlignAxisDelta(
                    nextY,
                    ref nextRemainderY);
                nextY += deltaY;
                if (nextY == 0)
                {
                    nextRemainderY = 0;
                }
            }

            var nextOffset = new KinematicOffset2(
                KinematicFixed.FromRaw(nextX),
                KinematicFixed.FromRaw(nextY));
            var isSettled = nextOffset.IsZero;
            return new UnitContinuousLocomotionState
            {
                localOffset = nextOffset,
                velocity = isSettled
                    ? KinematicVelocity2.Zero
                    : new KinematicVelocity2(
                        KinematicFixed.FromRaw(deltaX),
                        KinematicFixed.FromRaw(deltaY)),
                facing = normalizedSource.facing,
                lastMoveDirection = normalizedSource.lastMoveDirection,
                speedUnitsPerTick = Math.Abs(deltaX) + Math.Abs(deltaY),
                mode = isSettled
                    ? ContinuousLocomotionMode.Idle
                    : ContinuousLocomotionMode.AlignToAnchor,
                sequenceId = normalizedSource.sequenceId + 1,
                subUnitRemainderX = isSettled ? 0 : nextRemainderX,
                subUnitRemainderY = isSettled ? 0 : nextRemainderY,
            }.NormalizedForStorage();
        }

        private int CreateAlignAxisDelta(int offsetRaw, ref int axisRemainder)
        {
            if (offsetRaw == 0)
            {
                axisRemainder = 0;
                return 0;
            }

            var rawUnits = _playerContinuousLocomotion.SpeedUnitsPerTick;
            axisRemainder += _playerContinuousLocomotion.UnitsPerTickRemainder;
            if (axisRemainder >= _playerContinuousLocomotion.TicksPerCell)
            {
                rawUnits++;
                axisRemainder -= _playerContinuousLocomotion.TicksPerCell;
            }

            var step = Math.Min(Math.Abs(offsetRaw), rawUnits);
            return offsetRaw > 0 ? -step : step;
        }

        private static bool SelectAlignAxisX(KinematicOffset2 offset)
        {
            return Math.Abs(offset.X.RawValue) >= Math.Abs(offset.Y.RawValue);
        }

        private static bool TryResolveQueuedFree2DActionKind(
            PlayerTickCommand playerCommand,
            out PlayerQueuedFree2DActionKind actionKind)
        {
            if (playerCommand.PushPressed)
            {
                actionKind = PlayerQueuedFree2DActionKind.Push;
                return true;
            }

            if (playerCommand.FlipPressed)
            {
                actionKind = PlayerQueuedFree2DActionKind.Flip;
                return true;
            }

            actionKind = PlayerQueuedFree2DActionKind.None;
            return false;
        }

        private KinematicVelocity2 CreateContinuousDelta(
            UnitContinuousLocomotionState sourceState,
            Vector2Int directionDelta,
            out int nextRemainderX,
            out int nextRemainderY,
            out Direction facing)
        {
            nextRemainderX = sourceState.subUnitRemainderX;
            nextRemainderY = sourceState.subUnitRemainderY;
            var rawUnits = _playerContinuousLocomotion.SpeedUnitsPerTick;
            if (directionDelta.x != 0)
            {
                nextRemainderX += _playerContinuousLocomotion.UnitsPerTickRemainder;
                if (nextRemainderX >= _playerContinuousLocomotion.TicksPerCell)
                {
                    rawUnits++;
                    nextRemainderX -= _playerContinuousLocomotion.TicksPerCell;
                }

                facing = directionDelta.x > 0 ? Direction.Right : Direction.Left;
                return new KinematicVelocity2(
                    KinematicFixed.FromRaw(directionDelta.x * rawUnits),
                    KinematicFixed.Zero);
            }

            nextRemainderY += _playerContinuousLocomotion.UnitsPerTickRemainder;
            if (nextRemainderY >= _playerContinuousLocomotion.TicksPerCell)
            {
                rawUnits++;
                nextRemainderY -= _playerContinuousLocomotion.TicksPerCell;
            }

            facing = directionDelta.y > 0 ? Direction.Up : Direction.Down;
            return new KinematicVelocity2(
                KinematicFixed.Zero,
                KinematicFixed.FromRaw(directionDelta.y * rawUnits));
        }

        private static UnitContinuousLocomotionState CreateContinuousLocomotionState(
            UnitContinuousLocomotionState sourceState,
            ContinuousLocomotionSweepResult sweep,
            Direction facing,
            Direction lastMoveDirection,
            int nextRemainderX,
            int nextRemainderY)
        {
            var velocity = sweep.Blocked ? KinematicVelocity2.Zero : sweep.ResolvedVelocity;
            var mode = velocity.IsZero
                ? ContinuousLocomotionMode.Idle
                : ContinuousLocomotionMode.Moving;
            return new UnitContinuousLocomotionState
            {
                localOffset = sweep.ResolvedLocalOffset,
                velocity = velocity,
                facing = facing,
                lastMoveDirection = lastMoveDirection == Direction.None ? null : lastMoveDirection,
                speedUnitsPerTick = Math.Abs(sweep.ResolvedVelocity.X.RawValue) + Math.Abs(sweep.ResolvedVelocity.Y.RawValue),
                mode = mode,
                sequenceId = sourceState.sequenceId + 1,
                subUnitRemainderX = nextRemainderX,
                subUnitRemainderY = nextRemainderY,
            }.NormalizedForStorage();
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

        private List<MoveIntent> BuildEnemyChargeKinematicLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            List<string> rejectedReasons,
            Dictionary<int, MovementActionPlanPayload> kinematicPayloads)
        {
            var legacyIntents = new List<MoveIntent>(sortedIntents.Count);
            var continuingChargeEnemyIds = new HashSet<int>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!TryBuildEnemyChargeKinematicContinuationPayload(
                        snapshot,
                        entity.entityId,
                        tickIndex,
                        rejectedReasons,
                        out var continuationPayload))
                {
                    continue;
                }

                kinematicPayloads.Add(continuationPayload.ActionPlanId, continuationPayload);
                continuingChargeEnemyIds.Add(entity.entityId);
            }

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (continuingChargeEnemyIds.Contains(intent.SourceId))
                {
                    if (intent.CommandKind == Movement.MovementCommandKind.Move)
                    {
                        continue;
                    }

                    legacyIntents.Add(intent);
                    continue;
                }

                if (TryBuildEnemyChargeKinematicStartPayload(
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
                    out var facing,
                    out var glideKinematicKind))
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

            var totalTicks = intent.OrdinaryKinematicMoveTicks > 0
                ? intent.OrdinaryKinematicMoveTicks
                : _playerKinematicLocomotionTiming.TicksPerCell;
            var outcome = CreateKinematicMotionOutcome(
                sweep,
                entity.position,
                sourceState: pose.State,
                stepDirectionX: delta.x,
                stepDirectionY: delta.y,
                elapsedTicks: 1,
                totalTicks: totalTicks,
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
                executionLockWrites: CreateEnemyKinematicExecutionLockWrites(snapshot, entity, tickIndex),
                executionBoundaryKind: glideKinematicKind != EnemyGlideKinematicKind.None
                    ? MovementExecutionBoundaryKind.UnitSpecialLocomotion
                    : MovementExecutionBoundaryKind.UnitOrdinaryLocomotion,
                boundaryReason: ResolveEnemyKinematicStartBoundaryReason(glideKinematicKind));
            return true;
        }

        private bool TryBuildEnemyChargeKinematicStartPayload(
            WorldSnapshot snapshot,
            MoveIntent intent,
            int tickIndex,
            List<string> rejectedReasons,
            out bool handledByKinematic,
            out MovementActionPlanPayload payload)
        {
            handledByKinematic = false;
            payload = null;

            if (!TryResolveEnemyChargeKinematicStartScope(
                    snapshot,
                    intent,
                    out var entity,
                    out var pose,
                    out var delta,
                    out var facing))
            {
                return false;
            }

            handledByKinematic = true;
            if (!EnemyMovementStrategyShared.CanTraverseChargeStepIgnoringUnits(snapshot, entity, delta))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyChargeKinematicTraversalBlocked|Anchor={FormatCell(entity.position)}");
                return true;
            }

            var totalTicks = ResolveChargeKinematicStepTicks(intent.MoveCooldownTicks);
            var outcome = CreateKinematicMotionOutcome(
                entity.entityId,
                pose.AnchorCell,
                pose.LocalOffset,
                pose.State,
                delta.x,
                delta.y,
                elapsedTicks: 1,
                totalTicks: totalTicks,
                startedTick: tickIndex,
                mode: MotionMode.Charge);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                intent.IntentId,
                entity.entityId,
                intent.Priority,
                outcome,
                facing,
                writeFacing: true,
                executionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion,
                boundaryReason: "EnemyChargeKinematicActiveStep");
            return true;
        }

        private bool TryBuildEnemyChargeKinematicContinuationPayload(
            WorldSnapshot snapshot,
            int entityId,
            int tickIndex,
            List<string> rejectedReasons,
            out MovementActionPlanPayload payload)
        {
            payload = null;
            if (!snapshot.TryGetEntity(entityId, out var entity) ||
                !IsEnemyChargeKinematicParticipant(snapshot, entity) ||
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                pose.IsSettledAtAnchor ||
                pose.Mode != MotionMode.Charge ||
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
                    $"MovementRejected|Stage=Plan|Source={entityId}|Reason=EnemyChargeKinematicContinuationCorrupt|Anchor={FormatCell(entity.position)}");
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
                pose.State.startedTick,
                mode: MotionMode.Charge);
            var enemyChargeWrites = CreateEnemyChargeKinematicCompletionWrites(snapshot, entityId, outcome);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                _idAllocator.AllocateIntentId(),
                entityId,
                priority: 100,
                outcome: outcome,
                facing: facing,
                writeFacing: true,
                enemyChargeWrites: enemyChargeWrites,
                executionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion,
                boundaryReason: "EnemyChargeKinematicContinuation");

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
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                !IsEnemyKinematicContinuationParticipant(snapshot, entity, pose) ||
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
            var continuationGlideKind = TryResolveEnemyGlideKinematicContinuationKind(
                    snapshot,
                    entity,
                    pose,
                    out var resolvedGlideKind)
                ? resolvedGlideKind
                : EnemyGlideKinematicKind.None;
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                _idAllocator.AllocateIntentId(),
                entityId,
                priority: 100,
                outcome: outcome,
                facing: facing,
                writeFacing: true,
                executionBoundaryKind: continuationGlideKind != EnemyGlideKinematicKind.None
                    ? MovementExecutionBoundaryKind.UnitSpecialLocomotion
                    : MovementExecutionBoundaryKind.UnitOrdinaryLocomotion,
                boundaryReason: ResolveEnemyKinematicContinuationBoundaryReason(continuationGlideKind));

            return true;
        }

        private bool TryResolveEnemyKinematicStartScope(
            WorldSnapshot snapshot,
            MoveIntent intent,
            out EntityState entity,
            out UnitKinematicPose pose,
            out Vector2Int delta,
            out SurfaceCell destination,
            out Direction facing,
            out EnemyGlideKinematicKind glideKinematicKind)
        {
            entity = default;
            pose = default;
            delta = Vector2Int.zero;
            destination = default;
            facing = Direction.None;
            glideKinematicKind = EnemyGlideKinematicKind.None;

            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                !snapshot.TryGetEntity(intent.SourceId, out entity) ||
                !IsEnemyKinematicStartParticipant(snapshot, entity, out glideKinematicKind) ||
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

        private static bool TryResolveEnemyChargeKinematicStartScope(
            WorldSnapshot snapshot,
            MoveIntent intent,
            out EntityState entity,
            out UnitKinematicPose pose,
            out Vector2Int delta,
            out Direction facing)
        {
            entity = default;
            pose = default;
            delta = Vector2Int.zero;
            facing = Direction.None;

            var chargeDeltaCandidate = snapshot.TryGetEnemyChargeState(intent?.SourceId ?? 0, out var chargeState)
                ? EnemyMovementStrategyShared.ResolveDelta(chargeState.lockedDirection)
                : null;
            if (intent == null ||
                intent.CommandKind != Movement.MovementCommandKind.Move ||
                !snapshot.TryGetEntity(intent.SourceId, out entity) ||
                !IsEnemyChargeKinematicParticipant(snapshot, entity) ||
                !snapshot.TryGetUnitKinematicPose(intent.SourceId, out pose) ||
                !pose.IsSettledAtAnchor ||
                !chargeDeltaCandidate.HasValue)
            {
                return false;
            }

            var chargeDelta = chargeDeltaCandidate.Value;
            delta = intent.Destination - entity.position.PlanarPosition;
            if (!TryResolveKinematicVelocity(delta, out _, out facing) ||
                chargeDelta != delta)
            {
                return false;
            }

            return true;
        }

        private static bool IsEnemyChargeKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) ||
                entity.aiMode != EnemyAiMode.Charge)
            {
                return false;
            }

            if (!snapshot.TryGetEnemyChargeState(entity.entityId, out var chargeState) ||
                chargeState.phase != EnemyChargePhase.Active ||
                chargeState.remainingActiveSteps <= 0)
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
                BlocksNonGlideEnemyKinematicLocomotion(glideState.Phase))
            {
                return false;
            }

            return !snapshot.TryGetPhasedState(entity.entityId, out var phasedState) ||
                   !phasedState.IsActive;
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
                BlocksOrdinaryEnemyKinematicLocomotionForJump(jumpState.phase))
            {
                return false;
            }

            if (snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                glideState.HasAuthoritativeRecord &&
                BlocksNonGlideEnemyKinematicLocomotion(glideState.Phase))
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

        private static bool BlocksNonGlideEnemyKinematicLocomotion(EnemyGlidePhase phase)
        {
            return phase == EnemyGlidePhase.Windup ||
                   phase == EnemyGlidePhase.Active ||
                   phase == EnemyGlidePhase.LandingPending ||
                   phase == EnemyGlidePhase.Recovery;
        }

        private static bool BlocksOrdinaryEnemyKinematicLocomotionForJump(EnemyJumpPhase phase)
        {
            return phase == EnemyJumpPhase.Windup ||
                   phase == EnemyJumpPhase.Airborne;
        }

        private bool IsEnemyKinematicStartParticipant(
            WorldSnapshot snapshot,
            in EntityState entity,
            out EnemyGlideKinematicKind glideKinematicKind)
        {
            glideKinematicKind = EnemyGlideKinematicKind.None;
            if (_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion)
            {
                if (IsEnemyActiveGlideKinematicParticipant(snapshot, entity))
                {
                    glideKinematicKind = EnemyGlideKinematicKind.Active;
                    return true;
                }

                if (IsEnemyLandingPendingEgressKinematicParticipant(snapshot, entity))
                {
                    glideKinematicKind = EnemyGlideKinematicKind.LandingPendingEgress;
                    return true;
                }
            }

            return _runtimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion &&
                   IsEnemyOrdinaryKinematicParticipant(snapshot, entity);
        }

        private bool IsEnemyKinematicContinuationParticipant(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitKinematicPose pose)
        {
            if (_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion &&
                TryResolveEnemyGlideKinematicContinuationKind(snapshot, entity, pose, out _))
            {
                return true;
            }

            if (_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion &&
                IsEnemyGlideOwnedKinematicPose(snapshot, entity, pose))
            {
                return false;
            }

            if (_runtimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion &&
                IsEnemyOrdinaryKinematicParticipant(snapshot, entity))
            {
                return true;
            }

            return false;
        }

        private static bool IsEnemyActiveGlideKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) ||
                entity.aiMode != EnemyAiMode.Chase)
            {
                return false;
            }

            if (!snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) ||
                glideState.Phase != EnemyGlidePhase.Active)
            {
                return false;
            }

            if (snapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState) &&
                jumpState.IsActive)
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

        private static bool IsEnemyLandingPendingEgressKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) ||
                entity.aiMode != EnemyAiMode.Chase)
            {
                return false;
            }

            if (!snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) ||
                glideState.Phase != EnemyGlidePhase.LandingPending)
            {
                return false;
            }

            if (snapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState) &&
                jumpState.IsActive)
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

        private static bool IsEnemyGlideKinematicContinuation(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitKinematicPose pose)
        {
            return TryResolveEnemyGlideKinematicContinuationKind(snapshot, entity, pose, out _);
        }

        private static bool IsEnemyGlideOwnedKinematicPose(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitKinematicPose pose)
        {
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) ||
                entity.aiMode != EnemyAiMode.Chase ||
                pose.Mode != MotionMode.Voluntary ||
                !snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) ||
                !glideState.HasAuthoritativeRecord ||
                glideState.ActiveUntilTickExclusive <= 0 ||
                glideState.DurationTicks <= 0)
            {
                return false;
            }

            var activeStartTick = glideState.ActiveUntilTickExclusive - glideState.DurationTicks;
            var startedDuringActive = pose.State.startedTick >= activeStartTick &&
                                      pose.State.startedTick < glideState.ActiveUntilTickExclusive;
            if (startedDuringActive)
            {
                return true;
            }

            return glideState.Phase == EnemyGlidePhase.LandingPending &&
                   pose.State.startedTick >= glideState.ActiveUntilTickExclusive;
        }

        private static bool TryResolveEnemyGlideKinematicContinuationKind(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitKinematicPose pose,
            out EnemyGlideKinematicKind glideKinematicKind)
        {
            glideKinematicKind = EnemyGlideKinematicKind.None;
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) ||
                entity.aiMode != EnemyAiMode.Chase ||
                pose.Mode != MotionMode.Voluntary)
            {
                return false;
            }

            if (snapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState) &&
                jumpState.IsActive)
            {
                return false;
            }

            if (snapshot.TryGetEnemyChargeState(entity.entityId, out var chargeState) &&
                chargeState.IsActive)
            {
                return false;
            }

            if (snapshot.TryGetPhasedState(entity.entityId, out var phasedState) &&
                phasedState.IsActive)
            {
                return false;
            }

            if (!snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) ||
                !glideState.HasAuthoritativeRecord ||
                glideState.ActiveUntilTickExclusive <= 0 ||
                glideState.DurationTicks <= 0)
            {
                return false;
            }

            var activeStartTick = glideState.ActiveUntilTickExclusive - glideState.DurationTicks;
            var startedDuringActive = pose.State.startedTick >= activeStartTick &&
                                      pose.State.startedTick < glideState.ActiveUntilTickExclusive;
            if (glideState.Phase == EnemyGlidePhase.Active && startedDuringActive)
            {
                glideKinematicKind = EnemyGlideKinematicKind.Active;
                return true;
            }

            if (glideState.Phase == EnemyGlidePhase.LandingPending)
            {
                glideKinematicKind = startedDuringActive
                    ? EnemyGlideKinematicKind.Active
                    : EnemyGlideKinematicKind.LandingPendingEgress;
                return true;
            }

            if (startedDuringActive &&
                (glideState.Phase == EnemyGlidePhase.Recovery ||
                 glideState.Phase == EnemyGlidePhase.Cooldown) &&
                !TryResolveSolidBoundKinematicTerminal(snapshot, pose, out _))
            {
                glideKinematicKind = EnemyGlideKinematicKind.Active;
                return true;
            }

            return false;
        }

        private static bool TryResolveSolidBoundKinematicTerminal(
            WorldSnapshot snapshot,
            UnitKinematicPose pose,
            out SurfaceCell terminalCell)
        {
            terminalCell = default;
            if (!TryResolveKinematicTerminalCell(pose, out terminalCell))
            {
                return false;
            }

            return snapshot.TryGetSolidSemanticAt(terminalCell, out _);
        }

        private static bool TryResolveKinematicTerminalCell(UnitKinematicPose pose, out SurfaceCell terminalCell)
        {
            terminalCell = default;
            if (!TryResolveStepDirection(pose.State, out var stepDirectionX, out var stepDirectionY, out _) ||
                pose.State.commitTick <= 0)
            {
                return false;
            }

            terminalCell = pose.State.elapsedTicks >= pose.State.commitTick
                ? pose.AnchorCell
                : pose.AnchorCell + new Vector2Int(stepDirectionX, stepDirectionY);
            return true;
        }

        private static string ResolveEnemyKinematicStartBoundaryReason(EnemyGlideKinematicKind glideKinematicKind)
        {
            return glideKinematicKind switch
            {
                EnemyGlideKinematicKind.Active => "EnemyGlideActiveKinematicStart",
                EnemyGlideKinematicKind.LandingPendingEgress => "EnemyGlideLandingPendingKinematicStart",
                _ => "KinematicUnitLocomotion",
            };
        }

        private static string ResolveEnemyKinematicContinuationBoundaryReason(EnemyGlideKinematicKind glideKinematicKind)
        {
            return glideKinematicKind switch
            {
                EnemyGlideKinematicKind.Active => "EnemyGlideActiveKinematicContinuation",
                EnemyGlideKinematicKind.LandingPendingEgress => "EnemyGlideLandingPendingKinematicContinuation",
                _ => "KinematicUnitLocomotion",
            };
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

        private static IReadOnlyList<EnemyChargeWritePayload> CreateEnemyChargeKinematicCompletionWrites(
            WorldSnapshot snapshot,
            int entityId,
            in KinematicMotionOutcome outcome)
        {
            if (!outcome.ResolvedState.IsSettledZero ||
                !snapshot.TryGetEnemyChargeState(entityId, out var chargeState) ||
                chargeState.phase != EnemyChargePhase.Active)
            {
                return Array.Empty<EnemyChargeWritePayload>();
            }

            return new[]
            {
                new EnemyChargeWritePayload(
                    entityId,
                    EnemyChargeQueries.ConsumeActiveStep(chargeState),
                    "ConsumeChargeKinematicSettledStep"),
            };
        }

        private static int ResolveChargeKinematicStepTicks(int activeStepCooldownTicks)
        {
            if (activeStepCooldownTicks <= 1)
            {
                return 2;
            }

            return (activeStepCooldownTicks % 2) == 0
                ? activeStepCooldownTicks
                : activeStepCooldownTicks + 1;
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
            PlayerTickCommand playerCommand,
            int tickIndex,
            bool enableStoppableLocomotion,
            List<string> rejectedReasons,
            out MovementActionPlanPayload payload)
        {
            payload = null;
            if (!snapshot.TryGetPlayerControlState(entityId, out var playerControlState) ||
                !snapshot.TryGetEntity(entityId, out var entity) ||
                entity.type != EntityType.Unit ||
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                pose.IsSettledAtAnchor ||
                (pose.Mode != MotionMode.Voluntary &&
                 (!enableStoppableLocomotion || pose.Mode != MotionMode.Held)) ||
                !TryResolveStepDirection(pose.State, out var stepDirectionX, out var stepDirectionY, out var facing))
            {
                return false;
            }

            if (pose.State.totalTicks < 2 ||
                (pose.State.totalTicks % 2) != 0 ||
                pose.State.elapsedTicks <= 0)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entityId}|Reason=KinematicContinuationCorrupt|Anchor={FormatCell(entity.position)}");
                return false;
            }

            if (playerCommand.PushPressed || playerCommand.FlipPressed)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entityId}|Reason=ActionAttemptConsumesKinematicContinuation|Push={playerCommand.PushPressed}|Flip={playerCommand.FlipPressed}|StepDirection={facing}");
                return false;
            }

            if (enableStoppableLocomotion)
            {
                var inputMatchesStep = playerCommand.HeldMoveDirection == facing;
                var hasQueuedTurn = PlayerControlQueries.HasQueuedKinematicTurn(playerControlState);
                if (pose.Mode == MotionMode.Voluntary && !inputMatchesStep && !hasQueuedTurn)
                {
                    if (playerCommand.HeldMoveDirection != Direction.None)
                    {
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Plan|Source={entityId}|Reason=HeldKinematicDirectionMismatch|HeldDirection={playerCommand.HeldMoveDirection}|StepDirection={facing}");
                    }

                    var heldState = UnitKinematicRuntimeState.CreateHeldFreeze(pose.State);
                    var heldOutcome = CreateKinematicStateOnlyOutcome(entityId, pose, heldState);
                    payload = CreateKinematicMovementPayload(
                        _idAllocator.AllocateGroupId(),
                        _idAllocator.AllocateIntentId(),
                        entityId,
                        priority: 100,
                        outcome: heldOutcome,
                        facing: facing,
                        writeFacing: false);
                    return true;
                }

                if (pose.Mode == MotionMode.Held)
                {
                    if (inputMatchesStep)
                    {
                        var resumedState = UnitKinematicRuntimeState.CreateVoluntaryResumeFromHeld(
                            pose.State,
                            CreateDebugKinematicVelocity(stepDirectionX, stepDirectionY, pose.State.totalTicks));
                        var resumedOutcome = CreateKinematicStateOnlyOutcome(entityId, pose, resumedState);
                        payload = CreateKinematicMovementPayload(
                            _idAllocator.AllocateGroupId(),
                            _idAllocator.AllocateIntentId(),
                            entityId,
                            priority: 100,
                            outcome: resumedOutcome,
                            facing: facing,
                            writeFacing: false);
                        return true;
                    }

                    if (playerCommand.HeldMoveDirection == Direction.None ||
                        playerCommand.PushPressed ||
                        playerCommand.FlipPressed)
                    {
                        if (playerCommand.HeldMoveDirection != Direction.None)
                        {
                            rejectedReasons.Add(
                                $"MovementRejected|Stage=Plan|Source={entityId}|Reason=HeldKinematicDirectionMismatch|HeldDirection={playerCommand.HeldMoveDirection}|StepDirection={facing}");
                        }

                        return false;
                    }

                    if (!TryResolveDirectionDelta(playerCommand.HeldMoveDirection, out var heldDirectionDelta))
                    {
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Plan|Source={entityId}|Reason=HeldKinematicDirectionInvalid|HeldDirection={playerCommand.HeldMoveDirection}|StepDirection={facing}");
                        return false;
                    }

                    if (heldDirectionDelta.x == -stepDirectionX &&
                        heldDirectionDelta.y == -stepDirectionY)
                    {
                        if (!KinematicProgressResolver.TryResolveReverseFromHeld(
                                pose.AnchorCell,
                                pose.State,
                                heldDirectionDelta.x,
                                heldDirectionDelta.y,
                                out var mirroredAnchor,
                                out var mirroredState,
                                out var poseDeltaRawUnits) ||
                            poseDeltaRawUnits > 1)
                        {
                            rejectedReasons.Add(
                                $"MovementRejected|Stage=Plan|Source={entityId}|Reason=HeldKinematicReverseCorrupt|HeldDirection={playerCommand.HeldMoveDirection}|StepDirection={facing}");
                            return false;
                        }

                        var reverseOutcome = CreateKinematicReinterpretOutcome(
                            entityId,
                            pose,
                            mirroredAnchor,
                            mirroredState,
                            poseDeltaRawUnits <= 1);
                        payload = CreateKinematicMovementPayload(
                            _idAllocator.AllocateGroupId(),
                            _idAllocator.AllocateIntentId(),
                            entityId,
                            priority: 100,
                            outcome: reverseOutcome,
                            facing: playerCommand.HeldMoveDirection,
                            writeFacing: false);
                        return true;
                    }

                    if ((Math.Abs(heldDirectionDelta.x) + Math.Abs(heldDirectionDelta.y)) == 1 &&
                        heldDirectionDelta.x != stepDirectionX &&
                        heldDirectionDelta.y != stepDirectionY)
                    {
                        var resumedState = UnitKinematicRuntimeState.CreateVoluntaryResumeFromHeld(
                            pose.State,
                            CreateDebugKinematicVelocity(stepDirectionX, stepDirectionY, pose.State.totalTicks));
                        var resumedOutcome = CreateKinematicStateOnlyOutcome(entityId, pose, resumedState);
                        payload = CreateKinematicMovementPayload(
                            _idAllocator.AllocateGroupId(),
                            _idAllocator.AllocateIntentId(),
                            entityId,
                            priority: 100,
                            outcome: resumedOutcome,
                            facing: facing,
                            writeFacing: false,
                            playerControlWrites: new[]
                            {
                                new PlayerControlWritePayload(
                                    entityId,
                                    PlayerControlQueries.QueueKinematicTurn(playerControlState, playerCommand.HeldMoveDirection)),
                            });
                        return true;
                    }

                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Plan|Source={entityId}|Reason=HeldKinematicDirectionMismatch|HeldDirection={playerCommand.HeldMoveDirection}|StepDirection={facing}");
                    return false;
                }
            }

            var nextElapsedTicks = pose.State.elapsedTicks + 1;
            if (nextElapsedTicks <= 0)
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

        private bool TryBuildPlayerQueuedKinematicTurnStartPayload(
            WorldSnapshot snapshot,
            int entityId,
            int tickIndex,
            List<string> rejectedReasons,
            out MovementActionPlanPayload payload)
        {
            payload = null;
            if (!snapshot.TryGetPlayerControlState(entityId, out var playerControlState) ||
                !PlayerControlQueries.HasQueuedKinematicTurn(playerControlState) ||
                !snapshot.TryGetEntity(entityId, out var entity) ||
                entity.type != EntityType.Unit ||
                !snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                !pose.IsSettledAtAnchor)
            {
                return false;
            }

            var queuedDirection = playerControlState.queuedKinematicTurnDirection;
            var clearedState = PlayerControlQueries.ClearQueuedKinematicTurn(playerControlState);
            if (playerControlState.activeAction.IsActive ||
                PlayerControlQueries.IsMoveOnCooldown(playerControlState, tickIndex) ||
                !TryResolveDirectionDelta(queuedDirection, out var delta))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entityId}|Reason=QueuedKinematicTurnRejected|QueuedDirection={queuedDirection}");
                payload = CreatePlayerControlStateOnlyMovementPayload(
                    _idAllocator.AllocateGroupId(),
                    _idAllocator.AllocateIntentId(),
                    entityId,
                    priority: 100,
                    entity.position,
                    clearedState);
                return true;
            }

            var queuedIntent = new MoveIntent(
                entityId,
                priority: 100,
                entity.position.PlanarPosition + delta,
                localSequence: 0,
                moveCooldownTicks: 0,
                ordinaryKinematicMoveTicks: 0);
            queuedIntent.AssignIntentId(_idAllocator.AllocateIntentId());
            if (TryBuildPlayerKinematicStartPayload(
                    snapshot,
                    queuedIntent,
                    tickIndex,
                    rejectedReasons,
                    out var handledByKinematic,
                    out var startPayload) &&
                handledByKinematic &&
                startPayload != null)
            {
                payload = AddPlayerControlWrite(
                    startPayload,
                    new PlayerControlWritePayload(entityId, clearedState));
                return true;
            }

            rejectedReasons.Add(
                $"MovementRejected|Stage=Plan|Source={entityId}|Reason=QueuedKinematicTurnRejected|QueuedDirection={queuedDirection}");
            payload = CreatePlayerControlStateOnlyMovementPayload(
                _idAllocator.AllocateGroupId(),
                queuedIntent.IntentId,
                entityId,
                priority: 100,
                entity.position,
                clearedState);
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

        private static bool TryResolveDirectionDelta(Direction direction, out Vector2Int delta)
        {
            switch (direction)
            {
                case Direction.Right:
                    delta = Vector2Int.right;
                    return true;
                case Direction.Left:
                    delta = Vector2Int.left;
                    return true;
                case Direction.Up:
                    delta = Vector2Int.up;
                    return true;
                case Direction.Down:
                    delta = Vector2Int.down;
                    return true;
                default:
                    delta = default;
                    return false;
            }
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
            int startedTick,
            MotionMode mode = MotionMode.Voluntary)
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
                currentAnchor,
                mode);
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
            SurfaceCell? currentAnchorOverride = null,
            MotionMode mode = MotionMode.Voluntary)
        {
            var currentAnchor = currentAnchorOverride ?? sourceAnchorCell;
            var resolution = KinematicProgressResolver.ResolvePose(
                currentAnchor,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks);
            var resolvedState = CreateStepKinematicState(
                sourceState,
                resolution,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks,
                startedTick,
                mode);

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

        private static KinematicMotionOutcome CreateKinematicStateOnlyOutcome(
            int entityId,
            UnitKinematicPose pose,
            UnitKinematicRuntimeState resolvedState)
        {
            return new KinematicMotionOutcome(
                entityId,
                pose.AnchorCell,
                pose.LocalOffset,
                pose.AnchorCell,
                pose.LocalOffset,
                resolvedState.velocity,
                resolvedState,
                anchorChanged: false,
                blocked: false,
                rejectedBy: KinematicSweepRejectionReason.None);
        }

        private static KinematicMotionOutcome CreateKinematicReinterpretOutcome(
            int entityId,
            UnitKinematicPose pose,
            SurfaceCell resolvedAnchorCell,
            UnitKinematicRuntimeState resolvedState,
            bool posePreserved)
        {
            if (!posePreserved)
            {
                throw new InvalidOperationException("Kinematic reinterpretation must preserve the current world pose.");
            }

            return new KinematicMotionOutcome(
                entityId,
                pose.AnchorCell,
                pose.LocalOffset,
                resolvedAnchorCell,
                resolvedState.localOffset,
                resolvedState.velocity,
                resolvedState,
                anchorChanged: resolvedAnchorCell != pose.AnchorCell,
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

        private static UnitKinematicRuntimeState CreateStepKinematicState(
            UnitKinematicRuntimeState sourceState,
            KinematicProgressResolution resolution,
            int stepDirectionX,
            int stepDirectionY,
            int elapsedTicks,
            int totalTicks,
            int startedTick,
            MotionMode mode)
        {
            if (resolution.IsSettled)
            {
                return UnitKinematicRuntimeState.SettledZero;
            }

            return new UnitKinematicRuntimeState
            {
                localOffset = resolution.LocalOffset,
                velocity = CreateDebugKinematicVelocity(stepDirectionX, stepDirectionY, totalTicks),
                mode = mode,
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
            IReadOnlyList<EnemyChargeWritePayload> enemyChargeWrites = null,
            IReadOnlyList<ExecutionLockWritePayload> executionLockWrites = null,
            IReadOnlyList<PlayerControlWritePayload> playerControlWrites = null,
            MovementExecutionBoundaryKind executionBoundaryKind = MovementExecutionBoundaryKind.UnitOrdinaryLocomotion,
            string boundaryReason = "KinematicUnitLocomotion")
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
                enemyChargeWrites ?? Array.Empty<EnemyChargeWritePayload>(),
                playerControlWrites ?? Array.Empty<PlayerControlWritePayload>(),
                Array.Empty<DestroyWritePayload>(),
                hasImpactReservationPayload: false,
                impactReservationPayload: default,
                hasDeferredImpactPayload: false,
                deferredImpactPayload: default,
                kinematicMotionOutcomes: new[] { outcome },
                executionBoundaryKind: executionBoundaryKind,
                boundaryReason: boundaryReason);
        }

        private static MovementActionPlanPayload CreatePlayerControlStateOnlyMovementPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            SurfaceCell sourceCell,
            PlayerControlState playerControlState)
        {
            return new MovementActionPlanPayload(
                actionPlanId,
                intentId,
                sourceActorEntityId,
                priority,
                ResolvedActionSemanticKind.Stop,
                MovementCandidateKind.Stop,
                sourceCell,
                sourceCell,
                hasMovementEdge: false,
                movementEdge: default,
                new[] { sourceActorEntityId },
                MovementReservationKind.None,
                MovementBlockingType.NonBlocking,
                Array.Empty<StateChangeWritePayload>(),
                Array.Empty<MoveWritePayload>(),
                Array.Empty<BoardPresenceWritePayload>(),
                Array.Empty<FacingWritePayload>(),
                Array.Empty<BoxKineticOwnerWritePayload>(),
                Array.Empty<TopologyWritePayload>(),
                Array.Empty<ExecutionLockWritePayload>(),
                Array.Empty<EnemyLocomotionWritePayload>(),
                Array.Empty<EnemyPatrolWritePayload>(),
                Array.Empty<EnemyChargeWritePayload>(),
                new[] { new PlayerControlWritePayload(sourceActorEntityId, playerControlState) },
                Array.Empty<DestroyWritePayload>(),
                hasImpactReservationPayload: false,
                impactReservationPayload: default,
                hasDeferredImpactPayload: false,
                deferredImpactPayload: default);
        }

        private static MovementActionPlanPayload AddPlayerControlWrite(
            MovementActionPlanPayload payload,
            PlayerControlWritePayload playerControlWrite)
        {
            var playerControlWrites = new PlayerControlWritePayload[payload.PlayerControlWrites.Count + 1];
            for (var i = 0; i < payload.PlayerControlWrites.Count; i++)
            {
                playerControlWrites[i] = payload.PlayerControlWrites[i];
            }

            playerControlWrites[playerControlWrites.Length - 1] = playerControlWrite;
            return new MovementActionPlanPayload(
                payload.ActionPlanId,
                payload.IntentId,
                payload.SourceActorEntityId,
                payload.Priority,
                payload.SemanticKind,
                payload.MovementCandidateKind,
                payload.SourceCell,
                payload.DestinationCell,
                payload.HasMovementEdge,
                payload.MovementEdge,
                payload.AffectedEntityIds,
                payload.ReservationKind,
                payload.BlockingType,
                payload.StateChangeWrites,
                payload.MoveWrites,
                payload.BoardPresenceWrites,
                payload.FacingWrites,
                payload.BoxKineticOwnerWrites,
                payload.TopologyWrites,
                payload.ExecutionLockWrites,
                payload.EnemyLocomotionWrites,
                payload.EnemyPatrolWrites,
                payload.EnemyChargeWrites,
                playerControlWrites,
                payload.DestroyWrites,
                payload.HasImpactReservationPayload,
                payload.ImpactReservationPayload,
                payload.HasDeferredImpactPayload,
                payload.DeferredImpactPayload,
                payload.KinematicMotionOutcomes,
                payload.ExecutionBoundaryKind,
                payload.BoundaryReason);
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

                var enemyChargeWrites = new List<EnemyChargeWritePayload>();
                if (sourceEntity.type == EntityType.Unit &&
                    sourceEntity.aiMode == EnemyAiMode.Charge)
                {
                    var intent = FindMovementIntent(sortedIntents, group.IntentId);
                    if (intent != null &&
                        intent.CommandKind == Movement.MovementCommandKind.Move &&
                        snapshot.TryGetEnemyChargeState(group.SourceId, out var enemyChargeState) &&
                        enemyChargeState.phase == EnemyChargePhase.Active)
                    {
                        enemyChargeWrites.Add(
                            new EnemyChargeWritePayload(
                                group.SourceId,
                                EnemyChargeQueries.ConsumeActiveStep(enemyChargeState),
                                "ConsumeLegacyActiveStep"));
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
                var executionBoundaryKind = ResolveMovementExecutionBoundaryKind(snapshot, sortedIntents, group);

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
                    enemyChargeWrites,
                    playerControlWrites,
                    destroyWrites,
                    hasImpactReservationPayload,
                    impactReservationPayload,
                    hasDeferredImpactPayload,
                    deferredImpactPayload,
                    executionBoundaryKind: executionBoundaryKind,
                    boundaryReason: ResolveMovementExecutionBoundaryReason(executionBoundaryKind));
            }

            return payloads;
        }

        private static MovementExecutionBoundaryKind ResolveMovementExecutionBoundaryKind(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ActionGroup group)
        {
            if (group.TopologyChanges.Count > 0)
            {
                return MovementExecutionBoundaryKind.TopologyMaterialization;
            }

            if (group.GroupKind == ActionGroupKind.Push ||
                group.GroupKind == ActionGroupKind.Flip ||
                group.GroupKind == ActionGroupKind.Item ||
                group.GroupKind == ActionGroupKind.BoxImpact ||
                group.GroupKind == ActionGroupKind.Stop ||
                group.HasResolvedImpact ||
                group.HasDeferredImpact)
            {
                return MovementExecutionBoundaryKind.BoxActionMovement;
            }

            if (!snapshot.TryGetEntity(group.SourceId, out var sourceEntity))
            {
                return MovementExecutionBoundaryKind.Unknown;
            }

            if (sourceEntity.type != EntityType.Unit)
            {
                return MovementExecutionBoundaryKind.Unknown;
            }

            var intent = FindMovementIntent(sortedIntents, group.IntentId);
            if (intent != null &&
                intent.CommandKind == Movement.MovementCommandKind.Move &&
                group.GroupKind == ActionGroupKind.Move)
            {
                return MovementExecutionBoundaryKind.LegacyFallback;
            }

            return MovementExecutionBoundaryKind.Unknown;
        }

        private static string ResolveMovementExecutionBoundaryReason(MovementExecutionBoundaryKind kind)
        {
            return kind switch
            {
                MovementExecutionBoundaryKind.TopologyMaterialization => "TopologyMaterialization",
                MovementExecutionBoundaryKind.BoxActionMovement => "BoxActionMovement",
                MovementExecutionBoundaryKind.LegacyFallback => "LegacyFallback",
                _ => string.Empty,
            };
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
                var isLockedTargetLanding = landingCell == jumpState.lockedTargetCell;
                if (TryResolveContestedJumpLandingTarget(occupants, jumpEntry.EntityId, out var impactTargetId) &&
                    !isLockedTargetLanding)
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
                        isLockedTargetLanding ? JumpLandingKind.ExactStack : JumpLandingKind.Contested));
                jumpLandingSpaceContests.Add(
                    new Contest(
                        openLandingContestId,
                        ContestKind.Space,
                        openLandingActionPlanId,
                        jumpEntry.EntityId,
                        priority: 0,
                        affectedEntityId: isLockedTargetLanding ? jumpEntry.EntityId : 0,
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
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None,
            MovementExecutionBoundaryKind? executionBoundaryKindOverride = null,
            string boundaryReasonOverride = null)
        {
            var resolvedSemanticKind = semanticKindOverride ?? payload.SemanticKind;
            var executionBoundaryKind = executionBoundaryKindOverride ?? payload.ExecutionBoundaryKind;
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
                damageSourceType: DamageSourceType.None,
                movementExecutionBoundaryKind: executionBoundaryKind,
                boundaryReason: boundaryReasonOverride ?? payload.BoundaryReason);
        }

        private static FinalizationOperationMetadata CreateAttackMetadata(
            AttackActionPlanPayload payload,
            ResolutionRecord resolutionRecord,
            int localActionIndex,
            AttackSourceKind attackSourceKind = AttackSourceKind.Combat,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None,
            MovementExecutionBoundaryKind movementExecutionBoundaryKind = MovementExecutionBoundaryKind.Unknown,
            string boundaryReason = null)
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
                damageSourceType: ResolveDamageSourceType(attackSourceKind),
                movementExecutionBoundaryKind: movementExecutionBoundaryKind,
                boundaryReason: boundaryReason);
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
                presentationTargetCell: payload.DestinationCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion,
                boundaryReason: "EnemyJumpLanding");
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
                presentationTargetCell: payload.DestinationCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation,
                boundaryReason: "PhaseRelocation");
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

        internal static List<TileEffectBoxContact> BuildTileEffectBoxContacts(
            WorldSnapshot snapshot,
            params FinalizationBatch[] batches)
        {
            var contacts = new List<TileEffectBoxContact>();
            if (snapshot == null || batches == null)
            {
                return contacts;
            }

            for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
            {
                var batch = batches[batchIndex];
                if (batch == null)
                {
                    continue;
                }

                var operations = batch.Operations;
                for (var operationIndex = 0; operationIndex < operations.Count; operationIndex++)
                {
                    var operation = operations[operationIndex];
                    if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                        !TryResolveTileEffectBoxContactKind(operation.Metadata, out var kind) ||
                        !snapshot.TryGetEntity(operation.EntityId, out var entity) ||
                        entity.type != EntityType.Box ||
                        entity.position != operation.Destination ||
                        entity.boardPresence != EntityBoardPresence.Occupying ||
                        entity.hp <= 0 ||
                        entity.markedForDeath)
                    {
                        continue;
                    }

                    contacts.Add(new TileEffectBoxContact(operation.EntityId, operation.Destination, kind));
                }
            }

            contacts.Sort(CompareTileEffectBoxContacts);
            return contacts;
        }

        private static bool TryResolveTileEffectBoxContactKind(
            FinalizationOperationMetadata metadata,
            out TileEffectBoxContactKind kind)
        {
            if (metadata.LocalActionIndex == 1 &&
                (metadata.MovementSemanticKind == MovementSemanticKind.Push ||
                 metadata.MovementSemanticKind == MovementSemanticKind.Slide ||
                 metadata.MovementSemanticKind == MovementSemanticKind.Flip))
            {
                kind = TileEffectBoxContactKind.ImpactFollowThrough;
                return true;
            }

            switch (metadata.MovementSemanticKind)
            {
                case MovementSemanticKind.Slide:
                    kind = TileEffectBoxContactKind.SlideEnter;
                    return true;
                case MovementSemanticKind.Push:
                    kind = TileEffectBoxContactKind.PushEnter;
                    return true;
                case MovementSemanticKind.Flip:
                    kind = TileEffectBoxContactKind.FlipLanding;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static int CompareTileEffectBoxContacts(TileEffectBoxContact left, TileEffectBoxContact right)
        {
            var cellCompare = CompareSurfaceCells(left.Cell, right.Cell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var boxCompare = left.BoxEntityId.CompareTo(right.BoxEntityId);
            if (boxCompare != 0)
            {
                return boxCompare;
            }

            return left.Kind.CompareTo(right.Kind);
        }

        private static int CompareSurfaceCells(SurfaceCell left, SurfaceCell right)
        {
            var faceCompare = left.face.CompareTo(right.face);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            var xCompare = left.x.CompareTo(right.x);
            return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
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
                    var kinematicMetadata = CreateMovementMetadata(
                        payload,
                        baseResolution,
                        kinematicIndex,
                        executionBoundaryKindOverride: MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                        boundaryReasonOverride: ResolveKinematicAnchorCommitBoundaryReason(payload));
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

                for (var chargeIndex = 0; chargeIndex < payload.EnemyChargeWrites.Count; chargeIndex++)
                {
                    var enemyChargeWrite = payload.EnemyChargeWrites[chargeIndex];
                    batch.SetEnemyChargeState(
                        enemyChargeWrite.EntityId,
                        enemyChargeWrite.EnemyChargeState,
                        CreateMovementMetadata(payload, baseResolution, chargeIndex));
                    commitEvents.Add(
                        $"EnemyChargeStateUpdated|G={actionPlanId}|I={payload.IntentId}|E={enemyChargeWrite.EntityId}|Label={enemyChargeWrite.Label}|Phase={enemyChargeWrite.EnemyChargeState.phase}|ActiveSteps={enemyChargeWrite.EnemyChargeState.remainingActiveSteps}");
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

        private static string ResolveKinematicAnchorCommitBoundaryReason(MovementActionPlanPayload payload)
        {
            if (payload.BoundaryReason == "EnemyGlideActiveKinematicStart" ||
                payload.BoundaryReason == "EnemyGlideActiveKinematicContinuation")
            {
                return "GlideActiveKinematicAnchorCommit";
            }

            if (payload.BoundaryReason == "EnemyGlideLandingPendingKinematicStart" ||
                payload.BoundaryReason == "EnemyGlideLandingPendingKinematicContinuation")
            {
                return "GlideLandingPendingKinematicAnchorCommit";
            }

            return payload.ExecutionBoundaryKind == MovementExecutionBoundaryKind.UnitSpecialLocomotion
                ? "SpecialKinematicAnchorCommit"
                : "OrdinaryKinematicAnchorCommit";
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
                    batch.SpawnEntity(
                        finalizedSpawn.Entity,
                        CreateAttackMetadata(
                            payload,
                            planResolution,
                            spawnIndex,
                            movementExecutionBoundaryKind: MovementExecutionBoundaryKind.SpawnRespawnPlacement,
                            boundaryReason: "AttackSpawnPlacement"));
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

        private static List<MotionInterruptRecord> MaterializeUnitKinematicMotionInterrupts(
            WorldSnapshot attackSnapshot,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            FinalizationBatch attackStageBatch,
            List<string> commitEvents,
            int tickIndex,
            bool interruptPlayerKinematics,
            bool interruptEnemyGlideKinematics)
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

                TryMaterializeUnitKinematicMotionInterrupt(
                    attackSnapshot,
                    attackStageBatch,
                    commitEvents,
                    interruptRecords,
                    interruptedEntityIds,
                    damageResolution.TargetId,
                    damageResolution.SourceId,
                    damageResolution.ActionPlanId,
                    damageResolution.LocalActionIndex,
                    damageResolution.SourceKind,
                    tickIndex,
                    interruptPlayerKinematics,
                    interruptEnemyGlideKinematics,
                    damageResolution.Amount);
            }

            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                var destroyResolution = destroyResolutions[i];
                if (!destroyResolution.Accepted)
                {
                    continue;
                }

                TryMaterializeUnitKinematicMotionInterrupt(
                    attackSnapshot,
                    attackStageBatch,
                    commitEvents,
                    interruptRecords,
                    interruptedEntityIds,
                    destroyResolution.TargetId,
                    destroyResolution.SourceId,
                    destroyResolution.ActionPlanId,
                    destroyResolution.LocalActionIndex,
                    AttackSourceKind.Combat,
                    tickIndex,
                    interruptPlayerKinematics,
                    interruptEnemyGlideKinematics,
                    damageAmount: 0);
            }

            return interruptRecords;
        }

        private static bool TryMaterializeUnitKinematicMotionInterrupt(
            WorldSnapshot attackSnapshot,
            FinalizationBatch attackStageBatch,
            List<string> commitEvents,
            List<MotionInterruptRecord> interruptRecords,
            HashSet<int> interruptedEntityIds,
            int targetEntityId,
            int sourceEntityId,
            int actionPlanId,
            int localActionIndex,
            AttackSourceKind sourceKind,
            int tickIndex,
            bool interruptPlayerKinematics,
            bool interruptEnemyGlideKinematics,
            int damageAmount)
        {
            if (interruptedEntityIds.Contains(targetEntityId))
            {
                return false;
            }

            var hasPlayerControl = attackSnapshot.TryGetPlayerControlState(targetEntityId, out var playerControlState);
            if (attackSnapshot.TryGetUnitKinematicPose(targetEntityId, out var pose) &&
                pose.HasAuthoritativeState &&
                !pose.IsSettledAtAnchor &&
                (pose.Mode == MotionMode.Voluntary || pose.Mode == MotionMode.Held))
            {
                var playerInterrupt = interruptPlayerKinematics && hasPlayerControl;
                var enemyGlideInterrupt = interruptEnemyGlideKinematics &&
                                          pose.Mode == MotionMode.Voluntary &&
                                          attackSnapshot.TryGetEntity(targetEntityId, out var targetEntity) &&
                                          IsEnemyGlideKinematicContinuation(attackSnapshot, targetEntity, pose);
                if (!playerInterrupt && !enemyGlideInterrupt)
                {
                    return false;
                }

                var interruptedState = UnitKinematicRuntimeState.CreateInterruptedFreeze(pose.State);
                var metadata = new FinalizationOperationMetadata(
                    TickPhase.Resolve,
                    ResolvedActionSemanticKind.Attack,
                    sourceEntityId,
                    actionPlanId,
                    localActionIndex: localActionIndex,
                    attackSourceKind: sourceKind,
                    damageSourceType: ResolveDamageSourceType(sourceKind));
                interruptedEntityIds.Add(targetEntityId);
                attackStageBatch.SetUnitKinematicState(
                    targetEntityId,
                    interruptedState,
                    metadata);
                if (enemyGlideInterrupt &&
                    IsNonLethalDamage(attackSnapshot, targetEntityId, damageAmount) &&
                    attackSnapshot.TryGetEnemyGlideState(targetEntityId, out var glideState) &&
                    glideState.Phase == EnemyGlidePhase.Active)
                {
                    attackStageBatch.SetEnemyGlideState(
                        targetEntityId,
                        EnemyGlideQueries.BeginRecovery(glideState, tickIndex),
                        metadata);
                    commitEvents.Add(
                        $"EnemyGlideStateUpdated|E={targetEntityId}|Label=InterruptedToRecovery|Phase={EnemyGlidePhase.Recovery}|Seq={glideState.Sequence}");
                }

                if (hasPlayerControl &&
                    (PlayerControlQueries.HasQueuedKinematicTurn(playerControlState) ||
                     PlayerControlQueries.HasQueuedFree2DAction(playerControlState)))
                {
                    var clearedPlayerControlState = playerControlState;
                    if (PlayerControlQueries.HasQueuedKinematicTurn(clearedPlayerControlState))
                    {
                        clearedPlayerControlState = PlayerControlQueries.ClearQueuedKinematicTurn(clearedPlayerControlState);
                    }

                    if (PlayerControlQueries.HasQueuedFree2DAction(clearedPlayerControlState))
                    {
                        clearedPlayerControlState = PlayerControlQueries.ClearQueuedFree2DAction(clearedPlayerControlState);
                    }

                    attackStageBatch.SetPlayerControlState(
                        targetEntityId,
                        clearedPlayerControlState,
                        metadata);
                }

                interruptRecords.Add(
                    new MotionInterruptRecord(
                        targetEntityId,
                        MotionInterruptPolicy.FreezeCurrentPose,
                        sourceEntityId));
                commitEvents.Add(
                    $"KinematicMotionInterrupted|E={targetEntityId}|Source={sourceEntityId}|SourceKind={sourceKind}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}|Mode={interruptedState.mode}");
                return true;
            }

            if (!hasPlayerControl ||
                !attackSnapshot.TryGetUnitContinuousLocomotionPose(targetEntityId, out var continuousPose) ||
                !continuousPose.HasAuthoritativeState ||
                continuousPose.IsSettledAtAnchor)
            {
                return false;
            }

            var continuousInterruptedState = UnitContinuousLocomotionState.CreateIdleFreeze(continuousPose.State);
            interruptedEntityIds.Add(targetEntityId);
            attackStageBatch.SetUnitContinuousLocomotionState(
                targetEntityId,
                continuousInterruptedState,
                new FinalizationOperationMetadata(
                    TickPhase.Resolve,
                    ResolvedActionSemanticKind.Attack,
                    sourceEntityId,
                    actionPlanId,
                    localActionIndex: localActionIndex,
                    attackSourceKind: sourceKind,
                    damageSourceType: ResolveDamageSourceType(sourceKind)));
            if (PlayerControlQueries.HasQueuedFree2DAction(playerControlState))
            {
                attackStageBatch.SetPlayerControlState(
                    targetEntityId,
                    PlayerControlQueries.ClearQueuedFree2DAction(playerControlState),
                    new FinalizationOperationMetadata(
                        TickPhase.Resolve,
                        ResolvedActionSemanticKind.Attack,
                        sourceEntityId,
                        actionPlanId,
                        localActionIndex: localActionIndex,
                        attackSourceKind: sourceKind,
                        damageSourceType: ResolveDamageSourceType(sourceKind)));
                commitEvents.Add(
                    $"Free2DActionAssistCleared|Stage=Resolve|Source={targetEntityId}|Reason=Interrupted");
            }
            interruptRecords.Add(
                new MotionInterruptRecord(
                    targetEntityId,
                    MotionInterruptPolicy.FreezeCurrentPose,
                    sourceEntityId));
            commitEvents.Add(
                $"ContinuousLocomotionInterrupted|E={targetEntityId}|Source={sourceEntityId}|SourceKind={sourceKind}|Anchor={FormatCell(continuousPose.AnchorCell)}|Offset={continuousPose.LocalOffset}|Mode={continuousInterruptedState.mode}");
            return true;
        }

        private static bool IsNonLethalDamage(WorldSnapshot snapshot, int targetEntityId, int damageAmount)
        {
            return damageAmount > 0 &&
                   snapshot.TryGetEntity(targetEntityId, out var target) &&
                   target.hp > damageAmount;
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

}
