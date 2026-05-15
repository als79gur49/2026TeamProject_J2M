namespace Game.Feature.Gameplay.BoardState
{
    public enum Direction
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 3,
        Left = 4,
    }

    public static class DirectionUtility
    {
        public static Direction Opposite(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }

        public static bool IsCardinal(Direction direction)
        {
            return direction is Direction.Up or Direction.Right or Direction.Down or Direction.Left;
        }
    }
}
