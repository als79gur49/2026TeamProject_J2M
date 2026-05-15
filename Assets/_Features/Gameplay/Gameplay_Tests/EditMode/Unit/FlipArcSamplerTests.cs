using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class FlipArcSamplerTests
    {
        [Test]
        [Category("Extended")]
        public void FlipArcSampler_EndpointsUnchangedAndMotionClipUsesSlamSampler()
        {
            var startPose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var endPose = new GameplayEntityPose(
                new Vector3(2f, 0f, 0f),
                Quaternion.AngleAxis(180f, Vector3.up));
            const float peakHeightWorld = 1.4f;

            AssertPoseApproximately(
                startPose,
                FlipArcSampler.Sample(startPose, endPose, 0f, peakHeightWorld),
                0.0001f,
                0.001f);
            AssertPoseApproximately(
                endPose,
                FlipArcSampler.Sample(startPose, endPose, 1f, peakHeightWorld),
                0.0001f,
                0.001f);

            AssertMotionClipUsesSlamSampler(startPose, endPose, 0.25f, peakHeightWorld);
            AssertMotionClipUsesSlamSampler(startPose, endPose, 0.5f, peakHeightWorld);
            AssertMotionClipUsesSlamSampler(startPose, endPose, 0.75f, peakHeightWorld);

            var arcMidpoint = FlipArcSampler.Sample(startPose, endPose, 0.5f, peakHeightWorld);
            var slamMidpoint = BoxFlipSlamSampler.Sample(startPose, endPose, 0.5f, peakHeightWorld);
            Assert.That(Vector3.Distance(slamMidpoint.Position, arcMidpoint.Position), Is.GreaterThan(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipArcSampler_DegenerateTravel_FallsBackWithoutNaN()
        {
            var pose = new GameplayEntityPose(new Vector3(1f, 2f, 3f), Quaternion.identity);
            var sample = FlipArcSampler.Sample(pose, pose, 0.5f, 0.6f);

            Assert.That(float.IsNaN(sample.Position.x), Is.False);
            Assert.That(float.IsNaN(sample.Position.y), Is.False);
            Assert.That(float.IsNaN(sample.Position.z), Is.False);
            AssertPoseApproximately(pose, sample, 0.0001f, 0.001f);
        }

        [Test]
        [Category("Extended")]
        public void BoxFlipSlamSampler_LiftsOverPivotHoldsThenSlamsTowardTarget()
        {
            var startPose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var endPose = new GameplayEntityPose(
                new Vector3(2f, 0f, 0f),
                Quaternion.AngleAxis(180f, Vector3.up));
            const float peakHeightWorld = 1.4f;
            var travelAxis = Vector3.right;
            var liftAxis = -Vector3.forward;
            var previousSlamDuration = 0.86f - BoxFlipSlamSampler.HoldEndTime;
            var currentSlamDuration = BoxFlipSlamSampler.SlamEndTime - BoxFlipSlamSampler.HoldEndTime;

            var liftEnd = BoxFlipSlamSampler.Sample(
                startPose,
                endPose,
                BoxFlipSlamSampler.LiftEndTime,
                peakHeightWorld);
            var holdEnd = BoxFlipSlamSampler.Sample(
                startPose,
                endPose,
                BoxFlipSlamSampler.HoldEndTime,
                peakHeightWorld);
            var slamMidpoint = BoxFlipSlamSampler.Sample(
                startPose,
                endPose,
                (BoxFlipSlamSampler.HoldEndTime + BoxFlipSlamSampler.SlamEndTime) * 0.5f,
                peakHeightWorld);
            var slamContact = BoxFlipSlamSampler.Sample(
                startPose,
                endPose,
                BoxFlipSlamSampler.SlamEndTime,
                peakHeightWorld);

            Assert.That(currentSlamDuration, Is.EqualTo(previousSlamDuration * 1.2f).Within(0.0001f));
            Assert.That(ProjectTravelFraction(startPose.Position, endPose.Position, liftEnd.Position, travelAxis), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(ProjectTravelFraction(startPose.Position, endPose.Position, holdEnd.Position, travelAxis), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(Vector3.Dot(liftEnd.Position - startPose.Position, liftAxis), Is.EqualTo(peakHeightWorld).Within(0.0001f));
            Assert.That(Vector3.Dot(holdEnd.Position - startPose.Position, liftAxis), Is.EqualTo(peakHeightWorld).Within(0.0001f));
            Assert.That(Quaternion.Angle(liftEnd.Rotation, Quaternion.AngleAxis(90f, Vector3.up)), Is.LessThanOrEqualTo(0.001f));

            Assert.That(ProjectTravelFraction(startPose.Position, endPose.Position, slamMidpoint.Position, travelAxis), Is.GreaterThan(0.75f));
            Assert.That(Vector3.Dot(slamMidpoint.Position - startPose.Position, liftAxis), Is.LessThan(peakHeightWorld * 0.2f));
            AssertPoseApproximately(endPose, slamContact, 0.0001f, 0.001f);
        }

        [Test]
        [Category("Extended")]
        public void BoxMotionVisualScaleSampler_FlipImpactBeginsAtSlamContact()
        {
            var contactScale = BoxMotionVisualScaleSampler.SampleFlipMotion(BoxFlipSlamSampler.SlamEndTime);
            var settleStartScale = BoxMotionVisualScaleSampler.SampleFlipSettle(0f);

            Assert.That(Vector3.Distance(contactScale, settleStartScale), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(contactScale.z, Is.LessThan(1f));
            Assert.That(contactScale.x, Is.GreaterThan(1f));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerVisualHeight_ResolvesProjectedPeakHeight()
        {
            var root = new GameObject("GameplayTrackPlanner_PlayerVisualHeight_ResolvesProjectedPeakHeight");
            try
            {
                var view = root.AddComponent<GameplayEntityView>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(view.ModelRoot, worldPositionStays: false);
                visual.transform.localScale = new Vector3(0.25f, 1f, 0.25f);

                Assert.That(
                    GameplayTrackPlanner.TryResolvePlayerVisualHeightWorld(view, Vector3.up, out var playerHeight),
                    Is.True);
                Assert.That(playerHeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    GameplayTrackPlanner.ResolveFlipPeakHeightFromPlayerVisualHeightWorld(playerHeight),
                    Is.EqualTo(1.4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_FallbackFlipPeakHeight_UsesAtLeastPlayerScaleCellHeight()
        {
            Assert.That(
                GameplayTrackPlanner.ResolveFallbackFlipPeakHeightWorld(
                    configuredArcHeightWorld: 0.65f,
                    cellSize: 1f),
                Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(
                GameplayTrackPlanner.ResolveFallbackFlipPeakHeightWorld(
                    configuredArcHeightWorld: 2f,
                    cellSize: 1f),
                Is.EqualTo(2f).Within(0.0001f));
        }

        private static void AssertMotionClipUsesSlamSampler(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime,
            float peakHeightWorld)
        {
            var clip = MotionClip.Create(
                TickEntityMotionKind.Flip,
                startPose,
                endPose,
                durationSeconds: 1f,
                interpolateRotation: true,
                flipPeakHeightWorld: peakHeightWorld);
            clip.Advance(normalizedTime);

            var clipSample = clip.Sample();
            var samplerSample = BoxFlipSlamSampler.Sample(startPose, endPose, normalizedTime, peakHeightWorld);
            AssertPoseApproximately(clipSample, samplerSample, 0.0001f, 0.001f);
        }

        private static float ProjectTravelFraction(
            Vector3 startPosition,
            Vector3 endPosition,
            Vector3 samplePosition,
            Vector3 travelAxis)
        {
            var travelLength = Vector3.Distance(startPosition, endPosition);
            return Vector3.Dot(samplePosition - startPosition, travelAxis) / travelLength;
        }

        private static void AssertPoseApproximately(
            GameplayEntityPose expected,
            GameplayEntityPose actual,
            float positionEpsilon,
            float angleEpsilonDegrees)
        {
            Assert.That(Vector3.Distance(expected.Position, actual.Position), Is.LessThanOrEqualTo(positionEpsilon));
            Assert.That(Quaternion.Angle(expected.Rotation, actual.Rotation), Is.LessThanOrEqualTo(angleEpsilonDegrees));
        }
    }
}
