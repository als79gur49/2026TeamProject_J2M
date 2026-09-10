// Also compiled by the isolated Windows PowerShell helper. Keep this file Unity-free and C# 5 compatible.
using System;

namespace Game.Exhibition.RestartExperiment
{
    public enum RestartPurpose { GameOnly = 0, CompletedReset = 1 }
    public enum HandoffResult { Unknown = 0, NotStarted = 1, Accepted = 2 }
    public enum HandoffState { Idle, Launching, LaunchFailed, HandedOff, HandoffUncertain }
    public enum Trial { GameOnly, Survival, Probe, FullCycle }

    public sealed class RestartIdentity
    {
        public readonly uint AppId;
        public readonly ulong SteamId;
        public RestartIdentity(uint appId, ulong steamId) { AppId = appId; SteamId = steamId; }
    }

    public interface IRestartHandoff
    {
        void ValidateAvailable(RestartPurpose purpose);
        HandoffResult StartHandoff(RestartIdentity identity, RestartPurpose purpose);
        void RequestExit();
    }

    /// <summary>Experimental contract only; the product reset service does not use this type yet.</summary>
    public sealed class Handoff
    {
        private readonly IRestartHandoff adapter;
        private readonly Action<Exception> log;
        private readonly Func<RestartIdentity> captureIdentity;
        private RestartIdentity identity;
        private bool notifying;
        public event Action Changed;
        public HandoffState State { get; private set; }
        public string Error { get; private set; }
        public bool IsBusy { get { return State == HandoffState.Launching || State == HandoffState.HandedOff || State == HandoffState.HandoffUncertain; } }
        public bool BlocksMenu { get { return State != HandoffState.Idle; } }

        public Handoff(IRestartHandoff adapter, Func<RestartIdentity> captureIdentity, Action<Exception> log)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            if (captureIdentity == null) throw new ArgumentNullException("captureIdentity");
            this.adapter = adapter; this.captureIdentity = captureIdentity; this.log = log;
        }

        public void Start(RestartPurpose purpose)
        {
            if (State != HandoffState.Idle && State != HandoffState.LaunchFailed) return;
            State = HandoffState.Launching; Error = null; Notify();
            try
            {
                ValidatePurpose(purpose); adapter.ValidateAvailable(purpose);
                if (identity == null) identity = captureIdentity();
                if (identity == null || identity.AppId == 0 || identity.SteamId == 0) throw new InvalidOperationException("Valid current identity required.");
            }
            catch (Exception e) { State = HandoffState.LaunchFailed; Fail(e); return; }
            HandoffResult result;
            try { result = adapter.StartHandoff(identity, purpose); }
            catch (Exception e) { State = HandoffState.HandoffUncertain; Fail(e); return; }
            if (result == HandoffResult.NotStarted)
            {
                State = HandoffState.LaunchFailed; Fail(new InvalidOperationException("Helper was not created.")); return;
            }
            if (result != HandoffResult.Accepted)
            {
                State = HandoffState.HandoffUncertain; Fail(new InvalidOperationException("Helper creation is uncertain. Do not retry.")); return;
            }
            State = HandoffState.HandedOff; Notify();
            try { adapter.RequestExit(); }
            catch (Exception e) { Fail(e); }
        }

        public void FailMenuInitialization(string reason)
        {
            // Menu errors are observations, never cancellation of an accepted/uncertain handoff.
            Error = reason; Notify();
        }

        public static void ValidatePurpose(RestartPurpose purpose)
        {
            if (purpose != RestartPurpose.GameOnly && purpose != RestartPurpose.CompletedReset)
                throw new ArgumentOutOfRangeException("purpose");
        }

