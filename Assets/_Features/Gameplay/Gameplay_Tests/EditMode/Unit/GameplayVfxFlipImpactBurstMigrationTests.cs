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
    public sealed class GameplayVfxFlipImpactBurstMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string FlipImpactBurstPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FlipImpactBurstVfx.prefab";
        private const string FlipImpactBurstBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FlipImpactBurst_Binding.asset";
        private const string FlipImpactBurstPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/FlipImpactBurstVfxRequestPlanner.cs";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string TickPresentationDataPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs";

        [Test]
        [Category("Extended")]
        public void StayFlipImpact_EmitsFlipImpactBurstRequest()
        {
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay);
            var request = PlanSingleRequest(signal);

            AssertFlipImpactBurstRequest(request, signal, expectedSequence: signal.SourceActionPlanId);
        }

        [Test]
        [Category("Extended")]
        public void DestroySelfFlipImpact_EmitsFlipImpactBurstRequest()
        {
            var signal = CreateSignal(FlipImpactPresentationDisposition.DestroySelf);
            var request = PlanSingleRequest(signal);

            AssertFlipImpactBurstRequest(request, signal, expectedSequence: signal.SourceActionPlanId);
        }

        [Test]
        [Category("Extended")]
        public void InvalidBoxId_DoesNotEmitRequest()
        {
            var plan = PlanRequests(CreateSignal(FlipImpactPresentationDisposition.Stay, boxEntityId: 0));

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void UnsupportedDisposition_DoesNotEmitRequest()
        {
            var plan = PlanRequests(CreateSignal((FlipImpactPresentationDisposition)0));

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void Request_UsesImpactCellCellFloor()
        {
            var impactCell = new SurfaceCell(FaceId.Back, 5, 6);
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay, impactCell: impactCell);
            var request = PlanSingleRequest(signal);

            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(impactCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(signal.Topology));
        }

        [Test]
        [Category("Extended")]
        public void Request_PreservesSurfaceCellFace()
        {
            var impactCell = new SurfaceCell(FaceId.Front, 2, 4);
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay, impactCell: impactCell);
            var request = PlanSingleRequest(signal);

            Assert.That(request.Anchor.Cell, Is.EqualTo(impactCell));
            Assert.That(request.Anchor.Cell.face, Is.EqualTo(FaceId.Front));
        }

        [Test]
        [Category("Extended")]
        public void Request_SourceEntityId_IsBoxEntityId()
        {
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay, boxEntityId: 42);
            var request = PlanSingleRequest(signal);

            Assert.That(request.SourceEntityId, Is.EqualTo(42));
        }

        [Test]
        [Category("Extended")]
        public void Request_SequenceAndSeed_UseSourceActionPlanIdOrFallback()
        {
            var planIdSignal = CreateSignal(FlipImpactPresentationDisposition.Stay, sourceActionPlanId: 991);
            var fallbackSignal = CreateSignal(FlipImpactPresentationDisposition.Stay, sourceActionPlanId: 0, boxEntityId: 43);

            var planIdRequest = PlanSingleRequest(planIdSignal);
            var fallbackRequest = PlanSingleRequest(fallbackSignal);

            Assert.That(planIdRequest.SequenceId, Is.EqualTo(991));
            Assert.That(planIdRequest.PresentationSeed, Is.EqualTo(991));
            Assert.That(fallbackRequest.SequenceId, Is.EqualTo(43));
            Assert.That(fallbackRequest.PresentationSeed, Is.EqualTo(43));
        }

        [Test]
        [Category("Extended")]
        public void Request_IsTransient_NoPersistentKey()
        {
            var request = PlanSingleRequest(CreateSignal(FlipImpactPresentationDisposition.Stay));

            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PersistentKey, Is.EqualTo(VfxPersistentKey.None));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactBurstFlag_DefaultTrue()
        {
            var owner = new GameObject("FlipImpactBurstDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxFlipImpactBurstMigration, Is.True);
                Assert.That(ReadRepoFile(VfxProductionRuntimePath), Does.Not.Contain("SuppressLegacyFlipImpact"));
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOff_DropsFlipImpactBurstRequest()
        {
            var owner = new GameObject("FlipImpactBurstFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = false;

                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.Stay)));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.IsRuntimeInitialized, Is.False);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOn_MissingBinding_DiagnosticNoSpawn()
        {
            var owner = new GameObject("FlipImpactBurstMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = true;

                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.Stay)));

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
        public void FlagOn_BindingPresent_SpawnsOneTransientInstance()
        {
            var owner = new GameObject("FlipImpactBurstEnabled");
            var prefab = new GameObject("FlipImpactBurstRuntimePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, BoxVfxCue.FlipImpactBurst);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.Stay)));

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
        public void Flag_IndependentFromBoxDestroySmokeFlag()
        {
            AssertOtherBoxFlagDoesNotEnableFlipImpactBurst(runtime => runtime.EnableGameplayVfxBoxDestroySmokeMigration = true);
        }

        [Test]
        [Category("Extended")]
        public void Flag_IndependentFromItemConsumeFlag()
        {
            AssertOtherBoxFlagDoesNotEnableFlipImpactBurst(runtime => runtime.EnableGameplayVfxItemConsumeBurstMigration = true);
        }

        [Test]
        [Category("Extended")]
        public void FlagOn_DoesNotSuppressFlipImpactTrack()
        {
            var oldPresenterSource = ReadOldPresenterSource();
            var runtimeSource = ReadRepoFile(VfxProductionRuntimePath);

            Assert.That(runtimeSource, Does.Not.Contain("SuppressLegacyFlipImpact"));
            Assert.That(oldPresenterSource, Does.Not.Contain("EnableGameplayVfxFlipImpactBurstMigration"));
            Assert.That(oldPresenterSource, Does.Not.Contain("FlipImpactBurst"));
        }

        [Test]
        [Category("Extended")]
        public void FlagOn_DoesNotOwnDestroySelfMotionFinalization()
        {
            var exitControllerSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs");

            Assert.That(exitControllerSource, Does.Not.Contain("PlayFlipImpactDestroyEffect"));
            Assert.That(exitControllerSource, Does.Not.Contain("EnableGameplayVfxFlipImpactBurstMigration"));
            Assert.That(exitControllerSource, Does.Not.Contain("SuppressLegacyFlipImpact"));
        }

        [Test]
        [Category("Extended")]
        public void BoxDestroySmoke_DuplicateGuard_RemainsForFlipDestroySelf()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var boxPlanner = new BoxVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            boxPlanner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(
                        new[] { CreateExitSignal(30, TickEntityExitCause.BoxDestroy, cell, topology) },
                        new[] { CreateSignal(FlipImpactPresentationDisposition.DestroySelf, boxEntityId: 30, sourceCell: cell) }),
                    topology),
                builder);

            Assert.That(builder.Build().Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactBurstPrefab_PassesValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlipImpactBurstPrefabPath);

            Assert.That(prefab, Is.Not.Null, FlipImpactBurstPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(HasComponentTypeNamed(prefab, "NavMeshAgent"), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactBurstBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(FlipImpactBurstBindingPath);

            Assert.That(binding, Is.Not.Null, FlipImpactBurstBindingPath);
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.DefaultLifetimeSeconds, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.20f).Within(0.001f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(12));
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesFlipImpactBurst()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(
                    GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst),
                    out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(12));
        }

        [Test]
        [Category("Extended")]
        public void NoAuthorityOrSnapshotToken()
        {
            var source = ReadRepoFile(FlipImpactBurstPlannerPath) + "\n" +
                         ReadRepoFile(VfxPlanningPath) + "\n" +
                         ReadRepoFile(VfxProductionRuntimePath);
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

        [Test]
        [Category("Extended")]
        public void Runtime_DoesNotCreateSnapshots()
        {
            var owner = new GameObject("FlipImpactBurstSnapshotGuard");
            var prefab = new GameObject("FlipImpactBurstSnapshotPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, BoxVfxCue.FlipImpactBurst);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                SnapshotMaterializationCounts counts;
                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.Stay)));
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
        public void MotionTrackHostResolver_StillUnsupported()
        {
            var resolver = new GameplayVfxHostAnchorResolver(
                new RejectingCellProjector(),
                new RejectingEntityProjector());
            var request = new GameplayVfxRequest(
                1,
                1,
                17,
                GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst),
                VfxAnchor.ForMotionTrack(30),
                VfxTimingKind.AtMotionContact);

            var result = resolver.TryResolve(request, out var resolved);

            Assert.That(result, Is.False);
            Assert.That(resolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void NoOldPresenterBypassAdded()
        {
            var oldPresenterSource = ReadOldPresenterSource();

            Assert.That(oldPresenterSource, Does.Not.Contain("FlipImpactContactVfxAnchor"));
            Assert.That(oldPresenterSource, Does.Not.Contain("SuppressLegacyFlipImpact"));
            Assert.That(oldPresenterSource, Does.Not.Contain("EnableGameplayVfxFlipImpactBurstMigration"));
            Assert.That(oldPresenterSource, Does.Not.Contain("FlipImpactBurst"));
        }

        [Test]
        [Category("Extended")]
        public void NoTickPresentationShapeChange()
        {
            var source = ReadRepoFile(TickPresentationDataPath);

            Assert.That(source, Does.Not.Contain("FlipImpactBurst"));
            Assert.That(source, Does.Not.Contain("FlipImpactContactVfxAnchor"));
            Assert.That(source, Does.Not.Contain("EnableGameplayVfxFlipImpact"));
        }

        private static GameplayVfxRequestPlan PlanRequests(params FlipImpactPresentationSignal[] flipImpactSignals)
        {
            var planner = new FlipImpactBurstVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var topology = new CubeTopologyState(FaceId.Floor);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(Array.Empty<TickEntityExitPresentationSignal>(), flipImpactSignals),
                    topology,
                    GameplayTimingProfile.CreateDefault()),
                builder);

            return builder.Build();
        }

        private static GameplayVfxRequest PlanSingleRequest(FlipImpactPresentationSignal signal)
        {
            var plan = PlanRequests(signal);

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static void AssertFlipImpactBurstRequest(
            in GameplayVfxRequest request,
            in FlipImpactPresentationSignal signal,
            int expectedSequence)
        {
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.SequenceId, Is.EqualTo(expectedSequence));
            Assert.That(request.PresentationSeed, Is.EqualTo(expectedSequence));
            Assert.That(request.SourceEntityId, Is.EqualTo(signal.BoxEntityId));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PersistentKey, Is.EqualTo(VfxPersistentKey.None));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(signal.ImpactCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(signal.Topology));
        }

        private static void AssertOtherBoxFlagDoesNotEnableFlipImpactBurst(Action<GameplayVfxProductionRuntime> enableFlag)
        {
            var owner = new GameObject("FlipImpactBurstFlagIndependence");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = false;
                enableFlag(runtime);

                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.Stay)));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params FlipImpactPresentationSignal[] flipImpactSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(8, 8)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(Array.Empty<TickEntityExitPresentationSignal>(), flipImpactSignals),
                    topology),
                topology,
                stateStore,
                projector,
                timingProfile: GameplayTimingProfile.CreateDefault());
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

        private static TickPresentationData CreatePresentationData(
            TickEntityExitPresentationSignal[] exitSignals,
            FlipImpactPresentationSignal[] flipImpactSignals)
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
                Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals);
        }

        private static FlipImpactPresentationSignal CreateSignal(
            FlipImpactPresentationDisposition disposition,
            int sourceActionPlanId = 7,
            int boxEntityId = 30,
            SurfaceCell? sourceCell = null,
            SurfaceCell? impactCell = null)
        {
            return new FlipImpactPresentationSignal(
                sourceActionPlanId,
                boxEntityId,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                sourceCell ?? new SurfaceCell(FaceId.Floor, 1, 1),
                impactCell ?? new SurfaceCell(FaceId.Floor, 2, 1),
                new CubeTopologyState(FaceId.Floor),
                Direction.Right,
                Direction.Left,
                disposition);
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(
            int entityId,
            TickEntityExitCause exitCause,
            SurfaceCell cell,
            CubeTopologyState topology)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                exitCause,
                cell,
                topology,
                Direction.Right,
                EntityType.Box,
                sourceActorEntityId: 10,
                presentationSeed: 8831);
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
            SetField(binding, "defaultLifetimeSeconds", 0.22f);
            SetField(binding, "tailSeconds", 0.20f);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 12);
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
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static string ReadOldPresenterSource()
        {
            return ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs") +
                   "\n" +
                   ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs") +
                   "\n" +
                   ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath)).Replace("\r\n", "\n");
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

        private static bool HasComponentTypeNamed(GameObject prefab, string typeName)
        {
            var components = prefab.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RejectingCellProjector : IGameplayVfxCellAnchorProjector
        {
            public bool TryResolveCell(
                SurfaceCell cell,
                CubeTopologyState topology,
                VfxAnchorSlot slot,
                out VfxResolvedAnchor resolvedAnchor)
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }
        }

        private sealed class RejectingEntityProjector : IGameplayVfxEntityAnchorProjector
        {
            public bool TryResolveEntity(
                int entityId,
                VfxAnchorSlot slot,
                out VfxResolvedAnchor resolvedAnchor)
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }
        }
    }
}
