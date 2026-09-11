using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    [Category("Full")]
    public sealed class ExhibitionResetCoordinatorTests
    {
        private static readonly ResetIdentity Identity = new ResetIdentity(123, 456);
        private Journal _journal;
        private Steam _steam;
        private Progress _progress;
        private ExhibitionResetCoordinator Create() => new ExhibitionResetCoordinator(_journal, _steam, _progress);

        [SetUp]
        public void SetUp() { _journal = new Journal(); _steam = new Steam(); _progress = new Progress(); }

        [Test]
        public async Task MissingAndReadyDoNotResetExistingParticipant()
        {
            Assert.That(await Create().ResumeAsync(), Is.True);
            Create().RequestReset();
            _journal.Record.State = ResetRecord.Ready;
            Assert.That(await Create().ResumeAsync(), Is.True);
            Assert.That(_steam.Calls, Is.Zero);
            Assert.That(_progress.Calls, Is.Zero);
        }

        [Test]
        public void DuplicateRequestKeepsOriginalOperation()
        {
            var coordinator = Create(); coordinator.RequestReset();
            var operation = _journal.Record.OperationId;
            Assert.Throws<InvalidOperationException>(() => coordinator.RequestReset());
            Assert.That(_journal.Record.OperationId, Is.EqualTo(operation));
        }

        [Test]
        public async Task SuccessSavesReadyWithoutMutatingLoadedPending()
        {
            Create().RequestReset(); var pending = _journal.Record;
            await Create().ResumeAsync();
            Assert.That(pending.State, Is.EqualTo(ResetRecord.Pending));
            Assert.That(_journal.Record.State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(_steam.Calls, Is.EqualTo(1));
            Assert.That(_progress.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task NextParticipantRequestCreatesNewOperationOnlyAfterReady()
        {
            Create().RequestReset(); var firstOperation = _journal.Record.OperationId;
            await Create().ResumeAsync();
            Create().RequestReset();
            Assert.That(_journal.Record.State, Is.EqualTo(ResetRecord.Pending));
            Assert.That(_journal.Record.OperationId, Is.Not.EqualTo(firstOperation));
        }

        [Test]
        public void FailedAttemptCannotRetryInSameCoordinator()
        {
            Create().RequestReset(); _steam.Fail = true; var coordinator = Create();
            Assert.ThrowsAsync<IOException>(async () => await coordinator.ResumeAsync());
            _steam.Fail = false;
            Assert.ThrowsAsync<InvalidOperationException>(async () => await coordinator.ResumeAsync());
            Assert.That(_steam.Calls, Is.EqualTo(1));
        }

        [Test]
        public void SteamFailureKeepsPendingAndDoesNotResetProgress()
        {
            Create().RequestReset(); _steam.Fail = true;
            Assert.ThrowsAsync<IOException>(async () => await Create().ResumeAsync());
            Assert.That(_journal.Record.State, Is.EqualTo(ResetRecord.Pending));
            Assert.That(_progress.Calls, Is.Zero);
        }

        [Test]
        public async Task LocalFailureCanResumeInNewLifetime()
        {
            Create().RequestReset(); _progress.Fail = true;
            Assert.ThrowsAsync<IOException>(async () => await Create().ResumeAsync());
            Assert.That(_journal.Record.State, Is.EqualTo(ResetRecord.Pending));
            _progress.Fail = false; await Create().ResumeAsync();
            Assert.That(_steam.Calls, Is.EqualTo(2));
            Assert.That(_journal.Record.State, Is.EqualTo(ResetRecord.Ready));
        }

        [Test]
        public void FailedReadySaveDoesNotChangePendingObject()
        {
            Create().RequestReset(); var pending = _journal.Record; _journal.FailSave = true;
            Assert.ThrowsAsync<IOException>(async () => await Create().ResumeAsync());
            Assert.That(pending.State, Is.EqualTo(ResetRecord.Pending));
            Assert.That(_journal.Record, Is.SameAs(pending));
        }

        [Test]
        public void RequestWriteFailureDoesNotTouchSteamOrProgress()
        {
            _journal.FailSave = true;
            Assert.Throws<IOException>(() => Create().RequestReset());
            Assert.That(_steam.Calls, Is.Zero); Assert.That(_progress.Calls, Is.Zero);
        }

        [Test]
        public async Task RequestUsesCurrentAccountAndReadyDoesNotConsultSteam()
        {
            _steam.Identity = new ResetIdentity(123, 789);
            Create().RequestReset();
            Assert.That(_journal.Record.SteamId, Is.EqualTo(789));
            _journal.Record.State = ResetRecord.Ready;
            _steam.Identity = default;
            Assert.That(await Create().ResumeAsync(), Is.True);
            Assert.That(_steam.Calls, Is.Zero);
        }

        [Test]
        public void PendingAccountMismatchDoesNotDelete()
        {
            Create().RequestReset();
            _steam.Identity = new ResetIdentity(123, 789);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Create().ResumeAsync());
            Assert.That(_steam.Calls, Is.Zero);
            Assert.That(_progress.Calls, Is.Zero);
        }

        [Test]
        public async Task IdentityChangeDuringSteamResetBlocksLocalMutation()
        {
            Create().RequestReset(); _steam.Gate = new TaskCompletionSource<bool>();
            var running = Create().ResumeAsync();
            _steam.Identity = new ResetIdentity(123, 789); _steam.Gate.SetResult(true);
            try { await running; Assert.Fail("Expected identity mismatch"); }
            catch (InvalidOperationException) { }
            Assert.That(_progress.Calls, Is.Zero);
        }

        [Test]
        public async Task ConcurrentAndSameLifetimeRetriesDoNotStartAnotherStore()
        {
            Create().RequestReset(); _steam.Gate = new TaskCompletionSource<bool>();
            var coordinator = Create(); var running = coordinator.ResumeAsync();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await coordinator.ResumeAsync());
            Assert.Throws<InvalidOperationException>(() => coordinator.RequestReset());
            _steam.Gate.SetResult(true); await running;
            Assert.ThrowsAsync<InvalidOperationException>(async () => await coordinator.ResumeAsync());
            Assert.That(_steam.Calls, Is.EqualTo(1));
        }

        [TestCase("State")]
        [TestCase("Schema")]
        [TestCase("Mapping")]
        public void InvalidRecordDoesNotReset(string field)
        {
            Create().RequestReset();
            if (field == "State") _journal.Record.State = "Unknown";
            if (field == "Schema") _journal.Record.SchemaVersion = 2;
            if (field == "Mapping") _journal.Record.MappingVersion = "other";
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Create().ResumeAsync());
            Assert.That(_steam.Calls, Is.Zero);
        }

        [TestCase("Pending", "level-clear-v1")]
        [TestCase("Ready", "level-clear-v1")]
        [TestCase("Pending", "unknown-mapping")]
        [TestCase("Ready", "unknown-mapping")]
        public void UnsupportedMappingCannotBeReadResumedOrOverwritten(string state, string mapping)
        {
            Create().RequestReset();
            var original = _journal.Record;
            original.State = state;
            original.MappingVersion = mapping;
            Assert.Throws<InvalidOperationException>(() => Create().ReadRecord());
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Create().ResumeAsync());
            Assert.Throws<InvalidOperationException>(() => Create().RequestReset());
            Assert.That(_journal.Record, Is.SameAs(original));
            Assert.That(_journal.Record.MappingVersion, Is.EqualTo(mapping));
            Assert.That(_journal.Record.State, Is.EqualTo(state));
            Assert.That(_steam.Calls, Is.Zero);
            Assert.That(_progress.Calls, Is.Zero);
        }

        [Test]
        public void NewResetUsesEighteenAchievementMappingVersion()
        {
            Create().RequestReset();
            Assert.That(_journal.Record.MappingVersion, Is.EqualTo("level-and-efficient-clear-v2"));
            Assert.That(_journal.Record.SchemaVersion, Is.EqualTo(1));
        }

        private sealed class Journal : IExhibitionResetJournal
        {
            public ResetRecord Record; public bool FailSave;
            public ResetRecord Load() => Record;
            public void Save(ResetRecord record) { if (FailSave) throw new IOException(); Record = record; }
        }
        private sealed class Steam : IExhibitionSteamReset
        {
            public ResetIdentity Identity = ExhibitionResetCoordinatorTests.Identity;
            public int Calls; public bool Fail; public TaskCompletionSource<bool> Gate;
            public ResetIdentity GetIdentity() => Identity;
            public async Task ResetAsync(ResetIdentity expectedIdentity) { Calls++; if (Fail) throw new IOException(); if (Gate != null) await Gate.Task; }
        }
        private sealed class Progress : IParticipantProgressReset
        {
            public int Calls; public bool Fail;
            public void Reset() { Calls++; if (Fail) throw new IOException(); }
        }
    }
}
