using System;
using System.IO;
using Game.Feature.Gameplay.Tests.Support.Unity;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class VisualEvidenceManifestTests
    {
        private const string OnePixelPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        private string _temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                $"VisualEvidenceManifestTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, recursive: true);
            }
        }

        [Test]
        [Category("Extended")]
        public void ComposeScenarioFramePath_UsesDeterministicCanonicalName()
        {
            var path = VisualEvidenceManifestWriter.ComposeScenarioFramePath(
                _temporaryRoot,
                "push-full",
                2,
                "peak");

            Assert.That(
                path,
                Is.EqualTo(Path.Combine(_temporaryRoot, "push-full", "02-peak.png")));
        }

        [Test]
        [Category("Extended")]
        public void ComposeScenarioFramePath_RejectsTraversalSegments()
        {
            Assert.That(
                () => VisualEvidenceManifestWriter.ComposeScenarioFramePath(
                    _temporaryRoot,
                    "../outside",
                    0,
                    "pre-launch"),
                Throws.ArgumentException);
        }

        [Test]
        [Category("Extended")]
        public void ManifestWriter_AddFrameRejectsDuplicateArtifactPath()
        {
            var writer = CreateWriter();
            var frame = WriteFrame("push-full/00-pre-launch.png");
            writer.AddFrame(frame);

            Assert.That(
                () => writer.AddFrame(WriteFrame("push-full/00-pre-launch.png")),
                Throws.InvalidOperationException.With.Message.Contains("Duplicate"));
        }

        [Test]
        [Category("Extended")]
        public void ArtifactValidator_MissingPngFailsExplicitly()
        {
            Assert.That(
                () => VisualEvidenceArtifactValidator.ValidatePng(
                    Path.Combine(_temporaryRoot, "missing.png"),
                    1,
                    1),
                Throws.TypeOf<FileNotFoundException>());
        }

        [Test]
        [Category("Extended")]
        public void ManifestWriter_ValidArtifactWritesJsonWithoutOrphans()
        {
            var writer = CreateWriter();
            writer.AddFrame(WriteFrame("push-full/00-pre-launch.png"));

            var manifestPath = writer.WriteManifest();

            Assert.That(File.Exists(manifestPath), Is.True);
            var json = File.ReadAllText(manifestPath);
            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(json, Does.Contain("push-full/00-pre-launch.png"));
        }

        [Test]
        [Category("Extended")]
        public void ManifestWriter_RejectsPngOrphan()
        {
            var writer = CreateWriter();
            writer.AddFrame(WriteFrame("push-full/00-pre-launch.png"));
            WriteOnePixelPng("push-full/01-launch.png");

            Assert.That(
                () => writer.WriteManifest(),
                Throws.InvalidOperationException.With.Message.Contains("orphans=1"));
        }

        [Test]
        [Category("Extended")]
        public void ManifestMetadata_MissingRevisionFieldIsRejected()
        {
            var document = CreateDocument();
            document.trackedFingerprint = string.Empty;

            Assert.That(
                () => new VisualEvidenceManifestWriter(_temporaryRoot, document),
                Throws.InvalidOperationException.With.Message.Contains("metadata"));
        }

        private VisualEvidenceManifestWriter CreateWriter()
        {
            return new VisualEvidenceManifestWriter(_temporaryRoot, CreateDocument());
        }

        private static VisualEvidenceManifestDocument CreateDocument()
        {
            return new VisualEvidenceManifestDocument
            {
                timestamp = "2026-08-12T00:00:00Z",
                repository = "J2M",
                worktree = "/mnt/d/J2M/worktrees/test",
                branch = "test",
                head = new string('a', 40),
                tree = new string('b', 40),
                trackedFingerprint = new string('c', 64),
                untrackedFingerprint = new string('d', 64),
                unityVersion = "6000.3.11f1",
                cinemachineVersion = "3.1.6",
                graphicsApi = "Direct3D11",
                graphicsDevice = "Test GPU",
                renderPipeline = "UniversalRenderPipelineAsset",
                resolution = new VisualEvidenceResolutionMetadata { width = 1, height = 1 },
                captureBackend = "Camera.RenderTexture.ReadPixels.PNG",
                profilePath = "Assets/Test.asset",
                profileGuid = new string('e', 32),
                colorFormat = "R8G8B8A8_UNorm",
                colorSpace = "Linear",
                cameraHdr = false,
                antiAliasing = 1,
                hudCaptureCapability = "SECONDARY_LANE_REQUIRED",
            };
        }

        private VisualEvidenceFrameRecord WriteFrame(string relativePath)
        {
            var artifact = WriteOnePixelPng(relativePath);
            return new VisualEvidenceFrameRecord
            {
                scenario = "Push",
                cameraMotionLevel = "Full",
                frameName = "PreLaunch",
                frameIndex = 0,
                presentationTime = 0d,
                tickIndex = 1,
                pngPath = relativePath,
                width = 1,
                height = 1,
                pngBytes = artifact.ByteCount,
                sha256 = artifact.Sha256,
                pixelVariance = 0.1d,
                boxEntityId = 30,
                sourceActionPlanId = 1,
                trackStartTime = 0d,
                requestSubmitTime = 0d,
                normalizedProgress = 0d,
                contactNormalized = 0d,
                additivePositionMagnitude = 0d,
                additiveRotationDegrees = 0d,
            };
        }

        private VisualEvidenceArtifactValidation WriteOnePixelPng(string relativePath)
        {
            var path = Path.Combine(_temporaryRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));
            return VisualEvidenceArtifactValidator.ValidatePng(path, 1, 1);
        }
    }
}
