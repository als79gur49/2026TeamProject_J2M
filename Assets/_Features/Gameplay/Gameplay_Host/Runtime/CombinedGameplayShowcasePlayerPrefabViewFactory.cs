using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class CombinedGameplayShowcasePlayerPrefabViewFactory : IGameplayEntityViewFactory
    {
        private readonly IGameplayEntityViewFactory _fallbackFactory;
        private readonly Transform _parent;
        private readonly int _playerEntityId;
        private readonly GameplayEntityView _playerViewPrefab;

        public CombinedGameplayShowcasePlayerPrefabViewFactory(
            Transform parent,
            int playerEntityId,
            GameplayEntityView playerViewPrefab,
            float cellSize)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _playerEntityId = playerEntityId;
            _playerViewPrefab = playerViewPrefab != null
                ? playerViewPrefab
                : throw new ArgumentNullException(nameof(playerViewPrefab));
            _fallbackFactory = new GameplayBoxCapabilityLabelViewFactory(parent, cellSize, playerEntityId);
        }

        public GameplayEntityView CreateView(in EntityState entity)
        {
            if (entity.entityId != _playerEntityId)
            {
                return _fallbackFactory.CreateView(entity);
            }

            var instance = UnityEngine.Object.Instantiate(_playerViewPrefab, _parent);
            instance.name = $"EntityView_{entity.entityId}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.Initialize(entity.entityId);

            if (!instance.TryGetComponent<PlayerAnimatorDriver>(out _))
            {
                throw new InvalidOperationException(
                    "Player prefab view must include PlayerAnimatorDriver on the root.");
            }

            return instance;
        }
    }
}
