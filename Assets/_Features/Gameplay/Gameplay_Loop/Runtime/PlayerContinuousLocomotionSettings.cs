using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerContinuousLocomotionSnapshot
    {
        public PlayerContinuousLocomotionSnapshot(
            float moveDurationSecondsPerCell,
            int ticksPerCell,
            int speedUnitsPerTick,
            int unitsPerTickRemainder)
        {
            MoveDurationSecondsPerCell = moveDurationSecondsPerCell;
            TicksPerCell = ticksPerCell;
            SpeedUnitsPerTick = speedUnitsPerTick;
            UnitsPerTickRemainder = unitsPerTickRemainder;
        }

        public float MoveDurationSecondsPerCell { get; }

        public int TicksPerCell { get; }

        public int SpeedUnitsPerTick { get; }

        public int UnitsPerTickRemainder { get; }

        public bool IsConfigured => TicksPerCell > 0 && SpeedUnitsPerTick > 0;
    }

    [Serializable]
    public sealed class PlayerContinuousLocomotionSettings
    {
        public const float DefaultMoveDurationSecondsPerCell =
            PlayerKinematicLocomotionTimingSettings.DefaultKinematicMoveDurationSeconds;
        public const float MaxMoveDurationSecondsPerCell = PlayerKinematicLocomotionTimingSettings.MaxKinematicMoveDurationSeconds;
        public const int MinTicksPerCell = 2;

        public float MoveDurationSecondsPerCell = DefaultMoveDurationSecondsPerCell;

        public static PlayerContinuousLocomotionSettings CreateDefault()
        {
            return new PlayerContinuousLocomotionSettings();
        }

        public PlayerContinuousLocomotionSettings Clone()
        {
            return new PlayerContinuousLocomotionSettings
            {
                MoveDurationSecondsPerCell = MoveDurationSecondsPerCell,
            };
        }

        public void Validate()
        {
            if (float.IsNaN(MoveDurationSecondsPerCell) ||
                float.IsInfinity(MoveDurationSecondsPerCell) ||
                MoveDurationSecondsPerCell <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MoveDurationSecondsPerCell),
                    "Player continuous move duration must be greater than zero.");
            }

            if (MoveDurationSecondsPerCell > MaxMoveDurationSecondsPerCell)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MoveDurationSecondsPerCell),
                    "Player continuous move duration exceeds the supported maximum.");
            }
        }

        public PlayerContinuousLocomotionSnapshot CreateAuthoritativeSnapshot(
            int simulationTicksPerSecond)
        {
            Validate();

            var ticksPerCell = GameplayTimingProfile.SecondsToEvenCeilTicks(
                MoveDurationSecondsPerCell,
                simulationTicksPerSecond,
                MinTicksPerCell);
            var speedUnitsPerTick = Math.Max(1, KinematicFixed.UnitsPerCell / ticksPerCell);
            var remainder = KinematicFixed.UnitsPerCell % ticksPerCell;
            return new PlayerContinuousLocomotionSnapshot(
                MoveDurationSecondsPerCell,
                ticksPerCell,
                speedUnitsPerTick,
                remainder);
        }
    }
}
