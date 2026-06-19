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
    public sealed class GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string MotionPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDeathMotionVfx.prefab";
        private const string MotionBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDeathMotion_Binding.asset";
        private const string EnemyOutOfBoundsExitBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyOutOfBoundsExit_Binding.asset";
        private const string CommandPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyDeathMotionVfxCommandBuilder.cs";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string InactiveBlendProperty = "_InactiveBlend";
        private const string InactiveNoiseRevealProperty = "_InactiveNoiseReveal";
        private const string DesaturateStrengthProperty = "_DesaturateStrength";
        private const string EmissionSuppressionProperty = "_EmissionSuppression";
        private const string InactiveTintProperty = "_InactiveTint";

        [Test]
        [Category("Extended")]
        public void EnemyDeathExit_BuildsDeathMotionCommand()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath);

                var result = EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                Assert.That(result, Is.True);
                Assert.That(command.EntityId, Is.EqualTo(40));
                Assert.That(command.SourceActorEntityId, Is.EqualTo(10));
                Assert.That(command.SourceCell, Is.EqualTo(signal.SourceCell));
                Assert.That(command.Topology, Is.EqualTo(signal.Topology));
                Assert.That(command.PresentationSeed, Is.EqualTo(signal.PresentationSeed));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void NonDeathExit_DoesNotBuild()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(40, TickEntityExitCause.BoxDestroy)), Is.False);
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(40, TickEntityExitCause.ItemConsume)), Is.False);
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath, entityType: EntityType.Box)), Is.False);
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(0, TickEntityExitCause.EnemyDeath)), Is.False);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourcePose_ResolvedFromExitSignalSourceCell()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath);

                EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);
                fixture.PoseResolver.TryResolveEntityExitSignalLocalPose(
                    fixture.Projector,
                    signal,
                    out var expectedPose);

                Assert.That(Vector3.Distance(command.SourceLocalPosition, expectedPose.Position), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Quaternion.Angle(command.SourceLocalRotation, expectedPose.Rotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ContactDelayedSourcePose_UsesRetainedPoseBeforeExitSignalFallback()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var retainedPose = new GameplayEntityPose(
                    new Vector3(2.25f, 0.5f, -0.75f),
                    Quaternion.Euler(0f, 37f, 0f));
                fixture.StateStore.RetainedLocalTargetPoses[40] = retainedPose;
                var signal = CreateEnemyExitSignal(
                    40,
                    TickEntityExitCause.EnemyDeath,
                    timing: EntityExitPresentationTiming.AtContactTime,
                    visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime);

                var result = EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                Assert.That(result, Is.True);
                Assert.That(Vector3.Distance(command.SourceLocalPosition, retainedPose.Position), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Quaternion.Angle(command.SourceLocalRotation, retainedPose.Rotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void TargetPose_UsesLegacyCameraDirectionLogic()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath);
                fixture.PoseResolver.TryResolveEntityExitSignalLocalPose(
                    fixture.Projector,
                    signal,
                    out var sourcePose);
                var expectedPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    fixture.LocalSpaceRoot.transform,
                    sourcePose,
                    fixture.PlayerPose,
                    fixture.Camera,
                    fixture.Projector.CellSize,
                    signal.PresentationSeed);

                EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                Assert.That(Vector3.Distance(command.TargetLocalPosition, expectedPlan.TargetLocalPosition), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Vector3.Distance(command.ArcLocalDirection.normalized, expectedPlan.ArcLocalDirection.normalized), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(command.ArcHeight, Is.EqualTo(expectedPlan.ArcHeight).Within(0.0001f));
                Assert.That(command.SpinDegrees, Is.EqualTo(expectedPlan.SpinDegrees).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotionCommand_UsesPrefabWithSourceCloneAndDeathFade()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath),
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                var parameterized = command.ToParameterizedMotionVfxCommand();

                Assert.That(parameterized.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
                Assert.That(parameterized.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
                Assert.That(parameterized.CloneMode, Is.Not.EqualTo(ParameterizedMotionVfxCloneMode.SourceCloneMotion));
                Assert.That(parameterized.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.EnemyDeathFlyAway));
                Assert.That(parameterized.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.EnemyDeathFade));
                Assert.That(parameterized.BreakStartSeconds, Is.EqualTo(parameterized.DurationSeconds * 0.12f).Within(0.0001f));
                Assert.That(parameterized.FadeDurationSeconds, Is.EqualTo(parameterized.DurationSeconds * 0.88f).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotion_FrontFaceInactiveSource_EmitsSourceCloneFlyAway()
        {
            var owner = new GameObject("EnemyDeathMotionFrontFaceInactiveRuntime");
            var cameraObject = CreateCameraObject("EnemyDeathMotionFrontFaceInactiveCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionFrontFaceInactivePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            InactiveSourceViewFixture source = default;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.Killed);
                var context = CreateExtensionContext(signal);
                source = CreateInactiveSourceView(context.StateStore, entityId: 40);
                context.StateStore.EnemyVisualSemanticStatesByEntityId[40] =
                    new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive);

                runtime.Present(context);

                Assert.That(
                    runtime.LastPlannedRequestCount,
                    Is.EqualTo(1),
                    "EnemyDeathMotion is admitted from the death presentation fact, not the ForwardCellProjectile live source gate.");
                Assert.That(
                    runtime.GetActiveVfxInstanceCount(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)),
                    Is.EqualTo(1),
                    "FrontFaceInactive death feedback must remain visible through the presentation fact admission contract.");
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.MissingSourceViewCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.CommonHostUnavailableCount, Is.Zero);
                Assert.That(runtime.InvalidPlaybackModePolicyCount, Is.Zero);
                Assert.That(
                    FindParameterizedMotionClone(owner.transform),
                    Is.Not.Null,
                    "FrontFaceInactive EnemyDeathMotion must keep the PrefabWithSourceClone fly-away path.");
                Assert.That(
                    runtime.ForwardCellProjectilePresentationOnlyMisuseCandidateCount,
                    Is.Zero,
                    "EnemyDeathMotion death clone is independent from the ForwardCellProjectile live source gate.");
                Assert.That(
                    runtime.ForwardCellProjectilePresentationOnlyAllowedTopologyHelperCount,
                    Is.Zero,
                    "EnemyDeathMotion death clone must not use ForwardCellProjectile PresentationOnly counters.");

                var builderFixture = CreateBuilderFixture();
                try
                {
                    builderFixture.StateStore.EnemyVisualSemanticStatesByEntityId[40] =
                        new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive);
                    var built = EnemyDeathMotionVfxCommandBuilder.TryBuild(
                        signal,
                        builderFixture.TimingProfile,
                        builderFixture.PoseResolver,
                        builderFixture.Projector,
                        builderFixture.TargetResolver,
                        out var command);

                    Assert.That(
                        built,
                        Is.True,
                        "Death presentation fact admission must build the command without requiring the live source gate.");
                    AssertEnemyDeathMotionParameterizedContract(command.ToParameterizedMotionVfxCommand());
                }
                finally
                {
                    builderFixture.Destroy();
                }
            }
            finally
            {
                source.Destroy();
                Destroy(cueMap, binding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_EnemyDeathMotion_IsCanonical()
        {
            var owner = new GameObject("EnemyDeathMotionDefaultFlag");
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
        public void EnemyDeathMotion_MissingBindingDoesNotFallback()
        {
            var owner = new GameObject("EnemyDeathMotionFlagOffNoFallback");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.Killed)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotion_WithBinding_PlaysParameterizedMotionAlongsideCanonicalBurst()
        {
            var owner = new GameObject("EnemyDeathMotionRuntime");
            var cameraObject = CreateCameraObject("EnemyDeathMotionRuntimeCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionRuntimePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotion_AtContactTime_SpawnsDeathMotionAtVisualContact()
        {
            var owner = new GameObject("EnemyDeathMotionDelayedRuntime");
            var cameraObject = CreateCameraObject("EnemyDeathMotionDelayedRuntimeCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionDelayedRuntimePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);
                var context = CreateExtensionContext(
                    CreateEnemyExitSignal(
                        40,
                        TickEntityExitCause.EnemyDeath,
                        timing: EntityExitPresentationTiming.AtContactTime,
                        visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
                context.StateStore.RetainedLocalTargetPoses[40] = new GameplayEntityPose(
                    new Vector3(1.25f, 0.5f, -0.25f),
                    Quaternion.Euler(0f, 45f, 0f));

                runtime.Present(context);

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);

                const float epsilon = 0.001f;
                var contactDelaySeconds =
                    context.TimingProfile.FlipMotionDurationSeconds *
                    GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;
                runtime.UpdatePresentation(contactDelaySeconds - epsilon);
                runtime.RefreshPresentationMotionVfx(
                    new GameplayPresentationMotionVfxContext(
                        context.Result.TickIndex,
                        new GameplayPresentationTrackState(),
                        context.StateStore,
                        context.Projector,
                        context.TimingProfile));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);

                runtime.UpdatePresentation(epsilon);
                runtime.RefreshPresentationMotionVfx(
                    new GameplayPresentationMotionVfxContext(
                        context.Result.TickIndex,
                        new GameplayPresentationTrackState(),
                        context.StateStore,
                        context.Projector,
                        context.TimingProfile));

                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                runtime.RefreshPresentationMotionVfx(
                    new GameplayPresentationMotionVfxContext(
                        context.Result.TickIndex,
                        new GameplayPresentationTrackState(),
                        context.StateStore,
                        context.Projector,
                        context.TimingProfile));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void DelayedEnemyDeathMotion_CapturesInactiveVisualStateBeforeSourceReset()
        {
            var owner = new GameObject("EnemyDeathMotionDelayedInactiveRuntime");
            var cameraObject = CreateCameraObject("EnemyDeathMotionDelayedInactiveRuntimeCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionDelayedInactiveRuntimePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            InactiveSourceViewFixture source = default;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);
                var context = CreateExtensionContext(
                    CreateEnemyExitSignal(
                        40,
                        TickEntityExitCause.EnemyDeath,
                        timing: EntityExitPresentationTiming.AtContactTime,
                        visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
                context.StateStore.RetainedLocalTargetPoses[40] = new GameplayEntityPose(
                    new Vector3(1.25f, 0.5f, -0.25f),
                    Quaternion.Euler(0f, 45f, 0f));
                source = CreateInactiveSourceView(context.StateStore, entityId: 40);
                var scheduledTint = new Color(0.16f, 0.29f, 0.47f, 1f);
                ApplyInactivePropertyBlock(
                    source.Renderer,
                    inactiveBlend: 1f,
                    inactiveNoiseReveal: 1f,
                    desaturateStrength: 0.33f,
                    emissionSuppression: 0.77f,
                    scheduledTint);

                runtime.Present(context);
                ApplyInactivePropertyBlock(
                    source.Renderer,
                    inactiveBlend: 0f,
                    inactiveNoiseReveal: 0f,
                    desaturateStrength: 0.11f,
                    emissionSuppression: 0.22f,
                    new Color(0.9f, 0.1f, 0.1f, 1f));
                source.Owner.SetActive(false);

                var contactDelaySeconds =
                    context.TimingProfile.FlipMotionDurationSeconds *
                    GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;
                runtime.UpdatePresentation(contactDelaySeconds);
                runtime.RefreshPresentationMotionVfx(
                    new GameplayPresentationMotionVfxContext(
                        context.Result.TickIndex,
                        new GameplayPresentationTrackState(),
                        context.StateStore,
                        context.Projector,
                        context.TimingProfile));

                var clone = FindParameterizedMotionClone(owner.transform);
                Assert.That(clone, Is.Not.Null);
                var cloneMaterial = clone
                    .GetComponentInChildren<Renderer>(includeInactive: true)
                    .sharedMaterial;
                Assert.That(cloneMaterial.GetFloat(InactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(InactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(DesaturateStrengthProperty), Is.EqualTo(0.33f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(EmissionSuppressionProperty), Is.EqualTo(0.77f).Within(0.0001f));
                AssertColorApproximately(scheduledTint, cloneMaterial.GetColor(InactiveTintProperty));
            }
            finally
            {
                source.Destroy();
                Destroy(cueMap, binding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotion_MissingBinding_ReportsDiagnosticNoOp()
        {
            var owner = new GameObject("EnemyDeathMotionMissingBinding");
            var cameraObject = CreateCameraObject("EnemyDeathMotionMissingBindingCamera");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(2));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotion_MissingCamera_DiagnosticNoOp()
        {
            var owner = new GameObject("EnemyDeathMotionMissingCamera");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingAnchorCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionOnBurstOn_NewMotionAndBurstAllowed()
        {
            var owner = new GameObject("EnemyDeathMotionBurstCombo");
            var cameraObject = CreateCameraObject("EnemyDeathMotionBurstComboCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionBurstComboPrefab");
            VfxBindingDefinitionAsset motionBinding = null;
            VfxBindingDefinitionAsset burstBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                motionBinding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                burstBinding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.Death), tailSeconds: 0.25f, defaultLifetimeSeconds: 0.35f);
                cueMap = CreateCueMap(motionBinding, burstBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(2));
            }
            finally
            {
                Destroy(cueMap, motionBinding, burstBinding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_EnemyDeathMotionOn_UsesVfxMotionAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("EnemyDeathMotionCoordinator");
            var cameraObject = CreateCameraObject("EnemyDeathMotionCoordinatorCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionCoordinatorPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachOutputCamera(cameraObject.GetComponent<Camera>());
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateEnemyUnit(40, scenario.EnemyCell),
                    },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath, scenario.EnemyCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastDamageDeathExecutorOwnedFilteredRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(scenario.Registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Destroy(cueMap, binding, prefab, cameraObject);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotionPrefab_PassesValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotionPrefabPath);

            Assert.That(prefab, Is.Not.Null, MotionPrefabPath);
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
        public void EnemyDeathMotionBinding_IsPrefabWithSourceCloneFallbackPolicy()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(MotionBindingPath);

            Assert.That(binding, Is.Not.Null, MotionBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.PrefabWithSourceClone));
            Assert.That(binding.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.ExplicitPrefabRequired));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.DefaultLifetimeSeconds, Is.EqualTo(0f).Within(0.001f));
            Assert.That(binding.TailSeconds, Is.InRange(0.18f, 0.25f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(8));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesEnemyDeathMotionPrefabWithSourceClone()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), out var policy),
                Is.True);
            Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
            Assert.That(policy.CueId, Is.Not.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)));
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(policy.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.PrefabWithSourceClone));
            Assert.That(cueMap.TryResolvePrefab(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), out var prefab), Is.True);
            Assert.That(prefab, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotion_DoesNotUseOutOfBoundsExitBinding()
        {
            var deathBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(MotionBindingPath);
            var outOfBoundsBinding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(EnemyOutOfBoundsExitBindingPath);

            Assert.That(deathBinding, Is.Not.Null, MotionBindingPath);
            Assert.That(outOfBoundsBinding, Is.Not.Null, EnemyOutOfBoundsExitBindingPath);
            Assert.That(deathBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
            Assert.That(outOfBoundsBinding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)));
            Assert.That(deathBinding.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.PrefabWithSourceClone));
            Assert.That(outOfBoundsBinding.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion));
        }

        [Test]
        [Category("Extended")]
        public void CommandAndRuntimeSources_DoNotReferenceAuthorityTypes()
        {
            AssertForbiddenAuthorityTokensAbsent(ReadRepoFile(CommandPath) + "\n" + ReadRepoFile(ProductionRuntimePath));
        }

        private static bool TryBuild(BuilderFixture fixture, TickEntityExitPresentationSignal signal)
        {
            return EnemyDeathMotionVfxCommandBuilder.TryBuild(
                signal,
                fixture.TimingProfile,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.TargetResolver,
                out _);
        }

        private static void AssertEnemyDeathMotionParameterizedContract(ParameterizedMotionVfxCommand parameterized)
        {
            Assert.That(parameterized.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
            Assert.That(parameterized.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
            Assert.That(parameterized.CloneMode, Is.Not.EqualTo(ParameterizedMotionVfxCloneMode.SourceCloneMotion));
            Assert.That(parameterized.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.EnemyDeathFlyAway));
            Assert.That(parameterized.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.EnemyDeathFade));
        }

        private static BuilderFixture CreateBuilderFixture()
        {
            var localSpaceRoot = new GameObject("EnemyDeathMotionBuilderRoot");
            var cameraObject = CreateCameraObject("EnemyDeathMotionBuilderCamera");
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[40] = EntityType.Unit;
            stateStore.UnitRolesByEntityId[10] = UnitRole.Player;
            var playerPose = new GameplayEntityPose(new Vector3(0.35f, 0.15f, 0f), Quaternion.identity);
            stateStore.CommittedLocalTargetPoses[10] = playerPose;
            var trackState = new GameplayPresentationTrackState();
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);
            var resolver = new EnemyDeathMotionTargetResolver(
                localSpaceRoot.transform,
                cameraObject.GetComponent<Camera>(),
                stateStore,
                projector.CellSize);
            return new BuilderFixture(
                localSpaceRoot,
                cameraObject,
                cameraObject.GetComponent<Camera>(),
                GameplayTimingProfile.CreateDefault(),
                new GameplayPoseResolver(stateStore, trackState),
                projector,
                resolver,
                stateStore,
                playerPose);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params TickEntityExitPresentationSignal[] exitSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[40] = EntityType.Unit;
            stateStore.UnitRolesByEntityId[10] = UnitRole.Player;
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(
                new Vector3(0.35f, 0.15f, 0f),
                Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(entityExitSignals: exitSignals),
                    topology,
                    Array.Empty<EntityState>()),
                topology,
                stateStore,
                projector,
                timingProfile: GameplayTimingProfile.CreateDefault());
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
            TickEntityExitPresentationSignal[] entityExitSignals = null)
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
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickEntityExitPresentationSignal CreateEnemyExitSignal(
            int entityId,
            TickEntityExitCause exitCause,
            SurfaceCell cell = default,
            CubeTopologyState topology = default,
            EntityType entityType = EntityType.Unit,
            int presentationSeed = 9127,
            EntityExitPresentationTiming timing = EntityExitPresentationTiming.Immediate,
            float visualContactNormalizedTime = 0f)
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
                Direction.Left,
                entityType,
                sourceActorEntityId: 10,
                presentationSeed: presentationSeed,
                timing: timing,
                visualContactNormalizedTime: visualContactNormalizedTime);
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
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

        private static VfxBindingDefinitionAsset CreateBinding(
            GameObject prefab,
            GameplayVfxCueId cueId,
            float tailSeconds,
            float defaultLifetimeSeconds = 0f)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", cueId.Family);
            SetField(binding, "cueCode", cueId.Code);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(
                binding,
                "visualSourceMode",
                cueId == GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)
                    ? VfxVisualSourceMode.PrefabWithSourceClone
                    : VfxVisualSourceMode.PrefabOnly);
            SetField(binding, "hostRequirement", GameplayVfxHostRequirement.ExplicitPrefabRequired);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", defaultLifetimeSeconds);
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

        private static GameObject CreateRuntimePrefab(string name)
        {
            var prefab = new GameObject(name);
            var modelRoot = new GameObject("ModelRoot");
            modelRoot.transform.SetParent(prefab.transform, worldPositionStays: false);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(modelRoot.transform, worldPositionStays: false);
            visual.transform.localScale = Vector3.one * 0.35f;
            return prefab;
        }

        private static GameObject CreateCameraObject(string name)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.identity;
            return cameraObject;
        }

        private static InactiveSourceViewFixture CreateInactiveSourceView(
            GameplayPresentationStateStore stateStore,
            int entityId)
        {
            var owner = new GameObject("EnemyDeathMotionInactiveSourceView");
            var view = owner.AddComponent<GameplayEntityView>();
            view.Initialize(entityId);
            var modelRoot = view.EnsureModelRoot();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(modelRoot, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();
            var shader = Shader.Find("Game/Enemy/CustomEnemyLit");
            Assert.That(shader, Is.Not.Null, "Game/Enemy/CustomEnemyLit shader is required for inactive visual snapshot tests.");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.color = Color.white;
            }

            renderer.sharedMaterial = material;
            stateStore.ViewsByEntityId[entityId] = view;
            return new InactiveSourceViewFixture(owner, renderer, material);
        }

        private static void ApplyInactivePropertyBlock(
            Renderer renderer,
            float inactiveBlend,
            float inactiveNoiseReveal,
            float desaturateStrength,
            float emissionSuppression,
            Color inactiveTint)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(InactiveBlendProperty, inactiveBlend);
            block.SetFloat(InactiveNoiseRevealProperty, inactiveNoiseReveal);
            block.SetFloat(DesaturateStrengthProperty, desaturateStrength);
            block.SetFloat(EmissionSuppressionProperty, emissionSuppression);
            block.SetColor(InactiveTintProperty, inactiveTint);
            renderer.SetPropertyBlock(block);
        }

        private static Transform FindParameterizedMotionClone(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == "ParameterizedMotionCloneRoot")
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var childResult = FindParameterizedMotionClone(root.GetChild(i));
                if (childResult != null)
                {
                    return childResult;
                }
            }

            return null;
        }

        private static void AssertColorApproximately(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }

        private static void AssertForbiddenAuthorityTokensAbsent(string source)
        {
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

        private static string ReadRepoFile(string path)
        {
            return File.ReadAllText(path);
        }

        private static bool HasComponentTypeNamed(GameObject root, string typeName)
        {
            var components = root.GetComponentsInChildren<Component>(includeInactive: true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
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

        private readonly struct BuilderFixture
        {
            public BuilderFixture(
                GameObject localSpaceRoot,
                GameObject cameraObject,
                Camera camera,
                GameplayTimingProfile timingProfile,
                GameplayPoseResolver poseResolver,
                GameplayCubeProjector projector,
                IEnemyDeathMotionTargetResolver targetResolver,
                GameplayPresentationStateStore stateStore,
                GameplayEntityPose playerPose)
            {
                LocalSpaceRoot = localSpaceRoot;
                CameraObject = cameraObject;
                Camera = camera;
                TimingProfile = timingProfile;
                PoseResolver = poseResolver;
                Projector = projector;
                TargetResolver = targetResolver;
                StateStore = stateStore;
                PlayerPose = playerPose;
            }

            public GameObject LocalSpaceRoot { get; }

            private GameObject CameraObject { get; }

            public Camera Camera { get; }

            public GameplayTimingProfile TimingProfile { get; }

            public GameplayPoseResolver PoseResolver { get; }

            public GameplayCubeProjector Projector { get; }

            public IEnemyDeathMotionTargetResolver TargetResolver { get; }

            public GameplayPresentationStateStore StateStore { get; }

            public GameplayEntityPose PlayerPose { get; }

            public void Destroy()
            {
                GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests.Destroy(CameraObject, LocalSpaceRoot);
            }
        }

        private readonly struct PresenterScenario
        {
            public PresenterScenario(
                GameObject root,
                GameplayTickViewPresenter presenter,
                GameplayEntityViewRegistry registry,
                CubeTopologyState topology,
                SurfaceCell enemyCell)
            {
                Root = root;
                Presenter = presenter;
                Registry = registry;
                Topology = topology;
                EnemyCell = enemyCell;
            }

            public GameObject Root { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameplayEntityViewRegistry Registry { get; }

            public CubeTopologyState Topology { get; }

            public SurfaceCell EnemyCell { get; }

            public void Destroy()
            {
                GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests.Destroy(Root);
            }
        }

        private readonly struct InactiveSourceViewFixture
        {
            public InactiveSourceViewFixture(GameObject owner, Renderer renderer, Material material)
            {
                Owner = owner;
                Renderer = renderer;
                Material = material;
            }

            public GameObject Owner { get; }

            public Renderer Renderer { get; }

            private Material Material { get; }

            public void Destroy()
            {
                GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests.Destroy(Material, Owner);
            }
        }
    }
}
