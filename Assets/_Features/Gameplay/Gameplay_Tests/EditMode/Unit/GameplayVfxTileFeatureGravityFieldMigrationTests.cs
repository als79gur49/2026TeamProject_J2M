using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxTileFeatureGravityFieldMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string EntranceSpawnBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_EntranceSpawn_Binding.asset";
        private const string EntranceSpawnPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/TileFeature_EntranceSpawn_Vfx.prefab";

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
        [Category("Core")]
        public void ProductionRuntime_InitialEntranceSpawn_MissingBindingPolicySkipsWithoutCrash()
        {
            var owner = new GameObject("InitialEntranceSpawnRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("host default cue map is not configured"));
                runtime.PresentInitial(CreateInitialExtensionContext(CreateInitialPresentationData()));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialEntranceSpawnRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialActiveEntranceSpawnInstanceCount, Is.Zero);
                Assert.That(runtime.IsHostDefaultMapConfigured, Is.False);
                Assert.That(runtime.MapNotConfiguredCount, Is.EqualTo(1));
                Assert.That(runtime.InitialRequestSkippedBecauseMapNotConfiguredCount, Is.EqualTo(1));
                Assert.That(runtime.LastMapNotConfiguredContext, Is.EqualTo("PresentInitial"));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionRuntime_InitialEntranceSpawn_ConfiguredEmptyMap_ReportsMissingBindingOnly()
        {
            var owner = new GameObject("InitialEntranceSpawnRuntimeWithEmptyCueMap");
            var emptyCueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(emptyCueMap);

                runtime.PresentInitial(CreateInitialExtensionContext(CreateInitialPresentationData()));

                Assert.That(runtime.IsHostDefaultMapConfigured, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialEntranceSpawnRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialActiveEntranceSpawnInstanceCount, Is.Zero);
                Assert.That(runtime.MapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.InitialRequestSkippedBecauseMapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(emptyCueMap);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_InitialEntranceSpawn_WithDefaultCueMap_ResolvesAndPlays()
        {
            var owner = new GameObject("InitialEntranceSpawnRuntimeWithCueMap");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(LoadHostDefaultCueMap());
                CreatePresentationRuntimeInputs(
                    out var topology,
                    out var stateStore,
                    out var projector);

                runtime.PresentInitial(CreateInitialExtensionContext(
                    CreateInitialPresentationData(),
                    topology,
                    stateStore,
                    projector));

                var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn);
                Assert.That(runtime.IsHostDefaultMapConfigured, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialEntranceSpawnRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialActiveEntranceSpawnInstanceCount, Is.EqualTo(1));
                Assert.That(runtime.MapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.InitialRequestSkippedBecauseMapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.GetActiveVfxInstanceCount(cueId), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxProductionRuntime_FirstEnemyProfileConfigure_DoesNotHardCleanupExistingTileFeatureOneShot()
        {
            var owner = new GameObject("FirstEnemyProfileConfigureEntranceRuntime");
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(LoadHostDefaultCueMap());
                CreatePresentationRuntimeInputs(
                    out var topology,
                    out var stateStore,
                    out var projector);

                runtime.PresentInitial(CreateInitialExtensionContext(
                    CreateInitialPresentationData(),
                    topology,
                    stateStore,
                    projector));

                var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn);
                Assert.That(runtime.GetActiveVfxInstanceCount(cueId), Is.EqualTo(1));

                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(),
                    topology,
                    stateStore,
                    projector,
                    enemyPresentationCatalog: catalog));

                Assert.That(runtime.GetActiveVfxInstanceCount(cueId), Is.EqualTo(1));
                Assert.That(runtime.EnemyProfileFirstConfigureCount, Is.EqualTo(1));
                Assert.That(runtime.TileFeatureHardCleanupCount, Is.Zero);
                Assert.That(runtime.LastCleanupReason, Is.Not.EqualTo(GameplayVfxCleanupReason.EnemyProfileFirstConfigure));
                Assert.That(runtime.LastCleanupReason, Is.Not.EqualTo(GameplayVfxCleanupReason.EnemyProfileChanged));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxProductionRuntime_InitialEntranceSpawn_SurvivesFirstPresentBeforeLifetimeExpiry()
        {
            var owner = new GameObject("EntranceSpawnLifetimeFirstPresentRuntime");
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(LoadHostDefaultCueMap());
                CreatePresentationRuntimeInputs(
                    out var topology,
                    out var stateStore,
                    out var projector);
                var entranceSpawnBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                    EntranceSpawnBindingPath);
                Assert.That(entranceSpawnBinding, Is.Not.Null);
                Assert.That(entranceSpawnBinding.DefaultLifetimeSeconds, Is.GreaterThan(0.2f));

                runtime.PresentInitial(CreateInitialExtensionContext(
                    CreateInitialPresentationData(),
                    topology,
                    stateStore,
                    projector));
                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(),
                    topology,
                    stateStore,
                    projector,
                    enemyPresentationCatalog: catalog));
                runtime.UpdatePresentation(0.2f);

                Assert.That(
                    runtime.GetActiveVfxInstanceCount(GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn)),
                    Is.EqualTo(1));
                Assert.That(runtime.GetReleaseToPoolCount(GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn)), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxProductionRuntime_ActualEnemyProfileChange_CleansEnemyFamilyWithoutTileFeatureCollateral()
        {
            var owner = new GameObject("EnemyProfileChangeRuntime");
            var firstCatalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            var secondCatalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            var enemyPrefab = new GameObject("EnemyDamageRuntimeTestPrefab");
            var enemyBinding = CreateEnemyDamageBinding(enemyPrefab);
            var cueMap = CreateCueMap(LoadEntranceSpawnBinding(), enemyBinding);
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                CreatePresentationRuntimeInputs(
                    out var topology,
                    out var stateStore,
                    out var projector);
                runtime.PresentInitial(CreateInitialExtensionContext(
                    CreateInitialPresentationData(),
                    topology,
                    stateStore,
                    projector));

                var entranceCue = GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn);
                var enemyDamageCue = GameplayVfxCueId.From(EnemyVfxCue.Damage);
                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(enemyDamageSignals: new[] { CreateEnemyDamageSignal() }),
                    topology,
                    stateStore,
                    projector,
                    enemyPresentationCatalog: firstCatalog));
                Assert.That(runtime.GetActiveVfxInstanceCount(entranceCue), Is.EqualTo(1));
                Assert.That(runtime.GetActiveVfxInstanceCount(enemyDamageCue), Is.EqualTo(1));

                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(),
                    topology,
                    stateStore,
                    projector,
                    enemyPresentationCatalog: secondCatalog));

                Assert.That(runtime.GetActiveVfxInstanceCount(enemyDamageCue), Is.Zero);
                Assert.That(runtime.GetActiveVfxInstanceCount(entranceCue), Is.EqualTo(1));
                Assert.That(runtime.EnemyProfileChangedCleanupCount, Is.EqualTo(1));
                Assert.That(runtime.LastCleanupReason, Is.EqualTo(GameplayVfxCleanupReason.EnemyProfileChanged));
                Assert.That(runtime.LastCleanupScope, Is.EqualTo(GameplayVfxCleanupScope.EnemyFamily));
                Assert.That(runtime.TileFeatureHardCleanupCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(firstCatalog);
                Object.DestroyImmediate(secondCatalog);
                Object.DestroyImmediate(cueMap);
                Object.DestroyImmediate(enemyBinding);
                Object.DestroyImmediate(enemyPrefab);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxProductionRuntime_SessionReset_StillHardCleansTileFeatureOneShots()
        {
            var owner = new GameObject("SessionResetEntranceSpawnRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(LoadHostDefaultCueMap());
                CreatePresentationRuntimeInputs(
                    out var topology,
                    out var stateStore,
                    out var projector);
                runtime.PresentInitial(CreateInitialExtensionContext(
                    CreateInitialPresentationData(),
                    topology,
                    stateStore,
                    projector));

                var entranceCue = GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn);
                Assert.That(runtime.GetActiveVfxInstanceCount(entranceCue), Is.EqualTo(1));

                runtime.ResetSession();

                Assert.That(runtime.GetActiveVfxInstanceCount(entranceCue), Is.Zero);
                Assert.That(runtime.LastCleanupReason, Is.EqualTo(GameplayVfxCleanupReason.SessionReset));
                Assert.That(runtime.LastCleanupScope, Is.EqualTo(GameplayVfxCleanupScope.AllFamilies));
                Assert.That(runtime.TileFeatureHardCleanupCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_PlayerRespawnEntranceSpawn_WithDefaultCueMap_ResolvesAndPlays()
        {
            var owner = new GameObject("RespawnEntranceSpawnRuntimeWithCueMap");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(LoadHostDefaultCueMap());

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    entitySpawnSignals: new[] { CreateRespawnSpawnSignal() })));

                var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.GetActiveVfxInstanceCount(cueId), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionRuntime_InitialEntranceSpawn_TileFeatureLaneDisabled_DoesNotPlan()
        {
            var owner = new GameObject("InitialEntranceSpawnRuntimeTileLaneDisabled");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxTileFeatureLane = false;

                runtime.PresentInitial(CreateInitialExtensionContext(CreateInitialPresentationData()));

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
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(request.StyleKey, Is.EqualTo(VfxStyleKey.Green));
            AssertTileFeatureTransitionStartStopPolicy(request);
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_FlipButtonActiveLoop_WaitsForButtonActivatedVisibilityGate()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var barrierKey = PresentationBarrierKey.ButtonActivated(901);
            var timingAnchor = PresentationTimingAnchor.MotionContact(
                sourceEntityId: 20,
                targetEntityId: 0,
                actionPlanId: 45,
                localActionIndex: 0,
                movementSemanticKind: MovementSemanticKind.Flip,
                visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                barrierKey: barrierKey);
            var presentationData = CreatePresentationData(
                tileFeatureVisualStates: new[]
                {
                    new TileFeatureVisualState(
                        901,
                        cell,
                        TileFeatureKind.Button,
                        true,
                        sourceEntityId: 20,
                        ownerEntityId: 0,
                        teamId: 1,
                        visibilityGate: new PresentationVisibilityGate(timingAnchor, barrierKey)),
                });
            var context = new GameplayVfxPlanningContext(
                12,
                presentationData,
                new CubeTopologyState(FaceId.Floor));

            planner.Plan(context, builder);
            var plan = builder.Build();

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            var request = plan.Requests[0];
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.Delayed));
            Assert.That(
                request.DelaySeconds,
                Is.EqualTo(GameplayTimingProfile.CreateDefault().FlipMotionDurationSeconds *
                           GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_DestroyActiveState_CreatesStyledPersistentLaserActiveLoop()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var frontCell = new SurfaceCell(FaceId.Front, 0, 0);
            var bottomCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var presentationData = CreatePresentationData(
                tileFeatureActiveVisualStates: new[]
                {
                    new TileFeatureActiveVisualState(
                        901,
                        frontCell,
                        TileFeatureKind.Destroy,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1),
                    new TileFeatureActiveVisualState(
                        902,
                        bottomCell,
                        TileFeatureKind.Destroy,
                        sourceEntityId: 11,
                        ownerEntityId: 0,
                        teamId: 1),
                });
            var context = new GameplayVfxPlanningContext(
                12,
                presentationData,
                new CubeTopologyState(FaceId.Floor),
                tileFeatureVfxStyleBindings: new[]
                {
                    new TileFeatureVfxStyleBinding(901, VfxStyleKey.Red),
                    new TileFeatureVfxStyleBinding(902, VfxStyleKey.Blue),
                });

            planner.Plan(context, builder);
            var requests = builder.Build().Requests.OrderBy(request => request.PersistentKey.TileId).ToArray();

            Assert.That(requests, Has.Length.EqualTo(2));
            Assert.That(requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive)));
            Assert.That(requests[0].IsPersistent, Is.True);
            Assert.That(requests[0].PersistentKey.TileId, Is.EqualTo(901));
            Assert.That(requests[0].Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(requests[0].StyleKey, Is.EqualTo(VfxStyleKey.Red));
            AssertTileFeatureTransitionStartStopPolicy(requests[0]);
            Assert.That(requests[1].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive)));
            Assert.That(requests[1].IsPersistent, Is.True);
            Assert.That(requests[1].PersistentKey.TileId, Is.EqualTo(902));
            Assert.That(requests[1].Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(requests[1].StyleKey, Is.EqualTo(VfxStyleKey.Blue));
            AssertTileFeatureTransitionStartStopPolicy(requests[1]);
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_VisibleButtonState_CreatesStyledPersistentButtonVisibleLoopAtCellFloor()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var greenCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var yellowCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var presentationData = CreatePresentationData(
                tileFeatureVisibleVisualStates: new[]
                {
                    new TileFeatureVisualState(
                        901,
                        greenCell,
                        TileFeatureKind.Button,
                        true,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1),
                    new TileFeatureVisualState(
                        902,
                        yellowCell,
                        TileFeatureKind.Button,
                        true,
                        sourceEntityId: 11,
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
                    new TileFeatureVfxStyleBinding(902, VfxStyleKey.Yellow),
                });

            planner.Plan(context, builder);
            var requests = builder.Build().Requests.OrderBy(request => request.PersistentKey.TileId).ToArray();

            Assert.That(requests, Has.Length.EqualTo(2));
            Assert.That(requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop)));
            Assert.That(requests[0].IsPersistent, Is.True);
            Assert.That(requests[0].PersistentKey.TileId, Is.EqualTo(901));
            Assert.That(requests[0].PersistentKey.EffectIndex, Is.EqualTo(2));
            Assert.That(requests[0].Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(requests[0].StyleKey, Is.EqualTo(VfxStyleKey.Green));
            AssertTileFeatureTransitionStartStopPolicy(requests[0]);
            Assert.That(requests[1].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop)));
            Assert.That(requests[1].IsPersistent, Is.True);
            Assert.That(requests[1].PersistentKey.TileId, Is.EqualTo(902));
            Assert.That(requests[1].PersistentKey.EffectIndex, Is.EqualTo(2));
            Assert.That(requests[1].Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(requests[1].StyleKey, Is.EqualTo(VfxStyleKey.Yellow));
            AssertTileFeatureTransitionStartStopPolicy(requests[1]);
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_OpenExitState_CreatesPersistentExitOpenLoop()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var presentationData = CreatePresentationData(
                tileFeatureVisualStates: new[]
                {
                    new TileFeatureVisualState(
                        902,
                        cell,
                        TileFeatureKind.Exit,
                        true,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1),
                    new TileFeatureVisualState(
                        903,
                        new SurfaceCell(FaceId.Floor, 2, 1),
                        TileFeatureKind.Exit,
                        false,
                        sourceEntityId: 11,
                        ownerEntityId: 0,
                        teamId: 1),
                });
            var context = new GameplayVfxPlanningContext(
                12,
                presentationData,
                new CubeTopologyState(FaceId.Floor));

            planner.Plan(context, builder);
            var plan = builder.Build();

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            var request = plan.Requests[0];
            var expectedCueId = GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpenLoop);
            Assert.That(request.CueId, Is.EqualTo(expectedCueId));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey, Is.EqualTo(new VfxPersistentKey(
                expectedCueId,
                VfxAnchorKind.Cell,
                tileId: 902,
                cell: cell,
                hasCell: true,
                effectIndex: 1)));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Cell, Is.EqualTo(cell));
            AssertTileFeatureTransitionStartStopPolicy(request);
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_GatedExitOpen_UsesInheritedButtonActivatedBarrier()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var barrierKey = PresentationBarrierKey.ButtonActivated(901);
            var timingAnchor = PresentationTimingAnchor.MotionContact(
                sourceEntityId: 20,
                targetEntityId: 0,
                actionPlanId: 45,
                localActionIndex: 0,
                movementSemanticKind: MovementSemanticKind.Flip,
                visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                barrierKey: barrierKey);
            var visibilityGate = new PresentationVisibilityGate(timingAnchor, barrierKey);
            var presentationData = CreatePresentationData(
                tileEvents: new[]
                {
                    new TilePresentationEvent(
                        TilePresentationEventKind.ExitOpened,
                        902,
                        cell,
                        TileFeatureKind.Exit,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1,
                        timingAnchor: timingAnchor,
                        barrierKey: barrierKey),
                },
                tileFeatureVisualStates: new[]
                {
                    new TileFeatureVisualState(
                        902,
                        cell,
                        TileFeatureKind.Exit,
                        true,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1,
                        visibilityGate: visibilityGate),
                });
            var context = new GameplayVfxPlanningContext(
                12,
                presentationData,
                new CubeTopologyState(FaceId.Floor));

            planner.Plan(context, builder);
            var requests = builder.Build().Requests
                .OrderBy(request => request.IsPersistent ? 1 : 0)
                .ToArray();
            var expectedDelay = GameplayTimingProfile.CreateDefault().FlipMotionDurationSeconds *
                                GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;

            Assert.That(requests, Has.Length.EqualTo(2));
            Assert.That(requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpened)));
            Assert.That(requests[0].IsPersistent, Is.False);
            Assert.That(requests[0].Timing, Is.EqualTo(VfxTimingKind.Delayed));
            Assert.That(requests[0].DelaySeconds, Is.EqualTo(expectedDelay).Within(0.0001f));
            Assert.That(requests[1].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpenLoop)));
            Assert.That(requests[1].IsPersistent, Is.True);
            Assert.That(requests[1].Timing, Is.EqualTo(VfxTimingKind.Delayed));
            Assert.That(requests[1].DelaySeconds, Is.EqualTo(expectedDelay).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVfx_TopologyMotion_SourceDestinationTopology_AvailableToPlannerOrExplicitlyAbsent()
        {
            var planner = new TileFeatureVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var motion = new TickTopologyMotion(
                sourceTopology,
                destinationTopology,
                CubeRotationKind.Forward);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var presentationData = CreatePresentationData(
                topologyMotion: motion,
                tileFeatureVisualStates: new[]
                {
                    new TileFeatureVisualState(
                        902,
                        cell,
                        TileFeatureKind.Barricade,
                        true,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1),
                });
            var context = new GameplayVfxPlanningContext(
                12,
                presentationData,
                destinationTopology,
                topologyTransition: new GameplayVfxTopologyTransitionContext(
                    motion,
                    sourceTopology,
                    destinationTopology,
                    sourceTopology,
                    transitionEpoch: 1,
                    isTransitionStartTick: true,
                    isTransitionCompletionReconcile: false));

            planner.Plan(context, builder);
            var request = builder.Build().Requests.Single();

            Assert.That(context.TopologyTransition.HasTopologyMotion, Is.True);
            Assert.That(context.TopologyTransition.SourceTopology, Is.EqualTo(sourceTopology));
            Assert.That(context.TopologyTransition.DestinationTopology, Is.EqualTo(destinationTopology));
            Assert.That(request.Anchor.Topology, Is.EqualTo(sourceTopology));
            Assert.That(request.TopologyAnchorMode, Is.EqualTo(GameplayVfxTopologyAnchorMode.SourceDuringTransition));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVfx_ActivationDeactivationEvents_MappingPolicyIsExplicit()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var plan = PlanTileFeature(
                topology,
                new TilePresentationEvent(TilePresentationEventKind.DestroyTileActivated, 1, cell, TileFeatureKind.Destroy, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.DestroyTileDeactivated, 2, cell, TileFeatureKind.Destroy, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeActivated, 3, cell, TileFeatureKind.Barricade, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeDeactivated, 4, cell, TileFeatureKind.Barricade, 10, 0, 1));

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVfx_TopologyTransition_CompletionReconcile_DoesNotReplayTransientOneShots()
        {
            var owner = new GameObject("TileFeatureCompletionReconcileRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var motion = new TickTopologyMotion(sourceTopology, destinationTopology, CubeRotationKind.Forward);
                var data = CreatePresentationData(
                    topologyMotion: motion,
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
                    tileFeatureVisualStates: new[]
                    {
                        new TileFeatureVisualState(
                            901,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            TileFeatureKind.Barricade,
                            true,
                            sourceEntityId: 10,
                            ownerEntityId: 0,
                            teamId: 1),
                    });
                runtime.Present(CreateExtensionContext(data, destinationTopology));
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);

                runtime.ReconcileTopologyTransitionCompleted(
                    CreateExtensionContext(
                        data,
                        destinationTopology,
                        topologyTransitionEpoch: 1,
                        isTopologyTransitionCompletionReconcile: true));

                Assert.That(runtime.LastPlannedRequestCount, Is.LessThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DoesNotReferencePr28VisualControllersOrVfxController()
        {
            var coordinator = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs");

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
        public void Governance_DocumentsTileFeatureAndGravityFieldNoGenericExpansionOwnedPolicy()
        {
            var document = ReadRepoFile("Docs/Architecture/Gameplay-VFX-Governance.md");

            Assert.That(document, Does.Contain("TileFeature and GravityField visual migration is VFX-lane only."));
            Assert.That(document, Does.Contain("There is no coordinator fallback for TileFeature or GravityField visual lanes."));
            Assert.That(document, Does.Contain("does not restore PR #28 direct visual controllers"));
            Assert.That(document, Does.Contain("TileFeatureAudio and GravityFieldAudio are not VFX."));
        }

        [Test]
        [Category("Extended")]
        public void Authoring_DefaultCueMapContainsDestroyTileAndOmitsRemovedBarricadeBinding()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset");
            var sparkBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeatureDestroySpark_Binding.asset");
            var laserBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeatureDestroyLaserActive_Binding.asset");
            var redLaserBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeatureDestroyLaserActive_Red_Binding.asset");
            var blueLaserBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeatureDestroyLaserActive_Blue_Binding.asset");
            var barricadeBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_BarricadeActiveLoop_Binding.asset");
            var greenButtonBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonActiveLoop_Green_Binding.asset");
            var yellowButtonBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonActiveLoop_Yellow_Binding.asset");
            var greenButtonVisibleBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonVisibleLoop_Green_Binding.asset");
            var yellowButtonVisibleBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonVisibleLoop_Yellow_Binding.asset");
            var exitOpenLoopBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ExitOpenLoop_Binding.asset");

            Assert.That(cueMap, Is.Not.Null);
            Assert.That(sparkBinding, Is.Not.Null);
            Assert.That(laserBinding, Is.Not.Null);
            Assert.That(redLaserBinding, Is.Not.Null);
            Assert.That(blueLaserBinding, Is.Not.Null);
            Assert.That(barricadeBinding, Is.Null);
            Assert.That(greenButtonBinding, Is.Not.Null);
            Assert.That(yellowButtonBinding, Is.Not.Null);
            Assert.That(greenButtonVisibleBinding, Is.Not.Null);
            Assert.That(yellowButtonVisibleBinding, Is.Not.Null);
            Assert.That(exitOpenLoopBinding, Is.Not.Null);
            Assert.That(sparkBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileTriggered)));
            Assert.That(laserBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive)));
            Assert.That(redLaserBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive)));
            Assert.That(blueLaserBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive)));
            Assert.That(greenButtonBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(yellowButtonBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(
                greenButtonVisibleBinding.CueId,
                Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop)));
            Assert.That(
                yellowButtonVisibleBinding.CueId,
                Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop)));
            Assert.That(exitOpenLoopBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpenLoop)));
            Assert.That(greenButtonBinding.StyleKey, Is.EqualTo(VfxStyleKey.Green));
            Assert.That(yellowButtonBinding.StyleKey, Is.EqualTo(VfxStyleKey.Yellow));
            Assert.That(greenButtonVisibleBinding.StyleKey, Is.EqualTo(VfxStyleKey.Green));
            Assert.That(yellowButtonVisibleBinding.StyleKey, Is.EqualTo(VfxStyleKey.Yellow));
            Assert.That(redLaserBinding.StyleKey, Is.EqualTo(VfxStyleKey.Red));
            Assert.That(blueLaserBinding.StyleKey, Is.EqualTo(VfxStyleKey.Blue));
            Assert.That(sparkBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(sparkBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(laserBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(laserBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            AssertDestroyLaserActiveLoopBinding(redLaserBinding);
            AssertDestroyLaserActiveLoopBinding(blueLaserBinding);
            AssertButtonActiveLoopBinding(greenButtonBinding);
            AssertButtonActiveLoopBinding(yellowButtonBinding);
            AssertButtonActiveLoopBinding(greenButtonVisibleBinding);
            AssertButtonActiveLoopBinding(yellowButtonVisibleBinding);
            Assert.That(exitOpenLoopBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(exitOpenLoopBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(exitOpenLoopBinding.Prefab, Is.Not.Null);

            var runtimeMap = cueMap.BuildRuntimeMap();
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileTriggered), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive), VfxStyleKey.Red, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive), VfxStyleKey.Blue, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeActiveLoop), out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Green, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Yellow, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Default, out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop), VfxStyleKey.Green, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop), VfxStyleKey.Yellow, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop), VfxStyleKey.Default, out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpenLoop), out _), Is.True);

            var tileFeatureCatalog = ReadRepoFile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Catalogs/TileFeaturePresentationCatalog_CampaignMainBoard.asset");
            Assert.That(tileFeatureCatalog, Does.Contain("presentationKey: destroy.front"));
            Assert.That(tileFeatureCatalog, Does.Contain("value: Red"));
            Assert.That(tileFeatureCatalog, Does.Contain("presentationKey: destroy.bottom"));
            Assert.That(tileFeatureCatalog, Does.Contain("value: Blue"));
        }

        [Test]
        [Category("Extended")]
        public void ButtonProductionVfxLoops_StartStopAndCleanupByCatalogStyle()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);
            var greenButtonBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonActiveLoop_Green_Binding.asset");
            var yellowButtonBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonActiveLoop_Yellow_Binding.asset");
            var greenButtonVisibleBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonVisibleLoop_Green_Binding.asset");
            var yellowButtonVisibleBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_ButtonVisibleLoop_Yellow_Binding.asset");

            Assert.That(cueMap, Is.Not.Null);
            AssertButtonActiveLoopBinding(greenButtonBinding);
            AssertButtonActiveLoopBinding(yellowButtonBinding);
            AssertButtonActiveLoopBinding(greenButtonVisibleBinding);
            AssertButtonActiveLoopBinding(yellowButtonVisibleBinding);

            var runtimeMap = cueMap.BuildRuntimeMap();
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Green, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Yellow, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop), VfxStyleKey.Default, out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop), VfxStyleKey.Green, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop), VfxStyleKey.Yellow, out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop), VfxStyleKey.Default, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BarricadeActiveLoop_UnboundCue_IsExplicitNoOpWithoutMissingBindingSpam()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);
            var barricadeBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/TileFeature_BarricadeActiveLoop_Binding.asset");

            Assert.That(cueMap, Is.Not.Null);
            Assert.That(barricadeBinding, Is.Null);
            var runtimeMap = cueMap.BuildRuntimeMap();
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeActiveLoop), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxTileFeatureGravityField_DefaultCueMap_ContainsExitSliderAndRemainingGravityBindings()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null);
            var runtimeMap = cueMap.BuildRuntimeMap();

            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedUp), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedRight), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedDown), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedLeft), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpened), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.ExitObjectiveCleared), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(GravityFieldVfxCue.ChargeStarted), out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(GravityFieldVfxCue.ActiveStarted), out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(GravityFieldVfxCue.ChargingArea), out _), Is.False);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(GravityFieldVfxCue.ActiveArea), out _), Is.True);
            Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn), out _), Is.True);
            Assert.That(
                runtimeMap.TryResolve(new GameplayVfxCueId(GameplayVfxFamily.Player, (int)TileFeatureVfxCue.EntranceSpawn), out _),
                Is.False);
            Assert.That(
                runtimeMap.TryResolve(new GameplayVfxCueId(GameplayVfxFamily.Enemy, (int)TileFeatureVfxCue.EntranceSpawn), out _),
                Is.False);
            Assert.That(
                runtimeMap.TryResolve(new GameplayVfxCueId(GameplayVfxFamily.Projectile, (int)TileFeatureVfxCue.EntranceSpawn), out _),
                Is.False);
            Assert.That(
                cueMap.TryResolvePrefab(GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn), out var resolvedPrefab),
                Is.True);
            Assert.That(resolvedPrefab, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureEntranceSpawn_BindingPolicy_UsesFiveSecondAuthoredDuration()
        {
            var binding = LoadEntranceSpawnBinding();
            var policy = binding.BuildRuntimePolicy();

            AssertOneShotBinding(binding, GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn), 16);
            Assert.That(policy.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(policy.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(policy.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.PrefabOnly));
            Assert.That(policy.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.ExplicitPrefabRequired));
            Assert.That(policy.DefaultLifetimeSeconds, Is.EqualTo(5f));
            Assert.That(policy.TailSeconds, Is.EqualTo(5f));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(16));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureEntranceSpawn_PrefabPolicy_IsActualVisualPrefab()
        {
            var binding = LoadEntranceSpawnBinding();
            var entranceSpawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntranceSpawnPrefabPath);

            Assert.That(entranceSpawnPrefab, Is.Not.Null, EntranceSpawnPrefabPath);
            Assert.That(binding.Prefab, Is.EqualTo(entranceSpawnPrefab));
            Assert.That(
                VfxPrefabValidationDiagnostics.ValidatePrefab(entranceSpawnPrefab).HasErrors,
                Is.False);
            Assert.That(
                VfxPrefabValidationDiagnostics.ValidateModelRootContract(entranceSpawnPrefab).HasErrors,
                Is.False);
            Assert.That(entranceSpawnPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(entranceSpawnPrefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(entranceSpawnPrefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(entranceSpawnPrefab.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
            Assert.That(
                entranceSpawnPrefab.GetComponentsInChildren<ParticleSystem>(true)
                    .Any(particleSystem => Mathf.Approximately(particleSystem.main.duration, 5f)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Authoring_ButtonActiveLoopPrefabs_AreLoopingPlayOnAwakeAndNotPrefabLocalOnButtons()
        {
            AssertLoopingPlayOnAwake(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/TileFeature_ButtonActivatedVfx.prefab");
            AssertLoopingPlayOnAwake(
                "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/TileFeature_MoonBlockButtonActivatedVfx.prefab");

            var defaultButtonPrefab = ReadRepoFile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_Default.prefab");
            var moonButtonPrefab = ReadRepoFile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_MoonOnly.prefab");

            Assert.That(defaultButtonPrefab, Does.Not.Contain("buttonActivatedParticles"));
            Assert.That(moonButtonPrefab, Does.Not.Contain("buttonActivatedParticles"));
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

        private static void AssertDestroyLaserActiveLoopBinding(VfxBindingDefinitionAsset binding)
        {
            Assert.That(binding.Prefab, Is.Not.Null);
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(binding.DefaultLifetimeSeconds, Is.Zero);
            Assert.That(binding.TailSeconds, Is.EqualTo(0.25f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(16));
        }

        private static void AssertOneShotBinding(
            VfxBindingDefinitionAsset binding,
            GameplayVfxCueId expectedCueId,
            int maxConcurrentInstances)
        {
            Assert.That(binding, Is.Not.Null, expectedCueId.ToString());
            Assert.That(binding.CueId, Is.EqualTo(expectedCueId));
            Assert.That(binding.StyleKey, Is.EqualTo(VfxStyleKey.Default));
            Assert.That(binding.Prefab, Is.Not.Null);
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.TailSeconds, Is.GreaterThan(0f));
            Assert.That(binding.InitialPoolSize, Is.GreaterThan(0));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(maxConcurrentInstances));
        }

        private static void AssertPersistentLoopBinding(
            VfxBindingDefinitionAsset binding,
            GameplayVfxCueId expectedCueId,
            int maxConcurrentInstances)
        {
            Assert.That(binding, Is.Not.Null, expectedCueId.ToString());
            Assert.That(binding.CueId, Is.EqualTo(expectedCueId));
            Assert.That(binding.StyleKey, Is.EqualTo(VfxStyleKey.Default));
            Assert.That(binding.Prefab, Is.Not.Null);
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(binding.DefaultLifetimeSeconds, Is.Zero);
            Assert.That(binding.TailSeconds, Is.GreaterThan(0f));
            Assert.That(binding.InitialPoolSize, Is.GreaterThan(0));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(maxConcurrentInstances));
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

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickPresentationData presentationData,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            EnemyPresentationBinding[] enemyPresentationBindings = null,
            int topologyTransitionEpoch = 0,
            bool isTopologyTransitionCompletionReconcile = false)
        {
            return new GameplayTickPresentationExtensionContext(
                CreateResult(presentationData, topology),
                topology,
                stateStore,
                projector,
                enemyPresentationCatalog,
                enemyPresentationBindings,
                topologyTransitionEpoch: topologyTransitionEpoch,
                isTopologyTransitionCompletionReconcile: isTopologyTransitionCompletionReconcile);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickPresentationData presentationData,
            CubeTopologyState? topologyOverride = null,
            int topologyTransitionEpoch = 0,
            bool isTopologyTransitionCompletionReconcile = false,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            EnemyPresentationBinding[] enemyPresentationBindings = null)
        {
            var topology = topologyOverride ?? new CubeTopologyState(FaceId.Floor);
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
                projector,
                enemyPresentationCatalog,
                enemyPresentationBindings,
                topologyTransitionEpoch: topologyTransitionEpoch,
                isTopologyTransitionCompletionReconcile: isTopologyTransitionCompletionReconcile);
        }

        private static GameplayInitialPresentationExtensionContext CreateInitialExtensionContext(
            InitialPresentationData presentationData,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            return new GameplayInitialPresentationExtensionContext(
                presentationData,
                topology,
                stateStore,
                projector);
        }

        private static GameplayInitialPresentationExtensionContext CreateInitialExtensionContext(
            InitialPresentationData presentationData)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);

            return new GameplayInitialPresentationExtensionContext(
                presentationData,
                topology,
                stateStore,
                projector);
        }

        private static void CreatePresentationRuntimeInputs(
            out CubeTopologyState topology,
            out GameplayPresentationStateStore stateStore,
            out GameplayCubeProjector projector)
        {
            topology = new CubeTopologyState(FaceId.Floor);
            stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.right, Quaternion.identity);
            projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
        }

        private static InitialPresentationData CreateInitialPresentationData()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            return new InitialPresentationData(
                new[]
                {
                    new EntitySpawnPresentationSignal(
                        10,
                        EntityPresentationKind.Player,
                        EntitySpawnPresentationReason.InitialStageStart,
                        cell,
                        topology,
                        Direction.Right,
                        new TileFeaturePresentationSource(100, TileFeatureKind.Entrance, cell)),
                });
        }

        private static EntitySpawnPresentationSignal CreateRespawnSpawnSignal()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            return new EntitySpawnPresentationSignal(
                10,
                EntityPresentationKind.Player,
                EntitySpawnPresentationReason.PlayerRespawn,
                cell,
                topology,
                Direction.Right,
                new TileFeaturePresentationSource(100, TileFeatureKind.Entrance, cell));
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
            TileFeatureVisualState[] tileFeatureVisibleVisualStates = null,
            TileFeatureActiveVisualState[] tileFeatureActiveVisualStates = null,
            TickTopologyMotion? topologyMotion = null,
            EntitySpawnPresentationSignal[] entitySpawnSignals = null,
            TickEnemyDamagePresentationSignal[] enemyDamageSignals = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: topologyMotion,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals ?? Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates,
                tileFeatureVisualStates: tileFeatureVisualStates,
                tileFeatureVisibleVisualStates: tileFeatureVisibleVisualStates,
                tileFeatureActiveVisualStates: tileFeatureActiveVisualStates,
                entitySpawnSignals: entitySpawnSignals);
        }

        private static TickEnemyDamagePresentationSignal CreateEnemyDamageSignal()
        {
            return new TickEnemyDamagePresentationSignal(40, tookDamageThisTick: true, damageAmount: 1);
        }

        private static VfxCueMapAsset LoadHostDefaultCueMap()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);
            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            return cueMap;
        }

        private static VfxBindingDefinitionAsset LoadEntranceSpawnBinding()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(EntranceSpawnBindingPath);
            Assert.That(binding, Is.Not.Null, EntranceSpawnBindingPath);
            return binding;
        }

        private static VfxBindingDefinitionAsset CreateEnemyDamageBinding(GameObject prefab)
        {
            GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            binding.name = "EnemyDamagePresentationOnly_Binding";
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)EnemyVfxCue.Damage);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "visibilityMode", GameplayVfxVisibilityMode.PresentationOnly);
            SetField(binding, "defaultLifetimeSeconds", 5f);
            SetField(binding, "tailSeconds", 0.5f);
            SetField(binding, "initialPoolSize", 1);
            SetField(binding, "maxConcurrentInstances", 8);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
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

        private static GameplayVfxRequestPlan PlanTileFeature(
            CubeTopologyState topology,
            params TilePresentationEvent[] events)
        {
            var builder = new GameplayVfxRequestPlanBuilder();
            var data = CreatePresentationData(tileEvents: events);
            new TileFeatureVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(21, data, topology),
                builder);
            return builder.Build();
        }

        private static void AssertTileFeatureTransitionStartStopPolicy(in GameplayVfxRequest request)
        {
            Assert.That(request.TopologyStopMode, Is.EqualTo(GameplayVfxTopologyStopMode.HardClearAtTransitionStart));
            Assert.That(request.TopologySpawnMode, Is.EqualTo(GameplayVfxTopologySpawnMode.SuppressDuringTransition));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(fullPath);
        }
    }
}
