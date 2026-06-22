namespace Game.Feature.Gameplay.Loop
{
    public readonly struct GameplayRuntimeFeatureFlags
    {
        public GameplayRuntimeFeatureFlags(
            bool enableEnemySameFaceContinuousLocomotion = false,
            bool enableEnemyChargeKinematicLocomotion = false,
            bool enableEnemyGlideKinematicLocomotion = false,
            bool enablePlayerFree2DActionAssist = false,
            bool removedLegacyFallbackDiagnosticsEnabled = false)
        {
            EnableEnemySameFaceContinuousLocomotion = enableEnemySameFaceContinuousLocomotion;
            EnableEnemyChargeKinematicLocomotion = enableEnemyChargeKinematicLocomotion;
            EnableEnemyGlideKinematicLocomotion = enableEnemyGlideKinematicLocomotion;
            EnablePlayerFree2DActionAssist = enablePlayerFree2DActionAssist;
            RemovedLegacyFallbackDiagnosticsEnabled = removedLegacyFallbackDiagnosticsEnabled;
        }

        public static GameplayRuntimeFeatureFlags None => default;

        public static GameplayRuntimeFeatureFlags PlayerFree2DActionAssistEnabled =>
            new(enablePlayerFree2DActionAssist: true);

        public static GameplayRuntimeFeatureFlags DefaultGameplayLocomotion =>
            new(
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true,
                enableEnemyGlideKinematicLocomotion: true,
                enablePlayerFree2DActionAssist: true);

        public static GameplayRuntimeFeatureFlags RemovedLegacyFallbackDiagnosticBaseline =>
            new(removedLegacyFallbackDiagnosticsEnabled: true);

        public static GameplayRuntimeFeatureFlags EnemySameFaceContinuousLocomotionEnabled =>
            new(enableEnemySameFaceContinuousLocomotion: true);

        public static GameplayRuntimeFeatureFlags EnemyChargeKinematicLocomotionEnabled =>
            new(enableEnemyChargeKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags EnemyGlideKinematicLocomotionEnabled =>
            new(enableEnemyGlideKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags EnemyAndChargeKinematicLocomotionEnabled =>
            new(
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true);

        public static GameplayRuntimeFeatureFlags AllKinematicLocomotionEnabled =>
            new(
                enableEnemySameFaceContinuousLocomotion: true,
                enableEnemyChargeKinematicLocomotion: true,
                enableEnemyGlideKinematicLocomotion: true);

        public bool EnableEnemySameFaceContinuousLocomotion { get; }

        public bool EnableEnemyChargeKinematicLocomotion { get; }

        public bool EnableEnemyGlideKinematicLocomotion { get; }

        public bool EnablePlayerFree2DActionAssist { get; }

        public bool RemovedLegacyFallbackDiagnosticsEnabled { get; }
    }
}
