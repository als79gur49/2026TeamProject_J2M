using UnityEngine;

namespace Game.Platform.Runtime
{
    [DisallowMultipleComponent]
    internal sealed class PlatformRuntimeApplicationHost : MonoBehaviour
    {
        internal const string HostObjectName = "[PlatformRuntimeApplicationHost]";

        private static PlatformRuntimeApplicationHost current;
        private static bool duplicateDiagnosticReported;

        private PlatformRuntimeLifecycle lifecycle;
        private bool ownsLifecycle;

        internal static bool HasCanonicalHost => current != null;

        internal static PlatformRuntimeApplicationHost CurrentForTests => current;

        internal PlatformRuntimeSelectionResult Selection => lifecycle?.Selection ?? default;

        internal PlatformProviderId ProviderId => lifecycle?.ProviderId ?? default;

        internal PlatformAvailability Availability => lifecycle?.Availability ??
            PlatformAvailability.Unavailable("Platform runtime host has not been configured.");

        internal PlatformInitializationResult InitializationResult =>
            lifecycle?.InitializationResult ?? default;

        internal bool OwnsLifecycle => ownsLifecycle;

        internal bool InitializationAttempted => lifecycle?.InitializationAttempted ?? false;

        internal bool TickEnabled => lifecycle?.TickEnabled ?? false;

        internal bool ShutdownAttempted => lifecycle?.ShutdownAttempted ?? false;

        internal static PlatformRuntimeApplicationHost CreateOrGet(
            PlatformRuntimeSelectionResult selection)
        {
            if (current == null)
            {
                var hostObject = new GameObject(HostObjectName);
                current = hostObject.AddComponent<PlatformRuntimeApplicationHost>();
            }

            current.ConfigureAndInitialize(selection);
            return current;
        }

        internal bool ConfigureAndInitialize(PlatformRuntimeSelectionResult selection)
        {
            if (!ownsLifecycle || lifecycle != null)
            {
                return false;
            }

            gameObject.name = HostObjectName;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            lifecycle = new PlatformRuntimeLifecycle(selection);
            lifecycle.InitializeOnce();
            return true;
        }

        internal void TickOnceForTests()
        {
            lifecycle?.TickOnce();
        }

        internal void ShutdownOnce()
        {
            if (!ownsLifecycle)
            {
                return;
            }

            lifecycle?.ShutdownOnce();
        }

        internal static void ResetStaticOwnerForSubsystemRegistration()
        {
            current = null;
            duplicateDiagnosticReported = false;
        }

        private void Awake()
        {
            if (current != null && current != this)
            {
                ownsLifecycle = false;
                if (!duplicateDiagnosticReported)
                {
                    duplicateDiagnosticReported = true;
                    Debug.LogError(
                        "Duplicate platform runtime application host was rejected; " +
                        "the canonical host retains lifecycle ownership.");
                }

                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }

                return;
            }

            current = this;
            ownsLifecycle = true;
            gameObject.name = HostObjectName;
        }

        private void Update()
        {
            if (ownsLifecycle)
            {
                lifecycle?.TickOnce();
            }
        }

        private void OnApplicationQuit()
        {
            ShutdownOnce();
        }

        private void OnDestroy()
        {
            ShutdownOnce();
            if (current == this)
            {
                current = null;
            }

            ownsLifecycle = false;
        }
    }
}
