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
    public sealed class GameplayVfxEnemyJumpLandingDustTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string JumperLandingDustPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/JumperLandingDustVfx.prefab";
        private const string JumperLandingDustBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/JumperLandingDust_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_LandedSignal_EmitsJumperLandingDustRequest()
        {
            var landingCell = new SurfaceCell(FaceId.Back, 2, 1);
            var topology = new CubeTopologyState(FaceId.Floor);

            var request = PlanSingleRequest(
                CreateJumpSignal(landingCell, landed: true, phase: EnemyJumpPhase.Cooldown),
                topology);

            AssertJumperLandingDustRequest(request, landingCell, topology);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_CrushedBoxAndLandedOutcome_EmitsJumperLandingDustRequest()
        {
            var landingCell = new SurfaceCell(FaceId.Front, 1, 2);
            var topology = new CubeTopologyState(FaceId.Floor);

            var request = PlanSingleRequest(
                CreateJumpSignal(
                    landingCell,
                    phase: EnemyJumpPhase.Cooldown,
                    outcome: TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded),
                topology);

            AssertJumperLandingDustRequest(request, landingCell, topology);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_NonLandingSignals_DoNotEmitJumperLandingDust()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);

            AssertNoDustRequests(CreateJumpSignal(targetCell, startedWindup: true));
            AssertNoDustRequests(CreateJumpSignal(targetCell, startedAirborne: true, phase: EnemyJumpPhase.Airborne));
            AssertNoDustRequests(CreateJumpSignal(targetCell, retry: true, phase: EnemyJumpPhase.Airborne));
            AssertNoDustRequests(CreateJumpSignal(targetCell, phase: EnemyJumpPhase.Airborne));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_LandingDust_PreservesSurfaceCellFace()
        {
            var landingCell = new SurfaceCell(FaceId.Back, 2, 1);

            var request = PlanSingleRequest(CreateJumpSignal(
                landingCell,
                landed: true,
                phase: EnemyJumpPhase.Cooldown));

            Assert.That(request.Anchor.Cell.face, Is.EqualTo(landingCell.face));
            Assert.That(request.Anchor.Cell.x, Is.EqualTo(landingCell.x));
            Assert.That(request.Anchor.Cell.y, Is.EqualTo(landingCell.y));
            Assert.That(request.Anchor.Cell, Is.Not.EqualTo(new SurfaceCell(FaceId.Floor, landingCell.x, landingCell.y)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_LandingDustFlag_DefaultsFalse()
        {
            var owner = new GameObject("LandingDustDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableEnemyJumpLandingDustVfx, Is.False);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_LandingDustFlagOff_DoesNotInitializeOrPlan()
        {
            var owner = new GameObject("LandingDustFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                var context = CreateExtensionContext(CreateJumpSignal(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    landed: true,
                    phase: EnemyJumpPhase.Cooldown));

                runtime.Present(context);

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
        public void ProductionRuntime_LandingDustMissingBinding_SafelySkipsAfterPlanning()
        {
            var owner = new GameObject("LandingDustMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpLandingDustVfx = true;
                var context = CreateExtensionContext(CreateJumpSignal(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    landed: true,
                    phase: EnemyJumpPhase.Cooldown));

                runtime.Present(context);

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
        public void ProductionRuntime_LandingDustFlagOnWithBinding_PlaysOneTransientInstance()
        {
            var owner = new GameObject("LandingDustEnabled");
            var prefab = new GameObject("JumperLandingDustPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, EnemyVfxCue.JumperLandingDust);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpLandingDustVfx = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var context = CreateExtensionContext(CreateJumpSignal(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    landed: true,
                    phase: EnemyJumpPhase.Cooldown));

                runtime.Present(context);

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
        public void ProductionRuntime_TargetAndDustFlags_AreIndependent()
        {
            var windupSignal = CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true);
            var landedSignal = CreateJumpSignal(
                new SurfaceCell(FaceId.Floor, 1, 1),
                landed: true,
                phase: EnemyJumpPhase.Cooldown,
                entityId: 41);

            AssertFlagCombinationPlans(
                enableTarget: true,
                enableDust: false,
                expectedRequests: 1,
                windupSignal,
                landedSignal);
            AssertFlagCombinationPlans(
                enableTarget: false,
                enableDust: true,
                expectedRequests: 1,
                windupSignal,
                landedSignal);
            AssertFlagCombinationPlans(
                enableTarget: true,
                enableDust: true,
                expectedRequests: 2,
                windupSignal,
                landedSignal);
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_LandingDustFlagOnWithBinding_DoesNotCreateSnapshots()
        {
            var owner = new GameObject("LandingDustSnapshotGuard");
            var prefab = new GameObject("JumperLandingDustPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, EnemyVfxCue.JumperLandingDust);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpLandingDustVfx = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var context = CreateExtensionContext(CreateJumpSignal(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    landed: true,
                    phase: EnemyJumpPhase.Cooldown));

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
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void JumperLandingDustPrefab_PassesVfxPrefabValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JumperLandingDustPrefabPath);

            Assert.That(prefab, Is.Not.Null, JumperLandingDustPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void JumperLandingDustBinding_ValidatesAndUsesOneShotAuthoredDuration()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(JumperLandingDustBindingPath);

            Assert.That(binding, Is.Not.Null, JumperLandingDustBindingPath);
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.DefaultLifetimeSeconds, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.30f).Within(0.001f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(8));
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_ResolvesJumperLandingDust()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(
                    GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust),
                    out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(8));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileDustBindingBeatsHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust);
            var sourcePolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast, maxConcurrentInstances: 7);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic, maxConcurrentInstances: 3);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 10,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { sourcePolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(sourcePolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileMissingDustFallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic, maxConcurrentInstances: 3);
            var profileOnlyPolicy = CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget));
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 10,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { profileOnlyPolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_MissingSourceIdUsesHostDefaultForDust()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic);
            var sourcePolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast);
            var provider = new FakeProfileProvider(
                sourceEntityId: 10,
                profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { sourcePolicy }));
            var resolver = new ProfileAwareVfxBindingResolver(provider, new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 0), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
            Assert.That(provider.CallCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ExistingPresenterSources_DoNotReferenceJumperLandingDust()
        {
            var presenterPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectPresenter.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayUtilityWindupVfxPresenter.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs",
            };

            foreach (var path in presenterPaths)
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain("JumperLandingDust"), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void DustPlannerAndRuntimeSources_DoNotReferenceAuthorityTypes()
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

        private static void AssertNoDustRequests(params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust);

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

        private static void AssertJumperLandingDustRequest(
            in GameplayVfxRequest request,
            SurfaceCell landingCell,
            CubeTopologyState topology)
        {
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PersistentKey, Is.EqualTo(default(VfxPersistentKey)));
            Assert.That(request.PresentationSeed, Is.EqualTo(40));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(landingCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
        }

        private static void AssertFlagCombinationPlans(
            bool enableTarget,
            bool enableDust,
            int expectedRequests,
            params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            var owner = new GameObject("LandingDustFlagCombination");
            var prefab = new GameObject("LandingDustFlagCombinationPrefab");
            VfxBindingDefinitionAsset targetBinding = null;
            VfxBindingDefinitionAsset dustBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                targetBinding = CreateBinding(prefab, EnemyVfxCue.JumperLandingTarget);
                dustBinding = CreateBinding(prefab, EnemyVfxCue.JumperLandingDust);
                cueMap = CreateCueMap(targetBinding, dustBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = enableTarget;
                runtime.EnableEnemyJumpLandingDustVfx = enableDust;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(jumpSignals));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(expectedRequests));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(expectedRequests));
            }
            finally
            {
                Destroy(cueMap, dustBinding, targetBinding, prefab, owner);
            }
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            return new GameplayTickPresentationExtensionContext(
                CreateResult(CreatePresentationData(jumpSignals), topology),
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

        private static GameplayVfxRequest CreateRequest(GameplayVfxCueId cueId, int sourceEntityId)
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                sourceEntityId,
                cueId,
                VfxAnchor.ForEntity(3),
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional,
            int maxConcurrentInstances = 0)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                missingAnchorPolicy,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                maxConcurrentInstances: maxConcurrentInstances);
        }

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab, EnemyVfxCue cue)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)cue);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", 0.45f);
            SetField(binding, "tailSeconds", 0.30f);
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

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private sealed class FakeProfileProvider : IGameplayVfxProfileProvider
        {
            private readonly int sourceEntityId;
            private readonly VfxProfile profile;

            public FakeProfileProvider(int sourceEntityId, VfxProfile profile)
            {
                this.sourceEntityId = sourceEntityId;
                this.profile = profile;
            }

            public int CallCount { get; private set; }

            public bool TryResolveProfileForRequest(
                in GameplayVfxRequest request,
                out VfxProfile resolvedProfile)
            {
                CallCount++;
                if (request.SourceEntityId == sourceEntityId && profile != null)
                {
                    resolvedProfile = profile;
                    return true;
                }

                resolvedProfile = null;
                return false;
            }
        }
    }
}
