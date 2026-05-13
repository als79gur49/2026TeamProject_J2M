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

    public interface IGravityFieldLockedTargetVisualTarget
    {
        void ApplyGravityFieldLockedTarget(int emitterEntityId);

        void ClearGravityFieldLockedTarget(int emitterEntityId);
    }

    public interface IGravityFieldLockedBoxOneShotVisualTarget
    {
        void PlayGravityFieldLockedBox(Game.Feature.Gameplay.Loop.GravityFieldLockedBoxPayload payload);
    }
}
