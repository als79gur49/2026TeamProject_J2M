using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerTickCommand
    {
        public PlayerTickCommand(
            Direction moveDirection,
            bool pushPressed = false,
            bool flipPressed = false)
        {
            if (moveDirection == Direction.None && (pushPressed || flipPressed))
            {
                throw new ArgumentException("Push and Flip commands require a non-none move direction.", nameof(moveDirection));
            }

            if (moveDirection != Direction.None &&
                moveDirection != Direction.Up &&
                moveDirection != Direction.Right &&
                moveDirection != Direction.Down &&
                moveDirection != Direction.Left)
            {
                throw new ArgumentOutOfRangeException(nameof(moveDirection), moveDirection, "Player commands only support orthogonal move directions.");
            }

            MoveDirection = moveDirection;
            PushPressed = pushPressed;
            FlipPressed = flipPressed;
        }

        public Direction MoveDirection { get; }

        public bool PushPressed { get; }

        public bool FlipPressed { get; }

        public static PlayerTickCommand None => default;

        public static PlayerTickCommand Move(Direction direction)
        {
            return new PlayerTickCommand(direction);
        }

        public static PlayerTickCommand Push(Direction direction)
        {
            return new PlayerTickCommand(direction, pushPressed: true);
        }

        public static PlayerTickCommand Flip(Direction direction)
        {
            return new PlayerTickCommand(direction, flipPressed: true);
        }

        public static PlayerTickCommand Create(
            Direction moveDirection,
            bool pushPressed = false,
            bool flipPressed = false)
        {
            return new PlayerTickCommand(moveDirection, pushPressed, flipPressed);
        }
    }
}
