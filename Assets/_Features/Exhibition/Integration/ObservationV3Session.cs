// Unity-free/C#5 session state. Native and I/O adapters are supplied by the runtime.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Exhibition.RestartExperiment
{
    public enum ObservationV3SessionStage { Preparing, Observing, HandoffPreparing, GrantCommitInFlight, GrantConfirmed, ClosingCancelledBeforeCommit, Closing, QuitAllowed, Failed }
    public enum ObservationV3NativePhase { Waiting, Ready, ShuttingDown, ShutdownVerified }
    public sealed class ObservationV3PendingReport
    {
        public readonly long Sequence, Submitted, Deadline;
        public readonly int Generation;
        public readonly string Hash;
        internal readonly TaskCompletionSource<ObservationRef> Completion = new TaskCompletionSource<ObservationRef>(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ObservationRef> Task { get { return Completion.Task; } }
        internal ObservationV3PendingReport(long sequence, long submitted, int generation, string hash)
        { Sequence = sequence; Submitted = submitted; Deadline = submitted + 30000; Generation = generation; Hash = hash; }
    }
    public sealed class ObservationV3Session : IDisposable
    {
        public readonly ObservationV3Admission Admission;
        private readonly Func<long> now;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Dictionary<long, ObservationV3PendingReport> pending = new Dictionary<long, ObservationV3PendingReport>();
        private readonly Timer watchdog;
        private TaskCompletionSource<bool> closeCompletion;
        private Exception firstError;
        private long wireSequence, lastReport, closeDeadline, handoffDeadline;
        private bool reportClosed;
        private int? closeCancelledGeneration;
        private ObservationV3SessionStage stage = ObservationV3SessionStage.Preparing;
        private ObservationV3NativePhase nativePhase;
        private ObservationV3Checkpoint checkpoint = new ObservationV3Checkpoint { State = "None", Sequence = 0, ReportRef = null };
        public event Action<Exception> Failed;
        public event Action Changed;
        public ObservationV3Session(Func<long> now, bool timer = true)
        { this.now = now; Admission = new ObservationV3Admission(now); if (timer) watchdog = new Timer(_ => CheckDeadlines(), null, 10, 10); }
        public CancellationToken Token { get { return lifetime.Token; } }
        public Exception FirstError { get { lock (Admission.SyncRoot) return firstError; } }
        public ObservationV3SessionStage Stage { get { lock (Admission.SyncRoot) return stage; } }
        public ObservationV3NativePhase NativePhase { get { lock (Admission.SyncRoot) return nativePhase; } }
        public long CloseDeadline { get { lock (Admission.SyncRoot) return closeDeadline; } }
        public long LastReport { get { lock (Admission.SyncRoot) return lastReport; } }
        public int PendingCount { get { lock (Admission.SyncRoot) return pending.Count; } }
        public bool CanReport { get { lock (Admission.SyncRoot) return !reportClosed && firstError == null && stage == ObservationV3SessionStage.Observing && nativePhase == ObservationV3NativePhase.Ready; } }
        public ObservationV3Checkpoint Checkpoint { get { lock (Admission.SyncRoot) return ObservationV3Wire.Parse<ObservationV3Checkpoint>(ObservationV3Wire.Serialize(checkpoint)); } }
        // The cancellation reason outlives ClosingCancelledBeforeCommit (including QuitAllowed).
        public bool WasCancelledByClose(int operationGeneration)
        { lock (Admission.SyncRoot) return firstError == null && closeCancelledGeneration == operationGeneration; }
        public void Ready()
        { lock (Admission.SyncRoot) { RequireLive(); nativePhase = ObservationV3NativePhase.Ready; } }
        public void Observing()
        { lock (Admission.SyncRoot) { RequireLive(); if (stage != ObservationV3SessionStage.Preparing) throw new OperationCanceledException(); stage = ObservationV3SessionStage.Observing; } Notify(); }
        public void RequireLive() { lock (Admission.SyncRoot) { Admission.RequireCurrent(Admission.Generation, closeDeadline); if (firstError != null) throw firstError; } }
        // Called synchronously by the main-thread native adapter, before report reservation.
        public void CheckNative(Action observe)
        {
            try { RequireNativeReady(); observe(); }
            catch (Exception error) { TryFail(error); throw; }
        }
        public void RequireNativeReady()
        { lock (Admission.SyncRoot) { RequireLive(); if (nativePhase != ObservationV3NativePhase.Ready) throw new IOException("NativeNotReady"); } }
        public long NextSequence() { lock (Admission.SyncRoot) { RequireLive(); return ++wireSequence; } }
        public ObservationV3PendingReport AdmitReport(string hash)
        {
            lock (Admission.SyncRoot)
            {
                RequireNativeReady(); if (!CanReport) throw new IOException("ReportAdmissionClosed");
                var operation = new ObservationV3PendingReport(++wireSequence, now(), Admission.Generation, hash);
                pending.Add(operation.Sequence, operation); lastReport = operation.Sequence; return operation;
            }
        }
        public ObservationV3PendingReport FindReport(long sequence, string hash)
        {
            lock (Admission.SyncRoot)
            {
                ObservationV3PendingReport operation;
                if (!pending.TryGetValue(sequence, out operation) || operation.Hash != hash) throw new IOException("UnexpectedStoreAckOrHash");
                Admission.RequireCurrent(operation.Generation, operation.Deadline); RequireNativeReady(); return operation;
            }
        }
        public void AcceptReport(ObservationV3PendingReport operation, ObservationRef reference)
        {
            lock (Admission.SyncRoot)
            {
                var current = FindReport(operation.Sequence, reference.Sha256);
                if (!object.ReferenceEquals(current, operation) || operation.Sequence <= checkpoint.Sequence) throw new IOException("ReportAckOrder");
                pending.Remove(operation.Sequence);
                checkpoint = new ObservationV3Checkpoint { State = "Stored", Sequence = operation.Sequence, ReportRef = reference };
            }
            operation.Completion.TrySetResult(reference);
        }
        public Task DrainReports()
        { lock (Admission.SyncRoot) return Task.WhenAll(pending.Values.Select(p => p.Task).ToArray()); }
        public bool TryFail(Exception error)
        {
            ObservationV3PendingReport[] abandoned;
            lock (Admission.SyncRoot)
            {
                if (firstError != null || stage == ObservationV3SessionStage.QuitAllowed) return false;
                firstError = error; Admission.Close(); reportClosed = true; stage = ObservationV3SessionStage.Failed;
                abandoned = pending.Values.ToArray(); pending.Clear();
            }
            // Close admission before any callbacks, I/O, or task continuations run.
            foreach (var operation in abandoned) operation.Completion.TrySetException(error);
            Task.Run(() => { try { lifetime.Cancel(); } catch { } });
            var failed = Failed; if (failed != null) failed(error); Notify(); return true;
        }
        public void CheckDeadlines()
        {
            bool expired;
            lock (Admission.SyncRoot)
            {
                long time = now();
                expired = firstError == null && stage != ObservationV3SessionStage.QuitAllowed &&
                    ((Admission.Deadline != 0 && time >= Admission.Deadline) || (closeDeadline != 0 && time >= closeDeadline) || (handoffDeadline != 0 && time >= handoffDeadline) || pending.Values.Any(p => time >= p.Deadline));
            }
            if (expired) TryFail(new TimeoutException("ObservationSessionDeadlineExceeded"));
        }
        public ObservationV3Invocation BeginHandoff()
        {
            ObservationV3Invocation claim;
            lock (Admission.SyncRoot)
            {
                if (!CanReport || pending.Count != 0) throw new IOException("HandoffAdmissionClosed");
                stage = ObservationV3SessionStage.HandoffPreparing; reportClosed = true; handoffDeadline = now() + 30000;
                claim = Admission.Prepare("grant-confirmation", handoffDeadline);
            }
            Notify(); return claim;
        }
        public bool CommitGrant(ObservationV3Invocation claim)
        {
            lock (Admission.SyncRoot)
            {
                if (stage != ObservationV3SessionStage.HandoffPreparing || nativePhase != ObservationV3NativePhase.Ready || !Admission.TryCommitInvocation(claim)) return false;
                stage = ObservationV3SessionStage.GrantCommitInFlight;
            }
            Notify(); return true;
        }
        public void ConfirmGrant(ObservationV3Invocation claim)
        {
            lock (Admission.SyncRoot)
            {
                Admission.RequireCurrent(claim.Generation, claim.Deadline); RequireLive();
                if (stage != ObservationV3SessionStage.GrantCommitInFlight) throw new IOException("GrantStateMismatch");
                stage = ObservationV3SessionStage.GrantConfirmed; handoffDeadline = 0;
            }
            Notify();
        }
        public Task RequestClose(Func<Task> close)
        {
            TaskCompletionSource<bool> completion;
            lock (Admission.SyncRoot)
            {
                if (closeCompletion != null) return closeCompletion.Task;
                completion = closeCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); reportClosed = true; closeDeadline = now() + 30000;
                if (stage == ObservationV3SessionStage.Preparing || stage == ObservationV3SessionStage.HandoffPreparing)
                { closeCancelledGeneration = Admission.Generation; Admission.Invalidate(); handoffDeadline = 0; stage = ObservationV3SessionStage.ClosingCancelledBeforeCommit; }
                else if (stage == ObservationV3SessionStage.Observing) stage = ObservationV3SessionStage.Closing;
            }
            RunClose(close, completion); Notify(); return completion.Task;
        }
        private async void RunClose(Func<Task> close, TaskCompletionSource<bool> completion)
        { try { await close().ConfigureAwait(false); completion.TrySetResult(true); } catch (Exception error) { TryFail(error); completion.TrySetException(error); } }
        public void BeginShutdown()
        { lock (Admission.SyncRoot) { if (nativePhase == ObservationV3NativePhase.ShutdownVerified) return; nativePhase = ObservationV3NativePhase.ShuttingDown; } }
        public void VerifyShutdown()
        { lock (Admission.SyncRoot) { RequireLive(); if (nativePhase != ObservationV3NativePhase.ShuttingDown) throw new IOException("ShutdownPhaseMismatch"); nativePhase = ObservationV3NativePhase.ShutdownVerified; } }
        public bool TryAllowQuit(bool failureEvidenceReady)
        {
            bool expired;
            lock (Admission.SyncRoot)
            {
                if (stage == ObservationV3SessionStage.QuitAllowed) return true;
                expired = firstError == null && closeDeadline != 0 && now() >= closeDeadline;
                if (!expired)
                {
                    if (firstError != null && !failureEvidenceReady) return false;
                    stage = ObservationV3SessionStage.QuitAllowed; Admission.Close();
                }
            }
            if (expired) { TryFail(new TimeoutException("QuitDeadlineExceeded")); return false; }
            Notify(); return true;
        }
        private void Notify() { var changed = Changed; if (changed != null) changed(); }
        public void Dispose() { if (watchdog != null) watchdog.Dispose(); Admission.Close(); Task.Run(() => { try { lifetime.Cancel(); } catch { } }); }
    }
}
