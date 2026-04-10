using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Tests.Replay;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    internal sealed class DeterminismReplayComparer
    {
        private readonly TickReplayHarness _replayHarness = new();

        public DeterminismReplayComparison Compare(FuzzScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            var firstRunFrames = _replayHarness.Run(
                scenario.CreateWorldState(),
                scenario.CreateEntityLogics(),
                scenario.TickInputs);
            var secondRunFrames = _replayHarness.Run(
                scenario.CreateWorldState(),
                scenario.CreateEntityLogics(),
                scenario.TickInputs);

            var comparison = BuildComparison(scenario, firstRunFrames, secondRunFrames);
            return comparison;
        }

        private static DeterminismReplayComparison BuildComparison(
            FuzzScenarioDefinition scenario,
            IReadOnlyList<TickReplayFrame> firstRunFrames,
            IReadOnlyList<TickReplayFrame> secondRunFrames)
        {
            var comparedFrameCount = Math.Min(firstRunFrames.Count, secondRunFrames.Count);

            for (var i = 0; i < comparedFrameCount; i++)
            {
                var firstFrame = firstRunFrames[i];
                var secondFrame = secondRunFrames[i];

                if (firstFrame.TickIndex != secondFrame.TickIndex)
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        scenario.TickInputs[i].TickIndex,
                        "TickIndexMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.DeterminismHash, secondFrame.DeterminismHash, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "DeterminismHashMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.Trace, secondFrame.Trace, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "TraceTextMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.FinalEntitiesDump, secondFrame.FinalEntitiesDump, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "FinalEntitiesDumpMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.PlayerControlDump, secondFrame.PlayerControlDump, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "PlayerControlDumpMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.PlayerDamageDump, secondFrame.PlayerDamageDump, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "PlayerDamageDumpMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.OccupancyDump, secondFrame.OccupancyDump, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "OccupancyDumpMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.MarkedForDeathDump, secondFrame.MarkedForDeathDump, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "MarkedForDeathDumpMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }

                if (!string.Equals(firstFrame.EventLogDump, secondFrame.EventLogDump, StringComparison.Ordinal))
                {
                    return CreateMismatch(
                        scenario,
                        firstRunFrames,
                        secondRunFrames,
                        i,
                        firstFrame.TickIndex,
                        "EventLogDumpMismatch",
                        firstFrame.DeterminismHash,
                        secondFrame.DeterminismHash);
                }
            }

            if (firstRunFrames.Count != secondRunFrames.Count)
            {
                var divergenceIndex = comparedFrameCount;
                var divergentTick = divergenceIndex < scenario.TickInputs.Count
                    ? scenario.TickInputs[divergenceIndex].TickIndex
                    : 0;

                return CreateMismatch(
                    scenario,
                    firstRunFrames,
                    secondRunFrames,
                    divergenceIndex,
                    divergentTick,
                    "FrameCountMismatch",
                    divergenceIndex < firstRunFrames.Count ? firstRunFrames[divergenceIndex].DeterminismHash : "<missing>",
                    divergenceIndex < secondRunFrames.Count ? secondRunFrames[divergenceIndex].DeterminismHash : "<missing>");
            }

            return new DeterminismReplayComparison(firstRunFrames, secondRunFrames);
        }

        private static DeterminismReplayComparison CreateMismatch(
            FuzzScenarioDefinition scenario,
            IReadOnlyList<TickReplayFrame> firstRunFrames,
            IReadOnlyList<TickReplayFrame> secondRunFrames,
            int divergentFrameIndex,
            int divergentTick,
            string reason,
            string firstHash,
            string secondHash)
        {
            return new DeterminismReplayComparison(
                firstRunFrames,
                secondRunFrames,
                new ReplayDivergenceArtifact(
                    scenario.Seed,
                    scenario.CanonicalScenarioDump,
                    firstRunFrames,
                    secondRunFrames,
                    divergentFrameIndex,
                    divergentTick,
                    reason,
                    firstHash,
                    secondHash));
        }
    }

    internal sealed class DeterminismReplayComparison
    {
        private readonly ReadOnlyCollection<TickReplayFrame> _firstRunFrames;
        private readonly ReadOnlyCollection<TickReplayFrame> _secondRunFrames;

        public DeterminismReplayComparison(
            IReadOnlyList<TickReplayFrame> firstRunFrames,
            IReadOnlyList<TickReplayFrame> secondRunFrames,
            ReplayDivergenceArtifact divergenceArtifact = null)
        {
            if (firstRunFrames == null)
            {
                throw new ArgumentNullException(nameof(firstRunFrames));
            }

            if (secondRunFrames == null)
            {
                throw new ArgumentNullException(nameof(secondRunFrames));
            }

            _firstRunFrames = new ReadOnlyCollection<TickReplayFrame>(new List<TickReplayFrame>(firstRunFrames));
            _secondRunFrames = new ReadOnlyCollection<TickReplayFrame>(new List<TickReplayFrame>(secondRunFrames));
            DivergenceArtifact = divergenceArtifact;
        }

        public IReadOnlyList<TickReplayFrame> FirstRunFrames => _firstRunFrames;

        public IReadOnlyList<TickReplayFrame> SecondRunFrames => _secondRunFrames;

        public ReplayDivergenceArtifact DivergenceArtifact { get; }

        public bool IsMatch => DivergenceArtifact == null;
    }
}
