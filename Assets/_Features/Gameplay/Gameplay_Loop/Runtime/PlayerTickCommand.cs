using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerTickCommand
    {
        public PlayerTickCommand(
            Direction moveDirection,
            bool flipPressed = false)
            : this(moveDirection, flipPressed, legacyPushRequested: false)
        {
        }

        private PlayerTickCommand(
            Direction moveDirection,
            bool flipPressed,
            bool legacyPushRequested)
        {
            if (moveDirection == Direction.None && flipPressed)
            {
                throw new ArgumentException("Flip commands require a non-none move direction.", nameof(moveDirection));
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
            FlipPressed = flipPressed;
            LegacyPushRequested = legacyPushRequested;
        }

        public Direction MoveDirection { get; }

        public bool FlipPressed { get; }

        internal bool LegacyPushRequested { get; }

        public static PlayerTickCommand None => default;

        public static PlayerTickCommand Move(Direction direction)
        {
            return new PlayerTickCommand(direction);
        }

        [Obsolete("Push button input is no longer authoritative. Use Move(direction) and player control hold-to-push semantics.")]
        public static PlayerTickCommand Push(Direction direction)
        {
            return new PlayerTickCommand(direction, flipPressed: false, legacyPushRequested: true);
        }

        public static PlayerTickCommand Flip(Direction direction)
        {
            return new PlayerTickCommand(direction, flipPressed: true);
        }

        public static PlayerTickCommand Create(
            Direction moveDirection,
            bool flipPressed = false)
        {
            return new PlayerTickCommand(moveDirection, flipPressed);
        }

        [Obsolete("Push button input is no longer authoritative. Use Create(moveDirection, flipPressed) instead.")]
        public static PlayerTickCommand Create(
            Direction moveDirection,
            bool pushPressed,
            bool flipPressed)
        {
            return new PlayerTickCommand(moveDirection, flipPressed, pushPressed);
        }
    }
}
