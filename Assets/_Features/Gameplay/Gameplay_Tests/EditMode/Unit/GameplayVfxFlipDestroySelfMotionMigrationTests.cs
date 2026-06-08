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
    public sealed class GameplayVfxFlipDestroySelfSourceCloneMotionTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string MotionPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FlipDestroySelfMotionVfx.prefab";
        private const string MotionBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FlipDestroySelfMotion_Binding.asset";
        private const string CommandPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/FlipDestroySelfMotionVfxCommandBuilder.cs";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string ExitControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string TickPresentationDataPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs";

        [Test]
        [Category("Extended")]
        public void DestroySelfSignal_BuildsMotionCommand()
        {
            var fixture = CreateBuilderFixture();
            var signal = CreateSignal(FlipImpactPresentationDisposition.DestroySelf);

            var result = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                signal,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            Assert.That(result, Is.True);
            Assert.That(command.BoxEntityId, Is.EqualTo(signal.BoxEntityId));
            Assert.That(command.SourceActionPlanId, Is.EqualTo(signal.SourceActionPlanId));
            Assert.That(command.ActorEntityId, Is.EqualTo(signal.ActorEntityId));
            Assert.That(command.ImpactTargetEntityId, Is.EqualTo(signal.ImpactTargetEntityId));
        }

        [Test]
        [Category("Extended")]
        public void StaySignal_DoesNotBuildMotionCommand()
        {
            var fixture = CreateBuilderFixture();

            var result = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                CreateSignal(FlipImpactPresentationDisposition.Stay),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotion_StayStillExcluded()
        {
            var fixture = CreateBuilderFixture();

            var result = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                CreateSignal(FlipImpactPresentationDisposition.Stay),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void InvalidBoxId_ReturnsFalse()
        {
            var fixture = CreateBuilderFixture();

            var result = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                CreateSignal(FlipImpactPresentationDisposition.DestroySelf, boxEntityId: 0),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SourceAndImpactPose_MatchPoseResolver()
        {
            var fixture = CreateBuilderFixture();
            var signal = CreateSignal(FlipImpactPresentationDisposition.DestroySelf);

            var result = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                signal,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);
            fixture.PoseResolver.TryResolveFlipImpactSignalLocalPoses(
                fixture.Projector,
                signal,
                out var sourcePose,
                out var impactPose);

            Assert.That(result, Is.True);
            Assert.That(Vector3.Distance(command.SourceLocalPosition, sourcePose.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(command.SourceLocalRotation, sourcePose.Rotation), Is.LessThanOrEqualTo(0.001f));
            Assert.That(Vector3.Distance(command.ImpactLocalPosition, impactPose.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(command.ImpactLocalRotation, impactPose.Rotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void ContactThreshold_ComputesBreakAndFadeTiming()
        {
            var fixture = CreateBuilderFixture();

            FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                CreateSignal(FlipImpactPresentationDisposition.DestroySelf),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            Assert.That(command.BreakStartSeconds, Is.EqualTo(command.ContactNormalizedTime * command.FlightDurationSeconds).Within(0.0001f));
            Assert.That(command.FadeDurationSeconds, Is.EqualTo(command.FlightDurationSeconds - command.BreakStartSeconds).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedCommand_UsesFlipArcSampler()
        {
            var fixture = CreateBuilderFixture();

            FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                CreateSignal(FlipImpactPresentationDisposition.DestroySelf),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            var parameterized = command.ToParameterizedMotionVfxCommand();
            Assert.That(parameterized.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.FlipArc));
            Assert.That(parameterized.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.SourceCloneMotion));
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotion_UsesImpactDispositionAdmission_NotLiveSourceGate()
        {
            var fixture = CreateBuilderFixture();
            var signal = CreateSignal(FlipImpactPresentationDisposition.DestroySelf);

            var built = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                signal,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            Assert.That(
                built,
                Is.True,
                "FlipDestroySelfMotion is admitted from the impact disposition presentation fact, not a live source gate.");
            Assert.That(command.BoxEntityId, Is.EqualTo(signal.BoxEntityId));
            Assert.That(command.SourceCell, Is.EqualTo(signal.SourceCell));
            Assert.That(command.ImpactCell, Is.EqualTo(signal.ImpactCell));
            Assert.That(command.SourceLocalPosition, Is.Not.EqualTo(command.ImpactLocalPosition));

            var parameterized = command.ToParameterizedMotionVfxCommand();
            Assert.That(
                parameterized.CueId,
                Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion)),
                "Impact disposition admission must preserve the authored FlipDestroySelfMotion cue.");
            Assert.That(
                parameterized.CloneMode,
                Is.EqualTo(ParameterizedMotionVfxCloneMode.SourceCloneMotion),
                "Impact disposition admission must keep SourceCloneMotion instead of forcing PresentationOnly.");
            Assert.That(parameterized.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.FlipArc));
            Assert.That(parameterized.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.ScaleAndAlpha));
        }

        [Test]
        [Category("Extended")]
        public void PreservesSurfaceCellFaceTopologyAndFacing()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 2, 3);
            var impactCell = new SurfaceCell(FaceId.Front, 4, 5);
            var topology = new CubeTopologyState(FaceId.Front);
            var signal = CreateSignal(
                FlipImpactPresentationDisposition.DestroySelf,
                sourceCell: sourceCell,
                impactCell: impactCell,
                topology: topology);
            var fixture = CreateBuilderFixture();

            var built = FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                signal,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            Assert.That(built, Is.True);
            Assert.That(command.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(command.ImpactCell, Is.EqualTo(impactCell));
            Assert.That(command.SourceCell.face, Is.EqualTo(FaceId.Front));
            Assert.That(command.ImpactCell.face, Is.EqualTo(FaceId.Front));
            Assert.That(command.Topology, Is.EqualTo(signal.Topology));
            Assert.That(command.SourceFacing, Is.EqualTo(signal.SourceFacing));
            Assert.That(command.ImpactFacing, Is.EqualTo(signal.ImpactFacing));
        }

        [Test]
        [Category("Extended")]
        public void CommandBuilder_DoesNotReadAuthorityStateOrSnapshot()
        {
            AssertForbiddenAuthorityTokensAbsent(ReadRepoFile(CommandPath));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_FlipDestroySelfMotionFlag_DefaultsTrue()
        {
            var owner = new GameObject("FlipDestroySelfDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxFlipDestroySelfMotionMigration, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotionFlagOff_DisablesMotionVfxWithoutFallback()
        {
            var source = ReadRepoFile(ExitControllerPath);
            var owner = new GameObject("FlipDestroySelfFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = false;
                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.DestroySelf)));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                Assert.That(source, Does.Not.Contain("PlayFlipImpactDestroyEffect"));
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotionFlagOn_WithBinding_PlaysParameterizedMotionVfx()
        {
            var owner = new GameObject("FlipDestroySelfRuntime");
            var commonHost = CreateRuntimePrefab("FlipDestroySelfRuntimeCommonHost");
            var source = CreateSourceView("FlipDestroySelfRuntimeSource");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(null, BoxVfxCue.FlipDestroySelfMotion, tailSeconds: 0.18f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = false;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureCommonEmptyHostPrefab(commonHost);

                runtime.Present(CreateExtensionContext(source.View, CreateSignal(FlipImpactPresentationDisposition.DestroySelf)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.MissingSourceViewCount, Is.Zero);
                Assert.That(runtime.CommonHostUnavailableCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                source.Destroy();
                Destroy(cueMap, binding, commonHost, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void LegacyDestroySelfPath_Finalized()
        {
            var source = ReadRepoFile(ExitControllerPath);
            var runtimeSource = ReadRepoFile(ProductionRuntimePath);

            Assert.That(runtimeSource, Does.Not.Contain("SuppressLegacyFlipDestroySelfEffects"));
            Assert.That(source, Does.Not.Contain("suppressLegacyFlipDestroySelfEffects"));
            Assert.That(source, Does.Not.Contain("PlayFlipImpactDestroyEffect"));
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotion_MissingBinding_ReportsDiagnosticNoOp()
        {
            var owner = new GameObject("FlipDestroySelfMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = false;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;

                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.DestroySelf)));

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
        public void Flag_IndependentFromFlipImpactBurstFlag()
        {
            var owner = new GameObject("FlipDestroySelfFlagIndependence");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = false;
                runtime.EnableGameplayVfxFlipImpactBurstMigration = true;

                runtime.Present(CreateExtensionContext(CreateSignal(FlipImpactPresentationDisposition.DestroySelf)));

                Assert.That(runtime.EnableGameplayVfxFlipDestroySelfMotionMigration, Is.False);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactBurst_CanCoexist()
        {
            var owner = new GameObject("FlipDestroySelfBurstCoexist");
            var prefab = CreateRuntimePrefab("SharedBoxVfxPrefab");
            var commonHost = CreateRuntimePrefab("FlipDestroySelfBurstCoexistCommonHost");
            var source = CreateSourceView("FlipDestroySelfBurstCoexistSource");
            VfxBindingDefinitionAsset motionBinding = null;
            VfxBindingDefinitionAsset burstBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                motionBinding = CreateBinding(null, BoxVfxCue.FlipDestroySelfMotion, tailSeconds: 0.18f);
                burstBinding = CreateBinding(prefab, BoxVfxCue.FlipImpactBurst, tailSeconds: 0.2f);
                cueMap = CreateCueMap(motionBinding, burstBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;
                runtime.EnableGameplayVfxFlipImpactBurstMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureCommonEmptyHostPrefab(commonHost);

                runtime.Present(CreateExtensionContext(source.View, CreateSignal(FlipImpactPresentationDisposition.DestroySelf)));

                Assert.That(runtime.LastPlannedRequestCount, Is.LessThanOrEqualTo(2));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.LessThanOrEqualTo(1));

                runtime.UpdatePresentation(GameplayTimingProfile.CreateDefault().FlipMotionDurationSeconds);

                Assert.That(runtime.ActiveVfxInstanceCount, Is.LessThanOrEqualTo(2));
            }
            finally
            {
                source.Destroy();
                Destroy(cueMap, motionBinding, burstBinding, commonHost, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void StayBranch_Unaffected()
        {
            var owner = new GameObject("FlipDestroySelfStayUnaffected");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = false;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;

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
        public void Playback_StartsAtSourcePose_ReachesImpactAndReleasesAfterTail()
        {
            var source = CreateSourceView("FlipDestroySelfPlaybackSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.25f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                var command = CreateMotionCommand(flightDurationSeconds: 1f, contactNormalizedTime: 0.7f);
                var handle = poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, command);
                var instance = poolFixture.Root.OneShotRoot.GetChild(0);

                Assert.That(handle, Is.Not.Null);
                Assert.That(Vector3.Distance(instance.localPosition, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));

                poolFixture.TimeProvider.TimeSeconds = 0.5f;
                poolFixture.Pool.Advance(0.5f);
                Assert.That(Vector3.Distance(instance.localPosition, command.SourceLocalPosition), Is.GreaterThan(0.0001f));
                Assert.That(Vector3.Distance(instance.localPosition, command.ImpactLocalPosition), Is.GreaterThan(0.0001f));

                poolFixture.TimeProvider.TimeSeconds = 1f;
                poolFixture.Pool.Advance(0.5f);
                Assert.That(Vector3.Distance(instance.localPosition, command.ImpactLocalPosition), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(poolFixture.Pool.ActiveCount, Is.EqualTo(1));

                poolFixture.TimeProvider.TimeSeconds = 1.25f;
                poolFixture.Pool.Advance(0.25f);
                Assert.That(poolFixture.Pool.ActiveCount, Is.Zero);
                Assert.That(poolFixture.Pool.PooledCount, Is.EqualTo(1));
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Playback_BreakFade_StartsAtContactThreshold()
        {
            var source = CreateSourceView("FlipDestroySelfBreakFadeSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                var command = CreateMotionCommand(flightDurationSeconds: 1f, contactNormalizedTime: 0.7f);
                poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, command);
                var instance = poolFixture.Root.OneShotRoot.GetChild(0);

                poolFixture.TimeProvider.TimeSeconds = 0.69f;
                poolFixture.Pool.Advance(0.69f);
                Assert.That(instance.localScale, Is.EqualTo(Vector3.one));

                poolFixture.TimeProvider.TimeSeconds = 0.75f;
                poolFixture.Pool.Advance(0.06f);
                Assert.That(instance.localScale.z, Is.LessThan(1f));
                Assert.That(instance.localScale.x, Is.GreaterThan(1f));
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void HardCleanup_ClearsActiveMotionInstances()
        {
            var source = CreateSourceView("FlipDestroySelfHardCleanupSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.25f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                var handle = poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());
                var instance = poolFixture.Root.OneShotRoot.GetChild(0).gameObject;

                poolFixture.Pool.HardCleanupAll();

                Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.HardCleanup));
                Assert.That(instance == null, Is.True);
                Assert.That(poolFixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_Plays_WithSourceClone_WhenPrefabIsMissing()
        {
            var source = CreateSourceView("FlipDestroySelfSourceCloneSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                var handle = poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());

                Assert.That(handle, Is.Not.Null);
                Assert.That(poolFixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(poolFixture.Pool.MissingPrefabCount, Is.Zero);
                Assert.That(poolFixture.Pool.MissingSourceViewCount, Is.Zero);
                Assert.That(poolFixture.Pool.CommonHostUnavailableCount, Is.Zero);
                Assert.That(poolFixture.Root.OneShotRoot.GetChild(0).Find("ParameterizedMotionCloneRoot"), Is.Not.Null);
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_UsesCommonHost_WhenCuePrefabIsNull()
        {
            var source = CreateSourceView("FlipDestroySelfCommonHostSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                var handle = poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());
                var instance = poolFixture.Root.OneShotRoot.GetChild(0);

                Assert.That(handle, Is.Not.Null);
                Assert.That(instance.name, Does.Contain("FlipDestroySelfCommonHost"));
                Assert.That(instance.Find("ParameterizedMotionCloneRoot"), Is.Not.Null);
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_ReportsMissingSourceView_WhenSourceViewIsUnavailable()
        {
            var poolFixture = CreatePoolFixture(tailSeconds: 0.18f);
            try
            {
                var handle = poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());

                Assert.That(handle, Is.Null);
                Assert.That(poolFixture.Pool.MissingSourceViewCount, Is.EqualTo(1));
                Assert.That(poolFixture.Pool.MissingPrefabCount, Is.Zero);
                Assert.That(poolFixture.Pool.CommonHostUnavailableCount, Is.Zero);
            }
            finally
            {
                poolFixture.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_DoesNotReportMissingPrefab_ForSourceCloneMotion()
        {
            var source = CreateSourceView("FlipDestroySelfNoMissingPrefabSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());

                Assert.That(poolFixture.Pool.MissingPrefabCount, Is.Zero);
                Assert.That(poolFixture.Pool.MissingSourceViewCount, Is.Zero);
                Assert.That(poolFixture.Pool.CommonHostUnavailableCount, Is.Zero);
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_ReportsCommonHostUnavailable_WhenCommonHostIsMissing()
        {
            var source = CreateSourceView("FlipDestroySelfMissingCommonHostSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                commonHostAvailable: false);
            try
            {
                var handle = poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());

                Assert.That(handle, Is.Null);
                Assert.That(poolFixture.Pool.CommonHostUnavailableCount, Is.EqualTo(1));
                Assert.That(poolFixture.Pool.MissingPrefabCount, Is.Zero);
                Assert.That(poolFixture.Pool.MissingSourceViewCount, Is.Zero);
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_ReleasesHost_AfterLifetimeAndTail()
        {
            var source = CreateSourceView("FlipDestroySelfReleaseSource");
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.25f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand(flightDurationSeconds: 1f));

                poolFixture.TimeProvider.TimeSeconds = 1f;
                poolFixture.Pool.Advance(1f);
                Assert.That(poolFixture.Pool.ActiveCount, Is.EqualTo(1));

                poolFixture.TimeProvider.TimeSeconds = 1.25f;
                poolFixture.Pool.Advance(0.25f);
                Assert.That(poolFixture.Pool.ActiveCount, Is.Zero);
                Assert.That(poolFixture.Pool.PooledCount, Is.EqualTo(1));
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FlipDestroySelfMotion_DoesNotMutateOriginalView()
        {
            var source = CreateSourceView("FlipDestroySelfOriginalStableSource");
            var originalPosition = source.ModelRoot.localPosition;
            var originalRotation = source.ModelRoot.localRotation;
            var originalScale = source.ModelRoot.localScale;
            var poolFixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot));
            try
            {
                poolFixture.Pool.PlayFlipDestroySelfMotion(poolFixture.PlaybackCommand, CreateMotionCommand());

                poolFixture.TimeProvider.TimeSeconds = 0.9f;
                poolFixture.Pool.Advance(0.9f);

                Assert.That(source.ModelRoot.localPosition, Is.EqualTo(originalPosition));
                Assert.That(source.ModelRoot.localRotation, Is.EqualTo(originalRotation));
                Assert.That(source.ModelRoot.localScale, Is.EqualTo(originalScale));
            }
            finally
            {
                poolFixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotion_AuthoringUsesCommonHostAndNoCuePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotionPrefabPath);

            Assert.That(prefab, Is.Null, MotionPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotion_BindingPolicy_IsSourceCloneMotionWithCommonHost()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(MotionBindingPath);

            Assert.That(binding, Is.Not.Null, MotionBindingPath);
            Assert.That(binding.Prefab, Is.Null);
            Assert.That(binding.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion));
            Assert.That(binding.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.CommonHostAllowed));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(8));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesSourceCloneMotionCue()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion), out var policy),
                Is.True);
            Assert.That(policy.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion));
            Assert.That(policy.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.CommonHostAllowed));
            Assert.That(cueMap.TryResolvePrefab(GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SourceProfile_OverridesHostDefault()
        {
            var owner = new GameObject("FlipDestroySelfProfileOverride");
            var hostPrefab = CreateRuntimePrefab("HostMotionPrefab");
            var profilePrefab = CreateRuntimePrefab("ProfileMotionPrefab");
            var commonHost = CreateRuntimePrefab("ProfileOverrideCommonHost");
            var source = CreateSourceView("ProfileOverrideSource");
            VfxBindingDefinitionAsset hostBinding = null;
            VfxBindingDefinitionAsset profileBinding = null;
            VfxCueMapAsset cueMap = null;
            VfxProfileAsset profile = null;
            try
            {
                hostBinding = CreateBinding(hostPrefab, BoxVfxCue.FlipDestroySelfMotion, tailSeconds: 0.18f);
                profileBinding = CreateBinding(profilePrefab, BoxVfxCue.FlipDestroySelfMotion, tailSeconds: 0.18f);
                cueMap = CreateCueMap(hostBinding);
                profile = CreateProfile(profileBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxFlipImpactBurstMigration = false;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureFamilyProfiles(new[] { profile });
                runtime.ConfigureCommonEmptyHostPrefab(commonHost);

                runtime.Present(CreateExtensionContext(source.View, CreateSignal(FlipImpactPresentationDisposition.DestroySelf)));

                var spawned = owner.GetComponentInChildren<Transform>();
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(spawned, Is.Not.Null);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
            }
            finally
            {
                source.Destroy();
                Destroy(profile, cueMap, hostBinding, profileBinding, commonHost, hostPrefab, profilePrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void NoTickPresentationShapeChange()
        {
            var source = ReadRepoFile(TickPresentationDataPath);

            Assert.That(source, Does.Not.Contain("FlipDestroySelfMotionVfxCommand"));
            Assert.That(source, Does.Not.Contain("Vector3"));
            Assert.That(source, Does.Not.Contain("Quaternion"));
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
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                VfxAnchor.ForMotionTrack(30),
                VfxTimingKind.AtMotionContact);

            var result = resolver.TryResolve(request, out var resolved);

            Assert.That(result, Is.False);
            Assert.That(resolved.IsResolved, Is.False);
        }

        private static BuilderFixture CreateBuilderFixture()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[30] = EntityType.Box;
            var trackState = new GameplayPresentationTrackState();
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(8, 8)),
                1f);
            return new BuilderFixture(
                GameplayTimingProfile.CreateDefault(),
                new GameplayMotionTimingResolver(stateStore, trackState),
                new GameplayPoseResolver(stateStore, trackState),
                projector);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params FlipImpactPresentationSignal[] flipImpactSignals)
        {
            return CreateExtensionContext(null, flipImpactSignals);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            GameplayEntityView sourceView,
            params FlipImpactPresentationSignal[] flipImpactSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[30] = EntityType.Box;
            if (sourceView != null)
            {
                stateStore.ViewsByEntityId[30] = sourceView;
            }

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
            SurfaceCell? impactCell = null,
            CubeTopologyState? topology = null)
        {
            return new FlipImpactPresentationSignal(
                sourceActionPlanId,
                boxEntityId,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                sourceCell ?? new SurfaceCell(FaceId.Floor, 1, 1),
                impactCell ?? new SurfaceCell(FaceId.Floor, 2, 1),
                topology ?? new CubeTopologyState(FaceId.Floor),
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

        private static FlipDestroySelfMotionVfxCommand CreateMotionCommand(
            float flightDurationSeconds = 1f,
            float contactNormalizedTime = 0.7f)
        {
            var breakStartSeconds = flightDurationSeconds * contactNormalizedTime;
            return new FlipDestroySelfMotionVfxCommand(
                sourceActionPlanId: 7,
                boxEntityId: 30,
                actorEntityId: 10,
                impactTargetEntityId: 40,
                sourceCell: new SurfaceCell(FaceId.Floor, 1, 1),
                impactCell: new SurfaceCell(FaceId.Floor, 2, 1),
                topology: new CubeTopologyState(FaceId.Floor),
                sourceFacing: Direction.Right,
                impactFacing: Direction.Left,
                sourceLocalPosition: Vector3.zero,
                sourceLocalRotation: Quaternion.identity,
                impactLocalPosition: new Vector3(2f, 0f, 0f),
                impactLocalRotation: Quaternion.AngleAxis(180f, Vector3.up),
                flightDurationSeconds: flightDurationSeconds,
                contactNormalizedTime: contactNormalizedTime,
                breakStartSeconds: breakStartSeconds,
                fadeDurationSeconds: flightDurationSeconds - breakStartSeconds,
                arcHeight: 0.6f,
                presentationSeed: 7);
        }

        private static PoolFixture CreatePoolFixture(
            float tailSeconds,
            IGameplayVfxCloneSourceProvider cloneSourceProvider = null,
            bool commonHostAvailable = true)
        {
            var owner = new GameObject("FlipDestroySelfPoolOwner");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var commonHost = commonHostAvailable
                ? CreateRuntimePrefab("FlipDestroySelfCommonHost")
                : null;
            var prefabProvider = new SinglePrefabProvider(commonHost);
            var timeProvider = new FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(root, prefabProvider, timeProvider, cloneSourceProvider);
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion);
            var request = new GameplayVfxRequest(
                1,
                7,
                7,
                sourceEntityId: 30,
                cueId,
                VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0f,
                tailSeconds: tailSeconds,
                maxConcurrentInstances: 8,
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);
            var anchor = VfxResolvedAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellCenter,
                Vector3.zero,
                Quaternion.identity);
            return new PoolFixture(
                owner,
                commonHost,
                root,
                pool,
                timeProvider,
                new ResolvedVfxPlaybackCommand(request, policy, anchor));
        }

        private static GameObject CreateRuntimePrefab(string name)
        {
            var prefab = new GameObject(name);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(prefab.transform, worldPositionStays: false);
            visual.transform.localScale = Vector3.one * 0.35f;
            return prefab;
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            GameObject prefab,
            BoxVfxCue cue,
            float tailSeconds)
        {
            if (prefab != null)
            {
                GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);
            }

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Box);
            SetField(binding, "cueCode", (int)cue);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            var isSourceCloneMotion =
                cue == BoxVfxCue.FlipDestroySelfMotion ||
                cue == BoxVfxCue.DestroyShrink;
            SetField(
                binding,
                "visualSourceMode",
                isSourceCloneMotion
                    ? VfxVisualSourceMode.SourceCloneMotion
                    : VfxVisualSourceMode.PrefabOnly);
            SetField(
                binding,
                "hostRequirement",
                isSourceCloneMotion
                    ? GameplayVfxHostRequirement.CommonHostAllowed
                    : GameplayVfxHostRequirement.ExplicitPrefabRequired);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", isSourceCloneMotion ? 0f : tailSeconds);
            SetField(binding, "tailSeconds", tailSeconds);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 8);
            return binding;
        }

        private static SourceViewFixture CreateSourceView(string name)
        {
            var owner = new GameObject(name);
            var view = owner.AddComponent<GameplayEntityView>();
            view.Initialize(30);
            var modelRoot = view.EnsureModelRoot();
            modelRoot.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            modelRoot.localRotation = Quaternion.AngleAxis(15f, Vector3.up);
            modelRoot.localScale = new Vector3(1.2f, 0.9f, 1.1f);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(modelRoot, worldPositionStays: false);
            visual.transform.localScale = Vector3.one * 0.35f;
            return new SourceViewFixture(owner, view, modelRoot);
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static VfxProfileAsset CreateProfile(params VfxBindingDefinitionAsset[] bindings)
        {
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            SetField(profile, "family", GameplayVfxFamily.Box);
            SetField(profile, "bindings", bindings);
            return profile;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
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

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath)).Replace("\r\n", "\n");
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
                GameplayTimingProfile timingProfile,
                GameplayMotionTimingResolver motionTimingResolver,
                GameplayPoseResolver poseResolver,
                GameplayCubeProjector projector)
            {
                TimingProfile = timingProfile;
                MotionTimingResolver = motionTimingResolver;
                PoseResolver = poseResolver;
                Projector = projector;
            }

            public GameplayTimingProfile TimingProfile { get; }

            public GameplayMotionTimingResolver MotionTimingResolver { get; }

            public GameplayPoseResolver PoseResolver { get; }

            public GameplayCubeProjector Projector { get; }
        }

        private readonly struct PoolFixture
        {
            public PoolFixture(
                GameObject owner,
                GameObject commonHost,
                GameplayVfxRuntimeRoot root,
                GameplayVfxGameObjectPool pool,
                FakeTimeProvider timeProvider,
                ResolvedVfxPlaybackCommand playbackCommand)
            {
                Owner = owner;
                CommonHost = commonHost;
                Root = root;
                Pool = pool;
                TimeProvider = timeProvider;
                PlaybackCommand = playbackCommand;
            }

            public GameObject Owner { get; }

            public GameObject CommonHost { get; }

            public GameplayVfxRuntimeRoot Root { get; }

            public GameplayVfxGameObjectPool Pool { get; }

            public FakeTimeProvider TimeProvider { get; }

            public ResolvedVfxPlaybackCommand PlaybackCommand { get; }

            public void Destroy()
            {
                Pool?.HardCleanupAll();
                GameplayVfxFlipDestroySelfSourceCloneMotionTests.Destroy(CommonHost, Owner);
            }
        }

        private readonly struct SourceViewFixture
        {
            public SourceViewFixture(GameObject owner, GameplayEntityView view, Transform modelRoot)
            {
                Owner = owner;
                View = view;
                ModelRoot = modelRoot;
            }

            public GameObject Owner { get; }

            public GameplayEntityView View { get; }

            public Transform ModelRoot { get; }

            public void Destroy()
            {
                GameplayVfxFlipDestroySelfSourceCloneMotionTests.Destroy(Owner);
            }
        }

        private sealed class SinglePrefabProvider : IVfxPrefabProvider
        {
            private readonly GameObject prefab;

            public SinglePrefabProvider(GameObject prefab)
            {
                this.prefab = prefab;
            }

            public bool TryResolvePrefab(in ResolvedVfxPlaybackCommand command, out GameObject resolvedPrefab)
            {
                resolvedPrefab = prefab;
                return resolvedPrefab != null;
            }
        }

        private sealed class SingleCloneSourceProvider : IGameplayVfxCloneSourceProvider
        {
            private readonly Transform modelRoot;

            public SingleCloneSourceProvider(Transform modelRoot)
            {
                this.modelRoot = modelRoot;
            }

            public bool TryResolveCloneSource(GameplayVfxCloneSourceKey key, out GameplayVfxCloneSource source)
            {
                if (modelRoot == null)
                {
                    source = default;
                    return false;
                }

                source = new GameplayVfxCloneSource(modelRoot);
                return true;
            }
        }

        private sealed class FakeTimeProvider : IGameplayVfxTimeProvider
        {
            public float TimeSeconds { get; set; }
        }

        private sealed class RejectingCellProjector : IGameplayVfxCellAnchorProjector
        {
            public bool TryResolveCell(
                SurfaceCell cell,
                CubeTopologyState topology,
                VfxAnchorSlot slot,
                GameplayVfxVisibilityMode visibilityMode,
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
