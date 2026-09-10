using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class ObservationV3Tests
    {
        public const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public static ProcessIdentity Identity(int pid, string file = "game.exe", long ticks = 1) => new ProcessIdentity {
            Pid = pid, Path = Path.GetFullPath(file), StartTicks = ticks, Session = 1, UserSid = "S-1-5-21-1", Logon = "logon", Sha256 = Hash };
        public sealed class Fixture : IObservationV3Environment, IObservationLaunch, IObservationV3Peer
        {
            public ObservationV3Request Request;
            public ObservationV3Client Client;
            public ObservationV3Context Context;
            public ProcessIdentity Child = Identity(4, "game.exe", 100);
            public Dictionary<int, ProcessIdentity> Processes = new Dictionary<int, ProcessIdentity>();
            public Dictionary<string, object> Evidence = new Dictionary<string, object>();
            public long Now;
            public int Launches, Closes;
            public string Fault, WriteFault, PeerFault, AckFault;
            public bool Exited, Duplicate, BlockLaunch, BlockChallenge, BlockAck, CancelOnClaim;
            public Action OnClaim;
            public Action<string> OnEvidence;
            public ObservationV3Handoff Handoff;
            public Fixture()
            {
                Request = new ObservationV3Request { RunId = Guid.NewGuid().ToString("N"), Nonce = Guid.NewGuid().ToString("N"),
                    OperationId = Guid.NewGuid().ToString("N"), Directory = Path.GetFullPath("v3-evidence"), ConfigurationHash = Hash,
                    ManifestHash = Hash, ParticipantHash = Hash, OriginVisibilityHash = Hash, OriginDisplayHash = Hash, BaselineHash = Hash,
                    Targets = new[] { "VQ_LEVEL_0_CLEAR" }, MappingVersion = "v1", ReadyState = "Ready", AppId = 5218360, SteamId = 123, Owner = ObservationLaunchOwner.SteamDelegated,
                    Origin = Identity(1), OriginalSteam = Identity(2, "steam.exe") };
                Client = new ObservationV3Client { Creation = new ObservationV3HelperCreation { RequestHash = ObservationV3Wire.Digest(Request), Origin = Request.Origin, Helper = Identity(3, "helper.exe") }, RequestHash = ObservationV3Wire.Digest(Request), Original = Request.OriginalSteam,
                    Current = Identity(5, "steam.exe", 2), Probe = Identity(6, "probe.exe", 3), OriginalExited = true, ShutdownCommandExited = true,
                    ProbeShutdownReturned = true, ProbeExited = true, LoggedOn = true, AppId = Request.AppId, SteamId = Request.SteamId };
                Context = new ObservationV3Context { RequestHash = ObservationV3Wire.Digest(Request), ClientHash = ObservationV3Wire.Digest(Client),
                    RunId = Request.RunId, Nonce = Request.Nonce, Owner = Request.Owner, Endpoint = "j2m-observation-v3-" + Request.Nonce };
                foreach (var p in new[] { Request.Origin, Client.Helper, Request.OriginalSteam, Client.Current, Child }) Processes.Add(p.Pid, ObservationV3Wire.Copy(p));
            }
            public ObservationV3Handoff Make() => Handoff = new ObservationV3Handoff(Request, Client, Context, this, this);
            public ObservationV3Hello Hello() => new ObservationV3Hello { RunId = Request.RunId, Nonce = Request.Nonce,
                RequestHash = ObservationV3Wire.Digest(Request), ContextHash = ObservationV3Wire.Digest(Context), Child = Child, EffectiveArgumentsHash = Hash };
            public long Milliseconds => Now;
            public long UtcTicks => 99;
            public bool HasExited(ProcessIdentity identity) => Exited && ObservationV3Wire.Same(identity, Child);
            public ProcessIdentity Capture(int pid) { if (Fault == "capture") throw new IOException("CaptureUnavailable"); return Processes[pid]; }
            public void ValidateReadOnlyPins() { if (Fault == "pins") throw new IOException("PinsChanged"); }
            public void EnsureOnlyChild(ProcessIdentity p) { if (Duplicate) throw new IOException("DuplicateChild"); }
            public string VerifyEffectiveArguments(ProcessIdentity p, ObservationV3Context c) { if (Fault == "args") throw new IOException("InvalidEffectiveArguments"); return Hash; }
            public string CreateEvidence(string name, object value)
            {
                if (WriteFault == name) throw new IOException("WriteFailure:" + name);
                Evidence.Add(name, ObservationV3Wire.Serialize(value));
                OnEvidence?.Invoke(name);
                if (name == "submission-attempt.json") { if (CancelOnClaim) Handoff.Cancel(); OnClaim?.Invoke(); }
                return ObservationV3Wire.Digest(value);
            }
            public void CloseAdmission() { Closes++; }
            // These pure fake tests must not capture the Unity editor synchronization context.
            public async Task DelayAsync() { await Task.Delay(10).ConfigureAwait(false); Now += 1000; }
            public void RequireAvailable(ObservationLaunchOwner o) { if (Fault == "unavailable") throw new IOException("LaunchTransportUnavailable"); }
            public Task<ObservationSubmission> SubmitAsync(ObservationV3Context c, CancellationToken token)
            {
                Launches++; if (Fault == "submit") throw new IOException("SubmissionUncertain");
                return BlockLaunch ? new TaskCompletionSource<ObservationSubmission>().Task : Task.FromResult(new ObservationSubmission { Disposition = "returned", Launcher = Client.Helper });
            }
            public Task<bool> ConfirmAsync(string receiptHash, CancellationToken cancellation) => Task.FromResult(true);
            public void WatchLease(Action lost) { }
            public ProcessIdentity CapturePeer() => PeerFault == "launcher" ? Client.Helper : PeerFault == "wrong" ? Identity(4, "wrong.exe", 100) : Child;
            public Task<string> ChallengeAsync(string challenge, CancellationToken token) => BlockChallenge ? new TaskCompletionSource<string>().Task : Task.FromResult(PeerFault == "challenge" ? "wrong" : challenge);
            public Task<ObservationV3Ack> ReceiptAsync(ObservationV3Receipt r, CancellationToken token)
            {
                if (BlockAck) return new TaskCompletionSource<ObservationV3Ack>().Task;
                var ack = new ObservationV3Ack { ReceiptHash = ObservationV3Wire.Digest(r), Challenge = r.Challenge, Child = Child, Helper = Client.Helper,
                    NewClient = Client.Current, AppId = Request.AppId, SteamId = Request.SteamId, NativeReady = true, LoggedOn = true };
                if (AckFault == "hash") ack.ReceiptHash = Hash;
                if (AckFault == "account") ack.SteamId++;
                if (AckFault == "ready") ack.NativeReady = false;
                if (AckFault == "client") ack.NewClient = Request.OriginalSteam;
                if (AckFault == "helper") ack.Helper = Child;
                if (AckFault == "child") ack.Child = Request.Origin;
                return Task.FromResult(ack);
            }
        }
        [Test, Category("Integration")]
        public async Task BothLaunchOwnersAcceptOnlyVerifiedChildAndSeparateExitEvidence([Values] ObservationLaunchOwner owner)
        {
            var f = new Fixture(); f.Request.Owner = owner; f.Client.RequestHash = ObservationV3Wire.Digest(f.Request); f.Client.Creation.RequestHash = f.Client.RequestHash;
            f.Context.RequestHash = f.Client.RequestHash; f.Context.ClientHash = ObservationV3Wire.Digest(f.Client); f.Context.Owner = owner;
            using (var h = f.Make())
            {
                await h.SubmitAsync().ConfigureAwait(false); await h.SubmitAsync().ConfigureAwait(false); Assert.That(f.Launches, Is.EqualTo(1));
                Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Submitted));
                await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); Assert.That(h.FirstError, Is.Null); Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Observing));
                Assert.That(h.PurposeAchieved, Is.False); Assert.That(h.HelperExit, Is.EqualTo("unknown")); Assert.That(h.SteamTracking, Is.EqualTo("unknown"));
                h.RequestChildExit(); Assert.That(h.ChildExitConfirmed, Is.False);
                f.Exited = true; h.LeaseLost(); h.Tick(); Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Completed));
            }
        }
        [TestCase("unavailable", 0), TestCase("capture", 0), TestCase("pins", 0), TestCase("submit", 1), Category("Integration")]
        public async Task FailuresNeverRetryOrFallback(string fault, int launches)
        { var f = new Fixture { Fault = fault }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.SubmitAsync().ConfigureAwait(false); Assert.That(f.Launches, Is.EqualTo(launches)); Assert.That(h.FirstError, Is.Not.Null); Assert.That(f.Closes, Is.EqualTo(1)); } }
        [TestCase("submission-attempt.json", 0), TestCase("submission-result.json", 1), Category("Integration")]
        public async Task StorageFailurePreservesSubmissionBoundary(string name, int count)
        { var f = new Fixture { WriteFault = name }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); Assert.That(f.Launches, Is.EqualTo(count)); Assert.That(h.FirstError, Does.Contain("WriteFailure")); } }
        [Test, Category("Integration")]
        public async Task CancellationAfterClaimPreventsSubmissionEvenWhenTerminalWriteFails()
        { var f = new Fixture { CancelOnClaim = true, WriteFault = "terminal.json" }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); Assert.That(f.Launches, Is.Zero); Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Cancelled)); Assert.That(h.FirstError, Is.EqualTo("ObservationCancelled")); } }
        [TestCase("launch"), TestCase("challenge"), TestCase("ack"), Category("Integration")]
        public async Task HungOperationsAreBoundedAndLateChildrenCannotReopen(string which)
        { var f = new Fixture { BlockLaunch = which == "launch", BlockChallenge = which == "challenge", BlockAck = which == "ack" }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); var error = h.FirstError; Assert.That(error, Does.Contain("Timeout")); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); await h.SubmitAsync().ConfigureAwait(false); Assert.That(h.FirstError, Is.EqualTo(error)); Assert.That(f.Launches, Is.EqualTo(1)); } }
        [TestCase(29999, true), TestCase(30000, false), TestCase(30001, false), Category("Integration")]
        public async Task DeadlineEqualityRejectsChild(long now, bool accepted)
        { var f = new Fixture(); using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); f.Now = now; await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); Assert.That(h.Stage == ObservationV3Stage.Observing, Is.EqualTo(accepted)); } }
        [TestCase("launcher"), TestCase("wrong"), TestCase("challenge"), Category("Integration")]
        public async Task PeerOrChallengeMismatchRejectsChild(string fault)
        { var f = new Fixture { PeerFault = fault }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Failed)); } }
        [TestCase("hash"), TestCase("account"), TestCase("ready"), TestCase("client"), TestCase("helper"), TestCase("child"), Category("Integration")]
        public async Task AckMustBindNativeAndProcessIdentity(string fault)
        { var f = new Fixture { AckFault = fault }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); Assert.That(h.FirstError, Is.EqualTo("ChildAckMismatch")); } }
        [TestCase("run"), TestCase("nonce"), TestCase("request"), TestCase("context"), TestCase("args"), TestCase("version"), Category("Integration")]
        public async Task HelloMustMatchExactRequest(string fault)
        {
            var f = new Fixture(); var hello = f.Hello();
            switch (fault) { case "run": hello.RunId = Guid.NewGuid().ToString("N"); break; case "nonce": hello.Nonce = Guid.NewGuid().ToString("N"); break;
                case "request": hello.RequestHash = Hash; break; case "context": hello.ContextHash = Hash; break; case "args": hello.EffectiveArgumentsHash = new string('b', 64); break; case "version": hello.Version = 2; break; }
            using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, hello).ConfigureAwait(false); Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Failed)); }
        }
        [TestCase("old"), TestCase("pid"), TestCase("path"), TestCase("hash"), TestCase("scope"), TestCase("probe"), TestCase("shutdown"), Category("Integration")]
        public void ClientEvidenceRejectsOldOrUnverifiedClient(string fault)
        {
            var f = new Fixture(); switch (fault) { case "old": f.Client.Current = f.Request.OriginalSteam; break; case "pid": f.Client.Current.StartTicks = 1; break;
                case "path": f.Client.Current.Path += ".other"; break; case "hash": f.Client.Current.Sha256 = new string('b', 64); break;
                case "scope": f.Client.Current.Logon = "other"; break; case "probe": f.Client.ProbeExited = false; break; case "shutdown": f.Client.ShutdownCommandExited = false; break; }
            Assert.Throws<IOException>(() => ObservationV3Wire.ValidateClient(f.Request, f.Client));
        }
        [TestCase("child-receipt.json"), TestCase("child-ack.json"), Category("Integration")]
        public async Task ReceiptOrAckWriteFailureNeverAccepts(string name)
        { var f = new Fixture { WriteFault = name }; using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); Assert.That(h.FirstError, Does.Contain("WriteFailure")); } }
        [TestCase(false), TestCase(true), Category("Integration")]
        public async Task DuplicateBeforeOrAfterAcceptanceInvalidatesRun(bool after)
        { var f = new Fixture(); using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); if (after) await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); f.Duplicate = true; h.Tick(); Assert.That(h.FirstError, Is.EqualTo("DuplicateChild")); Assert.That(h.PurposeAchieved, Is.False); } }
        [Test, Category("Integration")]
        public async Task SecondPeerAndLeaseLossNeverRestoreFirstAcceptance()
        { var f = new Fixture(); using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); h.LeaseLost(); Assert.That(h.FirstError, Is.EqualTo("DuplicateChild")); Assert.That(h.PurposeAchieved, Is.False); } }
        [Test, Category("Integration")]
        public async Task RequestIsSnapshotAndExternalPidReuseFails()
        { var f = new Fixture(); using (var h = f.Make()) { f.Request.SteamId++; await h.SubmitAsync().ConfigureAwait(false); Assert.That(f.Launches, Is.EqualTo(1)); f.Processes[5].StartTicks++; h.Tick(); Assert.That(h.FirstError, Is.EqualTo("NewClientIdentityChanged")); } }
        [Test, Category("Integration")]
        public async Task ExitTimeoutLeavesTerminationUnknown()
        { var f = new Fixture(); using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); h.RequestChildExit(); f.Now = 30000; h.Tick(); Assert.That(h.ChildExitConfirmed, Is.False); Assert.That(h.FirstError, Is.EqualTo("ChildExitUnknown")); } }
        [TestCase("child-exited.json"), TestCase("terminal.json"), Category("Integration")]
        public async Task CancellationDuringCompletionCannotBeOverwritten(string evidence)
        {
            var f = new Fixture();
            using (var h = f.Make())
            {
                await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); h.RequestChildExit(); f.Exited = true;
                f.OnEvidence = name => { if (name == evidence) h.Cancel(); };
                h.Tick(); h.Tick();
                Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Cancelled));
                Assert.That(h.FirstError, Is.EqualTo("ObservationCancelled"));
                Assert.That(h.PurposeAchieved, Is.False);
                string failureFile = evidence == "terminal.json" ? "terminal-failure.json" : "terminal.json";
                var failure = ObservationV3Wire.Parse<ObservationV3Event>((string)f.Evidence[failureFile]);
                Assert.That(failure.Stage, Is.EqualTo("Cancelled"));
                Assert.That(failure.Error, Is.EqualTo("ObservationCancelled"));
            }
        }
        [TestCase(30000, false), TestCase(30001, false), TestCase(30000, true), TestCase(30001, true), Category("Integration")]
        public async Task LateExitNeverBecomesCompleted(int elapsed, bool direct)
        {
            var f = new Fixture();
            using (var h = f.Make())
            {
                await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); h.RequestChildExit();
                f.Now += elapsed; f.Exited = true;
                if (direct) h.ConfirmChildExit(f.Child, true); else h.Tick();
                Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Uncertain));
                Assert.That(h.FirstError, Is.EqualTo("ChildExitUnknown"));
                Assert.That(h.ChildExitConfirmed, Is.False);
            }
        }
        [Test, Category("Integration")]
        public async Task MatchedOriginRolesCannotProveReplacementSuccess()
        {
            var f = new Fixture();
            using (var h = f.Make())
            {
                await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false);
                var visibility = Visibility(f); visibility.Role = OverlayObservationRole.OriginObserver;
                h.SaveDisplay(Report(f, visibility, "unearned"), visibility, f.Request.Targets);
                Assert.That(h.Stage, Is.EqualTo(ObservationV3Stage.Failed));
                Assert.That(h.FirstError, Is.EqualTo("ReplacementReportRequired"));
                Assert.That(h.PurposeAchieved, Is.False);
                Assert.That(f.Evidence.ContainsKey("display-0.json"), Is.False);
            }
        }
        public static OverlayUserObservation Visibility(Fixture f) => new OverlayUserObservation { RunId = f.Request.RunId, Role = OverlayObservationRole.ReplacementObserver, Process = f.Child, Source = "User", AttemptReported = true, Visibility = "opened" };
        public static ObservationV3DisplayReport Report(Fixture f, OverlayUserObservation v, string display) => new ObservationV3DisplayReport {
            RunId = f.Request.RunId, Role = v.Role, Process = f.Child, VisibilityHash = ObservationV3Wire.Digest(v), ReportedUtc = DateTime.UtcNow.ToString("o"),
            OriginalText = "user report", Targets = new[] { new ObservationV3TargetDisplay { Id = "VQ_LEVEL_0_CLEAR", Display = display } } };
        [TestCase("earned", false), TestCase("unearned", true), TestCase("inconclusive", false), Category("Integration")]
        public async Task DisplayDoesNotExpandTargetScopeAndLeaseLossRevokesSuccess(string display, bool success)
        { var f = new Fixture(); using (var h = f.Make()) { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); var v = Visibility(f); h.SaveDisplay(Report(f, v, display), v, new[] { "VQ_LEVEL_0_CLEAR" }); Assert.That(h.PurposeAchieved, Is.EqualTo(success)); h.LeaseLost(); Assert.That(h.PurposeAchieved, Is.False); } }
        [TestCase("not-visible"), TestCase("inconclusive"), Category("Integration")]
        public void NonVisibleCannotProveUnearned(string visibility)
        { var f = new Fixture(); var v = Visibility(f); v.Visibility = visibility; Assert.Throws<IOException>(() => ObservationV3Wire.ValidateDisplay(Report(f, v, "unearned"), f.Request, v, f.Child, new[] { "VQ_LEVEL_0_CLEAR" })); }
        [Test, Category("Integration")]
        public async Task CorrectionPreservesOriginalAndMustReferenceIt()
        {
            var f = new Fixture(); using (var h = f.Make())
            { await h.SubmitAsync().ConfigureAwait(false); await h.AcceptAsync(f, f.Hello()).ConfigureAwait(false); var v = Visibility(f); var r = Report(f, v, "earned"); h.SaveDisplay(r, v, new[] { "VQ_LEVEL_0_CLEAR" });
                var next = Report(f, v, "unearned"); next.CorrectsHash = ObservationV3Wire.Digest(r); h.SaveDisplay(next, v, new[] { "VQ_LEVEL_0_CLEAR" });
                Assert.That(h.FirstError, Is.Null); Assert.That(f.Evidence.ContainsKey("display-0.json") && f.Evidence.ContainsKey("display-1.json"), Is.True); }
        }
        [Test, Category("Integration")]
        public void EvidenceStoreIsCreateOnlyAndChecksHash()
        {
            string root = Path.Combine(Path.GetTempPath(), "j2m-v3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try { var store = new ObservationV3Store(root); var f = new Fixture(); string hash = store.Create("request.json", f.Request);
                Assert.That(store.Read<ObservationV3Request>("request.json", hash).RunId, Is.EqualTo(f.Request.RunId));
                Assert.Throws<IOException>(() => store.Create("request.json", f.Request)); Assert.Throws<IOException>(() => store.Read<ObservationV3Request>("request.json", Hash));
                Assert.Throws<IOException>(() => store.Create("../bad.json", f.Request)); }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("missing"), TestCase("duplicate"), TestCase("equals"), TestCase("case"), TestCase("mixed"), TestCase("unknown"), Category("Integration")]
        public void ArgumentsNeverNormalizeAwayMissingOrConflictingOptIn(string fault)
        {
            var args = new List<string> { "game", "-j2mOverlayV3Role", "OriginObserver", "-j2mOverlayV3Owner", "SteamDelegated",
                "-j2mPlatformProvider", "steam" };
            if (fault == "missing") args.RemoveRange(5, 2);
            if (fault == "duplicate") args.AddRange(new[] { "-j2mPlatformProvider", "steam" });
            if (fault == "equals") { args.RemoveRange(5, 2); args.Add("-j2mPlatformProvider=steam"); }
            if (fault == "case") args[5] = "-J2MPlatformProvider";
            if (fault == "mixed") args.AddRange(new[] { "-j2mResetOverlayTrial", "reset" });
            if (fault == "unknown") args.AddRange(new[] { "-j2mOverlayV3Other", "value" });
            Assert.Throws<IOException>(() => ObservationV3Options.Parse(args.ToArray()));
        }
        [Test, Category("Integration")]
        public void BothRolesParseExactOsTokensIncludingUnicodePaths()
        {
            var f = new Fixture();
            var origin = ObservationV3Options.Parse(new[] { "game", "-j2mOverlayV3Role", "OriginObserver", "-j2mOverlayV3Owner", "SteamDelegated",
                "-j2mPlatformProvider", "steam" });
            Assert.That(origin.Role, Is.EqualTo(OverlayObservationRole.OriginObserver));
            var child = ObservationV3Options.Parse(new[] { "game", "-j2mOverlayV3Role", "ReplacementObserver", "-j2mOverlayV3Owner", "DirectExe",
                "-j2mOverlayV3Request", Path.GetFullPath("request.json"), "-j2mOverlayV3RequestHash", Hash,
                "-j2mOverlayV3Context", Path.GetFullPath("context.json"), "-j2mOverlayV3ContextHash", Hash,
                "-j2mPlatformProvider", "steam" });
            Assert.That(child.Role, Is.EqualTo(OverlayObservationRole.ReplacementObserver));
        }
        [TestCase("unknown"), TestCase("duplicate"), TestCase("nested"), Category("Integration")]
        public void JsonRejectsMixedAndDuplicateFields(string fault)
        {
            string json = ObservationV3Wire.Serialize(new Fixture().Request);
            if (fault == "unknown") json = json.Insert(1, "\"ResetOverlayWireVersion\":2,");
            if (fault == "duplicate") json = json.Insert(1, "\"Version\":3,");
            if (fault == "nested") json = json.Replace("\"Origin\":{", "\"Origin\":{\"Wrong\":1,");
            Assert.Throws<IOException>(() => ObservationV3Wire.Parse<ObservationV3Request>(json));
        }
        [TestCase("run"), TestCase("role"), TestCase("process"), TestCase("hash"), TestCase("targets"), TestCase("invalid"), Category("Integration")]
        public void DisplayRejectsAttributionAndScopeChanges(string fault)
        {
            var f = new Fixture(); var v = Visibility(f); var r = Report(f, v, "unearned"); var targets = f.Request.Targets;
            switch (fault) { case "run": r.RunId = Guid.NewGuid().ToString("N"); break; case "role": r.Role = OverlayObservationRole.OriginObserver; break;
                case "process": r.Process = f.Request.Origin; break; case "hash": r.VisibilityHash = Hash; break;
                case "targets": targets = ResetOverlayWire.Names(); break; case "invalid": r.Targets[0].Display = "false"; break; }
            Assert.Throws<IOException>(() => ObservationV3Wire.ValidateDisplay(r, f.Request, v, f.Child, targets));
        }
        [Test, Category("Integration")]
        public void FullZeroOfFiveRequiresExplicitFiveTargetReport()
        {
            var f = new Fixture(); f.Request.Targets = ResetOverlayWire.Names(); var v = Visibility(f); var r = Report(f, v, "unearned");
            Assert.Throws<IOException>(() => ObservationV3Wire.ValidateDisplay(r, f.Request, v, f.Child, f.Request.Targets));
            r.Targets = f.Request.Targets.Select(t => new ObservationV3TargetDisplay { Id = t, Display = "unearned" }).ToArray();
            Assert.DoesNotThrow(() => ObservationV3Wire.ValidateDisplay(r, f.Request, v, f.Child, f.Request.Targets));
        }
        [TestCase(2), TestCase(4), Category("Integration")]
        public void WireCannotDowngradeOrGuessVersion(int version)
        { var f = new Fixture(); f.Request.Version = version; Assert.Throws<IOException>(() => ObservationV3Wire.Validate(f.Request)); }
    }
}
