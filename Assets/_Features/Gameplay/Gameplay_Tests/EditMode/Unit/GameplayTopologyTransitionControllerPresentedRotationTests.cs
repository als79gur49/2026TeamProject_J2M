using System;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTopologyTransitionControllerPresentedRotationTests
    {
        [Test]
        [Category("Full")]
        public void GameplayTopologyTransitionController_AttachCameraRig_MidTransition_ReappliesCurrentPresentedRotation()
        {
            var rootObject =
                new GameObject("GameplayTopologyTransitionController_AttachCameraRig_MidTransition_ReappliesCurrentPresentedRotation");
            GameplayTopologyTransitionController controller = null;

            try
            {
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var cubeCenter = new Vector3(1.25f, -0.5f, 2f);
                controller = CreateController();
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var timingProfile = CreateTimingProfile();

                controller.Configure(
                    boardRoot,
                    boardSurfaceRenderer: null,
                    timingProfile,
                    TopologyRotationVisualMapping.ForwardUsesPositiveX,
                    new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.Linear,
                    },
                    () => cubeCenter);
                controller.Reset();
                controller.CompleteInitialTopology(sourceTopology);
                controller.RefreshTopologyTrack(
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        new TickTopologyMotion(sourceTopology, destinationTopology, CubeRotationKind.Forward),
                        Array.Empty<TickVisibilityChange>()),
                    sourceTopology);
                controller.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f, destinationTopology);

                var expectedMidRotation = Quaternion.Euler(45f, 0f, 0f);
                var sourceRestRotation = GameplayTopologyVisualRotationUtility.ResolveRestReferenceRotation(
                    sourceTopology,
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);
                var destinationRestRotation = GameplayTopologyVisualRotationUtility.ResolveRestReferenceRotation(
                    destinationTopology,
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);

                Assert.That(
                    Quaternion.Angle(controller.PresentedBoardRotation, expectedMidRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(controller.PresentedBoardRotation, sourceRestRotation),
                    Is.GreaterThan(0.1f));
                Assert.That(
                    Quaternion.Angle(controller.PresentedBoardRotation, destinationRestRotation),
                    Is.GreaterThan(0.1f));

                var rig = rootObject.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                controller.AttachCameraRig(rig);

                Assert.That(
                    Quaternion.Angle(rig.PresentedTopologyOrbit, Quaternion.Inverse(controller.PresentedBoardRotation)),
                    Is.LessThan(0.001f));
                Assert.That(rig.PresentedTopologyOrbitXDegrees, Is.EqualTo(-45f).Within(0.001f));
                Assert.That(boardRoot.CameraTargetRoot.localPosition, Is.EqualTo(cubeCenter));
                Assert.That(boardRoot.CameraTargetRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(boardRoot.CameraTargetRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));
            }
            finally
            {
                controller?.Reset();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayTopologyTransitionController CreateController()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            var motionTimingResolver = new GameplayMotionTimingResolver(stateStore, trackState);
            return new GameplayTopologyTransitionController(motionTimingResolver);
        }

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.1f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
        }
    }
}
