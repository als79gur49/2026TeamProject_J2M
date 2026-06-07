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
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxReservedCueRuntimePolicyTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string ImpactTransientBreakBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ImpactTransientBreak_Binding.asset";
        private const string BoxOutOfBoundsExitBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxOutOfBoundsExit_Binding.asset";
        private const string EnemyOutOfBoundsExitBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyOutOfBoundsExit_Binding.asset";
        private const string ExitControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string TickResultBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs";

        [Test]
        [Category("Extended")]
        public void ImpactTransientSignal_EmitsImpactTransientBreakVfx()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var impactCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var request = PlanSingleBoxRequest(
                Array.Empty<TickEntityExitPresentationSignal>(),
                new[]
                {
                    CreateImpactSignal(30, cell, impactCell, topology, presentationSeed: 901),
                });

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak)));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.PresentationSeed, Is.EqualTo(901));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Cell, Is.EqualTo(cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
        }

        [Test]
        [Category("Extended")]
        public void ImpactTransientCommand_UsesSourceToImpactPoseAndFadeTiming()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var impactCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var signal = CreateImpactSignal(30, cell, impactCell, topology, presentationSeed: 901);
            var projector = CreateProjector();
            var poseResolver = CreatePoseResolver();

            var built = ImpactTransientBreakVfxCommandBuilder.TryBuild(
                12,
                signal,
                GameplayTimingProfile.CreateDefault(),
                poseResolver,
                projector,
                out var command);

            Assert.That(built, Is.True);
            Assert.That(command.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak)));
            Assert.That(command.SourceEntityId, Is.EqualTo(30));
            Assert.That(command.SourceLocalPosition, Is.Not.EqualTo(command.TargetLocalPosition));
            Assert.That(command.DurationSeconds, Is.EqualTo(Mathf.Max(
                GameplayTimingProfile.DefaultBoxDestroyEffectDurationSeconds,
                GameplayTimingProfile.DefaultFlipMotionDurationSeconds)).Within(0.0001f));
            Assert.That(command.BreakStartSeconds, Is.EqualTo(command.DurationSeconds * 0.62f).Within(0.0001f));
            Assert.That(command.FadeDurationSeconds, Is.EqualTo(command.DurationSeconds - command.BreakStartSeconds).Within(0.0001f));
            Assert.That(command.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.SourceCloneMotion));
            Assert.That(command.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.ScaleAndAlpha));
            Assert.That(command.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.FlipArc));
        }

        [Test]
        [Category("Extended")]
        public void ImpactTransientBreak_MissingBinding_ReportsDiagnosticNoOp()
        {
            var owner = new GameObject("ImpactTransientMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    new[] { CreateImpactSignal(30) }));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                AssertExitControllerOldImpactPathDisabled();
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ImpactTransientBreak_FlagOff_DisablesVfxWithoutFallback()
        {
            var owner = new GameObject("ImpactTransientFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxImpactTransientBreakMigration = false;

                runtime.Present(CreateExtensionContext(
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    new[] { CreateImpactSignal(30) }));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                AssertExitControllerOldImpactPathDisabled();
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ImpactTransient_DuplicateGuardWithBoxDestroy_RemainsCorrect()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var request = PlanSingleBoxRequest(
                new[] { CreateExitSignal(30, TickEntityExitCause.BoxDestroy, cell, topology, EntityType.Box) },
                new[] { CreateImpactSignal(30, cell, new SurfaceCell(FaceId.Floor, 2, 1), topology) });

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak)));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBoundsExitSignal_EmitsFamilyOutOfBoundsVfx()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var boxRequest = PlanSingleBoxRequest(
                new[] { CreateExitSignal(30, TickEntityExitCause.OutOfBounds, cell, topology, EntityType.Box) },
                Array.Empty<TickImpactTransientPresentationSignal>());
            var enemyRequest = PlanSingleEnemyRequest(
                CreateExitSignal(40, TickEntityExitCause.OutOfBounds, cell, topology, EntityType.Unit));

            Assert.That(boxRequest.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit)));
            Assert.That(boxRequest.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(enemyRequest.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)));
            Assert.That(enemyRequest.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
        }

        [Test]
        [Category("Extended")]
        public void EntityExitOutOfBoundsCommand_UsesSourcePoseVanishFade()
        {
            var signal = CreateExitSignal(40, TickEntityExitCause.OutOfBounds, entityType: EntityType.Unit);
            Assert.That(EntityExitOutOfBoundsVfxCommandBuilder.TryResolveCue(signal, out var cueId), Is.True);

            var built = EntityExitOutOfBoundsVfxCommandBuilder.TryBuild(
                12,
                signal,
                cueId,
                GameplayTimingProfile.CreateDefault(),
                CreatePoseResolver(),
                CreateProjector(),
                out var command);

            Assert.That(built, Is.True);
            Assert.That(command.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)));
            Assert.That(command.SourceLocalPosition, Is.EqualTo(command.TargetLocalPosition));
            Assert.That(command.SourceLocalRotation, Is.EqualTo(command.TargetLocalRotation));
            Assert.That(command.DurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultItemConsumeEffectDurationSeconds).Within(0.0001f));
            Assert.That(command.FadeDurationSeconds, Is.EqualTo(command.DurationSeconds).Within(0.0001f));
            Assert.That(command.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.SourceCloneMotion));
            Assert.That(command.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.DestroyShrinkEase));
            Assert.That(command.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.Linear));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBoundsExit_FlagOff_DisablesVfxWithoutFallback()
        {
            var owner = new GameObject("OutOfBoundsFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxOutOfBoundsExitMigration = false;

                runtime.Present(CreateExtensionContext(
                    new[] { CreateExitSignal(30, TickEntityExitCause.OutOfBounds) },
                    Array.Empty<TickImpactTransientPresentationSignal>()));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                AssertExitControllerOldOutOfBoundsPathDisabled();
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void OutOfBoundsExit_MissingBinding_ReportsDiagnosticNoOp()
        {
            var owner = new GameObject("OutOfBoundsMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(
                    new[] { CreateExitSignal(30, TickEntityExitCause.OutOfBounds) },
                    Array.Empty<TickImpactTransientPresentationSignal>()));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                AssertExitControllerOldOutOfBoundsPathDisabled();
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceCloneMotionReservedCues_PlayThroughRuntime_WhenSourcesAndCommonHostExist()
        {
            var owner = new GameObject("ReservedSignalsRuntime");
            var commonHost = new GameObject("ReservedSignalsCommonHost");
            var impactSource = CreateSourceView("ReservedSignalsImpactSource", 30);
            var boxSource = CreateSourceView("ReservedSignalsBoxSource", 31);
            var enemySource = CreateSourceView("ReservedSignalsEnemySource", 41);
            VfxBindingDefinitionAsset impactBinding = null;
            VfxBindingDefinitionAsset boxOutOfBoundsBinding = null;
            VfxBindingDefinitionAsset enemyOutOfBoundsBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                impactBinding = CreateBinding(
                    null,
                    GameplayVfxFamily.Box,
                    (int)BoxVfxCue.ImpactTransientBreak,
                    sourceCloneMotion: true);
                boxOutOfBoundsBinding = CreateBinding(
                    null,
                    GameplayVfxFamily.Box,
                    (int)BoxVfxCue.OutOfBoundsExit,
                    sourceCloneMotion: true);
                enemyOutOfBoundsBinding = CreateBinding(
                    null,
                    GameplayVfxFamily.Enemy,
                    (int)EnemyVfxCue.OutOfBoundsExit,
                    sourceCloneMotion: true);
                cueMap = CreateCueMap(impactBinding, boxOutOfBoundsBinding, enemyOutOfBoundsBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureCommonEmptyHostPrefab(commonHost);

                runtime.Present(CreateExtensionContext(
                    new[]
                    {
                        CreateExitSignal(31, TickEntityExitCause.OutOfBounds, entityType: EntityType.Box),
                        CreateExitSignal(41, TickEntityExitCause.OutOfBounds, entityType: EntityType.Unit),
                    },
                    new[] { CreateImpactSignal(30) },
                    impactSource.View,
                    boxSource.View,
                    enemySource.View));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(3));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.MissingSourceViewCount, Is.Zero);
                Assert.That(runtime.CommonHostUnavailableCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(3));
            }
            finally
            {
                impactSource.Destroy();
                boxSource.Destroy();
                enemySource.Destroy();
                Destroy(cueMap, enemyOutOfBoundsBinding, boxOutOfBoundsBinding, impactBinding, commonHost, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ImpactTransientBreak_BindingPolicy_IsSourceCloneMotionWithCommonHost()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ImpactTransientBreakBindingPath);

            AssertSourceCloneMotionBinding(
                binding,
                ImpactTransientBreakBindingPath,
                GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBoundsExit_Box_BindingPolicy_IsSourceCloneMotionWithCommonHost()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(BoxOutOfBoundsExitBindingPath);

            AssertSourceCloneMotionBinding(
                binding,
                BoxOutOfBoundsExitBindingPath,
                GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBoundsExit_Enemy_BindingPolicy_IsSourceCloneMotionWithCommonHost()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(EnemyOutOfBoundsExitBindingPath);

            AssertSourceCloneMotionBinding(
                binding,
                EnemyOutOfBoundsExitBindingPath,
                GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit));
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesSourceCloneMotionReservedCues()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            AssertHostDefaultSourceCloneMotionCue(
                cueMap,
                GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak));
            AssertHostDefaultSourceCloneMotionCue(
                cueMap,
                GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit));
            AssertHostDefaultSourceCloneMotionCue(
                cueMap,
                GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBoundsExit_Enemy_DoesNotUseEnemyDeathMotionBinding()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit), out var outOfBounds),
                Is.True);
            Assert.That(outOfBounds.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)));
            Assert.That(outOfBounds.CueId, Is.Not.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
        }

        [Test]
        [Category("Extended")]
        public void ImpactTransient_NoNormalProducerPolicy_DocumentedAndPreserved()
        {
            var builder = File.ReadAllText(TickResultBuilderPath);

            Assert.That(builder, Does.Contain("BuildImpactTransientPresentation"));
            Assert.That(builder, Does.Contain("impactTransientSignals.Clear();"));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_DoesNotProduceOutOfBoundsExitCauseHint()
        {
            var loopFiles = Directory.GetFiles(
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime",
                "*.cs",
                SearchOption.TopDirectoryOnly);
            var combined = string.Join("\n", Array.ConvertAll(loopFiles, File.ReadAllText));

            Assert.That(combined, Does.Not.Contain("exitCauseHint: TickEntityExitCause.OutOfBounds"));
            Assert.That(combined, Does.Not.Contain("ExitCauseHint = TickEntityExitCause.OutOfBounds"));
        }

        private static GameplayVfxRequest PlanSingleBoxRequest(
            TickEntityExitPresentationSignal[] exitSignals,
            TickImpactTransientPresentationSignal[] impactTransientSignals)
        {
            var planner = new BoxVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(exitSignals, impactTransientSignals),
                    new CubeTopologyState(FaceId.Floor)),
                builder);
            var plan = builder.Build();

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayVfxRequest PlanSingleEnemyRequest(TickEntityExitPresentationSignal signal)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(new[] { signal }, Array.Empty<TickImpactTransientPresentationSignal>()),
                    new CubeTopologyState(FaceId.Floor)),
                builder);
            var plan = builder.Build();

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickEntityExitPresentationSignal[] exitSignals,
            TickImpactTransientPresentationSignal[] impactTransientSignals,
            params GameplayEntityView[] sourceViews)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            for (var i = 0; i < (sourceViews?.Length ?? 0); i++)
            {
                var sourceView = sourceViews[i];
                if (sourceView != null)
                {
                    stateStore.ViewsByEntityId[sourceView.EntityId] = sourceView;
                }
            }

            return new GameplayTickPresentationExtensionContext(
                new TickResult(
                    12,
                    Array.Empty<TickPhase>(),
                    Array.Empty<string>(),
                    MovementPhaseResult.Empty,
                    AttackPhaseResult.Empty,
                    Array.Empty<EntityState>(),
                    Array.Empty<string>(),
                    topology,
                    CreatePresentationData(exitSignals, impactTransientSignals),
                    "hash",
                    TickTrace.Empty),
                topology,
                stateStore,
                CreateProjector());
        }

        private static TickPresentationData CreatePresentationData(
            TickEntityExitPresentationSignal[] exitSignals,
            TickImpactTransientPresentationSignal[] impactTransientSignals)
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
                impactTransientSignals,
                Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickImpactTransientPresentationSignal CreateImpactSignal(
            int entityId,
            SurfaceCell cell = default,
            SurfaceCell impactCell = default,
            CubeTopologyState topology = default,
            int presentationSeed = 901)
        {
            var resolvedCell = cell.Equals(default(SurfaceCell))
                ? new SurfaceCell(FaceId.Floor, 1, 1)
                : cell;
            var resolvedImpactCell = impactCell.Equals(default(SurfaceCell))
                ? new SurfaceCell(FaceId.Floor, 2, 1)
                : impactCell;
            var resolvedTopology = topology.Equals(default(CubeTopologyState))
                ? new CubeTopologyState(FaceId.Floor)
                : topology;
            return new TickImpactTransientPresentationSignal(
                entityId,
                EntityType.Box,
                resolvedCell,
                resolvedImpactCell,
                resolvedTopology,
                Direction.Right,
                presentationSeed);
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

        private static GameplayCubeProjector CreateProjector()
        {
            return new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);
        }

        private static GameplayPoseResolver CreatePoseResolver()
        {
            return new GameplayPoseResolver(
                new GameplayPresentationStateStore(),
                new GameplayPresentationTrackState());
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            GameObject prefab,
            GameplayVfxFamily family,
            int cueCode,
            bool sourceCloneMotion = false)
        {
            if (prefab != null)
            {
                GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);
            }

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", family);
            SetField(binding, "cueCode", cueCode);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(
                binding,
                "visualSourceMode",
                sourceCloneMotion
                    ? VfxVisualSourceMode.SourceCloneMotion
                    : VfxVisualSourceMode.PrefabOnly);
            SetField(
                binding,
                "hostRequirement",
                sourceCloneMotion
                    ? GameplayVfxHostRequirement.CommonHostAllowed
                    : GameplayVfxHostRequirement.ExplicitPrefabRequired);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", 0f);
            SetField(binding, "tailSeconds", sourceCloneMotion ? 0.18f : 0.20f);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 8);
            return binding;
        }

        private static SourceViewFixture CreateSourceView(string name, int entityId)
        {
            var owner = new GameObject(name);
            var view = owner.AddComponent<GameplayEntityView>();
            view.Initialize(entityId);
            var modelRoot = view.EnsureModelRoot();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(modelRoot, worldPositionStays: false);
            visual.transform.localScale = Vector3.one * 0.35f;
            return new SourceViewFixture(owner, view);
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static void AssertSourceCloneMotionBinding(
            VfxBindingDefinitionAsset binding,
            string path,
            GameplayVfxCueId expectedCueId)
        {
            Assert.That(binding, Is.Not.Null, path);
            Assert.That(binding.CueId, Is.EqualTo(expectedCueId));
            Assert.That(binding.Prefab, Is.Null);
            Assert.That(binding.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion));
            Assert.That(binding.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.CommonHostAllowed));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(8));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        private static void AssertHostDefaultSourceCloneMotionCue(
            VfxCueMapAsset cueMap,
            GameplayVfxCueId cueId)
        {
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(cueId, out var policy), Is.True);
            Assert.That(policy.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion));
            Assert.That(policy.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.CommonHostAllowed));
            Assert.That(cueMap.TryResolvePrefab(cueId, out _), Is.False);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void AssertExitControllerOldImpactPathDisabled()
        {
            var source = File.ReadAllText(ExitControllerPath);
            Assert.That(source, Does.Not.Contain("PlayImpactBreakEffect"));
            Assert.That(File.Exists("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectPresenter.cs"), Is.False);
        }

        private static void AssertExitControllerOldOutOfBoundsPathDisabled()
        {
            var source = File.ReadAllText(ExitControllerPath);
            Assert.That(source, Does.Not.Contain("ShouldPlayLegacyEntityExitEffect"));
            Assert.That(source, Does.Not.Contain("PlayExitEffect"));
        }

        private static void Destroy(params Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private readonly struct SourceViewFixture
        {
            public SourceViewFixture(GameObject owner, GameplayEntityView view)
            {
                Owner = owner;
                View = view;
            }

            public GameObject Owner { get; }

            public GameplayEntityView View { get; }

            public void Destroy()
            {
                GameplayVfxReservedCueRuntimePolicyTests.Destroy(Owner);
            }
        }
    }
}
