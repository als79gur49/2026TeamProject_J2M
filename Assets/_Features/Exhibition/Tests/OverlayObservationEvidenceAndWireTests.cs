using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class OverlayObservationEvidenceAndWireTests
    {
        private string root;
        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(@"D:\J2M\evidence\overlay-handoff-automated", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }
        // Test-owned evidence remains available for inspection; no participant save path is ever opened.

        [Test, Category("Integration")]
        public void SaveManifestReadsWithoutCleanupAndRejectsChangesOrExtraBackups()
        {
            var file = Path.Combine(root, "achievements.json"); File.WriteAllText(file, "{\"earned\":[]}");
            File.WriteAllText(file + ".tmp", "interrupted");
            var initial = OverlayObservationEvidence.CaptureSave(root);
            Assert.That(File.ReadAllText(file + ".tmp"), Is.EqualTo("interrupted"));
            File.WriteAllText(Path.Combine(root, "exhibition-instance.lock"), "lease");
            Assert.DoesNotThrow(() => OverlayObservationEvidence.RequireSame(initial, OverlayObservationEvidence.CaptureSave(root)));
            File.WriteAllText(file + ".bak", "backup");
            Assert.Throws<IOException>(() => OverlayObservationEvidence.RequireSame(initial, OverlayObservationEvidence.CaptureSave(root)));
            File.Delete(file + ".bak"); File.WriteAllText(file, "different");
            Assert.Throws<IOException>(() => OverlayObservationEvidence.RequireSame(initial, OverlayObservationEvidence.CaptureSave(root)));
            Assert.That(File.ReadAllText(file), Is.EqualTo("different"));
        }

        [Test, Category("Integration")]
        public void ReadOnlyJournalRejectsPendingAndDoesNotRepairCompanions()
        {
            var path = Path.Combine(root, "exhibition-reset.json");
            File.WriteAllText(path + ".bak", "unreadable backup");
            Assert.Throws<IOException>(() => new FileExhibitionResetJournal(path).Load());
            Assert.That(File.Exists(path), Is.False); Assert.That(File.Exists(path + ".bak"), Is.True);
        }

        [Test, Category("Integration")]
        public void RetainedSaveStoreAndJournalCannotWriteOrRecoverAfterObservationEntry()
        {
            var store = new Game.Feature.Stages.AtomicTextFileStore(root);
            store.WriteAllTextAtomic("achievements.json", "before");
            File.WriteAllText(Path.Combine(root, "achievements.json.rollback"), "prior interruption");
            var before = OverlayObservationEvidence.CaptureSave(root);
            Game.Feature.Stages.CampaignSaveCompositionProvider.InhibitForObservation();
            try
            {
                Assert.Throws<InvalidOperationException>(() => store.WriteAllTextAtomic("achievements.json", "after"));
                Assert.Throws<InvalidOperationException>(() => store.Delete("achievements.json"));
                Assert.Throws<InvalidOperationException>(() => store.TryRestoreBackup("achievements.json"));
                Assert.Throws<InvalidOperationException>(() => store.CleanupTempFiles("achievements.json"));
                Assert.Throws<InvalidOperationException>(() => store.ReadAllText("achievements.json"));
                Assert.Throws<InvalidOperationException>(() => new FileExhibitionResetJournal(Path.Combine(root, "exhibition-reset.json")).Save(null));
                Assert.DoesNotThrow(() => OverlayObservationEvidence.RequireSame(before, OverlayObservationEvidence.CaptureSave(root)));
            }
            finally
            {
                typeof(Game.Feature.Stages.CampaignSaveCompositionProvider).GetMethod("ResetProductionSession",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            }
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), Category("Integration")]
        public void ProductionReadyPinRejectsPendingAccountMappingAndOperationChanges(int mutation)
        {
            var record = new ResetRecord { State = ResetRecord.Ready, AppId = 5218360, SteamId = 123,
                MappingVersion = ExhibitionResetCoordinator.MappingVersion, OperationId = Guid.NewGuid().ToString("N") };
            var config = new OverlayHandoffObservationConfig { Version = 1, AppId = record.AppId, SteamId = record.SteamId };
            var context = new OverlayObservationContext { OperationId = record.OperationId, MappingVersion = record.MappingVersion, ReadyState = record.State };
            Assert.DoesNotThrow(() => OverlayHandoffObservationRuntime.ValidateReady(record, config, context));
            if (mutation == 0) record.State = ResetRecord.Pending;
            if (mutation == 1) record.SteamId++;
            if (mutation == 2) record.MappingVersion = "other";
            if (mutation == 3) record.OperationId = Guid.NewGuid().ToString("N");
            Assert.Throws<IOException>(() => OverlayHandoffObservationRuntime.ValidateReady(record, config, context));
        }

        [TestCase(0), TestCase(2), Category("Integration")]
        public void InvalidConfigurationVersionIsNotAccepted(int version)
        {
            Assert.Throws<IOException>(() => OverlayHandoffObservationRuntime.ValidateConfig(new OverlayHandoffObservationConfig {
                Version = version, AppId = 5218360, SteamId = 123, PayloadManifestPath = root + "\\manifest.json", EvidenceRoot = root }));
        }

        private static ExperimentRequest Request() => new ExperimentRequest {
            Trial = Trial.GameOnly, Nonce = Guid.NewGuid().ToString("N"), AppId = 5218360, SteamId = 123,
            Parent = new ProcessIdentity { Pid = 10, StartTicks = 20, Session = 1, UserSid = "fake", Logon = "fake", Path = @"D:\payload\game.exe", Sha256 = new string('a', 64) },
            OverlayObservationContextSha256 = new string('c', 64), OverlayObservationWireVersion = 2, OverlayObservationChildRole = OverlayObservationRole.ReplacementObserver,
            OverlayObservationRunId = Guid.NewGuid().ToString("N"), OverlayObservationContextPath = @"D:\J2M\evidence\run\replacement-context.json" };

        [Test, Category("Integration")]
        public void ActualLaunchPreparationUsesGameOnlyPolicyAndOnlyObservationArguments()
        {
            var request = Request(); int checks = 0;
            var start = LaunchEnvironment.PrepareGame(request, @"D:\J2M\evidence\run\request.json", () => checks++, () => checks++, () => checks++, _ => { });
            Assert.That(checks, Is.EqualTo(3)); Assert.That(start.FileName, Is.EqualTo(request.Parent.Path));
            Assert.That(start.Arguments, Does.Contain("-j2mOverlayHandoffContext"));
            Assert.That(start.Arguments, Does.Contain("-j2mOverlayHandoffRequest"));
            Assert.That(start.Arguments, Does.Not.Contain("-j2mRestartObservation"));
            Assert.That(start.Arguments, Does.Not.Contain("-j2mResetOverlay"));
            Assert.That(start.EnvironmentVariables["SteamAppId"], Is.EqualTo("5218360"));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), Category("Integration")]
        public void OldPartialMixedOrWrongRoleRequestsFailAtActualLaunchBoundary(int mutation)
        {
            var request = Request();
            if (mutation == 0) request.OverlayObservationWireVersion = 0;
            if (mutation == 1) request.OverlayObservationContextPath = null;
            if (mutation == 2) request.ResetOverlayChildRole = ResetOverlayRole.ResetWorker;
            if (mutation == 3) request.OverlayObservationChildRole = OverlayObservationRole.OriginObserver;
            if (mutation == 4) request.Trial = Trial.FullCycle;
            if (mutation == 5) request.OverlayObservationWireVersion = 1;
            int created = 0;
            Assert.Throws<IOException>(() => LaunchEnvironment.Start(() => LaunchEnvironment.PrepareGame(request, "request", () => { }, () => { }, () => { }, _ => { }),
                null, _ => { created++; return null; }));
            Assert.That(created, Is.Zero);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), Category("Integration")]
        public void ReceiptRejectsWrongNonceRolePidStartHashAndAccount(int mutation)
        {
            var request = Request(); var child = new ProcessIdentity { Pid = 11, StartTicks = 22, Path = request.Parent.Path, Sha256 = request.Parent.Sha256, Session = 1, UserSid = "fake", Logon = "fake" };
            var context = new OverlayObservationContext { Version = 2, RunId = request.OverlayObservationRunId, AppId = request.AppId, SteamId = request.SteamId,
                Origin = request.Parent, ConfigurationSha256 = "config", ManifestSha256 = "manifest", OperationId = "op", MappingVersion = "mapping", ReadyState = "Ready" };
            var row = new OverlayObservationReceipt { Version = 2, Nonce = request.Nonce, RunId = context.RunId, ContextPath = request.OverlayObservationContextPath, ContextSha256 = request.OverlayObservationContextSha256,
                Role = OverlayObservationRole.ReplacementObserver, AppId = context.AppId, SteamId = context.SteamId, Origin = context.Origin,
                Child = ExperimentFiles.Parse<ProcessIdentity>(ExperimentFiles.Json(child)), ConfigurationSha256 = context.ConfigurationSha256,
                ManifestSha256 = context.ManifestSha256, OperationId = context.OperationId, MappingVersion = context.MappingVersion, ReadyState = context.ReadyState };
            Assert.That(OverlayObservationWire.Matches(row, request, context, child), Is.True);
            if (mutation == 0) row.Nonce = "different";
            if (mutation == 1) row.Role = OverlayObservationRole.OriginObserver;
            if (mutation == 2) row.Child.Pid++;
            if (mutation == 3) row.Child.StartTicks++;
            if (mutation == 4) row.Child.Sha256 = new string('b', 64);
            if (mutation == 5) row.SteamId++;
            Assert.That(OverlayObservationWire.Matches(row, request, context, child), Is.False);
        }

        [Test, Category("Integration")]
        public void GameOnlyRetainsThirtySecondParentWaitAndNullCreationDeadline()
        {
            var env = new CycleEnvironment(); Cycle.Run(env, Trial.GameOnly);
            Assert.That(env.GameCreates, Is.EqualTo(1)); Assert.That(env.SteamExits, Is.Zero);
            Assert.That(env.GameDeadline, Is.Null); Assert.That(env.TerminalBeforeRelease, Is.True);
            env = new CycleEnvironment { ParentNeverExits = true };
            Assert.Throws<TimeoutException>(() => Cycle.Run(env, Trial.GameOnly));
            Assert.That(env.Milliseconds, Is.EqualTo(30000)); Assert.That(env.GameCreates, Is.Zero);
        }

        [TestCase(OverlayVisibility.Opened, "opened", true), TestCase(OverlayVisibility.NotVisible, "not-visible", true),
         TestCase(OverlayVisibility.Inconclusive, "inconclusive", false), Category("Integration")]
        public async Task ActualReportWriterStoresUserIdentityOnce(OverlayVisibility visibility, string name, bool attempted)
        {
            // Inject only identities and a test-owned directory; bypass all real process/native/save construction.
            var runtime = (OverlayHandoffObservationRuntime)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OverlayHandoffObservationRuntime));
            var context = new OverlayObservationContext { RunId = Guid.NewGuid().ToString("N") };
            var identity = Request().Parent;
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(OverlayHandoffObservationRuntime).GetField("context", fields).SetValue(runtime, context);
            typeof(OverlayHandoffObservationRuntime).GetField("self", fields).SetValue(runtime, identity);
            typeof(OverlayHandoffObservationRuntime).GetField("roleDirectory", fields).SetValue(runtime, root);
            typeof(OverlayHandoffObservationRuntime).GetField("options", fields).SetValue(runtime, new OverlayHandoffObservationOptions { Role = OverlayObservationRole.OriginObserver });
            await Task.Run(() => runtime.SaveObservationAsync(visibility)).ConfigureAwait(false);
            string path = Path.Combine(root, "user-observation.json");
            var before = File.ReadAllBytes(path);
            var row = ExperimentFiles.Read<OverlayUserObservation>(path);
            Assert.That(row.Source, Is.EqualTo("User")); Assert.That(row.RunId, Is.EqualTo(context.RunId));
            Assert.That(row.Role, Is.EqualTo(OverlayObservationRole.OriginObserver)); Assert.That(row.Process.StartTicks, Is.EqualTo(identity.StartTicks));
            Assert.That(row.Visibility, Is.EqualTo(name)); Assert.That(row.AttemptReported, Is.EqualTo(attempted));
            Assert.That(row.Utc, Is.Not.Empty); Assert.That(row.MonotonicSeconds, Is.GreaterThan(0));
            try { await Task.Run(() => runtime.SaveObservationAsync(visibility)).ConfigureAwait(false); Assert.Fail("Duplicate report accepted"); }
            catch (IOException) { }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        private OverlayObservationContext ContextWithReport()
        {
            var request = Request();
            var context = new OverlayObservationContext { Version = 2, RunId = request.OverlayObservationRunId,
                Directory = root, Role = OverlayObservationRole.ReplacementObserver, Origin = request.Parent,
                AppId = request.AppId, SteamId = request.SteamId, OperationId = Guid.NewGuid().ToString("N"),
                ReadyState = "Ready", MappingVersion = "mapping", ConfigurationPath = Path.Combine(root, "config.json"),
                ConfigurationSha256 = new string('a', 64), ManifestSha256 = new string('b', 64), SaveManifestSha256 = new string('c', 64),
                OriginObservationPath = Path.Combine(root, "user-observation.json") };
            var report = new OverlayUserObservation { RunId = context.RunId, Role = OverlayObservationRole.OriginObserver,
                Process = context.Origin, Source = "User", AttemptReported = true, Visibility = "opened", Utc = DateTime.UtcNow.ToString("o"), MonotonicSeconds = 4000 };
            ResetOverlayTrialFiles.Create(context.OriginObservationPath, report);
            context.OriginObservationSha256 = ExperimentFiles.Hash(context.OriginObservationPath);
            return context;
        }

        [TestCase("hash"), TestCase("role"), TestCase("run"), TestCase("process"), TestCase("visibility"), TestCase("attempt"), TestCase("source"), TestCase("path"), Category("Integration")]
        public void ActualContextConsumerRejectsTamperedOriginReport(string mutation)
        {
            var context = ContextWithReport();
            Assert.DoesNotThrow(() => OverlayObservationWire.ValidateOriginReport(context));
            var report = ExperimentFiles.Read<OverlayUserObservation>(context.OriginObservationPath);
            if (mutation == "role") report.Role = OverlayObservationRole.ReplacementObserver;
            if (mutation == "run") report.RunId = Guid.NewGuid().ToString("N");
            if (mutation == "process") report.Process.StartTicks++;
            if (mutation == "visibility") report.Visibility = "not-visible";
            if (mutation == "attempt") report.AttemptReported = false;
            if (mutation == "source") report.Source = "Api";
            File.WriteAllText(context.OriginObservationPath, ExperimentFiles.Json(report));
            if (mutation != "hash") context.OriginObservationSha256 = ExperimentFiles.Hash(context.OriginObservationPath);
            else context.OriginObservationSha256 = new string('f', 64);
            if (mutation == "path") context.OriginObservationPath = Path.Combine(root, "..", "outside.json");
            Assert.Throws<IOException>(() => OverlayObservationWire.ValidateOriginReport(context));
        }

        [Test, Category("Integration")]
        public void OriginContextNeedsNoFutureReportButReplacementRequiresIt()
        {
            var context = ContextWithReport(); context.Role = OverlayObservationRole.OriginObserver;
            context.OriginObservationPath = context.OriginObservationSha256 = null;
            Assert.DoesNotThrow(() => OverlayObservationWire.ValidateContext(context, context.Role));
            context.Role = OverlayObservationRole.ReplacementObserver;
            Assert.Throws<IOException>(() => OverlayObservationWire.ValidateContext(context, context.Role));
            context.Version = 1; context.Role = OverlayObservationRole.OriginObserver;
            Assert.Throws<IOException>(() => OverlayObservationWire.ValidateContext(context, context.Role));
        }

        [Test, Category("Integration")]
        public void RealV2ContextRequestReceiptRoundTripDoesNotNeedSdkDocuments()
        {
            var context = ContextWithReport(); var request = Request();
            request.Parent = context.Origin; request.OperationId = context.OperationId; request.OverlayObservationRunId = context.RunId;
            request.EvidenceDirectory = root; request.OverlayObservationContextPath = Path.Combine(root, "context.json");
            ResetOverlayTrialFiles.Create(request.OverlayObservationContextPath, context);
            request.OverlayObservationContextSha256 = ExperimentFiles.Hash(request.OverlayObservationContextPath);
            var child = ExperimentFiles.Parse<ProcessIdentity>(ExperimentFiles.Json(request.Parent)); child.Pid++; child.StartTicks++;
            OverlayObservationWire.WriteReceipt(request, child);
            var receipt = ExperimentFiles.Read<OverlayObservationReceipt>(OverlayObservationWire.ReceiptPath(request));
            Assert.That(OverlayObservationWire.Matches(receipt, request, OverlayObservationWire.ReadContext(request), child), Is.True);
            Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(3));
            Assert.Throws<IOException>(() => OverlayObservationWire.WriteReceipt(request, child));
            receipt.Version = 1; Assert.That(OverlayObservationWire.Matches(receipt, request, context, child), Is.False);
        }

        private sealed class CycleEnvironment : ICycleEnvironment
        {
            public long Milliseconds { get; private set; }
            public bool ParentNeverExits, TerminalBeforeRelease;
            public int GameCreates, SteamExits;
            public Deadline GameDeadline;
            private bool terminal, cleaned;
            public void Validate() { }
            public IDisposable AcquireCycleLock() => new Lease(() => TerminalBeforeRelease = terminal && cleaned);
            public bool ParentAlive() => ParentNeverExits;
            public void EnsureNoOtherGame() { }
            public bool OriginalSteamAlive() => true;
            public void RequestSteamExit(Deadline deadline) => SteamExits++;
            public bool ShutdownCommandAlive() => false;
            public void EnsureSteamExited() { }
            public void StartSteam() { }
            public void EnsureNewSteamUnchanged() { }
            public bool ProbeReady(Deadline deadline) => true;
            public void StartGame(Deadline deadline) { GameCreates++; GameDeadline = deadline; Milliseconds += 200000; }
            public void Delay(int milliseconds) => Milliseconds += milliseconds;
            public void Record(string stage) { }
            public void Cleanup() => cleaned = true;
            public void RecordTerminal(Exception failure) => terminal = true;
            private sealed class Lease : IDisposable { private readonly Action release; public Lease(Action release) => this.release = release; public void Dispose() => release(); }
        }
    }
}
