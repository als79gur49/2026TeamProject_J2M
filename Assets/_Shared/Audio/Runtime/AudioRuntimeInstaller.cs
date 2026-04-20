using System;
using System.Linq;
using UnityEngine;

namespace Game.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioRuntimeInstaller : MonoBehaviour
    {
        [SerializeField] private bool installOnAwake = true;
        // This installer remains the explicit same-root access seam for gameplay and UI even
        // when it is exposing a persistent runtime created elsewhere.
        [SerializeField] private AudioRuntimeInstallerBindingMode bindingMode = AudioRuntimeInstallerBindingMode.LocalOnly;
        [SerializeField] private AudioRuntimeRoot runtimeRoot;

        public AudioRuntimeRoot RuntimeRoot => runtimeRoot;

        public AudioRuntimeInstallerBindingMode BindingMode => bindingMode;

        public IAudioService AudioService => runtimeRoot?.AudioService;

        public IAudioSettingsService AudioSettingsService => runtimeRoot?.AudioSettingsService;

        private void Awake()
        {
            if (installOnAwake)
            {
                Install();
            }
        }

        public void Install()
        {
            if (bindingMode == AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime &&
                AudioRuntimeExternalRootRegistry.TryGetRegisteredPersistentRuntime(out var registeredRuntimeRoot))
            {
                runtimeRoot = registeredRuntimeRoot;
                runtimeRoot.InitializeRuntime();
                return;
            }

            var runtimeRoots = GetComponentsInChildren<AudioRuntimeRoot>(includeInactive: true);
            if (runtimeRoots.Length > 1)
            {
                throw new InvalidOperationException("AudioRuntimeInstaller cannot own multiple AudioRuntimeRoot instances.");
            }

            runtimeRoot = runtimeRoots.SingleOrDefault();
            if (runtimeRoot == null)
            {
                var rootObject = new GameObject("AudioRuntimeRoot");
                rootObject.transform.SetParent(transform, worldPositionStays: false);
                runtimeRoot = rootObject.AddComponent<AudioRuntimeRoot>();
            }

            runtimeRoot.InitializeRuntime();
        }
    }
}
