using System;
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
            Assert.That(document.LegacyImport, Is.Not.Null);
            Assert.That(document.Slots, Is.Not.Null);
            Assert.That(document.Slots, Is.Empty);
        }

        [Test]
        public void CampaignProfileDocument_RoundTripsThroughJsonUtility()
        {
            var document = new CampaignProfileDocument
            {
                SchemaVersion = 1,
                ProductVersion = "test-product",
                SavedAtUtc = "2026-07-06T09:00:00Z",
                ProfileId = "profile-a",
                LastPlayedSlotNumber = 2,
                LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportedSourceHash = "legacy-hash",
                    ImportDisabled = true,
                    ResetTombstoneUtc = "2026-07-06T10:00:00Z",
                },
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 2,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 3,
                        CampaignCompleted = false,
                        LastPlayedAtUtc = "2026-07-06T11:00:00Z",
                    },
                },
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignProfileDocument>(json);

            Assert.That(roundTripped.SchemaVersion, Is.EqualTo(1));
            Assert.That(roundTripped.ProductVersion, Is.EqualTo("test-product"));
            Assert.That(roundTripped.SavedAtUtc, Is.EqualTo("2026-07-06T09:00:00Z"));
            Assert.That(roundTripped.ProfileId, Is.EqualTo("profile-a"));
            Assert.That(roundTripped.LastPlayedSlotNumber, Is.EqualTo(2));
            Assert.That(roundTripped.LegacyImport.ImportedSourceHash, Is.EqualTo("legacy-hash"));
            Assert.That(roundTripped.LegacyImport.ImportDisabled, Is.True);
            Assert.That(roundTripped.LegacyImport.ResetTombstoneUtc, Is.EqualTo("2026-07-06T10:00:00Z"));
            Assert.That(roundTripped.Slots, Has.Length.EqualTo(1));
            Assert.That(roundTripped.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(roundTripped.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(roundTripped.Slots[0].LevelGroupId, Is.EqualTo("level-1"));
            Assert.That(roundTripped.Slots[0].RemainingChances, Is.EqualTo(3));
            Assert.That(roundTripped.Slots[0].CampaignCompleted, Is.False);
            Assert.That(roundTripped.Slots[0].LastPlayedAtUtc, Is.EqualTo("2026-07-06T11:00:00Z"));
        }

        [Test]
        public void CampaignLegacyImportDocument_RoundTripsThroughJsonUtility()
        {
            var document = new CampaignLegacyImportDocument
            {
                ImportedSourceHash = "source-hash",
                ImportDisabled = true,
                ResetTombstoneUtc = "2026-07-06T12:00:00Z",
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignLegacyImportDocument>(json);

            Assert.That(roundTripped.ImportedSourceHash, Is.EqualTo("source-hash"));
            Assert.That(roundTripped.ImportDisabled, Is.True);
            Assert.That(roundTripped.ResetTombstoneUtc, Is.EqualTo("2026-07-06T12:00:00Z"));
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
                    CampaignProfileLoadStatus.CorruptQuarantined,
                    CampaignProfileLoadStatus.CorruptNoFallback,
                    CampaignProfileLoadStatus.Unauthorized,
                    CampaignProfileLoadStatus.IoFailed,
                    CampaignProfileLoadStatus.SchemaInvalid,
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
        public void CampaignProfileRepository_ValidProfileReturnsLoaded()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, JsonUtility.ToJson(CreateDocument("profile-loaded")));

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.ProfileId, Is.EqualTo("profile-loaded"));
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
        public void CampaignProfileRepository_CorruptProfileWithoutValidBackupQuarantinesWithoutSilentReset()
        {
            using var harness = CreateHarness();
            const string corruptPayload = "{\"SchemaVersion\":";
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, corruptPayload);

            var result = harness.Repository.Load();
            var corruptFiles = Directory.GetFiles(harness.SaveRootPath, "profile.json.corrupt.*");

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.CorruptQuarantined));
            Assert.That(result.HasDocument, Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(corruptFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(corruptFiles[0]), Is.EqualTo(corruptPayload));
        }

        [Test]
        public void CampaignProfileRepository_ValidJsonWithInvalidSchemaReturnsSchemaInvalid()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":0,\"ProfileId\":\"profile-invalid\"}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.SchemaInvalid));
            Assert.That(result.HasDocument, Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
        }

        [Test]
        public void CampaignProfileRepository_MissingSlotsArrayNormalizesToEmptyArray()
        {
            using var harness = CreateHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":1,\"ProfileId\":\"profile-no-slots\"}");

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileLoadStatus.Loaded));
            Assert.That(result.Document.Slots, Is.Not.Null);
            Assert.That(result.Document.Slots, Is.Empty);
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
        public void CampaignProfileRepository_TestsUseTempPathNotPersistentDataPath()
        {
            using var harness = CreateHarness();

            Assert.That(harness.SaveRootPath, Does.StartWith(Path.Combine("Temp", "CampaignProfileRepositoryTests")));
            Assert.That(harness.SaveRootPath, Does.Not.Contain(Application.persistentDataPath));
        }

        private static CampaignProfileDocument CreateDocument(string profileId)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = 1,
                ProductVersion = "test-product",
                SavedAtUtc = "2026-07-06T09:00:00Z",
                ProfileId = profileId,
                LastPlayedSlotNumber = 1,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 1,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 3,
                        LastPlayedAtUtc = "2026-07-06T10:00:00Z",
                    },
                },
            };
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

            public string LeftoverTempPath => Path.Combine(SaveRootPath, "profile.leftover.tmp");

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
