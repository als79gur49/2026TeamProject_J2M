// Completion-driven runtime-1 transitions. No Unity, process creation or SDK calls.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ObservationV3EventQueue : IDisposable
    {
        private readonly ConcurrentQueue<Action> events = new ConcurrentQueue<Action>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private int owner, disposed;
        public void Post(Action action)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            events.Enqueue(action);
            try { signal.Set(); } catch (ObjectDisposedException) { }
        }
        public bool DrainOne(int wait)
        {
            int thread = Thread.CurrentThread.ManagedThreadId;
            if (owner == 0) owner = thread;
            if (owner != thread) throw new InvalidOperationException("Event queue owner thread changed.");
            Action action;
            if (!events.TryDequeue(out action)) { if (wait > 0) signal.WaitOne(wait); return false; }
            action(); return true;
        }
        public void Dispose() { Interlocked.Exchange(ref disposed, 1); signal.Dispose(); }
    }
    // Atomic boundary between native readiness, ack submission and accepted read completion.
    public sealed class ObservationV3AcceptanceAdmission
    {
        private readonly object gate = new object();
        private bool ackStarted, rejected;
        public bool Received(bool successful)
        {
            lock (gate) { if (!successful || !ackStarted) rejected = true; return !rejected; }
        }
        public void BeginAck()
        {
            lock (gate)
            {
                if (rejected || ackStarted) throw new IOException("PrematureAcceptanceOrClosedAdmission");
                ackStarted = true;
            }
        }
    }
    public enum ObservationV3SubmissionState { NotAttempted, Pending, ReturnedPersisted, Failed }
    public enum ObservationV3RuntimeStage { Prepared, Handshaking, Observing, ExitPendingReports, ExitAuthorized, Completed, Uncertain }

    /// <summary>Out-of-queue I/O only returns completion events. A watchdog can close admission without queue progress.</summary>
    public sealed class ObservationV3Operations : IDisposable
    {
        private readonly ObservationV3EventQueue queue;
        private readonly Func<long> now;
        private readonly Action<Exception> failure;
        private readonly CancellationTokenSource closed = new CancellationTokenSource();
        private readonly Timer watchdog;
        private long deadline;
        private int operationId;
        private readonly ConcurrentDictionary<int, long> pendingDeadlines = new ConcurrentDictionary<int, long>();
        private int terminal, generation = 1;
        private Exception firstError;
        public Exception FirstError { get { lock (Admission.SyncRoot) return firstError; } }
        public readonly ObservationV3Admission Admission;
        public bool Closed { get { return Volatile.Read(ref terminal) != 0; } }
        public CancellationToken Token { get { return closed.Token; } }
        public int Generation { get { return generation; } }
        public ObservationV3Operations(ObservationV3EventQueue queue, Func<long> now, Action<Exception> failure)
        {
            this.queue = queue; this.now = now; this.failure = failure; Admission = new ObservationV3Admission(now);
            watchdog = new Timer(_ => CheckDeadline(), null, 10, 10);
        }
        public void Deadline(long absolute) { Admission.SetDeadline(absolute); Interlocked.Exchange(ref deadline, absolute); CheckDeadline(); }
        private bool CheckDeadline()
        {
            long d = Interlocked.Read(ref deadline);
            if ((d != 0 && now() >= d) || pendingDeadlines.Values.Any(expiry => now() >= expiry)) Fail(new TimeoutException("ObservationDeadlineExceeded"));
            return !Closed;
        }
        public void Fail(Exception error)
        {
            lock (Admission.SyncRoot)
            {
                if (terminal != 0) return;
                firstError = error; Admission.Close(); Interlocked.Exchange(ref terminal, 1); Interlocked.Increment(ref generation);
            }
            Task.Run(() => { try { closed.Cancel(); } catch { } });
            failure(error);
        }
        public bool Complete()
        {
            bool expired;
            lock (Admission.SyncRoot)
            {
                if (terminal != 0 || Admission.Closed) return false;
                long time = now(); long absolute = Interlocked.Read(ref deadline);
                expired = (absolute != 0 && time >= absolute) || pendingDeadlines.Values.Any(expiry => time >= expiry);
                if (!expired)
                {
                    Admission.Close(); Interlocked.Exchange(ref terminal, 1); Interlocked.Increment(ref generation);
                }
            }
            if (expired) { Fail(new TimeoutException("ObservationDeadlineExceeded")); return false; }
            watchdog.Change(Timeout.Infinite, Timeout.Infinite);
            Task.Run(() => { try { closed.Cancel(); } catch { } });
            return true;
        }
        public void Start<T>(Func<CancellationToken, Task<T>> io, Action<T> accept, bool idle = false)
        {
            if (!CheckDeadline()) return;
            int opGeneration = generation;
            int id = Interlocked.Increment(ref operationId);
            if (!idle) pendingDeadlines[id] = now() + 30000;
            // Includes synchronous adapter/query hangs: invoking io itself is outside the queue.
            Task.Run(async () =>
            {
                try
                {
                    closed.Token.ThrowIfCancellationRequested();
                    var work = Task.Run(() => io(closed.Token), closed.Token);
                    if (!idle && await Task.WhenAny(work, Task.Delay(30000, closed.Token)).ConfigureAwait(false) != work)
                    { Fail(new TimeoutException("ObservationOperationTimeout")); return; }
                    T result = await work.ConfigureAwait(false);
                    queue.Post(() => { if (opGeneration == generation && CheckDeadline()) { long ignored; pendingDeadlines.TryRemove(id, out ignored); try { accept(result); } catch (Exception error) { Fail(error); } } });
                }
                catch (Exception e) { Fail(e); }
            });
        }
        public void Dispose() { if (!Closed) Fail(new OperationCanceledException()); watchdog.Dispose(); }
    }

    public sealed class ObservationV3ReportLedger
    {
        private readonly string run, role;
        private ProcessIdentity self;
        private readonly string[] targets;
        private readonly ObservationRef binding, baseline;
        private ObservationV3Visibility visibility;
        private ObservationRef visibilityRef, displayRef;
        private ObservationV3Display display;
        private readonly Dictionary<long, ObservationRef> stored = new Dictionary<long, ObservationRef>();
        public long LastSubmitted { get; private set; }
        public long LastStored { get; private set; }
        public bool PurposeAchieved { get { return visibility != null && visibility.Visibility == "opened" && display != null &&
            ObservationV3RuntimeWire.SameRef(display.VisibilityRef, visibilityRef) && display.Targets.All(t => t.Display == "unearned"); } }
        public ObservationV3ReportLedger(string run, string role, ProcessIdentity self, string[] targets, ObservationRef binding, ObservationRef baseline)
        { this.run = run; this.role = role; this.self = self; this.targets = targets; this.binding = binding; this.baseline = baseline; }
        public void BindSelf(ProcessIdentity value)
        { if (self != null || LastSubmitted != 0) throw new IOException("ReportOwnerAlreadyBound"); ObservationV3Wire.Identity(value); self = value; }
        public void Submit(long sequence)
        {
            ObservationV3RuntimeWire.Require(sequence > LastSubmitted, "DuplicateReportSequence"); LastSubmitted = sequence;
        }
        public void Validate(object report)
        {
            var v = report as ObservationV3Visibility;
            if (v != null)
            {
                ObservationV3RuntimeWire.Visibility(v, binding, baseline, run, self, role);
                if (v.CorrectsRef != null) ObservationV3RuntimeWire.Require(ObservationV3RuntimeWire.SameRef(v.CorrectsRef, visibilityRef), "VisibilityCorrectionFork");
                return;
            }
            var d = report as ObservationV3Display;
            ObservationV3RuntimeWire.Require(d != null && visibility != null, "OpenedVisibilityRequired");
            ObservationV3RuntimeWire.Display(d, binding, baseline, visibilityRef, visibility, run, self, role, targets);
            if (d.CorrectsRef != null) ObservationV3RuntimeWire.Require(ObservationV3RuntimeWire.SameRef(d.CorrectsRef, displayRef), "DisplayCorrectionFork");
        }
        public void Commit(long sequence, object report, ObservationRef reference)
        {
            Validate(report);
            ObservationV3RuntimeWire.Require(sequence > LastStored && sequence <= LastSubmitted, "ReportCommitOrder");
            if (report is ObservationV3Visibility)
            { visibility = (ObservationV3Visibility)report; visibilityRef = reference; display = null; }
            else { display = (ObservationV3Display)report; displayRef = reference; }
            stored.Add(sequence, reference); LastStored = sequence;
        }
        public ObservationV3Checkpoint Checkpoint()
        {
            return new ObservationV3Checkpoint { State = LastStored == 0 ? "None" : "Stored", Sequence = LastStored,
                ReportRef = LastStored == 0 ? null : stored[LastStored] };
        }
        public void CheckPrefix(ObservationV3Checkpoint c)
        {
            ObservationV3RuntimeWire.Require(c != null, "ExitCheckpointRequired");
            if (c.State == "None") { ObservationV3RuntimeWire.Require(c.Sequence == 0 && c.ReportRef == null, "InvalidEmptyCheckpoint"); return; }
            ObservationRef r;
            ObservationV3RuntimeWire.Require(c.State == "Stored" && stored.TryGetValue(c.Sequence, out r) &&
                ObservationV3RuntimeWire.SameRef(r, c.ReportRef), "ExitCheckpointNotPrefix");
        }
    }
    public interface IObservationV3RuntimeHostPorts
    {
        long Now { get; }
        Task<ObservationV3Submission> Submit(CancellationToken token);
        Task<ObservationV3RuntimeHello> Hello(CancellationToken token);
        Task ValidateHello(ObservationV3RuntimeHello hello, CancellationToken token);
        Task<ObservationRef> Store(string name, object value, CancellationToken token);
        Task Accept(ObservationV3RuntimeHello hello, CancellationToken token);
        Task<ObservationV3Envelope> Read(CancellationToken token);
        Task Send(string kind, object body, CancellationToken token);
        Task ValidatePins(CancellationToken token);
        void BindAdmission(ObservationV3Admission admission, Action<Exception> fail);
        Task ValidateExitCompleted(ObservationV3Envelope frame, CancellationToken token);
        bool ChildExited();
        void Close();
        void TerminalFailure(Exception error);
    }
    public sealed class ObservationV3RuntimeHandoff : IDisposable
    {
        private readonly IObservationV3RuntimeHostPorts ports;
        private readonly ObservationV3Operations operations;
        private readonly ObservationV3ReportLedger ledger;
        private readonly ObservationV3EventQueue queue;
        private readonly Queue<ObservationV3Envelope> reports = new Queue<ObservationV3Envelope>();
        private ObservationV3RuntimeHello hello;
        private bool helloValid, acceptanceStarted, storing, exiting, checkingPins, authorizing, authorized, completing;
        private long inboundSequence, exitBoundary, exitDeadline;
        private bool completionReceived, completionVerified, actualExited, eof;
        private readonly TaskCompletionSource<bool> failureStored = new TaskCompletionSource<bool>();
        public Task FailureEvidence { get { return failureStored.Task; } }
        public ObservationV3RuntimeStage Stage { get; private set; }
        public ObservationV3SubmissionState Submission { get; private set; }
        public bool Closed { get { return operations.Closed; } }
        public bool PurposeAchieved { get { return Stage == ObservationV3RuntimeStage.Completed && ledger.PurposeAchieved; } }
        public ObservationV3RuntimeHandoff(IObservationV3RuntimeHostPorts ports, ObservationV3EventQueue queue, ObservationV3ReportLedger ledger)
        {
            this.ports = ports; this.queue = queue; this.ledger = ledger;
            operations = new ObservationV3Operations(queue, () => ports.Now, error =>
            {
                try { ports.Close(); } catch { }
                queue.Post(() => { Stage = ObservationV3RuntimeStage.Uncertain; });
                // Failure evidence cannot hold up admission closure or overwrite the first failure.
                Task.Run(() => { try { ports.TerminalFailure(error); failureStored.TrySetResult(true); } catch (Exception e) { failureStored.TrySetException(e); } });
            });
        }
        public void Start()
        {
            ports.BindAdmission(operations.Admission, operations.Fail);
            if (Submission != ObservationV3SubmissionState.NotAttempted) throw new IOException("SubmissionAlreadyAttempted");
            Stage = ObservationV3RuntimeStage.Handshaking;
            Submission = ObservationV3SubmissionState.Pending;
            operations.Deadline(ports.Now + 30000);
            operations.Start(async token =>
            {
                var result = await ports.Submit(token).ConfigureAwait(false);
                ObservationV3RuntimeWire.Require(result.Disposition == "returned", "SubmissionUncertain");
                await ports.Store("submission-result.json", result, token).ConfigureAwait(false);
                return result;
            }, result => { Submission = ObservationV3SubmissionState.ReturnedPersisted; TryAccept(); });
            operations.Start(token => ports.Hello(token), value =>
            {
                hello = value;
                operations.Start(async token => { await ports.ValidateHello(value, token).ConfigureAwait(false); return true; }, valid => { ledger.BindSelf(value.Self); helloValid = valid; TryAccept(); });
            });
        }
        private void TryAccept()
        {
            if (acceptanceStarted || !helloValid || Submission != ObservationV3SubmissionState.ReturnedPersisted) return;
            acceptanceStarted = true;
            operations.Start(async token => { await ports.Accept(hello, token).ConfigureAwait(false); return true; }, accepted =>
            {
                Stage = ObservationV3RuntimeStage.Observing; operations.Deadline(0); ReadNext();
            });
        }
        private void ReadNext()
        {
            operations.Start(token => ports.Read(token), frame =>
            {
                try
                {
                    if (frame == null)
                    {
                        if (!completionReceived) throw new IOException("ExitCompletedMissingAtEof");
                        eof = true; TryComplete(); return;
                    }
                    ObservationV3RuntimeWire.Require(frame.Sequence == ++inboundSequence, "UnexpectedFrameSequence");
                    if (frame.Kind == "cancel") throw new OperationCanceledException("ChildCancelled");
                    if (frame.Kind == "exit-request")
                    {
                        ObservationV3RuntimeWire.Require(!exiting, "DuplicateExitRequest");
                        var exit = ObservationV3RuntimeWire.Parse<ObservationV3ExitRequest>(System.Text.Encoding.UTF8.GetBytes(frame.Body));
                        ledger.CheckPrefix(exit.Checkpoint);
                        ObservationV3RuntimeWire.Require(exit.Boundary == ledger.LastSubmitted, "ExitBoundaryMismatch");
                        exiting = true; exitBoundary = exit.Boundary; exitDeadline = ports.Now + 30000;
                        Stage = ObservationV3RuntimeStage.ExitPendingReports; operations.Deadline(exitDeadline); TryExit();
                    }
                    else if (frame.Kind == "exit-completed")
                    {
                        ObservationV3RuntimeWire.Require(authorizing && !completionReceived, "UnexpectedExitCompleted");
                        var done = ObservationV3RuntimeWire.Parse<ObservationV3ExitCompleted>(System.Text.Encoding.UTF8.GetBytes(frame.Body));
                        ledger.CheckPrefix(done.Checkpoint);
                        ObservationV3RuntimeWire.Require(done.Boundary == exitBoundary && done.Checkpoint.Sequence == ledger.LastStored &&
                            done.Checkpoint.State == ledger.Checkpoint().State, "ExitCompletedBoundaryMismatch");
                        completionReceived = true;
                        operations.Start(async token => { await ports.ValidateExitCompleted(frame, token).ConfigureAwait(false); return true; }, valid =>
                        { completionVerified = true; TryComplete(); });
                    }
                    else
                    {
                        ObservationV3RuntimeWire.Require(!exiting && (frame.Kind == "visibility-report" || frame.Kind == "display-report"), "UnexpectedReport");
                        ledger.Submit(frame.Sequence); reports.Enqueue(frame); StoreNext();
                    }
                    // Exactly one reader. After authorization EOF is handled by ports.Read/exit observation.
                    ReadNext();
                }
                catch (Exception e) { operations.Fail(e); }
            }, true);
        }
        private void StoreNext()
        {
            if (storing || reports.Count == 0) { TryExit(); return; }
            storing = true; var frame = reports.Dequeue();
            object report;
            try
            {
                report = frame.Kind == "visibility-report" ? (object)ObservationV3RuntimeWire.Parse<ObservationV3Visibility>(System.Text.Encoding.UTF8.GetBytes(frame.Body)) :
                    ObservationV3RuntimeWire.Parse<ObservationV3Display>(System.Text.Encoding.UTF8.GetBytes(frame.Body));
                ledger.Validate(report);
            }
            catch (Exception e) { operations.Fail(e); return; }
            operations.Start(token => ports.Store("report-" + frame.Sequence.ToString("D8") + ".json", report, token), reference =>
            {
                try { ledger.Commit(frame.Sequence, report, reference); }
                catch (Exception e) { operations.Fail(e); return; }
                operations.Start(async token => { await ports.Send("report-stored", new ObservationV3Stored { ReportSequence = frame.Sequence, ReportRef = reference }, token).ConfigureAwait(false); return true; }, sent =>
                { storing = false; StoreNext(); });
            });
        }
        private void TryExit()
        {
            if (!exiting || storing || authorizing || reports.Count != 0 || Stage != ObservationV3RuntimeStage.ExitPendingReports) return;
            if (ledger.LastStored != exitBoundary) { operations.Fail(new IOException("UnstoredExitBoundary")); return; }
            authorizing = true;
            operations.Start(async token =>
            {
                await ports.Send("exit-authorized", new ObservationV3ExitAuthorized { Boundary = exitBoundary, Checkpoint = ledger.Checkpoint() }, token).ConfigureAwait(false); return true;
            }, sent => { authorized = true; Stage = ObservationV3RuntimeStage.ExitAuthorized; TryComplete(); });
        }
        public void Tick()
        {
            if (Closed || checkingPins || completing) return;
            checkingPins = true;
            operations.Start(async token =>
            {
                await ports.ValidatePins(token).ConfigureAwait(false); return ports.ChildExited();
            }, exited =>
            {
                checkingPins = false;
                if (exited) actualExited = true;
                TryComplete();
            });
        }
        private void TryComplete()
        {
            if (Closed || completing || !actualExited || !authorized || !completionVerified || !eof) return;
            if (storing || reports.Count != 0) { operations.Fail(new IOException("ChildExitedBeforeReportBarrier")); return; }
            completing = true;
            operations.Start(token => ports.Store("terminal.json", new ObservationV3Terminal {
                Version = 3, ProtocolRevision = ObservationV3RuntimeWire.Revision, Kind = "terminal", Author = "Helper", Sequence = long.MaxValue,
                WrittenUtc = DateTime.UtcNow.ToString("o"), RunId = hello.RunId, BindingRef = hello.ContextRef,
                Phase = "ChildExited", Outcome = "Completed", FirstError = "", ChildExit = "Confirmed", HelperExit = "Unknown", Tracking = "Unknown",
                PurposeAchieved = ledger.PurposeAchieved }, token), reference =>
            { if (operations.Complete()) Stage = ObservationV3RuntimeStage.Completed; ports.Close(); });
        }
        public void Cancel() { operations.Fail(new OperationCanceledException()); }
        public void Dispose() { operations.Dispose(); }
    }
}
