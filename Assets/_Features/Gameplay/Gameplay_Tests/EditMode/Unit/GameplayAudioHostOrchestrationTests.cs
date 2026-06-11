using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

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
        [Category("Extended")]
        public void GameplayAudioPresentationController_ResetAndDetach_ClearPendingPlan()
        {
            var mapBundle = CreateGameplayAudioMap();
            var stateStore = new GameplayPresentationStateStore();
            var controller = new GameplayAudioPresentationController(stateStore);
            try
            {
                controller.AttachRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                controller.ReplacePendingPlan(new[]
                {
                    CreateRequest(GameplayAudioSemanticId.PlayerDamage, ownerEntityId: 10),
                });

                Assert.That(controller.PendingRequestCount, Is.EqualTo(1));

                controller.ResetSession();
                Assert.That(controller.PendingRequestCount, Is.Zero);

                controller.ReplacePendingPlan(new[]
                {
                    CreateRequest(GameplayAudioSemanticId.PlayerDamage, ownerEntityId: 10),
                });
                Assert.That(controller.PendingRequestCount, Is.EqualTo(1));

                controller.DetachRuntime();
                Assert.That(controller.PendingRequestCount, Is.Zero);
            }
            finally
            {
                mapBundle.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentInitial_ClearsPendingGameplayAudioPlan()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentInitial_ClearsPendingGameplayAudioPlan));
            var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject);
                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                presenter.DebugRefreshGameplayAudioPlan(CreateTickResult(CreatePlayerDamagePresentationData(10)));

                Assert.That(presenter.PendingGameplayAudioRequestCount, Is.EqualTo(1));

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
        public void GameplayAudioPresentationController_ReplacePendingPlan_IsLastWriteWins_AndClearsAfterPlayback()
        {
            var mapBundle = CreateGameplayAudioMap();
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var controller = new GameplayAudioPresentationController(new GameplayPresentationStateStore());
            try
            {
                controller.AttachRuntime(playbackPort, mapBundle.Map);
                controller.ReplacePendingPlan(new[]
                {
                    CreateRequest(GameplayAudioSemanticId.PlayerDamage, ownerEntityId: 10),
                });
                controller.ReplacePendingPlan(new[]
                {
                    CreateRequest(GameplayAudioSemanticId.EnemyDamage, ownerEntityId: 20),
                    CreateRequest(GameplayAudioSemanticId.EnemyDamage, ownerEntityId: 20),
                });

                controller.PlayPlannedAudio();

                Assert.That(controller.PendingRequestCount, Is.Zero);
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "EnemyDamage",
                    "EnemyDamage",
                }));

                controller.PlayPlannedAudio();
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(2));
            }
            finally
            {
                mapBundle.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioPresentationController_EmptyReplaceAndClear_AreSafeAndIdempotent()
        {
            var mapBundle = CreateGameplayAudioMap();
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var controller = new GameplayAudioPresentationController(new GameplayPresentationStateStore());
            try
            {
                controller.AttachRuntime(playbackPort, mapBundle.Map);
                controller.ReplacePendingPlan(Array.Empty<GameplayAudioRequest>());
                controller.ClearPendingPlan();
                controller.ClearPendingPlan();
                controller.PlayPlannedAudio();

                Assert.That(controller.PendingRequestCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
            }
            finally
            {
                mapBundle.Dispose();
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioDeferredQueue_SameRequest_EnqueuedOnce_AndPlayedOnceAfterUnlock()
        {
            var mapBundle = CreateGameplayAudioMap();
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var controller = new GameplayAudioPresentationController(new GameplayPresentationStateStore());
            try
            {
                controller.AttachRuntime(playbackPort, mapBundle.Map);
                controller.SetPlaybackGateState(GameplayAudioPlaybackGateState.TopologyLocked);
                controller.ReplacePendingPlan(
                    new[] { CreateRequest(GameplayAudioSemanticId.PlayerDamage, ownerEntityId: 10) },
                    tickIndex: 7);
                controller.PlayPlannedAudio();
                controller.ReplacePendingPlan(
                    new[] { CreateRequest(GameplayAudioSemanticId.PlayerDamage, ownerEntityId: 10) },
                    tickIndex: 7);
                controller.PlayPlannedAudio();

                Assert.That(controller.PendingRequestCount, Is.Zero);
                Assert.That(controller.DeferredRequestCount, Is.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);

                controller.SetPlaybackGateState(GameplayAudioPlaybackGateState.Open);
                controller.Update(0f);
                controller.Update(0f);

                Assert.That(controller.DeferredRequestCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "PlayerDamage",
                }));
            }
            finally
            {
                mapBundle.Dispose();
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioPendingTimers_TopologyTransition_DoNotAdvanceWhileLocked_AndResumeAfterUnlock()
        {
            var mapBundle = CreateGameplayAudioMap();
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var controller = new GameplayAudioPresentationController(new GameplayPresentationStateStore());
            try
            {
                controller.AttachRuntime(playbackPort, mapBundle.Map);
                controller.SetPlaybackGateState(GameplayAudioPlaybackGateState.TopologyLocked);
                controller.ReplacePendingPlan(
                    new[] { CreateRequest(GameplayAudioSemanticId.PlayerDamage, ownerEntityId: 10, delaySeconds: 0.2f) },
                    tickIndex: 8);

                controller.Update(1f);
                Assert.That(controller.PendingRequestCount, Is.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);

                controller.SetPlaybackGateState(GameplayAudioPlaybackGateState.Open);
                controller.Update(0.19f);
                Assert.That(controller.PendingRequestCount, Is.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);

                controller.Update(0.02f);
                Assert.That(controller.PendingRequestCount, Is.Zero);
                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "PlayerDamage",
                }));
            }
            finally
            {
                mapBundle.Dispose();
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

        [Test]
        [Category("Extended")]
        public void GameplayAudioPresentationController_RemainsOneShotOnly_WithoutPlannerOrContinuousHandleState()
        {
            var fieldTypes = typeof(GameplayAudioPresentationController)
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.That(fieldTypes, Has.No.Member(typeof(GameplayAudioRequestPlanner)));
            Assert.That(fieldTypes, Has.No.Member(typeof(IAudioService)));
            Assert.That(fieldTypes, Has.No.Member(typeof(AudioPlaybackHandle)));
        }

        private static GameplayTickViewPresenter CreatePresenter(GameObject rootObject)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new SimpleViewFactory(registry.transform));
            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                CreateTimingProfile());
            return presenter;
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(GameplayPresentationAudioConfig audioConfig)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = Array.Empty<EntityState>(),
                InitialTerrain = GameplayTerrainData.Empty,
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

        private static TickPresentationData CreateExitPresentationData(int entityId, TickEntityExitCause exitCause)
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
                        EntityType.Unit),
                });
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null)
        {
            return new TickResult(
                1,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
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

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            private readonly Action<string> _traceSink;

            public RecordingGameplayAudioPlaybackPort(Action<string> traceSink = null)
            {
                _traceSink = traceSink;
            }

            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();
            public readonly List<(AudioDefinition Definition, GameplayEntityView Owner, AudioAttachmentSlot Slot, AudioPlaybackContext Context)> AttachedCalls = new();

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                _traceSink?.Invoke("Playback:Play2D");
                TwoDCalls.Add((definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                _traceSink?.Invoke("Playback:PlayAttached");
                AttachedCalls.Add((definition, (GameplayEntityView)owner, slot, context));
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
    }
}
