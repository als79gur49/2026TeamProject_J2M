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
        [SerializeField] private UnitKinematicLocomotionTimingSettings unitKinematicLocomotionTiming =
            UnitKinematicLocomotionTimingSettings.CreateDefault();
        [SerializeField] private PlayerFree2DLocomotionAuthoring playerFree2DLocomotion =
            PlayerFree2DLocomotionAuthoring.CreateDefault();
        [SerializeField] private PlayerRespawnTimingSettings playerRespawnTiming = PlayerRespawnTimingSettings.CreateDefault();
        [SerializeField] private float repeatedMoveIntervalSeconds = GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds;
        [SerializeField] private float boxSlideStepIntervalSeconds = GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds;
        [SerializeField] private float forwardCellTravelStepIntervalSeconds = GameplayTimingProfile.DefaultForwardCellTravelStepIntervalSeconds;

        public PlayerFree2DLocomotionAuthoring PlayerFree2DLocomotion => playerFree2DLocomotion;

        public void ApplyTo(GameplaySceneHostConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Validate();

            configuration.InitialMoveDelaySeconds = initialMoveDelaySeconds;
            configuration.PlayerControlTiming = playerControlTiming.Clone();
            configuration.UnitKinematicLocomotionTiming = unitKinematicLocomotionTiming.Clone();
            configuration.PlayerFree2DLocomotion = playerFree2DLocomotion;
            configuration.PlayerFree2DLocomotionOverride = PlayerFree2DLocomotionOverride.None;
            configuration.PlayerRespawnTiming = playerRespawnTiming.Clone();
            configuration.RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds;
            configuration.BoxSlideStepIntervalSeconds = boxSlideStepIntervalSeconds;
            configuration.ForwardCellTravelStepIntervalSeconds = forwardCellTravelStepIntervalSeconds;
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
            ValidatePositiveInterval(forwardCellTravelStepIntervalSeconds, nameof(forwardCellTravelStepIntervalSeconds));

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

            if (unitKinematicLocomotionTiming == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplaySimulationTimingPreset)} '{name}' requires {nameof(unitKinematicLocomotionTiming)}.");
            }

            playerControlTiming.Validate(repeatedMoveIntervalSeconds);
            unitKinematicLocomotionTiming.Validate();
            playerFree2DLocomotion.Validate();
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
