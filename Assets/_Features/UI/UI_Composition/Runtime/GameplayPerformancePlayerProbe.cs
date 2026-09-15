#if VECTORQUAKE_CAPTURE_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
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
        internal const string GameplayStageArgument = "--capture-stage";
        internal const string CleanupStrategyArgument = "--gameplay-cleanup-strategy";
        internal const string CampaignIdArgument = "--gameplay-evidence-campaign-id";
        internal const string AttemptIdArgument = "--gameplay-evidence-attempt-id";
        internal const string AttemptOrdinalArgument = "--gameplay-evidence-attempt-ordinal";
        internal const string AttemptKindArgument = "--gameplay-evidence-attempt-kind";
        internal const string CaptureNonceArgument = "--gameplay-evidence-capture-nonce";
        internal const string EvidenceStageArgument = "--gameplay-evidence-stage";
        internal const string ActiveStrategiesArgument = "--gameplay-evidence-active-strategies";
        internal const string PreBuildHeadArgument = "--gameplay-evidence-pre-build-head";
        internal const string PreBuildWorktreeArgument = "--gameplay-evidence-pre-build-worktree-sha256";
        internal const string PostRestoreHeadArgument = "--gameplay-evidence-post-restore-head";
        internal const string PostRestoreWorktreeArgument = "--gameplay-evidence-post-restore-worktree-sha256";
        internal const string RuntimeTreeArgument = "--gameplay-evidence-runtime-tree-sha256";
        internal const string PlayerArtifactArgument = "--gameplay-evidence-player-artifact-sha256";
        internal const string BuildPayloadArgument = "--gameplay-evidence-build-payload-sha256";
        internal const string RunnerHashArgument = "--gameplay-evidence-runner-sha256";
        internal const string PerformanceValidatorHashArgument = "--gameplay-evidence-performance-validator-sha256";
        internal const string CleanupValidatorHashArgument = "--gameplay-evidence-cleanup-validator-sha256";
        internal const string AggregatorHashArgument = "--gameplay-evidence-aggregator-sha256";
        internal const string ManifestToolHashArgument = "--gameplay-evidence-manifest-tool-sha256";
        internal const string WorkloadContractHashArgument = "--gameplay-evidence-workload-contract-sha256";
        internal const string HarnessHashArgument = "--gameplay-evidence-harness-sha256";
        internal const string SuccessMarker = "GAMEPLAY_PERFORMANCE:PASS";
        internal const string FailureMarker = "GAMEPLAY_PERFORMANCE:FAIL";

        private const int DefaultSampleFrames = 600;
        private const int DefaultWarmupFrames = 120;
        private const int DefaultTickInterval = 6;
        private const float HostReadyTimeoutSeconds = 30f;
        private const int CleanupCalibrationWarmupTicks = 200;
        private const int CleanupCalibrationSampleTicks = 200;
        private const int CleanupCalibrationRepetitions = 3;
        private const int CleanupAllocationWarmupFrames = 30;
        private const int CleanupAllocationSampleFrames = 100;

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
            var gameplayStage = ReadArgumentValue(GameplayStageArgument);
            var campaignId = ReadArgumentValue(CampaignIdArgument);
            var attemptId = ReadArgumentValue(AttemptIdArgument);
            var attemptOrdinalText = ReadArgumentValue(AttemptOrdinalArgument);
            var attemptKind = ReadArgumentValue(AttemptKindArgument);
            var captureNonce = ReadArgumentValue(CaptureNonceArgument);
            var evidenceStage = ReadArgumentValue(EvidenceStageArgument);
            var activeStrategies = ReadArgumentValue(ActiveStrategiesArgument);
            var preBuildHead = ReadArgumentValue(PreBuildHeadArgument);
            var preBuildWorktree = ReadArgumentValue(PreBuildWorktreeArgument);
            var postRestoreHead = ReadArgumentValue(PostRestoreHeadArgument);
            var postRestoreWorktree = ReadArgumentValue(PostRestoreWorktreeArgument);
            var runtimeTree = ReadArgumentValue(RuntimeTreeArgument);
            var playerArtifact = ReadArgumentValue(PlayerArtifactArgument);
            var buildPayload = ReadArgumentValue(BuildPayloadArgument);
            var runnerHash = ReadArgumentValue(RunnerHashArgument);
            var performanceValidatorHash = ReadArgumentValue(PerformanceValidatorHashArgument);
            var cleanupValidatorHash = ReadArgumentValue(CleanupValidatorHashArgument);
            var aggregatorHash = ReadArgumentValue(AggregatorHashArgument);
            var manifestToolHash = ReadArgumentValue(ManifestToolHashArgument);
            var workloadContractHash = ReadArgumentValue(WorkloadContractHashArgument);
            var harnessHash = ReadArgumentValue(HarnessHashArgument);
            if (!int.TryParse(attemptOrdinalText, NumberStyles.None, CultureInfo.InvariantCulture, out var attemptOrdinal) ||
                attemptOrdinal <= 0 ||
                string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(attemptId) ||
                string.IsNullOrWhiteSpace(attemptKind) || string.IsNullOrWhiteSpace(captureNonce) ||
                string.IsNullOrWhiteSpace(evidenceStage) || string.IsNullOrWhiteSpace(activeStrategies) ||
                string.IsNullOrWhiteSpace(preBuildHead) || string.IsNullOrWhiteSpace(preBuildWorktree) ||
                string.IsNullOrWhiteSpace(postRestoreHead) || string.IsNullOrWhiteSpace(postRestoreWorktree) ||
                string.IsNullOrWhiteSpace(runtimeTree) || string.IsNullOrWhiteSpace(playerArtifact) ||
                string.IsNullOrWhiteSpace(buildPayload) || string.IsNullOrWhiteSpace(runnerHash) ||
                string.IsNullOrWhiteSpace(performanceValidatorHash) || string.IsNullOrWhiteSpace(cleanupValidatorHash) ||
                string.IsNullOrWhiteSpace(aggregatorHash) || string.IsNullOrWhiteSpace(manifestToolHash) ||
                string.IsNullOrWhiteSpace(workloadContractHash) || string.IsNullOrWhiteSpace(harnessHash) ||
                string.IsNullOrWhiteSpace(gameplayStage))
            {
                Fail("Evidence Contract v4 capture identity is missing or invalid");
                yield break;
            }
            var cleanupStrategy = ReadArgumentValue(CleanupStrategyArgument);
            if (string.IsNullOrWhiteSpace(cleanupStrategy))
            {
                cleanupStrategy = "A";
            }

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
            IReadOnlyList<GameplayTickAttributionSample> attributionSamples =
                Array.Empty<GameplayTickAttributionSample>();
            var gcAllocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                1);
            using (var drawCalls = ProfilerRecorder.StartNew(
                       ProfilerCategory.Render,
                       "Draw Calls Count",
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
                GameplayTickAttributionCapture.BeginSession(
                    Math.Max(1, (sampleFrames + tickInterval - 1) / tickInterval));
                try
                {
                    yield return SamplePhase(
                        "gameplay-neutral-tick",
                        host,
                        sampleFrames,
                        tickInterval,
                        executeTicks: true,
                        drawCalls,
                        gcAllocated,
                        records);
                    attributionSamples = GameplayTickAttributionCapture.CompleteSession();
                }
                finally
                {
                    if (GameplayTickAttributionCapture.IsActive)
                    {
                        GameplayTickAttributionCapture.CancelSession();
                    }
                }
            }
            gcAllocated.Dispose();

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
            if (attributionSamples.Count != gameplaySummary.ExecutedTicks)
            {
                Fail(
                    $"Tick attribution sample count mismatch: " +
                    $"attribution={attributionSamples.Count} executed={gameplaySummary.ExecutedTicks}");
                yield break;
            }

            string cleanupCalibrationJson;
            try
            {
                cleanupCalibrationJson = CleanupSlice3PlayerCalibration.CaptureJson(
                    cleanupStrategy,
                    CleanupCalibrationWarmupTicks,
                    CleanupCalibrationSampleTicks,
                    CleanupCalibrationRepetitions);
                cleanupCalibrationJson = NormalizeCleanupCalibrationSchema2(cleanupCalibrationJson);
            }
            catch (Exception exception)
            {
                Fail($"Cleanup S3-A calibration failed closed: {exception.Message}");
                yield break;
            }

            string cleanupAllocationJson = null;
            yield return CaptureCleanupAllocationCalibration(
                cleanupStrategy,
                value => cleanupAllocationJson = value);
            if (string.IsNullOrWhiteSpace(cleanupAllocationJson))
            {
                Fail("Cleanup S3-A allocation calibration did not produce a result");
                yield break;
            }

            cleanupCalibrationJson = AppendJsonProperty(
                cleanupCalibrationJson,
                "frameAllocationCalibration",
                cleanupAllocationJson);

            var captureIdentityJson = BuildCaptureIdentityJson(
                campaignId, attemptId, attemptOrdinal, attemptKind, captureNonce,
                evidenceStage, activeStrategies, preBuildHead, preBuildWorktree,
                postRestoreHead, postRestoreWorktree, runtimeTree, playerArtifact,
                buildPayload, runnerHash, performanceValidatorHash, cleanupValidatorHash,
                aggregatorHash, manifestToolHash, workloadContractHash, harnessHash);
            WriteManifest(
                outputDirectory,
                revision,
                captureIdentityJson,
                width,
                height,
                sampleFrames,
                warmupFrames,
                tickInterval,
                drawCallsAvailable: idleSummary.ValidDrawCallSamples > 0,
                gcAllocatedAvailable: idleSummary.ValidGcAllocatedSamples > 0,
                idleSummary,
                gameplaySummary,
                cleanupCalibrationJson);
            WriteTickAttribution(
                outputDirectory,
                revision,
                gameplayStage,
                captureIdentityJson,
                attributionSamples);

            Debug.Log(
                $"{SuccessMarker} resolution={Screen.width}x{Screen.height} " +
                $"idleFrames={idleSummary.SampleCount} gameplayFrames={gameplaySummary.SampleCount} " +
                $"executedTicks={gameplaySummary.ExecutedTicks} " +
                $"attributionSamples={attributionSamples.Count}");
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

        private static IEnumerator CaptureCleanupAllocationCalibration(
            string cleanupStrategy,
            Action<string> completed)
        {
            MeasureAllocationCounterProbeBytes();
            var allocationCounterProbeBytes = MeasureAllocationCounterProbeBytes();
            var phaseJson = new List<string>(4);
            yield return CaptureCleanupAllocationPhase(
                cleanupStrategy,
                "cleanup-s3-target-wall-empty-v2",
                true,
                phaseJson);
            yield return CaptureCleanupAllocationPhase(
                cleanupStrategy,
                "cleanup-s3-target-wall-empty-v2",
                false,
                phaseJson);
            yield return CaptureCleanupAllocationPhase(
                cleanupStrategy,
                "cleanup-s3-stress-dense-v2",
                true,
                phaseJson);
            yield return CaptureCleanupAllocationPhase(
                cleanupStrategy,
                "cleanup-s3-stress-dense-v2",
                false,
                phaseJson);

            completed(
                "{" +
                "\"signal\":\"GC.GetAllocatedBytesForCurrentThread delta around exactly one synthetic tick\"" +
                ",\"allocationCounterProbeBytes\":" + allocationCounterProbeBytes +
                ",\"warmupFramesPerPhase\":" + CleanupAllocationWarmupFrames +
                ",\"sampleFramesPerPhase\":" + CleanupAllocationSampleFrames +
                ",\"phases\":[" + string.Join(",", phaseJson) + "]" +
                "}");
        }

        private static long MeasureAllocationCounterProbeBytes()
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096];
            probe[0] = 1;
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(probe);
            return Math.Max(0L, allocatedAfter - allocatedBefore);
        }

        private static IEnumerator CaptureCleanupAllocationPhase(
            string cleanupStrategy,
            string workloadId,
            bool captureDiagnostics,
            ICollection<string> phaseJson)
        {
            var session = CleanupSlice3PlayerCalibration.CreateAllocationSession(
                cleanupStrategy,
                workloadId);
            for (var frame = 0; frame < CleanupAllocationWarmupFrames; frame++)
            {
                session.RunTick(6000 + frame, captureDiagnostics);
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }

            var samples = new List<long>(CleanupAllocationSampleFrames);
            for (var frame = 0; frame < CleanupAllocationSampleFrames; frame++)
            {
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                session.RunTick(7000 + frame, captureDiagnostics);
                var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                samples.Add(Math.Max(0L, allocatedAfter - allocatedBefore));
                yield return null;
            }

            phaseJson.Add(
                "{" +
                "\"workloadId\":\"" + Escape(session.WorkloadId) + "\"" +
                ",\"strategy\":\"" + Escape(cleanupStrategy) + "\"" +
                ",\"scheduleHash\":\"" + session.ScheduleHash + "\"" +
                ",\"initialWorldFingerprint\":\"" + session.InitialWorldFingerprint + "\"" +
                ",\"captureDiagnostics\":" + (captureDiagnostics ? "true" : "false") +
                ",\"validSamples\":" + samples.Count +
                ",\"gcAllocatedBytesPerTick\":" + LongMetricSummary.Create(samples).ToJson() +
                "}");
        }

        private static string AppendJsonProperty(string objectJson, string name, string valueJson)
        {
            if (string.IsNullOrEmpty(objectJson) || objectJson[objectJson.Length - 1] != '}')
            {
                throw new InvalidOperationException("Cleanup calibration JSON root is malformed.");
            }

            return objectJson.Substring(0, objectJson.Length - 1) +
                   ",\"" + Escape(name) + "\":" + valueJson + "}";
        }

        private static string NormalizeCleanupCalibrationSchema2(string source)
        {
            const string prefix = "{\"schemaVersion\":1,\"stage\":\"S3-A\",\"strategy\":\"A\",";
            if (string.IsNullOrEmpty(source) || !source.StartsWith(prefix, StringComparison.Ordinal) ||
                source[source.Length - 1] != '}')
            {
                throw new InvalidOperationException("Cleanup calibration schema-1 producer shape changed.");
            }

            var normalized =
                "{\"schemaVersion\":2,\"evidenceContractVersion\":4," +
                "\"stage\":\"S3-A\",\"activeStrategies\":[\"A\"]," +
                source.Substring(prefix.Length);
            normalized = normalized.Replace(
                ",\"workloads\":[",
                ",\"captures\":[{\"strategy\":\"A\",\"workloads\":[");
            normalized = normalized.Substring(0, normalized.Length - 1) + "}]}";
            normalized = AddRunKeys(normalized, "cleanup-s3-target-wall-empty-v2");
            normalized = AddRunKeys(normalized, "cleanup-s3-stress-dense-v2");
            return normalized;
        }

        private static string AddRunKeys(string source, string workloadId)
        {
            var marker = "\"workloadId\":\"" + workloadId + "\"";
            var start = source.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                throw new InvalidOperationException($"Cleanup workload {workloadId} is missing.");
            }

            var next = source.IndexOf("\"workloadId\":\"", start + marker.Length, StringComparison.Ordinal);
            var length = next < 0 ? source.Length - start : next - start;
            var workload = source.Substring(start, length);
            workload = Regex.Replace(
                workload,
                "\\\"repetition\\\":([0-9]+)",
                match =>
                    "\"runKey\":\"A/" + workloadId + "/" + match.Groups[1].Value +
                    "\",\"repetition\":" + match.Groups[1].Value,
                RegexOptions.CultureInvariant);
            return source.Substring(0, start) + workload + source.Substring(start + length);
        }

        private static string BuildCaptureIdentityJson(
            string campaignId,
            string attemptId,
            int attemptOrdinal,
            string attemptKind,
            string captureNonce,
            string stage,
            string activeStrategies,
            string preBuildHead,
            string preBuildWorktree,
            string postRestoreHead,
            string postRestoreWorktree,
            string runtimeTree,
            string playerArtifact,
            string buildPayload,
            string runnerHash,
            string performanceValidatorHash,
            string cleanupValidatorHash,
            string aggregatorHash,
            string manifestToolHash,
            string workloadContractHash,
            string harnessHash)
        {
            if (!string.Equals(stage, "S3-A", StringComparison.Ordinal) ||
                !string.Equals(activeStrategies, "A", StringComparison.Ordinal) ||
                !string.Equals(attemptKind, "calibration", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("S3-A capture identity stage/strategy/kind is invalid.");
            }

            return "{" +
                   "\"campaignId\":\"" + Escape(campaignId) + "\"" +
                   ",\"attemptId\":\"" + Escape(attemptId) + "\"" +
                   ",\"attemptOrdinal\":" + attemptOrdinal +
                   ",\"attemptKind\":\"" + Escape(attemptKind) + "\"" +
                   ",\"captureNonce\":\"" + Escape(captureNonce) + "\"" +
                   ",\"stage\":\"" + Escape(stage) + "\"" +
                   ",\"activeStrategies\":[\"A\"]" +
                   ",\"preBuildHeadSha\":\"" + Escape(preBuildHead) + "\"" +
                   ",\"preBuildWorktreeSha256\":\"" + Escape(preBuildWorktree) + "\"" +
                   ",\"postRestoreHeadSha\":\"" + Escape(postRestoreHead) + "\"" +
                   ",\"postRestoreWorktreeSha256\":\"" + Escape(postRestoreWorktree) + "\"" +
                   ",\"runtimeTreeSha256\":\"" + Escape(runtimeTree) + "\"" +
                   ",\"playerArtifactSha256\":\"" + Escape(playerArtifact) + "\"" +
                   ",\"buildPayloadSha256\":\"" + Escape(buildPayload) + "\"" +
                   ",\"runnerSha256\":\"" + Escape(runnerHash) + "\"" +
                   ",\"performanceValidatorSha256\":\"" + Escape(performanceValidatorHash) + "\"" +
                   ",\"cleanupValidatorSha256\":\"" + Escape(cleanupValidatorHash) + "\"" +
                   ",\"aggregatorSha256\":\"" + Escape(aggregatorHash) + "\"" +
                   ",\"manifestToolSha256\":\"" + Escape(manifestToolHash) + "\"" +
                   ",\"workloadContractSha256\":\"" + Escape(workloadContractHash) + "\"" +
                   ",\"harnessSha256\":\"" + Escape(harnessHash) + "\"" +
                   "}";
        }

        private static void WriteManifest(
            string outputDirectory,
            string revision,
            string captureIdentityJson,
            int requestedWidth,
            int requestedHeight,
            int sampleFrames,
            int warmupFrames,
            int tickInterval,
            bool drawCallsAvailable,
            bool gcAllocatedAvailable,
            PhaseSummary idle,
            PhaseSummary gameplay,
            string cleanupCalibrationJson)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 2,");
            builder.AppendLine("  \"evidenceContractVersion\": 4,");
            builder.Append("  \"captureIdentity\": ").Append(captureIdentityJson).AppendLine(",");
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
            builder.AppendLine("  ],");
            builder.Append("  \"cleanupSlice3Calibration\": ")
                .AppendLine(cleanupCalibrationJson);
            builder.AppendLine("}");
            var destination = Path.Combine(outputDirectory, "performance-metrics.json");
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(builder.ToString());
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, null);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void WriteTickAttribution(
            string outputDirectory,
            string revision,
            string gameplayStage,
            string captureIdentityJson,
            IReadOnlyList<GameplayTickAttributionSample> samples)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 7,");
            builder.AppendLine("  \"measurementKind\": \"gameplay-tick-level7-pre-movement-before-attack-attribution\",");
            builder.Append("  \"captureIdentity\": ").Append(captureIdentityJson).AppendLine(",");
            builder.Append("  \"revision\": \"").Append(Escape(revision)).AppendLine("\",");
            builder.Append("  \"gameplayStage\": \"").Append(Escape(gameplayStage)).AppendLine("\",");
            builder.Append("  \"stopwatchFrequency\": ").Append(Stopwatch.Frequency).AppendLine(",");
            builder.Append("  \"sampleCount\": ").Append(samples.Count).AppendLine(",");
            builder.AppendLine("  \"samples\": [");
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                var simulation = sample.SimulationDetail;
                var presentation = sample.PresentationDetail;
                var simulationResidualTicks = sample.SimulationTicks -
                    (simulation.BootstrapTicks + simulation.PlanTicks + simulation.ResolveTicks +
                     simulation.FinalizeAndSnapshotTicks + simulation.CleanupAndSnapshotTicks +
                     simulation.RespawnAndFinalSnapshotTicks + simulation.ResultMaterializationTicks);
                var planResidualTicks = simulation.PlanTicks -
                    (simulation.PlanEnemyAiAndProjectionTicks +
                     simulation.PlanKinematicAndGravityProjectionTicks +
                     simulation.PlanPreMovementStateAndUtilityProjectionTicks +
                     simulation.PlanJumpLandingAndPlayerActionAttemptsTicks +
                     simulation.PlanMovementIntentCollectionAndPartitionTicks +
                     simulation.PlanLocomotionProjectionTicks +
                     simulation.PlanMovementExpansionTicks +
                     simulation.PlanPayloadOrderingAndResultTicks);
                var resolveResidualTicks = simulation.ResolveTicks -
                    (simulation.ResolveMovementPlanningAndMaterializationTicks +
                     simulation.ResolveInitialProjectionAndBeforeAttackStateTicks +
                     simulation.ResolvePreliminaryAttackAndImpactDispositionTicks +
                     simulation.ResolveMovementRematerializationAndJumpLandingTicks +
                     simulation.ResolveTileEffectsAndProjectionTicks +
                     simulation.ResolveFinalAttackAndMaterializationTicks +
                     simulation.ResolvePostAttackStateAndUtilityTicks +
                     simulation.ResolveResultMaterializationTicks);
                var resolveInitialProjectionResidualTicks = simulation.ResolveInitialProjectionAndBeforeAttackStateTicks -
                    (simulation.ResolveInitialProjectionSetupAndBatchApplyTicks +
                     simulation.ResolveInitialPostMovementSnapshotTicks +
                     simulation.ResolveBeforeAttackAiTransitionTicks +
                     simulation.ResolveBeforeAttackEnemyActionTicks);
                var resolveInitialPostMovementSnapshotResidualTicks = simulation.ResolveInitialPostMovementSnapshotTicks -
                    (simulation.ResolveInitialPostMovementSnapshotBaseImportTicks +
                     simulation.ResolveInitialPostMovementSnapshotOverlayApplyTicks +
                     simulation.ResolveInitialPostMovementSnapshotMaterializationTicks);
                var planPreMovementResidualTicks = simulation.PlanPreMovementStateAndUtilityProjectionTicks -
                    (simulation.PlanPreMovementSetupTicks + simulation.PlanPreMovementLogicTicks +
                     simulation.PlanPreMovementBookkeepingTicks + simulation.PlanPreMovementProjectionApplyTicks +
                     simulation.PlanPreMovementUtilityInputSnapshotTicks + simulation.PlanPreMovementUtilityResolveTicks +
                     simulation.PlanPreMovementUtilityProjectionApplyTicks);
                var resolveBeforeAttackAiResidualTicks = simulation.ResolveBeforeAttackAiTransitionTicks -
                    (simulation.ResolveBeforeAttackAiSetupTicks + simulation.ResolveBeforeAttackAiLogicTicks +
                     simulation.ResolveBeforeAttackAiProjectionApplyTicks);
                var presentationResidualTicks = sample.PresentationTicks -
                    (presentation.CoordinatorTicks + presentation.CameraAndStateNotificationTicks);
                var coordinatorResidualTicks = presentation.CoordinatorTicks -
                    (presentation.PreCommitPlanningTicks + presentation.CommittedFrameAndStateTicks +
                     presentation.MotionAnimationVfxTicks + presentation.AudioTicks +
                     presentation.ApplyCleanupUpdateTicks);
                builder.Append("    {\"tickIndex\":").Append(sample.TickIndex)
                    .Append(",\"threadId\":").Append(sample.ThreadId)
                    .Append(",\"outerTicks\":").Append(sample.OuterTicks)
                    .Append(",\"inputPreparationTicks\":").Append(sample.InputPreparationTicks)
                    .Append(",\"simulationTicks\":").Append(sample.SimulationTicks)
                    .Append(",\"hostPostProcessTicks\":").Append(sample.HostPostProcessTicks)
                    .Append(",\"presentationTicks\":").Append(sample.PresentationTicks)
                    .Append(",\"callbackTicks\":").Append(sample.CallbackTicks)
                    .Append(",\"simulationBootstrapTicks\":").Append(simulation.BootstrapTicks)
                    .Append(",\"simulationPlanTicks\":").Append(simulation.PlanTicks)
                    .Append(",\"simulationResolveTicks\":").Append(simulation.ResolveTicks)
                    .Append(",\"simulationFinalizeAndSnapshotTicks\":").Append(simulation.FinalizeAndSnapshotTicks)
                    .Append(",\"simulationCleanupAndSnapshotTicks\":").Append(simulation.CleanupAndSnapshotTicks)
                    .Append(",\"simulationRespawnAndFinalSnapshotTicks\":").Append(simulation.RespawnAndFinalSnapshotTicks)
                    .Append(",\"simulationResultMaterializationTicks\":").Append(simulation.ResultMaterializationTicks)
                    .Append(",\"simulationResidualTicks\":").Append(simulationResidualTicks)
                    .Append(",\"simulationPlanEnemyAiAndProjectionTicks\":").Append(simulation.PlanEnemyAiAndProjectionTicks)
                    .Append(",\"simulationPlanKinematicAndGravityProjectionTicks\":").Append(simulation.PlanKinematicAndGravityProjectionTicks)
                    .Append(",\"simulationPlanPreMovementStateAndUtilityProjectionTicks\":").Append(simulation.PlanPreMovementStateAndUtilityProjectionTicks)
                    .Append(",\"simulationPlanPreMovementSetupTicks\":").Append(simulation.PlanPreMovementSetupTicks)
                    .Append(",\"simulationPlanPreMovementLogicTicks\":").Append(simulation.PlanPreMovementLogicTicks)
                    .Append(",\"simulationPlanPreMovementBookkeepingTicks\":").Append(simulation.PlanPreMovementBookkeepingTicks)
                    .Append(",\"simulationPlanPreMovementProjectionApplyTicks\":").Append(simulation.PlanPreMovementProjectionApplyTicks)
                    .Append(",\"simulationPlanPreMovementUtilityInputSnapshotTicks\":").Append(simulation.PlanPreMovementUtilityInputSnapshotTicks)
                    .Append(",\"simulationPlanPreMovementUtilityResolveTicks\":").Append(simulation.PlanPreMovementUtilityResolveTicks)
                    .Append(",\"simulationPlanPreMovementUtilityProjectionApplyTicks\":").Append(simulation.PlanPreMovementUtilityProjectionApplyTicks)
                    .Append(",\"simulationPlanPreMovementResidualTicks\":").Append(planPreMovementResidualTicks)
                    .Append(",\"simulationPlanJumpLandingAndPlayerActionAttemptsTicks\":").Append(simulation.PlanJumpLandingAndPlayerActionAttemptsTicks)
                    .Append(",\"simulationPlanMovementIntentCollectionAndPartitionTicks\":").Append(simulation.PlanMovementIntentCollectionAndPartitionTicks)
                    .Append(",\"simulationPlanLocomotionProjectionTicks\":").Append(simulation.PlanLocomotionProjectionTicks)
                    .Append(",\"simulationPlanMovementExpansionTicks\":").Append(simulation.PlanMovementExpansionTicks)
                    .Append(",\"simulationPlanPayloadOrderingAndResultTicks\":").Append(simulation.PlanPayloadOrderingAndResultTicks)
                    .Append(",\"simulationPlanResidualTicks\":").Append(planResidualTicks)
                    .Append(",\"simulationResolveMovementPlanningAndMaterializationTicks\":").Append(simulation.ResolveMovementPlanningAndMaterializationTicks)
                    .Append(",\"simulationResolveInitialProjectionAndBeforeAttackStateTicks\":").Append(simulation.ResolveInitialProjectionAndBeforeAttackStateTicks)
                    .Append(",\"simulationResolvePreliminaryAttackAndImpactDispositionTicks\":").Append(simulation.ResolvePreliminaryAttackAndImpactDispositionTicks)
                    .Append(",\"simulationResolveMovementRematerializationAndJumpLandingTicks\":").Append(simulation.ResolveMovementRematerializationAndJumpLandingTicks)
                    .Append(",\"simulationResolveTileEffectsAndProjectionTicks\":").Append(simulation.ResolveTileEffectsAndProjectionTicks)
                    .Append(",\"simulationResolveFinalAttackAndMaterializationTicks\":").Append(simulation.ResolveFinalAttackAndMaterializationTicks)
                    .Append(",\"simulationResolvePostAttackStateAndUtilityTicks\":").Append(simulation.ResolvePostAttackStateAndUtilityTicks)
                    .Append(",\"simulationResolveResultMaterializationTicks\":").Append(simulation.ResolveResultMaterializationTicks)
                    .Append(",\"simulationResolveResidualTicks\":").Append(resolveResidualTicks)
                    .Append(",\"simulationResolveInitialProjectionSetupAndBatchApplyTicks\":").Append(simulation.ResolveInitialProjectionSetupAndBatchApplyTicks)
                    .Append(",\"simulationResolveInitialPostMovementSnapshotTicks\":").Append(simulation.ResolveInitialPostMovementSnapshotTicks)
                    .Append(",\"simulationResolveBeforeAttackAiTransitionTicks\":").Append(simulation.ResolveBeforeAttackAiTransitionTicks)
                    .Append(",\"simulationResolveBeforeAttackAiSetupTicks\":").Append(simulation.ResolveBeforeAttackAiSetupTicks)
                    .Append(",\"simulationResolveBeforeAttackAiLogicTicks\":").Append(simulation.ResolveBeforeAttackAiLogicTicks)
                    .Append(",\"simulationResolveBeforeAttackAiProjectionApplyTicks\":").Append(simulation.ResolveBeforeAttackAiProjectionApplyTicks)
                    .Append(",\"simulationResolveBeforeAttackAiResidualTicks\":").Append(resolveBeforeAttackAiResidualTicks)
                    .Append(",\"simulationResolveBeforeAttackEnemyActionTicks\":").Append(simulation.ResolveBeforeAttackEnemyActionTicks)
                    .Append(",\"simulationResolveInitialProjectionResidualTicks\":").Append(resolveInitialProjectionResidualTicks)
                    .Append(",\"simulationResolveInitialPostMovementSnapshotBaseImportTicks\":").Append(simulation.ResolveInitialPostMovementSnapshotBaseImportTicks)
                    .Append(",\"simulationResolveInitialPostMovementSnapshotOverlayApplyTicks\":").Append(simulation.ResolveInitialPostMovementSnapshotOverlayApplyTicks)
                    .Append(",\"simulationResolveInitialPostMovementSnapshotMaterializationTicks\":").Append(simulation.ResolveInitialPostMovementSnapshotMaterializationTicks)
                    .Append(",\"simulationResolveInitialPostMovementSnapshotResidualTicks\":").Append(resolveInitialPostMovementSnapshotResidualTicks)
                    .Append(",\"presentationCoordinatorTicks\":").Append(presentation.CoordinatorTicks)
                    .Append(",\"presentationCameraAndStateNotificationTicks\":").Append(presentation.CameraAndStateNotificationTicks)
                    .Append(",\"presentationResidualTicks\":").Append(presentationResidualTicks)
                    .Append(",\"presentationPreCommitPlanningTicks\":").Append(presentation.PreCommitPlanningTicks)
                    .Append(",\"presentationCommittedFrameAndStateTicks\":").Append(presentation.CommittedFrameAndStateTicks)
                    .Append(",\"presentationMotionAnimationVfxTicks\":").Append(presentation.MotionAnimationVfxTicks)
                    .Append(",\"presentationAudioTicks\":").Append(presentation.AudioTicks)
                    .Append(",\"presentationApplyCleanupUpdateTicks\":").Append(presentation.ApplyCleanupUpdateTicks)
                    .Append(",\"presentationCoordinatorResidualTicks\":").Append(coordinatorResidualTicks)
                    .Append('}');
                builder.AppendLine(index + 1 < samples.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            var destination = Path.Combine(outputDirectory, "tick-attribution.json");
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(builder.ToString());
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, null);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
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
