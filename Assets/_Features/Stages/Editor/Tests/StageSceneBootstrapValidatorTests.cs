using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
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

        [Test]
        public void EnabledMainMenuAndGameplayScenes_ReferenceExactAuthoritativeSequenceAsset()
        {
            var expectedSequence = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                CampaignStageSequenceAssetLoader.CanonicalAssetPath);
            var report = new StageSceneBootstrapValidator().ValidateEnabledBuildScenes(
                CreateProductionOptions(),
                expectedSequence);

            Assert.That(expectedSequence, Is.Not.Null);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.campaign-sequence.null"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.campaign-sequence.non-authoritative"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.campaign-sequence.field-missing"), Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "scene.campaign-sequence.required-build-scene-disabled"), Is.False);
        }

        [Test]
        public void SceneSequenceReference_RejectsNullAndDifferentSequenceAssets()
        {
            var expectedSequence = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                CampaignStageSequenceAssetLoader.CanonicalAssetPath);
            var otherSequence = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            try
            {
                var nullReport = new StageValidationReport();
                StageSceneBootstrapValidator.ValidateCampaignSequenceReference(
                    StageSceneBootstrapValidator.MainMenuScenePath,
                    expectedSequence,
                    null,
                    expectedSequence,
                    CreateProductionOptions(),
                    nullReport);

                var differentReport = new StageValidationReport();
                StageSceneBootstrapValidator.ValidateCampaignSequenceReference(
                    StageSceneBootstrapValidator.GameplayUiAudioScenePath,
                    expectedSequence,
                    otherSequence,
                    expectedSequence,
                    CreateProductionOptions(),
                    differentReport);

                Assert.That(
                    nullReport.Issues.Any(issue => issue.Code == "scene.campaign-sequence.null"),
                    Is.True);
                Assert.That(nullReport.HasErrors, Is.True);
                Assert.That(
                    differentReport.Issues.Any(issue => issue.Code == "scene.campaign-sequence.non-authoritative"),
                    Is.True);
                Assert.That(differentReport.HasErrors, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(otherSequence);
            }
        }

        private static StageCatalogValidationOptions CreateProductionOptions()
        {
            return new StageCatalogValidationOptions
            {
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase4_ProductionBootstrapConversion,
            };
        }
    }
}
