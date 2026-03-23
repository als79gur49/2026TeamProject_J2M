using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private readonly HashSet<int> _visibleEntityIds = new();

        private float _cellSize = 1f;
        private Vector3 _gridOrigin = Vector3.zero;
        private GameplayEntityViewBinder _viewBinder;
        private bool _isInitialized;

        public void Initialize(
            GameplayEntityViewBinder viewBinder,
            Vector3 gridOrigin,
            float cellSize)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            _viewBinder = viewBinder;
            _gridOrigin = gridOrigin;
            _cellSize = cellSize;
            _isInitialized = true;
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            EnsureInitialized();

            PresentEntities(result.FinalEntities);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();
            PresentEntities(entities);
        }

        public Vector3 ResolveWorldPosition(Vector2Int gridPosition)
        {
            return _gridOrigin + new Vector3(gridPosition.x * _cellSize, gridPosition.y * _cellSize, 0f);
        }

        private void PresentEntities(IReadOnlyList<EntityState> entities)
        {
            _visibleEntityIds.Clear();

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                _visibleEntityIds.Add(entity.entityId);

                var view = _viewBinder.ResolveOrCreate(entity);
                if (view == null)
                {
                    continue;
                }

                if (!view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(true);
                }

                view.transform.position = ResolveWorldPosition(entity.position);
            }

            _viewBinder.HideViewsExcept(_visibleEntityIds);
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayTickViewPresenter must be initialized before use.");
            }
        }
    }
}
