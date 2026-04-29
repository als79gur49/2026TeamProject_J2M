using System;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerKinematicLocomotionTimingSnapshot
    {
        public PlayerKinematicLocomotionTimingSnapshot(
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

    [Serializable]
    public sealed class PlayerKinematicLocomotionTimingSettings
    {
        public const float DefaultKinematicMoveDurationSeconds = 1f / 3f;
        public const float MaxKinematicMoveDurationSeconds = 2f;
        public const int MinTicksPerCell = 2;

        public float KinematicMoveDurationSeconds = DefaultKinematicMoveDurationSeconds;

        public static PlayerKinematicLocomotionTimingSettings CreateDefault()
        {
            return new PlayerKinematicLocomotionTimingSettings();
        }

        public PlayerKinematicLocomotionTimingSettings Clone()
        {
            return new PlayerKinematicLocomotionTimingSettings
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
                    "Player kinematic move duration must be greater than zero.");
            }

            if (KinematicMoveDurationSeconds > MaxKinematicMoveDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(KinematicMoveDurationSeconds),
                    "Player kinematic move duration exceeds the supported maximum.");
            }
        }

        public PlayerKinematicLocomotionTimingSnapshot CreateAuthoritativeSnapshot(
            int simulationTicksPerSecond)
        {
            Validate();

            var ticksPerCell = GameplayTimingProfile.SecondsToEvenCeilTicks(
                KinematicMoveDurationSeconds,
                simulationTicksPerSecond,
                MinTicksPerCell);
            return new PlayerKinematicLocomotionTimingSnapshot(
                KinematicMoveDurationSeconds,
                ticksPerCell,
                ticksPerCell / 2);
        }
    }
}
