using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveServiceTests
    {
        private const string FixedNowUtc = "2026-07-07T00:00:00Z";
        private const string ProfileId = "campaign-save-service-test-profile";
        private const string ProductVersion = "campaign-save-service-test-product";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.DeleteKey("settings.audio.master.volume");
            PlayerPrefs.DeleteKey("settings.audio.master.muted");
            PlayerPrefs.DeleteKey("settings.display.width");
            PlayerPrefs.DeleteKey("settings.display.height");
            PlayerPrefs.DeleteKey("Game.Feature.Input.KeyboardMovementScheme");
            PlayerPrefs.Save();
        }

        [Test]
        public void InitializeNewGame_WritesDocument()
        {
            var repository = new RecordingRepository();
            var service = CreateService(repository);

            var result = service.InitializeNewGame(1, "stage-1-1", "level-1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(repository.SavedDocument.SavedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(repository.SavedDocument.Slots[0].LevelGroupId, Is.EqualTo("level-1"));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void GetSlots_ReturnsDocumentSlots()
        {
            var document = CreateDocument(
                CreateSlot(2, "stage-2-1", "level-2", remainingChances: 1));
            var service = CreateService(new RecordingRepository(document));

            var result = service.GetSlots();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Slots, Has.Length.EqualTo(1));
            Assert.That(result.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(result.Slots[0].StageId, Is.EqualTo("stage-2-1"));
        }

        [Test]
        public void UpdateSlot_ChangesCurrentStageAndChances()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository);

            var result = service.UpdateSlot(1, new CampaignSlotUpdate
            {
                StageId = "stage-1-2",
                LevelGroupId = "level-1",
                RemainingChances = 2,
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-1-2"));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(2));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(repository.SavedDocument.SavedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void ApplyDeath_IncrementsTotalDeathsAndAppliesChanceUpdate()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 2, totalDeaths: 4)));
            var service = CreateService(repository);

            var result = service.ApplyDeath(1, new CampaignDeathSaveUpdate
            {
                StageId = "stage-1-1",
                LevelGroupId = "level-1",
                RemainingChances = 1,
                DeathsToAdd = 1,
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].TotalDeaths, Is.EqualTo(5));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void CinematicFlags_Persist()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository);

            var intro = service.SetIntroPlayed(1, "stage-1-1");
            var outro = service.SetOutroPlayed(1, "stage-1-1");

            Assert.That(intro.Succeeded, Is.True);
            Assert.That(outro.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].IntroPlayed, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].OutroPlayed, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void ApplyStageClear_UpdatesClearProfileRecordAndProcessedIds()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository);
            var update = new StageClearSaveUpdate
            {
                ClearedStageId = "stage-1-1",
                NextStageId = "stage-1-2",
                LevelGroupId = "level-1",
                StageRunId = "run-a",
                StageCompletionAttemptId = "attempt-a",
            };

            var result = service.ApplyStageClear(1, update);
            var duplicate = service.ApplyStageClear(1, update);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(duplicate.Succeeded, Is.True);
            var slot = repository.SavedDocument.Slots[0];
            Assert.That(slot.StageId, Is.EqualTo("stage-1-2"));
            Assert.That(slot.StageClearProfileSnapshot.Version, Is.EqualTo(1));
            Assert.That(slot.StageClearProfileSnapshot.Records, Has.Length.EqualTo(1));
            Assert.That(slot.StageClearProfileSnapshot.Records[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(slot.StageClearProfileSnapshot.Records[0].HasAttempted, Is.True);
            Assert.That(slot.StageClearProfileSnapshot.Records[0].HasCleared, Is.True);
            Assert.That(slot.StageClearProfileSnapshot.Records[0].ClearCount, Is.EqualTo(1));
            Assert.That(slot.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(slot.StageClearProfileSnapshot.ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(slot.StageClearProfileSnapshot.ProcessedClearAttemptIds, Does.Contain("attempt-a"));
        }

        [Test]
        public void DeleteSlot_RemovesSlotAndPreservesOtherSlots()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3),
                CreateSlot(2, "stage-2-1", "level-2", remainingChances: 1)));
            repository.CurrentDocument.LastPlayedSlotNumber = 1;
            var service = CreateService(repository);

            var result = service.DeleteSlot(1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots, Has.Length.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-2-1"));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void ClearAll_WritesEmptyValidProfileAndResetMarker()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, "legacy-stays");
            PlayerPrefs.Save();
            var marker = new RecordingResetMarkerPort();
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository, marker);

            var result = service.ClearAll();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.SchemaVersion, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.Zero);
            Assert.That(repository.SavedDocument.Slots, Is.Empty);
            Assert.That(repository.SavedDocument.LegacyImport.ImportDisabled, Is.True);
            Assert.That(repository.SavedDocument.LegacyImport.ResetTombstoneUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(marker.MarkCount, Is.EqualTo(1));
            Assert.That(marker.ResetTombstoneUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo("legacy-stays"));
        }

        [Test]
        public void ClearAll_WithLegacyResetMarkerPort_WritesRemigrationGuardMarkers()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, "legacy-stays");
            PlayerPrefs.Save();
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository, new CampaignLegacyImportResetMarkerPort());

            var result = service.ClearAll();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(PlayerPrefs.GetInt(CampaignLegacyImportMarkerStore.ImportDisabledKey), Is.EqualTo(1));
            Assert.That(
                PlayerPrefs.GetString(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey),
                Is.EqualTo(FixedNowUtc));
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo("legacy-stays"));
        }

        [Test]
        public void MarkLastPlayedSlot_UpdatesProfileMetadata()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3),
                CreateSlot(3, "stage-3-1", "level-3", remainingChances: 2)));
            var service = CreateService(repository);

            var result = service.MarkLastPlayedSlot(3);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(repository.SavedDocument.SavedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void Service_UsesRepositoryAndDoesNotTouchSettingsPlayerPrefs()
        {
            PlayerPrefs.SetFloat("settings.audio.master.volume", 0.25f);
            PlayerPrefs.SetInt("settings.audio.master.muted", 1);
            PlayerPrefs.SetInt("settings.display.width", 1600);
            PlayerPrefs.SetInt("settings.display.height", 900);
            PlayerPrefs.SetString("Game.Feature.Input.KeyboardMovementScheme", "wasd");
            PlayerPrefs.Save();
            var repository = new RecordingRepository();
            var service = CreateService(repository);

            var result = service.InitializeNewGame(1, "stage-1-1", "level-1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetFloat("settings.audio.master.volume"), Is.EqualTo(0.25f));
            Assert.That(PlayerPrefs.GetInt("settings.audio.master.muted"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("settings.display.width"), Is.EqualTo(1600));
            Assert.That(PlayerPrefs.GetInt("settings.display.height"), Is.EqualTo(900));
            Assert.That(PlayerPrefs.GetString("Game.Feature.Input.KeyboardMovementScheme"), Is.EqualTo("wasd"));
        }

        [Test]
        public void ProductionComposition_DoesNotReferenceCampaignSaveService()
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveService"));
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveService"));
            Assert.That(
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Not.Contain("CampaignSaveService"));
        }

        [Test]
        public void ServiceSource_DoesNotDeletePlayerPrefsOrCallSteamApis()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");

            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(source, Does.Not.Contain("Steamworks"));
            Assert.That(source, Does.Not.Contain("ISteamRemoteStorage"));
            Assert.That(source, Does.Not.Contain("SteamRemoteStorage"));
        }

        private static CampaignSaveService CreateService(
            RecordingRepository repository,
            ICampaignSaveResetMarkerPort resetMarkerPort = null)
        {
            return new CampaignSaveService(
                repository,
                resetMarkerPort,
                () => FixedNowUtc,
                ProfileId,
                ProductVersion);
        }

        private static CampaignProfileDocument CreateDocument(params CampaignSlotDocument[] slots)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = 1,
                ProductVersion = ProductVersion,
                SavedAtUtc = "2026-07-06T00:00:00Z",
                ProfileId = ProfileId,
                LastPlayedSlotNumber = slots.Length > 0 ? slots[0].SlotNumber : 0,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = slots,
            };
        }

        private static CampaignSlotDocument CreateSlot(
            int slotNumber,
            string stageId,
            string levelGroupId,
            int remainingChances,
            int totalDeaths = 0)
        {
            return new CampaignSlotDocument
            {
                SlotNumber = slotNumber,
                StageId = stageId,
                LevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
                TotalDeaths = totalDeaths,
                LastPlayedAtUtc = "2026-07-06T00:00:00Z",
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            public RecordingRepository(CampaignProfileDocument currentDocument = null)
            {
                CurrentDocument = currentDocument;
            }

            public CampaignProfileDocument CurrentDocument { get; set; }

            public CampaignProfileDocument SavedDocument { get; private set; }

            public int LoadCount { get; private set; }

            public int SaveCount { get; private set; }

            public CampaignProfileLoadResult Load()
            {
                LoadCount++;
                return CurrentDocument == null
                    ? new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Missing,
                        null,
                        "missing")
                    : new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Loaded,
                        CurrentDocument,
                        "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                SaveCount++;
                SavedDocument = document;
                CurrentDocument = document;
            }
        }

        private sealed class RecordingResetMarkerPort : ICampaignSaveResetMarkerPort
        {
            public int MarkCount { get; private set; }

            public string ResetTombstoneUtc { get; private set; }

            public void MarkResetImportDisabled(string resetTombstoneUtc)
            {
                MarkCount++;
                ResetTombstoneUtc = resetTombstoneUtc;
            }
        }
    }
}
