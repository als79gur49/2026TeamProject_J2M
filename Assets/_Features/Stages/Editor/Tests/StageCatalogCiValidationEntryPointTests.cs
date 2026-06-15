using System;
using System.IO;
using System.Linq;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogCiValidationEntryPointTests
    {
        private const string NonCompanionAssetCode = "campaign-content.stage-folder.non-companion-asset";

        [Test]
        public void Run_WritesGovernanceAndAliasUsageValidationSections()
        {
            var result = StageCatalogCiValidationEntryPoint.Run();
            var reportPath = StageCatalogCiValidationEntryPoint.ReportPath;
            var auditReportPath = Path.Combine(
                Path.GetDirectoryName(reportPath) ?? string.Empty,
                "stage-content-inventory-retired-residue-audit.md");

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(reportPath), Is.True);
            Assert.That(File.Exists(auditReportPath), Is.True);

            var reportText = File.ReadAllText(reportPath);
            var auditReportText = File.ReadAllText(auditReportPath);
            Assert.That(reportText, Does.Contain("## Authoring Sync Issues"));
            Assert.That(reportText, Does.Contain("## Presentation Catalog Issues"));
            Assert.That(reportText, Does.Contain("## Authoring Surface Classification"));
            Assert.That(reportText, Does.Contain("StageContentEntry: Stage Root"));
            Assert.That(reportText, Does.Contain("StageDefinition: Gameplay Companion"));
            Assert.That(reportText, Does.Contain("StagePresentationDefinition: Presentation Companion"));
            Assert.That(reportText, Does.Contain("StageAudioDefinition: Audio Companion"));
            Assert.That(reportText, Does.Contain("Reward / Progression / ClearEvaluation: Retired Companion Guard"));
            Assert.That(reportText, Does.Contain("RetiredStageLoadPathGuard: Retired Load Guard"));
            Assert.That(reportText, Does.Contain("defaultStageId residue: Retired Load Detector"));
            Assert.That(reportText, Does.Contain("direct stageDefinition residue: Retired Load Detector"));
            Assert.That(reportText, Does.Contain("serialized StageContentEntry residue: Retired Load Detector"));
            Assert.That(reportText, Does.Contain("compat mode residue: Retired Load Detector"));
            Assert.That(reportText, Does.Contain("StageEditorDirectPlayCatalog / StageEditorDirectPlayLauncher / StageEditorDirectPlayWindow: Editor Direct-Play Support"));
            Assert.That(reportText, Does.Contain("PresentationId: Presentation-Only Binding"));
            Assert.That(reportText, Does.Contain("UI / Audio / Topology helper references: Weak Helper / Reference"));
            Assert.That(reportText, Does.Contain("## Scene Bootstrap Guard Summary"));
            Assert.That(reportText, Does.Contain("AuditReport: stage-content-inventory-retired-residue-audit.md"));
            Assert.That(reportText, Does.Contain("LaunchContextCatalogResolvedInstallers"));
            Assert.That(reportText, Does.Contain("RetiredSerializedStageContentEntryResidue"));
            Assert.That(reportText, Does.Contain("RetiredLegacyStageDefinitionResidue"));
            Assert.That(reportText, Does.Contain("RemovedDefaultStageIdFallbackResidue"));
            Assert.That(reportText, Does.Contain("RemovedDirectStageDefinitionLoadResidue"));
            Assert.That(reportText, Does.Contain("EditorDirectPlayMappingSupport: Editor Direct-Play Support"));
            Assert.That(reportText, Does.Contain("## Full EditMode Known Failure Baseline"));
            Assert.That(reportText, Does.Contain("## Known Warning Governance Issues"));
            Assert.That(reportText, Does.Contain("## Alias Governance Issues"));
            Assert.That(reportText, Does.Contain("## Alias Usage Issues"));
            Assert.That(auditReportText, Does.Contain("# Stage Content Inventory / Retired Residue Audit"));
            Assert.That(auditReportText, Does.Contain("StageContentEntryClassification: Stage Root"));
            Assert.That(auditReportText, Does.Contain("StageDefinitionClassification: Gameplay Companion"));
            Assert.That(auditReportText, Does.Contain("StagePresentationDefinitionClassification: Presentation Companion"));
            Assert.That(auditReportText, Does.Contain("StageAudioDefinitionClassification: Audio Companion"));
            Assert.That(auditReportText, Does.Contain("RetiredCompanionClassification: Retired Companion Guard"));
            Assert.That(auditReportText, Does.Contain("RetiredLoadResidueClassification: Retired Load Detector"));
            Assert.That(auditReportText, Does.Contain("DirectPlayClassification: Editor Direct-Play Support"));
            Assert.That(auditReportText, Does.Contain("CanonicalGameplayCompanionCount:"));
            Assert.That(auditReportText, Does.Contain("DuplicateLegacyGameplayCompanionAssetCount:"));
            Assert.That(auditReportText, Does.Contain("## Duplicate Legacy Gameplay Companion Assets"));
            Assert.That(reportText, Does.Not.Contain("## Scene Mode Summary"));
            Assert.That(reportText, Does.Not.Contain("CatalogResolvedStageId:"));
            Assert.That(reportText, Does.Not.Contain("SerializedStageContentEntry:"));
            Assert.That(reportText, Does.Not.Contain("LegacyStageDefinition:"));
            Assert.That(reportText, Does.Not.Contain("DefaultStageId fallback"));
            Assert.That(reportText, Does.Not.Contain("Direct StageDefinition option"));
            Assert.That(reportText, Does.Not.Contain("FallbackStage option"));
            Assert.That(reportText, Does.Not.Contain("active missing companion"));
            Assert.That(reportText, Does.Not.Contain("production fallback"));
            Assert.That(reportText, Does.Not.Contain("stage-compat-audit.md"));
            Assert.That(auditReportText, Does.Not.Contain("Stage Compat Audit"));
            Assert.That(auditReportText, Does.Not.Contain("CanonicalGameplayAssetCount"));
        }

        [Test]
        public void StageContentInventory_ExposesDisplayClassificationLabels()
        {
            var gameplayItem = new StageGameplayCompanionInventoryItem(
                "guid",
                "Assets/StageDefinition.asset",
                isCanonicalCatalogGameplayCompanion: true,
                isDuplicateLegacyGameplayCompanionAsset: false);
            var buildSceneItem = new StageBuildSceneInventoryItem(
                "Assets/Scenes/UIAudioScene.unity",
                installerCount: 1,
                hasCompatModeResidue: false,
                hasDirectStageDefinitionResidue: false,
                hasSerializedEntryResidue: false,
                hasEnemyCatalogResidue: false,
                hasStaticCatalogResidue: false,
                hasDefaultStageIdResidue: false,
                hasDirectPlayCatalogCoverage: true);
            var snapshot = new StageContentInventorySnapshot(
                StageContentPaths.StageCatalogAssetPath,
                new[] { "guid" },
                new[] { gameplayItem },
                new[] { buildSceneItem },
                Array.Empty<StageIdAliasEntry>());

            Assert.That(snapshot.StageContentRootClassificationLabel, Is.EqualTo("Stage Root"));
            Assert.That(gameplayItem.ClassificationLabel, Is.EqualTo("Gameplay Companion"));
            Assert.That(buildSceneItem.ResidueClassificationLabel, Is.EqualTo("Retired Load Detector"));
            Assert.That(buildSceneItem.DirectPlayCoverageClassificationLabel, Is.EqualTo("Editor Direct-Play Support"));
        }

        [Test]
        public void CampaignGovernance_AllowsProductionStageAudioDefinitions()
        {
            var audioPaths = AssetDatabase.FindAssets(
                    "t:StageAudioDefinition",
                    new[] { StageContentPaths.CampaignLevel01StagesRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsDirectCampaignStageFolderChild)
                .ToArray();

            Assert.That(audioPaths, Is.Not.Empty);

            var report = ValidateCampaignGovernance();
            var violations = report.Issues
                .Where(issue => issue.Code == NonCompanionAssetCode && audioPaths.Contains(issue.AssetPath))
                .ToArray();

            Assert.That(violations, Is.Empty, FormatIssues(violations));
        }

        [Test]
        public void CampaignGovernance_AllowsStageAudioDefinitionAsStageFolderCompanion()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var audio = ScriptableObject.CreateInstance<StageAudioDefinition>();
            fixture.CreateAsset(audio, $"{fixture.StageIdValue}_Audio.asset");

            var report = ValidateCampaignGovernance();

            Assert.That(
                HasViolationForPath(report, fixture.AssetPath),
                Is.False,
                FormatIssues(report.Issues.Where(issue => issue.AssetPath == fixture.AssetPath)));
        }

        [Test]
        public void StageContentEntryCreation_GuardsRetiredRewardProgressionClearEvaluationAssetsRemainAbsent()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            fixture.CreateAsset(gameplay, $"{fixture.StageIdValue}.asset");

            var entry = StageContentEntryCreationTool.CreateForStageDefinition(
                gameplay,
                StageId.CreateOrThrow(fixture.StageIdValue));

            Assert.That(entry, Is.Not.Null);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Entry.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Authoring.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Presentation.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Audio.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_ClearEvaluation.asset")), Is.False);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Reward.asset")), Is.False);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Progression.asset")), Is.False);
        }

        [Test]
        public void CampaignGovernance_RejectsSharedAudioDefinitionEvenWhenNamedLikeAudioCompanion()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var audio = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            fixture.CreateAsset(audio, $"{fixture.StageIdValue}_Audio.asset");

            var report = ValidateCampaignGovernance();

            Assert.That(HasViolationForPath(report, fixture.AssetPath), Is.True, FormatIssues(report.Issues));
        }

        [Test]
        public void CampaignGovernance_RejectsNonCompanionScriptableObjectInStageFolder()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            fixture.CreateAsset(catalog, $"{fixture.StageIdValue}_Catalog.asset");

            var report = ValidateCampaignGovernance();

            Assert.That(HasViolationForPath(report, fixture.AssetPath), Is.True, FormatIssues(report.Issues));
        }

        private static StageValidationReport ValidateCampaignGovernance()
        {
            AssetDatabase.Refresh();
            return new StageCampaignContentGovernanceValidator().Validate(StageValidationTiming.TestOrCi);
        }

        private static bool HasViolationForPath(StageValidationReport report, string assetPath)
        {
            return report.Issues.Any(issue =>
                issue.Code == NonCompanionAssetCode &&
                string.Equals(issue.AssetPath, assetPath, StringComparison.Ordinal));
        }

        private static bool IsDirectCampaignStageFolderChild(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent))
            {
                return false;
            }

            var stageRoot = StageContentPaths.CampaignLevel01StagesRoot;
            return parent.StartsWith(stageRoot + "/", StringComparison.Ordinal) &&
                   string.Equals(
                       Path.GetDirectoryName(parent)?.Replace('\\', '/'),
                       stageRoot,
                       StringComparison.Ordinal);
        }

        private static string FormatIssues(System.Collections.Generic.IEnumerable<StageValidationIssue> issues)
        {
            return string.Join(
                Environment.NewLine,
                issues.Select(issue => $"{issue.Code}: {issue.Message} ({issue.AssetPath})"));
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory(), assetPath);
        }

        private sealed class TempCampaignStageAssetFixture : IDisposable
        {
            private TempCampaignStageAssetFixture(string stageFolder, string stageIdValue)
            {
                StageFolder = stageFolder;
                StageIdValue = stageIdValue;
            }

            public string StageFolder { get; }

            public string StageIdValue { get; }

            public string AssetPath { get; private set; } = string.Empty;

            public static TempCampaignStageAssetFixture Create()
            {
                var stageIdValue = $"codex-governance-audio-test-{Guid.NewGuid():N}";
                var stageFolder = $"{StageContentPaths.CampaignLevel01StagesRoot}/{stageIdValue}";
                Assert.That(AssetDatabase.IsValidFolder(stageFolder), Is.False);
                AssetDatabase.CreateFolder(StageContentPaths.CampaignLevel01StagesRoot, stageIdValue);
                return new TempCampaignStageAssetFixture(stageFolder, stageIdValue);
            }

            public void CreateAsset(UnityEngine.Object asset, string fileName)
            {
                AssetPath = $"{StageFolder}/{fileName}";
                asset.name = Path.GetFileNameWithoutExtension(fileName);
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            public void Dispose()
            {
                if (!string.IsNullOrWhiteSpace(StageFolder))
                {
                    AssetDatabase.DeleteAsset(StageFolder);
                    AssetDatabase.Refresh();
                }
            }
        }
    }
}
