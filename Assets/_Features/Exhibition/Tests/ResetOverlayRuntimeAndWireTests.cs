using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using Game.Feature.Stages;
using Game.Product.Achievements.Infrastructure;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    // Production runtime, journal, evidence serializer, wire and payload checks, with fake native/process effects.
    public sealed class ResetOverlayRuntimeAndWireTests
    {
        private sealed class Paths : ISavePathProvider { public string SaveRootPath { get; set; } public string GetSaveFilePath(string name) => Path.Combine(SaveRootPath, name); }
        private sealed class Steam : IExhibitionSteamReset
        {
            public ulong Account = 76561198000000001;
            public bool[] Values = { true, false, false, false, false };
            public bool QueryFails;
            public int Clears;
            public Func<Task> Completion;
            public ResetIdentity GetIdentity() => new ResetIdentity(5218360, Account);
            public async Task ResetAsync(ResetIdentity expected) { Clears += 5; Values = new bool[5]; if (Completion != null) await Completion(); }
            public bool Get(string name, out bool value) { value = Values[Array.IndexOf(ResetOverlayWire.Names(), name)]; return !QueryFails; }
        }
        private sealed class Progress : IParticipantProgressReset
        {
            public int Resets;
            public IParticipantProgressReset Inner;
            public void Reset() { Resets++; Inner.Reset(); }
        }
        private string root, configPath, payloadRoot, run;
        private Paths paths;
        private Steam steam;
        private Progress progress;
        private ProcessIdentity origin, client;
        private FileExhibitionResetJournal journal;
        private ResetOverlayTrialRuntime runtime;
        private ExhibitionResetCoordinator coordinator;
        private int starts, quits;
        [SetUp]
        public void Setup()
        {
            starts = quits = 0;
            typeof(CampaignSaveCompositionProvider).GetMethod("ResetProductionSession", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            root = Path.Combine(@"D:\J2M\evidence\reset-overlay-fakes", Guid.NewGuid().ToString("N"));
            paths = new Paths { SaveRootPath = Path.Combine(root, "save") };
            payloadRoot = Path.Combine(root, "payload"); Directory.CreateDirectory(paths.SaveRootPath); Directory.CreateDirectory(payloadRoot);
            string[] files = { "VectorQuake.exe", "Exhibition-Relaunch.ps1", "VectorQuake_Data/Managed/Game.Exhibition.Integration.dll",
                "RestartExperiment/RestartExperiment.cs", "RestartExperiment/RestartExperimentWindows.cs", "RestartExperiment/RestartExperimentNativeProbe.cs", "RestartExperiment/Restart-Experiment.ps1" };
            foreach (var name in files) { string path = Path.Combine(payloadRoot, name); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "fake payload"); }
            string manifestPath = Path.Combine(root, "payload.json");
            ResetOverlayTrialFiles.Create(manifestPath, new TrialPayloadManifest { fileCount = files.Length,
                files = files.Select(n => new TrialPayloadFile { relativePath = n, sha256 = ExperimentFiles.Hash(Path.Combine(payloadRoot, n)) }).ToArray() });
            steam = new Steam();
            configPath = Path.Combine(root, "config.json");
            ResetOverlayTrialFiles.Create(configPath, new ResetOverlayTrialConfig { Version = 2, AppId = 5218360, SteamId = steam.Account, PayloadManifestPath = manifestPath });
            origin = Identity(701, Path.Combine(payloadRoot, "VectorQuake.exe"));
            File.WriteAllText(Path.Combine(root, "fake-steam.exe"), "not executable"); client = Identity(702, Path.Combine(root, "fake-steam.exe"));
            journal = new FileExhibitionResetJournal(Path.Combine(paths.SaveRootPath, "exhibition-reset.json"));
            journal.Save(new ResetRecord { OperationId = Guid.NewGuid().ToString("N"), State = ResetRecord.Ready, AppId = 5218360, SteamId = steam.Account, MappingVersion = ExhibitionResetCoordinator.MappingVersion });
            progress = new Progress { Inner = new ParticipantProgressResetAdapter(paths) };
            coordinator = new ExhibitionResetCoordinator(journal, steam, progress);
            runtime = CreateRuntime(ResetOverlayRole.Initiator, configPath, null, origin); run = runtime.EvidenceDirectory;
        }
        private ProcessIdentity Identity(int pid, string path) => new ProcessIdentity { Pid = pid, StartTicks = pid + 1000, Session = 1, UserSid = "fake-user", Logon = "fake-logon", Path = path, Sha256 = ExperimentFiles.Hash(path) };
        private ResetOverlayTrialRuntime CreateRuntime(ResetOverlayRole role, string input, string request, ProcessIdentity self)
            => new ResetOverlayTrialRuntime(role, input, request, paths, steam.GetIdentity, steam.Get, () => self, () => client,
                _ => { starts++; return null; }, _ => { }, () => quits++, () => true, () => null);
        [TearDown]
        public void Cleanup()
        {
            if (run != null && Directory.Exists(run)) Directory.Delete(run, true);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        private ResetOverlayTrial Flow() => new ResetOverlayTrial(coordinator, runtime, ResetOverlayRole.Initiator);
        [TestCase("empty")][TestCase("query")][TestCase("baseline-record")]
        public Task ActualRuntimeCannotArmWithoutStoredEarnedSdkBaseline(string failure) => Task.Run(async () =>
        {
            if (failure == "empty") steam.Values = new bool[5];
            if (failure == "query") steam.QueryFails = true;
            if (failure == "baseline-record") File.WriteAllText(Path.Combine(run, "sdk-baseline.json"), "partial");
            var f = Flow(); await f.PrepareMenuAsync(); f.ScopeConfirmed = true; await f.RequestResetAsync();
            Assert.That(f.Error, Is.Not.Null); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(starts + steam.Clears, Is.Zero); Assert.That(File.Exists(Path.Combine(run, "initial-request.claimed.json")), Is.False);
            if (failure != "baseline-record") Assert.That(File.Exists(Path.Combine(run, "sdk-baseline.json")), Is.True);
        });
        [TestCase("account")][TestCase("client")][TestCase("payload")][TestCase("files")][TestCase("non-json")][TestCase("baseline")][TestCase("report")][TestCase("operation")][TestCase("config")]
        public Task RealRevalidationRejectsChangedPinsBeforeFirstParticipantWrite(string change) => Task.Run(async () =>
        {
            var f = Flow(); await f.PrepareMenuAsync(); await f.ReportAsync("opened", new[] { "earned" }); f.ScopeConfirmed = true;
            if (change == "account") steam.Account++;
            if (change == "client") client.StartTicks++;
            if (change == "payload") File.AppendAllText(origin.Path, "changed");
            if (change == "non-json") File.WriteAllText(Path.Combine(paths.SaveRootPath, "participant.bin"), "changed");
            if (change == "files") File.WriteAllText(Path.Combine(paths.SaveRootPath, "other.json"), "changed");
            if (change == "baseline") steam.Values[1] = true;
            if (change == "report") File.AppendAllText(Path.Combine(run, "Initiator", "user-report.json"), " ");
            if (change == "operation") { var record = journal.Load(); record.OperationId = Guid.NewGuid().ToString("N"); journal.Save(record); }
            if (change == "config") File.AppendAllText(configPath, " ");
            await f.RequestResetAsync();
            Assert.That(f.Error, Is.Not.Null); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Ready)); Assert.That(starts + steam.Clears, Is.Zero);
            Assert.That(File.Exists(Path.Combine(run, "initial-request.claimed.json")), Is.False);
        });
        [Test]
        public void TrialSessionLockNeverCreatesOrChangesParticipantFiles()
        {
            var before = Directory.GetFiles(paths.SaveRootPath);
            Assert.Throws<FileNotFoundException>(() => ExhibitionApplication.AcquireSessionLock(paths, true));
            Assert.That(Directory.GetFiles(paths.SaveRootPath), Is.EquivalentTo(before));
            var lockPath = Path.Combine(paths.SaveRootPath, "exhibition-instance.lock"); File.WriteAllText(lockPath, "existing");
            using (ExhibitionApplication.AcquireSessionLock(paths, true))
                Assert.Throws<IOException>(() => ExhibitionApplication.AcquireSessionLock(paths, true));
            Assert.That(File.ReadAllText(lockPath), Is.EqualTo("existing"));
        }
        private async Task<ExperimentRequest> PrepareRequest()
        {
            var flow = Flow(); await flow.PrepareMenuAsync(); await flow.ReportAsync("opened", new[] { "earned" }); flow.ScopeConfirmed = true;
            await flow.RequestResetAsync();
            Assert.That(starts, Is.EqualTo(1)); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Pending));
            return ExperimentFiles.Read<ExperimentRequest>(Path.Combine(run, "handoff-GameOnly", "request.json"));
        }
        [Test]
        public Task ProducerHelperChildWireAndRealLocalResultLinkWithoutNativeOrGame() => Task.Run(async () =>
        {
            var request = await PrepareRequest(); var context = ResetOverlayWire.ReadContext(request);
            string requestPath = Path.Combine(request.EvidenceDirectory, "request.json");
            var child = Identity(703, origin.Path);
            ResetOverlayReceipt.Write(request, child, requestPath);
            var receipt = ExperimentFiles.Read<ResetOverlayChildReceipt>(ResetOverlayReceipt.PathFor(request));
            Assert.That(ResetOverlayReceipt.Matches(receipt, request, child, requestPath), Is.True);
            Assert.Throws<IOException>(() => ResetOverlayReceipt.Write(request, child, requestPath));
            var worker = CreateRuntime(ResetOverlayRole.ResetWorker, request.ResetOverlayContextPath, requestPath, child);
            await worker.PrepareAsync(journal.Load(), () => { }); worker.Claim("reset-worker");
            await coordinator.ResumeAsync(() => worker.ValidateRecord(coordinator.ReadRecord(), true));
            await worker.VerifyResetAsync(journal.Load(), () => { });
            await worker.SaveReportAsync("opened", new[] { "unearned" });
            var report = ExperimentFiles.Read<ResetOverlayUserReport>(Path.Combine(run, "ResetWorker", "user-report.json"));
            Assert.That(report.Source, Is.EqualTo("User")); Assert.That(report.BaselineSha256, Is.EqualTo(context.BaselineSha256));
            Assert.That(report.ResetResultSha256, Is.EqualTo(ExperimentFiles.Hash(Path.Combine(run, "ResetWorker", "reset-result.json"))));
            Assert.That(steam.Clears, Is.EqualTo(5)); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(starts, Is.EqualTo(1)); Assert.That(quits, Is.Zero);
        });
        [TestCase("old")][TestCase("partial")][TestCase("mixed")][TestCase("fullcycle")][TestCase("final")][TestCase("context")][TestCase("baseline")][TestCase("report")][TestCase("nonce")]
        public Task SharedHelperRejectsOldMixedPartialOrTamperedReset(string change) => Task.Run(async () =>
        {
            var r = await PrepareRequest();
            if (change == "old") r.ResetOverlayWireVersion = 0;
            if (change == "partial") r.ResetOverlayContextSha256 = null;
            if (change == "mixed") r.OverlayObservationWireVersion = 2;
            if (change == "fullcycle") r.Trial = Trial.FullCycle;
            if (change == "final") r.ResetOverlayChildRole = ResetOverlayRole.FinalObserver;
            if (change == "context") File.AppendAllText(r.ResetOverlayContextPath, " ");
            if (change == "baseline") File.AppendAllText(Path.Combine(run, "sdk-baseline.json"), " ");
            if (change == "report") File.AppendAllText(Path.Combine(run, "Initiator", "user-report.json"), " ");
            if (change == "nonce") r.Nonce = "bad";
            Assert.That(() => ResetOverlayWire.ReadContext(r), Throws.Exception);
        });
        [TestCase("pid")][TestCase("ticks")][TestCase("hash")][TestCase("operation")][TestCase("request")][TestCase("version")]
        public Task ReceiptMatchesExactOwnedChildAndRequestBytes(string field) => Task.Run(async () =>
        {
            var r = await PrepareRequest(); var child = Identity(703, origin.Path); var path = Path.Combine(r.EvidenceDirectory, "request.json");
            ResetOverlayReceipt.Write(r, child, path); var receipt = ExperimentFiles.Read<ResetOverlayChildReceipt>(ResetOverlayReceipt.PathFor(r));
            if (field == "pid") child.Pid++;
            if (field == "ticks") child.StartTicks++;
            if (field == "hash") child.Sha256 = new string('a', 64);
            if (field == "operation") receipt.OperationId = Guid.NewGuid().ToString("N");
            if (field == "request") File.AppendAllText(path, " ");
            if (field == "version") receipt.Version = 1;
            Assert.That(ResetOverlayReceipt.Matches(receipt, r, child, path), Is.False);
        });
        [TestCase("after-preparation")][TestCase("after-claim")][TestCase("after-steam")]
        public Task WorkerGuardRejectsClientReplacementWithSameAccountAndAvailableSdk(string boundary) => Task.Run(async () =>
        {
            var request = await PrepareRequest(); var child = Identity(703, origin.Path);
            var worker = CreateRuntime(ResetOverlayRole.ResetWorker, request.ResetOverlayContextPath, Path.Combine(request.EvidenceDirectory, "request.json"), child);
            await worker.PrepareAsync(journal.Load(), () => { });
            if (boundary == "after-preparation") client.Pid++;
            worker.Claim("reset-worker");
            if (boundary == "after-claim") client.StartTicks++;
            if (boundary == "after-steam") steam.Completion = async () => { await Task.Yield(); client.StartTicks++; };
            Assert.That(worker.Available, Is.True); Assert.That(steam.GetIdentity().SteamId, Is.EqualTo(request.SteamId));
            bool rejected = false;
            // This is the same production record guard used by ResetOverlayTrial.PrepareCoreAsync.
            try { await coordinator.ResumeAsync(() => worker.ValidateRecord(coordinator.ReadRecord(), true)); }
            catch (IOException e) { rejected = true; Assert.That(e.Message, Does.Contain("Steam client identity changed")); }
            Assert.That(rejected, Is.True);
            Assert.That(steam.Clears, Is.EqualTo(boundary == "after-steam" ? 5 : 0));
            Assert.That(progress.Resets, Is.Zero); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Pending));
            Assert.That(File.Exists(Path.Combine(run, "ResetWorker", "reset-result.json")), Is.False);
            Assert.Throws<IOException>(() => worker.Claim("reset-worker"));
        });
        [TestCase("sdk")][TestCase("local")][TestCase("files")][TestCase("record")]
        public Task RequiredResetResultPersistsVerificationFailureWithoutRepeatingReset(string failure) => Task.Run(async () =>
        {
            var request = await PrepareRequest(); var child = Identity(703, origin.Path);
            var worker = CreateRuntime(ResetOverlayRole.ResetWorker, request.ResetOverlayContextPath, Path.Combine(request.EvidenceDirectory, "request.json"), child);
            await worker.PrepareAsync(journal.Load(), () => { }); worker.Claim("reset-worker"); await coordinator.ResumeAsync();
            var resultPath = Path.Combine(run, "ResetWorker", "reset-result.json");
            if (failure == "sdk") steam.Values[0] = true;
            if (failure == "local") File.WriteAllText(Path.Combine(paths.SaveRootPath, "achievements.json.bak"), "invalid");
            if (failure == "files") File.WriteAllText(Path.Combine(paths.SaveRootPath, "unrelated.json"), "changed");
            if (failure == "record") File.WriteAllText(resultPath, "partial");
            bool rejected = false;
            try { await worker.VerifyResetAsync(journal.Load(), () => { }); } catch (IOException) { rejected = true; }
            Assert.That(rejected, Is.True); Assert.That(steam.Clears, Is.EqualTo(5)); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Ready));
            Assert.That(File.Exists(Path.Combine(run, "ResetWorker", "user-report.json")), Is.False);
            if (failure == "record") Assert.That(File.ReadAllText(resultPath), Is.EqualTo("partial"));
            else
            {
                var result = ExperimentFiles.Read<ResetOverlayResult>(resultPath);
                Assert.That(result.LocalVerified, Is.EqualTo(failure != "local"));
                if (failure != "sdk") Assert.That(result.VerificationError, Is.Not.Null);
                else Assert.That(result.Achieved[0], Is.True);
            }
        });
        [Test]
        public Task SharedHelperClaimCannotBeReplayedEvenWithSameValidRequest() => Task.Run(async () =>
        {
            var request = await PrepareRequest(); ResetOverlayWire.ClaimChildCreation(request);
            Assert.Throws<IOException>(() => ResetOverlayWire.ClaimChildCreation(request));
            request.Nonce = Guid.NewGuid().ToString("N");
            Assert.Throws<IOException>(() => ResetOverlayWire.ReadContext(request));
        });
        [Test]
        public Task ActualCreationBoundaryCancellationRetainsClaimAndPendingWithoutProcessStart() => Task.Run(async () =>
        {
            await runtime.PrepareAsync(journal.Load(), () => { }); await runtime.SaveReportAsync("opened", new[] { "earned" });
            runtime.ConfirmScope(); await runtime.RevalidateAsync(() => { }); runtime.Claim("initial-request"); coordinator.RequestReset();
            await runtime.BindPendingAsync(journal.Load(), () => { });
            Assert.Throws<OperationCanceledException>(() => runtime.StartHelper(() => { }, () => throw new OperationCanceledException()));
            Assert.That(starts, Is.Zero); Assert.That(journal.Load().State, Is.EqualTo(ResetRecord.Pending));
            Assert.Throws<IOException>(() => runtime.Claim("initial-request"));
        });
        [Test]
        public void OrdinaryFullCycleRemainsAnIndependentValidRequest()
        {
            Assert.DoesNotThrow(() => LaunchEnvironment.ValidateChildRole(new ExperimentRequest { Trial = Trial.FullCycle }));
        }
    }
}
