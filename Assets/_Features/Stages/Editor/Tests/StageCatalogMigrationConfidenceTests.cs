using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogMigrationConfidenceTests
    {
        [Test]
        public void Analyze_ReportsCanonicalPrimaryDisposition_ForCurrentStageAssets()
        {
            var report = StageCatalogMigrationTool.Analyze();

            var combinedPrimary = report.items.Single(item =>
                item.sourceAssetPath == "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase.asset");
            Assert.That(combinedPrimary.chosenStageId, Is.EqualTo("combined-gameplay-showcase"));
            Assert.That(combinedPrimary.disposition, Is.EqualTo(StageCatalogMigrationDisposition.Migrate.ToString()));

            var tutorialPrimary = report.items.Single(item =>
                item.sourceAssetPath == "Assets/_Features/Stages/Content/tutorial-scene/tutorial-scene.asset");
            Assert.That(tutorialPrimary.chosenStageId, Is.EqualTo("tutorial-scene"));
            Assert.That(tutorialPrimary.disposition, Is.EqualTo(StageCatalogMigrationDisposition.Migrate.ToString()));

            Assert.That(report.items.Any(item => item.sourceAssetPath.EndsWith(" 1.asset")), Is.False);
            Assert.That(report.items.Any(item => item.sourceAssetPath.EndsWith(" 2.asset")), Is.False);
        }
    }
}
