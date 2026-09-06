using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Product.Achievements.Composition;
using Game.Product.Achievements.Infrastructure;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class FileProductAchievementRepositoryTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "j2m-product-achievements-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public void MissingDocument_ReturnsUsableEmptyV1WithoutCreatingFile()
        {
            var repository = CreateFileRepository();

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.Missing));
            Assert.That(result.IsUsable, Is.True);
            Assert.That(result.Document.SchemaVersion, Is.EqualTo(1));
            Assert.That(result.Document.EarnedAchievementIds, Is.Empty);
            Assert.That(File.Exists(AchievementPath), Is.False);
        }

        [Test]
        public void MissingPrimaryWithValidBackup_RestoresBackupBeforeUsingEmptyPolicy()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(
                AchievementPath + ".bak",
                "{\"SchemaVersion\":1,\"EarnedAchievementIds\":[\"campaign.level-4.clear\"],\"PendingAchievementPublicationIds\":[]}");
            var repository = CreateFileRepository();

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.BackupRecovered));
            Assert.That(result.Document.EarnedAchievementIds, Is.EqualTo(new[] { "campaign.level-4.clear" }));
            Assert.That(File.Exists(AchievementPath), Is.True);
        }

        [Test]
        public void MissingPrimaryWithCorruptBackup_FailsClosed()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(AchievementPath + ".bak", "{broken");

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.CorruptNoFallback));
            Assert.That(result.IsUsable, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(File.Exists(AchievementPath), Is.False);
        }

        [Test]
        public void SaveAndLoad_RoundTripsEarnedAndPending()
        {
            var repository = CreateFileRepository();
            var document = Document(
                new[] { "campaign.level-4.clear" },
                new[] { "campaign.level-4.clear" });

            var save = repository.Save(document);
            var load = repository.Load();

            Assert.That(save.IsSuccess, Is.True);
            Assert.That(load.Status, Is.EqualTo(AchievementDocumentLoadStatus.Loaded));
            Assert.That(load.Document.EarnedAchievementIds, Is.EqualTo(new[] { "campaign.level-4.clear" }));
            Assert.That(
                load.Document.PendingAchievementPublicationIds,
                Is.EqualTo(new[] { "campaign.level-4.clear" }));
        }

        [TestCase("{\"SchemaVersion\":1}")]
        [TestCase("{\"SchemaVersion\":1,\"EarnedAchievementIds\":null,\"PendingAchievementPublicationIds\":null}")]
        public void MissingOrNullArrays_LoadAsEmpty(string json)
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(AchievementPath, json);

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.Loaded));
            Assert.That(result.Document.EarnedAchievementIds, Is.Empty);
            Assert.That(result.Document.PendingAchievementPublicationIds, Is.Empty);
        }

        [Test]
        public void SameLogicalDocument_SerializesDeterministicallyAndWritesExactlyOncePerSave()
        {
            var store = new RecordingTextStore();
            var repository = new FileProductAchievementRepository(store);
            var first = Document(
                new[] { "future.z", "campaign.level-4.clear", "future.z" },
                new[] { "future.z" });
            var second = Document(
                new[] { "campaign.level-4.clear", "future.z" },
                new[] { "future.z", "future.z" });

            Assert.That(repository.Save(first).IsSuccess, Is.True);
            var firstJson = store.Writes[0];
            Assert.That(repository.Save(second).IsSuccess, Is.True);

            Assert.That(store.WriteCount, Is.EqualTo(2));
            Assert.That(store.Writes[1], Is.EqualTo(firstJson));
            Assert.That(firstJson, Does.Contain("\"EarnedAchievementIds\":[\"campaign.level-4.clear\",\"future.z\"]"));
        }

        [Test]
        public void Load_CleansOwnedWriteTempsAndPreservesUnrelatedFiles()
        {
            Directory.CreateDirectory(_tempDirectory);
            var staleTempPath = Path.Combine(_tempDirectory, "achievements.json.write.stale.tmp");
            var unrelatedTempPath = Path.Combine(_tempDirectory, "achievements.stale.tmp");
            var profileTempPath = Path.Combine(_tempDirectory, "profile.json.write.stale.tmp");
            File.WriteAllText(staleTempPath, "stale");
            File.WriteAllText(unrelatedTempPath, "unrelated");
            File.WriteAllText(profileTempPath, "profile temp");

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.Missing));
            Assert.That(File.Exists(staleTempPath), Is.False);
            Assert.That(File.ReadAllText(unrelatedTempPath), Is.EqualTo("unrelated"));
            Assert.That(File.ReadAllText(profileTempPath), Is.EqualTo("profile temp"));
        }

        [Test]
        public void CorruptPrimaryWithValidBackup_RecoversBackup()
        {
            var repository = CreateFileRepository();
            Assert.That(
                repository.Save(Document(new[] { "campaign.level-4.clear" }, Array.Empty<string>())).IsSuccess,
                Is.True);
            Assert.That(
                repository.Save(Document(new[] { "campaign.level-4.clear", "future.valid" }, Array.Empty<string>())).IsSuccess,
                Is.True);
            File.WriteAllText(AchievementPath, "{broken");

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.BackupRecovered));
            Assert.That(result.IsUsable, Is.True);
            Assert.That(result.Document.EarnedAchievementIds, Is.EqualTo(new[] { "campaign.level-4.clear" }));
            Assert.That(File.ReadAllText(AchievementPath), Does.Contain("campaign.level-4.clear"));
        }

        [Test]
        public void CorruptPrimaryWithoutBackup_IsQuarantinedAndFailsClosed()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(AchievementPath, "{broken");

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.CorruptQuarantined));
            Assert.That(result.IsUsable, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(File.Exists(AchievementPath), Is.False);
            Assert.That(Directory.GetFiles(_tempDirectory, "achievements.json.corrupt.*").Length, Is.EqualTo(1));
        }

        [Test]
        public void QuarantinedCorruption_RemainsFailClosedAcrossRepositoryInstances()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(AchievementPath, "{broken");

            var firstLoad = CreateFileRepository().Load();
            var secondLoad = CreateFileRepository().Load();

            Assert.That(firstLoad.Status, Is.EqualTo(AchievementDocumentLoadStatus.CorruptQuarantined));
            Assert.That(secondLoad.Status, Is.EqualTo(AchievementDocumentLoadStatus.CorruptNoFallback));
            Assert.That(secondLoad.IsUsable, Is.False);
            Assert.That(secondLoad.Document, Is.Null);
            Assert.That(File.Exists(AchievementPath), Is.False);
            Assert.That(Directory.GetFiles(_tempDirectory, "achievements.json.corrupt.*").Length, Is.EqualTo(1));
        }

        [Test]
        public void CorruptPrimaryWhenQuarantineFails_ReturnsFailureWithoutEmptyFallback()
        {
            var repository = new FileProductAchievementRepository(new CorruptNoQuarantineTextStore());

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.CorruptNoFallback));
            Assert.That(result.IsUsable, Is.False);
            Assert.That(result.Document, Is.Null);
        }

        [Test]
        public void SchemaInvalidAndUnsupportedPrimary_DoNotFallBackOrOverwrite()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(
                AchievementPath + ".bak",
                "{\"SchemaVersion\":1,\"EarnedAchievementIds\":[\"campaign.level-4.clear\"],\"PendingAchievementPublicationIds\":[]}");
            File.WriteAllText(
                AchievementPath,
                "{\"SchemaVersion\":1,\"EarnedAchievementIds\":[],\"PendingAchievementPublicationIds\":[\"campaign.level-4.clear\"]}");
            var schemaInvalid = CreateFileRepository().Load();
            Assert.That(schemaInvalid.Status, Is.EqualTo(AchievementDocumentLoadStatus.SchemaInvalid));
            Assert.That(File.Exists(AchievementPath), Is.True);

            File.WriteAllText(
                AchievementPath,
                "{\"SchemaVersion\":2,\"EarnedAchievementIds\":[],\"PendingAchievementPublicationIds\":[]}");
            var unsupported = CreateFileRepository().Load();
            Assert.That(unsupported.Status, Is.EqualTo(AchievementDocumentLoadStatus.UnsupportedVersion));
            Assert.That(File.ReadAllText(AchievementPath), Does.Contain("\"SchemaVersion\":2"));
        }

        [Test]
        public void InvalidPersistedToken_IsSchemaInvalid()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(
                AchievementPath,
                "{\"SchemaVersion\":1,\"EarnedAchievementIds\":[\" campaign.level-4.clear\"],\"PendingAchievementPublicationIds\":[]}");

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.SchemaInvalid));
            Assert.That(result.Document, Is.Null);
        }

        [Test]
        public void DefaultApplicationHost_UsesUnavailablePublisherAndKeepsPendingDurably()
        {
            using var host = ProductAchievementApplicationHost.CreateForSaveRoot(_tempDirectory);
            Assert.That(host.Initialize(), Is.True);

            var earnResult = host.Coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);
            var snapshot = host.Coordinator.GetSnapshot();

            Assert.That(earnResult, Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(snapshot.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.InFlightCount, Is.Zero);
            Assert.That(File.Exists(AchievementPath), Is.True);
        }

        [Test]
        public void MissingSchemaVersion_FailsClosed()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(
                AchievementPath,
                "{\"EarnedAchievementIds\":[],\"PendingAchievementPublicationIds\":[]}");

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.SchemaInvalid));
            Assert.That(result.IsUsable, Is.False);
            Assert.That(File.Exists(AchievementPath), Is.True);
        }

        [Test]
        public void NestedSchemaVersion_DoesNotSatisfyRequiredTopLevelField()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(
                AchievementPath,
                "{\"metadata\":{\"SchemaVersion\":1},\"EarnedAchievementIds\":[],\"PendingAchievementPublicationIds\":[]}");

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.SchemaInvalid));
            Assert.That(result.IsUsable, Is.False);
            Assert.That(result.Document, Is.Null);
        }

        [Test]
        public void ExcessiveJsonNesting_IsContainedAsCorruptInsteadOfRecursingWithoutBound()
        {
            Directory.CreateDirectory(_tempDirectory);
            const int excessiveDepth = 80;
            var json =
                "{\"SchemaVersion\":1,\"metadata\":" +
                new string('[', excessiveDepth) +
                "null" +
                new string(']', excessiveDepth) +
                ",\"EarnedAchievementIds\":[],\"PendingAchievementPublicationIds\":[]}";
            File.WriteAllText(AchievementPath, json);

            var result = CreateFileRepository().Load();

            Assert.That(result.Status, Is.EqualTo(AchievementDocumentLoadStatus.CorruptQuarantined));
            Assert.That(result.IsUsable, Is.False);
            Assert.That(result.Document, Is.Null);
        }

        [Test]
        public void LoadAndSave_ContainUnauthorizedAndIoFailures()
        {
            var unauthorized = new FileProductAchievementRepository(
                new ThrowingTextStore(new UnauthorizedAccessException("denied")));
            var io = new FileProductAchievementRepository(
                new ThrowingTextStore(new IOException("io")));

            Assert.That(unauthorized.Load().Status, Is.EqualTo(AchievementDocumentLoadStatus.Unauthorized));
            Assert.That(io.Load().Status, Is.EqualTo(AchievementDocumentLoadStatus.IoFailed));
            Assert.That(
                unauthorized.Save(ProductAchievementDocument.CreateEmpty()).Status,
                Is.EqualTo(AchievementDocumentSaveStatus.Unauthorized));
            Assert.That(
                io.Save(ProductAchievementDocument.CreateEmpty()).Status,
                Is.EqualTo(AchievementDocumentSaveStatus.IoFailed));
        }

        private string AchievementPath =>
            Path.Combine(_tempDirectory, FileProductAchievementRepository.AchievementFileName);

        private FileProductAchievementRepository CreateFileRepository()
        {
            var stageStore = new AtomicTextFileStore(_tempDirectory);
            return new FileProductAchievementRepository(
                new StageAtomicAchievementTextStoreAdapter(stageStore, _tempDirectory));
        }

        private static ProductAchievementDocument Document(string[] earned, string[] pending)
        {
            return new ProductAchievementDocument
            {
                SchemaVersion = 1,
                EarnedAchievementIds = earned,
                PendingAchievementPublicationIds = pending,
            };
        }

        private sealed class RecordingTextStore : IAchievementTextStore
        {
            public readonly List<string> Writes = new();

            public int WriteCount => Writes.Count;

            public bool Exists(string fileName)
            {
                return false;
            }

            public string ReadAllText(string fileName)
            {
                throw new InvalidOperationException();
            }

            public void WriteAllTextAtomic(string fileName, string contents)
            {
                Assert.That(fileName, Is.EqualTo(FileProductAchievementRepository.AchievementFileName));
                Writes.Add(contents);
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

            public bool HasQuarantinedCopy(string fileName)
            {
                return false;
            }

            public void CleanupTempFiles(string fileName)
            {
            }
        }

        private sealed class ThrowingTextStore : IAchievementTextStore
        {
            private readonly Exception _exception;

            public ThrowingTextStore(Exception exception)
            {
                _exception = exception;
            }

            public bool Exists(string fileName)
            {
                throw _exception;
            }

            public string ReadAllText(string fileName)
            {
                throw _exception;
            }

            public void WriteAllTextAtomic(string fileName, string contents)
            {
                throw _exception;
            }

            public bool TryRestoreBackup(string fileName)
            {
                throw _exception;
            }

            public bool TryQuarantine(string fileName, out string quarantinePath)
            {
                quarantinePath = string.Empty;
                throw _exception;
            }

            public bool HasQuarantinedCopy(string fileName)
            {
                throw _exception;
            }

            public void CleanupTempFiles(string fileName)
            {
                throw _exception;
            }
        }

        private sealed class CorruptNoQuarantineTextStore : IAchievementTextStore
        {
            public bool Exists(string fileName)
            {
                return string.Equals(
                    fileName,
                    FileProductAchievementRepository.AchievementFileName,
                    StringComparison.Ordinal);
            }

            public string ReadAllText(string fileName)
            {
                return "{broken";
            }

            public void WriteAllTextAtomic(string fileName, string contents)
            {
                throw new InvalidOperationException();
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

            public bool HasQuarantinedCopy(string fileName)
            {
                return false;
            }

            public void CleanupTempFiles(string fileName)
            {
            }
        }
    }
}
