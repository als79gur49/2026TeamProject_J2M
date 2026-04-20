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

            ResolveExecutedTransitionMode(profile);
            playbackPort.PlayImmediate(profile.LoopDefinition);
            currentProfile = profile;
        }

        public void StopCurrent()
        {
            playbackPort.StopImmediate();
            currentProfile = null;
        }

        public BgmProfile GetCurrentProfile()
        {
            return currentProfile;
        }

        private static BgmTransitionMode ResolveExecutedTransitionMode(BgmProfile profile)
        {
            if (profile.TransitionMode == BgmTransitionMode.Immediate)
            {
                return BgmTransitionMode.Immediate;
            }

            Debug.LogWarning(
                $"BgmProfile '{profile.name}' requests '{profile.TransitionMode}', but BGM flow v1 executes Immediate only. Degrading to Immediate.",
                profile);
            return BgmTransitionMode.Immediate;
        }
    }
}
