using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests.Support.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed partial class PlayerMovementPlayModeTests
    {
        private const int ExpectedFlipHostileVisualFrameCount = 45;

        private static readonly FlipFloorImpactPresentationKind[] FlipHostileVisualDispositions =
        {
            FlipFloorImpactPresentationKind.Stay,
            FlipFloorImpactPresentationKind.DestroySelf,
            FlipFloorImpactPresentationKind.FollowThrough,
        };

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeVisualEvidence_FlipHostileNineScenariosWriteValidatedManifest()
        {
            Assert.That(
                SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(GraphicsDeviceType.Null),
                "M2-B camera Shake visual evidence requires UNITY_GRAPHICS=1.");

            var outputDirectory = ReadCameraShakeVisualArgument("-cameraShakeVisualOutput");
            var profile = LoadCampaignCameraShakeProfile();
            var document = CreateCameraShakeVisualManifestDocument();
            var writer = new VisualEvidenceManifestWriter(outputDirectory, document);

            foreach (var disposition in FlipHostileVisualDispositions)
            {
                foreach (var motionLevel in CameraShakeVisualMotionLevels)
                {
                    var capture = CaptureFlipHostileCameraShakeVisualScenario(
                        outputDirectory,
                        profile,
                        disposition,
                        motionLevel,
                        writer,
                        document);
                    while (capture.MoveNext())
                    {
                        yield return capture.Current;
                    }
                }
            }

            Assert.That(writer.Frames, Has.Count.EqualTo(ExpectedFlipHostileVisualFrameCount));
            AssertFlipHostileFrameInventory(writer.Frames);
            foreach (var disposition in FlipHostileVisualDispositions)
            {
                var scenario = ResolveFlipHostileScenarioName(disposition);
                AssertSameTimelineAcrossMotionLevels(writer.Frames, scenario);
                AssertPeakCaptureHashesDemonstrateMotionLevelApplication(writer.Frames, scenario);
            }

            var fullPeaks = writer.Frames
                .Where(frame =>
                    frame.cameraMotionLevel == CameraMotionLevel.Full.ToString() &&
                    frame.frameName == "Peak")
                .ToDictionary(frame => frame.scenario, frame => frame.additivePositionMagnitude);
            Assert.That(fullPeaks["FollowThrough"], Is.GreaterThan(fullPeaks["Stay"]));
            Assert.That(fullPeaks["FollowThrough"], Is.GreaterThan(fullPeaks["DestroySelf"]));

            var manifestPath = writer.WriteManifest();
            Assert.That(File.Exists(manifestPath), Is.True);
            TestContext.WriteLine(
                $"CAMERA_SHAKE_VISUAL_MANIFEST path={manifestPath} frames={writer.Frames.Count} " +
                $"graphics={document.graphicsApi} device={document.graphicsDevice}");
        }

        private static IEnumerator CaptureFlipHostileCameraShakeVisualScenario(
            string outputDirectory,
            GameplayCameraShakeProfile profile,
            FlipFloorImpactPresentationKind disposition,
            CameraMotionLevel motionLevel,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            var scenario = ResolveFlipHostileScenarioName(disposition);
            var scenarioDirectory = $"{scenario.ToLowerInvariant()}-{motionLevel.ToString().ToLowerInvariant()}";
            var cameraRoot = new GameObject($"CameraShakeVisual_{scenarioDirectory}_CameraRoot");
            var outputCamera = cameraRoot.AddComponent<Camera>();
            var host = CreateHostileFlipCameraShakeHost(disposition, outputCamera);
            var materials = new List<Material>();
            try
            {
                AddCameraShakeVisualScene(host, materials);
                AddFlipHostileCameraShakeVisualEntities(host, materials);
                yield return null;

                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(motionLevel);
                var rig = host.GetComponent<GameplayCameraRig>();
                TickResult executeTick;
                if (disposition == FlipFloorImpactPresentationKind.Stay)
                {
                    executeTick = host.InputHost.RunSingleTick();
                }
                else
                {
                    host.InputHost.SetRawMoveInput(Vector2.left);
                    host.InputHost.BufferFlip();
                    Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                    host.InputHost.SetRawMoveInput(Vector2.zero);
                    executeTick = host.InputHost.RunSingleTick();
                }

                Assert.That(executeTick, Is.Not.Null);
                var floorSignal = executeTick.PresentationData.FlipFloorImpactSignals.Single();
                Assert.That(floorSignal.Kind, Is.EqualTo(disposition));
                Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
                Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.EqualTo(1));
                Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);

                var sourceKind = ResolveFlipHostileProgressSource(disposition);
                var submitNormalized = disposition == FlipFloorImpactPresentationKind.FollowThrough
                    ? GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime
                    : GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime;
                double presentationTime = 0d;
                double submitTime = -1d;

                if (disposition == FlipFloorImpactPresentationKind.FollowThrough)
                {
                    AdvanceFlipHostileVisualTo(
                        host,
                        floorSignal.SourceActionPlanId,
                        sourceKind,
                        GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime - 0.02f,
                        ref presentationTime);
                }

                CaptureFlipHostileVisualFrame(
                    "PreContact",
                    0,
                    "pre-contact",
                    presentationTime,
                    submitTime,
                    submitNormalized,
                    scenario,
                    scenarioDirectory,
                    motionLevel,
                    executeTick,
                    floorSignal,
                    sourceKind,
                    outputDirectory,
                    outputCamera,
                    rig,
                    host,
                    writer,
                    document);

                if (disposition == FlipFloorImpactPresentationKind.FollowThrough)
                {
                    AdvanceFlipHostileVisualTo(
                        host,
                        floorSignal.SourceActionPlanId,
                        sourceKind,
                        0.85f,
                        ref presentationTime);
                    Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);
                    AssertCameraShakePoseIdentity(rig);
                    CaptureFlipHostileVisualFrame(
                        "PreLanding",
                        1,
                        "followthrough-pre-landing",
                        presentationTime,
                        submitTime,
                        submitNormalized,
                        scenario,
                        scenarioDirectory,
                        motionLevel,
                        executeTick,
                        floorSignal,
                        sourceKind,
                        outputDirectory,
                        outputCamera,
                        rig,
                        host,
                        writer,
                        document);
                }

                AdvanceFlipHostileVisualTo(
                    host,
                    floorSignal.SourceActionPlanId,
                    sourceKind,
                    submitNormalized,
                    ref presentationTime);
                submitTime = presentationTime;
                Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));
                Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
                if (disposition != FlipFloorImpactPresentationKind.FollowThrough)
                {
                    CaptureFlipHostileVisualFrame(
                        "Contact",
                        1,
                        disposition == FlipFloorImpactPresentationKind.DestroySelf
                            ? "contact-break"
                            : "contact",
                        presentationTime,
                        submitTime,
                        submitNormalized,
                        scenario,
                        scenarioDirectory,
                        motionLevel,
                        executeTick,
                        floorSignal,
                        sourceKind,
                        outputDirectory,
                        outputCamera,
                        rig,
                        host,
                        writer,
                        document);
                }
                else
                {
                    CaptureFlipHostileVisualFrame(
                        "Landing",
                        2,
                        "landing",
                        presentationTime,
                        submitTime,
                        submitNormalized,
                        scenario,
                        scenarioDirectory,
                        motionLevel,
                        executeTick,
                        floorSignal,
                        sourceKind,
                        outputDirectory,
                        outputCamera,
                        rig,
                        host,
                        writer,
                        document);
                }

                host.Presenter.UpdatePresentation(0.02f);
                presentationTime += 0.02d;
                CaptureFlipHostileVisualFrame(
                    "Peak",
                    disposition == FlipFloorImpactPresentationKind.FollowThrough ? 3 : 2,
                    "peak",
                    presentationTime,
                    submitTime,
                    submitNormalized,
                    scenario,
                    scenarioDirectory,
                    motionLevel,
                    executeTick,
                    floorSignal,
                    sourceKind,
                    outputDirectory,
                    outputCamera,
                    rig,
                    host,
                    writer,
                    document);

                if (disposition != FlipFloorImpactPresentationKind.FollowThrough)
                {
                    host.Presenter.UpdatePresentation(0.06f);
                    presentationTime += 0.06d;
                    CaptureFlipHostileVisualFrame(
                        "Decay",
                        3,
                        "decay",
                        presentationTime,
                        submitTime,
                        submitNormalized,
                        scenario,
                        scenarioDirectory,
                        motionLevel,
                        executeTick,
                        floorSignal,
                        sourceKind,
                        outputDirectory,
                        outputCamera,
                        rig,
                        host,
                        writer,
                        document);
                }

                host.Presenter.UpdatePresentation(0.5f);
                presentationTime += 0.5d;
                Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));
                AssertCameraShakeReturnsToIdentity(host);
                CaptureFlipHostileVisualFrame(
                    "Rest",
                    4,
                    "rest",
                    presentationTime,
                    submitTime,
                    submitNormalized,
                    scenario,
                    scenarioDirectory,
                    motionLevel,
                    executeTick,
                    floorSignal,
                    sourceKind,
                    outputDirectory,
                    outputCamera,
                    rig,
                    host,
                    writer,
                    document);
            }
            finally
            {
                DestroyCameraShakeVisualMaterials(materials);
                Object.Destroy(host.gameObject);
                Object.Destroy(cameraRoot);
            }

            yield return null;
        }

        private static void AdvanceFlipHostileVisualTo(
            GameplaySceneHost host,
            int sourceActionPlanId,
            MotionTrackProgressSourceKind sourceKind,
            float targetNormalized,
            ref double presentationTime)
        {
            var progress = ReadFlipHostileVisualProgress(host, sourceActionPlanId, sourceKind);
            for (var step = 0; step < 160 && progress < targetNormalized; step++)
            {
                const float delta = 0.005f;
                host.Presenter.UpdatePresentation(delta);
                presentationTime += delta;
                progress = ReadFlipHostileVisualProgress(host, sourceActionPlanId, sourceKind);
            }

            Assert.That(progress, Is.GreaterThanOrEqualTo(targetNormalized));
        }

        private static double ReadFlipHostileVisualProgress(
            GameplaySceneHost host,
            int sourceActionPlanId,
            MotionTrackProgressSourceKind sourceKind)
        {
            var sample = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(value =>
                value.EntityId == 30 &&
                value.MotionKind == TickEntityMotionKind.Flip &&
                value.SequenceOrActionPlanId == sourceActionPlanId &&
                value.SourceKind == sourceKind);
            return sample.IsValid ? sample.CurrentNormalizedTime : 0d;
        }

        private static void CaptureFlipHostileVisualFrame(
            string frameName,
            int frameIndex,
            string frameStem,
            double presentationTime,
            double submitTime,
            double submitNormalized,
            string scenario,
            string scenarioDirectory,
            CameraMotionLevel motionLevel,
            TickResult executeTick,
            FlipFloorImpactPresentationSignal floorSignal,
            MotionTrackProgressSourceKind sourceKind,
            string outputDirectory,
            Camera outputCamera,
            GameplayCameraRig rig,
            GameplaySceneHost host,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            CaptureCameraShakeVisualFrame(
                outputDirectory,
                scenarioDirectory,
                scenario,
                motionLevel,
                frameName,
                frameIndex,
                frameStem,
                presentationTime,
                executeTick.TickIndex,
                floorSignal.BoxEntityId,
                floorSignal.SourceActionPlanId,
                0d,
                submitTime,
                ReadFlipHostileVisualProgress(host, floorSignal.SourceActionPlanId, sourceKind),
                submitNormalized,
                outputCamera,
                null,
                rig,
                writer,
                document);
        }

        private static MotionTrackProgressSourceKind ResolveFlipHostileProgressSource(
            FlipFloorImpactPresentationKind disposition)
        {
            return disposition switch
            {
                FlipFloorImpactPresentationKind.Stay => MotionTrackProgressSourceKind.OriginalViewMotion,
                FlipFloorImpactPresentationKind.DestroySelf => MotionTrackProgressSourceKind.FlipInteraction,
                _ => MotionTrackProgressSourceKind.LocalMotion,
            };
        }

        private static string ResolveFlipHostileScenarioName(FlipFloorImpactPresentationKind disposition)
        {
            return disposition switch
            {
                FlipFloorImpactPresentationKind.Stay => "Stay",
                FlipFloorImpactPresentationKind.DestroySelf => "DestroySelf",
                FlipFloorImpactPresentationKind.FollowThrough => "FollowThrough",
                _ => throw new System.ArgumentOutOfRangeException(nameof(disposition), disposition, null),
            };
        }

        private static void AddFlipHostileCameraShakeVisualEntities(
            GameplaySceneHost host,
            ICollection<Material> materials)
        {
            if (host.ViewRegistry.TryGetView(20, out var primaryEnemy))
            {
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Capsule,
                    "CameraShakeVisualHostilePrimary",
                    primaryEnemy.ModelRoot,
                    Vector3.zero,
                    new Vector3(0.7f, 0.9f, 0.7f),
                    new Color(0.95f, 0.08f, 0.18f, 1f),
                    materials,
                    localSpace: true);
            }

            if (host.ViewRegistry.TryGetView(21, out var secondaryEnemy))
            {
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Sphere,
                    "CameraShakeVisualHostileSecondary",
                    secondaryEnemy.ModelRoot,
                    Vector3.zero,
                    Vector3.one * 0.65f,
                    new Color(0.72f, 0.05f, 0.85f, 1f),
                    materials,
                    localSpace: true);
            }
        }

        private static void AssertFlipHostileFrameInventory(
            IReadOnlyList<VisualEvidenceFrameRecord> frames)
        {
            foreach (var disposition in FlipHostileVisualDispositions)
            {
                var scenario = ResolveFlipHostileScenarioName(disposition);
                var expectedNames = disposition == FlipFloorImpactPresentationKind.FollowThrough
                    ? new[] { "PreContact", "PreLanding", "Landing", "Peak", "Rest" }
                    : new[] { "PreContact", "Contact", "Peak", "Decay", "Rest" };
                foreach (var motionLevel in CameraShakeVisualMotionLevels)
                {
                    var scenarioFrames = frames.Where(frame =>
                            frame.scenario == scenario &&
                            frame.cameraMotionLevel == motionLevel.ToString())
                        .OrderBy(frame => frame.frameIndex)
                        .ToArray();
                    Assert.That(scenarioFrames.Select(frame => frame.frameName), Is.EqualTo(expectedNames));
                    Assert.That(scenarioFrames.Single(frame => frame.frameName == "Peak").requestSubmitTime,
                        Is.GreaterThanOrEqualTo(0d));
                }
            }

            foreach (var level in CameraShakeVisualMotionLevels)
            {
                var preLanding = frames.Single(frame =>
                    frame.scenario == "FollowThrough" &&
                    frame.cameraMotionLevel == level.ToString() &&
                    frame.frameName == "PreLanding");
                Assert.That(preLanding.normalizedProgress,
                    Is.GreaterThan(GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime));
                Assert.That(preLanding.normalizedProgress,
                    Is.LessThan(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
                Assert.That(preLanding.requestSubmitTime, Is.LessThan(0d));
                Assert.That(preLanding.additivePositionMagnitude, Is.EqualTo(0d).Within(0.0000001d));
            }
        }
    }
}
