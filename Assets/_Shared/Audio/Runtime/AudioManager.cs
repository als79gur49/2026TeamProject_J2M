using System;
using UnityEngine;

namespace Game.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour, IAudioService, IAudioSettingsService
    {
        [SerializeField] [Min(1)] private int initialPoolSize = 8;
        [SerializeField] [Min(1)] private int maxPoolSize = 24;

        private AudioMixingService mixingService;
        private IAudioSettingsPersistenceStore persistenceStoreOverride;
        private AudioPlaybackService playbackService;
        private bool runtimeInitialized;

        public void InitializeRuntime(Transform ownerRoot)
        {
            if (ownerRoot == null)
            {
                throw new ArgumentNullException(nameof(ownerRoot));
            }

            playbackService ??= new AudioPlaybackService();
            mixingService ??= new AudioMixingService(persistenceStoreOverride ?? new PlayerPrefsAudioSettingsStore());
            playbackService.Initialize(ownerRoot, initialPoolSize, maxPoolSize);
            playbackService.ApplyLiveMix(mixingService);
            runtimeInitialized = true;
        }

        public AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default)
        {
            ThrowIfNotInitialized();
            return playbackService.Play2D(definition, context, mixingService);
        }

        public AudioPlaybackHandle PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context = default)
        {
            ThrowIfNotInitialized();
            return playbackService.PlayAttached(definition, owner, slot, context, mixingService);
        }

        public AudioPlaybackHandle PlayBgm(AudioDefinition definition)
        {
            ThrowIfNotInitialized();
            return playbackService.PlayBgm(definition, mixingService);
        }

        public void Stop(AudioPlaybackHandle handle)
        {
            ThrowIfNotInitialized();
            playbackService.Stop(handle);
        }

        public void StopBgm()
        {
            ThrowIfNotInitialized();
            playbackService.StopBgm();
        }

        public AudioSettingsSnapshot ReadSettings()
        {
            ThrowIfNotInitialized();
            return mixingService.Snapshot;
        }

        public void SetChannelVolume(AudioChannel channel, float volume)
        {
            ThrowIfNotInitialized();
            mixingService.SetChannelVolume(channel, volume);
            playbackService.ApplyLiveMix(mixingService);
        }

        public void SetChannelMuted(AudioChannel channel, bool isMuted)
        {
            ThrowIfNotInitialized();
            mixingService.SetChannelMuted(channel, isMuted);
            playbackService.ApplyLiveMix(mixingService);
        }

        public void FlushSettings()
        {
            ThrowIfNotInitialized();
            mixingService.FlushPersistence();
        }

        private void Update()
        {
            if (!runtimeInitialized || playbackService == null)
            {
                return;
            }

            playbackService.Tick();
        }

        private void OnDestroy()
        {
            playbackService?.Dispose();
        }

        private void ThrowIfNotInitialized()
        {
            if (!runtimeInitialized)
            {
                throw new InvalidOperationException(
                    "AudioManager must be initialized by AudioRuntimeRoot. Add AudioRuntimeInstaller to the canonical bootstrap root.");
            }
        }

        internal void SetPersistenceStoreOverrideForTesting(IAudioSettingsPersistenceStore store)
        {
            persistenceStoreOverride = store;
        }

        internal AudioLivePlaybackDebugSnapshot[] CaptureLivePlaybackSnapshots()
        {
            return playbackService?.CaptureLivePlaybackSnapshots() ?? Array.Empty<AudioLivePlaybackDebugSnapshot>();
        }

        internal int CaptureLivePlaybackCount()
        {
            return playbackService?.LivePlaybackCount ?? 0;
        }
    }
}
