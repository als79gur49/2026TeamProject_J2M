using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public interface IGameplayCommandGateway
    {
        GameplayCommandAcceptance SetHeldMoveDirection(Direction direction);

        GameplayCommandAcceptance ClearHeldMoveDirection();

        GameplayCommandAcceptance RequestFlip(Direction direction);
    }
}
