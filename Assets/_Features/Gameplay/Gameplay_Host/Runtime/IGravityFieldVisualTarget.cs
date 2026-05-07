namespace Game.Feature.Gameplay.Host
{
    public interface IGravityFieldActivatedVisualTarget
    {
        void PlayGravityFieldActivated();
    }

    public interface IGravityFieldExpiredVisualTarget
    {
        void PlayGravityFieldExpired();
    }

    public interface IGravityFieldContinuousVisualTarget
    {
        void ApplyGravityFieldVisualState(Game.Feature.Gameplay.Loop.GravityFieldVisualState state);

        void ClearGravityFieldVisualState();
    }
}
