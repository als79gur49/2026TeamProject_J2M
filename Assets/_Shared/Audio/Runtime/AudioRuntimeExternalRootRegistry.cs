using System;
using UnityEngine;

namespace Game.Shared.Audio
{
    internal readonly struct AudioRuntimeExternalRootRegistryDebugSnapshot
    {
        public AudioRuntimeExternalRootRegistryDebugSnapshot(
            bool hasRegisteredRuntime,
            AudioRuntimeRoot runtimeRoot,
            string ownerTypeName)
        {
            HasRegisteredRuntime = hasRegisteredRuntime;
            RuntimeRoot = runtimeRoot;
            OwnerTypeName = ownerTypeName ?? string.Empty;
        }

        public bool HasRegisteredRuntime { get; }

        public AudioRuntimeRoot RuntimeRoot { get; }

        public string OwnerTypeName { get; }
    }

    internal static class AudioRuntimeExternalRootRegistry
    {
        private static AudioRuntimeRoot registeredRuntimeRoot;
        private static object registeredOwnerToken;

        // This registry is bootstrap plumbing only. It is intentionally single-slot and read
        // only by AudioRuntimeInstaller so it cannot drift into a general-purpose locator.
        internal static void RegisterPersistentRuntime(AudioRuntimeRoot runtimeRoot, object ownerToken)
        {
            if (runtimeRoot == null)
            {
                throw new ArgumentNullException(nameof(runtimeRoot));
            }

            if (ownerToken == null)
            {
                throw new ArgumentNullException(nameof(ownerToken));
            }

            if (registeredRuntimeRoot != null)
            {
                throw new InvalidOperationException(
                    "AudioRuntimeExternalRootRegistry cannot register multiple persistent AudioRuntimeRoot instances.");
            }

            registeredRuntimeRoot = runtimeRoot;
            registeredOwnerToken = ownerToken;
        }

        internal static bool TryGetRegisteredPersistentRuntime(out AudioRuntimeRoot runtimeRoot)
        {
            runtimeRoot = registeredRuntimeRoot;
            return runtimeRoot != null;
        }

        internal static void UnregisterPersistentRuntime(AudioRuntimeRoot runtimeRoot, object ownerToken)
        {
            if (registeredRuntimeRoot == null)
            {
                return;
            }

            if (!ReferenceEquals(registeredRuntimeRoot, runtimeRoot) ||
                !ReferenceEquals(registeredOwnerToken, ownerToken))
            {
                throw new InvalidOperationException(
                    "AudioRuntimeExternalRootRegistry unregister requires the same owner token and AudioRuntimeRoot that performed registration.");
            }

            registeredRuntimeRoot = null;
            registeredOwnerToken = null;
        }

        internal static AudioRuntimeExternalRootRegistryDebugSnapshot CaptureDebugSnapshot()
        {
            return new AudioRuntimeExternalRootRegistryDebugSnapshot(
                registeredRuntimeRoot != null,
                registeredRuntimeRoot,
                registeredOwnerToken?.GetType().Name ?? string.Empty);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            registeredRuntimeRoot = null;
            registeredOwnerToken = null;
        }
    }
}
