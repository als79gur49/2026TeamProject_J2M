using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    // Explicit presentation fixture composition. Supplied prefab mappings always use production
    // validation; an invalid mapped prefab must never be replaced by a synthetic test visual.
    internal sealed class PrimitivePresentationTestViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
    {
        private readonly Transform _parent;
        private readonly float _cellSize;
        private readonly int _playerEntityId;
        private readonly IReadOnlyDictionary<int, GameplayEntityView> _enemyPrefabs;
        private readonly IReadOnlyDictionary<int, GameplayEntityView> _staticPrefabs;
        private readonly HashSet<int> _syntheticEntityIds;
        private readonly EnemyInactiveVisualSettings _inactiveSettings;
        private DefaultGameplayEntityViewFactory _prefabFactory;

        public PrimitivePresentationTestViewFactory(
            Transform parent,
            float cellSize,
            int playerEntityId,
            GameplayEntityView playerViewPrefab = null,
            IReadOnlyDictionary<int, GameplayEntityView> enemyViewPrefabsByEntityId = null,
            IReadOnlyDictionary<int, GameplayEntityView> staticViewPrefabsByEntityId = null,
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null,
            IReadOnlyCollection<int> syntheticEntityIds = null)
        {
            _parent = parent;
            _cellSize = cellSize;
            _playerEntityId = playerEntityId;
            PlayerViewPrefab = playerViewPrefab;
            _enemyPrefabs = enemyViewPrefabsByEntityId;
            _staticPrefabs = staticViewPrefabsByEntityId;
            _inactiveSettings = enemyInactiveVisualSettings;
            _syntheticEntityIds = new HashSet<int>(syntheticEntityIds ?? Array.Empty<int>());
        }

        public GameplayEntityView PlayerViewPrefab { get; }

        public GameplayEntityView CreateView(in EntityState entity)
        {
            // Host bootstrap creates its registry search root after accepting the factory.
            var parent = _parent.GetComponent<GameplaySceneHost>() != null
                ? _parent.GetComponentInChildren<GameplayBoardRoot>(true).EntityRoot
                : _parent;
            if ((entity.entityId == _playerEntityId && PlayerViewPrefab != null) ||
                (_enemyPrefabs != null && _enemyPrefabs.ContainsKey(entity.entityId)) ||
                (_staticPrefabs != null && _staticPrefabs.ContainsKey(entity.entityId)))
            {
                _prefabFactory ??= new DefaultGameplayEntityViewFactory(parent, _cellSize, _playerEntityId,
                    PlayerViewPrefab, _enemyPrefabs, _staticPrefabs, _inactiveSettings);
                return _prefabFactory.CreateView(entity);
            }

            if (!_syntheticEntityIds.Contains(entity.entityId))
            {
                throw new InvalidOperationException($"No test View configured for entity {entity.entityId} ({entity.type}).");
            }

            var root = new GameObject($"TestEntityView_{entity.entityId}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GameplayEntityView>();
            view.Initialize(entity.entityId);
            if (entity.entityId == _playerEntityId)
            {
                root.AddComponent<PlayerAnimatorDriver>();
                root.AddComponent<PlayerAnimationTimingAuthoring>();
            }
            else if (entity.type == EntityType.Unit && EntityRolePolicy.IsEnemyUnit(entity))
            {
                root.AddComponent<EnemyAnimatorDriver>();
                var inactive = root.AddComponent<EnemyInactiveVisualController>();
                inactive.Configure(_inactiveSettings);
                inactive.ConfigureLegacyColorFallback(true);
            }

            var profile = GameplayEntityVisualProfile.Create(entity.type, _cellSize);
            view.ConfigureModelRoot(profile.ModelLocalPosition, profile.ModelLocalRotation);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(view.ModelRoot, false);
            visual.transform.localScale = profile.ModelLocalScale;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = entity.entityId == _playerEntityId ? new Color(0.2f, 0.85f, 0.35f) :
                    entity.type == EntityType.Box ? new Color(0.72f, 0.5f, 0.24f) :
                    entity.type == EntityType.None ? new Color(0.25f, 0.28f, 0.33f) :
                    new Color(0.75f, 0.75f, 0.82f),
            };
            visual.GetComponent<Renderer>().sharedMaterial = material;
            // Keep source-clone materials alive until the fixture root is torn down.
            var owner = _parent.GetComponent<PrimitivePresentationTestMaterials>() ??
                _parent.gameObject.AddComponent<PrimitivePresentationTestMaterials>();
            owner.Track(material);
            return view;
        }
    }

    internal sealed class PrimitivePresentationTestMaterials : MonoBehaviour
    {
        private readonly List<Material> _materials = new();

        public void Track(Material material) => _materials.Add(material);

        private void OnDestroy()
        {
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }
    }
}
