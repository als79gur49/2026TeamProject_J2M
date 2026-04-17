using System;
using System.Linq;
using UnityEngine;

namespace Game.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioRuntimeInstaller : MonoBehaviour
    {
        [SerializeField] private bool installOnAwake = true;
        [SerializeField] private AudioRuntimeRoot runtimeRoot;

        public AudioRuntimeRoot RuntimeRoot => runtimeRoot;

        public IAudioService AudioService => runtimeRoot?.AudioService;

        private void Awake()
        {
            if (installOnAwake)
            {
                Install();
            }
        }

        public void Install()
        {
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
