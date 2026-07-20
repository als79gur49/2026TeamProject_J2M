using System;
using System.IO;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuProfileBackedProviderIntegrationTests
    {
        private const string MainMenuControllerPath =
            "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs";
        private const string MainMenuUiFlowInstallerPath =
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs";
        private const string PendingLaunchProviderPath =
            "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs";
        private const string SaveSlotModelsPath =
            "Assets/_Features/Stages/Runtime/Campaign/SaveSlotModels.cs";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveSlotStore.DefaultPlayerPrefsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void MainMenuProductionPath_ReferencesProviderButNotProfileInternals()
        {
            AssertSourceDoesNotContain(
                MainMenuControllerPath,
                "LastPlayedSlotNumber",
                "CampaignProfileDocument",
                    "FileCampaignProfileRepository",
                    "ICampaignProfileRepository",
                    "profile.json",
                "Steamworks",
                "ISteamRemoteStorage");
            AssertSourceDoesNotContain(
                MainMenuUiFlowInstallerPath,
                    "FileCampaignProfileRepository",
                    "ICampaignProfileRepository",
                    "CampaignProfileDocument",
                "LastPlayedSlotNumber",
                "profile.json",
                "Steamworks",
                "ISteamRemoteStorage");
        }

        [Test]
        public void MainMenuUiFlowInstaller_DefaultsToProfileBackedProvider()
        {
            var source = ReadRepoFile(MainMenuUiFlowInstallerPath);

            Assert.That(source, Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(source, Does.Not.Contain("ProfileJsonExplicit"));
            Assert.That(source, Does.Not.Contain("EnableProfileWrite"));
            Assert.That(source, Does.Not.Contain("CampaignSaveFacadeFactory.Create().CampaignSaveSlots"));
        }

        [Test]
        public void MainMenuUiFlowInstaller_DoesNotWireFileCampaignProfileRepository()
        {
            var source = ReadRepoFile(MainMenuUiFlowInstallerPath);

            Assert.That(source, Does.Not.Contain("FileCampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("ICampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("profile.json"));
        }

        [TestCase("profile.json")]
        [TestCase("CampaignProfileDocument")]
        [TestCase("CampaignSaveService")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("ICampaignProfileRepository")]
        public void MainMenuController_DoesNotReadV2ProfileOrStorage(string forbiddenToken)
        {
            var source = ReadRepoFile(MainMenuControllerPath);

            Assert.That(source, Does.Not.Contain(forbiddenToken));
            Assert.That(source, Does.Contain("_saveSlotStore.LoadAllWithReport()"));
        }

        [Test]
        public void MainMenuPendingLaunchProvider_DoesNotReadProfileMetadata()
        {
            AssertSourceDoesNotContain(
                PendingLaunchProviderPath,
                "LastPlayedSlotNumber",
                "CampaignProfileDocument",
                "CampaignSaveService",
                "CampaignSaveServiceFactory",
                "FileCampaignProfileRepository",
                "ICampaignProfileRepository",
                "profile.json");
            Assert.That(ReadRepoFile(PendingLaunchProviderPath), Does.Contain("ICampaignLaunchHandoffStore"));
            Assert.That(ReadRepoFile(PendingLaunchProviderPath), Does.Contain("CampaignLaunchHandoffSessionStore"));
            Assert.That(ReadRepoFile(PendingLaunchProviderPath), Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
        }

        [Test]
        public void SaveSlotStorePublicConstructor_DefaultRemainsPlayerPrefs()
        {
            var store = new SaveSlotStore();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });

            Assert.That(store.PlayerPrefsKey, Is.EqualTo(SaveSlotStore.DefaultPlayerPrefsKey));
            Assert.That(SaveSlotStore.DefaultPlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(PlayerPrefs.HasKey(SaveSlotStore.DefaultPlayerPrefsKey), Is.True);
        }

        [Test]
        public void SaveSlotModels_DefaultStoreAndRunningSlotSlices_DoNotUseLastPlayedAsRuntimeSlot()
        {
            var source = ReadRepoFile(SaveSlotModelsPath);
            var saveSlotStoreSource = ExtractSourceRange(
                source,
                "public sealed class SaveSlotStore",
                "public sealed class SaveSlotStageClearProfileStore");
            var runningSlotStoreSource = ExtractSourceRange(
                source,
                "public sealed class SaveSlotStageClearProfileStore",
                "[Serializable]");

            Assert.That(saveSlotStoreSource, Does.Contain("new PlayerPrefsSaveSlotStorageBackend"));
            Assert.That(saveSlotStoreSource, Does.Not.Contain("CampaignSaveServiceFactory"));
            Assert.That(saveSlotStoreSource, Does.Not.Contain("FileCampaignProfileRepository"));
            Assert.That(runningSlotStoreSource, Does.Contain("CampaignRunningSlotContext"));
            Assert.That(runningSlotStoreSource, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(runningSlotStoreSource, Does.Not.Contain("CampaignProfileDocument"));
        }

        private static void AssertSourceDoesNotContain(string path, params string[] forbiddenTokens)
        {
            var source = ReadRepoFile(path);
            foreach (var forbiddenToken in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(forbiddenToken), $"{path}: {forbiddenToken}");
            }
        }

        private static string ExtractSourceRange(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return source.Substring(start, end - start);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }
    }
}
