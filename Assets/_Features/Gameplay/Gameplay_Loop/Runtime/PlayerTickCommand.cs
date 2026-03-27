using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerTickCommand
    {
        public PlayerTickCommand(
            Direction moveDirection,
            bool interactPressed = false,
            bool throwPressed = false)
        {
            if (moveDirection == Direction.None && (interactPressed || throwPressed))
            {
                throw new ArgumentException("Interact and Throw commands require a non-none move direction.", nameof(moveDirection));
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
            InteractPressed = interactPressed;
            ThrowPressed = throwPressed;
        }

        public Direction MoveDirection { get; }

        public bool InteractPressed { get; }

        public bool ThrowPressed { get; }

        public static PlayerTickCommand None => default;

        public static PlayerTickCommand Move(Direction direction)
        {
            return new PlayerTickCommand(direction);
        }

        public static PlayerTickCommand Interact(Direction direction)
        {
            return new PlayerTickCommand(direction, interactPressed: true);
        }

        public static PlayerTickCommand Throw(Direction direction)
        {
            return new PlayerTickCommand(direction, throwPressed: true);
        }

        public static PlayerTickCommand Create(
            Direction moveDirection,
            bool interactPressed = false,
            bool throwPressed = false)
        {
            return new PlayerTickCommand(moveDirection, interactPressed, throwPressed);
        }
    }
}
