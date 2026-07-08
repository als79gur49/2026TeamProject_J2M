using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SaveArchitectureV2ProductionReadinessTests
    {
        private static readonly string[] ProductionCompositionFiles =
        {
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs",
        };

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.Save();
        }

        [TestCase("CampaignSaveServiceFactory")]
        [TestCase("CampaignSaveService")]
        [TestCase("SaveSlotStoreCompatibilityAdapter")]
        [TestCase("CampaignSaveMigrationCoordinator")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("profile.json")]
        public void ProductionComposition_DoesNotReferenceV2FactoryOrServiceTypes(string forbiddenToken)
        {
            foreach (var path in EnumerateProductionReadinessSourceFiles())
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain(forbiddenToken), path);
            }
        }

        [TestCase("CampaignSaveServiceFactory")]
        [TestCase("CampaignSaveService")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("ICampaignProfileRepository")]
        [TestCase("CampaignProfileDocument")]
        [TestCase("profile.json")]
        [TestCase("LastPlayedSlotNumber")]
        public void MainMenuProductionPath_DoesNotReferenceV2MetadataTruthTokens(string forbiddenToken)
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs"),
                Does.Not.Contain(forbiddenToken),
                forbiddenToken);
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain(forbiddenToken),
                forbiddenToken);
        }

        [Test]
        public void MainMenuProductionPath_KeepsPlayerPrefsSaveSlotStoreAsUxSource()
        {
            var controller = File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var installer = File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(controller, Does.Contain("_saveSlotStore.LoadAll()"));
            Assert.That(installer, Does.Contain("var saveSlotStore = new SaveSlotStore();"));
            Assert.That(installer, Does.Contain("new ActiveSlotProviderPendingLaunchAdapter(activeSlotProvider)"));
        }

        [Test]
        public void SaveSlotStorePublicConstructor_DefaultStillUsesPlayerPrefsBackend()
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
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
        }

        [Test]
        public void CampaignSaveMigrationOptions_DefaultEnableProfileWriteIsFalse()
        {
            var options = new CampaignSaveMigrationOptions();

            Assert.That(options.EnableProfileWrite, Is.False);
            Assert.That(CampaignSaveMigrationOptions.Default.EnableProfileWrite, Is.False);
        }

        [Test]
        public void V2RuntimeSources_DoNotCallSteamApis()
        {
            foreach (var path in Directory.GetFiles(
                         "Assets/_Features/Stages/Runtime/Campaign/Save",
                         "*.cs"))
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("Steamworks"), path);
                Assert.That(source, Does.Not.Contain("ISteamRemoteStorage"), path);
                Assert.That(source, Does.Not.Contain("SteamRemoteStorage"), path);
            }
        }

        private static IEnumerable<string> EnumerateProductionReadinessSourceFiles()
        {
            foreach (var path in ProductionCompositionFiles)
            {
                yield return path;
            }

            foreach (var path in Directory.GetFiles(
                         "Assets/_Features/Stages/Runtime/Load",
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                yield return path;
            }
        }
    }
}
