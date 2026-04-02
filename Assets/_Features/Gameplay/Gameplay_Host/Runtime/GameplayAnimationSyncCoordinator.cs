using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayAnimationSyncCoordinator
    {
        private readonly Dictionary<int, EnemyAnimatorDriver> _enemyAnimatorDriversByEntityId = new();
        private readonly EnemyViewPresentationMapper _enemyViewPresentationMapper = new();
        private readonly Dictionary<int, EnemyViewPresentationState> _enemyViewPresentationStates = new();
        private readonly Dictionary<int, PlayerAnimatorDriver> _playerAnimatorDriversByEntityId = new();
        private readonly PlayerViewPresentationMapper _playerViewPresentationMapper = new();
        private readonly Dictionary<int, PlayerViewPresentationState> _playerViewPresentationStates = new();

        public IReadOnlyDictionary<int, PlayerViewPresentationState> PlayerPresentationStates => _playerViewPresentationStates;

        public void ApplyInitialEnemyPresentation(
            IReadOnlyList<EntityState> entities,
            IReadOnlyDictionary<int, GameplayEntityPose> committedLocalTargetPoses,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                if (!_enemyViewPresentationMapper.TryMapInitial(entities[i], out var state) ||
                    !TryGetEnemyAnimatorDriver(state.EntityId, viewsByEntityId, out var driver))
                {
                    continue;
                }

                driver.Apply(state);
                driver.SyncRuntimeState(committedLocalTargetPoses.ContainsKey(state.EntityId), isMoving: false);
            }
        }

        public void ApplyInitialPlayerPresentation(
            IReadOnlyDictionary<int, GameplayEntityPose> committedLocalTargetPoses)
        {
            foreach (var pair in _playerAnimatorDriversByEntityId)
            {
                var state = PlayerViewPresentationMapper.CreateInitial(pair.Key);
                _playerViewPresentationStates[pair.Key] = state;
                pair.Value.Apply(state);
                pair.Value.SyncRuntimeState(
                    committedLocalTargetPoses.ContainsKey(pair.Key),
                    PlayerViewAnimationState.Idle);
            }
        }

        public void ApplyTickPresentation(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            _enemyViewPresentationMapper.Build(result, viewsByEntityId, _enemyViewPresentationStates);
            foreach (var pair in _enemyViewPresentationStates)
            {
                if (TryGetEnemyAnimatorDriver(pair.Key, viewsByEntityId, out var driver))
                {
                    driver.Apply(pair.Value);
                }
            }

            _playerViewPresentationMapper.Build(result, viewsByEntityId, _playerViewPresentationStates);
            foreach (var pair in _playerViewPresentationStates)
            {
                if (TryGetPlayerAnimatorDriver(pair.Key, viewsByEntityId, out var driver))
                {
                    driver.Apply(pair.Value);
                }
            }
        }

        public void CacheDrivers(int entityId, GameplayEntityView view)
        {
            CacheEnemyAnimatorDriver(entityId, view);
            CachePlayerAnimatorDriver(entityId, view);
        }

        public void Reset()
        {
            _enemyAnimatorDriversByEntityId.Clear();
            _enemyViewPresentationStates.Clear();
            _playerAnimatorDriversByEntityId.Clear();
            _playerViewPresentationStates.Clear();
        }

        public void SyncEnemyRuntimeState(
            int entityId,
            bool isVisible,
            bool isMoving,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetEnemyAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.SyncRuntimeState(isVisible, isMoving);
            }
        }

        public void SyncHiddenDrivers(
            HashSet<int> visibleEntityIds,
            Func<int, PlayerViewAnimationState> resolveHiddenPlayerAnimationState,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            foreach (var pair in _enemyAnimatorDriversByEntityId)
            {
                if (visibleEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                pair.Value.SyncRuntimeState(isVisible: false, isMoving: false);
            }

            foreach (var pair in _playerAnimatorDriversByEntityId)
            {
                if (visibleEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                if (TryGetPlayerAnimatorDriver(pair.Key, viewsByEntityId, out var driver))
                {
                    driver.SyncRuntimeState(
                        isVisible: false,
                        resolveHiddenPlayerAnimationState(pair.Key));
                }
            }
        }

        public void SyncPlayerRuntimeState(
            int entityId,
            bool isVisible,
            PlayerViewAnimationState resolvedState,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetPlayerAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.SyncRuntimeState(isVisible, resolvedState);
            }
        }

        private void CacheEnemyAnimatorDriver(int entityId, GameplayEntityView view)
        {
            if (view != null &&
                view.TryGetComponent<EnemyAnimatorDriver>(out var driver) &&
                driver != null)
            {
                _enemyAnimatorDriversByEntityId[entityId] = driver;
                return;
            }

            _enemyAnimatorDriversByEntityId.Remove(entityId);
        }

        private void CachePlayerAnimatorDriver(int entityId, GameplayEntityView view)
        {
            if (view != null &&
                view.TryGetComponent<PlayerAnimatorDriver>(out var driver) &&
                driver != null)
            {
                _playerAnimatorDriversByEntityId[entityId] = driver;
                return;
            }

            _playerAnimatorDriversByEntityId.Remove(entityId);
            _playerViewPresentationStates.Remove(entityId);
        }

        private bool TryGetEnemyAnimatorDriver(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            out EnemyAnimatorDriver driver)
        {
            if (_enemyAnimatorDriversByEntityId.TryGetValue(entityId, out driver) &&
                driver != null)
            {
                return true;
            }

            if (viewsByEntityId.TryGetValue(entityId, out var view) &&
                view != null &&
                view.TryGetComponent<EnemyAnimatorDriver>(out driver) &&
                driver != null)
            {
                _enemyAnimatorDriversByEntityId[entityId] = driver;
                return true;
            }

            driver = null;
            return false;
        }

        private bool TryGetPlayerAnimatorDriver(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            out PlayerAnimatorDriver driver)
        {
            if (_playerAnimatorDriversByEntityId.TryGetValue(entityId, out driver) &&
                driver != null)
            {
                return true;
            }

            if (viewsByEntityId.TryGetValue(entityId, out var view) &&
                view != null &&
                view.TryGetComponent<PlayerAnimatorDriver>(out driver) &&
                driver != null)
            {
                _playerAnimatorDriversByEntityId[entityId] = driver;
                return true;
            }

            driver = null;
            return false;
        }
    }
}
