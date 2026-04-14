using System;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.Gameplay.UIAccess.Queries
{
    public sealed class GameplayQueryFacade : IGameplayQueryFacade
    {
        public GameplayQueryFacade(
            IGameplaySessionQuery session,
            IGameplayPlayerHudQuery playerHud,
            IGameplayObjectiveQuery objectives)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            PlayerHud = playerHud ?? throw new ArgumentNullException(nameof(playerHud));
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
        }

        public IGameplaySessionQuery Session { get; }

        public IGameplayPlayerHudQuery PlayerHud { get; }

        public IGameplayObjectiveQuery Objectives { get; }
    }
}
