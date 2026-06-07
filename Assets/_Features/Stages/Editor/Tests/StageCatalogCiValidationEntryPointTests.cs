using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogCiValidationEntryPointTests
    {
        private const string NonCompanionIssueCode = "campaign-content.stage-folder.non-companion-asset";

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
        public void CampaignGovernance_AllowsOwnedStageAudioDefinitionCompanions()
        {
            var report = new StageCampaignContentGovernanceValidator().Validate(StageValidationTiming.TestOrCi);

            AssertStageAudioCompanion(
                "legacy-stage-5-1",
                "c0bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "df156370538abbdf66d15215fb0d804e",
                report);
            AssertStageAudioCompanion(
                "mechanics-showcase",
                "c0111111111111111111111111111111",
                "6d3b6a4bfe3e41caa96088a8d011beef",
                report);
            AssertStageAudioCompanion(
                "onboarding",
                "c0cccccccccccccccccccccccccccccc",
                "d8a3fa0c6ff547208df399f499335d58",
                report);
        }

        [Test]
        public void CampaignGovernance_RejectsStageAudioDefinitionWithoutOwningEntryReference()
        {
            var stageId = $"audio-companion-test-{Guid.NewGuid():N}".Substring(0, 30);
            var stageFolder = $"{StageContentPaths.CampaignLevel01StagesRoot}/{stageId}";
            var audioPath = $"{stageFolder}/{stageId}_Audio.asset";

            EnsureFolder(StageContentPaths.CampaignLevel01StagesRoot);
            EnsureFolder(stageFolder);

            try
            {
                var audio = ScriptableObject.CreateInstance<StageAudioDefinition>();
                audio.name = $"{stageId}_Audio";
                AssetDatabase.CreateAsset(audio, audioPath);
                AssetDatabase.SaveAssets();

                var report = new StageCampaignContentGovernanceValidator().Validate(StageValidationTiming.TestOrCi);

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == NonCompanionIssueCode &&
                        string.Equals(issue.AssetPath, audioPath, StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(stageFolder);
                AssetDatabase.Refresh();
            }
        }

        private static void AssertStageAudioCompanion(
            string stageId,
            string expectedAudioGuid,
            string expectedEntryGuid,
            StageValidationReport report)
        {
            var stageFolder = $"{StageContentPaths.CampaignLevel01StagesRoot}/{stageId}";
            var audioPath = $"{stageFolder}/{stageId}_Audio.asset";
            var entryPath = $"{stageFolder}/{stageId}_Entry.asset";
            var audio = AssetDatabase.LoadAssetAtPath<StageAudioDefinition>(audioPath);
            var entry = AssetDatabase.LoadAssetAtPath<StageContentEntry>(entryPath);

            Assert.That(audio, Is.Not.Null, $"Missing StageAudioDefinition at {audioPath}.");
            Assert.That(entry, Is.Not.Null, $"Missing StageContentEntry at {entryPath}.");
            Assert.That(AssetDatabase.AssetPathToGUID(audioPath), Is.EqualTo(expectedAudioGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(entryPath), Is.EqualTo(expectedEntryGuid));
            Assert.That(audio.OwnerEntry, Is.SameAs(entry));
            Assert.That(audio.OwnerEntryGuid, Is.EqualTo(expectedEntryGuid));
            Assert.That(entry.AudioDefinition, Is.SameAs(audio));
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.AudioDefinition)),
                Is.EqualTo(expectedAudioGuid));
            Assert.That(
                report.Issues.Any(issue =>
                    issue.Code == NonCompanionIssueCode &&
                    string.Equals(issue.AssetPath, audioPath, StringComparison.Ordinal)),
                Is.False);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
