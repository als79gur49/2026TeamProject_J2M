using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Unity.Profiling;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class TerminalIrisPlayerVisualQualityProbe : MonoBehaviour
    {
        internal const string LaunchArgument = "--terminal-iris-player-visual-quality";
        internal const string OutputArgument = "--terminal-iris-player-visual-output";
        internal const string FocusArgument = "--terminal-iris-player-visual-focus";
        internal const string FrameRateArgument = "--terminal-iris-player-visual-fps";
        internal const string WidthArgument = "--terminal-iris-player-visual-width";
        internal const string HeightArgument = "--terminal-iris-player-visual-height";
        internal const string PerformanceArgument = "--terminal-iris-performance";
        internal const string SuccessMarker = "TERMINAL_IRIS_PLAYER_VISUAL_QUALITY:PASS";
        internal const string FailureMarker = "TERMINAL_IRIS_PLAYER_VISUAL_QUALITY:FAIL";
        private const float TimeoutSeconds = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (!HasArgument(LaunchArgument))
            {
                return;
            }

            var root = new GameObject(nameof(TerminalIrisPlayerVisualQualityProbe));
            DontDestroyOnLoad(root);
            root.AddComponent<TerminalIrisPlayerVisualQualityProbe>();
        }

        private IEnumerator Start()
        {
            var outputDirectory = ReadArgumentValue(OutputArgument);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Fail("output directory argument is missing");
                yield break;
            }

            Directory.CreateDirectory(outputDirectory);
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = ReadPositiveInt(FrameRateArgument, 60);
            var requestedWidth = ReadPositiveInt(WidthArgument, Screen.width);
            var requestedHeight = ReadPositiveInt(HeightArgument, Screen.height);
            Screen.SetResolution(
                requestedWidth,
                requestedHeight,
                FullScreenMode.Windowed);
            var resolutionDeadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < resolutionDeadline &&
                   (Screen.width != requestedWidth || Screen.height != requestedHeight))
            {
                yield return null;
            }

            if (Screen.width != requestedWidth || Screen.height != requestedHeight)
            {
                Fail(
                    $"requested resolution {requestedWidth}x{requestedHeight} " +
                    $"did not settle; actual={Screen.width}x{Screen.height}");
                yield break;
            }

            var focusName = ReadArgumentValue(FocusArgument);
            var focus = string.Equals(focusName, "center", StringComparison.OrdinalIgnoreCase)
                ? new Vector2(0.5f, 0.5f)
                : new Vector2(0.2f, 0.5f);
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            GameplayUiFlowInstaller installer = null;
            GameplaySceneHost host = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                installer = FindFirstObjectByType<GameplayUiFlowInstaller>(FindObjectsInactive.Include);
                host = FindFirstObjectByType<GameplaySceneHost>(FindObjectsInactive.Include);
                if (installer != null &&
                    host != null &&
                    installer.IsInstalledForDiagnostics &&
                    installer.TryGetTerminalTransitionPort(out _))
                {
                    break;
                }

                yield return null;
            }

            if (installer == null || host == null)
            {
                Fail("production gameplay host or UI installer did not become ready");
                yield break;
            }

            if (!StageLaunchContextStore.TryPeek(out var bootstrapContext) ||
                !StageLaunchContextStore.TryConsume(bootstrapContext, out var consumedContext) ||
                consumedContext == null ||
                !consumedContext.Equals(bootstrapContext))
            {
                Fail("exact capture bootstrap launch context was not consumable");
                yield break;
            }

            if (host.OutputCamera == null ||
                host.PlayerEntityId <= 0 ||
                !host.ViewRegistry.TryGetView(host.PlayerEntityId, out var playerView) ||
                playerView == null)
            {
                Fail("output camera or production player view is missing");
                yield break;
            }

            host.InputHost.SetAutoAdvanceTicks(false);
            MoveViewToViewport(host.OutputCamera, playerView, focus);
            yield return null;
            var irisView = installer.RootView.TerminalIrisOverlayView;
            var resolver = installer.RootView
                .RequireTerminalIrisMotionProfile()
                .CreateResolver();
            if (HasArgument(PerformanceArgument))
            {
                yield return CapturePerformanceComparison(
                    outputDirectory,
                    irisView,
                    resolver.ResolveClose(TerminalTransitionKind.Victory).Edge);
            }

            if (!installer.TryForceClearCurrentStageForDiagnostics(out var forceClearMessage) ||
                !installer.TryGetTerminalTransitionPort(out var transitionPort) ||
                transitionPort is not GameplayTerminalTransitionPort productionPort ||
                productionPort.CurrentPlayback == null)
            {
                Fail($"production victory failed: {forceClearMessage}");
                yield break;
            }

            var playback = productionPort.CurrentPlayback;
            var records = new List<CaptureRecord>();
            var timeline = new List<TimelineRecord>();
            var pendingCapturePaths = new List<string>();
            var previousEffectiveRadiusPixels =
                playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                playback.CurrentClosedOvershootPixels;
            var capturedLarge = false;
            var capturedMid = false;
            var capturedSmall = false;
            var capturedLast = false;
            var capturedClosed = false;
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !capturedClosed)
            {
                var effectiveRadiusPixels =
                    playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                    playback.CurrentClosedOvershootPixels;
                timeline.Add(
                    new TimelineRecord(
                        "victory-production-capture",
                        Time.frameCount,
                        Time.realtimeSinceStartup,
                        Time.unscaledDeltaTime,
                        playback.State,
                        playback.CurrentRadius,
                        playback.CurrentClosedOvershootPixels,
                        effectiveRadiusPixels,
                        effectiveRadiusPixels - previousEffectiveRadiusPixels));
                previousEffectiveRadiusPixels = effectiveRadiusPixels;

                if (irisView.IsVisible)
                {
                    var radius = playback.CurrentRadius;
                    if (!capturedLarge && radius <= 0.30f)
                    {
                        yield return Capture(
                            outputDirectory,
                            "victory-close-large",
                            playback,
                            records,
                            pendingCapturePaths);
                        capturedLarge = true;
                    }

                    if (!capturedMid && radius <= 0.15f)
                    {
                        yield return Capture(
                            outputDirectory,
                            "victory-close-mid",
                            playback,
                            records,
                            pendingCapturePaths);
                        capturedMid = true;
                    }

                    if (!capturedSmall && radius <= 0.05f)
                    {
                        yield return Capture(
                            outputDirectory,
                            "victory-close-small",
                            playback,
                            records,
                            pendingCapturePaths);
                        capturedSmall = true;
                    }

                    if (!capturedLast && radius <= 0.01f)
                    {
                        yield return Capture(
                            outputDirectory,
                            "victory-close-last-visible",
                            playback,
                            records,
                            pendingCapturePaths);
                        capturedLast = true;
                    }
                }

                if (!capturedClosed &&
                    (playback.State == TerminalTransitionState.Black ||
                     playback.State == TerminalTransitionState.Completed))
                {
                    yield return Capture(
                        outputDirectory,
                        "victory-close-fully-closed",
                        playback,
                        records,
                        pendingCapturePaths);
                    capturedClosed = true;
                }

                yield return null;
            }

            if (!capturedLarge || !capturedMid || !capturedSmall || !capturedLast || !capturedClosed)
            {
                Fail(
                    $"capture matrix incomplete large={capturedLarge} mid={capturedMid} " +
                    $"small={capturedSmall} last={capturedLast} closed={capturedClosed}");
                yield break;
            }

            installer.enabled = false;
            productionPort.Dispose();
            deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline &&
                   !AllCaptureFilesReady(pendingCapturePaths))
            {
                yield return null;
            }

            if (!AllCaptureFilesReady(pendingCapturePaths))
            {
                Fail("asynchronous Player screenshots did not finish writing");
                yield break;
            }

            yield return MeasureCloseSequence(
                focus,
                irisView,
                TerminalTransitionKind.Victory,
                resolver.ResolveClose(TerminalTransitionKind.Victory),
                timeline);
            yield return CaptureDefeatSequence(
                outputDirectory,
                focus,
                irisView,
                resolver.ResolveClose(TerminalTransitionKind.Defeat),
                null,
                timeline,
                null,
                writeCaptures: false);
            yield return CaptureStageEntrySequence(
                outputDirectory,
                focus,
                irisView,
                resolver.ResolveStageEntryOpen(),
                null,
                timeline,
                null,
                writeCaptures: false);
            yield return CaptureDefeatSequence(
                outputDirectory,
                focus,
                irisView,
                resolver.ResolveClose(TerminalTransitionKind.Defeat),
                records,
                null,
                pendingCapturePaths,
                writeCaptures: true);
            yield return CaptureStageEntrySequence(
                outputDirectory,
                focus,
                irisView,
                resolver.ResolveStageEntryOpen(),
                records,
                null,
                pendingCapturePaths,
                writeCaptures: true);

            deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline &&
                   !AllCaptureFilesReady(pendingCapturePaths))
            {
                yield return null;
            }

            if (!AllCaptureFilesReady(pendingCapturePaths))
            {
                Fail("complete asynchronous Player screenshot matrix did not finish writing");
                yield break;
            }

            WriteManifest(outputDirectory, focus, irisView, records, timeline);
            Debug.Log(
                $"{SuccessMarker} resolution={Screen.width}x{Screen.height} " +
                $"focus={focus.x:F3},{focus.y:F3} fps={UnityEngine.Application.targetFrameRate} " +
                $"captures={records.Count} timelineFrames={timeline.Count}");
            UnityEngine.Application.Quit(0);
        }

        private static IEnumerator CapturePerformanceComparison(
            string outputDirectory,
            TerminalIrisOverlayView irisView,
            TerminalIrisRuntimeEdgeSettings productionEdge)
        {
            const int warmupFrames = 90;
            const int sampleFrames = 600;
            irisView.Show();
            var material = irisView.RuntimeMaterialForTests;
            var productionShader = material.shader;
            var beforeShader = Resources.Load<Shader>(
                "UI/Transitions/TerminalIrisBeforeComparison");
            if (beforeShader == null)
            {
                throw new InvalidOperationException(
                    "Terminal Iris test-only before comparison shader is missing.");
            }

            var originalTargetFrameRate = UnityEngine.Application.targetFrameRate;
            var materialCountBefore = -1;
            var materialCountAfter = -1;
            var materialIdentityBefore = 0;
            var materialIdentityAfter = 0;
            var records = new List<PerformanceFrame>(sampleFrames * 3);
            using (var drawCalls = ProfilerRecorder.StartNew(
                       ProfilerCategory.Render,
                       "Draw Calls Count",
                       1))
            using (var gcAllocated = ProfilerRecorder.StartNew(
                       ProfilerCategory.Memory,
                       "GC Allocated In Frame",
                       1))
            {
                UnityEngine.Application.targetFrameRate = -1;
                irisView.Hide();
                yield return WarmupPerformanceFrames(warmupFrames);
                yield return SamplePerformanceFrames(
                    "inactive-after",
                    sampleFrames,
                    drawCalls,
                    gcAllocated,
                    records);

                irisView.Show();
                material.shader = beforeShader;
                material.SetVector("_Center", new Vector2(0.5f, 0.5f));
                material.SetFloat("_Radius", 0.30f);
                material.SetFloat("_Feather", 1.25f / Mathf.Max(1, Screen.height));
                material.SetColor("_OuterColor", Color.black);
                material.SetFloat("_OuterOpacity", 1f);
                material.SetFloat("_RimWidth", 0f);
                material.SetColor("_RimColor", Color.clear);
                material.SetFloat("_AspectRatio", (float)Screen.width / Mathf.Max(1, Screen.height));
                yield return WarmupPerformanceFrames(warmupFrames);
                materialCountBefore = Resources.FindObjectsOfTypeAll<Material>().Length;
                materialIdentityBefore = RuntimeHelpers.GetHashCode(material);
                yield return SamplePerformanceFrames(
                    "iris-active-before",
                    sampleFrames,
                    drawCalls,
                    gcAllocated,
                    records);

                material.shader = productionShader;
                material.SetVector("_Center", new Vector2(0.5f, 0.5f));
                material.SetFloat("_Radius", 0.30f);
                material.SetFloat("_ClosedOvershootPixels", 0f);
                material.SetColor("_OuterColor", Color.black);
                material.SetFloat("_OuterOpacity", 1f);
                material.SetFloat(
                    "_EdgeAntiAliasScale",
                    productionEdge.EdgeAntiAliasScale);
                material.SetFloat("_MinimumAAPixels", productionEdge.MinimumAAPixels);
                material.SetFloat(
                    "_ArtisticFeatherHalfWidthPixels",
                    productionEdge.ArtisticFeatherHalfWidthPixels);
                material.SetFloat("_RimWidthPixels", 0f);
                material.SetColor("_RimColor", Color.clear);
                yield return WarmupPerformanceFrames(warmupFrames);
                materialCountAfter = Resources.FindObjectsOfTypeAll<Material>().Length;
                materialIdentityAfter = RuntimeHelpers.GetHashCode(material);
                yield return SamplePerformanceFrames(
                    "iris-active-after",
                    sampleFrames,
                    drawCalls,
                    gcAllocated,
                    records);
            }

            WritePerformanceManifest(
                outputDirectory,
                records,
                materialCountBefore,
                materialCountAfter,
                materialIdentityBefore,
                materialIdentityAfter);
            material.shader = productionShader;
            irisView.Hide();
            UnityEngine.Application.targetFrameRate = originalTargetFrameRate;
        }

        private static IEnumerator WarmupPerformanceFrames(int frameCount)
        {
            for (var index = 0; index < frameCount; index++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }
        }

        private static IEnumerator SamplePerformanceFrames(
            string phase,
            int frameCount,
            ProfilerRecorder drawCalls,
            ProfilerRecorder gcAllocated,
            ICollection<PerformanceFrame> records)
        {
            var timings = new FrameTiming[1];
            for (var index = 0; index < frameCount; index++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                var count = FrameTimingManager.GetLatestTimings(1, timings);
                var timing = count > 0 ? timings[0] : default;
                records.Add(
                    new PerformanceFrame(
                        phase,
                        index,
                        timing.cpuMainThreadFrameTime > 0d
                            ? timing.cpuMainThreadFrameTime
                            : timing.cpuFrameTime,
                        timing.cpuRenderThreadFrameTime,
                        timing.gpuFrameTime,
                        drawCalls.Valid ? drawCalls.LastValue : -1,
                        gcAllocated.Valid ? gcAllocated.LastValue : -1));
            }
        }

        private static void WritePerformanceManifest(
            string outputDirectory,
            IReadOnlyList<PerformanceFrame> records,
            int materialCountBefore,
            int materialCountAfter,
            int materialIdentityBefore,
            int materialIdentityAfter)
        {
            var phases = new[]
            {
                "inactive-after",
                "iris-active-before",
                "iris-active-after",
            };
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 1,");
            builder.AppendLine($"  \"graphicsDeviceType\": \"{SystemInfo.graphicsDeviceType}\",");
            builder.AppendLine($"  \"graphicsDeviceName\": \"{Escape(SystemInfo.graphicsDeviceName)}\",");
            builder.AppendLine($"  \"graphicsDeviceVersion\": \"{Escape(SystemInfo.graphicsDeviceVersion)}\",");
            builder.AppendLine($"  \"resolution\": [{Screen.width}, {Screen.height}],");
            builder.AppendLine($"  \"materialCountBefore\": {materialCountBefore},");
            builder.AppendLine($"  \"materialCountAfter\": {materialCountAfter},");
            builder.AppendLine("  \"newMaterialInstances\": " +
                               (materialCountAfter - materialCountBefore) + ",");
            builder.AppendLine($"  \"materialIdentityBefore\": {materialIdentityBefore},");
            builder.AppendLine($"  \"materialIdentityAfter\": {materialIdentityAfter},");
            builder.AppendLine("  \"materialIdentityStable\": " +
                               (materialIdentityBefore == materialIdentityAfter
                                   ? "true,"
                                   : "false,"));
            builder.AppendLine("  \"sampleCountPerPhase\": 600,");
            builder.AppendLine("  \"summaries\": [");
            for (var phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
            {
                var phaseRecords = new List<PerformanceFrame>();
                foreach (var record in records)
                {
                    if (string.Equals(record.Phase, phases[phaseIndex], StringComparison.Ordinal))
                    {
                        phaseRecords.Add(record);
                    }
                }

                builder.Append("    ").Append(
                    PerformanceSummary.Create(phases[phaseIndex], phaseRecords).ToJson());
                builder.AppendLine(phaseIndex + 1 < phases.Length ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(
                Path.Combine(outputDirectory, "performance-metrics.json"),
                builder.ToString());
        }

        private static IEnumerator Capture(
            string outputDirectory,
            string label,
            TerminalTransitionPlayback playback,
            ICollection<CaptureRecord> records,
            ICollection<string> pendingCapturePaths)
        {
            yield return new WaitForEndOfFrame();
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var path = Path.Combine(outputDirectory, label + ".png");
            WriteCurrentFramePng(path, width, height);
            pendingCapturePaths.Add(path);
            records.Add(
                new CaptureRecord(
                    label,
                    Time.frameCount,
                    Time.realtimeSinceStartup,
                    Time.unscaledDeltaTime,
                    playback.State,
                    playback.CurrentRadius,
                    playback.CurrentClosedOvershootPixels,
                    (float)width / height,
                    width,
                    height));
        }

        private static IEnumerator CaptureDefeatSequence(
            string outputDirectory,
            Vector2 focus,
            TerminalIrisOverlayView irisView,
            TerminalIrisRuntimePreset preset,
            ICollection<CaptureRecord> captures,
            ICollection<TimelineRecord> timeline,
            ICollection<string> pendingCapturePaths,
            bool writeCaptures)
        {
            irisView.Show();
            using (var playback = new TerminalTransitionPlayback(preset))
            {
                var closeRadius = Mathf.Max(
                    irisView.CalculateFullyRevealedRadius(preset.FallbackCenter, 0f),
                    irisView.CalculateFullyRevealedRadius(focus, 0f));
                var revealRadius = irisView.CalculateFullyRevealedRadius(
                    focus,
                    preset.RevealPreset.Value.FullOpenMargin);
                playback.ConfigureFullyRevealedRadii(closeRadius, revealRadius);
                var token = new TerminalSessionToken(9001, Time.frameCount);
                if (!playback.TryBegin(
                        new TerminalTransitionRequest(
                            TerminalTransitionKind.Defeat,
                            1,
                            token,
                            TerminalTransitionDestinationMode.SameScene),
                        new TerminalFocusTarget(focus, 0.12f, false)))
                {
                    throw new InvalidOperationException(
                        "Player visual-quality Defeat playback did not begin.");
                }

                var previousEffectiveRadius =
                    playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                    playback.CurrentClosedOvershootPixels;
                var capturedMid = false;
                var capturedSmall = false;
                var capturedLast = false;
                while (playback.State != TerminalTransitionState.Black)
                {
                    playback.Advance(
                        ResolvePlaybackDeltaTime(writeCaptures));
                    irisView.Apply(playback);
                    RecordTimeline(
                        "defeat-close",
                        playback,
                        timeline,
                        ref previousEffectiveRadius);
                    if (playback.State == TerminalTransitionState.Closing &&
                        !capturedMid &&
                        playback.CurrentRadius <= 0.15f)
                    {
                        if (writeCaptures)
                        {
                            yield return Capture(
                                outputDirectory,
                                "defeat-close-mid",
                                playback,
                                captures,
                                pendingCapturePaths);
                        }

                        capturedMid = true;
                    }

                    if (playback.State == TerminalTransitionState.Closing &&
                        !capturedSmall &&
                        playback.CurrentRadius <= 0.05f)
                    {
                        if (writeCaptures)
                        {
                            yield return Capture(
                                outputDirectory,
                                "defeat-close-small",
                                playback,
                                captures,
                                pendingCapturePaths);
                        }

                        capturedSmall = true;
                    }

                    if (playback.State == TerminalTransitionState.Closing &&
                        !capturedLast &&
                        playback.CurrentRadius <= 0.01f)
                    {
                        if (writeCaptures)
                        {
                            yield return Capture(
                                outputDirectory,
                                "defeat-close-last-visible",
                                playback,
                                captures,
                                pendingCapturePaths);
                        }

                        capturedLast = true;
                    }

                    yield return null;
                }

                irisView.Apply(playback);
                if (writeCaptures)
                {
                    yield return Capture(
                        outputDirectory,
                        "defeat-close-fully-closed",
                        playback,
                        captures,
                        pendingCapturePaths);
                }

                if (!playback.RequestReveal(token))
                {
                    throw new InvalidOperationException(
                        "Player visual-quality Defeat reveal did not begin.");
                }

                var capturedFirst = false;
                capturedMid = false;
                previousEffectiveRadius =
                    playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                    playback.CurrentClosedOvershootPixels;
                while (playback.State == TerminalTransitionState.Revealing)
                {
                    playback.Advance(
                        ResolvePlaybackDeltaTime(writeCaptures));
                    irisView.Apply(playback);
                    RecordTimeline(
                        "defeat-reveal",
                        playback,
                        timeline,
                        ref previousEffectiveRadius);
                    if (!capturedFirst && playback.CurrentRadius > 0f)
                    {
                        if (writeCaptures)
                        {
                            yield return Capture(
                                outputDirectory,
                                "defeat-reveal-first-visible",
                                playback,
                                captures,
                                pendingCapturePaths);
                        }

                        capturedFirst = true;
                    }

                    if (!capturedMid && playback.CurrentRadius >= revealRadius * 0.5f)
                    {
                        if (writeCaptures)
                        {
                            yield return Capture(
                                outputDirectory,
                                "defeat-reveal-mid",
                                playback,
                                captures,
                                pendingCapturePaths);
                        }

                        capturedMid = true;
                    }

                    yield return null;
                }

                irisView.Apply(playback);
                if (writeCaptures)
                {
                    yield return Capture(
                        outputDirectory,
                        "defeat-reveal-fully-open",
                        playback,
                        captures,
                        pendingCapturePaths);
                }
            }
        }

        private static IEnumerator CaptureStageEntrySequence(
            string outputDirectory,
            Vector2 focus,
            TerminalIrisOverlayView irisView,
            TerminalIrisRuntimeOpenPreset preset,
            ICollection<CaptureRecord> captures,
            ICollection<TimelineRecord> timeline,
            ICollection<string> pendingCapturePaths,
            bool writeCaptures)
        {
            irisView.Show();
            irisView.ApplyClosedEntry(focus, preset);
            if (writeCaptures)
            {
                yield return CaptureEntry(
                    outputDirectory,
                    "stage-entry-fully-closed",
                    0f,
                    preset.FinalClosedOvershootPixels,
                    captures,
                    pendingCapturePaths);
            }
            var fullRadius = irisView.CalculateFullyRevealedRadius(
                focus,
                preset.FullOpenMargin);
            var elapsed = 0f;
            var previousEffectiveRadius = -preset.FinalClosedOvershootPixels;
            var capturedFirst = false;
            var capturedMid = false;
            while (elapsed < preset.OpeningDuration)
            {
                elapsed = Mathf.Min(
                    preset.OpeningDuration,
                    elapsed + ResolvePlaybackDeltaTime(writeCaptures));
                var progress = elapsed / preset.OpeningDuration;
                var eased = TerminalIrisEasingUtility.Evaluate(preset.OpeningEasing, progress);
                var radius = fullRadius * eased;
                var overshoot = preset.FinalClosedOvershootPixels * (1f - eased);
                irisView.ApplyEntryRadius(radius, overshoot);
                var effectiveRadius = radius * Mathf.Max(1, Screen.height) - overshoot;
                if (timeline != null)
                {
                    timeline.Add(
                        new TimelineRecord(
                            "stage-entry-open",
                            Time.frameCount,
                            Time.realtimeSinceStartup,
                            Time.unscaledDeltaTime,
                            TerminalTransitionState.Revealing,
                            radius,
                            overshoot,
                            effectiveRadius,
                            effectiveRadius - previousEffectiveRadius));
                }

                previousEffectiveRadius = effectiveRadius;
                if (!capturedFirst && radius > 0f)
                {
                    if (writeCaptures)
                    {
                        yield return CaptureEntry(
                            outputDirectory,
                            "stage-entry-first-visible",
                            radius,
                            overshoot,
                            captures,
                            pendingCapturePaths);
                    }

                    capturedFirst = true;
                }

                if (!capturedMid && radius >= fullRadius * 0.5f)
                {
                    if (writeCaptures)
                    {
                        yield return CaptureEntry(
                            outputDirectory,
                            "stage-entry-mid",
                            radius,
                            overshoot,
                            captures,
                            pendingCapturePaths);
                    }

                    capturedMid = true;
                }

                yield return null;
            }

            if (writeCaptures)
            {
                yield return CaptureEntry(
                    outputDirectory,
                    "stage-entry-fully-open",
                    fullRadius,
                    0f,
                    captures,
                    pendingCapturePaths);
            }

            irisView.Hide();
        }

        private static IEnumerator MeasureCloseSequence(
            Vector2 focus,
            TerminalIrisOverlayView irisView,
            TerminalTransitionKind kind,
            TerminalIrisRuntimePreset preset,
            ICollection<TimelineRecord> timeline)
        {
            irisView.Show();
            using (var playback = new TerminalTransitionPlayback(preset))
            {
                var fullyRevealedRadius = Mathf.Max(
                    irisView.CalculateFullyRevealedRadius(preset.FallbackCenter, 0f),
                    irisView.CalculateFullyRevealedRadius(focus, 0f));
                playback.ConfigureFullyRevealedRadii(
                    fullyRevealedRadius,
                    fullyRevealedRadius);
                var token = new TerminalSessionToken(9000, Time.frameCount);
                if (!playback.TryBegin(
                        new TerminalTransitionRequest(
                            kind,
                            1,
                            token,
                            TerminalTransitionDestinationMode.SceneHandoff),
                        new TerminalFocusTarget(focus, 0.12f, false)))
                {
                    throw new InvalidOperationException(
                        "Player visual-quality close measurement did not begin.");
                }

                var previousEffectiveRadius =
                    playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                    playback.CurrentClosedOvershootPixels;
                while (playback.State != TerminalTransitionState.Black)
                {
                    playback.Advance(Mathf.Max(0f, Time.unscaledDeltaTime));
                    irisView.Apply(playback);
                    if (playback.State == TerminalTransitionState.Closing ||
                        playback.State == TerminalTransitionState.Black)
                    {
                        RecordTimeline(
                            kind == TerminalTransitionKind.Victory
                                ? "victory-close"
                                : "defeat-close",
                            playback,
                            timeline,
                            ref previousEffectiveRadius);
                    }
                    else
                    {
                        previousEffectiveRadius =
                            playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                            playback.CurrentClosedOvershootPixels;
                    }

                    yield return null;
                }
            }
        }

        private static IEnumerator CaptureEntry(
            string outputDirectory,
            string label,
            float radius,
            float closedOvershootPixels,
            ICollection<CaptureRecord> records,
            ICollection<string> pendingCapturePaths)
        {
            yield return new WaitForEndOfFrame();
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var path = Path.Combine(outputDirectory, label + ".png");
            WriteCurrentFramePng(path, width, height);
            pendingCapturePaths.Add(path);
            records.Add(
                new CaptureRecord(
                    label,
                    Time.frameCount,
                    Time.realtimeSinceStartup,
                    Time.unscaledDeltaTime,
                    TerminalTransitionState.Revealing,
                    radius,
                    closedOvershootPixels,
                    (float)width / height,
                    width,
                    height));
        }

        private static void RecordTimeline(
            string transition,
            TerminalTransitionPlayback playback,
            ICollection<TimelineRecord> timeline,
            ref float previousEffectiveRadius)
        {
            if (timeline == null)
            {
                return;
            }

            var effectiveRadius =
                playback.CurrentRadius * Mathf.Max(1, Screen.height) -
                playback.CurrentClosedOvershootPixels;
            timeline.Add(
                new TimelineRecord(
                    transition,
                    Time.frameCount,
                    Time.realtimeSinceStartup,
                    Time.unscaledDeltaTime,
                    playback.State,
                    playback.CurrentRadius,
                    playback.CurrentClosedOvershootPixels,
                    effectiveRadius,
                    effectiveRadius - previousEffectiveRadius));
            previousEffectiveRadius = effectiveRadius;
        }

        private static float ResolvePlaybackDeltaTime(bool capturePass)
        {
            var actualDeltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            if (!capturePass)
            {
                return actualDeltaTime;
            }

            return Mathf.Min(
                actualDeltaTime,
                1f / Mathf.Max(1, UnityEngine.Application.targetFrameRate));
        }

        private static void WriteCurrentFramePng(string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                if (!target.Create())
                {
                    throw new InvalidOperationException(
                        "Player visual capture RenderTexture creation failed.");
                }

                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                for (var y = 0; y < height / 2; y++)
                {
                    var oppositeY = height - 1 - y;
                    for (var x = 0; x < width; x++)
                    {
                        var lowerIndex = y * width + x;
                        var upperIndex = oppositeY * width + x;
                        var temporary = pixels[lowerIndex];
                        pixels[lowerIndex] = pixels[upperIndex];
                        pixels[upperIndex] = temporary;
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Destroy(texture);
                Destroy(target);
            }
        }

        private static void WriteManifest(
            string outputDirectory,
            Vector2 focus,
            TerminalIrisOverlayView irisView,
            IReadOnlyList<CaptureRecord> records,
            IReadOnlyList<TimelineRecord> timeline)
        {
            var summaries = BuildTemporalSummaries(timeline);
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 3,");
            builder.AppendLine($"  \"resolution\": [{Screen.width}, {Screen.height}],");
            builder.AppendLine($"  \"focus\": [{Float(focus.x)}, {Float(focus.y)}],");
            builder.AppendLine($"  \"targetFrameRate\": {UnityEngine.Application.targetFrameRate},");
            builder.AppendLine($"  \"graphicsDeviceType\": \"{SystemInfo.graphicsDeviceType}\",");
            builder.AppendLine($"  \"graphicsDeviceName\": \"{Escape(SystemInfo.graphicsDeviceName)}\",");
            builder.AppendLine("  \"shaderAspectSource\": \"_ScreenParams\",");
            builder.AppendLine(
                $"  \"runtimeMaterialName\": \"{Escape(irisView.RuntimeMaterialForTests?.name)}\",");
            builder.AppendLine("  \"temporalLifecyclePass\": true,");
            builder.AppendLine("  \"temporalVisualPass\": null,");
            builder.AppendLine("  \"manualVisualVerdict\": \"EVIDENCE_INCONCLUSIVE\",");
            builder.AppendLine("  \"captures\": [");
            for (var index = 0; index < records.Count; index++)
            {
                builder.Append("    ").Append(records[index].ToJson());
                builder.AppendLine(index + 1 < records.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"timeline\": [");
            for (var index = 0; index < timeline.Count; index++)
            {
                builder.Append("    ").Append(timeline[index].ToJson());
                builder.AppendLine(index + 1 < timeline.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"temporalSummaries\": [");
            for (var index = 0; index < summaries.Count; index++)
            {
                builder.Append("    ").Append(summaries[index].ToJson());
                builder.AppendLine(index + 1 < summaries.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(outputDirectory, "player-visual-manifest.json"), builder.ToString());
        }

        private static IReadOnlyList<TemporalSummary> BuildTemporalSummaries(
            IReadOnlyList<TimelineRecord> timeline)
        {
            var summaries = new List<TemporalSummary>();
            var grouped = new Dictionary<string, List<TimelineRecord>>();
            foreach (var record in timeline)
            {
                if (!grouped.TryGetValue(record.Transition, out var records))
                {
                    records = new List<TimelineRecord>();
                    grouped.Add(record.Transition, records);
                }

                records.Add(record);
            }

            foreach (var pair in grouped)
            {
                var raw = pair.Value;
                var filtered = raw.FindAll(record => !record.SchedulerHitchFlag);
                if (filtered.Count == 0)
                {
                    filtered = raw;
                }

                var opening =
                    pair.Key.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pair.Key.IndexOf("reveal", StringComparison.OrdinalIgnoreCase) >= 0;
                var lifecyclePass = true;
                foreach (var record in raw)
                {
                    if ((opening && record.EdgeDisplacementPixels < -0.01f) ||
                        (!opening && record.EdgeDisplacementPixels > 0.01f))
                    {
                        lifecyclePass = false;
                        break;
                    }
                }

                summaries.Add(
                    new TemporalSummary(
                        pair.Key,
                        raw.Count,
                        raw.FindAll(record => record.SchedulerHitchFlag).Count,
                        Median(raw, record => record.MeasuredFps),
                        Maximum(raw, record => Mathf.Abs(record.EdgeDisplacementPixels)),
                        Percentile(raw, record => Mathf.Abs(record.EdgeDisplacementPixels), 0.95f),
                        Percentile(raw, record => Mathf.Abs(record.EdgeDisplacementPixels), 0.99f),
                        Maximum(
                            filtered,
                            record => Mathf.Abs(record.EdgeDisplacementPixels)),
                        Percentile(
                            filtered,
                            record => Mathf.Abs(record.EdgeDisplacementPixels),
                            0.95f),
                        Percentile(
                            filtered,
                            record => Mathf.Abs(record.EdgeDisplacementPixels),
                            0.99f),
                        lifecyclePass));
            }

            return summaries;
        }

        private static float Maximum(
            IReadOnlyList<TimelineRecord> records,
            Func<TimelineRecord, float> selector)
        {
            var maximum = float.MinValue;
            foreach (var record in records)
            {
                maximum = Mathf.Max(maximum, selector(record));
            }

            return maximum;
        }

        private static float Median(
            IReadOnlyList<TimelineRecord> records,
            Func<TimelineRecord, float> selector)
        {
            return Percentile(records, selector, 0.5f);
        }

        private static float Percentile(
            IReadOnlyList<TimelineRecord> records,
            Func<TimelineRecord, float> selector,
            float percentile)
        {
            var values = new List<float>(records.Count);
            foreach (var record in records)
            {
                values.Add(selector(record));
            }

            values.Sort();
            var position = Mathf.Clamp01(percentile) * (values.Count - 1);
            var lower = Mathf.FloorToInt(position);
            var upper = Mathf.CeilToInt(position);
            return Mathf.Lerp(values[lower], values[upper], position - lower);
        }

        private static bool AllCaptureFilesReady(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static void MoveViewToViewport(
            Camera camera,
            GameplayEntityView view,
            Vector2 targetViewport)
        {
            var bounds = CalculateRendererBounds(view);
            var currentViewport = camera.WorldToViewportPoint(bounds.center);
            var targetWorld = camera.ViewportToWorldPoint(
                new Vector3(targetViewport.x, targetViewport.y, currentViewport.z));
            view.transform.position += targetWorld - bounds.center;
        }

        private static Bounds CalculateRendererBounds(GameplayEntityView view)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
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

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void Fail(string diagnostic)
        {
            Debug.LogError($"{FailureMarker} {diagnostic}");
            UnityEngine.Application.Quit(2);
        }

        private readonly struct PerformanceFrame
        {
            public PerformanceFrame(
                string phase,
                int frame,
                double cpuFrameTimeMilliseconds,
                double cpuRenderThreadTimeMilliseconds,
                double gpuFrameTimeMilliseconds,
                long drawCalls,
                long gcAllocatedBytes)
            {
                Phase = phase;
                Frame = frame;
                CpuFrameTimeMilliseconds = cpuFrameTimeMilliseconds;
                CpuRenderThreadTimeMilliseconds = cpuRenderThreadTimeMilliseconds;
                GpuFrameTimeMilliseconds = gpuFrameTimeMilliseconds;
                DrawCalls = drawCalls;
                GcAllocatedBytes = gcAllocatedBytes;
            }

            public string Phase { get; }
            public int Frame { get; }
            public double CpuFrameTimeMilliseconds { get; }
            public double CpuRenderThreadTimeMilliseconds { get; }
            public double GpuFrameTimeMilliseconds { get; }
            public long DrawCalls { get; }
            public long GcAllocatedBytes { get; }
        }

        private readonly struct PerformanceSummary
        {
            private PerformanceSummary(
                string phase,
                int sampleCount,
                double cpuMedian,
                double cpuP95,
                double cpuP99,
                double cpuRenderMedian,
                double cpuRenderP95,
                double gpuMedian,
                double gpuP95,
                double gpuP99,
                long drawCallsMedian,
                long drawCallsMaximum,
                long gcAllocatedMedian,
                long gcAllocatedP95,
                long gcAllocatedP99,
                long gcAllocatedMaximum)
            {
                Phase = phase;
                SampleCount = sampleCount;
                CpuMedian = cpuMedian;
                CpuP95 = cpuP95;
                CpuP99 = cpuP99;
                CpuRenderMedian = cpuRenderMedian;
                CpuRenderP95 = cpuRenderP95;
                GpuMedian = gpuMedian;
                GpuP95 = gpuP95;
                GpuP99 = gpuP99;
                DrawCallsMedian = drawCallsMedian;
                DrawCallsMaximum = drawCallsMaximum;
                GcAllocatedMedian = gcAllocatedMedian;
                GcAllocatedP95 = gcAllocatedP95;
                GcAllocatedP99 = gcAllocatedP99;
                GcAllocatedMaximum = gcAllocatedMaximum;
            }

            private string Phase { get; }
            private int SampleCount { get; }
            private double CpuMedian { get; }
            private double CpuP95 { get; }
            private double CpuP99 { get; }
            private double CpuRenderMedian { get; }
            private double CpuRenderP95 { get; }
            private double GpuMedian { get; }
            private double GpuP95 { get; }
            private double GpuP99 { get; }
            private long DrawCallsMedian { get; }
            private long DrawCallsMaximum { get; }
            private long GcAllocatedMedian { get; }
            private long GcAllocatedP95 { get; }
            private long GcAllocatedP99 { get; }
            private long GcAllocatedMaximum { get; }

            public static PerformanceSummary Create(
                string phase,
                IReadOnlyList<PerformanceFrame> records)
            {
                var cpu = new List<double>(records.Count);
                var cpuRender = new List<double>(records.Count);
                var gpu = new List<double>(records.Count);
                var drawCalls = new List<long>(records.Count);
                var gcAllocated = new List<long>(records.Count);
                foreach (var record in records)
                {
                    if (record.CpuFrameTimeMilliseconds > 0d)
                    {
                        cpu.Add(record.CpuFrameTimeMilliseconds);
                    }

                    if (record.CpuRenderThreadTimeMilliseconds > 0d)
                    {
                        cpuRender.Add(record.CpuRenderThreadTimeMilliseconds);
                    }

                    if (record.GpuFrameTimeMilliseconds > 0d)
                    {
                        gpu.Add(record.GpuFrameTimeMilliseconds);
                    }

                    if (record.DrawCalls >= 0)
                    {
                        drawCalls.Add(record.DrawCalls);
                    }

                    if (record.GcAllocatedBytes >= 0)
                    {
                        gcAllocated.Add(record.GcAllocatedBytes);
                    }
                }

                cpu.Sort();
                cpuRender.Sort();
                gpu.Sort();
                drawCalls.Sort();
                gcAllocated.Sort();
                return new PerformanceSummary(
                    phase,
                    records.Count,
                    Sample(cpu, 0.5),
                    Sample(cpu, 0.95),
                    Sample(cpu, 0.99),
                    Sample(cpuRender, 0.5),
                    Sample(cpuRender, 0.95),
                    Sample(gpu, 0.5),
                    Sample(gpu, 0.95),
                    Sample(gpu, 0.99),
                    Sample(drawCalls, 0.5),
                    drawCalls.Count > 0 ? drawCalls[drawCalls.Count - 1] : -1,
                    Sample(gcAllocated, 0.5),
                    Sample(gcAllocated, 0.95),
                    Sample(gcAllocated, 0.99),
                    gcAllocated.Count > 0 ? gcAllocated[gcAllocated.Count - 1] : -1);
            }

            public string ToJson()
            {
                return "{\"phase\":\"" + Escape(Phase) +
                       "\",\"sampleCount\":" + SampleCount +
                       ",\"cpuMainMedianMilliseconds\":" + Double(CpuMedian) +
                       ",\"cpuMainP95Milliseconds\":" + Double(CpuP95) +
                       ",\"cpuMainP99Milliseconds\":" + Double(CpuP99) +
                       ",\"cpuRenderMedianMilliseconds\":" +
                       Double(CpuRenderMedian) +
                       ",\"cpuRenderP95Milliseconds\":" +
                       Double(CpuRenderP95) +
                       ",\"gpuMedianMilliseconds\":" + Double(GpuMedian) +
                       ",\"gpuP95Milliseconds\":" + Double(GpuP95) +
                       ",\"gpuP99Milliseconds\":" + Double(GpuP99) +
                       ",\"drawCallsMedian\":" + DrawCallsMedian +
                       ",\"drawCallsMaximum\":" + DrawCallsMaximum +
                       ",\"gcAllocatedBytesMedian\":" + GcAllocatedMedian +
                       ",\"gcAllocatedBytesP95\":" + GcAllocatedP95 +
                       ",\"gcAllocatedBytesP99\":" + GcAllocatedP99 +
                       ",\"gcAllocatedBytesMaximum\":" + GcAllocatedMaximum +
                       "}";
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
                return values[lower] +
                       (values[upper] - values[lower]) * (position - lower);
            }

            private static long Sample(IReadOnlyList<long> values, double percentile)
            {
                if (values.Count == 0)
                {
                    return -1;
                }

                var position = Math.Max(0d, Math.Min(1d, percentile)) * (values.Count - 1);
                return values[(int)Math.Round(position)];
            }

            private static string Double(double value)
            {
                return value.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        private readonly struct CaptureRecord
        {
            public CaptureRecord(
                string label,
                int frame,
                float timestamp,
                float deltaTime,
                TerminalTransitionState state,
                float radius,
                float closedOvershootPixels,
                float shaderAspect,
                int width,
                int height)
            {
                Label = label;
                Frame = frame;
                Timestamp = timestamp;
                DeltaTime = deltaTime;
                State = state;
                Radius = radius;
                ClosedOvershootPixels = closedOvershootPixels;
                ShaderAspect = shaderAspect;
                Width = width;
                Height = height;
            }

            private string Label { get; }
            private int Frame { get; }
            private float Timestamp { get; }
            private float DeltaTime { get; }
            private TerminalTransitionState State { get; }
            private float Radius { get; }
            private float ClosedOvershootPixels { get; }
            private float ShaderAspect { get; }
            private int Width { get; }
            private int Height { get; }

            public string ToJson()
            {
                return "{\"label\":\"" + Escape(Label) +
                       "\",\"frame\":" + Frame +
                       ",\"timestamp\":" + Float(Timestamp) +
                       ",\"deltaTime\":" + Float(DeltaTime) +
                       ",\"state\":\"" + State +
                       "\",\"radius\":" + Float(Radius) +
                       ",\"closedOvershootPixels\":" + Float(ClosedOvershootPixels) +
                       ",\"effectiveRadiusPixels\":" +
                       Float(Radius * Height - ClosedOvershootPixels) +
                       ",\"shaderAspect\":" + Float(ShaderAspect) +
                       ",\"targetAspect\":" + Float((float)Width / Height) +
                       "}";
            }
        }

        private readonly struct TimelineRecord
        {
            public TimelineRecord(
                string transition,
                int frame,
                float timestamp,
                float deltaTime,
                TerminalTransitionState state,
                float radius,
                float closedOvershootPixels,
                float effectiveRadiusPixels,
                float edgeDisplacementPixels)
            {
                Transition = transition;
                Frame = frame;
                Timestamp = timestamp;
                DeltaTime = deltaTime;
                State = state;
                Radius = radius;
                ClosedOvershootPixels = closedOvershootPixels;
                EffectiveRadiusPixels = effectiveRadiusPixels;
                EdgeDisplacementPixels = edgeDisplacementPixels;
            }

            public string Transition { get; }
            public int Frame { get; }
            public float Timestamp { get; }
            public float DeltaTime { get; }
            public TerminalTransitionState State { get; }
            public float Radius { get; }
            public float ClosedOvershootPixels { get; }
            public float EffectiveRadiusPixels { get; }
            public float EdgeDisplacementPixels { get; }
            public float MeasuredFps => DeltaTime > 0.000001f ? 1f / DeltaTime : 0f;
            public bool SchedulerHitchFlag =>
                DeltaTime > 1.75f /
                Mathf.Max(1, UnityEngine.Application.targetFrameRate);

            public string ToJson()
            {
                var height = Mathf.Max(1, Screen.height);
                var currentContourRadius = Mathf.Max(0f, EffectiveRadiusPixels);
                var previousContourRadius = Mathf.Max(
                    0f,
                    EffectiveRadiusPixels - EdgeDisplacementPixels);
                var newlyChangedArea = Mathf.Abs(
                    Mathf.PI *
                    (currentContourRadius * currentContourRadius -
                     previousContourRadius * previousContourRadius));
                var exactClosed =
                    State == TerminalTransitionState.Black ||
                    State == TerminalTransitionState.Completed;
                return "{\"transition\":\"" + Escape(Transition) +
                       "\",\"frame\":" + Frame +
                       ",\"timestamp\":" + Float(Timestamp) +
                       ",\"actualDeltaTime\":" + Float(DeltaTime) +
                       ",\"measuredFps\":" + Float(MeasuredFps) +
                       ",\"targetFps\":" + UnityEngine.Application.targetFrameRate +
                       ",\"state\":\"" + State +
                       "\",\"normalizedProgress\":null" +
                       ",\"inputRadius\":" + Float(Radius) +
                       ",\"closedOvershootPixels\":" + Float(ClosedOvershootPixels) +
                       ",\"closedOvershootNormalized\":" +
                       Float(ClosedOvershootPixels / height) +
                       ",\"effectiveRadiusNormalized\":" +
                       Float(EffectiveRadiusPixels / height) +
                       ",\"effectiveRadiusPixels\":" + Float(EffectiveRadiusPixels) +
                       ",\"measuredContourRadiusPixels\":" +
                       Float(currentContourRadius) +
                       ",\"edgeDisplacementPixels\":" + Float(EdgeDisplacementPixels) +
                       ",\"newlyChangedAreaPixels\":" + Float(newlyChangedArea) +
                       ",\"meanLuminanceDelta\":null" +
                       ",\"p95EdgeTemporalDifference\":null" +
                       ",\"p99EdgeTemporalDifference\":null" +
                       ",\"schedulerHitchFlag\":" +
                       (SchedulerHitchFlag ? "true" : "false") +
                       ",\"exactClosedFlag\":" +
                       (exactClosed ? "true" : "false") +
                       ",\"handoffReadyFlag\":" +
                       (exactClosed ? "true" : "false") +
                       "}";
            }
        }

        private readonly struct TemporalSummary
        {
            public TemporalSummary(
                string transition,
                int frameCount,
                int schedulerHitchCount,
                float measuredFpsMedian,
                float rawMaximumPixels,
                float rawP95Pixels,
                float rawP99Pixels,
                float hitchFilteredMaximumPixels,
                float hitchFilteredP95Pixels,
                float hitchFilteredP99Pixels,
                bool temporalLifecyclePass)
            {
                Transition = transition;
                FrameCount = frameCount;
                SchedulerHitchCount = schedulerHitchCount;
                MeasuredFpsMedian = measuredFpsMedian;
                RawMaximumPixels = rawMaximumPixels;
                RawP95Pixels = rawP95Pixels;
                RawP99Pixels = rawP99Pixels;
                HitchFilteredMaximumPixels = hitchFilteredMaximumPixels;
                HitchFilteredP95Pixels = hitchFilteredP95Pixels;
                HitchFilteredP99Pixels = hitchFilteredP99Pixels;
                TemporalLifecyclePass = temporalLifecyclePass;
            }

            private string Transition { get; }
            private int FrameCount { get; }
            private int SchedulerHitchCount { get; }
            private float MeasuredFpsMedian { get; }
            private float RawMaximumPixels { get; }
            private float RawP95Pixels { get; }
            private float RawP99Pixels { get; }
            private float HitchFilteredMaximumPixels { get; }
            private float HitchFilteredP95Pixels { get; }
            private float HitchFilteredP99Pixels { get; }
            private bool TemporalLifecyclePass { get; }

            public string ToJson()
            {
                return "{\"transition\":\"" + Escape(Transition) +
                       "\",\"frameCount\":" + FrameCount +
                       ",\"schedulerHitchCount\":" + SchedulerHitchCount +
                       ",\"measuredFpsMedian\":" + Float(MeasuredFpsMedian) +
                       ",\"rawMaximumPixels\":" + Float(RawMaximumPixels) +
                       ",\"rawP95Pixels\":" + Float(RawP95Pixels) +
                       ",\"rawP99Pixels\":" + Float(RawP99Pixels) +
                       ",\"hitchFilteredMaximumPixels\":" +
                       Float(HitchFilteredMaximumPixels) +
                       ",\"hitchFilteredP95Pixels\":" +
                       Float(HitchFilteredP95Pixels) +
                       ",\"hitchFilteredP99Pixels\":" +
                       Float(HitchFilteredP99Pixels) +
                       ",\"temporalLifecyclePass\":" +
                       (TemporalLifecyclePass ? "true" : "false") +
                       ",\"temporalVisualPass\":null" +
                       ",\"manualVisualVerdict\":\"EVIDENCE_INCONCLUSIVE\"" +
                       "}";
            }
        }
    }
}
