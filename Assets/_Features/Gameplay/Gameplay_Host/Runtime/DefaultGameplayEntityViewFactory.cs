using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class DefaultGameplayEntityViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
    {
        private readonly float _cellSize;
        private readonly Material _boxMaterial;
        private readonly GameplayEntityView _playerViewPrefab;
        private readonly Material _playerMaterial;
        private readonly Material _projectileMaterial;
        private readonly Transform _parent;
        private readonly int _playerEntityId;
        private readonly IReadOnlyDictionary<int, GameplayEntityView> _enemyViewPrefabsByEntityId;
        private readonly EnemyInactiveVisualSettings _enemyInactiveVisualSettings;
        private readonly IReadOnlyDictionary<int, GameplayEntityView> _staticViewPrefabsByEntityId;
        private readonly Material _unitMaterial;
        private readonly Material _wallMaterial;

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
            _cellSize = cellSize;
            _playerEntityId = playerEntityId;
            _playerViewPrefab = playerViewPrefab;
            _enemyViewPrefabsByEntityId = enemyViewPrefabsByEntityId;
            _staticViewPrefabsByEntityId = staticViewPrefabsByEntityId;
            _enemyInactiveVisualSettings = enemyInactiveVisualSettings;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "DefaultGameplayEntityViewFactory requires the 'Universal Render Pipeline/Unlit' shader.");
            }

            _playerMaterial = CreateMaterial(shader, new Color(0.2f, 0.85f, 0.35f));
            _unitMaterial = CreateMaterial(shader, new Color(0.75f, 0.75f, 0.82f));
            _boxMaterial = CreateMaterial(shader, new Color(0.72f, 0.5f, 0.24f));
            _projectileMaterial = CreateMaterial(shader, new Color(0.9f, 0.4f, 0.2f));
            _wallMaterial = CreateMaterial(shader, new Color(0.25f, 0.28f, 0.33f));
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

            return CreatePrimitiveView(entity);
        }

        public GameplayEntityView PlayerViewPrefab => _playerViewPrefab;

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

        private GameplayEntityView CreatePrimitiveView(in EntityState entity)
        {
            var viewObject = new GameObject($"EntityView_{entity.entityId}");
            viewObject.transform.SetParent(_parent, worldPositionStays: false);
            viewObject.transform.localPosition = Vector3.zero;
            viewObject.transform.localRotation = Quaternion.identity;
            viewObject.transform.localScale = Vector3.one;

            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(entity.entityId);

            if (entity.entityId == _playerEntityId)
            {
                viewObject.AddComponent<PlayerAnimatorDriver>();
                viewObject.AddComponent<PlayerAnimationTimingAuthoring>();
            }
            else if (entity.type == EntityType.Unit &&
                     EntityRolePolicy.IsEnemyUnit(entity))
            {
                viewObject.AddComponent<EnemyAnimatorDriver>();
                var inactiveVisualController = viewObject.AddComponent<EnemyInactiveVisualController>();
                inactiveVisualController.Configure(_enemyInactiveVisualSettings);
                inactiveVisualController.ConfigureLegacyColorFallback(true);
            }

            AttachPrimitiveVisual(view, entity);
            return view;
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
                $"Static presentation prefab for entity {entity.entityId} ({entity.type}) must provide an active Renderer and cannot rely on primitive fallback injection.");
        }

        private static void EnsureEnemyInactiveVisualController(GameplayEntityView view, in EntityState entity)
        {
            if (view == null ||
                !EntityRolePolicy.IsEnemyUnit(entity) ||
                view.GetComponent<EnemyInactiveVisualController>() != null)
            {
                return;
            }

            view.gameObject.AddComponent<EnemyInactiveVisualController>();
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

        private void AttachPrimitiveVisual(GameplayEntityView view, in EntityState entity)
        {
            var visualProfile = GameplayEntityVisualProfile.Create(entity.type, _cellSize);
            view.ConfigureModelRoot(visualProfile.ModelLocalPosition, visualProfile.ModelLocalRotation);

            var modelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            modelObject.name = "Visual";
            modelObject.transform.SetParent(view.ModelRoot, worldPositionStays: false);
            modelObject.transform.localPosition = Vector3.zero;
            modelObject.transform.localRotation = Quaternion.identity;
            modelObject.transform.localScale = visualProfile.ModelLocalScale;

            var collider = modelObject.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(collider);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            var renderer = modelObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ResolveMaterial(entity);
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

        private static Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader)
            {
                color = color,
            };

            return material;
        }

        private Material ResolveMaterial(in EntityState entity)
        {
            if (entity.entityId == _playerEntityId)
            {
                return _playerMaterial;
            }

            switch (entity.type)
            {
                case EntityType.Box:
                    return _boxMaterial;

                case EntityType.None:
                    return _wallMaterial;

                default:
                    return _unitMaterial;
            }
        }
    }
}
