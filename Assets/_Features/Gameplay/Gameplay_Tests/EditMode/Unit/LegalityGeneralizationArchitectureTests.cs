using System.IO;
using Game.Feature.Gameplay.BoardState;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class LegalityGeneralizationArchitectureTests
    {
        private static readonly string[] ModifierQueryForbiddenTokens =
        {
            "WorldSnapshot",
            "EnumerateUnitsAt(",
            "TryGetPlacementBlocker(",
            "TryGetAuthoritativePlacementBlocker(",
            "TryPickImpactTargetAt(",
            "TryPickHostileUnitImpactTargetAt(",
            "TryGetSolidSemanticAt(",
            "Prefab",
        };

        private static readonly string[] BlockerCentralizationForbiddenTokens =
        {
            "switch (blocker.Kind)",
            "LegalityBlockerKind.BoardEdge =>",
            "LegalityBlockerKind.Terrain =>",
            "LegalityBlockerKind.Unit =>",
            "LegalityBlockerKind.Solid =>",
            "LegalityBlockerKind.Reservation =>",
            "LegalityBlockerKind.TileFeature =>",
            "new LegalityBlocker(",
        };

        [Test]
        [Category("Extended")]
        public void ModifierQuery_DoesNotBecomeSnapshotEnumerationOrContentRuleStore()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/ModifierQuery.cs");

            for (var i = 0; i < ModifierQueryForbiddenTokens.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(ModifierQueryForbiddenTokens[i]), $"Forbidden ModifierQuery token found: {ModifierQueryForbiddenTokens[i]}");
            }
        }

        [Test]
        [Category("Extended")]
        public void BlockerFormatting_And_Materialization_AreCentralized()
        {
            AssertContainsNoForbiddenTokens(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/RuntimeTraversalLegalityPolicy.cs"),
                BlockerCentralizationForbiddenTokens);
            AssertContainsNoForbiddenTokens(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/RuntimeSettlementLegalityPolicy.cs"),
                BlockerCentralizationForbiddenTokens);
            AssertContainsNoForbiddenTokens(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Expansion/AttackExpander.cs"),
                BlockerCentralizationForbiddenTokens);
            AssertContainsNoForbiddenTokens(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
                BlockerCentralizationForbiddenTokens);
        }

        [Test]
        [Category("Extended")]
        public void LegalityBlockerKind_IncludesTileFeatureVocabulary()
        {
            Assert.That(LegalityBlockerKind.TileFeature, Is.EqualTo((LegalityBlockerKind)5));
        }

        private static void AssertContainsNoForbiddenTokens(string source, string[] forbiddenTokens)
        {
            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), $"Forbidden blocker-centralization token found: {forbiddenTokens[i]}");
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
