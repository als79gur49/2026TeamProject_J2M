using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class DefaultGameplayEntityViewFactory : IGameplayEntityViewFactory
    {
        private readonly float _cellSize;
        private readonly Material _entityBaseMaterial;
        private readonly Transform _parent;
        private readonly int _playerEntityId;

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

            _entityBaseMaterial = new Material(shader);
        }

        public GameplayEntityView CreateView(in EntityState entity)
        {
            var viewObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            viewObject.name = $"EntityView_{entity.entityId}";
            viewObject.transform.SetParent(_parent, worldPositionStays: false);
            viewObject.transform.localScale = Vector3.one * (_cellSize * 0.85f);

            var collider = viewObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(entity.entityId);

            var renderer = viewObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(_entityBaseMaterial);
                material.color = ResolveColor(entity);
                renderer.sharedMaterial = material;
            }

            return view;
        }

        private Color ResolveColor(in EntityState entity)
        {
            if (entity.entityId == _playerEntityId)
            {
                return new Color(0.2f, 0.85f, 0.35f);
            }

            switch (entity.type)
            {
                case EntityType.Box:
                    return new Color(0.72f, 0.5f, 0.24f);

                case EntityType.Projectile:
                    return new Color(0.9f, 0.4f, 0.2f);

                case EntityType.None:
                    return new Color(0.25f, 0.28f, 0.33f);

                default:
                    return new Color(0.75f, 0.75f, 0.82f);
            }
        }
    }
}
