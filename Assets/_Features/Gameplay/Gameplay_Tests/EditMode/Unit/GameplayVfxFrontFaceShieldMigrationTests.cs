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
    public sealed class GameplayVfxFrontFaceShieldMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string ActivePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldActiveVfx.prefab";
        private const string BlockPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldBlockVfx.prefab";
        private const string ActiveBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldActive_Binding.asset";
        private const string BlockBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldBlock_Binding.asset";
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
        public void ProductionRuntime_ShieldMigrationFlags_DefaultTrue()
        {
            var owner = new GameObject("FrontFaceShieldDefaultFlags");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxFrontFaceShieldActiveMigration, Is.True);
                Assert.That(runtime.EnableGameplayVfxFrontFaceShieldBlockMigration, Is.True);
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
        public void ProductionRuntime_ActiveFlagOnWithBinding_StartsAndStopsPersistentHandle()
        {
            var owner = new GameObject("FrontFaceShieldActivePersistent");
            var prefab = new GameObject("FrontFaceShieldActivePersistentPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateActiveBinding(prefab, tailSeconds: 0f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFrontFaceShieldActiveMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    activeSignals: new[]
                    {
                        CreateSourceSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                runtime.Present(CreateExtensionContext(CreatePresentationData()));
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
        public void ProductionRuntime_BlockFlagOnWithBinding_SpawnsOneTransient()
        {
            var owner = new GameObject("FrontFaceShieldBlockTransient");
            var prefab = new GameObject("FrontFaceShieldBlockTransientPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBlockBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFrontFaceShieldBlockMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    blockSignals: new[]
                    {
                        CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
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
                activeRuntime.EnableGameplayVfxFrontFaceShieldActiveMigration = true;
                activeRuntime.Present(CreateExtensionContext(CreatePresentationData(
                    activeSignals: new[]
                    {
                        CreateSourceSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                var blockRuntime = blockOwner.AddComponent<GameplayVfxProductionRuntime>();
                blockRuntime.EnableGameplayVfxFrontFaceShieldBlockMigration = true;
                blockRuntime.Present(CreateExtensionContext(CreatePresentationData(
                    blockSignals: new[]
                    {
                        CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                Assert.That(activeRuntime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(activeRuntime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(blockRuntime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(blockRuntime.MissingBindingCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(activeOwner, blockOwner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_ActiveFlagOn_CleansLegacyActiveAndDoesNotRecreate()
        {
            var root = new GameObject("FrontFaceShieldLegacyActiveCleanup");
            var activePrefab = new GameObject("FrontFaceShieldLegacyActiveCleanup_LegacyActivePrefab");
            var sourceObject = new GameObject("FrontFaceShieldLegacyActiveCleanup_Source");
            try
            {
                var sourceView = sourceObject.AddComponent<GameplayEntityView>();
                sourceView.Initialize(40);
                var shieldAuthoring = sourceObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                SetField(shieldAuthoring, "activeLoopPrefab", activePrefab);
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
                Assert.That(CountDescendantsByNamePrefix(sourceObject.transform, "FrontFaceShieldActiveLoop_40"), Is.EqualTo(1));

                presenter.RefreshActiveSources(
                    Array.Empty<TickFrontFaceShieldSourceSignal>(),
                    stateStore,
                    projector);

                Assert.That(CountDescendantsByNamePrefix(sourceObject.transform, "FrontFaceShieldActiveLoop_40"), Is.Zero);
            }
            finally
            {
                Destroy(activePrefab, sourceObject, root);
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
                runtime.EnableGameplayVfxFrontFaceShieldBlockMigration = true;
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
                runtime.EnableGameplayVfxFrontFaceShieldActiveMigration = false;
                runtime.EnableGameplayVfxFrontFaceShieldBlockMigration = false;
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
        public void ProductionRuntime_SourceProfile_OverridesHostDefault()
        {
            var owner = new GameObject("FrontFaceShieldSourceProfileOverride");
            var sourcePrefab = new GameObject("FrontFaceShieldSourceProfilePrefab");
            var hostPrefab = new GameObject("FrontFaceShieldHostPrefab");
            VfxBindingDefinitionAsset sourceBinding = null;
            VfxBindingDefinitionAsset hostBinding = null;
            VfxProfileAsset sourceProfile = null;
            VfxCueMapAsset cueMap = null;
            EnemyPresentationCatalog catalog = null;
            try
            {
                sourceBinding = CreateActiveBinding(sourcePrefab);
                hostBinding = CreateActiveBinding(hostPrefab);
                sourceProfile = CreateEnemyProfile(sourceBinding);
                cueMap = CreateCueMap(hostBinding);
                catalog = CreateEnemyPresentationCatalog("front-face-shield-source", sourceProfile);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFrontFaceShieldActiveMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(activeSignals: new[]
                    {
                        CreateSourceSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    }),
                    catalog,
                    new[] { new EnemyPresentationBinding { EntityId = 40, PresentationId = "front-face-shield-source" } }));

                var persistentRoot = owner.transform.Find("GameplayVfxRuntimeRoot/Persistent");
                Assert.That(persistentRoot, Is.Not.Null);
                Assert.That(persistentRoot.childCount, Is.EqualTo(1));
                Assert.That(persistentRoot.GetChild(0).name, Does.StartWith(sourcePrefab.name));
            }
            finally
            {
                Destroy(catalog, cueMap, sourceProfile, hostBinding, sourceBinding, hostPrefab, sourcePrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ShieldBindings_ValidateAndHostDefaultMapResolves()
        {
            var activeBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ActiveBindingPath);
            var blockBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(BlockBindingPath);
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(activeBinding, Is.Not.Null, ActiveBindingPath);
            Assert.That(blockBinding, Is.Not.Null, BlockBindingPath);
            Assert.That(activeBinding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(blockBinding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(activeBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive)));
            Assert.That(activeBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(activeBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(blockBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock)));
            Assert.That(blockBinding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(blockBinding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive), out _), Is.True);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock), out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void ShieldPrefabs_PassVfxPrefabValidation()
        {
            AssertPrefabValid(ActivePrefabPath);
            AssertPrefabValid(BlockPrefabPath);
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
            var owner = new GameObject($"FrontFaceShieldFlags_{activeEnabled}_{blockEnabled}");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFrontFaceShieldActiveMigration = activeEnabled;
                runtime.EnableGameplayVfxFrontFaceShieldBlockMigration = blockEnabled;
                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    activeSignals: new[]
                    {
                        CreateSourceSignal(40, new SurfaceCell(FaceId.Floor, 0, 0), new CubeTopologyState(FaceId.Floor)),
                    },
                    blockSignals: new[]
                    {
                        CreateBlockSignal(40, 20, 10, new SurfaceCell(FaceId.Floor, 1, 0), new CubeTopologyState(FaceId.Floor)),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(expectedRequests));
            }
            finally
            {
                Destroy(owner);
            }
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
            TickFrontFaceShieldSourceSignal[] activeSignals = null,
            TickFrontFaceShieldBlockSignal[] blockSignals = null,
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
                frontFaceShieldBlocks: blockSignals ?? Array.Empty<TickFrontFaceShieldBlockSignal>());
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
            var activePrefab = new GameObject($"{name}_LegacyActivePrefab");
            var blockPrefab = new GameObject($"{name}_LegacyBlockPrefab");
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
            SetField(shieldAuthoring, "activeLoopPrefab", activePrefab);
            SetField(shieldAuthoring, "blockBurstPrefab", blockPrefab);
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

            return new CoordinatorScenario(root, presenter, enemyPrefabObject, activePrefab, blockPrefab, sourceCell, topology);
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

        private static VfxBindingDefinitionAsset CreateBinding(
            GameObject prefab,
            EnemyVfxCue cue,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            float lifetimeSeconds,
            float tailSeconds,
            int maxConcurrent)
        {
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
                GameObject activePrefab,
                GameObject blockPrefab,
                SurfaceCell sourceCell,
                CubeTopologyState topology)
            {
                Root = root;
                Presenter = presenter;
                EnemyPrefab = enemyPrefab;
                ActivePrefab = activePrefab;
                BlockPrefab = blockPrefab;
                SourceCell = sourceCell;
                Topology = topology;
            }

            public GameObject Root { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameObject EnemyPrefab { get; }

            public GameObject ActivePrefab { get; }

            public GameObject BlockPrefab { get; }

            public SurfaceCell SourceCell { get; }

            public CubeTopologyState Topology { get; }

            public void Destroy()
            {
                GameplayVfxFrontFaceShieldMigrationTests.Destroy(BlockPrefab, ActivePrefab, EnemyPrefab, Root);
            }
        }
    }
}
