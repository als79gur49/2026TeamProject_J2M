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

    internal static class DamageDeathVfxExecutionPolicy
    {
        public const DamageDeathVfxExecutionMode ProductionDefault =
            DamageDeathVfxExecutionMode.OrchestrationExecutor;
        public const DamageDeathVfxExecutionMode LegacyFallback =
            DamageDeathVfxExecutionMode.LegacyExtension;

        public static DamageDeathVfxExecutionMode Normalize(DamageDeathVfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(DamageDeathVfxExecutionMode), mode)
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
        public const PlayerActionAnimationExecutionMode LegacyFallback =
            PlayerActionAnimationExecutionDefaults.LegacyFallback;

        public static PlayerActionAnimationExecutionMode Normalize(PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : LegacyFallback;
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

    internal static class ActionAudioExecutionPolicy
    {
        public const ActionAudioExecutionMode ProductionDefault =
            ActionAudioExecutionDefaults.ProductionDefault;
        public const ActionAudioExecutionMode LegacyFallback =
            ActionAudioExecutionDefaults.LegacyFallback;

        public static ActionAudioExecutionMode Normalize(ActionAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(ActionAudioExecutionMode), mode)
                ? mode
                : LegacyFallback;
        }
    }

    internal static class EnemyAudioExecutionPolicy
    {
        public const EnemyAudioExecutionMode ProductionDefault =
            EnemyAudioExecutionDefaults.ProductionDefault;
        public const EnemyAudioExecutionMode LegacyFallback =
            EnemyAudioExecutionDefaults.LegacyFallback;

        public static EnemyAudioExecutionMode Normalize(EnemyAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyAudioExecutionMode), mode)
                ? mode
                : LegacyFallback;
        }
    }
}
