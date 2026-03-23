using System;
using System.IO;
using System.Text;
using Game.Feature.Gameplay.Tests.Replay;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    internal sealed class ReplayArtifactWriter
    {
        internal const string DefaultArtifactRoot = "TestResult/DeterminismArtifacts";

        private readonly string _rootDirectoryPath;

        public ReplayArtifactWriter(string rootDirectoryPath = DefaultArtifactRoot)
        {
            if (string.IsNullOrWhiteSpace(rootDirectoryPath))
            {
                throw new ArgumentException("Artifact root directory path must not be empty.", nameof(rootDirectoryPath));
            }

            _rootDirectoryPath = ResolveRootDirectoryPath(rootDirectoryPath);
        }

        public string RootDirectoryPath => _rootDirectoryPath;

        public string Write(ReplayDivergenceArtifact artifact)
        {
            if (artifact == null)
            {
                throw new ArgumentNullException(nameof(artifact));
            }

            Directory.CreateDirectory(_rootDirectoryPath);

            var artifactDirectoryPath = CreateUniqueArtifactDirectoryPath(artifact);
            Directory.CreateDirectory(artifactDirectoryPath);

            File.WriteAllText(Path.Combine(artifactDirectoryPath, "summary.txt"), BuildSummary(artifact));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "scenario.txt"), EnsureTrailingNewline(artifact.ScenarioDump));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "first_run_hashes.txt"), BuildHashDump(artifact.FirstRunFrames));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "second_run_hashes.txt"), BuildHashDump(artifact.SecondRunFrames));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "first_run_trace.txt"), BuildTraceDump(artifact, firstRun: true));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "second_run_trace.txt"), BuildTraceDump(artifact, firstRun: false));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "first_run_event_log.txt"), BuildEventLogDump(artifact, firstRun: true));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "second_run_event_log.txt"), BuildEventLogDump(artifact, firstRun: false));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "first_run_final_entities.txt"), BuildFinalEntitiesDump(artifact, firstRun: true));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "second_run_final_entities.txt"), BuildFinalEntitiesDump(artifact, firstRun: false));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "first_run_occupancy.txt"), BuildOccupancyDump(artifact, firstRun: true));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "second_run_occupancy.txt"), BuildOccupancyDump(artifact, firstRun: false));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "first_run_marked_for_death.txt"), BuildMarkedForDeathDump(artifact, firstRun: true));
            File.WriteAllText(Path.Combine(artifactDirectoryPath, "second_run_marked_for_death.txt"), BuildMarkedForDeathDump(artifact, firstRun: false));

            return artifactDirectoryPath;
        }

        private static string ResolveRootDirectoryPath(string configuredRootDirectoryPath)
        {
            if (Path.IsPathRooted(configuredRootDirectoryPath))
            {
                return Path.GetFullPath(configuredRootDirectoryPath);
            }

            var projectRootPath = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRootPath))
            {
                return Path.GetFullPath(configuredRootDirectoryPath);
            }

            return Path.GetFullPath(Path.Combine(projectRootPath, configuredRootDirectoryPath));
        }

        private string CreateUniqueArtifactDirectoryPath(ReplayDivergenceArtifact artifact)
        {
            var baseDirectoryName = $"seed_{FormatSeedForPath(artifact.Seed)}_tick_{artifact.FirstDivergentTick:D5}";
            var artifactDirectoryPath = Path.Combine(_rootDirectoryPath, baseDirectoryName);
            var suffix = 1;

            while (Directory.Exists(artifactDirectoryPath))
            {
                artifactDirectoryPath = Path.Combine(_rootDirectoryPath, $"{baseDirectoryName}_{suffix:D2}");
                suffix++;
            }

            return artifactDirectoryPath;
        }

        private static string FormatSeedForPath(int seed)
        {
            var unsignedMagnitude = seed < 0 ? -(long)seed : seed;
            return seed < 0 ? $"neg_{unsignedMagnitude:D8}" : seed.ToString("D8");
        }

        private static string BuildSummary(ReplayDivergenceArtifact artifact)
        {
            var builder = new StringBuilder(256);
            builder.Append("seed=").Append(artifact.Seed).Append('\n');
            builder.Append("first_divergent_tick=").Append(artifact.FirstDivergentTick).Append('\n');
            builder.Append("reason=").Append(artifact.Reason).Append('\n');
            builder.Append("first_hash=").Append(artifact.FirstHash).Append('\n');
            builder.Append("second_hash=").Append(artifact.SecondHash).Append('\n');
            return builder.ToString();
        }

        private static string BuildHashDump(System.Collections.Generic.IReadOnlyList<TickReplayFrame> frames)
        {
            if (frames.Count == 0)
            {
                return "<empty>\n";
            }

            var builder = new StringBuilder(frames.Count * 32);

            for (var i = 0; i < frames.Count; i++)
            {
                builder
                    .Append("Tick ")
                    .Append(frames[i].TickIndex.ToString("D5"))
                    .Append(" | Hash ")
                    .Append(frames[i].DeterminismHash)
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static string BuildTraceDump(ReplayDivergenceArtifact artifact, bool firstRun)
        {
            if (firstRun)
            {
                return artifact.TryGetFirstRunDivergentFrame(out var frame)
                    ? EnsureTrailingNewline(frame.Trace)
                    : "<missing>\n";
            }

            return artifact.TryGetSecondRunDivergentFrame(out var secondFrame)
                ? EnsureTrailingNewline(secondFrame.Trace)
                : "<missing>\n";
        }

        private static string BuildEventLogDump(ReplayDivergenceArtifact artifact, bool firstRun)
        {
            if (firstRun)
            {
                return artifact.TryGetFirstRunDivergentFrame(out var frame)
                    ? EnsureTextDump(frame.EventLogDump)
                    : "<missing>\n";
            }

            return artifact.TryGetSecondRunDivergentFrame(out var secondFrame)
                ? EnsureTextDump(secondFrame.EventLogDump)
                : "<missing>\n";
        }

        private static string BuildFinalEntitiesDump(ReplayDivergenceArtifact artifact, bool firstRun)
        {
            if (firstRun)
            {
                return artifact.TryGetFirstRunDivergentFrame(out var frame)
                    ? EnsureTextDump(frame.FinalEntitiesDump)
                    : "<missing>\n";
            }

            return artifact.TryGetSecondRunDivergentFrame(out var secondFrame)
                ? EnsureTextDump(secondFrame.FinalEntitiesDump)
                : "<missing>\n";
        }

        private static string BuildOccupancyDump(ReplayDivergenceArtifact artifact, bool firstRun)
        {
            if (firstRun)
            {
                return artifact.TryGetFirstRunDivergentFrame(out var frame)
                    ? EnsureTextDump(frame.OccupancyDump)
                    : "<missing>\n";
            }

            return artifact.TryGetSecondRunDivergentFrame(out var secondFrame)
                ? EnsureTextDump(secondFrame.OccupancyDump)
                : "<missing>\n";
        }

        private static string BuildMarkedForDeathDump(ReplayDivergenceArtifact artifact, bool firstRun)
        {
            if (firstRun)
            {
                return artifact.TryGetFirstRunDivergentFrame(out var frame)
                    ? EnsureTextDump(frame.MarkedForDeathDump)
                    : "<missing>\n";
            }

            return artifact.TryGetSecondRunDivergentFrame(out var secondFrame)
                ? EnsureTextDump(secondFrame.MarkedForDeathDump)
                : "<missing>\n";
        }

        private static string EnsureTextDump(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "<empty>\n";
            }

            return EnsureTrailingNewline(value);
        }

        private static string EnsureTrailingNewline(string value)
        {
            if (value.EndsWith("\n", StringComparison.Ordinal))
            {
                return value;
            }

            return value + '\n';
        }
    }
}
