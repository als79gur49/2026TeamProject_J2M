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
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
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
            Assert.That(dto.SchemaId, Is.EqualTo(SaveSlotStore.SchemaId));
            Assert.That(dto.SchemaVersion, Is.EqualTo(SaveSlotStore.SchemaVersion));
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
        public void SavePathProvider_DefaultProvider_UsesPersistentDataSavesRoot()
        {
            var provider = new ApplicationPersistentDataSavePathProvider();

            Assert.That(
                provider.SaveRootPath,
                Is.EqualTo(Path.Combine(Application.persistentDataPath, ApplicationPersistentDataSavePathProvider.SavesDirectoryName)));
            Assert.That(
                provider.GetSaveFilePath("profile.json"),
                Is.EqualTo(Path.Combine(Application.persistentDataPath, "Saves", "profile.json")));
        }

        [Test]
        public void SavePathProvider_TempRootProvider_CanBeInjectedByTests()
        {
            var tempRoot = Path.Combine("Temp", "SavePathProviderTests", Guid.NewGuid().ToString("N"));
            ISavePathProvider provider = new TemporarySavePathProvider(tempRoot);

            Assert.That(provider.SaveRootPath, Is.EqualTo(tempRoot));
            Assert.That(provider.GetSaveFilePath("profile.json"), Is.EqualTo(Path.Combine(tempRoot, "profile.json")));
        }

        [Test]
        public void FileSaveSlotStorageBackend_NoFile_ReturnsDefaultAndCreatesNoFile()
        {
            using var harness = CreateSaveFileHarness();

            Assert.That(harness.Backend.HasPayload(), Is.False);
            Assert.That(harness.Backend.LoadPayload("default-payload"), Is.EqualTo("default-payload"));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
        }

        [Test]
        public void FileSaveSlotStorageBackend_SaveCreatesProfileAndDirectory()
        {
            using var harness = CreateSaveFileHarness();
            const string payload = "{\"value\":1}";

            harness.Backend.SavePayload(payload);

            Assert.That(Directory.Exists(harness.SaveRootPath), Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(payload));
        }

        [Test]
        public void FileSaveSlotStorageBackend_SaveThenLoad_ReturnsIdenticalRawPayload()
        {
            using var harness = CreateSaveFileHarness();
            const string payload = "{\"items\":[1,true,null],\"name\":\"profile\"}";

            harness.Backend.SavePayload(payload);

            Assert.That(harness.Backend.HasPayload(), Is.True);
            Assert.That(harness.Backend.LoadPayload(string.Empty), Is.EqualTo(payload));
        }

        [Test]
        public void FileSaveSlotStorageBackend_ReplacingExistingProfile_UpdatesBackup()
        {
            using var harness = CreateSaveFileHarness();
            const string firstPayload = "{\"value\":1}";
            const string secondPayload = "{\"value\":2}";

            harness.Backend.SavePayload(firstPayload);
            harness.Backend.SavePayload(secondPayload);

            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(secondPayload));
            Assert.That(File.Exists(harness.BackupPath), Is.True);
            Assert.That(File.ReadAllText(harness.BackupPath), Is.EqualTo(firstPayload));
        }

        [Test]
        public void FileSaveSlotStorageBackend_CorruptCanonicalWithValidBackup_RestoresBackupPayload()
        {
            using var harness = CreateSaveFileHarness();
            const string backupPayload = "{\"value\":1}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"value\":");
            File.WriteAllText(harness.BackupPath, backupPayload);

            var loaded = harness.Backend.LoadPayload("default-payload");

            Assert.That(loaded, Is.EqualTo(backupPayload));
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(backupPayload));
        }

        [Test]
        public void FileSaveSlotStorageBackend_CorruptCanonicalWithoutValidBackup_QuarantinesWithoutEmptyReset()
        {
            using var harness = CreateSaveFileHarness();
            const string corruptPayload = "{\"value\":";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, corruptPayload);
            File.WriteAllText(harness.BackupPath, "{\"backup\":");

            var loaded = harness.Backend.LoadPayload("default-payload");
            var corruptFiles = Directory.GetFiles(harness.SaveRootPath, "profile.json.corrupt.*");

            Assert.That(loaded, Is.EqualTo("default-payload"));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(corruptFiles.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(corruptFiles[0]), Is.EqualTo(corruptPayload));
        }

        [Test]
        public void FileSaveSlotStorageBackend_LeftoverTempIgnoredAndCleanedOnLoad()
        {
            using var harness = CreateSaveFileHarness();
            const string payload = "{\"value\":1}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, payload);
            var tempPath = Path.Combine(harness.SaveRootPath, "profile.leftover.tmp");
            File.WriteAllText(tempPath, "{\"value\":999}");

            var loaded = harness.Backend.LoadPayload(string.Empty);

            Assert.That(loaded, Is.EqualTo(payload));
            Assert.That(File.Exists(tempPath), Is.False);
        }

        [Test]
        public void FileSaveSlotStorageBackend_EmptyAndWhitespacePayloads_ReturnRawPayload()
        {
            using var harness = CreateSaveFileHarness();
            Directory.CreateDirectory(harness.SaveRootPath);

            File.WriteAllText(harness.ProfilePath, string.Empty);
            Assert.That(harness.Backend.LoadPayload("default-payload"), Is.EqualTo(string.Empty));

            const string whitespacePayload = " \r\n\t ";
            File.WriteAllText(harness.ProfilePath, whitespacePayload);
            Assert.That(harness.Backend.LoadPayload("default-payload"), Is.EqualTo(whitespacePayload));
        }

        [Test]
        public void SaveSlotStore_FileBackend_SaveSlotThenLoadAll_RoundTripsThroughProfileFile()
        {
            using var harness = CreateSaveFileHarness();
            var store = harness.CreateStore();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = 2,
            });

            var reloaded = harness.CreateStore().LoadAll();
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(reloaded[0].CurrentStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(reloaded[0].RemainingChances, Is.EqualTo(2));
            Assert.That(reloaded[1].IsEmpty, Is.True);
        }

        [Test]
        public void SaveSlotStore_FileBackend_DeleteSlot_RewritesProfilePayload()
        {
            using var harness = CreateSaveFileHarness();
            var store = harness.CreateStore();
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                CurrentLevelGroupId = "level-2",
            });

            store.DeleteSlot(1);

            var reloaded = harness.CreateStore().LoadAll();
            Assert.That(reloaded[0].IsEmpty, Is.True);
            Assert.That(reloaded[1].CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(File.ReadAllText(harness.ProfilePath), Does.Not.Contain("stage-1-1"));
            Assert.That(File.ReadAllText(harness.ProfilePath), Does.Contain("stage-2-1"));
        }

        [Test]
        public void SaveSlotStore_FileBackend_ClearAll_ClearsProfilePayload()
        {
            using var harness = CreateSaveFileHarness();
            var store = harness.CreateStore();
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });

            store.ClearAll();

            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(File.Exists(harness.BackupPath), Is.False);
            Assert.That(harness.CreateStore().LoadAll()[0].IsEmpty, Is.True);
        }

        [Test]
        public void SaveSlotStore_FileBackend_InvalidPayload_UsesGuardResetWithoutSilentEmptyOverwrite()
        {
            using var harness = CreateSaveFileHarness();
            const string invalidPayload = "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":99,\"SaveVersion\":1,\"Slots\":[]}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, invalidPayload);
            var store = harness.CreateStore();

            var slots = store.LoadAll();
            var corruptFiles = Directory.GetFiles(harness.SaveRootPath, "profile.json.corrupt.*");

            Assert.That(slots[0].IsEmpty, Is.True);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(corruptFiles.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(corruptFiles[0]), Is.EqualTo(invalidPayload));
        }

        [Test]
        public void SaveSlotStore_PublicConstructor_StillUsesPlayerPrefsBackendByDefault()
        {
            using var harness = CreateSaveFileHarness();
            ClearStageSavePrefsForTests();
            var store = new SaveSlotStore();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });

            Assert.That(PlayerPrefs.HasKey(SaveSlotStore.DefaultPlayerPrefsKey), Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
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
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
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
        public void StandaloneCampaignSeedImport_WritesProfileBackedStore_NotPlayerPrefs()
        {
            var seedPath = CreateTempSeedPath();
            var provider = CreateProvider("stage-2-2");
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var saveRootPath = Path.Combine("Temp", "StandaloneCampaignSeedProfileTests", Guid.NewGuid().ToString("N"));
            var saveStore = CampaignSaveCompositionProvider.Create(new CampaignSaveCompositionOptions
            {
                BackendMode = CampaignSaveBackendMode.ProfileJsonExplicit,
                PathProvider = new TemporarySavePathProvider(saveRootPath),
                EnableProfileWrite = true,
                AllowLegacyImport = false,
                ProductVersion = "seed-import-test",
                ProfileId = "seed-import-test-profile",
                LegacyImportMarkerStore = CreateMarkerStore("SeedImport"),
            });
            var activeSlotProvider = new ActiveSlotProvider(CreatePrefsKey("seed-active"));
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
                Assert.That(File.Exists(Path.Combine(saveRootPath, FileCampaignProfileRepository.ProfileFileName)), Is.True);
                Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
                Assert.That(slot.CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-2")));
                Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
            }
            finally
            {
                DeleteFileIfExists(seedPath);
                if (Directory.Exists(saveRootPath))
                {
                    Directory.Delete(saveRootPath, recursive: true);
                }

                activeSlotProvider.ClearActiveSlot();
                provider.Dispose();
            }
        }

        [Test]
        public void StandaloneCampaignSeedImport_RejectsMissingCatalogStageWithoutSaving()
        {
            var seedPath = CreateTempSeedPath();
            var provider = CreateProvider("stage-0-1");
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
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
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
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
        public void DirectPlayTemp_RemainsTempPlayerPrefsAndProductionOverwriteUsesProvider()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var tempMethod = ExtractSourceRange(source, "private static void PrimeCampaignTempSlot", "private static void PrimeCampaignProductionSlot");
            var productionMethod = ExtractSourceRange(source, "private static void PrimeCampaignProductionSlot", "private static void ValidateCampaignStage");

            Assert.That(tempMethod, Does.Contain("EditorDirectPlayContextStore.TempSaveSlotStoreKey"));
            Assert.That(tempMethod, Does.Contain("EditorDirectPlayContextStore.TempActiveSlotProviderKey"));
            Assert.That(tempMethod, Does.Contain("new SaveSlotStore("));
            Assert.That(tempMethod, Does.Contain("new ActiveSlotProvider(EditorDirectPlayContextStore.TempActiveSlotProviderKey)"));
            Assert.That(tempMethod, Does.Not.Contain("CampaignSaveCompositionProvider"));
            Assert.That(productionMethod, Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(productionMethod, Does.Contain("CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(saveStore)"));
            Assert.That(productionMethod, Does.Not.Contain("new SaveSlotStore()"));
            Assert.That(productionMethod, Does.Not.Contain("new ActiveSlotProvider()"));
            Assert.That(productionMethod, Does.Not.Contain("EditorDirectPlayContextStore.TempActiveSlotProviderKey"));
        }

        [Test]
        public void SaveSlotValidation_RejectsStageIdNotInSequence()
        {
            var provider = CreateProvider("stage-9-9");
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
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
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
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
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
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
            var provider = CreateProvider("stage-4-3");
            var saveStore = new SaveSlotStore();
            saveStore.ClearAll();
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
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
                Assert.That(result.Slot.CurrentStageId.Value, Is.EqualTo("stage-4-3"));
                Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo("level-4"));
                Assert.That(result.Slot.CampaignCompleted, Is.True);

                saveStore.SaveSlot(legacySlot);
                var synced = validation.ValidateAndSync(saveStore, 1);
                var persisted = saveStore.LoadSlot(1);

                Assert.That(synced.Status, Is.EqualTo(SaveSlotValidationStatus.Completed));
                Assert.That(synced.RequiresSaveSync, Is.False);
                Assert.That(persisted.CurrentStageId.Value, Is.EqualTo("stage-4-3"));
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

        private static CampaignLegacyImportMarkerStore CreateMarkerStore(string suffix)
        {
            return new CampaignLegacyImportMarkerStore(
                CreatePrefsKey(suffix + ".ImportDisabled"),
                CreatePrefsKey(suffix + ".ImportedSourceHash"),
                CreatePrefsKey(suffix + ".ResetTombstoneUtc"),
                CreatePrefsKey(suffix + ".DeletedSlotGuards"));
        }

        private static string ExtractSourceRange(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return source.Substring(start, end - start);
        }

        private static SaveFileHarness CreateSaveFileHarness()
        {
            return new SaveFileHarness(
                Path.Combine("Temp", "FileSaveSlotStorageBackendTests", Guid.NewGuid().ToString("N")));
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

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }

        private sealed class SaveFileHarness : IDisposable
        {
            private readonly string _testRootPath;
            private readonly TemporarySavePathProvider _provider;

            public SaveFileHarness(string testRootPath)
            {
                _testRootPath = testRootPath;
                SaveRootPath = Path.Combine(testRootPath, "Saves");
                _provider = new TemporarySavePathProvider(SaveRootPath);
                Backend = new FileSaveSlotStorageBackend(_provider);
                StoreKey = "Game.Feature.Stages.Editor.Tests.FileBackend." + Guid.NewGuid().ToString("N");
            }

            public string SaveRootPath { get; }

            public string StoreKey { get; }

            public FileSaveSlotStorageBackend Backend { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, FileSaveSlotStorageBackend.ProfileFileName);

            public string BackupPath => Path.Combine(SaveRootPath, FileSaveSlotStorageBackend.BackupFileName);

            public SaveSlotStore CreateStore()
            {
                return new SaveSlotStore(new FileSaveSlotStorageBackend(_provider), StoreKey);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_testRootPath))
                    {
                        Directory.Delete(_testRootPath, recursive: true);
                    }
                }
                catch
                {
                }
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
