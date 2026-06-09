using System;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.BoardState;
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

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxFrontFaceShieldMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string ActivePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldActiveVfx.prefab";
        private const string BlockPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldBlockVfx.prefab";
        private const string WindupPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldWindupVfx.prefab";
        private const string ActiveBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldActive_Binding.asset";
        private const string BlockBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldBlock_Binding.asset";
        private const string WindupBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldWindup_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ShieldActiveSignal_EmitsPersistentActiveRequest()
        {
            var sourceCell = new SurfaceCell(FaceId.Back, 2, 1);
            var topology = new CubeTopologyState(FaceId.Back);

            var request = PlanSingleActiveRequest(CreateSourceSignal(40, sourceCell, topology));

            var cueId = GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive);
            Assert.That(request.CueId, Is.EqualTo(cueId));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey, Is.EqualTo(new VfxPersistentKey(
                cueId,
                VfxAnchorKind.Entity,
                entityId: 40)));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
            Assert.That(request.Anchor.HasFallbackCell, Is.True);
            Assert.That(request.Anchor.FallbackCell, Is.EqualTo(sourceCell));
            Assert.That(request.Anchor.FallbackTopology, Is.EqualTo(topology));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ShieldActiveSourceExit_DoesNotEmitActiveRequest()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var plan = PlanRequests(CreatePresentationData(
                activeSignals: new[] { CreateSourceSignal(40, sourceCell, topology) },
                exitSignals: new[] { CreateExitSignal(40, sourceCell, topology) }));

            Assert.That(plan.Requests, Has.None.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ShieldBlockSignal_EmitsTransientBlockRequestAtBlockedCell()
        {
            var blockedCell = new SurfaceCell(FaceId.Front, 1, 2);
            var topology = new CubeTopologyState(FaceId.Front);

            var request = PlanSingleBlockRequest(CreateBlockSignal(40, 20, 10, blockedCell, topology));

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock)));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PersistentKey, Is.EqualTo(VfxPersistentKey.None));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(blockedCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ActiveAndBlockSameTick_BothAllowed()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var plan = PlanRequests(CreatePresentationData(
                activeSignals: new[] { CreateSourceSignal(40, sourceCell, topology) },
                blockSignals: new[] { CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), topology) }));

            Assert.That(plan.Requests, Has.Some.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive)));
            Assert.That(plan.Requests, Has.Some.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupSignal_EmitsPersistentWindupRequest()
        {
            var sourceCell = new SurfaceCell(FaceId.Back, 2, 1);
            var topology = new CubeTopologyState(FaceId.Back);

            var request = PlanSingleWindupRequest(CreateWindupSignal(
                40,
                sourceCell,
                topology,
                effectIndex: 2,
                activationSequence: 7,
                presentationSeed: 991));

            var cueId = GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldWindup);
            Assert.That(request.CueId, Is.EqualTo(cueId));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.PresentationSeed, Is.EqualTo(991));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey, Is.EqualTo(new VfxPersistentKey(
                cueId,
                VfxAnchorKind.Entity,
                entityId: 40,
                effectIndex: 2,
                activationSequence: 7)));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
            Assert.That(request.Anchor.HasFallbackCell, Is.True);
            Assert.That(request.Anchor.FallbackCell, Is.EqualTo(sourceCell));
            Assert.That(request.Anchor.FallbackTopology, Is.EqualTo(topology));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupRequest_KeyChangesWithActivationSequence()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var first = PlanSingleWindupRequest(CreateWindupSignal(40, sourceCell, topology, activationSequence: 1));
            var second = PlanSingleWindupRequest(CreateWindupSignal(40, sourceCell, topology, activationSequence: 2));

            Assert.That(first.PersistentKey, Is.Not.EqualTo(second.PersistentKey));
            Assert.That(first.PersistentKey.EffectIndex, Is.EqualTo(second.PersistentKey.EffectIndex));
            Assert.That(first.PersistentKey.EntityId, Is.EqualTo(second.PersistentKey.EntityId));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupSourceExit_DoesNotEmitWindupRequest()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var plan = PlanRequests(CreatePresentationData(
                windupSignals: new[] { CreateWindupSignal(40, sourceCell, topology) },
                exitSignals: new[] { CreateExitSignal(40, sourceCell, topology) }));

            Assert.That(plan.Requests, Has.None.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldWindup)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ActiveBlockWindupSameTick_AllowedWithDistinctCues()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var plan = PlanRequests(CreatePresentationData(
                activeSignals: new[] { CreateSourceSignal(40, sourceCell, topology) },
                blockSignals: new[] { CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), topology) },
                windupSignals: new[] { CreateWindupSignal(40, sourceCell, topology) }));

            Assert.That(plan.Requests, Has.Some.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive)));
            Assert.That(plan.Requests, Has.Some.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock)));
            Assert.That(plan.Requests, Has.Some.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldWindup)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_ShieldMigrationFlags_DefaultTrue()
        {
            var owner = new GameObject("FrontFaceShieldDefaultFlags");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_ShieldFlags_AreIndependent()
        {
            AssertFlagCombinationPlans(activeEnabled: true, blockEnabled: false, expectedRequests: 1);
            AssertFlagCombinationPlans(activeEnabled: false, blockEnabled: true, expectedRequests: 1);
            AssertFlagCombinationPlans(activeEnabled: true, blockEnabled: true, expectedRequests: 2);
            AssertFlagCombinationPlans(activeEnabled: false, blockEnabled: false, expectedRequests: 0);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_WindupFlag_IsIndependentFromActiveAndBlockFlags()
        {
            AssertFlagCombinationPlans(
                activeEnabled: false,
                blockEnabled: false,
                windupEnabled: true,
                expectedRequests: 1);
            AssertFlagCombinationPlans(
                activeEnabled: true,
                blockEnabled: false,
                windupEnabled: true,
                expectedRequests: 2);
            AssertFlagCombinationPlans(
                activeEnabled: false,
                blockEnabled: true,
                windupEnabled: true,
                expectedRequests: 2);
            AssertFlagCombinationPlans(
                activeEnabled: true,
                blockEnabled: true,
                windupEnabled: false,
                expectedRequests: 2);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_MissingBindings_DiagnosticNoOldFallback()
        {
            var activeOwner = new GameObject("FrontFaceShieldActiveMissingBinding");
            var blockOwner = new GameObject("FrontFaceShieldBlockMissingBinding");
            try
            {
                var activeRuntime = activeOwner.AddComponent<GameplayVfxProductionRuntime>();
                var activeView = AddSourceView(activeOwner);
                activeRuntime.Present(CreateExtensionContext(CreatePresentationData(
                    activeSignals: new[]
                    {
                        CreateSourceSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    }), sourceView: activeView));

                var blockRuntime = blockOwner.AddComponent<GameplayVfxProductionRuntime>();
                blockRuntime.Present(CreateExtensionContext(CreatePresentationData(
                    blockSignals: new[]
                    {
                        CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                Assert.That(activeRuntime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(activeRuntime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(blockRuntime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(blockRuntime.MissingBindingCount, Is.EqualTo(1));

                var windupOwner = new GameObject("FrontFaceShieldWindupMissingBinding");
                try
                {
                    var windupRuntime = windupOwner.AddComponent<GameplayVfxProductionRuntime>();
                    var windupView = AddSourceView(windupOwner);
                    windupRuntime.Present(CreateExtensionContext(CreatePresentationData(
                        windupSignals: new[]
                        {
                            CreateWindupSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                        }), sourceView: windupView));

                    Assert.That(windupRuntime.LastPlannedRequestCount, Is.EqualTo(1));
                    Assert.That(windupRuntime.MissingBindingCount, Is.EqualTo(1));
                }
                finally
                {
                    Destroy(windupOwner);
                }
            }
            finally
            {
                Destroy(activeOwner, blockOwner);
            }
        }

        [Test]
        [Category("Extended")]
        public void LegacyPresenter_ActiveRefreshDoesNotSpawnOldLoop()
        {
            var root = new GameObject("FrontFaceShieldLegacyActiveNoSpawn");
            var sourceObject = new GameObject("FrontFaceShieldLegacyActiveNoSpawn_Source");
            try
            {
                var sourceView = sourceObject.AddComponent<GameplayEntityView>();
                sourceView.Initialize(40);
                var shieldAuthoring = sourceObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                SetField(shieldAuthoring, "attachActiveLoopToSourceView", true);
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stateStore = new GameplayPresentationStateStore();
                stateStore.ResetSession(topology);
                stateStore.ViewsByEntityId[40] = sourceView;
                stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
                var projector = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    1f);
                var presenter = new GameplayFrontFaceShieldVfxPresenter();
                presenter.Initialize(root.transform, 1f);

                presenter.RefreshActiveSources(
                    new[] { CreateSourceSignal(40, sourceCell, topology) },
                    stateStore,
                    projector);
                Assert.That(CountDescendantsByNamePrefix(sourceObject.transform, "FrontFaceShieldActiveLoop_40"), Is.Zero);
                Assert.That(presenter.ActiveLoopInstanceCount, Is.Zero);

                presenter.RefreshActiveSources(
                    Array.Empty<TickFrontFaceShieldSourceSignal>(),
                    stateStore,
                    projector);

                Assert.That(CountDescendantsByNamePrefix(sourceObject.transform, "FrontFaceShieldActiveLoop_40"), Is.Zero);
            }
            finally
            {
                Destroy(sourceObject, root);
            }
        }

        [Test]
        [Category("Extended")]
        public void LegacyPresenter_WindupRefreshDoesNotSpawnOldTelegraph()
        {
            var root = new GameObject("FrontFaceShieldLegacyWindupNoSpawn");
            var sourceObject = new GameObject("FrontFaceShieldLegacyWindupNoSpawn_Source");
            var telegraphPrefab = new GameObject("FrontFaceShieldLegacyWindupPrefab");
            try
            {
                var sourceView = sourceObject.AddComponent<GameplayEntityView>();
                sourceView.Initialize(40);
                var shieldAuthoring = sourceObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                SetField(shieldAuthoring, "telegraphPrefab", telegraphPrefab);
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stateStore = new GameplayPresentationStateStore();
                stateStore.ResetSession(topology);
                stateStore.ViewsByEntityId[40] = sourceView;
                stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
                var projector = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    1f);
                var presenter = new GameplayFrontFaceShieldVfxPresenter();
                presenter.Initialize(root.transform, 1f);

                presenter.RefreshWindupWarnings(
                    new[] { CreateWindupSignal(40, sourceCell, topology) },
                    stateStore,
                    projector);

                Assert.That(CountDescendantsByNamePrefix(root.transform, "FrontFaceShieldWindup_40"), Is.Zero);
                Assert.That(presenter.WarningInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(telegraphPrefab, sourceObject, root);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_BlockFlagOn_SuppressesLegacyBlockBurst()
        {
            var scenario = CreateCoordinatorScenario("FrontFaceShieldLegacyBlockSuppress");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(new[] { CreateEnemyUnit(40, scenario.SourceCell) }, scenario.Topology);

                scenario.Presenter.Present(CreateTickResult(
                    12,
                    new[] { CreateEnemyUnit(40, scenario.SourceCell) },
                    scenario.Topology,
                    CreatePresentationData(blockSignals: new[]
                    {
                        CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), scenario.Topology),
                    })));

                Assert.That(CountDescendantsByNamePrefix(scenario.Root.transform, "FrontFaceShieldBlockBurst_40_20"), Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_FlagOff_DoesNotUseLegacyActiveOrBlockFallback()
        {
            var scenario = CreateCoordinatorScenario("FrontFaceShieldLegacyFlagOff");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(new[] { CreateEnemyUnit(40, scenario.SourceCell) }, scenario.Topology);

                scenario.Presenter.Present(CreateTickResult(
                    12,
                    new[] { CreateEnemyUnit(40, scenario.SourceCell) },
                    scenario.Topology,
                    CreatePresentationData(
                        activeSignals: new[] { CreateSourceSignal(40, scenario.SourceCell, scenario.Topology) },
                        blockSignals: new[]
                        {
                            CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), scenario.Topology),
                        })));

                Assert.That(CountDescendantsByNamePrefix(scenario.Root.transform, "FrontFaceShieldActiveLoop_40"), Is.Zero);
                Assert.That(CountDescendantsByNamePrefix(scenario.Root.transform, "FrontFaceShieldBlockBurst_40_20"), Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ShieldBindings_RemovedFromDefaultAuthoring()
        {
            var activeBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ActiveBindingPath);
            var blockBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(BlockBindingPath);
            var windupBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(WindupBindingPath);
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(activeBinding, Is.Null, ActiveBindingPath);
            Assert.That(blockBinding, Is.Null, BlockBindingPath);
            Assert.That(windupBinding, Is.Null, WindupBindingPath);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive), out _), Is.False);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock), out _), Is.False);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldWindup), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ShieldPrefabs_RemovedFromDefaultAuthoring()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(ActivePrefabPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefabPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(WindupPrefabPath), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void ShieldMigrationSources_DoNotReferenceAuthorityTypes()
        {
            var source = File.ReadAllText(VfxPlanningPath) + "\n" + File.ReadAllText(VfxProductionRuntimePath);
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "CreateSnapshot",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static GameplayVfxRequest PlanSingleActiveRequest(TickFrontFaceShieldSourceSignal signal)
        {
            var plan = PlanRequests(CreatePresentationData(activeSignals: new[] { signal }));
            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayVfxRequest PlanSingleBlockRequest(TickFrontFaceShieldBlockSignal signal)
        {
            var plan = PlanRequests(CreatePresentationData(blockSignals: new[] { signal }));
            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayVfxRequest PlanSingleWindupRequest(TickFrontFaceShieldWindupWarningSignal signal)
        {
            var plan = PlanRequests(CreatePresentationData(windupSignals: new[] { signal }));
            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayVfxRequestPlan PlanRequests(TickPresentationData presentationData)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    presentationData,
                    new CubeTopologyState(FaceId.Floor)),
                builder);
            return builder.Build();
        }

        private static void AssertFlagCombinationPlans(bool activeEnabled, bool blockEnabled, int expectedRequests)
        {
            AssertFlagCombinationPlans(activeEnabled, blockEnabled, windupEnabled: false, expectedRequests);
        }

        private static void AssertFlagCombinationPlans(
            bool activeEnabled,
            bool blockEnabled,
            bool windupEnabled,
            int expectedRequests)
        {
            var owner = new GameObject($"FrontFaceShieldFlags_{activeEnabled}_{blockEnabled}_{windupEnabled}");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                var sourceView = AddSourceView(owner);
                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    activeSignals: new[]
                    {
                        CreateSourceSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    },
                    blockSignals: new[]
                    {
                        CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), new CubeTopologyState(FaceId.Floor)),
                    },
                    windupSignals: new[]
                    {
                        CreateWindupSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    }), sourceView: sourceView));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(expectedRequests));
            }
            finally
            {
                Destroy(owner);
            }
        }

        private static GameplayEntityView AddSourceView(GameObject owner)
        {
            var view = owner.AddComponent<GameplayEntityView>();
            view.Initialize(40);
            return view;
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickPresentationData presentationData,
            EnemyPresentationCatalog catalog = null,
            EnemyPresentationBinding[] bindings = null,
            GameplayEntityView sourceView = null)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            if (sourceView != null)
            {
                stateStore.ViewsByEntityId[40] = sourceView;
            }

            stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(
                new Vector3(0.5f, 0.5f, 0.1f),
                Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateTickResult(12, new[] { CreateEnemyUnit(40, new SurfaceCell(FaceId.Floor, 0, 0)) }, topology, presentationData),
                topology,
                stateStore,
                projector,
                catalog,
                bindings);
        }

        private static TickPresentationData CreatePresentationData(
            TickFrontFaceShieldSourceSignal[] activeSignals = null,
            TickFrontFaceShieldBlockSignal[] blockSignals = null,
            TickFrontFaceShieldWindupWarningSignal[] windupSignals = null,
            TickEntityExitPresentationSignal[] exitSignals = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: exitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                frontFaceShieldSources: activeSignals ?? Array.Empty<TickFrontFaceShieldSourceSignal>(),
                frontFaceShieldBlocks: blockSignals ?? Array.Empty<TickFrontFaceShieldBlockSignal>(),
                frontFaceShieldWindupWarnings: windupSignals ?? Array.Empty<TickFrontFaceShieldWindupWarningSignal>());
        }

        private static TickFrontFaceShieldSourceSignal CreateSourceSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int tickIndex = 12,
            int presentationSeed = 440)
        {
            return new TickFrontFaceShieldSourceSignal(
                sourceEntityId,
                sourceCell,
                topology,
                radius: 1,
                includeSourceCell: true,
                FrontFaceShieldTargetPattern.ManhattanRadius,
                tickIndex,
                presentationSeed);
        }

        private static TickFrontFaceShieldBlockSignal CreateBlockSignal(
            int shieldSourceEntityId,
            int boxEntityId,
            int actorEntityId,
            SurfaceCell blockedCell,
            CubeTopologyState topology,
            int presentationSeed = 778)
        {
            return new TickFrontFaceShieldBlockSignal(
                shieldSourceEntityId,
                boxEntityId,
                actorEntityId,
                blockedCell,
                new SurfaceCell(blockedCell.face, 0, 0),
                FrontFaceShieldBlockMovementKind.SlidingContinuation,
                topology,
                tickIndex: 12,
                presentationSeed);
        }

        private static TickFrontFaceShieldWindupWarningSignal CreateWindupSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int effectIndex = 0,
            int activationSequence = 1,
            int presentationSeed = 884)
        {
            return new TickFrontFaceShieldWindupWarningSignal(
                sourceEntityId,
                effectIndex,
                sourceCell,
                topology,
                radius: 1,
                includeSourceCell: false,
                FrontFaceShieldTargetPattern.ManhattanRadius,
                windupStartTick: 12,
                windupEndTick: 14,
                activationSequence,
                tickIndex: 12,
                presentationSeed);
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(
            int entityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                TickEntityExitCause.Killed,
                sourceCell,
                topology,
                Direction.Right,
                EntityType.Unit,
                presentationSeed: entityId);
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            EntityState[] finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
            };
        }

        private static CoordinatorScenario CreateCoordinatorScenario(string name)
        {
            var root = new GameObject(name);
            var enemyPrefabObject = new GameObject($"{name}_EnemyPrefab");
            var enemyPrefab = enemyPrefabObject.AddComponent<GameplayEntityView>();
            enemyPrefab.Initialize(40);
            enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
            enemyPrefabObject.AddComponent<EnemyAnimationTimingAuthoring>();
            var unitAuthoring = enemyPrefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
            SetField(unitAuthoring, "moveMotionDurationSeconds", UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel);
            var entityAuthoring = enemyPrefabObject.AddComponent<EntityMotionPresentationAuthoring>();
            SetField(entityAuthoring, "moveMotionDurationSeconds", EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);
            var shieldAuthoring = enemyPrefabObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
            SetField(shieldAuthoring, "attachActiveLoopToSourceView", true);

            var presenter = root.AddComponent<GameplayTickViewPresenter>();
            var registry = root.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new System.Collections.Generic.Dictionary<int, GameplayEntityView>
                    {
                        { 40, enemyPrefab },
                    }));
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                topology,
                1f,
                CreateTimingProfile());

            return new CoordinatorScenario(root, presenter, enemyPrefabObject, sourceCell, topology);
        }

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
        }

        private static VfxBindingDefinitionAsset CreateActiveBinding(
            GameObject prefab,
            float tailSeconds = 0.3f)
        {
            return CreateBinding(
                prefab,
                EnemyVfxCue.FrontFaceShieldActive,
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                lifetimeSeconds: 0f,
                tailSeconds,
                maxConcurrent: 8);
        }

        private static VfxBindingDefinitionAsset CreateBlockBinding(GameObject prefab)
        {
            return CreateBinding(
                prefab,
                EnemyVfxCue.FrontFaceShieldBlock,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                lifetimeSeconds: 0.3f,
                tailSeconds: 0.2f,
                maxConcurrent: 12);
        }

        private static VfxBindingDefinitionAsset CreateWindupBinding(
            GameObject prefab,
            float tailSeconds = 0.25f)
        {
            return CreateBinding(
                prefab,
                EnemyVfxCue.FrontFaceShieldWindup,
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                lifetimeSeconds: 0f,
                tailSeconds,
                maxConcurrent: 8);
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            GameObject prefab,
            EnemyVfxCue cue,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            float lifetimeSeconds,
            float tailSeconds,
            int maxConcurrent)
        {
            GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)cue);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", playbackMode);
            SetField(binding, "stopPolicy", stopPolicy);
            SetField(binding, "defaultLifetimeSeconds", lifetimeSeconds);
            SetField(binding, "tailSeconds", tailSeconds);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", maxConcurrent);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static VfxProfileAsset CreateEnemyProfile(params VfxBindingDefinitionAsset[] bindings)
        {
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            SetField(profile, "family", GameplayVfxFamily.Enemy);
            SetField(profile, "bindings", bindings);
            return profile;
        }

        private static EnemyPresentationCatalog CreateEnemyPresentationCatalog(
            string presentationId,
            VfxProfileAsset profile)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            SetField(
                catalog,
                "entries",
                new[]
                {
                    new EnemyPresentationCatalogEntry
                    {
                        PresentationId = presentationId,
                        VfxProfileAsset = profile,
                    },
                });
            return catalog;
        }

        private static void AssertPrefabValid(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true), Is.Empty);
        }

        private static int CountDescendantsByNamePrefix(Transform root, string prefix)
        {
            var count = 0;
            var descendants = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null &&
                    descendants[i] != root &&
                    descendants[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObjects[i]);
                }
            }
        }

        private sealed class CoordinatorScenario
        {
            public CoordinatorScenario(
                GameObject root,
                GameplayTickViewPresenter presenter,
                GameObject enemyPrefab,
                SurfaceCell sourceCell,
                CubeTopologyState topology)
            {
                Root = root;
                Presenter = presenter;
                EnemyPrefab = enemyPrefab;
                SourceCell = sourceCell;
                Topology = topology;
            }

            public GameObject Root { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameObject EnemyPrefab { get; }

            public SurfaceCell SourceCell { get; }

            public CubeTopologyState Topology { get; }

            public void Destroy()
            {
                GameplayVfxFrontFaceShieldMigrationTests.Destroy(EnemyPrefab, Root);
            }
        }
    }
}
