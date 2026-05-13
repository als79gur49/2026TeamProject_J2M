using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayEntityViewRegistry : MonoBehaviour
    {
        private readonly Dictionary<int, GameplayEntityView> _viewsByEntityId = new();
        private Transform _searchRoot;

        public event Action<GameplayEntityView> ViewRegistered;

        private void Awake()
        {
            _searchRoot ??= transform;
            Rebuild();
        }

        private void OnTransformChildrenChanged()
        {
            Rebuild();
        }

        public Transform SearchRoot => _searchRoot != null ? _searchRoot : transform;

        public void ConfigureSearchRoot(Transform searchRoot)
        {
            _searchRoot = searchRoot != null ? searchRoot : transform;
            Rebuild();
        }

        public void GetRegisteredViews(List<GameplayEntityView> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var view in _viewsByEntityId.Values)
            {
                buffer.Add(view);
            }
        }

        public void Register(GameplayEntityView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (view.EntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(view), "Registered views must expose a positive entity ID.");
            }

            _viewsByEntityId[view.EntityId] = view;
            ViewRegistered?.Invoke(view);
        }

        public void Rebuild()
        {
            _viewsByEntityId.Clear();

            var views = SearchRoot.GetComponentsInChildren<GameplayEntityView>(includeInactive: true);
            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || view.EntityId <= 0)
                {
                    continue;
                }

                _viewsByEntityId[view.EntityId] = view;
            }
        }

        public bool TryGetView(int entityId, out GameplayEntityView view)
        {
            return _viewsByEntityId.TryGetValue(entityId, out view);
        }

        public bool Unregister(int entityId)
        {
            return _viewsByEntityId.Remove(entityId);
        }
    }
}
