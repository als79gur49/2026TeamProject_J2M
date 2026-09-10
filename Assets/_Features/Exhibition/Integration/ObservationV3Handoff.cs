// Unity-free observation v3 state machine. Only injected launchers can submit requests.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ObservationSubmission
    {
        public string Disposition; // returned / unknown; never a game creation receipt
        public ProcessIdentity Launcher;
    }
    public interface IObservationLaunch
    {
        void RequireAvailable(ObservationLaunchOwner owner);
        Task<ObservationSubmission> SubmitAsync(ObservationV3Context context, CancellationToken cancellation);
    }
    public sealed class UnavailableObservationLaunch : IObservationLaunch
    {
        public void RequireAvailable(ObservationLaunchOwner owner) { throw new InvalidOperationException("LaunchTransportUnavailable"); }
        public Task<ObservationSubmission> SubmitAsync(ObservationV3Context context, CancellationToken cancellation)
        { throw new InvalidOperationException("LaunchTransportUnavailable"); }
    }
    public interface IObservationV3Environment
    {
        long Milliseconds { get; }
        long UtcTicks { get; }
        ProcessIdentity Capture(int pid);
        bool HasExited(ProcessIdentity identity); // retained exact process handle; never process-name absence
        void ValidateReadOnlyPins(); // current account, Ready, payload, baseline and participant files; no repair
        void EnsureOnlyChild(ProcessIdentity child); // also detects candidates that never connect to IPC
        string VerifyEffectiveArguments(ProcessIdentity child, ObservationV3Context context);
        string CreateEvidence(string name, object value);
        void CloseAdmission();
        Task DelayAsync();
    }
    public interface IObservationV3Peer
    {
        ProcessIdentity CapturePeer();
        Task<string> ChallengeAsync(string challenge, CancellationToken cancellation);
        Task<ObservationV3Ack> ReceiptAsync(ObservationV3Receipt receipt, CancellationToken cancellation);
        Task<bool> ConfirmAsync(string receiptHash, CancellationToken cancellation);
        void WatchLease(Action lost);
    }
    public enum ObservationV3Stage { Prepared, Submitted, ChildVerified, Observing, ReportsStored, ChildExited, Completed, Failed, Uncertain, Cancelled }

    public sealed class ObservationV3Handoff : IDisposable
    {
        private readonly object gate = new object();
        private readonly IObservationV3Environment env;
        private readonly IObservationLaunch launch;
        private readonly ObservationV3Request request;
        private readonly ObservationV3Client client;
        private readonly ObservationV3Context context;
        private readonly CancellationTokenSource closed = new CancellationTokenSource();
        private bool terminal, attempted, accepting;
        private long deadline, submittedUtc, quitDeadline;
        private ProcessIdentity child;
        private string receiptHash;
        public ObservationV3Stage Stage { get; private set; }
        public string FirstError { get; private set; }
        public bool ChildExitConfirmed { get; private set; }
        // Neither this helper nor child can independently establish these external results.
        public string HelperExit { get { return "unknown"; } }
        public string SteamTracking { get { return "unknown"; } }
        public bool PurposeAchieved { get; private set; }
        public ObservationV3Handoff(ObservationV3Request request, ObservationV3Client client, ObservationV3Context context,
            IObservationLaunch launch, IObservationV3Environment env)
        {
            if (launch == null || env == null) throw new ArgumentNullException();
            ObservationV3Wire.ValidateContext(request, client, context);
            this.request = ObservationV3Wire.Copy(request); this.client = ObservationV3Wire.Copy(client);
            this.context = ObservationV3Wire.Copy(context); this.launch = launch; this.env = env;
            Stage = ObservationV3Stage.Prepared;
        }
        private void Fail(Exception error, ObservationV3Stage stage)
        {
            lock (gate)
            {
                if (terminal) return;
                terminal = true; Stage = stage; PurposeAchieved = false; FirstError = error.Message;
            }
            // Close in memory before any fallible evidence. Late completions can never reopen admission.
            try { closed.Cancel(); } catch (Exception) { }
            try { env.CloseAdmission(); } catch (Exception) { }
            var failure = new ObservationV3Event { Stage = stage.ToString(), Error = FirstError, ChildExited = ChildExitConfirmed,
                HelperExit = HelperExit, SteamTracking = SteamTracking };
            try { env.CreateEvidence("terminal.json", failure); }
            catch (Exception)
            {
                // Completion evidence may already exist when cancellation wins during its I/O.
                // Preserve the first failure separately; never overwrite the original evidence.
                try { env.CreateEvidence("terminal-failure.json", failure); } catch (Exception) { }
            }
        }
        private void Move(ObservationV3Stage next)
        {
            lock (gate) { if (terminal) throw new InvalidOperationException(FirstError ?? "AdmissionClosed"); Stage = next; }
        }
        private void Guard(bool checkDeadline)
        {
            lock (gate)
            {
                if (terminal) throw new InvalidOperationException(FirstError ?? "AdmissionClosed");
                if (checkDeadline && env.Milliseconds >= deadline) throw new TimeoutException("ChildHandoffTimeout");
            }
            env.ValidateReadOnlyPins();
            ObservationV3Wire.Require(ObservationV3Wire.Same(env.Capture(client.Helper.Pid), client.Helper), "HelperIdentityChanged");
            ObservationV3Wire.Require(ObservationV3Wire.Same(env.Capture(client.Current.Pid), client.Current), "NewClientIdentityChanged");
            env.EnsureOnlyChild(child);
        }
        public void Tick()
        {
            if (terminal || Stage == ObservationV3Stage.Prepared) return;
            try
            {
                Guard(Stage == ObservationV3Stage.Submitted || Stage == ObservationV3Stage.ChildVerified);
                if (quitDeadline != 0 && env.Milliseconds >= quitDeadline) throw new TimeoutException("ChildExitUnknown");
                if (child != null && quitDeadline != 0 && env.HasExited(child)) { ConfirmChildExit(child, true); return; }
                if (child != null) ObservationV3Wire.Require(ObservationV3Wire.Same(env.Capture(child.Pid), child), "ChildIdentityChanged");
                if (quitDeadline != 0 && env.Milliseconds >= quitDeadline) throw new TimeoutException("ChildExitUnknown");
            }
            catch (Exception e) { Fail(e, attempted ? ObservationV3Stage.Uncertain : ObservationV3Stage.Failed); }
        }
        private async Task<T> Bounded<T>(Task<T> operation)
        {
            if (operation == null) throw new IOException("MissingOperation");
            // Observe exceptions even when a non-cancellable launcher completes after admission closed.
            var observation = operation.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            while (!operation.IsCompleted)
            {
                Guard(true);
                await Task.WhenAny(operation, env.DelayAsync()).ConfigureAwait(false);
            }
            Guard(true);
            T result = await operation.ConfigureAwait(false);
            Guard(true); return result;
        }
        public async Task SubmitAsync()
        {
            lock (gate) { if (terminal || attempted) return; attempted = true; }
            bool invoked = false;
            try
            {
                launch.RequireAvailable(request.Owner); // before any cycle side effect in the cycle wrapper too
                deadline = checked(env.Milliseconds + 30000); Guard(true);
                env.CreateEvidence("submission-attempt.json", new ObservationV3Event { RunId = request.RunId, Nonce = request.Nonce, ContextHash = ObservationV3Wire.Digest(context) });
                Guard(true); submittedUtc = env.UtcTicks;
                Move(ObservationV3Stage.Submitted); invoked = true;
                var result = await Bounded(Task.Run(() => launch.SubmitAsync(ObservationV3Wire.Copy(context), closed.Token), closed.Token)).ConfigureAwait(false);
                ObservationV3Wire.Require(result != null && result.Disposition == "returned", "SubmissionUncertain");
                env.CreateEvidence("submission-result.json", result); Guard(true);
            }
            catch (Exception e) { Fail(e, invoked ? ObservationV3Stage.Uncertain : ObservationV3Stage.Failed); }
        }
        public async Task AcceptAsync(IObservationV3Peer peer, ObservationV3Hello hello)
        {
            lock (gate)
            {
                if (terminal) return;
                if (accepting || child != null) { Fail(new IOException("DuplicateChild"), ObservationV3Stage.Failed); return; }
                accepting = true;
            }
            try
            {
                ObservationV3Wire.Require(Stage == ObservationV3Stage.Submitted && peer != null && hello != null, "UnexpectedChild"); Guard(true);
                var captured = peer.CapturePeer(); ObservationV3Wire.SameFileScope(captured, request.Origin);
                ObservationV3Wire.Require(captured.StartTicks >= submittedUtc && !ObservationV3Wire.Same(captured, request.Origin) &&
                    ObservationV3Wire.Same(captured, env.Capture(captured.Pid)), "ChildIdentityMismatch");
                ObservationV3Wire.Require(hello.Version == 3 && hello.RunId == request.RunId && hello.Nonce == request.Nonce &&
                    hello.RequestHash == ObservationV3Wire.Digest(request) && hello.ContextHash == ObservationV3Wire.Digest(context) &&
                    ObservationV3Wire.Same(hello.Child, captured), "ChildContextMismatch");
                string argumentsHash = env.VerifyEffectiveArguments(captured, context); ObservationV3Wire.Hash(argumentsHash);
                ObservationV3Wire.Require(hello.EffectiveArgumentsHash == argumentsHash, "EffectiveArgumentsMismatch");
                child = ObservationV3Wire.Copy(captured); Guard(true);
                string challenge = Guid.NewGuid().ToString("N");
                string response = await Bounded(peer.ChallengeAsync(challenge, closed.Token)).ConfigureAwait(false);
                ObservationV3Wire.Require(response == challenge && ObservationV3Wire.Same(peer.CapturePeer(), child), "ChallengePeerMismatch");
                Guard(true); ObservationV3Wire.Require(ObservationV3Wire.Same(env.Capture(child.Pid), child), "ChildIdentityChanged");
                var receipt = new ObservationV3Receipt { RunId = request.RunId, Nonce = request.Nonce, RequestHash = ObservationV3Wire.Digest(request),
                    ContextHash = ObservationV3Wire.Digest(context), ClientHash = ObservationV3Wire.Digest(client), Challenge = challenge,
                    EffectiveArgumentsHash = argumentsHash, Origin = request.Origin, Helper = client.Helper, Child = child,
                    NewClient = client.Current, AcceptedAt = env.Milliseconds };
                receiptHash = env.CreateEvidence("child-receipt.json", receipt);
                ObservationV3Wire.Require(receiptHash == ObservationV3Wire.Digest(receipt), "ReceiptStorageMismatch");
                Guard(true); Move(ObservationV3Stage.ChildVerified);
                var ack = await Bounded(peer.ReceiptAsync(receipt, closed.Token)).ConfigureAwait(false);
                ObservationV3Wire.Require(ack != null && ack.Version == 3 && ack.ReceiptHash == receiptHash && ack.Challenge == challenge &&
                    ack.NativeReady && ack.LoggedOn && ack.AppId == request.AppId && ack.SteamId == request.SteamId &&
                    ObservationV3Wire.Same(ack.Child, child) && ObservationV3Wire.Same(ack.Helper, client.Helper) &&
                    ObservationV3Wire.Same(ack.NewClient, client.Current) && ObservationV3Wire.Same(peer.CapturePeer(), child), "ChildAckMismatch");
                Guard(true); env.CreateEvidence("child-ack.json", ack); Guard(true);
                ObservationV3Wire.Require(await Bounded(peer.ConfirmAsync(receiptHash, closed.Token)).ConfigureAwait(false), "AcceptanceNotDelivered");
                Guard(true); Move(ObservationV3Stage.Observing); peer.WatchLease(LeaseLost);
            }
            catch (Exception e) { Fail(e, ObservationV3Stage.Failed); }
        }
        public void LeaseLost()
        {
            // An explicit exit request permits only bounded external exit confirmation, never more reports.
            if (!terminal && quitDeadline != 0) { awaitingExit = true; return; }
            Fail(new IOException("HelperOrChildLeaseLost"), ObservationV3Stage.Uncertain);
        }
        private bool awaitingExit;
        public void Cancel() { Fail(new OperationCanceledException("ObservationCancelled"), ObservationV3Stage.Cancelled); }
        public void SaveDisplay(ObservationV3DisplayReport report, OverlayUserObservation visibility, string[] targets)
        {
            try
            {
                Guard(false); ObservationV3Wire.Require(!awaitingExit && quitDeadline == 0 && (Stage == ObservationV3Stage.Observing || Stage == ObservationV3Stage.ReportsStored), "ChildNotObserving");
                ObservationV3Wire.Require(report != null && report.Role == OverlayObservationRole.ReplacementObserver, "ReplacementReportRequired");
                ObservationV3Wire.ValidateDisplay(report, request, visibility, child, targets);
                if (Stage == ObservationV3Stage.ReportsStored) ObservationV3Wire.Require(report.CorrectsHash == lastReportHash, "CorrectionMustReferencePreviousReport");
                else ObservationV3Wire.Require(report.CorrectsHash == null, "UnexpectedCorrection");
                string name = "display-" + reportSequence++ + ".json";
                lastReportHash = env.CreateEvidence(name, report); Guard(false);
                lock (gate)
                {
                    if (terminal) throw new InvalidOperationException("AdmissionClosed");
                    PurposeAchieved = Array.TrueForAll(report.Targets, t => t.Display == "unearned"); Stage = ObservationV3Stage.ReportsStored;
                }
            }
            catch (Exception e) { Fail(e, ObservationV3Stage.Failed); }
        }
        private string lastReportHash;
        private int reportSequence;
        public void RequestChildExit() { if (!terminal && child != null && quitDeadline == 0) quitDeadline = checked(env.Milliseconds + 30000); }
        public void ConfirmChildExit(ProcessIdentity externallyObserved, bool exited)
        {
            try
            {
                Guard(false);
                ObservationV3Wire.Require(quitDeadline != 0, "ChildExitNotRequested");
                if (env.Milliseconds >= quitDeadline) throw new TimeoutException("ChildExitUnknown");
                ObservationV3Wire.Require(child != null && ObservationV3Wire.Same(child, externallyObserved) && exited && env.HasExited(child), "ChildExitUnconfirmed");
                ChildExitConfirmed = true; Move(ObservationV3Stage.ChildExited);
                env.CreateEvidence("child-exited.json", new ObservationV3Event { Child = child, ChildExited = true });
                env.CloseAdmission(); env.CreateEvidence("terminal.json", new ObservationV3Event { Stage = "HelperCompleted", HelperExit = HelperExit, SteamTracking = SteamTracking });
                lock (gate)
                {
                    if (terminal) return; // Cancellation/failure during evidence I/O wins permanently.
                    terminal = true; Stage = ObservationV3Stage.Completed;
                }
            }
            catch (Exception e) { Fail(e, ObservationV3Stage.Uncertain); }
        }
        public void Dispose() { if (!terminal) Cancel(); closed.Dispose(); }
    }

    // Reuses the existing shutdown/probe policy. No real launch adapter is registered by production startup.
    public sealed class ObservationV3Cycle : ICycleEnvironment
    {
        private readonly ICycleEnvironment inner;
        private readonly IObservationLaunch launch;
        private readonly ObservationLaunchOwner owner;
        private readonly Func<ObservationV3Handoff> startHandoff;
        public ObservationV3Cycle(ICycleEnvironment inner, IObservationLaunch launch, ObservationLaunchOwner owner, Func<ObservationV3Handoff> startHandoff)
        { this.inner = inner; this.launch = launch; this.owner = owner; this.startHandoff = startHandoff; }
        public long Milliseconds { get { return inner.Milliseconds; } }
        public void Validate() { launch.RequireAvailable(owner); inner.Validate(); }
        public IDisposable AcquireCycleLock() { return inner.AcquireCycleLock(); }
        public bool ParentAlive() { return inner.ParentAlive(); }
        public void EnsureNoOtherGame() { inner.EnsureNoOtherGame(); }
        public bool OriginalSteamAlive() { return inner.OriginalSteamAlive(); }
        public void RequestSteamExit(Deadline d) { inner.RequestSteamExit(d); }
        public bool ShutdownCommandAlive() { return inner.ShutdownCommandAlive(); }
        public void EnsureSteamExited() { inner.EnsureSteamExited(); }
        public void StartSteam() { inner.StartSteam(); }
        public void EnsureNewSteamUnchanged() { inner.EnsureNewSteamUnchanged(); }
        public bool ProbeReady(Deadline d) { return inner.ProbeReady(d); }
        public void StartGame(Deadline d)
        {
            if (d != null) d.Remaining();
            var handoff = startHandoff();
            if (handoff == null || handoff.FirstError != null || handoff.Stage != ObservationV3Stage.Completed)
                throw new IOException(handoff == null ? "HandoffMissing" : handoff.FirstError ?? "HandoffNotCompleted");
        }
        public void Delay(int ms) { inner.Delay(ms); }
        public void Record(string stage) { inner.Record(stage == "GameCreated" ? "ObservationHandoffReturned" : stage); }
        public void Cleanup() { inner.Cleanup(); }
        public void RecordTerminal(Exception e) { inner.RecordTerminal(e); }
    }
}
