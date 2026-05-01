namespace Game.Feature.Gameplay.Loop
{
    public readonly struct GameplayRuntimeFeatureFlags
    {
        public GameplayRuntimeFeatureFlags(bool enablePlayerSameFaceContinuousLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: false,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: false,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion,
            bool enableEnemyChargeKinematicLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion,
                enableEnemyChargeKinematicLocomotion,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: false,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion,
            bool enableEnemyChargeKinematicLocomotion,
            bool enablePlayerStoppableKinematicLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion,
                enableEnemyChargeKinematicLocomotion,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion,
            bool enableEnemyChargeKinematicLocomotion,
            bool enablePlayerStoppableKinematicLocomotion,
            bool enablePlayerFree2DLocalLocomotion)
            : this(
                enablePlayerSameFaceContinuousLocomotion,
                enableEnemySameFaceContinuousLocomotion,
                enableEnemyChargeKinematicLocomotion,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion,
                enablePlayerFree2DLocalLocomotion,
                enablePlayerFree2DActionAssist: false)
        {
        }

        public GameplayRuntimeFeatureFlags(
            bool enablePlayerSameFaceContinuousLocomotion,
            bool enableEnemySameFaceContinuousLocomotion,
            bool enableEnemyChargeKinematicLocomotion,
            bool enableEnemyGlideKinematicLocomotion,
            bool enablePlayerStoppableKinematicLocomotion,
            bool enablePlayerFree2DLocalLocomotion,
            bool enablePlayerFree2DActionAssist)
        {
            EnablePlayerSameFaceContinuousLocomotion = enablePlayerSameFaceContinuousLocomotion;
            EnableEnemySameFaceContinuousLocomotion = enableEnemySameFaceContinuousLocomotion;
            EnableEnemyChargeKinematicLocomotion = enableEnemyChargeKinematicLocomotion;
            EnableEnemyGlideKinematicLocomotion = enableEnemyGlideKinematicLocomotion;
            EnablePlayerStoppableKinematicLocomotion = enablePlayerSameFaceContinuousLocomotion &&
                                                       enablePlayerStoppableKinematicLocomotion;
            EnablePlayerFree2DLocalLocomotion = enablePlayerFree2DLocalLocomotion;
            EnablePlayerFree2DActionAssist = enablePlayerFree2DLocalLocomotion &&
                                             enablePlayerFree2DActionAssist;
        }

        public static GameplayRuntimeFeatureFlags None => default;

        public static GameplayRuntimeFeatureFlags PlayerSameFaceContinuousLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: false);

        public static GameplayRuntimeFeatureFlags PlayerStoppableKinematicLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: true,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false);

        public static GameplayRuntimeFeatureFlags PlayerFree2DLocalLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: true,
                enablePlayerFree2DLocalLocomotion: true,
                enablePlayerFree2DActionAssist: false);

        public static GameplayRuntimeFeatureFlags PlayerFree2DActionAssistEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: true,
                enablePlayerFree2DLocalLocomotion: true,
                enablePlayerFree2DActionAssist: true);

        public static GameplayRuntimeFeatureFlags DefaultGameplayLocomotion =>
            new(
                enablePlayerSameFaceContinuousLocomotion: true,
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true,
                enableEnemyGlideKinematicLocomotion: false,
                enablePlayerStoppableKinematicLocomotion: true,
                enablePlayerFree2DLocalLocomotion: true,
                enablePlayerFree2DActionAssist: true);

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

        public static GameplayRuntimeFeatureFlags EnemyGlideKinematicLocomotionEnabled =>
            new(
                enablePlayerSameFaceContinuousLocomotion: false,
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: true,
                enablePlayerStoppableKinematicLocomotion: false,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false);

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
                enableEnemyChargeKinematicLocomotion: true,
                enableEnemyGlideKinematicLocomotion: true,
                enablePlayerStoppableKinematicLocomotion: false,
                enablePlayerFree2DLocalLocomotion: false,
                enablePlayerFree2DActionAssist: false);

        public bool EnablePlayerSameFaceContinuousLocomotion { get; }

        public bool EnableEnemySameFaceContinuousLocomotion { get; }

        public bool EnableEnemyChargeKinematicLocomotion { get; }

        public bool EnableEnemyGlideKinematicLocomotion { get; }

        public bool EnablePlayerStoppableKinematicLocomotion { get; }

        public bool EnablePlayerFree2DLocalLocomotion { get; }

        public bool EnablePlayerFree2DActionAssist { get; }
    }
}
