using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageContentEntryCreationTests
    {
        [Test]
        public void CreateForStageDefinition_CreatesStageContentEntryRootWithCurrentCompanions()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            fixture.CreateAsset(gameplay, $"{fixture.StageIdValue}.asset");

            var entry = StageContentEntryCreationTool.CreateForStageDefinition(
                gameplay,
                StageId.CreateOrThrow(fixture.StageIdValue));

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.StageId.Value, Is.EqualTo(fixture.StageIdValue));
            Assert.That(entry.GameplayDefinition, Is.SameAs(gameplay));
            Assert.That(entry.PresentationDefinition, Is.Not.Null);
            Assert.That(entry.AudioDefinition, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(entry), Is.EqualTo($"{fixture.StageFolder}/{fixture.StageIdValue}_Entry.asset"));
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Entry.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Authoring.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Presentation.asset")), Is.True);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Audio.asset")), Is.True);
        }

        [Test]
        public void CreateForStageDefinition_TreatsSelectedStageDefinitionAsGameplayCompanionSource()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            fixture.CreateAsset(gameplay, $"{fixture.StageIdValue}.asset");

            var entry = StageContentEntryCreationTool.CreateForStageDefinition(
                gameplay,
                StageId.CreateOrThrow(fixture.StageIdValue));

            Assert.That(
                StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.StageRoot),
                Is.EqualTo("Stage Root"));
            Assert.That(
                StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.GameplayCompanion),
                Is.EqualTo("Gameplay Companion"));
            Assert.That(entry.GameplayDefinition, Is.SameAs(gameplay));
            Assert.That(entry, Is.Not.SameAs(gameplay));
        }

        [Test]
        public void CreateForStageDefinition_DoesNotCreateRetiredRewardProgressionClearEvaluationCompanions()
        {
            using var fixture = TempCampaignStageAssetFixture.Create();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            fixture.CreateAsset(gameplay, $"{fixture.StageIdValue}.asset");

            StageContentEntryCreationTool.CreateForStageDefinition(
                gameplay,
                StageId.CreateOrThrow(fixture.StageIdValue));

            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_ClearEvaluation.asset")), Is.False);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Reward.asset")), Is.False);
            Assert.That(File.Exists(ToAbsolutePath($"{fixture.StageFolder}/{fixture.StageIdValue}_Progression.asset")), Is.False);
        }

        [Test]
        public void CreationMenu_UsesGameplayCompanionVocabulary()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/StageContentEntryTooling.cs");

            Assert.That(source, Does.Contain("Create Stage Content Entry for Selected StageDefinition Gameplay Companion"));
            Assert.That(source, Does.Contain("StageContentEntry root and companions"));
            Assert.That(source, Does.Not.Contain("Create Stage Content Entry From Selected StageDefinition"));
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
                var stageIdValue = $"codex-entry-creation-test-{Guid.NewGuid():N}";
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
