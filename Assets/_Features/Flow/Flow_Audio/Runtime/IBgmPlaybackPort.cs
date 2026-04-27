using System;
using Game.Shared.Audio;

namespace Game.Feature.Flow.Audio
{
    public interface IBgmPlaybackPort
    {
        void Play(BgmPlaybackRequest request);

        void Stop(BgmStopRequest request);
    }

    internal sealed class BgmPlaybackPortAdapter : IBgmPlaybackPort
    {
        private readonly IAudioService audioService;

        public BgmPlaybackPortAdapter(IAudioService audioService)
        {
            this.audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public void Play(BgmPlaybackRequest request)
        {
            audioService.PlayBgm(new AudioBgmPlaybackRequest(
                request.Definition,
                MapTransition(request.Transition)));
        }

        public void Stop(BgmStopRequest request)
        {
            audioService.StopBgm(new AudioBgmStopRequest(MapTransition(request.Transition)));
        }

        private static AudioBgmTransition MapTransition(BgmPlaybackTransition transition)
        {
            return transition.Mode switch
            {
                BgmExecutedTransitionMode.Immediate => AudioBgmTransition.Immediate,
                BgmExecutedTransitionMode.FadeOutIn => AudioBgmTransition.FadeOutIn(
                    transition.FadeOutSeconds,
                    transition.FadeInSeconds),
                _ => throw new ArgumentOutOfRangeException(nameof(transition), transition.Mode, "Unsupported BGM transition mode."),
            };
        }
    }
}
