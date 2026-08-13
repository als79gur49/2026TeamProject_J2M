using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests.Support.Unity;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed partial class PlayerMovementPlayModeTests
    {
        private const int CameraShakeVisualWidth = 1280;
        private const int CameraShakeVisualHeight = 720;
        private const int ExpectedCameraShakeVisualFrameCount = 30;

        private static readonly CameraMotionLevel[] CameraShakeVisualMotionLevels =
        {
            CameraMotionLevel.Full,
            CameraMotionLevel.Reduced,
            CameraMotionLevel.Off,
        };

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeVisualEvidence_CanonicalPushFlipSixScenariosWriteValidatedManifest()
        {
            Assert.That(
                SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Camera Shake visual evidence requires UNITY_GRAPHICS=1.");

            var outputDirectory = ReadCameraShakeVisualArgument("-cameraShakeVisualOutput");
            var profile = LoadCampaignCameraShakeProfile();
            var document = CreateCameraShakeVisualManifestDocument();
            var writer = new VisualEvidenceManifestWriter(outputDirectory, document);

            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var push = CapturePushCameraShakeVisualScenario(
                    outputDirectory,
                    profile,
                    motionLevel,
                    writer,
                    document);
                while (push.MoveNext())
                {
                    yield return push.Current;
                }
            }

            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var flip = CaptureFlipCameraShakeVisualScenario(
                    outputDirectory,
                    profile,
                    motionLevel,
                    writer,
                    document);
                while (flip.MoveNext())
                {
                    yield return flip.Current;
                }
            }

            Assert.That(writer.Frames, Has.Count.EqualTo(ExpectedCameraShakeVisualFrameCount));
            AssertCanonicalFrameInventory(writer.Frames);
            AssertSameTimelineAcrossMotionLevels(writer.Frames, "Push");
            AssertSameTimelineAcrossMotionLevels(writer.Frames, "Flip");
            AssertPeakCaptureHashesDemonstrateMotionLevelApplication(writer.Frames, "Push");
            AssertPeakCaptureHashesDemonstrateMotionLevelApplication(writer.Frames, "Flip");

            var manifestPath = writer.WriteManifest();
            Assert.That(File.Exists(manifestPath), Is.True);
            TestContext.WriteLine(
                $"CAMERA_SHAKE_VISUAL_MANIFEST path={manifestPath} " +
                $"frames={writer.Frames.Count} graphics={document.graphicsApi} " +
                $"device={document.graphicsDevice}");
        }

        private static IEnumerator CapturePushCameraShakeVisualScenario(
            string outputDirectory,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            var scenarioDirectory = $"push-{motionLevel.ToString().ToLowerInvariant()}";
            var cameraRoot = new GameObject($"CameraShakeVisual_{scenarioDirectory}_CameraRoot");
            var outputCamera = cameraRoot.AddComponent<Camera>();
            var host = CreatePushCameraShakeHost(outputCamera);
            var materials = new List<Material>();
            try
            {
                AddCameraShakeVisualScene(host, materials);
                yield return null;

                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(motionLevel);
                var rig = host.GetComponent<GameplayCameraRig>();

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();
                var startTick = host.InputHost.RunSingleTick();
                Assert.That(startTick, Is.Not.Null);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Push",
                    motionLevel,
                    "PreLaunch",
                    0,
                    "pre-launch",
                    0d,
                    startTick.TickIndex,
                    30,
                    0,
                    0d,
                    0d,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.BoxSlide),
                    0d,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                var executeTick = host.InputHost.RunSingleTick();
                Assert.That(executeTick, Is.Not.Null);
                Assert.That(executeTick.PresentationData.BoxSlideStartSignals, Has.Count.EqualTo(1));
                var signal = executeTick.PresentationData.BoxSlideStartSignals[0];
                Assert.That(signal.BoxEntityId, Is.EqualTo(30));
                Assert.That(signal.SourceActionPlanId, Is.GreaterThan(0));
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Push",
                    motionLevel,
                    "Launch",
                    1,
                    "launch",
                    0d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    0d,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.BoxSlide),
                    0d,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.02f);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Push",
                    motionLevel,
                    "Peak",
                    2,
                    "peak",
                    0.02d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    0d,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.BoxSlide),
                    0d,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.04f);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Push",
                    motionLevel,
                    "Decay",
                    3,
                    "decay",
                    0.06d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    0d,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.BoxSlide),
                    0d,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.065f);
                AssertCameraShakePoseIdentity(rig);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Push",
                    motionLevel,
                    "Rest",
                    4,
                    "rest",
                    0.125d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    0d,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.BoxSlide),
                    0d,
                    outputCamera,
                    null,
                    rig,
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
            Assert.That(host == null, Is.True);
            Assert.That(cameraRoot == null, Is.True);
            Assert.That(materials.All(material => material == null), Is.True);
        }

        private static IEnumerator CaptureFlipCameraShakeVisualScenario(
            string outputDirectory,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            var scenarioDirectory = $"flip-{motionLevel.ToString().ToLowerInvariant()}";
            var cameraRoot = new GameObject($"CameraShakeVisual_{scenarioDirectory}_CameraRoot");
            var outputCamera = cameraRoot.AddComponent<Camera>();
            var host = CreateFlipCameraShakeHost(outputCamera);
            var materials = new List<Material>();
            try
            {
                AddCameraShakeVisualScene(host, materials);
                yield return null;

                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(motionLevel);
                var rig = host.GetComponent<GameplayCameraRig>();

                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                var startTick = host.InputHost.RunSingleTick();
                Assert.That(startTick, Is.Not.Null);
                host.InputHost.SetRawMoveInput(Vector2.zero);
                var executeTick = host.InputHost.RunSingleTick();
                Assert.That(executeTick, Is.Not.Null);
                Assert.That(executeTick.PresentationData.FlipFloorImpactSignals, Has.Count.EqualTo(1));
                var signal = executeTick.PresentationData.FlipFloorImpactSignals[0];
                Assert.That(signal.BoxEntityId, Is.EqualTo(30));
                Assert.That(signal.SourceActionPlanId, Is.GreaterThan(0));
                Assert.That(
                    signal.VisualContactNormalizedTime,
                    Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime)
                        .Within(0.000001f));

                var contactNormalized = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;
                double presentationTime = 0d;
                var preContactProgress = ReadCameraShakeVisualProgress(
                    host,
                    TickEntityMotionKind.Flip);
                var preContactTime = presentationTime;
                Assert.That(preContactProgress, Is.LessThan(contactNormalized));
                Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.EqualTo(1));
                AssertCameraShakePoseIdentity(rig);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Flip",
                    motionLevel,
                    "PreContact",
                    0,
                    "pre-contact",
                    preContactTime,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    -1d,
                    preContactProgress,
                    contactNormalized,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                var crossingProgress = preContactProgress;
                for (var step = 0; step < 100 && crossingProgress < contactNormalized; step++)
                {
                    const float delta = 0.005f;
                    host.Presenter.UpdatePresentation(delta);
                    presentationTime += delta;
                    crossingProgress = ReadCameraShakeVisualProgress(
                        host,
                        TickEntityMotionKind.Flip);
                }

                var contactTime = presentationTime;
                Assert.That(crossingProgress, Is.GreaterThanOrEqualTo(contactNormalized));
                Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Flip",
                    motionLevel,
                    "Contact",
                    1,
                    "contact",
                    contactTime,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    contactTime,
                    crossingProgress,
                    contactNormalized,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.02f);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Flip",
                    motionLevel,
                    "Peak",
                    2,
                    "peak",
                    contactTime + 0.02d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    contactTime,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.Flip),
                    contactNormalized,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.06f);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Flip",
                    motionLevel,
                    "Decay",
                    3,
                    "decay",
                    contactTime + 0.08d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    contactTime,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.Flip),
                    contactNormalized,
                    outputCamera,
                    null,
                    rig,
                    writer,
                    document);

                host.Presenter.UpdatePresentation(0.095f);
                AssertCameraShakePoseIdentity(rig);
                CaptureCameraShakeVisualFrame(
                    outputDirectory,
                    scenarioDirectory,
                    "Flip",
                    motionLevel,
                    "Rest",
                    4,
                    "rest",
                    contactTime + 0.175d,
                    executeTick.TickIndex,
                    signal.BoxEntityId,
                    signal.SourceActionPlanId,
                    0d,
                    contactTime,
                    ReadCameraShakeVisualProgress(host, TickEntityMotionKind.Flip),
                    contactNormalized,
                    outputCamera,
                    null,
                    rig,
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
            Assert.That(host == null, Is.True);
            Assert.That(cameraRoot == null, Is.True);
            Assert.That(materials.All(material => material == null), Is.True);
        }

        private static void CaptureCameraShakeVisualFrame(
            string outputDirectory,
            string scenarioDirectory,
            string scenario,
            CameraMotionLevel motionLevel,
            string frameName,
            int frameIndex,
            string frameStem,
            double presentationTime,
            int tickIndex,
            int boxEntityId,
            int sourceActionPlanId,
            double trackStartTime,
            double requestSubmitTime,
            double normalizedProgress,
            double contactNormalized,
            Camera outputCamera,
            CinemachineBrain brain,
            GameplayCameraRig rig,
            VisualEvidenceManifestWriter writer,
            VisualEvidenceManifestDocument document)
        {
            if (brain != null)
            {
                var cinemachineCameras = Object.FindObjectsByType<CinemachineCamera>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (var index = 0; index < cinemachineCameras.Length; index++)
                {
                    cinemachineCameras[index].PreviousStateIsValid = false;
                }

                brain.ManualUpdate();
            }
            AssertFiniteCameraPose(outputCamera.transform.position, outputCamera.transform.rotation);
            var artifactPath = VisualEvidenceManifestWriter.ComposeScenarioFramePath(
                outputDirectory,
                scenarioDirectory,
                frameIndex,
                frameStem);
            var capture = VisualEvidenceFrameCapture.CaptureFrame(
                outputCamera,
                artifactPath,
                CameraShakeVisualWidth,
                CameraShakeVisualHeight);
            if (string.IsNullOrWhiteSpace(document.colorFormat))
            {
                document.colorFormat = capture.ColorFormat;
                document.colorSpace = capture.ColorSpace;
                document.cameraHdr = capture.CameraHdr;
                document.antiAliasing = capture.AntiAliasing;
            }
            else
            {
                Assert.That(capture.ColorFormat, Is.EqualTo(document.colorFormat));
                Assert.That(capture.ColorSpace, Is.EqualTo(document.colorSpace));
                Assert.That(capture.CameraHdr, Is.EqualTo(document.cameraHdr));
                Assert.That(capture.AntiAliasing, Is.EqualTo(document.antiAliasing));
            }

            var relativePath = Path.GetRelativePath(outputDirectory, artifactPath).Replace('\\', '/');
            writer.AddFrame(new VisualEvidenceFrameRecord
            {
                scenario = scenario,
                cameraMotionLevel = motionLevel.ToString(),
                frameName = frameName,
                frameIndex = frameIndex,
                presentationTime = presentationTime,
                tickIndex = tickIndex,
                pngPath = relativePath,
                width = capture.Width,
                height = capture.Height,
                pngBytes = capture.PngByteCount,
                sha256 = capture.Sha256,
                pixelVariance = capture.LuminanceVariance,
                boxEntityId = boxEntityId,
                sourceActionPlanId = sourceActionPlanId,
                trackStartTime = trackStartTime,
                requestSubmitTime = requestSubmitTime,
                normalizedProgress = normalizedProgress,
                contactNormalized = contactNormalized,
                additivePositionMagnitude = rig.AdditiveLocalPosition.magnitude,
                additiveRotationDegrees = MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation),
            });
        }

        private static VisualEvidenceManifestDocument CreateCameraShakeVisualManifestDocument()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(CinemachineCamera).Assembly);
            return new VisualEvidenceManifestDocument
            {
                timestamp = ReadCameraShakeVisualArgument("-cameraShakeVisualTimestamp"),
                repository = ReadCameraShakeVisualArgument("-cameraShakeVisualRepository"),
                worktree = ReadCameraShakeVisualArgument("-cameraShakeVisualWorktree"),
                branch = ReadCameraShakeVisualArgument("-cameraShakeVisualBranch"),
                head = ReadCameraShakeVisualArgument("-cameraShakeVisualHead"),
                tree = ReadCameraShakeVisualArgument("-cameraShakeVisualTree"),
                trackedFingerprint = ReadCameraShakeVisualArgument(
                    "-cameraShakeVisualTrackedFingerprint"),
                untrackedFingerprint = ReadCameraShakeVisualArgument(
                    "-cameraShakeVisualUntrackedFingerprint"),
                unityVersion = Application.unityVersion,
                cinemachineVersion = package?.version ?? "unknown",
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                renderPipeline = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.GetType().Name
                    : "BuiltIn",
                resolution = new VisualEvidenceResolutionMetadata
                {
                    width = CameraShakeVisualWidth,
                    height = CameraShakeVisualHeight,
                },
                captureBackend =
                    "GameplayCameraRig Direct Camera -> RenderTexture -> ReadPixels -> PNG",
                profilePath = CampaignCameraShakeProfilePath,
                profileGuid = AssetDatabase.AssetPathToGUID(CampaignCameraShakeProfilePath),
                colorFormat = string.Empty,
                colorSpace = string.Empty,
                cameraHdr = false,
                antiAliasing = 0,
                hudCaptureCapability = "SECONDARY_LANE_REQUIRED",
            };
        }

        private static string ReadCameraShakeVisualArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            throw new InvalidOperationException($"Missing required {name} command-line argument.");
        }

        private static double ReadCameraShakeVisualProgress(
            GameplaySceneHost host,
            TickEntityMotionKind preferredKind)
        {
            var sample = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(value =>
                value.EntityId == 30 && value.MotionKind == preferredKind);
            if (!sample.IsValid)
            {
                sample = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(value =>
                    value.EntityId == 30);
            }

            return sample.IsValid ? sample.CurrentNormalizedTime : 0d;
        }

        private static void AddCameraShakeVisualScene(
            GameplaySceneHost host,
            ICollection<Material> materials)
        {
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            Assert.That(host.ViewRegistry.TryGetView(30, out var boxView), Is.True);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Capsule,
                "CameraShakeVisualPlayer",
                playerView.ModelRoot,
                Vector3.zero,
                new Vector3(0.7f, 0.9f, 0.7f),
                new Color(0.1f, 0.65f, 1f, 1f),
                materials,
                localSpace: true);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Cube,
                "CameraShakeVisualBox",
                boxView.ModelRoot,
                Vector3.zero,
                Vector3.one * 0.85f,
                new Color(1f, 0.32f, 0.05f, 1f),
                materials,
                localSpace: true);

            var sceneRoot = new GameObject("CameraShakeVisualSceneGeometry");
            sceneRoot.transform.SetParent(host.transform, false);
            AddCameraShakeVisualPrimitive(
                PrimitiveType.Cube,
                "CameraShakeVisualGround",
                sceneRoot.transform,
                new Vector3(0f, -0.55f, 0f),
                new Vector3(14f, 0.1f, 14f),
                new Color(0.045f, 0.07f, 0.12f, 1f),
                materials,
                localSpace: false);
            var landmarkPositions = new[]
            {
                new Vector3(-5f, 0.2f, -5f),
                new Vector3(5f, 0.2f, -5f),
                new Vector3(-5f, 0.2f, 5f),
                new Vector3(5f, 0.2f, 5f),
                new Vector3(0f, 0.2f, 4f),
            };
            var landmarkColors = new[]
            {
                new Color(0.65f, 0.15f, 0.85f, 1f),
                new Color(0.1f, 0.8f, 0.45f, 1f),
                new Color(0.9f, 0.75f, 0.08f, 1f),
                new Color(0.85f, 0.12f, 0.28f, 1f),
                new Color(0.08f, 0.8f, 0.9f, 1f),
            };
            for (var index = 0; index < landmarkPositions.Length; index++)
            {
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Cube,
                    $"CameraShakeVisualLandmark{index}",
                    sceneRoot.transform,
                    landmarkPositions[index],
                    new Vector3(0.6f, 1.5f + index * 0.2f, 0.6f),
                    landmarkColors[index],
                    materials,
                    localSpace: false);
            }

            for (var coordinate = -5; coordinate <= 5; coordinate++)
            {
                var lineColor = coordinate % 2 == 0
                    ? new Color(0.2f, 0.32f, 0.5f, 1f)
                    : new Color(0.08f, 0.17f, 0.3f, 1f);
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Cube,
                    $"CameraShakeVisualGridX{coordinate}",
                    sceneRoot.transform,
                    new Vector3(coordinate, -0.485f, 0f),
                    new Vector3(0.025f, 0.02f, 12f),
                    lineColor,
                    materials,
                    localSpace: false);
                AddCameraShakeVisualPrimitive(
                    PrimitiveType.Cube,
                    $"CameraShakeVisualGridZ{coordinate}",
                    sceneRoot.transform,
                    new Vector3(0f, -0.48f, coordinate),
                    new Vector3(12f, 0.025f, 0.025f),
                    lineColor,
                    materials,
                    localSpace: false);
            }
        }

        private static void AddCameraShakeVisualPrimitive(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            ICollection<Material> materials,
            bool localSpace)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, worldPositionStays: !localSpace);
            if (localSpace)
            {
                primitive.transform.localPosition = position;
                primitive.transform.localRotation = Quaternion.identity;
                primitive.transform.localScale = scale;
            }
            else
            {
                primitive.transform.position = position;
                primitive.transform.rotation = Quaternion.identity;
                primitive.transform.localScale = scale;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null, "Visual evidence requires an unlit shader.");
            var material = new Material(shader)
            {
                name = $"{name}_VisualEvidenceMaterial",
                color = color,
            };
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            materials.Add(material);
        }

        private static void DestroyCameraShakeVisualMaterials(IEnumerable<Material> materials)
        {
            foreach (var material in materials)
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }
        }

        private static void AssertCanonicalFrameInventory(
            IReadOnlyList<VisualEvidenceFrameRecord> frames)
        {
            var expectedNames = new[] { "PreLaunch", "Launch", "Peak", "Decay", "Rest" };
            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var push = frames.Where(frame =>
                        frame.scenario == "Push" &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .ToArray();
                Assert.That(push.Select(frame => frame.frameName), Is.EqualTo(expectedNames));

                var flip = frames.Where(frame =>
                        frame.scenario == "Flip" &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .ToArray();
                Assert.That(
                    flip.Select(frame => frame.frameName),
                    Is.EqualTo(new[] { "PreContact", "Contact", "Peak", "Decay", "Rest" }));
                Assert.That(
                    flip.Single(frame => frame.frameName == "Contact").normalizedProgress,
                    Is.GreaterThanOrEqualTo(
                        GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
            }
        }

        private static void AssertPeakCaptureHashesDemonstrateMotionLevelApplication(
            IReadOnlyList<VisualEvidenceFrameRecord> frames,
            string scenario)
        {
            var peaks = frames
                .Where(frame => frame.scenario == scenario && frame.frameName == "Peak")
                .ToDictionary(frame => frame.cameraMotionLevel, frame => frame);
            Assert.That(peaks, Has.Count.EqualTo(3));
            Assert.That(peaks["Full"].sha256, Is.Not.EqualTo(peaks["Off"].sha256));
            Assert.That(
                peaks["Full"].additivePositionMagnitude,
                Is.GreaterThan(peaks["Reduced"].additivePositionMagnitude));
            Assert.That(peaks["Reduced"].additivePositionMagnitude, Is.GreaterThan(0d));
            Assert.That(peaks["Off"].additivePositionMagnitude, Is.EqualTo(0d).Within(0.0000001d));
        }

        private static void AssertSameTimelineAcrossMotionLevels(
            IReadOnlyList<VisualEvidenceFrameRecord> frames,
            string scenario)
        {
            var reference = frames
                .Where(frame =>
                    frame.scenario == scenario &&
                    frame.cameraMotionLevel == CameraMotionLevel.Off.ToString())
                .OrderBy(frame => frame.frameIndex)
                .ToArray();
            Assert.That(reference, Has.Length.EqualTo(5));

            foreach (var motionLevel in CameraShakeVisualMotionLevels)
            {
                var candidate = frames
                    .Where(frame =>
                        frame.scenario == scenario &&
                        frame.cameraMotionLevel == motionLevel.ToString())
                    .OrderBy(frame => frame.frameIndex)
                    .ToArray();
                Assert.That(candidate, Has.Length.EqualTo(reference.Length));
                for (var index = 0; index < reference.Length; index++)
                {
                    Assert.That(candidate[index].frameName, Is.EqualTo(reference[index].frameName));
                    Assert.That(candidate[index].tickIndex, Is.EqualTo(reference[index].tickIndex));
                    Assert.That(candidate[index].boxEntityId, Is.EqualTo(reference[index].boxEntityId));
                    Assert.That(
                        candidate[index].sourceActionPlanId,
                        Is.EqualTo(reference[index].sourceActionPlanId));
                    Assert.That(
                        candidate[index].presentationTime,
                        Is.EqualTo(reference[index].presentationTime).Within(0.0000001d));
                    Assert.That(
                        candidate[index].normalizedProgress,
                        Is.EqualTo(reference[index].normalizedProgress).Within(0.0000001d));
                }
            }
        }
    }
}
