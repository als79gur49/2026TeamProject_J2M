using System;

namespace Game.Feature.Gameplay.Host
{
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

}
