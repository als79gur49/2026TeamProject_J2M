using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogMigrationConfidenceTests
    {
        [Test]
        public void Analyze_ReportsCanonicalPrimaryAndDuplicateDisposition_ForCurrentStageAssets()
        {
            var report = StageCatalogMigrationTool.Analyze();

            var combinedPrimary = report.items.Single(item =>
                item.sourceAssetPath == "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset");
            Assert.That(combinedPrimary.chosenStageId, Is.EqualTo("combined-gameplay-showcase"));
            Assert.That(combinedPrimary.disposition, Is.EqualTo(StageCatalogMigrationDisposition.MigrateAndAlias.ToString()));
            Assert.That(combinedPrimary.confidence, Is.EqualTo(StageCatalogMigrationConfidence.Medium.ToString()));

            var staleDuplicate = report.items.Single(item =>
                item.sourceAssetPath == "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase 1.asset");
            Assert.That(staleDuplicate.disposition, Is.EqualTo(StageCatalogMigrationDisposition.Skip.ToString()));
            Assert.That(staleDuplicate.conflicts, Does.Contain("stale-duplicate"));

            var tutorialPrimary = report.items.Single(item =>
                item.sourceAssetPath == "Assets/_Features/Stages/Stage_TutorialScene/Stage_TutorialSecne.asset");
            Assert.That(tutorialPrimary.chosenStageId, Is.EqualTo("tutorial-scene"));
            Assert.That(tutorialPrimary.aliasPlan, Does.Contain("Stage_TutorialSecne"));
        }
    }
}
