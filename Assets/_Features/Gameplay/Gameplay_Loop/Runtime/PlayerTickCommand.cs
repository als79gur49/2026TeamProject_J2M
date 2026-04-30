using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerTickCommand
    {
        public PlayerTickCommand(
            Direction moveDirection,
            bool pushPressed = false,
            bool flipPressed = false,
            bool isMoveBuffered = false,
            Direction heldMoveDirection = Direction.None)
        {
            if (moveDirection == Direction.None &&
                flipPressed &&
                !pushPressed)
            {
                throw new ArgumentException("Flip commands require a non-none move direction.", nameof(moveDirection));
            }

            if (moveDirection == Direction.None && isMoveBuffered)
            {
                throw new ArgumentException("Buffered move commands require a non-none move direction.", nameof(moveDirection));
            }

            if (moveDirection != Direction.None &&
                moveDirection != Direction.Up &&
                moveDirection != Direction.Right &&
                moveDirection != Direction.Down &&
                moveDirection != Direction.Left)
            {
                throw new ArgumentOutOfRangeException(nameof(moveDirection), moveDirection, "Player commands only support orthogonal move directions.");
            }

            if (heldMoveDirection != Direction.None &&
                heldMoveDirection != Direction.Up &&
                heldMoveDirection != Direction.Right &&
                heldMoveDirection != Direction.Down &&
                heldMoveDirection != Direction.Left)
            {
                throw new ArgumentOutOfRangeException(nameof(heldMoveDirection), heldMoveDirection, "Player held move directions only support orthogonal move directions.");
            }

            MoveDirection = moveDirection;
            PushPressed = pushPressed;
            FlipPressed = flipPressed;
            IsMoveBuffered = isMoveBuffered;
            HeldMoveDirection = heldMoveDirection == Direction.None &&
                                moveDirection != Direction.None &&
                                !pushPressed &&
                                !flipPressed &&
                                !isMoveBuffered
                ? moveDirection
                : heldMoveDirection;
        }

        public Direction MoveDirection { get; }

        public bool PushPressed { get; }

        public bool FlipPressed { get; }

        public bool IsMoveBuffered { get; }

        public Direction HeldMoveDirection { get; }

        public static PlayerTickCommand None => default;

        public static PlayerTickCommand Move(Direction direction, bool isMoveBuffered = false)
        {
            return new PlayerTickCommand(
                direction,
                isMoveBuffered: isMoveBuffered,
                heldMoveDirection: isMoveBuffered ? Direction.None : direction);
        }

        public static PlayerTickCommand Push(
            Direction direction,
            bool isMoveBuffered = false,
            Direction heldMoveDirection = Direction.None)
        {
            return new PlayerTickCommand(
                direction,
                pushPressed: true,
                isMoveBuffered: isMoveBuffered,
                heldMoveDirection: heldMoveDirection);
        }

        public static PlayerTickCommand Flip(Direction direction, Direction heldMoveDirection = Direction.None)
        {
            return new PlayerTickCommand(direction, flipPressed: true, heldMoveDirection: heldMoveDirection);
        }

        public static PlayerTickCommand Create(
            Direction moveDirection,
            bool pushPressed = false,
            bool flipPressed = false,
            bool isMoveBuffered = false,
            Direction heldMoveDirection = Direction.None)
        {
            return new PlayerTickCommand(moveDirection, pushPressed, flipPressed, isMoveBuffered, heldMoveDirection);
        }
    }
}
