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
    public sealed class GameplayVfxEnemyJumpTargetTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string JumperJumpStartBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/JumperJumpStart_Binding.asset";
        private const string CombinedGameplayShowcaseScenePath =
            "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string GameplayVfxProductionRuntimeScriptGuid = "77f98ca183bf441ba81f70f521126c17";
        private const string GameplayVfxRuntimeInstallerScriptGuid = "d35824c2bc2045a4b5fc027c8f7561a4";
        private const string HostDefaultCueMapGuid = "3ed23d03c1c440cb9a1441a4b18c46e5";

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_StartedWindupThisTick_EmitsJumperLandingTargetRequest()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);

            var request = PlanSingleRequest(CreateJumpSignal(targetCell, startedWindup: true), topology);

            AssertJumperLandingTargetRequest(request, targetCell, topology);
        }

        [Test]
        [Category("Extended")]
        public void EnemyVfxRequestPlanner_JumperLandingTarget_SetsSourceEntityId()
        {
            var targetCell = new SurfaceCell(FaceId.Back, 2, 1);
            var topology = new CubeTopologyState(FaceId.Floor);

            var request = PlanSingleRequest(
                CreateJumpSignal(targetCell, startedWindup: true, entityId: 123),
                topology);

            Assert.That(request.SourceEntityId, Is.EqualTo(123));
            Assert.That(request.PresentationSeed, Is.EqualTo(123));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(targetCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(request.PersistentKey.AnchorKind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.PersistentKey.EntityId, Is.EqualTo(123));
            Assert.That(request.PersistentKey.Cell, Is.EqualTo(targetCell));
            Assert.That(request.PersistentKey.HasCell, Is.True);
            Assert.That(request.PersistentKey.ActivationSequence, Is.EqualTo(3));
            Assert.That(request.TopologyStopMode, Is.EqualTo(GameplayVfxTopologyStopMode.TopologyHelperExempt));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_WindupStartedOutcome_EmitsJumperLandingTargetRequest()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 1, 0);
            var topology = new CubeTopologyState(FaceId.Floor);

            var request = PlanSingleRequest(
                CreateJumpSignal(
                    targetCell,
                    outcome: TickEnemyJumpPresentationOutcome.WindupStarted),
                topology);

            AssertJumperLandingTargetRequest(request, targetCell, topology);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_AirborneSignal_ContinuesLandingTarget()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var topology = new CubeTopologyState(FaceId.Floor);

            var request = PlanSingleRequest(
                CreateJumpSignal(
                    targetCell,
                    phase: EnemyJumpPhase.Airborne),
                topology);

            AssertJumperLandingTargetRequest(request, targetCell, topology);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_AirborneStarted_EmitsJumperJumpStartRequest()
        {
            var request = PlanRequestForCue(
                EnemyVfxCue.JumperJumpStart,
                CreateJumpSignal(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    startedAirborne: true,
                    phase: EnemyJumpPhase.Airborne));

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperJumpStart)));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_LandedSignal_DoesNotEmitLandingTarget()
        {
            AssertNoCueRequests(
                EnemyVfxCue.JumperLandingTarget,
                CreateJumpSignal(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    landed: true,
                    phase: EnemyJumpPhase.Cooldown));
        }

        [Test]
        [Category("Full")]
        public void EnemyPlanner_RetrySignal_ContinuesLandingTarget()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var request = PlanSingleRequest(CreateJumpSignal(
                targetCell,
                retry: true,
                phase: EnemyJumpPhase.Airborne));

            AssertJumperLandingTargetRequest(request, targetCell, new CubeTopologyState(FaceId.Floor));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_MultipleNonActiveJumpSignals_DoNotEmitLandingTargetFalsePositive()
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(
                        CreateJumpSignal(targetCell, landed: true, phase: EnemyJumpPhase.Cooldown),
                        CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Cooldown),
                        CreateJumpSignal(targetCell, phase: EnemyJumpPhase.None)),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(
                builder.Build().Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_PresentationTargetSurfaceCellFace_IsPreserved()
        {
            var targetCell = new SurfaceCell(FaceId.Back, 2, 1);

            var request = PlanSingleRequest(CreateJumpSignal(targetCell, startedWindup: true));

            Assert.That(request.Anchor.Cell.face, Is.EqualTo(targetCell.face));
            Assert.That(request.Anchor.Cell.x, Is.EqualTo(targetCell.x));
            Assert.That(request.Anchor.Cell.y, Is.EqualTo(targetCell.y));
            Assert.That(request.Anchor.Cell, Is.EqualTo(targetCell));
            Assert.That(request.Anchor.Cell, Is.Not.EqualTo(new SurfaceCell(FaceId.Floor, targetCell.x, targetCell.y)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FeatureFlag_DefaultsTrue()
        {
            var owner = new GameObject("VfxRuntimeDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableEnemyJumpTargetVfx, Is.True);
                Assert.That(runtime.EnableEnemyJumpLandingDustVfx, Is.True);
                Assert.That(runtime.IsRuntimeInitialized, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOff_DoesNotInitializeOrPlan()
        {
            var owner = new GameObject("VfxRuntimeFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = false;
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                runtime.Present(context);

                Assert.That(runtime.IsRuntimeInitialized, Is.False);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_MissingBinding_SafelySkipsAfterPlanning()
        {
            var owner = new GameObject("VfxRuntimeMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                runtime.Present(context);

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOnWithBinding_DoesNotCreateSnapshots()
        {
            var owner = new GameObject("VfxRuntimeSnapshotGuard");
            var prefab = new GameObject("JumperLandingTargetPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                SnapshotMaterializationCounts counts;
                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    runtime.Present(context);
                    counts = capture.Counts;
                }

                Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOnWithBinding_PlaysOnePersistentInstance()
        {
            var owner = new GameObject("VfxRuntimeEnabled");
            var prefab = new GameObject("JumperLandingTargetPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                runtime.Present(context);

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxShowsWhenOwnerNotSuspended()
        {
            var owner = new GameObject(nameof(EnemyJumpWindupDangerVfxShowsWhenOwnerNotSuspended));
            var prefab = CreateRuntimeMarkerPrefab("JumperLandingTargetVisiblePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true)));

                var marker = AssertSinglePersistentMarker(owner);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(AnyRendererEnabled(marker), Is.True);
                Assert.That(AnyParticlePaused(marker), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxInitialSpawnNotBlockedByMissingSemanticState()
        {
            var owner = new GameObject(nameof(EnemyJumpWindupDangerVfxInitialSpawnNotBlockedByMissingSemanticState));
            var prefab = CreateRuntimeMarkerPrefab("JumperLandingTargetSemanticPendingPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(AssertSinglePersistentMarker(owner), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxRequestStillEmittedWhileSuspended()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var request = PlanSingleRequest(
                CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Airborne),
                new CubeTopologyState(FaceId.Front));

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey.EntityId, Is.EqualTo(40));
            Assert.That(request.PersistentKey.Cell, Is.EqualTo(targetCell));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxPersistentHandleSurvivesSuspend()
        {
            var owner = new GameObject(nameof(EnemyJumpWindupDangerVfxPersistentHandleSurvivesSuspend));
            var prefab = CreateRuntimeMarkerPrefab("JumperLandingTargetSurvivePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var targetCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stateStore = CreateOwnerStateStore(owner, 40);
                var projector = CreateProjector();

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, startedWindup: true),
                    new CubeTopologyState(FaceId.Floor),
                    stateStore,
                    projector));
                var marker = AssertSinglePersistentMarker(owner);
                var markerInstanceId = marker.GetInstanceID();

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Airborne),
                    new CubeTopologyState(FaceId.Front),
                    stateStore,
                    projector));

                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(AssertSinglePersistentMarker(owner).GetInstanceID(), Is.EqualTo(markerInstanceId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxRendererOnlyHiddenDuringSuspend()
        {
            var owner = new GameObject(nameof(EnemyJumpWindupDangerVfxRendererOnlyHiddenDuringSuspend));
            var prefab = CreateRuntimeMarkerPrefab("JumperLandingTargetHiddenPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var targetCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stateStore = CreateOwnerStateStore(owner, 40);
                var projector = CreateProjector();

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, startedWindup: true),
                    new CubeTopologyState(FaceId.Floor),
                    stateStore,
                    projector));
                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Airborne),
                    new CubeTopologyState(FaceId.Front),
                    stateStore,
                    projector));

                var marker = AssertSinglePersistentMarker(owner);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(AnyRendererEnabled(marker), Is.False);
                Assert.That(AnyParticlePaused(marker), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxRestoresVisibleOnResume()
        {
            var owner = new GameObject(nameof(EnemyJumpWindupDangerVfxRestoresVisibleOnResume));
            var prefab = CreateRuntimeMarkerPrefab("JumperLandingTargetResumePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var targetCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stateStore = CreateOwnerStateStore(owner, 40);
                var projector = CreateProjector();

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, startedWindup: true),
                    new CubeTopologyState(FaceId.Floor),
                    stateStore,
                    projector));
                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Airborne),
                    new CubeTopologyState(FaceId.Front),
                    stateStore,
                    projector));
                var suspendedMarker = AssertSinglePersistentMarker(owner);
                var markerInstanceId = suspendedMarker.GetInstanceID();

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Airborne),
                    new CubeTopologyState(FaceId.Floor),
                    stateStore,
                    projector));

                var marker = AssertSinglePersistentMarker(owner);
                Assert.That(marker.GetInstanceID(), Is.EqualTo(markerInstanceId));
                Assert.That(AnyRendererEnabled(marker), Is.True);
                Assert.That(AnyParticlePaused(marker), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxProductionRuntimeFinalVisibility()
        {
            var owner = new GameObject(nameof(EnemyJumpWindupDangerVfxProductionRuntimeFinalVisibility));
            var prefab = CreateRuntimeMarkerPrefab("JumperLandingTargetFinalVisibilityPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true)));

                var marker = AssertSinglePersistentMarker(owner);
                var renderer = marker.GetComponentInChildren<Renderer>(includeInactive: true);
                var particle = marker.GetComponentInChildren<ParticleSystem>(includeInactive: true);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(particle, Is.Not.Null);
                Assert.That(particle.isPaused, Is.False);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_LandedSignal_StopsPersistentLandingTarget()
        {
            var owner = new GameObject("VfxRuntimeLandingStop");
            var prefab = new GameObject("JumperLandingTargetPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                runtime.EnableEnemyJumpLandingDustVfx = false;
                runtime.ConfigureHostDefaultMap(cueMap);
                var targetCell = new SurfaceCell(FaceId.Floor, 0, 0);

                runtime.Present(CreateExtensionContext(CreateJumpSignal(targetCell, startedWindup: true)));
                var persistentRoot = owner.transform.Find("GameplayVfxRuntimeRoot/Persistent");
                Assert.That(persistentRoot, Is.Not.Null);
                Assert.That(persistentRoot.childCount, Is.EqualTo(1));

                runtime.Present(CreateExtensionContext(CreateJumpSignal(
                    targetCell,
                    landed: true,
                    phase: EnemyJumpPhase.Cooldown)));
                runtime.UpdatePresentation(0f);

                Assert.That(persistentRoot.childCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlagOnWithActualBinding_PlaysMarkerOnTargetCell()
        {
            var owner = new GameObject("VfxRuntimeActualBinding");
            var targetCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            try
            {
                var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);
                Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var context = CreateExtensionContext(CreateJumpSignal(targetCell, startedWindup: true));

                runtime.Present(context);

                var persistentRoot = owner.transform.Find("GameplayVfxRuntimeRoot/Persistent");
                Assert.That(persistentRoot, Is.Not.Null);
                Assert.That(persistentRoot.childCount, Is.EqualTo(1));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                var marker = persistentRoot.GetChild(0);
                var projector = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    1f);
                Assert.That(projector.TryProjectSurfaceCell(targetCell, topology, out var targetPose), Is.True);
                Assert.That(
                    projector.TryProjectSurfaceCell(new SurfaceCell(FaceId.Floor, 2, 0), topology, out var sourcePose),
                    Is.True);
                var expectedMarkerPosition = targetPose.LocalPosition -
                                             (targetPose.Normal * projector.SurfaceTileThickness);
                var expectedMarkerRotation = targetPose.LocalRotation * Quaternion.Euler(180f, 0f, 0f);
                Assert.That(Vector3.Distance(marker.localPosition, expectedMarkerPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(marker.localPosition, sourcePose.LocalPosition), Is.GreaterThan(0.1f));
                Assert.That(Quaternion.Angle(marker.localRotation, expectedMarkerRotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeInstaller_Install_ConfiguresSameRootProductionRuntime_WithHostDefaultMap()
        {
            var owner = new GameObject("VfxRuntimeInstallerOwner");
            var prefab = new GameObject("JumperLandingTargetPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                var installer = owner.AddComponent<GameplayVfxRuntimeInstaller>();
                SetField(installer, "hostDefaultCueMap", cueMap);
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                installer.Install();
                runtime.Present(context);

                Assert.That(installer.HostDefaultCueMap, Is.SameAs(cueMap));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeInstaller_Install_Throws_WhenSameRootProductionRuntimeMissing()
        {
            var owner = new GameObject("VfxRuntimeInstallerMissingRuntime");
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                {
                    var installer = owner.AddComponent<GameplayVfxRuntimeInstaller>();
                    installer.Install();
                });

                Assert.That(
                    exception.Message,
                    Is.EqualTo(
                        "GameplayVfxRuntimeInstaller requires a co-located GameplayVfxProductionRuntime on the canonical host root."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeInstaller_Install_DoesNotUseSceneGlobalProductionRuntimeFallback()
        {
            var otherRoot = new GameObject("VfxRuntimeInstallerOtherRuntime");
            var owner = new GameObject("VfxRuntimeInstallerNoGlobalFallback");
            try
            {
                otherRoot.AddComponent<GameplayVfxProductionRuntime>();
                var exception = Assert.Throws<InvalidOperationException>(() =>
                {
                    var installer = owner.AddComponent<GameplayVfxRuntimeInstaller>();
                    installer.Install();
                });

                Assert.That(
                    exception.Message,
                    Is.EqualTo(
                        "GameplayVfxRuntimeInstaller requires a co-located GameplayVfxProductionRuntime on the canonical host root."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        [Category("Extended")]
        public void JumperJumpStartBinding_ValidatesAndHostDefaultMapResolves()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(JumperJumpStartBindingPath);
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(binding, Is.Not.Null, JumperJumpStartBindingPath);
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperJumpStart)));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.JumperJumpStart), out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
        }

        [Test]
        [Category("Extended")]
        public void CombinedGameplayShowcase_WiresJumperLandingTargetVfxRuntime()
        {
            var sceneText = File.ReadAllText(CombinedGameplayShowcaseScenePath);
            var productionRuntimeBlock = ReadSceneComponentBlock(
                sceneText,
                "Game.Feature.Gameplay.Vfx.Host.GameplayVfxProductionRuntime");
            var runtimeInstallerBlock = ReadSceneComponentBlock(
                sceneText,
                "Game.Feature.Gameplay.Vfx.Host.GameplayVfxRuntimeInstaller");

            Assert.That(
                sceneText,
                Does.Contain($"m_Script: {{fileID: 11500000, guid: {GameplayVfxProductionRuntimeScriptGuid}, type: 3}}"));
            Assert.That(
                sceneText,
                Does.Contain($"m_Script: {{fileID: 11500000, guid: {GameplayVfxRuntimeInstallerScriptGuid}, type: 3}}"));
            Assert.That(productionRuntimeBlock, Does.Contain("enableEnemyJumpTargetVfx: 1"));
            Assert.That(productionRuntimeBlock, Does.Contain("enableEnemyJumpLandingDustVfx: 1"));
            Assert.That(productionRuntimeBlock, Does.Not.Contain("hostDefaultCueMap:"));
            Assert.That(
                runtimeInstallerBlock,
                Does.Contain($"hostDefaultCueMap: {{fileID: 11400000, guid: {HostDefaultCueMapGuid}, type: 2}}"));
        }

        private static GameplayVfxRequest PlanSingleRequest(
            TickEnemyJumpPresentationSignal jumpSignal,
            CubeTopologyState topology = default)
        {
            var resolvedTopology = topology.Equals(default(CubeTopologyState))
                ? new CubeTopologyState(FaceId.Floor)
                : topology;
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(jumpSignal),
                    resolvedTopology),
                builder);

            var plan = builder.Build();
            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayVfxRequest PlanRequestForCue(
            EnemyVfxCue cue,
            TickEnemyJumpPresentationSignal jumpSignal,
            CubeTopologyState topology = default)
        {
            var resolvedTopology = topology.Equals(default(CubeTopologyState))
                ? new CubeTopologyState(FaceId.Floor)
                : topology;
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cueId = GameplayVfxCueId.From(cue);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(jumpSignal),
                    resolvedTopology),
                builder);

            var plan = builder.Build();
            Assert.That(
                plan.Requests,
                Has.Exactly(1).Matches<GameplayVfxRequest>(request => request.CueId == cueId));
            for (var i = 0; i < plan.Requests.Count; i++)
            {
                if (plan.Requests[i].CueId == cueId)
                {
                    return plan.Requests[i];
                }
            }

            Assert.Fail($"Missing request for cue '{cueId}'.");
            return default;
        }

        private static void AssertNoRequests(params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(jumpSignals),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(builder.Build(), Is.SameAs(GameplayVfxRequestPlan.Empty));
        }

        private static void AssertNoCueRequests(
            EnemyVfxCue cue,
            params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cueId = GameplayVfxCueId.From(cue);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(jumpSignals),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(
                builder.Build().Requests,
                Has.None.Matches<GameplayVfxRequest>(request => request.CueId == cueId));
        }

        private static void AssertJumperLandingTargetRequest(
            in GameplayVfxRequest request,
            SurfaceCell targetCell,
            CubeTopologyState topology)
        {
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PresentationSeed, Is.EqualTo(40));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(targetCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
            Assert.That(request.PersistentKey.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(request.PersistentKey.AnchorKind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.PersistentKey.EntityId, Is.EqualTo(40));
            Assert.That(request.PersistentKey.Cell, Is.EqualTo(targetCell));
            Assert.That(request.PersistentKey.HasCell, Is.True);
            Assert.That(request.PersistentKey.ActivationSequence, Is.EqualTo(3));
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickEnemyJumpPresentationSignal jumpSignal)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            return CreateExtensionContext(jumpSignal, topology);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickEnemyJumpPresentationSignal jumpSignal,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore = null)
        {
            return CreateExtensionContext(jumpSignal, topology, stateStore, CreateProjector());
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickEnemyJumpPresentationSignal jumpSignal,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (stateStore == null)
            {
                stateStore = new GameplayPresentationStateStore();
                stateStore.ResetSession(topology);
            }
            else
            {
                stateStore.CommittedTopology = topology;
            }

            return new GameplayTickPresentationExtensionContext(
                CreateResult(CreatePresentationData(jumpSignal), topology),
                topology,
                stateStore,
                projector);
        }

        private static GameplayCubeProjector CreateProjector()
        {
            return new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
        }

        private static GameplayPresentationStateStore CreateOwnerStateStore(
            GameObject owner,
            int entityId)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            var viewObject = new GameObject($"EnemyView_{entityId}");
            viewObject.transform.SetParent(owner.transform, worldPositionStays: false);
            var view = viewObject.AddComponent<GameplayEntityView>();
            stateStore.ViewsByEntityId[entityId] = view;
            stateStore.EnemyVisualSemanticStatesByEntityId[entityId] =
                new EnemyVisualSemanticState(EnemyVisualActivityState.Normal);
            return stateStore;
        }

        private static GameObject CreateRuntimeMarkerPrefab(string name)
        {
            var prefab = new GameObject(name);
            var modelRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            modelRoot.name = "ModelRoot";
            var collider = modelRoot.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            modelRoot.transform.SetParent(prefab.transform, worldPositionStays: false);
            modelRoot.AddComponent<ParticleSystem>();
            return prefab;
        }

        private static Transform AssertSinglePersistentMarker(GameObject owner)
        {
            var persistentRoot = owner.transform.Find("GameplayVfxRuntimeRoot/Persistent");
            Assert.That(persistentRoot, Is.Not.Null);
            Assert.That(persistentRoot.childCount, Is.EqualTo(1));
            return persistentRoot.GetChild(0);
        }

        private static bool AnyRendererEnabled(Transform marker)
        {
            var renderers = marker.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AnyParticlePaused(Transform marker)
        {
            var particles = marker.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null && particles[i].isPaused)
                {
                    return true;
                }
            }

            return false;
        }

        private static TickResult CreateResult(TickPresentationData presentationData, CubeTopologyState topology)
        {
            return new TickResult(
                12,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                jumpSignals,
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickEnemyJumpPresentationSignal CreateJumpSignal(
            SurfaceCell targetCell,
            bool startedWindup = false,
            bool startedAirborne = false,
            bool landed = false,
            bool retry = false,
            EnemyJumpPhase phase = EnemyJumpPhase.Windup,
            TickEnemyJumpPresentationOutcome outcome = TickEnemyJumpPresentationOutcome.None,
            int entityId = 40)
        {
            return new TickEnemyJumpPresentationSignal(
                entityId: entityId,
                sequence: 3,
                phase: phase,
                startedWindupThisTick: startedWindup,
                startedAirborneThisTick: startedAirborne,
                landedThisTick: landed,
                retryThisTick: retry,
                sourceCell: new SurfaceCell(FaceId.Floor, 2, 0),
                lockedTargetCell: targetCell,
                presentationTargetCell: targetCell,
                facing: Direction.Right,
                landingTick: 15,
                outcome: outcome);
        }

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab)
        {
            EnsureModelRoot(prefab);
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)EnemyVfxCue.JumperLandingTarget);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.Optional);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.SkipOptional);
            SetField(binding, "playbackMode", VfxPlaybackMode.Loop);
            SetField(binding, "stopPolicy", VfxStopPolicy.StopEmittingThenRelease);
            SetField(binding, "defaultLifetimeSeconds", 0f);
            SetField(binding, "tailSeconds", 0f);
            return binding;
        }

        private static void EnsureModelRoot(GameObject prefab)
        {
            if (prefab != null && prefab.transform.Find("ModelRoot") == null)
            {
                new GameObject("ModelRoot").transform.SetParent(prefab.transform, worldPositionStays: false);
            }
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

        private static string ReadSceneComponentBlock(string sceneText, string marker)
        {
            var markerIndex = sceneText.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0), $"Missing scene marker '{marker}'.");

            var blockStart = sceneText.LastIndexOf("--- !u!114", markerIndex, StringComparison.Ordinal);
            Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), $"Missing MonoBehaviour block for '{marker}'.");

            var blockEnd = sceneText.IndexOf("--- !u!", markerIndex, StringComparison.Ordinal);
            return blockEnd >= 0
                ? sceneText.Substring(blockStart, blockEnd - blockStart)
                : sceneText.Substring(blockStart);
        }
    }
}
