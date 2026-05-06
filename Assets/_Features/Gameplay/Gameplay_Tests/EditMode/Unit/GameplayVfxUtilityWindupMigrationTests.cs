using System;
using System.Collections.Generic;
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
    public sealed class GameplayVfxUtilityWindupMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string UtilityWindupPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyUtilityWindupTelegraphVfx.prefab";
        private const string UtilityWindupBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyUtilityWindupTelegraph_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupActive_EmitsPersistentRequest()
        {
            var sourceCell = new SurfaceCell(FaceId.Back, 2, 1);
            var topology = new CubeTopologyState(FaceId.Back);

            var request = PlanSingleUtilityWindupRequest(
                CreateSummonWindupWarningSignal(40, sourceCell, topology, effectIndex: 2, activationSequence: 7));

            AssertUtilityWindupRequest(request, 40, sourceCell, topology, effectIndex: 2, activationSequence: 7);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupStarted_EmitsPersistentRequest()
        {
            var signal = CreateSummonWindupWarningSignal(
                40,
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                windupStartTick: 12,
                tickIndex: 12);

            var request = PlanSingleUtilityWindupRequest(signal);

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup)));
            Assert.That(request.IsPersistent, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupEndedOrCancelled_DoesNotEmitRequest()
        {
            AssertNoUtilityWindupRequests(CreatePresentationData());
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_SourceEntityExitSameTick_DoesNotEmitRequest()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var presentationData = CreatePresentationData(
                summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, sourceCell, topology) },
                entityExitSignals: new[] { CreateExitSignal(40, sourceCell, topology) });

            AssertNoUtilityWindupRequests(presentationData);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_InvalidSourceEntity_DoesNotEmitRequest()
        {
            AssertNoUtilityWindupRequests(CreatePresentationData(
                summonWindupWarnings: new[]
                {
                    CreateSummonWindupWarningSignal(0, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                }));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_UtilityWindupMigrationFlag_DefaultsTrue()
        {
            var owner = new GameObject("UtilityWindupDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxUtilityWindupMigration, Is.True);
                Assert.That(runtime.SuppressLegacyUtilityWindupVfx, Is.True);
                Assert.That(runtime.IsRuntimeInitialized, Is.False);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOff_DoesNotPlanOrInitialize()
        {
            var owner = new GameObject("UtilityWindupFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = false;

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[]
                    {
                        CreateSummonWindupWarningSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.IsRuntimeInitialized, Is.False);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOnMissingBinding_DiagnosticNoOldFallback()
        {
            var owner = new GameObject("UtilityWindupMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = true;

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[]
                    {
                        CreateSummonWindupWarningSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                Assert.That(runtime.SuppressLegacyUtilityWindupVfx, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_ActiveTwoTicks_DoesNotDuplicateSpawn()
        {
            var owner = new GameObject("UtilityWindupPersistentReuse");
            var prefab = new GameObject("UtilityWindupPersistentPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateUtilityWindupBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var topology = new CubeTopologyState(FaceId.Floor);

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, sourceCell, topology, tickIndex: 12) })));
                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, sourceCell, topology, tickIndex: 13) })));

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

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_DesiredStateRemoved_StopsTailAndReleases()
        {
            var owner = new GameObject("UtilityWindupPersistentStop");
            var prefab = new GameObject("UtilityWindupStopPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateUtilityWindupBinding(prefab, tailSeconds: 0f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[]
                    {
                        CreateSummonWindupWarningSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                runtime.Present(CreateExtensionContext(CreatePresentationData()));
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                runtime.UpdatePresentation(0f);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_EnemyDeath_RemovesDesiredStateAndStopsHandle()
        {
            var owner = new GameObject("UtilityWindupDeathStop");
            var prefab = new GameObject("UtilityWindupDeathStopPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateUtilityWindupBinding(prefab, tailSeconds: 0f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var topology = new CubeTopologyState(FaceId.Floor);

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, sourceCell, topology) })));
                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, sourceCell, topology, tickIndex: 13) },
                    entityExitSignals: new[] { CreateExitSignal(40, sourceCell, topology) })));
                runtime.UpdatePresentation(0f);

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOnWithBinding_DoesNotCreateSnapshots()
        {
            var owner = new GameObject("UtilityWindupSnapshotGuard");
            var prefab = new GameObject("UtilityWindupSnapshotPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateUtilityWindupBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                SnapshotMaterializationCounts counts;
                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    runtime.Present(CreateExtensionContext(CreatePresentationData(
                        summonWindupWarnings: new[]
                        {
                            CreateSummonWindupWarningSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                        })));
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
        public void ProductionRuntime_SourceProfile_OverridesHostDefault()
        {
            var owner = new GameObject("UtilityWindupSourceProfileOverride");
            var sourcePrefab = new GameObject("UtilityWindupSourceProfilePrefab");
            var hostPrefab = new GameObject("UtilityWindupHostPrefab");
            VfxBindingDefinitionAsset sourceBinding = null;
            VfxBindingDefinitionAsset hostBinding = null;
            VfxProfileAsset sourceProfile = null;
            VfxCueMapAsset cueMap = null;
            EnemyPresentationCatalog catalog = null;
            try
            {
                sourceBinding = CreateUtilityWindupBinding(sourcePrefab);
                hostBinding = CreateUtilityWindupBinding(hostPrefab);
                sourceProfile = CreateEnemyProfile(sourceBinding);
                cueMap = CreateCueMap(hostBinding);
                catalog = CreateEnemyPresentationCatalog("utility-source", sourceProfile);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(
                        summonWindupWarnings: new[]
                        {
                            CreateSummonWindupWarningSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                        }),
                    catalog,
                    new[] { new EnemyPresentationBinding { EntityId = 40, PresentationId = "utility-source" } }));

                var persistentRoot = owner.transform.Find("GameplayVfxRuntimeRoot/Persistent");
                Assert.That(persistentRoot, Is.Not.Null);
                Assert.That(persistentRoot.childCount, Is.EqualTo(1));
                Assert.That(persistentRoot.GetChild(0).name, Does.StartWith(sourcePrefab.name));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(catalog, cueMap, sourceProfile, hostBinding, sourceBinding, hostPrefab, sourcePrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_FlagOff_DoesNotUseLegacyFallback()
        {
            var scenario = CreateCoordinatorScenario("UtilityWindupLegacyFlagOff");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = false;
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(new[] { CreateEnemyUnit(40, scenario.SourceCell) }, scenario.Topology);

                scenario.Presenter.Present(CreateTickResult(
                    tickIndex: 12,
                    finalEntities: new[] { CreateEnemyUnit(40, scenario.SourceCell) },
                    topology: scenario.Topology,
                    presentationData: CreatePresentationData(
                        summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, scenario.SourceCell, scenario.Topology) })));

                Assert.That(CountDescendantsByNamePrefix(scenario.Root.transform, "SummonWindupWarning_40"), Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_FlagOn_SuppressesLegacyPresenter()
        {
            var scenario = CreateCoordinatorScenario("UtilityWindupLegacyFlagOn");
            var vfxPrefab = new GameObject("UtilityWindupCoordinatorVfxPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateUtilityWindupBinding(vfxPrefab);
                cueMap = CreateCueMap(binding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(new[] { CreateEnemyUnit(40, scenario.SourceCell) }, scenario.Topology);

                scenario.Presenter.Present(CreateTickResult(
                    tickIndex: 12,
                    finalEntities: new[] { CreateEnemyUnit(40, scenario.SourceCell) },
                    topology: scenario.Topology,
                    presentationData: CreatePresentationData(
                        summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, scenario.SourceCell, scenario.Topology) })));

                Assert.That(CountDescendantsByNamePrefix(scenario.Root.transform, "SummonWindupWarning_40"), Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, vfxPrefab);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_FlagOnMissingBinding_DoesNotFallbackToLegacy()
        {
            var scenario = CreateCoordinatorScenario("UtilityWindupLegacyMissingBinding");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxUtilityWindupMigration = true;
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(new[] { CreateEnemyUnit(40, scenario.SourceCell) }, scenario.Topology);

                scenario.Presenter.Present(CreateTickResult(
                    tickIndex: 12,
                    finalEntities: new[] { CreateEnemyUnit(40, scenario.SourceCell) },
                    topology: scenario.Topology,
                    presentationData: CreatePresentationData(
                        summonWindupWarnings: new[] { CreateSummonWindupWarningSignal(40, scenario.SourceCell, scenario.Topology) })));

                Assert.That(CountDescendantsByNamePrefix(scenario.Root.transform, "SummonWindupWarning_40"), Is.Zero);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void LegacyPresenter_EmptyRefreshCleansExistingWarnings()
        {
            var root = new GameObject("UtilityWindupLegacyCleanup");
            var warningPrefab = new GameObject("UtilityWindupLegacyCleanup_LegacyWarningPrefab");
            var sourceObject = new GameObject("UtilityWindupLegacyCleanup_Source");
            try
            {
                var sourceView = sourceObject.AddComponent<GameplayEntityView>();
                sourceView.Initialize(40);
                var utilityAuthoring = sourceObject.AddComponent<EnemyUtilityWindupPresentationAuthoring>();
                SetField(utilityAuthoring, "summonWindupWarningPrefab", warningPrefab);
                SetField(utilityAuthoring, "attachSummonWarningToSourceView", true);
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stateStore = new GameplayPresentationStateStore();
                stateStore.ResetSession(topology);
                stateStore.ViewsByEntityId[40] = sourceView;
                stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
                var projector = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    1f);
                var presenter = new GameplayUtilityWindupVfxPresenter();
                presenter.Initialize(root.transform);

                presenter.RefreshSummonWarnings(
                    new[] { CreateSummonWindupWarningSignal(40, sourceCell, topology) },
                    stateStore,
                    projector);
                Assert.That(CountDescendantsByNamePrefix(sourceObject.transform, "SummonWindupWarning_40"), Is.EqualTo(1));

                presenter.RefreshSummonWarnings(
                    Array.Empty<TickSummonWindupWarningSignal>(),
                    stateStore,
                    projector);

                Assert.That(CountDescendantsByNamePrefix(sourceObject.transform, "SummonWindupWarning_40"), Is.Zero);
            }
            finally
            {
                Destroy(warningPrefab, sourceObject, root);
            }
        }

        [Test]
        [Category("Extended")]
        public void UtilityWindupBinding_ValidatesAndIsPersistentCompatible()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(UtilityWindupBindingPath);

            Assert.That(binding, Is.Not.Null, UtilityWindupBindingPath);
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(binding.DefaultLifetimeSeconds, Is.Zero);
            Assert.That(binding.TailSeconds, Is.EqualTo(0.30f).Within(0.001f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(8));
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_ResolvesUtilityWindup()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup), out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(cueMap.TryResolvePrefab(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup), out var prefab), Is.True);
            Assert.That(prefab, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void UtilityWindupPrefab_PassesVfxPrefabValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UtilityWindupPrefabPath);

            Assert.That(prefab, Is.Not.Null, UtilityWindupPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void UtilityWindupPlannerAndRuntimeSources_DoNotReferenceAuthorityTypes()
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

        private static GameplayVfxRequest PlanSingleUtilityWindupRequest(TickSummonWindupWarningSignal signal)
        {
            var plan = PlanUtilityWindupRequests(CreatePresentationData(summonWindupWarnings: new[] { signal }));

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static void AssertNoUtilityWindupRequests(TickPresentationData presentationData)
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup);
            var plan = PlanUtilityWindupRequests(presentationData);

            Assert.That(
                plan.Requests,
                Has.None.Matches<GameplayVfxRequest>(request => request.CueId == cueId));
        }

        private static GameplayVfxRequestPlan PlanUtilityWindupRequests(TickPresentationData presentationData)
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

        private static void AssertUtilityWindupRequest(
            in GameplayVfxRequest request,
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int effectIndex,
            int activationSequence)
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup);
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.SequenceId, Is.EqualTo(sourceEntityId));
            Assert.That(request.SourceEntityId, Is.EqualTo(sourceEntityId));
            Assert.That(request.PresentationSeed, Is.Not.Zero);
            Assert.That(request.CueId, Is.EqualTo(cueId));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
            Assert.That(request.Anchor.EntityId, Is.EqualTo(sourceEntityId));
            Assert.That(request.Anchor.HasFallbackCell, Is.True);
            Assert.That(request.Anchor.FallbackCell, Is.EqualTo(sourceCell));
            Assert.That(request.Anchor.FallbackTopology, Is.EqualTo(topology));
            Assert.That(request.PersistentKey, Is.EqualTo(new VfxPersistentKey(
                cueId,
                VfxAnchorKind.Entity,
                entityId: sourceEntityId,
                effectIndex: effectIndex,
                activationSequence: activationSequence)));
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickPresentationData presentationData,
            EnemyPresentationCatalog catalog = null,
            EnemyPresentationBinding[] bindings = null)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
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
            IReadOnlyList<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals = null)
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
                entityExitSignals: entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                summonWindupWarnings: summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>());
        }

        private static TickSummonWindupWarningSignal CreateSummonWindupWarningSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int effectIndex = 0,
            int activationSequence = 1,
            int windupStartTick = 10,
            int tickIndex = 12)
        {
            return new TickSummonWindupWarningSignal(
                sourceEntityId,
                effectIndex,
                sourceCell,
                topology,
                Direction.Right,
                windupStartTick,
                windupStartTick + 2,
                activationSequence,
                tickIndex,
                presentationSeed: tickIndex * 31 + sourceEntityId + effectIndex + activationSequence);
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
            IReadOnlyList<EntityState> finalEntities,
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
            var legacyWarningPrefab = new GameObject($"{name}_LegacyWarningPrefab");
            var enemyPrefabObject = new GameObject($"{name}_EnemyPrefab");
            var enemyPrefab = enemyPrefabObject.AddComponent<GameplayEntityView>();
            enemyPrefab.Initialize(40);
            enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
            enemyPrefabObject.AddComponent<EnemyAnimationTimingAuthoring>();
            var unitAuthoring = enemyPrefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
            SetField(unitAuthoring, "moveMotionDurationSeconds", UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel);
            var entityAuthoring = enemyPrefabObject.AddComponent<EntityMotionPresentationAuthoring>();
            SetField(entityAuthoring, "moveMotionDurationSeconds", EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);
            var utilityAuthoring = enemyPrefabObject.AddComponent<EnemyUtilityWindupPresentationAuthoring>();
            SetField(utilityAuthoring, "summonWindupWarningPrefab", legacyWarningPrefab);
            SetField(utilityAuthoring, "attachSummonWarningToSourceView", true);

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
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                topology,
                1f,
                CreateTimingProfile());

            return new CoordinatorScenario(root, presenter, enemyPrefabObject, legacyWarningPrefab, sourceCell, topology);
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

        private static VfxBindingDefinitionAsset CreateUtilityWindupBinding(
            GameObject prefab,
            float tailSeconds = 0.3f)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)EnemyVfxCue.UtilityWindup);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.Loop);
            SetField(binding, "stopPolicy", VfxStopPolicy.StopEmittingThenRelease);
            SetField(binding, "defaultLifetimeSeconds", 0f);
            SetField(binding, "tailSeconds", tailSeconds);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 8);
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
                GameObject legacyWarningPrefab,
                SurfaceCell sourceCell,
                CubeTopologyState topology)
            {
                Root = root;
                Presenter = presenter;
                EnemyPrefab = enemyPrefab;
                LegacyWarningPrefab = legacyWarningPrefab;
                SourceCell = sourceCell;
                Topology = topology;
            }

            public GameObject Root { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameObject EnemyPrefab { get; }

            public GameObject LegacyWarningPrefab { get; }

            public SurfaceCell SourceCell { get; }

            public CubeTopologyState Topology { get; }

            public void Destroy()
            {
                GameplayVfxUtilityWindupMigrationTests.Destroy(LegacyWarningPrefab, EnemyPrefab, Root);
            }
        }
    }
}
