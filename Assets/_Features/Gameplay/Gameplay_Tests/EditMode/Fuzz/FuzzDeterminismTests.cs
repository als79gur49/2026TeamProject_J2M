using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Tests.Replay;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    public sealed class FuzzDeterminismTests
    {
        private static readonly int[] SmokeSeeds =
        {
            1,
            7,
            23,
            42,
            97,
            451,
            2048,
            20260323,
        };

        [Test]
        public void Fuzz_SameSeedScenarioGeneration_ProducesSameCanonicalScenarioDump()
        {
            const int seed = 20260323;
            var generator = new FuzzScenarioGenerator();

            var firstScenario = generator.Generate(seed);
            var secondScenario = generator.Generate(seed);

            Assert.That(firstScenario.CanonicalScenarioDump, Is.EqualTo(secondScenario.CanonicalScenarioDump));
        }

        [Test]
        public void Fuzz_CuratedSeeds_ProduceIdenticalPerTickHashesTwice()
        {
            var generator = new FuzzScenarioGenerator();
            var comparer = new DeterminismReplayComparer();

            for (var i = 0; i < SmokeSeeds.Length; i++)
            {
                var scenario = generator.Generate(SmokeSeeds[i]);
                var comparison = comparer.Compare(scenario);

                AssertHashesMatchOrWriteArtifact(scenario, comparison);
            }
        }

        [Test]
        public void Fuzz_CuratedSeeds_ProduceIdenticalPerTickTraceAndDumpsTwice()
        {
            var generator = new FuzzScenarioGenerator();
            var comparer = new DeterminismReplayComparer();

            for (var i = 0; i < SmokeSeeds.Length; i++)
            {
                var scenario = generator.Generate(SmokeSeeds[i]);
                var comparison = comparer.Compare(scenario);

                AssertFullMatchOrWriteArtifact(scenario, comparison);
                CollectionAssert.AreEqual(
                    comparison.FirstRunFrames.Select(frame => frame.Trace).ToArray(),
                    comparison.SecondRunFrames.Select(frame => frame.Trace).ToArray());
                CollectionAssert.AreEqual(
                    comparison.FirstRunFrames.Select(frame => frame.FinalEntitiesDump).ToArray(),
                    comparison.SecondRunFrames.Select(frame => frame.FinalEntitiesDump).ToArray());
                CollectionAssert.AreEqual(
                    comparison.FirstRunFrames.Select(frame => frame.OccupancyDump).ToArray(),
                    comparison.SecondRunFrames.Select(frame => frame.OccupancyDump).ToArray());
                CollectionAssert.AreEqual(
                    comparison.FirstRunFrames.Select(frame => frame.MarkedForDeathDump).ToArray(),
                    comparison.SecondRunFrames.Select(frame => frame.MarkedForDeathDump).ToArray());
                CollectionAssert.AreEqual(
                    comparison.FirstRunFrames.Select(frame => frame.EventLogDump).ToArray(),
                    comparison.SecondRunFrames.Select(frame => frame.EventLogDump).ToArray());
            }
        }

        [Test]
        public void ReplayArtifactWriter_WritesFirstDivergentTickArtifacts()
        {
            const int seed = 90901;
            var artifact = new ReplayDivergenceArtifact(
                seed,
                "Seed=90901\nInitialEntities\n  E=10|Pos=(0,0)\n",
                new[]
                {
                    new TickReplayFrame(1, "AAAAAAAAAAAAAAAA", "Trace-A-1", "E=10|Pos=(0,0)", "Unit|Cell=(0,0)|E=10", "<empty>", "Event-A-1"),
                    new TickReplayFrame(2, "BBBBBBBBBBBBBBBB", "Trace-A-2", "E=10|Pos=(1,0)", "Unit|Cell=(1,0)|E=10", "10", "Event-A-2"),
                },
                new[]
                {
                    new TickReplayFrame(1, "AAAAAAAAAAAAAAAA", "Trace-A-1", "E=10|Pos=(0,0)", "Unit|Cell=(0,0)|E=10", "<empty>", "Event-A-1"),
                    new TickReplayFrame(2, "CCCCCCCCCCCCCCCC", "Trace-B-2", "E=10|Pos=(2,0)", "Unit|Cell=(2,0)|E=10", "<empty>", "Event-B-2"),
                },
                firstDivergentFrameIndex: 1,
                firstDivergentTick: 2,
                reason: "DeterminismHashMismatch",
                firstHash: "BBBBBBBBBBBBBBBB",
                secondHash: "CCCCCCCCCCCCCCCC");

            var writer = new ReplayArtifactWriter(Path.Combine(Path.GetTempPath(), "GameplayFuzzArtifactWriterTests"));
            var artifactDirectoryPath = writer.Write(artifact);

            Assert.That(artifactDirectoryPath, Does.Contain("GameplayFuzzArtifactWriterTests"));
            AssertArtifactFileExists(artifactDirectoryPath, "summary.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "scenario.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "first_run_hashes.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "second_run_hashes.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "first_run_trace.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "second_run_trace.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "first_run_event_log.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "second_run_event_log.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "first_run_final_entities.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "second_run_final_entities.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "first_run_occupancy.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "second_run_occupancy.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "first_run_marked_for_death.txt");
            AssertArtifactFileExists(artifactDirectoryPath, "second_run_marked_for_death.txt");

            StringAssert.Contains("seed=90901", File.ReadAllText(Path.Combine(artifactDirectoryPath, "summary.txt")));
            StringAssert.Contains("first_divergent_tick=2", File.ReadAllText(Path.Combine(artifactDirectoryPath, "summary.txt")));
            StringAssert.Contains("reason=DeterminismHashMismatch", File.ReadAllText(Path.Combine(artifactDirectoryPath, "summary.txt")));
            StringAssert.Contains("first_hash=BBBBBBBBBBBBBBBB", File.ReadAllText(Path.Combine(artifactDirectoryPath, "summary.txt")));
            StringAssert.Contains("second_hash=CCCCCCCCCCCCCCCC", File.ReadAllText(Path.Combine(artifactDirectoryPath, "summary.txt")));
            StringAssert.Contains("Seed=90901", File.ReadAllText(Path.Combine(artifactDirectoryPath, "scenario.txt")));
            StringAssert.Contains("Tick 00002 | Hash BBBBBBBBBBBBBBBB", File.ReadAllText(Path.Combine(artifactDirectoryPath, "first_run_hashes.txt")));
            StringAssert.Contains("Tick 00002 | Hash CCCCCCCCCCCCCCCC", File.ReadAllText(Path.Combine(artifactDirectoryPath, "second_run_hashes.txt")));
            StringAssert.Contains("Trace-A-2", File.ReadAllText(Path.Combine(artifactDirectoryPath, "first_run_trace.txt")));
            StringAssert.Contains("Trace-B-2", File.ReadAllText(Path.Combine(artifactDirectoryPath, "second_run_trace.txt")));
            StringAssert.Contains("Event-A-2", File.ReadAllText(Path.Combine(artifactDirectoryPath, "first_run_event_log.txt")));
            StringAssert.Contains("Event-B-2", File.ReadAllText(Path.Combine(artifactDirectoryPath, "second_run_event_log.txt")));
            StringAssert.Contains("E=10|Pos=(1,0)", File.ReadAllText(Path.Combine(artifactDirectoryPath, "first_run_final_entities.txt")));
            StringAssert.Contains("E=10|Pos=(2,0)", File.ReadAllText(Path.Combine(artifactDirectoryPath, "second_run_final_entities.txt")));
            StringAssert.Contains("Unit|Cell=(1,0)|E=10", File.ReadAllText(Path.Combine(artifactDirectoryPath, "first_run_occupancy.txt")));
            StringAssert.Contains("Unit|Cell=(2,0)|E=10", File.ReadAllText(Path.Combine(artifactDirectoryPath, "second_run_occupancy.txt")));
            StringAssert.Contains("10", File.ReadAllText(Path.Combine(artifactDirectoryPath, "first_run_marked_for_death.txt")));
            StringAssert.Contains("<empty>", File.ReadAllText(Path.Combine(artifactDirectoryPath, "second_run_marked_for_death.txt")));
        }

        [Test]
        [Explicit("Long-running deterministic replay fuzz sweep.")]
        [Category("LongRunning")]
        public void Fuzz_LongRunningCuratedSeeds_ProduceIdenticalPerTickArtifactsTwice()
        {
            var generator = new FuzzScenarioGenerator();
            var comparer = new DeterminismReplayComparer();

            foreach (var seed in BuildLongRunningSeeds())
            {
                var scenario = generator.Generate(seed);
                var comparison = comparer.Compare(scenario);
                AssertFullMatchOrWriteArtifact(scenario, comparison);
            }
        }

        private static void AssertHashesMatchOrWriteArtifact(
            FuzzScenarioDefinition scenario,
            DeterminismReplayComparison comparison)
        {
            var firstRunHashes = comparison.FirstRunFrames.Select(frame => frame.DeterminismHash).ToArray();
            var secondRunHashes = comparison.SecondRunFrames.Select(frame => frame.DeterminismHash).ToArray();

            if (firstRunHashes.SequenceEqual(secondRunHashes))
            {
                return;
            }

            var artifactDirectoryPath = new ReplayArtifactWriter().Write(comparison.DivergenceArtifact);
            Assert.Fail(
                $"Seed {scenario.Seed} produced different per-tick hashes at tick {comparison.DivergenceArtifact.FirstDivergentTick}. Artifact: {artifactDirectoryPath}");
        }

        private static void AssertFullMatchOrWriteArtifact(
            FuzzScenarioDefinition scenario,
            DeterminismReplayComparison comparison)
        {
            if (comparison.IsMatch)
            {
                return;
            }

            var artifactDirectoryPath = new ReplayArtifactWriter().Write(comparison.DivergenceArtifact);
            Assert.Fail(
                $"Seed {scenario.Seed} diverged at tick {comparison.DivergenceArtifact.FirstDivergentTick} ({comparison.DivergenceArtifact.Reason}). Artifact: {artifactDirectoryPath}");
        }

        private static IEnumerable<int> BuildLongRunningSeeds()
        {
            for (var i = 0; i < 64; i++)
            {
                yield return 1000 + (i * 37);
            }
        }

        private static void AssertArtifactFileExists(string artifactDirectoryPath, string fileName)
        {
            Assert.That(File.Exists(Path.Combine(artifactDirectoryPath, fileName)), Is.True, fileName);
        }
    }
}
