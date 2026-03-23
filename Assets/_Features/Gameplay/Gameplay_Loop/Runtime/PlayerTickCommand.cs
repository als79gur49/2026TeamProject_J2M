using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public enum PlayerPrimaryCommandKind
    {
        None = 0,
        Move = 1,
        InteractSlide = 2,
    }

    public readonly struct PlayerTickCommand
    {
        public PlayerTickCommand(PlayerPrimaryCommandKind primaryKind, Direction direction)
        {
            if (primaryKind == PlayerPrimaryCommandKind.None)
            {
                if (direction != Direction.None)
                {
                    throw new ArgumentException("None commands cannot specify a direction.", nameof(direction));
                }
            }
            else if (direction == Direction.None)
            {
                throw new ArgumentException("Primary player commands must specify a non-none direction.", nameof(direction));
            }

            PrimaryKind = primaryKind;
            Direction = primaryKind == PlayerPrimaryCommandKind.None ? Direction.None : direction;
        }

        public PlayerPrimaryCommandKind PrimaryKind { get; }

        public Direction Direction { get; }

        public static PlayerTickCommand None => default;

        public static PlayerTickCommand Move(Direction direction)
        {
            return new PlayerTickCommand(PlayerPrimaryCommandKind.Move, direction);
        }

        public static PlayerTickCommand InteractSlide(Direction direction)
        {
            return new PlayerTickCommand(PlayerPrimaryCommandKind.InteractSlide, direction);
        }
    }
}
