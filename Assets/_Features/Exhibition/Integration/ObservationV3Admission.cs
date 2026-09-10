// Shared with the Windows host: Unity-free, C# 5.
using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ObservationV3InvocationFact
    {
        public string OperationId, Outcome;
        public int Generation;
        public long Deadline;
        public bool InvocationCommitted, InvocationEntered, Returned;
    }
    public sealed class ObservationV3Invocation
    {
        public readonly string OperationId;
        public readonly int Generation;
        public readonly long Deadline;
        public bool InvocationCommitted { get; internal set; }
        public bool InvocationEntered { get; internal set; }
        public bool Returned { get; internal set; }
        internal ObservationV3Invocation(string id, int generation, long deadline)
        { OperationId = id; Generation = generation; Deadline = deadline; }
    }

    // This lock protects only in-memory decisions. Never invoke I/O, native code,
    // cancellation callbacks or task continuations while holding it.
    public sealed class ObservationV3Admission
    {
        public readonly object SyncRoot = new object();
        private readonly Func<long> now;
        private readonly Dictionary<string, ObservationV3Invocation> claims = new Dictionary<string, ObservationV3Invocation>();
        private bool closed;
        private int generation = 1;
        private long deadline;
        public ObservationV3Admission(Func<long> now) { this.now = now; }
        public int Generation { get { lock (SyncRoot) return generation; } }
        public bool Closed { get { lock (SyncRoot) return closed; } }
        public long Deadline { get { lock (SyncRoot) return deadline; } }
        public void SetDeadline(long value) { lock (SyncRoot) deadline = value; }
        public bool Close()
        { lock (SyncRoot) { if (closed) return false; closed = true; generation++; return true; } }
        public void Invalidate() { lock (SyncRoot) generation++; }
        public void RequireCurrent(int expected, long absolute)
        {
            lock (SyncRoot)
            {
                if (closed || generation != expected) throw new OperationCanceledException("ObservationAdmissionClosed");
                long time = now();
                if ((absolute != 0 && time >= absolute) || (deadline != 0 && time >= deadline))
                    throw new TimeoutException("ObservationDeadlineExceeded");
            }
        }
        public ObservationV3Invocation Prepare(string id, long absolute)
        {
            lock (SyncRoot)
            {
                RequireCurrent(generation, absolute);
                if (claims.ContainsKey(id)) throw new IOException("InvocationAlreadyPrepared:" + id);
                var claim = new ObservationV3Invocation(id, generation, absolute); claims.Add(id, claim); return claim;
            }
        }
        public bool TryCommitInvocation(ObservationV3Invocation claim)
        {
            lock (SyncRoot)
            {
                ObservationV3Invocation current;
                if (claim == null || !claims.TryGetValue(claim.OperationId, out current) || !object.ReferenceEquals(current, claim) || claim.InvocationCommitted) return false;
                try { RequireCurrent(claim.Generation, claim.Deadline); } catch (OperationCanceledException) { return false; } catch (TimeoutException) { return false; }
                claim.InvocationCommitted = true; return true;
            }
        }
        // Production OS adapters and deterministic tests use this same executor.
        public T Execute<T>(ObservationV3Invocation claim, Action prepare, Func<T> invoke)
        {
            prepare();
            if (!TryCommitInvocation(claim)) throw new OperationCanceledException("InvocationCommitDenied:" + claim.OperationId);
            return InvokeCommitted(claim, invoke);
        }
        public T InvokeCommitted<T>(ObservationV3Invocation claim, Func<T> invoke)
        {
            lock (SyncRoot)
            {
                if (!claim.InvocationCommitted || claim.InvocationEntered) throw new IOException("InvalidInvocationClaim");
                claim.InvocationEntered = true;
            }
            // Once committed, cancellation cannot establish zero invocations.
            T value = invoke();
            lock (SyncRoot) claim.Returned = true;
            return value;
        }
        public ObservationV3InvocationFact[] Facts()
        {
            lock (SyncRoot)
            {
                var result = new List<ObservationV3InvocationFact>();
                foreach (var claim in claims.Values) result.Add(new ObservationV3InvocationFact { OperationId = claim.OperationId,
                    Generation = claim.Generation, Deadline = claim.Deadline, InvocationCommitted = claim.InvocationCommitted,
                    InvocationEntered = claim.InvocationEntered, Returned = claim.Returned,
                    Outcome = !claim.InvocationCommitted ? "NotCommitted" : claim.Returned ? "Returned" : "Uncertain" });
                return result.ToArray();
            }
        }
        public ObservationV3Invocation[] Snapshot() { lock (SyncRoot) return new List<ObservationV3Invocation>(claims.Values).ToArray(); }
    }
}
