namespace Game.Feature.Gameplay.Loop
{
    public readonly struct GameplayRuntimeFeatureFlags
    {
        public GameplayRuntimeFeatureFlags(bool enablePlayerSameFaceContinuousLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion)
        {
            EnablePlayerSameFaceContinuousLocomotion = enablePlayerSameFaceContinuousLocomotion;
            EnableEnemySameFaceContinuousLocomotion = enableEnemySameFaceContinuousLocomotion;
        }

        public static GameplayRuntimeFeatureFlags None => default;

        public static GameplayRuntimeFeatureFlags PlayerSameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: false);

        public static GameplayRuntimeFeatureFlags EnemySameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: false,
                enableEnemySameFaceContinuousLocomotion: true);

        public static GameplayRuntimeFeatureFlags PlayerAndEnemySameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: true);

        public bool EnablePlayerSameFaceContinuousLocomotion { get; }

        public bool EnableEnemySameFaceContinuousLocomotion { get; }
    }
}