        private void Fail(Exception e) { Error = e.Message; Log(e); Notify(); }
        private void Log(Exception e) { try { if (log != null) log(e); } catch (Exception) { } }
        private void Notify()
        {
            if (notifying) return;
            var handlers = Changed;
            if (handlers == null) return;
            notifying = true;
            try
            {
                foreach (Action handler in handlers.GetInvocationList())
                    try { handler(); } catch (Exception e) { Log(e); }
            }
            finally { notifying = false; }
        }
    }

    public sealed class Deadline
    {
        private readonly Func<long> clock;
        public readonly long Expires;
        public readonly string Error;
        public Deadline(Func<long> clock, long expires, string error)
        { this.clock = clock; Expires = expires; Error = error; }
        public long Remaining()
        {
            long remaining = Expires - clock();
            if (remaining <= 0) throw new TimeoutException(Error);
            return remaining;
        }
        public Deadline Limit(int milliseconds)
        {
            if (milliseconds <= 0) throw new ArgumentOutOfRangeException("milliseconds");
            long now = clock();
            if (now >= Expires) throw new TimeoutException(Error);
            return new Deadline(clock, Math.Min(Expires, checked(now + milliseconds)), "Probe timed out; no further launch.");
        }
    }

    public interface ICycleEnvironment
    {
        long Milliseconds { get; }
        void Validate();
        IDisposable AcquireCycleLock();
        bool ParentAlive();
        void EnsureNoOtherGame();
        bool OriginalSteamAlive();
        void RequestSteamExit(Deadline deadline);
        bool ShutdownCommandAlive();
        void EnsureSteamExited();
        void StartSteam();
        void EnsureNewSteamUnchanged();
        bool ProbeReady(Deadline deadline);
        void StartGame(Deadline deadline);
        void Delay(int milliseconds);
        void Record(string stage);
        void Cleanup();
        void RecordTerminal(Exception failure);
    }

    /// <summary>Same implementation runs against fakes and the Windows helper. No persistent retry state.</summary>
    public static class Cycle
    {
        public static void Run(ICycleEnvironment env, Trial trial)
        {
            if (!Enum.IsDefined(typeof(Trial), trial)) throw new ArgumentOutOfRangeException("trial");
            env.Validate();
            using (var lease = env.AcquireCycleLock())
            {
                if (lease == null) throw new InvalidOperationException("Cycle lock unavailable.");
                Exception failure = null;
                try { Execute(env, trial); }
                catch (Exception e) { failure = e; }
                try { env.Cleanup(); }
                catch (Exception e) { failure = Combine(failure, e); }
                try { env.RecordTerminal(failure); }
                catch (Exception e) { failure = Combine(failure, e); }
                if (failure != null) throw failure;
            }
        }

        private static Exception Combine(Exception first, Exception next)
        { return first == null ? next : new AggregateException("Cycle failed; first error is preserved.", first, next); }

        private static void Execute(ICycleEnvironment env, Trial trial)
        {
            if (trial == Trial.GameOnly)
            {
                env.Record("LockAcquired");
                Wait(env, new Deadline(() => env.Milliseconds, env.Milliseconds + 30000, "ParentExitTimeout"), env.ParentAlive);
                env.Record("ParentExited"); env.EnsureNoOtherGame(); env.StartGame(null); env.Record("GameCreated"); return;
            }
            var ready = PrepareClient(env, trial);
            if (trial != Trial.FullCycle) return;
            env.EnsureNewSteamUnchanged(); env.EnsureNoOtherGame(); ready.Remaining();
            env.StartGame(ready); env.Record("GameCreated");
        }

        // Does not own a lock, cleanup, game submission, or terminal evidence.
        // Runtime-1 holds its same-thread mutex until its observation host terminates.
        public static Deadline PrepareClient(ICycleEnvironment env, Trial trial)
        {
                env.Record("LockAcquired");
                Wait(env, new Deadline(() => env.Milliseconds, env.Milliseconds + 30000, "ParentExitTimeout"), env.ParentAlive);
                env.Record("ParentExited");
                env.EnsureNoOtherGame();
                if (trial == Trial.GameOnly) throw new ArgumentException("Client preparation requires a restart trial.");
                var shutdown = new Deadline(() => env.Milliseconds, env.Milliseconds + 60000, "SteamExitTimeout");
                if (!env.OriginalSteamAlive()) throw new InvalidOperationException("Original Steam already exited; inspect manually.");
                shutdown.Remaining();
                env.RequestSteamExit(shutdown); env.Record("SteamExitRequested");
                Wait(env, shutdown, () => { env.ShutdownCommandAlive(); return env.OriginalSteamAlive(); });
                env.Record("OriginalSteamExited");
                Wait(env, new Deadline(() => env.Milliseconds, shutdown.Expires, "ShutdownCommandExitTimeout"), () =>
                { env.EnsureSteamExited(); return env.ShutdownCommandAlive(); });
                env.EnsureSteamExited(); shutdown.Remaining();
                env.EnsureNoOtherGame();
                shutdown.Remaining();
                if (trial == Trial.Survival) { env.Record("SurvivalObservationComplete"); return shutdown; }
                env.StartSteam(); env.Record("SteamCreated");
                var deadline = new Deadline(() => env.Milliseconds, env.Milliseconds + 120000, "ReadinessTimeout: inspect probe-attempts.jsonl.");
                while (true)
                {
                    env.EnsureNoOtherGame(); env.EnsureNewSteamUnchanged();
                    deadline.Remaining();
                    bool ready = env.ProbeReady(deadline.Limit(10000));
                    deadline.Remaining();
                    env.EnsureNewSteamUnchanged(); env.EnsureNoOtherGame();
                    long remaining = deadline.Remaining();
                    if (ready) break;
                    env.Delay((int)Math.Min(1000, remaining));
                }
                env.Record("ProbeReadyAndExited");
                deadline.Remaining();
                return deadline;
        }

        private static void Wait(ICycleEnvironment env, Deadline deadline, Func<bool> alive)
        {
            while (true)
            {
                bool observedAlive = alive();
                env.EnsureNoOtherGame();
                long remaining = deadline.Remaining();
                if (!observedAlive) return;
                env.Delay((int)Math.Min(100, remaining));
            }
        }
    }
}
