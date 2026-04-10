using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Timing
{
    [CreateAssetMenu(
        menuName = "Gameplay/Timing/Simulation Timing Preset",
        fileName = "GameplaySimulationTimingPreset")]
    public sealed class GameplaySimulationTimingPreset : ScriptableObject
    {
        [SerializeField] private float initialMoveDelaySeconds = GameplayTimingProfile.DefaultInitialMoveDelaySeconds;
        [SerializeField] private PlayerControlTimingSettings playerControlTiming = PlayerControlTimingSettings.CreateDefault();
        [SerializeField] private PlayerRespawnTimingSettings playerRespawnTiming = PlayerRespawnTimingSettings.CreateDefault();
        [SerializeField] private float repeatedMoveIntervalSeconds = GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds;
        [SerializeField] private float boxSlideStepIntervalSeconds = GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds;
        [SerializeField] private float projectileStepIntervalSeconds = GameplayTimingProfile.DefaultProjectileStepIntervalSeconds;

        public void ApplyTo(GameplaySceneHostConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Validate();

            configuration.InitialMoveDelaySeconds = initialMoveDelaySeconds;
            configuration.PlayerControlTiming = playerControlTiming.Clone();
            configuration.PlayerRespawnTiming = playerRespawnTiming.Clone();
            configuration.RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds;
            configuration.BoxSlideStepIntervalSeconds = boxSlideStepIntervalSeconds;
            configuration.ProjectileStepIntervalSeconds = projectileStepIntervalSeconds;
        }

        public void Validate()
        {
            if (initialMoveDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialMoveDelaySeconds),
                    "Initial move delay must be zero or greater.");
            }

            ValidatePositiveInterval(repeatedMoveIntervalSeconds, nameof(repeatedMoveIntervalSeconds));
            ValidatePositiveInterval(boxSlideStepIntervalSeconds, nameof(boxSlideStepIntervalSeconds));
            ValidatePositiveInterval(projectileStepIntervalSeconds, nameof(projectileStepIntervalSeconds));

            if (playerControlTiming == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplaySimulationTimingPreset)} '{name}' requires {nameof(playerControlTiming)}.");
            }

            if (playerRespawnTiming == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplaySimulationTimingPreset)} '{name}' requires {nameof(playerRespawnTiming)}.");
            }

            playerControlTiming.Validate(repeatedMoveIntervalSeconds);
            playerRespawnTiming.Validate();
        }

        private static void ValidatePositiveInterval(float value, string paramName)
        {
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "Simulation timing intervals must be greater than zero.");
            }
        }
    }
}
