using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SaveSlotStoreCompatibilityAdapterTests
    {
        private const string FixedNowUtc = "2026-07-07T00:00:00Z";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(CreatePrefsKey("saves"));
            PlayerPrefs.DeleteKey(CreatePrefsKey("active"));
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void LoadAll_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));
            legacy.SaveSlot(CreateSlot(3, "stage-3-1", "level-3", 1));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));
            adapter.SaveSlot(CreateSlot(3, "stage-3-1", "level-3", 1));

            AssertSlotsEquivalent(legacy.LoadAll(), adapter.LoadAll());
            Assert.That(adapter.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.Current));
        }

        [Test]
        public void LoadSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 1));
            adapter.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 1));

            AssertSlotEquivalent(legacy.LoadSlot(2), adapter.LoadSlot(2));
        }

        [Test]
        public void SaveSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            var slot = CreateSlot(1, "stage-1-1", "level-1", 2);
            slot.IntroPlayed = true;
            slot.TotalDeaths = 4;

            legacy.SaveSlot(slot);
            adapter.SaveSlot(slot);

            AssertSlotEquivalent(legacy.LoadSlot(1), adapter.LoadSlot(1));
        }

        [Test]
        public void InitializeNewGame_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            var resolver = new CampaignStageSequenceResolver(
                CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());

            var legacySlot = legacy.InitializeNewGame(1, resolver, FixedNowUtc);
            var adapterSlot = adapter.InitializeNewGame(1, resolver, FixedNowUtc);

            AssertSlotEquivalent(legacySlot, adapterSlot);
        }

        [Test]
        public void UpdateSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));

            legacy.UpdateSlot(1, slot =>
            {
                slot.CurrentStageId = StageId.CreateOrThrow("stage-1-2");
                slot.RemainingChances = 1;
                slot.TotalDeaths = 2;
                slot.StageClearProfileSnapshot.Version = 5;
            });
            adapter.UpdateSlot(1, slot =>
            {
                slot.CurrentStageId = StageId.CreateOrThrow("stage-1-2");
                slot.RemainingChances = 1;
                slot.TotalDeaths = 2;
                slot.StageClearProfileSnapshot.Version = 5;
            });

            AssertSlotEquivalent(legacy.LoadSlot(1), adapter.LoadSlot(1));
        }

        [Test]
        public void DeleteSlot_Parity()
        {
            var legacy = CreateLegacyStore();
            var adapter = CreateAdapter();
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            legacy.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 2));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            adapter.SaveSlot(CreateSlot(2, "stage-2-1", "level-2", 2));

            legacy.DeleteSlot(1);
            adapter.DeleteSlot(1);

            AssertSlotsEquivalent(legacy.LoadAll(), adapter.LoadAll());
        }

        [Test]
        public void ClearAll_Parity()
        {
            var legacy = CreateLegacyStore();
            var repository = new RecordingRepository();
            var adapter = CreateAdapter(repository);
            legacy.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));
            adapter.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 3));

            legacy.ClearAll();
            adapter.ClearAll();

            AssertSlotsEquivalent(legacy.LoadAll(), adapter.LoadAll());
            Assert.That(repository.SavedDocument.Slots, Is.Empty);
            Assert.That(repository.SavedDocument.LegacyImport.ImportDisabled, Is.True);
        }

        [Test]
        public void SaveSlotStorePublicConstructor_DefaultRemainsPlayerPrefs()
        {
            var store = new SaveSlotStore();

            store.SaveSlot(CreateSlot(1, "stage-1-1", "level-1", 2));

            Assert.That(store.PlayerPrefsKey, Is.EqualTo(SaveSlotStore.DefaultPlayerPrefsKey));
            Assert.That(SaveSlotStore.DefaultPlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(PlayerPrefs.HasKey(SaveSlotStore.DefaultPlayerPrefsKey), Is.True);
        }

        [Test]
        public void ProductionComposition_DoesNotReferenceCompatibilityAdapter()
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain("SaveSlotStoreCompatibilityAdapter"));
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Not.Contain("SaveSlotStoreCompatibilityAdapter"));
            Assert.That(
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Not.Contain("SaveSlotStoreCompatibilityAdapter"));
        }

        private static SaveSlotStore CreateLegacyStore()
        {
            return new SaveSlotStore(CreatePrefsKey("saves"), CreatePrefsKey("active"));
        }

        private static SaveSlotStoreCompatibilityAdapter CreateAdapter(RecordingRepository repository = null)
        {
            repository ??= new RecordingRepository();
            var service = new CampaignSaveService(
                repository,
                new RecordingResetMarkerPort(),
                () => FixedNowUtc,
                "adapter-test-profile",
                "adapter-test-product");
            return new SaveSlotStoreCompatibilityAdapter(service);
        }

        private static SaveSlotData CreateSlot(
            int slotNumber,
            string stageId,
            string levelGroupId,
            int remainingChances)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
                LastPlayedAt = FixedNowUtc,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static void AssertSlotsEquivalent(SaveSlotData[] expected, SaveSlotData[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                AssertSlotEquivalent(expected[i], actual[i]);
            }
        }

        private static void AssertSlotEquivalent(SaveSlotData expected, SaveSlotData actual)
        {
            Assert.That(actual.SlotNumber, Is.EqualTo(expected.SlotNumber));
            Assert.That(actual.CurrentStageId, Is.EqualTo(expected.CurrentStageId));
            Assert.That(actual.CurrentLevelGroupId, Is.EqualTo(expected.CurrentLevelGroupId));
            Assert.That(actual.RemainingChances, Is.EqualTo(expected.RemainingChances));
            Assert.That(actual.CampaignCompleted, Is.EqualTo(expected.CampaignCompleted));
            Assert.That(actual.IntroPlayed, Is.EqualTo(expected.IntroPlayed));
            Assert.That(actual.OutroPlayed, Is.EqualTo(expected.OutroPlayed));
            Assert.That(actual.TotalDeaths, Is.EqualTo(expected.TotalDeaths));
            Assert.That(actual.LastPlayedAt, Is.EqualTo(expected.LastPlayedAt));
            Assert.That(
                actual.StageClearProfileSnapshot.Version,
                Is.EqualTo(expected.StageClearProfileSnapshot.Version));
            Assert.That(
                actual.StageClearProfileSnapshot.ClearRecordsByStageId.Count,
                Is.EqualTo(expected.StageClearProfileSnapshot.ClearRecordsByStageId.Count));
            Assert.That(
                actual.StageClearProfileSnapshot.ProcessedStageRunIds,
                Is.EquivalentTo(expected.StageClearProfileSnapshot.ProcessedStageRunIds));
            Assert.That(
                actual.StageClearProfileSnapshot.ProcessedClearAttemptIds,
                Is.EquivalentTo(expected.StageClearProfileSnapshot.ProcessedClearAttemptIds));
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.Stages.Tests.SaveSlotStoreCompatibilityAdapter." + suffix;
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            public CampaignProfileDocument CurrentDocument { get; set; }

            public CampaignProfileDocument SavedDocument { get; private set; }

            public CampaignProfileLoadResult Load()
            {
                return CurrentDocument == null
                    ? new CampaignProfileLoadResult(CampaignProfileLoadStatus.Missing, null, "missing")
                    : new CampaignProfileLoadResult(CampaignProfileLoadStatus.Loaded, CurrentDocument, "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                SavedDocument = document;
                CurrentDocument = document;
            }
        }

        private sealed class RecordingResetMarkerPort : ICampaignSaveResetMarkerPort
        {
            public void MarkResetImportDisabled(string resetTombstoneUtc)
            {
            }
        }
    }
}
