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
    }

    public sealed class FlipImpactDestroyEffectTrackTests
    {
        [Test]
        [Category("Extended")]
        public void DestroySelf_StartsBreakAtThreshold_WhileContinuingFlight()
        {
            FlipImpactDestroyEffectTrack track = null;

            try
            {
                track = CreateDestroyEffectTrack(out var root, out var material, flightDurationSeconds: 1f, totalDurationSeconds: 1.2f);
                track.Advance(0.75f);

                Assert.That(Vector3.Distance(root.transform.localPosition, Vector3.zero), Is.GreaterThan(0.0001f));
                Assert.That(Vector3.Distance(root.transform.localPosition, new Vector3(2f, 0f, 0f)), Is.GreaterThan(0.0001f));
                Assert.That(root.transform.localScale.z, Is.LessThan(1f));
                Assert.That(root.transform.localScale.z, Is.GreaterThan(0.98f));
                Assert.That(GetAlpha(material), Is.LessThan(1f));
                Assert.That(GetAlpha(material), Is.GreaterThan(0.99f));
            }
            finally
            {
                track?.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void DestroySelf_ReachesImpactPoseOnlyAtFlightDuration()
        {
            FlipImpactDestroyEffectTrack track = null;

            try
            {
                track = CreateDestroyEffectTrack(out var root, out _, flightDurationSeconds: 1f, totalDurationSeconds: 1.2f);
                track.Advance(0.99f);

                Assert.That(Vector3.Distance(root.transform.localPosition, new Vector3(2f, 0f, 0f)), Is.GreaterThan(0.0001f));

                track.Advance(0.01f);

                Assert.That(Vector3.Distance(root.transform.localPosition, new Vector3(2f, 0f, 0f)), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        root.transform.localRotation,
                        Quaternion.AngleAxis(180f, Vector3.up)),
                    Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                track?.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void DestroySelf_TotalDurationIsClampedToAtLeastFlightDuration()
        {
            FlipImpactDestroyEffectTrack track = null;

            try
            {
                track = CreateDestroyEffectTrack(out _, out _, flightDurationSeconds: 1f, totalDurationSeconds: 0.5f);

                Assert.That(track.TotalDurationSeconds, Is.EqualTo(track.FlightDurationSeconds));
                track.Advance(0.99f);
                Assert.That(track.IsComplete, Is.False);

                track.Advance(0.01f);
                Assert.That(track.IsComplete, Is.True);
            }
            finally
            {
                track?.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void DestroySelf_ContactNormalizedTimeMeansBreakOnsetNotFinalArrival()
        {
            FlipImpactDestroyEffectTrack track = null;

            try
            {
                track = CreateDestroyEffectTrack(out var root, out var material, flightDurationSeconds: 1f, totalDurationSeconds: 1.2f);

                track.Advance(track.BreakStartSeconds + 0.01f);

                Assert.That(GetAlpha(material), Is.LessThan(1f));
                Assert.That(Vector3.Distance(root.transform.localPosition, new Vector3(2f, 0f, 0f)), Is.GreaterThan(0.0001f));
            }
            finally
            {
                track?.Dispose();
            }
        }

        private static FlipImpactDestroyEffectTrack CreateDestroyEffectTrack(
            out GameObject root,
            out Material material,
            float flightDurationSeconds,
            float totalDurationSeconds)
        {
            root = new GameObject("DestroyEffectRoot");
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "Expected a shader with a writable color property for alpha checks.");
            material = new Material(shader);
            if (material.HasProperty("_Color"))
            {
                material.color = Color.white;
            }

            return new FlipImpactDestroyEffectTrack(
                root.transform,
                new Renderer[] { null },
                new[] { new[] { material } },
                flightDurationSeconds,
                totalDurationSeconds,
                new GameplayEntityPose(Vector3.zero, Quaternion.identity),
                new GameplayEntityPose(new Vector3(2f, 0f, 0f), Quaternion.AngleAxis(180f, Vector3.up)),
                arcHeightWorld: 0.6f,
                new FlipImpactTimingSettings(0.70f, 0.35f, 0.30f, 0.10f));
        }

        private static float GetAlpha(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor").a;
            }

            if (material.HasProperty("_Color"))
            {
                return material.color.a;
            }

            Assert.Fail("Material does not expose a readable alpha property.");
            return 0f;
        }
    }
}
