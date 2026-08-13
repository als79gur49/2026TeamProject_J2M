using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Support.Unity
{
    public readonly struct VisualEvidenceCaptureResult
    {
        public VisualEvidenceCaptureResult(
            string absolutePath,
            int width,
            int height,
            long pngByteCount,
            string sha256,
            double luminanceVariance,
            byte minimumAlpha,
            byte maximumAlpha,
            string colorFormat,
            string colorSpace,
            bool cameraHdr,
            int antiAliasing)
        {
            AbsolutePath = absolutePath;
            Width = width;
            Height = height;
            PngByteCount = pngByteCount;
            Sha256 = sha256;
            LuminanceVariance = luminanceVariance;
            MinimumAlpha = minimumAlpha;
            MaximumAlpha = maximumAlpha;
            ColorFormat = colorFormat;
            ColorSpace = colorSpace;
            CameraHdr = cameraHdr;
            AntiAliasing = antiAliasing;
        }

        public string AbsolutePath { get; }

        public int Width { get; }

        public int Height { get; }

        public long PngByteCount { get; }

        public string Sha256 { get; }

        public double LuminanceVariance { get; }

        public byte MinimumAlpha { get; }

        public byte MaximumAlpha { get; }

        public string ColorFormat { get; }

        public string ColorSpace { get; }

        public bool CameraHdr { get; }

        public int AntiAliasing { get; }

        public string VarianceInvariant =>
            LuminanceVariance.ToString("R", CultureInfo.InvariantCulture);
    }

    public static class VisualEvidenceFrameCapture
    {
        public const int MinimumPngByteCount = 128;
        public const double MinimumLuminanceVariance = 0.000001d;

        private static readonly byte[] PngSignature =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        };

        public static VisualEvidenceCaptureResult CaptureFrame(
            Camera camera,
            string artifactPath,
            int width,
            int height)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException(
                    "Visual evidence capture requires a non-null graphics device.");
            }

            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "Visual evidence dimensions must be positive.");
            }

            var absolutePath = Path.GetFullPath(
                artifactPath ?? throw new ArgumentNullException(nameof(artifactPath)));
            if (!string.Equals(Path.GetExtension(absolutePath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Visual evidence artifact must use the .png extension: {absolutePath}");
            }

            if (File.Exists(absolutePath))
            {
                throw new IOException($"Visual evidence artifact already exists: {absolutePath}");
            }

            var directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    $"Visual evidence artifact has no parent directory: {absolutePath}");
            }

            Directory.CreateDirectory(directory);

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = $"VisualEvidence_{width}x{height}",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };
            Texture2D readback = null;
            Texture2D decoded = null;
            try
            {
                if (!renderTexture.Create() || !renderTexture.IsCreated())
                {
                    throw new InvalidOperationException(
                        $"RenderTexture creation failed for {width}x{height}.");
                }

                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);
                var pixels = readback.GetPixels32();
                var variance = MeasureLuminanceVariance(
                    pixels,
                    out var minimumAlpha,
                    out var maximumAlpha);
                if (variance <= MinimumLuminanceVariance)
                {
                    throw new InvalidOperationException(
                        $"Rendered frame is blank or single-color (variance={variance:R}).");
                }
                if (maximumAlpha == 0)
                {
                    throw new InvalidOperationException(
                        "Rendered frame is fully transparent.");
                }

                var pngBytes = readback.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length < MinimumPngByteCount)
                {
                    throw new InvalidOperationException(
                        $"PNG encoder returned an undersized artifact ({pngBytes?.Length ?? 0} bytes).");
                }

                ValidatePngSignature(pngBytes);
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!decoded.LoadImage(pngBytes, markNonReadable: false) ||
                    decoded.width != width ||
                    decoded.height != height)
                {
                    throw new InvalidOperationException(
                        $"PNG decode did not return the expected {width}x{height} dimensions.");
                }

                using (var stream = new FileStream(
                           absolutePath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(pngBytes, 0, pngBytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                if (!File.Exists(absolutePath) || new FileInfo(absolutePath).Length != pngBytes.LongLength)
                {
                    throw new IOException(
                        $"PNG write completion could not be verified: {absolutePath}");
                }

                return new VisualEvidenceCaptureResult(
                    absolutePath,
                    width,
                    height,
                    pngBytes.LongLength,
                    ComputeSha256(pngBytes),
                    variance,
                    minimumAlpha,
                    maximumAlpha,
                    renderTexture.graphicsFormat.ToString(),
                    QualitySettings.activeColorSpace.ToString(),
                    camera.allowHDR,
                    renderTexture.antiAliasing);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (decoded != null)
                {
                    Object.DestroyImmediate(decoded);
                }

                if (readback != null)
                {
                    Object.DestroyImmediate(readback);
                }

                if (renderTexture.IsCreated())
                {
                    renderTexture.Release();
                }

                Object.DestroyImmediate(renderTexture);
            }
        }

        public static void ValidatePngFile(
            string artifactPath,
            int expectedWidth,
            int expectedHeight,
            string expectedSha256)
        {
            var bytes = File.ReadAllBytes(artifactPath);
            ValidatePngSignature(bytes);
            if (bytes.Length < 24)
            {
                throw new InvalidOperationException($"PNG header is incomplete: {artifactPath}");
            }

            var width = ReadNetworkInt32(bytes, 16);
            var height = ReadNetworkInt32(bytes, 20);
            if (width != expectedWidth || height != expectedHeight)
            {
                throw new InvalidOperationException(
                    $"PNG dimensions are {width}x{height}; expected {expectedWidth}x{expectedHeight}.");
            }

            var sha256 = ComputeSha256(bytes);
            if (!string.Equals(sha256, expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PNG SHA-256 mismatch for {artifactPath}.");
            }
        }

        private static double MeasureLuminanceVariance(
            Color32[] pixels,
            out byte minimumAlpha,
            out byte maximumAlpha)
        {
            if (pixels == null || pixels.Length == 0)
            {
                throw new InvalidOperationException("Rendered frame contains no pixels.");
            }

            minimumAlpha = byte.MaxValue;
            maximumAlpha = byte.MinValue;
            double sum = 0d;
            double squaredSum = 0d;
            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                minimumAlpha = Math.Min(minimumAlpha, pixel.a);
                maximumAlpha = Math.Max(maximumAlpha, pixel.a);
                var luminance =
                    (0.2126d * pixel.r + 0.7152d * pixel.g + 0.0722d * pixel.b) / 255d;
                sum += luminance;
                squaredSum += luminance * luminance;
            }

            var mean = sum / pixels.Length;
            return Math.Max(0d, squaredSum / pixels.Length - mean * mean);
        }

        private static void ValidatePngSignature(byte[] bytes)
        {
            if (bytes == null || bytes.Length < PngSignature.Length)
            {
                throw new InvalidOperationException("PNG payload is missing or truncated.");
            }

            for (var index = 0; index < PngSignature.Length; index++)
            {
                if (bytes[index] != PngSignature[index])
                {
                    throw new InvalidOperationException("Artifact does not have a valid PNG signature.");
                }
            }
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
}
