using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringFacingDisplayUtility
    {
        public static string ToFacingLabel(Direction facing)
        {
            return IsValidFacing(facing) ? facing.ToString() : $"Unknown ({(int)facing})";
        }

        public static string ToFacingArrow(Direction facing)
        {
            return facing switch
            {
                Direction.Up => "↑",
                Direction.Right => "→",
                Direction.Down => "↓",
                Direction.Left => "←",
                Direction.None => "-",
                _ => "?",
            };
        }

        public static Direction RotateClockwise(Direction facing)
        {
            return facing switch
            {
                Direction.None => Direction.Right,
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => facing,
            };
        }

        public static Direction RotateCounterClockwise(Direction facing)
        {
            return facing switch
            {
                Direction.None => Direction.Left,
                Direction.Up => Direction.Left,
                Direction.Left => Direction.Down,
                Direction.Down => Direction.Right,
                Direction.Right => Direction.Up,
                _ => facing,
            };
        }

        public static bool IsValidFacing(Direction facing)
        {
            return facing == Direction.None ||
                   facing == Direction.Up ||
                   facing == Direction.Right ||
                   facing == Direction.Down ||
                   facing == Direction.Left;
        }
    }
}
