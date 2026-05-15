using System;
using System.IO;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxTileFeatureGravityFieldMigrationTests
    {
        [Test]
        [Category("Extended")]
        public void ProductionRuntime_TileFeatureAndGravityFieldRequests_ArePlannedByVfxRuntime()
        {
            var owner = new GameObject("TileFeatureGravityFieldRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    tileEvents: new[]
                    {
                        new TilePresentationEvent(
                            TilePresentationEventKind.ButtonActivated,
                            1,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            TileFeatureKind.Button,
                            10,
                            0,
                            1),
                    },
                    gravityFieldEvents: new[]
                    {
                        new GravityFieldPresentationEvent(
                            GravityFieldPresentationEventKind.Activated,
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0)),
                    },
                    gravityFieldVisualStates: new[]
                    {
                        new GravityFieldVisualState(
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            GravityFieldPhase.Active,
                            1,
                            3,
                            1f,
                            lockedTargetEntityIds: new[] { 10 }),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(4));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_TileFeatureAndGravityFieldFlagsOff_DoNotPlanFallbacks()
        {
            var owner = new GameObject("TileFeatureGravityFieldFlagsOffRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxTileFeatureLane = false;
                runtime.EnableGameplayVfxGravityFieldEvents = false;
                runtime.EnableGameplayVfxGravityFieldContinuous = false;
                runtime.EnableGameplayVfxGravityFieldLockedTarget = false;

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    tileEvents: new[]
                    {
                        new TilePresentationEvent(
                            TilePresentationEventKind.ButtonActivated,
                            1,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            TileFeatureKind.Button,
                            10,
                            0,
                            1),
                    },
                    gravityFieldEvents: new[]
                    {
                        new GravityFieldPresentationEvent(
                            GravityFieldPresentationEventKind.Activated,
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0)),
                    },
                    gravityFieldVisualStates: new[]
                    {
                        new GravityFieldVisualState(
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            GravityFieldPhase.Active,
                            1,
                            3,
                            1f,
                            lockedTargetEntityIds: new[] { 10 }),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_GravityFieldContinuousState_DropsWhenPresentationStateDisappears()
        {
            var owner = new GameObject("GravityFieldContinuousRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    gravityFieldVisualStates: new[]
                    {
                        new GravityFieldVisualState(
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            GravityFieldPhase.Active,
                            1,
                            3,
                            1f,
                            lockedTargetEntityIds: new[] { 10 }),
                    })));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));

                runtime.Present(CreateExtensionContext(CreatePresentationData()));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_TileFeatureActiveState_DropsWhenPresentationStateDisappears()
        {
            var owner = new GameObject("TileFeatureActiveStateRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    tileFeatureVisualStates: new[]
                    {
                        new TileFeatureVisualState(
                            101,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            TileFeatureKind.Barricade,
                            true,
                            sourceEntityId: 40,
                            ownerEntityId: 0,
                            teamId: 2),
                    },
                    tileFeatureActiveVisualStates: new[]
                    {
                        new TileFeatureActiveVisualState(
                            100,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            TileFeatureKind.Destroy,
                            sourceEntityId: 10,
                            ownerEntityId: 0,
                            teamId: 1),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(2));

                runtime.Present(CreateExtensionContext(CreatePresentationData()));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_ActivatedButtonState_CreatesStyledPersistentButtonActiveLoop()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var presentationData = CreatePresentationData(
                tileFeatureVisualStates: new[]
                {
                    new TileFeatureVisualState(
                        901,
                        cell,
                        TileFeatureKind.Button,
                        true,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1),
                });
            var context = new GameplayVfxPlanningContext(
                12,
                presentationData,
                new CubeTopologyState(FaceId.Floor),
                tileFeatureVfxStyleBindings: new[]
                {
                    new TileFeatureVfxStyleBinding(901, VfxStyleKey.Green),
                });

            planner.Plan(context, builder);
            var plan = builder.Build();

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            var request = plan.Requests[0];
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey.TileId, Is.EqualTo(901));
            Assert.That(request.StyleKey, Is.EqualTo(VfxStyleKey.Green));
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DoesNotReferencePr28VisualControllersOrVfxController()
        {
            var coordinator = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs");

            Assert.That(coordinator, Does.Not.Contain("TileFeatureVisualPresentationController"));
            Assert.That(coordinator, Does.Not.Contain("GravityFieldVisualPresentationController"));
            Assert.That(coordinator, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(coordinator, Does.Not.Contain("GravityFieldVisualTargetView"));
            Assert.That(coordinator, Does.Not.Contain("GameplayVfxPresentationController"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayHost_DoesNotDirectlyDependOnVfxCoreOrAuthoring()
        {
            var hostAsmdef = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Gameplay.Host.asmdef");

            Assert.That(hostAsmdef, Does.Not.Contain("Gameplay.Vfx"));
            Assert.That(hostAsmdef, Does.Not.Contain("Gameplay.Vfx.Authoring"));
        }

        [Test]
        [Category("Extended")]
        public void Governance_DocumentsTileFeatureAndGravityFieldNoLegacyFallbackPolicy()
        {
            var document = ReadRepoFile("Docs/Architecture/Gameplay-VFX-Governance.md");

            Assert.That(document, Does.Contain("TileFeature and GravityField visual migration is VFX-lane only."));
            Assert.That(document, Does.Contain("There is no coordinator fallback for TileFeature or GravityField visual lanes."));
            Assert.That(document, Does.Contain("does not restore PR #28 direct visual controllers"));
            Assert.That(document, Does.Contain("TileFeatureAudio and GravityFieldAudio are not VFX."));
        }

        [Test]
        [Category("Extended")]
        public void Authoring_DefaultCueMapContainsDestroyTileAndBarricadeBindings()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset");
            var sparkBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeatureDestroySpark_Binding.asset");
            var laserBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeatureDestroyLaserActive_Binding.asset");
            var barricadeBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_BarricadeActiveLoop_Binding.asset");
            var greenButtonBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonActiveLoop_Green_Binding.asset");
            var yellowButtonBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonActiveLoop_Yellow_Binding.asset");

            Assert.That(cueMap, Is.Not.Null);
            Assert.That(sparkBinding, Is.Not.Null);
            Assert.That(laserBinding, Is.Not.Null);
            Assert.That(barricadeBinding, Is.Not.Null);
            Assert.That(greenButtonBinding, Is.Not.Null);
            Assert.That(yellowButtonBinding, Is.Not.Null);
            Assert.That(sparkBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileTriggered)));
            Assert.That(laserBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive)));
            Assert.That(barricadeBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeActiveLoop)));
            Assert.That(greenButtonBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(yellowButtonBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(greenButtonBinding.StyleKey, Is.EqualTo(VfxStyleKey.Green));
            Assert.That(yellowButtonBinding.StyleKey, Is.EqualTo(VfxStyleKey.Yellow));
            Assert.That(sparkBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(sparkBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(laserBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(laserBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(barricadeBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(barricadeBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            AssertButtonActiveLoopBinding(greenButtonBinding);
            AssertButtonActiveLoopBinding(yellowButtonBinding);

            var runtimeMap = cueMap.BuildRuntimeMap();
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileTriggered), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeActiveLoop), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Green, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Yellow, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Default, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Authoring_ButtonActiveLoopPrefabs_AreLoopingPlayOnAwakeAndNotPrefabLocalOnButtons()
        {
            AssertLoopingPlayOnAwake(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/TileFeatureButtonActivatedVfx.prefab");
            AssertLoopingPlayOnAwake(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/TileFeatureMoonBlockButtonActivatedVfx.prefab");

            var defaultButtonPrefab = ReadRepoFile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_Default.prefab");
            var moonButtonPrefab = ReadRepoFile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_MoonOnly.prefab");

            Assert.That(defaultButtonPrefab, Does.Contain("buttonActivatedParticles: {fileID: 0}"));
            Assert.That(moonButtonPrefab, Does.Contain("buttonActivatedParticles: {fileID: 0}"));
            Assert.That(defaultButtonPrefab, Does.Not.Contain("guid: 751080a0ee13c914f9b17bd4ab9d198b"));
            Assert.That(moonButtonPrefab, Does.Not.Contain("guid: d7184659b8b2d0740a0898761e0bde6b"));
        }

        private static void AssertButtonActiveLoopBinding(VfxBindingDefinitionAsset binding)
        {
            Assert.That(binding.Prefab, Is.Not.Null);
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.3f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(32));
        }

        private static void AssertLoopingPlayOnAwake(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, prefabPath);
            var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(particleSystems, Is.Not.Empty, prefabPath);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var main = particleSystems[i].main;
                Assert.That(main.loop, Is.True, $"{prefabPath} particle[{i}] loop");
                Assert.That(main.playOnAwake, Is.True, $"{prefabPath} particle[{i}] playOnAwake");
            }
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(TickPresentationData presentationData)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.right, Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(presentationData, topology),
                topology,
                stateStore,
                projector);
        }

        private static TickResult CreateResult(TickPresentationData presentationData, CubeTopologyState topology)
        {
            return new TickResult(
                12,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { CreateUnit(10), CreateUnit(40) },
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            TilePresentationEvent[] tileEvents = null,
            GravityFieldPresentationEvent[] gravityFieldEvents = null,
            GravityFieldVisualState[] gravityFieldVisualStates = null,
            TileFeatureVisualState[] tileFeatureVisualStates = null,
            TileFeatureActiveVisualState[] tileFeatureActiveVisualStates = null)
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates,
                tileFeatureVisualStates: tileFeatureVisualStates,
                tileFeatureActiveVisualStates: tileFeatureActiveVisualStates);
        }

        private static EntityState CreateUnit(int entityId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, entityId == 10 ? 0 : 1, 0),
                hp = 3,
                maxHp = 3,
                teamId = entityId == 10 ? 1 : 2,
                type = EntityType.Unit,
                unitRole = entityId == 10 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static string ReadRepoFile(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(fullPath);
        }
    }
}
