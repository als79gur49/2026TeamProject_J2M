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
            : this(
                moveDurationSecondsPerCell,
                ticksPerCell,
                speedUnitsPerTick,
                unitsPerTickRemainder,
                collisionRadiusUnits: 0)
        {
        }

        public PlayerContinuousLocomotionSnapshot(
            float moveDurationSecondsPerCell,
            int ticksPerCell,
            int speedUnitsPerTick,
            int unitsPerTickRemainder,
            int collisionRadiusUnits)
        {
            MoveDurationSecondsPerCell = moveDurationSecondsPerCell;
            TicksPerCell = ticksPerCell;
            SpeedUnitsPerTick = speedUnitsPerTick;
            UnitsPerTickRemainder = unitsPerTickRemainder;
            CollisionRadiusUnits = collisionRadiusUnits;
        }

        public float MoveDurationSecondsPerCell { get; }

        public int TicksPerCell { get; }

        public int SpeedUnitsPerTick { get; }

        public int UnitsPerTickRemainder { get; }

        public int CollisionRadiusUnits { get; }

        public bool IsConfigured => TicksPerCell > 0 && SpeedUnitsPerTick > 0;
    }

    [Serializable]
    public sealed class PlayerContinuousLocomotionSettings
    {
        public const float DefaultMoveDurationSecondsPerCell =
            PlayerKinematicLocomotionTimingSettings.DefaultKinematicMoveDurationSeconds;
        public const float DefaultCollisionRadiusCells = 0f;
        public const float MaxMoveDurationSecondsPerCell = PlayerKinematicLocomotionTimingSettings.MaxKinematicMoveDurationSeconds;
        public const int MinTicksPerCell = 2;

        public float MoveDurationSecondsPerCell = DefaultMoveDurationSecondsPerCell;
        public float CollisionRadiusCells = DefaultCollisionRadiusCells;

        public static PlayerContinuousLocomotionSettings CreateDefault()
        {
            return new PlayerContinuousLocomotionSettings();
        }

        public PlayerContinuousLocomotionSettings Clone()
        {
            return new PlayerContinuousLocomotionSettings
            {
                MoveDurationSecondsPerCell = MoveDurationSecondsPerCell,
                CollisionRadiusCells = CollisionRadiusCells,
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

            if (float.IsNaN(CollisionRadiusCells) ||
                float.IsInfinity(CollisionRadiusCells) ||
                CollisionRadiusCells < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CollisionRadiusCells),
                    "Player continuous collision radius must be zero or greater.");
            }

            if (CollisionRadiusCells >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CollisionRadiusCells),
                    "Player continuous collision radius must be less than half a cell.");
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
            var collisionRadiusUnits = (int)Math.Round(
                CollisionRadiusCells * KinematicFixed.UnitsPerCell,
                MidpointRounding.AwayFromZero);
            return new PlayerContinuousLocomotionSnapshot(
                MoveDurationSecondsPerCell,
                ticksPerCell,
                speedUnitsPerTick,
                remainder,
                collisionRadiusUnits);
        }
    }
}
