using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCompatUsageReportingTests
    {
        [Test]
        public void SummarizeEnabledBuildSceneModes_ReportsNoCompatUsageForProductionScenes()
        {
            var summary = new StageSceneBootstrapValidator().SummarizeEnabledBuildSceneModes();

            Assert.That(summary.CatalogResolvedStageIdCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(summary.SerializedStageContentEntryCount, Is.Zero);
            Assert.That(summary.LegacyStageDefinitionCount, Is.Zero);
        }
    }
}
