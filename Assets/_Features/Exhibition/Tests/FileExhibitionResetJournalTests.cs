using System;
using System.IO;
using Game.Exhibition.Integration;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    [Category("Full")]
    public sealed class FileExhibitionResetJournalTests
    {
        private string _root;
        private string PathName => Path.Combine(_root, "exhibition-reset.json");
        private FileExhibitionResetJournal Journal => new FileExhibitionResetJournal(PathName);
        [SetUp] public void SetUp() { _root = Path.Combine(Path.GetTempPath(), "j2m-exhibition-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); }
        [TearDown] public void TearDown() { Directory.Delete(_root, true); }
        private static ResetRecord Pending() => new ResetRecord { OperationId = Guid.NewGuid().ToString("N"), State = ResetRecord.Pending, AppId = 123, SteamId = 456, MappingVersion = ExhibitionResetCoordinator.MappingVersion };

        [TestCase("Pending", "unknown-mapping")]
        [TestCase("Ready", "unknown-mapping")]
        public void UnsupportedMappingIsNotIgnoredMigratedOrOverwritten(string state, string mapping)
        {
            var record = Pending(); record.State = state; record.MappingVersion = mapping;
            var original = UnityEngine.JsonUtility.ToJson(record);
            File.WriteAllText(PathName, original);
            File.WriteAllText(PathName + ".bak", original);
            Assert.Throws<IOException>(() => Journal.Load());
            Assert.Throws<IOException>(() => Journal.Save(Pending()));
            Assert.That(File.ReadAllText(PathName), Is.EqualTo(original));
            Assert.That(File.ReadAllText(PathName + ".bak"), Is.EqualTo(original));
            Assert.That(File.Exists(PathName + ".tmp"), Is.False);
        }

        private ResetRecord WriteLegacy(string state)
        {
            var record = Pending(); record.State = state;
            record.MappingVersion = ExhibitionResetCoordinator.PreviousMappingVersion;
            File.WriteAllText(PathName, UnityEngine.JsonUtility.ToJson(record)); return record;
        }
        private string ArchivePath(ResetRecord record) => Path.Combine(PathName + ".archive", record.OperationId + ".json");

        [TestCase("Pending")] [TestCase("Ready")]
        public void KnownLegacyLoadsButOrdinarySaveCannotOverwrite(string state)
        {
            var record = WriteLegacy(state); var original = File.ReadAllText(PathName);
            Assert.That(Journal.Load().OperationId, Is.EqualTo(record.OperationId));
            Assert.Throws<IOException>(() => Journal.Save(Pending()));
            Assert.That(File.ReadAllText(PathName), Is.EqualTo(original));
        }

        [Test]
        public void ReadyArchiveIsDurableAndStalePendingCompanionCannotResurrect()
        {
            var record = WriteLegacy(ResetRecord.Ready);
            var companion = record.Copy(); companion.State = ResetRecord.Pending;
            File.WriteAllText(PathName + ".bak", UnityEngine.JsonUtility.ToJson(companion));
            Journal.ArchiveLegacyReady(record);
            Assert.That(Journal.Load(), Is.Null); Assert.That(Journal.HasArchivedRecord, Is.True);
            Assert.That(File.Exists(PathName + ".bak"), Is.False);
            Assert.That(UnityEngine.JsonUtility.FromJson<ResetRecord>(File.ReadAllText(ArchivePath(record))).State, Is.EqualTo(ResetRecord.Ready));
        }

        [TestCase("Pending")] [TestCase("Ready")]
        public void InterruptedAfterArchiveCanResumeIdempotently(string state)
        {
            var record = WriteLegacy(state);
            Directory.CreateDirectory(PathName + ".archive");
            File.WriteAllText(ArchivePath(record), File.ReadAllText(PathName));
            if (state == ResetRecord.Ready) { Journal.ArchiveLegacyReady(record); Assert.That(Journal.Load(), Is.Null); }
            else { var replacement = Pending(); Journal.ReplaceLegacyPending(record, replacement); Assert.That(Journal.Load().OperationId, Is.EqualTo(replacement.OperationId)); }
            Assert.That(Journal.HasArchivedRecord, Is.True);
        }

        [TestCase("archive")] [TestCase("companion")]
        public void ConflictingArchiveOrUnexpectedCompanionPreventsAnyCanonicalMutation(string obstruction)
        {
            var record = WriteLegacy(ResetRecord.Pending); var original = File.ReadAllText(PathName);
            if (obstruction == "archive") { Directory.CreateDirectory(PathName + ".archive"); File.WriteAllText(ArchivePath(record), "{}"); }
            else File.WriteAllText(PathName + ".tmp", UnityEngine.JsonUtility.ToJson(Pending()));
            Assert.Throws<IOException>(() => Journal.ReplaceLegacyPending(record, Pending()));
            Assert.That(File.ReadAllText(PathName), Is.EqualTo(original));
        }

        [Test]
        public void ArchiveFailureLeavesPendingCanonicalUntouched()
        {
            var record = WriteLegacy(ResetRecord.Pending); var original = File.ReadAllText(PathName);
            File.WriteAllText(PathName + ".archive", "blocked");
            Assert.Throws<IOException>(() => Journal.ReplaceLegacyPending(record, Pending()));
            Assert.That(File.ReadAllText(PathName), Is.EqualTo(original));
        }

        [Test]
        public void PendingReplacementArchivesOldIdentityBeforeWritingNewRequest()
        {
            var record = WriteLegacy(ResetRecord.Pending); var replacement = Pending();
            Journal.ReplaceLegacyPending(record, replacement);
            Assert.That(Journal.Load().OperationId, Is.EqualTo(replacement.OperationId));
            var archived = UnityEngine.JsonUtility.FromJson<ResetRecord>(File.ReadAllText(ArchivePath(record)));
            Assert.That(archived.MappingVersion, Is.EqualTo(ExhibitionResetCoordinator.PreviousMappingVersion));
            Assert.That(archived.OperationId, Is.EqualTo(record.OperationId));
        }

        [TestCase("Pending")] [TestCase("Ready")]
        public void ArchivePreservesExactCanonicalBytesIncludingUnknownFields(string state)
        {
            var record = WriteLegacy(state);
            var text = "  \n" + File.ReadAllText(PathName).TrimEnd('}') + ",\"futureEvidence\":\"keep me\"}\n";
            var original = System.Text.Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(PathName, original);
            if (state == ResetRecord.Ready) Journal.ArchiveLegacyReady(record);
            else Journal.ReplaceLegacyPending(record, Pending());
            Assert.That(File.ReadAllBytes(ArchivePath(record)), Is.EqualTo(original));
        }

        [Test]
        public void SemanticallyEqualButDifferentArchiveBytesBlockReplacement()
        {
            var record = WriteLegacy(ResetRecord.Pending); var original = File.ReadAllText(PathName);
            Directory.CreateDirectory(PathName + ".archive");
            File.WriteAllText(ArchivePath(record), original + "\n");
            Assert.Throws<IOException>(() => Journal.ReplaceLegacyPending(record, Pending()));
            Assert.That(File.ReadAllText(PathName), Is.EqualTo(original));
        }

        [TestCase("Pending")] [TestCase("Ready")]
        public void LockedCanonicalAfterArchiveRemainsRecoverableOnRetry(string state)
        {
            var record = WriteLegacy(state); var original = File.ReadAllBytes(PathName);
            var replacement = Pending();
            using (var locked = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (state == ResetRecord.Pending)
                    Assert.Throws<IOException>(() => Journal.ReplaceLegacyPending(record, replacement));
                else Assert.Throws<IOException>(() => Journal.ArchiveLegacyReady(record));
                Assert.That(File.ReadAllBytes(ArchivePath(record)), Is.EqualTo(original));
                Assert.That(File.ReadAllBytes(PathName), Is.EqualTo(original));
            }
            if (state == ResetRecord.Pending)
            {
                Journal.ReplaceLegacyPending(record, replacement);
                Assert.That(Journal.Load().OperationId, Is.EqualTo(replacement.OperationId));
            }
            else { Journal.ArchiveLegacyReady(record); Assert.That(Journal.Load(), Is.Null); }
        }

        [Test] public void MissingIsNotPending() => Assert.That(Journal.Load(), Is.Null);
        [Test] public void ReadyReplacesPendingWithoutBackup()
        {
            var record = Pending(); Journal.Save(record); record.State = ResetRecord.Ready; Journal.Save(record);
            Assert.That(Journal.Load().State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(Directory.GetFiles(_root).Length, Is.EqualTo(1));
        }
        [TestCase(".bak")] [TestCase(".rollback")] [TestCase(".tmp")]
        public void MissingCanonicalWithCompanionDoesNotRestore(string suffix)
        {
            File.WriteAllText(PathName + suffix, "old Pending");
            Assert.Throws<IOException>(() => Journal.Load());
            Assert.That(File.Exists(PathName), Is.False);
            Assert.Throws<IOException>(() => Journal.Save(Pending()));
        }
        [Test] public void CorruptionNeverBecomesMissing()
        {
            File.WriteAllText(PathName, "{}");
            Assert.Throws<IOException>(() => Journal.Load());
        }
        [Test] public void CanonicalReadyWinsOverOldPendingCompanion()
        {
            var record = Pending(); Journal.Save(record); var oldPending = File.ReadAllText(PathName);
            record.State = ResetRecord.Ready; Journal.Save(record); File.WriteAllText(PathName + ".rollback", oldPending);
            Assert.That(Journal.Load().State, Is.EqualTo(ResetRecord.Ready));
            File.Delete(PathName);
            Assert.Throws<IOException>(() => Journal.Load());
        }
    }
}
