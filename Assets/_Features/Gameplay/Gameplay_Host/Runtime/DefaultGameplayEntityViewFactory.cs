using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class DefaultGameplayEntityViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
    {
        private readonly GameplayEntityView _playerViewPrefab;
        private readonly Transform _parent;
        private readonly int _playerEntityId;
        private readonly IReadOnlyDictionary<int, GameplayEntityView> _enemyViewPrefabsByEntityId;
        private readonly EnemyInactiveVisualSettings _enemyInactiveVisualSettings;
        private readonly IReadOnlyDictionary<int, GameplayEntityView> _staticViewPrefabsByEntityId;

        public DefaultGameplayEntityViewFactory(
            Transform parent,
            float cellSize,
            int playerEntityId,
            GameplayEntityView playerViewPrefab = null,
            IReadOnlyDictionary<int, GameplayEntityView> enemyViewPrefabsByEntityId = null,
            IReadOnlyDictionary<int, GameplayEntityView> staticViewPrefabsByEntityId = null,
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null)
        {
            _parent = parent;
            _playerEntityId = playerEntityId;
            _playerViewPrefab = playerViewPrefab;
            _enemyViewPrefabsByEntityId = enemyViewPrefabsByEntityId;
            _staticViewPrefabsByEntityId = staticViewPrefabsByEntityId;
            _enemyInactiveVisualSettings = enemyInactiveVisualSettings;
        }

        public GameplayEntityView CreateView(in EntityState entity)
        {
            if (entity.entityId == _playerEntityId &&
                _playerViewPrefab != null)
            {
                return CreatePlayerPrefabView(entity);
            }

            if (TryCreateEnemyPrefabView(entity, out var enemyView))
            {
                return enemyView;
            }

            if (TryCreateStaticPrefabView(entity, out var staticView))
            {
                return staticView;
            }

            throw new InvalidOperationException(
                $"DefaultGameplayEntityViewFactory cannot create View for entity {entity.entityId} ({entity.type}): {DescribeMissingSupply(entity)}.");
        }

        public GameplayEntityView PlayerViewPrefab => _playerViewPrefab;

        private string DescribeMissingSupply(in EntityState entity)
        {
            if (entity.entityId == _playerEntityId)
            {
                return "PlayerViewPrefab was not supplied";
            }

            if (entity.type == EntityType.Unit)
            {
                return DescribeMissingBinding(_enemyViewPrefabsByEntityId, entity.entityId, "Enemy");
            }

            if (IsStaticPresentationCandidate(entity))
            {
                return DescribeMissingBinding(_staticViewPrefabsByEntityId, entity.entityId, "static");
            }

            return "unsupported entity type has no prefab supply path";
        }

        private static string DescribeMissingBinding(
            IReadOnlyDictionary<int, GameplayEntityView> prefabs,
            int entityId,
            string supplyKind)
        {
            if (prefabs == null)
            {
                return $"{supplyKind} prefab dictionary was not supplied";
            }

            return !prefabs.ContainsKey(entityId)
                ? $"{supplyKind} prefab binding is missing for entity ID {entityId}"
                : $"{supplyKind} prefab binding for entity ID {entityId} references a null prefab";
        }

        private GameplayEntityView CreatePlayerPrefabView(in EntityState entity)
        {
            PlayerViewPrefabRequirements.ValidatePlayerViewPrefab(_playerViewPrefab, nameof(DefaultGameplayEntityViewFactory));
            var instance = UnityEngine.Object.Instantiate(_playerViewPrefab, _parent);
            ResetViewTransform(instance, entity.entityId);
            SanitizePrefabPhysics(instance);
            PlayerViewPrefabRequirements.ValidatePlayerViewInstance(instance, nameof(DefaultGameplayEntityViewFactory));
            return instance;
        }

        private bool TryCreateEnemyPrefabView(in EntityState entity, out GameplayEntityView view)
        {
            view = null;

            if (entity.type != EntityType.Unit ||
                _enemyViewPrefabsByEntityId == null ||
                !_enemyViewPrefabsByEntityId.TryGetValue(entity.entityId, out var prefab) ||
                prefab == null)
            {
                return false;
            }

            var instance = GameplayEnemyViewPrefabInstantiator.InstantiateEnemyView(
                prefab,
                _parent,
                entity,
                nameof(DefaultGameplayEntityViewFactory),
                _enemyInactiveVisualSettings);
            view = instance;
            return true;
        }

        private bool TryCreateStaticPrefabView(in EntityState entity, out GameplayEntityView view)
        {
            view = null;

            if (!IsStaticPresentationCandidate(entity) ||
                _staticViewPrefabsByEntityId == null ||
                !_staticViewPrefabsByEntityId.TryGetValue(entity.entityId, out var prefab) ||
                prefab == null)
            {
                return false;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, _parent);
            ResetViewTransform(instance, entity.entityId);
            SanitizePrefabPhysics(instance);
            ValidateStaticPrefabVisual(instance, entity);
            view = instance;
            return true;
        }

        private static void ResetViewTransform(GameplayEntityView view, int entityId)
        {
            view.name = $"EntityView_{entityId}";
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = Vector3.one;
            view.Initialize(entityId);
        }

        private static void ValidateStaticPrefabVisual(GameplayEntityView view, in EntityState entity)
        {
            if (view == null)
            {
                throw new InvalidOperationException("Static presentation prefab instance cannot be null.");
            }

            if (view.GetComponentInChildren<Renderer>(includeInactive: false) != null)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Static presentation prefab for entity {entity.entityId} ({entity.type}) must provide an active Renderer.");
        }

        private static bool IsStaticPresentationCandidate(in EntityState entity)
        {
            return entity.type == EntityType.Box ||
                   entity.type == EntityType.None;
        }

        private static void SanitizePrefabPhysics(GameplayEntityView view)
        {
            if (view == null)
            {
                return;
            }

            // Runtime entity views are driven by the authoritative board simulation, not Unity physics.
            // Disable physics components immediately to prevent interference, then destroy them so authored
            // prefabs remain presentation-only shells around simulation entities.
            var colliders = view.GetComponentsInChildren<Collider>(includeInactive: true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = false;
                DestroyComponent(collider);
            }

            var rigidbodies = view.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                var rigidbody = rigidbodies[i];
                if (rigidbody == null)
                {
                    continue;
                }

                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
                DestroyComponent(rigidbody);
            }
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(component);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

    }
}
