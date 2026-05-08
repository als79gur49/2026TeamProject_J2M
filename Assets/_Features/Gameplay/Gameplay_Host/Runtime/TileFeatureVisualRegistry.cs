using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class TileFeatureVisualRegistry : MonoBehaviour, ITileFeatureVisualRegistry
    {
        private readonly Dictionary<int, ITileFeatureVisualTarget> _targetsByTileId = new();
        private Transform _searchRoot;

        public Transform SearchRoot => _searchRoot != null ? _searchRoot : transform;

        private void Awake()
        {
            _searchRoot ??= transform;
            Rebuild();
        }

        private void OnTransformChildrenChanged()
        {
            Rebuild();
        }

        public void ConfigureSearchRoot(Transform searchRoot)
        {
            _searchRoot = searchRoot != null ? searchRoot : transform;
            Rebuild();
        }

        public void Register(ITileFeatureVisualTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.TileId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(target), "Registered tile feature visuals must expose a positive tile ID.");
            }

            if (_targetsByTileId.ContainsKey(target.TileId))
            {
                UnityEngine.Debug.LogWarning($"Duplicate tile feature visual target registration for TileId {target.TileId}; keeping the first registered target.");
                return;
            }

            _targetsByTileId.Add(target.TileId, target);
        }

        public void Rebuild()
        {
            _targetsByTileId.Clear();

            var targets = SearchRoot.GetComponentsInChildren<TileFeatureVisualTargetView>(includeInactive: true);
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null ||
                    target.TileId <= 0)
                {
                    continue;
                }

                if (_targetsByTileId.ContainsKey(target.TileId))
                {
                    UnityEngine.Debug.LogWarning($"Duplicate tile feature visual target discovered for TileId {target.TileId}; keeping the first discovered target.");
                    continue;
                }

                _targetsByTileId.Add(target.TileId, target);
            }
        }

        public bool TryGetTileVisual(int tileId, out ITileFeatureVisualTarget target)
        {
            if (!_targetsByTileId.TryGetValue(tileId, out target))
            {
                return false;
            }

            if (target is UnityEngine.Object unityTarget &&
                unityTarget == null)
            {
                _targetsByTileId.Remove(tileId);
                target = null;
                return false;
            }

            return target != null;
        }

        public bool Unregister(int tileId)
        {
            return _targetsByTileId.Remove(tileId);
        }
    }
}
