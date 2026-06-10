using System;
using System.IO;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SaveSlotValidationAndDirectPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            ClearStageSavePrefsForTests();
        }

        [Test]
        public void NonCampaignDirectPlay_PrimesSuppressContext()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");

            StageEditorDirectPlayLauncher.PrimeNonCampaignForTests(stageId);

            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out var context), Is.True);
            Assert.That(context.Mode, Is.EqualTo(EditorDirectPlayMode.NonCampaign));
            Assert.That(context.StageId, Is.EqualTo(stageId));
            Assert.That(context.SuppressCampaignFlow, Is.True);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var pending), Is.True);
            Assert.That(pending, Is.EqualTo(stageId));
        }

        [Test]
        public void CampaignTempDirectPlay_UsesTempSaveAndActiveKeys_NotProductionKeys()
        {
            ClearStageSavePrefsForTests();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            var resolver = new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var stageId = StageId.CreateOrThrow("stage-2-2");

            StageEditorDirectPlayLauncher.PrimeCampaignTempSlotForTests(stageId, resolver, remainingChances: 2);

            Assert.That(PlayerPrefs.HasKey(SaveSlotStore.DefaultPlayerPrefsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacySaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey), Is.True);
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out var context), Is.True);
            Assert.That(context.SaveSlotStoreKey, Is.EqualTo(EditorDirectPlayContextStore.TempSaveSlotStoreKey));
            Assert.That(context.ActiveSlotProviderKey, Is.EqualTo(EditorDirectPlayContextStore.TempActiveSlotProviderKey));
        }

        [Test]
        public void SaveSlotStore_NewWrite_UsesStageClearSaveSlotsKey()
        {
            ClearStageSavePrefsForTests();
            var store = new SaveSlotStore();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });

            Assert.That(SaveSlotStore.DefaultPlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacySaveSlotsKey), Is.False);
            var dto = JsonUtility.FromJson<SaveSlotStoreDto>(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(dto.Slots[0].CurrentStageId, Is.EqualTo("stage-1-1"));
        }

        [Test]
        public void ActiveSlotStageClearProfileStore_UsesActiveStageClearSaveSlotKey()
        {
            ClearStageSavePrefsForTests();
            var activeSlotProvider = new ActiveSlotProvider();

            activeSlotProvider.SetActiveSlot(2);

            Assert.That(activeSlotProvider.PlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.ActiveSaveSlotKey));
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.EqualTo(2));
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey), Is.False);
        }

        [Test]
        public void SaveSlotStore_OldPrefsKey_IsDeletedOnInitialize()
        {
            ClearStageSavePrefsForTests();
            PlayerPrefs.SetString(SaveSlotPrefsKeys.LegacySaveSlotsKey, "{\"SaveVersion\":1}");
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            _ = new SaveSlotStore();

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacySaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey), Is.False);
        }

        [Test]
        public void SaveSlotStore_CustomKey_DoesNotDeleteLegacyPrefs()
        {
            ClearStageSavePrefsForTests();
            PlayerPrefs.SetString(SaveSlotPrefsKeys.LegacySaveSlotsKey, "{\"SaveVersion\":1}");
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey, 1);
            PlayerPrefs.Save();
            var customKey = CreatePrefsKey(nameof(SaveSlotStore_CustomKey_DoesNotDeleteLegacyPrefs));

            _ = new SaveSlotStore(customKey);

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacySaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey), Is.True);
        }

        [Test]
        public void SaveSlotTestScope_ClearsOldAndNewPrefsKeys()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.LegacySaveSlotsKey, "old-save");
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey, 1);
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, "new-save");
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 2);
            PlayerPrefs.Save();

            ClearStageSavePrefsForTests();

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacySaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.False);
        }

        [Test]
        public void CampaignTempDirectPlay_PrimesSlot1WithSelectedStageDerivedLevelGroupAndRemainingChances()
        {
            var resolver = new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var stageId = StageId.CreateOrThrow("stage-3-2");

            StageEditorDirectPlayLauncher.PrimeCampaignTempSlotForTests(stageId, resolver, remainingChances: 1);

            var store = new SaveSlotStore(EditorDirectPlayContextStore.TempSaveSlotStoreKey);
            var activeSlotProvider = new ActiveSlotProvider(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            var slot = store.LoadSlot(1);
            Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(1));
            Assert.That(slot.CurrentStageId, Is.EqualTo(stageId));
            Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-3"));
            Assert.That(slot.RemainingChances, Is.EqualTo(1));
        }

        [Test]
        public void StandaloneCampaignSeedImport_PrimesDefaultSaveStoreAndActiveSlot()
        {
            var seedPath = CreateTempSeedPath();
            var provider = CreateProvider("stage-2-2");
            var resolver = new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var saveStore = new SaveSlotStore();
            var activeSlotProvider = new ActiveSlotProvider();
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            try
            {
                File.WriteAllText(
                    seedPath,
                    StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                        StageId.CreateOrThrow("stage-2-2"),
                        slotNumber: 2,
                        remainingChances: 1));

                Assert.That(
                    StandaloneCampaignSaveSeedImporter.TryImportSeedFile(
                        seedPath,
                        saveStore,
                        activeSlotProvider,
                        resolver,
                        provider.Provider,
                        deleteAfterImport: true,
                        out var result),
                    Is.True);

                var slot = saveStore.LoadSlot(2);
                Assert.That(result.Status, Is.EqualTo(StandaloneCampaignSaveSeedImportStatus.Imported));
                Assert.That(File.Exists(seedPath), Is.False);
                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
                Assert.That(slot.CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-2")));
                Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
            }
            finally
            {
                DeleteFileIfExists(seedPath);
                provider.Dispose();
            }
        }

        [Test]
        public void StandaloneCampaignSeedImport_RejectsMissingCatalogStageWithoutSaving()
        {
            var seedPath = CreateTempSeedPath();
            var provider = CreateProvider("stage-0-1");
            var resolver = new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var saveStore = new SaveSlotStore();
            var activeSlotProvider = new ActiveSlotProvider();
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            try
            {
                File.WriteAllText(
                    seedPath,
                    StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                        StageId.CreateOrThrow("stage-2-2"),
                        slotNumber: 2,
                        remainingChances: 1));

                Assert.That(
                    StandaloneCampaignSaveSeedImporter.TryImportSeedFile(
                        seedPath,
                        saveStore,
                        activeSlotProvider,
                        resolver,
                        provider.Provider,
                        deleteAfterImport: false,
                        out var result),
                    Is.False);

                Assert.That(result.Status, Is.EqualTo(StandaloneCampaignSaveSeedImportStatus.StageMissingFromCatalog));
                Assert.That(saveStore.LoadSlot(2).IsEmpty, Is.True);
                Assert.That(activeSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
                Assert.That(File.Exists(seedPath), Is.True);
            }
            finally
            {
                DeleteFileIfExists(seedPath);
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignDirectPlay_RejectsSelectedStageMissingFromSequenceOrCatalog()
        {
            var provider = CreateProvider("stage-0-1");
            try
            {
                var validation = new SaveSlotValidationService(
                    new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                    provider.Provider);

                Assert.That(
                    validation.Validate(new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-9-9"),
                    }).Status,
                    Is.EqualTo(SaveSlotValidationStatus.StageMissingFromSequence));

                Assert.That(
                    validation.Validate(new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                    }).Status,
                    Is.EqualTo(SaveSlotValidationStatus.StageMissingFromCatalog));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_RejectsStageIdNotInSequence()
        {
            var provider = CreateProvider("stage-9-9");
            try
            {
                var validation = new SaveSlotValidationService(
                    new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                    provider.Provider);

                var result = validation.Validate(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-9-9"),
                });

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.StageMissingFromSequence));
                Assert.That(result.CanContinue, Is.False);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_RejectsStageIdMissingFromCatalog()
        {
            var provider = CreateProvider("stage-0-1");
            try
            {
                var validation = new SaveSlotValidationService(
                    new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                    provider.Provider);

                var result = validation.Validate(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                });

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.StageMissingFromCatalog));
                Assert.That(result.CanContinue, Is.False);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_SyncsLevelGroupFromCurrentStage()
        {
            var provider = CreateProvider("stage-2-2");
            try
            {
                var validation = new SaveSlotValidationService(
                    new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                    provider.Provider);

                var result = validation.Validate(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-5",
                });

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.Valid));
                Assert.That(result.LevelGroupWasSynced, Is.True);
                Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_MigratesRetiredStageFiveOne_ToCompletedFinalStage()
        {
            var provider = CreateProvider("stage-4-2");
            var saveStore = new SaveSlotStore();
            saveStore.ClearAll();
            try
            {
                var validation = new SaveSlotValidationService(
                    new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                    provider.Provider);
                var legacySlot = new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-5-1"),
                    CurrentLevelGroupId = "level-5",
                    RemainingChances = 2,
                };

                var result = validation.Validate(legacySlot);

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.Completed));
                Assert.That(result.RequiresSaveSync, Is.True);
                Assert.That(result.Slot.CurrentStageId.Value, Is.EqualTo("stage-4-2"));
                Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo("level-4"));
                Assert.That(result.Slot.CampaignCompleted, Is.True);

                saveStore.SaveSlot(legacySlot);
                var synced = validation.ValidateAndSync(saveStore, 1);
                var persisted = saveStore.LoadSlot(1);

                Assert.That(synced.Status, Is.EqualTo(SaveSlotValidationStatus.Completed));
                Assert.That(synced.RequiresSaveSync, Is.False);
                Assert.That(persisted.CurrentStageId.Value, Is.EqualTo("stage-4-2"));
                Assert.That(persisted.CurrentLevelGroupId, Is.EqualTo("level-4"));
                Assert.That(persisted.CampaignCompleted, Is.True);
            }
            finally
            {
                saveStore.ClearAll();
                provider.Dispose();
            }
        }

        [Test]
        public void DirectPlayRouteConfigAsset_UsesBuildSettingsScenes()
        {
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(
                "Assets/_Features/UI/UI_Composition/Authoring/GameplayStageLaunchRouteConfig.asset");
            Assert.That(routeConfig, Is.Not.Null);
            Assert.That(routeConfig.MainMenuScenePath, Is.EqualTo("Assets/Scenes/MainMenuScene.unity"));
            Assert.That(routeConfig.GameplayShellScenePath, Is.EqualTo("Assets/Scenes/UIAudioScene.unity"));
        }

        private static ProviderHarness CreateProvider(params string[] stageIds)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            var provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            var entries = new StageContentEntry[stageIds.Length];
            for (var i = 0; i < stageIds.Length; i++)
            {
                entries[i] = ScriptableObject.CreateInstance<StageContentEntry>();
                entries[i].AssignStageId(StageId.CreateOrThrow(stageIds[i]));
            }

            catalog.SetEntries(entries);
            provider.AssignCatalog(catalog);
            return new ProviderHarness(provider, catalog, entries);
        }

        private static string CreateTempSeedPath()
        {
            var directory = Path.Combine("Temp", "StandaloneCampaignSeedTests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.Stages.Editor.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private static void ClearStageSavePrefsForTests()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.Save();
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class ProviderHarness : IDisposable
        {
            private readonly StageCatalog _catalog;
            private readonly StageContentEntry[] _entries;

            public ProviderHarness(
                ScriptableObjectStageCatalogProvider provider,
                StageCatalog catalog,
                StageContentEntry[] entries)
            {
                Provider = provider;
                _catalog = catalog;
                _entries = entries;
            }

            public ScriptableObjectStageCatalogProvider Provider { get; }

            public void Dispose()
            {
                for (var i = 0; i < _entries.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_entries[i]);
                }

                UnityEngine.Object.DestroyImmediate(Provider);
                UnityEngine.Object.DestroyImmediate(_catalog);
            }
        }
    }
}
