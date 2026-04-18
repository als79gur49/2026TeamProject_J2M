using UnityEngine;

namespace Game.Shared.Audio
{
    internal sealed class AudioMixingService
    {
        private readonly IAudioSettingsPersistenceStore persistenceStore;
        private bool hasDirtySnapshot;
        private AudioSettingsSnapshot snapshot;

        public AudioMixingService(IAudioSettingsPersistenceStore persistenceStore)
        {
            this.persistenceStore = persistenceStore ?? new PlayerPrefsAudioSettingsStore();
            snapshot = this.persistenceStore.Load();
        }

        public AudioSettingsSnapshot Snapshot => snapshot;

        public bool HasDirtySnapshot => hasDirtySnapshot;

        public void SetChannelVolume(AudioChannel channel, float volume)
        {
            var updatedState = new AudioChannelState(volume, snapshot.GetChannelState(channel).IsMuted);
            SetChannelState(channel, updatedState);
        }

        public void SetChannelMuted(AudioChannel channel, bool isMuted)
        {
            var updatedState = new AudioChannelState(snapshot.GetChannelState(channel).Volume, isMuted);
            SetChannelState(channel, updatedState);
        }

        public float ResolvePlaybackVolume(AudioChannel channel, float baseClipVolume)
        {
            var masterFactor = snapshot.Master.EffectiveFactor;
            if (channel == AudioChannel.Master)
            {
                return Mathf.Clamp01(baseClipVolume * masterFactor);
            }

            var leafFactor = snapshot.GetChannelState(channel).EffectiveFactor;
            return Mathf.Clamp01(baseClipVolume * masterFactor * leafFactor);
        }

        public void FlushPersistence()
        {
            if (!hasDirtySnapshot)
            {
                return;
            }

            persistenceStore.Save(snapshot);
            hasDirtySnapshot = false;
        }

        private void SetChannelState(AudioChannel channel, AudioChannelState state)
        {
            snapshot = snapshot.WithChannelState(channel, state);
            hasDirtySnapshot = true;
        }
    }
}
