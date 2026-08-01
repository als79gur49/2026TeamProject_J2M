using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    internal readonly struct TerminalIrisContourAnalysis
    {
        internal TerminalIrisContourAnalysis(
            Vector2 fittedCenterPixels,
            Vector2 fittedCenterViewport,
            float fittedRadiusPixels,
            float centerErrorPixels,
            float rmsRadialErrorPixels,
            float maximumRadialErrorPixels,
            float p99RadialErrorPixels,
            int transparentPixelCount,
            int transparentComponentCount,
            int unexpectedTransparentComponentCount,
            int opaquePinholePixelCount,
            int transparentArtifactPixelCount,
            int contourPointCount)
        {
            FittedCenterPixels = fittedCenterPixels;
            FittedCenterViewport = fittedCenterViewport;
            FittedRadiusPixels = fittedRadiusPixels;
            CenterErrorPixels = centerErrorPixels;
            RmsRadialErrorPixels = rmsRadialErrorPixels;
            MaximumRadialErrorPixels = maximumRadialErrorPixels;
            P99RadialErrorPixels = p99RadialErrorPixels;
            TransparentPixelCount = transparentPixelCount;
            TransparentComponentCount = transparentComponentCount;
            UnexpectedTransparentComponentCount = unexpectedTransparentComponentCount;
            OpaquePinholePixelCount = opaquePinholePixelCount;
            TransparentArtifactPixelCount = transparentArtifactPixelCount;
            ContourPointCount = contourPointCount;
        }

        internal Vector2 FittedCenterPixels { get; }

        internal Vector2 FittedCenterViewport { get; }

        internal float FittedRadiusPixels { get; }

        internal float CenterErrorPixels { get; }

        internal float RmsRadialErrorPixels { get; }

        internal float MaximumRadialErrorPixels { get; }

        internal float P99RadialErrorPixels { get; }

        internal int TransparentPixelCount { get; }

        internal int TransparentComponentCount { get; }

        internal int UnexpectedTransparentComponentCount { get; }

        internal int OpaquePinholePixelCount { get; }

        internal int TransparentArtifactPixelCount { get; }

        internal int ContourPointCount { get; }
    }

    internal sealed class TerminalIrisFrameSample
    {
        internal TerminalIrisFrameSample(
            int renderSequenceIndex,
            int frameCount,
            float unscaledTime,
            float inputRadius,
            float effectiveRadius,
            float closedOvershootPixels,
            bool authoringExactClosed,
            int transparentPixelCount,
            float contourRadiusPixels,
            Color32[] pixels,
            Vector2 materialCenter = default,
            int materialInstanceId = 0,
            int materialApplicationFrame = 0,
            int renderAcknowledgementFrame = 0)
        {
            RenderSequenceIndex = renderSequenceIndex;
            FrameCount = frameCount;
            UnscaledTime = unscaledTime;
            InputRadius = inputRadius;
            EffectiveRadius = effectiveRadius;
            ClosedOvershootPixels = closedOvershootPixels;
            AuthoringExactClosed = authoringExactClosed;
            TransparentPixelCount = transparentPixelCount;
            ContourRadiusPixels = contourRadiusPixels;
            Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
            MaterialCenter = materialCenter;
            MaterialInstanceId = materialInstanceId;
            MaterialApplicationFrame = materialApplicationFrame;
            RenderAcknowledgementFrame = renderAcknowledgementFrame;
        }

        internal int RenderSequenceIndex { get; }
        internal int FrameCount { get; }
        internal float UnscaledTime { get; }
        internal float InputRadius { get; }
        internal float EffectiveRadius { get; }
        internal float ClosedOvershootPixels { get; }
        internal bool AuthoringExactClosed { get; }
        internal int TransparentPixelCount { get; }
        internal float ContourRadiusPixels { get; }
        internal Color32[] Pixels { get; }
        internal Vector2 MaterialCenter { get; }
        internal int MaterialInstanceId { get; }
        internal int MaterialApplicationFrame { get; }
        internal int RenderAcknowledgementFrame { get; }
    }

    internal readonly struct TerminalIrisFrameDelta
    {
        internal TerminalIrisFrameDelta(
            int fromSequenceIndex,
            int toSequenceIndex,
            int changedPixelCount,
            int maxChannelDelta,
            RectInt changedBounds,
            float meanAbsoluteChannelDelta,
            float meanLuminanceDelta,
            float p99LuminanceDelta,
            int unexpectedChromaShiftPixelCount,
            int changedComponentCount)
        {
            FromSequenceIndex = fromSequenceIndex;
            ToSequenceIndex = toSequenceIndex;
            ChangedPixelCount = changedPixelCount;
            MaxChannelDelta = maxChannelDelta;
            ChangedBounds = changedBounds;
            MeanAbsoluteChannelDelta = meanAbsoluteChannelDelta;
            MeanLuminanceDelta = meanLuminanceDelta;
            P99LuminanceDelta = p99LuminanceDelta;
            UnexpectedChromaShiftPixelCount = unexpectedChromaShiftPixelCount;
            ChangedComponentCount = changedComponentCount;
        }

        internal int FromSequenceIndex { get; }
        internal int ToSequenceIndex { get; }
        internal int ChangedPixelCount { get; }
        internal int MaxChannelDelta { get; }
        internal RectInt ChangedBounds { get; }
        internal float MeanAbsoluteChannelDelta { get; }
        internal float MeanLuminanceDelta { get; }
        internal float P99LuminanceDelta { get; }
        internal int UnexpectedChromaShiftPixelCount { get; }
        internal int ChangedComponentCount { get; }
    }

    internal sealed class TerminalIrisFrameSelection
    {
        internal TerminalIrisFrameSelection(
            int lastAnimatedParameterFrame,
            int lastPixelChangingFrame,
            int firstExactClosedFrame,
            IReadOnlyList<TerminalIrisFrameDelta> deltas,
            bool hasMissingFrameIndex,
            bool hasDuplicateFrameIndex,
            bool changedAfterStableClosed)
        {
            LastAnimatedParameterFrame = lastAnimatedParameterFrame;
            LastPixelChangingFrame = lastPixelChangingFrame;
            FirstExactClosedFrame = firstExactClosedFrame;
            Deltas = deltas;
            HasMissingFrameIndex = hasMissingFrameIndex;
            HasDuplicateFrameIndex = hasDuplicateFrameIndex;
            ChangedAfterStableClosed = changedAfterStableClosed;
        }

        internal int LastAnimatedParameterFrame { get; }
        internal int LastPixelChangingFrame { get; }
        internal int FirstExactClosedFrame { get; }
        internal IReadOnlyList<TerminalIrisFrameDelta> Deltas { get; }
        internal bool HasMissingFrameIndex { get; }
        internal bool HasDuplicateFrameIndex { get; }
        internal bool ChangedAfterStableClosed { get; }
    }

    internal static class TerminalIrisEvidenceAnalyzer
    {
        private const float ContourThreshold = 0.5f;
        private const float TransparentThreshold = 0.1f;
        private const float OpaqueThreshold = 0.9f;
        private const float ArtifactInsetPixels = 2f;

        internal static float[] ResolveCoverage(
            IReadOnlyList<Color32> pixels,
            Color background,
            Color opaqueCover)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

            var backgroundVector = new Vector3(background.r, background.g, background.b);
            var coverVector = new Vector3(opaqueCover.r, opaqueCover.g, opaqueCover.b);
            var coverDelta = coverVector - backgroundVector;
            var denominator = Vector3.Dot(coverDelta, coverDelta);
            if (denominator <= 0.000001f)
            {
                throw new ArgumentException(
                    "Opaque cover and capture background must have different RGB values.");
            }

            var coverage = new float[pixels.Count];
            for (var index = 0; index < pixels.Count; index++)
            {
                var pixel = pixels[index];
                var sample = new Vector3(
                    pixel.r / 255f,
                    pixel.g / 255f,
                    pixel.b / 255f);
                coverage[index] = Mathf.Clamp01(
                    Vector3.Dot(sample - backgroundVector, coverDelta) / denominator);
            }

            return coverage;
        }

        internal static TerminalIrisContourAnalysis AnalyzeCoverage(
            IReadOnlyList<float> coverage,
            int width,
            int height,
            Vector2 expectedCenterViewport)
        {
            ValidateDimensions(coverage, width, height);
            var contourPoints = ExtractContourPoints(coverage, width, height);
            if (contourPoints.Count < 16)
            {
                throw new InvalidOperationException(
                    $"Terminal Iris alpha-0.5 contour has only {contourPoints.Count} points.");
            }

            var circle = FitCircle(contourPoints);
            var radialErrors = new float[contourPoints.Count];
            double squaredErrorSum = 0d;
            for (var index = 0; index < contourPoints.Count; index++)
            {
                var error = Mathf.Abs(
                    Vector2.Distance(contourPoints[index], circle.Center) - circle.Radius);
                radialErrors[index] = error;
                squaredErrorSum += error * error;
            }

            Array.Sort(radialErrors);
            var expectedCenterPixels = new Vector2(
                expectedCenterViewport.x * Math.Max(1, width - 1),
                expectedCenterViewport.y * Math.Max(1, height - 1));
            var transparentPixelCount = 0;
            var opaquePinholePixelCount = 0;
            var transparentArtifactPixelCount = 0;
            var innerRadius = Mathf.Max(0f, circle.Radius - ArtifactInsetPixels);
            var outerRadius = circle.Radius + ArtifactInsetPixels;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = coverage[y * width + x];
                    if (value < TransparentThreshold)
                    {
                        transparentPixelCount++;
                    }

                    var distance = Vector2.Distance(new Vector2(x, y), circle.Center);
                    if (distance < innerRadius && value > OpaqueThreshold)
                    {
                        opaquePinholePixelCount++;
                    }
                    else if (distance > outerRadius && value < TransparentThreshold)
                    {
                        transparentArtifactPixelCount++;
                    }
                }
            }

            var transparentComponentCount = CountComponents(
                coverage,
                width,
                height,
                value => value < TransparentThreshold);
            return new TerminalIrisContourAnalysis(
                circle.Center,
                new Vector2(
                    circle.Center.x / Math.Max(1, width - 1),
                    circle.Center.y / Math.Max(1, height - 1)),
                circle.Radius,
                Vector2.Distance(circle.Center, expectedCenterPixels),
                Mathf.Sqrt((float)(squaredErrorSum / radialErrors.Length)),
                radialErrors[radialErrors.Length - 1],
                Percentile(radialErrors, 0.99f),
                transparentPixelCount,
                transparentComponentCount,
                Mathf.Max(0, transparentComponentCount - 1),
                opaquePinholePixelCount,
                transparentArtifactPixelCount,
                contourPoints.Count);
        }

        internal static float[] CreateSyntheticCoverage(
            int width,
            int height,
            Vector2 centerViewport,
            float radiusViewportHeight,
            float edgeWidthPixels)
        {
            if (width <= 1 || height <= 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "Synthetic fixture dimensions must exceed one pixel.");
            }

            var center = new Vector2(
                centerViewport.x * (width - 1),
                centerViewport.y * (height - 1));
            var radius = radiusViewportHeight * height;
            var coverage = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var signedDistance = Vector2.Distance(new Vector2(x, y), center) - radius;
                    float value;
                    if (edgeWidthPixels <= 0f)
                    {
                        value = signedDistance >= 0f ? 1f : 0f;
                    }
                    else
                    {
                        value = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(
                                -edgeWidthPixels * 0.5f,
                                edgeWidthPixels * 0.5f,
                                signedDistance));
                    }

                    coverage[y * width + x] = value;
                }
            }

            return coverage;
        }

        internal static TerminalIrisFrameSelection AnalyzeFrameSequence(
            IReadOnlyList<TerminalIrisFrameSample> frames,
            int width,
            int height,
            Color32 opaqueCover,
            int channelTolerance = 0)
        {
            if (frames == null || frames.Count < 2)
            {
                throw new ArgumentException("At least two rendered frames are required.");
            }

            var deltas = new List<TerminalIrisFrameDelta>(frames.Count - 1);
            var missing = false;
            var duplicate = false;
            var lastAnimated = -1;
            var lastChanging = -1;
            for (var index = 0; index < frames.Count; index++)
            {
                if (frames[index].Pixels.Length != width * height)
                {
                    throw new ArgumentException(
                        $"Frame {frames[index].RenderSequenceIndex} has invalid dimensions.");
                }

                if (index == 0)
                {
                    continue;
                }

                var sequenceDelta =
                    frames[index].RenderSequenceIndex -
                    frames[index - 1].RenderSequenceIndex;
                missing |= sequenceDelta > 1;
                duplicate |= sequenceDelta <= 0;
                if (ParameterChanged(frames[index - 1], frames[index]))
                {
                    lastAnimated = index - 1;
                }

                var delta = AnalyzeFramePair(
                    frames[index - 1],
                    frames[index],
                    width,
                    height,
                    opaqueCover,
                    channelTolerance);
                deltas.Add(delta);
                if (delta.ChangedPixelCount > 0)
                {
                    lastChanging = index - 1;
                }
            }

            var firstExactClosed = -1;
            for (var index = 0; index + 1 < frames.Count; index++)
            {
                if (frames[index].AuthoringExactClosed &&
                    frames[index].TransparentPixelCount == 0 &&
                    frames[index].ContourRadiusPixels <= 0f &&
                    deltas[index].ChangedPixelCount == 0)
                {
                    firstExactClosed = index;
                    break;
                }
            }

            var changedAfterStableClosed = false;
            if (firstExactClosed >= 0)
            {
                for (var index = firstExactClosed; index < deltas.Count; index++)
                {
                    changedAfterStableClosed |= deltas[index].ChangedPixelCount > 0;
                }
            }

            return new TerminalIrisFrameSelection(
                lastAnimated,
                lastChanging,
                firstExactClosed,
                deltas,
                missing,
                duplicate,
                changedAfterStableClosed);
        }

        private static TerminalIrisFrameDelta AnalyzeFramePair(
            TerminalIrisFrameSample first,
            TerminalIrisFrameSample second,
            int width,
            int height,
            Color32 opaqueCover,
            int channelTolerance)
        {
            var changed = new bool[first.Pixels.Length];
            var luminanceDeltas = new List<float>();
            long absoluteChannelDelta = 0;
            double luminanceDeltaSum = 0d;
            var changedCount = 0;
            var maxChannelDelta = 0;
            var unexpectedChroma = 0;
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var index = 0; index < first.Pixels.Length; index++)
            {
                var before = first.Pixels[index];
                var after = second.Pixels[index];
                var dr = Math.Abs(after.r - before.r);
                var dg = Math.Abs(after.g - before.g);
                var db = Math.Abs(after.b - before.b);
                var da = Math.Abs(after.a - before.a);
                var maximum = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                if (maximum <= channelTolerance)
                {
                    continue;
                }

                changed[index] = true;
                changedCount++;
                maxChannelDelta = Math.Max(maxChannelDelta, maximum);
                absoluteChannelDelta += dr + dg + db + da;
                var luminanceDelta = Math.Abs(
                    Luminance(after) - Luminance(before));
                luminanceDeltas.Add(luminanceDelta);
                luminanceDeltaSum += luminanceDelta;
                var x = index % width;
                var y = index / width;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                if (ColorDistanceSquared(after, opaqueCover) >
                    ColorDistanceSquared(before, opaqueCover) + 1)
                {
                    unexpectedChroma++;
                }
            }

            luminanceDeltas.Sort();
            var bounds = changedCount == 0
                ? new RectInt(0, 0, 0, 0)
                : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return new TerminalIrisFrameDelta(
                first.RenderSequenceIndex,
                second.RenderSequenceIndex,
                changedCount,
                maxChannelDelta,
                bounds,
                changedCount == 0
                    ? 0f
                    : (float)absoluteChannelDelta / (changedCount * 4f),
                changedCount == 0 ? 0f : (float)(luminanceDeltaSum / changedCount),
                changedCount == 0
                    ? 0f
                    : Percentile(luminanceDeltas, 0.99f),
                unexpectedChroma,
                CountComponents(changed, width, height, value => value));
        }

        private static bool ParameterChanged(
            TerminalIrisFrameSample first,
            TerminalIrisFrameSample second)
        {
            return !Mathf.Approximately(first.InputRadius, second.InputRadius) ||
                   !Mathf.Approximately(first.EffectiveRadius, second.EffectiveRadius) ||
                   !Mathf.Approximately(
                       first.ClosedOvershootPixels,
                       second.ClosedOvershootPixels);
        }

        private static float Luminance(Color32 color)
        {
            return (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
        }

        private static int ColorDistanceSquared(Color32 first, Color32 second)
        {
            var dr = first.r - second.r;
            var dg = first.g - second.g;
            var db = first.b - second.b;
            return dr * dr + dg * dg + db * db;
        }

        private static List<Vector2> ExtractContourPoints(
            IReadOnlyList<float> coverage,
            int width,
            int height)
        {
            var points = new List<Vector2>((width + height) * 4);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x + 1 < width; x++)
                {
                    var left = coverage[y * width + x];
                    var right = coverage[y * width + x + 1];
                    if (!CrossesContour(left, right))
                    {
                        continue;
                    }

                    points.Add(new Vector2(
                        x + ResolveCrossingFraction(left, right),
                        y));
                }
            }

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y + 1 < height; y++)
                {
                    var lower = coverage[y * width + x];
                    var upper = coverage[(y + 1) * width + x];
                    if (!CrossesContour(lower, upper))
                    {
                        continue;
                    }

                    points.Add(new Vector2(
                        x,
                        y + ResolveCrossingFraction(lower, upper)));
                }
            }

            return points;
        }

        private static bool CrossesContour(float first, float second)
        {
            return (first < ContourThreshold && second >= ContourThreshold) ||
                   (first >= ContourThreshold && second < ContourThreshold);
        }

        private static float ResolveCrossingFraction(float first, float second)
        {
            var difference = second - first;
            return Mathf.Abs(difference) <= 0.000001f
                ? 0.5f
                : Mathf.Clamp01((ContourThreshold - first) / difference);
        }

        private static Circle FitCircle(IReadOnlyList<Vector2> points)
        {
            double count = points.Count;
            double sumX = 0d;
            double sumY = 0d;
            double sumXX = 0d;
            double sumXY = 0d;
            double sumYY = 0d;
            double sumRadiusSquared = 0d;
            double sumXRadiusSquared = 0d;
            double sumYRadiusSquared = 0d;
            for (var index = 0; index < points.Count; index++)
            {
                var x = (double)points[index].x;
                var y = (double)points[index].y;
                var radiusSquared = x * x + y * y;
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
                sumYY += y * y;
                sumRadiusSquared += radiusSquared;
                sumXRadiusSquared += x * radiusSquared;
                sumYRadiusSquared += y * radiusSquared;
            }

            var solution = SolveLinear3(
                new[,]
                {
                    { count, sumX, sumY },
                    { sumX, sumXX, sumXY },
                    { sumY, sumXY, sumYY },
                },
                new[]
                {
                    -sumRadiusSquared,
                    -sumXRadiusSquared,
                    -sumYRadiusSquared,
                });
            var center = new Vector2(
                (float)(-0.5d * solution[1]),
                (float)(-0.5d * solution[2]));
            var radius = Mathf.Sqrt(
                Mathf.Max(0f, center.sqrMagnitude - (float)solution[0]));
            return new Circle(center, radius);
        }

        private static double[] SolveLinear3(double[,] matrix, double[] vector)
        {
            var augmented = new double[3, 4];
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    augmented[row, column] = matrix[row, column];
                }

                augmented[row, 3] = vector[row];
            }

            for (var pivot = 0; pivot < 3; pivot++)
            {
                var largest = pivot;
                for (var row = pivot + 1; row < 3; row++)
                {
                    if (Math.Abs(augmented[row, pivot]) >
                        Math.Abs(augmented[largest, pivot]))
                    {
                        largest = row;
                    }
                }

                if (Math.Abs(augmented[largest, pivot]) <= 0.000000001d)
                {
                    throw new InvalidOperationException(
                        "Terminal Iris contour circle-fit matrix is singular.");
                }

                if (largest != pivot)
                {
                    for (var column = pivot; column < 4; column++)
                    {
                        var temporary = augmented[pivot, column];
                        augmented[pivot, column] = augmented[largest, column];
                        augmented[largest, column] = temporary;
                    }
                }

                var divisor = augmented[pivot, pivot];
                for (var column = pivot; column < 4; column++)
                {
                    augmented[pivot, column] /= divisor;
                }

                for (var row = 0; row < 3; row++)
                {
                    if (row == pivot)
                    {
                        continue;
                    }

                    var factor = augmented[row, pivot];
                    for (var column = pivot; column < 4; column++)
                    {
                        augmented[row, column] -= factor * augmented[pivot, column];
                    }
                }
            }

            return new[]
            {
                augmented[0, 3],
                augmented[1, 3],
                augmented[2, 3],
            };
        }

        private static int CountComponents<T>(
            IReadOnlyList<T> values,
            int width,
            int height,
            Func<T, bool> predicate)
        {
            var visited = new bool[values.Count];
            var queue = new Queue<int>();
            var components = 0;
            for (var start = 0; start < values.Count; start++)
            {
                if (visited[start] || !predicate(values[start]))
                {
                    continue;
                }

                components++;
                visited[start] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var x = current % width;
                    var y = current / width;
                    Enqueue(x - 1, y);
                    Enqueue(x + 1, y);
                    Enqueue(x, y - 1);
                    Enqueue(x, y + 1);
                }
            }

            return components;

            void Enqueue(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    return;
                }

                var index = y * width + x;
                if (visited[index] || !predicate(values[index]))
                {
                    return;
                }

                visited[index] = true;
                queue.Enqueue(index);
            }
        }

        private static float Percentile(IReadOnlyList<float> sortedValues, float percentile)
        {
            var position = Mathf.Clamp01(percentile) * (sortedValues.Count - 1);
            var lower = Mathf.FloorToInt(position);
            var upper = Mathf.CeilToInt(position);
            return Mathf.Lerp(
                sortedValues[lower],
                sortedValues[upper],
                position - lower);
        }

        private static void ValidateDimensions(
            IReadOnlyList<float> coverage,
            int width,
            int height)
        {
            if (coverage == null)
            {
                throw new ArgumentNullException(nameof(coverage));
            }

            if (width <= 1 || height <= 1 || coverage.Count != width * height)
            {
                throw new ArgumentException(
                    $"Coverage dimensions are invalid: {width}x{height}, pixels={coverage.Count}.");
            }
        }

        private readonly struct Circle
        {
            internal Circle(Vector2 center, float radius)
            {
                Center = center;
                Radius = radius;
            }

            internal Vector2 Center { get; }

            internal float Radius { get; }
        }
    }
}
