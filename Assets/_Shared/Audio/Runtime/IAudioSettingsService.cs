namespace Game.Shared.Audio
{
    public interface IAudioSettingsService
    {
        AudioSettingsSnapshot ReadSettings();

        void SetChannelVolume(AudioChannel channel, float volume);

        void SetChannelMuted(AudioChannel channel, bool isMuted);

        void FlushSettings();
    }
}
