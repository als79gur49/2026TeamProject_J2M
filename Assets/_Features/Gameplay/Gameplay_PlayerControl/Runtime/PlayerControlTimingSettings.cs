using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.PlayerControl
{
    public readonly struct PlayerControlTimingAuthoritativeSnapshot
    {
        public PlayerControlTimingAuthoritativeSnapshot(
            float moveCooldownSeconds,
            int moveCooldownTicks,
            float pushContactThresholdSeconds,
            int pushContactThresholdTicks,
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
            MoveCooldownSeconds = moveCooldownSeconds;
            MoveCooldownTicks = moveCooldownTicks;
            PushContactThresholdSeconds = pushContactThresholdSeconds;
            PushContactThresholdTicks = pushContactThresholdTicks;
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

        public float MoveCooldownSeconds { get; }

        public int MoveCooldownTicks { get; }

        public float PushContactThresholdSeconds { get; }

        public int PushContactThresholdTicks { get; }

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

    [Serializable]
    public sealed class PlayerControlTimingSettings
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

        public float MoveCooldownSeconds = -1f;
        public float PushContactThresholdSeconds = -1f;
        public float PushExecuteDelaySeconds = DefaultPushExecuteDelaySeconds;
        public float PushInputLockDurationSeconds = DefaultPushInputLockDurationSeconds;
        public float FlipExecuteDelaySeconds = DefaultFlipExecuteDelaySeconds;
        public float FlipInputLockDurationSeconds = DefaultFlipInputLockDurationSeconds;

        public static PlayerControlTimingSettings CreateDefault()
        {
            return new PlayerControlTimingSettings();
        }

        public PlayerControlTimingSettings Clone()
        {
            return new PlayerControlTimingSettings
            {
                MoveCooldownSeconds = MoveCooldownSeconds,
                PushContactThresholdSeconds = PushContactThresholdSeconds,
                PushExecuteDelaySeconds = PushExecuteDelaySeconds,
                PushInputLockDurationSeconds = PushInputLockDurationSeconds,
                FlipExecuteDelaySeconds = FlipExecuteDelaySeconds,
                FlipInputLockDurationSeconds = FlipInputLockDurationSeconds,
            };
        }

        public void Validate(float repeatedMoveIntervalSeconds)
        {
            if (repeatedMoveIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatedMoveIntervalSeconds),
                    "Repeated move interval must be greater than zero.");
            }

            var moveCooldownSeconds = ResolveMoveCooldownSeconds(repeatedMoveIntervalSeconds);
            var pushContactThresholdSeconds = ResolvePushContactThresholdSeconds();

            if (moveCooldownSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MoveCooldownSeconds),
                    "Move cooldown must be zero or greater.");
            }

            if (pushContactThresholdSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(PushContactThresholdSeconds),
                    "Push contact threshold must be zero or greater.");
            }

            ValidateActionTiming(
                PushExecuteDelaySeconds,
                PushInputLockDurationSeconds,
                nameof(PushExecuteDelaySeconds),
                nameof(PushInputLockDurationSeconds));
            ValidateActionTiming(
                FlipExecuteDelaySeconds,
                FlipInputLockDurationSeconds,
                nameof(FlipExecuteDelaySeconds),
                nameof(FlipInputLockDurationSeconds));
        }

        public PlayerControlTimingAuthoritativeSnapshot CreateAuthoritativeSnapshot(
            int simulationTicksPerSecond,
            float repeatedMoveIntervalSeconds)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            Validate(repeatedMoveIntervalSeconds);

            var moveCooldownSeconds = ResolveMoveCooldownSeconds(repeatedMoveIntervalSeconds);
            var pushContactThresholdSeconds = ResolvePushContactThresholdSeconds();
            var moveCooldownTicks = GameplayTimingProfile.SecondsToTicks(
                moveCooldownSeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var pushContactThresholdTicks = GameplayTimingProfile.SecondsToTicks(
                pushContactThresholdSeconds,
                simulationTicksPerSecond);
            var pushExecuteDelayTicks = GameplayTimingProfile.SecondsToTicks(
                PushExecuteDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var pushInputLockDurationTicks = GameplayTimingProfile.SecondsToTicks(
                PushInputLockDurationSeconds,
                simulationTicksPerSecond);
            var flipExecuteDelayTicks = GameplayTimingProfile.SecondsToTicks(
                FlipExecuteDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var flipInputLockDurationTicks = GameplayTimingProfile.SecondsToTicks(
                FlipInputLockDurationSeconds,
                simulationTicksPerSecond);

            return new PlayerControlTimingAuthoritativeSnapshot(
                moveCooldownSeconds,
                moveCooldownTicks,
                pushContactThresholdSeconds,
                pushContactThresholdTicks,
                PushExecuteDelaySeconds,
                pushExecuteDelayTicks,
                PushInputLockDurationSeconds,
                pushInputLockDurationTicks,
                pushWindupTicks: pushExecuteDelayTicks,
                pushRecoveryTicks: Mathf.Max(0, pushInputLockDurationTicks - pushExecuteDelayTicks),
                FlipExecuteDelaySeconds,
                flipExecuteDelayTicks,
                FlipInputLockDurationSeconds,
                flipInputLockDurationTicks,
                flipWindupTicks: flipExecuteDelayTicks,
                flipRecoveryTicks: Mathf.Max(0, flipInputLockDurationTicks - flipExecuteDelayTicks));
        }

        private float ResolveMoveCooldownSeconds(float repeatedMoveIntervalSeconds)
        {
            return MoveCooldownSeconds >= 0f
                ? MoveCooldownSeconds
                : repeatedMoveIntervalSeconds;
        }

        private float ResolvePushContactThresholdSeconds()
        {
            return PushContactThresholdSeconds >= 0f
                ? PushContactThresholdSeconds
                : GameplayTimingProfile.DefaultPlayerPushContactThresholdSeconds;
        }

        private static void ValidateActionTiming(
            float executeDelaySeconds,
            float inputLockDurationSeconds,
            string executeDelayParameterName,
            string inputLockParameterName)
        {
            if (executeDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    executeDelayParameterName,
                    "Execute delay must be zero or greater.");
            }

            if (inputLockDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    inputLockParameterName,
                    "Input lock duration must be greater than zero.");
            }

            if (inputLockDurationSeconds < executeDelaySeconds)
            {
                throw new ArgumentOutOfRangeException(
                    inputLockParameterName,
                    "Input lock duration must be greater than or equal to execute delay.");
            }
        }
    }
}
