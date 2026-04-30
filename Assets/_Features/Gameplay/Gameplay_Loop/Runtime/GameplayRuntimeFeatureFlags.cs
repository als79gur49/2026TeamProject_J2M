namespace Game.Feature.Gameplay.Loop
{
    public readonly struct GameplayRuntimeFeatureFlags
    {
        public GameplayRuntimeFeatureFlags(bool enablePlayerSameFaceContinuousLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion,
                enableEnemyChargeKinematicLocomotion: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion,
            bool enableEnemyChargeKinematicLocomotion)
        {
            EnablePlayerSameFaceContinuousLocomotion = enablePlayerSameFaceContinuousLocomotion;
            EnableEnemySameFaceContinuousLocomotion = enableEnemySameFaceContinuousLocomotion;
            EnableEnemyChargeKinematicLocomotion = enableEnemyChargeKinematicLocomotion;
        }

        public static GameplayRuntimeFeatureFlags None => default;

        public static GameplayRuntimeFeatureFlags PlayerSameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: false);

        public static GameplayRuntimeFeatureFlags EnemySameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: false,
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: false);

        public static GameplayRuntimeFeatureFlags EnemyChargeKinematicLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: false,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags PlayerAndEnemySameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: false);

        public static GameplayRuntimeFeatureFlags EnemyAndChargeKinematicLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: false,
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags AllKinematicLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true);

        public bool EnablePlayerSameFaceContinuousLocomotion { get; }

        public bool EnableEnemySameFaceContinuousLocomotion { get; }

        public bool EnableEnemyChargeKinematicLocomotion { get; }
    }
}
