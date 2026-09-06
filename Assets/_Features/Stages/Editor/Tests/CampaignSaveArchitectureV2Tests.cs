using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
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
        public void CampaignSlotRawDataMapper_MapsDiagnosticCarrierWithoutKnownLoss()
        {
            WriteFieldInventory();
            var slot = CreateLegacySlotFixture();

            var document = CampaignSlotRawDataMapper.ToProfileDocument(
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
        public void CampaignRawBoundary_ExposesExplicitRawAndImmutableConversions()
        {
            var methods = typeof(CampaignSlotRawDataMapper).GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            var methodNames = Array.ConvertAll(methods, method => method.Name);
            Assert.That(methodNames, Does.Contain("ToProfileDocument"));
            Assert.That(methodNames, Does.Contain("FromProfileDocument"));
            Assert.That(methodNames, Does.Contain("ToDocument"));
            Assert.That(methodNames, Does.Contain("FromDocument"));
            Assert.That(methodNames, Does.Contain("ToState"));
            Assert.That(methodNames, Does.Contain("ToEntry"));
        }

        [Test]
        public void CampaignDocumentMaterialization_HasOneExplicitPostValidationOwner()
        {
            var repository = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/FileCampaignProfileRepository.cs");
            var materializer = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocument.cs");
            var service = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");

            var validationIndex = repository.IndexOf(
                "CampaignProfileDocumentValidator.Validate(document)",
                StringComparison.Ordinal);
            var materializationIndex = repository.IndexOf(
                "CampaignProfileDocumentMaterializer.MaterializeValidated(document)",
                StringComparison.Ordinal);

            Assert.That(validationIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(materializationIndex, Is.GreaterThan(validationIndex));
            Assert.That(materializer, Does.Contain(
                "internal static class CampaignProfileDocumentMaterializer"));
            Assert.That(repository, Does.Not.Contain("private static void Normalize("));
            Assert.That(service, Does.Not.Contain("private static void Normalize("));
            Assert.That(service, Does.Not.Contain("Math.Max(0, profile.Version)"));
        }

        [Test]
        public void CampaignPerformanceProjection_UsesCanonicalStateWithoutRecoveryProjection()
        {
            var mapper = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocumentMapper.cs");
            var policy = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSlotDocument.cs");
            var achievement = File.ReadAllText(
                "Assets/_Features/Achievements/Achievement_CampaignIntegration/Runtime/CampaignStageAchievementIntegration.cs");

            Assert.That(mapper, Does.Not.Contain(
                "NormalStagePerformanceRecordPolicy.Normalize"));
            Assert.That(mapper, Does.Not.Contain("Math.Max(0, document.Version)"));
            Assert.That(mapper, Does.Not.Contain("Math.Max(0, record.ClearCount)"));
            Assert.That(policy, Does.Not.Contain(
                "public static NormalStagePerformanceRecord[] Normalize("));
            Assert.That(policy, Does.Not.Contain(
                "public static class NormalStagePerformanceRecordPolicy"));
            Assert.That(typeof(CampaignSlotState).GetProperty("NormalStagePerformanceRecords").PropertyType,
                Is.EqualTo(typeof(IReadOnlyList<CampaignStagePerformanceState>)));
            Assert.That(achievement, Does.Contain(
                "committedSlot.NormalStagePerformanceRecords"));
            Assert.That(achievement, Does.Not.Contain(
                "CampaignStageAchievementReadModelBuilder"));
            Assert.That(achievement, Does.Not.Contain(
                "NormalStagePerformanceRecordPolicy.Normalize"));
        }

        [Test]
        public void NormalStagePerformanceRecord_RoundTripsCanonicalRecordExactly()
        {
            var stageId = StageId.CreateOrThrow("stage-1-2");

            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                NormalStagePerformanceRecords = new[]
                {
                    new NormalStagePerformanceRecord
                    {
                        Version = NormalStagePerformanceRecord.CurrentVersion,
                        StageId = stageId,
                        BestCombinedPushFlipUses = 24,
                    },
                },
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
            var document = CampaignSlotRawDataMapper.ToDocument(slot);
            var roundTripped = CampaignSlotRawDataMapper.FromDocument(document)
                .NormalStagePerformanceRecords;

            Assert.That(roundTripped, Has.Length.EqualTo(1));
            Assert.That(roundTripped[0].StageId, Is.EqualTo(stageId));
            Assert.That(roundTripped[0].BestCombinedPushFlipUses, Is.EqualTo(24));
        }

        [Test]
        public void CampaignSlotRawDataMapper_NullStageClearProfileMapsToEmptyDocument()
        {
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
            };
            slot.StageClearProfileSnapshot = null;

            var document = CampaignSlotRawDataMapper.ToDocument(slot);

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
        public void CampaignSlotRawDataMapper_EmptyStageClearProfileMapsDeterministically()
        {
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
            var document = CampaignSlotRawDataMapper.ToDocument(slot)
                .StageClearProfileSnapshot;

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
        [TestCase("slot-chances-zero")]
        [TestCase("slot-chances-negative")]
        [TestCase("slot-chances-over-cap")]
        [TestCase("slot-deaths-negative")]
        [TestCase("receipt-invalid")]
        [TestCase("receipt-absent-populated")]
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
        [TestCase("slot-chances-zero")]
        [TestCase("slot-chances-negative")]
        [TestCase("slot-chances-over-cap")]
        [TestCase("slot-deaths-negative")]
        [TestCase("receipt-invalid")]
        [TestCase("receipt-absent-populated")]
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
        public void CampaignProfileRepository_NullNestedContainersMaterializeToEmpty()
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
        public void CampaignProfileDocumentMaterializer_MaterializesOnlyAllowedAbsence()
        {
            var document = CreateDocument("materializer-contract");
            var slot = document.Slots[0];
            document.ProductVersion = null;
            document.SavedAtUtc = null;
            slot.LevelGroupId = null;
            slot.LastPlayedAtUtc = null;
            slot.HasNormalCampaignCompletionReceipt = false;
            slot.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceiptDocument();
            slot.NormalStagePerformanceRecords = null;
            slot.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds = null;
            slot.StageClearProfileSnapshot.ProcessedStageRunIds = null;
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds = null;

            var validation = CampaignProfileDocumentValidator.Validate(document);

            Assert.That(validation, Is.EqualTo(CampaignProfileDocumentValidationResult.Valid));
            Assert.That(document.ProductVersion, Is.Null);
            Assert.That(slot.NormalCampaignCompletionReceipt, Is.Not.Null);
            Assert.That(slot.NormalStagePerformanceRecords, Is.Null);
            Assert.That(slot.StageClearProfileSnapshot.ProcessedStageRunIds, Is.Null);

            CampaignProfileDocumentMaterializer.MaterializeValidated(document);

            Assert.That(document.ProductVersion, Is.Empty);
            Assert.That(document.SavedAtUtc, Is.Empty);
            Assert.That(slot.LevelGroupId, Is.Empty);
            Assert.That(slot.LastPlayedAtUtc, Is.Empty);
            Assert.That(slot.NormalCampaignCompletionReceipt, Is.Null);
            Assert.That(slot.NormalStagePerformanceRecords, Is.Empty);
            Assert.That(slot.StageClearProfileSnapshot.Records[0].ProcessedStageRunIds, Is.Empty);
            Assert.That(slot.StageClearProfileSnapshot.ProcessedStageRunIds, Is.Empty);
            Assert.That(slot.StageClearProfileSnapshot.ProcessedClearAttemptIds, Is.Empty);
        }

        [TestCase("clear-negative")]
        [TestCase("slot-chances-zero")]
        [TestCase("slot-chances-negative")]
        [TestCase("slot-chances-over-cap")]
        [TestCase("slot-deaths-negative")]
        [TestCase("receipt-invalid")]
        [TestCase("receipt-absent-populated")]
        [TestCase("receipt-absent-mixed-default-strings")]
        [TestCase("receipt-absent-null-pair-version-nonzero")]
        [TestCase("receipt-absent-empty-pair-version-nonzero")]
        [TestCase("receipt-absent-null-pair-source-nonzero")]
        [TestCase("receipt-absent-empty-pair-source-nonzero")]
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

        [TestCase(CampaignReceiptPresence.Absent)]
        [TestCase(CampaignReceiptPresence.PresentWithoutPayload)]
        [TestCase(CampaignReceiptPresence.PresentWithPayload)]
        public void CampaignProfileRepository_SaveJsonLoadPreservesReceiptPresence(
            CampaignReceiptPresence presence)
        {
            using var harness = CreateHarness();
            var document = CreateDocument($"receipt-{presence}");
            var slot = document.Slots[0];
            slot.HasNormalCampaignCompletionReceipt =
                presence != CampaignReceiptPresence.Absent;
            slot.NormalCampaignCompletionReceipt =
                presence == CampaignReceiptPresence.PresentWithPayload
                    ? new NormalCampaignCompletionReceiptDocument
                    {
                        Version = NormalCampaignCompletionReceipt.CurrentVersion,
                        CompletedStageId = "stage-1-1",
                        StageRunId = string.Empty,
                        ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                    }
                    : null;

            harness.Repository.Save(document);
            var loaded = harness.Repository.Load();

            Assert.That(loaded.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            var parsed = CampaignSlotParser.ParseEntry(1, loaded.Document.Slots[0]);
            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Entry.State.Receipt.Presence, Is.EqualTo(presence));
            if (presence == CampaignReceiptPresence.PresentWithPayload)
            {
                Assert.That(parsed.Entry.State.Receipt.Payload.Version,
                    Is.EqualTo(NormalCampaignCompletionReceipt.CurrentVersion));
                Assert.That(parsed.Entry.State.Receipt.Payload.CompletedStageId.Value,
                    Is.EqualTo("stage-1-1"));
                Assert.That(parsed.Entry.State.Receipt.Payload.StageRunId, Is.Empty);
                Assert.That(parsed.Entry.State.Receipt.Payload.ClearSource,
                    Is.EqualTo(NormalCampaignCompletionReceipt.LegacyClearSourceAbsent));
            }
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
            Assert.That(source, Does.Contain("`CampaignSlotStateDocumentMapper`"));
            Assert.That(source, Does.Contain("`ICampaignSlotSeedImportPort`"));
            Assert.That(source, Does.Contain("exactly two valid in-memory string shapes"));
            Assert.That(source, Does.Contain("both receipt strings are null"));
            Assert.That(source, Does.Contain("both are empty"));
            Assert.That(source, Does.Contain("Mixed null/empty receipt strings"));
            Assert.That(source, Does.Contain("non-mutating validating raw conversion boundary"));
            Assert.That(source, Does.Contain("post-`JsonUtility`"));
            Assert.That(source, Does.Contain("reject rather than"));
            Assert.That(source, Does.Contain("restores `PresentWithoutPayload`"));
            Assert.That(source, Does.Contain("`CampaignSlotState` construction owns"));
            Assert.That(source, Does.Contain("`CampaignSlotTransitionEngine.UpsertPerformance` owns"));
            Assert.That(source, Does.Not.Contain("NormalStagePerformanceRecordPolicy"));
            Assert.That(source, Does.Not.Contain("Complete maintenance replacement"));
            Assert.That(source, Does.Not.Contain("shared slot canonicalizer"));
            Assert.That(source, Does.Not.Contain("pending Phase 5 removal"));
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
            Assert.That(section, Does.Contain("CampaignSlotStateDocumentMapper"));
            Assert.That(section, Does.Contain("CampaignSlotRawDataMapper"));
            Assert.That(section, Does.Contain("non-mutating validating raw conversion"));
            Assert.That(section, Does.Contain("post-`JsonUtility` object shape"));
            Assert.That(section, Does.Contain("paired shape로 보정하지 않는다"));
            Assert.That(section, Does.Contain("CampaignSlotTransitionEngine"));
            Assert.That(section, Does.Contain("`CampaignSlotState` construction"));
            Assert.That(section, Does.Contain("`CampaignSlotTransitionEngine.UpsertPerformance`"));
            Assert.That(section, Does.Contain(
                "두 문자열이 모두 null이거나 모두 빈 문자열"));
            Assert.That(section, Does.Contain("혼합된 null/빈 문자열 residue"));
            Assert.That(section, Does.Contain("`PresentWithoutPayload`로 복원"));
            Assert.That(section, Does.Not.Contain("NormalStagePerformanceRecordPolicy"));
            Assert.That(section, Does.Contain("CampaignSlotLaunchEvaluator"));
            Assert.That(section, Does.Contain("CampaignSlotActionPolicy"));
            Assert.That(section, Does.Not.Contain("CampaignProfileDocumentMapper.ToDocument"));
            Assert.That(section, Does.Not.Contain("CampaignSlotCanonicalizer"));
            Assert.That(section, Does.Not.Contain("CampaignStageAchievementReadModelBuilder"));
            Assert.That(section, Does.Not.Contain("제거는 Phase 5 범위다"));
            Assert.That(section, Does.Contain("first-public schema freeze"));
            Assert.That(section, Does.Contain("release exposure"));
            Assert.That(section, Does.Contain("ICampaignProgressionCommitter.CommitStageClear"));
            Assert.That(section, Does.Contain("휴면 `CampaignSaveService.ApplyDeath` / `ApplyStageClear` command는 제거"));
            Assert.That(section, Does.Contain("normalization 전에 검증"));
            Assert.That(section, Does.Contain("음수 slot counter"));
            Assert.That(section, Does.Contain("`LoadAllWithReport`만 blocked profile"));
            Assert.That(section, Does.Contain("backup을 canonical보다 먼저"));
        }

        [Test]
        public void CampaignReceiptSerializerResidue_UsesOnlyExactPairedStringShapes()
        {
            var documentSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocument.cs");
            var stateSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSlotState.cs");

            Assert.That(documentSource, Does.Contain("IsExactDefaultReceiptResidue"));
            Assert.That(documentSource, Does.Contain("receipt.Version == 0"));
            Assert.That(documentSource, Does.Contain("receipt.ClearSource == 0"));
            Assert.That(documentSource, Does.Contain("receipt.CompletedStageId == null"));
            Assert.That(documentSource, Does.Contain("receipt.StageRunId == null"));
            Assert.That(documentSource, Does.Contain("receipt.CompletedStageId == string.Empty"));
            Assert.That(documentSource, Does.Contain("receipt.StageRunId == string.Empty"));
            Assert.That(documentSource, Does.Not.Contain(
                "string.IsNullOrEmpty(receipt.CompletedStageId)"));
            Assert.That(stateSource, Does.Contain(
                "CampaignSlotDocumentValidator.IsExactDefaultReceiptResidue(receipt)"));

            var rawMapperSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocumentMapper.cs");
            Assert.That(rawMapperSource, Does.Not.Contain(
                "CompletedStageId = receipt.CompletedStageId ?? string.Empty"));
            Assert.That(rawMapperSource, Does.Not.Contain(
                "StageRunId = receipt.StageRunId ?? string.Empty"));
        }

        [Test]
        public void GameplayTestGuide_LabelsSupersededCampaignSaveRowsAsHistorical()
        {
            var guide = File.ReadAllText(
                "Docs/Testing/Gameplay-Test-Automation-Guide.md");

            Assert.That(guide, Does.Contain(
                "두 행은 당시 과도기 구조의 역사 기록이다"));
            Assert.That(guide, Does.Contain(
                "two 2026-08-24 typed-committer and slot-clone/canonicalization rows are historical records"));
            Assert.That(guide, Does.Contain(
                "campaign-save exact serializer-residue follow-up"));
            Assert.That(guide, Does.Contain(
                "two exact paired string shapes"));
            Assert.That(guide, Does.Contain(
                "physical Save→JSON→Load preserves absent, present-null, and present-payload states"));
            Assert.That(guide, Does.Contain(
                "campaign-save raw receipt mapper boundary closeout"));
            Assert.That(guide, Does.Contain("EditMode `179/4`"));
            Assert.That(guide, Does.Contain("15-fixture touched cluster `590/0`"));

            const string englishHeading = "### English Original";
            const string nextHeading =
                "## Visual runner interruption contract / visual runner 중단 계약";
            var englishStart = guide.IndexOf(englishHeading, StringComparison.Ordinal);
            var englishEnd = guide.IndexOf(nextHeading, StringComparison.Ordinal);
            Assert.That(englishStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(englishEnd, Is.GreaterThan(englishStart));
            var englishSection = guide.Substring(
                englishStart,
                englishEnd - englishStart);
            Assert.That(englishSection, Does.Contain(
                "campaign-save raw receipt mapper boundary closeout"));
            Assert.That(englishSection, Does.Contain("EditMode `179/4`"));
            Assert.That(englishSection, Does.Contain(
                "15-fixture touched cluster `590/0`"));
            Assert.That(englishSection, Does.Contain(
                "/mnt/d/J2M/evidence/20260825-075401-campaign-save-receipt-raw-boundary-closeout/"));
            Assert.That(englishSection, Does.Contain(
                "broad unfiltered `full` lane and manual Player/build smoke were not run"));
            Assert.That(englishSection, Does.Contain(
                "campaign-save final structural audit closeout"));
            Assert.That(englishSection, Does.Contain("EditMode `160/3`"));
            Assert.That(englishSection, Does.Contain(
                "15-fixture touched cluster `592/0`"));
            Assert.That(englishSection, Does.Contain(
                "/mnt/d/J2M/evidence/20260825-084058-campaign-save-final-structure-closeout/"));

            var remediation = File.ReadAllText(
                "Docs/Architecture/Campaign-Save-Long-Term-Structural-Remediation.md");
            Assert.That(remediation, Does.Contain(
                "### 13.3 Raw receipt mapper boundary closeout"));
            Assert.That(remediation, Does.Contain(
                "### Historical post-package verification notes"));
            Assert.That(remediation, Does.Contain(
                "post-`JsonUtility` object-shape 계약"));
            Assert.That(remediation, Does.Contain(
                "### 13.4 Final structural audit closeout"));
            Assert.That(remediation, Does.Contain(
                "`FromDocuments`/`ToRawSlots` duplicate-slot 경로"));
        }

        [Test]
        public void CampaignMutationBoundary_ExposesOnlyTypedPortsAndCommands()
        {
            var ports = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/CampaignSavePorts.cs");
            var service = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");
            var gameplayFlow = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs");
            var mainMenu = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(ports, Does.Not.Contain("interface ICampaignSaveSlotStore"));
            Assert.That(ports, Does.Not.Contain("Action<SaveSlotData>"));
            Assert.That(ports, Does.Contain("ICampaignProgressionCommitter"));
            Assert.That(ports, Does.Contain("ICampaignContinuePreparationPort"));
            Assert.That(ports, Does.Not.Contain("ICampaignSlotMaintenancePort"));
            Assert.That(ports, Does.Not.Contain("ReplaceValidatedSlot"));
            Assert.That(service, Does.Not.Contain(
                "public CampaignSaveServiceResult ApplyDeath("));
            Assert.That(service, Does.Not.Contain(
                "public CampaignSaveServiceResult ApplyStageClear("));
            Assert.That(service, Does.Not.Contain("UpdateSlot("));
            Assert.That(gameplayFlow, Does.Not.Contain("UpdateSlot("));
            Assert.That(gameplayFlow, Does.Contain("CommitDeath("));
            Assert.That(gameplayFlow, Does.Contain("CommitStageClear("));
            Assert.That(mainMenu, Does.Contain("ICampaignContinuePreparationPort"));
            Assert.That(mainMenu, Does.Contain("PrepareContinue("));
            Assert.That(mainMenu, Does.Not.Contain("ICampaignSlotMaintenancePort"));
            Assert.That(mainMenu, Does.Not.Contain("ReplaceValidatedSlot("));
        }

        [Test]
        public void CampaignTransitionEngine_UsesImmutableStateAndOwnsComicCompletion()
        {
            var engineType = typeof(CampaignSlotTransitionEngine);
            var death = engineType.GetMethod(
                "ApplyDeath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var clear = engineType.GetMethod(
                "ApplyStageClear",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var comic = engineType.GetMethod(
                "ApplyComicCompletion",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var resultSlot = typeof(CampaignSlotTransitionResult).GetProperty("Slot");
            var introServiceMethod = typeof(CampaignSaveService).GetMethod(
                "SetIntroComicCompleted");
            var outroServiceMethod = typeof(CampaignSaveService).GetMethod(
                "SetOutroComicCompleted");

            Assert.That(death, Is.Not.Null);
            Assert.That(clear, Is.Not.Null);
            Assert.That(comic, Is.Not.Null);
            Assert.That(introServiceMethod, Is.Not.Null);
            Assert.That(outroServiceMethod, Is.Not.Null);
            Assert.That(death.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(clear.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(comic.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(resultSlot.PropertyType, Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(introServiceMethod.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(outroServiceMethod.GetParameters(), Has.Length.EqualTo(1));
        }

        [Test]
        public void CampaignStoreInternals_UseImmutableStateWithoutCanonicalizerEntryAdapter()
        {
            var service = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");
            var transient = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/TransientCampaignState.cs");

            Assert.That(transient,
                Does.Contain("Dictionary<string, CampaignSlotEntry[]>"));
            Assert.That(transient,
                Does.Not.Contain("Dictionary<string, SaveSlotData[]>"));
            Assert.That(transient,
                Does.Not.Contain("CampaignSlotCanonicalizer.CreateValidatedCopy"));
            Assert.That(transient, Does.Contain("CampaignSlotStateFactory.CreateNewGame"));
            Assert.That(transient, Does.Contain("ApplyComicCompletion"));
            Assert.That(service, Does.Contain("CampaignSlotParser.ParseEntry"));
            Assert.That(service, Does.Contain("CampaignSlotStateDocumentMapper.ToDocument"));
            Assert.That(service, Does.Contain("CampaignSlotStateFactory.CreateNewGame"));
            Assert.That(service, Does.Contain("ApplyComicCompletion"));
            Assert.That(service, Does.Not.Contain("CampaignSlotMapper.ToDomain"));
            Assert.That(service, Does.Not.Contain("CampaignSlotMapper.ToDocument"));
        }

        [Test]
        public void CampaignMutableCompatibilitySurface_IsRemovedAfterConsumerMigration()
        {
            var concreteRuntimeTypes = new[]
            {
                typeof(CampaignSaveSlotStoreAdapter),
                typeof(TransientCampaignSaveSlotStore),
            };
            foreach (var runtimeType in concreteRuntimeTypes)
            {
                var publicMethods = runtimeType.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (var method in publicMethods)
                {
                    Assert.That(method.ReturnType.FullName,
                        Is.Not.EqualTo("Game.Feature.Stages.SaveSlotData"),
                        $"{runtimeType.Name}.{method.Name} returns the mutable carrier.");
                    Assert.That(method.ReturnType.FullName,
                        Is.Not.EqualTo("Game.Feature.Stages.SaveSlotData[]"),
                        $"{runtimeType.Name}.{method.Name} returns mutable carriers.");
                    Assert.That(Array.Exists(method.GetParameters(), parameter =>
                            parameter.ParameterType.FullName ==
                            "Game.Feature.Stages.SaveSlotData"),
                        Is.False,
                        $"{runtimeType.Name}.{method.Name} accepts the mutable carrier.");
                }
            }

            var removedRuntimeTokens = new[]
            {
                "CampaignSlotStateCompatibilityAdapter",
                "CampaignSlotFixtureCompatibilityProjection",
                "CampaignSaveTestFixture",
                "CampaignSlotPlanningCompatibilityExtensions",
                "ReplaceValidatedSlot",
                "CampaignSlotCanonicalizer",
                "CampaignSlotDomainValidator",
            };
            var runtimePaths = Directory.GetFiles(
                "Assets/_Features",
                "*.cs",
                SearchOption.AllDirectories);
            foreach (var path in runtimePaths)
            {
                var normalizedPath = path.Replace('\\', '/');
                if (!normalizedPath.Contains("/Runtime/"))
                {
                    continue;
                }

                var source = File.ReadAllText(path);
                foreach (var token in removedRuntimeTokens)
                {
                    Assert.That(source, Does.Not.Contain(token),
                        $"{normalizedPath} retains Phase 5 compatibility token {token}.");
                }
            }

            var rawBoundary = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/" +
                "CampaignProfileDocumentMapper.cs");
            Assert.That(rawBoundary, Does.Not.Contain("class CampaignProfileDocumentMapper"));
            Assert.That(rawBoundary, Does.Not.Contain("class CampaignSlotMapper"));
            Assert.That(rawBoundary, Does.Contain("class CampaignSlotRawDataMapper"));
        }

        [Test]
        public void CampaignConsumerPorts_ExposeImmutableSlotEntriesAndPurposeNamedCommands()
        {
            var loadAll = typeof(ICampaignSaveQuery).GetMethod("LoadAll");
            var loadSlot = typeof(ICampaignSaveQuery).GetMethod("LoadSlot");
            var initialize = typeof(ICampaignSlotLifecyclePort).GetMethod("InitializeNewGame");
            var diagnostic = typeof(ICampaignDiagnosticSlotPort).GetMethod(
                "SetActiveStageForDiagnostics");
            var intro = typeof(ICampaignComicProgressPort).GetMethod(
                "MarkIntroComicCompleted");
            var outro = typeof(ICampaignComicProgressPort).GetMethod(
                "MarkOutroComicCompleted");

            Assert.That(loadAll.ReturnType, Is.EqualTo(typeof(CampaignSlotEntry[])));
            Assert.That(loadSlot.ReturnType, Is.EqualTo(typeof(CampaignSlotEntry)));
            Assert.That(initialize.ReturnType, Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(diagnostic.ReturnType, Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(typeof(CampaignDeathCommitResult).GetProperty("Slot").PropertyType,
                Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(typeof(CampaignStageClearCommitResult).GetProperty("Slot").PropertyType,
                Is.EqualTo(typeof(CampaignSlotState)));
            Assert.That(intro.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(outro.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(typeof(ICampaignSlotSeedImportPort).GetMethod("ImportSlotSeed"),
                Is.Not.Null);
        }

        [Test]
        public void CampaignRuntimeConsumers_DoNotNameTheMutableSlotCarrier()
        {
            var consumerPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs",
                "Assets/_Features/Achievements/Achievement_CampaignIntegration/Runtime/CampaignStageAchievementIntegration.cs",
                "Assets/_Features/Achievements/Achievement_CampaignIntegration/Runtime/CampaignStageAchievementStartupReconciler.cs",
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs",
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuSlotViewModelMapper.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/SlotComicProgressStore.cs",
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs",
                "Assets/_Features/Stages/Runtime/Load/PlayerCaptureLaunchBootstrap.cs",
            };

            foreach (var path in consumerPaths)
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain("SaveSlotData"), path);
            }
        }

        [Test]
        public void MainMenuPresentation_ConsumesSeparatedLaunchResultsWithoutValidationFacade()
        {
            const string validationFacadePath =
                "Assets/_Features/Stages/Runtime/Campaign/SaveSlotValidationService.cs";
            var controller = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var mapper = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuSlotViewModelMapper.cs");

            Assert.That(File.Exists(validationFacadePath), Is.False);
            Assert.That(controller, Does.Contain("CampaignSlotLaunchEvaluator"));
            Assert.That(controller, Does.Not.Contain("SaveSlotValidationResult"));
            Assert.That(controller, Does.Not.Contain("SaveSlotValidationStatus"));
            Assert.That(controller, Does.Not.Contain("SaveSlotValidationService"));
            Assert.That(mapper, Does.Contain("CampaignSlotLaunchEvaluation"));
            Assert.That(mapper, Does.Contain("CampaignSlotActionPolicy"));
            Assert.That(mapper, Does.Not.Contain("SaveSlotValidationResult"));
            Assert.That(mapper, Does.Not.Contain("SaveSlotValidationStatus"));
            Assert.That(mapper, Does.Not.Contain("SaveSlotValidationService"));
        }

        [Test]
        public void CampaignConsumers_RequestNarrowPortsWithoutRuntimeCastingOrMaintenanceWrites()
        {
            var consumerPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                "Assets/_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/SlotComicProgressStore.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ComicIntroStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ComicOutroMainMenuReturnRouter.cs",
                "Assets/_Features/Achievements/Achievement_CampaignIntegration/Runtime/CampaignStageAchievementIntegration.cs",
            };

            foreach (var path in consumerPaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain(" as ICampaign"), path);
                Assert.That(source, Does.Not.Contain("ICampaignSlotMaintenancePort"), path);
                Assert.That(source, Does.Not.Contain("ReplaceValidatedSlot("), path);
            }
        }

        [Test]
        public void CampaignCanonicalState_ConstructionAndMutationSurfaceIsOwnerRestricted()
        {
            var stateTypes = new[]
            {
                typeof(CampaignSlotState),
                typeof(CampaignSlotEntry),
                typeof(CampaignReceiptState),
                typeof(CampaignCompletionReceiptState),
                typeof(CampaignStagePerformanceState),
                typeof(CampaignStageClearProfileState),
                typeof(CampaignStageClearRecordState),
            };

            foreach (var stateType in stateTypes)
            {
                Assert.That(
                    stateType.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
                    Is.Empty,
                    $"{stateType.FullName} exposes a public constructor.");
                foreach (var property in stateType.GetProperties(
                             BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.That(property.SetMethod, Is.Null,
                        $"{stateType.FullName}.{property.Name} exposes a public setter.");
                }
            }

            var constructionTokens = new[]
            {
                "new CampaignSlotState(",
                "new CampaignCompletionReceiptState(",
                "new CampaignStagePerformanceState(",
                "new CampaignStageClearProfileState(",
                "new CampaignStageClearRecordState(",
            };
            var allowedOwnerPaths = new[]
            {
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSlotState.cs",
                "Assets/_Features/Stages/Runtime/Campaign/CampaignSlotTransitionEngine.cs",
            };
            var runtimePaths = Directory.GetFiles(
                "Assets/_Features",
                "*.cs",
                SearchOption.AllDirectories);
            foreach (var path in runtimePaths)
            {
                var normalizedPath = path.Replace('\\', '/');
                if (!normalizedPath.Contains("/Runtime/"))
                {
                    continue;
                }

                var source = File.ReadAllText(path);
                foreach (var token in constructionTokens)
                {
                    if (!source.Contains(token))
                    {
                        continue;
                    }

                    Assert.That(allowedOwnerPaths, Does.Contain(normalizedPath),
                        $"Canonical state construction escaped its parser/factory/engine owners: {path}");
                }
            }
        }

        [Test]
        public void CampaignProductionAndTransient_ComposeTheSameAuthoritativeTransitionEngine()
        {
            var service = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");
            var transient = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/TransientCampaignState.cs");

            foreach (var source in new[] { service, transient })
            {
                Assert.That(CountOccurrences(
                    source,
                    "CampaignSlotTransitionEngine.ApplyDeath"), Is.EqualTo(1));
                Assert.That(CountOccurrences(
                    source,
                    "CampaignSlotTransitionEngine.ApplyStageClear"), Is.EqualTo(1));
                Assert.That(CountOccurrences(
                    source,
                    "CampaignSlotTransitionEngine.ApplyComicCompletion"),
                    Is.GreaterThanOrEqualTo(1));
            }

            Assert.That(service, Does.Not.Contain("CommitDeathCore"));
            Assert.That(service, Does.Not.Contain("CommitStageClearCore"));
            Assert.That(transient, Does.Not.Contain("CommitDeathCore"));
            Assert.That(transient, Does.Not.Contain("CommitStageClearCore"));
        }

        [Test]
        public void CampaignServiceTransientUiAndDirectPlay_DoNotDirectlyAssignGameplaySlotFields()
        {
            var guardedPaths = new[]
            {
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs",
                "Assets/_Features/Stages/Runtime/Campaign/TransientCampaignState.cs",
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs",
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuSlotViewModelMapper.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/SlotComicProgressStore.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ComicIntroStageLaunchRouter.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/ComicOutroMainMenuReturnRouter.cs",
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs",
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayWindow.cs",
                "Assets/_Features/Stages/Runtime/Load/PlayerCaptureLaunchBootstrap.cs",
            };
            var directAssignment = new Regex(
                @"\.(CurrentStageId|CurrentLevelGroupId|RemainingChances|CampaignCompleted|" +
                @"IntroComicCompleted|OutroComicCompleted|TotalDeaths|" +
                @"NormalStagePerformanceRecords|StageClearProfile|Receipt)\s*" +
                @"(=(?!=)|\+=|-=|\+\+|--)",
                RegexOptions.CultureInvariant);

            foreach (var path in guardedPaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(directAssignment.IsMatch(source), Is.False,
                    $"{path} directly assigns an authoritative gameplay slot field.");
            }
        }

        [Test]
        public void CampaignStrictPersistenceMapper_HasOneCompleteWriteSurfaceAndNoRepairLogic()
        {
            var stateSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSlotState.cs");
            var mapperStart = stateSource.IndexOf(
                "internal static class CampaignSlotStateDocumentMapper",
                StringComparison.Ordinal);
            var mapperEnd = stateSource.IndexOf(
                "internal static class CampaignSlotRawDocumentCloner",
                mapperStart,
                StringComparison.Ordinal);
            Assert.That(mapperStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(mapperEnd, Is.GreaterThan(mapperStart));
            var mapperSource = stateSource.Substring(
                mapperStart,
                mapperEnd - mapperStart);

            Assert.That(mapperSource, Does.Not.Contain("continue;"));
            Assert.That(mapperSource, Does.Not.Contain("Math.Max("));
            Assert.That(mapperSource, Does.Not.Contain("Math.Min("));
            Assert.That(mapperSource, Does.Not.Contain("Mathf.Clamp("));
            Assert.That(mapperSource, Does.Not.Contain(".Normalize("));
            Assert.That(mapperSource, Does.Not.Contain("DocumentValidator"));

            var mapperType = typeof(CampaignSlotState).Assembly.GetType(
                "Game.Feature.Stages.CampaignSlotStateDocumentMapper");
            Assert.That(mapperType, Is.Not.Null);
            var methods = mapperType.GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (method.Name == "ToDocument")
                {
                    Assert.That(method.GetParameters(), Has.Length.EqualTo(1));
                    Assert.That(method.GetParameters()[0].ParameterType,
                        Is.EqualTo(typeof(CampaignSlotState)));
                    Assert.That(method.ReturnType,
                        Is.EqualTo(typeof(CampaignSlotDocument)));
                    continue;
                }

                Assert.That(method.IsPrivate, Is.True,
                    $"Strict persistence fragment {method.Name} is externally callable.");
            }
        }

        [Test]
        public void CampaignRuntimeConsumers_CannotRequestRawFragmentsOrFullReplacement()
        {
            var rawMapperPublicMethods = typeof(CampaignSlotRawDataMapper).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in rawMapperPublicMethods)
            {
                Assert.That(method.Name, Does.Not.Contain("Receipt"));
                Assert.That(method.Name, Does.Not.Contain("PerformanceRecord"));
                Assert.That(method.Name, Does.Not.Contain("StageClearProfile"));
                Assert.That(method.Name, Does.Not.Contain("PlayerStageClearRecord"));
            }

            var allowedRawBoundaryPaths = new[]
            {
                "Assets/_Features/Stages/Runtime/Campaign/SaveSlotModels.cs",
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocumentMapper.cs",
            };
            var runtimePaths = Directory.GetFiles(
                "Assets/_Features",
                "*.cs",
                SearchOption.AllDirectories);
            foreach (var path in runtimePaths)
            {
                var normalizedPath = path.Replace('\\', '/');
                if (!normalizedPath.Contains("/Runtime/"))
                {
                    continue;
                }

                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("ReplaceValidatedSlot"), path);
                if (source.Contains("CampaignSlotRawDataMapper"))
                {
                    Assert.That(allowedRawBoundaryPaths, Does.Contain(normalizedPath),
                        $"Runtime consumer requested the public raw mapper: {path}");
                }
            }
        }

        [Test]
        public void CampaignPersistenceComposition_HasNoPlayerPrefsCompatibilityPath()
        {
            var compositionPaths = new[]
            {
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs",
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveServiceFactory.cs",
                "Assets/_Features/Stages/Runtime/Campaign/ActiveSlotStorage.cs",
                "Assets/_Features/Stages/Runtime/Campaign/Save/FileCampaignProfileRepository.cs",
            };
            var forbiddenTokens = new[]
            {
                "PlayerPrefs.",
                "LegacyPlayerPrefsCampaignImporter",
                "CampaignSaveMigrationCoordinator",
                "PlayerPrefsCampaign",
                "PlayerPrefsActiveSlot",
                "DeleteKey(",
            };

            foreach (var path in compositionPaths)
            {
                var source = File.ReadAllText(path);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token),
                        $"Campaign persistence composition regained {token}: {path}");
                }
            }

            var assembly = typeof(CampaignSaveCompositionProvider).Assembly;
            Assert.That(assembly.GetType(
                "Game.Feature.Stages.LegacyPlayerPrefsCampaignImporter"), Is.Null);
            Assert.That(assembly.GetType(
                "Game.Feature.Stages.CampaignSaveMigrationCoordinator"), Is.Null);
        }

        [Test]
        public void CampaignSavedChanceContract_RejectsZeroAtPolicyCommandAndDocumentBoundaries()
        {
            Assert.That(CampaignSaveSlotPolicy.DefaultRemainingChances, Is.EqualTo(3));
            Assert.That(CampaignSaveSlotPolicy.MaxRemainingChances, Is.EqualTo(3));
            Assert.That(CampaignSaveSlotPolicy.IsValidRemainingChances(0), Is.False);
            Assert.That(CampaignSaveSlotPolicy.IsValidRemainingChances(1), Is.True);
            Assert.That(CampaignSaveSlotPolicy.IsValidRemainingChances(2), Is.True);
            Assert.That(CampaignSaveSlotPolicy.IsValidRemainingChances(3), Is.True);
            Assert.That(CampaignSaveSlotPolicy.IsValidRemainingChances(4), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CampaignSlotSeedImportRequest(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    "level-1",
                    0,
                    string.Empty));

            var invalidDocument = CreateDocument("chance-zero-guard");
            invalidDocument.Slots[0].RemainingChances = 0;
            Assert.That(CampaignProfileDocumentValidator.Validate(invalidDocument),
                Is.EqualTo(CampaignProfileDocumentValidationResult.InvalidDocument));
            var parse = CampaignSlotParser.ParseEntry(1, invalidDocument.Slots[0]);
            Assert.That(parse.IsSuccess, Is.False);
            Assert.That(parse.Entry, Is.Null);

            var saveBoundaryPaths = new[]
            {
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSlotState.cs",
                "Assets/_Features/Stages/Runtime/Campaign/CampaignSlotTransitionEngine.cs",
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs",
                "Assets/_Features/Stages/Runtime/Campaign/TransientCampaignState.cs",
                "Assets/_Features/Stages/Runtime/Campaign/CampaignSavePorts.cs",
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs",
                "Assets/_Features/Stages/Runtime/Load/PlayerCaptureLaunchBootstrap.cs",
            };
            foreach (var path in saveBoundaryPaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Match(@"\bRemainingChances\s*=\s*0\b"),
                    $"Saved chance zero sentinel re-entered {path}.");
                Assert.That(source, Does.Not.Match(
                        @"(?:Math\.Max|Mathf\.Clamp)\([^\r\n]*0[^\r\n]*RemainingChances"),
                    $"Saved chance zero normalization re-entered {path}.");
            }
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

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
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
                case "slot-chances-zero":
                    slot.RemainingChances = 0;
                    break;
                case "slot-chances-over-cap":
                    slot.RemainingChances = CampaignSaveSlotPolicy.MaxRemainingChances + 1;
                    break;
                case "slot-deaths-negative":
                    slot.TotalDeaths = -1;
                    break;
                case "receipt-invalid":
                    slot.HasNormalCampaignCompletionReceipt = true;
                    slot.NormalCampaignCompletionReceipt =
                        new NormalCampaignCompletionReceiptDocument
                        {
                            Version = NormalCampaignCompletionReceipt.CurrentVersion + 1,
                            CompletedStageId = "stage-1-1",
                            StageRunId = string.Empty,
                            ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                        };
                    break;
                case "receipt-absent-populated":
                    slot.HasNormalCampaignCompletionReceipt = false;
                    slot.NormalCampaignCompletionReceipt =
                        new NormalCampaignCompletionReceiptDocument
                        {
                            Version = NormalCampaignCompletionReceipt.CurrentVersion,
                            CompletedStageId = "stage-1-1",
                            StageRunId = string.Empty,
                            ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
                        };
                    break;
                case "receipt-absent-mixed-default-strings":
                    slot.HasNormalCampaignCompletionReceipt = false;
                    slot.NormalCampaignCompletionReceipt =
                        new NormalCampaignCompletionReceiptDocument
                        {
                            Version = 0,
                            CompletedStageId = string.Empty,
                            StageRunId = null,
                            ClearSource = 0,
                        };
                    break;
                case "receipt-absent-null-pair-version-nonzero":
                    SetAbsentReceiptResidue(slot, 1, null, null, 0);
                    break;
                case "receipt-absent-empty-pair-version-nonzero":
                    SetAbsentReceiptResidue(slot, 1, string.Empty, string.Empty, 0);
                    break;
                case "receipt-absent-null-pair-source-nonzero":
                    SetAbsentReceiptResidue(slot, 0, null, null, 1);
                    break;
                case "receipt-absent-empty-pair-source-nonzero":
                    SetAbsentReceiptResidue(slot, 0, string.Empty, string.Empty, 1);
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

        private static void SetAbsentReceiptResidue(
            CampaignSlotDocument slot,
            int version,
            string completedStageId,
            string stageRunId,
            int clearSource)
        {
            slot.HasNormalCampaignCompletionReceipt = false;
            slot.NormalCampaignCompletionReceipt =
                new NormalCampaignCompletionReceiptDocument
                {
                    Version = version,
                    CompletedStageId = completedStageId,
                    StageRunId = stageRunId,
                    ClearSource = clearSource,
                };
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
