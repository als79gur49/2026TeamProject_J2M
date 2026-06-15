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

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(reportPath), Is.True);

            var reportText = File.ReadAllText(reportPath);
            Assert.That(reportText, Does.Contain("## Authoring Sync Issues"));
            Assert.That(reportText, Does.Contain("## Presentation Catalog Issues"));
            Assert.That(reportText, Does.Contain("## Full EditMode Known Failure Baseline"));
            Assert.That(reportText, Does.Contain("## Known Warning Governance Issues"));
            Assert.That(reportText, Does.Contain("## Alias Governance Issues"));
            Assert.That(reportText, Does.Contain("## Alias Usage Issues"));
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
