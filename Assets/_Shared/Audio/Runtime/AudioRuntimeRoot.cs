using System;
using System.Linq;
using UnityEngine;

namespace Game.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioRuntimeRoot : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;

        public AudioManager AudioManager => audioManager;

        public IAudioService AudioService => audioManager;

        public IAudioSettingsService AudioSettingsService => audioManager;

        public void InitializeRuntime()
        {
            var managers = GetComponentsInChildren<AudioManager>(includeInactive: true);
            if (managers.Length > 1)
            {
                throw new InvalidOperationException("AudioRuntimeRoot cannot contain duplicate AudioManager instances.");
            }

            audioManager = managers.SingleOrDefault();
            if (audioManager == null)
            {
                audioManager = gameObject.AddComponent<AudioManager>();
            }

            audioManager.InitializeRuntime(transform);
        }
    }
}
