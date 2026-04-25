using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class SummonedEnemyPresentationResolver
    {
        private readonly HashSet<int> _ownedEntityIds = new();
        private readonly List<int> _staleOwnedEntityIds = new();

        private GameplayAnimationSyncCoordinator _animationSync;
        private EnemyPresentationArchetypeRegistry _registry;
        private GameplayPresentationStateStore _stateStore;
        private Transform _viewParent;
        private GameplayEntityViewRegistry _viewRegistry;

        public void Initialize(
            Transform viewParent,
            GameplayEntityViewRegistry viewRegistry,
            GameplayPresentationStateStore stateStore,
            GameplayAnimationSyncCoordinator animationSync,
            EnemyPresentationArchetypeRegistry registry)
        {
            _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
            _viewParent = viewParent != null ? viewParent : _viewRegistry.SearchRoot;
            _registry = registry;
        }

        public void Reconcile(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var bindings = result.PresentationData.SummonedEnemyPresentationBindings;
            if (bindings.Count == 0)
            {
                return;
            }

            if (_registry == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(SummonedEnemyPresentationResolver)} requires an {nameof(EnemyPresentationArchetypeRegistry)} when summoned enemy presentation bindings are present.");
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (_viewRegistry.TryGetView(binding.EntityId, out _))
                {
                    continue;
                }

                if (!TryFindFinalEntity(result.FinalEntities, binding.EntityId, out var entity))
                {
                    continue;
                }

                if (!binding.HasEnemyDefinitionBinding)
                {
                    throw new InvalidOperationException(
                        $"Summoned enemy entity {binding.EntityId} is missing {nameof(EnemyDefinitionBindingState)}.");
                }

                if (!_registry.TryGetRuntime(binding.ArchetypeId, out var runtime))
                {
                    throw new InvalidOperationException(
                        $"Missing summoned enemy presentation mapping for archetype '{binding.ArchetypeId}' on entity {binding.EntityId}.");
                }

                var view = GameplayEnemyViewPrefabInstantiator.InstantiateEnemyView(
                    runtime.ViewPrefab,
                    _viewParent,
                    entity,
                    nameof(SummonedEnemyPresentationResolver));
                _viewRegistry.Register(view);
                _ownedEntityIds.Add(binding.EntityId);
            }
        }

        public void CleanupOwnedViews(IReadOnlyList<EntityState> finalEntities)
        {
            _staleOwnedEntityIds.Clear();

            foreach (var entityId in _ownedEntityIds)
            {
                if (ContainsEntity(finalEntities, entityId))
                {
                    continue;
                }

                _staleOwnedEntityIds.Add(entityId);
            }

            for (var i = 0; i < _staleOwnedEntityIds.Count; i++)
            {
                var entityId = _staleOwnedEntityIds[i];
                GameplayEntityView view = null;
                if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var stateView))
                {
                    view = stateView;
                }
                else if (_viewRegistry.TryGetView(entityId, out var registryView))
                {
                    view = registryView;
                }

                _stateStore.ViewsByEntityId.Remove(entityId);
                _viewRegistry.Unregister(entityId);
                _animationSync.ReleaseEntity(entityId);
                GameplayTransientEffectTrackUtility.SafeDestroy(view != null ? view.gameObject : null);
                _ownedEntityIds.Remove(entityId);
            }
        }

        private static bool ContainsEntity(IReadOnlyList<EntityState> entities, int entityId)
        {
            return TryFindFinalEntity(entities, entityId, out _);
        }

        private static bool TryFindFinalEntity(
            IReadOnlyList<EntityState> entities,
            int entityId,
            out EntityState entity)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].entityId != entityId)
                {
                    continue;
                }

                entity = entities[i];
                return EntityRolePolicy.IsEnemyUnit(entity);
            }

            entity = default;
            return false;
        }
    }
}
