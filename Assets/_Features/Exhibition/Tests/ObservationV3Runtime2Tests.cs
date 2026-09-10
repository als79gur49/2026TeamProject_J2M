using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class ObservationV3Runtime2Tests
    {
        [TestCase(29999, true)][TestCase(30000, false)][TestCase(30001, false)][TestCase(-1, false)]
        public void IndependentWindowUsesCommonClockAndStrictDeadline(long time, bool allowed)
        {
            var context = new ObservationV3IndependentContext { PreparedTimestamp = 0, DeadlineTimestamp = 30000, TimestampFrequency = 1000 };
            if (allowed) ObservationV3LaunchWindow.RequireOpen(context, time, 1000);
            else Assert.Throws<IOException>(() => ObservationV3LaunchWindow.RequireOpen(context, time, 1000));
            Assert.Throws<IOException>(() => ObservationV3LaunchWindow.RequireOpen(context, 0, 2000));
        }
        [Test] public void Runtime3ContextRejectsOldRevisionAndRemovedEndpoint()
        {
            var context = ObservationV3RuntimeWire.Stamp(new ObservationV3IndependentContext {
                RequestRef = Ref(), ClientRef = Ref(), LaunchPlanRef = Ref(), Nonce = Guid.NewGuid().ToString("N"),
                Role = "ReplacementObserver", Owner = "SteamDelegated", PreparedTimestamp = 0, DeadlineTimestamp = 30000, TimestampFrequency = 1000
            }, "independent-context", "Helper", 1, Guid.NewGuid().ToString("N"));
            var json = ObservationV3Wire.Serialize(context);
            ObservationV3RuntimeWire.Parse<ObservationV3IndependentContext>(Encoding.UTF8.GetBytes(json));
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3IndependentContext>(Encoding.UTF8.GetBytes(json.Replace(ObservationV3RuntimeWire.Revision, "observation-v3-runtime-2"))));
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3IndependentContext>(Encoding.UTF8.GetBytes(json.Insert(1, "\"Endpoint\":\"old-pipe\","))));
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3IndependentContext>(Encoding.UTF8.GetBytes(json.Replace("\"DeadlineTimestamp\":30000,", ""))));
        }
        [Test] public void ReplacementClaimUsesActualCreateOnlyStorage()
        {
            string root = Path.Combine(@"D:\J2M\evidence", "overlay-v3-claim-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var value = ObservationV3RuntimeWire.Stamp(new ObservationV3ReplacementClaim { ContextRef = Ref(), Self = new ProcessIdentity { Pid = 1, StartTicks = 1, Path = "fake", Sha256 = new string('a',64), UserSid = "fake", Logon = "fake" }, Client = new ProcessIdentity { Pid = 2, StartTicks = 2, Path = "fake", Sha256 = new string('a',64), UserSid = "fake", Logon = "fake" },
                    EffectiveArgumentsHash = new string('a', 64), WorkingDirectory = root, Tokens = new[] { "fake" }
                }, "replacement-claim", "ReplacementObserver", 1, Guid.NewGuid().ToString("N"));
                var reference = ObservationV3RuntimeWire.Create(root, "replacement-claim.json", value);
                var original = File.ReadAllBytes(reference.Path);
                Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Create(root, "replacement-claim.json", value));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(reference.Path));
            }
            finally { Directory.Delete(root, true); }
        }
        // The same admission executor and Session as production, with explicit main/worker queues.
        private sealed class Scheduler
        {
            public long Now;
            public readonly Queue<Action> Main = new Queue<Action>(), Worker = new Queue<Action>();
            public void RunMain() { Main.Dequeue()(); }
            public void RunWorker() { Worker.Dequeue()(); }
        }
        private static ObservationRef Ref(string name = "report") => new ObservationRef { Path = "D:\\J2M\\evidence\\fake\\" + name, Sha256 = new string('a', 64) };
        private static ObservationV3Session Ready(Scheduler time)
        { var s = new ObservationV3Session(() => time.Now, false); s.Ready(); s.Observing(); return s; }

        [TestCase(false)][TestCase(true)]
        public void LastSlowQueryAndCancellationHaveOneInvocationBoundary(bool commitFirst)
        {
            var time = new Scheduler(); var gate = new ObservationV3Admission(() => time.Now); int created = 0;
            var claim = gate.Prepare("steam-start", 60000);
            if (commitFirst)
            {
                Assert.IsTrue(gate.TryCommitInvocation(claim)); gate.Close();
                gate.InvokeCommitted(claim, () => ++created);
                Assert.IsTrue(claim.InvocationCommitted); Assert.IsTrue(claim.Returned);
                Assert.Throws<OperationCanceledException>(() => gate.RequireCurrent(claim.Generation, claim.Deadline));
            }
            else
            {
                Assert.Throws<OperationCanceledException>(() => gate.Execute(claim, () => { time.Now = 60000; gate.Close(); }, () => ++created));
                Assert.IsFalse(claim.InvocationCommitted);
            }
            Assert.AreEqual(commitFirst ? 1 : 0, created);
            Assert.IsFalse(gate.TryCommitInvocation(claim));
        }
        [Test]
        public void DeadlineEqualityAndLateReturnNeverAuthorizeSuccess()
        {
            var time = new Scheduler(); var gate = new ObservationV3Admission(() => time.Now);
            var claim = gate.Prepare("launch", 30000); time.Now = 30000;
            Assert.IsFalse(gate.TryCommitInvocation(claim));
            time.Now = 0; var other = gate.Prepare("helper", 30000);
            gate.Execute(other, () => { }, () => { time.Now = 30001; return true; });
            Assert.IsTrue(other.Returned); Assert.Throws<TimeoutException>(() => gate.RequireCurrent(other.Generation, other.Deadline));
        }
        [TestCase(29000, true)][TestCase(30000, false)][TestCase(31000, false)]
        public void ReportBudgetIncludesQueueAndAckValidation(long completed, bool success)
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                var operation = session.AdmitReport(Ref().Sha256);
                time.Worker.Enqueue(() => time.Main.Enqueue(() =>
                {
                    if (success) session.AcceptReport(operation, Ref());
                    else { session.CheckDeadlines(); Assert.Throws<IOException>(() => session.FindReport(operation.Sequence, operation.Hash)); }
                }));
                time.Now = completed; time.RunWorker(); time.RunMain();
                Assert.AreEqual(success ? 1 : 0, session.Checkpoint.Sequence);
                Assert.AreEqual(0, session.PendingCount); Assert.IsTrue(operation.Task.IsCompleted);
                Assert.AreEqual(success, operation.Task.Status == TaskStatus.RanToCompletion);
            }
        }
        [Test]
        public void AckArrivesBeforeDeadlineButFileCheckReturnsLate()
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                var operation = session.AdmitReport(Ref().Sha256); time.Now = 29000;
                var found = session.FindReport(operation.Sequence, operation.Hash);
                time.Worker.Enqueue(() => time.Main.Enqueue(() => Assert.Throws<IOException>(() => session.AcceptReport(found, Ref()))));
                time.Now = 31000; session.CheckDeadlines(); time.RunWorker(); time.RunMain();
                Assert.AreEqual(0, session.Checkpoint.Sequence); Assert.IsTrue(operation.Task.IsFaulted);
                Assert.Throws<OperationCanceledException>(() => session.Admission.Prepare("exit-completed", 60000));
            }
        }
        [TestCase("RunCallbacks")][TestCase("AccountChanged")][TestCase("LoggedOff")][TestCase("GetterException")]
        public void NativeFaultClosesReportAdmissionAndKeepsFirstError(string fault)
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                int cleanup = 0, reports = 0; session.Failed += e => cleanup++;
                var error = new IOException(fault);
                Assert.Throws<IOException>(() => session.CheckNative(() => { throw error; }));
                Assert.Throws<OperationCanceledException>(() => { session.AdmitReport(Ref().Sha256); reports++; });
                session.TryFail(new IOException("secondary"));
                Assert.AreSame(error, session.FirstError); Assert.AreEqual(1, cleanup); Assert.AreEqual(0, reports);
            }
        }
        [Test]
        public void IntentionalShutdownDoesNotReopenNativeOrReports()
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                session.BeginShutdown(); session.VerifyShutdown();
                Assert.IsFalse(session.CanReport); Assert.AreEqual(ObservationV3NativePhase.ShutdownVerified, session.NativePhase);
                Assert.IsNull(session.FirstError);
            }
        }
        [TestCase(false)][TestCase(true)]
        public void CloseAndGrantCommitHaveOneWinnerAndShareCloseTask(bool commitFirst)
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                var grant = session.BeginHandoff(); int closeCalls = 0;
                if (commitFirst) Assert.IsTrue(session.CommitGrant(grant));
                var pending = new TaskCompletionSource<bool>();
                var first = session.RequestClose(() => { closeCalls++; return pending.Task; });
                var second = session.RequestClose(() => { closeCalls++; return Task.FromResult(true); });
                Assert.AreSame(first, second); Assert.AreEqual(1, closeCalls);
                Assert.AreEqual(commitFirst ? ObservationV3SessionStage.GrantCommitInFlight : ObservationV3SessionStage.ClosingCancelledBeforeCommit, session.Stage);
                Assert.IsFalse(session.CommitGrant(grant));
                if (commitFirst) { session.Admission.InvokeCommitted(grant, () => true); session.ConfirmGrant(grant); }
                time.Now = 29999; Assert.AreEqual(30000, session.CloseDeadline);
                pending.SetResult(true);
            }
        }
        [Test]
        public void TimeoutAndCloseCompletePendingWithoutExtendingItsDeadline()
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                var operation = session.AdmitReport(Ref().Sha256); time.Now = 29000;
                var close = session.RequestClose(() => session.DrainReports());
                Assert.AreEqual(59000, session.CloseDeadline); Assert.AreEqual(30000, operation.Deadline);
                time.Now = 30000; session.CheckDeadlines();
                Assert.IsTrue(operation.Task.IsFaulted); Assert.AreEqual(0, session.PendingCount);
                Assert.AreSame(close, session.RequestClose(() => Task.FromResult(true)));
            }
        }
        [TestCase("Pending")][TestCase("operation")][TestCase("mapping")][TestCase("account")][TestCase("appid")][TestCase("unchanged")]
        public void JournalSnapshotBindsMeaningAndDigest(string change)
        {
            var root = Path.Combine(@"D:\J2M\evidence", "overlay-v3-journal-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var path = Path.Combine(root, "exhibition-reset.json");
                var journal = new ObservationV3Journal { SchemaVersion = 1, OperationId = Guid.NewGuid().ToString("N"), State = "Ready", AppId = 5218360, SteamId = 123, MappingVersion = "level-clear-v1" };
                File.WriteAllBytes(path, ObservationV3RuntimeWire.Bytes(journal)); var snap = ObservationV3JournalReader.Read(path, 5218360, 123);
                var ready = new ObservationV3ReadySnapshot { Journal = snap.Reference, State = journal.State, OperationId = journal.OperationId, AppId = journal.AppId, SteamId = journal.SteamId, MappingVersion = journal.MappingVersion };
                var participants = new ObservationV3ParticipantSnapshot { Root = root, Files = new[] { snap.Reference } };
                if (change == "Pending") journal.State = "Pending";
                if (change == "operation") journal.OperationId = Guid.NewGuid().ToString("N");
                if (change == "mapping") journal.MappingVersion = "other";
                if (change == "account") journal.SteamId++;
                if (change == "appid") journal.AppId++;
                File.WriteAllBytes(path, ObservationV3RuntimeWire.Bytes(journal));
                if (change == "unchanged")
                {
                    ObservationV3JournalReader.RequireUnchanged(snap, 5218360, 123);
                    ObservationV3JournalReader.Validate(ready, participants, 5218360, 123);
                    participants.Files = new ObservationRef[0]; Assert.Throws<IOException>(() => ObservationV3JournalReader.Validate(ready, participants, 5218360, 123));
                    participants.Files = new[] { snap.Reference, snap.Reference }; Assert.Throws<IOException>(() => ObservationV3JournalReader.Validate(ready, participants, 5218360, 123));
                }
                else
                {
                    Assert.Throws<IOException>(() => ObservationV3JournalReader.RequireUnchanged(snap, 5218360, 123));
                    Assert.Throws<IOException>(() => ObservationV3JournalReader.Validate(ready, participants, 5218360, 123));
                }
                Assert.AreEqual("Ready", snap.Value.State); Assert.AreEqual(123, snap.Value.SteamId);
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("SchemaVersion")][TestCase("OperationId")][TestCase("State")][TestCase("AppId")][TestCase("SteamId")][TestCase("MappingVersion")]
        public void JournalMissingFieldsDoNotUseInitializers(string field)
        {
            var data = new Dictionary<string, object> { { "SchemaVersion", 1 }, { "OperationId", Guid.NewGuid().ToString("N") }, { "State", "Ready" }, { "AppId", 5218360 }, { "SteamId", 123 }, { "MappingVersion", "level-clear-v1" } };
            string json = "{" + string.Join(",", data.Where(p => p.Key != field).Select(p => "\"" + p.Key + "\":" + (p.Value is string ? "\"" + p.Value + "\"" : p.Value.ToString()))) + "}";
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3Journal>(Encoding.UTF8.GetBytes(json)));
        }
        [TestCase("Boundary")][TestCase("Checkpoint")][TestCase("LifecycleRef")]
        public void ExitCompletedRequiresEveryBodyField(string field)
        {
            string json = "{\"Boundary\":0,\"Checkpoint\":{\"State\":\"None\",\"Sequence\":0,\"ReportRef\":null},\"LifecycleRef\":{\"Path\":\"D:\\\\a\",\"Sha256\":\"a\"}}";
            var xml = new System.Xml.XmlDocument();
            using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), System.Xml.XmlDictionaryReaderQuotas.Max)) xml.Load(reader);
            xml.DocumentElement.RemoveChild(xml.DocumentElement.SelectSingleNode(field));
            using (var memory = new MemoryStream())
            {
                using (var writer = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonWriter(memory, Encoding.UTF8, false)) { xml.WriteTo(writer); writer.Flush(); }
                Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationV3ExitCompleted>(memory.ToArray()));
            }
        }
        [TestCase("unrelated-file")][TestCase("unchanged")][TestCase("Pending")][TestCase("operation")][TestCase("mapping")][TestCase("account")]
        public void ProductionBundleConsumerRejectsReplacedJournalBeforeNativeOrHelper(string change)
        {
            var root = Path.Combine(@"D:\J2M\evidence", "overlay-v3-consumer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string saves = Path.Combine(root, "saves"); Directory.CreateDirectory(saves);
            try
            {
                string run = Guid.NewGuid().ToString("N"), path = Path.Combine(saves, "exhibition-reset.json");
                var journal = new ObservationV3Journal { SchemaVersion = 1, State = "Ready", OperationId = Guid.NewGuid().ToString("N"), AppId = 5218360, SteamId = 123, MappingVersion = "level-clear-v1" };
                File.WriteAllBytes(path, ObservationV3RuntimeWire.Bytes(journal));
                var frozen = ObservationV3JournalReader.Read(path, journal.AppId, journal.SteamId);
                var ready = ObservationV3RuntimeWire.Stamp(new ObservationV3ReadySnapshot { Journal = frozen.Reference, State = journal.State, OperationId = journal.OperationId,
                    AppId = journal.AppId, SteamId = journal.SteamId, MappingVersion = journal.MappingVersion }, "ready-snapshot", "OriginObserver", 1, run);
                var participants = ObservationV3RuntimeWire.Stamp(new ObservationV3ParticipantSnapshot { Root = saves, Files = new[] { frozen.Reference } }, "participant-snapshot", "OriginObserver", 2, run);
                // Simulate the original bug too: stale meaning paired with the newly substituted file hash.
                if (change == "Pending") journal.State = "Pending";
                if (change == "operation") journal.OperationId = Guid.NewGuid().ToString("N");
                if (change == "mapping") journal.MappingVersion = "changed";
                if (change == "account") journal.SteamId++;
                File.WriteAllBytes(path, ObservationV3RuntimeWire.Bytes(journal));
                ready.Journal = new ObservationRef { Path = path, Sha256 = ExperimentFiles.Hash(path) }; participants.Files = new[] { ready.Journal };
                var readyRef = ObservationV3RuntimeWire.Create(root, "ready.json", ready);
                var participantRef = ObservationV3RuntimeWire.Create(root, "participants.json", participants);
                int native = 0, helper = 0;
                Action consumer = () => { ObservationV3Bundle.ValidateReadyPins(readyRef, participantRef, root, run, 5218360, 123); native++; helper++; };
                bool unchanged = change == "unchanged" || change == "unrelated-file";
                if (change == "unrelated-file") File.WriteAllText(Path.Combine(saves, "unrelated.txt"), "not observation input");
                if (unchanged) consumer(); else Assert.Throws<IOException>(() => consumer());
                Assert.AreEqual(unchanged ? 1 : 0, native); Assert.AreEqual(native, helper);
                if (!unchanged) Assert.Throws<IOException>(() => ObservationV3JournalReader.RequireUnchanged(frozen, 5218360, 123));
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("normal")][TestCase("run")][TestCase("owner")][TestCase("context")][TestCase("receipt")][TestCase("self")][TestCase("init")][TestCase("count")][TestCase("return")][TestCase("failure")][TestCase("order")][TestCase("hash")]
        public void LifecycleBytesAndTypedOwnershipGateCompletion(string change)
        {
            var root = Path.Combine(@"D:\J2M\evidence", "overlay-v3-lifecycle-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string run = Guid.NewGuid().ToString("N");
                var self = new ProcessIdentity { Pid = 1, StartTicks = 2, Session = 1, UserSid = "S-1-5-21-1", Logon = "test", Path = @"D:\fake.exe", Sha256 = Ref().Sha256 };
                var frame = ObservationV3RuntimeWire.Stamp(new ObservationV3Envelope { Self = self, ContextRef = Ref("context"), ReceiptRef = Ref("receipt"), Role = "ReplacementObserver", Body = "{}" }, "exit-completed", "ReplacementObserver", 2, run);
                var value = ObservationV3RuntimeWire.Stamp(new ObservationV3Lifecycle { ContextRef = frame.ContextRef, ReceiptRef = frame.ReceiptRef, Self = self,
                    RuntimePresent = true, InitializationAttempted = true, InitializationSucceeded = true, ShutdownCallCount = 1, ShutdownReturned = true,
                    RuntimeState = "Shutdown", StartupState = "Stopped", Failure = "None", ShutdownStarted = 10, ShutdownFinished = 11, IdentityObservationCount = 1, LastIdentityObservation = 9 }, "native-lifecycle", "ReplacementObserver", 1, run);
                if (change == "run") value.RunId = Guid.NewGuid().ToString("N");
                if (change == "owner") value.Author = "OriginObserver";
                if (change == "context") value.ContextRef = Ref("other");
                if (change == "receipt") value.ReceiptRef = Ref("other");
                if (change == "self") value.Self = new ProcessIdentity { Pid = 20 };
                if (change == "init") value.InitializationSucceeded = false;
                if (change == "count") value.ShutdownCallCount = 2;
                if (change == "return") value.ShutdownReturned = false;
                if (change == "failure") value.Failure = "ShutdownException";
                if (change == "order") value.ShutdownFinished = 8;
                var reference = ObservationV3RuntimeWire.Create(root, "lifecycle.json", value);
                if (change == "hash") File.AppendAllText(reference.Path, " ");
                Action validate = () => ObservationV3RuntimeWire.ValidateLifecycle(ObservationV3RuntimeWire.Read<ObservationV3Lifecycle>(reference, root).Value, frame);
                if (change == "normal") validate(); else Assert.Throws<IOException>(() => validate());
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("old")][TestCase("missing")][TestCase("duplicate")][TestCase("extra")]
        public void OuterFrameRequiresStrictCurrentRevision(string change)
        {
            string revision = change == "old" ? "observation-v3-runtime-1" : ObservationV3RuntimeWire.Revision;
            string json = "{\"Kind\":\"hello\",\"Body\":\"{}\"";
            if (change != "missing") json += ",\"ProtocolRevision\":\"" + revision + "\"";
            if (change == "duplicate") json += ",\"ProtocolRevision\":\"" + revision + "\"";
            if (change == "extra") json += ",\"Other\":\"value\"";
            json += "}";
            Assert.Throws<IOException>(() => ObservationV3Pipe.ParseRuntimeFrame(Encoding.UTF8.GetBytes(json)));
        }
        [Test]
        public void AbandonedIdentityQueryCannotBlockHandleCollectionDisposal()
        {
            using (var entered = new System.Threading.ManualResetEventSlim())
            using (var release = new System.Threading.ManualResetEventSlim())
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                var handles = new ObservationV3ProcessHandles(p =>
                {
                    entered.Set(); release.Wait();
                    return new ProcessIdentity { Pid = p.Id, StartTicks = 1, Session = 1, UserSid = "sid", Logon = "logon", Path = "fake", Sha256 = Ref().Sha256 };
                });
                var query = Task.Run(() => { try { handles.Capture(process.Id); return null; } catch (Exception error) { return error; } });
                try
                {
                    Assert.IsTrue(entered.Wait(3000));
                    var dispose = Task.Run(() => handles.Dispose()); Assert.IsTrue(dispose.Wait(3000));
                }
                finally { release.Set(); }
                Assert.IsTrue(query.Wait(3000)); Assert.IsInstanceOf<ObjectDisposedException>(query.Result);
            }
        }
        [TestCase(false)][TestCase(true)]
        public void FailureOrDeadlineInQueuedQuitGapMustJoinEvidence(bool deadline)
        {
            var time = new Scheduler(); using (var session = Ready(time))
            {
                var hold = new TaskCompletionSource<bool>(); session.RequestClose(() => hold.Task);
                Assert.IsNull(session.FirstError);
                time.Main.Enqueue(() => Assert.IsFalse(session.TryAllowQuit(false)));
                if (deadline) time.Now = 30000; else session.TryFail(new IOException("queued-fault"));
                time.RunMain(); Assert.IsNotNull(session.FirstError);
                Assert.IsFalse(session.TryAllowQuit(false)); Assert.IsTrue(session.TryAllowQuit(true));
                Assert.AreEqual(ObservationV3SessionStage.QuitAllowed, session.Stage); hold.SetResult(true);
            }
        }
        [Test]
        public void RuntimeOneDocumentsAreRejected()
        {
            var doc = ObservationV3RuntimeWire.Stamp(new ObservationRunDocument(), "test", "OriginObserver", 1, Guid.NewGuid().ToString("N"));
            doc.ProtocolRevision = "observation-v3-runtime-1";
            Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Parse<ObservationRunDocument>(ObservationV3RuntimeWire.Bytes(doc)));
        }
    }
}
