using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureAudioRuntimeTests
    {
        [Test]
        [Category("Extended")]
        public void TileFeatureAudioRequestPlanner_EmptyRequests_ReturnsEmpty()
        {
            var planner = new TileFeatureAudioRequestPlanner();

            Assert.That(planner.BuildRequests(Array.Empty<TilePresentationRequest>()), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioRequestPlanner_ButtonActivated_PreservesPayload()
        {
            var planner = new TileFeatureAudioRequestPlanner();
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);

            var requests = planner.BuildRequests(new[]
            {
                CreateTilePresentationRequest(100, cell, sourceEntityId: 30, ownerEntityId: 40, teamId: 2),
            });

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.ButtonActivated));
            Assert.That(requests[0].TileId, Is.EqualTo(100));
            Assert.That(requests[0].Cell, Is.EqualTo(cell));
            Assert.That(requests[0].SourceEntityId, Is.EqualTo(30));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].TeamId, Is.EqualTo(2));
            Assert.That(requests[0].Context.OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].Context.DebugTag, Is.EqualTo("ButtonActivated"));
            Assert.That(requests[0].TargetEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioRequestPlanner_DestroyTileTriggered_PreservesPayload()
        {
            var planner = new TileFeatureAudioRequestPlanner();
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);

            var requests = planner.BuildRequests(new[]
            {
                CreateTilePresentationRequest(
                    100,
                    cell,
                    sourceEntityId: 30,
                    ownerEntityId: 40,
                    teamId: 2,
                    requestKind: TilePresentationRequestKind.DestroyTileTriggered,
                    tileFeatureKind: TileFeatureKind.Destroy,
                    targetEntityId: 50),
            });

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.DestroyTileTriggered));
            Assert.That(requests[0].TileId, Is.EqualTo(100));
            Assert.That(requests[0].Cell, Is.EqualTo(cell));
            Assert.That(requests[0].SourceEntityId, Is.EqualTo(30));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].TeamId, Is.EqualTo(2));
            Assert.That(requests[0].Context.OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].Context.DebugTag, Is.EqualTo("DestroyTileTriggered"));
            Assert.That(requests[0].TargetEntityId, Is.EqualTo(50));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioRequestPlanner_SlideTileRedirected_PreservesPayload()
        {
            var planner = new TileFeatureAudioRequestPlanner();
            var cell = new SurfaceCell(FaceId.Front, 2, 3);

            var requests = planner.BuildRequests(new[]
            {
                CreateTilePresentationRequest(
                    100,
                    cell,
                    sourceEntityId: 30,
                    ownerEntityId: 40,
                    teamId: 2,
                    requestKind: TilePresentationRequestKind.SlideTileRedirected,
                    tileFeatureKind: TileFeatureKind.Slide,
                    targetEntityId: 50),
            });

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.SlideTileRedirected));
            Assert.That(requests[0].TileId, Is.EqualTo(100));
            Assert.That(requests[0].Cell, Is.EqualTo(cell));
            Assert.That(requests[0].SourceEntityId, Is.EqualTo(30));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].TeamId, Is.EqualTo(2));
            Assert.That(requests[0].Context.OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].Context.DebugTag, Is.EqualTo("SlideTileRedirected"));
            Assert.That(requests[0].TargetEntityId, Is.EqualTo(50));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioRequestPlanner_PreservesOrderAndDuplicates_AndSkipsUnknownKinds()
        {
            var planner = new TileFeatureAudioRequestPlanner();
            var first = CreateTilePresentationRequest(30, new SurfaceCell(FaceId.Floor, 0, 0));
            var duplicate = CreateTilePresentationRequest(30, new SurfaceCell(FaceId.Floor, 0, 0));
            var second = CreateTilePresentationRequest(10, new SurfaceCell(FaceId.Floor, 1, 0));
            var unknown = new TilePresentationRequest(
                (TilePresentationRequestKind)999,
                999,
                new SurfaceCell(FaceId.Front, 1, 1),
                TileFeatureKind.Button,
                0,
                0,
                0);

            var requests = planner.BuildRequests(new[] { first, unknown, duplicate, second });

            Assert.That(requests.Select(request => request.TileId).ToArray(), Is.EqualTo(new[] { 30, 30, 10 }));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioRequestPlanner_DoesNotReferenceAuthorityOrTileEventSources()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Runtime/TileFeatureAudioRequestPlanner.cs");
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "TickPipeline",
                "TickPresentationData",
                "TileEvents",
                "TilePresentationRequestPlanner",
            };

            AssertForbiddenTokensAbsent(source, forbiddenTokens);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioMap_ValidateRequiredCues_FailsWhenButtonActivatedMissing()
        {
            var map = ScriptableObject.CreateInstance<TileFeatureAudioMap>();
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => map.ValidateRequiredCuesOrThrow(TileFeatureAudioCueCatalog.RequiredOneShotV1));
                Assert.That(
                    exception.Message,
                    Is.EqualTo($"TileFeatureAudioMap '{map.name}' is missing required tile feature audio cues: ButtonActivated."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioMap_ValidateOrThrow_RejectsDuplicateAndEmptyCues()
        {
            using var scope = new TestAssetScope();
            var duplicateMap = scope.CreateMap();
            var emptyMap = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            var binding = scope.CreateBinding(definition);

            SetEntries(duplicateMap, (TileFeatureAudioCue.ButtonActivated, binding), (TileFeatureAudioCue.ButtonActivated, binding));
            SetEntries(emptyMap, (TileFeatureAudioCue.None, binding));

            Assert.That(
                Assert.Throws<InvalidOperationException>(() => duplicateMap.ValidateOrThrow()).Message,
                Does.Contain("duplicate tile feature audio cue 'ButtonActivated'"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => emptyMap.ValidateOrThrow()).Message,
                Does.Contain("contains an empty tile feature audio cue"));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioMap_ValidateOrThrow_RejectsInvalidBindingsAndCategories()
        {
            using var scope = new TestAssetScope();

            AssertValidationError(scope, null, "is missing an AudioBinding");
            AssertValidationError(scope, scope.CreateBinding(null), "is missing an AudioDefinition binding");
            AssertValidationError(scope, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: true)), "only allows one-shot definitions");
            AssertValidationError(scope, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Master, loop: false)), "Master is reserved");
            AssertValidationError(scope, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Ui, loop: false)), "only [Sfx] are allowed");
            AssertValidationError(scope, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Bgm, loop: false)), "only [Sfx] are allowed");
            AssertValidationError(scope, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Voice, loop: false)), "only [Sfx] are allowed");
            AssertValidationError(scope, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Ambience, loop: false)), "only [Sfx] are allowed");
            AssertValidationError(
                scope,
                scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false), policy: new TestAudioPlaybackPolicy()),
                "AudioBinding.Policy");
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioMap_ValidateOrThrow_AllowsValidSfxOneShot()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));

            Assert.DoesNotThrow(() => map.ValidateRequiredCuesOrThrow(TileFeatureAudioCueCatalog.RequiredOneShotV1));
            Assert.That(map.ResolveOrThrow(TileFeatureAudioCue.ButtonActivated).Definition.Category, Is.EqualTo(AudioCategory.Sfx));
            Assert.That(map.TryResolveOptional(TileFeatureAudioCue.DestroyTileTriggered, out _), Is.False);
            Assert.That(map.TryResolveOptional(TileFeatureAudioCue.SlideTileRedirected, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioMap_TryResolveOptional_ReturnsOptionalBindingsWhenPresent()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var binding = scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false));
            var slideBinding = scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false));
            SetEntries(
                map,
                (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))),
                (TileFeatureAudioCue.DestroyTileTriggered, binding),
                (TileFeatureAudioCue.SlideTileRedirected, slideBinding));

            Assert.That(map.TryResolveOptional(TileFeatureAudioCue.DestroyTileTriggered, out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(binding));
            Assert.That(map.TryResolveOptional(TileFeatureAudioCue.SlideTileRedirected, out var resolvedSlide), Is.True);
            Assert.That(resolvedSlide, Is.SameAs(slideBinding));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_PlaybackPolicy_FallbackDuplicatesOrderAndCache()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(definition, AudioAttachmentSlot.FromId("tile"))));
            var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var first = CreateTileAudioRequest(30, ownerEntityId: 100);
            var second = CreateTileAudioRequest(10, ownerEntityId: 0);
            var plannedRequests = new List<TileFeatureAudioRequest> { first, first, second };
            var before = plannedRequests.Select(request => request.TileId).ToArray();

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(plannedRequests);
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.AttachedCalls, Is.Empty);
            Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(3));
            Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
            {
                "ButtonActivated:30",
                "ButtonActivated:30",
                "ButtonActivated:10",
            }));
            Assert.That(plannedRequests.Select(request => request.TileId).ToArray(), Is.EqualTo(before));

            controller.ReplacePendingPlan(Array.Empty<TileFeatureAudioRequest>());
            controller.PlayPlannedAudio();
            Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_DestroyTileMissingOptionalBinding_NoOps()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
            var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
            var playbackPort = new RecordingGameplayAudioPlaybackPort();

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(new[]
            {
                CreateTileAudioRequest(
                    30,
                    ownerEntityId: 0,
                    cue: TileFeatureAudioCue.DestroyTileTriggered,
                    targetEntityId: 20),
            });
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.TwoDCalls, Is.Empty);
            Assert.That(playbackPort.AttachedCalls, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_SlideTileMissingOptionalBinding_NoOps()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
            var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
            var playbackPort = new RecordingGameplayAudioPlaybackPort();

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(new[]
            {
                CreateTileAudioRequest(
                    30,
                    ownerEntityId: 0,
                    cue: TileFeatureAudioCue.SlideTileRedirected,
                    targetEntityId: 20),
            });
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.TwoDCalls, Is.Empty);
            Assert.That(playbackPort.AttachedCalls, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_DestroyTileBinding_PlaysWhenPresent()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(
                map,
                (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))),
                (TileFeatureAudioCue.DestroyTileTriggered, scope.CreateBinding(definition)));
            var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
            var playbackPort = new RecordingGameplayAudioPlaybackPort();

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(new[]
            {
                CreateTileAudioRequest(
                    30,
                    ownerEntityId: 0,
                    cue: TileFeatureAudioCue.DestroyTileTriggered,
                    targetEntityId: 20),
            });
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
            Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(definition));
            Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("DestroyTileTriggered:30"));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_SlideTileBinding_PlaysDuplicatesWhenPresent()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(
                map,
                (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))),
                (TileFeatureAudioCue.SlideTileRedirected, scope.CreateBinding(definition)));
            var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
            var playbackPort = new RecordingGameplayAudioPlaybackPort();

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(new[]
            {
                CreateTileAudioRequest(
                    30,
                    ownerEntityId: 0,
                    cue: TileFeatureAudioCue.SlideTileRedirected,
                    targetEntityId: 20),
                CreateTileAudioRequest(
                    30,
                    ownerEntityId: 0,
                    cue: TileFeatureAudioCue.SlideTileRedirected,
                    targetEntityId: 20),
            });
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(2));
            Assert.That(playbackPort.TwoDCalls.All(call => ReferenceEquals(call.Definition, definition)), Is.True);
            Assert.That(
                playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                Is.EqualTo(new[] { "SlideTileRedirected:30", "SlideTileRedirected:30" }));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_AttachesWhenOwnerViewIsLive()
        {
            using var scope = new TestAssetScope();
            var ownerObject = new GameObject(nameof(TileFeatureAudioPresentationController_AttachesWhenOwnerViewIsLive));
            try
            {
                var map = scope.CreateMap();
                SetEntries(
                    map,
                    (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false), AudioAttachmentSlot.FromId("tile"))));
                var stateStore = new GameplayPresentationStateStore();
                var ownerView = ownerObject.AddComponent<GameplayEntityView>();
                ownerView.Initialize(100);
                stateStore.ViewsByEntityId[100] = ownerView;
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var controller = new TileFeatureAudioPresentationController(stateStore);

                controller.AttachRuntime(playbackPort, map);
                controller.ReplacePendingPlan(new[] { CreateTileAudioRequest(30, ownerEntityId: 100) });
                controller.PlayPlannedAudio();

                Assert.That(playbackPort.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.AttachedCalls[0].Owner, Is.SameAs(ownerView));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudioPresentationController_DoesNotReferenceForbiddenAuthorityTypes()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureAudioPresentationController.cs");
            AssertForbiddenTokensAbsent(source, new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "TickPipeline",
                "TickPresentationData",
                "TileEvents",
                "TileFeatureVisual",
                "GameObject.Find",
                "FindObject",
                "AudioManager",
            });
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_PlaysTileFeatureAudioFromCurrentTilePresentationRequests()
        {
            using var scope = new TestAssetScope();
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_PlaysTileFeatureAudioFromCurrentTilePresentationRequests));
            try
            {
                var map = scope.CreateMap();
                SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachTileFeatureAudioRuntime(playbackPort, map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                var result = CreateTickResult(CreatePresentationData(CreateButtonActivatedTileEvent(100)));
                var hashBeforePresent = result.DeterminismHash;

                presenter.Present(result);
                presenter.Present(CreateTickResult(TickPresentationData.Empty));

                Assert.That(result.DeterminismHash, Is.EqualTo(hashBeforePresent));
                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("ButtonActivated"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_FailedLatchPathPlaysNothing()
        {
            using var scope = new TestAssetScope();
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_FailedLatchPathPlaysNothing));
            try
            {
                var map = scope.CreateMap();
                SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
                var presenter = CreatePresenter(rootObject);
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachTileFeatureAudioRuntime(playbackPort, map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(TickPresentationData.Empty));

                Assert.That(playbackPort.TwoDCalls, Is.Empty);
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_TileFeatureAudioMap_HostRuntimePolicy()
        {
            using var scope = new TestAssetScope();
            var hostObject = new GameObject(nameof(GameplaySceneHost_TileFeatureAudioMap_HostRuntimePolicy));
            var otherRoot = new GameObject(nameof(GameplaySceneHost_TileFeatureAudioMap_HostRuntimePolicy) + "_OtherRoot");
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                Assert.DoesNotThrow(() => host.Initialize(CreateHostConfiguration(tileFeatureAudioMap: null)));

                var map = scope.CreateMap();
                SetEntries(map, (TileFeatureAudioCue.ButtonActivated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
                otherRoot.AddComponent<AudioRuntimeInstaller>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(CreateHostConfiguration(map)));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when TileFeatureAudioMap is assigned."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudio_ArchitectureBoundaries_StaySeparateFromCoreGameplayUiAndActionAudio()
        {
            var tileAudioReferences = typeof(TileFeatureAudioRequestPlanner).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var hostReferences = typeof(GameplayTickViewPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(tileAudioReferences, Does.Contain("Game.Feature.Gameplay"));
            Assert.That(tileAudioReferences, Does.Contain("Game.Shared.Audio"));
            Assert.That(tileAudioReferences, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(tileAudioReferences, Does.Not.Contain("Game.Feature.Gameplay.Audio"));
            Assert.That(tileAudioReferences, Does.Not.Contain("Game.Feature.Gameplay.ActionAudio"));
            Assert.That(hostReferences, Does.Contain("Game.Feature.Gameplay.TileFeatureAudio"));
            Assert.That(Enum.GetNames(typeof(Game.Feature.Gameplay.Audio.GameplayAudioSemanticId)), Does.Not.Contain("ButtonActivated"));

            Assert.That(ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/UiAudioCueMap.cs"), Does.Not.Contain("ButtonActivated"));
            Assert.That(ReadRepoFile("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime/GameplayActionAudioProfile.cs"), Does.Not.Contain("TileFeatureAudio"));
            Assert.That(ReadRepoFile("Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs"), Does.Not.Contain("TileFeatureAudio"));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureAudio_DoesNotEnterTickPipelineOrBoardState()
        {
            var pipelineDirectory = GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime");
            foreach (var pipelineFile in Directory.GetFiles(pipelineDirectory, "TickPipeline*.cs", SearchOption.TopDirectoryOnly))
            {
                Assert.That(File.ReadAllText(pipelineFile), Does.Not.Contain("TileFeatureAudio"), pipelineFile);
            }

            var boardStateDirectory = GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime");
            foreach (var boardStateFile in Directory.GetFiles(boardStateDirectory, "*.cs", SearchOption.AllDirectories))
            {
                Assert.That(File.ReadAllText(boardStateFile), Does.Not.Contain("TileFeatureAudio"), boardStateFile);
            }
        }

        private static void AssertValidationError(TestAssetScope scope, AudioBinding binding, string expectedMessageFragment)
        {
            var map = scope.CreateMap();
            SetEntries(map, (TileFeatureAudioCue.ButtonActivated, binding));

            Assert.That(
                Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow()).Message,
                Does.Contain(expectedMessageFragment));
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

        private static GameplaySceneHostConfiguration CreateHostConfiguration(TileFeatureAudioMap tileFeatureAudioMap)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = Array.Empty<EntityState>(),
                InitialTerrain = GameplayTerrainData.Empty,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                TileFeatureAudioMap = tileFeatureAudioMap,
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

        private static TickResult CreateTickResult(TickPresentationData presentationData)
        {
            return new TickResult(
                1,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static TickPresentationData CreatePresentationData(params TilePresentationEvent[] tileEvents)
        {
            return new TickPresentationData(
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
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents);
        }

        private static TilePresentationEvent CreateButtonActivatedTileEvent(int tileId)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.ButtonActivated,
                tileId,
                new SurfaceCell(FaceId.Floor, 0, 0),
                TileFeatureKind.Button,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateTilePresentationRequest(
            int tileId,
            SurfaceCell cell,
            int sourceEntityId = 0,
            int ownerEntityId = 0,
            int teamId = 0,
            TilePresentationRequestKind requestKind = TilePresentationRequestKind.ButtonActivated,
            TileFeatureKind tileFeatureKind = TileFeatureKind.Button,
            int targetEntityId = 0)
        {
            return new TilePresentationRequest(
                requestKind,
                tileId,
                cell,
                tileFeatureKind,
                sourceEntityId,
                ownerEntityId,
                teamId,
                targetEntityId);
        }

        private static TileFeatureAudioRequest CreateTileAudioRequest(
            int tileId,
            int ownerEntityId,
            TileFeatureAudioCue cue = TileFeatureAudioCue.ButtonActivated,
            int targetEntityId = 0)
        {
            return new TileFeatureAudioRequest(
                cue,
                tileId,
                new SurfaceCell(FaceId.Floor, 0, 0),
                sourceEntityId: tileId + 1,
                ownerEntityId: ownerEntityId,
                teamId: tileId + 3,
                new AudioPlaybackContext(
                    ownerEntityId: ownerEntityId > 0 ? ownerEntityId : null,
                    debugTag: $"{TileFeatureAudioCueCatalog.Format(cue)}:{tileId}"),
                targetEntityId);
        }

        private static void SetEntries(
            TileFeatureAudioMap map,
            params (TileFeatureAudioCue cue, AudioBinding binding)[] entries)
        {
            var entryType = typeof(TileFeatureAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);
            var array = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                entryType.GetField("Cue").SetValue(entry, entries[i].cue);
                entryType.GetField("Binding").SetValue(entry, entries[i].binding);
                array.SetValue(entry, i);
            }

            SetSerializedField(typeof(TileFeatureAudioMap), map, "entries", array);
        }

        private static void AssertForbiddenTokensAbsent(string source, IReadOnlyList<string> forbiddenTokens)
        {
            for (var i = 0; i < forbiddenTokens.Count; i++)
            {
                Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), $"Forbidden token '{forbiddenTokens[i]}' was present.");
            }
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath));
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            private readonly List<TwoDCall> _twoDCalls = new();
            private readonly List<AttachedCall> _attachedCalls = new();

            public IReadOnlyList<TwoDCall> TwoDCalls => _twoDCalls;

            public IReadOnlyList<AttachedCall> AttachedCalls => _attachedCalls;

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                _twoDCalls.Add(new TwoDCall(definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                _attachedCalls.Add(new AttachedCall(definition, owner, slot, context));
            }
        }

        private readonly struct TwoDCall
        {
            public TwoDCall(AudioDefinition definition, AudioPlaybackContext context)
            {
                Definition = definition;
                Context = context;
            }

            public AudioDefinition Definition { get; }

            public AudioPlaybackContext Context { get; }
        }

        private readonly struct AttachedCall
        {
            public AttachedCall(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                AudioPlaybackContext context)
            {
                Definition = definition;
                Owner = owner;
                Slot = slot;
                Context = context;
            }

            public AudioDefinition Definition { get; }

            public Component Owner { get; }

            public AudioAttachmentSlot Slot { get; }

            public AudioPlaybackContext Context { get; }
        }

        private sealed class TestAssetScope : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects = new();

            public TileFeatureAudioMap CreateMap()
            {
                var map = Track(ScriptableObject.CreateInstance<TileFeatureAudioMap>());
                map.name = "TileFeatureAudioMap_Test";
                return map;
            }

            public AudioBinding CreateBinding(
                AudioDefinition definition,
                AudioAttachmentSlot attachmentSlot = default,
                AudioPlaybackPolicy policy = null)
            {
                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", attachmentSlot);
                SetSerializedField(typeof(AudioBinding), binding, "policy", policy);
                return binding;
            }

            public SingleAudioDefinition CreateDefinition(AudioCategory category, bool loop)
            {
                var clip = Track(AudioClip.Create($"{category}_{loop}_Clip", 1, 1, 44100, false));
                var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
                definition.name = $"{category}_{loop}_Def";
                SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                SetSerializedField(typeof(AudioDefinition), definition, "category", category);
                SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                SetSerializedField(typeof(AudioDefinition), definition, "loop", loop);
                return definition;
            }

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

            private T Track<T>(T unityObject) where T : UnityEngine.Object
            {
                _trackedObjects.Add(unityObject);
                return unityObject;
            }
        }

        private sealed class TestAudioPlaybackPolicy : AudioPlaybackPolicy
        {
        }

        private sealed class SimpleViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _root;

            public SimpleViewFactory(Transform root)
            {
                _root = root;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var gameObject = new GameObject($"View_{entity.entityId}");
                gameObject.transform.SetParent(_root, worldPositionStays: false);
                var view = gameObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                return view;
            }
        }
    }
}
