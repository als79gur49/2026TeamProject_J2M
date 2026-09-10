using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Game.Platform.Steam
{
    public interface ISteamObservationAchievementsReader
    {
        SteamObservationAchievement ReadAchievement(string target);
    }
    public sealed class SteamObservationAchievement
    {
        public string Target;
        public bool ReadSucceeded;
        public bool? Achieved;
    }
    internal sealed class SteamObservationPermitSnapshot
    {
        internal string RunId, Role, BindingHash, ClientIdentity;
        internal int SelfPid, Generation;
        internal long SelfStartTicks;
        internal Func<bool> IsCurrent;
    }
    internal sealed class ValidatedObservationNativePermit
    {
        private readonly int thread, pid, generation;
        private readonly long start;
        private readonly Func<bool> current;
        private readonly string run, role, binding, client;
        private bool consumed, revoked;
        private ValidatedObservationNativePermit(SteamObservationPermitSnapshot s)
        {
            thread = Thread.CurrentThread.ManagedThreadId; pid = s.SelfPid; start = s.SelfStartTicks;
            generation = s.Generation; current = s.IsCurrent;
            run = s.RunId; role = s.Role; binding = s.BindingHash; client = s.ClientIdentity;
        }
        internal static ValidatedObservationNativePermit Issue(SteamObservationPermitSnapshot s)
        {
            SteamOverlayObservationAccess.RequireMainThread();
            Guid run;
            if (s == null || !Guid.TryParseExact(s.RunId, "N", out run) ||
                (s.Role != "OriginObserver" && s.Role != "ReplacementObserver") || s.Generation <= 0 ||
                s.BindingHash == null || s.BindingHash.Length != 64 || !s.BindingHash.All(c => "0123456789abcdef".Contains(c)) ||
                string.IsNullOrWhiteSpace(s.ClientIdentity) || s.IsCurrent == null || !s.IsCurrent() ||
                SteamOverlayObservationAccess.NativeStartupAttempted || !SteamOverlayObservationAccess.Requested)
                throw new InvalidOperationException("Invalid observation native permit snapshot.");
            using (var self = Process.GetCurrentProcess())
                if (self.Id != s.SelfPid || self.StartTime.ToUniversalTime().Ticks != s.SelfStartTicks)
                    throw new InvalidOperationException("Observation permit self mismatch.");
            return new ValidatedObservationNativePermit(s);
        }
        internal void ValidateBinding(SteamObservationPermitSnapshot expected)
        {
            SteamOverlayObservationAccess.RequireMainThread();
            if (expected == null || expected.RunId != run || expected.Role != role || expected.BindingHash != binding || expected.ClientIdentity != client ||
                expected.Generation != generation || expected.SelfPid != pid || expected.SelfStartTicks != start || !current())
                throw new InvalidOperationException("Observation native permit owner binding mismatch.");
        }
        internal void Consume()
        {
            SteamOverlayObservationAccess.RequireMainThread();
            using (var self = Process.GetCurrentProcess())
                if (revoked || consumed || generation <= 0 || thread != Thread.CurrentThread.ManagedThreadId ||
                    pid != self.Id || start != self.StartTime.ToUniversalTime().Ticks || !current())
                    throw new InvalidOperationException("Observation native permit expired or already consumed.");
            consumed = true;
        }
        internal void Revoke() { revoked = true; }
    }
    internal static class SteamObservationPermitIssuer
    {
        internal static ValidatedObservationNativePermit Issue(SteamObservationPermitSnapshot snapshot)
        { return ValidatedObservationNativePermit.Issue(snapshot); }
    }
}
