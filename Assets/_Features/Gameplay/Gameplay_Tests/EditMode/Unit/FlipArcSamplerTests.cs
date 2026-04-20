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
        public void FlipArcSampler_EpsilonEquivalentToMotionClipSamples()
        {
            var startPose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var endPose = new GameplayEntityPose(
                new Vector3(2f, 0f, 0f),
                Quaternion.AngleAxis(180f, Vector3.up));
            const float arcHeightWorld = 0.6f;

            AssertPoseApproximately(
                startPose,
                FlipArcSampler.Sample(startPose, endPose, 0f, arcHeightWorld),
                0.0001f,
                0.001f);
            AssertPoseApproximately(
                endPose,
                FlipArcSampler.Sample(startPose, endPose, 1f, arcHeightWorld),
                0.0001f,
                0.001f);

            AssertMotionClipEquivalent(startPose, endPose, 0.25f, arcHeightWorld);
            AssertMotionClipEquivalent(startPose, endPose, 0.5f, arcHeightWorld);
            AssertMotionClipEquivalent(startPose, endPose, 0.75f, arcHeightWorld);
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

        private static void AssertMotionClipEquivalent(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime,
            float arcHeightWorld)
        {
            var clip = MotionClip.Create(
                TickEntityMotionKind.Flip,
                startPose,
                endPose,
                durationSeconds: 1f,
                interpolateRotation: true,
                flipArcHeightWorld: arcHeightWorld);
            clip.Advance(normalizedTime);

            var clipSample = clip.Sample();
            var samplerSample = FlipArcSampler.Sample(startPose, endPose, normalizedTime, arcHeightWorld);
            AssertPoseApproximately(clipSample, samplerSample, 0.0001f, 0.001f);
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
