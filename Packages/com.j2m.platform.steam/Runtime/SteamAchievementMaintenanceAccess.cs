using System;
using UnityEngine;

namespace Game.Platform.Steam
{
    /// <summary>Exclusive callback access before the normal publication session starts.</summary>
    public static class SteamAchievementMaintenanceAccess
    {
        private static SteamPlatformRuntime runtime;
        public static bool IsDeferred { get; private set; }
        public static bool ResetTrial { get; private set; }
        public static void InhibitForResetTrial()
        {
            ResetTrial = true;
            IsDeferred = true;
            runtime?.StopPublication();
        }
        public static bool IsAvailable => runtime != null && runtime.MaintenanceAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { runtime = null; IsDeferred = false; ResetTrial = false; }

        public static void DeferAutomaticPublication()
        {
            if (runtime != null) throw new InvalidOperationException("Steam runtime already started.");
            IsDeferred = true;
        }

        internal static void Register(SteamPlatformRuntime value) => runtime = value;
        internal static void Unregister(SteamPlatformRuntime value)
        {
            if (ReferenceEquals(runtime, value)) runtime = null;
        }

        public static SteamAchievementMaintenanceLease Acquire(
            Action<SteamStatsStoredObservation> stats,
            Action<SteamAchievementStoredObservation> achievements)
        {
            if (runtime == null) throw new InvalidOperationException("Steam runtime is not available.");
            return runtime.AcquireMaintenance(stats, achievements);
        }

        public static bool StartPublication() => runtime != null && runtime.StartDeferredPublication();
        public static void StopPublication() => runtime?.StopPublication();
    }

    public sealed class SteamAchievementMaintenanceLease : IDisposable
    {
        private Action<bool> release;
        private bool failed;
        public ISteamAchievementApi Api { get; }
        internal SteamAchievementMaintenanceLease(ISteamAchievementApi api, Action<bool> release)
        { Api = api; this.release = release; }
        public void MarkFailed() => failed = true;
        public void Dispose()
        {
            var action = release;
            release = null;
            action?.Invoke(failed);
        }
    }
}
