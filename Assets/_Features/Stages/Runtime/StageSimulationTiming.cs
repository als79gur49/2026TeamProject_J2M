using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Stages
{
    public readonly struct StageSimulationTiming
    {
        public static readonly StageSimulationTiming Default =
            FromTicksPerSecond(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

        private StageSimulationTiming(float tickDeltaSeconds, int simulationTicksPerSecond)
        {
            if (float.IsNaN(tickDeltaSeconds) ||
                float.IsInfinity(tickDeltaSeconds) ||
                tickDeltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tickDeltaSeconds),
                    "Tick delta seconds must be finite and greater than zero.");
            }

            if (simulationTicksPerSecond < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation ticks per second must be zero or greater.");
            }

            TickDeltaSeconds = tickDeltaSeconds;
            SimulationTicksPerSecond = simulationTicksPerSecond;
        }

        public float TickDeltaSeconds { get; }

        public int SimulationTicksPerSecond { get; }

        public static StageSimulationTiming FromTicksPerSecond(int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new StageSimulationTiming(1f / simulationTicksPerSecond, simulationTicksPerSecond);
        }

        public static StageSimulationTiming FromTickDeltaSeconds(float tickDeltaSeconds)
        {
            return new StageSimulationTiming(tickDeltaSeconds, simulationTicksPerSecond: 0);
        }

        public static StageSimulationTiming FromTimingProfile(GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            return new StageSimulationTiming(
                timingProfile.SimulationTickIntervalSeconds,
                timingProfile.SimulationTicksPerSecond);
        }

        public int SecondsToTicksCeil(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds),
                    "Seconds must be finite and zero or greater.");
            }

            var rawTicks = SimulationTicksPerSecond > 0
                ? seconds * SimulationTicksPerSecond
                : seconds / TickDeltaSeconds;
            return Mathf.Max(1, Mathf.CeilToInt(rawTicks));
        }
    }
}
