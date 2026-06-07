using System;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    public sealed class BgmFlowCoordinator : IBgmFlowCoordinator
    {
        private readonly IBgmPlaybackPort playbackPort;
        private BgmProfile currentProfile;

        public BgmFlowCoordinator(IBgmPlaybackPort playbackPort)
        {
            this.playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
        }

        public void RequestSceneDefault(BgmProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.ValidateOrThrow();

            if (ReferenceEquals(currentProfile, profile) && !profile.RestartIfAlreadyPlaying)
            {
                return;
            }

            var transition = ResolvePlaybackTransition(profile);
            playbackPort.Play(new BgmPlaybackRequest(profile.LoopDefinition, transition));
            currentProfile = profile;
        }

        public void StopCurrent()
        {
            if (currentProfile == null)
            {
                return;
            }

            playbackPort.Stop(new BgmStopRequest(BgmPlaybackTransition.Immediate));
            currentProfile = null;
        }

        public BgmProfile GetCurrentProfile()
        {
            return currentProfile;
        }

        private static BgmPlaybackTransition ResolvePlaybackTransition(BgmProfile profile)
        {
            switch (profile.TransitionMode)
            {
                case BgmTransitionMode.Immediate:
                    return BgmPlaybackTransition.Immediate;
                case BgmTransitionMode.FadeOutIn:
                    return BgmPlaybackTransition.FadeOutIn(profile.FadeOutSeconds, profile.FadeInSeconds);
                case BgmTransitionMode.Crossfade:
                    return ResolveCrossfadeFallbackTransition(profile);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(profile),
                        profile.TransitionMode,
                        $"BgmProfile '{profile.name}' uses an unsupported transition mode.");
            }
        }

        private static BgmPlaybackTransition ResolveCrossfadeFallbackTransition(BgmProfile profile)
        {
            var fallback = profile.FadeOutSeconds > 0f || profile.FadeInSeconds > 0f
                ? BgmPlaybackTransition.FadeOutIn(profile.FadeOutSeconds, profile.FadeInSeconds)
                : BgmPlaybackTransition.Immediate;
            Debug.LogWarning(
                $"BgmProfile '{profile.name}' requests Crossfade, but single-source BGM runtime does not support Crossfade. Falling back to {fallback.Mode}.",
                profile);
            return fallback;
        }
    }
}
