using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public interface IGameplayCommandGateway
    {
        GameplayCommandAcceptance SetHeldMoveDirection(GameplayUiDirection direction);

        GameplayCommandAcceptance ClearHeldMoveDirection();
    }
}
