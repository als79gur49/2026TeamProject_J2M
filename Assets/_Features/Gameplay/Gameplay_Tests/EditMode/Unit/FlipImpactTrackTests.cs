using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class FlipImpactTrackTests
    {
        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_ReturnsToExactSourcePoseAtCompletion()
        {
            var sourcePose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var impactPose = new GameplayEntityPose(
                new Vector3(2f, 0f, 0f),
                Quaternion.AngleAxis(180f, Vector3.up));
            var settings = new FlipImpactTimingSettings(0.70f, 0.35f, 0.30f, 0.10f);
            var signal = new FlipImpactPresentationSignal(
                sourceActionPlanId: 7,
                boxEntityId: 30,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                impactCell: new SurfaceCell(FaceId.Floor, 2, 0),
                topology: new CubeTopologyState(FaceId.Floor),
                sourceFacing: Direction.Left,
                impactFacing: Direction.Right,
                disposition: FlipImpactPresentationDisposition.Stay,
                hasLandingCell: false);
            var track = new FlipImpactTrack(
                FlipImpactInstanceKey.Create(signal, tickIndexFallback: 1),
                signal,
                sourcePose,
                impactPose,
                durationSeconds: 1f,
                arcHeightWorld: 0.6f,
                settings);

            track.Advance(1f);
            var sample = track.Sample();

            Assert.That(Vector3.Distance(sample.Position, sourcePose.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.Rotation, sourcePose.Rotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactTrack_Stay_ContactSquashesBoxVisualScale()
        {
            var sourcePose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var impactPose = new GameplayEntityPose(new Vector3(2f, 0f, 0f), Quaternion.identity);
            var settings = new FlipImpactTimingSettings(0.70f, 0.35f, 0.30f, 0.10f);
            var signal = new FlipImpactPresentationSignal(
                sourceActionPlanId: 7,
                boxEntityId: 30,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                impactCell: new SurfaceCell(FaceId.Floor, 2, 0),
                topology: new CubeTopologyState(FaceId.Floor),
                sourceFacing: Direction.Left,
                impactFacing: Direction.Right,
                disposition: FlipImpactPresentationDisposition.Stay,
                hasLandingCell: false);
            var track = new FlipImpactTrack(
                FlipImpactInstanceKey.Create(signal, tickIndexFallback: 1),
                signal,
                sourcePose,
                impactPose,
                durationSeconds: 1f,
                arcHeightWorld: 0.6f,
                settings);

            track.Advance(settings.ContactNormalizedTime);
            var scale = track.SampleVisualScaleMultiplier();

            Assert.That(scale.x, Is.GreaterThan(1f));
            Assert.That(scale.y, Is.GreaterThan(1f));
            Assert.That(scale.z, Is.LessThan(1f));
        }
    }

}
