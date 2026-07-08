using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessCiNonBlockingTests
    {
        private const string CiGuidePath = "Docs/Testing/Save-Readiness-CI-Guide.md";

        private string _saveKey;

        [SetUp]
        public void SetUp()
        {
            _saveKey = "Game.Feature.Stages.Editor.Tests.CampaignProfileReadinessCiNonBlockingTests." +
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
        public void MissingProfile_ProducesNonBlockingReadinessReportContent()
        {
            using var harness = new ProfileHarness();

            var report = harness.BuildReport();
            var markdown = report.ToMarkdown();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            AssertNonBlockingPolicy(markdown);
            Assert.That(markdown, Does.Contain("metadata load status: Missing"));
            Assert.That(markdown, Does.Contain("profile.json exists/missing for diagnostics: missing for diagnostics"));
        }

        [Test]
        public void CorruptProfile_ProducesNonBlockingReadinessReportContent()
        {
            using var harness = new ProfileHarness();
            harness.WriteRawProfile("{\"SchemaVersion\":");

            var report = harness.BuildReport();
            var markdown = report.ToMarkdown();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Corrupt));
            AssertNonBlockingPolicy(markdown);
            Assert.That(markdown, Does.Contain("metadata load status: Corrupt"));
            Assert.That(markdown, Does.Contain("Corrupt or invalid profile metadata is a diagnostics/readiness finding only"));
        }

        [Test]
        public void StaleProfileLastPlayedAndSourceHashMismatch_AreNonBlockingDiagnostics()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(1, "stage-1-1");
            var currentSourceHash = LegacyPlayerPrefsCampaignImporter.ComputeImportedSourceHash(
                PlayerPrefs.GetString(_saveKey));
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 3,
                importedSourceHash: "stale-source-hash",
                slots: CreateSlot(3, "stage-3-1")));

            var report = harness.BuildReport();
            var markdown = report.ToMarkdown();
            var slots = store.LoadAll();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(report.DiagnosticLastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(report.ImportedSourceHash, Is.EqualTo("stale-source-hash"));
            Assert.That(report.ImportedSourceHash, Is.Not.EqualTo(currentSourceHash));
            Assert.That(slots[0].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-1-1")));
            Assert.That(slots[2].IsEmpty, Is.True);
            AssertNonBlockingPolicy(markdown);
            Assert.That(markdown, Does.Contain("Diagnostic LastPlayedSlotNumber is metadata only"));
            Assert.That(markdown, Does.Contain("imported source hash diagnostic: present"));
        }

        [Test]
        public void ResetTombstoneAndDeletedGuards_AreNonBlockingDiagnostics()
        {
            using var harness = new ProfileHarness();
            var store = CreateStoreWithPlayerPrefsSlot(2, "stage-2-1");
            harness.WriteProfile(CreateProfile(
                lastPlayedSlotNumber: 1,
                importedSourceHash: "source-hash",
                importDisabled: true,
                resetTombstoneUtc: "2026-07-08T01:02:03.0000000Z",
                deletedSlotGuards: new[]
                {
                    new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = 2,
                        ImportedSourceHash = "source-hash",
                        DeletedAtUtc = "2026-07-08T02:03:04.0000000Z",
                        Reason = "delete-slot",
                    },
                },
                slots: CreateSlot(1, "stage-1-1")));

            var report = harness.BuildReport();
            var markdown = report.ToMarkdown();
            var slots = store.LoadAll();

            Assert.That(report.ImportDisabledMarkerMetadata, Is.True);
            Assert.That(report.HasResetTombstoneMarkerMetadata, Is.True);
            Assert.That(report.DeletedSlotGuardCount, Is.EqualTo(1));
            Assert.That(slots[1].CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-1")));
            AssertNonBlockingPolicy(markdown);
            Assert.That(markdown, Does.Contain("import/reset marker metadata is inventory, not a PlayerPrefs UX decision"));
        }

        [Test]
        public void CiGuide_DocumentsNonBlockingDiagnosticsPolicy()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("report findings do not block builds or releases"));
            Assert.That(guide, Does.Contain("CI may fail for:"));
            Assert.That(guide, Does.Contain("Compile error."));
            Assert.That(guide, Does.Contain("Test failure."));
            Assert.That(guide, Does.Contain("Report writer contract violation."));
            Assert.That(guide, Does.Contain("Invalid artifact path accepted by the writer."));
            Assert.That(guide, Does.Contain("Forbidden wording."));
            Assert.That(guide, Does.Contain("Forbidden runtime consumer."));
            Assert.That(guide, Does.Contain("Report generation exception."));
            Assert.That(guide, Does.Contain("CI and release must not fail for valid diagnostics findings"));
            Assert.That(guide, Does.Contain("Profile missing."));
            Assert.That(guide, Does.Contain("Profile corrupt."));
            Assert.That(guide, Does.Contain("Profile stale."));
            Assert.That(guide, Does.Contain("`LastPlayedSlotNumber` mismatch."));
            Assert.That(guide, Does.Contain("`importedSourceHash` mismatch."));
            Assert.That(guide, Does.Contain("Reset tombstone present."));
            Assert.That(guide, Does.Contain("Deleted guards present."));
            Assert.That(guide, Does.Contain("Any valid diagnostics warning emitted by the readiness report."));
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
                SchemaVersion = 1,
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

        private static CampaignSlotDocument CreateSlot(int slotNumber, string stageId)
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

        private static void AssertNonBlockingPolicy(string markdown)
        {
            Assert.That(markdown, Does.Contain("Report findings are diagnostics/readiness-only."));
            Assert.That(markdown, Does.Contain("Report findings do not block build or release."));
            Assert.That(markdown, Does.Contain("Current production UX truth remains SaveSlotStore / PlayerPrefs."));
            Assert.That(markdown, Does.Not.Contain("release blocker"));
            Assert.That(markdown, Does.Not.Contain("build blocker"));
            Assert.That(markdown, Does.Not.Contain("blocks release"));
            Assert.That(markdown, Does.Not.Contain("must fix before release"));
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadinessCiNonBlockingTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public CampaignProfileReadinessReport BuildReport()
            {
                return new CampaignProfileReadinessReportBuilder()
                    .Build(new CampaignProfileReadinessReportOptions(SaveRootPath));
            }

            public void WriteProfile(CampaignProfileDocument document)
            {
                WriteRawProfile(JsonUtility.ToJson(document));
            }

            public void WriteRawProfile(string rawProfile)
            {
                Directory.CreateDirectory(SaveRootPath);
                File.WriteAllText(ProfilePath, rawProfile);
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
