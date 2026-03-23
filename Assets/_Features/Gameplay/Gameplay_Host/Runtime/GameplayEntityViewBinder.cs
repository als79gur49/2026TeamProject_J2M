using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayEntityViewBinder
    {
        private readonly List<GameplayEntityView> _registeredViews = new();
        private readonly IGameplayEntityViewFactory _viewFactory;
        private readonly GameplayEntityViewRegistry _viewRegistry;

        public GameplayEntityViewBinder(
            GameplayEntityViewRegistry viewRegistry,
            IGameplayEntityViewFactory viewFactory)
        {
            _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
            _viewFactory = viewFactory;
        }

        public GameplayEntityView ResolveOrCreate(in EntityState entity)
        {
            if (_viewRegistry.TryGetView(entity.entityId, out var existingView))
            {
                return existingView;
            }

            if (_viewFactory == null)
            {
                return null;
            }

            var createdView = _viewFactory.CreateView(entity);
            if (createdView == null)
            {
                throw new InvalidOperationException("Gameplay entity view factories must not return null.");
            }

            _viewRegistry.Register(createdView);
            return createdView;
        }

        public void HideViewsExcept(HashSet<int> visibleEntityIds)
        {
            if (visibleEntityIds == null)
            {
                throw new ArgumentNullException(nameof(visibleEntityIds));
            }

            _viewRegistry.GetRegisteredViews(_registeredViews);
            for (var i = 0; i < _registeredViews.Count; i++)
            {
                var view = _registeredViews[i];
                if (visibleEntityIds.Contains(view.EntityId))
                {
                    continue;
                }

                if (view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }
    }
}
