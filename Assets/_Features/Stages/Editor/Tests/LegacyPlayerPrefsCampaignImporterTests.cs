using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class LegacyPlayerPrefsCampaignImporterTests
    {
        private const string FixedNowUtc = "2026-07-06T00:00:00Z";
        private const string ProfileId = "test-profile";
        private const string ProductVersion = "test-product";

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey);
            PlayerPrefs.DeleteKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            PlayerPrefs.DeleteKey("settings.audio.master");
            PlayerPrefs.DeleteKey("settings.display.width");
            PlayerPrefs.DeleteKey("Game.Feature.Input.KeyboardMovementScheme");
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void ValidCurrentCampaignPlayerPrefsPayload_ImportsToCampaignProfileDocument()
        {
            var slot = CreateSlot(2, "stage-2-1", "level-2");
            WriteAllowlistPayload(slot);

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.Importable));
            Assert.That(result.SourceFound, Is.True);
            Assert.That(result.HasDocument, Is.True);
            Assert.That(result.Document.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(result.Document.ProductVersion, Is.EqualTo(ProductVersion));
            Assert.That(result.Document.SavedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(result.Document.Slots, Has.Length.EqualTo(SaveSlotStore.SlotCount));
            AssertSlotMatches(result.Document.Slots[1], slot);
            Assert.That(result.Document.LegacyImport.ImportedSourceHash, Is.EqualTo(result.ImportedSourceHash));
        }

        [Test]
        public void InvalidLegacyPayload_ReturnsInvalidStatusAndDoesNotDeletePlayerPrefs()
        {
            const string invalidPayload = "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":";
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, invalidPayload);
            PlayerPrefs.Save();

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.InvalidPayload));
            Assert.That(result.SourceFound, Is.True);
            Assert.That(result.HasDocument, Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo(invalidPayload));
        }

        [Test]
        public void RawCopyIsNotUsed_DocumentIsCanonicalized()
        {
            var payload = BuildPayload(CreateSlot(1, "stage-1-1", "level-1"));
            payload = payload.Insert(1, "\"RawCopySentinel\":\"raw-only\",");
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, payload);
            PlayerPrefs.Save();

            var result = CreateImporter().BuildImportCandidate();
            var documentJson = JsonUtility.ToJson(result.Document);

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.Importable));
            Assert.That(documentJson, Does.Not.Contain("RawCopySentinel"));
            Assert.That(documentJson, Does.Not.Contain("raw-only"));
        }

        [TestCase(SaveSlotPrefsKeys.LegacySaveSlotsKey, "old legacy campaign key")]
        [TestCase(EditorDirectPlayContextStore.TempSaveSlotStoreKey, "direct-play temp save key")]
        [TestCase("settings.audio.master", "audio setting key")]
        [TestCase("settings.display.width", "display setting key")]
        [TestCase("Game.Feature.Input.KeyboardMovementScheme", "input setting key")]
        public void ExcludedPlayerPrefsKeys_AreIgnored(string key, string classification)
        {
            PlayerPrefs.SetString(key, BuildPayload(CreateSlot(1, "stage-1-1", "level-1")));
            PlayerPrefs.Save();

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.Missing), classification);
            Assert.That(result.SourceFound, Is.False);
            Assert.That(PlayerPrefs.HasKey(key), Is.True);
        }

        [Test]
        public void ActiveSlotKey_IsNotImportedAsPendingLaunchState()
        {
            WriteAllowlistPayload(CreateSlot(2, "stage-2-1", "level-2"));
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 2);
            PlayerPrefs.Save();

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.Importable));
            Assert.That(result.Document.LastPlayedSlotNumber, Is.EqualTo(2));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.EqualTo(2));
        }

        [Test]
        public void LastPlayedSlotNumber_DerivesFirstNonEmptySlotWhenActiveSlotIsEmpty()
        {
            WriteAllowlistPayload(CreateSlot(1, "stage-1-1", "level-1"));
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 3);
            PlayerPrefs.Save();

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.Importable));
            Assert.That(result.Document.LastPlayedSlotNumber, Is.EqualTo(1));
        }

        [Test]
        public void ImportedSourceHash_IsStableForSamePayloadAndDiffersForDifferentPayload()
        {
            var firstPayload = BuildPayload(CreateSlot(1, "stage-1-1", "level-1"));
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, firstPayload);
            PlayerPrefs.Save();
            var first = CreateImporter().BuildImportCandidate();
            var second = CreateImporter().BuildImportCandidate();

            var secondPayload = BuildPayload(CreateSlot(1, "stage-1-2", "level-1"));
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, secondPayload);
            PlayerPrefs.Save();
            var different = CreateImporter().BuildImportCandidate();

            Assert.That(first.ImportedSourceHash, Is.EqualTo(second.ImportedSourceHash));
            Assert.That(first.ImportedSourceHash, Is.Not.EqualTo(different.ImportedSourceHash));
        }

        [Test]
        public void ImportDisabledMarker_BlocksImport()
        {
            WriteAllowlistPayload(CreateSlot(1, "stage-1-1", "level-1"));
            new CampaignLegacyImportMarkerStore().SetImportDisabled(true);

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.ImportDisabled));
            Assert.That(result.ImportDisabled, Is.True);
            Assert.That(result.HasDocument, Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
        }

        [Test]
        public void NoFileAndNoLegacyPlayerPrefs_ReturnsNoImportCandidate()
        {
            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.Missing));
            Assert.That(result.SourceFound, Is.False);
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void MarkerStore_RecordsAndReadsImportDisabled()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();

            markerStore.SetImportDisabled(true);

            Assert.That(markerStore.IsImportDisabled(), Is.True);
        }

        [Test]
        public void MarkerStore_RecordsAndReadsImportedSourceHash()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();

            markerStore.SetImportedSourceHash("source-hash");

            Assert.That(markerStore.GetImportedSourceHash(), Is.EqualTo("source-hash"));
        }

        [Test]
        public void MarkerStore_RecordsAndReadsResetTombstone()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();

            markerStore.SetResetTombstoneUtc("2026-07-06T12:00:00Z");

            Assert.That(markerStore.GetResetTombstoneUtc(), Is.EqualTo("2026-07-06T12:00:00Z"));
            Assert.That(markerStore.HasResetTombstone(), Is.True);
        }

        [Test]
        public void MarkerStore_RecordsAndReadsDeletedSlotGuards()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();

            markerStore.RecordDeletedSlotGuard(
                2,
                "source-hash",
                "2026-07-06T12:30:00Z",
                "DeleteSlot");

            var guards = markerStore.ReadDeletedSlotGuards();
            Assert.That(guards, Has.Length.EqualTo(1));
            Assert.That(guards[0].SlotNumber, Is.EqualTo(2));
            Assert.That(guards[0].ImportedSourceHash, Is.EqualTo("source-hash"));
            Assert.That(guards[0].DeletedAtUtc, Is.EqualTo("2026-07-06T12:30:00Z"));
            Assert.That(guards[0].Reason, Is.EqualTo("DeleteSlot"));
        }

        [Test]
        public void MarkerStore_InvalidDeletedSlotGuardNumberIsIgnored()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();

            markerStore.RecordDeletedSlotGuard(
                99,
                "source-hash",
                "2026-07-06T12:30:00Z",
                "DeleteSlot");

            Assert.That(markerStore.ReadDeletedSlotGuards(), Is.Empty);
        }

        [Test]
        public void ResetTombstoneMarker_BlocksImport()
        {
            WriteAllowlistPayload(CreateSlot(1, "stage-1-1", "level-1"));
            new CampaignLegacyImportMarkerStore().SetResetTombstoneUtc("2026-07-06T12:00:00Z");

            var result = CreateImporter().BuildImportCandidate();

            Assert.That(result.Status, Is.EqualTo(CampaignLegacyImportStatus.ImportDisabled));
            Assert.That(result.ImportDisabled, Is.True);
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void ImporterDoesNotWriteProfileJson()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/LegacyPlayerPrefsCampaignImporter.cs");

            Assert.That(source, Does.Not.Contain("FileCampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("IAtomicTextFileStore"));
            Assert.That(source, Does.Not.Contain("AtomicTextFileStore"));
            Assert.That(source, Does.Not.Contain("FileSaveSlotStorageBackend"));
            Assert.That(source, Does.Not.Contain("profile.json"));
        }

        [Test]
        public void ImporterDoesNotInvokeSaveSlotStoreDestructiveResetPath()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/LegacyPlayerPrefsCampaignImporter.cs");

            Assert.That(source, Does.Not.Contain("LoadAll("));
            Assert.That(source, Does.Not.Contain("LoadSlot("));
            Assert.That(source, Does.Not.Contain("ClearAll("));
            Assert.That(source, Does.Not.Contain("DeleteSlot("));
            Assert.That(source, Does.Not.Contain("ResetRejectedPayload("));
            Assert.That(source, Does.Not.Contain("StageClearSavePrefsResetPolicy"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"));
        }

        [Test]
        public void CampaignLegacyImportStatus_CoversExpectedValues()
        {
            Assert.That(
                Enum.GetValues(typeof(CampaignLegacyImportStatus)),
                Is.EquivalentTo(new[]
                {
                    CampaignLegacyImportStatus.Missing,
                    CampaignLegacyImportStatus.Importable,
                    CampaignLegacyImportStatus.InvalidPayload,
                    CampaignLegacyImportStatus.UnsupportedSchema,
                    CampaignLegacyImportStatus.ImportDisabled,
                    CampaignLegacyImportStatus.EmptyPayload,
                    CampaignLegacyImportStatus.ParseFailed,
                    CampaignLegacyImportStatus.MappingFailed,
                }));
        }

        private static LegacyPlayerPrefsCampaignImporter CreateImporter()
        {
            return new LegacyPlayerPrefsCampaignImporter(
                new CampaignLegacySourceReader(),
                new CampaignLegacyImportMarkerStore(),
                () => FixedNowUtc,
                ProfileId,
                ProductVersion);
        }

        private static void WriteAllowlistPayload(params SaveSlotData[] slots)
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildPayload(slots));
            PlayerPrefs.Save();
        }

        private static string BuildPayload(params SaveSlotData[] slots)
        {
            return JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(slots));
        }

        private static SaveSlotData CreateSlot(int slotNumber, string stageId, string levelGroupId)
        {
            var parsedStageId = StageId.CreateOrThrow(stageId);
            var slot = new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = parsedStageId,
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = 2,
                CampaignCompleted = false,
                IntroComicCompleted = true,
                OutroComicCompleted = false,
                TotalDeaths = 4,
                LastPlayedAt = "2026-07-06T11:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot
                {
                    Version = 5,
                },
            };

            slot.StageClearProfileSnapshot.ClearRecordsByStageId[parsedStageId] =
                new PlayerStageClearRecord
                {
                    StageId = parsedStageId,
                    HasAttempted = true,
                    HasCleared = true,
                    ClearCount = 2,
                    ProcessedStageRunIds = new[] { "run-b", "run-a" },
                };
            slot.StageClearProfileSnapshot.ProcessedStageRunIds.Add("run-b");
            slot.StageClearProfileSnapshot.ProcessedStageRunIds.Add("run-a");
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds.Add("attempt-b");
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds.Add("attempt-a");
            return slot;
        }

        private static void AssertSlotMatches(CampaignSlotDocument document, SaveSlotData slot)
        {
            Assert.That(document.SlotNumber, Is.EqualTo(slot.SlotNumber));
            Assert.That(document.StageId, Is.EqualTo(slot.CurrentStageId.Value));
            Assert.That(document.LevelGroupId, Is.EqualTo(slot.CurrentLevelGroupId));
            Assert.That(document.RemainingChances, Is.EqualTo(slot.RemainingChances));
            Assert.That(document.CampaignCompleted, Is.EqualTo(slot.CampaignCompleted));
            Assert.That(document.IntroComicCompleted, Is.EqualTo(slot.IntroComicCompleted));
            Assert.That(document.OutroComicCompleted, Is.EqualTo(slot.OutroComicCompleted));
            Assert.That(document.TotalDeaths, Is.EqualTo(slot.TotalDeaths));
            Assert.That(document.LastPlayedAtUtc, Is.EqualTo(slot.LastPlayedAt));
            Assert.That(document.StageClearProfileSnapshot.Version, Is.EqualTo(5));
            Assert.That(document.StageClearProfileSnapshot.Records, Has.Length.EqualTo(1));
            Assert.That(document.StageClearProfileSnapshot.Records[0].StageId, Is.EqualTo(slot.CurrentStageId.Value));
            Assert.That(document.StageClearProfileSnapshot.Records[0].HasAttempted, Is.True);
            Assert.That(document.StageClearProfileSnapshot.Records[0].HasCleared, Is.True);
            Assert.That(document.StageClearProfileSnapshot.Records[0].ClearCount, Is.EqualTo(2));
            Assert.That(document.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Is.EqualTo(new[] { "run-b", "run-a" }));
            Assert.That(document.StageClearProfileSnapshot.ProcessedStageRunIds, Is.EqualTo(new[] { "run-a", "run-b" }));
            Assert.That(document.StageClearProfileSnapshot.ProcessedClearAttemptIds, Is.EqualTo(new[] { "attempt-a", "attempt-b" }));
        }
    }
}
