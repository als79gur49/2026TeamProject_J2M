using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class GameplayTimingProfile
    {
        public const int DefaultSimulationTicksPerSecond = 60;
        public const float DefaultLegacyTickIntervalSeconds = 0.2f;
        public const float DefaultFlipArcHeightInCells = 0.65f;
        public const int DefaultMaxTicksPerFrame = 8;
        public const int DefaultPlayerPushContactThresholdTicks = 2;

        public GameplayTimingProfile(
            int simulationTicksPerSecond,
            float initialMoveDelaySeconds,
            float repeatedMoveIntervalSeconds,
            float boxSlideStepIntervalSeconds,
            float projectileStepIntervalSeconds,
            float pushMotionDurationSeconds,
            float flipMotionDurationSeconds,
            float flipArcHeightInCells,
            int maxTicksPerFrame)
            : this(
                simulationTicksPerSecond,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds,
                projectileStepIntervalSeconds,
                pushMotionDurationSeconds,
                pushMotionDurationSeconds,
                flipMotionDurationSeconds,
                flipArcHeightInCells,
                maxTicksPerFrame)
        {
        }

        public GameplayTimingProfile(
            int simulationTicksPerSecond,
            float initialMoveDelaySeconds,
            float repeatedMoveIntervalSeconds,
            float boxSlideStepIntervalSeconds,
            float projectileStepIntervalSeconds,
            float pushMotionDurationSeconds,
            float topologyMotionDurationSeconds,
            float flipMotionDurationSeconds,
            float flipArcHeightInCells,
            int maxTicksPerFrame,
            float playerMoveCooldownSeconds = -1f,
            float playerPushContactThresholdSeconds = -1f)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            if (initialMoveDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialMoveDelaySeconds),
                    "Initial move delay must be zero or greater.");
            }

            if (repeatedMoveIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatedMoveIntervalSeconds),
                    "Repeated move interval must be greater than zero.");
            }

            if (boxSlideStepIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boxSlideStepIntervalSeconds),
                    "Box slide step interval must be greater than zero.");
            }

            if (projectileStepIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectileStepIntervalSeconds),
                    "Projectile step interval must be greater than zero.");
            }

            if (pushMotionDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pushMotionDurationSeconds),
                    "Push motion duration must be greater than zero.");
            }

            if (topologyMotionDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(topologyMotionDurationSeconds),
                    "Topology motion duration must be greater than zero.");
            }

            if (flipMotionDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flipMotionDurationSeconds),
                    "Flip motion duration must be greater than zero.");
            }

            if (flipArcHeightInCells <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flipArcHeightInCells),
                    "Flip arc height must be greater than zero.");
            }

            if (maxTicksPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTicksPerFrame),
                    "Max ticks per frame must be greater than zero.");
            }

            SimulationTicksPerSecond = simulationTicksPerSecond;
            SimulationTickIntervalSeconds = 1f / simulationTicksPerSecond;
            InitialMoveDelaySeconds = initialMoveDelaySeconds;
            RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds;
            BoxSlideStepIntervalSeconds = boxSlideStepIntervalSeconds;
            ProjectileStepIntervalSeconds = projectileStepIntervalSeconds;
            PushMotionDurationSeconds = pushMotionDurationSeconds;
            TopologyMotionDurationSeconds = topologyMotionDurationSeconds;
            FlipMotionDurationSeconds = flipMotionDurationSeconds;
            FlipArcHeightInCells = flipArcHeightInCells;
            MaxTicksPerFrame = maxTicksPerFrame;
            InitialMoveDelayTicks = SecondsToTicks(initialMoveDelaySeconds, simulationTicksPerSecond, allowZero: true);
            RepeatedMoveIntervalTicks = SecondsToTicks(repeatedMoveIntervalSeconds, simulationTicksPerSecond);
            BoxSlideStepIntervalTicks = SecondsToTicks(boxSlideStepIntervalSeconds, simulationTicksPerSecond);
            ProjectileStepIntervalTicks = SecondsToTicks(projectileStepIntervalSeconds, simulationTicksPerSecond);
            PlayerMoveCooldownSeconds = playerMoveCooldownSeconds >= 0f
                ? playerMoveCooldownSeconds
                : repeatedMoveIntervalSeconds;
            PlayerPushContactThresholdSeconds = playerPushContactThresholdSeconds >= 0f
                ? playerPushContactThresholdSeconds
                : DefaultPlayerPushContactThresholdTicks / (float)simulationTicksPerSecond;
            PlayerMoveCooldownTicks = SecondsToTicks(PlayerMoveCooldownSeconds, simulationTicksPerSecond, allowZero: true);
            PlayerPushContactThresholdTicks = SecondsToTicks(PlayerPushContactThresholdSeconds, simulationTicksPerSecond);
        }

        public int SimulationTicksPerSecond { get; }

        public float SimulationTickIntervalSeconds { get; }

        public float InitialMoveDelaySeconds { get; }

        public float RepeatedMoveIntervalSeconds { get; }

        public float BoxSlideStepIntervalSeconds { get; }

        public float ProjectileStepIntervalSeconds { get; }

        public float PushMotionDurationSeconds { get; }

        public float TopologyMotionDurationSeconds { get; }

        public float FlipMotionDurationSeconds { get; }

        public float FlipArcHeightInCells { get; }

        public int MaxTicksPerFrame { get; }

        public int InitialMoveDelayTicks { get; }

        public int RepeatedMoveIntervalTicks { get; }

        public int BoxSlideStepIntervalTicks { get; }

        public int ProjectileStepIntervalTicks { get; }

        public float PlayerMoveCooldownSeconds { get; }

        public float PlayerPushContactThresholdSeconds { get; }

        public int PlayerMoveCooldownTicks { get; }

        public int PlayerPushContactThresholdTicks { get; }

        public static GameplayTimingProfile CreateDefault()
        {
            return CreateFromLegacy(
                DefaultSimulationTicksPerSecond,
                initialMoveDelayTicks: 0,
                repeatedMoveIntervalTicks: 2,
                legacyTickIntervalSeconds: DefaultLegacyTickIntervalSeconds,
                flipArcHeightInCells: DefaultFlipArcHeightInCells,
                maxTicksPerFrame: DefaultMaxTicksPerFrame);
        }

        public static GameplayTimingProfile CreateFromLegacy(
            int simulationTicksPerSecond,
            int initialMoveDelayTicks,
            int repeatedMoveIntervalTicks,
            float legacyTickIntervalSeconds,
            float flipArcHeightInCells,
            int maxTicksPerFrame)
        {
            if (initialMoveDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialMoveDelayTicks),
                    "Initial move delay ticks must be zero or greater.");
            }

            if (repeatedMoveIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatedMoveIntervalTicks),
                    "Repeated move interval ticks must be greater than zero.");
            }

            if (legacyTickIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(legacyTickIntervalSeconds),
                    "Legacy tick interval must be greater than zero.");
            }

            return new GameplayTimingProfile(
                simulationTicksPerSecond,
                initialMoveDelayTicks * legacyTickIntervalSeconds,
                repeatedMoveIntervalTicks * legacyTickIntervalSeconds,
                legacyTickIntervalSeconds,
                legacyTickIntervalSeconds,
                legacyTickIntervalSeconds,
                legacyTickIntervalSeconds,
                flipArcHeightInCells,
                maxTicksPerFrame);
        }

        public static int SecondsToTicks(float seconds, int simulationTicksPerSecond, bool allowZero = false)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            if (seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be zero or greater.");
            }

            var roundedTicks = Mathf.RoundToInt(seconds * simulationTicksPerSecond);
            if (allowZero && seconds <= 0f)
            {
                return 0;
            }

            return Mathf.Max(1, roundedTicks);
        }
    }
}
