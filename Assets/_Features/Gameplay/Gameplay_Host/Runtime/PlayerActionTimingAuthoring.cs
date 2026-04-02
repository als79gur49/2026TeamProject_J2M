using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct PlayerActionTimingAuthoritativeSnapshot
    {
        public PlayerActionTimingAuthoritativeSnapshot(
            float pushExecuteDelaySeconds,
            int pushExecuteDelayTicks,
            float pushInputLockDurationSeconds,
            int pushInputLockDurationTicks,
            int pushWindupTicks,
            int pushRecoveryTicks,
            float flipExecuteDelaySeconds,
            int flipExecuteDelayTicks,
            float flipInputLockDurationSeconds,
            int flipInputLockDurationTicks,
            int flipWindupTicks,
            int flipRecoveryTicks)
        {
            PushExecuteDelaySeconds = pushExecuteDelaySeconds;
            PushExecuteDelayTicks = pushExecuteDelayTicks;
            PushInputLockDurationSeconds = pushInputLockDurationSeconds;
            PushInputLockDurationTicks = pushInputLockDurationTicks;
            PushWindupTicks = pushWindupTicks;
            PushRecoveryTicks = pushRecoveryTicks;
            FlipExecuteDelaySeconds = flipExecuteDelaySeconds;
            FlipExecuteDelayTicks = flipExecuteDelayTicks;
            FlipInputLockDurationSeconds = flipInputLockDurationSeconds;
            FlipInputLockDurationTicks = flipInputLockDurationTicks;
            FlipWindupTicks = flipWindupTicks;
            FlipRecoveryTicks = flipRecoveryTicks;
        }

        public float PushExecuteDelaySeconds { get; }

        public int PushExecuteDelayTicks { get; }

        public float PushInputLockDurationSeconds { get; }

        public int PushInputLockDurationTicks { get; }

        public int PushWindupTicks { get; }

        public int PushRecoveryTicks { get; }

        public float FlipExecuteDelaySeconds { get; }

        public int FlipExecuteDelayTicks { get; }

        public float FlipInputLockDurationSeconds { get; }

        public int FlipInputLockDurationTicks { get; }

        public int FlipWindupTicks { get; }

        public int FlipRecoveryTicks { get; }
    }

    public readonly struct PlayerActionTimingPresentationSnapshot
    {
        public PlayerActionTimingPresentationSnapshot(
            float pushPresentationDurationSeconds,
            float flipPresentationDurationSeconds)
        {
            PushPresentationDurationSeconds = pushPresentationDurationSeconds;
            FlipPresentationDurationSeconds = flipPresentationDurationSeconds;
        }

        public float PushPresentationDurationSeconds { get; }

        public float FlipPresentationDurationSeconds { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerActionTimingAuthoring : MonoBehaviour
    {
        public const int DefaultPushExecuteDelayTicksAtDefaultSimulationRate = 1;
        public const int DefaultPushInputLockDurationTicksAtDefaultSimulationRate =
            DefaultPushExecuteDelayTicksAtDefaultSimulationRate;
        public const int DefaultFlipExecuteDelayTicksAtDefaultSimulationRate = 1;
        public const int DefaultFlipInputLockDurationTicksAtDefaultSimulationRate =
            DefaultFlipExecuteDelayTicksAtDefaultSimulationRate;
        public const float DefaultPushExecuteDelaySeconds =
            DefaultPushExecuteDelayTicksAtDefaultSimulationRate / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public const float DefaultPushInputLockDurationSeconds =
            DefaultPushInputLockDurationTicksAtDefaultSimulationRate / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public const float DefaultFlipExecuteDelaySeconds =
            DefaultFlipExecuteDelayTicksAtDefaultSimulationRate / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public const float DefaultFlipInputLockDurationSeconds =
            DefaultFlipInputLockDurationTicksAtDefaultSimulationRate / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public const float DefaultPresentationDurationSeconds = GameplayTimingProfile.DefaultPushMotionDurationSeconds;

        [SerializeField] private float pushExecuteDelaySeconds = DefaultPushExecuteDelaySeconds;
        [SerializeField] private float pushInputLockDurationSeconds = DefaultPushInputLockDurationSeconds;
        [SerializeField] private float pushPresentationDurationSeconds = DefaultPresentationDurationSeconds;
        [SerializeField] private float flipExecuteDelaySeconds = DefaultFlipExecuteDelaySeconds;
        [SerializeField] private float flipInputLockDurationSeconds = DefaultFlipInputLockDurationSeconds;
        [SerializeField] private float flipPresentationDurationSeconds = DefaultPresentationDurationSeconds;

        public float PushExecuteDelaySeconds => pushExecuteDelaySeconds;

        public float PushInputLockDurationSeconds => pushInputLockDurationSeconds;

        public float PushPresentationDurationSeconds => pushPresentationDurationSeconds;

        public float FlipExecuteDelaySeconds => flipExecuteDelaySeconds;

        public float FlipInputLockDurationSeconds => flipInputLockDurationSeconds;

        public float FlipPresentationDurationSeconds => flipPresentationDurationSeconds;

        public void Validate()
        {
            ValidateActionTiming(
                pushExecuteDelaySeconds,
                pushInputLockDurationSeconds,
                pushPresentationDurationSeconds,
                nameof(pushExecuteDelaySeconds),
                nameof(pushInputLockDurationSeconds),
                nameof(pushPresentationDurationSeconds));
            ValidateActionTiming(
                flipExecuteDelaySeconds,
                flipInputLockDurationSeconds,
                flipPresentationDurationSeconds,
                nameof(flipExecuteDelaySeconds),
                nameof(flipInputLockDurationSeconds),
                nameof(flipPresentationDurationSeconds));
        }

        public PlayerActionTimingAuthoritativeSnapshot CreateAuthoritativeSnapshot(int simulationTicksPerSecond)
        {
            Validate();

            var pushExecuteDelayTicks = GameplayTimingProfile.SecondsToTicks(
                pushExecuteDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var pushInputLockDurationTicks = GameplayTimingProfile.SecondsToTicks(
                pushInputLockDurationSeconds,
                simulationTicksPerSecond);
            var flipExecuteDelayTicks = GameplayTimingProfile.SecondsToTicks(
                flipExecuteDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var flipInputLockDurationTicks = GameplayTimingProfile.SecondsToTicks(
                flipInputLockDurationSeconds,
                simulationTicksPerSecond);

            return new PlayerActionTimingAuthoritativeSnapshot(
                pushExecuteDelaySeconds,
                pushExecuteDelayTicks,
                pushInputLockDurationSeconds,
                pushInputLockDurationTicks,
                pushWindupTicks: pushExecuteDelayTicks,
                pushRecoveryTicks: Mathf.Max(0, pushInputLockDurationTicks - pushExecuteDelayTicks),
                flipExecuteDelaySeconds,
                flipExecuteDelayTicks,
                flipInputLockDurationSeconds,
                flipInputLockDurationTicks,
                flipWindupTicks: flipExecuteDelayTicks,
                flipRecoveryTicks: Mathf.Max(0, flipInputLockDurationTicks - flipExecuteDelayTicks));
        }

        public PlayerActionTimingPresentationSnapshot CreatePresentationSnapshot()
        {
            Validate();
            return new PlayerActionTimingPresentationSnapshot(
                pushPresentationDurationSeconds,
                flipPresentationDurationSeconds);
        }

        private static void ValidateActionTiming(
            float executeDelaySeconds,
            float inputLockDurationSeconds,
            float presentationDurationSeconds,
            string executeDelayParameterName,
            string inputLockParameterName,
            string presentationDurationParameterName)
        {
            if (executeDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(executeDelayParameterName, "Execute delay must be zero or greater.");
            }

            if (inputLockDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(inputLockParameterName, "Input lock duration must be greater than zero.");
            }

            if (inputLockDurationSeconds < executeDelaySeconds)
            {
                throw new ArgumentOutOfRangeException(
                    inputLockParameterName,
                    "Input lock duration must be greater than or equal to execute delay.");
            }

            if (presentationDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    presentationDurationParameterName,
                    "Presentation duration must be greater than zero.");
            }
        }
    }

    internal static class PlayerViewPrefabRequirements
    {
        public static PlayerActionTimingAuthoring GetTimingAuthoring(
            GameplayEntityView playerViewPrefab,
            string ownerDescription)
        {
            if (playerViewPrefab == null)
            {
                throw new ArgumentNullException(nameof(playerViewPrefab));
            }

            if (!playerViewPrefab.TryGetComponent<PlayerActionTimingAuthoring>(out var authoring) ||
                authoring == null)
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} requires PlayerActionTimingAuthoring on the player prefab root.");
            }

            authoring.Validate();
            return authoring;
        }

        public static void ValidatePlayerViewPrefab(GameplayEntityView playerViewPrefab, string ownerDescription)
        {
            if (playerViewPrefab == null)
            {
                throw new ArgumentNullException(nameof(playerViewPrefab));
            }

            if (!playerViewPrefab.TryGetComponent<PlayerAnimatorDriver>(out _))
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} requires PlayerAnimatorDriver on the player prefab root.");
            }

            GetTimingAuthoring(playerViewPrefab, ownerDescription);
        }

        public static void ValidatePlayerViewInstance(GameplayEntityView playerViewInstance, string ownerDescription)
        {
            ValidatePlayerViewPrefab(playerViewInstance, ownerDescription);
        }
    }
}
