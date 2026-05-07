using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class FlipImpactTrackTests
    {
        private const string StayCommandPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommand.cs";
        private const string StayCommandBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommandBuilder.cs";
        private const string VfxEnumsPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxEnums.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_SamplesPreContactArc()
        {
            var track = CreateTrack();
            var command = CreateCommand();

            track.Advance(command.DurationSeconds * command.ContactNormalizedTime * 0.5f);
            var sample = track.Sample();

            Assert.That(sample.Position.x, Is.GreaterThan(command.SourceLocalPosition.x));
            Assert.That(sample.Position.x, Is.LessThan(command.ImpactLocalPosition.x));
            Assert.That(sample.Position.z, Is.LessThan(-0.01f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_HoldsAtContactWindow()
        {
            var command = CreateCommand();
            var track = CreateTrack(command);
            var holdSampleTime = command.ContactNormalizedTime +
                                 (command.PostContactHoldNormalizedDuration * 0.5f);

            track.Advance(command.DurationSeconds * holdSampleTime);
            var sample = track.Sample();

            Assert.That(Vector3.Distance(sample.Position, command.ImpactLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.Rotation, command.ImpactLocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_ReturnsThroughArcToSource()
        {
            var command = CreateCommand();
            var track = CreateTrack(command);
            var holdEndTime = command.ContactNormalizedTime + command.PostContactHoldNormalizedDuration;
            var returnMidpointTime = holdEndTime + ((1f - holdEndTime) * 0.5f);

            track.Advance(command.DurationSeconds * returnMidpointTime);
            var sample = track.Sample();

            Assert.That(sample.Position.x, Is.GreaterThan(command.SourceLocalPosition.x));
            Assert.That(sample.Position.x, Is.LessThan(command.ImpactLocalPosition.x));
            Assert.That(sample.Position.z, Is.GreaterThan(0.01f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_ReturnsToExactSourcePoseAtCompletion()
        {
            var command = CreateCommand();
            var track = CreateTrack(command);

            track.Advance(command.DurationSeconds);
            var sample = track.Sample();

            Assert.That(Vector3.Distance(sample.Position, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.Rotation, command.SourceLocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_ContactSquashPreserved()
        {
            var command = CreateCommand();
            var track = CreateTrack(command);

            track.Advance(command.DurationSeconds * command.ContactNormalizedTime);
            var scale = track.SampleVisualScaleMultiplier();

            Assert.That(scale.x, Is.GreaterThan(1f));
            Assert.That(scale.y, Is.GreaterThan(1f));
            Assert.That(scale.z, Is.LessThan(1f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_CreateStay_UsesCommand()
        {
            var command = CreateCommand(sourceActionPlanId: 0, presentationSeed: 99);

            var track = FlipImpactTrack.CreateStay(command);

            Assert.That(track.InstanceKey, Is.EqualTo(new FlipImpactInstanceKey(99, command.BoxEntityId, true)));
            Assert.That(track.Signal.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(track.Signal.SourceCell, Is.EqualTo(command.SourceCell));
            Assert.That(track.Signal.ImpactCell, Is.EqualTo(command.ImpactCell));
            Assert.That(track.DurationSeconds, Is.EqualTo(command.DurationSeconds).Within(0.0001f));
            Assert.That(Vector3.Distance(track.SourcePose.Position, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Vector3.Distance(track.ImpactPose.Position, command.ImpactLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(track.ContactNormalizedTime, Is.EqualTo(command.ContactNormalizedTime).Within(0.0001f));
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

        private static FlipImpactTrack CreateTrack()
        {
            return CreateTrack(CreateCommand());
        }

        private static FlipImpactTrack CreateTrack(in FlipImpactStayMotionCommand command)
        {
            return FlipImpactTrack.CreateStay(command);
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

        private static FlipImpactPresentationSignal CreateSignal(FlipImpactPresentationDisposition disposition)
        {
            return new FlipImpactPresentationSignal(
                sourceActionPlanId: 7,
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
