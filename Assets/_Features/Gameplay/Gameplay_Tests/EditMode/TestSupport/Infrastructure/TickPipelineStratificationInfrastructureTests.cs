using System;
using System.Linq;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineStructureTests
    {
        private static readonly string[] RequiredCoreContracts =
        {
            "canonical_path",
            "resolve_finalize_contract",
            "determinism",
            "semantic_metadata",
            "interaction_push",
            "interaction_flip",
            "interaction_jump",
            "damage_affects_movement",
            "movement_affects_attack",
            "spawn_destroy_delayed_ordering",
        };

        [Test]
        [Category("Extended")]
        public void GameplayTestStratification_ManifestAndSourceRemainConsistent()
        {
            var manifest = GameplayTestStratificationLoader.LoadManifest();
            var overrides = GameplayTestStratificationLoader.LoadOverrides();
            var discoveredTests = GameplayTestSourceDiscovery.Discover();

            Assert.That(discoveredTests, Is.Not.Empty);
            Assert.That(manifest.tests.Length, Is.EqualTo(discoveredTests.Length), "Manifest test count must match source discovery.");

            var manifestByName = manifest.tests.ToDictionary(test => test.fullyQualifiedName, StringComparer.Ordinal);
            var discoveredByName = discoveredTests.ToDictionary(test => test.fullyQualifiedName, StringComparer.Ordinal);

            Assert.That(manifestByName.Count, Is.EqualTo(manifest.tests.Length), "Manifest must not contain duplicate test entries.");
            Assert.That(discoveredByName.Count, Is.EqualTo(discoveredTests.Length), "Source discovery must not produce duplicate test entries.");
            Assert.That(overrides.overrides.Select(entry => entry.fullyQualifiedName).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(overrides.overrides.Length), "Override file must not contain duplicate test entries.");

            foreach (var discoveredTest in discoveredTests)
            {
                Assert.That(discoveredTest.primaryCategoryCount, Is.EqualTo(1), $"Test must have exactly one primary category: {discoveredTest.fullyQualifiedName}");
                Assert.That(manifestByName.TryGetValue(discoveredTest.fullyQualifiedName, out var manifestTest), Is.True, $"Manifest is missing {discoveredTest.fullyQualifiedName}");
                Assert.That(manifestTest.category, Is.EqualTo(discoveredTest.category), $"Manifest/source category mismatch for {discoveredTest.fullyQualifiedName}");
                Assert.That(manifestTest.mode, Is.EqualTo(discoveredTest.mode), $"Manifest/source mode mismatch for {discoveredTest.fullyQualifiedName}");
                Assert.That(manifestTest.sourcePath, Is.EqualTo(discoveredTest.sourcePath), $"Manifest/source path mismatch for {discoveredTest.fullyQualifiedName}");
                Assert.That(manifestTest.contracts, Is.Not.Empty, $"Manifest contracts are missing for {discoveredTest.fullyQualifiedName}");
                Assert.That(manifestTest.reason, Is.Not.Empty, $"Manifest reason is missing for {discoveredTest.fullyQualifiedName}");
            }

            foreach (var overrideEntry in overrides.overrides)
            {
                Assert.That(discoveredByName.ContainsKey(overrideEntry.fullyQualifiedName), Is.True, $"Override points at a missing test: {overrideEntry.fullyQualifiedName}");
                Assert.That(manifestByName.ContainsKey(overrideEntry.fullyQualifiedName), Is.True, $"Manifest is missing overridden test: {overrideEntry.fullyQualifiedName}");
            }

            var coreTests = manifest.tests.Where(test => string.Equals(test.category, "Core", StringComparison.Ordinal)).ToArray();
            var totalTests = manifest.tests.Length;
            var coreRatio = totalTests == 0 ? 0d : (double)coreTests.Length / totalTests;

            Assert.That(coreRatio, Is.GreaterThanOrEqualTo(0.10d), "Core ratio must stay above 10%.");
            Assert.That(coreRatio, Is.LessThanOrEqualTo(0.25d), "Core ratio must stay below 25%.");
            Assert.That(coreTests.Any(test => string.Equals(test.mode, "PlayMode", StringComparison.Ordinal)), Is.True, "Core must include at least one PlayMode integration test.");

            var coreContractCounts = manifest.tests
                .Where(test => string.Equals(test.category, "Core", StringComparison.Ordinal))
                .SelectMany(test => test.contracts)
                .GroupBy(contract => contract, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            for (var i = 0; i < RequiredCoreContracts.Length; i++)
            {
                Assert.That(coreContractCounts.TryGetValue(RequiredCoreContracts[i], out var count), Is.True, $"Missing Core contract coverage for {RequiredCoreContracts[i]}");
                Assert.That(count, Is.GreaterThanOrEqualTo(2), $"Core contract {RequiredCoreContracts[i]} must be protected by at least two tests.");
            }
        }
    }
}
