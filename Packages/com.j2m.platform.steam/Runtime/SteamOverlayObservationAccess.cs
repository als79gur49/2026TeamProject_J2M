using System;
using UnityEngine;

namespace Game.Platform.Steam
{
    /// <summary>Optional identity getter implemented by the existing native adapter; never owns native Init.</summary>
    public interface ISteamObservationIdentityApi
    {
        ulong GetSteamId();
    }

    public sealed class SteamObservationIdentity
    {
        public uint AppId;
        public ulong SteamId;
        public bool LoggedOn, Valid;
    }

    /// <summary>Session-long write inhibition and access to the single native runtime.</summary>
    public static class SteamOverlayObservationAccess
    {
        private static SteamPlatformRuntime runtime;
        private static bool requested, nativeBlocked;
        private static ValidatedObservationNativePermit permit;
        private static int mainThread;
        internal static void RequireMainThread()
        {
            if (mainThread == 0 || mainThread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Observation requires the registered Unity main thread.");
        }
        internal static void Admit(ValidatedObservationNativePermit validated, SteamObservationPermitSnapshot expected)
        {
            RequireMainThread();
            if (validated == null || permit != null || NativeStartupAttempted) throw new InvalidOperationException("Observation owner already assigned.");
            validated.ValidateBinding(expected);
            permit = validated;
        }
        internal static bool ValidatedOwner => permit != null && NativeStartupAttempted;
        internal static void RevokePermit() { permit?.Revoke(); }
        public static bool SessionRequested => requested;
        public static bool Requested => requested || Game.Product.Achievements.Composition.ProductAchievementStartupControl.ObservationBuild;
        public static void BlockNativeStartup() { nativeBlocked = true; InhibitWrites(); }
        public static bool NativeStartupAttempted { get; private set; }
        public static SteamPlatformRuntime Runtime => runtime;
        public static string NativeFailure
        {
            get
            {
                if (runtime == null) return null;
                var diagnostics = runtime.Diagnostics;
                switch (diagnostics.State)
                {
                    case SteamPlatformRuntimeState.Faulted:
                    case SteamPlatformRuntimeState.Unavailable:
                    case SteamPlatformRuntimeState.ShuttingDown:
                    case SteamPlatformRuntimeState.Shutdown:
                        return "Native runtime failed: " + diagnostics.LastFailureReason;
                    default: return null;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId; permit = null; runtime = null; requested = false; nativeBlocked = false; NativeStartupAttempted = false; }

        public static bool InhibitWrites()
        {
            bool alreadyStarted = NativeStartupAttempted;
            requested = true;
            if (alreadyStarted) runtime?.StopPublication();
            return alreadyStarted;
        }

        internal static void Starting(SteamPlatformRuntime value)
        {
            if (nativeBlocked || Game.Product.Achievements.Composition.ProductAchievementStartupControl.ObservationBuild)
            {
                if (permit == null) throw new InvalidOperationException("Validated observation native permit required.");
                permit.Consume();
            }
            if ((Requested || SteamAchievementMaintenanceAccess.ResetTrial) && runtime != null && !ReferenceEquals(runtime, value))
                throw new InvalidOperationException("Observation cannot create a second native runtime.");
            NativeStartupAttempted = true; runtime = value;
        }

        public static void RequireWritesAllowed()
        {
            if (Requested) throw new InvalidOperationException("Observation session forbids Steam writes.");
        }
    }

}
