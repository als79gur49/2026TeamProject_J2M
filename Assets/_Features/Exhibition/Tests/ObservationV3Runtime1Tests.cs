using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Exhibition.Integration;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class ObservationV3Runtime1Tests
    {
        private static readonly string Run = Guid.NewGuid().ToString("N");
        private static readonly string[] Targets = { ResetOverlayWire.Names().First() };
        private static ObservationRef Ref(string name) => new ObservationRef { Path = @"D:\J2M\evidence\fake\" + name + ".json", Sha256 = new string('a', 64) };
        private static ProcessIdentity Self() => new ProcessIdentity { Pid = 20, StartTicks = 200, Session = 1, UserSid = "S-1-5-21-1", Logon = "x", Path = @"D:\fake.exe", Sha256 = new string('a', 64) };
        private static T Doc<T>(T d, string kind) where T : ObservationDocument => ObservationV3RuntimeWire.Stamp(d, kind, "test", 1, Run);
        private static ObservationV3Visibility Visibility(string value, ObservationRef correction = null) => Doc(new ObservationV3Visibility {
            BindingRef = Ref("context"), BaselineRef = Ref("baseline"), CorrectsRef = correction, Self = Self(), Role = "ReplacementObserver",
            Visibility = value, OriginalText = "user observation", ObservedUtc = null }, "visibility-report");
        private static ObservationV3Display Display(string value, ObservationRef visibility, ObservationRef correction = null) => Doc(new ObservationV3Display {
            BindingRef = Ref("context"), BaselineRef = Ref("baseline"), VisibilityRef = visibility, CorrectsRef = correction, Self = Self(), Role = "ReplacementObserver",
            Targets = Targets.Select(t => new ObservationV3TargetDisplay { Id = t, Display = value }).ToArray(), OriginalText = "user report", ObservedUtc = null }, "display-report");
        private static ObservationV3ReportLedger Ledger(bool bind = true) => new ObservationV3ReportLedger(Run, "ReplacementObserver", bind ? Self() : null, Targets, Ref("context"), Ref("baseline"));

        [Test]
        public void AcceptedBeforeAckOrFailedReadClosesAdmission()
        {
            var early = new ObservationV3AcceptanceAdmission();
            Assert.IsFalse(early.Received(true)); Assert.Throws<IOException>(() => early.BeginAck());
            var eof = new ObservationV3AcceptanceAdmission();
            Assert.IsFalse(eof.Received(false)); Assert.Throws<IOException>(() => eof.BeginAck());
            var normal = new ObservationV3AcceptanceAdmission(); normal.BeginAck(); Assert.IsTrue(normal.Received(true));
        }
        [Test]
        public void CompletionAtDeadlineCannotResumeSideEffects()
        {
            using (var q = new ObservationV3EventQueue())
            {
                long now = 0; bool accepted = false; Exception error = null;
                using (var op = new ObservationV3Operations(q, () => Interlocked.Read(ref now), e => error = e))
                {
                    var pending = new TaskCompletionSource<bool>();
                    op.Start(t => pending.Task, r => accepted = true);
                    Interlocked.Exchange(ref now, 30000); pending.SetResult(true);
                    Pump(q, () => op.Closed); for (int i = 0; i < 5; i++) q.DrainOne(5);
                    Assert.IsFalse(accepted); Assert.IsInstanceOf<TimeoutException>(error);
                }
            }
        }
        [TestCase("ProtocolRevision")][TestCase("Version")][TestCase("Sequence")][TestCase("RunId")][TestCase("Role")][TestCase("CorrectsRef")]
        public void MissingRequiredFieldsAreRejectedEvenWhenClrDefaultsExist(string field)
        {
            var json = ObservationV3Wire.Serialize(Visibility("opened"));
            var xml = new System.Xml.XmlDocument();
            using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), System.Xml.XmlDictionaryReaderQuotas.Max)) xml.Load(reader);
            var node = xml.DocumentElement.ChildNodes.Cast<System.Xml.XmlElement>().Single(n => n.Name == field); xml.DocumentElement.RemoveChild(node);
            using (var memory = new MemoryStream())
            {
                using (var writer = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonWriter(memory, Encoding.UTF8, false)) { xml.WriteTo(writer); writer.Flush(); }
                Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3Visibility>(memory.ToArray()));
            }
        }
        [Test]
        public void ExplicitNullableReportFieldsRoundTrip()
        {
            var v = ObservationV3RuntimeWire.Parse<ObservationV3Visibility>(ObservationV3RuntimeWire.Bytes(Visibility("opened")));
            Assert.IsNull(v.CorrectsRef); Assert.IsNull(v.ObservedUtc); Assert.AreEqual("opened", v.Visibility);
        }
        [TestCase("plain")][TestCase("with space")][TestCase("quote\"and\\slash")][TestCase("trailing\\")][TestCase("한글 日本語")][TestCase("")]
        public void WindowsQuotedTokensRoundTrip(string token)
        { Assert.AreEqual(new[] { token }, ObservationV3Arguments.Tokenize(ObservationV3Arguments.Quote(token))); }
        [Test]
        public void MeaningHashIgnoresOrderButRejectsDuplicateProviderAndUnityOptions()
        {
            var a = new[] { "-j2mOverlayV3Role", "OriginObserver", "-j2mOverlayV3Owner", "SteamDelegated", "-j2mPlatformProvider", "steam" };
            var b = a.Skip(4).Concat(a.Take(4)).ToArray();
            Assert.AreEqual(ObservationV3Arguments.SemanticHash(a), ObservationV3Arguments.SemanticHash(b));
            Assert.Throws<IOException>(() => ObservationV3Arguments.SemanticHash(a.Concat(new[] { "-j2mPlatformProvider", "steam" }).ToArray()));
            Assert.Throws<IOException>(() => ObservationV3Arguments.SemanticHash(a.Concat(new[] { "-logFile", "log" }).ToArray()));
            Assert.Throws<IOException>(() => ObservationV3Options.Parse(a.Concat(new[] { "-j2mOverlayV3Config", @"D:\old-config.json" }).ToArray()));
        }
        [Test]
        public void VisibilityCorrectionInvalidatesPreviouslySuccessfulDisplay()
        {
            var l = Ledger(); l.Submit(1); l.Commit(1, Visibility("opened"), Ref("v1"));
            l.Submit(2); l.Commit(2, Display("unearned", Ref("v1")), Ref("d1")); Assert.IsTrue(l.PurposeAchieved);
            l.Submit(3); l.Commit(3, Visibility("inconclusive", Ref("v1")), Ref("v2")); Assert.IsFalse(l.PurposeAchieved);
            Assert.Throws<IOException>(() => l.Validate(Display("unearned", Ref("v1"))));
        }
        [Test]
        public void CheckpointAcceptsAckPrefixRatherThanOnlyLatestHash()
        {
            var l = Ledger(); l.Submit(1); l.Commit(1, Visibility("opened"), Ref("v1")); var prefix = l.Checkpoint();
            l.Submit(2); l.Commit(2, Display("unearned", Ref("v1")), Ref("d1"));
            Assert.DoesNotThrow(() => l.CheckPrefix(prefix)); prefix.ReportRef = Ref("other");
            Assert.Throws<IOException>(() => l.CheckPrefix(prefix));
        }
        [Test]
        public void LaunchPlanContainsOnlyCurrentExecutionInputs()
        {
            var p = Doc(new ObservationV3LaunchPlan { AppId = 5218360, SteamExe = Ref("steam"), GameExe = Ref("game"), NativeDll = Ref("dll"), PayloadRoot = @"D:\payload", WorkingDirectory = @"D:\payload" }, "launch-plan");
            string json = ObservationV3Wire.Serialize(p); StringAssert.DoesNotContain("RunId", json);
            Assert.AreEqual(p.AppId, ObservationV3RuntimeWire.Parse<ObservationV3LaunchPlan>(Encoding.UTF8.GetBytes(json)).AppId);
        }
        [Test]
        public void MinimalLocalConfigNeedsNoReleaseDocumentsOrInventory()
        {
            string root = Path.Combine(@"D:\J2M\evidence", "overlay-v3-config-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var plan = Doc(new ObservationV3LaunchPlan { AppId = 5218360, PayloadRoot = root, WorkingDirectory = root,
                    SteamExe = Ref("steam"), GameExe = Ref("game"), NativeDll = Ref("dll") }, "launch-plan");
                var planRef = ObservationV3RuntimeWire.Create(root, "plan.json", plan);
                var config = Doc(new ObservationV3OriginConfig { AppId = 5218360, ExpectedSteamId = 123, Targets = Targets,
                    EvidenceRoot = root, LaunchPlanRef = planRef }, "origin-config");
                var configRef = ObservationV3RuntimeWire.Create(root, "config.json", config);
                var bundle = ObservationV3Bundle.LoadConfig(configRef.Path);
                File.WriteAllText(Path.Combine(root, "unrelated.txt"), "unrelated file");
                Assert.DoesNotThrow(bundle.ValidatePins);
                Assert.Catch<IOException>(bundle.RequireLaunchAvailable, "Missing actual executables still block handoff");
                string old = ObservationV3Wire.Serialize(config).TrimEnd('}') + ",\"PayloadRef\":null}";
                Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3OriginConfig>(Encoding.UTF8.GetBytes(old)));
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase(false)][TestCase(true)]
        public void BaselineRecordsEarnedValueWithoutBlockingObservation(bool earned)
        {
            var config = new ObservationV3OriginConfig { AppId = 5218360, ExpectedSteamId = 123, Targets = Targets };
            var prep = Doc(new ObservationV3OriginPreparation { Self = Self(), OriginalSteam = Self() }, "origin-preparation");
            var baseline = Doc(new ObservationV3Baseline { PreparationRef = Ref("prep"), Origin = Self(), OriginalSteam = Self(),
                AppId = config.AppId, ExpectedSteamId = 123, ObservedSteamId = 123, LoggedOn = true,
                Targets = Targets.Select(t => new ObservationV3AchievementValue { Target = t, ReadSucceeded = true, Achieved = earned }).ToArray() }, "baseline");
            Assert.DoesNotThrow(() => ObservationV3RuntimeWire.Baseline(baseline, Ref("prep"), prep, config));
            baseline.Targets[0].ReadSucceeded = false;
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Baseline(baseline, Ref("prep"), prep, config));
        }
        [TestCase("run")][TestCase("preparation")][TestCase("self")][TestCase("phase")][TestCase("order")]
        public void BaselinePinsRejectMixedOwnershipOrQueryOrder(string fault)
        {
            var prep = Doc(new ObservationV3OriginPreparation { Self = Self(), OriginalSteam = Self() }, "origin-preparation");
            var baseline = Doc(new ObservationV3Baseline { Started = 10, Finished = 20 }, "baseline");
            var before = Doc(new ObservationV3PinEvidence { Self = Self(), Client = Self(), PreparationRef = Ref("prep"), Phase = "baseline-before", Monotonic = 9 }, "pin-validation");
            var after = Doc(new ObservationV3PinEvidence { Self = Self(), Client = Self(), PreparationRef = Ref("prep"), Phase = "baseline-after", Monotonic = 21 }, "pin-validation");
            ObservationV3RuntimeWire.BaselinePins(baseline, prep, Ref("prep"), before, after);
            if (fault == "run") after.RunId = Guid.NewGuid().ToString("N");
            if (fault == "preparation") after.PreparationRef = Ref("other");
            if (fault == "self") after.Self.Pid++;
            if (fault == "phase") after.Phase = "baseline-before";
            if (fault == "order") after.Monotonic = 19;
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.BaselinePins(baseline, prep, Ref("prep"), before, after));
        }
        private sealed class PlayerPorts : IObservationV3PlayerPorts
        {
            public bool CanRestart => true;
            public string[] Targets => ObservationV3Runtime1Tests.Targets;
            public int Launches, Closes;
            public Task Prepare() => Task.CompletedTask;
            public Task StoreVisibility(string v, string text, string utc) => Task.CompletedTask;
            public Task StoreDisplay(ObservationV3TargetDisplay[] v, string text, string utc) => Task.CompletedTask;
            public Task FullCycle() { Launches++; return Task.CompletedTask; }
            public Task Close() { Closes++; return Task.CompletedTask; }
        }
        [Test]
        public async Task OriginFlowAcceptsStoredUnearnedAndCorrectionInvalidatesLaunch()
        {
            var p = new PlayerPorts(); var f = new ObservationV3Flow(p, OverlayObservationRole.OriginObserver);
            await f.PrepareMenuAsync(); Assert.IsFalse(f.CanFullCycle);
            await f.ReportVisibility("opened", "visible");
            await f.ReportDisplay(new[] { new ObservationV3TargetDisplay { Id = Targets[0], Display = "earned" } }, "earned");
            Assert.IsTrue(f.CanFullCycle);
            await f.ReportVisibility("not-visible", "corrected"); Assert.IsFalse(f.CanFullCycle);
            await f.FullCycleAsync(); Assert.AreEqual(0, p.Launches);
            await f.ReportVisibility("opened", "visible");
            await f.ReportDisplay(new[] { new ObservationV3TargetDisplay { Id = Targets[0], Display = "unearned" } }, "already changed");
            Assert.AreEqual(0, p.Closes); Assert.AreEqual(0, p.Launches); Assert.IsTrue(f.CanReport);
            Assert.IsTrue(f.CanFullCycle);
            await f.FullCycleAsync(); await f.FullCycleAsync(); Assert.AreEqual(1, p.Launches);
        }
        [TestCase("earned", true)]
        [TestCase("unearned", true)]
        [TestCase("inconclusive", false)]
        public async Task FullCycleFlowClaimsOnlyOnceAndReplacementCannotLaunch(string display, bool allowed)
        {
            foreach (var role in new[] { OverlayObservationRole.OriginObserver, OverlayObservationRole.ReplacementObserver })
            {
                var p = new PlayerPorts(); var f = new ObservationV3Flow(p, role); await f.PrepareMenuAsync();
                await f.ReportVisibility("opened", "visible");
                await f.ReportDisplay(new[] { new ObservationV3TargetDisplay { Id = Targets[0], Display = display } }, display);
                await f.FullCycleAsync(); await f.FullCycleAsync();
                Assert.AreEqual(role == OverlayObservationRole.OriginObserver && allowed ? 1 : 0, p.Launches);
            }
        }
        private sealed class Ports : IObservationV3RuntimeHostPorts
        {
            internal readonly TaskCompletionSource<ObservationV3Submission> Submission = new TaskCompletionSource<ObservationV3Submission>();
            internal readonly Queue<TaskCompletionSource<ObservationV3Envelope>> Inputs = new Queue<TaskCompletionSource<ObservationV3Envelope>>();
            internal readonly List<string> Stores = new List<string>(), Sends = new List<string>();
            internal TaskCompletionSource<ObservationRef> BlockedStore;
            internal TaskCompletionSource<bool> LifecycleValidation;
            internal bool Exit, Accepted, Closed; internal long Clock; internal Exception Error;
            public long Now => Clock;
            public Task<ObservationV3Submission> Submit(CancellationToken t) => Submission.Task;
            public Task<ObservationV3RuntimeHello> Hello(CancellationToken t) => Task.FromResult(Doc(new ObservationV3RuntimeHello { ContextRef = Ref("context"), Self = Self(), Tokens = new string[0], EffectiveArgumentsHash = new string('a', 64), WorkingDirectory = @"D:\" }, "hello"));
            public Task ValidateHello(ObservationV3RuntimeHello h, CancellationToken t) => Task.CompletedTask;
            public Task<ObservationRef> Store(string n, object v, CancellationToken t)
            { lock (Stores) Stores.Add(n); return n.StartsWith("report-") && BlockedStore != null ? BlockedStore.Task : Task.FromResult(Ref(n)); }
            public Task Accept(ObservationV3RuntimeHello h, CancellationToken t) { Accepted = true; return Task.CompletedTask; }
            public Task<ObservationV3Envelope> Read(CancellationToken t)
            { var value = new TaskCompletionSource<ObservationV3Envelope>(); lock (Inputs) Inputs.Enqueue(value); return value.Task; }
            public Task Send(string kind, object body, CancellationToken t) { lock (Sends) Sends.Add(kind); return Task.CompletedTask; }
            public Task ValidatePins(CancellationToken t) => Task.CompletedTask;
            public void BindAdmission(ObservationV3Admission a, Action<Exception> fail) { }
            public Task ValidateExitCompleted(ObservationV3Envelope f, CancellationToken t) => LifecycleValidation == null ? Task.CompletedTask : LifecycleValidation.Task;
            public bool ChildExited() => Exit;
            public void Close() { Closed = true; }
            public void TerminalFailure(Exception e) { Error = e; }
            internal bool HasInput { get { lock (Inputs) return Inputs.Count > 0; } }
            internal void Deliver(string kind, object body, long seq)
            {
                TaskCompletionSource<ObservationV3Envelope> reader; lock (Inputs) reader = Inputs.Dequeue();
                var e = Doc(new ObservationV3Envelope { Body = ObservationV3Wire.Serialize(body), ContextRef = Ref("context"), ReceiptRef = Ref("receipt"), Role = "ReplacementObserver", Self = Self() }, kind);
                e.Sequence = seq; reader.SetResult(e);
            }
        }
        private static void Pump(ObservationV3EventQueue q, Func<bool> done)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!done() && watch.ElapsedMilliseconds < 3000) q.DrainOne(5);
            Assert.IsTrue(done(), "completion queue did not reach expected boundary");
        }
        [Test]
        public void ReceiptCannotOvertakePendingSubmissionOrItsEvidence()
        {
            using (var q = new ObservationV3EventQueue())
            {
                var p = new Ports(); using (var h = new ObservationV3RuntimeHandoff(p, q, Ledger(false)))
                {
                    h.Start(); for (int i = 0; i < 10; i++) q.DrainOne(5);
                    Assert.IsFalse(p.Accepted); p.Submission.SetException(new IOException("submission failed"));
                    Pump(q, () => h.Closed); Assert.IsFalse(p.Accepted); Assert.IsFalse(p.Stores.Contains("terminal.json"));
                }
            }
        }
        [Test]
        public void ExitWaitsForPendingReportStoreAndAck()
        {
            using (var q = new ObservationV3EventQueue())
            {
                var p = new Ports(); using (var h = new ObservationV3RuntimeHandoff(p, q, Ledger(false)))
                {
                    h.Start(); p.Submission.SetResult(Doc(new ObservationV3Submission { ContextRef = Ref("context"), Launcher = Self(), Disposition = "returned" }, "submission"));
                    Pump(q, () => p.HasInput);
                    p.BlockedStore = new TaskCompletionSource<ObservationRef>(); p.Deliver("visibility-report", Visibility("opened"), 1);
                    Pump(q, () => p.HasInput && p.Stores.Any(n => n.StartsWith("report-")));
                    p.Deliver("exit-request", new ObservationV3ExitRequest { Boundary = 1, Checkpoint = new ObservationV3Checkpoint { State = "None", Sequence = 0, ReportRef = null } }, 2);
                    Pump(q, () => h.Stage == ObservationV3RuntimeStage.ExitPendingReports);
                    Assert.IsFalse(p.Sends.Contains("exit-authorized"));
                    p.BlockedStore.SetResult(Ref("visibility"));
                    Pump(q, () => h.Stage == ObservationV3RuntimeStage.ExitAuthorized);
                    CollectionAssert.AreEqual(new[] { "report-stored", "exit-authorized" }, p.Sends);
                    Pump(q, () => p.HasInput);
                    p.Deliver("exit-completed", new ObservationV3ExitCompleted { Boundary = 1, Checkpoint = new ObservationV3Checkpoint { State = "Stored", Sequence = 1, ReportRef = Ref("visibility") }, LifecycleRef = Ref("lifecycle") }, 3);
                    Pump(q, () => p.HasInput); lock (p.Inputs) p.Inputs.Dequeue().SetResult(null);
                    p.Exit = true; h.Tick(); Pump(q, () => h.Closed); Assert.AreEqual(ObservationV3RuntimeStage.Completed, h.Stage);
                }
            }
        }
        [TestCase("missing")][TestCase("duplicate")][TestCase("validation-failure")][TestCase("validation-late")][TestCase("hang")][TestCase("normal")]
        public void CompletionJoinsFinalFrameValidationEofAndActualExit(string mode)
        {
            using (var q = new ObservationV3EventQueue())
            {
                var p = new Ports(); using (var h = new ObservationV3RuntimeHandoff(p, q, Ledger(false)))
                {
                    h.Start(); p.Submission.SetResult(Doc(new ObservationV3Submission { Disposition = "returned" }, "submission"));
                    Pump(q, () => p.HasInput);
                    var checkpoint = new ObservationV3Checkpoint { State = "None", Sequence = 0, ReportRef = null };
                    p.Deliver("exit-request", new ObservationV3ExitRequest { Boundary = 0, Checkpoint = checkpoint }, 1);
                    Pump(q, () => p.HasInput && h.Stage == ObservationV3RuntimeStage.ExitAuthorized);
                    p.Exit = mode != "hang"; h.Tick();
                    if (mode == "hang") { p.Clock = 30000; Pump(q, () => h.Closed); }
                    else if (mode == "missing") { lock (p.Inputs) p.Inputs.Dequeue().SetResult(null); Pump(q, () => h.Closed); }
                    else
                    {
                        p.LifecycleValidation = new TaskCompletionSource<bool>();
                        var done = new ObservationV3ExitCompleted { Boundary = 0, Checkpoint = checkpoint, LifecycleRef = Ref("lifecycle") };
                        p.Deliver("exit-completed", done, 2); Pump(q, () => p.HasInput);
                        for (int n = 0; n < 5; n++) q.DrainOne(5);
                        Assert.IsFalse(h.Closed); Assert.IsFalse(p.Stores.Contains("terminal.json"));
                        if (mode == "duplicate") p.Deliver("exit-completed", done, 3);
                        else lock (p.Inputs) p.Inputs.Dequeue().SetResult(null);
                        if (mode == "validation-failure") p.LifecycleValidation.SetException(new IOException("cleanup"));
                        else if (mode == "validation-late") { p.Clock = 30000; Pump(q, () => h.Closed); p.LifecycleValidation.SetResult(true); }
                        else p.LifecycleValidation.SetResult(true);
                        Pump(q, () => h.Closed);
                    }
                    Pump(q, () => mode == "normal" ? h.Stage == ObservationV3RuntimeStage.Completed : h.Stage == ObservationV3RuntimeStage.Uncertain);
                    Assert.AreEqual(mode == "normal", p.Stores.Contains("terminal.json"));
                }
            }
        }
        [Test]
        public void ClosedAdmissionCannotWinCompletionWhileFailureCallbackWaits()
        {
            using (var q = new ObservationV3EventQueue())
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var operations = new ObservationV3Operations(q, () => 0, e => { entered.Set(); release.Wait(); }))
            {
                var failing = Task.Run(() => operations.Fail(new IOException("first")));
                try { Assert.IsTrue(entered.Wait(3000)); Assert.IsFalse(operations.Complete()); Assert.IsTrue(operations.Admission.Closed); }
                finally { release.Set(); failing.Wait(); }
            }
        }
        [Test]
        public void DeadlineClosesAdmissionWhileQueryOrSubmissionIsBlocked()
        {
            using (var q = new ObservationV3EventQueue())
            {
                var p = new Ports(); using (var h = new ObservationV3RuntimeHandoff(p, q, Ledger(false)))
                {
                    h.Start(); p.Clock = 30000; Pump(q, () => h.Closed); Assert.IsTrue(p.Closed);
                    p.Submission.TrySetResult(Doc(new ObservationV3Submission { Disposition = "returned" }, "submission"));
                    for (int i = 0; i < 10; i++) q.DrainOne(5); Assert.IsFalse(p.Accepted);
                }
            }
        }
    }
}
