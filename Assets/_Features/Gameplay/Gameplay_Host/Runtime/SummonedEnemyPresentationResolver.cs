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
        private readonly Dictionary<int, GameplayEntityView> _ownedViewsByEntityId = new();
        private readonly List<int> _staleOwnedEntityIds = new();

        private GameplayAnimationSyncCoordinator _animationSync;
        private EnemyPresentationArchetypeRegistry _registry;
        private GameplayPresentationStateStore _stateStore;
        private Transform _viewParent;
        private GameplayEntityViewRegistry _viewRegistry;
        private EnemyInactiveVisualSettings _enemyInactiveVisualSettings;
        private Func<int, bool> _shouldRetainViewForPendingExit;
        private bool _isResettingSession;

        public void SetPendingExitRetentionPredicate(Func<int, bool> predicate)
        {
            _shouldRetainViewForPendingExit = predicate;
        }

        public void Initialize(
            Transform viewParent,
            GameplayEntityViewRegistry viewRegistry,
            GameplayPresentationStateStore stateStore,
            GameplayAnimationSyncCoordinator animationSync,
            EnemyPresentationArchetypeRegistry registry,
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null)
        {
            var resolvedViewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
            var resolvedStateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            var resolvedAnimationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));

            ResetSession();
            _viewRegistry = resolvedViewRegistry;
            _stateStore = resolvedStateStore;
            _animationSync = resolvedAnimationSync;
            _viewParent = viewParent != null ? viewParent : _viewRegistry.SearchRoot;
            _registry = registry;
            _enemyInactiveVisualSettings = enemyInactiveVisualSettings;
        }

        public void Reconcile(TickResult result)
        {
            if (_isResettingSession)
            {
                throw new InvalidOperationException(
                    $"{nameof(SummonedEnemyPresentationResolver)} cannot reconcile new Views during session reset.");
            }

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
                if (_viewRegistry.TryGetView(binding.EntityId, out var registeredView))
                {
                    if (_ownedViewsByEntityId.TryGetValue(binding.EntityId, out var ownedView) &&
                        !ReferenceEquals(registeredView, ownedView))
                    {
                        ReleaseOwnedViewIfPresent(binding.EntityId);
                    }

                    continue;
                }

                if (_ownedViewsByEntityId.TryGetValue(binding.EntityId, out var existingOwnedView) &&
                    existingOwnedView != null)
                {
                    _viewRegistry.Register(existingOwnedView);
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
                    nameof(SummonedEnemyPresentationResolver),
                    _enemyInactiveVisualSettings);
                _ownedViewsByEntityId[binding.EntityId] = view;
                try
                {
                    _viewRegistry.Register(view);
                }
                catch (Exception registrationException)
                {
                    _ownedViewsByEntityId.Remove(binding.EntityId);
                    Exception rollbackException = null;
                    try
                    {
                        _viewRegistry.UnregisterIfMatches(binding.EntityId, view);
                    }
                    catch (Exception exception)
                    {
                        rollbackException = exception;
                    }
                    finally
                    {
                        GameplayTransientEffectTrackUtility.SafeDestroy(
                            view != null ? view.gameObject : null);
                    }

                    if (rollbackException != null)
                    {
                        throw new AggregateException(
                            "Summoned enemy View registration and rollback both failed.",
                            registrationException,
                            rollbackException);
                    }

                    throw;
                }
            }
        }

        public void CleanupOwnedViews(IReadOnlyList<EntityState> finalEntities)
        {
            _staleOwnedEntityIds.Clear();

            foreach (var entityId in _ownedViewsByEntityId.Keys)
            {
                if (ContainsEntity(finalEntities, entityId))
                {
                    continue;
                }

                if (_shouldRetainViewForPendingExit != null &&
                    _shouldRetainViewForPendingExit(entityId))
                {
                    continue;
                }

                _staleOwnedEntityIds.Add(entityId);
            }

            List<Exception> cleanupExceptions = null;
            for (var i = 0; i < _staleOwnedEntityIds.Count; i++)
            {
                var entityId = _staleOwnedEntityIds[i];
                try
                {
                    ReleaseOwnedViewIfPresent(entityId);
                }
                catch (Exception exception)
                {
                    cleanupExceptions ??= new List<Exception>();
                    cleanupExceptions.Add(exception);
                }
            }

            _staleOwnedEntityIds.Clear();
            ThrowCleanupExceptionsIfAny(cleanupExceptions);
        }

        public void ResetSession()
        {
            if (_isResettingSession)
            {
                return;
            }

            _isResettingSession = true;
            List<Exception> cleanupExceptions = null;
            try
            {
                _staleOwnedEntityIds.Clear();
                foreach (var entityId in _ownedViewsByEntityId.Keys)
                {
                    _staleOwnedEntityIds.Add(entityId);
                }

                for (var i = 0; i < _staleOwnedEntityIds.Count; i++)
                {
                    try
                    {
                        ReleaseOwnedViewIfPresent(_staleOwnedEntityIds[i]);
                    }
                    catch (Exception exception)
                    {
                        cleanupExceptions ??= new List<Exception>();
                        cleanupExceptions.Add(exception);
                    }
                }
            }
            finally
            {
                _staleOwnedEntityIds.Clear();
                _isResettingSession = false;
            }

            ThrowCleanupExceptionsIfAny(cleanupExceptions);
        }

        internal bool ReleaseOwnedViewIfPresent(int entityId)
        {
            if (!_ownedViewsByEntityId.TryGetValue(entityId, out var ownedView))
            {
                return false;
            }

            _ownedViewsByEntityId.Remove(entityId);
            GameplayEntityView replacementView = null;
            if (_stateStore != null &&
                _stateStore.ViewsByEntityId.TryGetValue(entityId, out var stateView))
            {
                if (stateView == null || ReferenceEquals(stateView, ownedView))
                {
                    _stateStore.ViewsByEntityId.Remove(entityId);
                }
                else
                {
                    replacementView = stateView;
                }
            }

            if (_viewRegistry != null &&
                _viewRegistry.TryGetView(entityId, out var registryView) &&
                !ReferenceEquals(registryView, ownedView))
            {
                replacementView = registryView;
            }

            List<Exception> cleanupExceptions = null;
            try
            {
                if (_animationSync != null)
                {
                    _animationSync.ReleaseEntity(entityId);
                }
            }
            catch (Exception exception)
            {
                cleanupExceptions = new List<Exception> { exception };
            }

            try
            {
                if (_animationSync != null && replacementView != null)
                {
                    _animationSync.CacheDrivers(entityId, replacementView);
                }
            }
            catch (Exception exception)
            {
                cleanupExceptions ??= new List<Exception>();
                cleanupExceptions.Add(exception);
            }

            try
            {
                _viewRegistry?.UnregisterIfMatches(entityId, ownedView);
            }
            catch (Exception exception)
            {
                cleanupExceptions ??= new List<Exception>();
                cleanupExceptions.Add(exception);
            }
            finally
            {
                GameplayTransientEffectTrackUtility.SafeDestroy(
                    ownedView != null ? ownedView.gameObject : null);
            }

            ThrowCleanupExceptionsIfAny(cleanupExceptions);
            return true;
        }

        private static void ThrowCleanupExceptionsIfAny(IReadOnlyList<Exception> cleanupExceptions)
        {
            if (cleanupExceptions == null || cleanupExceptions.Count == 0)
            {
                return;
            }

            if (cleanupExceptions.Count == 1)
            {
                throw cleanupExceptions[0];
            }

            throw new AggregateException(
                "One or more summoned enemy View cleanup steps failed.",
                cleanupExceptions);
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
