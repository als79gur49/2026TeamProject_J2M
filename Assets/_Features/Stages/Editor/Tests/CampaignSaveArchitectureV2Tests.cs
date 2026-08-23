using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveArchitectureV2Tests
    {
        [Test]
        public void CampaignProfileDocument_CanBeDefaultConstructed()
        {
            var document = new CampaignProfileDocument();

            Assert.That(document.SchemaVersion, Is.EqualTo(0));
            Assert.That(document.Slots, Is.Not.Null);
            Assert.That(document.Slots, Is.Empty);
        }

        [Test]
        public void CampaignProfileDocument_RoundTripsThroughJsonUtility()
        {
            var document = new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = "test-product",
                SavedAtUtc = "2026-07-06T09:00:00Z",
                ProfileId = "profile-a",
                LastPlayedSlotNumber = 2,
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 2,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 3,
                        CampaignCompleted = false,
                        IntroComicCompleted = true,
                        OutroComicCompleted = true,
                        TotalDeaths = 5,
                        LastPlayedAtUtc = "2026-07-06T11:00:00Z",
                        StageClearProfileSnapshot = new CampaignStageClearProfileDocument
                        {
                            Version = 7,
                            Records = new[]
                            {
                                new PlayerStageClearRecordDocument
                                {
                                    StageId = "stage-1-1",
                                    HasAttempted = true,
                                    HasCleared = true,
                                    ClearCount = 2,
                                    ProcessedStageRunIds = new[] { "run-a" },
                                },
                            },
                            ProcessedStageRunIds = new[] { "run-a" },
                            ProcessedClearAttemptIds = new[] { "attempt-a" },
                        },
                    },
                },
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignProfileDocument>(json);

            Assert.That(roundTripped.SchemaVersion, Is.EqualTo(CampaignProfileDocument.CurrentSchemaVersion));
            Assert.That(roundTripped.ProductVersion, Is.EqualTo("test-product"));
            Assert.That(roundTripped.SavedAtUtc, Is.EqualTo("2026-07-06T09:00:00Z"));
            Assert.That(roundTripped.ProfileId, Is.EqualTo("profile-a"));
            Assert.That(roundTripped.LastPlayedSlotNumber, Is.EqualTo(2));
            Assert.That(roundTripped.Slots, Has.Length.EqualTo(1));
            Assert.That(roundTripped.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(roundTripped.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(roundTripped.Slots[0].LevelGroupId, Is.EqualTo("level-1"));
            Assert.That(roundTripped.Slots[0].RemainingChances, Is.EqualTo(3));
            Assert.That(roundTripped.Slots[0].CampaignCompleted, Is.False);
            Assert.That(roundTripped.Slots[0].IntroComicCompleted, Is.True);
            Assert.That(roundTripped.Slots[0].OutroComicCompleted, Is.True);
            Assert.That(roundTripped.Slots[0].TotalDeaths, Is.EqualTo(5));
            Assert.That(roundTripped.Slots[0].LastPlayedAtUtc, Is.EqualTo("2026-07-06T11:00:00Z"));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Version, Is.EqualTo(7));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Records, Has.Length.EqualTo(1));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Records[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Records[0].HasAttempted, Is.True);
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Records[0].HasCleared, Is.True);
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Records[0].ClearCount, Is.EqualTo(2));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.Slots[0].StageClearProfileSnapshot.ProcessedClearAttemptIds, Does.Contain("attempt-a"));
        }

        [Test]
        public void CampaignSlotDocument_RoundTripsExtendedSlotFieldsThroughJsonUtility()
        {
            var document = new CampaignSlotDocument
            {
                SlotNumber = 1,
                StageId = "stage-2-1",
                LevelGroupId = "level-2",
                RemainingChances = 1,
                CampaignCompleted = true,
                IntroComicCompleted = true,
                OutroComicCompleted = true,
                TotalDeaths = 12,
                LastPlayedAtUtc = "2026-07-06T12:00:00Z",
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignSlotDocument>(json);

            Assert.That(roundTripped.IntroComicCompleted, Is.True);
            Assert.That(roundTripped.OutroComicCompleted, Is.True);
            Assert.That(roundTripped.TotalDeaths, Is.EqualTo(12));
        }

        [Test]
        public void CampaignSlotDocument_RoundTripsStageClearProfileRecordsThroughJsonUtility()
        {
            var document = new CampaignSlotDocument
            {
                SlotNumber = 1,
                StageId = "stage-3-1",
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument
                {
                    Version = 3,
                    Records = new[]
                    {
                        new PlayerStageClearRecordDocument
                        {
                            StageId = "stage-3-1",
                            HasAttempted = true,
                            HasCleared = true,
                            ClearCount = 4,
                            ProcessedStageRunIds = new[] { "run-a", "run-b" },
                        },
                    },
                    ProcessedStageRunIds = new[] { "run-a", "run-b" },
                    ProcessedClearAttemptIds = new[] { "attempt-a" },
                },
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignSlotDocument>(json);

            Assert.That(roundTripped.StageClearProfileSnapshot.Version, Is.EqualTo(3));
            Assert.That(roundTripped.StageClearProfileSnapshot.Records, Has.Length.EqualTo(1));
            Assert.That(roundTripped.StageClearProfileSnapshot.Records[0].StageId, Is.EqualTo("stage-3-1"));
            Assert.That(roundTripped.StageClearProfileSnapshot.Records[0].HasAttempted, Is.True);
            Assert.That(roundTripped.StageClearProfileSnapshot.Records[0].HasCleared, Is.True);
            Assert.That(roundTripped.StageClearProfileSnapshot.Records[0].ClearCount, Is.EqualTo(4));
            Assert.That(roundTripped.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Does.Contain("run-b"));
            Assert.That(roundTripped.StageClearProfileSnapshot.ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.StageClearProfileSnapshot.ProcessedStageRunIds, Does.Contain("run-b"));
            Assert.That(roundTripped.StageClearProfileSnapshot.ProcessedClearAttemptIds, Does.Contain("attempt-a"));
        }

        [Test]
        public void CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss()
        {
            WriteFieldInventory();
            var slot = CreateLegacySlotFixture();

            var document = CampaignProfileDocumentMapper.ToDocument(
                new[] { slot },
                "profile-lossless",
                slot.SlotNumber,
                "2026-07-06T13:00:00Z",
                "test-product");

            Assert.That(document.SchemaVersion, Is.EqualTo(CampaignProfileDocument.CurrentSchemaVersion));
            Assert.That(document.ProductVersion, Is.EqualTo("test-product"));
            Assert.That(document.SavedAtUtc, Is.EqualTo("2026-07-06T13:00:00Z"));
            Assert.That(document.ProfileId, Is.EqualTo("profile-lossless"));
            Assert.That(document.LastPlayedSlotNumber, Is.EqualTo(slot.SlotNumber));
            Assert.That(document.Slots, Has.Length.EqualTo(1));
            AssertSlotMatchesLegacySlot(document.Slots[0], slot);
        }

        [Test]
        public void NormalStagePerformanceRecord_RoundTripsAndKeepsBestCombinedCount()
        {
            var stageId = StageId.CreateOrThrow("stage-1-2");
            var records = NormalStagePerformanceRecordPolicy.UpsertBest(
                Array.Empty<NormalStagePerformanceRecord>(),
                stageId,
                25);
            records = NormalStagePerformanceRecordPolicy.UpsertBest(records, stageId, 30);
            records = NormalStagePerformanceRecordPolicy.UpsertBest(records, stageId, 24);

            var documents = CampaignProfileDocumentMapper.ToPerformanceRecordDocuments(records);
            var roundTripped = CampaignProfileDocumentMapper.ToPerformanceRecords(documents);

            Assert.That(roundTripped, Has.Length.EqualTo(1));
            Assert.That(roundTripped[0].StageId, Is.EqualTo(stageId));
            Assert.That(roundTripped[0].BestCombinedPushFlipUses, Is.EqualTo(24));
        }

        [Test]
        public void CampaignProfileDocumentMapper_NullStageClearProfileMapsToEmptyDocument()
        {
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
            };
            slot.StageClearProfileSnapshot = null;

            var document = CampaignProfileDocumentMapper.ToSlotDocument(slot);

            Assert.That(document.StageClearProfileSnapshot, Is.Not.Null);
            Assert.That(document.StageClearProfileSnapshot.Version, Is.EqualTo(0));
            Assert.That(document.StageClearProfileSnapshot.Records, Is.Not.Null);
            Assert.That(document.StageClearProfileSnapshot.Records, Is.Empty);
            Assert.That(document.StageClearProfileSnapshot.ProcessedStageRunIds, Is.Not.Null);
            Assert.That(document.StageClearProfileSnapshot.ProcessedStageRunIds, Is.Empty);
            Assert.That(document.StageClearProfileSnapshot.ProcessedClearAttemptIds, Is.Not.Null);
            Assert.That(document.StageClearProfileSnapshot.ProcessedClearAttemptIds, Is.Empty);
        }

        [Test]
        public void CampaignProfileDocumentMapper_EmptyStageClearProfileMapsDeterministically()
        {
            var document = CampaignProfileDocumentMapper.ToStageClearProfileDocument(new StageClearProfileSnapshot());

            Assert.That(document, Is.Not.Null);
            Assert.That(document.Version, Is.EqualTo(0));
            Assert.That(document.Records, Is.Not.Null);
            Assert.That(document.Records, Is.Empty);
            Assert.That(document.ProcessedStageRunIds, Is.Not.Null);
            Assert.That(document.ProcessedStageRunIds, Is.Empty);
            Assert.That(document.ProcessedClearAttemptIds, Is.Not.Null);
            Assert.That(document.ProcessedClearAttemptIds, Is.Empty);
        }

        [Test]
        public void CampaignProfileLoadStatus_CoversExpectedValues()
        {
            Assert.That(
                Enum.GetValues(typeof(CampaignProfileLoadStatus)),
                Is.EquivalentTo(new[]
                {
                    CampaignProfileLoadStatus.Missing,
                    CampaignProfileLoadStatus.Loaded,
                    CampaignProfileLoadStatus.BackupRecovered,
                    CampaignProfileLoadStatus.CorruptNoFallback,
                    CampaignProfileLoadStatus.Unauthorized,
                    CampaignProfileLoadStatus.IoFailed,
                    CampaignProfileLoadStatus.UnsupportedVersion,
                    CampaignProfileLoadStatus.InvalidDocument,
                }));
        }

        [Test]
        public void CampaignProfileRepositoryAndAtomicTextFileStore_AreCompileTargets()
        {
            using var harness = CreateHarness();
            ICampaignProfileRepository repository = harness.Repository;
            IAtomicTextFileStore textFileStore = harness.Store;

            Assert.That(repository.Load().Status, Is.EqualTo(CampaignProfileLoadStatus.Missing));
            Assert.That(textFileStore.Exists("profile.json"), Is.False);
        }

        [Test]
        public void AtomicTextFileStore_MissingFileReportsMissingAndCreatesNoFile()
        {
            using var harness = CreateHarness();

            Assert.That(harness.Store.Exists(FileCampaignProfileRepository.ProfileFileName), Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
        }

        [Test]
        public void AtomicTextFileStore_WriteCreatesProfileJson()
        {
            using var harness = CreateHarness();

            harness.Store.WriteAllTextAtomic(FileCampaignProfileRepository.ProfileFileName, "{\"value\":1}");

            Assert.That(File.Exists(harness.ProfilePath), Is.True);
        }

        [Test]
        public void AtomicTextFileStore_WriteThenReadReturnsIdenticalText()
        {
            using var harness = CreateHarness();
            const string payload = "{\"value\":\"same text\"}";

            harness.Store.WriteAllTextAtomic(FileCampaignProfileRepository.ProfileFileName, payload);
            var loaded = harness.Store.ReadAllText(FileCampaignProfileRepository.ProfileFileName);

            Assert.That(loaded, Is.EqualTo(payload));
        }

        [Test]
        public void AtomicTextFileStore_WriteCreatesDirectoryIfMissing()
        {
            using var harness = CreateHarness();

            harness.Store.WriteAllTextAtomic(FileCampaignProfileRepository.ProfileFileName, "{\"value\":1}");

            Assert.That(Directory.Exists(harness.SaveRootPath), Is.True);
        }

        [Test]
        public void AtomicTextFileStore_ReplaceExistingFileCreatesOrUpdatesBackup()
        {
            using var harness = CreateHarness();
            const string firstPayload = "{\"value\":1}";
            const string secondPayload = "{\"value\":2}";

            harness.Store.WriteAllTextAtomic(FileCampaignProfileRepository.ProfileFileName, firstPayload);
            harness.Store.WriteAllTextAtomic(FileCampaignProfileRepository.ProfileFileName, secondPayload);

            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(secondPayload));
            Assert.That(File.Exists(harness.BackupPath), Is.True);
            Assert.That(File.ReadAllText(harness.BackupPath), Is.EqualTo(firstPayload));
        }

        [Test]
        public void AtomicTextFileStore_LeftoverTempIsIgnoredAndCleanedBestEffort()
        {
            using var harness = CreateHarness();
            const string payload = "{\"value\":1}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, payload);
            File.WriteAllText(harness.LeftoverTempPath, "{\"value\":999}");

            var loaded = harness.Store.ReadAllText(FileCampaignProfileRepository.ProfileFileName);

            Assert.That(loaded, Is.EqualTo(payload));
            Assert.That(File.Exists(harness.LeftoverTempPath), Is.False);
        }

        [Test]
        public void AtomicTextFileStore_MissingCanonicalRecoversDurableRollbackBeforeTempCleanup()
        {
            using var harness = CreateHarness();
            const string previousPayload = "{\"value\":1}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.RollbackPath, previousPayload);
            File.WriteAllText(harness.LeftoverTempPath, "{\"value\":999}");

            harness.Store.CleanupTempFiles(FileCampaignProfileRepository.ProfileFileName);

            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(previousPayload));
            Assert.That(File.Exists(harness.RollbackPath), Is.False);
            Assert.That(File.Exists(harness.LeftoverTempPath), Is.False);
        }

        [Test]
        public void AtomicTextFileStore_RecoverInterruptedBackupWriteRestoresLogicalBackup()
        {
            using var harness = CreateHarness();
            const string previousBackup = "{\"value\":\"previous-backup\"}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.BackupRollbackPath, previousBackup);

            harness.Store.RecoverInterruptedWrite(
                FileCampaignProfileRepository.ProfileFileName + ".bak");

            Assert.That(File.ReadAllText(harness.BackupPath), Is.EqualTo(previousBackup));
            Assert.That(File.Exists(harness.BackupRollbackPath), Is.False);
        }

        [Test]
        public void AtomicTextFileStore_ProfileCleanupDoesNotDeleteBackupWriteOrRollbackFiles()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.LeftoverTempPath, "profile-temp");
            File.WriteAllText(harness.BackupTempPath, "backup-temp");
            File.WriteAllText(harness.BackupRollbackPath, "backup-rollback");

            harness.Store.CleanupTempFiles(FileCampaignProfileRepository.ProfileFileName);

            Assert.That(File.Exists(harness.LeftoverTempPath), Is.False);
            Assert.That(File.Exists(harness.BackupTempPath), Is.True);
            Assert.That(File.Exists(harness.BackupRollbackPath), Is.True);
        }

        [Test]
        public void AtomicTextFileStore_DeleteActiveFileArtifacts_RemovesCanonicalBackupAndWriteResidueOnly()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "profile");
            File.WriteAllText(harness.BackupPath, "backup");
            File.WriteAllText(harness.RollbackPath, "profile-rollback");
            File.WriteAllText(harness.BackupRollbackPath, "backup-rollback");
            File.WriteAllText(harness.LeftoverTempPath, "profile-temp");
            File.WriteAllText(harness.BackupTempPath, "backup-temp");
            var corruptPath = harness.ProfilePath + ".corrupt.audit";
            var rejectedPath = harness.BackupPath + ".rejected.audit";
            var unrelatedPath = Path.Combine(harness.SaveRootPath, "unrelated.json");
            File.WriteAllText(corruptPath, "corrupt-evidence");
            File.WriteAllText(rejectedPath, "rejected-evidence");
            File.WriteAllText(unrelatedPath, "unrelated");

            harness.Store.DeleteActiveFileArtifacts(FileCampaignProfileRepository.ProfileFileName);

            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(File.Exists(harness.BackupPath), Is.False);
            Assert.That(File.Exists(harness.RollbackPath), Is.False);
            Assert.That(File.Exists(harness.BackupRollbackPath), Is.False);
            Assert.That(File.Exists(harness.LeftoverTempPath), Is.False);
            Assert.That(File.Exists(harness.BackupTempPath), Is.False);
            Assert.That(File.Exists(corruptPath), Is.True);
            Assert.That(File.Exists(rejectedPath), Is.True);
            Assert.That(File.Exists(unrelatedPath), Is.True);
        }

        [Test]
        public void CampaignSaveCompositionProvider_ClearTemporaryCampaignState_DoesNotRecoverRollbackResidue()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            var governedFileNames = new[]
            {
                FileCampaignProfileRepository.ProfileFileName,
                CampaignLocalLaunchStateRepository.FileName,
                CampaignSaveRecoveryService.PendingResetFileName,
            };

            for (var i = 0; i < governedFileNames.Length; i++)
            {
                var fileName = governedFileNames[i];
                File.WriteAllText(Path.Combine(harness.SaveRootPath, fileName + ".rollback"), "rollback");
                File.WriteAllText(Path.Combine(harness.SaveRootPath, fileName + ".bak.rollback"), "backup-rollback");
                File.WriteAllText(Path.Combine(harness.SaveRootPath, fileName + ".write.audit.tmp"), "temp");
                File.WriteAllText(Path.Combine(harness.SaveRootPath, fileName + ".bak.write.audit.tmp"), "backup-temp");
            }

            CampaignSaveCompositionProvider.ClearTemporaryCampaignState(
                new TemporarySavePathProvider(harness.SaveRootPath));

            for (var i = 0; i < governedFileNames.Length; i++)
            {
                var fileName = governedFileNames[i];
                Assert.That(File.Exists(Path.Combine(harness.SaveRootPath, fileName)), Is.False);
                Assert.That(Directory.GetFiles(harness.SaveRootPath, fileName + ".write.*.tmp"), Is.Empty);
                Assert.That(Directory.GetFiles(harness.SaveRootPath, fileName + ".bak.write.*.tmp"), Is.Empty);
                Assert.That(File.Exists(Path.Combine(harness.SaveRootPath, fileName + ".rollback")), Is.False);
                Assert.That(File.Exists(Path.Combine(harness.SaveRootPath, fileName + ".bak.rollback")), Is.False);
            }
        }

        [Test]
        public void AtomicTextFileStore_QuarantineCorruptFileCreatesTimestampedCorruptFile()
        {
            using var harness = CreateHarness();
            const string corruptPayload = "{\"value\":";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, corruptPayload);

            var quarantined = harness.Store.TryQuarantine(
                FileCampaignProfileRepository.ProfileFileName,
                out var quarantinePath);

            Assert.That(quarantined, Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(Path.GetFileName(quarantinePath), Does.StartWith("profile.json.corrupt."));
            Assert.That(File.ReadAllText(quarantinePath), Is.EqualTo(corruptPayload));
        }

        [Test]
        public void CampaignProfileRepository_MissingProfileReturnsMissing()
        {
            using var harness = CreateHarness();

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Missing));
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void CampaignProfileRepository_MissingCanonicalWithValidBackupRecoversBackup()
        {
            using var harness = CreateHarness();
            var backupDocument = CreateDocument("profile-backup-only");
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(backupDocument));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.BackupRecovered));
            Assert.That(result.Document.ProfileId, Is.EqualTo("profile-backup-only"));
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(File.ReadAllText(harness.BackupPath)));
        }

        [Test]
        public void CampaignProfileRepository_MissingCanonicalWithUnsupportedBackupFailsClosed()
        {
            using var harness = CreateHarness();
            const string unsupported = "{\"SchemaVersion\":3,\"Slots\":[]}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.BackupPath, unsupported);

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.UnsupportedVersion));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(File.ReadAllText(harness.BackupPath), Is.EqualTo(unsupported));
        }

        [Test]
        public void CampaignProfileRepository_MissingCanonicalWithCorruptBackupIsNotMissing()
        {
            using var harness = CreateHarness();
            const string corrupt = "{\"SchemaVersion\":";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.BackupPath, corrupt);

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.CorruptNoFallback));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(File.ReadAllText(harness.BackupPath), Is.EqualTo(corrupt));
        }

        [Test]
        public void CampaignProfileRepository_ValidProfileReturnsLoaded()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, JsonUtility.ToJson(CreateDocument("profile-loaded")));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.ProfileId, Is.EqualTo("profile-loaded"));
        }

        [TestCase("slot-stage-noncanonical")]
        [TestCase("slot-chances-negative")]
        [TestCase("slot-deaths-negative")]
        [TestCase("performance-null")]
        [TestCase("performance-version")]
        [TestCase("performance-stage")]
        [TestCase("performance-duplicate")]
        [TestCase("performance-negative")]
        [TestCase("snapshot-version")]
        [TestCase("clear-null")]
        [TestCase("clear-stage")]
        [TestCase("clear-duplicate")]
        [TestCase("clear-negative")]
        [TestCase("profile-run-empty")]
        [TestCase("profile-run-duplicate")]
        [TestCase("profile-attempt-empty")]
        [TestCase("record-run-empty")]
        [TestCase("record-run-duplicate")]
        public void CampaignProfileRepository_MalformedPersistedSlotDataFailsClosedWithoutMutation(
            string malformedCase)
        {
            using var harness = CreateHarness();
            var document = CreateMalformedPersistedSlotDocument(malformedCase);
            var original = JsonUtility.ToJson(document);
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, original);

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.InvalidDocument));
            Assert.That(result.HasDocument, Is.False);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(original));
        }

        [TestCase("performance-duplicate")]
        [TestCase("slot-chances-negative")]
        [TestCase("slot-deaths-negative")]
        public void CampaignProfileRepository_MalformedCanonicalRecoversValidBackup(
            string malformedCase)
        {
            using var harness = CreateHarness();
            var malformed = CreateMalformedPersistedSlotDocument(malformedCase);
            var backup = CreateDocument("valid-backup");
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, JsonUtility.ToJson(malformed));
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(backup));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.BackupRecovered));
            Assert.That(result.Document.ProfileId, Is.EqualTo("valid-backup"));
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(File.ReadAllText(harness.BackupPath)));
        }

        [Test]
        public void CampaignProfileRepository_NullNestedContainersNormalizeToEmpty()
        {
            using var harness = CreateHarness();
            var document = CreateDocument("null-nested-containers");
            document.Slots[0].NormalStagePerformanceRecords = null;
            document.Slots[0].StageClearProfileSnapshot = null;
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, JsonUtility.ToJson(document));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.Slots[0].NormalStagePerformanceRecords, Is.Empty);
            Assert.That(result.Document.Slots[0].StageClearProfileSnapshot, Is.Not.Null);
            Assert.That(result.Document.Slots[0].StageClearProfileSnapshot.Records, Is.Empty);
        }

        [Test]
        public void CampaignProfileRepository_ZeroRemainingChancesRemainsValid()
        {
            using var harness = CreateHarness();
            var document = CreateDocument("zero-remaining-chances");
            document.Slots[0].RemainingChances = 0;
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, JsonUtility.ToJson(document));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.Slots[0].RemainingChances, Is.Zero);
        }

        [TestCase("clear-negative")]
        [TestCase("slot-chances-negative")]
        [TestCase("slot-deaths-negative")]
        public void CampaignProfileRepository_SaveRejectsMalformedPersistedSlotDataBeforeWriting(
            string malformedCase)
        {
            using var harness = CreateHarness();
            var document = CreateMalformedPersistedSlotDocument(malformedCase);

            Assert.Throws<ArgumentException>(() => harness.Repository.Save(document));

            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
        }

        [Test]
        public void CampaignProfileRepository_SaveThenLoadReturnsEquivalentDocument()
        {
            using var harness = CreateHarness();
            var document = CreateDocument("profile-roundtrip");

            harness.Repository.Save(document);
            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.SchemaVersion, Is.EqualTo(document.SchemaVersion));
            Assert.That(result.Document.ProductVersion, Is.EqualTo(document.ProductVersion));
            Assert.That(result.Document.ProfileId, Is.EqualTo(document.ProfileId));
            Assert.That(result.Document.Slots, Has.Length.EqualTo(1));
            Assert.That(result.Document.Slots[0].StageId, Is.EqualTo(document.Slots[0].StageId));
            Assert.That(result.Document.Slots[0].IntroComicCompleted, Is.EqualTo(document.Slots[0].IntroComicCompleted));
            Assert.That(result.Document.Slots[0].OutroComicCompleted, Is.EqualTo(document.Slots[0].OutroComicCompleted));
            Assert.That(result.Document.Slots[0].TotalDeaths, Is.EqualTo(document.Slots[0].TotalDeaths));
            Assert.That(result.Document.Slots[0].StageClearProfileSnapshot.Version, Is.EqualTo(2));
            Assert.That(result.Document.Slots[0].StageClearProfileSnapshot.Records, Has.Length.EqualTo(1));
            Assert.That(result.Document.Slots[0].StageClearProfileSnapshot.Records[0].StageId, Is.EqualTo("stage-1-1"));
        }

        [Test]
        public void CampaignProfileRepository_CorruptProfileWithValidBackupReturnsBackupRecovered()
        {
            using var harness = CreateHarness();
            var backupDocument = CreateDocument("profile-backup");
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":");
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(backupDocument));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.BackupRecovered));
            Assert.That(result.Document.ProfileId, Is.EqualTo("profile-backup"));
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(File.ReadAllText(harness.BackupPath)));
        }

        [Test]
        public void CampaignProfileRepository_CorruptProfileWithoutValidBackupDoesNotMutateCanonicalFile()
        {
            using var harness = CreateHarness();
            const string corruptPayload = "{\"SchemaVersion\":";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, corruptPayload);

            var result = harness.Repository.Load();
            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.CorruptNoFallback));
            Assert.That(result.HasDocument, Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(corruptPayload));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.corrupt.*"), Is.Empty);
        }

        [Test]
        public void CampaignProfileRepository_ValidJsonWithUnsupportedVersionReturnsUnsupportedVersion()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":0,\"ProfileId\":\"profile-invalid\"}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.UnsupportedVersion));
            Assert.That(result.HasDocument, Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
        }

        [Test]
        public void CampaignProfileRepository_FutureSchemaWithoutProfileIdDoesNotRestoreOlderBackup()
        {
            using var harness = CreateHarness();
            const string futureCanonical = "{\"SchemaVersion\":3,\"Slots\":[]}";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, futureCanonical);
            File.WriteAllText(
                harness.BackupPath,
                JsonUtility.ToJson(CreateDocument("profile-current-backup")));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.UnsupportedVersion));
            Assert.That(result.HasDocument, Is.False);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(futureCanonical));
        }

        [Test]
        public void CampaignProfileRepository_DestructiveSaveKeepsBackupAtCommittedState()
        {
            using var harness = CreateHarness();
            harness.Repository.Save(CreateDocument("profile-before-delete"));
            var cleared = CreateDocument("profile-after-delete");
            cleared.Slots = Array.Empty<CampaignSlotDocument>();
            cleared.LastPlayedSlotNumber = 0;

            harness.Repository.SaveDestructive(cleared);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":");
            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.BackupRecovered));
            Assert.That(result.Document.ProfileId, Is.EqualTo("profile-after-delete"));
            Assert.That(result.Document.Slots, Is.Empty);
            Assert.That(result.Document.LastPlayedSlotNumber, Is.Zero);
        }

        [Test]
        public void CampaignProfileRepository_DestructiveCanonicalFailureRestoresPreviousFiles()
        {
            var previousCanonical = JsonUtility.ToJson(CreateDocument("profile-before"));
            var previousBackup = JsonUtility.ToJson(CreateDocument("profile-older"));
            var store = new FaultInjectingTextFileStore(previousCanonical, previousBackup)
            {
                CanonicalWriteFailuresRemaining = 1,
            };
            var repository = new FileCampaignProfileRepository(store);

            Assert.Throws<InvalidOperationException>(
                () => repository.SaveDestructive(CreateDocument("profile-after")));

            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName),
                Is.EqualTo(previousCanonical));
            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName + ".bak"),
                Is.EqualTo(previousBackup));
        }

        [Test]
        public void CampaignProfileRepository_DestructiveFailureRestoresRecoveredBackupRollback()
        {
            var previousCanonical = JsonUtility.ToJson(CreateDocument("profile-before"));
            var previousBackup = JsonUtility.ToJson(CreateDocument("profile-older"));
            var store = new FaultInjectingTextFileStore(previousCanonical, backup: null)
            {
                CanonicalWriteFailuresRemaining = 1,
            };
            store.SetFile(
                FileCampaignProfileRepository.ProfileFileName + ".bak.rollback",
                previousBackup);
            var repository = new FileCampaignProfileRepository(store);

            Assert.Throws<InvalidOperationException>(
                () => repository.SaveDestructive(CreateDocument("profile-after")));

            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName),
                Is.EqualTo(previousCanonical));
            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName + ".bak"),
                Is.EqualTo(previousBackup));
            Assert.That(store.Exists(FileCampaignProfileRepository.ProfileFileName + ".bak.rollback"),
                Is.False);
        }

        [Test]
        public void CampaignProfileRepository_NormalizationFailureWritesNothing()
        {
            var previousCanonical = JsonUtility.ToJson(CreateDocument("profile-before"));
            var store = new FaultInjectingTextFileStore(previousCanonical, backup: null)
            {
                RecoverFailureFileName = FileCampaignProfileRepository.ProfileFileName + ".bak",
            };
            store.SetFile(
                FileCampaignProfileRepository.ProfileFileName + ".bak.rollback",
                JsonUtility.ToJson(CreateDocument("profile-older")));
            var repository = new FileCampaignProfileRepository(store);

            Assert.Throws<IOException>(
                () => repository.SaveDestructive(CreateDocument("profile-after")));

            Assert.That(store.TotalWriteCount, Is.Zero);
            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName),
                Is.EqualTo(previousCanonical));
            Assert.That(store.Exists(FileCampaignProfileRepository.ProfileFileName + ".bak"), Is.False);
            Assert.That(store.Exists(FileCampaignProfileRepository.ProfileFileName + ".bak.rollback"), Is.True);
        }

        [Test]
        public void CampaignProfileRepository_DestructiveCompensationFailurePreservesNewRecoveryBackup()
        {
            var previousCanonical = JsonUtility.ToJson(CreateDocument("profile-before"));
            var replacement = CreateDocument("profile-after");
            var replacementJson = JsonUtility.ToJson(replacement);
            var store = new FaultInjectingTextFileStore(previousCanonical, backup: null)
            {
                CanonicalWriteFailuresRemaining = 2,
            };
            var repository = new FileCampaignProfileRepository(store);

            Assert.Throws<AggregateException>(() => repository.SaveDestructive(replacement));

            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName),
                Is.EqualTo(previousCanonical));
            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName + ".bak"),
                Is.EqualTo(replacementJson));
        }

        [Test]
        public void CampaignProfileRepository_DestructiveBackupCleanupFailureIsReported()
        {
            var previousCanonical = JsonUtility.ToJson(CreateDocument("profile-before"));
            var replacement = CreateDocument("profile-after");
            var store = new FaultInjectingTextFileStore(previousCanonical, backup: null)
            {
                CanonicalWriteFailuresRemaining = 1,
                FailBackupDelete = true,
            };
            var repository = new FileCampaignProfileRepository(store);

            Assert.Throws<AggregateException>(() => repository.SaveDestructive(replacement));

            Assert.That(store.ReadAllText(FileCampaignProfileRepository.ProfileFileName),
                Is.EqualTo(previousCanonical));
            Assert.That(store.Exists(FileCampaignProfileRepository.ProfileFileName + ".bak"), Is.True);
        }

        [Test]
        public void CampaignProfileRepository_ValidBackupThatCannotRestoreReturnsIoFailed()
        {
            var store = new FaultInjectingTextFileStore(
                "{\"SchemaVersion\":",
                JsonUtility.ToJson(CreateDocument("profile-backup")))
            {
                RestoreBackupResult = false,
            };
            var repository = new FileCampaignProfileRepository(store);

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.IoFailed));
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void CampaignProfileRepository_MissingSlotsArrayNormalizesToEmptyArray()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                harness.ProfilePath,
                $"{{\"SchemaVersion\":{CampaignProfileDocument.CurrentSchemaVersion},\"ProfileId\":\"profile-no-slots\"}}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.Slots, Is.Not.Null);
            Assert.That(result.Document.Slots, Is.Empty);
        }

        [Test]
        public void CampaignProfileRepository_CurrentProfileWithoutCompatibilityMetadataLoadsSuccessfully()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                harness.ProfilePath,
                $"{{\"SchemaVersion\":{CampaignProfileDocument.CurrentSchemaVersion},\"ProfileId\":\"profile-current\"}}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.SchemaVersion, Is.EqualTo(CampaignProfileDocument.CurrentSchemaVersion));
        }

        [Test]
        public void CampaignProfileRepository_PreviousSchemaIsRejected()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                harness.ProfilePath,
                "{\"SchemaVersion\":1,\"ProfileId\":\"profile-previous\",\"Slots\":[]}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.UnsupportedVersion));
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void CampaignProfileRepository_UnknownPreReleaseCompatibilityMetadataIsIgnored()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                harness.ProfilePath,
                $"{{\"SchemaVersion\":{CampaignProfileDocument.CurrentSchemaVersion},\"ProfileId\":\"profile-null-guards\",\"LegacyImport\":{{\"DeletedSlotGuards\":null}}}}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(typeof(CampaignProfileDocument).GetField("LegacyImport"), Is.Null);
        }

        [Test]
        public void CampaignProfileRepository_CorruptProfileWhenQuarantineFailsReturnsCorruptNoFallback()
        {
            var repository = new FileCampaignProfileRepository(
                new QuarantineFailingTextFileStore("{\"SchemaVersion\":"));

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.CorruptNoFallback));
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void CampaignProfileRepository_MissingFileDoesNotCreateProfileJson()
        {
            using var harness = CreateHarness();

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Missing));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
        }

        [Test]
        public void CampaignSaveRecoveryPolicy_OnlyAllowsResetForRepairableProfileFailures()
        {
            Assert.That(
                CampaignSaveRecoveryPolicy.GetActions(CampaignSaveLoadStatus.SchemaInvalidRepairRequired),
                Is.EqualTo(CampaignSaveRecoveryActions.Retry | CampaignSaveRecoveryActions.ResetProfile));
            Assert.That(
                CampaignSaveRecoveryPolicy.GetActions(CampaignSaveLoadStatus.CorruptRepairRequired),
                Is.EqualTo(CampaignSaveRecoveryActions.Retry | CampaignSaveRecoveryActions.ResetProfile));
            Assert.That(
                CampaignSaveRecoveryPolicy.GetActions(CampaignSaveLoadStatus.Unauthorized),
                Is.EqualTo(CampaignSaveRecoveryActions.Retry));
            Assert.That(
                CampaignSaveRecoveryPolicy.GetActions(CampaignSaveLoadStatus.IoFailed),
                Is.EqualTo(CampaignSaveRecoveryActions.Retry));
            Assert.That(
                CampaignSaveRecoveryPolicy.GetActions(CampaignSaveLoadStatus.RecoveryPending),
                Is.EqualTo(CampaignSaveRecoveryActions.Retry));
            Assert.That(
                CampaignSaveRecoveryPolicy.GetActions(CampaignSaveLoadStatus.Loaded),
                Is.EqualTo(CampaignSaveRecoveryActions.None));
        }

        [Test]
        public void CampaignSaveRecovery_UnsupportedProfileAndBackup_AreArchivedAndReplacedWithEmptyCurrentProfile()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            const string unsupported = "{\"SchemaVersion\":1,\"ProfileId\":\"legacy-profile\",\"Slots\":[]}";
            File.WriteAllText(harness.ProfilePath, unsupported);
            File.WriteAllText(harness.BackupPath, unsupported);
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.ResetBlockedProfile(
                CampaignSaveLoadStatus.SchemaInvalidRepairRequired);
            var loaded = harness.Repository.Load();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(loaded.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(loaded.Document.Slots, Is.Empty);
            Assert.That(loaded.Document.SavedAtUtc, Is.EqualTo("2026-08-20T01:02:03.0000000Z"));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*"), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.bak.rejected.*"), Has.Length.EqualTo(1));
            Assert.That(harness.Store.Exists(CampaignSaveRecoveryService.PendingResetFileName), Is.False);
        }

        [Test]
        public void CampaignSaveRecovery_BackupQuarantineFailureLeavesCanonicalForSafeRetry()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            const string unsupported = "{\"SchemaVersion\":1,\"ProfileId\":\"legacy-profile\",\"Slots\":[]}";
            File.WriteAllText(harness.ProfilePath, unsupported);
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(CreateDocument("old-backup")));
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            CampaignSaveResetResult firstResult;
            using (new FileStream(harness.BackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                firstResult = recovery.ResetBlockedProfile(
                    CampaignSaveLoadStatus.SchemaInvalidRepairRequired);

                Assert.That(firstResult, Is.EqualTo(CampaignSaveResetResult.Failed));
                Assert.That(File.Exists(harness.ProfilePath), Is.True);
                Assert.That(File.Exists(harness.BackupPath), Is.True);
                Assert.That(recovery.HasPendingReset, Is.True);
                Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*"), Is.Empty);
            }

            var retryResult = recovery.RetryPendingReset();
            var loaded = harness.Repository.Load();

            Assert.That(retryResult, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(loaded.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(loaded.Document.ProfileId, Is.EqualTo("campaign-profile"));
            Assert.That(loaded.Document.Slots, Is.Empty);
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*"), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.bak.rejected.*"), Has.Length.EqualTo(1));
            Assert.That(recovery.HasPendingReset, Is.False);
        }

        [Test]
        public void CampaignSaveRecovery_CanonicalQuarantineFailureAfterBackupArchiveResumes()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            const string unsupported = "{\"SchemaVersion\":1,\"ProfileId\":\"legacy-profile\",\"Slots\":[]}";
            File.WriteAllText(harness.ProfilePath, unsupported);
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(CreateDocument("old-backup")));
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            using (new FileStream(harness.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var firstResult = recovery.ResetBlockedProfile(
                    CampaignSaveLoadStatus.SchemaInvalidRepairRequired);

                Assert.That(firstResult, Is.EqualTo(CampaignSaveResetResult.Failed));
                Assert.That(File.Exists(harness.ProfilePath), Is.True);
                Assert.That(File.Exists(harness.BackupPath), Is.False);
                Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.bak.rejected.*"), Has.Length.EqualTo(1));
                Assert.That(recovery.HasPendingReset, Is.True);
            }

            var retryResult = recovery.RetryPendingReset();

            Assert.That(retryResult, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(harness.Repository.Load().Document.Slots, Is.Empty);
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.bak.rejected.*"), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*"), Has.Length.EqualTo(1));
            Assert.That(recovery.HasPendingReset, Is.False);
        }

        [Test]
        public void CampaignSaveRecovery_RollbackSourcesAreNormalizedBeforeQuarantine()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            const string unsupportedCanonical =
                "{\"SchemaVersion\":1,\"ProfileId\":\"rollback-canonical\",\"Slots\":[]}";
            const string unsupportedBackup =
                "{\"SchemaVersion\":1,\"ProfileId\":\"rollback-backup\",\"Slots\":[]}";
            File.WriteAllText(harness.RollbackPath, unsupportedCanonical);
            File.WriteAllText(harness.BackupRollbackPath, unsupportedBackup);
            File.WriteAllText(harness.LeftoverTempPath, "temporary-canonical");
            File.WriteAllText(harness.BackupTempPath, "temporary-backup");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.ResetBlockedProfile(
                CampaignSaveLoadStatus.SchemaInvalidRepairRequired);

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(File.Exists(harness.RollbackPath), Is.False);
            Assert.That(File.Exists(harness.BackupRollbackPath), Is.False);
            Assert.That(File.Exists(harness.LeftoverTempPath), Is.False);
            Assert.That(File.Exists(harness.BackupTempPath), Is.False);
            Assert.That(
                File.ReadAllText(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*")[0]),
                Is.EqualTo(unsupportedCanonical));
            Assert.That(
                File.ReadAllText(Directory.GetFiles(harness.SaveRootPath, "profile.json.bak.rejected.*")[0]),
                Is.EqualTo(unsupportedBackup));
        }

        [Test]
        public void CampaignSaveRecovery_StateChangedBeforeConfirmation_DoesNotReplaceCurrentProfile()
        {
            using var harness = CreateHarness();
            harness.Repository.Save(CreateDocument("current-profile"));
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.ResetBlockedProfile(
                CampaignSaveLoadStatus.SchemaInvalidRepairRequired);

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.StateChanged));
            Assert.That(harness.Repository.Load().Document.ProfileId, Is.EqualTo("current-profile"));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.rejected.*"), Is.Empty);
        }

        [Test]
        public void CampaignSaveRecovery_PendingReset_ResumesWithoutCompatibilityImport()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                harness.ProfilePath,
                "{\"SchemaVersion\":1,\"ProfileId\":\"legacy-profile\",\"Slots\":[]}");
            harness.Store.WriteAllTextAtomic(
                CampaignSaveRecoveryService.PendingResetFileName,
                "{\"ResetId\":\"202608200102030000000\",\"StartedAtUtc\":\"2026-08-20T01:02:03.0000000Z\"}");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 4, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.ResumePendingReset();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(harness.Repository.Load().Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(
                harness.Repository.Load().Document.SavedAtUtc,
                Is.EqualTo("2026-08-20T01:02:03.0000000Z"));
            Assert.That(harness.Store.Exists(CampaignSaveRecoveryService.PendingResetFileName), Is.False);
        }

        [Test]
        public void CampaignSaveRecovery_PendingResetPreservesValidProfileWithDifferentTombstone()
        {
            using var harness = CreateHarness();
            harness.Repository.Save(CreateDocument("newer-valid-profile"));
            harness.Store.WriteAllTextAtomic(
                CampaignSaveRecoveryService.PendingResetFileName,
                "{\"ResetId\":\"202608200102030000000\",\"StartedAtUtc\":\"2026-08-20T01:02:03.0000000Z\"}");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 4, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.RetryPendingReset();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.StateChanged));
            Assert.That(harness.Repository.Load().Document.ProfileId, Is.EqualTo("newer-valid-profile"));
            Assert.That(recovery.HasPendingReset, Is.False);
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.rejected.*"), Is.Empty);
        }

        [Test]
        public void CampaignSaveRecovery_MalformedPendingWithMissingProfile_RebuildsEmptyProfile()
        {
            using var harness = CreateHarness();
            harness.Store.WriteAllTextAtomic(
                CampaignSaveRecoveryService.PendingResetFileName,
                "{not-json");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 4, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.RetryPendingReset();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(harness.Repository.Load().Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(harness.Repository.Load().Document.Slots, Is.Empty);
            Assert.That(recovery.HasPendingReset, Is.False);
        }

        [Test]
        public void CampaignSaveRecovery_UnsafePendingResetIdWithUnsupportedProfile_RebuildsAndCompletesReset()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                harness.ProfilePath,
                "{\"SchemaVersion\":1,\"ProfileId\":\"unsupported-profile\",\"Slots\":[]}");
            harness.Store.WriteAllTextAtomic(
                CampaignSaveRecoveryService.PendingResetFileName,
                "{\"ResetId\":\"bad/name\",\"StartedAtUtc\":\"2026-08-20T01:02:03.0000000Z\"}");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 4, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.RetryPendingReset();
            var loaded = harness.Repository.Load();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(loaded.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(loaded.Document.Slots, Is.Empty);
            Assert.That(recovery.HasPendingReset, Is.False);
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void CampaignSaveRecovery_InvalidPendingTimestampWithCorruptProfile_RebuildsAndCompletesReset()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":");
            harness.Store.WriteAllTextAtomic(
                CampaignSaveRecoveryService.PendingResetFileName,
                "{\"ResetId\":\"202608200102030000000\",\"StartedAtUtc\":\"invalid-time\"}");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 4, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.RetryPendingReset();
            var loaded = harness.Repository.Load();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.Completed));
            Assert.That(loaded.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(loaded.Document.Slots, Is.Empty);
            Assert.That(recovery.HasPendingReset, Is.False);
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "profile.json.rejected.*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void CampaignSaveRecovery_InvalidPendingFieldsPreserveValidCurrentProfile()
        {
            using var harness = CreateHarness();
            harness.Repository.Save(CreateDocument("newer-valid-profile"));
            harness.Store.WriteAllTextAtomic(
                CampaignSaveRecoveryService.PendingResetFileName,
                "{\"ResetId\":\"bad/name\",\"StartedAtUtc\":\"invalid-time\"}");
            var recovery = new CampaignSaveRecoveryService(
                harness.Repository,
                harness.Store,
                () => new DateTime(2026, 8, 20, 1, 2, 4, DateTimeKind.Utc),
                "campaign-profile",
                "test-product");

            var result = recovery.RetryPendingReset();

            Assert.That(result, Is.EqualTo(CampaignSaveResetResult.StateChanged));
            Assert.That(harness.Repository.Load().Document.ProfileId, Is.EqualTo("newer-valid-profile"));
            Assert.That(recovery.HasPendingReset, Is.False);
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.rejected.*"), Is.Empty);
        }

        [Test]
        public void CampaignProfileRepository_TestsUseTempPathNotPersistentDataPath()
        {
            using var harness = CreateHarness();

            Assert.That(harness.SaveRootPath, Does.StartWith(Path.Combine("Temp", "CampaignProfileRepositoryTests")));
            Assert.That(harness.SaveRootPath, Does.Not.Contain(Application.persistentDataPath));
        }

        [Test]
        public void PreReleasePolicy_DocumentsJsonOnlyDeletionAndActiveSlotContract()
        {
            var source = File.ReadAllText(
                "Docs/Architecture/Pre-Release-Save-Baseline-Policy.md");

            Assert.That(source, Does.Contain("`Saves/profile.json`"));
            Assert.That(source, Does.Contain("`Saves/local-launch-state.json`"));
            Assert.That(source, Does.Contain("PlayerPrefs campaign progression import is unsupported"));
            Assert.That(source, Does.Contain("deleting a current profile slot must still clear matching committed active state"));
            Assert.That(source, Does.Contain("validated before normalization"));
            Assert.That(source, Does.Contain("persisted slot counters"));
            Assert.That(source, Does.Contain("archives the backup before the"));
            Assert.That(source, Does.Contain("rollbacks are normalized before"));
        }

        [Test]
        public void ArchitectureReadme_DocumentsJsonOnlyProductionAndRetainedProcessedIdSchema()
        {
            var readme = File.ReadAllText("Docs/Architecture/README.md");
            const string sectionHeading = "## Stage clear save/profile boundary";
            const string nextHeading = "## Campaign save architecture V2 policy closeout";
            var sectionStart = readme.IndexOf(sectionHeading, StringComparison.Ordinal);
            var sectionEnd = readme.IndexOf(nextHeading, StringComparison.Ordinal);

            Assert.That(sectionStart, Is.GreaterThanOrEqualTo(0), "Stage clear save/profile section is required.");
            Assert.That(sectionEnd, Is.GreaterThan(sectionStart), "Stage clear save/profile section must stay bounded.");

            var section = readme.Substring(sectionStart, sectionEnd - sectionStart);
            Assert.That(section, Does.Contain("Saves/profile.json"));
            Assert.That(
                section,
                Does.Contain("`CampaignProfileDocument`의 `SchemaVersion = 2`"));
            Assert.That(section, Does.Contain("Records[]"));
            Assert.That(section, Does.Contain("PlayerPrefs progression import는 지원하지 않는다"));
            Assert.That(section, Does.Contain("<file>.rollback"));
            Assert.That(section, Does.Contain("별도 save schema가 아니다"));
            Assert.That(section, Does.Contain("Saves/local-launch-state.json"));
            Assert.That(section, Does.Contain("PlayerPrefs fallback을 사용하지 않는다"));
            Assert.That(section, Does.Contain("production read/write/delete path에 사용하지 않는다"));
            Assert.That(section, Does.Contain("Library/J2M/DirectPlayCampaign/Saves"));
            Assert.That(section, Does.Contain("Profile `ProcessedStageRunIds`"));
            Assert.That(section, Does.Contain("`ProcessedClearAttemptIds`"));
            Assert.That(section, Does.Contain("Record `ProcessedStageRunIds`"));
            Assert.That(section, Does.Contain("Current Production semantic use"));
            Assert.That(section, Does.Contain("| 없음 |"));
            Assert.That(section, Does.Contain("active idempotency mechanism"));
            Assert.That(section, Does.Contain("first-public schema freeze"));
            Assert.That(section, Does.Contain("release exposure"));
            Assert.That(section, Does.Contain("ApplyStageClear"));
            Assert.That(section, Does.Contain("current Production caller"));
            Assert.That(section, Does.Contain("normalization 전에 검증"));
            Assert.That(section, Does.Contain("음수 slot counter"));
            Assert.That(section, Does.Contain("`LoadAllWithReport`만 blocked profile"));
            Assert.That(section, Does.Contain("backup을 canonical보다 먼저"));
        }

        [Test]
        public void ProductAchievementFoundation_DistinguishesRuntimeAndPersistedPerformanceDuplicates()
        {
            var source = File.ReadAllText(
                "Docs/Architecture/Product-Achievement-Foundation.md");

            Assert.That(source, Does.Contain("Runtime upsert and in-memory normalization"));
            Assert.That(source, Does.Contain("persisted current-schema profile"));
            Assert.That(source, Does.Contain("duplicate `StageId` performance records is invalid"));
            Assert.That(source, Does.Contain("fails closed before normalization"));
        }

        private static CampaignProfileDocument CreateDocument(string profileId)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = "test-product",
                SavedAtUtc = "2026-07-06T09:00:00Z",
                ProfileId = profileId,
                LastPlayedSlotNumber = 1,
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 1,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 3,
                        IntroComicCompleted = true,
                        OutroComicCompleted = false,
                        TotalDeaths = 6,
                        LastPlayedAtUtc = "2026-07-06T10:00:00Z",
                        StageClearProfileSnapshot = new CampaignStageClearProfileDocument
                        {
                            Version = 2,
                            Records = new[]
                            {
                                new PlayerStageClearRecordDocument
                                {
                                    StageId = "stage-1-1",
                                    HasAttempted = true,
                                    HasCleared = true,
                                    ClearCount = 1,
                                    ProcessedStageRunIds = new[] { "run-repository" },
                                },
                            },
                            ProcessedStageRunIds = new[] { "run-repository" },
                            ProcessedClearAttemptIds = new[] { "attempt-repository" },
                        },
                    },
                },
            };
        }

        private static CampaignProfileDocument CreateMalformedPersistedSlotDocument(string malformedCase)
        {
            var document = CreateDocument("malformed-persisted-slot");
            var slot = document.Slots[0];
            slot.NormalStagePerformanceRecords = new[]
            {
                new NormalStagePerformanceRecordDocument
                {
                    Version = NormalStagePerformanceRecord.CurrentVersion,
                    StageId = "stage-1-1",
                    BestCombinedPushFlipUses = 4,
                },
            };

            switch (malformedCase)
            {
                case "slot-stage-noncanonical":
                    slot.StageId = " Stage_1_1 ";
                    break;
                case "slot-chances-negative":
                    slot.RemainingChances = -1;
                    break;
                case "slot-deaths-negative":
                    slot.TotalDeaths = -1;
                    break;
                case "performance-null":
                    slot.NormalStagePerformanceRecords = new NormalStagePerformanceRecordDocument[] { null };
                    break;
                case "performance-version":
                    slot.NormalStagePerformanceRecords[0].Version++;
                    break;
                case "performance-stage":
                    slot.NormalStagePerformanceRecords[0].StageId = " Stage_1_1 ";
                    break;
                case "performance-duplicate":
                    slot.NormalStagePerformanceRecords = new[]
                    {
                        slot.NormalStagePerformanceRecords[0],
                        new NormalStagePerformanceRecordDocument
                        {
                            Version = NormalStagePerformanceRecord.CurrentVersion,
                            StageId = "stage-1-1",
                            BestCombinedPushFlipUses = 3,
                        },
                    };
                    break;
                case "performance-negative":
                    slot.NormalStagePerformanceRecords[0].BestCombinedPushFlipUses = -1;
                    break;
                case "snapshot-version":
                    slot.StageClearProfileSnapshot.Version = -1;
                    break;
                case "clear-null":
                    slot.StageClearProfileSnapshot.Records = new PlayerStageClearRecordDocument[] { null };
                    break;
                case "clear-stage":
                    slot.StageClearProfileSnapshot.Records[0].StageId = " Stage_1_1 ";
                    break;
                case "clear-duplicate":
                    slot.StageClearProfileSnapshot.Records = new[]
                    {
                        slot.StageClearProfileSnapshot.Records[0],
                        new PlayerStageClearRecordDocument
                        {
                            StageId = "stage-1-1",
                            HasAttempted = true,
                            HasCleared = true,
                            ClearCount = 2,
                            ProcessedStageRunIds = new[] { "run-duplicate" },
                        },
                    };
                    break;
                case "clear-negative":
                    slot.StageClearProfileSnapshot.Records[0].ClearCount = -1;
                    break;
                case "profile-run-empty":
                    slot.StageClearProfileSnapshot.ProcessedStageRunIds = new[] { " " };
                    break;
                case "profile-run-duplicate":
                    slot.StageClearProfileSnapshot.ProcessedStageRunIds = new[] { "run-a", "run-a" };
                    break;
                case "profile-attempt-empty":
                    slot.StageClearProfileSnapshot.ProcessedClearAttemptIds = new string[] { null };
                    break;
                case "record-run-empty":
                    slot.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds = new[] { string.Empty };
                    break;
                case "record-run-duplicate":
                    slot.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds =
                        new[] { "run-a", "run-a" };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(malformedCase), malformedCase, null);
            }

            return document;
        }

        private static SaveSlotData CreateLegacySlotFixture()
        {
            var stageId = StageId.CreateOrThrow("stage-3-1");
            var otherStageId = StageId.CreateOrThrow("stage-2-1");
            var slot = new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-3",
                RemainingChances = 1,
                CampaignCompleted = true,
                IntroComicCompleted = true,
                OutroComicCompleted = true,
                TotalDeaths = 9,
                LastPlayedAt = "2026-07-06T14:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot
                {
                    Version = 5,
                },
            };
            slot.StageClearProfileSnapshot.ClearRecordsByStageId[otherStageId] = new PlayerStageClearRecord
            {
                StageId = otherStageId,
                HasAttempted = true,
                HasCleared = false,
                ClearCount = 0,
                ProcessedStageRunIds = new[] { "run-c" },
            };
            slot.StageClearProfileSnapshot.ClearRecordsByStageId[stageId] = new PlayerStageClearRecord
            {
                StageId = stageId,
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

        private static void AssertSlotMatchesLegacySlot(CampaignSlotDocument document, SaveSlotData slot)
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
            Assert.That(document.StageClearProfileSnapshot.Version, Is.EqualTo(slot.StageClearProfileSnapshot.Version));
            Assert.That(document.StageClearProfileSnapshot.Records, Has.Length.EqualTo(2));
            Assert.That(document.StageClearProfileSnapshot.Records[0].StageId, Is.EqualTo("stage-2-1"));
            Assert.That(document.StageClearProfileSnapshot.Records[0].HasAttempted, Is.True);
            Assert.That(document.StageClearProfileSnapshot.Records[0].HasCleared, Is.False);
            Assert.That(document.StageClearProfileSnapshot.Records[0].ClearCount, Is.EqualTo(0));
            Assert.That(document.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Does.Contain("run-c"));
            Assert.That(document.StageClearProfileSnapshot.Records[1].StageId, Is.EqualTo("stage-3-1"));
            Assert.That(document.StageClearProfileSnapshot.Records[1].HasAttempted, Is.True);
            Assert.That(document.StageClearProfileSnapshot.Records[1].HasCleared, Is.True);
            Assert.That(document.StageClearProfileSnapshot.Records[1].ClearCount, Is.EqualTo(2));
            Assert.That(document.StageClearProfileSnapshot.Records[1].ProcessedStageRunIds, Is.EqualTo(new[] { "run-b", "run-a" }));
            Assert.That(document.StageClearProfileSnapshot.ProcessedStageRunIds, Is.EqualTo(new[] { "run-a", "run-b" }));
            Assert.That(document.StageClearProfileSnapshot.ProcessedClearAttemptIds, Is.EqualTo(new[] { "attempt-a", "attempt-b" }));
        }

        private static void WriteFieldInventory()
        {
            TestContext.WriteLine("Legacy field | Current owner | Required in V2 | Target V2 field | Lossless | Test");
            TestContext.WriteLine("SlotNumber | SaveSlotData | yes | CampaignSlotDocument.SlotNumber | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("CurrentStageId | SaveSlotData | yes | CampaignSlotDocument.StageId | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("CurrentLevelGroupId | SaveSlotData | yes | CampaignSlotDocument.LevelGroupId | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("RemainingChances | SaveSlotData | yes | CampaignSlotDocument.RemainingChances | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("CampaignCompleted | SaveSlotData | yes | CampaignSlotDocument.CampaignCompleted | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("IntroComicCompleted | SaveSlotData | yes | CampaignSlotDocument.IntroComicCompleted | yes | CampaignSlotDocument_RoundTripsExtendedSlotFieldsThroughJsonUtility");
            TestContext.WriteLine("OutroComicCompleted | SaveSlotData | yes | CampaignSlotDocument.OutroComicCompleted | yes | CampaignSlotDocument_RoundTripsExtendedSlotFieldsThroughJsonUtility");
            TestContext.WriteLine("TotalDeaths | SaveSlotData | yes | CampaignSlotDocument.TotalDeaths | yes | CampaignSlotDocument_RoundTripsExtendedSlotFieldsThroughJsonUtility");
            TestContext.WriteLine("LastPlayedAt | SaveSlotData | yes | CampaignSlotDocument.LastPlayedAtUtc | yes | CampaignProfileDocument_RoundTripsThroughJsonUtility");
            TestContext.WriteLine("StageClearProfileSnapshot.Version | StageClearProfileSnapshot | yes | CampaignStageClearProfileDocument.Version | yes | CampaignSlotDocument_RoundTripsStageClearProfileRecordsThroughJsonUtility");
            TestContext.WriteLine("ClearRecordsByStageId | StageClearProfileSnapshot | yes | CampaignStageClearProfileDocument.Records | yes | CampaignSlotDocument_RoundTripsStageClearProfileRecordsThroughJsonUtility");
            TestContext.WriteLine("ProcessedStageRunIds | StageClearProfileSnapshot | yes | CampaignStageClearProfileDocument.ProcessedStageRunIds | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("ProcessedClearAttemptIds | StageClearProfileSnapshot | yes | CampaignStageClearProfileDocument.ProcessedClearAttemptIds | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("StageId | PlayerStageClearRecord | yes | PlayerStageClearRecordDocument.StageId | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("HasAttempted | PlayerStageClearRecord | yes | PlayerStageClearRecordDocument.HasAttempted | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("HasCleared | PlayerStageClearRecord | yes | PlayerStageClearRecordDocument.HasCleared | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("ClearCount | PlayerStageClearRecord | yes | PlayerStageClearRecordDocument.ClearCount | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
            TestContext.WriteLine("ProcessedStageRunIds | PlayerStageClearRecord | yes | PlayerStageClearRecordDocument.ProcessedStageRunIds | yes | CampaignProfileDocumentMapper_MapsSaveSlotDataWithoutKnownLoss");
        }

        private static RepositoryHarness CreateHarness()
        {
            return new RepositoryHarness(
                Path.Combine("Temp", "CampaignProfileRepositoryTests", Guid.NewGuid().ToString("N")));
        }

        private sealed class QuarantineFailingTextFileStore : IAtomicTextFileStore
        {
            private readonly string _payload;

            public QuarantineFailingTextFileStore(string payload)
            {
                _payload = payload;
            }

            public bool Exists(string fileName)
            {
                return string.Equals(fileName, FileCampaignProfileRepository.ProfileFileName, StringComparison.Ordinal);
            }

            public string ReadAllText(string fileName)
            {
                return _payload;
            }

            public void WriteAllTextAtomic(string fileName, string contents)
            {
                throw new NotSupportedException();
            }

            public void WriteAllTextAtomicWithoutBackup(string fileName, string contents)
            {
                throw new NotSupportedException();
            }

            public bool Delete(string fileName)
            {
                return false;
            }

            public void EnsureDirectory()
            {
            }

            public bool TryRestoreBackup(string fileName)
            {
                return false;
            }

            public bool TryQuarantine(string fileName, out string quarantinePath)
            {
                quarantinePath = string.Empty;
                return false;
            }

            public bool TryQuarantine(string fileName, string suffix, out string quarantinePath)
            {
                quarantinePath = string.Empty;
                return false;
            }

            public void RecoverInterruptedWrite(string fileName)
            {
            }

            public void CleanupTempFiles(string fileName)
            {
            }
        }

        private sealed class FaultInjectingTextFileStore : IAtomicTextFileStore
        {
            private readonly Dictionary<string, string> _files =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public FaultInjectingTextFileStore(string canonical, string backup)
            {
                if (canonical != null)
                {
                    _files[FileCampaignProfileRepository.ProfileFileName] = canonical;
                }

                if (backup != null)
                {
                    _files[FileCampaignProfileRepository.ProfileFileName + ".bak"] = backup;
                }
            }

            public int CanonicalWriteFailuresRemaining { get; set; }

            public bool FailBackupDelete { get; set; }

            public bool RestoreBackupResult { get; set; } = true;

            public string RecoverFailureFileName { get; set; }

            public int TotalWriteCount { get; private set; }

            public void SetFile(string fileName, string contents)
            {
                _files[fileName] = contents;
            }

            public bool Exists(string fileName)
            {
                return _files.ContainsKey(fileName);
            }

            public string ReadAllText(string fileName)
            {
                return _files[fileName];
            }

            public void WriteAllTextAtomic(string fileName, string contents)
            {
                WriteAllTextAtomicWithoutBackup(fileName, contents);
            }

            public void WriteAllTextAtomicWithoutBackup(string fileName, string contents)
            {
                TotalWriteCount++;
                if (string.Equals(
                        fileName,
                        FileCampaignProfileRepository.ProfileFileName,
                        StringComparison.Ordinal) &&
                    CanonicalWriteFailuresRemaining > 0)
                {
                    CanonicalWriteFailuresRemaining--;
                    throw new InvalidOperationException("Injected canonical write failure.");
                }

                _files[fileName] = contents;
            }

            public bool Delete(string fileName)
            {
                if (FailBackupDelete &&
                    string.Equals(
                        fileName,
                        FileCampaignProfileRepository.ProfileFileName + ".bak",
                        StringComparison.Ordinal))
                {
                    return false;
                }

                return _files.Remove(fileName);
            }

            public void EnsureDirectory()
            {
            }

            public bool TryRestoreBackup(string fileName)
            {
                var backupFileName = fileName + ".bak";
                if (!RestoreBackupResult || !_files.TryGetValue(backupFileName, out var backup))
                {
                    return false;
                }

                _files[fileName] = backup;
                return true;
            }

            public bool TryQuarantine(string fileName, out string quarantinePath)
            {
                return TryQuarantine(fileName, "corrupt", out quarantinePath);
            }

            public bool TryQuarantine(string fileName, string suffix, out string quarantinePath)
            {
                quarantinePath = string.Empty;
                return false;
            }

            public void RecoverInterruptedWrite(string fileName)
            {
                if (string.Equals(fileName, RecoverFailureFileName, StringComparison.Ordinal))
                {
                    throw new IOException("Injected interrupted-write recovery failure.");
                }

                var rollbackFileName = fileName + ".rollback";
                if (!_files.TryGetValue(rollbackFileName, out var rollback))
                {
                    return;
                }

                if (!_files.ContainsKey(fileName))
                {
                    _files[fileName] = rollback;
                }

                _files.Remove(rollbackFileName);
            }

            public void CleanupTempFiles(string fileName)
            {
            }
        }

        private sealed class RepositoryHarness : IDisposable
        {
            private readonly string _testRootPath;

            public RepositoryHarness(string testRootPath)
            {
                _testRootPath = testRootPath;
                SaveRootPath = Path.Combine(testRootPath, "Saves");
                Store = new AtomicTextFileStore(SaveRootPath);
                Repository = new FileCampaignProfileRepository(Store);
            }

            public string SaveRootPath { get; }

            public AtomicTextFileStore Store { get; }

            public FileCampaignProfileRepository Repository { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public string BackupPath => ProfilePath + ".bak";

            public string LeftoverTempPath => Path.Combine(
                SaveRootPath,
                "profile.json.write.leftover.tmp");

            public string RollbackPath => ProfilePath + ".rollback";

            public string BackupTempPath => Path.Combine(
                SaveRootPath,
                "profile.json.bak.write.leftover.tmp");

            public string BackupRollbackPath => BackupPath + ".rollback";

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

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }
    }
}
