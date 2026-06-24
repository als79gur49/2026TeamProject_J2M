using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.AudioPolicy;
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
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyAudioRuntimeTests
    {
        private const string EnemyPrefabRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs";
        private const string BlackEyeAudioProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioProfiles/EnemyAudioProfile_BlackEye.asset";
        private const string BlackEyeActiveDefinitionPath =
            "Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/BlackEye_Act_Def.asset";
        private const string BlackEyePlasmaDefinitionPath =
            "Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/BlackEye_Plasma_Def.asset";
        private const string BlackEyePlasmaClipPath =
            "Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_BlackEye_plazma.wav";
        private const string DrSaturnAudioProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioProfiles/EnemyAudioProfile_DrSaturn.asset";
        private const string DrSaturnAudioProfileGuid = "42ebae281b3c4fffa43d495c3a98dd73";
        private const string StartisAudioProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioProfiles/EnemyAudioProfile_Startis.asset";
        private const string StartisPassiveContactDefinitionPath =
            "Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/Starteeth_Move_Def.asset";
        private const string EnemyPresentationCatalogPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Catalogs/EnemyPresentationCatalog_CampaignMain.asset";
        private const string EnemyAudioRequirementBindingRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioRequirementBindings";

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_MapsEnemyPresentationSignalsToDocumentedCues()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, targetCell),
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    enemyActionSignals: new[]
                    {
                        new TickEnemyActionPresentationSignal(
                            20,
                            EnemyActionKind.Melee,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            canceledThisTick: false,
                            executedThisTick: true,
                            startedRecoveryThisTick: false),
                    },
                    enemyJumpSignals: new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            23,
                            sequence: 1,
                            EnemyJumpPhase.Cooldown,
                            startedWindupThisTick: false,
                            startedAirborneThisTick: false,
                            landedThisTick: true,
                            retryThisTick: false),
                    },
                    enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            21,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 3),
                        new TickEnemyUtilityPresentationSignal(
                            21,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.RecoverStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 3),
                    },
                    summonWindupWarnings: new[]
                    {
                        new TickSummonWindupWarningSignal(
                            sourceEntityId: 22,
                            effectIndex: 0,
                            sourceCell,
                            topology,
                            Direction.Right,
                            windupStartTick: 1,
                            windupEndTick: 2,
                            activationSequence: 1,
                            tickIndex: 1,
                            presentationSeed: 0),
                    },
                    entityExitSignals: new[]
                    {
                        new TickEntityExitPresentationSignal(
                            24,
                            TickEntityExitCause.EnemyDeath,
                            sourceCell,
                            topology,
                            Direction.Left,
                            EntityType.Unit),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player),
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(21, UnitRole.Enemy),
                    CreateUnit(22, UnitRole.Enemy),
                    CreateUnit(23, UnitRole.Enemy),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[]
                {
                    (20, EnemyAudioCue.Move),
                    (20, EnemyAudioCue.Windup),
                    (20, EnemyAudioCue.Active),
                    (21, EnemyAudioCue.Windup),
                    (21, EnemyAudioCue.Recover),
                    (22, EnemyAudioCue.Windup),
                    (23, EnemyAudioCue.Landing),
                    (24, EnemyAudioCue.Death),
                }));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlanning_LegacyRequestPlannerParity_UsesCurrentOneShotVocabulary()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    new TickEnemyActionPresentationSignal(
                        60,
                        EnemyActionKind.Melee,
                        activeActionSequence: 11,
                        startedThisTick: true,
                        canceledThisTick: false,
                        executedThisTick: true,
                        startedRecoveryThisTick: true,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.Executed),
                    new TickEnemyActionPresentationSignal(
                        61,
                        EnemyActionKind.Melee,
                        activeActionSequence: 12,
                        startedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        startedRecoveryThisTick: false,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.Executed),
                },
                enemyJumpSignals: new[]
                {
                    new TickEnemyJumpPresentationSignal(
                        62,
                        sequence: 13,
                        EnemyJumpPhase.Cooldown,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: false,
                        landedThisTick: true,
                        retryThisTick: false,
                        sourceCell: sourceCell,
                        lockedTargetCell: targetCell,
                        presentationTargetCell: targetCell,
                        facing: Direction.Right,
                        windupTicks: 0,
                        landingTick: 7,
                        remainingAirborneTicks: 0,
                        retryCount: 0,
                        TickEnemyJumpPresentationOutcome.Landed),
                },
                enemyChargeSignals: new[]
                {
                    new TickEnemyChargePresentationSignal(
                        63,
                        sequence: 14,
                        EnemyChargePhase.Active,
                        startedWindupThisTick: false,
                        startedActiveThisTick: true,
                        startedRecoverThisTick: false,
                        lockedDirection: Direction.Up),
                },
                forwardCellProjectileArrivalSignals: new[]
                {
                    new TickForwardCellProjectileArrivalPresentationSignal(
                        impactId: 91,
                        presentationKey: 910,
                        ownerId: 64,
                        sourceEnemyId: 64,
                        targetCell: targetCell,
                        direction: Direction.Up,
                        impactTick: 7,
                        PendingCellImpactResolutionKind.Hit,
                        targetEntityId: 10),
                },
                entityExitSignals: new[]
                {
                    new TickEntityExitPresentationSignal(
                        65,
                        TickEntityExitCause.Killed,
                        sourceCell,
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Left,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: 915),
                }),
                tickIndex: 7);
            var legacyRequests = new EnemyAudioRequestPlanner().BuildRequests(result);
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyAudioCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var bridgePort = new RecordingEnemyAudioPlaybackPort();
            var executor = new GameplayEnemyAudioPresentationExecutor(
                bridgePort,
                EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                new EnemyAudioExecutionGuard(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));

            executor.Play(playbackPlan);

            Assert.That(cueFrame.Cues, Has.Count.EqualTo(legacyRequests.Count));
            Assert.That(bridgePort.Requests, Has.Count.EqualTo(legacyRequests.Count));
            for (var i = 0; i < legacyRequests.Count; i++)
            {
                Assert.That(cueFrame.Cues[i].Domain, Is.EqualTo(PresentationDomain.EnemyAudio));
                Assert.That(cueFrame.Cues[i].Key.TryGetEnemyAudioCueKey(out var cueKey), Is.True);
                Assert.That((int)cueKey, Is.EqualTo((int)legacyRequests[i].Cue));
                Assert.That(cueFrame.Cues[i].EnemyAudioPayload.OwnerEntityId, Is.EqualTo(legacyRequests[i].OwnerEntityId));
                Assert.That(cueFrame.Cues[i].Source.TickIndex, Is.EqualTo(result.TickIndex));
                Assert.That(cueFrame.Cues[i].Target, Is.EqualTo(PresentationTarget.Entity(legacyRequests[i].OwnerEntityId)));
                Assert.That(cueFrame.Cues[i].Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
                Assert.That(cueFrame.Cues[i].PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.OneShot));
                Assert.That(cueFrame.Cues[i].PolicyHint.Blocking, Is.False);

                var bridgeRequest = bridgePort.Requests[i];
                Assert.That((int)bridgeRequest.CueKey, Is.EqualTo((int)legacyRequests[i].Cue));
                Assert.That(bridgeRequest.Cue, Is.EqualTo(legacyRequests[i].Cue));
                Assert.That(bridgeRequest.OwnerEntityId, Is.EqualTo(legacyRequests[i].OwnerEntityId));
                Assert.That(bridgeRequest.Source.TickIndex, Is.EqualTo(result.TickIndex));
                Assert.That(bridgeRequest.Target, Is.EqualTo(PresentationTarget.Entity(legacyRequests[i].OwnerEntityId)));
                Assert.That(bridgeRequest.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
                Assert.That(bridgeRequest.Context.OwnerEntityId, Is.EqualTo(legacyRequests[i].Context.OwnerEntityId));
                Assert.That(bridgeRequest.Context.DebugTag, Is.EqualTo(legacyRequests[i].Context.DebugTag));
                Assert.That(playbackPlan.Cues[i].Policy.Blocking, Is.False);
            }

            Assert.That(executor.Diagnostics.RequestPlannedCount, Is.EqualTo(legacyRequests.Count));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(legacyRequests.Count));
            Assert.That(executor.Diagnostics.PlaybackSucceededCount, Is.EqualTo(legacyRequests.Count));
            Assert.That(executor.Diagnostics.UnsupportedLoopSemanticCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlanningOnly_DoesNotChangeLegacyPlaybackCount()
        {
            var rootObject = new GameObject(nameof(EnemyAudioPlanningOnly_DoesNotChangeLegacyPlaybackCount));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);
                var result = CreateTickResult(
                    CreatePresentationData(
                        enemyActionSignals: new[]
                        {
                            new TickEnemyActionPresentationSignal(
                                60,
                                EnemyActionKind.Melee,
                                activeActionSequence: 11,
                                startedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                startedRecoveryThisTick: false,
                                EnemyActionPresentationSource.Combat,
                                EnemyActionPresentationOutcome.Executed),
                        }),
                    new[] { enemy },
                    tickIndex: 7);
                var diagnosticsPipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                diagnosticsPipeline.Present(result);
                presenter.Present(result);

                Assert.That(diagnosticsPipeline.LastPlaybackPlan.Diagnostics.EnemyAudioPlaybackCueCount, Is.EqualTo(1));
                Assert.That(diagnosticsPipeline.LastPlaybackPlan.Diagnostics.EnemyAudioNoPlaybackBecausePlanningOnlyCount, Is.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("Active"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_DefaultMode_UsesLegacyControllerAndDoesNotCallBridgePort()
        {
            var rootObject = new GameObject(nameof(EnemyAudio_DefaultMode_UsesLegacyControllerAndDoesNotCallBridgePort));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingEnemyAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);

                presenter.ConfigureEnemyAudioExecution(
                    EnemyAudioExecutionMode.LegacyEnemyAudioController,
                    bridgePort);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 7));

                Assert.That(presenter.EnemyAudioExecutionMode, Is.EqualTo(EnemyAudioExecutionMode.LegacyEnemyAudioController));
                Assert.That(bridgePort.Requests, Is.Empty);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[] { "Active" }));
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByLegacyCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_RoutesOneShotCueToBridgePortOnly()
        {
            var rootObject = new GameObject(nameof(EnemyAudio_OrchestrationBridgeMode_RoutesOneShotCueToBridgePortOnly));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingEnemyAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);

                presenter.ConfigureEnemyAudioExecution(
                    EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                    bridgePort);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 7));

                Assert.That(playbackPort.TwoDCalls, Is.Empty);
                Assert.That(bridgePort.Requests, Has.Count.EqualTo(1));
                var request = bridgePort.Requests[0];
                Assert.That(request.CueKey, Is.EqualTo(PresentationEnemyAudioCueKey.Active));
                Assert.That(request.Cue, Is.EqualTo(EnemyAudioCue.Active));
                Assert.That(request.OwnerEntityId, Is.EqualTo(enemy.entityId));
                Assert.That(request.Source.TickIndex, Is.EqualTo(7));
                Assert.That(request.EnemyAudioPayload.OriginKind, Is.EqualTo(PresentationEnemyAudioOriginKind.Action));
                Assert.That(request.EnemyAudioPayload.Phase, Is.EqualTo(PresentationEnemyAudioPhase.Active));
                Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(enemy.entityId)));
                Assert.That(request.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_ProductionTelemetry_CoversOneShotOwnerSemanticLoopAndRollbackValues()
        {
            var rootObject = new GameObject(nameof(EnemyAudio_ProductionTelemetry_CoversOneShotOwnerSemanticLoopAndRollbackValues));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingEnemyAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);

                presenter.ConfigureEnemyAudioExecution(
                    EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                    bridgePort);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 8));

                var telemetry = presenter.EnemyAudioProductionTelemetrySnapshot;
                Assert.That(telemetry.CurrentMode, Is.EqualTo(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.ProductionDefaultMode, Is.EqualTo(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));
                Assert.That(telemetry.RollbackMode, Is.EqualTo(EnemyAudioExecutionMode.LegacyEnemyAudioController));
                Assert.That(telemetry.LastTickIndex, Is.EqualTo(8));
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationEnemyAudioCueKey.Active));
                Assert.That(telemetry.LastOwnerEntityId, Is.EqualTo(enemy.entityId));
                Assert.That(telemetry.LastOriginKind, Is.EqualTo(PresentationEnemyAudioOriginKind.Action));
                Assert.That(telemetry.LastPhase, Is.EqualTo(PresentationEnemyAudioPhase.Active));
                Assert.That(telemetry.LastFailureReason, Is.EqualTo(EnemyAudioTelemetryFailureReason.None));
                Assert.That(telemetry.LegacyOwnerAttemptCount, Is.EqualTo(1));
                Assert.That(telemetry.LegacyOwnerSkippedByPolicyCount, Is.EqualTo(1));
                Assert.That(telemetry.ExecutorOwnerAttemptCount, Is.EqualTo(1));
                Assert.That(telemetry.ExecutorOwnerExecutedCount, Is.EqualTo(1));
                Assert.That(telemetry.ObservedCueCount, Is.EqualTo(1));
                Assert.That(telemetry.RequestPlannedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackSucceededCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_DefaultAdapterUsesExistingControllerBoundary()
        {
            var rootObject = new GameObject(nameof(EnemyAudio_OrchestrationBridgeMode_DefaultAdapterUsesExistingControllerBoundary));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);

                presenter.ConfigureEnemyAudioExecution(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 7));

                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[] { "Active" }));
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByLegacyCount, Is.Zero);
                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_DuplicateGuardBlocksForcedSecondAttempt()
        {
            var result = CreateTickResult(CreatePresentationData(enemyActionSignals: new[]
            {
                CreateEnemyActionExecutedSignal(
                    60,
                    EnemyActionPresentationSource.Combat,
                    EnemyActionPresentationOutcome.Executed),
                CreateEnemyActionExecutedSignal(
                    60,
                    EnemyActionPresentationSource.Combat,
                    EnemyActionPresentationOutcome.Executed),
            }));
            var playbackPlan = CreateEnemyAudioPlaybackPlan(result);
            var bridgePort = new RecordingEnemyAudioPlaybackPort();
            var guard = new EnemyAudioExecutionGuard(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge);
            var executor = new GameplayEnemyAudioPresentationExecutor(
                bridgePort,
                EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                guard);

            executor.Play(playbackPlan);

            Assert.That(playbackPlan.Cues.Count(cue => cue.Cue.Domain == PresentationDomain.EnemyAudio), Is.EqualTo(2));
            Assert.That(bridgePort.Requests, Has.Count.EqualTo(1));
            Assert.That(executor.Diagnostics.DuplicateSuppressedCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.LastFailureReason, Is.EqualTo(EnemyAudioTelemetryFailureReason.DuplicateSuppressed));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_ReportsMissingDiagnosticsByCause()
        {
            AssertBridgeAdapterMissingResult(
                _ => new EnemyAudioViewFactory(null, null),
                finalEntities: Array.Empty<EntityState>(),
                expected: diagnostics => diagnostics.OwnerViewMissingCount);
            AssertBridgeAdapterMissingResult(
                parent => new EnemyAudioViewFactory(parent, null),
                finalEntities: new[] { CreateUnit(60, UnitRole.Enemy) },
                expected: diagnostics => diagnostics.AuthoringMissingCount);
            AssertBridgeAdapterMissingResult(
                parent => new EnemyAudioViewFactory(parent, null, attachAuthoringWithoutProfile: true),
                finalEntities: new[] { CreateUnit(60, UnitRole.Enemy) },
                expected: diagnostics => diagnostics.ProfileMissingCount);

            using var emptyProfileBundle = CreateEnemyAudioProfile();
            AssertBridgeAdapterMissingResult(
                parent => new EnemyAudioViewFactory(parent, emptyProfileBundle.Profile),
                finalEntities: new[] { CreateUnit(60, UnitRole.Enemy) },
                expected: diagnostics => diagnostics.OptionalProfileEntryMissingNoOpCount);

            var portMissingExecutor = new GameplayEnemyAudioPresentationExecutor(
                playbackPort: null,
                EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                new EnemyAudioExecutionGuard(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));
            portMissingExecutor.Play(CreateEnemyAudioPlaybackPlan(CreateTickResult(
                CreatePresentationData(enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        60,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.Executed),
                }))));

            Assert.That(portMissingExecutor.Diagnostics.PortMissingCount, Is.EqualTo(1));
            Assert.That(portMissingExecutor.Diagnostics.LastFailureReason, Is.EqualTo(EnemyAudioTelemetryFailureReason.PortMissing));
            AssertEnemyAudioExecutorPortResult(
                GameplayEnemyAudioPlaybackResultKind.BindingMissing,
                diagnostics => diagnostics.BindingMissingCount,
                EnemyAudioTelemetryFailureReason.BindingMissing);
            AssertEnemyAudioExecutorPortResult(
                GameplayEnemyAudioPlaybackResultKind.UnsupportedSemantic,
                diagnostics => diagnostics.UnsupportedSemanticCount,
                EnemyAudioTelemetryFailureReason.UnsupportedSemantic);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_IgnoresChargeActiveLoopAndKeepsLegacyLoopOwner()
        {
            var playbackPlan = CreateEnemyAudioPlaybackPlan(CreateTickResult(CreatePresentationData(
                enemyChargeSignals: new[]
                {
                    CreateChargeSignal(60, sequence: 4, EnemyChargePhase.Active, startedActiveThisTick: true),
                })));
            var bridgePort = new RecordingEnemyAudioPlaybackPort();
            var executor = new GameplayEnemyAudioPresentationExecutor(
                bridgePort,
                EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                new EnemyAudioExecutionGuard(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));

            executor.Play(playbackPlan);

            Assert.That(
                playbackPlan.Cues.Select(cue => cue.Cue.Key.TryGetEnemyAudioCueKey(out var cueKey) ? cueKey : default),
                Has.No.Member(PresentationEnemyAudioCueKey.ChargeActiveLoop));
            Assert.That(bridgePort.Requests.Select(request => request.CueKey).ToArray(), Is.EqualTo(new[]
            {
                PresentationEnemyAudioCueKey.Active,
            }));

            executor.Play(CreateManualEnemyAudioPlaybackPlan(PresentationEnemyAudioCueKey.ChargeActiveLoop));

            Assert.That(executor.Diagnostics.UnsupportedLoopSemanticCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.LastFailureReason, Is.EqualTo(EnemyAudioTelemetryFailureReason.UnsupportedLoopSemantic));
            Assert.That(bridgePort.Requests.Select(request => request.CueKey).ToArray(), Is.EqualTo(new[]
            {
                PresentationEnemyAudioCueKey.Active,
            }));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_LifecycleClearsDiagnosticsPortAndGuard()
        {
            var rootObject = new GameObject(nameof(EnemyAudio_OrchestrationBridgeMode_LifecycleClearsDiagnosticsPortAndGuard));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingEnemyAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);

                presenter.ConfigureEnemyAudioExecution(
                    EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                    bridgePort);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 11));

                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));

                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));

                Assert.That(presenter.EnemyAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(presenter.EnemyAudioProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(EnemyAudioTelemetryCleanupReason.ResetSession));
                Assert.That(bridgePort.ResetCount, Is.GreaterThanOrEqualTo(1));

                var cleanupPort = new RecordingEnemyAudioPlaybackPort();
                var cleanupExecutor = new GameplayEnemyAudioPresentationExecutor(
                    cleanupPort,
                    EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                    new EnemyAudioExecutionGuard(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));
                cleanupExecutor.Play(CreateEnemyAudioPlaybackPlan(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 12)));

                Assert.That(cleanupExecutor.Diagnostics.PlaybackSucceededCount, Is.EqualTo(1));

                cleanupExecutor.HardCleanup();

                Assert.That(cleanupExecutor.Diagnostics.ObservedCueCount, Is.Zero);
                Assert.That(cleanupExecutor.Diagnostics.LastCleanupReason, Is.EqualTo(EnemyAudioTelemetryCleanupReason.HardCleanupPresentationExtensions));
                Assert.That(cleanupPort.HardCleanupCount, Is.EqualTo(1));
            }
            finally
            {
                if (rootObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(rootObject);
                }
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudio_OrchestrationBridgeMode_DoesNotMutateTickResultOrBlockingState()
        {
            var rootObject = new GameObject(nameof(EnemyAudio_OrchestrationBridgeMode_DoesNotMutateTickResultOrBlockingState));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, null));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingEnemyAudioPlaybackPort();
                var enemy = CreateUnit(60, UnitRole.Enemy);
                var finalEntities = new[] { enemy };
                var eventLog = Array.Empty<string>();
                var result = CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    finalEntities,
                    tickIndex: 17);

                presenter.ConfigureEnemyAudioExecution(
                    EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                    bridgePort);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(finalEntities, new CubeTopologyState(FaceId.Floor));
                presenter.Present(result);

                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.DeterminismHash, Is.Empty);
                Assert.That(result.ObjectiveResult, Is.SameAs(StageObjectiveTickResult.NoObjective));
                Assert.That(presenter.EnemyAudioExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.EnemyAudioExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioCue_SerializedValuesRemainStable_WhenStationaryActiveIsAppended()
        {
            Assert.That((int)EnemyAudioCue.None, Is.EqualTo(0));
            Assert.That((int)EnemyAudioCue.Move, Is.EqualTo(1));
            Assert.That((int)EnemyAudioCue.Death, Is.EqualTo(2));
            Assert.That((int)EnemyAudioCue.Windup, Is.EqualTo(3));
            Assert.That((int)EnemyAudioCue.Landing, Is.EqualTo(4));
            Assert.That((int)EnemyAudioCue.Active, Is.EqualTo(5));
            Assert.That((int)EnemyAudioCue.Recover, Is.EqualTo(6));
            Assert.That((int)EnemyAudioCue.ForwardCellImpact, Is.EqualTo(7));
            Assert.That((int)EnemyAudioCue.ChargeActiveLoop, Is.EqualTo(8));
            Assert.That((int)EnemyAudioCue.StationaryActive, Is.EqualTo(9));
            Assert.That((int)EnemyAudioCue.PassiveContact, Is.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_DoesNotEmitEnemyActiveCueWhenNoContactCueAuthored()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.Executed),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.Cue), Has.No.EqualTo(EnemyAudioCue.Active));
            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.PassiveContact) }));
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_DoesNotEmitEnemySummonCueWhenNoContactCueAuthored()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.Executed),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.Cue), Has.No.EqualTo(EnemyAudioCue.Active));
            Assert.That(requests.Select(request => request.Context.DebugTag), Has.No.EqualTo("Active"));
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_EmitsContactCueOnlyWhenAuthored()
        {
            var rootObject = new GameObject(nameof(PassiveContact_EmitsContactCueOnlyWhenAuthored));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.PassiveContact, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            20,
                            EnemyActionPresentationSource.PassiveContact,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 1));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "PassiveContact" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_WithoutContactCueAuthored_DoesNotPlayActiveBinding()
        {
            var rootObject = new GameObject(nameof(PassiveContact_WithoutContactCueAuthored_DoesNotPlayActiveBinding));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            20,
                            EnemyActionPresentationSource.PassiveContact,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { enemy },
                    tickIndex: 1));

                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_PassiveContactReject_DoesNotEmitEnemyActiveAudio()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.RejectedByPlayerInvincible),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.Cue), Has.No.EqualTo(EnemyAudioCue.Active));
            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_PassiveContactReject_DoesNotEmitEnemySummonAudio()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.RejectedByPlayerInvincible),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.Cue), Has.No.EqualTo(EnemyAudioCue.Active));
            Assert.That(requests.Select(request => request.Context.DebugTag), Has.No.EqualTo("Active"));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_ActualEnemySummon_StillEmitsSummonAudio()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var spawnCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                visibilityChanges: new[]
                {
                    new TickVisibilityChange(
                        entityId: 60,
                        TickVisibilityChangeKind.Spawn,
                        spawnCell,
                        topology,
                        Direction.Left),
                },
                summonedEnemyPresentationBindings: new[]
                {
                    new TickSummonedEnemyPresentationBinding(
                        entityId: 60,
                        hasEnemyDefinitionBinding: true,
                        archetypeId: default,
                        sourceEntityId: 22),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (22, EnemyAudioCue.Active) }));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_ActualEnemyActive_StillEmitsActiveAudio()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.Executed),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.Active) }));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_ActualEnemyActiveReject_StillEmitsActiveAudio()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.RejectedByPlayerInvincible),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.Active) }));
        }

        [Test]
        [Category("Core")]
        public void ReceiverCooldownReject_DoesNotEmitEnemyActiveAudio()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.RejectedByReceiverCooldown),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ForwardCellImpact_DoesNotEmitEnemyActiveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        20,
                        EnemyActionPresentationSource.ForwardCellImpact,
                        EnemyActionPresentationOutcome.Executed),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlanner_DoesNotDependOnEnemyProfileNames()
        {
            var source = File.ReadAllText("Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioRequestPlanner.cs");

            Assert.That(source, Does.Not.Contain("JPeter"));
            Assert.That(source, Does.Not.Contain("Nebulous"));
            Assert.That(source, Does.Not.Contain("DrSaturn"));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioRequestPlanner_DoesNotUseEnemyNameSpecialCase()
        {
            var source = File.ReadAllText("Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioRequestPlanner.cs");

            Assert.That(source, Does.Not.Contain("BlackEye"));
            Assert.That(source, Does.Not.Contain("black_eye"));
            Assert.That(source, Does.Not.Contain("plazma"));
        }

        [Test]
        [Category("Core")]
        public void AudioRuntime_DoesNotAddGameplaySpecificSuppressRules()
        {
            var playbackService = File.ReadAllText("Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs");
            var audioManager = File.ReadAllText("Assets/_Shared/Audio/Runtime/AudioManager.cs");

            Assert.That(playbackService, Does.Not.Contain("PassiveContact"));
            Assert.That(audioManager, Does.Not.Contain("PassiveContact"));
            Assert.That(playbackService, Does.Not.Contain("PlayerInvincible"));
            Assert.That(audioManager, Does.Not.Contain("PlayerInvincible"));
            Assert.That(playbackService, Does.Not.Contain("ForwardCellImpact"));
            Assert.That(audioManager, Does.Not.Contain("ForwardCellImpact"));
            Assert.That(playbackService, Does.Not.Contain("ForwardCellImpact"));
            Assert.That(audioManager, Does.Not.Contain("ForwardCellImpact"));
        }

        [Test]
        [Category("Core")]
        public void AudioRuntime_DoesNotAddBlackEyeSpecificRules()
        {
            var playbackService = File.ReadAllText("Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs");
            var audioManager = File.ReadAllText("Assets/_Shared/Audio/Runtime/AudioManager.cs");

            Assert.That(playbackService, Does.Not.Contain("BlackEye"));
            Assert.That(audioManager, Does.Not.Contain("BlackEye"));
            Assert.That(playbackService, Does.Not.Contain("black_eye"));
            Assert.That(audioManager, Does.Not.Contain("black_eye"));
            Assert.That(playbackService, Does.Not.Contain("plazma"));
            Assert.That(audioManager, Does.Not.Contain("plazma"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_StationaryEnemyWithoutMotionFact_EmitsStationaryActiveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var enemy = CreateUnit(20, UnitRole.Enemy);
            var result = CreateTickResult(CreatePresentationData(), new[] { enemy });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue, request.Context.DebugTag)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.StationaryActive, "StationaryActive") }));
        }

        [Test]
        [Category("Extended")]
        public void LegacyEntityMotionMove_StillPlansEnemyMoveCue()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.Move) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyKinematicMotionTrack_PlansEnemyMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            startedTick: 1,
                            elapsedTicks: 1,
                            totalTicks: 16),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.Move) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyKinematicMotionTrack_ContinuationTick_DoesNotPlanMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            startedTick: 1,
                            elapsedTicks: 2,
                            totalTicks: 16),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) },
                tickIndex: 2);

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void KinematicHeldOrTerminalTrack_DoesNotPlanMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Held,
                            ForcedMotionOp.None,
                            sourceLocalOffset: new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero),
                            destinationLocalOffset: new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero)),
                        CreateKinematicTrack(
                            21,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            terminalKind: TickKinematicMotionTerminalKind.Interrupted),
                        CreateKinematicTrack(22, MotionMode.Settled, ForcedMotionOp.None),
                    }),
                new[]
                {
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(21, UnitRole.Enemy),
                    CreateUnit(22, UnitRole.Enemy),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void LegacyAndKinematicSameEntity_DedupesMoveCue()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            startedTick: 1,
                            elapsedTicks: 1,
                            totalTicks: 16),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Count(request => request.OwnerEntityId == 20 && request.Cue == EnemyAudioCue.Move),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void NonEnemyKinematicTrack_DoesNotPlanEnemyMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(10, MotionMode.Voluntary, ForcedMotionOp.None),
                        CreateKinematicTrack(
                            30,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            entityType: EntityType.Box),
                        CreateKinematicTrack(
                            40,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            entityType: EntityType.Box),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player),
                    CreateEntity(30, EntityType.Box),
                    CreateEntity(40, EntityType.Box),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SunwheelKinematicMove_WithMoveProfile_ProducesMoveRequest()
        {
            var rootObject = new GameObject(nameof(SunwheelKinematicMove_WithMoveProfile_ProducesMoveRequest));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        kinematicMotionTracks: new[]
                        {
                            CreateKinematicTrack(
                                20,
                                MotionMode.Voluntary,
                                ForcedMotionOp.None,
                                startedTick: 1,
                                elapsedTicks: 1,
                                totalTicks: 16),
                        }),
                    new[] { enemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_StationaryActiveBinding_PlaysWithoutMoveFact()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_StationaryActiveBinding_PlaysWithoutMoveFact));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.StationaryActive, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(), new[] { enemy }, tickIndex: 1));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "StationaryActive" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_FrontFaceStationaryActive_SuppressesWithoutConsumingCadenceOrMoveCue()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_FrontFaceStationaryActive_SuppressesWithoutConsumingCadenceOrMoveCue));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.StationaryActive, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var frontStationaryEnemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Front, 0, 0));
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 1);
                var targetCell = new SurfaceCell(FaceId.Front, 1, 1);
                var frontMovingEnemy = CreateUnit(21, UnitRole.Enemy, targetCell);
                var bottomStationaryEnemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { frontStationaryEnemy, frontMovingEnemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(entityMotions: new[]
                    {
                        new TickEntityMotion(frontMovingEnemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                    }),
                    new[] { frontStationaryEnemy, frontMovingEnemy },
                    tickIndex: 1));

                var snapshot = presenter.DebugCaptureEntityPresentationLifecycle(frontStationaryEnemy.entityId);
                Assert.That(snapshot.ViewsByEntityIdContainsEntityId, Is.True);
                Assert.That(snapshot.GameObjectActiveSelf, Is.True);
                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move" }));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(),
                    new[] { bottomStationaryEnemy, frontMovingEnemy },
                    tickIndex: 2));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move", "StationaryActive" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_StationaryActiveWithoutBinding_DropsCandidate()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_StationaryActiveWithoutBinding_DropsCandidate));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(), new[] { enemy }, tickIndex: 1));

                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_StationaryActiveCadence_ThrottlesRepeatedTicks()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_StationaryActiveCadence_ThrottlesRepeatedTicks));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.StationaryActive, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(), new[] { enemy }, tickIndex: 1));
                presenter.Present(CreateTickResult(CreatePresentationData(), new[] { enemy }, tickIndex: 2));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "StationaryActive" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_StationaryActive_DoesNotConsumeMoveCadence()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_StationaryActive_DoesNotConsumeMoveCadence));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.StationaryActive, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(), new[] { enemy }, tickIndex: 1));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(entityMotions: new[]
                    {
                        new TickEntityMotion(enemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                    }),
                    new[] { enemy },
                    tickIndex: 2));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "StationaryActive", "Move" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentInitial_StationaryActive_DoesNotPlay()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentInitial_StationaryActive_DoesNotPlay));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.StationaryActive, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));

                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySfxPolicyCatalog_StationaryActive_UsesGenericGameplayNotEnemyMovement()
        {
            Assert.That(GameplaySfxPolicyCatalog.Resolve("Move").Group, Is.EqualTo(AudioVoiceGroupId.EnemyMovement));
            Assert.That(GameplaySfxPolicyCatalog.Resolve("StationaryActive").Group, Is.EqualTo(AudioVoiceGroupId.GenericGameplay));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_DefaultTiming_UsesDocumentedTwentyTpsIntervals()
        {
            var gate = new EnemyMoveCadenceGate();

            gate.Configure(simulationTicksPerSecond: 20);

            Assert.That(gate.PerEntityMoveMinIntervalTicks, Is.EqualTo(140));
            Assert.That(gate.GlobalMoveMinIntervalTicks, Is.EqualTo(60));
            Assert.That(gate.JitterTicks, Is.EqualTo(20));
            Assert.That(gate.MaxMoveRequestsPerTick, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_PerEntityInterval_ThrottlesSameEnemyMove()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 16,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 10);

            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 1), Is.True);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 16), Is.False);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 17), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_GlobalInterval_ThrottlesDifferentEnemyMoves()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 0,
                globalMoveMinIntervalTicks: 4,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 10);

            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 1), Is.True);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 4), Is.False);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 5), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_TickBudget_AllowsOnlyConfiguredMovesPerTick()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 0,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 1);

            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 1), Is.True);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 1), Is.False);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 2), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_Jitter_IsDeterministicAndBounded()
        {
            var first = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 4);
            var second = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 4);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.InRange(0, 4));
            Assert.That(EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 0), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_EnemyMoveCadence_ThrottlesMoveButNotActionCue()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_EnemyMoveCadence_ThrottlesMoveButNotActionCue));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.Windup, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(enemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        },
                        enemyActionSignals: new[] { CreateEnemyActionStartedSignal(enemy.entityId) }),
                    new[] { enemy },
                    tickIndex: 1));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(enemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        },
                        enemyActionSignals: new[] { CreateEnemyActionStartedSignal(enemy.entityId) }),
                    new[] { enemy },
                    tickIndex: 2));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move", "Windup", "Windup" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_EnemyMoveCadence_AppliesGlobalTickBudget()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_EnemyMoveCadence_AppliesGlobalTickBudget));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var firstEnemy = CreateUnit(20, UnitRole.Enemy);
                var secondEnemy = CreateUnit(21, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { firstEnemy, secondEnemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(firstEnemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                            new TickEntityMotion(secondEnemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        }),
                    new[] { firstEnemy, secondEnemy },
                    tickIndex: 1));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_SummonWindupStartTick_EmitsSingleWindupCue()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                summonWindupWarnings: new[]
                {
                    new TickSummonWindupWarningSignal(
                        sourceEntityId: 22,
                        effectIndex: 0,
                        sourceCell,
                        topology,
                        Direction.Right,
                        windupStartTick: 10,
                        windupEndTick: 14,
                        activationSequence: 1,
                        tickIndex: 10,
                        presentationSeed: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (22, EnemyAudioCue.Windup) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_SummonWindupActiveTick_DoesNotEmitWindupCue()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                summonWindupWarnings: new[]
                {
                    new TickSummonWindupWarningSignal(
                        sourceEntityId: 22,
                        effectIndex: 0,
                        sourceCell,
                        topology,
                        Direction.Right,
                        windupStartTick: 10,
                        windupEndTick: 14,
                        activationSequence: 1,
                        tickIndex: 12,
                        presentationSeed: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_SummonedEnemySpawn_EmitsActiveCueForSourceSummoner()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var spawnCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                visibilityChanges: new[]
                {
                    new TickVisibilityChange(
                        entityId: 60,
                        TickVisibilityChangeKind.Spawn,
                        spawnCell,
                        topology,
                        Direction.Left),
                },
                summonedEnemyPresentationBindings: new[]
                {
                    new TickSummonedEnemyPresentationBinding(
                        entityId: 60,
                        hasEnemyDefinitionBinding: true,
                        archetypeId: default,
                        sourceEntityId: 22),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (22, EnemyAudioCue.Active) }));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_HitArrival_CreatesForwardCellImpactSfx()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                forwardCellProjectileArrivalSignals: new[]
                {
                    CreateForwardCellProjectileArrivalSignal(
                        targetCell,
                        PendingCellImpactResolutionKind.Hit,
                        targetEntityId: 10),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.ForwardCellImpact) }));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ForwardCellImpactSfx_UsesArrivalSignalNotHitSignal()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                forwardCellImpactSignals: new[]
                {
                    CreateForwardCellImpactSignal(
                        ownerId: 20,
                        sourceEnemyId: 20,
                        targetCell: targetCell,
                        hit: true,
                        targetEntityId: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => request.Cue).ToArray(),
                Has.No.EqualTo(EnemyAudioCue.ForwardCellImpact));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_MissArrival_CreatesForwardCellImpactSfx()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                forwardCellProjectileArrivalSignals: new[]
                {
                    CreateForwardCellProjectileArrivalSignal(
                        targetCell,
                        PendingCellImpactResolutionKind.Miss,
                        targetEntityId: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.ForwardCellImpact) }));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_CancelledSourceInvalid_DoesNotCreateForwardCellImpactSfx()
        {
            AssertForwardCellProjectileArrivalDoesNotCreateForwardCellImpactSfx(
                PendingCellImpactResolutionKind.CancelledSourceInvalid);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_CancelledTargetInvalid_DoesNotCreateForwardCellImpactSfx()
        {
            AssertForwardCellProjectileArrivalDoesNotCreateForwardCellImpactSfx(
                PendingCellImpactResolutionKind.CancelledTargetInvalid);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ExpiredTopologyInvalid_DoesNotCreateForwardCellImpactSfx()
        {
            AssertForwardCellProjectileArrivalDoesNotCreateForwardCellImpactSfx(
                PendingCellImpactResolutionKind.ExpiredTopologyInvalid);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_Hit_DoesNotDuplicateForwardCellImpactSfx()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                forwardCellImpactSignals: new[]
                {
                    CreateForwardCellImpactSignal(
                        ownerId: 20,
                        sourceEnemyId: 20,
                        targetCell: targetCell,
                        hit: true,
                        targetEntityId: 10),
                },
                forwardCellProjectileArrivalSignals: new[]
                {
                    CreateForwardCellProjectileArrivalSignal(
                        targetCell,
                        PendingCellImpactResolutionKind.Hit,
                        targetEntityId: 10),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Count(request => request.Cue == EnemyAudioCue.ForwardCellImpact),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_PlayerHit_EmitsForwardCellImpactRequest()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            20,
                            EnemyActionPresentationSource.ForwardCellImpact,
                            EnemyActionPresentationOutcome.Executed),
                    },
                    forwardCellImpactSignals: new[]
                    {
                        CreateForwardCellImpactSignal(
                            ownerId: 20,
                            sourceEnemyId: 20,
                            targetCell: targetCell,
                            hit: true,
                            targetEntityId: 10),
                    },
                    forwardCellProjectileArrivalSignals: new[]
                    {
                        CreateForwardCellProjectileArrivalSignal(
                            targetCell,
                            PendingCellImpactResolutionKind.Hit,
                            targetEntityId: 10),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player, targetCell),
                    CreateUnit(20, UnitRole.Enemy),
                });

            var requests = planner.BuildRequests(result);
            var requestPairs = requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray();

            Assert.That(
                requestPairs,
                Has.Some.EqualTo((20, EnemyAudioCue.ForwardCellImpact)));
            Assert.That(
                requestPairs,
                Has.No.EqualTo((20, EnemyAudioCue.Active)));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioRequestPlanner_DoesNotSuppressActualActiveWhenForwardCellImpactExists()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            20,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    },
                    forwardCellImpactSignals: new[]
                    {
                        CreateForwardCellImpactSignal(
                            ownerId: 20,
                            sourceEnemyId: 20,
                            targetCell: targetCell,
                            hit: true,
                            targetEntityId: 10),
                    },
                    forwardCellProjectileArrivalSignals: new[]
                    {
                        CreateForwardCellProjectileArrivalSignal(
                            targetCell,
                            PendingCellImpactResolutionKind.Hit,
                            targetEntityId: 10),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player, targetCell),
                    CreateUnit(20, UnitRole.Enemy),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[]
                {
                    (20, EnemyAudioCue.Active),
                    (20, EnemyAudioCue.ForwardCellImpact),
                }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_GravityFieldAuraPhases_EmitWindupAttackAndRecoverCues()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyUtilitySignals: new[]
                {
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.WindupStarted,
                        startTick: 1,
                        executeTick: 2,
                        durationTicks: 1),
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.AttackStarted,
                        startTick: 2,
                        executeTick: 2,
                        durationTicks: 0),
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.RecoverStarted,
                        startTick: 2,
                        executeTick: 3,
                        durationTicks: 1),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (30, EnemyAudioCue.Windup), (30, EnemyAudioCue.Active), (30, EnemyAudioCue.Recover) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_ChargeActiveStart_EmitsActiveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyChargeSignals: new[]
                {
                    new TickEnemyChargePresentationSignal(
                        40,
                        sequence: 1,
                        EnemyChargePhase.Active,
                        startedWindupThisTick: false,
                        startedActiveThisTick: true,
                        startedRecoverThisTick: false,
                        lockedDirection: Direction.Right),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (40, EnemyAudioCue.Active) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_GlidePhaseStarts_EmitWindupActiveAndRecoverCues()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyGlideSignals: new[]
                {
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Windup, phaseElapsedTicks: 0),
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Active, phaseElapsedTicks: 0),
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Active, phaseElapsedTicks: 1),
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Recovery, phaseElapsedTicks: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[]
                {
                    (60, EnemyAudioCue.Windup),
                    (60, EnemyAudioCue.Active),
                    (60, EnemyAudioCue.Recover),
                }));
        }

        [Test]
        [Category("Core")]
        public void TickPresentationFactExtractor_GravityFieldAuraPhases_EmitTypedEnemyAudioFacts()
        {
            const int UtilityActivationSequenceId = ((11 + 1) * 397) ^ (12 + 1);
            const int UtilityRecoverSequenceId = ((11 + 1) * 397) ^ (13 + 1);
            var result = CreateTickResult(
                CreatePresentationData(enemyUtilitySignals: new[]
                {
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.WindupStarted,
                        startTick: 11,
                        executeTick: 12,
                        durationTicks: 2),
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.ActiveStarted,
                        startTick: 11,
                        executeTick: 12,
                        durationTicks: 2),
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.AttackStarted,
                        startTick: 11,
                        executeTick: 12,
                        durationTicks: 2),
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.RecoverStarted,
                        startTick: 11,
                        executeTick: 13,
                        durationTicks: 1),
                }),
                tickIndex: 12);

            var facts = ExtractEnemyAudioFacts(result);
            var cues = PlanEnemyAudioCues(result);

            Assert.That(
                facts.Select(fact => (
                    fact.EnemyAudioPayload.OwnerEntityId,
                    (PresentationEnemyAudioCueKey)fact.EnemyAudioPayload.CueKey,
                    fact.EnemyAudioPayload.OriginKind,
                    fact.EnemyAudioPayload.Phase,
                    fact.EnemyAudioPayload.SourceSequenceId)).ToArray(),
                Is.EqualTo(new[]
                {
                    (30, PresentationEnemyAudioCueKey.Windup, PresentationEnemyAudioOriginKind.Utility, PresentationEnemyAudioPhase.Windup, UtilityActivationSequenceId),
                    (30, PresentationEnemyAudioCueKey.Active, PresentationEnemyAudioOriginKind.Utility, PresentationEnemyAudioPhase.Active, UtilityActivationSequenceId),
                    (30, PresentationEnemyAudioCueKey.Recover, PresentationEnemyAudioOriginKind.Utility, PresentationEnemyAudioPhase.Recover, UtilityRecoverSequenceId),
                }));
            Assert.That(
                cues.Select(cue => (cue.EnemyAudioPayload.OwnerEntityId, ResolveEnemyAudioCueKey(cue))).ToArray(),
                Is.EqualTo(new[]
                {
                    (30, PresentationEnemyAudioCueKey.Windup),
                    (30, PresentationEnemyAudioCueKey.Active),
                    (30, PresentationEnemyAudioCueKey.Recover),
                }));
        }

        [Test]
        [Category("Core")]
        public void TickPresentationFactExtractor_GlidePhaseStarts_EmitTypedEnemyAudioFacts()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var result = CreateTickResult(CreatePresentationData(
                enemyGlideSignals: new[]
                {
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Windup, phaseElapsedTicks: 0),
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Active, phaseElapsedTicks: 0),
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Active, phaseElapsedTicks: 1),
                    CreateGlideSignal(60, cell, EnemyGlidePhase.Recovery, phaseElapsedTicks: 0),
                }));

            var facts = ExtractEnemyAudioFacts(result);

            Assert.That(
                facts.Select(fact => (
                    fact.EnemyAudioPayload.OwnerEntityId,
                    (PresentationEnemyAudioCueKey)fact.EnemyAudioPayload.CueKey,
                    fact.EnemyAudioPayload.OriginKind,
                    fact.EnemyAudioPayload.Phase,
                    fact.EnemyAudioPayload.SourceSequenceId)).ToArray(),
                Is.EqualTo(new[]
                {
                    (60, PresentationEnemyAudioCueKey.Windup, PresentationEnemyAudioOriginKind.Glide, PresentationEnemyAudioPhase.Windup, 1),
                    (60, PresentationEnemyAudioCueKey.Active, PresentationEnemyAudioOriginKind.Glide, PresentationEnemyAudioPhase.Active, 1),
                    (60, PresentationEnemyAudioCueKey.Recover, PresentationEnemyAudioOriginKind.Glide, PresentationEnemyAudioPhase.Recover, 1),
                }));
        }

        [Test]
        [Category("Core")]
        public void TickPresentationFactExtractor_SummonedEnemySpawn_EmitsSourceOwnerActiveFact()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var spawnCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var result = CreateTickResult(CreatePresentationData(
                visibilityChanges: new[]
                {
                    new TickVisibilityChange(
                        entityId: 60,
                        TickVisibilityChangeKind.Spawn,
                        spawnCell,
                        topology,
                        Direction.Left),
                    new TickVisibilityChange(
                        entityId: 61,
                        TickVisibilityChangeKind.Spawn,
                        spawnCell,
                        topology,
                        Direction.Left),
                },
                summonedEnemyPresentationBindings: new[]
                {
                    new TickSummonedEnemyPresentationBinding(
                        entityId: 60,
                        hasEnemyDefinitionBinding: true,
                        archetypeId: default,
                        sourceEntityId: 22),
                    new TickSummonedEnemyPresentationBinding(
                        entityId: 62,
                        hasEnemyDefinitionBinding: true,
                        archetypeId: default,
                        sourceEntityId: 22),
                }));

            var facts = ExtractEnemyAudioFacts(result);

            Assert.That(facts.Count, Is.EqualTo(1));
            Assert.That(facts[0].Source.SemanticSource, Is.EqualTo(PresentationSemanticSource.EnemySummon));
            Assert.That(facts[0].EnemyAudioPayload.OwnerEntityId, Is.EqualTo(22));
            Assert.That((PresentationEnemyAudioCueKey)facts[0].EnemyAudioPayload.CueKey, Is.EqualTo(PresentationEnemyAudioCueKey.Active));
            Assert.That(facts[0].EnemyAudioPayload.OriginKind, Is.EqualTo(PresentationEnemyAudioOriginKind.Summon));
            Assert.That(facts[0].EnemyAudioPayload.Phase, Is.EqualTo(PresentationEnemyAudioPhase.Active));
            Assert.That(facts[0].EnemyAudioPayload.TargetEntityId, Is.EqualTo(60));
            Assert.That(facts[0].Target, Is.EqualTo(PresentationTarget.Entity(22)));
        }

        [Test]
        [Category("Core")]
        public void TickPresentationFactExtractor_EnemyMoveSources_EmitSingleMoveFact()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                        new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(20, MotionMode.Voluntary, ForcedMotionOp.None, startedTick: 1),
                        CreateKinematicTrack(21, MotionMode.Voluntary, ForcedMotionOp.None, startedTick: 1),
                        CreateKinematicTrack(22, MotionMode.Charge, ForcedMotionOp.None, startedTick: 1),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player),
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(21, UnitRole.Enemy),
                    CreateUnit(22, UnitRole.Enemy),
                });

            var facts = ExtractEnemyAudioFacts(result);

            Assert.That(
                facts.Select(fact => (
                    fact.Source.SemanticSource,
                    fact.EnemyAudioPayload.OwnerEntityId,
                    (PresentationEnemyAudioCueKey)fact.EnemyAudioPayload.CueKey,
                    fact.EnemyAudioPayload.OriginKind,
                    fact.EnemyAudioPayload.Phase)).ToArray(),
                Is.EqualTo(new[]
                {
                    (PresentationSemanticSource.EnemyMove, 20, PresentationEnemyAudioCueKey.Move, PresentationEnemyAudioOriginKind.Move, PresentationEnemyAudioPhase.Move),
                    (PresentationSemanticSource.EnemyMove, 21, PresentationEnemyAudioCueKey.Move, PresentationEnemyAudioOriginKind.Move, PresentationEnemyAudioPhase.Move),
                }));
        }

        [Test]
        [Category("Core")]
        public void TickPresentationFactExtractor_StationaryActive_EmitsOnlyWithoutSpecificCueOrMotion()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(21, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            22,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                new[]
                {
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(21, UnitRole.Enemy),
                    CreateUnit(22, UnitRole.Enemy),
                    CreateUnit(10, UnitRole.Player),
                });

            var facts = ExtractEnemyAudioFacts(result);

            Assert.That(
                facts.Select(fact => (
                    fact.EnemyAudioPayload.OwnerEntityId,
                    (PresentationEnemyAudioCueKey)fact.EnemyAudioPayload.CueKey,
                    fact.EnemyAudioPayload.OriginKind,
                    fact.EnemyAudioPayload.Phase)).ToArray(),
                Is.EqualTo(new[]
                {
                    (21, PresentationEnemyAudioCueKey.Move, PresentationEnemyAudioOriginKind.Move, PresentationEnemyAudioPhase.Move),
                    (22, PresentationEnemyAudioCueKey.Active, PresentationEnemyAudioOriginKind.Action, PresentationEnemyAudioPhase.Active),
                    (20, PresentationEnemyAudioCueKey.StationaryActive, PresentationEnemyAudioOriginKind.Stationary, PresentationEnemyAudioPhase.StationaryActive),
                }));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlanning_LegacyAndTypedRequiredSemanticParity_CoversRecoveredCarriers()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            30,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 2),
                        new TickEnemyUtilityPresentationSignal(
                            30,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.AttackStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 2),
                        new TickEnemyUtilityPresentationSignal(
                            30,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.RecoverStarted,
                            startTick: 1,
                            executeTick: 3,
                            durationTicks: 1),
                    },
                    enemyGlideSignals: new[]
                    {
                        CreateGlideSignal(40, sourceCell, EnemyGlidePhase.Windup, phaseElapsedTicks: 0),
                        CreateGlideSignal(40, sourceCell, EnemyGlidePhase.Active, phaseElapsedTicks: 0),
                        CreateGlideSignal(40, sourceCell, EnemyGlidePhase.Recovery, phaseElapsedTicks: 0),
                    },
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(
                            entityId: 60,
                            TickVisibilityChangeKind.Spawn,
                            targetCell,
                            topology,
                            Direction.Left),
                    },
                    summonedEnemyPresentationBindings: new[]
                    {
                        new TickSummonedEnemyPresentationBinding(
                            entityId: 60,
                            hasEnemyDefinitionBinding: true,
                            archetypeId: default,
                            sourceEntityId: 50),
                    }),
                new[]
                {
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(30, UnitRole.Enemy),
                    CreateUnit(40, UnitRole.Enemy),
                    CreateUnit(50, UnitRole.Enemy),
                });

            var legacyRequests = new EnemyAudioRequestPlanner().BuildRequests(result);
            var typedCues = PlanEnemyAudioCues(result);

            Assert.That(
                typedCues.Select(ToSemanticCueKey).ToArray(),
                Is.EqualTo(legacyRequests.Select(ToSemanticRequestKey).ToArray()));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioSemanticProjector_RemainsPureCanonicalProjectionBoundary()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/EnemyAudioSemanticProjector.cs");
            var referencedAssemblies = typeof(EnemyAudioSemanticProjector)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .ToArray();

            Assert.That(source, Does.Not.Contain("UnityEngine"));
            Assert.That(source, Does.Not.Contain("GameObject"));
            Assert.That(source, Does.Not.Contain("MonoBehaviour"));
            Assert.That(source, Does.Not.Contain("AudioDefinition"));
            Assert.That(source, Does.Not.Contain("AudioBinding"));
            Assert.That(source, Does.Not.Contain("EnemyAudioProfile"));
            Assert.That(source, Does.Not.Contain("EnemyAudioAuthoring"));
            Assert.That(source, Does.Not.Contain("IAudioService"));
            Assert.That(source, Does.Not.Contain("IGameplayAudioPlaybackPort"));
            Assert.That(source, Does.Not.Contain("GameplaySceneHost"));
            Assert.That(source, Does.Not.Contain("Resources.Load"));
            Assert.That(source, Does.Not.Contain("GameObject.Find"));
            Assert.That(source, Does.Not.Contain("FindObjectOfType"));
            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(referencedAssemblies, Has.No.EqualTo("Game.Feature.Gameplay.EnemyAudio"));
            Assert.That(referencedAssemblies, Has.No.EqualTo("Game.Feature.Gameplay.Host"));
            Assert.That(referencedAssemblies, Has.No.EqualTo("Game.Shared.Audio"));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioSemanticConsumers_UseSharedProjectorForRecoveredMappings()
        {
            var plannerSource = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioRequestPlanner.cs");
            var factExtractorSource = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_PresentationRuntime/Runtime/GameplayPresentationPipeline.cs");

            Assert.That(plannerSource, Does.Contain("EnemyAudioSemanticProjector.Project(result)"));
            Assert.That(factExtractorSource, Does.Contain("EnemyAudioSemanticProjector.Project(result)"));
            Assert.That(plannerSource, Does.Contain("BuildLegacySummonWindupRequests"));
            Assert.That(plannerSource, Does.Not.Contain("BuildMoveRequests"));
            Assert.That(plannerSource, Does.Not.Contain("BuildUtilityRequests"));
            Assert.That(plannerSource, Does.Not.Contain("BuildSummonRequests"));
            Assert.That(plannerSource, Does.Not.Contain("BuildGlideRequests"));
            Assert.That(plannerSource, Does.Not.Contain("BuildStationaryActiveRequests"));
            Assert.That(plannerSource, Does.Not.Contain("TryResolveSummonSourceEntityId"));
            Assert.That(plannerSource, Does.Not.Contain("IsEnemyLocomotionMoveTrack"));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioSemanticProjector_DeterministicProjectionKeepsOrderedEventsFactsCuesAndPlaybackKeys()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            30,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.AttackStarted,
                            startTick: 4,
                            executeTick: 5,
                            durationTicks: 2),
                    },
                    enemyGlideSignals: new[]
                    {
                        CreateGlideSignal(40, sourceCell, EnemyGlidePhase.Windup, phaseElapsedTicks: 0),
                    },
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(
                            entityId: 60,
                            TickVisibilityChangeKind.Spawn,
                            targetCell,
                            topology,
                            Direction.Left),
                    },
                    summonedEnemyPresentationBindings: new[]
                    {
                        new TickSummonedEnemyPresentationBinding(
                            entityId: 60,
                            hasEnemyDefinitionBinding: true,
                            archetypeId: default,
                            sourceEntityId: 50),
                    },
                    forwardCellProjectileArrivalSignals: new[]
                    {
                        CreateForwardCellProjectileArrivalSignal(
                            targetCell,
                            PendingCellImpactResolutionKind.Hit,
                            ownerId: 70,
                            sourceEnemyId: 70,
                            targetEntityId: 10,
                            impactId: 901,
                            presentationKey: 902,
                            impactTick: 23),
                    },
                    entityExitSignals: new[]
                    {
                        new TickEntityExitPresentationSignal(
                            80,
                            TickEntityExitCause.EnemyDeath,
                            sourceCell,
                            topology,
                            Direction.Left,
                            EntityType.Unit),
                    }),
                new[]
                {
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(30, UnitRole.Enemy),
                    CreateUnit(40, UnitRole.Enemy),
                    CreateUnit(50, UnitRole.Enemy),
                    CreateUnit(70, UnitRole.Enemy),
                },
                tickIndex: 23);

            var firstEvents = EnemyAudioSemanticProjector.Project(result).Select(ToSemanticEventKey).ToArray();
            var secondEvents = EnemyAudioSemanticProjector.Project(result).Select(ToSemanticEventKey).ToArray();
            var firstFacts = ExtractEnemyAudioFacts(result).Select(ToFactKey).ToArray();
            var secondFacts = ExtractEnemyAudioFacts(result).Select(ToFactKey).ToArray();
            var firstCues = PlanEnemyAudioCues(result).Select(ToSemanticCueKey).ToArray();
            var secondCues = PlanEnemyAudioCues(result).Select(ToSemanticCueKey).ToArray();
            var firstPlaybackKeys = CreateEnemyAudioPlaybackPlan(result).Cues.Select(ToPlaybackKey).ToArray();
            var secondPlaybackKeys = CreateEnemyAudioPlaybackPlan(result).Cues.Select(ToPlaybackKey).ToArray();

            Assert.That(secondEvents, Is.EqualTo(firstEvents));
            Assert.That(secondFacts, Is.EqualTo(firstFacts));
            Assert.That(secondCues, Is.EqualTo(firstCues));
            Assert.That(secondPlaybackKeys, Is.EqualTo(firstPlaybackKeys));
            Assert.That(firstCues.Select(cue => cue.Cue), Has.No.EqualTo(PresentationEnemyAudioCueKey.ChargeActiveLoop));
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioSemanticProjector_CardinalityEdges_AreExplicitlyGoverned()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);

            Assert.That(CountTypedCue(CreateTickResult(CreatePresentationData(enemyUtilitySignals: new[]
                {
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.ActiveStarted,
                        1,
                        2,
                        2),
                })), PresentationEnemyAudioCueKey.Active), Is.EqualTo(1));
            Assert.That(CountTypedCue(CreateTickResult(CreatePresentationData(enemyUtilitySignals: new[]
                {
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.AttackStarted,
                        1,
                        2,
                        2),
                })), PresentationEnemyAudioCueKey.Active), Is.EqualTo(1));
            Assert.That(CountTypedCue(CreateTickResult(CreatePresentationData(enemyUtilitySignals: new[]
                {
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.ActiveStarted,
                        1,
                        2,
                        2),
                    new TickEnemyUtilityPresentationSignal(
                        30,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.AttackStarted,
                        1,
                        2,
                        2),
                })), PresentationEnemyAudioCueKey.Active), Is.EqualTo(1));

            var singleSummonResult = CreateTickResult(CreatePresentationData(
                visibilityChanges: new[]
                {
                    new TickVisibilityChange(60, TickVisibilityChangeKind.Spawn, targetCell, topology, Direction.Left),
                },
                summonedEnemyPresentationBindings: new[]
                {
                    new TickSummonedEnemyPresentationBinding(60, true, default, 22),
                }));
            var singleSummonFacts = ExtractEnemyAudioFacts(singleSummonResult);
            Assert.That(singleSummonFacts.Count, Is.EqualTo(1));
            Assert.That(singleSummonFacts[0].EnemyAudioPayload.OwnerEntityId, Is.EqualTo(22));
            Assert.That(singleSummonFacts[0].EnemyAudioPayload.TargetEntityId, Is.EqualTo(60));
            Assert.That(
                CountTypedCue(CreateTickResult(CreatePresentationData(visibilityChanges: new[]
                {
                    new TickVisibilityChange(61, TickVisibilityChangeKind.Spawn, targetCell, topology, Direction.Left),
                })), PresentationEnemyAudioCueKey.Active),
                Is.Zero);
            Assert.That(
                CountTypedCue(CreateTickResult(CreatePresentationData(
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(62, TickVisibilityChangeKind.Spawn, targetCell, topology, Direction.Left),
                    },
                    summonedEnemyPresentationBindings: new[]
                    {
                        new TickSummonedEnemyPresentationBinding(62, false, default, 22),
                    })), PresentationEnemyAudioCueKey.Active),
                Is.Zero);
            Assert.That(
                CountTypedCue(CreateTickResult(CreatePresentationData(
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(63, TickVisibilityChangeKind.Spawn, targetCell, topology, Direction.Left),
                        new TickVisibilityChange(64, TickVisibilityChangeKind.Spawn, targetCell, topology, Direction.Left),
                    },
                    summonedEnemyPresentationBindings: new[]
                    {
                        new TickSummonedEnemyPresentationBinding(63, true, default, 22),
                        new TickSummonedEnemyPresentationBinding(64, true, default, 22),
                    })), PresentationEnemyAudioCueKey.Active),
                Is.EqualTo(2));

            Assert.That(
                CountTypedCue(CreateTickResult(
                    CreatePresentationData(entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    }),
                    new[] { CreateUnit(20, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.Move),
                Is.EqualTo(1));
            Assert.That(
                CountTypedCue(CreateTickResult(
                    CreatePresentationData(kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(20, MotionMode.Voluntary, ForcedMotionOp.None, startedTick: 1),
                    }),
                    new[] { CreateUnit(20, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.Move),
                Is.EqualTo(1));
            Assert.That(
                CountTypedCue(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                        },
                        kinematicMotionTracks: new[]
                        {
                            CreateKinematicTrack(20, MotionMode.Voluntary, ForcedMotionOp.None, startedTick: 1),
                        }),
                    new[] { CreateUnit(20, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.Move),
                Is.EqualTo(1));
            Assert.That(
                CountTypedCue(CreateTickResult(
                    CreatePresentationData(kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(20, MotionMode.Voluntary, ForcedMotionOp.Knockback, startedTick: 1),
                    }),
                    new[] { CreateUnit(20, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.Move),
                Is.Zero);

            Assert.That(
                CountTypedCue(CreateTickResult(CreatePresentationData(), new[] { CreateUnit(90, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.StationaryActive),
                Is.EqualTo(1));
            Assert.That(
                CountTypedCue(CreateTickResult(
                    CreatePresentationData(entityMotions: new[]
                    {
                        new TickEntityMotion(90, TickEntityMotionKind.Move, sourceCell, targetCell),
                    }),
                    new[] { CreateUnit(90, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.StationaryActive),
                Is.Zero);
            Assert.That(
                CountTypedCue(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            90,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    new[] { CreateUnit(90, UnitRole.Enemy) }),
                    PresentationEnemyAudioCueKey.StationaryActive),
                Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_ActionRecoveryStart_EmitsRecoverCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                enemyActionSignals: new[]
                {
                    new TickEnemyActionPresentationSignal(
                        50,
                        EnemyActionKind.Melee,
                        activeActionSequence: 1,
                        startedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: false,
                        startedRecoveryThisTick: true),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (50, EnemyAudioCue.Recover) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioProfile_ValidateOrThrow_RejectsInvalidEntries()
        {
            using var duplicateProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            using var emptyCueProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.None, CreateDefinitionSpec()));
            using var loopingProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Death, CreateDefinitionSpec(loop: true)));
            using var chargeLoopNonLoopingProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    CreateDefinitionSpec(loop: false),
                    attachmentSlotId: "charge-active-loop"));
            using var chargeLoopDetachedProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.ChargeActiveLoop, CreateDefinitionSpec(loop: true)));

            Assert.That(
                Assert.Throws<InvalidOperationException>(() => duplicateProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("duplicate enemy audio cue 'Move'"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => emptyCueProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("empty enemy audio cue"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => loopingProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("only allows one-shot definitions"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => chargeLoopNonLoopingProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("requires a looping AudioDefinition"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => chargeLoopDetachedProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("requires an attachment slot"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioProfile_ValidateOrThrow_AllowsAttachedChargeActiveLoop()
        {
            using var profile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    CreateDefinitionSpec(loop: true),
                    attachmentSlotId: "charge-active-loop"));

            Assert.DoesNotThrow(() => profile.Profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_ChargeActiveLoop_StartsOnceAndStopsOnRecover()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ChargeActiveLoop_StartsOnceAndStopsOnRecover));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    CreateDefinitionSpec(loop: true),
                    attachmentSlotId: "charge-active-loop"));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(40, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 1, EnemyChargePhase.Active, startedActiveThisTick: true),
                    }),
                    new[] { enemy },
                    tickIndex: 1));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 1, EnemyChargePhase.Active, startedActiveThisTick: false),
                    }),
                    new[] { enemy },
                    tickIndex: 2));

                Assert.That(playbackPort.AttachedLoopCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.AttachedLoopCalls[0].Context.DebugTag, Is.EqualTo("ChargeActiveLoop"));

                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 1, EnemyChargePhase.Recover, startedRecoverThisTick: true),
                    }),
                    new[] { enemy },
                    tickIndex: 3));

                Assert.That(playbackPort.AttachedLoopCalls[0].Controller.StopCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_ChargeActiveLoop_StopsWhenActiveSignalDisappears()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ChargeActiveLoop_StopsWhenActiveSignalDisappears));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    CreateDefinitionSpec(loop: true),
                    attachmentSlotId: "charge-active-loop"));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(40, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 1, EnemyChargePhase.Active, startedActiveThisTick: true),
                    }),
                    new[] { enemy },
                    tickIndex: 1));

                presenter.Present(CreateTickResult(
                    CreatePresentationData(),
                    new[] { enemy },
                    tickIndex: 2));

                Assert.That(playbackPort.AttachedLoopCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.AttachedLoopCalls[0].Controller.StopCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_ChargeActiveLoop_RestartsWhenSequenceChanges()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ChargeActiveLoop_RestartsWhenSequenceChanges));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    CreateDefinitionSpec(loop: true),
                    attachmentSlotId: "charge-active-loop"));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(40, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 1, EnemyChargePhase.Active, startedActiveThisTick: true),
                    }),
                    new[] { enemy },
                    tickIndex: 1));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 2, EnemyChargePhase.Active, startedActiveThisTick: true),
                    }),
                    new[] { enemy },
                    tickIndex: 2));

                Assert.That(playbackPort.AttachedLoopCalls, Has.Count.EqualTo(2));
                Assert.That(playbackPort.AttachedLoopCalls[0].Controller.StopCount, Is.EqualTo(1));
                Assert.That(playbackPort.AttachedLoopCalls[1].Controller.StopCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentInitial_ChargeActiveLoop_StopsActiveLoop()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentInitial_ChargeActiveLoop_StopsActiveLoop));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    CreateDefinitionSpec(loop: true),
                    attachmentSlotId: "charge-active-loop"));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(40, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(enemy.entityId, sequence: 1, EnemyChargePhase.Active, startedActiveThisTick: true),
                    }),
                    new[] { enemy }));

                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));

                Assert.That(playbackPort.AttachedLoopCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.AttachedLoopCalls[0].Controller.StopCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_EnemyAudioDeathProfileSuppressesGenericEnemyDeath()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_EnemyAudioDeathProfileSuppressesGenericEnemyDeath));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Death, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(20, UnitRole.Enemy) }, topology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityExitSignals: new[]
                        {
                            new TickEntityExitPresentationSignal(
                                20,
                                TickEntityExitCause.EnemyDeath,
                                sourceCell,
                                topology,
                                Direction.Left,
                                EntityType.Unit),
                        }),
                    finalEntities: Array.Empty<EntityState>()));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Death" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_Present_LethalEnemyDamageWithEnemyDeathCueSuppressesCoreEnemyDamage()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_LethalEnemyDamageWithEnemyDeathCueSuppressesCoreEnemyDamage));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Death, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var lethalEnemy = CreateUnit(20, UnitRole.Enemy);
                var damagedEnemy = CreateUnit(21, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { lethalEnemy, damagedEnemy }, topology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        enemyDamageSignals: new[]
                        {
                            new TickEnemyDamagePresentationSignal(
                                lethalEnemy.entityId,
                                tookDamageThisTick: true,
                                damageAmount: 1),
                            new TickEnemyDamagePresentationSignal(
                                damagedEnemy.entityId,
                                tookDamageThisTick: true,
                                damageAmount: 1),
                        },
                        entityExitSignals: new[]
                        {
                            new TickEntityExitPresentationSignal(
                                lethalEnemy.entityId,
                                TickEntityExitCause.EnemyDeath,
                                sourceCell,
                                topology,
                                Direction.Left,
                                EntityType.Unit),
                        }),
                    finalEntities: new[] { damagedEnemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => (call.Context.OwnerEntityId, call.Context.DebugTag)).ToArray(),
                    Is.EqualTo(new[]
                    {
                        ((int?)damagedEnemy.entityId, "EnemyDamage"),
                        ((int?)lethalEnemy.entityId, "Death"),
                    }));
                Assert.That(
                    playbackPort.TwoDCalls.Any(call =>
                        call.Context.OwnerEntityId == lethalEnemy.entityId &&
                        call.Context.DebugTag == "EnemyDamage"),
                    Is.False);
                Assert.That(
                    playbackPort.TwoDCalls.Any(call => call.Context.DebugTag == "EntityExitEnemyDeath"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_NonLethalEnemyDamageKeepsCoreEnemyDamage()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_NonLethalEnemyDamageKeepsCoreEnemyDamage));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Death, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        enemyDamageSignals: new[]
                        {
                            new TickEnemyDamagePresentationSignal(
                                enemy.entityId,
                                tookDamageThisTick: true,
                                damageAmount: 1),
                        }),
                    finalEntities: new[] { enemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => (call.Context.OwnerEntityId, call.Context.DebugTag)).ToArray(),
                    Is.EqualTo(new[]
                    {
                        ((int?)enemy.entityId, "EnemyDamage"),
                    }));
                Assert.That(
                    playbackPort.TwoDCalls.Any(call => call.Context.DebugTag == "Death"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_LethalEnemyDamageWithoutEnemyDeathCueKeepsCoreDamageFeedback()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_LethalEnemyDamageWithoutEnemyDeathCueKeepsCoreDamageFeedback));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, topology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        enemyDamageSignals: new[]
                        {
                            new TickEnemyDamagePresentationSignal(
                                enemy.entityId,
                                tookDamageThisTick: true,
                                damageAmount: 1),
                        },
                        entityExitSignals: new[]
                        {
                            new TickEntityExitPresentationSignal(
                                enemy.entityId,
                                TickEntityExitCause.EnemyDeath,
                                sourceCell,
                                topology,
                                Direction.Left,
                                EntityType.Unit),
                        }),
                    finalEntities: Array.Empty<EntityState>()));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => (call.Context.OwnerEntityId, call.Context.DebugTag)).ToArray(),
                    Is.EqualTo(new[]
                    {
                        ((int?)enemy.entityId, "EnemyDamage"),
                        ((int?)enemy.entityId, "EntityExitEnemyDeath"),
                    }));
                Assert.That(
                    playbackPort.TwoDCalls.Any(call => call.Context.DebugTag == "Death"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyPrefabs_HaveValidEnemyAudioAuthoring()
        {
            foreach (var expectation in PrefabExpectations())
            {
                var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(expectation.Path);
                Assert.That(view, Is.Not.Null, $"Missing enemy view prefab at '{expectation.Path}'.");

                var authoring = view.GetComponent<EnemyAudioAuthoring>();
                Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAudioAuthoring)} on '{expectation.Path}'.");
                Assert.That(authoring.Profile, Is.Not.Null, $"Missing profile on '{expectation.Path}'.");
                Assert.DoesNotThrow(() => authoring.Validate());
                Assert.That(
                    Enum.GetValues(typeof(EnemyAudioCue))
                        .Cast<EnemyAudioCue>()
                        .Where(cue => cue != EnemyAudioCue.None)
                        .Any(cue => authoring.Profile.HasCue(cue)),
                    Is.True,
                    $"Expected '{expectation.Path}' to define at least one enemy audio cue.");
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAudioProductionReachabilityMatrix_AllProductionRequiredAndDisabledCuesAreGoverned()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(EnemyPresentationCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing enemy presentation catalog at '{EnemyPresentationCatalogPath}'.");

            foreach (var expectation in ProductionExpectations())
            {
                var catalogEntry = FindCatalogEntry(catalog, expectation.PresentationId);
                var expectedPrefabPath = $"{EnemyPrefabRoot}/{expectation.PrefabName}";
                var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(expectedPrefabPath);
                var binding = AssetDatabase.LoadAssetAtPath<EnemyAudioRequirementBinding>(expectation.BindingPath);

                Assert.That(prefab, Is.Not.Null, $"{expectation.Enemy}: missing production prefab '{expectedPrefabPath}'.");
                Assert.That(catalogEntry.ViewPrefab, Is.SameAs(prefab), $"{expectation.Enemy}: catalog does not use the expected production prefab.");
                Assert.That(binding, Is.Not.Null, $"{expectation.Enemy}: missing requirement binding '{expectation.BindingPath}'.");
                Assert.DoesNotThrow(() => binding.ValidateOrThrow(), $"{expectation.Enemy}: requirement binding must validate.");

                var authoring = prefab.GetComponent<EnemyAudioAuthoring>();
                Assert.That(authoring, Is.Not.Null, $"{expectation.Enemy}: prefab missing EnemyAudioAuthoring.");
                Assert.That(authoring.Profile, Is.Not.Null, $"{expectation.Enemy}: prefab missing EnemyAudioProfile.");
                Assert.That(binding.TargetProfile, Is.SameAs(authoring.Profile), $"{expectation.Enemy}: requirement binding target profile differs from prefab profile.");

                foreach (var cue in expectation.RequiredCues)
                {
                    Assert.That(
                        binding.GetEffectiveRequirement(cue),
                        Is.EqualTo(EnemyAudioCueRequirement.Required),
                        $"{expectation.Enemy}/{cue}: requirement policy must be Required.");
                    Assert.That(authoring.Profile.TryResolve(cue, out var resolvedBinding), Is.True, $"{expectation.Enemy}/{cue}: profile binding missing.");
                    Assert.That(resolvedBinding, Is.Not.Null, $"{expectation.Enemy}/{cue}: binding null.");
                    Assert.That(resolvedBinding.Definition, Is.Not.Null, $"{expectation.Enemy}/{cue}: definition null.");
                    Assert.That(resolvedBinding.Definition.Category, Is.EqualTo(AudioCategory.Sfx), $"{expectation.Enemy}/{cue}: wrong category.");
                    Assert.That(
                        resolvedBinding.Definition.Resolve(new AudioPlaybackContext()).Clip,
                        Is.Not.Null,
                        $"{expectation.Enemy}/{cue}: definition has no concrete clip.");

                    if (cue == EnemyAudioCue.ChargeActiveLoop)
                    {
                        Assert.That(resolvedBinding.Definition.Loop, Is.True, $"{expectation.Enemy}/{cue}: loop definition expected.");
                        Assert.That(
                            PlanEnemyAudioCues(CreateTypedReachabilityResult(cue)).Select(ResolveEnemyAudioCueKey).ToArray(),
                            Has.No.EqualTo(PresentationEnemyAudioCueKey.ChargeActiveLoop),
                            $"{expectation.Enemy}/{cue}: ChargeActiveLoop must not be planned by the one-shot projector.");
                    }
                    else
                    {
                        var typedCues = PlanEnemyAudioCues(CreateTypedReachabilityResult(cue));
                        var playbackPlan = CreateEnemyAudioPlaybackPlan(CreateTypedReachabilityResult(cue));
                        Assert.That(
                            typedCues.Select(ResolveEnemyAudioCueKey).ToArray(),
                            Has.Some.EqualTo(ToPresentationCueKey(cue)),
                            $"{expectation.Enemy}/{cue}: typed cue unreachable.");
                        Assert.That(
                            playbackPlan.Cues.Select(playbackCue => ResolveEnemyAudioCueKey(playbackCue.Cue)).ToArray(),
                            Has.Some.EqualTo(ToPresentationCueKey(cue)),
                            $"{expectation.Enemy}/{cue}: playback plan unreachable.");
                    }
                }

                foreach (var cue in expectation.DisabledCues)
                {
                    Assert.That(
                        binding.GetEffectiveRequirement(cue),
                        Is.EqualTo(EnemyAudioCueRequirement.Disabled),
                        $"{expectation.Enemy}/{cue}: requirement policy must be Disabled.");
                    Assert.That(authoring.Profile.HasCue(cue), Is.False, $"{expectation.Enemy}/{cue}: disabled cue must not carry a binding.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioProductionDisabledContracts_DoNotCreateConcreteOneShotPlayback()
        {
            AssertActualProductionProfileDoesNotPlayCue(
                $"{EnemyPrefabRoot}/EnemyView_JPeter.prefab",
                EnemyAudioCue.Windup,
                CreateTickResult(CreatePresentationData(enemyActionSignals: new[]
                {
                    CreateEnemyActionStartedSignal(60),
                }), new[] { CreateUnit(60, UnitRole.Enemy) }));
            AssertActualProductionProfileDoesNotPlayCue(
                $"{EnemyPrefabRoot}/EnemyView_RocketFace.prefab",
                EnemyAudioCue.Active,
                CreateTickResult(CreatePresentationData(enemyChargeSignals: new[]
                {
                    CreateChargeSignal(60, sequence: 1, phase: EnemyChargePhase.Active, startedActiveThisTick: true),
                }), new[] { CreateUnit(60, UnitRole.Enemy) }));
            AssertActualProductionProfileDoesNotPlayCue(
                $"{EnemyPrefabRoot}/EnemyView_Nebulous.prefab",
                EnemyAudioCue.PassiveContact,
                CreateTickResult(CreatePresentationData(enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        60,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.Executed),
                }), new[] { CreateUnit(60, UnitRole.Enemy) }));
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_PlayerHit_ResolvesForwardCellImpactToBlackEyePlasma()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);
            var expectedDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyePlasmaDefinitionPath);

            Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
            Assert.That(expectedDefinition, Is.Not.Null, $"Missing BlackEye plasma definition at '{BlackEyePlasmaDefinitionPath}'.");
            Assert.That(profile.TryResolve(EnemyAudioCue.ForwardCellImpact, out var binding), Is.True);
            Assert.That(binding.Definition, Is.SameAs(expectedDefinition));
            Assert.That(
                AssetDatabase.GetAssetPath(binding.Definition.Resolve(new AudioPlaybackContext()).Clip),
                Is.EqualTo(BlackEyePlasmaClipPath));
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_PlayerHit_DoesNotResolveForwardCellImpactToBlackEyeActive()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);
            var activeDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyeActiveDefinitionPath);

            Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
            Assert.That(activeDefinition, Is.Not.Null, $"Missing BlackEye active definition at '{BlackEyeActiveDefinitionPath}'.");
            Assert.That(profile.TryResolve(EnemyAudioCue.ForwardCellImpact, out var projectileBinding), Is.True);
            Assert.That(profile.TryResolve(EnemyAudioCue.Active, out var activeBinding), Is.True);
            Assert.That(projectileBinding.Definition, Is.Not.SameAs(activeDefinition));
            Assert.That(projectileBinding.Definition, Is.Not.SameAs(activeBinding.Definition));
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_PlayerHit_FinalPlaybackContainsBlackEyePlasma()
        {
            var rootObject = new GameObject(nameof(BlackEyeProjectile_PlayerHit_FinalPlaybackContainsBlackEyePlasma));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);
                var expectedDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyePlasmaDefinitionPath);
                var activeDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyeActiveDefinitionPath);
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var player = CreateUnit(10, UnitRole.Player, targetCell);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
                Assert.That(expectedDefinition, Is.Not.Null, $"Missing BlackEye plasma definition at '{BlackEyePlasmaDefinitionPath}'.");
                Assert.That(activeDefinition, Is.Not.Null, $"Missing BlackEye active definition at '{BlackEyeActiveDefinitionPath}'.");

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player, enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        enemyActionSignals: new[]
                        {
                            CreateEnemyActionExecutedSignal(
                                enemy.entityId,
                                EnemyActionPresentationSource.ForwardCellImpact,
                                EnemyActionPresentationOutcome.Executed),
                        },
                        forwardCellImpactSignals: new[]
                        {
                            CreateForwardCellImpactSignal(
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetCell: targetCell,
                                hit: true,
                                targetEntityId: player.entityId),
                        },
                        forwardCellProjectileArrivalSignals: new[]
                        {
                            CreateForwardCellProjectileArrivalSignal(
                                targetCell,
                                PendingCellImpactResolutionKind.Hit,
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetEntityId: player.entityId),
                        },
                        playerDamageSignals: new[]
                        {
                            new TickPlayerDamagePresentationSignal(
                                player.entityId,
                                tookDamageThisTick: true,
                                damageAmount: 1),
                        }),
                    finalEntities: new[] { player, enemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "PlayerDamage", "ForwardCellImpact" }));
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Definition), Has.No.SameAs(activeDefinition));

                var projectileCall = playbackPort.TwoDCalls.Single(call => call.Context.DebugTag == "ForwardCellImpact");
                Assert.That(projectileCall.Definition, Is.SameAs(expectedDefinition));
                Assert.That(projectileCall.Definition, Is.Not.SameAs(activeDefinition));
                Assert.That(
                    AssetDatabase.GetAssetPath(projectileCall.Definition.Resolve(new AudioPlaybackContext()).Clip),
                    Is.EqualTo(BlackEyePlasmaClipPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyTransition_AndForwardCellImpactAudioInSamePresent_IsDeferredUntilTransitionCompletes()
        {
            var rootObject = new GameObject(nameof(TopologyTransition_AndForwardCellImpactAudioInSamePresent_IsDeferredUntilTransitionCompletes));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.ForwardCellImpact, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var player = CreateUnit(10, UnitRole.Player, targetCell);
                var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Front, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player, enemy }, sourceTopology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        topologyMotion: new TickTopologyMotion(
                            sourceTopology,
                            destinationTopology,
                            CubeRotationKind.Forward),
                        forwardCellImpactSignals: new[]
                        {
                            CreateForwardCellImpactSignal(
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetCell: targetCell,
                                hit: true,
                                targetEntityId: player.entityId),
                        },
                        forwardCellProjectileArrivalSignals: new[]
                        {
                            CreateForwardCellProjectileArrivalSignal(
                                targetCell,
                                PendingCellImpactResolutionKind.Hit,
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetEntityId: player.entityId),
                        },
                        playerDamageSignals: new[]
                        {
                            new TickPlayerDamagePresentationSignal(
                                player.entityId,
                                tookDamageThisTick: true,
                                damageAmount: 1),
                        }),
                    finalEntities: new[] { player, enemy },
                    finalTopology: destinationTopology));

                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(presenter.DeferredGameplayAudioRequestCount, Is.EqualTo(2));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);

                presenter.UpdatePresentation(0.05f);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(playbackPort.TwoDCalls, Is.Empty);

                presenter.UpdatePresentation(0.2f);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.DeferredGameplayAudioRequestCount, Is.Zero);
                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "PlayerDamage", "ForwardCellImpact" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyTransition_ForwardCellImpactAudioDeferred_DoesNotDuplicateOnRepeatedPresentationUpdate()
        {
            var rootObject = new GameObject(nameof(TopologyTransition_ForwardCellImpactAudioDeferred_DoesNotDuplicateOnRepeatedPresentationUpdate));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.ForwardCellImpact, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Front, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, sourceTopology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        topologyMotion: new TickTopologyMotion(
                            sourceTopology,
                            destinationTopology,
                            CubeRotationKind.Forward),
                        forwardCellImpactSignals: new[]
                        {
                            CreateForwardCellImpactSignal(
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetCell: targetCell,
                                hit: true,
                                targetEntityId: 10),
                        },
                        forwardCellProjectileArrivalSignals: new[]
                        {
                            CreateForwardCellProjectileArrivalSignal(
                                targetCell,
                                PendingCellImpactResolutionKind.Hit,
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetEntityId: 10),
                        }),
                    finalEntities: new[] { enemy },
                    finalTopology: destinationTopology));

                Assert.That(playbackPort.TwoDCalls, Is.Empty);

                presenter.UpdatePresentation(0.2f);
                presenter.UpdatePresentation(0.2f);

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "ForwardCellImpact" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_PlayerHit_FinalPlaybackDoesNotContainBlackEyeActiveWhenActiveIsImpactDerived()
        {
            var rootObject = new GameObject(nameof(BlackEyeProjectile_PlayerHit_FinalPlaybackDoesNotContainBlackEyeActiveWhenActiveIsImpactDerived));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);
                var activeDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyeActiveDefinitionPath);
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var player = CreateUnit(10, UnitRole.Player, targetCell);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
                Assert.That(activeDefinition, Is.Not.Null, $"Missing BlackEye active definition at '{BlackEyeActiveDefinitionPath}'.");

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player, enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        enemyActionSignals: new[]
                        {
                            CreateEnemyActionExecutedSignal(
                                enemy.entityId,
                                EnemyActionPresentationSource.ForwardCellImpact,
                                EnemyActionPresentationOutcome.Executed),
                        },
                        forwardCellImpactSignals: new[]
                        {
                            CreateForwardCellImpactSignal(
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetCell: targetCell,
                                hit: true,
                                targetEntityId: player.entityId),
                        },
                        forwardCellProjectileArrivalSignals: new[]
                        {
                            CreateForwardCellProjectileArrivalSignal(
                                targetCell,
                                PendingCellImpactResolutionKind.Hit,
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetEntityId: player.entityId),
                        }),
                    finalEntities: new[] { player, enemy }));

                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag), Has.No.EqualTo("Active"));
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Definition), Has.No.SameAs(activeDefinition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_FireRelease_StillEmitsBlackEyeActive()
        {
            var rootObject = new GameObject(nameof(BlackEyeProjectile_FireRelease_StillEmitsBlackEyeActive));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);
                var activeDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyeActiveDefinitionPath);
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
                Assert.That(activeDefinition, Is.Not.Null, $"Missing BlackEye active definition at '{BlackEyeActiveDefinitionPath}'.");

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        enemyActionSignals: new[]
                        {
                            CreateEnemyActionExecutedSignal(
                                enemy.entityId,
                                EnemyActionPresentationSource.Combat,
                                EnemyActionPresentationOutcome.Executed),
                        }),
                    finalEntities: new[] { enemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Active" }));
                Assert.That(playbackPort.TwoDCalls.Single().Definition, Is.SameAs(activeDefinition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BlackEyeProjectile_Miss_FinalPlaybackStillContainsBlackEyePlasma()
        {
            var rootObject = new GameObject(nameof(BlackEyeProjectile_Miss_FinalPlaybackStillContainsBlackEyePlasma));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);
                var expectedDefinition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(BlackEyePlasmaDefinitionPath);
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
                Assert.That(expectedDefinition, Is.Not.Null, $"Missing BlackEye plasma definition at '{BlackEyePlasmaDefinitionPath}'.");

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        forwardCellProjectileArrivalSignals: new[]
                        {
                            CreateForwardCellProjectileArrivalSignal(
                                targetCell,
                                PendingCellImpactResolutionKind.Miss,
                                ownerId: enemy.entityId,
                                sourceEnemyId: enemy.entityId,
                                targetEntityId: 10),
                        }),
                    finalEntities: new[] { enemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "ForwardCellImpact" }));
                Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(expectedDefinition));
                Assert.That(
                    AssetDatabase.GetAssetPath(playbackPort.TwoDCalls[0].Definition.Resolve(new AudioPlaybackContext()).Clip),
                    Is.EqualTo(BlackEyePlasmaClipPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BlackEyeAudioProfile_UsesActForActiveAndPlasmaForForwardCellImpact()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(BlackEyeAudioProfilePath);

            Assert.That(profile, Is.Not.Null, $"Missing BlackEye audio profile at '{BlackEyeAudioProfilePath}'.");
            Assert.That(profile.HasCue(EnemyAudioCue.Windup), Is.False);
            Assert.That(profile.HasCue(EnemyAudioCue.Active), Is.True);
            Assert.That(profile.HasCue(EnemyAudioCue.ForwardCellImpact), Is.True);
        }

        [Test]
        [Category("Full")]
        public void DrSaturnAudioProfile_MoveRandomizesMoveAndActClips()
        {
            const string moveClipPath =
                "Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Dr.saturn_move.wav";
            const string actClipPath =
                "Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Dr.saturn_act.wav";

            var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(DrSaturnAudioProfilePath);
            var moveClip = AssetDatabase.LoadAssetAtPath<AudioClip>(moveClipPath);
            var actClip = AssetDatabase.LoadAssetAtPath<AudioClip>(actClipPath);

            Assert.That(profile, Is.Not.Null, $"Missing Dr.Saturn audio profile at '{DrSaturnAudioProfilePath}'.");
            Assert.That(profile.name, Is.EqualTo("EnemyAudioProfile_DrSaturn"));
            Assert.That(AssetDatabase.AssetPathToGUID(DrSaturnAudioProfilePath), Is.EqualTo(DrSaturnAudioProfileGuid));
            Assert.That(profile.HasCue(EnemyAudioCue.Move), Is.True);
            Assert.That(profile.HasCue(EnemyAudioCue.Windup), Is.True);
            Assert.That(profile.HasCue(EnemyAudioCue.Active), Is.True);
            Assert.That(profile.HasCue(EnemyAudioCue.Recover), Is.True);
            Assert.That(profile.HasCue(EnemyAudioCue.Death), Is.True);
            Assert.That(profile.HasCue(EnemyAudioCue.Landing), Is.False);
            Assert.That(profile.HasCue(EnemyAudioCue.ForwardCellImpact), Is.False);
            Assert.That(profile.HasCue(EnemyAudioCue.ChargeActiveLoop), Is.False);
            Assert.That(profile.HasCue(EnemyAudioCue.StationaryActive), Is.False);
            Assert.That(profile.HasCue(EnemyAudioCue.PassiveContact), Is.False);
            Assert.That(moveClip, Is.Not.Null, $"Missing Dr.Saturn move clip at '{moveClipPath}'.");
            Assert.That(actClip, Is.Not.Null, $"Missing Dr.Saturn act clip at '{actClipPath}'.");
            Assert.That(profile.TryResolve(EnemyAudioCue.Move, out var moveBinding), Is.True);
            Assert.That(moveBinding.Definition, Is.TypeOf<RandomAudioDefinition>());

            var definitionObject = new SerializedObject(moveBinding.Definition);
            var clipsProperty = definitionObject.FindProperty("clips");

            Assert.That(clipsProperty, Is.Not.Null);
            Assert.That(clipsProperty.arraySize, Is.EqualTo(2));
            Assert.That(
                ResolveRandomDefinitionClips(clipsProperty),
                Is.EquivalentTo(new[] { moveClip, actClip }));
        }

        [Test]
        [Category("Full")]
        public void SecBotAudioProfile_StationaryActive_ReusesSecBotMoveDefinition()
        {
            const string profilePath =
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioProfiles/EnemyAudioProfile_SecBot.asset";
            const string definitionPath =
                "Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/SecBot_Move_Def.asset";

            var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(profilePath);
            var definition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(definitionPath);

            Assert.That(profile, Is.Not.Null, $"Missing SecBot audio profile at '{profilePath}'.");
            Assert.That(definition, Is.Not.Null, $"Missing SecBot move definition at '{definitionPath}'.");
            Assert.That(profile.TryResolve(EnemyAudioCue.Move, out var moveBinding), Is.True);
            Assert.That(profile.TryResolve(EnemyAudioCue.StationaryActive, out var stationaryBinding), Is.True);
            Assert.That(moveBinding.Definition, Is.SameAs(definition));
            Assert.That(stationaryBinding.Definition, Is.SameAs(definition));
        }

        [Test]
        [Category("Full")]
        public void PassiveContactOverlapTargets_DoNotAuthorPassiveContactCueByDefault()
        {
            AssertEnemyPrefabProfileDoesNotHaveCue(
                $"{EnemyPrefabRoot}/EnemyView_JPeter.prefab",
                EnemyAudioCue.PassiveContact);
            AssertEnemyPrefabProfileDoesNotHaveCue(
                $"{EnemyPrefabRoot}/EnemyView_DrSaturn.prefab",
                EnemyAudioCue.PassiveContact);
            AssertEnemyPrefabProfileDoesNotHaveCue(
                $"{EnemyPrefabRoot}/EnemyView_Nebulous.prefab",
                EnemyAudioCue.PassiveContact);
        }

        [Test]
        [Category("Core")]
        public void StartisAudioProfile_BindsPassiveContactForNonAttackingProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(StartisAudioProfilePath);
            var definition = AssetDatabase.LoadAssetAtPath<AudioDefinition>(StartisPassiveContactDefinitionPath);

            Assert.That(profile, Is.Not.Null, $"Missing Startis audio profile at '{StartisAudioProfilePath}'.");
            Assert.That(definition, Is.Not.Null, $"Missing Startis passive-contact definition at '{StartisPassiveContactDefinitionPath}'.");
            Assert.That(profile.TryResolve(EnemyAudioCue.PassiveContact, out var binding), Is.True);
            Assert.That(binding.Definition, Is.SameAs(definition));
            Assert.That(definition.Category, Is.EqualTo(AudioCategory.Sfx));
            Assert.That(definition.Loop, Is.False);
        }

        private static void AssertEnemyPrefabProfileDoesNotHaveCue(string prefabPath, EnemyAudioCue cue)
        {
            var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(view, Is.Not.Null, $"Missing enemy view prefab at '{prefabPath}'.");

            var authoring = view.GetComponent<EnemyAudioAuthoring>();
            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAudioAuthoring)} on '{prefabPath}'.");
            Assert.That(authoring.Profile, Is.Not.Null, $"Missing profile on '{prefabPath}'.");
            Assert.That(authoring.Profile.HasCue(cue), Is.False, $"'{prefabPath}' should not author '{cue}'.");
        }

        private static void AssertActualProductionProfileDoesNotPlayCue(
            string prefabPath,
            EnemyAudioCue cue,
            TickResult result)
        {
            var rootObject = new GameObject(nameof(AssertActualProductionProfileDoesNotPlayCue));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var profile = LoadEnemyAudioProfileFromPrefab(prefabPath);
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(60, UnitRole.Enemy) }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(result);

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Has.No.EqualTo(cue.ToString()),
                    $"{prefabPath}: disabled cue '{cue}' should not create concrete playback.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static EnemyAudioProfile LoadEnemyAudioProfileFromPrefab(string prefabPath)
        {
            var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(view, Is.Not.Null, $"Missing enemy view prefab at '{prefabPath}'.");

            var authoring = view.GetComponent<EnemyAudioAuthoring>();
            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAudioAuthoring)} on '{prefabPath}'.");
            Assert.That(authoring.Profile, Is.Not.Null, $"Missing profile on '{prefabPath}'.");
            return authoring.Profile;
        }

        private static EnemyPresentationCatalogEntry FindCatalogEntry(
            EnemyPresentationCatalog catalog,
            string presentationId)
        {
            var normalizedPresentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId);
            foreach (var entry in catalog.Entries)
            {
                if (EnemyPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId) == normalizedPresentationId)
                {
                    return entry;
                }
            }

            Assert.Fail($"Missing enemy presentation catalog entry '{presentationId}'.");
            return default;
        }

        private static IReadOnlyList<EnemyPrefabExpectation> PrefabExpectations()
        {
            return new[]
            {
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Sunwheel.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Astreton.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Landing,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_BlackEye.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Active,
                    EnemyAudioCue.ForwardCellImpact,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_DrSaturn.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Windup,
                    EnemyAudioCue.Active,
                    EnemyAudioCue.Recover,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_JPeter.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Active,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Startis.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.PassiveContact,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Nebulous.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Windup,
                    EnemyAudioCue.Active,
                    EnemyAudioCue.Recover,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_RocketFace.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.ChargeActiveLoop,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_SecBot.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.StationaryActive,
                    EnemyAudioCue.Death),
            };
        }

        private static IReadOnlyList<ProductionEnemyAudioExpectation> ProductionExpectations()
        {
            return new[]
            {
                new ProductionEnemyAudioExpectation(
                    "WallFollowerSun / Sunwheel",
                    "sunwheel",
                    "EnemyView_Sunwheel.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_WallFollowerSun.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.Death },
                    Array.Empty<EnemyAudioCue>()),
                new ProductionEnemyAudioExpectation(
                    "JumpChaserAstra / Astreton",
                    "astreton",
                    "EnemyView_Astreton.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_JumpChaserAstra.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.Landing, EnemyAudioCue.Death },
                    Array.Empty<EnemyAudioCue>()),
                new ProductionEnemyAudioExpectation(
                    "BlackEye",
                    "black_eye",
                    "EnemyView_BlackEye.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_BlackEye.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.Active, EnemyAudioCue.ForwardCellImpact, EnemyAudioCue.Death },
                    Array.Empty<EnemyAudioCue>()),
                new ProductionEnemyAudioExpectation(
                    "DrSaturn",
                    "dr_saturn",
                    "EnemyView_DrSaturn.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_DrSaturn.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.Windup, EnemyAudioCue.Active, EnemyAudioCue.Recover, EnemyAudioCue.Death },
                    Array.Empty<EnemyAudioCue>()),
                new ProductionEnemyAudioExpectation(
                    "UtilitySummoner / JPeter",
                    "j_peter",
                    "EnemyView_JPeter.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_UtilitySummoner.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.Active, EnemyAudioCue.Death },
                    new[] { EnemyAudioCue.Windup }),
                new ProductionEnemyAudioExpectation(
                    "Nebulous",
                    "nebulous",
                    "EnemyView_Nebulous.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_Nebulous.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.Windup, EnemyAudioCue.Active, EnemyAudioCue.Recover, EnemyAudioCue.Death },
                    new[] { EnemyAudioCue.PassiveContact }),
                new ProductionEnemyAudioExpectation(
                    "RocketFace",
                    "rocket_face",
                    "EnemyView_RocketFace.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_RocketFace.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.ChargeActiveLoop, EnemyAudioCue.Death },
                    new[] { EnemyAudioCue.Active }),
                new ProductionEnemyAudioExpectation(
                    "SecBot",
                    "secbot",
                    "EnemyView_SecBot.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_SecBot.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.StationaryActive, EnemyAudioCue.Death },
                    Array.Empty<EnemyAudioCue>()),
                new ProductionEnemyAudioExpectation(
                    "Startis",
                    "startis",
                    "EnemyView_Startis.prefab",
                    $"{EnemyAudioRequirementBindingRoot}/EnemyAudioRequirementBinding_Startis.asset",
                    new[] { EnemyAudioCue.Move, EnemyAudioCue.PassiveContact, EnemyAudioCue.Death },
                    Array.Empty<EnemyAudioCue>()),
            };
        }

        private static GameplayTickViewPresenter CreatePresenter(GameObject rootObject, IGameplayEntityViewFactory viewFactory)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, viewFactory);
            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                CreateTimingProfile());
            return presenter;
        }

        private static PresentationPlaybackPlan CreateEnemyAudioPlaybackPlan(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyAudioCuePlanner(),
            }).Plan(factFrame);
            return new PresentationPlaybackPlanner().Plan(cueFrame);
        }

        private static IReadOnlyList<PresentationFact> ExtractEnemyAudioFacts(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            return factFrame.Facts
                .Where(fact => fact.Kind == PresentationFactKind.EnemyAudio)
                .ToArray();
        }

        private static IReadOnlyList<PresentationCue> PlanEnemyAudioCues(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyAudioCuePlanner(),
            }).Plan(factFrame);
            return cueFrame.Cues
                .Where(cue => cue.Domain == PresentationDomain.EnemyAudio)
                .ToArray();
        }

        private static int CountTypedCue(TickResult result, PresentationEnemyAudioCueKey cueKey)
        {
            return PlanEnemyAudioCues(result)
                .Count(cue => ResolveEnemyAudioCueKey(cue) == cueKey);
        }

        private static TickResult CreateTypedReachabilityResult(EnemyAudioCue cue)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var enemy = CreateUnit(60, UnitRole.Enemy);

            switch (cue)
            {
                case EnemyAudioCue.Move:
                    return CreateTickResult(
                        CreatePresentationData(entityMotions: new[]
                        {
                            new TickEntityMotion(enemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        }),
                        new[] { enemy });
                case EnemyAudioCue.Death:
                    return CreateTickResult(CreatePresentationData(entityExitSignals: new[]
                    {
                        new TickEntityExitPresentationSignal(
                            enemy.entityId,
                            TickEntityExitCause.EnemyDeath,
                            sourceCell,
                            topology,
                            Direction.Left,
                            EntityType.Unit),
                    }));
                case EnemyAudioCue.Windup:
                    return CreateTickResult(CreatePresentationData(enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            enemy.entityId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 2),
                    }));
                case EnemyAudioCue.Landing:
                    return CreateTickResult(CreatePresentationData(enemyJumpSignals: new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            enemy.entityId,
                            sequence: 1,
                            EnemyJumpPhase.Cooldown,
                            startedWindupThisTick: false,
                            startedAirborneThisTick: false,
                            landedThisTick: true,
                            retryThisTick: false,
                            sourceCell: sourceCell,
                            lockedTargetCell: targetCell,
                            presentationTargetCell: targetCell,
                            facing: Direction.Right,
                            windupTicks: 0,
                            landingTick: 1,
                            remainingAirborneTicks: 0,
                            retryCount: 0,
                            TickEnemyJumpPresentationOutcome.Landed),
                    }));
                case EnemyAudioCue.Active:
                    return CreateTickResult(CreatePresentationData(enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            enemy.entityId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.ActiveStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 2),
                    }));
                case EnemyAudioCue.Recover:
                    return CreateTickResult(CreatePresentationData(enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            enemy.entityId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.RecoverStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 1),
                    }));
                case EnemyAudioCue.ForwardCellImpact:
                    return CreateTickResult(CreatePresentationData(forwardCellProjectileArrivalSignals: new[]
                    {
                        CreateForwardCellProjectileArrivalSignal(
                            targetCell,
                            PendingCellImpactResolutionKind.Hit,
                            ownerId: enemy.entityId,
                            sourceEnemyId: enemy.entityId,
                            targetEntityId: 10),
                    }));
                case EnemyAudioCue.ChargeActiveLoop:
                    return CreateTickResult(CreatePresentationData(enemyChargeSignals: new[]
                    {
                        CreateChargeSignal(
                            enemy.entityId,
                            sequence: 1,
                            phase: EnemyChargePhase.Active,
                            startedActiveThisTick: true),
                    }));
                case EnemyAudioCue.StationaryActive:
                    return CreateTickResult(CreatePresentationData(), new[] { enemy });
                case EnemyAudioCue.PassiveContact:
                    return CreateTickResult(CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            enemy.entityId,
                            EnemyActionPresentationSource.PassiveContact,
                            EnemyActionPresentationOutcome.Executed),
                    }));
                default:
                    return CreateTickResult(TickPresentationData.Empty);
            }
        }

        private static PresentationEnemyAudioCueKey ResolveEnemyAudioCueKey(PresentationCue cue)
        {
            Assert.That(cue.Key.TryGetEnemyAudioCueKey(out var cueKey), Is.True);
            return cueKey;
        }

        private static PresentationEnemyAudioCueKey ToPresentationCueKey(EnemyAudioCue cue)
        {
            return cue switch
            {
                EnemyAudioCue.Move => PresentationEnemyAudioCueKey.Move,
                EnemyAudioCue.Death => PresentationEnemyAudioCueKey.Death,
                EnemyAudioCue.Windup => PresentationEnemyAudioCueKey.Windup,
                EnemyAudioCue.Landing => PresentationEnemyAudioCueKey.Landing,
                EnemyAudioCue.Active => PresentationEnemyAudioCueKey.Active,
                EnemyAudioCue.Recover => PresentationEnemyAudioCueKey.Recover,
                EnemyAudioCue.ForwardCellImpact => PresentationEnemyAudioCueKey.ForwardCellImpact,
                EnemyAudioCue.ChargeActiveLoop => PresentationEnemyAudioCueKey.ChargeActiveLoop,
                EnemyAudioCue.StationaryActive => PresentationEnemyAudioCueKey.StationaryActive,
                EnemyAudioCue.PassiveContact => PresentationEnemyAudioCueKey.PassiveContact,
                _ => PresentationEnemyAudioCueKey.None,
            };
        }

        private static (
            int OwnerEntityId,
            EnemyAudioSemanticCue Cue,
            EnemyAudioSemanticOriginKind OriginKind,
            EnemyAudioSemanticPhase Phase,
            int TickIndex,
            int SourceSequenceId,
            int TargetEntityId,
            int ImpactTick,
            int ImpactId,
            int PresentationKey,
            int Timing) ToSemanticEventKey(EnemyAudioSemanticEvent semanticEvent)
        {
            return (
                semanticEvent.OwnerEntityId,
                semanticEvent.Cue,
                semanticEvent.OriginKind,
                semanticEvent.Phase,
                semanticEvent.TickIndex,
                semanticEvent.SourceSequenceId,
                semanticEvent.TargetEntityId,
                semanticEvent.ImpactTick,
                semanticEvent.ImpactId,
                semanticEvent.PresentationKey,
                semanticEvent.Timing);
        }

        private static (
            int OwnerEntityId,
            PresentationEnemyAudioCueKey Cue,
            PresentationEnemyAudioOriginKind OriginKind,
            PresentationEnemyAudioPhase Phase,
            int SourceSequenceId,
            int TargetEntityId,
            int ImpactTick,
            int ImpactId,
            int PresentationKey,
            int Timing) ToFactKey(PresentationFact fact)
        {
            return (
                fact.EnemyAudioPayload.OwnerEntityId,
                (PresentationEnemyAudioCueKey)fact.EnemyAudioPayload.CueKey,
                fact.EnemyAudioPayload.OriginKind,
                fact.EnemyAudioPayload.Phase,
                fact.EnemyAudioPayload.SourceSequenceId,
                fact.EnemyAudioPayload.TargetEntityId,
                fact.EnemyAudioPayload.ImpactTick,
                fact.EnemyAudioPayload.ImpactId,
                fact.EnemyAudioPayload.PresentationKey,
                fact.EnemyAudioPayload.Timing);
        }

        private static (
            int OwnerEntityId,
            PresentationEnemyAudioCueKey Cue,
            int DedupeKey,
            int CancellationKey,
            int CooldownKey) ToPlaybackKey(PresentationPlaybackCue playbackCue)
        {
            return (
                playbackCue.Cue.EnemyAudioPayload.OwnerEntityId,
                ResolveEnemyAudioCueKey(playbackCue.Cue),
                playbackCue.Policy.DedupeKey,
                playbackCue.Policy.CancellationKey,
                playbackCue.Policy.CooldownKey);
        }

        private static (
            int OwnerEntityId,
            PresentationEnemyAudioCueKey Cue,
            PresentationEnemyAudioOriginKind OriginKind,
            PresentationEnemyAudioPhase Phase,
            int SourceSequenceId,
            int TargetEntityId,
            int ImpactTick,
            int ImpactId,
            int PresentationKey,
            int Timing) ToSemanticCueKey(PresentationCue cue)
        {
            return (
                cue.EnemyAudioPayload.OwnerEntityId,
                ResolveEnemyAudioCueKey(cue),
                cue.EnemyAudioPayload.OriginKind,
                cue.EnemyAudioPayload.Phase,
                cue.EnemyAudioPayload.SourceSequenceId,
                cue.EnemyAudioPayload.TargetEntityId,
                cue.EnemyAudioPayload.ImpactTick,
                cue.EnemyAudioPayload.ImpactId,
                cue.EnemyAudioPayload.PresentationKey,
                cue.EnemyAudioPayload.Timing);
        }

        private static (
            int OwnerEntityId,
            PresentationEnemyAudioCueKey Cue,
            PresentationEnemyAudioOriginKind OriginKind,
            PresentationEnemyAudioPhase Phase,
            int SourceSequenceId,
            int TargetEntityId,
            int ImpactTick,
            int ImpactId,
            int PresentationKey,
            int Timing) ToSemanticRequestKey(EnemyAudioRequest request)
        {
            return (
                request.OwnerEntityId,
                (PresentationEnemyAudioCueKey)request.Cue,
                ResolveRequestOriginKind(request.SemanticEvent.OriginKind),
                ResolveRequestPhase(request.SemanticEvent.Phase),
                request.SemanticEvent.SourceSequenceId,
                request.SemanticEvent.TargetEntityId,
                request.SemanticEvent.ImpactTick,
                request.SemanticEvent.ImpactId,
                request.SemanticEvent.PresentationKey,
                request.SemanticEvent.Timing);
        }

        private static PresentationEnemyAudioOriginKind ResolveRequestOriginKind(
            EnemyAudioSemanticOriginKind originKind)
        {
            return originKind switch
            {
                EnemyAudioSemanticOriginKind.Action => PresentationEnemyAudioOriginKind.Action,
                EnemyAudioSemanticOriginKind.Jump => PresentationEnemyAudioOriginKind.Jump,
                EnemyAudioSemanticOriginKind.Charge => PresentationEnemyAudioOriginKind.Charge,
                EnemyAudioSemanticOriginKind.Death => PresentationEnemyAudioOriginKind.Death,
                EnemyAudioSemanticOriginKind.ForwardCellImpact => PresentationEnemyAudioOriginKind.ForwardCellImpact,
                EnemyAudioSemanticOriginKind.Utility => PresentationEnemyAudioOriginKind.Utility,
                EnemyAudioSemanticOriginKind.Glide => PresentationEnemyAudioOriginKind.Glide,
                EnemyAudioSemanticOriginKind.Summon => PresentationEnemyAudioOriginKind.Summon,
                EnemyAudioSemanticOriginKind.Move => PresentationEnemyAudioOriginKind.Move,
                EnemyAudioSemanticOriginKind.Stationary => PresentationEnemyAudioOriginKind.Stationary,
                _ => PresentationEnemyAudioOriginKind.None,
            };
        }

        private static PresentationEnemyAudioPhase ResolveRequestPhase(EnemyAudioSemanticPhase phase)
        {
            return phase switch
            {
                EnemyAudioSemanticPhase.Windup => PresentationEnemyAudioPhase.Windup,
                EnemyAudioSemanticPhase.Active => PresentationEnemyAudioPhase.Active,
                EnemyAudioSemanticPhase.Recover => PresentationEnemyAudioPhase.Recover,
                EnemyAudioSemanticPhase.Landing => PresentationEnemyAudioPhase.Landing,
                EnemyAudioSemanticPhase.Death => PresentationEnemyAudioPhase.Death,
                EnemyAudioSemanticPhase.Impact => PresentationEnemyAudioPhase.Impact,
                EnemyAudioSemanticPhase.Move => PresentationEnemyAudioPhase.Move,
                EnemyAudioSemanticPhase.StationaryActive => PresentationEnemyAudioPhase.StationaryActive,
                _ => PresentationEnemyAudioPhase.None,
            };
        }

        private static PresentationPlaybackPlan CreateManualEnemyAudioPlaybackPlan(PresentationEnemyAudioCueKey cueKey)
        {
            var ownerEntityId = 60;
            var cue = new PresentationCue(
                PresentationDomain.EnemyAudio,
                PresentationCueKey.ForEnemyAudio(cueKey),
                new PresentationSource(21, PresentationSemanticSource.EnemyCharge, ownerEntityId, sourceSequence: 4),
                PresentationTarget.Entity(ownerEntityId),
                PresentationAnchor.ForEntityVisualRoot(ownerEntityId),
                PresentationPlaybackPolicyHint.OneShot(2104),
                enemyAudioPayload: new PresentationEnemyAudioPayload(
                    ownerEntityId,
                    (int)cueKey,
                    sourceTickIndex: 21,
                    sourceSequenceId: 4,
                    PresentationEnemyAudioOriginKind.Charge,
                    PresentationEnemyAudioPhase.Active));
            return new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                21,
                new[] { cue },
                default));
        }

        private static void AssertBridgeAdapterMissingResult(
            Func<Transform, IGameplayEntityViewFactory> viewFactoryFactory,
            IReadOnlyList<EntityState> finalEntities,
            Func<GameplayEnemyAudioExecutorDiagnostics, int> expected)
        {
            var rootObject = new GameObject(nameof(AssertBridgeAdapterMissingResult));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject, viewFactoryFactory(rootObject.transform));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.ConfigureEnemyAudioExecution(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(finalEntities, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(enemyActionSignals: new[]
                    {
                        CreateEnemyActionExecutedSignal(
                            60,
                            EnemyActionPresentationSource.Combat,
                            EnemyActionPresentationOutcome.Executed),
                    }),
                    finalEntities,
                    tickIndex: 13));

                Assert.That(expected(presenter.EnemyAudioExecutorDiagnostics), Is.EqualTo(1));
                Assert.That(presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static void AssertEnemyAudioExecutorPortResult(
            GameplayEnemyAudioPlaybackResultKind resultKind,
            Func<GameplayEnemyAudioExecutorDiagnostics, int> expected,
            EnemyAudioTelemetryFailureReason expectedFailureReason)
        {
            var playbackPlan = CreateEnemyAudioPlaybackPlan(CreateTickResult(
                CreatePresentationData(enemyActionSignals: new[]
                {
                    CreateEnemyActionExecutedSignal(
                        60,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.Executed),
                })));
            var executor = new GameplayEnemyAudioPresentationExecutor(
                new RecordingEnemyAudioPlaybackPort(resultKind),
                EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge,
                new EnemyAudioExecutionGuard(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));

            executor.Play(playbackPlan);

            Assert.That(expected(executor.Diagnostics), Is.EqualTo(1));
            Assert.That(executor.Diagnostics.LastFailureReason, Is.EqualTo(expectedFailureReason));
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

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null,
            int tickIndex = 1,
            CubeTopologyState? finalTopology = null)
        {
            return new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                Array.Empty<string>(),
                finalTopology ?? new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static TickPresentationData CreatePresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions = null,
            IReadOnlyList<TickEnemyActionPresentationSignal> enemyActionSignals = null,
            IReadOnlyList<TickEnemyJumpPresentationSignal> enemyJumpSignals = null,
            IReadOnlyList<TickEnemyChargePresentationSignal> enemyChargeSignals = null,
            IReadOnlyList<TickEnemyUtilityPresentationSignal> enemyUtilitySignals = null,
            IReadOnlyList<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals = null,
            IReadOnlyList<TickKinematicMotionTrack> kinematicMotionTracks = null,
            IReadOnlyList<TickVisibilityChange> visibilityChanges = null,
            IReadOnlyList<TickSummonedEnemyPresentationBinding> summonedEnemyPresentationBindings = null,
            IReadOnlyList<TickForwardCellImpactPresentationSignal> forwardCellImpactSignals = null,
            IReadOnlyList<TickForwardCellProjectileArrivalPresentationSignal> forwardCellProjectileArrivalSignals = null,
            IReadOnlyList<TickEnemyGlidePresentationSignal> enemyGlideSignals = null,
            IReadOnlyList<TickEnemyDamagePresentationSignal> enemyDamageSignals = null,
            IReadOnlyList<TickPlayerDamagePresentationSignal> playerDamageSignals = null,
            TickTopologyMotion? topologyMotion = null)
        {
            return new TickPresentationData(
                entityMotions ?? Array.Empty<TickEntityMotion>(),
                topologyMotion,
                visibilityChanges ?? Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals ?? Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals ?? Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals ?? Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals ?? Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals ?? Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: summonedEnemyPresentationBindings ?? Array.Empty<TickSummonedEnemyPresentationBinding>(),
                summonWindupWarnings: summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>(),
                kinematicMotionTracks: kinematicMotionTracks ?? Array.Empty<TickKinematicMotionTrack>(),
                enemyUtilitySignals: enemyUtilitySignals ?? Array.Empty<TickEnemyUtilityPresentationSignal>(),
                forwardCellImpactSignals: forwardCellImpactSignals ?? Array.Empty<TickForwardCellImpactPresentationSignal>(),
                forwardCellProjectileArrivalSignals: forwardCellProjectileArrivalSignals ?? Array.Empty<TickForwardCellProjectileArrivalPresentationSignal>(),
                enemyGlideSignals: enemyGlideSignals ?? Array.Empty<TickEnemyGlidePresentationSignal>());
        }

        private static TickEnemyGlidePresentationSignal CreateGlideSignal(
            int entityId,
            SurfaceCell anchorCell,
            EnemyGlidePhase phase,
            int phaseElapsedTicks)
        {
            return new TickEnemyGlidePresentationSignal(
                entityId,
                anchorCell,
                phase,
                sequence: 1,
                phaseElapsedTicks,
                phaseTotalTicks: 2,
                normalizedPhaseProgress: phaseElapsedTicks == 0 ? 0f : 0.5f,
                liftHeightUnits: 1024,
                recoveryDipHeightUnits: 0,
                currentHeightUnits: phase == EnemyGlidePhase.Active ? 1024 : 0,
                isAirborneVisual: phase == EnemyGlidePhase.Active,
                wantsRecover: false,
                isTerminalZero: false);
        }

        private static TickEnemyChargePresentationSignal CreateChargeSignal(
            int entityId,
            int sequence,
            EnemyChargePhase phase,
            bool startedActiveThisTick = false,
            bool startedRecoverThisTick = false)
        {
            return new TickEnemyChargePresentationSignal(
                entityId,
                sequence,
                phase,
                startedWindupThisTick: false,
                startedActiveThisTick,
                startedRecoverThisTick,
                lockedDirection: Direction.Right);
        }

        private static AudioClip[] ResolveRandomDefinitionClips(SerializedProperty clipsProperty)
        {
            var clips = new AudioClip[clipsProperty.arraySize];
            for (var i = 0; i < clipsProperty.arraySize; i++)
            {
                clips[i] = clipsProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Clip")
                    .objectReferenceValue as AudioClip;
            }

            return clips;
        }

        private static TickKinematicMotionTrack CreateKinematicTrack(
            int entityId,
            MotionMode motionMode,
            ForcedMotionOp forcedMotionOp,
            EntityType entityType = EntityType.Unit,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None,
            KinematicOffset2? sourceLocalOffset = null,
            KinematicOffset2? destinationLocalOffset = null,
            int startedTick = 0,
            int elapsedTicks = 0,
            int totalTicks = 0)
        {
            return new TickKinematicMotionTrack(
                entityId,
                new SurfaceCell(FaceId.Floor, 0, 0),
                sourceLocalOffset ?? KinematicOffset2.Zero,
                new SurfaceCell(FaceId.Floor, 1, 0),
                destinationLocalOffset ?? KinematicOffset2.Zero,
                motionMode,
                forcedMotionOp,
                entityType,
                sourceTopology: null,
                destinationTopology: null,
                sourceFacing: Direction.Right,
                destinationFacing: Direction.Right,
                terminalKind: terminalKind,
                startedTick: startedTick,
                elapsedTicks: elapsedTicks,
                totalTicks: totalTicks);
        }

        private static TickForwardCellImpactPresentationSignal CreateForwardCellImpactSignal(
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            bool hit,
            int targetEntityId)
        {
            return new TickForwardCellImpactPresentationSignal(
                100,
                100,
                ownerId,
                sourceEnemyId,
                targetCell,
                Direction.Right,
                hit,
                targetEntityId);
        }

        private static TickForwardCellProjectileArrivalPresentationSignal CreateForwardCellProjectileArrivalSignal(
            SurfaceCell targetCell,
            PendingCellImpactResolutionKind resolutionKind,
            int ownerId = 20,
            int sourceEnemyId = 20,
            int targetEntityId = 0,
            int impactId = 100,
            int presentationKey = 100,
            int impactTick = 12)
        {
            return new TickForwardCellProjectileArrivalPresentationSignal(
                impactId,
                presentationKey,
                ownerId,
                sourceEnemyId,
                targetCell,
                Direction.Right,
                impactTick,
                resolutionKind,
                targetEntityId);
        }

        private static void AssertForwardCellProjectileArrivalDoesNotCreateForwardCellImpactSfx(
            PendingCellImpactResolutionKind resolutionKind)
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                forwardCellProjectileArrivalSignals: new[]
                {
                    CreateForwardCellProjectileArrivalSignal(targetCell, resolutionKind),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => request.Cue).ToArray(),
                Has.No.EqualTo(EnemyAudioCue.ForwardCellImpact));
        }

        private static TickEnemyActionPresentationSignal CreateEnemyActionStartedSignal(int entityId)
        {
            return new TickEnemyActionPresentationSignal(
                entityId,
                EnemyActionKind.Melee,
                activeActionSequence: 1,
                startedThisTick: true,
                canceledThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false);
        }

        private static TickEnemyActionPresentationSignal CreateEnemyActionExecutedSignal(
            int entityId,
            EnemyActionPresentationSource source,
            EnemyActionPresentationOutcome outcome)
        {
            return new TickEnemyActionPresentationSignal(
                entityId,
                EnemyActionKind.Melee,
                activeActionSequence: 1,
                startedThisTick: false,
                canceledThisTick: false,
                executedThisTick: true,
                startedRecoveryThisTick: false,
                source,
                outcome);
        }

        private static EntityState CreateUnit(int entityId, UnitRole unitRole, SurfaceCell? position = null)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position ?? new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = unitRole == UnitRole.Player ? 1 : 2,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEntity(int entityId, EntityType entityType)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = entityType,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static DefinitionSpec CreateDefinitionSpec(
            AudioCategory category = AudioCategory.Sfx,
            bool loop = false)
        {
            return new DefinitionSpec(category, loop);
        }

        private static EnemyAudioProfileBundle CreateEnemyAudioProfile(params EnemyAudioEntrySpec[] entrySpecs)
        {
            var profile = ScriptableObject.CreateInstance<EnemyAudioProfile>();
            var trackedObjects = new List<UnityEngine.Object> { profile };
            var entries = new EnemyAudioEntry[entrySpecs.Length];

            for (var i = 0; i < entrySpecs.Length; i++)
            {
                AudioBinding binding = null;
                if (entrySpecs[i].DefinitionSpec.HasValue)
                {
                    var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                    definition.name = entrySpecs[i].Cue.ToString();
                    var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                    trackedObjects.Add(clip);
                    trackedObjects.Add(definition);
                    SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                    SetSerializedField(typeof(AudioDefinition), definition, "category", entrySpecs[i].DefinitionSpec.Value.Category);
                    SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                    SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                    SetSerializedField(typeof(AudioDefinition), definition, "loop", entrySpecs[i].DefinitionSpec.Value.Loop);

                    binding = new AudioBinding();
                    SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                    SetSerializedField(
                        typeof(AudioBinding),
                        binding,
                        "attachmentSlot",
                        AudioAttachmentSlot.FromId(entrySpecs[i].AttachmentSlotId));
                    SetSerializedField(typeof(AudioBinding), binding, "policy", null);
                }

                entries[i] = new EnemyAudioEntry
                {
                    Cue = entrySpecs[i].Cue,
                    Binding = binding,
                    IsOptional = entrySpecs[i].IsOptional,
                };
            }

            SetSerializedField(typeof(EnemyAudioProfile), profile, "entries", entries);
            return new EnemyAudioProfileBundle(profile, trackedObjects);
        }

        private static GameplayAudioMapBundle CreateGameplayAudioMap()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definitions = new List<UnityEngine.Object>();
            var serializedObject = new SerializedObject(map);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = GameplayAudioSemanticCatalog.RequiredOneShotV1.Count;

            for (var i = 0; i < GameplayAudioSemanticCatalog.RequiredOneShotV1.Count; i++)
            {
                var semanticId = GameplayAudioSemanticCatalog.RequiredOneShotV1[i];
                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                definition.name = semanticId.ToString();
                var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                definitions.Add(clip);
                definitions.Add(definition);
                SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Sfx);
                SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                SetSerializedField(typeof(AudioDefinition), definition, "loop", false);

                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("SemanticId").enumValueIndex = (int)semanticId;
                var binding = element.FindPropertyRelative("Binding");
                binding.FindPropertyRelative("definition").objectReferenceValue = definition;
                binding.FindPropertyRelative("attachmentSlot").FindPropertyRelative("id").stringValue = string.Empty;
                binding.FindPropertyRelative("policy").managedReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return new GameplayAudioMapBundle(map, definitions);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private readonly struct DefinitionSpec
        {
            public DefinitionSpec(AudioCategory category, bool loop)
            {
                Category = category;
                Loop = loop;
            }

            public AudioCategory Category { get; }

            public bool Loop { get; }
        }

        private readonly struct EnemyAudioEntrySpec
        {
            public EnemyAudioEntrySpec(
                EnemyAudioCue cue,
                DefinitionSpec? definitionSpec,
                bool isOptional = false,
                string attachmentSlotId = null)
            {
                Cue = cue;
                DefinitionSpec = definitionSpec;
                IsOptional = isOptional;
                AttachmentSlotId = attachmentSlotId;
            }

            public EnemyAudioCue Cue { get; }

            public DefinitionSpec? DefinitionSpec { get; }

            public bool IsOptional { get; }

            public string AttachmentSlotId { get; }
        }

        private readonly struct EnemyPrefabExpectation
        {
            public EnemyPrefabExpectation(string path, params EnemyAudioCue[] cues)
            {
                Path = path;
                Cues = cues;
            }

            public string Path { get; }

            public IReadOnlyList<EnemyAudioCue> Cues { get; }
        }

        private readonly struct ProductionEnemyAudioExpectation
        {
            public ProductionEnemyAudioExpectation(
                string enemy,
                string presentationId,
                string prefabName,
                string bindingPath,
                IReadOnlyList<EnemyAudioCue> requiredCues,
                IReadOnlyList<EnemyAudioCue> disabledCues)
            {
                Enemy = enemy;
                PresentationId = presentationId;
                PrefabName = prefabName;
                BindingPath = bindingPath;
                RequiredCues = requiredCues ?? Array.Empty<EnemyAudioCue>();
                DisabledCues = disabledCues ?? Array.Empty<EnemyAudioCue>();
            }

            public string Enemy { get; }

            public string PresentationId { get; }

            public string PrefabName { get; }

            public string BindingPath { get; }

            public IReadOnlyList<EnemyAudioCue> RequiredCues { get; }

            public IReadOnlyList<EnemyAudioCue> DisabledCues { get; }
        }

        private sealed class EnemyAudioProfileBundle : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects;

            public EnemyAudioProfileBundle(EnemyAudioProfile profile, List<UnityEngine.Object> trackedObjects)
            {
                Profile = profile;
                _trackedObjects = trackedObjects;
            }

            public EnemyAudioProfile Profile { get; }

            public void Dispose()
            {
                for (var i = _trackedObjects.Count - 1; i >= 0; i--)
                {
                    if (_trackedObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_trackedObjects[i]);
                    }
                }
            }
        }

        private sealed class GameplayAudioMapBundle : IDisposable
        {
            private readonly List<UnityEngine.Object> _definitions;

            public GameplayAudioMapBundle(GameplayAudioMap map, List<UnityEngine.Object> definitions)
            {
                Map = map;
                _definitions = definitions;
            }

            public GameplayAudioMap Map { get; }

            public void Dispose()
            {
                for (var i = _definitions.Count - 1; i >= 0; i--)
                {
                    if (_definitions[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_definitions[i]);
                    }
                }

                if (Map != null)
                {
                    UnityEngine.Object.DestroyImmediate(Map);
                }
            }
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort, IGameplayAudioLoopPlaybackPort
        {
            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();
            public readonly List<AttachedLoopCall> AttachedLoopCalls = new();

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                TwoDCalls.Add((definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                Play2D(definition, context);
            }

            public AudioPlaybackHandle PlayAttachedLoop(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                var controller = new RecordingPlaybackController();
                var handle = new AudioPlaybackHandle(controller);
                AttachedLoopCalls.Add(new AttachedLoopCall(definition, owner, slot, context, controller));
                return handle;
            }
        }

        private sealed class RecordingEnemyAudioPlaybackPort : IGameplayEnemyAudioPlaybackPort
        {
            private readonly GameplayEnemyAudioPlaybackResultKind _resultKind;

            public RecordingEnemyAudioPlaybackPort(
                GameplayEnemyAudioPlaybackResultKind resultKind = GameplayEnemyAudioPlaybackResultKind.Succeeded)
            {
                _resultKind = resultKind;
            }

            public readonly List<GameplayEnemyAudioPlaybackRequest> Requests = new();
            public int ResetCount { get; private set; }
            public int HardCleanupCount { get; private set; }

            public bool TryPlayEnemyAudio(
                in GameplayEnemyAudioPlaybackRequest request,
                out GameplayEnemyAudioPlaybackResult result)
            {
                Requests.Add(request);
                result = new GameplayEnemyAudioPlaybackResult(_resultKind);
                return _resultKind == GameplayEnemyAudioPlaybackResultKind.Succeeded ||
                       _resultKind == GameplayEnemyAudioPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetCount++;
            }

            public void HardCleanup()
            {
                HardCleanupCount++;
            }
        }

        private readonly struct AttachedLoopCall
        {
            public AttachedLoopCall(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                AudioPlaybackContext context,
                RecordingPlaybackController controller)
            {
                Definition = definition;
                Owner = owner;
                Slot = slot;
                Context = context;
                Controller = controller;
            }

            public AudioDefinition Definition { get; }

            public Component Owner { get; }

            public AudioAttachmentSlot Slot { get; }

            public AudioPlaybackContext Context { get; }

            public RecordingPlaybackController Controller { get; }
        }

        private sealed class RecordingPlaybackController : IAudioPlaybackController
        {
            public int StopCount { get; private set; }

            public bool IsValid => StopCount == 0;

            public void Stop()
            {
                StopCount++;
            }

            public void Pause()
            {
            }

            public void Resume()
            {
            }

            public void SetVolume(float volume)
            {
            }
        }

        private sealed class EnemyAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;
            private readonly EnemyAudioProfile _profile;
            private readonly bool _attachAuthoringWithoutProfile;

            public EnemyAudioViewFactory(
                Transform parent,
                EnemyAudioProfile profile,
                bool attachAuthoringWithoutProfile = false)
            {
                _parent = parent;
                _profile = profile;
                _attachAuthoringWithoutProfile = attachAuthoringWithoutProfile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                if (_parent != null)
                {
                    viewObject.transform.SetParent(_parent, worldPositionStays: false);
                }

                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                viewObject.AddComponent<EntityEffectPresentationAuthoring>();

                if ((_profile != null || _attachAuthoringWithoutProfile) &&
                    entity.unitRole == UnitRole.Enemy)
                {
                    var authoring = viewObject.AddComponent<EnemyAudioAuthoring>();
                    SetSerializedField(typeof(EnemyAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
            }
        }
    }
}
