using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Queries
{
    public sealed class GameplayQueryFacade : IGameplayQueryFacade
    {
        public GameplayQueryFacade(
            IGameplaySessionQuery session,
            IGameplayPlayerHudQuery playerHud,
            IGameplayObjectiveQuery objectives)
            : this(
                session,
                EmptyGameplayStageQuery.Instance,
                playerHud,
                objectives)
        {
        }

        public GameplayQueryFacade(
            IGameplaySessionQuery session,
            IGameplayStageQuery stage,
            IGameplayPlayerHudQuery playerHud,
            IGameplayObjectiveQuery objectives)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            PlayerHud = playerHud ?? throw new ArgumentNullException(nameof(playerHud));
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
        }

        public IGameplaySessionQuery Session { get; }

        public IGameplayStageQuery Stage { get; }

        public IGameplayPlayerHudQuery PlayerHud { get; }

        public IGameplayObjectiveQuery Objectives { get; }

        private sealed class EmptyGameplayStageQuery : IGameplayStageQuery
        {
            public static readonly EmptyGameplayStageQuery Instance = new();

            private EmptyGameplayStageQuery()
            {
            }

            public GameplayStageReadModel Read()
            {
                return default;
            }
        }
    }
}
