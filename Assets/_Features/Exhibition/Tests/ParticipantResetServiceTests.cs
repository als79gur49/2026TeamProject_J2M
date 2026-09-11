using System;
using System.IO;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class ParticipantResetServiceTests
    {
        private sealed class Journal : IExhibitionResetJournal, IExhibitionResetJournalMaintenance
        {
            public ResetRecord Record;
            public bool FailBefore, FailAfter, FailRead;
            public ResetRecord Archived;
            public void ArchiveLegacyReady(ResetRecord expected) { Archived = expected.Copy(); Record = null; }
            public void ReplaceLegacyPending(ResetRecord expected, ResetRecord replacement)
            {
                if (FailBefore) throw new IOException("archive unavailable");
                Archived = expected.Copy(); Save(replacement);
            }
            public ResetRecord Load() => FailRead ? throw new IOException("read") : Record;
            public void Save(ResetRecord record)
            {
                if (FailBefore) throw new IOException("before commit");
                Record = record;
                if (FailAfter) throw new IOException("after commit");
            }
        }
        private sealed class Steam : IExhibitionSteamReset
        {
            public int IdentityCalls, ResetCalls;
            public TaskCompletionSource<bool> Gate;
            public ResetIdentity GetIdentity() { IdentityCalls++; return new ResetIdentity(123, 456); }
            public async Task ResetAsync(ResetIdentity expectedIdentity)
            {
                ResetCalls++;
                if (Gate != null) await Gate.Task;
            }
        }
        private sealed class Progress : IParticipantProgressReset
        {
            public int Calls;
            public void Reset() => Calls++;
        }
        private sealed class RestartFake : IParticipantRestart
        {
            public bool FailValidation, FailLaunch;
            public int Calls;
            public ResetIdentity Identity;
            public void ValidateAvailable() { if (FailValidation) throw new IOException("unavailable"); }
            public void Restart(ResetIdentity identity)
            {
                Calls++; Identity = identity;
                if (FailLaunch) throw new IOException("launch failed");
            }
        }
        private sealed class ReturnFake : ICompletedParticipantResetReturn
        {
            public int Calls;
            public void Validate(ExhibitionResetCoordinator value) { Calls++; }
        }
        private Journal journal;
        private Steam steam;
        private Progress progress;
        private RestartFake restart;
        private ExhibitionResetCoordinator coordinator;
        private int stopped, started, reconciled;
        [SetUp] public void SetUp()
        {
            journal = new Journal(); steam = new Steam(); progress = new Progress(); restart = new RestartFake();
            coordinator = new ExhibitionResetCoordinator(journal, steam, progress);
            stopped = started = reconciled = 0;
        }
        private ParticipantResetService Service(bool available = true) => new ParticipantResetService(
            coordinator, restart, () => available, () => stopped++, () => started++,
            () => reconciled++, journal.Record);

        private void LegacyPending()
        {
            journal.Record = new ResetRecord { OperationId = Guid.NewGuid().ToString("N"),
                State = ResetRecord.Pending, AppId = 123, SteamId = 456,
                MappingVersion = ExhibitionResetCoordinator.PreviousMappingVersion };
        }

        [Test]
        public async Task LegacyPendingWaitsForExplicitConfirmationAndCannotGenericRestart()
        {
            LegacyPending(); var original = journal.Record.OperationId;
            var service = Service();
            await service.PrepareMenuAsync(); service.CompleteMenuInitialization(); service.Restart(); service.RequestReset();
            Assert.That(service.RequiresLegacyRecovery, Is.True);
            Assert.That(service.BlocksMenu, Is.True);
            Assert.That(service.CanRestartAfterFailure, Is.False);
            Assert.That(steam.IdentityCalls + steam.ResetCalls + progress.Calls + restart.Calls + started, Is.Zero);
            Assert.That(journal.Record.OperationId, Is.EqualTo(original));
            service.RequestLegacyReset(); service.RequestLegacyReset();
            Assert.That(journal.Archived.OperationId, Is.EqualTo(original));
            Assert.That(journal.Record.OperationId, Is.Not.EqualTo(original));
            Assert.That(journal.Record.MappingVersion, Is.EqualTo(ExhibitionResetCoordinator.MappingVersion));
            Assert.That(restart.Calls, Is.EqualTo(1));
            Assert.That(steam.ResetCalls + progress.Calls + started, Is.Zero);
            Assert.That(service.RequiresLegacyRecovery, Is.False);
            Assert.That(service.BlocksMenu, Is.True);
        }

        [TestCase(true)] [TestCase(false)]
        public void LegacyRecoveryPreflightOrArchiveFailurePreservesOldRequest(bool preflight)
        {
            LegacyPending(); var original = journal.Record.OperationId;
            restart.FailValidation = preflight; journal.FailBefore = !preflight;
            var service = Service(); service.RequestLegacyReset();
            Assert.That(journal.Record.OperationId, Is.EqualTo(original));
            Assert.That(service.RequiresLegacyRecovery, Is.True);
            Assert.That(service.BlocksMenu, Is.True);
            Assert.That(service.IsBusy, Is.False);
            Assert.That(restart.Calls + steam.ResetCalls + progress.Calls, Is.Zero);
            Assert.That(service.Error, Is.Not.Empty);
        }

        [Test]
        public void LegacyRecoveryLaunchFailureRetriesOnlyCommittedCurrentRequest()
        {
            LegacyPending(); restart.FailLaunch = true;
            var service = Service(); service.RequestLegacyReset();
            var committed = journal.Record.OperationId;
            Assert.That(service.RequiresLegacyRecovery, Is.False);
            Assert.That(service.CanRestartAfterFailure, Is.True);
            restart.FailLaunch = false; service.Restart();
            Assert.That(journal.Record.OperationId, Is.EqualTo(committed));
            Assert.That(restart.Calls, Is.EqualTo(2));
            Assert.That(steam.ResetCalls + progress.Calls, Is.Zero);
        }

        [Test]
        public void ArchivedStartupHintSuppressesSeedImportWithoutBlockingNormalMenu()
        {
            var service = new ParticipantResetService(coordinator, restart, () => true,
                () => stopped++, () => started++, () => reconciled++, null, suppressSaveSeedImport: true);
            service.CompleteMenuInitialization();
            Assert.That(service.SuppressSaveSeedImport, Is.True);
            Assert.That(service.BlocksMenu, Is.False);
            Assert.That(service.CanRequest, Is.True);
            Assert.That(steam.IdentityCalls + steam.ResetCalls + progress.Calls, Is.Zero);
        }

        [Test]
        public async Task ReadyMenuAssemblyFailureKeepsRestartAvailableWithoutRepeatingDeletion()
        {
            coordinator.RequestReset();
            var service = Service();
            await service.PrepareMenuAsync();
            service.FailMenuInitialization("menu assembly failed");
            Assert.That(service.BlocksMenu, Is.True);
            Assert.That(service.CanRequest, Is.False);
            Assert.That(journal.Record.State, Is.EqualTo(ResetRecord.Ready));
            service.Restart();
            Assert.That(restart.Calls, Is.EqualTo(1));
            Assert.That(steam.ResetCalls, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public async Task NormalAndCurrentReadyDoNotCallSteamOrRestart(bool ready)
        {
            if (ready)
                journal.Record = new ResetRecord { OperationId = Guid.NewGuid().ToString("N"),
                    State = ResetRecord.Ready, AppId = 999, SteamId = 999, MappingVersion = ExhibitionResetCoordinator.MappingVersion };
            var service = Service(false);
            await service.PrepareMenuAsync();
            service.CompleteMenuInitialization();
            Assert.That(service.BlocksMenu, Is.False);
            Assert.That(service.CanRequest, Is.False);
            Assert.That(steam.IdentityCalls + steam.ResetCalls + restart.Calls + started + reconciled, Is.Zero);
        }

        [Test]
        public async Task PendingBlocksMenuUntilSteamLocalAndReadyComplete()
        {
            coordinator.RequestReset();
            steam.Gate = new TaskCompletionSource<bool>();
            var service = Service();
            var task = service.PrepareMenuAsync();
            service.CompleteMenuInitialization();
            Assert.That(service.BlocksMenu, Is.True);
            Assert.That(service.CanRequest, Is.False);
            Assert.That(started + reconciled + progress.Calls, Is.Zero);
            steam.Gate.SetResult(true);
            await task;
            Assert.That(journal.Record.State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(progress.Calls, Is.EqualTo(1));
            Assert.That(started, Is.EqualTo(1));
            Assert.That(reconciled, Is.Zero);
            service.CompleteMenuInitialization();
            Assert.That(reconciled, Is.EqualTo(1));
            Assert.That(service.CanRequest, Is.True);
        }

        [Test]
        public async Task PendingProductionCompletionStartsSteamCycleAndKeepsServicesBlocked()
        {
            coordinator.RequestReset();
            var completed = new RestartFake();
            var service = new ParticipantResetService(coordinator, restart, () => true, () => stopped++, () => started++,
                () => reconciled++, journal.Record, completedResetRestart: completed);
            await service.PrepareMenuAsync();
            Assert.That(journal.Record.State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(completed.Calls, Is.EqualTo(1));
            Assert.That(completed.Identity.AppId, Is.EqualTo(123));
            Assert.That(started + reconciled, Is.Zero);
            Assert.That(service.BlocksMenu, Is.True);
        }

        [Test]
        public async Task CompletedResetReturnValidatesBeforePublishingAndUnblocksOnce()
        {
            journal.Record = new ResetRecord { OperationId = Guid.NewGuid().ToString("N"), State = ResetRecord.Ready,
                AppId = 123, SteamId = 456, MappingVersion = ExhibitionResetCoordinator.MappingVersion };
            var returned = new ReturnFake(); int runtime = 0;
            var service = new ParticipantResetService(coordinator, restart, () => true, () => stopped++, () => started++,
                () => reconciled++, journal.Record, completedResetReturn: returned, startRuntime: () => runtime++);
            await service.PrepareMenuAsync();
            service.CompleteMenuInitialization();
            Assert.That(runtime, Is.EqualTo(1));
            Assert.That(returned.Calls, Is.EqualTo(1));
            Assert.That(started, Is.EqualTo(1));
            Assert.That(reconciled, Is.EqualTo(1));
            Assert.That(service.BlocksMenu, Is.False);
        }

        [Test]
        public void RestartPreflightFailureLeavesMenuAndStorageUntouched()
        {
            var service = Service(); service.CompleteMenuInitialization();
            restart.FailValidation = true;
            service.RequestReset();
            Assert.That(journal.Record, Is.Null);
            Assert.That(service.BlocksMenu, Is.False);
            Assert.That(stopped + steam.IdentityCalls + progress.Calls, Is.Zero);
            Assert.That(service.Error, Is.Not.Empty);
        }

        [Test]
        public void CompletedSteamCyclePreflightFailureLeavesResetUncommitted()
        {
            var completed = new RestartFake { FailValidation = true };
            var service = new ParticipantResetService(coordinator, restart, () => true, () => stopped++, () => started++,
                () => reconciled++, journal.Record, completedResetRestart: completed);
            service.CompleteMenuInitialization();
            service.RequestReset();
            Assert.That(journal.Record, Is.Null);
            Assert.That(restart.Calls + completed.Calls + stopped, Is.Zero);
            Assert.That(service.BlocksMenu, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void CanonicalPendingLocksMenuEvenWhenSaveOrLaunchThrows(bool afterCommit)
        {
            journal.FailAfter = afterCommit;
            restart.FailLaunch = !afterCommit;
            var service = Service(); service.CompleteMenuInitialization(); service.RequestReset();
            Assert.That(journal.Record.State, Is.EqualTo(ResetRecord.Pending));
            Assert.That(service.BlocksMenu, Is.True);
            Assert.That(service.CanRequest, Is.False);
            Assert.That(restart.Calls, Is.EqualTo(1));
            Assert.That(restart.Identity.SteamId, Is.EqualTo(456));
            Assert.That(steam.ResetCalls + progress.Calls, Is.Zero);
        }

        [Test]
        public void PreCommitFailureWithReadableMissingRecordKeepsMenu()
        {
            journal.FailBefore = true;
            var service = Service(); service.CompleteMenuInitialization(); service.RequestReset();
            Assert.That(service.BlocksMenu, Is.False);
            Assert.That(restart.Calls + stopped, Is.Zero);
        }

        [Test]
        public void UnreadableJournalNeverReturnsToPlay()
        {
            var service = Service(); service.CompleteMenuInitialization(); journal.FailRead = true;
            service.RequestReset();
            Assert.That(service.BlocksMenu, Is.True);
            Assert.That(restart.Calls + progress.Calls, Is.Zero);
        }

        [Test]
        public async Task ReadyCommittedDespiteCompanionCleanupExceptionStartsServices()
        {
            coordinator.RequestReset(); journal.FailAfter = true;
            var service = Service(); await service.PrepareMenuAsync();
            Assert.That(service.BlocksMenu, Is.False);
            Assert.That(journal.Record.State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(started, Is.EqualTo(1));
        }

        private sealed class Paths : ISavePathProvider
        {
            public string SaveRootPath { get; set; }
            public string GetSaveFilePath(string fileName) => Path.Combine(SaveRootPath, fileName);
        }
        [Test]
        public void SameRootHasOneSessionAndCanBeReopenedAfterDisposal()
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-session-" + Guid.NewGuid().ToString("N"));
            var paths = new Paths { SaveRootPath = root };
            try
            {
                using (ExhibitionApplication.AcquireSessionLock(paths))
                    Assert.Throws<IOException>(() => ExhibitionApplication.AcquireSessionLock(paths));
                using (var next = ExhibitionApplication.AcquireSessionLock(paths)) Assert.That(next.CanRead, Is.True);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void LeavingMenuRevokesResetEvenWhenSteamIsAvailable()
        {
            var service = Service(); service.CompleteMenuInitialization(); service.LeaveMenu();
            service.RequestReset();
            Assert.That(journal.Record, Is.Null);
            Assert.That(service.CanRequest, Is.False);
        }

        [Test]
        public void DestructiveProgressUsesInjectedRootWithoutProductionRecovery()
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-participant-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                foreach (var name in new[] { "profile.json", "local-launch-state.json", "profile.reset.pending.json", "achievements.json" })
                    foreach (var suffix in new[] { "", ".bak", ".rollback", ".bak.rollback", ".corrupt.old", ".rejected.old" })
                        File.WriteAllText(Path.Combine(root, name + suffix), "old participant");
                File.WriteAllText(Path.Combine(root, "settings.json"), "settings");
                File.WriteAllText(Path.Combine(root, "exhibition-reset.json"), "pending intent");
                CampaignSaveCompositionProvider.SuspendProductionAccess();
                new ParticipantProgressResetAdapter(new Paths { SaveRootPath = root }).Reset();
                Assert.That(Directory.GetFiles(root, "profile.*"), Is.Empty);
                Assert.That(Directory.GetFiles(root, "local-launch-state.*"), Is.Empty);
                Assert.That(File.ReadAllText(Path.Combine(root, "settings.json")), Is.EqualTo("settings"));
                Assert.That(File.ReadAllText(Path.Combine(root, "exhibition-reset.json")), Is.EqualTo("pending intent"));
                Assert.That(File.ReadAllText(Path.Combine(root, "achievements.json")), Does.Not.Contain("old participant"));
            }
            finally { CampaignSaveCompositionProvider.ReleaseProductionAccess(); Directory.Delete(root, true); }
        }
    }
}
