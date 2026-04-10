using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class GameplayTimingProfile
    {
        public const int DefaultSimulationTicksPerSecond = 60;
        public const float DefaultInitialMoveDelaySeconds = 0f;
        public const float DefaultRepeatedMoveIntervalSeconds = 0.4f;
        public const float DefaultBoxSlideStepIntervalSeconds = 0.2f;
        public const float DefaultProjectileStepIntervalSeconds = 0.2f;
        public const float DefaultMoveMotionDurationSeconds = 0.2f;
        public const float DefaultMoveOccupancyDurationSeconds = DefaultMoveMotionDurationSeconds;
        private const float UseMoveMotionDurationForOccupancySentinel = -1f;
        public const float DefaultPushMotionDurationSeconds = 0.2f;
        public const float DefaultTopologyMotionDurationSeconds = DefaultPushMotionDurationSeconds;
        public const float DefaultFlipMotionDurationSeconds = 0.2f;
        public const float DefaultItemConsumeEffectDurationSeconds = 0.18f;
        public const float DefaultBoxDestroyEffectDurationSeconds = 0.14f;
        public const float DefaultEnemyDeathEffectDurationSeconds = 0.2f;
        public const float DefaultFlipArcHeightInCells = 0.65f;
        public const int DefaultMaxTicksPerFrame = 8;
        public const int DefaultPlayerPushContactThresholdTicks = 12;
        public const float DefaultPlayerPushContactThresholdSeconds =
            DefaultPlayerPushContactThresholdTicks / (float)DefaultSimulationTicksPerSecond;

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
            float moveMotionDurationSeconds,
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
                moveMotionDurationSeconds,
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
            float moveMotionDurationSeconds,
            float pushMotionDurationSeconds,
            float topologyMotionDurationSeconds,
            float flipMotionDurationSeconds,
            float flipArcHeightInCells,
            int maxTicksPerFrame,
            float itemConsumeEffectDurationSeconds = DefaultItemConsumeEffectDurationSeconds,
            float boxDestroyEffectDurationSeconds = DefaultBoxDestroyEffectDurationSeconds,
            float moveOccupancyDurationSeconds = UseMoveMotionDurationForOccupancySentinel,
            float enemyDeathEffectDurationSeconds = DefaultEnemyDeathEffectDurationSeconds)
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

            if (moveMotionDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveMotionDurationSeconds),
                    "Move motion duration must be greater than zero.");
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

            var resolvedMoveOccupancyDurationSeconds = moveOccupancyDurationSeconds > 0f
                ? moveOccupancyDurationSeconds
                : moveMotionDurationSeconds;

            if (resolvedMoveOccupancyDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveOccupancyDurationSeconds),
                    "Move occupancy duration must be greater than zero.");
            }

            if (itemConsumeEffectDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(itemConsumeEffectDurationSeconds),
                    "Item consume effect duration must be greater than zero.");
            }

            if (boxDestroyEffectDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boxDestroyEffectDurationSeconds),
                    "Box destroy effect duration must be greater than zero.");
            }

            if (enemyDeathEffectDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyDeathEffectDurationSeconds),
                    "Enemy death effect duration must be greater than zero.");
            }

            SimulationTicksPerSecond = simulationTicksPerSecond;
            SimulationTickIntervalSeconds = 1f / simulationTicksPerSecond;
            InitialMoveDelaySeconds = initialMoveDelaySeconds;
            RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds;
            BoxSlideStepIntervalSeconds = boxSlideStepIntervalSeconds;
            ProjectileStepIntervalSeconds = projectileStepIntervalSeconds;
            MoveMotionDurationSeconds = moveMotionDurationSeconds;
            MoveOccupancyDurationSeconds = resolvedMoveOccupancyDurationSeconds;
            PushMotionDurationSeconds = pushMotionDurationSeconds;
            TopologyMotionDurationSeconds = topologyMotionDurationSeconds;
            FlipMotionDurationSeconds = flipMotionDurationSeconds;
            ItemConsumeEffectDurationSeconds = itemConsumeEffectDurationSeconds;
            BoxDestroyEffectDurationSeconds = boxDestroyEffectDurationSeconds;
            EnemyDeathEffectDurationSeconds = enemyDeathEffectDurationSeconds;
            FlipArcHeightInCells = flipArcHeightInCells;
            MaxTicksPerFrame = maxTicksPerFrame;
            InitialMoveDelayTicks = SecondsToTicks(initialMoveDelaySeconds, simulationTicksPerSecond, allowZero: true);
            RepeatedMoveIntervalTicks = SecondsToTicks(repeatedMoveIntervalSeconds, simulationTicksPerSecond);
            BoxSlideStepIntervalTicks = SecondsToTicks(boxSlideStepIntervalSeconds, simulationTicksPerSecond);
            ProjectileStepIntervalTicks = SecondsToTicks(projectileStepIntervalSeconds, simulationTicksPerSecond);
            MoveOccupancyTicks = SecondsToCeilTicks(resolvedMoveOccupancyDurationSeconds, simulationTicksPerSecond);
        }

        public int SimulationTicksPerSecond { get; }

        public float SimulationTickIntervalSeconds { get; }

        public float InitialMoveDelaySeconds { get; }

        public float RepeatedMoveIntervalSeconds { get; }

        public float BoxSlideStepIntervalSeconds { get; }

        public float ProjectileStepIntervalSeconds { get; }

        public float MoveMotionDurationSeconds { get; }

        public float MoveOccupancyDurationSeconds { get; }

        public float PushMotionDurationSeconds { get; }

        public float TopologyMotionDurationSeconds { get; }

        public float FlipMotionDurationSeconds { get; }

        public float ItemConsumeEffectDurationSeconds { get; }

        public float BoxDestroyEffectDurationSeconds { get; }

        public float EnemyDeathEffectDurationSeconds { get; }

        public float FlipArcHeightInCells { get; }

        public int MaxTicksPerFrame { get; }

        public int InitialMoveDelayTicks { get; }

        public int RepeatedMoveIntervalTicks { get; }

        public int BoxSlideStepIntervalTicks { get; }

        public int ProjectileStepIntervalTicks { get; }

        public int MoveOccupancyTicks { get; }

        public static GameplayTimingProfile CreateDefault()
        {
            return new GameplayTimingProfile(
                DefaultSimulationTicksPerSecond,
                DefaultInitialMoveDelaySeconds,
                DefaultRepeatedMoveIntervalSeconds,
                DefaultBoxSlideStepIntervalSeconds,
                DefaultProjectileStepIntervalSeconds,
                DefaultMoveMotionDurationSeconds,
                DefaultPushMotionDurationSeconds,
                DefaultTopologyMotionDurationSeconds,
                DefaultFlipMotionDurationSeconds,
                DefaultFlipArcHeightInCells,
                DefaultMaxTicksPerFrame,
                DefaultItemConsumeEffectDurationSeconds,
                DefaultBoxDestroyEffectDurationSeconds,
                DefaultMoveOccupancyDurationSeconds,
                DefaultEnemyDeathEffectDurationSeconds);
        }

        public static int SecondsToCeilTicks(float seconds, int simulationTicksPerSecond, bool allowZero = false)
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

            if (allowZero && seconds <= 0f)
            {
                return 0;
            }

            var ceilTicks = Mathf.CeilToInt(seconds * simulationTicksPerSecond);
            return Mathf.Max(1, ceilTicks);
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
