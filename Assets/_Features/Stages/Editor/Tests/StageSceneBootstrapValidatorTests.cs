using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    [Category("Core")]
    [Category("Phase3BGate")]
    public sealed class StageSceneBootstrapValidatorTests
    {
        private static readonly string[] ProductionScenePaths =
        {
            "Assets/Scenes/UIAudioScene.unity",
        };

        [Test]
        public void ProductionScenes_UseCatalogResolvedBootstrapWithoutCompatResidue()
        {
            var validator = new StageSceneBootstrapValidator();
            var report = validator.ValidateScenes(
                ProductionScenePaths,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                    Phase = StageValidationPhase.Phase4_ProductionBootstrapConversion,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "scene.stage-definition.direct-ref"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.compat-mode.production"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.catalog-provider.null"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.default-stage-id.residue"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.direct-play.catalog.missing"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.enemy-catalog.residue"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.static-catalog.residue"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.camera-topology.inline.production"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.camera-topology.preset.null"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.camera-topology.stage-preset.mismatch"), Is.False);
        }
    }
}
