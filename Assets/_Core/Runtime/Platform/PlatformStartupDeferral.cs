using System;
using System.Threading;
using UnityEngine;

namespace Game.Platform.Runtime
{
    public enum PlatformStartupState { Waiting, ReleaseRequested, Initializing, Running, Cancelled, Failed, Stopped }

    /// <summary>A single application-start capability. It cannot reset selection or retry initialization.</summary>
    public static class PlatformStartupDeferral
    {
        private static PlatformStartupHandle current;
        private static int ownerThread;
        private static bool registrationFinished;
        internal static bool IsDeferred => current != null;

        internal static void Reset()
        {
            current = null;
            registrationFinished = false;
            ownerThread = Thread.CurrentThread.ManagedThreadId;
        }

        public static PlatformStartupHandle Request()
        {
            RequireThread();
            if (current != null || registrationFinished || PlatformRuntimeRegistry.IsSealed || PlatformRuntimeApplicationHost.HasCanonicalHost)
                throw new InvalidOperationException("Startup deferral must be requested once before registration closes.");
            current = new PlatformStartupHandle(ownerThread);
            return current;
        }

        internal static void RegistrationFinished()
        {
            registrationFinished = true;
            if (current == null) return;
            var go = new GameObject("[PlatformStartupDeferral]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PlatformStartupDeferralPump>();
        }

        internal static void Pump()
        {
            RequireThread();
            if (registrationFinished && current != null) current.Pump();
        }

        private static void RequireThread()
        {
            if (ownerThread == 0 || ownerThread != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Startup capability requires the application main thread.");
        }
    }

    public sealed class PlatformStartupHandle
    {
        private readonly int thread;
        private bool stopRequested;
        private PlatformRuntimeApplicationHost host;
        internal PlatformStartupHandle(int thread) { this.thread = thread; }
        public PlatformStartupState State { get; private set; } = PlatformStartupState.Waiting;
        public bool IsAvailable => State == PlatformStartupState.Running && host != null && host.Availability.IsAvailable;
        public void Release()
        {
            RequireThread();
            if (State != PlatformStartupState.Waiting) throw new InvalidOperationException("Startup release is single-use.");
            State = PlatformStartupState.ReleaseRequested;
        }
        public void Cancel()
        {
            RequireThread();
            stopRequested = true;
            // During synchronous/reentrant Init the candidate has not been handed back yet.
            // Do not consume the canonical shutdown latch with a null active runtime.
            if (State == PlatformStartupState.Initializing) return;
            if (host != null) host.ShutdownOnce();
            State = PlatformStartupState.Cancelled;
        }
        public void ShutdownOwnedRuntime()
        {
            RequireThread();
            stopRequested = true;
            if (State == PlatformStartupState.Initializing) return;
            if (host != null) host.ShutdownOnce();
            State = PlatformStartupState.Stopped;
        }
        internal void Pump()
        {
            if (State != PlatformStartupState.ReleaseRequested || stopRequested) return;
            State = PlatformStartupState.Initializing;
            try
            {
                host = PlatformRuntimeBootstrap.ReleaseDeferredStartup();
                if (stopRequested)
                {
                    if (host != null) host.ShutdownOnce();
                    State = PlatformStartupState.Cancelled;
                }
                else State = host != null && host.Availability.IsAvailable ? PlatformStartupState.Running : PlatformStartupState.Failed;
            }
            catch
            {
                State = PlatformStartupState.Failed;
                throw;
            }
        }
        private void RequireThread()
        {
            if (thread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Wrong startup owner thread.");
        }
    }
    internal sealed class PlatformStartupDeferralPump : MonoBehaviour
    {
        private void Update() { PlatformStartupDeferral.Pump(); }
    }
}
