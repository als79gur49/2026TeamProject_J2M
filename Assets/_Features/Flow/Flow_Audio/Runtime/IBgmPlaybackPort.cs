using System;
using Game.Shared.Audio;

namespace Game.Feature.Flow.Audio
{
    public interface IBgmPlaybackPort
    {
        // v1 keeps playback execution immediate-only. Future fade/crossfade support should be
        // added here as explicit playback capability rather than pushed into coordinator timing logic.
        void PlayImmediate(AudioDefinition definition);

        void StopImmediate();
    }

    internal sealed class BgmPlaybackPortAdapter : IBgmPlaybackPort
    {
        private readonly IAudioService audioService;

        public BgmPlaybackPortAdapter(IAudioService audioService)
        {
            this.audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public void PlayImmediate(AudioDefinition definition)
        {
            audioService.PlayBgm(definition);
        }

        public void StopImmediate()
        {
            audioService.StopBgm();
        }
    }
}
