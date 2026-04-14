using Game.Feature.Gameplay.UIAccess.Queries;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public interface IGameplayQueryFacade
    {
        IGameplaySessionQuery Session { get; }

        IGameplayPlayerHudQuery PlayerHud { get; }

        IGameplayObjectiveQuery Objectives { get; }
    }
}
