using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public static class PlayerFree2DLocomotionDefaults
    {
        public const float SecondsPerCellAtFullSpeed = 0.33333334f;
        public const float MaxSecondsPerCellAtFullSpeed = 2f;
        public const float CollisionRadiusCells = 0f;
        public const float ActionAssistSettleWindowCells = 0.125f;
        public const int MinTicksPerCell = 2;
    }

    public readonly struct PlayerFree2DLocomotionSettings
    {
        public PlayerFree2DLocomotionSettings(
            float secondsPerCellAtFullSpeed,
            int ticksPerCell,
            int speedUnitsPerTick,
            int unitsPerTickRemainder)
            : this(
                secondsPerCellAtFullSpeed,
                ticksPerCell,
                speedUnitsPerTick,
                unitsPerTickRemainder,
                collisionRadiusUnits: 0)
        {
        }

        public PlayerFree2DLocomotionSettings(
            float secondsPerCellAtFullSpeed,
            int ticksPerCell,
            int speedUnitsPerTick,
            int unitsPerTickRemainder,
            int collisionRadiusUnits)
            : this(
                secondsPerCellAtFullSpeed,
                ticksPerCell,
                speedUnitsPerTick,
                unitsPerTickRemainder,
                collisionRadiusUnits,
                DefaultActionAssistSettleWindowUnits)
        {
        }

        public PlayerFree2DLocomotionSettings(
            float secondsPerCellAtFullSpeed,
            int ticksPerCell,
            int speedUnitsPerTick,
            int unitsPerTickRemainder,
            int collisionRadiusUnits,
            int actionAssistSettleWindowUnits)
        {
            SecondsPerCellAtFullSpeed = secondsPerCellAtFullSpeed;
            TicksPerCell = ticksPerCell;
            SpeedUnitsPerTick = speedUnitsPerTick;
            UnitsPerTickRemainder = unitsPerTickRemainder;
            CollisionRadiusUnits = collisionRadiusUnits;
            ActionAssistSettleWindowUnits = Math.Max(0, actionAssistSettleWindowUnits);
        }

        public float SecondsPerCellAtFullSpeed { get; }

        public int TicksPerCell { get; }

        public int SpeedUnitsPerTick { get; }

        public int UnitsPerTickRemainder { get; }

        public int CollisionRadiusUnits { get; }

        public int ActionAssistSettleWindowUnits { get; }

        public bool IsConfigured => TicksPerCell > 0 && SpeedUnitsPerTick > 0;

        private static int DefaultActionAssistSettleWindowUnits => (int)Math.Round(
            PlayerFree2DLocomotionDefaults.ActionAssistSettleWindowCells *
            KinematicFixed.UnitsPerCell,
            MidpointRounding.AwayFromZero);
    }

    [Serializable]
    public struct PlayerFree2DLocomotionAuthoring
    {
        public float SecondsPerCellAtFullSpeed;
        public float CollisionRadiusCells;
        public float ActionAssistSettleWindowCells;

        public static PlayerFree2DLocomotionAuthoring CreateDefault()
        {
            return new PlayerFree2DLocomotionAuthoring
            {
                SecondsPerCellAtFullSpeed = PlayerFree2DLocomotionDefaults.SecondsPerCellAtFullSpeed,
                CollisionRadiusCells = PlayerFree2DLocomotionDefaults.CollisionRadiusCells,
                ActionAssistSettleWindowCells = PlayerFree2DLocomotionDefaults.ActionAssistSettleWindowCells,
            };
        }

        public void Validate()
        {
            if (float.IsNaN(SecondsPerCellAtFullSpeed) ||
                float.IsInfinity(SecondsPerCellAtFullSpeed) ||
                SecondsPerCellAtFullSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(SecondsPerCellAtFullSpeed),
                    "Player Free2D seconds per cell at full speed must be greater than zero.");
            }

            if (SecondsPerCellAtFullSpeed > PlayerFree2DLocomotionDefaults.MaxSecondsPerCellAtFullSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(SecondsPerCellAtFullSpeed),
                    "Player Free2D seconds per cell at full speed exceeds the supported maximum.");
            }

            if (float.IsNaN(CollisionRadiusCells) ||
                float.IsInfinity(CollisionRadiusCells) ||
                CollisionRadiusCells < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CollisionRadiusCells),
                    "Player Free2D collision radius must be zero or greater.");
            }

            if (CollisionRadiusCells >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CollisionRadiusCells),
                    "Player Free2D collision radius must be less than half a cell.");
            }

            if (float.IsNaN(ActionAssistSettleWindowCells) ||
                float.IsInfinity(ActionAssistSettleWindowCells) ||
                ActionAssistSettleWindowCells < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ActionAssistSettleWindowCells),
                    "Player Free2D action assist settle window must be zero or greater.");
            }

            if (ActionAssistSettleWindowCells >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ActionAssistSettleWindowCells),
                    "Player Free2D action assist settle window must be less than half a cell.");
            }
        }

        public PlayerFree2DLocomotionSettings Compile(
            int simulationTicksPerSecond)
        {
            Validate();

            var ticksPerCell = GameplayTimingProfile.SecondsToEvenCeilTicks(
                SecondsPerCellAtFullSpeed,
                simulationTicksPerSecond,
                PlayerFree2DLocomotionDefaults.MinTicksPerCell);
            var speedUnitsPerTick = Math.Max(1, KinematicFixed.UnitsPerCell / ticksPerCell);
            var remainder = KinematicFixed.UnitsPerCell % ticksPerCell;
            var collisionRadiusUnits = (int)Math.Round(
                CollisionRadiusCells * KinematicFixed.UnitsPerCell,
                MidpointRounding.AwayFromZero);
            var actionAssistSettleWindowUnits = (int)Math.Round(
                ActionAssistSettleWindowCells * KinematicFixed.UnitsPerCell,
                MidpointRounding.AwayFromZero);
            return new PlayerFree2DLocomotionSettings(
                SecondsPerCellAtFullSpeed,
                ticksPerCell,
                speedUnitsPerTick,
                remainder,
                collisionRadiusUnits,
                actionAssistSettleWindowUnits);
        }
    }
}
