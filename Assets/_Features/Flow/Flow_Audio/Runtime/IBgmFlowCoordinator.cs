using Game.Shared.Audio;

namespace Game.Feature.Flow.Audio
{
    public interface IBgmFlowCoordinator
    {
        void RequestSceneDefault(BgmProfile profile);

        void StopCurrent();

        BgmProfile GetCurrentProfile();
    }
}
