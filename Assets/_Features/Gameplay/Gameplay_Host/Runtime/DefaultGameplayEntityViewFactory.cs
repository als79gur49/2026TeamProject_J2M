using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class DefaultGameplayEntityViewFactory : IGameplayEntityViewFactory
    {
        private readonly float _cellSize;
        private readonly Material _boxMaterial;
        private readonly Material _playerMaterial;
        private readonly Material _projectileMaterial;
        private readonly Transform _parent;
        private readonly int _playerEntityId;
        private readonly Material _unitMaterial;
        private readonly Material _wallMaterial;

        public DefaultGameplayEntityViewFactory(Transform parent, float cellSize, int playerEntityId)
        {
            _parent = parent;
            _cellSize = cellSize;
            _playerEntityId = playerEntityId;
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
            var viewObject = new GameObject($"EntityView_{entity.entityId}");
            viewObject.transform.SetParent(_parent, worldPositionStays: false);
            viewObject.transform.localPosition = Vector3.zero;
            viewObject.transform.localRotation = Quaternion.identity;
            viewObject.transform.localScale = Vector3.one;

            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(entity.entityId);

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
