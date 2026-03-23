using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct PlayerTickCommand
    {
        public PlayerTickCommand(Direction moveDirection, bool hasMove)
        {
            if (hasMove && moveDirection == Direction.None)
            {
                throw new ArgumentException("Player move commands must specify a non-none direction.", nameof(moveDirection));
            }

            MoveDirection = hasMove ? moveDirection : Direction.None;
            HasMove = hasMove;
        }

        public Direction MoveDirection { get; }

        public bool HasMove { get; }

        public static PlayerTickCommand None => default;

        public static PlayerTickCommand Move(Direction moveDirection)
        {
            return new PlayerTickCommand(moveDirection, hasMove: true);
        }
    }
}
