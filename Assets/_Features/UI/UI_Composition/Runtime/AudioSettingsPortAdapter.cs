using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;

namespace Game.Feature.UI.Composition
{
    internal sealed class AudioSettingsPortAdapter : IAudioSettingsPort
    {
        private readonly IAudioSettingsService audioSettingsService;

        public AudioSettingsPortAdapter(IAudioSettingsService audioSettingsService)
        {
            this.audioSettingsService = audioSettingsService ?? throw new ArgumentNullException(nameof(audioSettingsService));
        }

        public AudioSettingsPortSnapshot Read()
        {
            var snapshot = audioSettingsService.ReadSettings();
            return new AudioSettingsPortSnapshot(
                ToVisibleState(snapshot, AudioSettingsChannel.Main),
                ToVisibleState(snapshot, AudioSettingsChannel.Bgm),
                ToVisibleState(snapshot, AudioSettingsChannel.Sfx));
        }

        public void SetVolume(AudioSettingsChannel channel, float volume)
        {
            audioSettingsService.SetChannelVolume(UIAudioChannelMapper.Map(channel), volume);
        }

        public void SetMuted(AudioSettingsChannel channel, bool isMuted)
        {
            audioSettingsService.SetChannelMuted(UIAudioChannelMapper.Map(channel), isMuted);
        }

        public void Flush()
        {
            audioSettingsService.FlushSettings();
        }

        private static AudioSettingsPortChannelState ToVisibleState(
            AudioSettingsSnapshot snapshot,
            AudioSettingsChannel channel)
        {
            var state = snapshot.GetChannelState(UIAudioChannelMapper.Map(channel));
            return new AudioSettingsPortChannelState(state.Volume, state.IsMuted);
        }
    }
}
