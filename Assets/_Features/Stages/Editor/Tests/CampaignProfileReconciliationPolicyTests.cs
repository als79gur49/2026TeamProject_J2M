using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReconciliationPolicyTests
    {
        private string _saveKey;

        [SetUp]
        public void SetUp()
        {
            _saveKey = "Game.Feature.Stages.Editor.Tests.CampaignProfileReconciliationPolicyTests." +
                       Guid.NewGuid().ToString("N");
            PlayerPrefs.DeleteKey(_saveKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_saveKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void PlayerPrefsValidSlots_ProfileMissing_SaveSlotStoreStillWinsUx()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(2, "stage-2-1");

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            Assert.That(slots[1].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-1")));
            Assert.That(slots.Count(slot => !slot.IsEmpty), Is.EqualTo(1));
        }

        [Test]
        public void PlayerPrefsValidSlots_ProfileStale_SaveSlotStoreStillWinsUxAndProbeOnlyReportsMetadata()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(1, "stage-1-1");
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 3,
                importedSourceHash: "old-hash",
                slots: CreateProfileSlot(3, "stage-3-1")));

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(probeResult.LastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(probeResult.ImportedSourceHash, Is.EqualTo("old-hash"));
            Assert.That(slots[0].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-1-1")));
            Assert.That(slots[2].IsEmpty, Is.True);
        }

        [Test]
        public void ProfileLastPlayedPointingEmptyOrDeletedSlot_IsDiagnosticsOnly()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(1, "stage-1-1");
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 2,
                importedSourceHash: "source-hash",
                deletedSlotGuards: new[]
                {
                    new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = 2,
                        ImportedSourceHash = "source-hash",
                        DeletedAtUtc = "2026-07-08T01:02:03.0000000Z",
                        Reason = "deleted",
                    },
                },
                slots: CreateProfileSlot(2, "stage-2-1")));

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.LastPlayedSlotNumber, Is.EqualTo(2));
            Assert.That(probeResult.DeletedSlotGuardCount, Is.EqualTo(1));
            Assert.That(slots[0].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-1-1")));
            Assert.That(slots[1].IsEmpty, Is.True);
        }

        [Test]
        public void LastPlayedSlotNumberNotInSaveSlotStore_IsDiagnosticsMismatchOnly()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(1, "stage-1-1");
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 3,
                importedSourceHash: "source-hash",
                slots: CreateProfileSlot(3, "stage-3-1")));

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(probeResult.LastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(slots[0].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-1-1")));
            Assert.That(slots[1].IsEmpty, Is.True);
            Assert.That(slots[2].IsEmpty, Is.True);
        }

        [Test]
        public void ProfileCorrupt_SaveSlotStoreStillWinsUxAndProbeDoesNotMutateProfile()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(1, "stage-1-1");
            harness.WriteRawProfile("{\"SchemaVersion\":");

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Corrupt));
            Assert.That(harness.ReadRawProfile(), Is.EqualTo("{\"SchemaVersion\":"));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.corrupt.*"), Is.Empty);
            Assert.That(slots[0].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-1-1")));
        }

        [Test]
        public void ImportedSourceHashMismatch_IsDiagnosticsWarningOnly()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(2, "stage-2-1");
            var currentLegacyHash = LegacyPlayerPrefsCampaignImporter.ComputeImportedSourceHash(
                PlayerPrefs.GetString(_saveKey));
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 1,
                importedSourceHash: "stale-hash",
                slots: CreateProfileSlot(1, "stage-1-1")));

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.ImportedSourceHash, Is.EqualTo("stale-hash"));
            Assert.That(probeResult.ImportedSourceHash, Is.Not.EqualTo(currentLegacyHash));
            Assert.That(slots[1].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-1")));
            Assert.That(slots[0].IsEmpty, Is.True);
        }

        [Test]
        public void ImportDisabledAndResetTombstone_DoNotBlockCurrentPlayerPrefsUx()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(3, "stage-3-1");
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 1,
                importedSourceHash: "source-hash",
                importDisabled: true,
                resetTombstoneUtc: "2026-07-08T01:02:03.0000000Z",
                slots: CreateProfileSlot(1, "stage-1-1")));
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.SetImportDisabled(true);
            markerStore.SetResetTombstoneUtc("2026-07-08T01:02:03.0000000Z");

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.ImportDisabled, Is.True);
            Assert.That(probeResult.HasResetTombstone, Is.True);
            Assert.That(slots[2].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-3-1")));
            Assert.That(slots[0].IsEmpty, Is.True);
        }

        [Test]
        public void DeletedSlotGuards_DoNotHideCurrentPlayerPrefsSlotVisibility()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(2, "stage-2-1");
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 2,
                importedSourceHash: "source-hash",
                deletedSlotGuards: new[]
                {
                    new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = 2,
                        ImportedSourceHash = "source-hash",
                        DeletedAtUtc = "2026-07-08T01:02:03.0000000Z",
                        Reason = "profile-deleted-guard",
                    },
                },
                slots: CreateProfileSlot(1, "stage-1-1")));

            var slots = store.LoadAll();
            var probeResult = harness.Probe.Probe();

            Assert.That(probeResult.DeletedSlotGuardCount, Is.EqualTo(1));
            Assert.That(slots[1].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-1")));
            Assert.That(slots[0].IsEmpty, Is.True);
            Assert.That(slots.Count(slot => !slot.IsEmpty), Is.EqualTo(1));
        }

        private SaveSlotStore CreateStoreWithPlayerPrefsSlot(int slotNumber, string stageId)
        {
            var store = new SaveSlotStore(_saveKey);
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = $"level-{slotNumber}",
                RemainingChances = 2,
                LastPlayedAt = "2026-07-08T00:00:00.0000000Z",
            });
            return store;
        }

        private static CampaignProfileDocument CreateProfile(
            int lastPlayedSlotNumber,
            string importedSourceHash,
            bool importDisabled = false,
            string resetTombstoneUtc = "",
            CampaignLegacyDeletedSlotGuardDocument[] deletedSlotGuards = null,
            params CampaignSlotDocument[] slots)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = "tests",
                SavedAtUtc = "2026-07-08T00:00:00.0000000Z",
                ProfileId = "profile-tests",
                LastPlayedSlotNumber = lastPlayedSlotNumber,
                LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportedSourceHash = importedSourceHash,
                    ImportDisabled = importDisabled,
                    ResetTombstoneUtc = resetTombstoneUtc,
                    DeletedSlotGuards = deletedSlotGuards ?? Array.Empty<CampaignLegacyDeletedSlotGuardDocument>(),
                },
                Slots = slots ?? Array.Empty<CampaignSlotDocument>(),
            };
        }

        private static CampaignSlotDocument CreateProfileSlot(int slotNumber, string stageId)
        {
            return new CampaignSlotDocument
            {
                SlotNumber = slotNumber,
                StageId = stageId,
                LevelGroupId = $"level-{slotNumber}",
                RemainingChances = 3,
                LastPlayedAtUtc = "2026-07-08T00:00:00.0000000Z",
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReconciliationPolicyTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
                Probe = new CampaignProfileMetadataProbe(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public CampaignProfileMetadataProbe Probe { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public void WriteProfile(CampaignProfileDocument document)
            {
                WriteRawProfile(JsonUtility.ToJson(document));
            }

            public void WriteRawProfile(string rawProfile)
            {
                Directory.CreateDirectory(SaveRootPath);
                File.WriteAllText(ProfilePath, rawProfile);
            }

            public string ReadRawProfile()
            {
                return File.ReadAllText(ProfilePath);
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
    }
}
