using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignHudReadStoreTests
    {
        [TestCase("survival")]
        [TestCase("death")]
        [TestCase("clear")]
        public void BackupRecovered_RejectsOldGameplayOutcomeEvenWhenSlotValuesMatch(string command)
        {
            var files = new Files();
            var services = Create(files);
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            services.SlotStore.InitializeNewGame(1, resolver, string.Empty, GameMode.Casual);
            var slot = services.SlotStore.LoadSlot(1).State;
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var before = files.Data[FileCampaignProfileRepository.ProfileFileName];
            files.Data[FileCampaignProfileRepository.ProfileFileName + ".bak"] = before;
            files.Data[FileCampaignProfileRepository.ProfileFileName] = "broken";
            var writes = 0;
            files.BeforeWrite = () => writes++;
            var result = command == "survival"
                ? services.Service.CommitSurvival(1, new CampaignSurvivalCommitRequest(slot.CurrentStageId, 3, 2))
                : command == "death"
                    ? services.Service.CommitDeath(1, planner.PlanDeath(slot))
                    : services.Service.CommitStageClear(1, new CampaignStageClearCommitRequest
                        { Plan = planner.PlanStageClear(slot.CurrentStageId) });
            Assert.That(result.Status, Is.EqualTo(CampaignSaveCommandStatus.StalePrecondition));
            Assert.That(result.ProfileLoadStatus, Is.EqualTo(CampaignProfileLoadStatus.BackupRecovered));
            Assert.That(writes, Is.Zero);
            Assert.That(files.Data[FileCampaignProfileRepository.ProfileFileName], Is.EqualTo(before));
            Assert.That(services.SlotStore.LoadSlot(1).ResumeHp, Is.EqualTo(3), "Menu reads may use the restored file.");
        }

        [Test]
        public void RecoveryNotification_IsOutsideGate_BlocksGameplayUntilAllHandlersFinish_AndCacheDoesNotRepeatIt()
        {
            var files = new Files();
            var services = Create(files);
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            services.SlotStore.InitializeNewGame(1, resolver, string.Empty, GameMode.Casual);
            files.Data[FileCampaignProfileRepository.ProfileFileName + ".bak"] = files.Data[FileCampaignProfileRepository.ProfileFileName];
            files.Data[FileCampaignProfileRepository.ProfileFileName] = "broken";
            var reads = services.Service.HudReadStore;
            var firstCalls = 0;
            var secondCalls = 0;
            var underLock = true;
            var querySucceeded = false;
            CampaignSaveCommandStatus? competingStatus = null;
            reads.BackupRecovered += () =>
            {
                firstCalls++;
                underLock = System.Threading.Monitor.IsEntered(reads.SyncRoot);
                querySucceeded = !services.SlotStore.LoadAllWithReport().Report.BlocksCampaignAccess; // Loaded now; no recursive notification.
                var task = System.Threading.Tasks.Task.Run(() => services.Service.CommitSurvival(1,
                    new CampaignSurvivalCommitRequest(resolver.FirstStageId, 3, 2)));
                if (task.Wait(2000)) competingStatus = task.Result.Status;
                throw new InvalidOperationException("observer failure must not skip another run");
            };
            reads.BackupRecovered += () => secondCalls++;
            using var reader = Open(services);
            Assert.That(reader.Read().ResumeHp, Is.EqualTo(3));
            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(1));
            Assert.That(underLock, Is.False);
            Assert.That(querySucceeded, Is.True);
            Assert.That(competingStatus, Is.EqualTo(CampaignSaveCommandStatus.StalePrecondition));
            Assert.That(services.Service.CommitSurvival(1,
                new CampaignSurvivalCommitRequest(resolver.FirstStageId, 3, 2)).Succeeded, Is.True,
                "A later fresh run is not blocked by a cached recovery report.");
            Assert.That(reader.Read().ResumeHp, Is.EqualTo(2));
            Assert.That(firstCalls, Is.EqualTo(1));
        }

        [TestCase("survival", false)]
        [TestCase("survival", true)]
        [TestCase("death", false)]
        [TestCase("death", true)]
        [TestCase("clear", false)]
        [TestCase("clear", true)]
        [TestCase("final", false)]
        [TestCase("final", true)]
        public void FailedResponse_FileReadDeterminesResumeWithoutReplayingCommand(string command, bool afterWrite)
        {
            var files = new Files();
            var services = Create(files);
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            services.Service.InitializeNewGame(new CampaignNewGameRequest
            {
                SlotNumber = 1, GameMode = GameMode.Casual,
                InitialStageId = command == "final" ? resolver.FinalStageId.Value : resolver.FirstStageId.Value,
                InitialLevelGroupId = command == "final" ? "level-4" : "level-0",
            });
            var before = services.SlotStore.LoadSlot(1).State;
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var writes = 0;
            files.BeforeWrite = () => { writes++; if (!afterWrite) throw new IOException("before write"); };
            files.AfterWrite = () => { if (afterWrite) throw new IOException("response lost"); };
            var result = command == "survival"
                ? services.Service.CommitSurvival(1, new CampaignSurvivalCommitRequest(before.CurrentStageId, 3, 2))
                : command == "death"
                    ? services.Service.CommitDeath(1, planner.PlanDeath(before))
                    : services.Service.CommitStageClear(1, new CampaignStageClearCommitRequest
                        { Plan = planner.PlanStageClear(before.CurrentStageId) });
            Assert.That(result.Status, Is.EqualTo(CampaignSaveCommandStatus.SaveFailed));
            files.BeforeWrite = null;
            files.AfterWrite = null;
            var resumed = Create(files).SlotStore.LoadSlot(1).State;
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(resumed.ResumeHp, Is.EqualTo(command == "survival" && afterWrite ? 2 : 3));
            Assert.That(resumed.TotalDeaths, Is.EqualTo(command == "death" && afterWrite ? 1 : 0));
            Assert.That(resumed.CampaignCompleted, Is.EqualTo(command == "final" && afterWrite));
            if (command == "clear") Assert.That(resumed.CurrentStageId,
                Is.EqualTo(afterWrite ? resolver.GetNextOrNone(before.CurrentStageId) : before.CurrentStageId));
        }

        [Test]
        public void BindThenOneHundredReads_DoNotAccessFiles_ReloadDoes()
        {
            var files = new Files();
            var services = Create(files);
            Assert.That(files.Accesses, Is.Zero, "Composition alone must not validate HUD storage.");
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            var first = reader.Read();
            files.Accesses = 0;
            for (var i = 0; i < 100; i++) Assert.That(reader.Read(), Is.SameAs(first));
            Assert.That(files.Accesses, Is.Zero);
            reader.Reload();
            Assert.That(files.Accesses, Is.GreaterThan(0));
        }

        [Test]
        public void SameBackingFacadesShareCommits_SeparateFakeBackingsDoNot()
        {
            var files = new Files();
            var first = Create(files);
            var second = Create(files);
            var isolated = Create(new Files());
            first.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(first);
            using var other = Open(isolated);
            Assert.That(reader.Read().IsEmpty, Is.False);
            Assert.That(other.Read().IsEmpty, Is.True);
            second.SlotStore.DeleteSlot(1);
            files.Accesses = 0;
            Assert.That(reader.Read().IsEmpty, Is.True);
            Assert.That(files.Accesses, Is.Zero);
        }

        [Test]
        public void ExternalDeletion_IsObservedAtReloadAndNewBind_NotArbitraryHudRead()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            Assert.That(reader.Read().IsEmpty, Is.False);
            files.Data.Clear();
            Assert.That(reader.Read().IsEmpty, Is.False, "Explicit external edit policy.");
            using var nextScene = Open(services);
            Assert.That(nextScene.Read().IsEmpty, Is.True);
            Assert.That(reader.Read().IsEmpty, Is.True);
        }

        [Test]
        public void FailedPersistNeverPublishesMutation_AndFailedReadDoesNotRetryEveryRefresh()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            Assert.That(reader.Read().IsEmpty, Is.False);
            files.FailWrites = true;
            Assert.Throws<InvalidOperationException>(() => services.SlotStore.DeleteSlot(1));
            files.FailReads = true;
            Assert.Throws<InvalidOperationException>(() => reader.Read());
            files.Accesses = 0;
            for (var i = 0; i < 3; i++) Assert.Throws<InvalidOperationException>(() => reader.Read());
            Assert.That(files.Accesses, Is.Zero);
            files.FailReads = false;
            files.FailWrites = false;
            Assert.That(reader.Reload().IsEmpty, Is.False);
        }

        [Test]
        public void NormalRawProfileObservationDoesNotClearPendingResetGate()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            files.Data[CampaignSaveRecoveryService.PendingResetFileName] = "{}";
            using var reader = Open(services);
            Assert.Throws<InvalidOperationException>(() => reader.Read());
            Assert.That(services.Repository.Load().HasDocument, Is.True);
            Assert.Throws<InvalidOperationException>(() => reader.Read());
            files.Data.Remove(CampaignSaveRecoveryService.PendingResetFileName);
            Assert.Throws<InvalidOperationException>(() => reader.Read());
            Assert.That(reader.Reload().IsEmpty, Is.False);
        }

        [Test]
        public void ResetKeepsLiveStoreAndNewFacadeAttached_DisposeRevokesReader()
        {
            var files = new Files();
            var first = Create(files);
            var reader = Open(first);
            reader.Read();
            var generation = reader.Generation;
            CampaignHudReadRegistry.Reset(CampaignHudReadRegistry.MemoryKey(files));
            var second = Create(files);
            Assert.That(first.Repository.HudReadStore, Is.SameAs(second.Repository.HudReadStore));
            Assert.That(reader.Generation, Is.GreaterThan(generation));
            reader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => reader.Read());
        }

        [Test]
        public void ParticipantReset_InvalidatesLiveReaderAndReloadsDeletedProfile()
        {
            var root = Path.Combine(Path.GetTempPath(), "campaign-hud-participant-reset-" + Guid.NewGuid().ToString("N"));
            var paths = new RootedPaths(root);
            try
            {
                var facade = CampaignSaveFacadeFactory.Create(new CampaignSaveCompositionOptions
                {
                    PathProvider = paths,
                });
                facade.ProfileServices.Service.InitializeNewGame(1, "stage-1-1", "level-1");
                using var reader = ((ICampaignHudReadProvider)facade.CampaignSaveSlots).OpenHudReadSession(1);
                Assert.That(reader.Read().IsEmpty, Is.False);
                var generation = reader.Generation;

                ParticipantCampaignReset.Clear(paths);

                Assert.That(reader.Generation, Is.GreaterThan(generation));
                Assert.That(reader.Read().IsEmpty, Is.True);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void TransientNamespaceReadersObserveOtherWriters()
        {
            var key = Guid.NewGuid().ToString("N");
            var first = new TransientCampaignSaveSlotStore(key);
            var second = new TransientCampaignSaveSlotStore(key);
            using var reader = ((ICampaignHudReadProvider)first).OpenHudReadSession(1);
            Assert.That(reader.Read().IsEmpty, Is.True);
            var generation = reader.Generation;
            second.ClearAll();
            Assert.That(reader.Generation, Is.GreaterThan(generation));
            Assert.That(reader.Read().IsEmpty, Is.True);
        }

        [Test]
        public void PendingMarkerDeletionFailureRemainsBlockedAfterValidProfileRead()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            files.Data[CampaignSaveRecoveryService.PendingResetFileName] = "{}";
            files.FailWrites = true;
            Assert.That(services.Recovery.RetryPendingReset(), Is.EqualTo(CampaignSaveResetResult.Failed));
            using var reader = Open(services);
            Assert.Throws<InvalidOperationException>(() => reader.Read());
            services.Repository.Load();
            Assert.Throws<InvalidOperationException>(() => reader.Read());
        }

        [Test]
        public void CanonicalFileKeysNormalizeRelativeSegmentsAndSeparators()
        {
            var root = Path.Combine(Path.GetTempPath(), "campaign-hud-root");
            Assert.That(CampaignHudReadRegistry.FileKey(Path.Combine(root, "child", "..")),
                Is.EqualTo(CampaignHudReadRegistry.FileKey(root)));
            Assert.That(CampaignHudReadRegistry.FileKey(root.Replace('\\', '/')),
                Is.EqualTo(CampaignHudReadRegistry.FileKey(root)));
        }

        [Test]
        public void OrdinaryValidatedReadObservesExternalChangesForExistingReader()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            reader.Read();
            files.Data.Clear();
            services.SlotStore.LoadAll();
            files.Accesses = 0;
            Assert.That(reader.Read().IsEmpty, Is.True);
            Assert.That(files.Accesses, Is.Zero);
        }

        [Test]
        public void FacadeStartupAndReaderCreationAddNoHudValidationToPendingCheck()
        {
            var files = new Files();
            var facade = CampaignSaveFacadeFactory.Create(new CampaignSaveCompositionOptions
            { PathProvider = new Paths(), TextFileStore = files });
            Assert.That(files.Accesses, Is.EqualTo(1), "Only the existing RetryPendingReset check runs.");
            using var reader = ((ICampaignHudReadProvider)facade.CampaignSaveSlots).OpenHudReadSession(1);
            Assert.That(files.Accesses, Is.EqualTo(1));
        }

        [Test]
        public void FailedWriteWithReadableBackingRevalidatesDurableValue()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            reader.Read();
            files.FailWrites = true;
            Assert.Throws<InvalidOperationException>(() => services.SlotStore.DeleteSlot(1));
            files.Accesses = 0;
            Assert.That(reader.Read().IsEmpty, Is.False, "Failed mutation documents must not replace persisted state.");
            Assert.That(files.Accesses, Is.GreaterThan(0));
        }

        [Test]
        public void NoWriteContinueObservesExternalProfile_WithoutHudReload()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            reader.Read();
            files.Data[FileCampaignProfileRepository.ProfileFileName] =
                files.Data[FileCampaignProfileRepository.ProfileFileName].Replace("\"RemainingChances\":3", "\"RemainingChances\":2");
            files.FailWrites = true;
            var prepared = services.SlotStore.PrepareContinue(new CampaignContinuePreparationCommand(
                1, StageId.CreateOrThrow("stage-1-1"), "level-1", "level-1"));
            Assert.That(prepared.Succeeded, Is.True);
            files.Accesses = 0;
            Assert.That(reader.Read().RemainingChances, Is.EqualTo(2));
            Assert.That(files.Accesses, Is.Zero);
        }

        [Test]
        public void BackupRecoveryIsObserved_AndSubsequentHudReadsAreMemoryOnly()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            reader.Read();
            files.Data[FileCampaignProfileRepository.ProfileFileName + ".bak"] = files.Data[FileCampaignProfileRepository.ProfileFileName];
            files.Data[FileCampaignProfileRepository.ProfileFileName] = "{broken";
            Assert.That(reader.Reload().RemainingChances, Is.EqualTo(3));
            files.Accesses = 0;
            reader.Read();
            Assert.That(files.Accesses, Is.Zero);
        }

        [Test]
        public void CommitSucceededButAdapterRereadFailed_DoesNotLeaveReadyHudState()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            reader.Read();
            files.AfterWrite = () => files.FailReads = true;
            Assert.Throws<InvalidOperationException>(() => services.SlotStore.ImportSlotSeed(
                new CampaignSlotSeedImportRequest(1, StageId.CreateOrThrow("stage-1-1"), "level-1", 2, string.Empty)));
            Assert.Throws<InvalidOperationException>(() => reader.Read());
            files.FailReads = false;
            files.AfterWrite = null;
            Assert.That(reader.Reload().RemainingChances, Is.EqualTo(2));
        }

        [Test]
        public void ClearCaptureProvidesImmutablePreMutationStateBeforePersistence()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            services.SlotStore.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1, StageId.CreateOrThrow("stage-1-1"), "level-1", 2, string.Empty));
            using var reader = Open(services);
            using var capture = reader.CaptureBeforeMutation();
            var observed = false;
            files.BeforeWrite = () =>
            {
                observed = true;
                Assert.That(capture.State.RemainingChances, Is.EqualTo(2));
                Assert.Throws<InvalidOperationException>(() => reader.Read());
            };
            services.SlotStore.CommitStageClear(1, new CampaignStageClearCommitRequest
            {
                Plan = new CampaignStageClearTransitionPlan(StageId.CreateOrThrow("stage-1-1"),
                    StageId.CreateOrThrow("stage-2-1"), "level-1", "level-2", false, true),
            });
            Assert.That(observed, Is.True);
            Assert.That(capture.State.RemainingChances, Is.EqualTo(2));
            Assert.That(reader.Read().RemainingChances, Is.EqualTo(3));
        }

        [Test]
        public void RawProfileReadCannotReuseEarlierGateApprovalWhenExternalPendingMarkerAppears()
        {
            var files = new Files();
            var services = Create(files);
            services.Service.InitializeNewGame(1, "stage-1-1", "level-1");
            using var reader = Open(services);
            Assert.That(reader.Read().IsEmpty, Is.False);
            files.Data[CampaignSaveRecoveryService.PendingResetFileName] = "{}";
            Assert.That(services.Service.LoadProfile().Succeeded, Is.True);
            Assert.Throws<InvalidOperationException>(() => reader.Read());
        }

        [Test]
        public void WeakRegistryDoesNotKeepDisposedReaderBackingAlive()
        {
            var weak = CreateDisposedReaderBacking();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.That(weak.IsAlive, Is.False);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference CreateDisposedReaderBacking()
        {
            var services = Create(new Files());
            using var reader = Open(services);
            reader.Read();
            return new WeakReference(((ICampaignHudReadProvider)services.SlotStore).HudReadStore);
        }

        private static ICampaignHudReadSession Open(CampaignSaveServiceFactoryResult result) =>
            ((ICampaignHudReadProvider)result.SlotStore).OpenHudReadSession(1);
        private static CampaignSaveServiceFactoryResult Create(Files files) =>
            CampaignSaveServiceFactory.Create(new CampaignSaveServiceFactoryOptions
            {
                PathProvider = new Paths(), TextFileStore = files,
                UtcNow = () => new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            });
        private sealed class Paths : SavePathProviderBase { internal Paths() : base("hud-fake-shared-path") { } }
        private sealed class RootedPaths : SavePathProviderBase { internal RootedPaths(string root) : base(root) { } }

        private sealed class Files : IAtomicTextFileStore
        {
            internal readonly Dictionary<string, string> Data = new();
            internal int Accesses;
            internal bool FailReads, FailWrites;
            internal Action BeforeWrite, AfterWrite;
            public bool Exists(string name) { Accesses++; if (FailReads) throw new IOException("read"); return Data.ContainsKey(name); }
            public string ReadAllText(string name) { Accesses++; if (FailReads) throw new IOException("read"); return Data[name]; }
            public void WriteAllTextAtomic(string name, string text)
            { Accesses++; BeforeWrite?.Invoke(); if (FailWrites) throw new IOException("write"); Data[name] = text; AfterWrite?.Invoke(); }
            public void WriteAllTextAtomicWithoutBackup(string name, string text) => WriteAllTextAtomic(name, text);
            public bool Delete(string name) { Accesses++; if (FailWrites) return false; return Data.Remove(name); }
            public void EnsureDirectory() { Accesses++; }
            public bool TryRestoreBackup(string name)
            {
                Accesses++;
                if (FailWrites || !Data.TryGetValue(name + ".bak", out var backup)) return false;
                Data[name] = backup;
                return true;
            }
            public bool TryQuarantine(string name, out string path) { Accesses++; path = ""; return false; }
            public bool TryQuarantine(string name, string suffix, out string path) => TryQuarantine(name, out path);
            public void RecoverInterruptedWrite(string name) { Accesses++; }
            public void CleanupTempFiles(string name) { Accesses++; }
        }
    }
}
