// Also compiled by the isolated Windows PowerShell helper. Keep this file Unity-free and C# 5 compatible.
using System;

namespace Game.Exhibition.RestartExperiment
{
    public enum Trial { FullCycle = 3 }

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
        void Cleanup();
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
                if (failure != null) throw failure;
            }
        }

        private static Exception Combine(Exception first, Exception next)
        { return first == null ? next : new AggregateException("Cycle failed; first error is preserved.", first, next); }

        private static void Execute(ICycleEnvironment env, Trial trial)
        {
            var ready = PrepareClient(env, trial);
            env.EnsureNewSteamUnchanged(); env.EnsureNoOtherGame(); ready.Remaining();
            env.StartGame(ready);
        }

        // The caller owns the lock and cleanup through the final game submission.
        public static Deadline PrepareClient(ICycleEnvironment env, Trial trial)
        {

                Wait(env, new Deadline(() => env.Milliseconds, env.Milliseconds + 30000, "ParentExitTimeout"), env.ParentAlive);

                env.EnsureNoOtherGame();
                var shutdown = new Deadline(() => env.Milliseconds, env.Milliseconds + 60000, "SteamExitTimeout");
                if (!env.OriginalSteamAlive()) throw new InvalidOperationException("Original Steam already exited; inspect manually.");
                shutdown.Remaining();
                env.RequestSteamExit(shutdown);
                Wait(env, shutdown, () => { env.ShutdownCommandAlive(); return env.OriginalSteamAlive(); });

                Wait(env, new Deadline(() => env.Milliseconds, shutdown.Expires, "ShutdownCommandExitTimeout"), () =>
                { env.EnsureSteamExited(); return env.ShutdownCommandAlive(); });
                env.EnsureSteamExited(); shutdown.Remaining();
                env.EnsureNoOtherGame();
                shutdown.Remaining();
                env.StartSteam();
                var deadline = new Deadline(() => env.Milliseconds, env.Milliseconds + 120000, "Steam readiness timed out.");
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
