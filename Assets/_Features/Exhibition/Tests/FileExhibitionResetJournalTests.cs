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
