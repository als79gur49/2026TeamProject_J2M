#if VECTORQUAKE_CAPTURE_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Feature.Gameplay.Host;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Game.Feature.UI.Composition
{
    internal sealed class GameplayPerformancePlayerProbe : MonoBehaviour
    {
        internal const string LaunchArgument = "--gameplay-performance";
        internal const string OutputArgument = "--gameplay-performance-output";
        internal const string SampleFramesArgument = "--gameplay-performance-sample-frames";
        internal const string WarmupFramesArgument = "--gameplay-performance-warmup-frames";
        internal const string TickIntervalArgument = "--gameplay-performance-tick-interval";
        internal const string WidthArgument = "--gameplay-performance-width";
        internal const string HeightArgument = "--gameplay-performance-height";
        internal const string RevisionArgument = "--gameplay-performance-revision";
        internal const string SuccessMarker = "GAMEPLAY_PERFORMANCE:PASS";
        internal const string FailureMarker = "GAMEPLAY_PERFORMANCE:FAIL";

        private const int DefaultSampleFrames = 600;
        private const int DefaultWarmupFrames = 120;
        private const int DefaultTickInterval = 6;
        private const float HostReadyTimeoutSeconds = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (!HasArgument(LaunchArgument))
            {
                return;
            }

            UnityEngine.Application.runInBackground = true;
            var root = new GameObject(nameof(GameplayPerformancePlayerProbe));
            DontDestroyOnLoad(root);
            root.AddComponent<GameplayPerformancePlayerProbe>();
        }

        private IEnumerator Start()
        {
            var outputDirectory = ReadArgumentValue(OutputArgument);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Fail("output directory argument is missing");
                yield break;
            }

            var sampleFrames = ReadPositiveInt(SampleFramesArgument, DefaultSampleFrames);
            var warmupFrames = ReadPositiveInt(WarmupFramesArgument, DefaultWarmupFrames);
            var tickInterval = ReadPositiveInt(TickIntervalArgument, DefaultTickInterval);
            var width = ReadPositiveInt(WidthArgument, 1920);
            var height = ReadPositiveInt(HeightArgument, 1080);
            var revision = ReadArgumentValue(RevisionArgument);

            Directory.CreateDirectory(outputDirectory);
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            Screen.SetResolution(width, height, FullScreenMode.Windowed);

            var resolutionReady = false;
            var deadline = Time.realtimeSinceStartup + HostReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Screen.width == width && Screen.height == height)
                {
                    resolutionReady = true;
                    break;
                }

                yield return null;
            }

            if (!resolutionReady)
            {
                Fail(
                    $"requested resolution did not become active: " +
                    $"requested={width}x{height} actual={Screen.width}x{Screen.height}");
                yield break;
            }

            GameplaySceneHost host = null;
            deadline = Time.realtimeSinceStartup + HostReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                host = FindFirstObjectByType<GameplaySceneHost>(FindObjectsInactive.Include);
                if (host != null &&
                    host.HasStrongGameplayEntryRuntime &&
                    host.InputHost != null &&
                    host.OutputCamera != null &&
                    host.OutputCamera.isActiveAndEnabled &&
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    break;
                }

                yield return null;
            }

            if (host == null ||
                !host.HasStrongGameplayEntryRuntime ||
                host.InputHost == null ||
                host.OutputCamera == null)
            {
                Fail("production gameplay host did not become ready");
                yield break;
            }

            host.InputHost.SetAutoAdvanceTicks(false);
            host.InputHost.SetRawMoveInput(Vector2.zero);

            for (var frame = 0; frame < warmupFrames; frame++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }

            var admissionReady = false;
            deadline = Time.realtimeSinceStartup + HostReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (host.InputHost.RunSingleTick() != null)
                {
                    admissionReady = true;
                    break;
                }

                yield return null;
            }

            if (!admissionReady)
            {
                Fail("gameplay tick admission did not become ready");
                yield break;
            }

            for (var frame = 0; frame < Math.Max(30, warmupFrames / 2); frame++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }

            var records = new List<FrameRecord>(sampleFrames * 2);
            using (var drawCalls = ProfilerRecorder.StartNew(
                       ProfilerCategory.Render,
                       "Draw Calls Count",
                       1))
            using (var gcAllocated = ProfilerRecorder.StartNew(
                       ProfilerCategory.Memory,
                       "GC Allocated In Frame",
                       1))
            {
                yield return SamplePhase(
                    "render-idle",
                    host,
                    sampleFrames,
                    tickInterval,
                    executeTicks: false,
                    drawCalls,
                    gcAllocated,
                    records);
                yield return SamplePhase(
                    "gameplay-neutral-tick",
                    host,
                    sampleFrames,
                    tickInterval,
                    executeTicks: true,
                    drawCalls,
                    gcAllocated,
                    records);
            }

            var idleSummary = PhaseSummary.Create("render-idle", records);
            var gameplaySummary = PhaseSummary.Create("gameplay-neutral-tick", records);
            var minimumExpectedTicks = Math.Max(5, sampleFrames / tickInterval / 4);
            if (gameplaySummary.ExecutedTicks < minimumExpectedTicks)
            {
                Fail(
                    $"insufficient admitted gameplay ticks: " +
                    $"executed={gameplaySummary.ExecutedTicks} expectedAtLeast={minimumExpectedTicks}");
                yield break;
            }

            WriteManifest(
                outputDirectory,
                revision,
                width,
                height,
                sampleFrames,
                warmupFrames,
                tickInterval,
                drawCallsAvailable: idleSummary.ValidDrawCallSamples > 0,
                gcAllocatedAvailable: idleSummary.ValidGcAllocatedSamples > 0,
                idleSummary,
                gameplaySummary);

            Debug.Log(
                $"{SuccessMarker} resolution={Screen.width}x{Screen.height} " +
                $"idleFrames={idleSummary.SampleCount} gameplayFrames={gameplaySummary.SampleCount} " +
                $"executedTicks={gameplaySummary.ExecutedTicks}");
            UnityEngine.Application.Quit(0);
        }

        private static IEnumerator SamplePhase(
            string phase,
            GameplaySceneHost host,
            int sampleFrames,
            int tickInterval,
            bool executeTicks,
            ProfilerRecorder drawCalls,
            ProfilerRecorder gcAllocated,
            ICollection<FrameRecord> records)
        {
            var timings = new FrameTiming[1];
            for (var frame = 0; frame < sampleFrames; frame++)
            {
                var tickAttempted = executeTicks && frame % tickInterval == 0;
                var tickExecuted = false;
                var tickMilliseconds = 0d;
                if (tickAttempted)
                {
                    var stopwatch = Stopwatch.StartNew();
                    tickExecuted = host.InputHost.RunSingleTick() != null;
                    stopwatch.Stop();
                    tickMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                }

                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                var timingCount = FrameTimingManager.GetLatestTimings(1, timings);
                var timing = timingCount > 0 ? timings[0] : default;
                records.Add(
                    new FrameRecord(
                        phase,
                        Time.unscaledDeltaTime * 1000d,
                        timing.cpuMainThreadFrameTime > 0d
                            ? timing.cpuMainThreadFrameTime
                            : timing.cpuFrameTime,
                        timing.cpuRenderThreadFrameTime,
                        timing.gpuFrameTime,
                        drawCalls.Valid ? drawCalls.LastValue : -1,
                        gcAllocated.Valid ? gcAllocated.LastValue : -1,
                        tickAttempted,
                        tickExecuted,
                        tickMilliseconds));
            }
        }

        private static void WriteManifest(
            string outputDirectory,
            string revision,
            int requestedWidth,
            int requestedHeight,
            int sampleFrames,
            int warmupFrames,
            int tickInterval,
            bool drawCallsAvailable,
            bool gcAllocatedAvailable,
            PhaseSummary idle,
            PhaseSummary gameplay)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 1,");
            builder.AppendLine("  \"measurementKind\": \"release-like-player-headroom\",");
            builder.AppendLine("  \"budgetVerdict\": \"NOT_CONFIGURED\",");
            builder.AppendLine($"  \"revision\": \"{Escape(revision)}\",");
            builder.AppendLine($"  \"unityVersion\": \"{Escape(UnityEngine.Application.unityVersion)}\",");
            builder.AppendLine($"  \"developmentBuild\": {(Debug.isDebugBuild ? "true" : "false")},");
            builder.AppendLine($"  \"productName\": \"{Escape(UnityEngine.Application.productName)}\",");
            builder.AppendLine($"  \"operatingSystem\": \"{Escape(SystemInfo.operatingSystem)}\",");
            builder.AppendLine($"  \"processorType\": \"{Escape(SystemInfo.processorType)}\",");
            builder.AppendLine($"  \"processorCount\": {SystemInfo.processorCount},");
            builder.AppendLine($"  \"systemMemorySizeMB\": {SystemInfo.systemMemorySize},");
            builder.AppendLine($"  \"graphicsDeviceType\": \"{SystemInfo.graphicsDeviceType}\",");
            builder.AppendLine($"  \"graphicsDeviceName\": \"{Escape(SystemInfo.graphicsDeviceName)}\",");
            builder.AppendLine($"  \"graphicsDeviceVersion\": \"{Escape(SystemInfo.graphicsDeviceVersion)}\",");
            builder.AppendLine($"  \"graphicsMemorySizeMB\": {SystemInfo.graphicsMemorySize},");
            builder.AppendLine($"  \"qualityLevel\": {QualitySettings.GetQualityLevel()},");
            builder.AppendLine($"  \"qualityName\": \"{Escape(QualitySettings.names[QualitySettings.GetQualityLevel()])}\",");
            builder.AppendLine($"  \"requestedResolution\": [{requestedWidth}, {requestedHeight}],");
            builder.AppendLine($"  \"actualResolution\": [{Screen.width}, {Screen.height}],");
            builder.AppendLine("  \"vSyncCount\": 0,");
            builder.AppendLine("  \"targetFrameRate\": -1,");
            builder.AppendLine($"  \"warmupFrames\": {warmupFrames},");
            builder.AppendLine($"  \"sampleFramesPerPhase\": {sampleFrames},");
            builder.AppendLine($"  \"gameplayTickIntervalFrames\": {tickInterval},");
            builder.AppendLine($"  \"drawCallsCounterAvailable\": {(drawCallsAvailable ? "true" : "false")},");
            builder.AppendLine($"  \"gcAllocatedCounterAvailable\": {(gcAllocatedAvailable ? "true" : "false")},");
            builder.AppendLine("  \"phases\": [");
            builder.Append("    ").Append(idle.ToJson()).AppendLine(",");
            builder.Append("    ").Append(gameplay.ToJson()).AppendLine();
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(
                Path.Combine(outputDirectory, "performance-metrics.json"),
                builder.ToString());
        }

        private static bool HasArgument(string argument)
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                candidate => string.Equals(candidate, argument, StringComparison.Ordinal));
        }

        private static string ReadArgumentValue(string argument)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], argument, StringComparison.Ordinal) &&
                    index + 1 < arguments.Length)
                {
                    return arguments[index + 1] ?? string.Empty;
                }

                var prefix = argument + "=";
                if (arguments[index] != null &&
                    arguments[index].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return arguments[index].Substring(prefix.Length);
                }
            }

            return string.Empty;
        }

        private static int ReadPositiveInt(string argument, int fallback)
        {
            return int.TryParse(
                       ReadArgumentValue(argument),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out var value) &&
                   value > 0
                ? value
                : fallback;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Fail(string diagnostic)
        {
            Debug.LogError($"{FailureMarker} {diagnostic}");
            UnityEngine.Application.Quit(2);
        }

        private readonly struct FrameRecord
        {
            public FrameRecord(
                string phase,
                double frameIntervalMilliseconds,
                double cpuMainMilliseconds,
                double cpuRenderMilliseconds,
                double gpuMilliseconds,
                long drawCalls,
                long gcAllocatedBytes,
                bool tickAttempted,
                bool tickExecuted,
                double tickMilliseconds)
            {
                Phase = phase;
                FrameIntervalMilliseconds = frameIntervalMilliseconds;
                CpuMainMilliseconds = cpuMainMilliseconds;
                CpuRenderMilliseconds = cpuRenderMilliseconds;
                GpuMilliseconds = gpuMilliseconds;
                DrawCalls = drawCalls;
                GcAllocatedBytes = gcAllocatedBytes;
                TickAttempted = tickAttempted;
                TickExecuted = tickExecuted;
                TickMilliseconds = tickMilliseconds;
            }

            public string Phase { get; }
            public double FrameIntervalMilliseconds { get; }
            public double CpuMainMilliseconds { get; }
            public double CpuRenderMilliseconds { get; }
            public double GpuMilliseconds { get; }
            public long DrawCalls { get; }
            public long GcAllocatedBytes { get; }
            public bool TickAttempted { get; }
            public bool TickExecuted { get; }
            public double TickMilliseconds { get; }
        }

        private readonly struct PhaseSummary
        {
            private PhaseSummary(
                string phase,
                int sampleCount,
                int attemptedTicks,
                int executedTicks,
                int validCpuMainSamples,
                int validCpuRenderSamples,
                int validGpuSamples,
                int validDrawCallSamples,
                int validGcAllocatedSamples,
                MetricSummary frameInterval,
                MetricSummary cpuMain,
                MetricSummary cpuRender,
                MetricSummary gpu,
                MetricSummary tick,
                LongMetricSummary drawCalls,
                LongMetricSummary gcAllocated)
            {
                Phase = phase;
                SampleCount = sampleCount;
                AttemptedTicks = attemptedTicks;
                ExecutedTicks = executedTicks;
                ValidCpuMainSamples = validCpuMainSamples;
                ValidCpuRenderSamples = validCpuRenderSamples;
                ValidGpuSamples = validGpuSamples;
                ValidDrawCallSamples = validDrawCallSamples;
                ValidGcAllocatedSamples = validGcAllocatedSamples;
                FrameInterval = frameInterval;
                CpuMain = cpuMain;
                CpuRender = cpuRender;
                Gpu = gpu;
                Tick = tick;
                DrawCalls = drawCalls;
                GcAllocated = gcAllocated;
            }

            public int SampleCount { get; }
            public int ExecutedTicks { get; }
            public int ValidDrawCallSamples { get; }
            public int ValidGcAllocatedSamples { get; }
            private string Phase { get; }
            private int AttemptedTicks { get; }
            private int ValidCpuMainSamples { get; }
            private int ValidCpuRenderSamples { get; }
            private int ValidGpuSamples { get; }
            private MetricSummary FrameInterval { get; }
            private MetricSummary CpuMain { get; }
            private MetricSummary CpuRender { get; }
            private MetricSummary Gpu { get; }
            private MetricSummary Tick { get; }
            private LongMetricSummary DrawCalls { get; }
            private LongMetricSummary GcAllocated { get; }

            public static PhaseSummary Create(string phase, IEnumerable<FrameRecord> allRecords)
            {
                var frameIntervals = new List<double>();
                var cpuMain = new List<double>();
                var cpuRender = new List<double>();
                var gpu = new List<double>();
                var tick = new List<double>();
                var drawCalls = new List<long>();
                var gcAllocated = new List<long>();
                var attemptedTicks = 0;
                var executedTicks = 0;
                foreach (var record in allRecords)
                {
                    if (!string.Equals(record.Phase, phase, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddPositive(frameIntervals, record.FrameIntervalMilliseconds);
                    AddPositive(cpuMain, record.CpuMainMilliseconds);
                    AddPositive(cpuRender, record.CpuRenderMilliseconds);
                    AddPositive(gpu, record.GpuMilliseconds);
                    if (record.DrawCalls >= 0)
                    {
                        drawCalls.Add(record.DrawCalls);
                    }

                    if (record.GcAllocatedBytes >= 0)
                    {
                        gcAllocated.Add(record.GcAllocatedBytes);
                    }

                    if (record.TickAttempted)
                    {
                        attemptedTicks++;
                    }

                    if (record.TickExecuted)
                    {
                        executedTicks++;
                        AddPositive(tick, record.TickMilliseconds);
                    }
                }

                var sampleCount = frameIntervals.Count;
                return new PhaseSummary(
                    phase,
                    sampleCount,
                    attemptedTicks,
                    executedTicks,
                    cpuMain.Count,
                    cpuRender.Count,
                    gpu.Count,
                    drawCalls.Count,
                    gcAllocated.Count,
                    MetricSummary.Create(frameIntervals),
                    MetricSummary.Create(cpuMain),
                    MetricSummary.Create(cpuRender),
                    MetricSummary.Create(gpu),
                    MetricSummary.Create(tick),
                    LongMetricSummary.Create(drawCalls),
                    LongMetricSummary.Create(gcAllocated));
            }

            public string ToJson()
            {
                return "{\"phase\":\"" + Escape(Phase) +
                       "\",\"sampleCount\":" + SampleCount +
                       ",\"attemptedTicks\":" + AttemptedTicks +
                       ",\"executedTicks\":" + ExecutedTicks +
                       ",\"validCpuMainSamples\":" + ValidCpuMainSamples +
                       ",\"validCpuRenderSamples\":" + ValidCpuRenderSamples +
                       ",\"validGpuSamples\":" + ValidGpuSamples +
                       ",\"validDrawCallSamples\":" + ValidDrawCallSamples +
                       ",\"validGcAllocatedSamples\":" + ValidGcAllocatedSamples +
                       ",\"frameIntervalMilliseconds\":" + FrameInterval.ToJson() +
                       ",\"cpuMainMilliseconds\":" + CpuMain.ToJson() +
                       ",\"cpuRenderMilliseconds\":" + CpuRender.ToJson() +
                       ",\"gpuMilliseconds\":" + Gpu.ToJson() +
                       ",\"tickWallMilliseconds\":" + Tick.ToJson() +
                       ",\"drawCalls\":" + DrawCalls.ToJson() +
                       ",\"gcAllocatedBytes\":" + GcAllocated.ToJson() +
                       "}";
            }

            private static void AddPositive(ICollection<double> values, double value)
            {
                if (value > 0d && !double.IsNaN(value) && !double.IsInfinity(value))
                {
                    values.Add(value);
                }
            }
        }

        private readonly struct MetricSummary
        {
            private MetricSummary(int count, double median, double p95, double p99, double maximum)
            {
                Count = count;
                Median = median;
                P95 = p95;
                P99 = p99;
                Maximum = maximum;
            }

            private int Count { get; }
            private double Median { get; }
            private double P95 { get; }
            private double P99 { get; }
            private double Maximum { get; }

            public static MetricSummary Create(List<double> values)
            {
                values.Sort();
                return new MetricSummary(
                    values.Count,
                    Sample(values, 0.5),
                    Sample(values, 0.95),
                    Sample(values, 0.99),
                    values.Count > 0 ? values[values.Count - 1] : 0d);
            }

            public string ToJson()
            {
                return "{\"count\":" + Count +
                       ",\"median\":" + Number(Median) +
                       ",\"p95\":" + Number(P95) +
                       ",\"p99\":" + Number(P99) +
                       ",\"maximum\":" + Number(Maximum) + "}";
            }

            private static double Sample(IReadOnlyList<double> values, double percentile)
            {
                if (values.Count == 0)
                {
                    return 0d;
                }

                var position = Math.Max(0d, Math.Min(1d, percentile)) * (values.Count - 1);
                var lower = (int)Math.Floor(position);
                var upper = (int)Math.Ceiling(position);
                return values[lower] + (values[upper] - values[lower]) * (position - lower);
            }
        }

        private readonly struct LongMetricSummary
        {
            private LongMetricSummary(int count, long median, long p95, long p99, long maximum)
            {
                Count = count;
                Median = median;
                P95 = p95;
                P99 = p99;
                Maximum = maximum;
            }

            private int Count { get; }
            private long Median { get; }
            private long P95 { get; }
            private long P99 { get; }
            private long Maximum { get; }

            public static LongMetricSummary Create(List<long> values)
            {
                values.Sort();
                return new LongMetricSummary(
                    values.Count,
                    Sample(values, 0.5),
                    Sample(values, 0.95),
                    Sample(values, 0.99),
                    values.Count > 0 ? values[values.Count - 1] : -1);
            }

            public string ToJson()
            {
                return "{\"count\":" + Count +
                       ",\"median\":" + Median +
                       ",\"p95\":" + P95 +
                       ",\"p99\":" + P99 +
                       ",\"maximum\":" + Maximum + "}";
            }

            private static long Sample(IReadOnlyList<long> values, double percentile)
            {
                if (values.Count == 0)
                {
                    return -1;
                }

                var index = (int)Math.Round(
                    Math.Max(0d, Math.Min(1d, percentile)) * (values.Count - 1));
                return values[index];
            }
        }
    }
}
#endif
