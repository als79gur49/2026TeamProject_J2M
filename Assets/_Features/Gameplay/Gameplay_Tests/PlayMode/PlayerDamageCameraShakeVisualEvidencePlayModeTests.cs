#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests.Support.Unity;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed partial class PlayerMovementPlayModeTests
    {
        private const int ExpectedM3ACameraShakeVisualFrameCount = 33;
        private const int ExpectedM3BHeavyLandingVisualFrameCount = 15;

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeM3BVisualEvidence_HeavyLandingFullReducedOffWriteValidatedManifest()
        {
            Assert.That(
                SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Camera Shake M3-B visual evidence requires UNITY_GRAPHICS=1.");
            var outputDirectory = ReadCameraShakeVisualArgument("-cameraShakeVisualOutput");
            var document = CreateCameraShakeVisualManifestDocument();
            var writer = new VisualEvidenceManifestWriter(outputDirectory, document);

            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var capture = CaptureHeavyEnemyLandingVisualScenario(
                    outputDirectory,
                    motionLevel,
                    writer,
                    document);
                while (capture.MoveNext())
                {
                    yield return capture.Current;
                }
            }

            Assert.That(writer.Frames, Has.Count.EqualTo(ExpectedM3BHeavyLandingVisualFrameCount));
            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var frames = writer.Frames.Where(frame =>
                        frame.scenario == "HeavyEnemyJumpLanding" &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .ToArray();
                Assert.That(
                    frames.Select(frame => frame.frameName),
                    Is.EqualTo(new[] { "PreLanding", "Landing", "Peak", "Decay", "Rest" }));
                Assert.That(frames.Last().additivePositionMagnitude, Is.Zero.Within(0.0000001d));
            }

            AssertSameM3ATimelineAcrossMotionLevels(
                writer.Frames,
                "HeavyEnemyJumpLanding",
                expectedCount: 5);
            AssertPeakCaptureHashesDemonstrateMotionLevelApplication(
                writer.Frames,
                "HeavyEnemyJumpLanding");
            var manifestPath = writer.WriteManifest();
            Assert.That(File.Exists(manifestPath), Is.True);
            TestContext.WriteLine(
                $"CAMERA_SHAKE_M3B_VISUAL_MANIFEST path={manifestPath} " +
                $"frames={writer.Frames.Count} graphics={document.graphicsApi} " +
                $"device={document.graphicsDevice}");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeM3AVisualEvidence_DamageLethalSixScenariosWriteValidatedManifest()
        {
            Assert.That(
                SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Camera Shake M3-A visual evidence requires UNITY_GRAPHICS=1.");
            var outputDirectory = ReadCameraShakeVisualArgument("-cameraShakeVisualOutput");
            var profile = LoadCampaignCameraShakeProfile();
            var document = CreateCameraShakeVisualManifestDocument();
            var writer = new VisualEvidenceManifestWriter(outputDirectory, document);

            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var capture = CapturePlayerImpactVisualScenario(
                    outputDirectory,
                    profile,
                    motionLevel,
                    lethal: false,
                    writer,
                    document);
                while (capture.MoveNext())
                {
                    yield return capture.Current;
                }
            }

            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var capture = CapturePlayerImpactVisualScenario(
                    outputDirectory,
                    profile,
                    motionLevel,
                    lethal: true,
                    writer,
                    document);
                while (capture.MoveNext())
                {
                    yield return capture.Current;
                }
            }

            Assert.That(writer.Frames, Has.Count.EqualTo(ExpectedM3ACameraShakeVisualFrameCount));
            AssertM3ACanonicalFrameInventory(writer.Frames);
            AssertSameM3ATimelineAcrossMotionLevels(writer.Frames, "PlayerDamage", expectedCount: 5);
            AssertSameM3ATimelineAcrossMotionLevels(writer.Frames, "PlayerLethal", expectedCount: 6);
            AssertPeakCaptureHashesDemonstrateMotionLevelApplication(writer.Frames, "PlayerDamage");
            AssertPeakCaptureHashesDemonstrateMotionLevelApplication(writer.Frames, "PlayerLethal");
            var fullDamagePeak = writer.Frames.Single(frame =>
                frame.scenario == "PlayerDamage" &&
                frame.cameraMotionLevel == "Full" &&
                frame.frameName == "Peak");
            var fullLethalPeak = writer.Frames.Single(frame =>
                frame.scenario == "PlayerLethal" &&
                frame.cameraMotionLevel == "Full" &&
                frame.frameName == "Peak");
            Assert.That(
                fullLethalPeak.additivePositionMagnitude,
                Is.GreaterThan(fullDamagePeak.additivePositionMagnitude));
            Assert.That(
                fullLethalPeak.additiveRotationDegrees,
                Is.GreaterThan(fullDamagePeak.additiveRotationDegrees));
            foreach (var scenario in new[] { "PlayerDamage", "PlayerLethal" })
            {
                var pre = writer.Frames.Single(frame =>
                    frame.scenario == scenario &&
                    frame.cameraMotionLevel == "Full" &&
                    frame.frameName == "PreHit");
                var peak = writer.Frames.Single(frame =>
                    frame.scenario == scenario &&
                    frame.cameraMotionLevel == "Full" &&
                    frame.frameName == "Peak");
                Assert.That(peak.sha256, Is.Not.EqualTo(pre.sha256));
            }

            var manifestPath = writer.WriteManifest();
            Assert.That(File.Exists(manifestPath), Is.True);
            TestContext.WriteLine(
                $"CAMERA_SHAKE_M3A_VISUAL_MANIFEST path={manifestPath} " +
                $"frames={writer.Frames.Count} graphics={document.graphicsApi} " +
                $"device={document.graphicsDevice}");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeM3AHudVisualEvidence_LethalFullKeepsOverlayStableThroughTerminalReset()
        {
            var outputRoot = Path.GetFullPath(ReadCameraShakeVisualArgument("-cameraShakeVisualOutput"));
            var hudOutput = Path.Combine(outputRoot, "hud");
            Directory.CreateDirectory(hudOutput);
            var records = new List<CameraShakeHudFrameRecord>();
            var cameraObject = new GameObject("CameraShakeM3A_HudLethal_Camera");
            var camera = cameraObject.AddComponent<Camera>();
            var host = CreatePlayerDamageCameraShakeHost(playerHp: 1, camera);
            var materials = new List<Material>();
            CameraShakeHudOverlay overlay = default;
            try
            {
                AddPlayerImpactVisualScene(host, materials);
                overlay = CreateCameraShakeHudOverlay("PlayerLethal", camera);
                host.Presenter.ConfigureGameplayCameraShakeProfile(LoadCampaignCameraShakeProfile());
                host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
                yield return WaitForCameraShakeHudResolution();
                Canvas.ForceUpdateCanvases();
                yield return null;

                records.Add(CaptureCameraShakeHudFrame(
                    hudOutput,
                    "playerlethal-full",
                    "00-pre-hit",
                    "PlayerLethal",
                    "PreHit",
                    host,
                    overlay));
                var result = host.InputHost.RunSingleTick();
                Assert.That(result.PresentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
                host.Presenter.UpdatePresentation(0.02f);
                yield return null;
                records.Add(CaptureCameraShakeHudFrame(
                    hudOutput,
                    "playerlethal-full",
                    "02-peak",
                    "PlayerLethal",
                    "Peak",
                    host,
                    overlay));

                host.Presenter.ApplyStageTerminalPresentation(
                    GameplayStageTerminalPresentationReason.PlayerDeathRetry,
                    result);
                host.Presenter.CompleteStageTerminalCameraHandoff();
                yield return null;
                records.Add(CaptureCameraShakeHudFrame(
                    hudOutput,
                    "playerlethal-full",
                    "05-terminal-after-reset",
                    "PlayerLethal",
                    "Rest",
                    host,
                    overlay));

                AssertM3AHudScenarioStable(records);
                WriteCameraShakeHudManifest(hudOutput, records);
            }
            finally
            {
                DestroyCameraShakeVisualMaterials(materials);
                if (overlay.CanvasObject != null)
                {
                    Object.Destroy(overlay.CanvasObject);
                }

                Object.Destroy(host.gameObject);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }

        private static void AssertM3AHudScenarioStable(
            IReadOnlyList<CameraShakeHudFrameRecord> records)
        {
            Assert.That(records, Has.Count.EqualTo(3));
            var pre = records.Single(record => record.Marker == "PreHit");
            var peak = records.Single(record => record.Marker == "Peak");
            var rest = records.Single(record => record.Marker == "Rest");
            Assert.That(peak.AdditivePositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(rest.AdditivePositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(Vector2.Distance(pre.HudRootScreenPosition, peak.HudRootScreenPosition), Is.LessThan(0.05f));
            Assert.That(Vector2.Distance(pre.NotificationScreenPosition, peak.NotificationScreenPosition), Is.LessThan(0.05f));
            Assert.That(Vector2.Distance(pre.PauseScreenPosition, peak.PauseScreenPosition), Is.LessThan(0.05f));
            Assert.That(Vector2.Distance(pre.HudRootScreenPosition, rest.HudRootScreenPosition), Is.LessThan(0.05f));
            Assert.That(peak.Sha256, Is.Not.EqualTo(pre.Sha256));
        }

        private static IEnumerator CapturePlayerImpactVisualScenario(
            string outputDirectory,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel,
            bool lethal,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            var scenario = lethal ? "PlayerLethal" : "PlayerDamage";
            var scenarioDirectory = $"{(lethal ? "lethal" : "damage")}-{motionLevel.ToString().ToLowerInvariant()}";
            var cameraRoot = new GameObject($"CameraShakeM3A_{scenarioDirectory}_CameraRoot");
            var outputCamera = cameraRoot.AddComponent<Camera>();
            var host = CreatePlayerDamageCameraShakeHost(lethal ? 1 : 3, outputCamera);
            var materials = new List<Material>();
            try
            {
                AddPlayerImpactVisualScene(host, materials);
                yield return null;
                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(motionLevel);
                var rig = host.GetComponent<GameplayCameraRig>();

                CaptureM3APlayerImpactFrame(
                    outputDirectory,
                    scenarioDirectory,
                    scenario,
                    motionLevel,
                    "PreHit",
                    0,
                    "00-pre-hit",
                    0d,
                    0,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                var result = host.InputHost.RunSingleTick();
                Assert.That(result, Is.Not.Null);
                Assert.That(result.PresentationData.PlayerDamageSignals, Has.Count.EqualTo(1));
                Assert.That(result.PresentationData.PlayerDeathSignals.Count > 0, Is.EqualTo(lethal));
                CaptureM3APlayerImpactFrame(
                    outputDirectory,
                    scenarioDirectory,
                    scenario,
                    motionLevel,
                    lethal ? "LethalHit" : "Hit",
                    1,
                    lethal ? "01-lethal-hit" : "01-hit",
                    0d,
                    result.TickIndex,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.02f);
                CaptureM3APlayerImpactFrame(
                    outputDirectory,
                    scenarioDirectory,
                    scenario,
                    motionLevel,
                    "Peak",
                    2,
                    "02-peak",
                    0.02d,
                    result.TickIndex,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                if (!lethal)
                {
                    host.Presenter.UpdatePresentation(0.04f);
                    CaptureM3APlayerImpactFrame(
                        outputDirectory,
                        scenarioDirectory,
                        scenario,
                        motionLevel,
                        "Decay",
                        3,
                        "03-decay",
                        0.06d,
                        result.TickIndex,
                        outputCamera,
                        null,
                        rig,
                        writer,
                        document);
                    host.Presenter.UpdatePresentation(0.07f);
                    AssertCameraShakePoseIdentity(rig);
                    CaptureM3APlayerImpactFrame(
                        outputDirectory,
                        scenarioDirectory,
                        scenario,
                        motionLevel,
                        "Rest",
                        4,
                        "04-rest",
                        0.13d,
                        result.TickIndex,
                        outputCamera,
                        null,
                        rig,
                        writer,
                        document);
                }
                else
                {
                    host.Presenter.UpdatePresentation(0.10f);
                    CaptureM3APlayerImpactFrame(
                        outputDirectory,
                        scenarioDirectory,
                        scenario,
                        motionLevel,
                        "DeathHold",
                        3,
                        "03-death-hold",
                        0.12d,
                        result.TickIndex,
                        outputCamera,
                        null,
                        rig,
                        writer,
                        document);
                    host.Presenter.ApplyStageTerminalPresentation(
                        GameplayStageTerminalPresentationReason.PlayerDeathRetry,
                        result);
                    CaptureM3APlayerImpactFrame(
                        outputDirectory,
                        scenarioDirectory,
                        scenario,
                        motionLevel,
                        "TerminalHandoff",
                        4,
                        "04-terminal-handoff",
                        0.12d,
                        result.TickIndex,
                        outputCamera,
                        null,
                        rig,
                        writer,
                        document);
                    host.Presenter.CompleteStageTerminalCameraHandoff();
                    AssertCameraShakePoseIdentity(rig);
                    CaptureM3APlayerImpactFrame(
                        outputDirectory,
                        scenarioDirectory,
                        scenario,
                        motionLevel,
                        "TerminalAfterReset",
                        5,
                        "05-terminal-after-reset",
                        0.12d,
                        result.TickIndex,
                        outputCamera,
                        null,
                        rig,
                        writer,
                        document);
                }
            }
            finally
            {
                DestroyCameraShakeVisualMaterials(materials);
                Object.Destroy(host.gameObject);
                Object.Destroy(cameraRoot);
            }

            yield return null;
        }

        private static IEnumerator CaptureHeavyEnemyLandingVisualScenario(
            string outputDirectory,
            CameraMotionLevel motionLevel,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            var scenarioDirectory = $"heavylanding-{motionLevel.ToString().ToLowerInvariant()}";
            var cameraRoot = new GameObject($"CameraShakeM3B_{scenarioDirectory}_CameraRoot");
            var outputCamera = cameraRoot.AddComponent<Camera>();
            var host = CreateHeavyEnemyJumpLandingHost(outputCamera);
            var materials = new List<Material>();
            try
            {
                AddHeavyEnemyLandingVisualScene(host, materials);
                host.Presenter.SetCameraMotionLevel(motionLevel);
                yield return null;

                var preLandingTick = host.InputHost.RunSingleTick();
                Assert.That(preLandingTick, Is.Not.Null);
                Assert.That(preLandingTick.PresentationData.EnemyJumpSignals.Any(signal => signal.LandedThisTick), Is.False);
                host.Presenter.UpdatePresentation(host.TimingProfile.SimulationTickIntervalSeconds);
                CaptureM3BHeavyLandingFrame(
                    outputDirectory,
                    scenarioDirectory,
                    motionLevel,
                    "PreLanding",
                    0,
                    "pre-landing",
                    0d,
                    preLandingTick.TickIndex,
                    sequence: 1,
                    requestSubmitTime: -1d,
                    outputCamera,
                    host.GetComponent<GameplayCameraRig>(),
                    writer,
                    document);

                var landingTick = host.InputHost.RunSingleTick();
                var signal = landingTick.PresentationData.EnemyJumpSignals.Single(value => value.LandedThisTick);
                Assert.That(signal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Landed));
                Assert.That(host.Presenter.ObservedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
                Assert.That(host.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
                CaptureM3BHeavyLandingFrame(
                    outputDirectory,
                    scenarioDirectory,
                    motionLevel,
                    "Landing",
                    1,
                    "landing",
                    0d,
                    landingTick.TickIndex,
                    signal.Sequence,
                    requestSubmitTime: 0d,
                    outputCamera,
                    host.GetComponent<GameplayCameraRig>(),
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.02f);
                CaptureM3BHeavyLandingFrame(
                    outputDirectory,
                    scenarioDirectory,
                    motionLevel,
                    "Peak",
                    2,
                    "peak",
                    0.02d,
                    landingTick.TickIndex,
                    signal.Sequence,
                    requestSubmitTime: 0d,
                    outputCamera,
                    host.GetComponent<GameplayCameraRig>(),
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.04f);
                CaptureM3BHeavyLandingFrame(
                    outputDirectory,
                    scenarioDirectory,
                    motionLevel,
                    "Decay",
                    3,
                    "decay",
                    0.06d,
                    landingTick.TickIndex,
                    signal.Sequence,
                    requestSubmitTime: 0d,
                    outputCamera,
                    host.GetComponent<GameplayCameraRig>(),
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.11f);
                AssertCameraShakeReturnsToIdentity(host);
                CaptureM3BHeavyLandingFrame(
                    outputDirectory,
                    scenarioDirectory,
                    motionLevel,
                    "Rest",
                    4,
                    "rest",
                    0.17d,
                    landingTick.TickIndex,
                    signal.Sequence,
                    requestSubmitTime: 0d,
                    outputCamera,
                    host.GetComponent<GameplayCameraRig>(),
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

        private static void CaptureM3BHeavyLandingFrame(
            string outputDirectory,
            string scenarioDirectory,
            CameraMotionLevel motionLevel,
            string frameName,
            int frameIndex,
            string frameStem,
            double presentationTime,
            int tickIndex,
            int sequence,
            double requestSubmitTime,
            Camera outputCamera,
            GameplayCameraRig rig,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            CaptureCameraShakeVisualFrame(
                outputDirectory,
                scenarioDirectory,
                "HeavyEnemyJumpLanding",
                motionLevel,
                frameName,
                frameIndex,
                frameStem,
                presentationTime,
                tickIndex,
                boxEntityId: 40,
                sourceActionPlanId: sequence,
                trackStartTime: 0d,
                requestSubmitTime,
                normalizedProgress: presentationTime / 0.16d,
                contactNormalized: frameIndex == 0 ? 0d : 1d,
                outputCamera,
                brain: null,
                rig,
                writer,
                document);
        }

        private static void AddHeavyEnemyLandingVisualScene(
            GameplaySceneHost host,
            ICollection<Material> materials)
        {
            Assert.That(host.ViewRegistry.TryGetView(40, out var enemyView), Is.True);
            var sceneRoot = new GameObject("CameraShakeM3B_HeavyLandingSceneGeometry");
            sceneRoot.transform.SetParent(host.transform, false);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Cube,
                "CameraShakeM3B_Ground",
                sceneRoot.transform,
                new Vector3(0f, -0.55f, 0f),
                new Vector3(14f, 0.1f, 14f),
                new Color(0.035f, 0.055f, 0.10f, 1f),
                materials,
                localSpace: false);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Cylinder,
                "CameraShakeM3B_LandingMarker",
                sceneRoot.transform,
                new Vector3(2f, -0.46f, 0f),
                new Vector3(1.25f, 0.025f, 1.25f),
                new Color(1f, 0.46f, 0.08f, 1f),
                materials,
                localSpace: false);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Capsule,
                "CameraShakeM3B_PlayerReference",
                sceneRoot.transform,
                new Vector3(-2f, 0f, 0f),
                new Vector3(0.65f, 0.9f, 0.65f),
                new Color(0.08f, 0.65f, 1f, 1f),
                materials,
                localSpace: false);
            for (var index = 0; index < 4; index++)
            {
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Cube,
                    $"CameraShakeM3B_Landmark{index}",
                    sceneRoot.transform,
                    new Vector3(index % 2 == 0 ? -4.5f : 4.5f, 0.4f + index * 0.25f, index < 2 ? -4f : 4f),
                    new Vector3(0.55f, 1.2f + index * 0.25f, 0.55f),
                    index % 2 == 0
                        ? new Color(0.72f, 0.18f, 0.9f, 1f)
                        : new Color(0.12f, 0.85f, 0.48f, 1f),
                    materials,
                    localSpace: false);
            }

            Assert.That(enemyView.GetComponent<EnemyJumpMotionPresentationAuthoring>(), Is.Not.Null);
        }

        private static void CaptureM3APlayerImpactFrame(
            string outputDirectory,
            string scenarioDirectory,
            string scenario,
            CameraMotionLevel motionLevel,
            string frameName,
            int frameIndex,
            string frameStem,
            double presentationTime,
            int tickIndex,
            Camera outputCamera,
            CinemachineBrain brain,
            GameplayCameraRig rig,
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
                tickIndex,
                boxEntityId: 10,
                sourceActionPlanId: tickIndex,
                trackStartTime: 0d,
                requestSubmitTime: tickIndex > 0 ? 0d : -1d,
                normalizedProgress: presentationTime,
                contactNormalized: 0d,
                outputCamera,
                brain,
                rig,
                writer,
                document);
        }

        private static void AddPlayerImpactVisualScene(
            GameplaySceneHost host,
            ICollection<Material> materials)
        {
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            Assert.That(host.ViewRegistry.TryGetView(40, out var enemyView), Is.True);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Capsule,
                "CameraShakeM3A_Player",
                playerView.ModelRoot,
                Vector3.zero,
                new Vector3(0.72f, 0.94f, 0.72f),
                new Color(0.1f, 0.65f, 1f, 1f),
                materials,
                localSpace: true);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Sphere,
                "CameraShakeM3A_Attacker",
                enemyView.ModelRoot,
                new Vector3(0.7f, 0.15f, 0.2f),
                Vector3.one * 0.62f,
                new Color(1f, 0.15f, 0.2f, 1f),
                materials,
                localSpace: true);

            var sceneRoot = new GameObject("CameraShakeM3A_SceneGeometry");
            sceneRoot.transform.SetParent(host.transform, false);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Cube,
                "CameraShakeM3A_Ground",
                sceneRoot.transform,
                new Vector3(0f, -0.55f, 0f),
                new Vector3(14f, 0.1f, 14f),
                new Color(0.045f, 0.07f, 0.12f, 1f),
                materials,
                localSpace: false);
            var positions = new[]
            {
                new Vector3(-5f, 0.5f, -4f),
                new Vector3(5f, 0.75f, -4f),
                new Vector3(-4f, 1f, 5f),
                new Vector3(4f, 1.25f, 5f),
            };
            var colors = new[]
            {
                new Color(0.7f, 0.15f, 0.85f, 1f),
                new Color(0.1f, 0.8f, 0.45f, 1f),
                new Color(0.9f, 0.72f, 0.08f, 1f),
                new Color(0.08f, 0.8f, 0.9f, 1f),
            };
            for (var index = 0; index < positions.Length; index++)
            {
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Cube,
                    $"CameraShakeM3A_Landmark{index}",
                    sceneRoot.transform,
                    positions[index],
                    new Vector3(0.7f, 1.4f + index * 0.35f, 0.7f),
                    colors[index],
                    materials,
                    localSpace: false);
            }
        }

        private static void AssertM3ACanonicalFrameInventory(
            IReadOnlyList<VisualEvidenceFrameRecord> frames)
        {
            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var damage = frames.Where(frame =>
                        frame.scenario == "PlayerDamage" &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .Select(frame => frame.frameName)
                    .ToArray();
                Assert.That(damage, Is.EqualTo(new[] { "PreHit", "Hit", "Peak", "Decay", "Rest" }));

                var lethal = frames.Where(frame =>
                        frame.scenario == "PlayerLethal" &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .Select(frame => frame.frameName)
                    .ToArray();
                Assert.That(
                    lethal,
                    Is.EqualTo(new[]
                    {
                        "PreHit", "LethalHit", "Peak", "DeathHold", "TerminalHandoff", "TerminalAfterReset",
                    }));
                Assert.That(
                    frames.Single(frame =>
                        frame.scenario == "PlayerLethal" &&
                        frame.cameraMotionLevel == motionLevel.ToString() &&
                        frame.frameName == "TerminalAfterReset").additivePositionMagnitude,
                    Is.Zero.Within(0.0000001d));
            }
        }

        private static void AssertSameM3ATimelineAcrossMotionLevels(
            IReadOnlyList<VisualEvidenceFrameRecord> frames,
            string scenario,
            int expectedCount)
        {
            var reference = frames.Where(frame =>
                    frame.scenario == scenario &&
                    frame.cameraMotionLevel == CameraMotionLevel.Off.ToString())
                .OrderBy(frame => frame.frameIndex)
                .ToArray();
            Assert.That(reference, Has.Length.EqualTo(expectedCount));
            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var candidate = frames.Where(frame =>
                        frame.scenario == scenario &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .ToArray();
                Assert.That(candidate.Select(frame => frame.frameName), Is.EqualTo(reference.Select(frame => frame.frameName)));
                Assert.That(candidate.Select(frame => frame.tickIndex), Is.EqualTo(reference.Select(frame => frame.tickIndex)));
                Assert.That(candidate.Select(frame => frame.presentationTime), Is.EqualTo(reference.Select(frame => frame.presentationTime)));
            }
        }
    }
}
#endif
