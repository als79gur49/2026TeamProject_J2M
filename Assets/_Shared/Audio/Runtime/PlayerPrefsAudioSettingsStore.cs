using UnityEngine;

namespace Game.Shared.Audio
{
    internal interface IAudioSettingsPersistenceStore
    {
        AudioSettingsSnapshot Load();

        void Save(AudioSettingsSnapshot snapshot);
    }

    internal sealed class PlayerPrefsAudioSettingsStore : IAudioSettingsPersistenceStore
    {
        private const string KeyPrefix = "settings.audio.";

        public AudioSettingsSnapshot Load()
        {
            return new AudioSettingsSnapshot(
                LoadChannel(AudioChannel.Master),
                LoadChannel(AudioChannel.Bgm),
                LoadChannel(AudioChannel.Sfx),
                LoadChannel(AudioChannel.Ui),
                LoadChannel(AudioChannel.Voice),
                LoadChannel(AudioChannel.Ambience));
        }

        public void Save(AudioSettingsSnapshot snapshot)
        {
            SaveChannel(AudioChannel.Master, snapshot.Master);
            SaveChannel(AudioChannel.Bgm, snapshot.Bgm);
            SaveChannel(AudioChannel.Sfx, snapshot.Sfx);
            SaveChannel(AudioChannel.Ui, snapshot.Ui);
            SaveChannel(AudioChannel.Voice, snapshot.Voice);
            SaveChannel(AudioChannel.Ambience, snapshot.Ambience);
            PlayerPrefs.Save();
        }

        private static AudioChannelState LoadChannel(AudioChannel channel)
        {
            var volume = PlayerPrefs.GetFloat(BuildVolumeKey(channel), 1f);
            var isMuted = PlayerPrefs.GetInt(BuildMutedKey(channel), 0) != 0;
            return new AudioChannelState(volume, isMuted);
        }

        private static void SaveChannel(AudioChannel channel, AudioChannelState state)
        {
            PlayerPrefs.SetFloat(BuildVolumeKey(channel), state.Volume);
            PlayerPrefs.SetInt(BuildMutedKey(channel), state.IsMuted ? 1 : 0);
        }

        private static string BuildVolumeKey(AudioChannel channel)
        {
            return KeyPrefix + channel.ToString().ToLowerInvariant() + ".volume";
        }

        private static string BuildMutedKey(AudioChannel channel)
        {
            return KeyPrefix + channel.ToString().ToLowerInvariant() + ".muted";
        }
    }
}
