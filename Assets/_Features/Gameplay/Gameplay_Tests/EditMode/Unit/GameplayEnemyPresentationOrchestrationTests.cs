using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayEnemyPresentationOrchestrationTests
    {
        private const int JumpWindupEnemyId = 40;
        private const int JumpAirborneEnemyId = 41;
        private const int JumpLandEnemyId = 42;
        private const int ChargeWindupEnemyId = 43;
        private const int ChargeActiveEnemyId = 44;
        private const int ChargeRecoverEnemyId = 45;
        private const int DeathEnemyId = 46;
        private const int TickIndex = 31;
        private const string EnemyAnimatorControllerPath = "Assets/3DM/2BlackEye/BlackEye.controller";
        private static readonly CubeTopologyState Topology = new(FaceId.Floor);
        private static readonly SurfaceCell SourceCell = new(FaceId.Floor, 0, 0);
        private static readonly SurfaceCell TargetCell = new(FaceId.Floor, 1, 0);

        [Test]
        [Category("Core")]
        public void EnemyPresentation_DefaultLegacyMapperMode_DoesNotCallExecutorPortAndKeepsLegacyOwner()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_DefaultLegacyMapperMode_DoesNotCallExecutorPortAndKeepsLegacyOwner));
            var port = new RecordingEnemyPresentationPlaybackPort();

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper,
                    port,
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true));

                coordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.Mode, Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutedByLegacyCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.ObservedCueCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_InvalidMode_NormalizesToLegacyMapper()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_InvalidMode_NormalizesToLegacyMapper));
            var port = new RecordingEnemyPresentationPlaybackPort();

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    (EnemyPresentationExecutionMode)999,
                    port,
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true));

                coordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutedByLegacyCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_OrchestrationExecutorMode_RoutesAllJumpChargeDeathRequestsThroughHostPort()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_OrchestrationExecutorMode_RoutesAllJumpChargeDeathRequestsThroughHostPort));
            var port = new RecordingEnemyPresentationPlaybackPort();

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    port,
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true));

                coordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(port.TryPlayCallCount, Is.EqualTo(7));
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyJumpWindup, JumpWindupEnemyId, PresentationEnemyPresentationKind.Jump, PresentationEnemyPresentationPhase.Windup, PresentationEnemyPresentationOutcome.Started, 101);
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyJumpAirborne, JumpAirborneEnemyId, PresentationEnemyPresentationKind.Jump, PresentationEnemyPresentationPhase.Airborne, PresentationEnemyPresentationOutcome.ActiveStarted, 102);
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyJumpLand, JumpLandEnemyId, PresentationEnemyPresentationKind.Jump, PresentationEnemyPresentationPhase.Land, PresentationEnemyPresentationOutcome.Landed, 103);
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyChargeWindup, ChargeWindupEnemyId, PresentationEnemyPresentationKind.Charge, PresentationEnemyPresentationPhase.Windup, PresentationEnemyPresentationOutcome.Started, 201);
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyChargeActive, ChargeActiveEnemyId, PresentationEnemyPresentationKind.Charge, PresentationEnemyPresentationPhase.Active, PresentationEnemyPresentationOutcome.ActiveStarted, 202);
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyChargeRecover, ChargeRecoverEnemyId, PresentationEnemyPresentationKind.Charge, PresentationEnemyPresentationPhase.Recover, PresentationEnemyPresentationOutcome.Started, 203);
                AssertEnemyRequest(port.Requests, PresentationAnimationCueKey.EnemyDeath, DeathEnemyId, PresentationEnemyPresentationKind.Death, PresentationEnemyPresentationPhase.Death, PresentationEnemyPresentationOutcome.Death, 301);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                AssertBlockingSnapshotCleared(coordinator.EnemyPresentationExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_LegacyMapperAndOrchestrationRequests_AreSemanticallyEquivalent()
        {
            var result = CreateEnemyPresentationTickResult();
            var legacyStates = BuildLegacyEnemyPresentationStates(result)
                .SelectMany(ToSemanticRecords)
                .OrderBy(record => record.EnemyEntityId)
                .ThenBy(record => record.Kind)
                .ThenBy(record => record.Phase)
                .ToArray();
            var port = new RecordingEnemyPresentationPlaybackPort();
            var executor = new GameplayEnemyPresentationExecutor(
                port,
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));

            executor.Play(CreatePlaybackPlan(result));

            var orchestrationRecords = port.Requests
                .Select(ToSemanticRecord)
                .OrderBy(record => record.EnemyEntityId)
                .ThenBy(record => record.Kind)
                .ThenBy(record => record.Phase)
                .ToArray();

            Assert.That(orchestrationRecords, Is.EqualTo(legacyStates));
            Assert.That(port.Requests.All(request => request.OwnershipKey.CueKey == request.CueKey), Is.True);
            Assert.That(port.Requests.All(request => request.OwnershipKey.SourceSequenceId == request.EnemyPayload.SourceSequenceId), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_ControlledSyncPort_MapsTypedCuesToCurrentLegacyDriverCommands()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_ControlledSyncPort_MapsTypedCuesToCurrentLegacyDriverCommands));

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    playbackPort: null,
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true, addAnimator: true));

                coordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.CommandRequestedCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.EnemyJumpCueMappedToLegacyCommandCount, Is.EqualTo(3));
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.EnemyChargeCueMappedToLegacyCommandCount, Is.EqualTo(3));
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.EnemyDeathCueMappedToLegacyCommandCount, Is.EqualTo(1));
                Assert.That(GetEnemyDriver(rootObject, JumpWindupEnemyId).JumpWindupSignalCount, Is.EqualTo(1));
                Assert.That(GetEnemyDriver(rootObject, JumpAirborneEnemyId).JumpAirborneSignalCount, Is.EqualTo(1));
                Assert.That(GetEnemyDriver(rootObject, JumpLandEnemyId).LastCrossFadedStateName, Is.EqualTo("Move"));
                Assert.That(GetEnemyDriver(rootObject, ChargeWindupEnemyId).WindupSignalCount, Is.EqualTo(1));
                Assert.That(GetEnemyDriver(rootObject, ChargeActiveEnemyId).ChargeActiveSignalCount, Is.EqualTo(1));
                Assert.That(GetEnemyDriver(rootObject, ChargeRecoverEnemyId).RecoverySignalCount, Is.EqualTo(1));
                Assert.That(GetEnemyDriver(rootObject, DeathEnemyId).DeathSignalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_DuplicateGuard_BlocksForcedSecondExecutorAttemptButNormalModesHaveNoDuplicates()
        {
            var normalRoot = new GameObject(nameof(EnemyPresentation_DuplicateGuard_BlocksForcedSecondExecutorAttemptButNormalModesHaveNoDuplicates) + "_Normal");
            var duplicateRoot = new GameObject(nameof(EnemyPresentation_DuplicateGuard_BlocksForcedSecondExecutorAttemptButNormalModesHaveNoDuplicates) + "_Duplicate");

            try
            {
                var normalPort = new RecordingEnemyPresentationPlaybackPort();
                var normalCoordinator = CreateInitializedCoordinator(
                    normalRoot,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    normalPort,
                    new EnemyPresentationViewFactory(normalRoot.transform, addDriver: true));
                normalCoordinator.Present(CreateEnemyPresentationTickResult());

                var duplicatePort = new RecordingEnemyPresentationPlaybackPort();
                var duplicateCoordinator = CreateInitializedCoordinator(
                    duplicateRoot,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    duplicatePort,
                    new EnemyPresentationViewFactory(duplicateRoot.transform, addDriver: true),
                    duplicateExecutors: true);
                duplicateCoordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(normalCoordinator.EnemyPresentationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(normalPort.TryPlayCallCount, Is.EqualTo(7));
                Assert.That(duplicatePort.TryPlayCallCount, Is.EqualTo(7));
                Assert.That(duplicateCoordinator.EnemyPresentationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(7));
                Assert.That(duplicateCoordinator.EnemyPresentationOwnershipDiagnostics.DuplicateAttemptCount, Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normalRoot);
                UnityEngine.Object.DestroyImmediate(duplicateRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_ControlledIntegration_DistinguishesMissingDiagnosticsWithoutExceptions()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_ControlledIntegration_DistinguishesMissingDiagnosticsWithoutExceptions));

            try
            {
                var malformedCue = CreateMutatedCue(PresentationAnimationCueKey.EnemyJumpWindup, PresentationTarget.None(), PresentationAnchor.ForEntityVisualRoot(JumpWindupEnemyId));
                var anchorMissingCue = CreateMutatedCue(PresentationAnimationCueKey.EnemyJumpAirborne, PresentationTarget.Entity(JumpAirborneEnemyId), PresentationAnchor.None());
                var targetAnchorCoordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    new RecordingEnemyPresentationPlaybackPort(),
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true),
                    overrideCues: new[] { malformedCue, anchorMissingCue });

                targetAnchorCoordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(targetAnchorCoordinator.EnemyPresentationExecutorDiagnostics.TargetMissingCount, Is.EqualTo(1));
                Assert.That(targetAnchorCoordinator.EnemyPresentationExecutorDiagnostics.AnchorMissingCount, Is.EqualTo(1));
                Assert.That(targetAnchorCoordinator.EnemyPresentationExecutorDiagnostics.CommandRequestedCount, Is.Zero);

                AssertExecutorPortDiagnostic(GameplayEnemyPresentationPlaybackResultKind.BindingMissing, diagnostics => diagnostics.BindingMissingCount);
                AssertExecutorPortDiagnostic(GameplayEnemyPresentationPlaybackResultKind.MapperMissing, diagnostics => diagnostics.MapperMissingCount);
                AssertExecutorPortDiagnostic(GameplayEnemyPresentationPlaybackResultKind.DriverMissing, diagnostics => diagnostics.DriverMissingCount);
                AssertExecutorPortDiagnostic(GameplayEnemyPresentationPlaybackResultKind.AnimatorMissing, diagnostics => diagnostics.AnimatorMissingCount);
                AssertMissingPortDiagnostic();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_LifecycleCleanup_ClearsGuardDiagnosticsPortAndStaleDriverState()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_LifecycleCleanup_ClearsGuardDiagnosticsPortAndStaleDriverState));
            var port = new RecordingEnemyPresentationPlaybackPort();

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    port,
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true, addAnimator: true));

                coordinator.Present(CreateEnemyPresentationTickResult());
                Assert.That(port.TryPlayCallCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutorAttemptCount, Is.EqualTo(7));

                coordinator.PresentInitial(CreateInitialEntities(), Topology);
                Assert.That(port.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.CommandRequestedCount, Is.Zero);
                Assert.That(GetEnemyDriver(rootObject, DeathEnemyId).LastPresentationState.DidDie, Is.False);

                coordinator.Present(CreateEnemyPresentationTickResult());
                coordinator.HardCleanupPresentationExtensions();
                Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_OrchestrationRoute_DoesNotMutateAuthoritativeResultOrBlockingState()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_OrchestrationRoute_DoesNotMutateAuthoritativeResultOrBlockingState));
            var result = CreateEnemyPresentationTickResult();
            var determinismHash = result.DeterminismHash;
            var finalEntities = result.FinalEntities.ToArray();
            var eventLog = result.EventLog.ToArray();
            var objectiveResult = result.ObjectiveResult;

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    new RecordingEnemyPresentationPlaybackPort(),
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true));

                coordinator.Present(result);

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.EnemyPresentationExecutionPipelineBlockingSnapshot);
                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
                Assert.That(result.FinalEntities.Single(entity => entity.entityId == JumpWindupEnemyId).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(result.FinalEntities.Single(entity => entity.entityId == JumpAirborneEnemyId).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(result.FinalEntities.Single(entity => entity.entityId == ChargeActiveEnemyId).aiMode, Is.EqualTo(EnemyAiMode.Charge));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentation_OrchestrationMode_DoesNotChangeEnemyAudioPlanningOrOwnershipVocabulary()
        {
            var rootObject = new GameObject(nameof(EnemyPresentation_OrchestrationMode_DoesNotChangeEnemyAudioPlanningOrOwnershipVocabulary));
            var result = CreateEnemyPresentationTickResult();
            var planner = new EnemyAudioRequestPlanner();
            var audioRequestsBefore = planner.BuildRequests(result)
                .Select(request => (request.OwnerEntityId, request.Cue))
                .ToArray();

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    new RecordingEnemyPresentationPlaybackPort(),
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true));

                coordinator.Present(result);

                var audioRequestsAfter = planner.BuildRequests(result)
                    .Select(request => (request.OwnerEntityId, request.Cue))
                    .ToArray();
                Assert.That(audioRequestsAfter, Is.EqualTo(audioRequestsBefore));
                Assert.That(audioRequestsAfter, Does.Contain((JumpLandEnemyId, EnemyAudioCue.Landing)));
                Assert.That(audioRequestsAfter, Does.Contain((DeathEnemyId, EnemyAudioCue.Death)));
                Assert.That(typeof(EnemyAudioPresentationController).AssemblyQualifiedName, Does.Not.Contain("GameplayEnemyPresentationExecutor"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayTickPresentationCoordinator CreateInitializedCoordinator(
            GameObject rootObject,
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort,
            IGameplayEntityViewFactory viewFactory,
            bool duplicateExecutors = false,
            IReadOnlyList<PresentationCue> overrideCues = null,
            bool forceNullExecutorPort = false)
        {
            var registry = rootObject.GetComponent<GameplayEntityViewRegistry>() ??
                           rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, viewFactory);
            var coordinator = new GameplayTickPresentationCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline,
                (pipelineMode, port, guard) => CreateEnemyPresentationExecutionPipeline(
                    pipelineMode,
                    forceNullExecutorPort ? null : port,
                    guard,
                    duplicateExecutors,
                    overrideCues));

            coordinator.ConfigureEnemyPresentationExecution(mode, playbackPort);
            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                Topology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(CreateInitialEntities(), Topology);
            return coordinator;
        }

        private static GameplayPresentationPipeline CreateEnemyPresentationExecutionPipeline(
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort,
            EnemyPresentationExecutionGuard guard,
            bool duplicateExecutors,
            IReadOnlyList<PresentationCue> overrideCues)
        {
            if (mode != EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor)
            {
                return null;
            }

            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new GameplayEnemyPresentationExecutor(playbackPort, mode, guard),
                    new GameplayEnemyPresentationExecutor(playbackPort, mode, guard),
                }
                : new IPresentationExecutor[]
                {
                    new GameplayEnemyPresentationExecutor(playbackPort, mode, guard),
                };
            var cuePlanner = overrideCues == null
                ? (IPresentationCuePlanner)new EnemyPresentationCuePlanner()
                : new StaticEnemyPresentationCuePlanner(overrideCues);

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new[] { cuePlanner }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static TickResult CreateEnemyPresentationTickResult()
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                new[]
                {
                    new TickEnemyJumpPresentationSignal(
                        JumpWindupEnemyId,
                        sequence: 101,
                        EnemyJumpPhase.Windup,
                        startedWindupThisTick: true,
                        startedAirborneThisTick: false,
                        landedThisTick: false,
                        retryThisTick: false,
                        SourceCell,
                        TargetCell,
                        TargetCell,
                        Direction.Right),
                    new TickEnemyJumpPresentationSignal(
                        JumpAirborneEnemyId,
                        sequence: 102,
                        EnemyJumpPhase.Airborne,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: true,
                        landedThisTick: false,
                        retryThisTick: false,
                        SourceCell,
                        TargetCell,
                        TargetCell,
                        Direction.Right),
                    new TickEnemyJumpPresentationSignal(
                        JumpLandEnemyId,
                        sequence: 103,
                        EnemyJumpPhase.Cooldown,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: false,
                        landedThisTick: true,
                        retryThisTick: false,
                        SourceCell,
                        TargetCell,
                        TargetCell,
                        Direction.Right),
                },
                new[]
                {
                    new TickEnemyChargePresentationSignal(
                        ChargeWindupEnemyId,
                        sequence: 201,
                        EnemyChargePhase.Windup,
                        startedWindupThisTick: true,
                        startedActiveThisTick: false,
                        startedRecoverThisTick: false,
                        Direction.Down),
                    new TickEnemyChargePresentationSignal(
                        ChargeActiveEnemyId,
                        sequence: 202,
                        EnemyChargePhase.Active,
                        startedWindupThisTick: false,
                        startedActiveThisTick: true,
                        startedRecoverThisTick: false,
                        Direction.Down),
                    new TickEnemyChargePresentationSignal(
                        ChargeRecoverEnemyId,
                        sequence: 203,
                        EnemyChargePhase.Recover,
                        startedWindupThisTick: false,
                        startedActiveThisTick: false,
                        startedRecoverThisTick: true,
                        Direction.Down),
                },
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        DeathEnemyId,
                        TickEntityExitCause.EnemyDeath,
                        SourceCell,
                        Topology,
                        Direction.Left,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        anchorEntityId: null,
                        presentationSeed: 301,
                        timing: EntityExitPresentationTiming.Immediate,
                        hasPresentationTargetCell: true,
                        presentationTargetCell: SourceCell),
                },
                Array.Empty<FlipImpactPresentationSignal>());

            return new TickResult(
                TickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                CreateInitialEntities(),
                new[] { "AuthoritativeEnemyPresentationEvent" },
                Topology,
                presentationData,
                "ENEMY-PRESENTATION-ORCHESTRATION-HASH",
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static EntityState[] CreateInitialEntities()
        {
            return new[]
            {
                CreateEnemy(JumpWindupEnemyId, EnemyAiMode.Chase),
                CreateEnemy(JumpAirborneEnemyId, EnemyAiMode.Chase),
                CreateEnemy(JumpLandEnemyId, EnemyAiMode.Chase),
                CreateEnemy(ChargeWindupEnemyId, EnemyAiMode.Charge),
                CreateEnemy(ChargeActiveEnemyId, EnemyAiMode.Charge),
                CreateEnemy(ChargeRecoverEnemyId, EnemyAiMode.Charge),
                CreateEnemy(DeathEnemyId, EnemyAiMode.Chase),
            };
        }

        private static EntityState CreateEnemy(int entityId, EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                teamId = 2,
                position = SourceCell,
                facing = Direction.Right,
                hp = 3,
                maxHp = 3,
                aiMode = aiMode,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 20,
                initialMoveDelaySeconds: 0.1f,
                repeatedMoveIntervalSeconds: 0.1f,
                boxSlideStepIntervalSeconds: 0.1f,
                projectileStepIntervalSeconds: 0.1f,
                moveMotionDurationSeconds: 0.1f,
                pushMotionDurationSeconds: 0.1f,
                topologyMotionDurationSeconds: 0.1f,
                flipMotionDurationSeconds: 0.1f,
                flipArcHeightInCells: 1f,
                maxTicksPerFrame: 4,
                itemConsumeEffectDurationSeconds: 0.1f,
                boxDestroyEffectDurationSeconds: 0.1f,
                enemyDeathEffectDurationSeconds: 0.1f);
        }

        private static PresentationPlaybackPlan CreatePlaybackPlan(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyPresentationCuePlanner(),
            }).Plan(factFrame);
            return new PresentationPlaybackPlanner().Plan(cueFrame);
        }

        private static EnemyViewPresentationState[] BuildLegacyEnemyPresentationStates(TickResult result)
        {
            var mapper = new EnemyViewPresentationMapper();
            var buffer = new Dictionary<int, EnemyViewPresentationState>();
            mapper.Build(result, new Dictionary<int, GameplayEntityView>(), buffer);
            return buffer.Values.ToArray();
        }

        private static IEnumerable<EnemyPresentationSemanticRecord> ToSemanticRecords(EnemyViewPresentationState state)
        {
            if (state.StartedJumpWindupThisTick)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Jump,
                    PresentationEnemyPresentationPhase.Windup,
                    PresentationEnemyPresentationOutcome.Started);
            }

            if (state.StartedJumpAirborneThisTick)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Jump,
                    PresentationEnemyPresentationPhase.Airborne,
                    PresentationEnemyPresentationOutcome.ActiveStarted);
            }

            if (state.LandedFromJumpThisTick)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Jump,
                    PresentationEnemyPresentationPhase.Land,
                    PresentationEnemyPresentationOutcome.Landed);
            }

            if (state.StartedChargeWindupThisTick)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Charge,
                    PresentationEnemyPresentationPhase.Windup,
                    PresentationEnemyPresentationOutcome.Started);
            }

            if (state.StartedChargeActiveThisTick)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Charge,
                    PresentationEnemyPresentationPhase.Active,
                    PresentationEnemyPresentationOutcome.ActiveStarted);
            }

            if (state.StartedChargeRecoverThisTick)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Charge,
                    PresentationEnemyPresentationPhase.Recover,
                    PresentationEnemyPresentationOutcome.Started);
            }

            if (state.DidDie)
            {
                yield return new EnemyPresentationSemanticRecord(
                    state.EntityId,
                    PresentationEnemyPresentationKind.Death,
                    PresentationEnemyPresentationPhase.Death,
                    PresentationEnemyPresentationOutcome.Death);
            }
        }

        private static EnemyPresentationSemanticRecord ToSemanticRecord(
            GameplayEnemyPresentationPlaybackRequest request)
        {
            return new EnemyPresentationSemanticRecord(
                request.EnemyEntityId,
                request.EnemyPayload.Kind,
                request.EnemyPayload.Phase,
                request.EnemyPayload.Outcome);
        }

        private static PresentationCue CreateMutatedCue(
            PresentationAnimationCueKey cueKey,
            PresentationTarget target,
            PresentationAnchor anchor)
        {
            var sourceCue = CreateCueFrame(CreateEnemyPresentationTickResult())
                .Cues.Single(cue => cue.Key.TryGetAnimationCueKey(out var key) && key == cueKey);
            return new PresentationCue(
                sourceCue.Domain,
                sourceCue.Key,
                sourceCue.Source,
                target,
                anchor,
                sourceCue.PolicyHint,
                sourceCue.TopologyPayload,
                sourceCue.MotionPayload,
                sourceCue.AnimationPayload,
                sourceCue.EnemyPayload);
        }

        private static PresentationCueFrame CreateCueFrame(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyPresentationCuePlanner(),
            }).Plan(factFrame);
        }

        private static void AssertEnemyRequest(
            IReadOnlyList<GameplayEnemyPresentationPlaybackRequest> requests,
            PresentationAnimationCueKey cueKey,
            int enemyEntityId,
            PresentationEnemyPresentationKind kind,
            PresentationEnemyPresentationPhase phase,
            PresentationEnemyPresentationOutcome outcome,
            int sequenceId)
        {
            var request = requests.Single(candidate => candidate.CueKey == cueKey);
            Assert.That(request.TickIndex, Is.EqualTo(TickIndex));
            Assert.That(request.EnemyEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(enemyEntityId)));
            Assert.That(request.Anchor, Is.EqualTo(PresentationAnchor.ForEntityVisualRoot(enemyEntityId)));
            Assert.That(request.EnemyPayload.Kind, Is.EqualTo(kind));
            Assert.That(request.EnemyPayload.Phase, Is.EqualTo(phase));
            Assert.That(request.EnemyPayload.Outcome, Is.EqualTo(outcome));
            Assert.That(request.EnemyPayload.SourceTickIndex, Is.EqualTo(TickIndex));
            Assert.That(request.EnemyPayload.SourceSequenceId, Is.EqualTo(sequenceId));
            Assert.That(request.OwnershipKey.EnemyEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(cueKey));
            Assert.That(request.OwnershipKey.SourceSequenceId, Is.EqualTo(sequenceId));
        }

        private static void AssertExecutorPortDiagnostic(
            GameplayEnemyPresentationPlaybackResultKind resultKind,
            Func<GameplayEnemyPresentationExecutorDiagnostics, int> selector)
        {
            var executor = new GameplayEnemyPresentationExecutor(
                new RecordingEnemyPresentationPlaybackPort(resultKind),
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));

            executor.Play(CreatePlaybackPlan(CreateEnemyPresentationTickResult()));

            Assert.That(selector(executor.Diagnostics), Is.EqualTo(7));
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(7));
        }

        private static void AssertMissingPortDiagnostic()
        {
            var rootObject = new GameObject(nameof(AssertMissingPortDiagnostic));

            try
            {
                var coordinator = CreateInitializedCoordinator(
                    rootObject,
                    EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                    playbackPort: null,
                    new EnemyPresentationViewFactory(rootObject.transform, addDriver: true),
                    forceNullExecutorPort: true);

                coordinator.Present(CreateEnemyPresentationTickResult());

                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.MissingPortCount, Is.EqualTo(7));
                Assert.That(coordinator.EnemyPresentationExecutorDiagnostics.CommandRequestedCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static EnemyAnimatorDriver GetEnemyDriver(GameObject rootObject, int entityId)
        {
            var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.TryGetView(entityId, out var view), Is.True);
            Assert.That(view.TryGetComponent<EnemyAnimatorDriver>(out var driver), Is.True);
            return driver;
        }

        private static void AssertBlockingSnapshotCleared(PresentationBlockingSnapshot snapshot)
        {
            Assert.That(snapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(snapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(snapshot.PlannedBlockingBarrierCount, Is.Zero);
            Assert.That(snapshot.ActiveBlockingSourceCount, Is.Zero);
        }

        private readonly struct EnemyPresentationSemanticRecord
        {
            public EnemyPresentationSemanticRecord(
                int enemyEntityId,
                PresentationEnemyPresentationKind kind,
                PresentationEnemyPresentationPhase phase,
                PresentationEnemyPresentationOutcome outcome)
            {
                EnemyEntityId = enemyEntityId;
                Kind = kind;
                Phase = phase;
                Outcome = outcome;
            }

            public int EnemyEntityId { get; }

            public PresentationEnemyPresentationKind Kind { get; }

            public PresentationEnemyPresentationPhase Phase { get; }

            public PresentationEnemyPresentationOutcome Outcome { get; }
        }

        private sealed class StaticEnemyPresentationCuePlanner : IPresentationCuePlanner
        {
            private readonly IReadOnlyList<PresentationCue> _cues;

            public StaticEnemyPresentationCuePlanner(IReadOnlyList<PresentationCue> cues)
            {
                _cues = cues ?? Array.Empty<PresentationCue>();
            }

            public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
            {
                for (var i = 0; i < _cues.Count; i++)
                {
                    builder.Add(_cues[i]);
                }
            }
        }

        private sealed class RecordingEnemyPresentationPlaybackPort : IGameplayEnemyPresentationPlaybackPort
        {
            private readonly GameplayEnemyPresentationPlaybackResultKind _resultKind;

            public RecordingEnemyPresentationPlaybackPort(
                GameplayEnemyPresentationPlaybackResultKind resultKind = GameplayEnemyPresentationPlaybackResultKind.Applied)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public List<GameplayEnemyPresentationPlaybackRequest> Requests { get; } = new();

            public bool TryPlayEnemyPresentation(
                in GameplayEnemyPresentationPlaybackRequest request,
                out GameplayEnemyPresentationPlaybackResult result)
            {
                TryPlayCallCount++;
                Requests.Add(request);
                result = new GameplayEnemyPresentationPlaybackResult(_resultKind);
                return _resultKind == GameplayEnemyPresentationPlaybackResultKind.Applied ||
                       _resultKind == GameplayEnemyPresentationPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                Requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                Requests.Clear();
            }
        }

        private sealed class EnemyPresentationViewFactory : IGameplayEntityViewFactory
        {
            private readonly bool _addAnimator;
            private readonly bool _addDriver;
            private readonly Transform _parent;

            public EnemyPresentationViewFactory(Transform parent, bool addDriver, bool addAnimator = false)
            {
                _parent = parent;
                _addDriver = addDriver;
                _addAnimator = addAnimator;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                if (!_addDriver)
                {
                    return view;
                }

                var driver = viewObject.AddComponent<EnemyAnimatorDriver>();
                if (_addAnimator)
                {
                    var animator = viewObject.AddComponent<Animator>();
                    animator.runtimeAnimatorController =
                        AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyAnimatorControllerPath);
                    Assert.That(animator.runtimeAnimatorController, Is.Not.Null, EnemyAnimatorControllerPath);
                    PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                }

                return view;
            }
        }
    }
}
