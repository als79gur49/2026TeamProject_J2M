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
using Unity.Profiling;

namespace Game.Feature.Gameplay.Loop
{
    public sealed partial class TickPipeline
    {
        private static readonly ProfilerMarker RunCleanupPhaseMarker = new("Gameplay.RunCleanupPhase");
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
        private readonly GravityFieldLockedBoxOneShotState _gravityFieldLockedBoxOneShotState = new();
        private readonly List<EntityState> _playerRespawnTemplates;
        private readonly StageObjectiveTracker _objectiveTracker;
        private readonly int _moveOccupancyTicks;
        private readonly int _playerDamageCooldownTicks;
        private readonly int _playerRespawnDelayTicks;
        private readonly int _gravityFieldChargeTicks;
        private readonly int _gravityFieldActiveTicks;
        private readonly UnitKinematicLocomotionTimingSnapshot _unitKinematicLocomotionTiming;
        private readonly PlayerContinuousLocomotionSnapshot _playerContinuousLocomotion;
        private readonly bool _allowPlayerRespawn;
        private readonly GameplayRuntimeFeatureFlags _runtimeFeatureFlags;
        private readonly int _slidingStateTimerTicks;
        private readonly IReadOnlyList<TileFeatureRuntimeDefinition> _tileFeatureDefinitions;
        private readonly TileFeatureTraversalEvidence _tileFeatureTraversalEvidence;
        private readonly TileFeatureSettlementEvidence _tileFeatureSettlementEvidence;
        private readonly IReadOnlyList<MoonBlockRespawnDefinition> _moonBlockRespawnDefinitions;
        private readonly ITileEffectResolver _tileEffectResolver;
        private readonly WorldState _worldState;

        private enum EnemyGlideKinematicKind
        {
            None = 0,
            Active = 1,
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
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming = default,
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
                unitKinematicLocomotionTiming,
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
            UnitKinematicLocomotionTimingSnapshot unitKinematicLocomotionTiming,
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
            _playerDamageCooldownTicks = Math.Max(0, playerControlTiming.DamageCooldownTicks);
            _playerRespawnDelayTicks = playerRespawnDelayTicks;
            _gravityFieldChargeTicks = GameplayTimingProfile.SecondsToCeilTicks(
                GravityFieldRuntimePolicy.ChargeDurationSeconds,
                resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _gravityFieldActiveTicks = GameplayTimingProfile.SecondsToCeilTicks(
                GravityFieldRuntimePolicy.ActiveDurationSeconds,
                resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _unitKinematicLocomotionTiming = unitKinematicLocomotionTiming.IsConfigured
                ? unitKinematicLocomotionTiming
                : UnitKinematicLocomotionTimingSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _playerContinuousLocomotion = playerContinuousLocomotion.IsConfigured
                ? playerContinuousLocomotion
                : PlayerContinuousLocomotionSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(resolvedGeneralTimingProfile.SimulationTicksPerSecond);
            _tileFeatureDefinitions = tileFeatureDefinitions == null
                ? Array.Empty<TileFeatureRuntimeDefinition>()
                : new List<TileFeatureRuntimeDefinition>(tileFeatureDefinitions).AsReadOnly();
            _tileFeatureTraversalEvidence = new TileFeatureTraversalEvidence(_tileFeatureDefinitions);
            _tileFeatureSettlementEvidence = new TileFeatureSettlementEvidence(_tileFeatureDefinitions);
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
#if GAMEPLAY_DEBUG_OUTPUT_FORCE_OFF
            return false;
#elif GAMEPLAY_ENABLE_TICK_TRACE
            return true;
#elif UNITY_EDITOR
            return true;
#elif DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        private static bool ShouldEmitDeterminismHash()
        {
#if GAMEPLAY_DEBUG_OUTPUT_FORCE_OFF
            return false;
#elif UNITY_EDITOR
            return true;
#elif DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        public TickResult RunTick(in TickInput input)
        {
            return RunTick(input, DemoGameplayOverrideSnapshot.None);
        }

        public TickResult RunTick(
            in TickInput input,
            DemoGameplayOverrideSnapshot demoGameplayOverrideSnapshot)
        {
            _idAllocator.ResetForTick(input.TickIndex);

            var completedPhases = new List<TickPhase>(5);
            var phaseTrace = new List<string>(10);
            var drainedDelayedAttackEffects = _delayedAttackEffectQueue.Drain(input.TickIndex);

            var initialSnapshot = SnapshotBuilder.Create(_worldState);
            var entityLogicsForTick = _entityLogicProvider.Build(initialSnapshot, _staticEntityLogics);
            BindTileFeatureDefinitionContext(entityLogicsForTick);
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
                demoGameplayOverrideSnapshot,
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
                resolvePhaseResult.FinalizationBatch,
                planPhaseResult.PlayerActionAttemptResolutions,
                resolvePhaseResult.EnemyGravityFieldAuraLockedTargetFacts);
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

            return TickResult.CreateFromOwnedData(
                input.TickIndex,
                completedPhases,
                phaseTrace,
                movementPhaseResult,
                attackPhaseResult,
                tickResultData,
                finalAuthoritativeSnapshot.Topology,
                determinismHash,
                tickTrace);
        }

        private void BindTileFeatureDefinitionContext(EntityLogicSet entityLogicsForTick)
        {
            if (entityLogicsForTick == null)
            {
                return;
            }

            BindTileFeatureDefinitionContext(entityLogicsForTick.AiStateLogics);
            BindTileFeatureDefinitionContext(entityLogicsForTick.PreMovementStateLogics);
            BindTileFeatureDefinitionContext(entityLogicsForTick.MovementLogics);
            BindTileFeatureDefinitionContext(entityLogicsForTick.EnemyActionStateLogics);
        }

        private void BindTileFeatureDefinitionContext<TLogic>(IReadOnlyList<TLogic> logics)
        {
            if (logics == null)
            {
                return;
            }

            for (var i = 0; i < logics.Count; i++)
            {
                if (logics[i] is ITileFeatureDefinitionContextReceiver receiver)
                {
                    receiver.BindTileFeatureDefinitions(_tileFeatureDefinitions);
                }
            }
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

                var canCloseEnemyGlide = closeEnemyGlideKinematics &&
                                         IsEnemyInterruptedGlideKinematicParticipant(snapshot, entity);
                if (!canCloseEnemyGlide)
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

        private static bool IsEnemyInterruptedGlideKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            return IsEnemyLogicParticipant(entity) &&
                   EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity) &&
                   entity.aiMode == EnemyAiMode.Chase &&
                   snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                   glideState.HasAuthoritativeRecord &&
                   (glideState.Phase == EnemyGlidePhase.Active ||
                    glideState.Phase == EnemyGlidePhase.Recovery);
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
            var projectedWorld = new ProjectedWorld(snapshot, seedBaseSnapshot: true);

            var beforeMovementAiBatch = new FinalizationBatch();
            var beforeMovementAiContext = new RecordingFinalizationContext(beforeMovementAiBatch, snapshot, TickPhase.Plan);
            var aiPhaseResult = RunEnemyAiPhase(
                entityLogicsForTick.AiStateLogics,
                snapshot,
                in input,
                beforeMovementAiContext);
            WorldSnapshot topologyActivationPreviousSnapshot = null;
            planFinalizationBatch.MergeFrom(beforeMovementAiBatch);
            CaptureTopologyActivationPreviousSnapshot(
                ref topologyActivationPreviousSnapshot,
                projectedWorld,
                beforeMovementAiBatch,
                ProjectedWorldSnapshotReason.PlanAfterEnemyAi);
            projectedWorld.ApplyBatch(beforeMovementAiBatch, ProjectedWorldBatchReason.PlanBeforeMovementAi);
            var snapshotAfterEnemyAi = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanAfterEnemyAi);

            var kinematicClosureBatch = new FinalizationBatch();
            var kinematicClosureEvents = new List<string>();
            if (_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion)
            {
                CloseInterruptedUnitKinematics(
                    snapshotAfterEnemyAi,
                    kinematicClosureBatch,
                    kinematicClosureEvents,
                    closeEnemyGlideKinematics: _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion);
                planFinalizationBatch.MergeFrom(kinematicClosureBatch);
                CaptureTopologyActivationPreviousSnapshot(
                    ref topologyActivationPreviousSnapshot,
                    projectedWorld,
                    kinematicClosureBatch,
                    ProjectedWorldSnapshotReason.PlanAfterKinematicClosure);
                projectedWorld.ApplyBatch(kinematicClosureBatch, ProjectedWorldBatchReason.PlanKinematicClosure);
                snapshotAfterEnemyAi = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanAfterKinematicClosure);
            }

            var gravityFieldResult = GravityFieldRuntimeResolver.ResolvePreMovement(
                snapshotAfterEnemyAi,
                input.TickIndex,
                _gravityFieldChargeTicks,
                _gravityFieldActiveTicks,
                _gravityFieldLockedBoxOneShotState);
            var gravityFieldBatch = gravityFieldResult.Batch;
            var gravityFieldEvents = gravityFieldResult.EventLogEntries;
            if (gravityFieldBatch.Operations.Count > 0)
            {
                planFinalizationBatch.MergeFrom(gravityFieldBatch);
                CaptureTopologyActivationPreviousSnapshot(
                    ref topologyActivationPreviousSnapshot,
                    projectedWorld,
                    gravityFieldBatch,
                    ProjectedWorldSnapshotReason.PlanAfterGravityField);
                projectedWorld.ApplyBatch(gravityFieldBatch, ProjectedWorldBatchReason.PlanGravityField);
                snapshotAfterEnemyAi = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanAfterGravityField);
            }

            var preMovementBatch = new FinalizationBatch();
            var utilityTriggerIntents = new List<EnemyUtilityTriggerIntent>();
            var summonBehaviorTriggerIntents = new List<EnemySummonBehaviorTriggerIntent>();
            var preMovementContext = new RecordingFinalizationContext(
                preMovementBatch,
                snapshotAfterEnemyAi,
                TickPhase.Plan,
                utilityTriggerIntents,
                summonBehaviorTriggerIntents);
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
            summonBehaviorTriggerIntents.Sort(EnemySummonBehaviorTriggerIntentComparer.Instance);
            preMovementStateResult.SummonBehaviorTriggerIntents.AddRange(summonBehaviorTriggerIntents);
            planFinalizationBatch.MergeFrom(preMovementBatch);
            CaptureTopologyActivationPreviousSnapshot(
                ref topologyActivationPreviousSnapshot,
                projectedWorld,
                preMovementBatch,
                ProjectedWorldSnapshotReason.PlanPostPreMovement);
            projectedWorld.ApplyBatch(preMovementBatch, ProjectedWorldBatchReason.PlanPreMovementState);
            var preMovementUtilityResolveResult = EnemyUtilityResolver.ResolvePreMovementProjectedEffects(
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanPreMovementUtilityInput),
                preMovementStateResult.UtilityTriggerIntents,
                input.TickIndex);
            planFinalizationBatch.MergeFrom(preMovementUtilityResolveResult.Batch);
            CaptureTopologyActivationPreviousSnapshot(
                ref topologyActivationPreviousSnapshot,
                projectedWorld,
                preMovementUtilityResolveResult.Batch,
                ProjectedWorldSnapshotReason.PlanPostPreMovement);
            projectedWorld.ApplyBatch(preMovementUtilityResolveResult.Batch, ProjectedWorldBatchReason.PlanPreMovementUtility);
            AddRange(preMovementStateResult.EventLogEntries, preMovementUtilityResolveResult.EventLogEntries);

