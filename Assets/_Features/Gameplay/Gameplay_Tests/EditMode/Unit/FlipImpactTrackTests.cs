using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PresentationMotionTrackTests
    {
        private const string ExitControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string PresentationMotionTrackPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs";
        private const string GameplayTrackPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTrackPlanner.cs";
        private const string StayCommandPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommand.cs";
        private const string StayCommandBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommandBuilder.cs";
        private const string VfxEnumsPath =
            "Assets/_Features/Gameplay/Gameplay_VfxContracts/Runtime/GameplayVfxEnums.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string GameplayEntityPresentationApplierPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs";
        private const string BoxFlipInteractionDriverPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs";

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_Stay_PreContactArcMatchesExpected()
        {
            var command = CreateCommand();
            var track = CreatePresentationTrack(command);
            var sampleTime = command.ContactNormalizedTime * 0.5f;
            var expectedPose = FlipArcSampler.Sample(
                command.SourcePose,
                command.ImpactPose,
                0.5f,
                command.ArcHeightWorld);

            track.Advance(command.DurationSeconds * sampleTime);
            var sample = track.Sample();

            AssertSamplePoseMatches(sample, expectedPose);
            Assert.That(sample.LocalPose.Position.x, Is.GreaterThan(command.SourceLocalPosition.x));
            Assert.That(sample.LocalPose.Position.x, Is.LessThan(command.ImpactLocalPosition.x));
            Assert.That(sample.LocalPose.Position.z, Is.LessThan(-0.01f));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_Stay_HoldWindowMatchesExpected()
        {
            var command = CreateCommand();
            var track = CreatePresentationTrack(command);
            var holdSampleTime = command.ContactNormalizedTime +
                                 (command.PostContactHoldNormalizedDuration * 0.5f);

            track.Advance(command.DurationSeconds * holdSampleTime);
            var sample = track.Sample();

            Assert.That(Vector3.Distance(sample.LocalPose.Position, command.ImpactLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalPose.Rotation, command.ImpactLocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_Stay_SquashScaleMatchesExpected()
        {
            var command = CreateCommand();
            var track = CreatePresentationTrack(command);
            var expectedScale = BoxMotionVisualScaleSampler.SampleFlipFlight(1f);

            track.Advance(command.DurationSeconds * command.ContactNormalizedTime);
            var scale = track.Sample().VisualScaleMultiplier;

            Assert.That(Vector3.Distance(scale, expectedScale), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(scale.x, Is.GreaterThan(1f));
            Assert.That(scale.y, Is.GreaterThan(1f));
            Assert.That(scale.z, Is.LessThan(1f));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_Stay_ReturnArcMatchesExpected()
        {
            var command = CreateCommand();
            var track = CreatePresentationTrack(command);
            var holdEndTime = command.ContactNormalizedTime + command.PostContactHoldNormalizedDuration;
            var returnMidpointTime = holdEndTime + ((1f - holdEndTime) * 0.5f);
            var expectedPose = FlipArcSampler.Sample(
                command.ImpactPose,
                command.SourcePose,
                0.5f,
                command.ArcHeightWorld * command.ReturnArcMultiplier);

            track.Advance(command.DurationSeconds * returnMidpointTime);
            var sample = track.Sample();

            AssertSamplePoseMatches(sample, expectedPose);
            Assert.That(sample.LocalPose.Position.x, Is.GreaterThan(command.SourceLocalPosition.x));
            Assert.That(sample.LocalPose.Position.x, Is.LessThan(command.ImpactLocalPosition.x));
            Assert.That(sample.LocalPose.Position.z, Is.GreaterThan(0.01f));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_Stay_CompletesAtExactSourcePose()
        {
            var command = CreateCommand();
            var track = CreatePresentationTrack(command);

            track.Advance(command.DurationSeconds);
            var sample = track.Sample();

            Assert.That(track.IsComplete, Is.True);
            Assert.That(sample.IsComplete, Is.True);
            Assert.That(Vector3.Distance(sample.LocalPose.Position, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalPose.Rotation, command.SourceLocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_Stay_ReportsCompletionPoseAndResetScale()
        {
            var command = CreateCommand();
            var track = CreatePresentationTrack(command);

            track.Advance(command.DurationSeconds);
            var sample = track.Sample();

            Assert.That(sample.IsComplete, Is.True);
            Assert.That(Vector3.Distance(sample.CompletionPose.Position, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.CompletionPose.Rotation, command.SourceLocalRotation), Is.LessThanOrEqualTo(0.001f));
            Assert.That(Vector3.Distance(sample.VisualScaleMultiplier, Vector3.one), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(sample.SuppressBoxInteractionOverlay, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionInstanceKey_CreateFlipImpactStay_IsStable()
        {
            var actionPlanCommand = CreateCommand(sourceActionPlanId: 7, presentationSeed: 99);
            var fallbackCommand = CreateCommand(sourceActionPlanId: 0, presentationSeed: 99);
            var actionPlanSignal = CreateSignal(FlipImpactPresentationDisposition.Stay, sourceActionPlanId: 7);
            var fallbackSignal = CreateSignal(FlipImpactPresentationDisposition.Stay, sourceActionPlanId: 0);

            Assert.That(
                PresentationMotionInstanceKey.CreateFlipImpactStay(actionPlanCommand),
                Is.EqualTo(new PresentationMotionInstanceKey(
                    PresentationMotionKind.FlipImpactStay,
                    7,
                    actionPlanCommand.BoxEntityId,
                    usesTickFallback: false)));
            Assert.That(
                PresentationMotionInstanceKey.CreateFlipImpactStay(fallbackCommand),
                Is.EqualTo(new PresentationMotionInstanceKey(
                    PresentationMotionKind.FlipImpactStay,
                    99,
                    fallbackCommand.BoxEntityId,
                    usesTickFallback: true)));
            Assert.That(
                PresentationMotionInstanceKey.CreateFlipImpactStay(actionPlanSignal, tickIndexFallback: 55),
                Is.EqualTo(new PresentationMotionInstanceKey(
                    PresentationMotionKind.FlipImpactStay,
                    7,
                    actionPlanSignal.BoxEntityId,
                    usesTickFallback: false)));
            Assert.That(
                PresentationMotionInstanceKey.CreateFlipImpactStay(fallbackSignal, tickIndexFallback: 55),
                Is.EqualTo(new PresentationMotionInstanceKey(
                    PresentationMotionKind.FlipImpactStay,
                    55,
                    fallbackSignal.BoxEntityId,
                    usesTickFallback: true)));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayMotionCommandAdapter_BuildsGenericPresentationMotionCommand()
        {
            var command = CreateCommand(sourceActionPlanId: 0, presentationSeed: 99);

            var presentationCommand =
                FlipImpactStayPresentationMotionCommandAdapter.ToPresentationMotionCommand(command);

            Assert.That(presentationCommand.EntityId, Is.EqualTo(command.BoxEntityId));
            Assert.That(presentationCommand.Kind, Is.EqualTo(PresentationMotionKind.FlipImpactStay));
            Assert.That(
                presentationCommand.InstanceKey,
                Is.EqualTo(new PresentationMotionInstanceKey(
                    PresentationMotionKind.FlipImpactStay,
                    99,
                    command.BoxEntityId,
                    usesTickFallback: true)));
            Assert.That(presentationCommand.Phases.Count, Is.EqualTo(3));
            Assert.That(presentationCommand.Phases.Phase0.Kind, Is.EqualTo(PresentationMotionPhaseKind.Arc));
            Assert.That(presentationCommand.Phases.Phase1.Kind, Is.EqualTo(PresentationMotionPhaseKind.Hold));
            Assert.That(presentationCommand.Phases.Phase2.Kind, Is.EqualTo(PresentationMotionPhaseKind.Arc));
            Assert.That(presentationCommand.ScalePolicy, Is.EqualTo(PresentationMotionScalePolicy.FlipImpactStay));
            Assert.That(
                presentationCommand.InteractionPolicy,
                Is.EqualTo(PresentationMotionInteractionPolicy.SuppressBoxInteractionOverlay));
            Assert.That(presentationCommand.HasRequiredFinalCell, Is.True);
            Assert.That(presentationCommand.RequiredFinalCell, Is.EqualTo(command.SourceCell));
            Assert.That(Vector3.Distance(presentationCommand.CompletionPose.Position, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_FlipImpactStay_ReturnArcMultiplierZero_ReturnsWithoutArcLift()
        {
            var command = CreateCommand(returnArcMultiplier: 0f);
            var track = PresentationMotionTrack.CreateFlipImpactStay(command);
            var holdEndTime = command.ContactNormalizedTime + command.PostContactHoldNormalizedDuration;
            var returnMidpointTime = holdEndTime + ((1f - holdEndTime) * 0.5f);
            var expectedPose = FlipArcSampler.Sample(
                command.ImpactPose,
                command.SourcePose,
                0.5f,
                0f);

            track.Advance(command.DurationSeconds * returnMidpointTime);
            var sample = track.Sample();

            AssertSamplePoseMatches(sample, expectedPose);
            Assert.That(sample.LocalPose.Position.x, Is.GreaterThan(command.SourceLocalPosition.x));
            Assert.That(sample.LocalPose.Position.x, Is.LessThan(command.ImpactLocalPosition.x));
            Assert.That(Mathf.Abs(sample.LocalPose.Position.z), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(sample.SuppressBoxInteractionOverlay, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayMotionCommandBuilder_BuildsFromStaySignal()
        {
            var fixture = CreateBuilderFixture();
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay);

            var result = FlipImpactStayMotionCommandBuilder.TryBuild(
                signal,
                tickIndexFallback: 55,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            Assert.That(result, Is.True);
            Assert.That(command.BoxEntityId, Is.EqualTo(signal.BoxEntityId));
            Assert.That(command.SourceActionPlanId, Is.EqualTo(signal.SourceActionPlanId));
            Assert.That(command.PresentationSeed, Is.EqualTo(signal.SourceActionPlanId));
            Assert.That(command.ArcHeightWorld, Is.EqualTo(fixture.TimingProfile.FlipArcHeightInCells * fixture.Projector.CellSize).Within(0.0001f));
            Assert.That(command.DurationSeconds, Is.EqualTo(fixture.MotionTimingResolver.ResolveMotionDurationSeconds(
                signal.BoxEntityId,
                TickEntityMotionKind.Flip,
                fixture.TimingProfile)).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayMotionCommandBuilder_RejectsDestroySelf()
        {
            var fixture = CreateBuilderFixture();

            var result = FlipImpactStayMotionCommandBuilder.TryBuild(
                CreateSignal(FlipImpactPresentationDisposition.DestroySelf),
                tickIndexFallback: 55,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                out var command);

            Assert.That(result, Is.False);
            Assert.That(command.BoxEntityId, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void GameplayExitPresentationController_DestroySelfBookkeeping_DoesNotUsePresentationMotionKey()
        {
            var exitController = ReadRepoFile(ExitControllerPath);

            Assert.That(exitController, Does.Contain("HashSet<int> _entitiesWithDestroySelfFlipImpact"));
            Assert.That(exitController, Does.Contain("_entitiesWithDestroySelfFlipImpact.Add(signal.BoxEntityId)"));
            Assert.That(exitController, Does.Not.Contain("PresentationMotionInstanceKey"));
            Assert.That(exitController, Does.Not.Contain("FlipImpactInstanceKey"));
        }

        [Test]
        [Category("Extended")]
        public void NoFlipImpactTrackAdapterSurface_Remains()
        {
            Assert.That(File.Exists(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs")), Is.False);
            Assert.That(File.Exists(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs.meta")), Is.False);

            var productionSource = ReadRepoFile(ExitControllerPath) + "\n" +
                                   ReadRepoFile(PresentationMotionTrackPath) + "\n" +
                                   ReadRepoFile(GameplayTrackPlannerPath) + "\n" +
                                   ReadRepoFile(GameplayEntityPresentationApplierPath);

            Assert.That(productionSource, Does.Not.Contain("FlipImpactTrack"));
            Assert.That(productionSource, Does.Not.Contain("FlipImpactInstanceKey"));
            Assert.That(productionSource, Does.Not.Contain("ToFlipImpactInstanceKey"));
        }

        [Test]
        [Category("Extended")]
        public void RetainedOriginalViewMotionOwners_Remain()
        {
            var presentationTrack = ReadRepoFile(PresentationMotionTrackPath);
            var planner = ReadRepoFile(GameplayTrackPlannerPath);
            var applier = ReadRepoFile(GameplayEntityPresentationApplierPath);
            var boxFlipInteractionDriver = ReadRepoFile(BoxFlipInteractionDriverPath);

            Assert.That(presentationTrack, Does.Contain("internal sealed class PresentationMotionTrack"));
            Assert.That(presentationTrack, Does.Contain("PresentationMotionSample"));
            Assert.That(planner, Does.Contain("OriginalViewMotionTracks"));
            Assert.That(planner, Does.Contain("CompletedPresentationMotionKeys"));
            Assert.That(planner, Does.Contain("PresentationMotionTrack.CreateFlipImpactStay(command)"));
            Assert.That(applier, Does.Contain("HasSuppressingOriginalViewMotion(track.BoxEntityId)"));
            Assert.That(boxFlipInteractionDriver, Does.Contain("BoxFlipInteractionDriver"));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayMotionCommand_PreservesSourceAndImpactCells()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 1, 2);
            var impactCell = new SurfaceCell(FaceId.Back, 3, 4);
            var command = CreateCommand(sourceCell: sourceCell, impactCell: impactCell);

            Assert.That(command.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(command.ImpactCell, Is.EqualTo(impactCell));
            Assert.That(command.SourceCell.face, Is.EqualTo(FaceId.Front));
            Assert.That(command.ImpactCell.face, Is.EqualTo(FaceId.Back));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayMotionCommand_PreservesTimingValues()
        {
            var command = CreateCommand(
                durationSeconds: 1.25f,
                contactNormalizedTime: 0.62f,
                postContactHoldNormalizedDuration: 0.18f,
                returnArcMultiplier: 0.55f,
                arcHeightWorld: 0.75f);

            Assert.That(command.DurationSeconds, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(command.ContactNormalizedTime, Is.EqualTo(0.62f).Within(0.0001f));
            Assert.That(command.PostContactHoldNormalizedDuration, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(command.ReturnArcMultiplier, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(command.ArcHeightWorld, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayMotionCommandBuilder_DoesNotReadAuthorityOrVfxRuntime()
        {
            var source = ReadRepoFile(StayCommandPath) + "\n" + ReadRepoFile(StayCommandBuilderPath);

            AssertForbiddenTokensAbsent(source);
        }

        [Test]
        [Category("Extended")]
        public void NoTickPresentationDataShapeChange()
        {
            var propertyNames = typeof(FlipImpactPresentationSignal)
                .GetProperties()
                .Select(property => property.Name);

            Assert.That(
                propertyNames,
                Is.EquivalentTo(new[]
                {
                    nameof(FlipImpactPresentationSignal.SourceActionPlanId),
                    nameof(FlipImpactPresentationSignal.BoxEntityId),
                    nameof(FlipImpactPresentationSignal.ImpactTargetEntityId),
                    nameof(FlipImpactPresentationSignal.ActorEntityId),
                    nameof(FlipImpactPresentationSignal.SourceCell),
                    nameof(FlipImpactPresentationSignal.ImpactCell),
                    nameof(FlipImpactPresentationSignal.Topology),
                    nameof(FlipImpactPresentationSignal.SourceFacing),
                    nameof(FlipImpactPresentationSignal.ImpactFacing),
                    nameof(FlipImpactPresentationSignal.HasLandingCell),
                    nameof(FlipImpactPresentationSignal.LandingCell),
                    nameof(FlipImpactPresentationSignal.Disposition),
                }));
        }

        [Test]
        [Category("Extended")]
        public void NoGameplayVfxStayMotionCueAdded()
        {
            var vfxEnums = ReadRepoFile(VfxEnumsPath);
            var productionRuntime = ReadRepoFile(VfxProductionRuntimePath);

            Assert.That(vfxEnums, Does.Not.Contain("FlipImpactStayMotion"));
            Assert.That(productionRuntime, Does.Not.Contain("FlipImpactStayMotion"));
        }

        [Test]
        [Category("Extended")]
        public void StayMotionCommand_DoesNotUseParameterizedMotionVfxCommand()
        {
            var source = ReadRepoFile(StayCommandPath) + "\n" + ReadRepoFile(StayCommandBuilderPath);

            Assert.That(source, Does.Not.Contain("ParameterizedMotionVfxCommand"));
            Assert.That(source, Does.Not.Contain("GameplayVfxProductionRuntime"));
        }

        [Test]
        [Category("Extended")]
        public void BoxFlipInteractionDriver_SuppressedOnlyForSuppressingOriginalViewMotion()
        {
            var applier = ReadRepoFile(GameplayEntityPresentationApplierPath);

            Assert.That(applier, Does.Contain("HasSuppressingOriginalViewMotion(track.BoxEntityId)"));
            Assert.That(applier, Does.Contain("_trackState.OriginalViewMotionTracks.TryGetValue(entityId, out var track)"));
            Assert.That(applier, Does.Contain("PresentationMotionInteractionPolicy.SuppressBoxInteractionOverlay"));
            Assert.That(applier, Does.Not.Contain("FlipImpactBurst"));
            Assert.That(applier, Does.Not.Contain("BoxVfxCue"));
            Assert.That(applier, Does.Not.Contain("GameplayVfxProductionRuntime"));
        }

        [Test]
        [Category("Extended")]
        public void BoxFlipInteractionDriver_ResetRegression()
        {
            var owner = new GameObject("BoxFlipInteractionDriver_ResetRegression");
            try
            {
                var visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(owner.transform, worldPositionStays: false);
                visualRoot.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
                visualRoot.localRotation = Quaternion.Euler(5f, 10f, 15f);
                var basePosition = visualRoot.localPosition;
                var baseRotation = visualRoot.localRotation;

                var driver = owner.AddComponent<BoxFlipInteractionDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "visualRoot", visualRoot);

                driver.ApplyInteraction(new Vector3(0.4f, 0.5f, 0.6f), Quaternion.Euler(20f, 30f, 40f), 1f);
                Assert.That(Vector3.Distance(visualRoot.localPosition, basePosition), Is.GreaterThan(0.01f));

                driver.ResetInteraction();

                Assert.That(Vector3.Distance(visualRoot.localPosition, basePosition), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Quaternion.Angle(visualRoot.localRotation, baseRotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static PresentationMotionTrack CreatePresentationTrack(in FlipImpactStayMotionCommand command)
        {
            return PresentationMotionTrack.CreateFlipImpactStay(command);
        }

        private static void AssertSamplePoseMatches(PresentationMotionSample sample, GameplayEntityPose expectedPose)
        {
            Assert.That(Vector3.Distance(sample.LocalPose.Position, expectedPose.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalPose.Rotation, expectedPose.Rotation), Is.LessThanOrEqualTo(0.001f));
        }

        private static FlipImpactStayMotionCommand CreateCommand(
            int sourceActionPlanId = 7,
            int presentationSeed = 7,
            SurfaceCell? sourceCell = null,
            SurfaceCell? impactCell = null,
            float durationSeconds = 1f,
            float contactNormalizedTime = 0.70f,
            float postContactHoldNormalizedDuration = 0.10f,
            float returnArcMultiplier = 0.35f,
            float arcHeightWorld = 0.6f)
        {
            return new FlipImpactStayMotionCommand(
                sourceActionPlanId,
                30,
                10,
                40,
                sourceCell ?? new SurfaceCell(FaceId.Floor, 0, 0),
                impactCell ?? new SurfaceCell(FaceId.Floor, 2, 0),
                new CubeTopologyState(FaceId.Floor),
                Direction.Left,
                Direction.Right,
                Vector3.zero,
                Quaternion.identity,
                new Vector3(2f, 0f, 0f),
                Quaternion.AngleAxis(180f, Vector3.up),
                durationSeconds,
                contactNormalizedTime,
                postContactHoldNormalizedDuration,
                returnArcMultiplier,
                arcHeightWorld,
                presentationSeed);
        }

        private static FlipImpactPresentationSignal CreateSignal(
            FlipImpactPresentationDisposition disposition,
            int sourceActionPlanId = 7)
        {
            return new FlipImpactPresentationSignal(
                sourceActionPlanId: sourceActionPlanId,
                boxEntityId: 30,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                impactCell: new SurfaceCell(FaceId.Floor, 2, 0),
                topology: new CubeTopologyState(FaceId.Floor),
                sourceFacing: Direction.Left,
                impactFacing: Direction.Right,
                disposition: disposition,
                hasLandingCell: false);
        }

        private static BuilderFixture CreateBuilderFixture()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);
            return new BuilderFixture(
                GameplayTimingProfile.CreateDefault(),
                new GameplayMotionTimingResolver(stateStore, trackState),
                new GameplayPoseResolver(stateStore, trackState),
                projector);
        }

        private static void AssertForbiddenTokensAbsent(string source)
        {
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "GameplayVfxProductionRuntime",
                "ParameterizedMotionVfxCommand",
                "BoxVfxCue",
                "EnemyVfxCue",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
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
    }
}
