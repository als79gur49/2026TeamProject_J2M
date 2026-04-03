using System;
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
        private readonly Material _unitMaterial;
        private readonly Material _wallMaterial;

        public DefaultGameplayEntityViewFactory(
            Transform parent,
            float cellSize,
            int playerEntityId,
            GameplayEntityView playerViewPrefab = null)
        {
            _parent = parent;
            _cellSize = cellSize;
            _playerEntityId = playerEntityId;
            _playerViewPrefab = playerViewPrefab;
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
                PlayerViewPrefabRequirements.ValidatePlayerViewPrefab(_playerViewPrefab, nameof(DefaultGameplayEntityViewFactory));
                var instance = UnityEngine.Object.Instantiate(_playerViewPrefab, _parent);
                instance.name = $"EntityView_{entity.entityId}";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                instance.Initialize(entity.entityId);
                PlayerViewPrefabRequirements.ValidatePlayerViewInstance(instance, nameof(DefaultGameplayEntityViewFactory));
                return instance;
            }

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
                     entity.aiMode != EnemyAiMode.None)
            {
                viewObject.AddComponent<EnemyAnimatorDriver>();
            }

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

            return view;
        }

        public GameplayEntityView PlayerViewPrefab => _playerViewPrefab;

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

                case EntityType.Projectile:
                    return _projectileMaterial;

                case EntityType.None:
                    return _wallMaterial;

                default:
                    return _unitMaterial;
            }
        }
    }
}
