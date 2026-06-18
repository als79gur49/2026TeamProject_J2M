namespace Game.Feature.Gameplay.Loop
{
    public readonly struct GameplayRuntimeFeatureFlags
    {
        public GameplayRuntimeFeatureFlags(
            bool enableEnemySameFaceContinuousLocomotion,
            bool enableEnemyChargeKinematicLocomotion = false,
            bool enableEnemyGlideKinematicLocomotion = false,
            bool removedLegacyFallbackDiagnosticsEnabled = false)
        {
            EnableEnemySameFaceContinuousLocomotion = enableEnemySameFaceContinuousLocomotion;
            EnableEnemyChargeKinematicLocomotion = enableEnemyChargeKinematicLocomotion;
            EnableEnemyGlideKinematicLocomotion = enableEnemyGlideKinematicLocomotion;
            RemovedLegacyFallbackDiagnosticsEnabled = removedLegacyFallbackDiagnosticsEnabled;
        }

        public static GameplayRuntimeFeatureFlags None => default;

        public static GameplayRuntimeFeatureFlags DefaultGameplayLocomotion =>
            new(
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true,
                enableEnemyGlideKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags RemovedLegacyFallbackDiagnosticBaseline =>
            new(
                enableEnemySameFaceContinuousLocomotion: false,
                enableEnemyChargeKinematicLocomotion: false,
                enableEnemyGlideKinematicLocomotion: false,
                removedLegacyFallbackDiagnosticsEnabled: true);

        public static GameplayRuntimeFeatureFlags EnemySameFaceContinuousLocomotionEnabled =>
            new(enableEnemySameFaceContinuousLocomotion: true);

        public static GameplayRuntimeFeatureFlags EnemyChargeKinematicLocomotionEnabled =>
            new(enableEnemySameFaceContinuousLocomotion: false, enableEnemyChargeKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags EnemyGlideKinematicLocomotionEnabled =>
            new(enableEnemySameFaceContinuousLocomotion: false, enableEnemyGlideKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags EnemyAndChargeKinematicLocomotionEnabled =>
            new(enableEnemySameFaceContinuousLocomotion: true, enableEnemyChargeKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags AllEnemyKinematicLocomotionEnabled =>
            new(
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true,
                enableEnemyGlideKinematicLocomotion: true);

        public bool EnableEnemySameFaceContinuousLocomotion { get; }

        public bool EnableEnemyChargeKinematicLocomotion { get; }

        public bool EnableEnemyGlideKinematicLocomotion { get; }

        public bool RemovedLegacyFallbackDiagnosticsEnabled { get; }
    }
}
