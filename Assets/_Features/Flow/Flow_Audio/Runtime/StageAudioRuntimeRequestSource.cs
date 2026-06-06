using System;
using Game.Feature.Stages;

namespace Game.Feature.Flow.Audio
{
    public sealed class StageAudioRuntimeRequestSource
    {
        public void Apply(StageAudioResolvedData audioData, BgmRequestRouter router)
        {
            if (audioData == null)
            {
                throw new ArgumentNullException(nameof(audioData));
            }

            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }

            var gameplayBgm = audioData.GameplayBgm;
            if (gameplayBgm.Mode == StageBgmSlotMode.Profile)
            {
                router.Submit(BgmFlowRequest.ProfileRequest(
                    BgmRequestSourceKind.StageGameplay,
                    BgmRequestPriority.StageGameplay,
                    gameplayBgm.Profile));
                return;
            }

            router.Submit(BgmFlowRequest.StopRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay));
        }
    }
}
