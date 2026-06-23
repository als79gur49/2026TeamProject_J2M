using System;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct UnitKinematicLocomotionTimingSnapshot
    {
        public UnitKinematicLocomotionTimingSnapshot(
            float kinematicMoveDurationSeconds,
            int ticksPerCell,
            int commitTick)
        {
            KinematicMoveDurationSeconds = kinematicMoveDurationSeconds;
            TicksPerCell = ticksPerCell;
            CommitTick = commitTick;
        }

        public float KinematicMoveDurationSeconds { get; }

        public int TicksPerCell { get; }

        public int CommitTick { get; }

        public bool IsConfigured => TicksPerCell > 0 && CommitTick > 0;
    }

    public static class SimulationMotionTimingDefaults
    {
        public const float DefaultMoveDurationSecondsPerCell = 1f / 3f;
        public const float MaxMoveDurationSecondsPerCell = 2f;
        public const int MinTicksPerCell = 2;
    }

    [Serializable]
    public sealed class UnitKinematicLocomotionTimingSettings
    {
        public const float DefaultKinematicMoveDurationSeconds =
            SimulationMotionTimingDefaults.DefaultMoveDurationSecondsPerCell;
        public const float MaxKinematicMoveDurationSeconds =
            SimulationMotionTimingDefaults.MaxMoveDurationSecondsPerCell;
        public const int MinTicksPerCell = SimulationMotionTimingDefaults.MinTicksPerCell;

        public float KinematicMoveDurationSeconds = DefaultKinematicMoveDurationSeconds;

        public static UnitKinematicLocomotionTimingSettings CreateDefault()
        {
            return new UnitKinematicLocomotionTimingSettings();
        }

        public UnitKinematicLocomotionTimingSettings Clone()
        {
            return new UnitKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = KinematicMoveDurationSeconds,
            };
        }

        public void Validate()
        {
            if (float.IsNaN(KinematicMoveDurationSeconds) ||
                float.IsInfinity(KinematicMoveDurationSeconds) ||
                KinematicMoveDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(KinematicMoveDurationSeconds),
                    "Unit kinematic move duration must be greater than zero.");
            }

            if (KinematicMoveDurationSeconds > MaxKinematicMoveDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(KinematicMoveDurationSeconds),
                    "Unit kinematic move duration exceeds the supported maximum.");
            }
        }

        public UnitKinematicLocomotionTimingSnapshot CreateAuthoritativeSnapshot(
            int simulationTicksPerSecond)
        {
            Validate();

            var ticksPerCell = GameplayTimingProfile.SecondsToEvenCeilTicks(
                KinematicMoveDurationSeconds,
                simulationTicksPerSecond,
                MinTicksPerCell);
            return new UnitKinematicLocomotionTimingSnapshot(
                KinematicMoveDurationSeconds,
                ticksPerCell,
                ticksPerCell / 2);
        }
    }
}
