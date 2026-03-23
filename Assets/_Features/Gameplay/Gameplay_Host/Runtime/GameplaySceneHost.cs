using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplaySceneHost : MonoBehaviour
    {
        private GameplayInputHost _inputHost;
        private GameplayTickViewPresenter _presenter;
        private GameplayEntityViewBinder _viewBinder;
        private GameplayEntityViewRegistry _viewRegistry;

        public GameplayInputHost InputHost => _inputHost;

        public TickInputBuffer InputBuffer { get; private set; }

        public GameplayTickViewPresenter Presenter => _presenter;

        public TickRunner TickRunner { get; private set; }

        public GameplayEntityViewRegistry ViewRegistry => _viewRegistry;

        public WorldState WorldState { get; private set; }

        public void Initialize(GameplaySceneHostConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.PlayerEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration), "Player entity ID must be positive.");
            }

            EnsureComponents();

            var initialEntities = configuration.InitialEntities ?? Array.Empty<EntityState>();

            WorldState = GameplayCompositionRoot.CreateWorldState(initialEntities);
            InputBuffer = new TickInputBuffer();
            var staticEntityLogics = BuildStaticEntityLogics(configuration);
            TickRunner = GameplayCompositionRoot.CreateTickRunner(
                WorldState,
                staticEntityLogics,
                InputBuffer,
                startTickIndex: 1);

            _viewRegistry.Rebuild();
            var viewFactory = configuration.ViewFactory ??
                (configuration.AutoCreateViews
                    ? new DefaultGameplayEntityViewFactory(_viewRegistry.transform, configuration.CellSize, configuration.PlayerEntityId)
                    : null);
            _viewBinder = new GameplayEntityViewBinder(_viewRegistry, viewFactory);
            _presenter.Initialize(_viewBinder, configuration.GridOrigin, configuration.CellSize);
            _presenter.PresentInitial(initialEntities);

            _inputHost.Initialize(
                InputBuffer,
                TickRunner,
                _presenter,
                configuration.Actions,
                configuration.TickIntervalSeconds,
                configuration.MoveDeadzone,
                configuration.InitialMoveDelayTicks,
                configuration.RepeatedMoveIntervalTicks,
                configuration.DirectionChangeConsumesDelay,
                configuration.AutoAdvanceTicks);
        }

        private void EnsureComponents()
        {
            _inputHost = GetComponent<GameplayInputHost>() ?? gameObject.AddComponent<GameplayInputHost>();
            _presenter = GetComponent<GameplayTickViewPresenter>() ?? gameObject.AddComponent<GameplayTickViewPresenter>();
            _viewRegistry = GetComponent<GameplayEntityViewRegistry>() ?? gameObject.AddComponent<GameplayEntityViewRegistry>();
        }

        private static IReadOnlyList<IEntityLogic> BuildStaticEntityLogics(GameplaySceneHostConfiguration configuration)
        {
            var entityLogics = new List<IEntityLogic>
            {
                new PlayerLogic(configuration.PlayerEntityId),
            };

            if (configuration.StaticEntityLogics == null)
            {
                return entityLogics;
            }

            for (var i = 0; i < configuration.StaticEntityLogics.Length; i++)
            {
                var entityLogic = configuration.StaticEntityLogics[i];
                if (entityLogic == null)
                {
                    throw new ArgumentException("Static entity logic collections cannot contain null entries.", nameof(configuration));
                }

                entityLogics.Add(entityLogic);
            }

            return entityLogics;
        }
    }
}
