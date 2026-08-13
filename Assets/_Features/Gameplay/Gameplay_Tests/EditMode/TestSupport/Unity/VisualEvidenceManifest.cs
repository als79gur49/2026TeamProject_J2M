using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Support.Unity
{
    [Serializable]
    public sealed class VisualEvidenceResolutionMetadata
    {
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class VisualEvidenceManifestDocument
    {
        public int schemaVersion = 1;
        public string timestamp;
        public string repository;
        public string worktree;
        public string branch;
        public string head;
        public string tree;
        public string trackedFingerprint;
        public string untrackedFingerprint;
        public string unityVersion;
        public string cinemachineVersion;
        public string graphicsApi;
        public string graphicsDevice;
        public string renderPipeline;
        public VisualEvidenceResolutionMetadata resolution;
        public string captureBackend;
        public string profilePath;
        public string profileGuid;
        public string colorFormat;
        public string colorSpace;
        public bool cameraHdr;
        public int antiAliasing;
        public string hudCaptureCapability;
        public List<VisualEvidenceFrameRecord> frames = new();
    }

    [Serializable]
    public sealed class VisualEvidenceFrameRecord
    {
        public string scenario;
        public string cameraMotionLevel;
        public string frameName;
        public int frameIndex;
        public double presentationTime;
        public int tickIndex;
        public string pngPath;
        public int width;
        public int height;
        public long pngBytes;
        public string sha256;
        public double pixelVariance;
        public int boxEntityId;
        public int sourceActionPlanId;
        public double trackStartTime;
        public double requestSubmitTime;
        public double normalizedProgress;
        public double contactNormalized;
        public double additivePositionMagnitude;
        public double additiveRotationDegrees;
    }

    public readonly struct VisualEvidenceArtifactValidation
    {
        public VisualEvidenceArtifactValidation(
            string absolutePath,
            int width,
            int height,
            long byteCount,
            string sha256)
        {
            AbsolutePath = absolutePath;
            Width = width;
            Height = height;
            ByteCount = byteCount;
            Sha256 = sha256;
        }

        public string AbsolutePath { get; }
        public int Width { get; }
        public int Height { get; }
        public long ByteCount { get; }
        public string Sha256 { get; }
    }

    public static class VisualEvidenceArtifactValidator
    {
        private static readonly byte[] PngSignature =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        };

        public static VisualEvidenceArtifactValidation ValidatePng(
            string artifactPath,
            int expectedWidth,
            int expectedHeight,
            string expectedSha256 = null)
        {
            var absolutePath = Path.GetFullPath(
                artifactPath ?? throw new ArgumentNullException(nameof(artifactPath)));
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("Visual evidence PNG is missing.", absolutePath);
            }

            if (!string.Equals(Path.GetExtension(absolutePath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Visual evidence artifact must use .png: {absolutePath}");
            }

            var bytes = File.ReadAllBytes(absolutePath);
            if (bytes.Length < 24)
            {
                throw new InvalidOperationException($"PNG header is incomplete: {absolutePath}");
            }

            for (var index = 0; index < PngSignature.Length; index++)
            {
                if (bytes[index] != PngSignature[index])
                {
                    throw new InvalidOperationException(
                        $"Artifact does not have a valid PNG signature: {absolutePath}");
                }
            }

            var width = ReadNetworkInt32(bytes, 16);
            var height = ReadNetworkInt32(bytes, 20);
            if (width != expectedWidth || height != expectedHeight)
            {
                throw new InvalidOperationException(
                    $"PNG dimensions are {width}x{height}; expected {expectedWidth}x{expectedHeight}.");
            }

            var sha256 = ComputeSha256(bytes);
            if (!string.IsNullOrWhiteSpace(expectedSha256) &&
                !string.Equals(sha256, expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PNG SHA-256 mismatch for {absolutePath}.");
            }

            return new VisualEvidenceArtifactValidation(
                absolutePath,
                width,
                height,
                bytes.LongLength,
                sha256);
        }

        private static int ReadNetworkInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }

    public sealed class VisualEvidenceManifestWriter
    {
        private readonly VisualEvidenceManifestDocument _document;
        private readonly string _evidenceRoot;
        private readonly string _evidenceRootPrefix;
        private readonly HashSet<string> _artifactPaths = new(StringComparer.Ordinal);

        public VisualEvidenceManifestWriter(
            string evidenceRoot,
            VisualEvidenceManifestDocument document)
        {
            _evidenceRoot = Path.GetFullPath(
                evidenceRoot ?? throw new ArgumentNullException(nameof(evidenceRoot)));
            _evidenceRootPrefix = _evidenceRoot.TrimEnd(
                                      Path.DirectorySeparatorChar,
                                      Path.AltDirectorySeparatorChar) +
                                  Path.DirectorySeparatorChar;
            _document = document ?? throw new ArgumentNullException(nameof(document));
            ValidateRequiredMetadata(document);
            Directory.CreateDirectory(_evidenceRoot);
        }

        public IReadOnlyList<VisualEvidenceFrameRecord> Frames => _document.frames;

        public static string ComposeScenarioFramePath(
            string evidenceRoot,
            string scenarioDirectory,
            int frameIndex,
            string frameStem)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }

            ValidatePathSegment(scenarioDirectory, nameof(scenarioDirectory));
            ValidatePathSegment(frameStem, nameof(frameStem));
            return Path.Combine(
                Path.GetFullPath(evidenceRoot),
                scenarioDirectory,
                $"{frameIndex:00}-{frameStem}.png");
        }

        public void AddFrame(VisualEvidenceFrameRecord frame)
        {
            ValidateFrame(frame);
            var relativePath = NormalizeRelativePath(frame.pngPath);
            var absolutePath = ResolveInsideEvidenceRoot(relativePath);
            if (!_artifactPaths.Add(relativePath))
            {
                throw new InvalidOperationException(
                    $"Duplicate visual evidence artifact path: {relativePath}");
            }

            var artifact = VisualEvidenceArtifactValidator.ValidatePng(
                absolutePath,
                frame.width,
                frame.height,
                frame.sha256);
            if (artifact.ByteCount != frame.pngBytes)
            {
                throw new InvalidOperationException(
                    $"PNG byte count mismatch for {relativePath}.");
            }

            frame.pngPath = relativePath;
            _document.frames.Add(frame);
        }

        public string WriteManifest(string fileName = "manifest.json")
        {
            ValidatePathSegment(Path.GetFileNameWithoutExtension(fileName), nameof(fileName));
            if (!string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Visual evidence manifest must use .json.");
            }

            var diskArtifacts = Directory
                .EnumerateFiles(_evidenceRoot, "*.png", SearchOption.AllDirectories)
                .Select(path => NormalizeRelativePath(Path.GetRelativePath(_evidenceRoot, path)))
                .ToHashSet(StringComparer.Ordinal);
            var missing = _artifactPaths.Except(diskArtifacts, StringComparer.Ordinal).ToArray();
            var orphans = diskArtifacts.Except(_artifactPaths, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0 || orphans.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Manifest/artifact mismatch: missing={missing.Length}, orphans={orphans.Length}.");
            }

            var manifestPath = Path.Combine(_evidenceRoot, fileName);
            var json = JsonUtility.ToJson(_document, prettyPrint: true);
            using (var stream = new FileStream(
                       manifestPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
            }

            return manifestPath;
        }

        private string ResolveInsideEvidenceRoot(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(_evidenceRoot, relativePath));
            if (!absolutePath.StartsWith(_evidenceRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Visual evidence path escapes the evidence root: {relativePath}");
            }

            return absolutePath;
        }

        private static string NormalizeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
            {
                throw new InvalidOperationException(
                    "Visual evidence pngPath must be a non-empty relative path.");
            }

            return value.Replace('\\', '/');
        }

        private static void ValidateRequiredMetadata(VisualEvidenceManifestDocument document)
        {
            if (document.schemaVersion <= 0 ||
                document.resolution == null ||
                document.resolution.width <= 0 ||
                document.resolution.height <= 0)
            {
                throw new InvalidOperationException(
                    "Visual evidence manifest requires a schema version and positive resolution.");
            }

            var required = new[]
            {
                document.timestamp,
                document.repository,
                document.worktree,
                document.branch,
                document.head,
                document.tree,
                document.trackedFingerprint,
                document.untrackedFingerprint,
                document.unityVersion,
                document.cinemachineVersion,
                document.graphicsApi,
                document.graphicsDevice,
                document.captureBackend,
                document.profilePath,
                document.profileGuid,
            };
            if (required.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    "Visual evidence manifest is missing required revision, graphics, or profile metadata.");
            }
        }

        private static void ValidateFrame(VisualEvidenceFrameRecord frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (string.IsNullOrWhiteSpace(frame.scenario) ||
                string.IsNullOrWhiteSpace(frame.cameraMotionLevel) ||
                string.IsNullOrWhiteSpace(frame.frameName) ||
                string.IsNullOrWhiteSpace(frame.pngPath) ||
                string.IsNullOrWhiteSpace(frame.sha256))
            {
                throw new InvalidOperationException(
                    "Visual evidence frame requires scenario, motion level, marker, path, and hash.");
            }

            if (frame.frameIndex < 0 ||
                frame.tickIndex < 0 ||
                frame.width <= 0 ||
                frame.height <= 0 ||
                frame.pngBytes <= 0 ||
                !IsFinite(frame.presentationTime) ||
                !IsFinite(frame.pixelVariance) ||
                !IsFinite(frame.trackStartTime) ||
                !IsFinite(frame.requestSubmitTime) ||
                !IsFinite(frame.normalizedProgress) ||
                !IsFinite(frame.contactNormalized) ||
                !IsFinite(frame.additivePositionMagnitude) ||
                !IsFinite(frame.additiveRotationDegrees))
            {
                throw new InvalidOperationException(
                    "Visual evidence frame contains invalid dimensions, indices, byte counts, or numeric metadata.");
            }
        }

        private static void ValidatePathSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value == "." ||
                value == ".." ||
                value.IndexOfAny(new[] { '/', '\\' }) >= 0 ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Visual evidence path segment is invalid.",
                    parameterName);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
