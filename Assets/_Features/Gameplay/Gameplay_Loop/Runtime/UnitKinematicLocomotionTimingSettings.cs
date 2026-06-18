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
            MoveDurationSeconds = kinematicMoveDurationSeconds;
            TicksPerCell = ticksPerCell;
            CommitTick = commitTick;
        }

        public float MoveDurationSeconds { get; }

        public int TicksPerCell { get; }

        public int CommitTick { get; }

        public bool IsConfigured => TicksPerCell > 0 && CommitTick > 0;
    }

    [Serializable]
    public sealed class UnitKinematicLocomotionTimingSettings
    {
        public const float DefaultMoveDurationSeconds = 1f / 3f;
        public const float MaxMoveDurationSeconds = 2f;
        public const int MinTicksPerCell = 2;

        public float MoveDurationSeconds = DefaultMoveDurationSeconds;

        public static UnitKinematicLocomotionTimingSettings CreateDefault()
        {
            return new UnitKinematicLocomotionTimingSettings();
        }

        public UnitKinematicLocomotionTimingSettings Clone()
        {
            return new UnitKinematicLocomotionTimingSettings
            {
                MoveDurationSeconds = MoveDurationSeconds,
            };
        }

        public void Validate()
        {
            if (float.IsNaN(MoveDurationSeconds) ||
                float.IsInfinity(MoveDurationSeconds) ||
                MoveDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MoveDurationSeconds),
                    "Unit kinematic move duration must be greater than zero.");
            }

            if (MoveDurationSeconds > MaxMoveDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MoveDurationSeconds),
                    "Unit kinematic move duration exceeds the supported maximum.");
            }
        }

        public UnitKinematicLocomotionTimingSnapshot CreateAuthoritativeSnapshot(
            int simulationTicksPerSecond)
        {
            Validate();

            var ticksPerCell = GameplayTimingProfile.SecondsToEvenCeilTicks(
                MoveDurationSeconds,
                simulationTicksPerSecond,
                MinTicksPerCell);
            return new UnitKinematicLocomotionTimingSnapshot(
                MoveDurationSeconds,
                ticksPerCell,
                ticksPerCell / 2);
        }
    }
}
