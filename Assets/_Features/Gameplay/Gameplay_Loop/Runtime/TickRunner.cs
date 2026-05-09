using System;
using System.IO;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickRunner
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string GlideTraceDirectoryName = "TickTraces";
        private const string GlideTraceFileName = "glide_tick_trace.log";
        private static bool s_glideTraceLogInitialized;
        private static string s_glideTraceLogPath;
#endif

        private readonly TickInputBuffer _inputBuffer;
        private readonly TickPipeline _pipeline;

        public TickRunner(
            TickPipeline pipeline,
            TickInputBuffer inputBuffer,
            int startTickIndex = 1)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));

            if (startTickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startTickIndex), "TickRunner requires a positive starting tick index.");
            }

            NextTickIndex = startTickIndex;
        }

        public int NextTickIndex { get; private set; }

        public TickResult LastResult { get; private set; }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _pipeline.ObjectiveDefinition;

        public StageObjectiveTickResult CurrentObjectiveResult => _pipeline.CurrentObjectiveResult;

        public TickResult RunNextTick()
        {
            return RunTick(_inputBuffer.ConsumeOrDefault(NextTickIndex));
        }

        public TickResult RunTick(in TickInput input)
        {
            if (input.TickIndex != NextTickIndex)
            {
                throw new InvalidOperationException("TickRunner requires monotonic tick execution.");
            }

            var result = _pipeline.RunTick(input);
            if (result.TickIndex != input.TickIndex)
            {
                throw new InvalidOperationException("TickPipeline returned a mismatched tick index.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DumpGlideTraceIfNeeded(result);
#endif

            LastResult = result;
            NextTickIndex = result.TickIndex + 1;
            return result;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void DumpGlideTraceIfNeeded(TickResult result)
        {
            var traceText = result?.Trace?.Text;
            if (string.IsNullOrEmpty(traceText) ||
                !ContainsGlideDebugTrace(traceText))
            {
                return;
            }

            try
            {
                var path = ResolveGlideTraceLogPath();
                File.AppendAllText(
                    path,
                    traceText + Environment.NewLine + Environment.NewLine +
                    "====================" + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to write Glide tick trace. {exception.Message}");
            }
        }

        private static bool ContainsGlideDebugTrace(string traceText)
        {
            return traceText.Contains("EnemyGlideStateDebug", StringComparison.Ordinal) ||
                   traceText.Contains("EnemyGlideMoveIntentDebug", StringComparison.Ordinal) ||
                   traceText.Contains("EnemyGlideKinematicStartDebug", StringComparison.Ordinal);
        }

        private static string ResolveGlideTraceLogPath()
        {
            if (s_glideTraceLogInitialized)
            {
                return s_glideTraceLogPath;
            }

            var directory = Path.Combine(Application.persistentDataPath, GlideTraceDirectoryName);
            Directory.CreateDirectory(directory);

            s_glideTraceLogPath = Path.Combine(directory, GlideTraceFileName);
            if (File.Exists(s_glideTraceLogPath))
            {
                File.Delete(s_glideTraceLogPath);
            }

            s_glideTraceLogInitialized = true;
            UnityEngine.Debug.Log($"Glide tick trace log path: {s_glideTraceLogPath}");
            return s_glideTraceLogPath;
        }
#endif
    }
}
