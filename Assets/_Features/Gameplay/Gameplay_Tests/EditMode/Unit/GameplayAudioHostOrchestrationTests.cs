using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayAudioHostOrchestrationTests
    {
        private const string BlockAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset";
        private const string PlayerLocomotionAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset";
        private const string TopologyAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TopologyAudio/Maps/TopologyAudioMap_ObjectSounds.asset";
        private const string GravityFieldAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset";
        private const string TileFeatureAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Maps/TileFeatureAudioMap_ObjectSounds.asset";

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_UsesCanonicalGameplayAudioOrdering()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_UsesCanonicalGameplayAudioOrdering));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var trace = new List<string>();

                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                presenter.SetPresentationTraceSink(trace.Add);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(TickPresentationData.Empty));

                Assert.That(trace, Is.EqualTo(new[]
                {
                    "RefreshAudioPlan",
                    "RefreshUtilityWindupWarnings",
                    "PlayPlannedAudio",
                    "ApplyEntityExitOwnership",
                }));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_ExecutesGameplayAudioPlayback_BetweenVfxAndExitOwnershipApplication()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ExecutesGameplayAudioPlayback_BetweenVfxAndExitOwnershipApplication));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var trace = new List<string>();
                var playbackPort = new RecordingGameplayAudioPlaybackPort(trace.Add);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.SetPresentationTraceSink(trace.Add);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(trace, Is.EqualTo(new[]
                {
                    "RefreshAudioPlan",
                    "RefreshUtilityWindupWarnings",
                    "PlayPlannedAudio",
                    "Playback:Play2D",
                    "ApplyEntityExitOwnership",
                }));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_ExitOwnedEntityAudio_AttachesBeforeOwnershipRemoval()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ExitOwnedEntityAudio_AttachesBeforeOwnershipRemoval));
            var mapBundle = CreateGameplayAudioMap(new Dictionary<GameplayAudioSemanticId, AudioAttachmentSlot>
            {
                { GameplayAudioSemanticId.EntityExitEnemyDeath, AudioAttachmentSlot.FromId("death") },
            });
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var topology = new CubeTopologyState(FaceId.Floor);
                var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, topology);
                presenter.Present(CreateTickResult(
                    CreateExitPresentationData(enemy.entityId, TickEntityExitCause.EnemyDeath),
                    Array.Empty<EntityState>()));

                Assert.That(playbackPort.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.AttachedCalls[0].Owner, Is.Not.Null);
                Assert.That(playbackPort.AttachedCalls[0].Owner.EntityId, Is.EqualTo(enemy.entityId));
                Assert.That(playbackPort.AttachedCalls[0].Slot.Id, Is.EqualTo("death"));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
                Assert.That(playbackPort.AttachedCalls[0].Owner.gameObject.activeSelf, Is.False);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_MissingOwnerView_FallsBackToTwoD()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_MissingOwnerView_FallsBackToTwoD));
            var mapBundle = CreateGameplayAudioMap(new Dictionary<GameplayAudioSemanticId, AudioAttachmentSlot>
            {
                { GameplayAudioSemanticId.PlayerDamage, AudioAttachmentSlot.FromId("body") },
            });
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(player.entityId)));

                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("PlayerDamage"));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_LiveOwnerWithoutUsableAttachmentSlot_FallsBackToTwoD()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_LiveOwnerWithoutUsableAttachmentSlot_FallsBackToTwoD));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePlayerDamagePresentationData(player.entityId),
                    new[] { player }));

                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("PlayerDamage"));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_DefaultMode_IsOrchestrationBridge()
        {
            var rootObject = new GameObject(nameof(CoreSfx_DefaultMode_IsOrchestrationBridge));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(presenter.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(presenter.PendingGameplayAudioRequestCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "PlayerDamage",
                }));
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.RequestPlannedCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_DefaultOrchestration_TelemetryReportsProductionOwner()
        {
            var rootObject = new GameObject(nameof(CoreSfx_DefaultOrchestration_TelemetryReportsProductionOwner));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), tickIndex: 9));

                var diagnostics = presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.PlaybackRequestPlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(diagnostics.LastTickIndex, Is.EqualTo(9));
                Assert.That(diagnostics.LastSemanticKey, Is.EqualTo(PresentationSfxCueKey.PlayerDamage));
                Assert.That(diagnostics.LastDiagnosticReason, Is.EqualTo(GameplaySfxDiagnosticReason.None));
                AssertSemanticDiagnostics(
                    diagnostics,
                    PresentationSfxCueKey.PlayerDamage,
                    planned: 1,
                    requested: 1,
                    succeeded: 1,
                    fallback: 0);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_DefaultOrchestration_DoesNotDuplicateLegacyPlayback()
        {
            var rootObject = new GameObject(nameof(CoreSfx_DefaultOrchestration_DoesNotDuplicateLegacyPlayback));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreateCoreSfxPresentationData(10, 20)));

                Assert.That(presenter.PendingGameplayAudioRequestCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "PlayerDamage",
                    "EntityExitEnemyDeath",
                }));
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.RequestPlannedCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioBatch_MixedProductionAndLegacyModes_PreservesTwoPassOrdering()
        {
            var rootObject = new GameObject(nameof(GameplayAudioBatch_MixedProductionAndLegacyModes_PreservesTwoPassOrdering));
            var trace = new List<string>();
            var mapBundle = CreateGameplayAudioMap();
            var actionProfileBundle = CreateActionAudioPushWindupProfile();
            var enemyProfileBundle = CreateEnemyDeathAudioProfile();
            try
            {
                var enemyBridgePort = new RecordingEnemyAudioPlaybackPort(trace.Add);
                var presenter = CreatePresenter(
                    rootObject,
                    new MixedAudioViewFactory(
                        rootObject.transform,
                        actionProfileBundle.Profile,
                        enemyProfileBundle.Profile),
                    _ => GameplayHostPresentationPipelineFactory.CreateEnemyAudioExecutionPipeline(enemyBridgePort));
                var legacyPort = new RecordingGameplayAudioPlaybackPort(trace.Add, traceDebugTag: true);
                var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0));
                var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.AttachGameplayAudioRuntime(legacyPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player, enemy }, new CubeTopologyState(FaceId.Floor));

                presenter.Present(CreateTickResult(
                    CreateMixedAudioPresentationData(player.entityId, enemy.entityId),
                    new[] { player, enemy },
                    tickIndex: 17));

                Assert.That(trace, Is.EqualTo(new[]
                {
                    "Legacy:PlayerPushWindup",
                    "EnemyProduction:Death",
                    "Legacy:PlayerDamage",
                }));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(enemyBridgePort.Requests, Has.Count.EqualTo(1));
            }
            finally
            {
                actionProfileBundle.Dispose();
                enemyProfileBundle.Dispose();
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_DefaultOrchestration_TelemetryCoversAllIncludedSemantics()
        {
            var rootObject = new GameObject(nameof(CoreSfx_DefaultOrchestration_TelemetryCoversAllIncludedSemantics));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreateAllCoreSfxPresentationData(), tickIndex: 17));

                var expectedTags = new[]
                {
                    "PlayerDamage",
                    "EnemyDamage",
                    "EntityExitItemConsume",
                    "EntityExitBoxDestroy",
                    "EntityExitEnemyDeath",
                };
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(expectedTags));

                var diagnostics = presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.PlaybackRequestPlannedCount, Is.EqualTo(6));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(6));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(6));
                Assert.That(diagnostics.MapMissingCount, Is.Zero);
                Assert.That(diagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(diagnostics.DiagnosticCount, Is.Zero);
                Assert.That(diagnostics.SemanticDiagnostics.Count, Is.EqualTo(6));

                foreach (var semanticCase in CreateCoreSfxSemanticCases())
                {
                    var semanticDiagnostics = AssertSemanticDiagnostics(
                        diagnostics,
                        semanticCase.CueKey,
                        planned: 1,
                        requested: 1,
                        succeeded: 1,
                        fallback: 0);
                    Assert.That(semanticDiagnostics.LastDedupeKey, Is.GreaterThan(0), semanticCase.Name);
                }
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_DefaultOrchestration_CoversAllIncludedSemantics()
        {
            var rootObject = new GameObject(nameof(CoreSfx_DefaultOrchestration_CoversAllIncludedSemantics));
            try
            {
                var executorPort = new RecordingGameplaySfxPlaybackPort();
                var coordinator = CreateInitializedCoreSfxCoordinator(
                    rootObject,
                    playbackPort: executorPort);

                coordinator.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                coordinator.Present(CreateTickResult(CreateAllCoreSfxPresentationData(), tickIndex: 17));

                Assert.That(executorPort.Requests.Select(request => request.CueKey).ToArray(), Is.EqualTo(new[]
                {
                    PresentationSfxCueKey.PlayerDamage,
                    PresentationSfxCueKey.EnemyDamage,
                    PresentationSfxCueKey.EntityExitItemConsume,
                    PresentationSfxCueKey.EntityExitBoxDestroy,
                    PresentationSfxCueKey.EntityExitEnemyDeath,
                    PresentationSfxCueKey.EntityExitOutOfBounds,
                }));
                AssertCoreSfxRequest(
                    executorPort.Requests[0],
                    PresentationSfxCueKey.PlayerDamage,
                    PresentationSemanticSource.PlayerDamage,
                    targetEntityId: 10,
                    sourceEntityId: 10,
                    expectedEntityType: EntityType.None,
                    expectedExitCause: TickEntityExitCause.None,
                    expectedSourceActorEntityId: 0,
                    expectedAnchorKind: PresentationAnchorKind.EntityCenter,
                    expectedTickIndex: 17);
                AssertCoreSfxRequest(
                    executorPort.Requests[1],
                    PresentationSfxCueKey.EnemyDamage,
                    PresentationSemanticSource.EnemyDamage,
                    targetEntityId: 20,
                    sourceEntityId: 20,
                    expectedEntityType: EntityType.None,
                    expectedExitCause: TickEntityExitCause.None,
                    expectedSourceActorEntityId: 0,
                    expectedAnchorKind: PresentationAnchorKind.EntityCenter,
                    expectedTickIndex: 17);
                AssertCoreSfxRequest(
                    executorPort.Requests[2],
                    PresentationSfxCueKey.EntityExitItemConsume,
                    PresentationSemanticSource.EntityExit,
                    targetEntityId: 30,
                    sourceEntityId: 30,
                    expectedEntityType: EntityType.Unit,
                    expectedExitCause: TickEntityExitCause.ItemConsume,
                    expectedSourceActorEntityId: 10,
                    expectedAnchorKind: PresentationAnchorKind.SurfaceCellCenter,
                    expectedTickIndex: 17);
                AssertCoreSfxRequest(
                    executorPort.Requests[3],
                    PresentationSfxCueKey.EntityExitBoxDestroy,
                    PresentationSemanticSource.EntityExit,
                    targetEntityId: 40,
                    sourceEntityId: 40,
                    expectedEntityType: EntityType.Box,
                    expectedExitCause: TickEntityExitCause.BoxDestroy,
                    expectedSourceActorEntityId: 10,
                    expectedAnchorKind: PresentationAnchorKind.SurfaceCellCenter,
                    expectedTickIndex: 17);
                AssertCoreSfxRequest(
                    executorPort.Requests[4],
                    PresentationSfxCueKey.EntityExitEnemyDeath,
                    PresentationSemanticSource.EntityExit,
                    targetEntityId: 50,
                    sourceEntityId: 50,
                    expectedEntityType: EntityType.Unit,
                    expectedExitCause: TickEntityExitCause.EnemyDeath,
                    expectedSourceActorEntityId: 10,
                    expectedAnchorKind: PresentationAnchorKind.SurfaceCellCenter,
                    expectedTickIndex: 17);
                AssertCoreSfxRequest(
                    executorPort.Requests[5],
                    PresentationSfxCueKey.EntityExitOutOfBounds,
                    PresentationSemanticSource.EntityExit,
                    targetEntityId: 60,
                    sourceEntityId: 60,
                    expectedEntityType: EntityType.Unit,
                    expectedExitCause: TickEntityExitCause.OutOfBounds,
                    expectedSourceActorEntityId: 0,
                    expectedAnchorKind: PresentationAnchorKind.SurfaceCellCenter,
                    expectedTickIndex: 17);
                Assert.That(coordinator.PendingGameplayAudioRequestCount, Is.Zero);
                Assert.That(coordinator.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(6));
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.RequestPlannedCount, Is.EqualTo(6));
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(6));
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(6));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_TopologyLockDefersDamageAndDrainsAfterUnlock()
        {
            var rootObject = new GameObject(nameof(CoreSfx_TopologyLockDefersDamageAndDrainsAfterUnlock));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreateTopologyLockedCoreSfxPresentationData(), tickIndex: 21));

                Assert.That(presenter.IsTopologyTransitionActive, Is.True);
                Assert.That(presenter.DeferredGameplayAudioRequestCount, Is.EqualTo(2));
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "EntityExitItemConsume",
                }));

                var lockedDiagnostics = presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(lockedDiagnostics.DeferredDuringTopologyLockCount, Is.EqualTo(2));
                Assert.That(lockedDiagnostics.DeferredDrainCount, Is.Zero);
                Assert.That(lockedDiagnostics.DuplicateSuppressedCount, Is.Zero);
                AssertSemanticDiagnostics(lockedDiagnostics, PresentationSfxCueKey.PlayerDamage, 1, 1, 1, 0);
                AssertSemanticDiagnostics(lockedDiagnostics, PresentationSfxCueKey.EnemyDamage, 1, 1, 1, 0);
                AssertSemanticDiagnostics(lockedDiagnostics, PresentationSfxCueKey.EntityExitItemConsume, 1, 1, 1, 0);

                presenter.UpdatePresentation(CreateTimingProfile().TopologyMotionDurationSeconds);

                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
                Assert.That(presenter.DeferredGameplayAudioRequestCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "EntityExitItemConsume",
                    "PlayerDamage",
                    "EnemyDamage",
                }));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.DeferredDuringTopologyLockCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.DeferredDrainCount, Is.EqualTo(2));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_EnemyDeathSuppression_UsesCurrentExecutorPolicy()
        {
            var orchestrationRoot = new GameObject(nameof(CoreSfx_EnemyDeathSuppression_UsesCurrentExecutorPolicy) + "_Orchestration");
            var absentProfileRoot = new GameObject(nameof(CoreSfx_EnemyDeathSuppression_UsesCurrentExecutorPolicy) + "_AbsentProfile");
            var nonLethalRoot = new GameObject(nameof(CoreSfx_EnemyDeathSuppression_UsesCurrentExecutorPolicy) + "_NonLethal");
            var mapBundle = CreateGameplayAudioMap();
            var profileBundle = CreateEnemyDeathAudioProfile();
            try
            {
                var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
                var lethalDeathData = CreateEnemyDamageAndDeathPresentationData(enemy.entityId);

                var orchestrationPresenter = CreatePresenter(
                    orchestrationRoot,
                    new EnemyAudioViewFactory(orchestrationRoot.transform, profileBundle.Profile));
                var orchestrationPort = new RecordingGameplayAudioPlaybackPort();
                orchestrationPresenter.AttachGameplayAudioRuntime(orchestrationPort, mapBundle.Map);
                orchestrationPresenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                orchestrationPresenter.Present(CreateTickResult(lethalDeathData, tickIndex: 31));

                Assert.That(orchestrationPresenter.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(orchestrationPresenter.CoreGameplaySfxExecutorDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount, Is.EqualTo(1));
                Assert.That(orchestrationPresenter.CoreGameplaySfxExecutorDiagnostics.LethalEnemyDamageSuppressedByDeathCount, Is.EqualTo(1));
                Assert.That(orchestrationPresenter.CoreGameplaySfxExecutorDiagnostics.PlaybackNoOpSuppressedCount, Is.EqualTo(2));
                Assert.That(orchestrationPresenter.CoreGameplaySfxExecutorDiagnostics.DuplicateSuppressedCount, Is.Zero);

                var absentProfilePresenter = CreatePresenter(absentProfileRoot);
                var absentProfilePort = new RecordingGameplayAudioPlaybackPort();
                absentProfilePresenter.AttachGameplayAudioRuntime(absentProfilePort, mapBundle.Map);
                absentProfilePresenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                absentProfilePresenter.Present(CreateTickResult(CreateExitPresentationData(enemy.entityId, TickEntityExitCause.EnemyDeath), tickIndex: 32));

                Assert.That(absentProfilePort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "EntityExitEnemyDeath",
                }));
                Assert.That(absentProfilePresenter.CoreGameplaySfxExecutorDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount, Is.Zero);

                var nonLethalPresenter = CreatePresenter(
                    nonLethalRoot,
                    new EnemyAudioViewFactory(nonLethalRoot.transform, profileBundle.Profile));
                var nonLethalPort = new RecordingGameplayAudioPlaybackPort();
                nonLethalPresenter.AttachGameplayAudioRuntime(nonLethalPort, mapBundle.Map);
                nonLethalPresenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                nonLethalPresenter.Present(CreateTickResult(CreateEnemyDamagePresentationData(enemy.entityId), tickIndex: 33));

                Assert.That(nonLethalPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Does.Contain("EnemyDamage"));
                Assert.That(nonLethalPresenter.CoreGameplaySfxExecutorDiagnostics.LethalEnemyDamageSuppressedByDeathCount, Is.Zero);
                Assert.That(nonLethalPresenter.CoreGameplaySfxExecutorDiagnostics.DuplicateSuppressedCount, Is.Zero);
            }
            finally
            {
                profileBundle.Dispose();
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(orchestrationRoot);
                UnityEngine.Object.DestroyImmediate(absentProfileRoot);
                UnityEngine.Object.DestroyImmediate(nonLethalRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_AttachedAndTwoDFallbackSmoke_RemainsEquivalent()
        {
            var rootObject = new GameObject(nameof(CoreSfx_AttachedAndTwoDFallbackSmoke_RemainsEquivalent));
            var mapBundle = CreateGameplayAudioMap(new Dictionary<GameplayAudioSemanticId, AudioAttachmentSlot>
            {
                { GameplayAudioSemanticId.PlayerDamage, AudioAttachmentSlot.FromId("body") },
            });
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(player.entityId), new[] { player }, tickIndex: 41));

                Assert.That(playbackPort.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.AttachedLikePlaybackCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.OwnerMissingTwoDPlaybackCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.LastDiagnosticReason, Is.EqualTo(GameplaySfxDiagnosticReason.None));

                playbackPort.Clear();
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(player.entityId), tickIndex: 42));

                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.AttachedLikePlaybackCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.OwnerMissingTwoDPlaybackCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.LastDiagnosticReason, Is.EqualTo(GameplaySfxDiagnosticReason.OwnerViewMissingPlay2D));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.DiagnosticCount, Is.EqualTo(1));
                Assert.That(
                    typeof(PresentationCue).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
                Assert.That(
                    typeof(PresentationPlaybackPlan).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_ProductionTelemetry_IsNonAuthoritative()
        {
            var rootObject = new GameObject(nameof(CoreSfx_ProductionTelemetry_IsNonAuthoritative));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var result = CreateTickResult(
                    CreateAllCoreSfxPresentationData(),
                    finalEntities: new[] { CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    tickIndex: 61,
                    eventLog: new[] { "BeforePresentation" },
                    determinismHash: "hash-before");
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;

                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(result);

                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(result.DeterminismHash, Is.EqualTo("hash-before"));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
                Assert.That(presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_LegacyAndOrchestrationRequests_AreSemanticallyEquivalentForAllIncludedSemantics()
        {
            var planner = new GameplayAudioRequestPlanner();
            var cases = CreateCoreSfxSemanticCases();

            for (var i = 0; i < cases.Length; i++)
            {
                var semanticCase = cases[i];
                var result = CreateTickResult(semanticCase.PresentationData, tickIndex: 30 + i);
                var legacyRequest = planner.BuildRequests(result, CreateTimingProfile()).Single();
                var factFrame = new TickPresentationFactExtractor().Extract(result);
                var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new SfxCuePlanner(),
                }).Plan(factFrame);
                var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
                var port = new RecordingGameplaySfxPlaybackPort();
                var executor = new GameplaySfxPresentationExecutor(
                    port,
                    new CoreGameplaySfxExecutionGuard());

                executor.Play(playbackPlan);

                Assert.That(port.Requests, Has.Count.EqualTo(1), semanticCase.Name);
                var orchestrationRequest = port.Requests[0];
                Assert.That(legacyRequest.SemanticId, Is.EqualTo(semanticCase.AudioSemantic), semanticCase.Name);
                Assert.That(legacyRequest.OwnerEntityId, Is.EqualTo(semanticCase.TargetEntityId), semanticCase.Name);
                Assert.That(legacyRequest.Context.DebugTag, Is.EqualTo(GameplayAudioSemanticCatalog.Format(semanticCase.AudioSemantic)), semanticCase.Name);
                Assert.That(orchestrationRequest.CueKey, Is.EqualTo(semanticCase.CueKey), semanticCase.Name);
                Assert.That(orchestrationRequest.Target, Is.EqualTo(PresentationTarget.Entity(semanticCase.TargetEntityId)), semanticCase.Name);
                Assert.That(orchestrationRequest.Source.SourceEntityId, Is.EqualTo(semanticCase.SourceEntityId), semanticCase.Name);
                Assert.That(orchestrationRequest.TickIndex, Is.EqualTo(result.TickIndex), semanticCase.Name);
                Assert.That(orchestrationRequest.SfxPayload.EntityType, Is.EqualTo((int)semanticCase.EntityType), semanticCase.Name);
                Assert.That(orchestrationRequest.SfxPayload.ExitCause, Is.EqualTo((int)semanticCase.ExitCause), semanticCase.Name);
                Assert.That(orchestrationRequest.SfxPayload.SourceActorEntityId, Is.EqualTo(semanticCase.SourceActorEntityId), semanticCase.Name);
                Assert.That(playbackPlan.Cues.Single().Policy.UnitKind, Is.EqualTo(PresentationPlaybackUnitKind.OneShot), semanticCase.Name);
                Assert.That(playbackPlan.Cues.Single().Policy.Blocking, Is.False, semanticCase.Name);
                Assert.That(playbackPlan.Cues.Single().Policy.DedupeKey, Is.GreaterThan(0), semanticCase.Name);
                Assert.That(orchestrationRequest.OwnershipKey.CueKey, Is.EqualTo(semanticCase.CueKey), semanticCase.Name);
                Assert.That(orchestrationRequest.OwnershipKey.TargetEntityId, Is.EqualTo(semanticCase.TargetEntityId), semanticCase.Name);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_CurrentPlanner_EmitsPlayerDamageFallbackForPlayerDeath()
        {
            var result = CreateTickResult(CreatePlayerDeathPresentationData(10), tickIndex: 44);
            var legacyRequest = new GameplayAudioRequestPlanner()
                .BuildRequests(result, CreateTimingProfile())
                .Single();
            var factFrame = new TickPresentationFactExtractor(CreateTimingProfile()).Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var port = new RecordingGameplaySfxPlaybackPort();
            var executor = new GameplaySfxPresentationExecutor(
                port,
                new CoreGameplaySfxExecutionGuard());

            executor.Play(playbackPlan);

            Assert.That(legacyRequest.SemanticId, Is.EqualTo(GameplayAudioSemanticId.PlayerDamage));
            Assert.That(port.Requests, Has.Count.EqualTo(1));
            Assert.That(port.Requests[0].CueKey, Is.EqualTo(PresentationSfxCueKey.PlayerDamage));
            Assert.That(port.Requests[0].Target, Is.EqualTo(PresentationTarget.Entity(10)));
            Assert.That(port.Requests[0].Source.SemanticSource, Is.EqualTo(PresentationSemanticSource.PlayerDeath));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_CurrentPlanner_PreservesAfterEntityMotionBoxDestroyDelay()
        {
            var timingProfile = new GameplayTimingProfile(
                simulationTicksPerSecond: 20,
                initialMoveDelaySeconds: 0.1f,
                repeatedMoveIntervalSeconds: 0.1f,
                boxSlideStepIntervalSeconds: 0.37f,
                projectileStepIntervalSeconds: 0.1f,
                moveMotionDurationSeconds: 0.11f,
                pushMotionDurationSeconds: 0.12f,
                topologyMotionDurationSeconds: 0.1f,
                flipMotionDurationSeconds: 0.13f,
                flipArcHeightInCells: 1f,
                maxTicksPerFrame: 4,
                itemConsumeEffectDurationSeconds: 0.1f,
                boxDestroyEffectDurationSeconds: 0.1f,
                enemyDeathEffectDurationSeconds: 0.1f);
            var result = CreateTickResult(
                CreateAfterEntityMotionBoxDestroyPresentationData(40),
                tickIndex: 45);
            var legacyRequest = new GameplayAudioRequestPlanner()
                .BuildRequests(result, timingProfile)
                .Single();
            var factFrame = new TickPresentationFactExtractor(timingProfile).Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var port = new RecordingGameplaySfxPlaybackPort();
            var executor = new GameplaySfxPresentationExecutor(
                port,
                new CoreGameplaySfxExecutionGuard());

            executor.Play(playbackPlan);

            Assert.That(legacyRequest.DelaySeconds, Is.EqualTo(timingProfile.BoxSlideStepIntervalSeconds).Within(0.0001f));
            Assert.That(port.Requests, Has.Count.EqualTo(1));
            Assert.That(port.Requests[0].CueKey, Is.EqualTo(PresentationSfxCueKey.EntityExitBoxDestroy));
            Assert.That(port.Requests[0].DelaySeconds, Is.EqualTo(timingProfile.BoxSlideStepIntervalSeconds).Within(0.0001f));
            Assert.That(port.Requests[0].SfxPayload.DelaySeconds, Is.EqualTo(timingProfile.BoxSlideStepIntervalSeconds).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_NonSensitiveDelayedRequest_DrainsOnScheduleDuringTopologyLock()
        {
            var stateStore = new GameplayPresentationStateStore();
            var adapter = new GameplaySfxPlaybackPortAdapter(stateStore);
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            using (var mapBundle = CreateGameplayAudioMap())
            {
                adapter.AttachRuntime(playbackPort, mapBundle.Map);
                adapter.SetPlaybackGateState(GameplayAudioPlaybackGateState.TopologyLocked);
                var request = CreateSfxPlaybackRequest(
                    PresentationSfxCueKey.EntityExitBoxDestroy,
                    targetEntityId: 40,
                    delaySeconds: 0.2f,
                    semanticSource: PresentationSemanticSource.EntityExit);

                Assert.That(adapter.TryPlayCoreGameplaySfx(request, out var result), Is.True);
                Assert.That(result.Kind, Is.EqualTo(GameplaySfxPlaybackResultKind.Requested));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
                Assert.That(adapter.DeferredRequestCount, Is.EqualTo(1));

                adapter.Update(0.19f);
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
                Assert.That(adapter.DeferredRequestCount, Is.EqualTo(1));

                adapter.Update(0.02f);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("EntityExitBoxDestroy"));
                Assert.That(adapter.DeferredRequestCount, Is.Zero);

                adapter.Update(1f);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_ForcedDuplicateStillBlocksSecondOwner()
        {
            var rootObject = new GameObject(nameof(CoreSfx_ForcedDuplicateStillBlocksSecondOwner));
            try
            {
                var port = new RecordingGameplaySfxPlaybackPort();
                var coordinator = CreateInitializedCoreSfxCoordinator(
                    rootObject,
                    playbackPort: port,
                    duplicateExecutors: true);

                coordinator.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), tickIndex: 41));

                Assert.That(port.Requests, Has.Count.EqualTo(1));
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.DuplicateAttemptCount, Is.EqualTo(1));
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.DuplicateSuppressedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_DefaultOrchestration_MissingOwnerViewFallsBackToTwoDAndRecordsDiagnostics()
        {
            var rootObject = new GameObject(nameof(CoreSfx_DefaultOrchestration_MissingOwnerViewFallsBackToTwoDAndRecordsDiagnostics));
            var mapBundle = CreateGameplayAudioMap(new Dictionary<GameplayAudioSemanticId, AudioAttachmentSlot>
            {
                { GameplayAudioSemanticId.PlayerDamage, AudioAttachmentSlot.FromId("body") },
            });
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.OwnerViewMissingCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.PortMissingCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.BindingMissingCount, Is.Zero);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_OrchestrationMode_DistinguishesControlledIntegrationDiagnostics()
        {
            var unsupportedCue = new PresentationCue(
                PresentationDomain.Sfx,
                new PresentationCueKey(PresentationDomain.Sfx, localKey: 999),
                new PresentationSource(51, PresentationSemanticSource.PlayerDamage, 10),
                PresentationTarget.Entity(10),
                PresentationAnchor.ForEntityCenter(10),
                PresentationPlaybackPolicyHint.OneShot(1));
            var targetMissingCue = new PresentationCue(
                PresentationDomain.Sfx,
                PresentationCueKey.ForSfx(PresentationSfxCueKey.PlayerDamage),
                new PresentationSource(51, PresentationSemanticSource.PlayerDamage, 0),
                PresentationTarget.None(),
                PresentationAnchor.None(),
                PresentationPlaybackPolicyHint.OneShot(2));

            AssertCoreSfxExecutorDiagnostic(
                new[] { unsupportedCue },
                new RecordingGameplaySfxPlaybackPort(),
                diagnostics => diagnostics.SemanticUnsupportedCount,
                expectedCount: 1);
            AssertCoreSfxExecutorDiagnostic(
                new[] { targetMissingCue },
                new RecordingGameplaySfxPlaybackPort(),
                diagnostics => diagnostics.TargetMissingCount,
                expectedCount: 1);
            AssertCoreSfxExecutorDiagnostic(
                CreateSfxCueFrame(includeEnemyDeathExit: false).Cues,
                playbackPort: null,
                diagnostics => diagnostics.PortMissingCount,
                expectedCount: 2);
            AssertCoreSfxExecutorDiagnostic(
                CreateSfxCueFrame(includeEnemyDeathExit: false).Cues,
                new RecordingGameplaySfxPlaybackPort(GameplaySfxPlaybackResultKind.MapMissing),
                diagnostics => diagnostics.MapMissingCount,
                expectedCount: 2);
            AssertCoreSfxExecutorDiagnostic(
                CreateSfxCueFrame(includeEnemyDeathExit: false).Cues,
                new RecordingGameplaySfxPlaybackPort(GameplaySfxPlaybackResultKind.BindingMissing),
                diagnostics => diagnostics.BindingMissingCount,
                expectedCount: 2);
            AssertCoreSfxExecutorDiagnostic(
                CreateSfxCueFrame(includeEnemyDeathExit: false).Cues,
                new RecordingGameplaySfxPlaybackPort(GameplaySfxPlaybackResultKind.OwnerViewMissing),
                diagnostics => diagnostics.OwnerViewMissingCount,
                expectedCount: 2);
            AssertCoreSfxExecutorDiagnostic(
                CreateSfxCueFrame(includeEnemyDeathExit: false).Cues,
                new RecordingGameplaySfxPlaybackPort(GameplaySfxPlaybackResultKind.NoOpSuppressed),
                diagnostics => diagnostics.PlaybackNoOpSuppressedCount,
                expectedCount: 2);
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_AttachedAndFallbackParity_Remains()
        {
            var legacyRoot = new GameObject(nameof(CoreSfx_AttachedAndFallbackParity_Remains) + "_Legacy");
            var orchestrationRoot = new GameObject(nameof(CoreSfx_AttachedAndFallbackParity_Remains) + "_Orchestration");
            var mapBundle = CreateGameplayAudioMap(new Dictionary<GameplayAudioSemanticId, AudioAttachmentSlot>
            {
                { GameplayAudioSemanticId.PlayerDamage, AudioAttachmentSlot.FromId("body") },
            });
            try
            {
                var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0));
                var legacyPresenter = CreatePresenter(legacyRoot);
                var legacyPort = new RecordingGameplayAudioPlaybackPort();
                legacyPresenter.AttachGameplayAudioRuntime(legacyPort, mapBundle.Map);
                legacyPresenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                legacyPresenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), new[] { player }));

                var orchestrationPresenter = CreatePresenter(orchestrationRoot);
                var orchestrationPort = new RecordingGameplayAudioPlaybackPort();
                orchestrationPresenter.AttachGameplayAudioRuntime(orchestrationPort, mapBundle.Map);
                orchestrationPresenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                orchestrationPresenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), new[] { player }));

                Assert.That(legacyPort.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(orchestrationPort.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(legacyPort.AttachedCalls[0].Context.DebugTag, Is.EqualTo(orchestrationPort.AttachedCalls[0].Context.DebugTag));
                Assert.That(legacyPort.AttachedCalls[0].Slot.Id, Is.EqualTo(orchestrationPort.AttachedCalls[0].Slot.Id));
                Assert.That(orchestrationPresenter.CoreGameplaySfxExecutorDiagnostics.OwnerViewMissingCount, Is.Zero);

                legacyPort.Clear();
                orchestrationPort.Clear();
                legacyPresenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                orchestrationPresenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                legacyPresenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));
                orchestrationPresenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(legacyPort.AttachedCalls, Is.Empty);
                Assert.That(orchestrationPort.AttachedCalls, Is.Empty);
                Assert.That(legacyPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(orchestrationPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(legacyPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo(orchestrationPort.TwoDCalls[0].Context.DebugTag));
                Assert.That(orchestrationPresenter.CoreGameplaySfxExecutorDiagnostics.OwnerViewMissingCount, Is.EqualTo(1));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(orchestrationRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_PresentInitial_ClearsExecutorDiagnosticsAndOwnershipGuard()
        {
            var rootObject = new GameObject(nameof(CoreGameplaySfx_PresentInitial_ClearsExecutorDiagnosticsAndOwnershipGuard));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);

                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.EqualTo(1));
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));

                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));

                Assert.That(presenter.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(presenter.CoreGameplaySfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfx_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState()
        {
            var rootObject = new GameObject(nameof(CoreGameplaySfx_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState));
            try
            {
                var port = new RecordingGameplaySfxPlaybackPort();
                var coordinator = CreateInitializedCoreSfxCoordinator(
                    rootObject,
                    port);

                coordinator.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), tickIndex: 61));
                Assert.That(port.Requests, Has.Count.EqualTo(1));
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.EqualTo(1));

                coordinator.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(port.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));

                coordinator.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), tickIndex: 62));
                coordinator.HardCleanupPresentationExtensions();
                Assert.That(coordinator.CoreGameplaySfxExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
                Assert.That(
                    typeof(PresentationCue).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
                Assert.That(
                    typeof(PresentationPlaybackPlan).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void Determinism_NonContamination_AfterCoreSfxDefaultSwitch()
        {
            var rootObject = new GameObject(nameof(Determinism_NonContamination_AfterCoreSfxDefaultSwitch));
            try
            {
                var coordinator = CreateInitializedCoreSfxCoordinator(
                    rootObject,
                    playbackPort: new RecordingGameplaySfxPlaybackPort());
                var result = CreateTickResult(
                    CreateAllCoreSfxPresentationData(),
                    finalEntities: new[] { CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    tickIndex: 71,
                    eventLog: new[] { "BeforePresentation" },
                    determinismHash: "hash-before");
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;

                coordinator.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                coordinator.Present(result);

                Assert.That(coordinator.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(coordinator.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(coordinator.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                Assert.That(result.DeterminismHash, Is.EqualTo("hash-before"));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfxExecutionGuard_DuplicateOwnerAttempt_BlocksSecondAttempt()
        {
            var guard = new CoreGameplaySfxExecutionGuard();
            var key = new CoreGameplaySfxPlaybackKey(
                tickIndex: 5,
                PresentationSemanticSource.PlayerDamage,
                sourceEntityId: 10,
                targetEntityId: 10,
                PresentationSfxCueKey.PlayerDamage);

            Assert.That(
                guard.TryBeginExecution(CoreGameplaySfxExecutionOwner.CurrentExecutor, key),
                Is.True);
            Assert.That(
                guard.TryBeginExecution(CoreGameplaySfxExecutionOwner.CurrentExecutor, key),
                Is.False);
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void CoreGameplaySfxPlaybackPortAdapter_SeparatesMapBindingTargetAndPortDiagnostics()
        {
            var stateStore = new GameplayPresentationStateStore();
            var adapter = new GameplaySfxPlaybackPortAdapter(stateStore);
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var request = CreateSfxPlaybackRequest(PresentationSfxCueKey.PlayerDamage, targetEntityId: 10);

            adapter.AttachRuntime(null, null);
            Assert.That(adapter.TryPlayCoreGameplaySfx(request, out var result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(GameplaySfxPlaybackResultKind.PortMissing));

            adapter.AttachRuntime(playbackPort, null);
            Assert.That(adapter.TryPlayCoreGameplaySfx(request, out result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(GameplaySfxPlaybackResultKind.MapMissing));

            using (var mapBundle = CreateGameplayAudioMap(excludedSemantics: new[] { GameplayAudioSemanticId.PlayerDamage }))
            {
                adapter.AttachRuntime(playbackPort, mapBundle.Map);
                Assert.That(adapter.TryPlayCoreGameplaySfx(request, out result), Is.False);
                Assert.That(result.Kind, Is.EqualTo(GameplaySfxPlaybackResultKind.MapMissing));
            }

            using (var mapBundle = CreateGameplayAudioMap())
            {
                adapter.AttachRuntime(playbackPort, mapBundle.Map);
                var missingTargetRequest = CreateSfxPlaybackRequest(PresentationSfxCueKey.PlayerDamage, targetEntityId: 0);
                Assert.That(adapter.TryPlayCoreGameplaySfx(missingTargetRequest, out result), Is.False);
                Assert.That(result.Kind, Is.EqualTo(GameplaySfxPlaybackResultKind.TargetMissing));
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_DebugRefreshCoreSfx_DoesNotCreateLegacyPendingPlan()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_DebugRefreshCoreSfx_DoesNotCreateLegacyPendingPlan));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                presenter.DebugRefreshGameplayAudioPlan(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(presenter.PendingGameplayAudioRequestCount, Is.Zero);

                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                Assert.That(presenter.PendingGameplayAudioRequestCount, Is.Zero);
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_DoesNotReplayConsumedPlan_OnLaterCycleWithoutNewRequests()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_DoesNotReplayConsumedPlan_OnLaterCycleWithoutNewRequests));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));
                presenter.Present(CreateTickResult(TickPresentationData.Empty));

                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("PlayerDamage"));
            }
            finally
            {
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_RequiresCoLocatedAudioRuntimeInstaller_WhenGameplayPresentationAudioConfigIsAssigned()
        {
            var hostObject = new GameObject(nameof(GameplaySceneHost_Initialize_RequiresCoLocatedAudioRuntimeInstaller_WhenGameplayPresentationAudioConfigIsAssigned));
            var mapBundle = CreateGameplayAudioMap();
            var audioConfig = CreateGameplayPresentationAudioConfig(mapBundle.Map);
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(CreateHostConfiguration(audioConfig)));

                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayPresentationAudioConfig is assigned."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioConfig);
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_FailsBeforeFirstTick_WhenRequiredSemanticIsMissing()
        {
            var hostObject = new GameObject(nameof(GameplaySceneHost_Initialize_FailsBeforeFirstTick_WhenRequiredSemanticIsMissing));
            var mapBundle = CreateGameplayAudioMap(excludedSemantics: new[] { GameplayAudioSemanticId.EntityExitOutOfBounds });
            var audioConfig = CreateGameplayPresentationAudioConfig(mapBundle.Map);
            try
            {
                hostObject.AddComponent<AudioRuntimeInstaller>();
                var host = hostObject.AddComponent<GameplaySceneHost>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(CreateHostConfiguration(audioConfig)));

                Assert.That(
                    exception.Message,
                    Is.EqualTo($"GameplayAudioMap '{mapBundle.Map.name}' is missing required gameplay audio semantics: EntityExitOutOfBounds."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioConfig);
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_AllowsNullGameplayPresentationAudioConfig_WithoutAudioRuntimeInstaller()
        {
            var hostObject = new GameObject(nameof(GameplaySceneHost_Initialize_AllowsNullGameplayPresentationAudioConfig_WithoutAudioRuntimeInstaller));
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                Assert.DoesNotThrow(() => host.Initialize(CreateHostConfiguration(null)));
                Assert.That(host.Presenter, Is.Not.Null);
                Assert.DoesNotThrow(() => host.Presenter.Present(CreateTickResult(TickPresentationData.Empty)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_SucceedsWithAssignedMap_AndSameRootAudioRuntimeInstaller()
        {
            var hostObject = new GameObject(nameof(GameplaySceneHost_Initialize_SucceedsWithAssignedMap_AndSameRootAudioRuntimeInstaller));
            var mapBundle = CreateGameplayAudioMap();
            var audioConfig = CreateGameplayPresentationAudioConfig(mapBundle.Map);
            try
            {
                var installer = hostObject.AddComponent<AudioRuntimeInstaller>();
                var host = hostObject.AddComponent<GameplaySceneHost>();

                Assert.DoesNotThrow(() => host.Initialize(CreateHostConfiguration(audioConfig)));
                Assert.That(host.Presenter, Is.Not.Null);
                Assert.That(installer.RuntimeRoot, Is.Not.Null);
                Assert.That(installer.AudioService, Is.Not.Null);
                Assert.That(installer.AudioSettingsService, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioConfig);
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_DoesNotUseSceneGlobalAudioRuntimeInstallerFallback()
        {
            var otherRoot = new GameObject(nameof(GameplaySceneHost_Initialize_DoesNotUseSceneGlobalAudioRuntimeInstallerFallback) + "_OtherRoot");
            var hostObject = new GameObject(nameof(GameplaySceneHost_Initialize_DoesNotUseSceneGlobalAudioRuntimeInstallerFallback));
            var mapBundle = CreateGameplayAudioMap();
            var audioConfig = CreateGameplayPresentationAudioConfig(mapBundle.Map);
            try
            {
                otherRoot.AddComponent<AudioRuntimeInstaller>();
                var host = hostObject.AddComponent<GameplaySceneHost>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(CreateHostConfiguration(audioConfig)));

                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayPresentationAudioConfig is assigned."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioConfig);
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(otherRoot);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioPlaybackPort_ExposesNoPlayBgmCapability()
        {
            var methodNames = typeof(IGameplayAudioPlaybackPort)
                .GetMethods()
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(methodNames, Is.EqualTo(new[] { "Play2D", "PlayAttached" }));
            Assert.That(methodNames, Does.Not.Contain("PlayBgm"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticCatalog_RequiredSemantics_StayInsideApprovedHostFamilies()
        {
            GameplayAudioGovernanceAssertions.AssertOnlyApprovedHostFamilies(
                GameplayAudioSemanticCatalog.RequiredOneShotV1,
                "Gameplay audio host required semantics");
        }

        private static GameplayTickPresentationCoordinator CreateInitializedCoreSfxCoordinator(
            GameObject rootObject,
            IGameplaySfxPlaybackPort playbackPort,
            bool duplicateExecutors = false)
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyPresentationExecutionPipeline,
                (requestedPort, executionGuard) => CreateCoreSfxTestPipeline(
                    playbackPort ?? requestedPort,
                    executionGuard,
                    duplicateExecutors));
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new SimpleViewFactory(registry.transform));

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                CreateTimingProfile());
            return coordinator;
        }

        private static GameplayPresentationPipeline CreateCoreSfxTestPipeline(
            IGameplaySfxPlaybackPort playbackPort,
            CoreGameplaySfxExecutionGuard executionGuard,
            bool duplicateExecutors)
        {
            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new GameplaySfxPresentationExecutor(playbackPort, executionGuard),
                    new GameplaySfxPresentationExecutor(playbackPort, executionGuard),
                }
                : new IPresentationExecutor[]
                {
                    new GameplaySfxPresentationExecutor(playbackPort, executionGuard),
                };
            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new SfxCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static GameplayTickViewPresenter CreatePresenter(GameObject rootObject)
        {
            return CreatePresenter(rootObject, null);
        }

        private static GameplayTickViewPresenter CreatePresenter(
            GameObject rootObject,
            IGameplayEntityViewFactory viewFactory,
            EnemyAudioExecutionPipelineFactory enemyAudioExecutionPipelineFactory = null)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            presenter.BindCoordinator(GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                enemyAudioExecutionPipelineFactory: enemyAudioExecutionPipelineFactory));
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                viewFactory ?? new SimpleViewFactory(registry.transform));
            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                CreateTimingProfile());
            return presenter;
        }

        private static GameplaySfxSemanticDiagnostics AssertSemanticDiagnostics(
            GameplaySfxExecutorDiagnostics diagnostics,
            PresentationSfxCueKey cueKey,
            int planned,
            int requested,
            int succeeded,
            int fallback)
        {
            var semanticDiagnostics = diagnostics.SemanticDiagnostics.Single(entry => entry.CueKey == cueKey);
            Assert.That(semanticDiagnostics.PlannedCount, Is.EqualTo(planned), cueKey.ToString());
            Assert.That(semanticDiagnostics.RequestedCount, Is.EqualTo(requested), cueKey.ToString());
            Assert.That(semanticDiagnostics.SucceededCount, Is.EqualTo(succeeded), cueKey.ToString());
            Assert.That(semanticDiagnostics.DiagnosticCount, Is.EqualTo(fallback), cueKey.ToString());
            Assert.That(semanticDiagnostics.DuplicateSuppressedCount, Is.Zero, cueKey.ToString());
            return semanticDiagnostics;
        }

        private static void AssertCoreSfxRequest(
            in GameplaySfxPlaybackRequest request,
            PresentationSfxCueKey expectedCueKey,
            PresentationSemanticSource expectedSemanticSource,
            int targetEntityId,
            int sourceEntityId,
            EntityType expectedEntityType,
            TickEntityExitCause expectedExitCause,
            int expectedSourceActorEntityId,
            PresentationAnchorKind expectedAnchorKind,
            int expectedTickIndex)
        {
            Assert.That(request.CueKey, Is.EqualTo(expectedCueKey));
            Assert.That(request.TickIndex, Is.EqualTo(expectedTickIndex));
            Assert.That(request.Source.TickIndex, Is.EqualTo(expectedTickIndex));
            Assert.That(request.Source.SemanticSource, Is.EqualTo(expectedSemanticSource));
            Assert.That(request.Source.SourceEntityId, Is.EqualTo(sourceEntityId));
            Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(targetEntityId)));
            Assert.That(request.OwnerEntityId, Is.EqualTo(targetEntityId));
            Assert.That(request.Anchor.Kind, Is.EqualTo(expectedAnchorKind));
            Assert.That(request.OwnershipKey.TickIndex, Is.EqualTo(expectedTickIndex));
            Assert.That(request.OwnershipKey.SemanticSource, Is.EqualTo(expectedSemanticSource));
            Assert.That(request.OwnershipKey.SourceEntityId, Is.EqualTo(sourceEntityId));
            Assert.That(request.OwnershipKey.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(expectedCueKey));
            Assert.That(request.SfxPayload.EntityType, Is.EqualTo((int)expectedEntityType));
            Assert.That(request.SfxPayload.ExitCause, Is.EqualTo((int)expectedExitCause));
            Assert.That(request.SfxPayload.SourceActorEntityId, Is.EqualTo(expectedSourceActorEntityId));
        }

        private static void AssertCoreSfxExecutorDiagnostic(
            IReadOnlyList<PresentationCue> cues,
            IGameplaySfxPlaybackPort playbackPort,
            Func<GameplaySfxExecutorDiagnostics, int> selector,
            int expectedCount)
        {
            var plan = new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                tickIndex: 51,
                cues,
                new PresentationCueFrameDiagnostics(sourceFactCount: cues.Count, plannedCueCount: cues.Count, plannerCount: 1)));
            var executor = new GameplaySfxPresentationExecutor(
                playbackPort,
                new CoreGameplaySfxExecutionGuard());

            executor.Play(plan);

            Assert.That(selector(executor.Diagnostics), Is.EqualTo(expectedCount));
        }

        private static PresentationCueFrame CreateSfxCueFrame(bool includeEnemyDeathExit)
        {
            var data = includeEnemyDeathExit
                ? CreateCoreSfxPresentationData(10, 50)
                : CreateDamageSfxPresentationData();
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(new TickPresentationFactExtractor().Extract(CreateTickResult(data)));
        }

        private static CoreSfxSemanticCase[] CreateCoreSfxSemanticCases()
        {
            return new[]
            {
                new CoreSfxSemanticCase(
                    "PlayerDamage",
                    CreatePlayerDamagePresentationData(10),
                    PresentationSfxCueKey.PlayerDamage,
                    GameplayAudioSemanticId.PlayerDamage,
                    targetEntityId: 10,
                    sourceEntityId: 10,
                    EntityType.None,
                    TickEntityExitCause.None,
                    sourceActorEntityId: 0),
                new CoreSfxSemanticCase(
                    "EnemyDamage",
                    CreateEnemyDamagePresentationData(20),
                    PresentationSfxCueKey.EnemyDamage,
                    GameplayAudioSemanticId.EnemyDamage,
                    targetEntityId: 20,
                    sourceEntityId: 20,
                    EntityType.None,
                    TickEntityExitCause.None,
                    sourceActorEntityId: 0),
                new CoreSfxSemanticCase(
                    "EntityExitItemConsume",
                    CreateExitPresentationData(30, TickEntityExitCause.ItemConsume, EntityType.Unit, sourceActorEntityId: 10),
                    PresentationSfxCueKey.EntityExitItemConsume,
                    GameplayAudioSemanticId.EntityExitItemConsume,
                    targetEntityId: 30,
                    sourceEntityId: 30,
                    EntityType.Unit,
                    TickEntityExitCause.ItemConsume,
                    sourceActorEntityId: 10),
                new CoreSfxSemanticCase(
                    "EntityExitBoxDestroy",
                    CreateExitPresentationData(40, TickEntityExitCause.BoxDestroy, EntityType.Box, sourceActorEntityId: 10),
                    PresentationSfxCueKey.EntityExitBoxDestroy,
                    GameplayAudioSemanticId.EntityExitBoxDestroy,
                    targetEntityId: 40,
                    sourceEntityId: 40,
                    EntityType.Box,
                    TickEntityExitCause.BoxDestroy,
                    sourceActorEntityId: 10),
                new CoreSfxSemanticCase(
                    "EntityExitEnemyDeath",
                    CreateExitPresentationData(50, TickEntityExitCause.EnemyDeath, EntityType.Unit, sourceActorEntityId: 10),
                    PresentationSfxCueKey.EntityExitEnemyDeath,
                    GameplayAudioSemanticId.EntityExitEnemyDeath,
                    targetEntityId: 50,
                    sourceEntityId: 50,
                    EntityType.Unit,
                    TickEntityExitCause.EnemyDeath,
                    sourceActorEntityId: 10),
                new CoreSfxSemanticCase(
                    "EntityExitOutOfBounds",
                    CreateExitPresentationData(60, TickEntityExitCause.OutOfBounds, EntityType.Unit),
                    PresentationSfxCueKey.EntityExitOutOfBounds,
                    GameplayAudioSemanticId.EntityExitOutOfBounds,
                    targetEntityId: 60,
                    sourceEntityId: 60,
                    EntityType.Unit,
                    TickEntityExitCause.OutOfBounds,
                    sourceActorEntityId: 0),
            };
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(GameplayPresentationAudioConfig audioConfig)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = Array.Empty<EntityState>(),
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayPresentationAudioConfig = audioConfig,
            };
        }

        private static GameplayPresentationAudioConfig CreateGameplayPresentationAudioConfig(
            GameplayAudioMap gameplayAudioMap)
        {
            var config = ScriptableObject.CreateInstance<GameplayPresentationAudioConfig>();
            config.name = "GameplayPresentationAudioConfig_Test";
            SetSerializedField(typeof(GameplayPresentationAudioConfig), config, "gameplayAudioMap", gameplayAudioMap);
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "blockAudioMap",
                LoadCanonical<BlockAudioMap>(BlockAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "playerLocomotionAudioMap",
                LoadCanonical<PlayerLocomotionAudioMap>(PlayerLocomotionAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "topologyAudioMap",
                LoadCanonical<TopologyAudioMap>(TopologyAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "gravityFieldAudioMap",
                LoadCanonical<GravityFieldAudioMap>(GravityFieldAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "tileFeatureAudioMap",
                LoadCanonical<TileFeatureAudioMap>(TileFeatureAudioMapPath));
            return config;
        }

        private static T LoadCanonical<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
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

        private static TickPresentationData CreatePlayerDamagePresentationData(int entityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(entityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreatePlayerDeathPresentationData(int entityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickPlayerDeathPresentationSignal(
                        entityId,
                        didDieThisTick: true,
                        sourceEntityId: 30,
                        fallbackFacing: Direction.Down,
                        resolvedDamageSourceAvailable: true,
                        damageAmountAtFatalHit: 1,
                        deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyDamagePresentationData(int entityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyDamagePresentationSignal(entityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateDamageSfxPresentationData()
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateTopologyLockedCoreSfxPresentationData()
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward),
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        30,
                        TickEntityExitCause.ItemConsume,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                });
        }

        private static TickPresentationData CreateEnemyDamageAndDeathPresentationData(int enemyEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyDamagePresentationSignal(enemyEntityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        enemyEntityId,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                });
        }

        private static TickPresentationData CreateExitPresentationData(
            int entityId,
            TickEntityExitCause exitCause,
            EntityType entityType = EntityType.Unit,
            int? sourceActorEntityId = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        entityId,
                        exitCause,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        entityType,
                        sourceActorEntityId),
                });
        }

        private static TickPresentationData CreateAfterEntityMotionBoxDestroyPresentationData(int entityId)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            return new TickPresentationData(
                new[]
                {
                    new TickEntityMotion(
                        entityId,
                        TickEntityMotionKind.BoxSlide,
                        sourceCell,
                        destinationCell),
                },
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        entityId,
                        TickEntityExitCause.BoxDestroy,
                        sourceCell,
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Right,
                        EntityType.Box,
                        sourceActorEntityId: 10,
                        timing: EntityExitPresentationTiming.AfterEntityMotion),
                });
        }

        private static TickPresentationData CreateCoreSfxPresentationData(int playerEntityId, int exitedEnemyEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(playerEntityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEnemyEntityId,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit),
                });
        }

        private static TickPresentationData CreateAllCoreSfxPresentationData()
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        30,
                        TickEntityExitCause.ItemConsume,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        40,
                        TickEntityExitCause.BoxDestroy,
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Box,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        50,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 1),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        60,
                        TickEntityExitCause.OutOfBounds,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit),
                });
        }

        private static TickPresentationData CreateMixedAudioPresentationData(int playerEntityId, int enemyEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        playerEntityId,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 17,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        targetEntityId: enemyEntityId,
                        direction: Direction.Right,
                        actionPlanId: 170),
                },
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(playerEntityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        enemyEntityId,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: playerEntityId),
                });
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null,
            int tickIndex = 1,
            IEnumerable<string> eventLog = null,
            string determinismHash = "")
        {
            return new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                eventLog ?? Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                determinismHash,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static GameplayAudioRequest CreateRequest(
            GameplayAudioSemanticId semanticId,
            int? ownerEntityId,
            float delaySeconds = 0f)
        {
            return new GameplayAudioRequest(
                semanticId,
                ownerEntityId,
                new AudioPlaybackContext(debugTag: GameplayAudioSemanticCatalog.Format(semanticId)),
                delaySeconds);
        }

        private static GameplaySfxPlaybackRequest CreateSfxPlaybackRequest(
            PresentationSfxCueKey cueKey,
            int targetEntityId,
            float delaySeconds = 0f,
            PresentationSemanticSource semanticSource = PresentationSemanticSource.PlayerDamage)
        {
            var source = new PresentationSource(
                tickIndex: 5,
                semanticSource,
                sourceEntityId: targetEntityId);
            var target = PresentationTarget.Entity(targetEntityId);
            var key = new CoreGameplaySfxPlaybackKey(
                source.TickIndex,
                source.SemanticSource,
                source.SourceEntityId,
                targetEntityId,
                cueKey);
            return new GameplaySfxPlaybackRequest(
                key,
                cueKey,
                source,
                target,
                targetEntityId > 0 ? PresentationAnchor.ForEntityCenter(targetEntityId) : PresentationAnchor.None(),
                delaySeconds: delaySeconds);
        }

        private static EntityState CreateUnit(int entityId, UnitRole unitRole, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
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

        private static GameplayAudioMapBundle CreateGameplayAudioMap(
            IReadOnlyDictionary<GameplayAudioSemanticId, AudioAttachmentSlot> attachmentSlotsBySemantic = null,
            IReadOnlyCollection<GameplayAudioSemanticId> excludedSemantics = null)
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definitions = new Dictionary<GameplayAudioSemanticId, AudioDefinition>();
            var serializedObject = new SerializedObject(map);
            var entries = new List<(GameplayAudioSemanticId semanticId, AudioDefinition definition, AudioAttachmentSlot slot)>();

            foreach (var semanticId in GameplayAudioSemanticCatalog.RequiredOneShotV1)
            {
                if (excludedSemantics != null && excludedSemantics.Contains(semanticId))
                {
                    continue;
                }

                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                definitions[semanticId] = definition;
                var slot = attachmentSlotsBySemantic != null && attachmentSlotsBySemantic.TryGetValue(semanticId, out var attachmentSlot)
                    ? attachmentSlot
                    : default;
                entries.Add((semanticId, definition, slot));
            }

            var entriesProperty = serializedObject.FindProperty("entries");
            entriesProperty.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("SemanticId").enumValueIndex = (int)entries[i].semanticId;
                var binding = element.FindPropertyRelative("Binding");
                binding.FindPropertyRelative("definition").objectReferenceValue = entries[i].definition;
                binding.FindPropertyRelative("attachmentSlot").FindPropertyRelative("id").stringValue = entries[i].slot.Id;
                binding.FindPropertyRelative("policy").managedReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return new GameplayAudioMapBundle(map, definitions.Values.ToArray());
        }

        private static EnemyAudioProfileBundle CreateEnemyDeathAudioProfile()
        {
            var profile = ScriptableObject.CreateInstance<EnemyAudioProfile>();
            profile.name = "EnemyAudioProfile_CoreSfxTelemetryDeath_Test";
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            definition.name = "EnemyDeath_Def_Test";
            SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Sfx);

            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            var entries = new[]
            {
                new EnemyAudioEntry
                {
                    Cue = EnemyAudioCue.Death,
                    Binding = binding,
                    IsOptional = false,
                },
            };
            SetSerializedField(typeof(EnemyAudioProfile), profile, "entries", entries);
            return new EnemyAudioProfileBundle(profile, definition);
        }

        private static ActionAudioProfileBundle CreateActionAudioPushWindupProfile()
        {
            var profile = ScriptableObject.CreateInstance<GameplayActionAudioProfile>();
            profile.name = "ActionAudioProfile_MixedOrdering_Test";
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            definition.name = "ActionPushWindup_Def_Test";
            SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Sfx);

            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            var entries = new[]
            {
                new GameplayActionAudioEntry
                {
                    Action = GameplayActionKind.Push,
                    Moment = GameplayActionAudioMoment.Windup,
                    Binding = binding,
                    IsOptional = false,
                },
            };
            SetSerializedField(typeof(GameplayActionAudioProfile), profile, "entries", entries);
            return new ActionAudioProfileBundle(profile, definition);
        }

        private sealed class GameplayAudioMapBundle : IDisposable
        {
            private readonly UnityEngine.Object[] _definitions;

            public GameplayAudioMapBundle(GameplayAudioMap map, UnityEngine.Object[] definitions)
            {
                Map = map;
                _definitions = definitions;
            }

            public GameplayAudioMap Map { get; }

            public void Dispose()
            {
                for (var i = 0; i < _definitions.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_definitions[i]);
                }

                UnityEngine.Object.DestroyImmediate(Map);
            }
        }

        private sealed class ActionAudioProfileBundle : IDisposable
        {
            private readonly UnityEngine.Object _definition;

            public ActionAudioProfileBundle(GameplayActionAudioProfile profile, UnityEngine.Object definition)
            {
                Profile = profile;
                _definition = definition;
            }

            public GameplayActionAudioProfile Profile { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_definition);
                UnityEngine.Object.DestroyImmediate(Profile);
            }
        }

        private sealed class EnemyAudioProfileBundle : IDisposable
        {
            private readonly UnityEngine.Object _definition;

            public EnemyAudioProfileBundle(EnemyAudioProfile profile, UnityEngine.Object definition)
            {
                Profile = profile;
                _definition = definition;
            }

            public EnemyAudioProfile Profile { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_definition);
                UnityEngine.Object.DestroyImmediate(Profile);
            }
        }

        private readonly struct CoreSfxSemanticCase
        {
            public CoreSfxSemanticCase(
                string name,
                TickPresentationData presentationData,
                PresentationSfxCueKey cueKey,
                GameplayAudioSemanticId audioSemantic,
                int targetEntityId,
                int sourceEntityId,
                EntityType entityType,
                TickEntityExitCause exitCause,
                int sourceActorEntityId)
            {
                Name = name;
                PresentationData = presentationData;
                CueKey = cueKey;
                AudioSemantic = audioSemantic;
                TargetEntityId = targetEntityId;
                SourceEntityId = sourceEntityId;
                EntityType = entityType;
                ExitCause = exitCause;
                SourceActorEntityId = sourceActorEntityId;
            }

            public string Name { get; }

            public TickPresentationData PresentationData { get; }

            public PresentationSfxCueKey CueKey { get; }

            public GameplayAudioSemanticId AudioSemantic { get; }

            public int TargetEntityId { get; }

            public int SourceEntityId { get; }

            public EntityType EntityType { get; }

            public TickEntityExitCause ExitCause { get; }

            public int SourceActorEntityId { get; }
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            private readonly Action<string> _traceSink;
            private readonly bool _traceDebugTag;

            public RecordingGameplayAudioPlaybackPort(Action<string> traceSink = null, bool traceDebugTag = false)
            {
                _traceSink = traceSink;
                _traceDebugTag = traceDebugTag;
            }

            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();
            public readonly List<(AudioDefinition Definition, GameplayEntityView Owner, AudioAttachmentSlot Slot, AudioPlaybackContext Context)> AttachedCalls = new();

            public void Clear()
            {
                TwoDCalls.Clear();
                AttachedCalls.Clear();
            }

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                _traceSink?.Invoke(_traceDebugTag ? $"Legacy:{context.DebugTag}" : "Playback:Play2D");
                TwoDCalls.Add((definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                _traceSink?.Invoke(_traceDebugTag ? $"Legacy:{context.DebugTag}" : "Playback:PlayAttached");
                AttachedCalls.Add((definition, (GameplayEntityView)owner, slot, context));
            }
        }

        private sealed class RecordingGameplaySfxPlaybackPort : IGameplaySfxPlaybackPort
        {
            private readonly GameplaySfxPlaybackResultKind _resultKind;
            private readonly Action<string> _traceSink;

            public RecordingGameplaySfxPlaybackPort(
                GameplaySfxPlaybackResultKind resultKind = GameplaySfxPlaybackResultKind.Succeeded)
            {
                _resultKind = resultKind;
            }

            public RecordingGameplaySfxPlaybackPort(Action<string> traceSink)
                : this()
            {
                _traceSink = traceSink;
            }

            public readonly List<GameplaySfxPlaybackRequest> Requests = new();

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public bool TryPlayCoreGameplaySfx(
                in GameplaySfxPlaybackRequest request,
                out GameplaySfxPlaybackResult result)
            {
                _traceSink?.Invoke($"CoreProduction:{request.CueKey}");
                Requests.Add(request);
                result = new GameplaySfxPlaybackResult(_resultKind);
                return _resultKind == GameplaySfxPlaybackResultKind.Succeeded ||
                       _resultKind == GameplaySfxPlaybackResultKind.Requested ||
                       _resultKind == GameplaySfxPlaybackResultKind.OwnerViewMissing;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                Requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                Requests.Clear();
            }
        }

        private sealed class RecordingEnemyAudioPlaybackPort : IGameplayEnemyAudioPlaybackPort
        {
            private readonly Action<string> _traceSink;

            public RecordingEnemyAudioPlaybackPort(Action<string> traceSink)
            {
                _traceSink = traceSink;
            }

            public readonly List<GameplayEnemyAudioPlaybackRequest> Requests = new();

            public bool TryPlayEnemyAudio(
                in GameplayEnemyAudioPlaybackRequest request,
                out GameplayEnemyAudioPlaybackResult result)
            {
                _traceSink?.Invoke($"EnemyProduction:{request.CueKey}");
                Requests.Add(request);
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.Succeeded);
                return true;
            }

            public void ResetSession()
            {
                Requests.Clear();
            }

            public void HardCleanup()
            {
                Requests.Clear();
            }
        }

        private sealed class SimpleViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            public SimpleViewFactory(Transform parent)
            {
                _parent = parent;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                return view;
            }
        }

        private sealed class EnemyAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;
            private readonly EnemyAudioProfile _profile;

            public EnemyAudioViewFactory(Transform parent, EnemyAudioProfile profile)
            {
                _parent = parent;
                _profile = profile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                if (entity.unitRole == UnitRole.Enemy && _profile != null)
                {
                    var authoring = viewObject.AddComponent<EnemyAudioAuthoring>();
                    SetSerializedField(typeof(EnemyAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
            }
        }

        private sealed class MixedAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;
            private readonly GameplayActionAudioProfile _actionProfile;
            private readonly EnemyAudioProfile _enemyProfile;

            public MixedAudioViewFactory(
                Transform parent,
                GameplayActionAudioProfile actionProfile,
                EnemyAudioProfile enemyProfile)
            {
                _parent = parent;
                _actionProfile = actionProfile;
                _enemyProfile = enemyProfile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                if (entity.unitRole == UnitRole.Player && _actionProfile != null)
                {
                    var authoring = viewObject.AddComponent<GameplayActionAudioAuthoring>();
                    SetSerializedField(typeof(GameplayActionAudioAuthoring), authoring, "profile", _actionProfile);
                }

                if (entity.unitRole == UnitRole.Enemy && _enemyProfile != null)
                {
                    var authoring = viewObject.AddComponent<EnemyAudioAuthoring>();
                    SetSerializedField(typeof(EnemyAudioAuthoring), authoring, "profile", _enemyProfile);
                }

                return view;
            }
        }
    }
}
