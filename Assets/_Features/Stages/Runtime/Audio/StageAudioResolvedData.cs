using Game.Shared.Audio;

namespace Game.Feature.Stages
{
    public readonly struct StageBgmResolvedSlot
    {
        public StageBgmResolvedSlot(StageBgmSlotMode mode, BgmProfile profile)
        {
            Mode = mode;
            Profile = profile;
        }

        public StageBgmSlotMode Mode { get; }

        public BgmProfile Profile { get; }
    }

    public sealed class StageAudioResolvedData
    {
        public StageAudioResolvedData(StageBgmResolvedSlot gameplayBgm)
        {
            GameplayBgm = gameplayBgm;
        }

        public StageBgmResolvedSlot GameplayBgm { get; }
    }
}
