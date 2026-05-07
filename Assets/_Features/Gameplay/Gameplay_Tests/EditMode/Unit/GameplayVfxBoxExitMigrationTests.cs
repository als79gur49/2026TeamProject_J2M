using System;
using System.IO;
using System.Reflection;
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

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBoxExitMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string BoxDestroySmokePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxDestroySmokeVfx.prefab";
        private const string BoxDestroyShrinkPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxDestroyShrinkVfx.prefab";
        private const string BoxDestroySmokeBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxDestroySmoke_Binding.asset";
        private const string BoxDestroyShrinkBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxDestroyShrink_Binding.asset";
        private const string ItemConsumeBurstPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/ItemConsumeBurstVfx.prefab";
        private const string ItemConsumeBurstBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ItemConsumeBurst_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void BoxPlanner_BoxDestroy_EmitsDestroySmokeRequest()
        {
            var cell = new SurfaceCell(FaceId.Back, 2, 3);
            var topology = new CubeTopologyState(FaceId.Back);
            var request = PlanSingleRequest(CreateExitSignal(20, TickEntityExitCause.BoxDestroy, cell, topology));

            AssertBoxExitRequest(request, BoxVfxCue.DestroySmoke, 20, 8831, cell, topology);
        }

        [Test]
        [Category("Extended")]
        public void BoxPlanner_ItemConsume_EmitsItemConsumeRequest()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 4);
            var topology = new CubeTopologyState(FaceId.Front);
            var request = PlanSingleRequest(CreateExitSignal(21, TickEntityExitCause.ItemConsume, cell, topology));

            AssertBoxExitRequest(request, BoxVfxCue.ItemConsume, 21, 8831, cell, topology);
        }

        [Test]
        [Category("Extended")]
        public void BoxPlanner_CausesDoNotCrossEmit()
        {
            var destroyPlan = PlanRequests(CreateExitSignal(20, TickEntityExitCause.BoxDestroy));
            var consumePlan = PlanRequests(CreateExitSignal(21, TickEntityExitCause.ItemConsume));

            Assert.That(destroyPlan.Requests, Has.None.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(BoxVfxCue.ItemConsume)));
            Assert.That(consumePlan.Requests, Has.None.Matches<GameplayVfxRequest>(
                request => request.CueId == GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)));
        }

        [Test]
        [Category("Extended")]
        public void BoxPlanner_NonBoxAndEnemyExit_DoNotEmitBoxVfx()
        {
            var nonBoxPlan = PlanRequests(CreateExitSignal(30, TickEntityExitCause.BoxDestroy, entityType: EntityType.Unit));
            var enemyPlan = PlanRequests(CreateExitSignal(40, TickEntityExitCause.Killed, entityType: EntityType.Unit));

            Assert.That(nonBoxPlan.Requests, Is.Empty);
            Assert.That(enemyPlan.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BoxPlanner_FlipImpactDestroySelf_DoesNotEmitDuplicateDestroySmoke()
        {
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var planner = new BoxVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(
                        new[] { CreateExitSignal(20, TickEntityExitCause.BoxDestroy, cell, topology) },
                        flipImpactSignals: new[]
                        {
                            new FlipImpactPresentationSignal(
                                sourceActionPlanId: 1,
                                boxEntityId: 20,
                                impactTargetEntityId: 30,
                                actorEntityId: 10,
                                cell,
                                new SurfaceCell(FaceId.Floor, 1, 0),
                                topology,
                                Direction.Right,
                                Direction.Left,
                                FlipImpactPresentationDisposition.DestroySelf),
                        }),
                    topology),
                builder);

            Assert.That(builder.Build().Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BoxPlanner_ImpactTransient_DoesNotEmitDuplicateDestroySmoke()
        {
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var planner = new BoxVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(
                        new[] { CreateExitSignal(20, TickEntityExitCause.BoxDestroy, cell, topology) },
                        impactTransientSignals: new[]
                        {
                            new TickImpactTransientPresentationSignal(
                                20,
                                EntityType.Box,
                                cell,
                                new SurfaceCell(FaceId.Floor, 1, 0),
                                topology,
                                Direction.Right,
                                presentationSeed: 101),
                        }),
                    topology),
                builder);

            Assert.That(builder.Build().Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BoxPlanner_ZeroPresentationSeed_FallsBackToExitedEntityId()
        {
            var request = PlanSingleRequest(CreateExitSignal(22, TickEntityExitCause.BoxDestroy, presentationSeed: 0));

            Assert.That(request.PresentationSeed, Is.EqualTo(22));
        }

        [Test]
        [Category("Extended")]
        public void BoxDestroyShrinkBuilder_BoxDestroy_BuildsSourceCloneEaseCommand()
        {
            var cell = new SurfaceCell(FaceId.Back, 2, 3);
            var topology = new CubeTopologyState(FaceId.Back);
            var signal = CreateExitSignal(20, TickEntityExitCause.BoxDestroy, cell, topology);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);
            var poseResolver = new GameplayPoseResolver(
                new GameplayPresentationStateStore(),
                new GameplayPresentationTrackState());

            var built = BoxDestroyShrinkVfxCommandBuilder.TryBuild(
                12,
                signal,
                GameplayTimingProfile.CreateDefault(),
                poseResolver,
                projector,
                out var command);

            Assert.That(built, Is.True);
            Assert.That(command.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.DestroyShrink)));
            Assert.That(command.SourceEntityId, Is.EqualTo(20));
            Assert.That(command.SequenceId, Is.EqualTo(8831));
            Assert.That(command.PresentationSeed, Is.EqualTo(8831));
            Assert.That(command.SourceLocalPosition, Is.EqualTo(command.TargetLocalPosition));
            Assert.That(command.SourceLocalRotation, Is.EqualTo(command.TargetLocalRotation));
            Assert.That(command.DurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultBoxDestroyEffectDurationSeconds).Within(0.0001f));
            Assert.That(command.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.SourceViewCloneWithPrefabFallback));
            Assert.That(command.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.DestroyShrinkEase));
            Assert.That(command.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.Linear));
        }

        [Test]
        [Category("Extended")]
        public void BoxDestroyShrinkBuilder_NonCandidates_DoNotBuild()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);
            var poseResolver = new GameplayPoseResolver(
                new GameplayPresentationStateStore(),
                new GameplayPresentationTrackState());

            Assert.That(TryBuildShrink(CreateExitSignal(21, TickEntityExitCause.ItemConsume), poseResolver, projector), Is.False);
            Assert.That(TryBuildShrink(CreateExitSignal(30, TickEntityExitCause.BoxDestroy, entityType: EntityType.Unit), poseResolver, projector), Is.False);
            Assert.That(TryBuildShrink(CreateExitSignal(0, TickEntityExitCause.BoxDestroy), poseResolver, projector), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_BoxExitMigrationFlags_DefaultTrue()
        {
            var owner = new GameObject("BoxExitDefaultFlags");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxBoxDestroySmokeMigration, Is.True);
                Assert.That(runtime.EnableGameplayVfxBoxDestroyShrinkMigration, Is.True);
                Assert.That(runtime.EnableGameplayVfxItemConsumeBurstMigration, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_BoxAndItemFlags_AreIndependent()
        {
            AssertFlagCombinationPlans(boxEnabled: true, itemEnabled: false, expectedRequests: 1);
            AssertFlagCombinationPlans(boxEnabled: false, itemEnabled: true, expectedRequests: 1);
            AssertFlagCombinationPlans(boxEnabled: true, itemEnabled: true, expectedRequests: 2);
            AssertFlagCombinationPlans(boxEnabled: false, itemEnabled: false, expectedRequests: 0);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_BoxDestroyFlagOnMissingBinding_DiagnosticOnly()
        {
            var owner = new GameObject("BoxDestroyMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = true;
                runtime.EnableGameplayVfxBoxDestroyShrinkMigration = false;

                runtime.Present(CreateExtensionContext(CreateExitSignal(20, TickEntityExitCause.BoxDestroy)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_BoxDestroyShrinkFlagOnMissingBinding_DiagnosticOnlyNoOldFallback()
        {
            var owner = new GameObject("BoxDestroyShrinkMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = false;
                runtime.EnableGameplayVfxBoxDestroyShrinkMigration = true;

                runtime.Present(CreateExtensionContext(CreateExitSignal(20, TickEntityExitCause.BoxDestroy)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_ItemConsumeFlagOnMissingBinding_DiagnosticOnly()
        {
            var owner = new GameObject("ItemConsumeMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxItemConsumeBurstMigration = true;

                runtime.Present(CreateExtensionContext(CreateExitSignal(21, TickEntityExitCause.ItemConsume)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_BoxDestroyFlagOnWithBinding_PlaysOneTransientInstance()
        {
            AssertFlagOnWithBindingPlaysOneInstance(
                TickEntityExitCause.BoxDestroy,
                BoxVfxCue.DestroySmoke,
                runtime => runtime.EnableGameplayVfxBoxDestroySmokeMigration = true);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_ItemConsumeFlagOnWithBinding_PlaysOneTransientInstance()
        {
            AssertFlagOnWithBindingPlaysOneInstance(
                TickEntityExitCause.ItemConsume,
                BoxVfxCue.ItemConsume,
                runtime => runtime.EnableGameplayVfxItemConsumeBurstMigration = true);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_BoxExitFlagOnWithBinding_DoesNotCreateSnapshots()
        {
            var owner = new GameObject("BoxExitSnapshotGuard");
            var prefab = new GameObject("BoxExitSnapshotPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, BoxVfxCue.DestroySmoke);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                SnapshotMaterializationCounts counts;
                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    runtime.Present(CreateExtensionContext(CreateExitSignal(20, TickEntityExitCause.BoxDestroy)));
                    counts = capture.Counts;
                }

                Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_BoxDestroyFlagOff_DoesNotUseOldExitFallbackAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("BoxDestroyFlagOff");
            try
            {
                scenario.Presenter.PresentInitial(
                    new[] { CreateBox(20, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateExitSignal(20, TickEntityExitCause.BoxDestroy, scenario.BoxCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(scenario.Registry.TryGetView(20, out var boxView), Is.True);
                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_BoxDestroyFlagOn_UsesVfxOnlyAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("BoxDestroyFlagOn");
            var vfxPrefab = new GameObject("BoxDestroyFlagOn_VfxPrefab");
            VfxBindingDefinitionAsset smokeBinding = null;
            VfxBindingDefinitionAsset shrinkBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                smokeBinding = CreateBinding(vfxPrefab, BoxVfxCue.DestroySmoke);
                shrinkBinding = CreateBinding(vfxPrefab, BoxVfxCue.DestroyShrink);
                cueMap = CreateCueMap(smokeBinding, shrinkBinding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = true;
                runtime.EnableGameplayVfxBoxDestroyShrinkMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[] { CreateBox(20, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateExitSignal(20, TickEntityExitCause.BoxDestroy, scenario.BoxCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(2));
                Assert.That(scenario.Registry.TryGetView(20, out var boxView), Is.True);
                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Destroy(cueMap, shrinkBinding, smokeBinding, vfxPrefab);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_SmokeOnShrinkOff_UsesSmokeOnlyAndNoOldExitEffect()
        {
            var scenario = CreatePresenterScenario("SmokeOnShrinkOff");
            var vfxPrefab = new GameObject("SmokeOnShrinkOff_VfxPrefab");
            VfxBindingDefinitionAsset smokeBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                smokeBinding = CreateBinding(vfxPrefab, BoxVfxCue.DestroySmoke);
                cueMap = CreateCueMap(smokeBinding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = true;
                runtime.EnableGameplayVfxBoxDestroyShrinkMigration = false;
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[] { CreateBox(20, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateExitSignal(20, TickEntityExitCause.BoxDestroy, scenario.BoxCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(scenario.Registry.TryGetView(20, out var boxView), Is.True);
                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Destroy(cueMap, smokeBinding, vfxPrefab);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_ShrinkOnSmokeOff_UsesShrinkOnlyAndSuppressesOldExitEffect()
        {
            var scenario = CreatePresenterScenario("ShrinkOnSmokeOff");
            var vfxPrefab = new GameObject("ShrinkOnSmokeOff_VfxPrefab");
            VfxBindingDefinitionAsset shrinkBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                shrinkBinding = CreateBinding(vfxPrefab, BoxVfxCue.DestroyShrink);
                cueMap = CreateCueMap(shrinkBinding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = false;
                runtime.EnableGameplayVfxBoxDestroyShrinkMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[] { CreateBox(20, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateExitSignal(20, TickEntityExitCause.BoxDestroy, scenario.BoxCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, shrinkBinding, vfxPrefab);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_ItemConsumeFlagOn_UsesVfxOnlyAndDoesNotFallbackWhenMissingBinding()
        {
            var scenario = CreatePresenterScenario("ItemConsumeFlagOnMissingBinding");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxItemConsumeBurstMigration = true;
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[] { CreateBox(21, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateExitSignal(21, TickEntityExitCause.ItemConsume, scenario.BoxCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                Assert.That(scenario.Registry.TryGetView(21, out var itemView), Is.True);
                Assert.That(itemView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_ItemConsumeFlagOff_DoesNotUseOldFallbackAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("ItemConsumeFlagOff");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxItemConsumeBurstMigration = false;
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[] { CreateBox(21, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateExitSignal(21, TickEntityExitCause.ItemConsume, scenario.BoxCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                Assert.That(scenario.Registry.TryGetView(21, out var itemView), Is.True);
                Assert.That(itemView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_EnemyDeathOldPath_NotRestoredByBoxItemFlags()
        {
            var scenario = CreatePresenterScenario("EnemyDeathNotSuppressed");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = true;
                runtime.EnableGameplayVfxItemConsumeBurstMigration = true;
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[] { CreateEnemyUnit(40, scenario.BoxCell) },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(new[]
                    {
                        CreateExitSignal(40, TickEntityExitCause.Killed, scenario.BoxCell, scenario.Topology, EntityType.Unit),
                    }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));

                Assert.That(scenario.Presenter.ActiveTransientEffectCount, Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxExitPrefabs_PassVfxPrefabValidation()
        {
            AssertPrefabValid(BoxDestroySmokePrefabPath);
            AssertPrefabValid(BoxDestroyShrinkPrefabPath);
            AssertPrefabValid(ItemConsumeBurstPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void BoxExitBindings_ValidateAndUseExpectedPolicy()
        {
            AssertBindingPolicy(
                BoxDestroySmokeBindingPath,
                BoxVfxCue.DestroySmoke,
                expectedLifetime: 0.18f,
                expectedTail: 0.25f,
                expectedMaxConcurrent: 12);
            AssertBindingPolicy(
                BoxDestroyShrinkBindingPath,
                BoxVfxCue.DestroyShrink,
                expectedLifetime: 0f,
                expectedTail: 0.18f,
                expectedMaxConcurrent: 12);
            AssertBindingPolicy(
                ItemConsumeBurstBindingPath,
                BoxVfxCue.ItemConsume,
                expectedLifetime: 0.18f,
                expectedTail: 0.20f,
                expectedMaxConcurrent: 8);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_ResolvesBoxExitCues()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke), out var smoke), Is.True);
            Assert.That(smoke.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(smoke.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(smoke.MaxConcurrentInstances, Is.EqualTo(12));
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(BoxVfxCue.DestroyShrink), out var shrink), Is.True);
            Assert.That(shrink.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(shrink.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(shrink.MaxConcurrentInstances, Is.EqualTo(12));
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(BoxVfxCue.ItemConsume), out var item), Is.True);
            Assert.That(item.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(item.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(item.MaxConcurrentInstances, Is.EqualTo(8));
        }

        [Test]
        [Category("Extended")]
        public void BoxExitPlannerAndRuntimeSources_DoNotReferenceAuthorityTypes()
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

        private static GameplayVfxRequestPlan PlanRequests(params TickEntityExitPresentationSignal[] exitSignals)
        {
            var planner = new BoxVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var topology = new CubeTopologyState(FaceId.Floor);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(exitSignals),
                    topology),
                builder);

            return builder.Build();
        }

        private static GameplayVfxRequest PlanSingleRequest(TickEntityExitPresentationSignal exitSignal)
        {
            var plan = PlanRequests(exitSignal);

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static bool TryBuildShrink(
            TickEntityExitPresentationSignal signal,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector)
        {
            return BoxDestroyShrinkVfxCommandBuilder.TryBuild(
                12,
                signal,
                GameplayTimingProfile.CreateDefault(),
                poseResolver,
                projector,
                out _);
        }

        private static void AssertBoxExitRequest(
            in GameplayVfxRequest request,
            BoxVfxCue cue,
            int entityId,
            int presentationSeed,
            SurfaceCell cell,
            CubeTopologyState topology)
        {
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.SequenceId, Is.EqualTo(entityId));
            Assert.That(request.SourceEntityId, Is.EqualTo(entityId));
            Assert.That(request.PresentationSeed, Is.EqualTo(presentationSeed));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(cue)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PersistentKey, Is.EqualTo(VfxPersistentKey.None));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(cell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
        }

        private static void AssertFlagCombinationPlans(bool boxEnabled, bool itemEnabled, int expectedRequests)
        {
            var owner = new GameObject("BoxExitFlagCombination");
            var prefab = new GameObject("BoxExitFlagCombinationPrefab");
            VfxBindingDefinitionAsset smokeBinding = null;
            VfxBindingDefinitionAsset itemBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                smokeBinding = CreateBinding(prefab, BoxVfxCue.DestroySmoke);
                itemBinding = CreateBinding(prefab, BoxVfxCue.ItemConsume);
                cueMap = CreateCueMap(smokeBinding, itemBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = boxEnabled;
                runtime.EnableGameplayVfxBoxDestroyShrinkMigration = false;
                runtime.EnableGameplayVfxItemConsumeBurstMigration = itemEnabled;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreateExitSignal(20, TickEntityExitCause.BoxDestroy),
                    CreateExitSignal(21, TickEntityExitCause.ItemConsume)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(expectedRequests));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(expectedRequests));
            }
            finally
            {
                Destroy(cueMap, itemBinding, smokeBinding, prefab, owner);
            }
        }

        private static void AssertFlagOnWithBindingPlaysOneInstance(
            TickEntityExitCause exitCause,
            BoxVfxCue cue,
            Action<GameplayVfxProductionRuntime> enableFlag)
        {
            var owner = new GameObject($"BoxExit_{cue}_Enabled");
            var prefab = new GameObject($"BoxExit_{cue}_Prefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, cue);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                if (cue == BoxVfxCue.DestroySmoke)
                {
                    runtime.EnableGameplayVfxBoxDestroyShrinkMigration = false;
                }

                enableFlag(runtime);
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreateExitSignal(20, exitCause)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params TickEntityExitPresentationSignal[] exitSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(exitSignals),
                    topology,
                    Array.Empty<EntityState>()),
                topology,
                stateStore,
                projector);
        }

        private static PresenterScenario CreatePresenterScenario(string name)
        {
            var root = new GameObject(name);
            var presenter = root.AddComponent<GameplayTickViewPresenter>();
            var registry = root.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10));
            var topology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3));

            presenter.Initialize(
                binder,
                boardBounds,
                topology,
                1f,
                GameplayTimingProfile.CreateDefault());

            return new PresenterScenario(
                root,
                presenter,
                registry,
                topology,
                new SurfaceCell(FaceId.Floor, 1, 1));
        }

        private static TickResult CreateResult(
            TickPresentationData presentationData,
            CubeTopologyState topology,
            EntityState[] finalEntities)
        {
            return new TickResult(
                12,
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

        private static TickPresentationData CreatePresentationData(
            TickEntityExitPresentationSignal[] exitSignals,
            TickImpactTransientPresentationSignal[] impactTransientSignals = null,
            FlipImpactPresentationSignal[] flipImpactSignals = null)
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
                exitSignals,
                impactTransientSignals ?? Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals ?? Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(
            int entityId,
            TickEntityExitCause exitCause,
            SurfaceCell cell = default,
            CubeTopologyState topology = default,
            EntityType entityType = EntityType.Box,
            int presentationSeed = 8831)
        {
            var resolvedCell = cell.Equals(default(SurfaceCell))
                ? new SurfaceCell(FaceId.Floor, 1, 1)
                : cell;
            var resolvedTopology = topology.Equals(default(CubeTopologyState))
                ? new CubeTopologyState(FaceId.Floor)
                : topology;
            return new TickEntityExitPresentationSignal(
                entityId,
                exitCause,
                resolvedCell,
                resolvedTopology,
                Direction.Right,
                entityType,
                sourceActorEntityId: 10,
                presentationSeed: presentationSeed);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 0,
                maxHp = 2,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Chase,
            };
        }

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab, BoxVfxCue cue)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Box);
            SetField(binding, "cueCode", (int)cue);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", cue == BoxVfxCue.DestroyShrink ? 0f : 0.18f);
            SetField(binding, "tailSeconds", cue == BoxVfxCue.DestroySmoke ? 0.25f : cue == BoxVfxCue.DestroyShrink ? 0.18f : 0.20f);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", cue == BoxVfxCue.DestroySmoke || cue == BoxVfxCue.DestroyShrink ? 12 : 8);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
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
        }

        private static void AssertBindingPolicy(
            string path,
            BoxVfxCue cue,
            float expectedLifetime,
            float expectedTail,
            int expectedMaxConcurrent)
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(path);

            Assert.That(binding, Is.Not.Null, path);
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(cue)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.DefaultLifetimeSeconds, Is.EqualTo(expectedLifetime).Within(0.001f));
            Assert.That(binding.TailSeconds, Is.EqualTo(expectedTail).Within(0.001f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(expectedMaxConcurrent));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
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

        private readonly struct PresenterScenario
        {
            public PresenterScenario(
                GameObject root,
                GameplayTickViewPresenter presenter,
                GameplayEntityViewRegistry registry,
                CubeTopologyState topology,
                SurfaceCell boxCell)
            {
                Root = root;
                Presenter = presenter;
                Registry = registry;
                Topology = topology;
                BoxCell = boxCell;
            }

            public GameObject Root { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameplayEntityViewRegistry Registry { get; }

            public CubeTopologyState Topology { get; }

            public SurfaceCell BoxCell { get; }

            public void Destroy()
            {
                GameplayVfxBoxExitMigrationTests.Destroy(Root);
            }
        }
    }
}
