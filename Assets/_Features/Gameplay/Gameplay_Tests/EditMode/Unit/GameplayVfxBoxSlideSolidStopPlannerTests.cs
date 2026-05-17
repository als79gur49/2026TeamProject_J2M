using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBoxSlideSolidStopPlannerTests
    {
        private const string SolidStopPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideSolidStopVfx.prefab";
        private const string FollowPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideSparkFollowVfx.prefab";
        private const string SolidStopBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxSlideSolidStop_Binding.asset";

        [Test]
        [Category("Extended")]
        public void BoxSlideSolidStopBinding_UsesDedicatedTransientPrefab()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(SolidStopBindingPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SolidStopPrefabPath);
            var followPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FollowPrefabPath);

            Assert.That(binding, Is.Not.Null, SolidStopBindingPath);
            Assert.That(prefab, Is.Not.Null, SolidStopPrefabPath);
            Assert.That(followPrefab, Is.Not.Null, FollowPrefabPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop)));
            Assert.That(binding.Prefab, Is.EqualTo(prefab));
            Assert.That(binding.Prefab, Is.Not.EqualTo(followPrefab));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);

            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);
            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
        }

        [Test]
        public void SolidEntitySignal_EmitsBoxSlideSolidStopRequest()
        {
            var signal = CreateSignal(BoxSlideStopperKind.SolidEntity);
            var presentationData = CreatePresentationData(signal);
            var builder = new GameplayVfxRequestPlanBuilder();

            new BoxVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(7, presentationData, signal.Topology),
                builder);

            var request = builder.Build().Requests.Single();
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop)));
            Assert.That(request.SourceEntityId, Is.EqualTo(signal.BoxEntityId));
            Assert.That(request.Anchor.Cell, Is.EqualTo(signal.SourceCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(signal.Topology));
        }

        [TestCase(BoxSlideStopperKind.Terrain)]
        [TestCase(BoxSlideStopperKind.BoardEdge)]
        [TestCase(BoxSlideStopperKind.Shield)]
        [TestCase(BoxSlideStopperKind.None)]
        public void NonSolidStopper_DoesNotEmitBoxSlideSolidStopRequest(BoxSlideStopperKind stopperKind)
        {
            var signal = CreateSignal(stopperKind);
            var presentationData = CreatePresentationData(signal);
            var builder = new GameplayVfxRequestPlanBuilder();

            new BoxVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(7, presentationData, signal.Topology),
                builder);

            Assert.That(builder.Build().Requests, Is.Empty);
        }

        [Test]
        public void StopperCellAndDirection_ContributeToStableRequestSequence()
        {
            var baseSignal = CreateSignal(BoxSlideStopperKind.SolidEntity);
            var changedStopper = new BoxSlideStopPresentationSignal(
                baseSignal.BoxEntityId,
                baseSignal.SourceCell,
                new SurfaceCell(FaceId.Floor, 1, 2),
                baseSignal.SlideDirection,
                baseSignal.StopperKind,
                baseSignal.StopperEntityId,
                baseSignal.SolidKind,
                baseSignal.Topology,
                baseSignal.Cause);
            var changedDirection = new BoxSlideStopPresentationSignal(
                baseSignal.BoxEntityId,
                baseSignal.SourceCell,
                baseSignal.StopperCell,
                Direction.Up,
                baseSignal.StopperKind,
                baseSignal.StopperEntityId,
                baseSignal.SolidKind,
                baseSignal.Topology,
                baseSignal.Cause);

            var baseSequence = PlanSingle(baseSignal).SequenceId;

            Assert.That(PlanSingle(changedStopper).SequenceId, Is.Not.EqualTo(baseSequence));
            Assert.That(PlanSingle(changedDirection).SequenceId, Is.Not.EqualTo(baseSequence));
        }

        [Test]
        public void CommandBuilder_CrossFaceStopDoesNotBuildCommand()
        {
            var baseSignal = CreateSignal(BoxSlideStopperKind.SolidEntity);
            var signal = new BoxSlideStopPresentationSignal(
                baseSignal.BoxEntityId,
                new SurfaceCell(FaceId.Floor, 1, 1),
                new SurfaceCell(FaceId.Front, 1, 0),
                Direction.Up,
                baseSignal.StopperKind,
                baseSignal.StopperEntityId,
                baseSignal.SolidKind,
                baseSignal.Topology,
                baseSignal.Cause);
            var projector = CreateProjector();

            var sourceProjectionSucceeded = projector.TryProjectSurfaceCell(
                signal.SourceCell,
                signal.Topology,
                out _);
            var stopperProjectionSucceeded = projector.TryProjectSurfaceCell(
                signal.StopperCell,
                signal.Topology,
                out _);
            var built = BoxSlideSolidStopVfxCommandBuilder.TryBuild(
                7,
                signal,
                projector,
                out _,
                out var anchor);

            Assert.That(sourceProjectionSucceeded, Is.True);
            Assert.That(stopperProjectionSucceeded, Is.True);
            Assert.That(built, Is.False);
            Assert.That(anchor.HasLocalPose, Is.False);
            Assert.That(anchor.UsedFallback, Is.False);
        }

        [Test]
        public void CommandBuilder_UsesMidpointBetweenSourceAndStopperCells()
        {
            var signal = CreateSignal(BoxSlideStopperKind.SolidEntity);
            var projector = CreateProjector();

            var built = BoxSlideSolidStopVfxCommandBuilder.TryBuild(
                7,
                signal,
                projector,
                out var request,
                out var anchor);

            projector.TryProjectSurfaceCell(signal.SourceCell, signal.Topology, out var sourcePose);
            projector.TryProjectSurfaceCell(signal.StopperCell, signal.Topology, out var stopperPose);
            var expected = Vector3.Lerp(sourcePose.LocalPosition, stopperPose.LocalPosition, 0.5f);
            Assert.That(built, Is.True);
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop)));
            Assert.That(anchor.HasLocalPose, Is.True);
            AssertVector(anchor.LocalPosition, expected);
        }

        [Test]
        public void CommandBuilder_FallsBackToSourceCellWhenStopperProjectionFails()
        {
            var baseSignal = CreateSignal(BoxSlideStopperKind.SolidEntity);
            var signal = new BoxSlideStopPresentationSignal(
                baseSignal.BoxEntityId,
                baseSignal.SourceCell,
                new SurfaceCell(FaceId.Floor, 99, 99),
                baseSignal.SlideDirection,
                baseSignal.StopperKind,
                baseSignal.StopperEntityId,
                baseSignal.SolidKind,
                baseSignal.Topology,
                baseSignal.Cause);
            var projector = CreateProjector();

            var built = BoxSlideSolidStopVfxCommandBuilder.TryBuild(
                7,
                signal,
                projector,
                out _,
                out var anchor);

            projector.TryProjectSurfaceCell(signal.SourceCell, signal.Topology, out var sourcePose);
            Assert.That(built, Is.True);
            Assert.That(anchor.UsedFallback, Is.True);
            AssertVector(anchor.LocalPosition, sourcePose.LocalPosition);
        }

        private static GameplayVfxRequest PlanSingle(in BoxSlideStopPresentationSignal signal)
        {
            var builder = new GameplayVfxRequestPlanBuilder();
            new BoxVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(7, CreatePresentationData(signal), signal.Topology),
                builder);
            return builder.Build().Requests.Single();
        }

        private static TickPresentationData CreatePresentationData(BoxSlideStopPresentationSignal signal)
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                boxSlideStopSignals: new[] { signal });
        }

        private static BoxSlideStopPresentationSignal CreateSignal(BoxSlideStopperKind stopperKind)
        {
            return new BoxSlideStopPresentationSignal(
                boxEntityId: 20,
                sourceCell: new SurfaceCell(FaceId.Floor, 1, 1),
                stopperCell: new SurfaceCell(FaceId.Floor, 2, 1),
                slideDirection: Direction.Right,
                stopperKind: stopperKind,
                stopperEntityId: stopperKind == BoxSlideStopperKind.SolidEntity ? 90 : 0,
                solidKind: SolidKind.Box,
                topology: new CubeTopologyState(FaceId.Floor),
                cause: BoxSlideStopCause.SlidingContinuationBlocked);
        }

        private static GameplayCubeProjector CreateProjector()
        {
            return new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(8, 8)),
                1f);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