            var nextContestId = 1;
            var jumpLandingPlans = new List<JumpLandingPlan>();
            var jumpLandingSpaceContests = new List<Contest>();
            var jumpLandingEvents = new List<string>();
            var planSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanPostPreMovement);
            ResolvePlanJumpLandings(
                planSnapshot,
                input.TickIndex,
                entityLogicsForTick.MovementLogics,
                preMovementStateResult.Updates,
                jumpLandingPlans,
                jumpLandingSpaceContests,
                jumpLandingEvents,
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
                CaptureTopologyActivationPreviousSnapshot(
                    ref topologyActivationPreviousSnapshot,
                    projectedWorld,
                    playerActionAttemptBatch,
                    ProjectedWorldSnapshotReason.PlanAfterPlayerActionAttempt);
                projectedWorld.ApplyBatch(playerActionAttemptBatch, ProjectedWorldBatchReason.PlanPlayerActionAttempt);
                planSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanAfterPlayerActionAttempt);
            }

            var rawMovementIntents = new List<RawMovementIntent>();
            var movementDebugEvents = new List<string>();
            _movementIntentCollector.Collect(
                planSnapshot,
                in input,
                entityLogicsForTick.MovementLogics,
                rawMovementIntents,
                movementDebugEvents);
            FilterConsumedPlayerActionAttemptMovementIntents(rawMovementIntents, consumedPlayerActionAttemptEntityIds);
            var executableMovementIntents = FilterExecutionLockedMovementIntents(planSnapshot, input.TickIndex, rawMovementIntents, rejectedReasons);
            var sortedIntents = BuildMovementIntents(executableMovementIntents);
            var movementIntentPartitions = MovementIntentPartitioner.Partition(
                planSnapshot,
                sortedIntents,
                consumedPlayerActionAttemptEntityIds);
            var expansionIntents = movementIntentPartitions.GenericExpansionIntents;
            var kinematicMovementActionPlanPayloads = new Dictionary<int, MovementActionPlanPayload>();
            var playerTopologyTransitionBlockedSignals =
                new List<TickPlayerTopologyTransitionBlockedSignal>();
            var free2DBatch = new FinalizationBatch();
            BuildPlayerFree2DLocalLocomotionPlans(
                planSnapshot,
                movementIntentPartitions.PlayerFree2DOrdinaryIntents,
                input.PlayerCommand,
                input.TickIndex,
                rejectedReasons,
                free2DBatch,
                consumedPlayerActionAttemptEntityIds,
                playerTopologyTransitionBlockedSignals);
            planFinalizationBatch.MergeFrom(free2DBatch);
            CaptureTopologyActivationPreviousSnapshot(
                ref topologyActivationPreviousSnapshot,
                projectedWorld,
                free2DBatch,
                ProjectedWorldSnapshotReason.PlanAfterPlayerFree2DLocomotion);
            projectedWorld.ApplyBatch(free2DBatch, ProjectedWorldBatchReason.PlanPlayerFree2DLocomotion);
            planSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanAfterPlayerFree2DLocomotion);
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
            else
            {
                expansionIntents = RejectEnemyChargeActiveFallbackIntents(
                    planSnapshot,
                    expansionIntents,
                    rejectedReasons);
            }

            var playerTraversalSourceIds = CollectPlayerTraversalSourceIds(entityLogicsForTick.MovementLogics);
            var barricadeBlockFacts = new List<BarricadeBlockFact>();
            var boxSlideStops = new List<BoxSlideStopResult>();
            var expandedCandidates = new List<ActionGroup>();
            var preExpansionRejectedReasons = new List<string>(rejectedReasons);
            _movementExpander.Expand(
                planSnapshot,
                input.TickIndex,
                expansionIntents,
                playerTraversalSourceIds,
                expandedCandidates,
                rejectedReasons,
                barricadeBlockFacts,
                _tileFeatureDefinitions,
                boxSlideStops,
                playerTopologyTransitionBlockedSignals);
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
                barricadeBlockFacts,
                boxSlideStops,
                playerTopologyTransitionBlockedSignals,
                movementDebugEvents,
                nextContestId,
                aiPhaseResult,
                preMovementStateResult,
                snapshotAfterEnemyAi,
                planSnapshot,
                topologyActivationPreviousSnapshot,
                planFinalizationBatch,
                gravityFieldResult.PresentationEvents,
                gravityFieldResult.LockedTargetFacts,
                playerActionAttemptResolutions,
                preMovementUtilityResolveResult.GravityFieldAuraLockedTargetFacts);
        }

        private static void CaptureTopologyActivationPreviousSnapshot(
            ref WorldSnapshot topologyActivationPreviousSnapshot,
            ProjectedWorld projectedWorld,
            FinalizationBatch batch,
            ProjectedWorldSnapshotReason snapshotReason)
        {
            if (topologyActivationPreviousSnapshot != null ||
                projectedWorld == null ||
                !TryGetFirstTopologySourceOperationOrdinal(new[] { batch }, out _))
            {
                return;
            }

            topologyActivationPreviousSnapshot = projectedWorld.CreateSnapshot(snapshotReason);
        }

        private ResolvePhaseResult RunResolvePhase(
            WorldSnapshot planSnapshot,
            in TickInput input,
            DemoGameplayOverrideSnapshot demoGameplayOverrideSnapshot,
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
            projectedWorld.ApplyBatch(movementStageBatch);
            // postMovementSnapshot is the movement-visible resolve surface. Accepted
            // impact follow-through writes are materialized here before jump landing.
            var postMovementSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostMovement);

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
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveEnemyActionBeforeAttackInput),
                in input,
                EnemyActionStage.BeforeAttackCollection,
                enemyActionBeforeAttackContext,
                new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()));
            finalizationBatch.MergeFrom(enemyActionBeforeAttackBatch);
            projectedWorld.ApplyBatch(enemyActionBeforeAttackBatch);

            var attackSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveAttackSnapshot);
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
                tickIndex,
                demoGameplayOverrideSnapshot);
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
                impactDispositionRecords,
                planPhaseResult.BarricadeBlockFacts);

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
                projectedWorld.ApplyBatch(movementStageBatch);
                postMovementSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostMovement);

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
                    projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveEnemyActionBeforeAttackInput),
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
                projectedWorld.ApplyBatch(movementStageBatch);
                projectedWorld.ApplyBatch(jumpLandingResolveBatch);
                postMovementSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostMovement);

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
                    projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveEnemyActionBeforeAttackInput),
                    in input,
                    EnemyActionStage.BeforeAttackCollection,
                    enemyActionBeforeAttackContext,
                    new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()));
                finalizationBatch.MergeFrom(enemyActionBeforeAttackBatch);
                projectedWorld.ApplyBatch(enemyActionBeforeAttackBatch);
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
            var attackReadSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveAttackRead);
            var tileEffectEntityContacts = BuildTileEffectEntityContacts(
                planSnapshot,
                postMovementSnapshot,
                movementStageBatch,
                jumpLandingResolveBatch);
            var tileEffectBoxStops = BuildTileEffectBoxStops(
                planSnapshot,
                attackReadSnapshot,
                movementStageBatch);
            var tileFeatureActivationOccupantFacts = BuildDestroyTileActivationOccupantFacts(
                planPhaseResult.TopologyActivationPreviousSnapshot,
                postMovementSnapshot,
                _tileFeatureDefinitions,
                TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                planPhaseResult.PlanFinalizationBatch,
                movementStageBatch,
                jumpLandingResolveBatch);
            IReadOnlyList<TilePresentationEvent> tilePresentationEvents = Array.Empty<TilePresentationEvent>();
            var tileEffectResult = _tileEffectResolver.Resolve(
                new TileEffectResolutionContext(
                    tickIndex,
                    postMovementSnapshot,
                    _tileFeatureDefinitions,
                    tileEffectEntityContacts,
                    planSnapshot,
                    tileEffectBoxStops,
                    tileFeatureActivationOccupantFacts));
            tilePresentationEvents = tileEffectResult.TileEvents;
            if (!tileEffectResult.IsEmpty)
            {
                if (TileEffectResultTargetsUnit(tileEffectResult, postMovementSnapshot))
                {
                    finalizationBatch = new FinalizationBatch();
                    finalizationBatch.MergeFrom(planPhaseResult.PlanFinalizationBatch);
                    finalizationBatch.MergeFrom(movementStageBatch);
                    finalizationBatch.MergeFrom(jumpLandingResolveBatch);
                    if (!tileEffectResult.Operations.IsEmpty)
                    {
                        finalizationBatch.ApplyTileFeatureOperations(tileEffectResult.Operations);
                    }

                    if (tileEffectResult.EntityOperations.Operations.Count > 0)
                    {
                        finalizationBatch.MergeFrom(tileEffectResult.EntityOperations);
                    }

                    projectedWorld = new ProjectedWorld(planSnapshot);
                    projectedWorld.ApplyBatch(movementStageBatch);
                    projectedWorld.ApplyBatch(jumpLandingResolveBatch);
                    if (!tileEffectResult.Operations.IsEmpty)
                    {
                        projectedWorld.ApplyTileFeatureOperations(tileEffectResult.Operations);
                    }

                    if (tileEffectResult.EntityOperations.Operations.Count > 0)
                    {
                        projectedWorld.ApplyBatch(tileEffectResult.EntityOperations);
                    }

                    postMovementSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostMovement);
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
                        projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveEnemyActionBeforeAttackInput),
                        in input,
                        EnemyActionStage.BeforeAttackCollection,
                        enemyActionBeforeAttackContext,
                        new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()));
                    finalizationBatch.MergeFrom(enemyActionBeforeAttackBatch);
                    projectedWorld.ApplyBatch(enemyActionBeforeAttackBatch);
                    attackReadSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveAttackRead);
                }
                else
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

                    attackReadSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveAttackRead);
                }
            }

            attackSnapshot = attackReadSnapshot;
            var duePendingCellImpacts = CollectDuePendingCellImpacts(attackSnapshot, tickIndex);
            attackPlanResult = BuildAttackPlan(
                attackSnapshot,
                in input,
                entityLogicsForTick.AttackLogics,
                frozenMovementReservationExport,
                drainedDelayedAttackEffects,
                tickIndex,
                duePendingCellImpacts);
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
                tickIndex,
                demoGameplayOverrideSnapshot);
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
            for (var i = 0; i < attackPlanResult.PendingCellImpactResolutions.Count; i++)
            {
                var impactResolution = attackPlanResult.PendingCellImpactResolutions[i];
                attackStageBatch.RemovePendingCellImpact(impactResolution.Impact.ImpactId);
                attackCommitEvents.Add(BuildPendingCellImpactCommitEvent(impactResolution));
            }
            var motionInterruptRecords = MaterializeUnitMotionInterrupts(
                attackSnapshot,
                damageResolutions,
                destroyResolutions,
                attackStageBatch,
                attackCommitEvents,
                tickIndex,
                interruptEnemyGlideKinematics: _runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion);
            finalizationBatch.MergeFrom(attackStageBatch);
            projectedWorld.ApplyBatch(attackStageBatch);
            AddRange(resolutionRecords, attackResolutionRecords);

            var enemyActionAfterAttackBatch = new FinalizationBatch();
            var enemyActionAfterAttackContext = new RecordingFinalizationContext(enemyActionAfterAttackBatch);
            CommitEnemyActionState(
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostAttack),
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
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostAttack),
                in input,
                entityLogicsForTick.AiStateLogics,
                EnemyAiTransitionStage.AfterAttack,
                afterAttackAiContext,
                aiPhaseResult.AfterAttackTransitions);
            finalizationBatch.MergeFrom(afterAttackAiBatch);
            projectedWorld.ApplyBatch(afterAttackAiBatch);
            var utilityResolveResult = EnemyUtilityResolver.ResolvePostAttackEffects(
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostAttack),
                planPhaseResult.PreMovementStatePhaseResult.UtilityTriggerIntents,
                planPhaseResult.PreMovementStatePhaseResult.SummonBehaviorTriggerIntents,
                tickIndex,
                _entityIdAllocator,
                _enemySpawnDefaultsByArchetypeId,
                _tileFeatureDefinitions);
            finalizationBatch.MergeFrom(utilityResolveResult.Batch);
            projectedWorld.ApplyBatch(utilityResolveResult.Batch);
            var postAttackSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostAttack);

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
            AddRange(movementResolvedOperations, tileEffectResult.EntityOperations.Operations);
            AppendBoxInteractionLockBlockedEvents(movementRejectedReasons, movementCommitEvents, tickIndex);
            var movementPhaseResult = new MovementPhaseResult(
                planPhaseResult.RawIntents,
                planPhaseResult.SortedIntents,
                movementResolutionRecords,
                impactDispositionRecords,
                movementResolvedOperations,
                movementCommitEvents,
                movementRejectedReasons,
                planPhaseResult.BarricadeBlockFacts,
                FilterSelectedBoxSlideStops(
                    planPhaseResult.BoxSlideStops,
                    movementResolutionRecords,
                    planPhaseResult.MovementActionPlanPayloads),
                planPhaseResult.PlayerTopologyTransitionBlockedSignals,
                planPhaseResult.MovementDebugEvents);

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
                motionInterruptRecords,
                attackPlanResult.PendingCellImpactResolutions);

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
                planPhaseResult.GravityFieldLockedTargetFacts,
                planPhaseResult.EnemyGravityFieldAuraLockedTargetFacts);
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
            var timingStartedAt = CleanupSlice3Diagnostics.BeginTiming();
            using var markerScope = RunCleanupPhaseMarker.Auto();
            try
            {
                phaseTrace.Add("Cleanup:Enter");
                var cleanupPhaseResult = _cleanupProcessor.Process(snapshot, writeContext, tickIndex);
                cleanupPhaseResult = ExpireBoxInteractionLocks(snapshot, writeContext, tickIndex, cleanupPhaseResult);
                cleanupPhaseResult = ExpireEnemyGravityFieldAuraFields(snapshot, writeContext, tickIndex, cleanupPhaseResult);
                cleanupPhaseResult = ExpirePendingEnemyBlockedReactions(snapshot, writeContext, tickIndex, cleanupPhaseResult);
                phaseTrace.Add("Cleanup:Exit");
                completedPhases.Add(TickPhase.Cleanup);

                return cleanupPhaseResult;
            }
            finally
            {
                CleanupSlice3Diagnostics.RecordRunCleanupPhaseTiming(timingStartedAt);
            }
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

        private static CleanupPhaseResult ExpirePendingEnemyBlockedReactions(
            WorldSnapshot snapshot,
            ICleanupCommitContext writeContext,
            int tickIndex,
            CleanupPhaseResult cleanupPhaseResult)
        {
            var expiredEventLogEntries = new List<string>();
            var removedEntityIdsThisTick = cleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(cleanupPhaseResult.RemovedEntityIds)
                : null;
            var reactionEntries = new List<PendingEnemyBlockedReactionSnapshotEntry>();
            snapshot.EnumeratePendingEnemyBlockedReactionsOrdered(reactionEntries);

            for (var i = 0; i < reactionEntries.Count; i++)
            {
                var entry = reactionEntries[i];
                if ((removedEntityIdsThisTick != null && removedEntityIdsThisTick.Contains(entry.EntityId)) ||
                    !entry.Reaction.ShouldCleanupAtEndOfTick(tickIndex))
                {
                    continue;
                }

                writeContext.ClearPendingEnemyBlockedReaction(entry.EntityId);
                expiredEventLogEntries.Add(
                    $"PendingEnemyBlockedReactionExpired|E={entry.EntityId}|Created={entry.Reaction.CreatedTick}|Expire={entry.Reaction.ExpireTick}|Tick={tickIndex}");
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

        private static CleanupPhaseResult ExpireEnemyGravityFieldAuraFields(
            WorldSnapshot snapshot,
            ICleanupCommitContext writeContext,
            int tickIndex,
            CleanupPhaseResult cleanupPhaseResult)
        {
            var expiredEventLogEntries = new List<string>();
            var fieldEntries = new List<EnemyGravityFieldAuraFieldSnapshotEntry>();
            snapshot.EnumerateEnemyGravityFieldAuraFieldStatesOrdered(fieldEntries);

            for (var i = 0; i < fieldEntries.Count; i++)
            {
                var entry = fieldEntries[i];
                if (entry.State.IsActive(tickIndex))
                {
                    continue;
                }

                writeContext.RemoveEnemyGravityFieldAuraFieldState(entry.FieldId);
                expiredEventLogEntries.Add(
                    $"EnemyGravityFieldAuraFieldExpired|Field={entry.FieldId}|Source={entry.State.SourceEntityId}|Effect={entry.State.SourceEffectIndex}|Expires={entry.State.ExpiresTickExclusive}|Tick={tickIndex}");
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
                moonBlockGeneratorResult.RespawnFacts.Count > 0 ||
                moonBlockGeneratorResult.BlockedFacts.Count > 0)
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
                var blockedFacts = new List<MoonBlockGeneratorBlockedFact>(
                    respawnPhaseResult.MoonBlockGeneratorBlockedFacts.Count +
                    moonBlockGeneratorResult.BlockedFacts.Count);
                AddRange(blockedFacts, respawnPhaseResult.MoonBlockGeneratorBlockedFacts);
                AddRange(blockedFacts, moonBlockGeneratorResult.BlockedFacts);
                respawnPhaseResult = new RespawnPhaseResult(
                    respawnPhaseResult.RespawnedEntities,
                    eventLogEntries,
                    respawnPhaseResult.PlayerRespawnDelayRecords,
                    respawnPhaseResult.RespawnPlacementRecords,
                    respawnPhaseResult.TopologyResetRequest,
                    respawnFacts,
                    blockedFacts);
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

        private void BuildPlayerFree2DLocalLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
            FinalizationBatch batch,
            HashSet<int> consumedPlayerActionAttemptEntityIds,
            List<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals)
        {
            var consumedFree2DIntentIds = new HashSet<int>();
            var settledApproachPlayerIds = new HashSet<int>();
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

                    var nativeTopologyDisposition = TryMaterializePlayerFree2DNativeTopologyTransition(
                        snapshot,
                        entity,
                        playerControlState,
                        playerCommand,
                        intent,
                        rejectedReasons,
                        batch,
                        playerTopologyTransitionBlockedSignals);
                    if (nativeTopologyDisposition == PlayerFree2DNativeTopologyDisposition.Materialized)
                    {
                        consumedFree2DIntentIds.Add(intent.IntentId);
                        settledApproachPlayerIds.Add(entity.entityId);
                        continue;
                    }

                    if (nativeTopologyDisposition == PlayerFree2DNativeTopologyDisposition.TargetRejected)
                    {
                        consumedFree2DIntentIds.Add(intent.IntentId);
                        continue;
                    }

                    if (nativeTopologyDisposition == PlayerFree2DNativeTopologyDisposition.AccessBlocked)
                    {
                        consumedFree2DIntentIds.Add(intent.IntentId);
                        settledApproachPlayerIds.Add(entity.entityId);
                        continue;
                    }

                    consumedFree2DIntentIds.Add(intent.IntentId);
                }

                if (consumedByActionAttempt)
                {
                    continue;
                }

                if (settledApproachPlayerIds.Contains(entity.entityId))
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
                    batch);
            }

        }

        private PlayerFree2DNativeTopologyDisposition TryMaterializePlayerFree2DNativeTopologyTransition(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            MoveIntent intent,
            List<string> rejectedReasons,
            FinalizationBatch batch,
            List<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals)
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
                    out var transition,
                    _tileFeatureDefinitions))
            {
                if (ShouldRecordPlayerFree2DNativeTopologyReject(transition.RejectReason))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Plan|Source={entity.entityId}|I={intent.IntentId}|Reason=Free2DTopologyNativeRejected|RejectedBy={transition.RejectReason}|Anchor={FormatCell(entity.position)}");
                }

                AddPlayerFree2DTopologyTransitionBlockedSignalIfNeeded(
                    playerTopologyTransitionBlockedSignals,
                    snapshot.Topology,
                    directionDelta,
                    transition);

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

        private static void AddPlayerFree2DTopologyTransitionBlockedSignalIfNeeded(
            List<TickPlayerTopologyTransitionBlockedSignal> signals,
            CubeTopologyState sourceTopology,
            Vector2Int directionDelta,
            in Free2DTopologyTransitionResult transition,
            TickTraversalBlockerKind explicitBlockerKind = TickTraversalBlockerKind.None)
        {
            if (signals == null ||
                transition.EntityId <= 0 ||
                transition.RotationKind == CubeRotationKind.None ||
                !TryResolveMoveDirection(directionDelta, out var direction))
            {
                return;
            }

            var primaryBlockerKind = explicitBlockerKind == TickTraversalBlockerKind.None
                ? ResolveFree2DTopologyBlockedSignalKind(transition)
                : explicitBlockerKind;
            if (primaryBlockerKind == TickTraversalBlockerKind.None ||
                primaryBlockerKind == TickTraversalBlockerKind.BoardEdge)
            {
                return;
            }

            signals.Add(
                new TickPlayerTopologyTransitionBlockedSignal(
                    transition.EntityId,
                    direction,
                    transition.SourceAnchor,
                    transition.TargetAnchor,
                    sourceTopology,
                    transition.UpdatedTopology,
                    transition.RotationKind,
                    primaryBlockerKind));
        }

        private static bool TryResolveMoveDirection(Vector2Int directionDelta, out Direction direction)
        {
            if (directionDelta == Vector2Int.up)
            {
                direction = Direction.Up;
                return true;
            }

            if (directionDelta == Vector2Int.down)
            {
                direction = Direction.Down;
                return true;
            }

            if (directionDelta == Vector2Int.left)
            {
                direction = Direction.Left;
                return true;
            }

            if (directionDelta == Vector2Int.right)
            {
                direction = Direction.Right;
                return true;
            }

            direction = Direction.None;
            return false;
        }

        private static TickTraversalBlockerKind ResolveFree2DTopologyBlockedSignalKind(
            in Free2DTopologyTransitionResult transition)
        {
            if (TryResolvePrimaryNonBoardEdgeBlockerKind(
                    transition.TargetLegality.Blockers,
                    out var blockerKind))
            {
                return blockerKind;
            }

            return transition.RejectReason switch
            {
                Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid => TickTraversalBlockerKind.Solid,
                Free2DTopologyTransitionRejectReason.TargetFaceBlockedByUnit => TickTraversalBlockerKind.Unit,
                Free2DTopologyTransitionRejectReason.TargetFaceBlockedByReservation => TickTraversalBlockerKind.Reservation,
                Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTileFeature => TickTraversalBlockerKind.TileFeature,
                _ => TickTraversalBlockerKind.None,
            };
        }

        private static bool TryResolvePrimaryNonBoardEdgeBlockerKind(
            IReadOnlyList<LegalityBlocker> blockers,
            out TickTraversalBlockerKind primaryBlockerKind)
        {
            if (blockers != null)
            {
                for (var i = 0; i < blockers.Count; i++)
                {
                    var blockerKind = blockers[i].Kind;
                    if (blockerKind == LegalityBlockerKind.BoardEdge)
                    {
                        continue;
                    }

                    primaryBlockerKind = MovementExpander.ToTickTraversalBlockerKind(blockerKind);
                    return primaryBlockerKind != TickTraversalBlockerKind.None;
                }
            }

            primaryBlockerKind = TickTraversalBlockerKind.None;
            return false;
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
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedByUnit ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedByReservation ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTileFeature ||
                   reason == Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked ||
                   reason == Free2DTopologyTransitionRejectReason.RemapInvalid;
        }

        private enum PlayerFree2DNativeTopologyDisposition
        {
            NotCandidate = 0,
            Materialized = 1,
            TargetRejected = 2,
            AccessBlocked = 3,
        }

        private void ResolvePlayerFree2DLocalLocomotion(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            int tickIndex,
            List<string> rejectedReasons,
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

            var blocksActiveDestroyTileForEntity = TileFeatureHazardQueries.IsDestroyTileLethalForUnit(entity);

            bool BlocksPlayerVoluntaryFree2DDestroyTile(
                SurfaceCell candidateCell,
                CubeTopologyState evaluationTopology,
                out ContinuousLocomotionRejectionReason rejectedBy)
            {
                if (blocksActiveDestroyTileForEntity &&
                    TileFeatureAccessQueries.IsActiveDestroyTile(
                        snapshot,
                        _tileFeatureDefinitions,
                        candidateCell,
                        evaluationTopology))
                {
                    rejectedBy = ContinuousLocomotionRejectionReason.PlayerVoluntaryDestroyTileEntryBlocked;
                    return true;
                }

                rejectedBy = ContinuousLocomotionRejectionReason.None;
                return false;
            }

            if (!SurfaceContinuousLocomotionQueries.TryResolveSameFaceAxisMove(
                    snapshot,
                    entity.entityId,
                    delta,
                    _playerContinuousLocomotion.CollisionRadiusUnits,
                    out var sweep,
                    _tileFeatureDefinitions,
                    BlocksPlayerVoluntaryFree2DDestroyTile))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={entity.entityId}|Reason=Free2DContinuousSweepRejected|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
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
                if (sweep.RejectedBy == ContinuousLocomotionRejectionReason.PlayerVoluntaryDestroyTileEntryBlocked)
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Free2D|Source={entity.entityId}|Reason=PlayerVoluntaryDestroyTileEntryBlocked|Anchor={FormatCell(entity.position)}");
                }
                else
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Plan|Source={entity.entityId}|Reason=Free2DContinuousBlocked|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
                }
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
                        tickIndex,
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

        private bool TryCreatePlayerActionAttemptResolution(
            WorldSnapshot snapshot,
            in EntityState entity,
            in PlayerControlState playerControlState,
            PlayerTickCommand playerCommand,
            int tickIndex,
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

            var direction = PlayerActionDirectionResolver.Resolve(playerCommand, entity.facing);
            var queuedActionKind = PlayerControlQueries.ToQueuedFree2DActionKind(actionKind);
            var feedbackKind = PlayerActionAttemptFeedbackKind.NoTarget;
            var targetEntityId = 0;
            var hasTarget = false;
            var emitsVisualFeedback = true;

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
                var withinAssistAttemptWindow = !free2DPose.HasValue ||
                                                free2DPose.Value.State.localOffset.IsZero ||
                                                IsWithinFree2DActionAssistSettleWindow(
                                                    free2DPose.Value.State.localOffset);
                if (withinAssistAttemptWindow &&
                    PlayerControlQueries.TryResolveBoxInteractionLockedTarget(
                        snapshot,
                        entity,
                        anchor,
                        queuedActionKind,
                        direction,
                        tickIndex,
                        out var lockedTarget))
                {
                    targetEntityId = lockedTarget.TargetEntityId;
                    hasTarget = true;
                    feedbackKind = PlayerActionAttemptFeedbackKind.Invalid;
                    emitsVisualFeedback = false;
                }
                else if (PlayerControlQueries.TryResolveFree2DActionAssistCandidate(
                        snapshot,
                        entity,
                        anchor,
                        queuedActionKind,
                        direction,
                        out var target))
                {
                    targetEntityId = target.TargetEntityId;
                    hasTarget = true;
                    feedbackKind = !withinAssistAttemptWindow
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
                else if (actionKind == PlayerActionKind.Flip &&
                         PlayerControlQueries.TryResolveBlockedFlipLandingTarget(
                             snapshot,
                             entity,
                             anchor,
                             direction,
                             tickIndex,
                             out var blockedFlipTarget))
                {
                    targetEntityId = blockedFlipTarget.TargetEntityId;
                    hasTarget = true;
                    feedbackKind = PlayerActionAttemptFeedbackKind.Invalid;
                    emitsVisualFeedback = false;
                }
            }

            resolution = new PlayerActionAttemptResolution(
                entity.entityId,
                actionKind,
                direction == Direction.None ? Direction.Up : direction,
                feedbackKind,
                consumesMovement: true,
                emitsFakePresentation: true,
                targetEntityId,
                hasTarget,
                emitsVisualFeedback);
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
            var actionDirection = PlayerActionDirectionResolver.Resolve(playerCommand, entity.facing);
            if (playerControlState.activeAction.IsActive ||
                PlayerControlQueries.HasQueuedFree2DAction(playerControlState) ||
                pose.State.localOffset.IsZero ||
                !TryResolveQueuedFree2DActionKind(playerCommand, out var actionKind) ||
                actionDirection == Direction.None)
            {
                return false;
            }

            if (!IsWithinFree2DActionAssistSettleWindow(pose.State.localOffset))
            {
                rejectedReasons.Add(
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=OutsideSettleWindow|Source={entity.entityId}|Kind={actionKind}|Direction={actionDirection}|Offset={pose.LocalOffset}|Window={_playerContinuousLocomotion.ActionAssistSettleWindowUnits}");
                return false;
            }

            if (PlayerControlQueries.TryResolveBoxInteractionLockedTarget(
                    snapshot,
                    entity,
                    pose.AnchorCell,
                    actionKind,
                    actionDirection,
                    tickIndex,
                    out var lockedTarget))
            {
                rejectedReasons.Add(
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=BoxInteractionLocked|Source={entity.entityId}|Kind={actionKind}|Direction={actionDirection}|Target={lockedTarget.TargetEntityId}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}");
                return false;
            }

            if (!PlayerControlQueries.HasFree2DActionAssistCandidate(
                    snapshot,
                    entity,
                    pose.AnchorCell,
                    actionKind,
                    actionDirection))
            {
                rejectedReasons.Add(
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=NoActionCandidate|Source={entity.entityId}|Kind={actionKind}|Direction={actionDirection}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}");
                return false;
            }

            queuedPlayerControlState = PlayerControlQueries.QueueFree2DAction(
                playerControlState,
                actionKind,
                actionDirection,
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
                $"Free2DActionAssistQueued|Stage=Plan|Source={entity.entityId}|Kind={actionKind}|Direction={actionDirection}|RequestedTick={tickIndex}|Anchor={FormatCell(entity.position)}|Offset={pose.LocalOffset}");
            return true;
        }

        private bool IsWithinFree2DActionAssistSettleWindow(SimulationOffset2 localOffset)
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
            if (PlayerControlQueries.TryResolveBoxInteractionLockedTarget(
                    snapshot,
                    entity,
                    pose.AnchorCell,
                    queuedAction.kind,
                    queuedAction.direction,
                    tickIndex,
                    out var lockedTarget))
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
                    $"Free2DActionAssistRejected|Stage=Plan|Reason=BoxInteractionLocked|Source={entity.entityId}|Kind={queuedAction.kind}|Direction={queuedAction.direction}|Target={lockedTarget.TargetEntityId}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}|RequestedTick={queuedAction.requestedTick}");
                rejectedReasons.Add(
                    $"Free2DActionAssistCleared|Stage=Plan|Source={entity.entityId}|Reason=BoxInteractionLocked");
                return;
            }

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

            var nextOffset = new SimulationOffset2(
                SimulationFixed.FromRaw(nextX),
                SimulationFixed.FromRaw(nextY));
            var isSettled = nextOffset.IsZero;
            return new UnitContinuousLocomotionState
            {
                localOffset = nextOffset,
                velocity = isSettled
                    ? SimulationVelocity2.Zero
                    : new SimulationVelocity2(
                        SimulationFixed.FromRaw(deltaX),
                        SimulationFixed.FromRaw(deltaY)),
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

        private static bool SelectAlignAxisX(SimulationOffset2 offset)
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

        private SimulationVelocity2 CreateContinuousDelta(
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
                return new SimulationVelocity2(
                    SimulationFixed.FromRaw(directionDelta.x * rawUnits),
                    SimulationFixed.Zero);
            }

            nextRemainderY += _playerContinuousLocomotion.UnitsPerTickRemainder;
            if (nextRemainderY >= _playerContinuousLocomotion.TicksPerCell)
            {
                rawUnits++;
                nextRemainderY -= _playerContinuousLocomotion.TicksPerCell;
            }

            facing = directionDelta.y > 0 ? Direction.Up : Direction.Down;
            return new SimulationVelocity2(
                SimulationFixed.Zero,
                SimulationFixed.FromRaw(directionDelta.y * rawUnits));
        }

        private static UnitContinuousLocomotionState CreateContinuousLocomotionState(
            UnitContinuousLocomotionState sourceState,
            ContinuousLocomotionSweepResult sweep,
            Direction facing,
            Direction lastMoveDirection,
            int nextRemainderX,
            int nextRemainderY)
        {
            var velocity = sweep.Blocked ? SimulationVelocity2.Zero : sweep.ResolvedVelocity;
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
            var remainingExpansionIntents = new List<MoveIntent>(sortedIntents.Count);
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

                    remainingExpansionIntents.Add(intent);
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

                remainingExpansionIntents.Add(intent);
            }

            return remainingExpansionIntents;
        }

        private List<MoveIntent> BuildEnemyChargeKinematicLocomotionPlans(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            List<string> rejectedReasons,
            Dictionary<int, MovementActionPlanPayload> kinematicPayloads)
        {
            var remainingExpansionIntents = new List<MoveIntent>(sortedIntents.Count);
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

                    remainingExpansionIntents.Add(intent);
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

                remainingExpansionIntents.Add(intent);
            }

            return remainingExpansionIntents;
        }

        private List<MoveIntent> RejectEnemyChargeActiveFallbackIntents(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<string> rejectedReasons)
        {
            var filteredIntents = new List<MoveIntent>(sortedIntents.Count);
            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (TryResolveEnemyChargeKinematicStartScope(
                        snapshot,
                        intent,
                        out var entity,
                        out _,
                        out _,
                        out _))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyChargeKinematicFlagOffActiveMoveRejected|Anchor={FormatCell(entity.position)}");
                    continue;
                }

                filteredIntents.Add(intent);
            }

            return filteredIntents;
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
                    out var glideKinematicKind,
                    out var isGlideDirectionMismatch))
            {
                if (isGlideDirectionMismatch)
                {
                    handledByKinematic = true;
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyGlideDirectionMismatch");
                    return true;
                }

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
                snapshot.Topology,
                _tileFeatureTraversalEvidence);
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
                    out var sweep,
                    _tileFeatureDefinitions) ||
                sweep.Blocked)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Plan|Source={intent.SourceId}|I={intent.IntentId}|Reason=EnemyKinematicSweepRejected|RejectedBy={sweep.RejectedBy}|Anchor={FormatCell(entity.position)}");
                return true;
            }

            var totalTicks = intent.OrdinaryKinematicMoveTicks > 0
                ? intent.OrdinaryKinematicMoveTicks
                : _unitKinematicLocomotionTiming.TicksPerCell;
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
            if (!EnemyMovementStrategyShared.CanTraverseChargeStepIgnoringUnits(
                    snapshot,
                    entity,
                    delta,
                    _tileFeatureDefinitions))
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
            // A Flip or another solid can arrive after the step starts. Revalidate only
            // when changing anchors; once committed, the old cell is no longer our anchor.
            IReadOnlyList<EnemyLocomotionWritePayload> enemyLocomotionWrites =
                Array.Empty<EnemyLocomotionWritePayload>();
            if (outcome.AnchorChanged &&
                !EnemyMovementStrategyShared.CanTraverseChargeStepIgnoringUnits(
                    snapshot,
                    entity,
                    new Vector2Int(stepDirectionX, stepDirectionY),
                    _tileFeatureDefinitions))
            {
                outcome = CreateBlockedKinematicMotionOutcome(
                    entityId, pose, KinematicSweepRejectionReason.TraversalBlocked);
                enemyLocomotionWrites = new[] { new EnemyLocomotionWritePayload(entityId, 0) };
            }

            var enemyChargeWrites = CreateEnemyChargeKinematicCompletionWrites(snapshot, entityId, outcome);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                _idAllocator.AllocateIntentId(),
                entityId,
                priority: 100,
                outcome: outcome,
                facing: facing,
                writeFacing: true,
                enemyLocomotionWrites: enemyLocomotionWrites,
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
                pose.IsSettledAtAnchor ||
                pose.Mode != MotionMode.Voluntary ||
                !TryResolveStepDirection(pose.State, out var stepDirectionX, out var stepDirectionY, out var facing))
            {
                return false;
            }

            if (!IsEnemyKinematicContinuationParticipant(snapshot, entity, pose))
            {
                return TryBuildEnemyGlideBoundaryKinematicClosePayload(
                    snapshot,
                    entity,
                    pose,
                    rejectedReasons,
                    out payload);
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
            if (continuationGlideKind == EnemyGlideKinematicKind.None &&
                outcome.AnchorChanged)
            {
                var anchorCommitLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                    snapshot,
                    EntityType.Unit,
                    outcome.ResolvedAnchorCell,
                    entityId,
                    snapshot.Topology,
                    CubeRotationKind.None,
                    snapshot.Topology,
                    _tileFeatureTraversalEvidence);
                if (anchorCommitLegality.Verdict != LegalityVerdict.Allowed)
                {
                    rejectedReasons.Add(FormatEnemyKinematicContinuationBlockedReason(
                        entityId,
                        pose,
                        outcome.ResolvedAnchorCell,
                        anchorCommitLegality));
                    var pendingBlockedReactionWrites =
                        TryCreatePendingEnemyBlockedReactionWrite(
                            snapshot,
                            entity,
                            pose,
                            outcome.ResolvedAnchorCell,
                            facing,
                            anchorCommitLegality,
                            tickIndex,
                            out var pendingBlockedReactionWrite)
                            ? new[] { pendingBlockedReactionWrite }
                            : null;
                    outcome = CreateBlockedKinematicMotionOutcome(
                        entityId,
                        pose,
                        KinematicSweepRejectionReason.TraversalBlocked);
                    payload = CreateKinematicMovementPayload(
                        _idAllocator.AllocateGroupId(),
                        _idAllocator.AllocateIntentId(),
                        entityId,
                        priority: 100,
                        outcome: outcome,
                        facing: facing,
                        writeFacing: true,
                        executionBoundaryKind: MovementExecutionBoundaryKind.UnitOrdinaryLocomotion,
                        boundaryReason: ResolveEnemyKinematicContinuationBoundaryReason(continuationGlideKind),
                        pendingEnemyBlockedReactionWrites: pendingBlockedReactionWrites);

                    return true;
                }
            }

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

        private bool TryBuildEnemyGlideBoundaryKinematicClosePayload(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitKinematicPose pose,
            List<string> rejectedReasons,
            out MovementActionPlanPayload payload)
        {
            payload = null;
            if (!_runtimeFeatureFlags.EnableEnemyGlideKinematicLocomotion ||
                !IsEnemyGlideOwnedKinematicPose(snapshot, entity, pose) ||
                !snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) ||
                glideState.Phase == EnemyGlidePhase.Active)
            {
                return false;
            }

            rejectedReasons.Add(
                $"MovementNoOp|Stage=Plan|Source={entity.entityId}|Reason=EnemyGlideBoundaryKinematicClosed|Phase={glideState.Phase}|Anchor={FormatCell(pose.AnchorCell)}");
            var outcome = CreateBlockedKinematicMotionOutcome(
                entity.entityId,
                pose,
                KinematicSweepRejectionReason.TraversalBlocked);
            payload = CreateKinematicMovementPayload(
                _idAllocator.AllocateGroupId(),
                _idAllocator.AllocateIntentId(),
                entity.entityId,
                priority: 100,
                outcome: outcome,
                facing: entity.facing,
                writeFacing: false,
                executionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion,
                boundaryReason: "EnemyGlideBoundaryKinematicClosed");
            return true;
        }

        private static string FormatEnemyKinematicContinuationBlockedReason(
            int entityId,
            UnitKinematicPose pose,
            SurfaceCell blockedAnchorCell,
            LegalityResult legality)
        {
            var blocker = legality.Blockers.Count > 0 ? legality.Blockers[0] : default;
            var blockerId = legality.Blockers.Count > 0 ? blocker.EntityId : 0;
            var blockerKind = legality.Blockers.Count > 0 ? blocker.Kind.ToString() : "None";
            return
                $"EnemyKinematicContinuationBlocked|E={entityId}|From={FormatCell(pose.AnchorCell)}|To={FormatCell(blockedAnchorCell)}|Blocker={blockerId}|BlockerKind={blockerKind}|Boundary={MovementExecutionBoundaryKind.LocomotionAnchorCommit}|BoundaryReason=OrdinaryKinematicAnchorCommit|{LegalityDiagnosticsFormatter.FormatStableSummary(legality)}";
        }

        private bool TryResolveEnemyKinematicStartScope(
            WorldSnapshot snapshot,
            MoveIntent intent,
            out EntityState entity,
            out UnitKinematicPose pose,
            out Vector2Int delta,
            out SurfaceCell destination,
            out Direction facing,
            out EnemyGlideKinematicKind glideKinematicKind,
            out bool isGlideDirectionMismatch)
        {
            entity = default;
            pose = default;
            delta = Vector2Int.zero;
            destination = default;
            facing = Direction.None;
            glideKinematicKind = EnemyGlideKinematicKind.None;
            isGlideDirectionMismatch = false;

            if (intent == null)
            {
                return false;
            }

            if (intent.CommandKind != Movement.MovementCommandKind.Move)
            {
                return false;
            }

            if (!snapshot.TryGetEntity(intent.SourceId, out entity))
            {
                return false;
            }

            if (!IsEnemyLogicParticipant(entity))
            {
                return false;
            }

            if (!IsEnemyKinematicStartParticipant(snapshot, entity, out glideKinematicKind))
            {
                return false;
            }

            if (!snapshot.TryGetUnitKinematicPose(intent.SourceId, out pose))
            {
                return false;
            }

            if (!pose.IsSettledAtAnchor)
            {
                return false;
            }

            delta = intent.Destination - entity.position.PlanarPosition;
            if (!TryResolveKinematicVelocity(delta, out _, out facing))
            {
                return false;
            }

            if (!snapshot.TryResolveUnitStep(
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

        private static bool TryCreatePendingEnemyBlockedReactionWrite(
            WorldSnapshot snapshot,
            in EntityState entity,
            UnitKinematicPose pose,
            SurfaceCell blockedTargetCell,
            Direction blockedDirection,
            LegalityResult anchorCommitLegality,
            int tickIndex,
            out PendingEnemyBlockedReactionWritePayload write)
        {
            write = default;
            if (snapshot == null ||
                pose.Mode != MotionMode.Voluntary ||
                !CanRecordPendingEnemyBlockedReactionForMode(entity.aiMode) ||
                !DirectionUtility.IsCardinal(blockedDirection) ||
                !IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity))
            {
                return false;
            }

            if (snapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState) &&
                BlocksEnemyBlockedReactionForJump(jumpState, tickIndex))
            {
                return false;
            }

            if (snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                glideState.HasAuthoritativeRecord)
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

            if (!TryFindSolidBoxOrWallBlocker(anchorCommitLegality, out var blocker))
            {
                return false;
            }

            var reaction = new PendingEnemyBlockedReaction(
                entity.entityId,
                EnemyBlockedReactionKind.KinematicContinuationTargetBlocked,
                entity.aiMode,
                pose.AnchorCell,
                blockedTargetCell,
                blockedDirection,
                blocker.Kind,
                blocker.SolidKind,
                blocker.EntityType,
                blocker.EntityId > 0 ? blocker.EntityId : (int?)null,
                tickIndex,
                tickIndex + 1);
            write = new PendingEnemyBlockedReactionWritePayload(entity.entityId, reaction);
            return true;
        }

        private static bool CanRecordPendingEnemyBlockedReactionForMode(EnemyAiMode mode)
        {
            return mode == EnemyAiMode.Chase ||
                   mode == EnemyAiMode.Patrol;
        }

        private static bool BlocksEnemyBlockedReactionForJump(
            in EnemyJumpRuntimeState jumpState,
            int tickIndex)
        {
            return BlocksOrdinaryEnemyKinematicLocomotionForJump(jumpState.phase) ||
                   (jumpState.phase == EnemyJumpPhase.Cooldown &&
                    jumpState.landingTick == tickIndex);
        }

        private static bool TryFindSolidBoxOrWallBlocker(
            LegalityResult legality,
            out LegalityBlocker blocker)
        {
            var blockers = legality.Blockers;
            for (var i = 0; i < blockers.Count; i++)
            {
                var candidate = blockers[i];
                if (candidate.Kind != LegalityBlockerKind.Solid ||
                    (candidate.SolidKind != SolidKind.Box &&
                     candidate.SolidKind != SolidKind.Wall))
                {
                    continue;
                }

                blocker = candidate;
                return true;
            }

            blocker = default;
            return false;
        }

        private static bool IsEnemyActiveGlideKinematicParticipant(WorldSnapshot snapshot, in EntityState entity)
        {
            if (!IsEnemyLogicParticipant(entity) ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, entity))
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

            return false;
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
            if (glideState.Phase == EnemyGlidePhase.Active &&
                (startedDuringActive || glideState.WantsRecover))
            {
                glideKinematicKind = EnemyGlideKinematicKind.Active;
                return true;
            }

            return false;
        }

        private static bool TryGetLockedGlideStep(
            in EnemyGlideRuntimeState glideState,
            out Vector2Int lockedStep)
        {
            lockedStep = new Vector2Int(glideState.LockedStepX, glideState.LockedStepY);
            return glideState.HasLockedStep &&
                   Math.Abs(lockedStep.x) + Math.Abs(lockedStep.y) == 1;
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
                _ => "KinematicUnitLocomotion",
            };
        }

        private static string ResolveEnemyKinematicContinuationBoundaryReason(EnemyGlideKinematicKind glideKinematicKind)
        {
            return glideKinematicKind switch
            {
                EnemyGlideKinematicKind.Active => "EnemyGlideActiveKinematicContinuation",
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

            if (outcome.Blocked)
            {
                // End the active budget without consuming a successful step. Keep phase
                // ownership in the resolver: zero cooldown lets it enter Recover next tick.
                chargeState.remainingActiveSteps = 0;
                return new[]
                {
                    new EnemyChargeWritePayload(entityId, chargeState, "StopBlockedChargeKinematicStep"),
                };
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

        private static bool TryResolveKinematicVelocity(
            Vector2Int delta,
            out SimulationVelocity2 velocity,
            out Direction facing)
        {
            if (delta == Vector2Int.right)
            {
                velocity = new SimulationVelocity2(SimulationFixed.FromRaw(SimulationFixed.DefaultReferenceUnitsPerTick), SimulationFixed.Zero);
                facing = Direction.Right;
                return true;
            }

            if (delta == Vector2Int.left)
            {
                velocity = new SimulationVelocity2(SimulationFixed.FromRaw(-SimulationFixed.DefaultReferenceUnitsPerTick), SimulationFixed.Zero);
                facing = Direction.Left;
                return true;
            }

            if (delta == Vector2Int.up)
            {
                velocity = new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(SimulationFixed.DefaultReferenceUnitsPerTick));
                facing = Direction.Up;
                return true;
            }

            if (delta == Vector2Int.down)
            {
                velocity = new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(-SimulationFixed.DefaultReferenceUnitsPerTick));
                facing = Direction.Down;
                return true;
            }

            velocity = SimulationVelocity2.Zero;
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

        private static bool TryResolveFacing(SimulationVelocity2 velocity, out Direction facing)
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
            SimulationOffset2 sourceLocalOffset,
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
                SimulationOffset2.Zero,
                SimulationVelocity2.Zero,
                UnitKinematicRuntimeState.SettledZero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: sweep.RejectedBy);
        }

        private static KinematicMotionOutcome CreateBlockedKinematicMotionOutcome(
            int entityId,
            UnitKinematicPose pose,
            KinematicSweepRejectionReason rejectedBy)
        {
            return new KinematicMotionOutcome(
                entityId,
                pose.AnchorCell,
                pose.LocalOffset,
                pose.AnchorCell,
                SimulationOffset2.Zero,
                SimulationVelocity2.Zero,
                UnitKinematicRuntimeState.SettledZero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: rejectedBy);
        }

        private static UnitKinematicRuntimeState CreateVoluntaryKinematicState(
            UnitKinematicRuntimeState sourceState,
            SimulationOffset2 localOffset,
            SimulationVelocity2 velocity)
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
                remainingTicks = (remainingDistanceUnits + SimulationFixed.DefaultReferenceUnitsPerTick - 1) /
                                 SimulationFixed.DefaultReferenceUnitsPerTick,
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
            SimulationOffset2 localOffset,
            SimulationVelocity2 velocity)
        {
            if (velocity.X.RawValue > 0)
            {
                return localOffset.X.RawValue < 0
                    ? -localOffset.X.RawValue
                    : SimulationFixed.UnitsPerCell - localOffset.X.RawValue;
            }

            if (velocity.X.RawValue < 0)
            {
                return localOffset.X.RawValue > 0
                    ? localOffset.X.RawValue
                    : SimulationFixed.UnitsPerCell + localOffset.X.RawValue;
            }

            if (velocity.Y.RawValue > 0)
            {
                return localOffset.Y.RawValue < 0
                    ? -localOffset.Y.RawValue
                    : SimulationFixed.UnitsPerCell - localOffset.Y.RawValue;
            }

            if (velocity.Y.RawValue < 0)
            {
                return localOffset.Y.RawValue > 0
                    ? localOffset.Y.RawValue
                    : SimulationFixed.UnitsPerCell + localOffset.Y.RawValue;
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
            string boundaryReason = "KinematicUnitLocomotion",
            IReadOnlyList<PendingEnemyBlockedReactionWritePayload> pendingEnemyBlockedReactionWrites = null)
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
                boundaryReason: boundaryReason,
                pendingEnemyBlockedReactionWrites: pendingEnemyBlockedReactionWrites);
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
                payload.BoundaryReason,
                payload.PendingEnemyBlockedReactionWrites);
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
            int tickIndex,
            IReadOnlyList<PendingCellImpact> duePendingCellImpacts = null)
        {
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, in input, entityLogics, rawAttackIntents);
            var rejectedReasons = new List<string>();
            var executableAttackIntents = FilterExecutionLockedAttackIntents(snapshot, tickIndex, rawAttackIntents, rejectedReasons);
            var sortedInputs = NormalizeAttackInputs(executableAttackIntents, impactReservations, drainedDelayedAttackEffects);
            var expandedAttackCandidates = new List<ActionGroup>();
            _attackExpander.Expand(snapshot, sortedInputs, expandedAttackCandidates, rejectedReasons);
            var pendingCellImpactResolutions = AppendPendingCellImpactAttackGroups(
                snapshot,
                duePendingCellImpacts,
                expandedAttackCandidates);
            expandedAttackCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedAttackCandidates);
            var actionPlanPayloads = BuildAttackActionPlanPayloads(snapshot, expandedAttackCandidates, tickIndex);
            var orderedActionPlanIds = BuildOrderedAttackActionPlanIds(snapshot, expandedAttackCandidates);
            return new AttackPlanBuildResult(
                rawAttackIntents,
                expandedAttackCandidates,
                actionPlanPayloads,
                orderedActionPlanIds,
                rejectedReasons,
                pendingCellImpactResolutions);
        }

        private AttackPlanBuildResult BuildAttackPlan(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<IAttackEntityLogic> entityLogics,
            FrozenMovementReservationExport movementReservationExport,
            List<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            int tickIndex,
            IReadOnlyList<PendingCellImpact> duePendingCellImpacts = null)
        {
            var rawAttackIntents = new List<RawAttackIntent>();
            _attackIntentCollector.Collect(snapshot, in input, entityLogics, rawAttackIntents);
            var rejectedReasons = new List<string>();
            var executableAttackIntents = FilterExecutionLockedAttackIntents(snapshot, tickIndex, rawAttackIntents, rejectedReasons);
            var sortedInputs = NormalizeAttackInputs(executableAttackIntents, movementReservationExport, drainedDelayedAttackEffects);
            var expandedAttackCandidates = new List<ActionGroup>();
            _attackExpander.Expand(snapshot, sortedInputs, expandedAttackCandidates, rejectedReasons);
            var pendingCellImpactResolutions = AppendPendingCellImpactAttackGroups(
                snapshot,
                duePendingCellImpacts,
                expandedAttackCandidates);
            expandedAttackCandidates.Sort(ActionGroupComparer.Instance);
            AssignAttackGroupIds(expandedAttackCandidates);
            var actionPlanPayloads = BuildAttackActionPlanPayloads(snapshot, expandedAttackCandidates, tickIndex);
            var orderedActionPlanIds = BuildOrderedAttackActionPlanIds(snapshot, expandedAttackCandidates);
            return new AttackPlanBuildResult(
                rawAttackIntents,
                expandedAttackCandidates,
                actionPlanPayloads,
                orderedActionPlanIds,
                rejectedReasons,
                pendingCellImpactResolutions);
        }

        private static List<PendingCellImpact> CollectDuePendingCellImpacts(WorldSnapshot snapshot, int tickIndex)
        {
            var entries = new List<PendingCellImpactSnapshotEntry>();
            snapshot.EnumeratePendingCellImpactsOrdered(entries);
            var due = new List<PendingCellImpact>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Impact.ImpactTick == tickIndex)
                {
                    due.Add(entries[i].Impact);
                }
            }

            return due;
        }

        private static List<PendingCellImpactResolutionRecord> AppendPendingCellImpactAttackGroups(
            WorldSnapshot snapshot,
            IReadOnlyList<PendingCellImpact> duePendingCellImpacts,
            List<ActionGroup> expandedAttackCandidates)
        {
            var resolutions = new List<PendingCellImpactResolutionRecord>();
            if (duePendingCellImpacts == null || duePendingCellImpacts.Count == 0)
            {
                return resolutions;
            }

            var players = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(players);
            for (var impactIndex = 0; impactIndex < duePendingCellImpacts.Count; impactIndex++)
            {
                var impact = duePendingCellImpacts[impactIndex];
                var invalidResult = ValidatePendingCellImpact(snapshot, impact);
                if (invalidResult != null)
                {
                    resolutions.Add(invalidResult.Value);
                    continue;
                }

                var hitTargetId = 0;
                for (var entityIndex = 0; entityIndex < players.Count; entityIndex++)
                {
                    var candidate = players[entityIndex];
                    if (!EntityRolePolicy.IsPlayerUnit(candidate) ||
                        candidate.hp <= 0 ||
                        candidate.markedForDeath ||
                        !IsPlayerCurrentCombatAnchorCell(snapshot, candidate, impact.TargetCell))
                    {
                        continue;
                    }

                    hitTargetId = candidate.entityId;
                    break;
                }

                resolutions.Add(
                    new PendingCellImpactResolutionRecord(
                        impact,
                        hitTargetId > 0
                            ? PendingCellImpactResolutionKind.Hit
                            : PendingCellImpactResolutionKind.Miss,
                        hitTargetId));
                if (hitTargetId <= 0)
                {
                    continue;
                }

                var actionGroup = new ActionGroup(
                    intentId: impact.ImpactId,
                    sourceId: impact.OwnerId,
                    priority: 0,
                    ActionGroupKind.Attack,
                    AttackSourceKind.ForwardCellImpact);
                actionGroup.Damages.Add(new DamageAction(hitTargetId, impact.Damage));
                actionGroup.Destroys.Add(new DestroyAction(hitTargetId));
                expandedAttackCandidates.Add(actionGroup);
            }

            return resolutions;
        }

        private static PendingCellImpactResolutionRecord? ValidatePendingCellImpact(
            WorldSnapshot snapshot,
            in PendingCellImpact impact)
        {
            if (!IsPendingCellImpactLaunchTopologyActive(snapshot, impact))
            {
                return new PendingCellImpactResolutionRecord(
                    impact,
                    PendingCellImpactResolutionKind.ExpiredTopologyInvalid);
            }

            if (!IsPendingCellImpactLaunchTopologyRevisionActive(snapshot, impact))
            {
                return new PendingCellImpactResolutionRecord(
                    impact,
                    PendingCellImpactResolutionKind.ExpiredTopologyInvalid);
            }

            if (IsPendingCellImpactTargetFaceInactive(snapshot, impact))
            {
                return new PendingCellImpactResolutionRecord(
                    impact,
                    PendingCellImpactResolutionKind.CancelledTargetInvalid);
            }

            return null;
        }

        private static bool IsPendingCellImpactLaunchTopologyActive(
            WorldSnapshot snapshot,
            in PendingCellImpact impact)
        {
            return impact.LaunchTopology.Equals(snapshot.Topology);
        }

        private static bool IsPendingCellImpactLaunchTopologyRevisionActive(
            WorldSnapshot snapshot,
            in PendingCellImpact impact)
        {
            return impact.LaunchTopologyRevision == snapshot.TopologyRevision;
        }

        private static bool IsPendingCellImpactTargetFaceInactive(
            WorldSnapshot snapshot,
            in PendingCellImpact impact)
        {
            return !snapshot.Topology.IsFaceActive(impact.TargetCell.face);
        }

        private static string BuildPendingCellImpactCommitEvent(in PendingCellImpactResolutionRecord resolution)
        {
            var impact = resolution.Impact;
            if (resolution.ResultKind == PendingCellImpactResolutionKind.CancelledSourceInvalid ||
                resolution.ResultKind == PendingCellImpactResolutionKind.CancelledTargetInvalid)
            {
                return
                    $"PendingCellImpactCancelled|Impact={impact.ImpactId}|Owner={impact.OwnerId}|Cell={FormatCell(impact.TargetCell)}|Reason={resolution.ResultKind}";
            }

            if (resolution.ResultKind == PendingCellImpactResolutionKind.ExpiredTopologyInvalid)
            {
                return
                    $"PendingCellImpactExpired|Impact={impact.ImpactId}|Owner={impact.OwnerId}|Cell={FormatCell(impact.TargetCell)}|Reason={resolution.ResultKind}";
            }

            return
                $"ForwardCellImpactResolved|Impact={impact.ImpactId}|Owner={impact.OwnerId}|Cell={FormatCell(impact.TargetCell)}|Hit={(resolution.Hit ? 1 : 0)}|Target={resolution.TargetEntityId}|Result={resolution.ResultKind}";
        }

        private static bool IsPlayerCurrentCombatAnchorCell(
            WorldSnapshot snapshot,
            in EntityState player,
            SurfaceCell targetCell)
        {
            return CombatWindupPoseQueries.TryResolveSimulationCombatOrigin(snapshot, player, out var playerOrigin) &&
                   playerOrigin.AnchorCell.Equals(targetCell);
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
                    sortedIntents,
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
                return MovementExecutionBoundaryKind.GenericExpansionOwned;
            }

            return MovementExecutionBoundaryKind.Unknown;
        }

        private static string ResolveMovementExecutionBoundaryReason(MovementExecutionBoundaryKind kind)
        {
            return kind switch
            {
                MovementExecutionBoundaryKind.TopologyMaterialization => "TopologyMaterialization",
                MovementExecutionBoundaryKind.BoxActionMovement => "BoxActionMovement",
                MovementExecutionBoundaryKind.GenericExpansionOwned => "GenericExpansionOwned",
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

            if (group.AttackSourceKind == AttackSourceKind.ForwardCellImpact)
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
                AttackSourceKind.ForwardCellImpact => 4,
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
                    return ResolvedActionSemanticKind.Impact;

                case ActionGroupKind.Item:
                    return ResolvedActionSemanticKind.Item;

                case ActionGroupKind.Move:
                    if (TryResolveMovementEntity(snapshot, group, out var entity))
                    {
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
            return ResolveReservationMode(snapshot, group) == ReservationMode.UnitSharedMove
                ? MovementBlockingType.NonBlocking
                : MovementBlockingType.Blocking;
        }

        private bool TryBuildImpactReservationPayload(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
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
                    snapshot.Topology,
                    snapshot.BoardBounds,
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

            var isFlipImpact = IsFlipImpactAction(sortedIntents, group);
            var participants = new BoxImpactParticipants(
                group.SourceId,
                impactSourceEntity.entityId,
                group.ImpactTargetIds);
            var travel = new ImpactTravelGeometry(
                impactSourceEntity.position,
                impactCell,
                geometry.FollowThroughCell,
                geometry.TravelDirection);
            var attack = new ImpactAttackHandoff(
                impactSourceEntity.entityId,
                ResolveImpactDamageAmount(group),
                sequence: 0);
            var disposition = new ImpactSourceDispositionPayload(
                isFlipImpact ? ImpactDispositionPolicyKind.Flip : ImpactDispositionPolicyKind.PushLike,
                hasImpactSourcePoseCommit: true,
                new ImpactSourcePoseCommit(impactSourceEntity.entityId, geometry.TravelDirection),
                hasStateChange: !isFlipImpact,
                state: EntityPhaseState.Sliding,
                stateTimer: !isFlipImpact ? _slidingStateTimerTicks : 0,
                semanticKind: isFlipImpact
                    ? ResolvedActionSemanticKind.Flip
                    : ResolveImpactContingentSemanticKind(impactSourceEntity, group.SourceId));
            impactReservationPayload = new MovementImpactReservationPayload(
                participants,
                travel,
                attack,
                disposition);
            rejectedReason = string.Empty;
            return true;
        }

        private static bool IsFlipImpactAction(IReadOnlyList<MoveIntent> sortedIntents, ActionGroup group)
        {
            return FindMovementIntent(sortedIntents, group.IntentId)?.CommandKind == Movement.MovementCommandKind.Flip;
        }

        private static string BuildImpactReservationRejectedReason(
            ActionGroup group,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            ImpactGeometryRejectReason rejectReason)
        {
            return
                $"ImpactReservationRejected|Stage=Plan|G={group.GroupId}|I={group.IntentId}|Source={group.ImpactSourceId}|Targets={FormatEntityIds(group.ImpactTargetIds)}|Reason={rejectReason}|SourceCell={FormatCell(sourceCell)}|ImpactCell={FormatCell(impactCell)}";
        }

        private static string FormatEntityIds(IReadOnlyList<int> entityIds)
        {
            return entityIds == null || entityIds.Count == 0
                ? string.Empty
                : string.Join(",", entityIds);
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
                group.ImpactTargetIds.Count > 0 &&
                snapshot.TryGetEntity(group.ImpactTargetIds[0], out var target))
            {
                return target.position;
            }

            return default;
        }

        private static int ResolveImpactDamageAmount(ActionGroup group)
        {
            return group.HasResolvedImpact ? 1 : 0;
        }

        private void ResolvePlanJumpLandings(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<IMovementEntityLogic> movementLogics,
            List<string> preMovementUpdates,
            List<JumpLandingPlan> jumpLandingPlans,
            List<Contest> jumpLandingSpaceContests,
            List<string> jumpLandingEvents,
            ref int nextContestId)
        {
            var jumpEntries = new List<EnemyJumpSnapshotEntry>();
            snapshot.EnumerateEnemyJumpStatesOrdered(jumpEntries);

            for (var i = 0; i < jumpEntries.Count; i++)
            {
                var jumpEntry = jumpEntries[i];
                var jumpState = jumpEntry.State;
                if (jumpState.phase != EnemyJumpPhase.Airborne ||
                    tickIndex < jumpState.landingTick ||
                    !snapshot.TryGetEntity(jumpEntry.EntityId, out var source) ||
                    source.position.face != snapshot.Topology.BottomFace ||
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
                        jumpState.lockedTargetCell),
                    _tileFeatureSettlementEvidence);
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

                if (!EnemyJumpQueries.TryResolveLandingCell(
                        snapshot,
                        source,
                        jumpState,
                        out var landingCell,
                        out var landingRule,
                        _tileFeatureDefinitions))
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

                var reservationInfo = reservationBook.GetCellReservationInfo(payload.DestinationCell);
                var reservationStatus = ResolveJumpLandingReservationStatus(payload, reservationInfo);
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
                        jumpLandingEvidence,
                        _tileFeatureSettlementEvidence);
                    landingLegality = crushEvaluation.LegalityResult;
                    resolvedCrushedBoxEntityId = crushEvaluation.CrushedBoxEntityId;
                }
                else
                {
                    landingLegality = RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                        settlementContext,
                        jumpLandingEvidence,
                        _tileFeatureSettlementEvidence);
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
                    reservationBook.ReserveJumpLanding(
                        payload.SourceActorEntityId,
                        payload.DestinationCell,
                        BlocksUnitSharedSettlementForJumpLanding(payload.LandingKind));
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

        private static ReservationStatus ResolveJumpLandingReservationStatus(
            JumpLandingActionPlanPayload payload,
            CellReservationInfo reservationInfo)
        {
            if (reservationInfo.Status != ReservationStatus.Conflicted)
            {
                return reservationInfo.Status;
            }

            return IsUnitSharedSettlementCompatibleJumpLandingKind(payload.LandingKind) &&
                   reservationInfo.IsUnitSharedSettlementCompatible
                ? ReservationStatus.None
                : ReservationStatus.Conflicted;
        }

        private static bool BlocksUnitSharedSettlementForJumpLanding(JumpLandingKind landingKind)
        {
            return !IsUnitSharedSettlementCompatibleJumpLandingKind(landingKind);
        }

        private static bool IsUnitSharedSettlementCompatibleJumpLandingKind(JumpLandingKind landingKind)
        {
            return landingKind == JumpLandingKind.ExactStack ||
                   landingKind == JumpLandingKind.Contested;
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
                    disposition == ImpactDispositionKind.DestroySelf ||
                    disposition == ImpactDispositionKind.BarricadeReassertCrush)
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

        private static bool HasAcceptedImpactDestroys(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int sourceEntityId,
            IReadOnlyList<int> targetEntityIds)
        {
            if (targetEntityIds == null ||
                targetEntityIds.Count == 0)
            {
                return false;
            }

            for (var targetIndex = 0; targetIndex < targetEntityIds.Count; targetIndex++)
            {
                if (!HasAcceptedImpactDestroy(destroyResolutions, sourceEntityId, targetEntityIds[targetIndex]))
                {
                    return false;
                }
            }

            return true;
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
                ResolvedActionSemanticKind.ForwardCellMove => MovementSemanticKind.ForwardCellMove,
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
                AttackSourceKind.ForwardCellImpact => DamageSourceType.Attack,
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

        internal static List<TileEffectEntityContact> BuildTileEffectEntityContacts(
            WorldSnapshot sourceSnapshot,
            WorldSnapshot destinationSnapshot,
            params FinalizationBatch[] batches)
        {
            var contacts = new List<TileEffectEntityContact>();
            if (sourceSnapshot == null || destinationSnapshot == null || batches == null)
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
                        !destinationSnapshot.TryGetEntity(operation.EntityId, out var entity) ||
                        entity.position != operation.Destination ||
                        entity.boardPresence != EntityBoardPresence.Occupying ||
                        entity.hp <= 0 ||
                        entity.markedForDeath)
                    {
                        continue;
                    }

                    var operationOrder = ((long)batchIndex << 32) | (uint)operationIndex;
                    if (entity.type == EntityType.Box &&
                        TryResolveTileEffectEntityBoxContactKind(operation.Metadata, out var boxKind))
                    {
                        var fromCell = sourceSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity)
                            ? sourceEntity.position
                            : operation.Destination;
                        contacts.Add(new TileEffectEntityContact(
                            operation.EntityId,
                            entity.type,
                            fromCell,
                            operation.Destination,
                            operation.Destination,
                            boxKind,
                            operation.Metadata.MovementSemanticKind,
                            operationOrder,
                            operation.Metadata.ActionPlanId,
                            operation.Metadata.LocalActionIndex,
                            operation.Metadata.IntentId,
                            ResolveTileEffectContactVisualContactTime(operation.Metadata)));
                        continue;
                    }

                    if (entity.type == EntityType.Unit &&
                        IsTileEffectUnitMoveEnterContact(
                            sourceSnapshot,
                            operation,
                            out var unitFromCell))
                    {
                        contacts.Add(new TileEffectEntityContact(
                            operation.EntityId,
                            entity.type,
                            unitFromCell,
                            operation.Destination,
                            operation.Destination,
                            TileEffectEntityContactKind.MoveEnter,
                            operation.Metadata.MovementSemanticKind,
                            operationOrder,
                            operation.Metadata.ActionPlanId,
                            operation.Metadata.LocalActionIndex,
                            operation.Metadata.IntentId,
                            ResolveTileEffectContactVisualContactTime(operation.Metadata)));
                    }
                }
            }

            contacts.Sort(CompareTileEffectEntityContacts);
            return contacts;
        }

        private static float ResolveTileEffectContactVisualContactTime(
            FinalizationOperationMetadata metadata)
        {
            return metadata.MovementSemanticKind == MovementSemanticKind.Flip
                ? GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime
                : 0f;
        }

        internal static List<TileFeatureActivationOccupantFact> BuildDestroyTileActivationOccupantFacts(
            WorldSnapshot previousSnapshot,
            WorldSnapshot currentSnapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            TileEffectTriggerSourceKind sourceKind,
            params FinalizationBatch[] sourceBatches)
        {
            var facts = new List<TileFeatureActivationOccupantFact>();
            if (previousSnapshot == null ||
                currentSnapshot == null ||
                tileFeatureDefinitions == null ||
                sourceKind != TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant ||
                previousSnapshot.Topology.Equals(currentSnapshot.Topology) ||
                !TryGetFirstTopologySourceOperationOrdinal(sourceBatches, out var sourceOperationOrdinal))
            {
                return facts;
            }

            var entities = new List<EntityState>();
            var tileFeaturesAtCell = new List<TileFeatureState>();
            currentSnapshot.EnumerateEntitiesOrdered(entities);
            for (var entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                var entity = entities[entityIndex];
                if (!IsValidDestroyTileActivationOccupant(currentSnapshot, entity))
                {
                    continue;
                }

                currentSnapshot.EnumerateTileFeaturesAt(entity.position, tileFeaturesAtCell);
                for (var tileIndex = 0; tileIndex < tileFeaturesAtCell.Count; tileIndex++)
                {
                    var tileFeature = tileFeaturesAtCell[tileIndex];
                    if (tileFeature.Kind != TileFeatureKind.Destroy ||
                        !TryFindTileFeatureDefinition(tileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                        !IsDestroyTileActivationTransition(
                            previousSnapshot,
                            currentSnapshot,
                            tileFeature,
                            definition))
                    {
                        continue;
                    }

                    facts.Add(new TileFeatureActivationOccupantFact(
                        tileFeature.TileId,
                        tileFeature.Cell,
                        tileFeature.Kind,
                        entity.entityId,
                        entity.type,
                        sourceKind,
                        sourceOperationOrdinal));
                }
            }

            facts.Sort(CompareTileFeatureActivationOccupantFacts);
            return facts;
        }

        private static bool TryGetFirstTopologySourceOperationOrdinal(
            FinalizationBatch[] sourceBatches,
            out int sourceOperationOrdinal)
        {
            sourceOperationOrdinal = default;
            if (sourceBatches == null)
            {
                return false;
            }

            for (var batchIndex = 0; batchIndex < sourceBatches.Length; batchIndex++)
            {
                var batch = sourceBatches[batchIndex];
                if (batch == null)
                {
                    continue;
                }

                var operations = batch.Operations;
                for (var operationIndex = 0; operationIndex < operations.Count; operationIndex++)
                {
                    if (operations[operationIndex].Kind == FinalizationOperationKind.SetTopology)
                    {
                        sourceOperationOrdinal = (batchIndex * 100000) + operationIndex;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsValidDestroyTileActivationOccupant(
            WorldSnapshot snapshot,
            EntityState entity)
        {
            if (entity.boardPresence != EntityBoardPresence.Occupying ||
                entity.hp <= 0 ||
                entity.markedForDeath)
            {
                return false;
            }

            return entity.type == EntityType.Box ||
                   entity.type == EntityType.Unit &&
                   TileFeatureHazardQueries.IsDestroyTileLethalForUnit(entity);
        }

        private static bool IsDestroyTileActivationTransition(
            WorldSnapshot previousSnapshot,
            WorldSnapshot currentSnapshot,
            TileFeatureState currentTileFeature,
            TileFeatureRuntimeDefinition definition)
        {
            if (!previousSnapshot.TryGetTileFeature(currentTileFeature.TileId, out var previousTileFeature) ||
                previousTileFeature.Kind != TileFeatureKind.Destroy ||
                previousTileFeature.Cell != currentTileFeature.Cell)
            {
                return false;
            }

            return !TileFeatureActivationQueries.IsActive(previousTileFeature, definition, previousSnapshot.Topology) &&
                   TileFeatureActivationQueries.IsActive(currentTileFeature, definition, currentSnapshot.Topology);
        }

        private static bool TryFindTileFeatureDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].TileId == tileId)
                {
                    definition = definitions[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private static int CompareTileFeatureActivationOccupantFacts(
            TileFeatureActivationOccupantFact left,
            TileFeatureActivationOccupantFact right)
        {
            var cellCompare = CompareSurfaceCells(left.FeatureCell, right.FeatureCell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var kindCompare = left.FeatureKind.CompareTo(right.FeatureKind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            var tileCompare = left.FeatureTileId.CompareTo(right.FeatureTileId);
            if (tileCompare != 0)
            {
                return tileCompare;
            }

            var occupantCompare = left.OccupantEntityId.CompareTo(right.OccupantEntityId);
            if (occupantCompare != 0)
            {
                return occupantCompare;
            }

            var sourceCompare = left.SourceKind.CompareTo(right.SourceKind);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            if (!left.SourceOperationOrdinal.HasValue && !right.SourceOperationOrdinal.HasValue)
            {
                return 0;
            }

            if (!left.SourceOperationOrdinal.HasValue)
            {
                return -1;
            }

            if (!right.SourceOperationOrdinal.HasValue)
            {
                return 1;
            }

            return left.SourceOperationOrdinal.Value.CompareTo(right.SourceOperationOrdinal.Value);
        }

        internal static List<TileEffectBoxStop> BuildTileEffectBoxStops(
            WorldSnapshot beforeMovementOrPreStopSnapshot,
            WorldSnapshot finalSnapshot,
            FinalizationBatch movementStageBatch)
        {
            var stops = new List<TileEffectBoxStop>();
            if (beforeMovementOrPreStopSnapshot == null ||
                finalSnapshot == null ||
                movementStageBatch == null)
            {
                return stops;
            }

            var seen = new HashSet<int>();
            var operations = movementStageBatch.Operations;
            for (var operationIndex = 0; operationIndex < operations.Count; operationIndex++)
            {
                var operation = operations[operationIndex];
                if (operation.Kind == FinalizationOperationKind.MoveEntity)
                {
                    if (!TryResolveTileEffectBoxStopMovementFamily(operation.Metadata, out var family) ||
                        !TryGetValidTileEffectStoppedBox(
                            finalSnapshot,
                            operation.EntityId,
                            operation.Destination,
                            out _))
                    {
                        continue;
                    }

                    if (seen.Add(operation.EntityId))
                    {
                        stops.Add(new TileEffectBoxStop(
                            operation.EntityId,
                            operation.Destination,
                            family,
                            CreateTileEffectBoxStopCause(operation, family, operation.Destination)));
                    }

                    continue;
                }

                if (operation.Kind != FinalizationOperationKind.ApplyStateChange ||
                    operation.PhaseState != EntityPhaseState.Idle ||
                    operation.StateTimer != 0 ||
                    !IsTileEffectBoxStopStateChange(operation.Metadata) ||
                    !beforeMovementOrPreStopSnapshot.TryGetEntity(operation.EntityId, out var before) ||
                    before.type != EntityType.Box ||
                    before.state != EntityPhaseState.Sliding ||
                    !TryGetValidTileEffectStoppedBox(
                        finalSnapshot,
                        operation.EntityId,
                        before.position,
                        out _))
                {
                    continue;
                }

                if (seen.Add(operation.EntityId))
                {
                    stops.Add(new TileEffectBoxStop(
                        operation.EntityId,
                        before.position,
                        TileEffectBoxMovementFamily.Slide,
                        CreateTileEffectBoxStopCause(
                            operation,
                            TileEffectBoxMovementFamily.Slide,
                            before.position)));
                }
            }

            stops.Sort(CompareTileEffectBoxStops);
            return stops;
        }

        private static TileEffectBoxStopCause CreateTileEffectBoxStopCause(
            FinalizationOperation operation,
            TileEffectBoxMovementFamily family,
            SurfaceCell cell)
        {
            var visualContactNormalizedTime = operation.Metadata.MovementSemanticKind == MovementSemanticKind.Flip
                ? GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime
                : 0f;
            return TileEffectBoxStopCause.Create(
                operation.EntityId,
                cell,
                family,
                operation.Metadata.MovementSemanticKind,
                operation.Metadata.ActionPlanId,
                operation.Metadata.LocalActionIndex,
                operation.Metadata.IntentId,
                visualContactNormalizedTime);
        }

        private static bool TryResolveTileEffectBoxStopMovementFamily(
            FinalizationOperationMetadata metadata,
            out TileEffectBoxMovementFamily family)
        {
            if (metadata.LocalActionIndex == 1 &&
                metadata.MovementSemanticKind != MovementSemanticKind.Flip)
            {
                family = default;
                return false;
            }

            switch (metadata.MovementSemanticKind)
            {
                case MovementSemanticKind.Push:
                    family = TileEffectBoxMovementFamily.Push;
                    return true;
                case MovementSemanticKind.Slide:
                    family = TileEffectBoxMovementFamily.Slide;
                    return true;
                case MovementSemanticKind.Flip:
                    family = TileEffectBoxMovementFamily.Flip;
                    return true;
                default:
                    family = default;
                    return false;
            }
        }

        private static bool IsTileEffectBoxStopStateChange(FinalizationOperationMetadata metadata)
        {
            return metadata.MovementSemanticKind == MovementSemanticKind.Stop ||
                   metadata.SemanticKind == ResolvedActionSemanticKind.Stop;
        }

        private static bool TryGetValidTileEffectStoppedBox(
            WorldSnapshot snapshot,
            int boxEntityId,
            SurfaceCell expectedCell,
            out EntityState box)
        {
            if (snapshot.TryGetEntity(boxEntityId, out var entity) &&
                entity.type == EntityType.Box &&
                entity.position == expectedCell &&
                entity.state == EntityPhaseState.Idle &&
                entity.boardPresence == EntityBoardPresence.Occupying &&
                entity.hp > 0 &&
                !entity.markedForDeath &&
                snapshot.TryGetSolidSemanticAt(expectedCell, out var semantic) &&
                semantic.Kind == SolidKind.Box &&
                semantic.Entity.entityId == boxEntityId)
            {
                box = entity;
                return true;
            }

            box = default;
            return false;
        }

        private static int CompareTileEffectBoxStops(TileEffectBoxStop left, TileEffectBoxStop right)
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

            return left.MovementFamily.CompareTo(right.MovementFamily);
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

        private static bool TryResolveTileEffectEntityBoxContactKind(
            FinalizationOperationMetadata metadata,
            out TileEffectEntityContactKind kind)
        {
            if (TryResolveTileEffectBoxContactKind(metadata, out var boxKind))
            {
                kind = TileEffectEntityContact.ToEntityContactKind(boxKind);
                return true;
            }

            kind = default;
            return false;
        }

        private static bool TileEffectResultTargetsUnit(
            TileEffectResolutionResult result,
            WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            var operations = result.EntityOperations.Operations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MarkDestroy &&
                    snapshot.TryGetEntity(operation.EntityId, out var entity) &&
                    entity.type == EntityType.Unit)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTileEffectUnitMoveEnterContact(
            WorldSnapshot sourceSnapshot,
            FinalizationOperation operation,
            out SurfaceCell fromCell)
        {
            fromCell = default;
            if (!IsTileEffectUnitMoveEnterMovement(operation.Metadata) ||
                !sourceSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity) ||
                sourceEntity.type != EntityType.Unit ||
                sourceEntity.position == operation.Destination)
            {
                return false;
            }

            fromCell = sourceEntity.position;
            return true;
        }

        private static bool IsTileEffectUnitMoveEnterMovement(FinalizationOperationMetadata metadata)
        {
            switch (metadata.MovementSemanticKind)
            {
                case MovementSemanticKind.Move:
                    return metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.UnitOrdinaryLocomotion ||
                           metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned ||
                           metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LocomotionAnchorCommit;
                case MovementSemanticKind.JumpLanding:
                    return metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.UnitSpecialLocomotion;
                default:
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

        private static int CompareTileEffectEntityContacts(TileEffectEntityContact left, TileEffectEntityContact right)
        {
            var cellCompare = CompareSurfaceCells(left.TileCell, right.TileCell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var entityCompare = left.EntityId.CompareTo(right.EntityId);
            if (entityCompare != 0)
            {
                return entityCompare;
            }

            var kindCompare = left.ContactKind.CompareTo(right.ContactKind);
            return kindCompare != 0 ? kindCompare : left.OperationOrder.CompareTo(right.OperationOrder);
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
            var impactDispositionByActionPlanId = new Dictionary<int, ImpactDispositionResolutionRecord>(impactDispositionRecords.Count);
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

                for (var impactIndex = 0; impactIndex < impactReservations.Count; impactIndex++)
                {
                    var impactReservation = impactReservations[impactIndex];
                    if (impactReservation.SourceActionPlanId != actionPlanId)
                    {
                        continue;
                    }

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
                var hasBarricadeReassertCrushDisposition = hasImpactDisposition &&
                                                           payload.HasImpactReservationPayload &&
                                                           impactDisposition.DispositionKind == ImpactDispositionKind.BarricadeReassertCrush;

                if (!hasImpactFollowThrough &&
                    !hasBarricadeReassertCrushDisposition)
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
                        semanticKindOverride: payload.ImpactReservationPayload.Disposition.SemanticKind);
                    // Keep the local vacate ahead of MoveEntity so the replay sees a
                    // free destination cell without changing global cleanup semantics.
                    for (var targetIndex = 0; targetIndex < payload.ImpactReservationPayload.Participants.TargetEntityIds.Count; targetIndex++)
                    {
                        var targetEntityId = payload.ImpactReservationPayload.Participants.TargetEntityIds[targetIndex];
                        batch.SetBoardPresence(
                            targetEntityId,
                            EntityBoardPresence.Detached,
                            contingentMetadata);
                        commitEvents.Add(
                            $"BoardPresenceCommitted|G={actionPlanId}|I={payload.IntentId}|E={targetEntityId}|Presence={EntityBoardPresence.Detached}");
                    }

                    batch.MoveEntity(
                        payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                        payload.ImpactReservationPayload.Travel.FollowThroughCell,
                        contingentMetadata);
                    if (payload.ImpactReservationPayload.Disposition.HasImpactSourcePoseCommit)
                    {
                        var impactSourcePose = payload.ImpactReservationPayload.Disposition.ImpactSourcePose;
                        batch.SetFacing(
                            impactSourcePose.ImpactSourceEntityId,
                            impactSourcePose.Facing,
                            contingentMetadata);
                    }

                    commitEvents.Add(
                        $"MoveCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.Participants.ImpactSourceEntityId}|To={FormatCell(payload.ImpactReservationPayload.Travel.FollowThroughCell)}|Facing={payload.ImpactReservationPayload.Travel.TravelDirection}");
                    if (payload.ImpactReservationPayload.Disposition.HasStateChange)
                    {
                        batch.ApplyStateChange(
                            payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                            payload.ImpactReservationPayload.Disposition.State,
                            payload.ImpactReservationPayload.Disposition.StateTimer,
                            contingentMetadata);
                        commitEvents.Add(
                            $"StateChanged|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.Participants.ImpactSourceEntityId}|State={payload.ImpactReservationPayload.Disposition.State}|Timer={payload.ImpactReservationPayload.Disposition.StateTimer}");
                    }
                }
                else if (hasBarricadeReassertCrushDisposition)
                {
                    var crushCell = impactDisposition.BarricadeCell.Equals(default(SurfaceCell))
                        ? payload.ImpactReservationPayload.Travel.FollowThroughCell
                        : impactDisposition.BarricadeCell;
                    BarricadeCrushOperationPolicy.AddBoxRemoval(
                        batch,
                        actionPlanId,
                        payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                        crushCell);
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.Participants.ImpactSourceEntityId}|Presence={EntityBoardPresence.Detached}");
                    commitEvents.Add(
                        $"DestroyMarked|G={actionPlanId}|I={payload.IntentId}|Target={payload.ImpactReservationPayload.Participants.ImpactSourceEntityId}|Reason=BarricadeReassertCrush|Tile={impactDisposition.BarricadeTileId}");
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
                        // This materializes kinematic anchor commits as authoritative movement.
                        // Boundary reasons such as GlideActiveKinematicAnchorCommit are not
                        // presentation-only signals while they flow through this branch.
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

                for (var blockedReactionIndex = 0; blockedReactionIndex < payload.PendingEnemyBlockedReactionWrites.Count; blockedReactionIndex++)
                {
                    var reactionWrite = payload.PendingEnemyBlockedReactionWrites[blockedReactionIndex];
                    batch.SetPendingEnemyBlockedReaction(
                        reactionWrite.EntityId,
                        reactionWrite.Reaction,
                        CreateMovementMetadata(payload, baseResolution, blockedReactionIndex));
                    commitEvents.Add(
                        $"PendingEnemyBlockedReactionSet|G={actionPlanId}|I={payload.IntentId}|E={reactionWrite.EntityId}|Source={FormatCell(reactionWrite.Reaction.SourceCell)}|BlockedTarget={FormatCell(reactionWrite.Reaction.BlockedTargetCell)}|Direction={reactionWrite.Reaction.BlockedDirection}|BlockerKind={reactionWrite.Reaction.BlockerKind}|SolidKind={reactionWrite.Reaction.BlockerSolidKind}|BlockerEntityType={reactionWrite.Reaction.BlockerEntityType}|BlockerEntityId={reactionWrite.Reaction.BlockerEntityId.GetValueOrDefault(0)}|Created={reactionWrite.Reaction.CreatedTick}|Expire={reactionWrite.Reaction.ExpireTick}");
                }

                if (hasDestroySelfDisposition)
                {
                    var destroySelfMetadata = CreateMovementMetadata(
                        payload,
                        baseResolution,
                        localActionIndex: 1,
                        semanticKindOverride: payload.ImpactReservationPayload.Disposition.SemanticKind,
                        exitCauseHint: TickEntityExitCause.DestroyedByImpact);
                    batch.SetBoardPresence(
                        payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                        EntityBoardPresence.Detached,
                        destroySelfMetadata);
                    batch.MarkDestroy(
                        payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                        destroySelfMetadata);
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={actionPlanId}|I={payload.IntentId}|E={payload.ImpactReservationPayload.Participants.ImpactSourceEntityId}|Presence={EntityBoardPresence.Detached}");
                    commitEvents.Add(
                        $"DestroyMarked|G={actionPlanId}|I={payload.IntentId}|Target={payload.ImpactReservationPayload.Participants.ImpactSourceEntityId}|Reason=ImpactDestroySelf");
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

            if (payload.BoundaryReason == "EnemyGlideBoundaryKinematicClosed")
            {
                return payload.BoundaryReason;
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
                    var hasDamageContestResolution = TryFindResolutionRecord(
                        resolutionRecords,
                        ContestKind.Damage,
                        actionPlanId,
                        damageIndex,
                        out var damageContestResolution);
                    if (!hasDamageContestResolution ||
                        !damageContestResolution.Accepted)
                    {
                        if (hasDamageContestResolution &&
                            damageResolution.ConsumesReceiverCooldown &&
                            damageResolution.HasPlayerDamageState)
                        {
                            batch.SetPlayerDamageState(
                                damageResolution.TargetId,
                                damageResolution.PlayerDamageState,
                                CreateAttackMetadata(payload, damageContestResolution, damageIndex, attackSourceKind: damageWrite.AttackSourceKind));
                        }

                        var sourceKindSuffix = BuildSourceKindSuffix(damageWrite.AttackSourceKind);
                        commitEvents.Add(
                            $"DamageRejected|G={actionPlanId}|I={payload.IntentId}|Source={payload.SourceActorEntityId}{sourceKindSuffix}|Target={damageWrite.TargetEntityId}|Amount={damageWrite.Amount}|Reason={damageResolution.RejectReason}");
                        continue;
                    }

                    if (damageResolution.HasPlayerDamageState)
                    {
                        batch.SetPlayerDamageState(
                            damageResolution.TargetId,
                            damageResolution.PlayerDamageState,
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

        private static List<MotionInterruptRecord> MaterializeUnitMotionInterrupts(
            WorldSnapshot attackSnapshot,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            FinalizationBatch attackStageBatch,
            List<string> commitEvents,
            int tickIndex,
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

                var enemyInterruptHandled = TryMaterializeEnemyKinematicMotionInterrupt(
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
                    interruptEnemyGlideKinematics,
                    damageResolution.Amount);
                if (!enemyInterruptHandled)
                {
                    TryMaterializePlayerContinuousLocomotionInterrupt(
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
            }

            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                var destroyResolution = destroyResolutions[i];
                if (!destroyResolution.Accepted)
                {
                    continue;
                }

                var enemyInterruptHandled = TryMaterializeEnemyKinematicMotionInterrupt(
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
                    interruptEnemyGlideKinematics,
                    damageAmount: 0);
                if (!enemyInterruptHandled)
                {
                    TryMaterializePlayerContinuousLocomotionInterrupt(
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
            }

            return interruptRecords;
        }

        private static bool TryMaterializeEnemyKinematicMotionInterrupt(
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
            bool interruptEnemyGlideKinematics,
            int damageAmount)
        {
            if (interruptedEntityIds.Contains(targetEntityId))
            {
                return false;
            }

            if (attackSnapshot.TryGetUnitKinematicPose(targetEntityId, out var pose) &&
                pose.HasAuthoritativeState &&
                !pose.IsSettledAtAnchor &&
                (pose.Mode == MotionMode.Voluntary || pose.Mode == MotionMode.Held))
            {
                var enemyGlideInterrupt = interruptEnemyGlideKinematics &&
                                          pose.Mode == MotionMode.Voluntary &&
                                          attackSnapshot.TryGetEntity(targetEntityId, out var targetEntity) &&
                                          IsEnemyGlideKinematicContinuation(attackSnapshot, targetEntity, pose);
                if (!enemyGlideInterrupt)
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

                interruptRecords.Add(
                    new MotionInterruptRecord(
                        targetEntityId,
                        MotionInterruptPolicy.FreezeCurrentPose,
                        sourceEntityId));
                commitEvents.Add(
                    $"KinematicMotionInterrupted|E={targetEntityId}|Source={sourceEntityId}|SourceKind={sourceKind}|Anchor={FormatCell(pose.AnchorCell)}|Offset={pose.LocalOffset}|Mode={interruptedState.mode}");
                return true;
            }

            return false;
        }

        private static bool TryMaterializePlayerContinuousLocomotionInterrupt(
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
            if (interruptedEntityIds.Contains(targetEntityId))
            {
                return false;
            }

            var hasPlayerControl = attackSnapshot.TryGetPlayerControlState(targetEntityId, out var playerControlState);
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

            var entityOperationCount = projectionBatch.Operations.Count;
            var tileFeatureOperationCount = projectionBatch.TileFeatureOperations.Count;
            if (entityOperationCount == 0 &&
                tileFeatureOperationCount == 0)
            {
                SnapshotMaterializationDiagnostics.RecordCompositeDamageProjection(
                    entityOperationCount,
                    tileFeatureOperationCount,
                    delayedAttackEffectCount: 0,
                    damageFactCount: 0,
                    returnedBaseSnapshot: true);
                return baseSnapshot;
            }

            SnapshotMaterializationDiagnostics.RecordCompositeDamageProjection(
                entityOperationCount,
                tileFeatureOperationCount,
                delayedAttackEffectCount: 0,
                damageFactCount: 0,
                returnedBaseSnapshot: false);
            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyBatch(projectionBatch, ProjectedWorldBatchReason.DamageProjection);
            return projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.DamageProjection);
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
                else if (payload.MovementCandidateKind == MovementCandidateKind.BoxImpact)
                {
                    accepted = true;
                    selectedIntentIds.Add(payload.IntentId);
                }
                else
                {
                    if (reservationBook.TryAcceptPayload(snapshot, payload, out var conflict))
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

                for (var targetIndex = 0; targetIndex < payload.ImpactReservationPayload.Participants.TargetEntityIds.Count; targetIndex++)
                {
                    impactReservations.Add(
                        new ImpactReservation(
                            payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                            payload.ImpactReservationPayload.Participants.TargetEntityIds[targetIndex],
                            payload.ImpactReservationPayload.Travel.ImpactCell,
                            payload.ImpactReservationPayload.Attack.DamageAmount,
                            tickIndex,
                            payload.ActionPlanId,
                            reservationSequence++));
                }
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
                    !payload.HasDeferredImpactPayload)
                {
                    continue;
                }

                var targets = new List<EntityState>();
                if (!TryResolveImpactReservationAgainstSnapshot(snapshot, payload, targets, out var impactCell, out var damageAmount))
                {
                    continue;
                }

                for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    impactReservations.Add(
                        new ImpactReservation(
                            payload.DeferredImpactPayload.SourceEntityId,
                            targets[targetIndex].entityId,
                            impactCell,
                            damageAmount,
                            tickIndex,
                            payload.ActionPlanId,
                            reservationSequence++));
                }
            }

            return impactReservations;
        }

        private static bool TryResolveImpactReservationAgainstSnapshot(
            WorldSnapshot snapshot,
            MovementActionPlanPayload payload,
            List<EntityState> targets,
            out SurfaceCell impactCell,
            out int damageAmount)
        {
            impactCell = default;
            damageAmount = 0;
            targets.Clear();

            var sourceEntityId = 0;
            if (payload.HasImpactReservationPayload)
            {
                sourceEntityId = payload.ImpactReservationPayload.Participants.ImpactSourceEntityId;
                impactCell = payload.ImpactReservationPayload.Travel.ImpactCell;
                damageAmount = payload.ImpactReservationPayload.Attack.DamageAmount;
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

            snapshot.EnumerateUnitImpactTargetsAt(impactCell, targets);
            return targets.Count > 0;
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
                        payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                        payload.ImpactReservationPayload.Travel.FollowThroughCell,
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
            int tickIndex,
            DemoGameplayOverrideSnapshot demoGameplayOverrideSnapshot)
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

                    if (demoGameplayOverrideSnapshot.PlayerInvincible)
                    {
                        var cooldownState = PlayerDamageQueries.ConsumeReceiverCooldown(
                            damageState,
                            tickIndex,
                            _playerDamageCooldownTicks);
                        playerDamageStatesByEntityId[damageWrite.TargetEntityId] = cooldownState;
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                payload.ActionPlanId,
                                payload.IntentId,
                                payload.SourceActorEntityId,
                                damageWrite.AttackSourceKind,
                                damageWrite.TargetEntityId,
                                damageWrite.Amount,
                                accepted: false,
                                DamageRejectReason.PlayerInvincible,
                                localActionIndex: damageIndex,
                                hasPlayerDamageState: true,
                                playerDamageState: cooldownState,
                                consumesReceiverCooldown: true));
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
            List<ImpactDispositionResolutionRecord> impactDispositionRecords,
            List<BarricadeBlockFact> barricadeBlockFacts)
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
                var reassertBarricadeTileId = 0;
                var reassertBarricadeCell = default(SurfaceCell);
                if (payloads.TryGetValue(contest.ActionPlanId, out var payload) &&
                    payload.HasImpactReservationPayload)
                {
                    targetDestroyed = HasAcceptedImpactDestroys(
                        destroyResolutions,
                        payload.ImpactReservationPayload.Attack.AttackSourceEntityId,
                        payload.ImpactReservationPayload.Participants.TargetEntityIds);

                    switch (payload.ImpactReservationPayload.Disposition.PolicyKind)
                    {
                        case ImpactDispositionPolicyKind.PushLike:
                            if (targetDestroyed)
                            {
                                followThroughLegalityChecked = true;
                                var reservationStatus = reservationBook.GetImpactPayloadStatus(payload.ImpactReservationPayload);
                                var impactEvaluation = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThroughDetailed(
                                    new SettlementContext(
                                        attackSnapshot,
                                        BuildLegalityActorRef(attackSnapshot, payload.ImpactReservationPayload.Participants.ImpactSourceEntityId, EntityType.Box),
                                        payload.ImpactReservationPayload.Travel.FollowThroughCell,
                                        attackSnapshot.Topology,
                                        SpatialState.Anchored,
                                        reservationStatus),
                                    new ImpactFollowThroughEvidence(
                                        payload.ImpactReservationPayload.Attack.AttackSourceEntityId,
                                        payload.ImpactReservationPayload.Participants.TargetEntityIds,
                                        destroyResolutions),
                                    _tileFeatureSettlementEvidence);
                                var impactLegality = impactEvaluation.LegalityResult;
                                followThroughAccepted = impactLegality.Verdict == LegalityVerdict.Allowed;
                                if (followThroughAccepted)
                                {
                                    accepted = true;
                                    dispositionKind = ImpactDispositionKind.FollowThrough;
                                    reservationBook.ReserveImpactPayload(payload.ImpactReservationPayload, contest.ActionPlanId);
                                }
                                else if (impactEvaluation.OutcomeKind ==
                                         ImpactFollowThroughSettlementOutcomeKind.BarricadeReassertCrush)
                                {
                                    dispositionKind = ImpactDispositionKind.BarricadeReassertCrush;
                                    reassertBarricadeTileId = impactEvaluation.Barricade.TileId;
                                    reassertBarricadeCell = impactEvaluation.Barricade.Cell;
                                }
                                else
                                {
                                    TryAppendImpactFollowThroughBarricadeBlockFact(
                                        impactLegality,
                                        payload.ImpactReservationPayload,
                                        barricadeBlockFacts);
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
                            var flipImpactEvaluation = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThroughDetailed(
                                new SettlementContext(
                                    attackSnapshot,
                                    BuildLegalityActorRef(attackSnapshot, payload.ImpactReservationPayload.Participants.ImpactSourceEntityId, EntityType.Box),
                                    payload.ImpactReservationPayload.Travel.FollowThroughCell,
                                    attackSnapshot.Topology,
                                    SpatialState.Anchored,
                                    flipReservationStatus),
                                new ImpactFollowThroughEvidence(
                                    payload.ImpactReservationPayload.Attack.AttackSourceEntityId,
                                    payload.ImpactReservationPayload.Participants.TargetEntityIds,
                                    destroyResolutions,
                                    ignoreActiveGlideOccupants: true),
                                _tileFeatureSettlementEvidence);
                            var flipImpactLegality = flipImpactEvaluation.LegalityResult;
                            followThroughAccepted = flipImpactLegality.Verdict == LegalityVerdict.Allowed;
                            if (followThroughAccepted)
                            {
                                accepted = true;
                                dispositionKind = ImpactDispositionKind.FollowThrough;
                                reservationBook.ReserveImpactPayload(payload.ImpactReservationPayload, contest.ActionPlanId);
                            }
                            else if (flipImpactEvaluation.OutcomeKind ==
                                     ImpactFollowThroughSettlementOutcomeKind.BarricadeReassertCrush)
                            {
                                dispositionKind = ImpactDispositionKind.BarricadeReassertCrush;
                                reassertBarricadeTileId = flipImpactEvaluation.Barricade.TileId;
                                reassertBarricadeCell = flipImpactEvaluation.Barricade.Cell;
                            }
                            else
                            {
                                TryAppendImpactFollowThroughBarricadeBlockFact(
                                    flipImpactLegality,
                                    payload.ImpactReservationPayload,
                                    barricadeBlockFacts);
                            }

                            break;
                    }

                    impactDispositionRecords.Add(
                        new ImpactDispositionResolutionRecord(
                            contest.ActionPlanId,
                            payload.ImpactReservationPayload.Participants.ImpactSourceEntityId,
                            payload.ImpactReservationPayload.Participants.TargetEntityIds,
                            payload.ImpactReservationPayload.Travel.ImpactCell,
                            payload.ImpactReservationPayload.Disposition.PolicyKind,
                            dispositionKind,
                            targetDestroyed,
                            followThroughLegalityChecked,
                            followThroughAccepted,
                            reassertBarricadeTileId,
                            reassertBarricadeCell));
                }

                resolutionRecords.Add(CreateResolutionRecord(contest, accepted));
            }
        }

        private static void TryAppendImpactFollowThroughBarricadeBlockFact(
            LegalityResult legality,
            MovementImpactReservationPayload payload,
            List<BarricadeBlockFact> barricadeBlockFacts)
        {
            if (legality.Verdict != LegalityVerdict.Blocked ||
                barricadeBlockFacts == null)
            {
                return;
            }

            for (var blockerIndex = 0; blockerIndex < legality.Blockers.Count; blockerIndex++)
            {
                var blocker = legality.Blockers[blockerIndex];
                if (blocker.Kind != LegalityBlockerKind.TileFeature ||
                    blocker.TileFeatureKind != TileFeatureKind.Barricade ||
                    blocker.TileId <= 0)
                {
                    continue;
                }

                barricadeBlockFacts.Add(
                    new BarricadeBlockFact(
                        blocker.TileId,
                        legality.Cell,
                        payload.Participants.ImpactSourceEntityId,
                        payload.Travel.TravelDirection));
                return;
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

        private static string BuildSourceKindSuffix(AttackSourceKind sourceKind)
        {
            return sourceKind == AttackSourceKind.PassiveContact ||
                   sourceKind == AttackSourceKind.ForwardCellImpact
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
                .Append("|Retry=").Append(state.retryCount)
                .Append("|TopologySuspendLast=").Append(state.topologySuspendLastTick);

            if (!string.IsNullOrEmpty(extra))
            {
                builder.Append('|').Append(extra);
            }

            return builder.ToString();
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
            return projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.DamageProjection);
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

        private static List<BoxSlideStopResult> FilterSelectedBoxSlideStops(
            IReadOnlyList<BoxSlideStopResult> candidates,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new List<BoxSlideStopResult>();
            }

            var selectedIntentIds = new HashSet<int>();
            for (var i = 0; i < resolutionRecords.Count; i++)
            {
                var record = resolutionRecords[i];
                if (!record.Accepted ||
                    !payloads.TryGetValue(record.ActionPlanId, out var payload) ||
                    payload.MovementCandidateKind != MovementCandidateKind.Stop)
                {
                    continue;
                }

                selectedIntentIds.Add(payload.IntentId);
            }

            var filtered = new List<BoxSlideStopResult>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (selectedIntentIds.Contains(candidate.IntentId))
                {
                    filtered.Add(candidate);
                }
            }

            return filtered;
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
