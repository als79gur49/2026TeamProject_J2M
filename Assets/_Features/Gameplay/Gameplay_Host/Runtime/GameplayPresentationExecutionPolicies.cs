using System;

namespace Game.Feature.Gameplay.Host
{
    internal static class TopologyPresentationExecutionPolicy
    {
        public const TopologyPresentationExecutionMode ProductionDefault =
            TopologyPresentationExecutionDefaults.ProductionDefault;
        public const TopologyPresentationExecutionMode LegacyFallback =
            TopologyPresentationExecutionDefaults.LegacyFallback;

        public static TopologyPresentationExecutionMode Normalize(TopologyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(TopologyPresentationExecutionMode), mode)
                ? mode
                : LegacyFallback;
        }
    }

    internal static class BoxMotionExecutionPolicy
    {
        public const BoxMotionPresentationExecutionMode ProductionDefault =
            BoxMotionPresentationExecutionDefaults.ProductionDefault;
        public const BoxMotionPresentationExecutionMode LegacyFallback =
            BoxMotionPresentationExecutionDefaults.LegacyFallback;

        public static BoxMotionPresentationExecutionMode Normalize(BoxMotionPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(BoxMotionPresentationExecutionMode), mode)
                ? mode
                : LegacyFallback;
        }
    }

    internal static class PlayerActionAnimationExecutionPolicy
    {
        public const PlayerActionAnimationExecutionMode ProductionDefault =
            PlayerActionAnimationExecutionDefaults.ProductionDefault;

        public static PlayerActionAnimationExecutionMode Normalize(PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : ProductionDefault;
        }
    }

    internal static class EnemyPresentationExecutionPolicy
    {
        public const EnemyPresentationExecutionMode ProductionDefault =
            EnemyPresentationExecutionDefaults.ProductionDefault;
        public const EnemyPresentationExecutionMode LegacyFallback =
            EnemyPresentationExecutionDefaults.LegacyFallback;

        public static EnemyPresentationExecutionMode Normalize(EnemyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyPresentationExecutionMode), mode)
                ? mode
                : LegacyFallback;
        }
    }

    internal static class CoreGameplaySfxExecutionPolicy
    {
        public const CoreGameplaySfxExecutionMode ProductionDefault =
            CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor;
        public const CoreGameplaySfxExecutionMode LegacyFallback =
            CoreGameplaySfxExecutionMode.LegacyGameplayAudioController;

        public static CoreGameplaySfxExecutionMode Normalize(CoreGameplaySfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(CoreGameplaySfxExecutionMode), mode)
                ? mode
                : LegacyFallback;
        }
    }

}
