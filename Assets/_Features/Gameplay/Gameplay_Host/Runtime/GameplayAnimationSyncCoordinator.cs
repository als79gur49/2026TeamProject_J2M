using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayAnimationSyncCoordinator
    {
        private readonly List<int> _completedPlayerVisualHoldEntityIds = new();
        private readonly List<int> _playerVisualHoldEntityIds = new();
        private readonly Dictionary<int, EnemyAnimatorDriver> _enemyAnimatorDriversByEntityId = new();
        private readonly EnemyViewPresentationMapper _enemyViewPresentationMapper = new();
        private readonly Dictionary<int, EnemyViewPresentationState> _enemyViewPresentationStates = new();
        private readonly Dictionary<int, PlayerAnimatorDriver> _playerAnimatorDriversByEntityId = new();
        private readonly Dictionary<int, PlayerVisualPresentationHoldState> _playerVisualHoldStates = new();
        private readonly PlayerViewPresentationMapper _playerViewPresentationMapper = new();
        private readonly Dictionary<int, PlayerViewPresentationState> _playerViewPresentationStates = new();

        public bool HasActivePlayerVisualHold => _playerVisualHoldStates.Count > 0;

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
            _playerVisualHoldStates.Clear();

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
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            Func<int, PlayerActionKind, float> resolvePlayerMotionDurationSeconds)
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
                    UpdatePlayerVisualHold(pair.Key, pair.Value, driver, resolvePlayerMotionDurationSeconds);
                    driver.Apply(pair.Value);
                }
            }
        }

        public void AdvancePlayerPresentation(float deltaTime)
        {
            _completedPlayerVisualHoldEntityIds.Clear();
            _playerVisualHoldEntityIds.Clear();

            foreach (var pair in _playerVisualHoldStates)
            {
                _playerVisualHoldEntityIds.Add(pair.Key);
            }

            for (var i = 0; i < _playerVisualHoldEntityIds.Count; i++)
            {
                var entityId = _playerVisualHoldEntityIds[i];
                var advancedState = _playerVisualHoldStates[entityId].Advance(deltaTime);
                if (!advancedState.IsActive)
                {
                    _completedPlayerVisualHoldEntityIds.Add(entityId);
                    continue;
                }

                _playerVisualHoldStates[entityId] = advancedState;
            }

            for (var i = 0; i < _completedPlayerVisualHoldEntityIds.Count; i++)
            {
                _playerVisualHoldStates.Remove(_completedPlayerVisualHoldEntityIds[i]);
            }
        }

        public void CacheDrivers(int entityId, GameplayEntityView view)
        {
            CacheEnemyAnimatorDriver(entityId, view);
            CachePlayerAnimatorDriver(entityId, view);
        }

        public void Reset()
        {
            _completedPlayerVisualHoldEntityIds.Clear();
            _playerVisualHoldEntityIds.Clear();
            _enemyAnimatorDriversByEntityId.Clear();
            _enemyViewPresentationStates.Clear();
            _playerAnimatorDriversByEntityId.Clear();
            _playerVisualHoldStates.Clear();
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
            Func<int, PlayerViewAnimationState, float> resolveHiddenPlayerMotionDurationSeconds,
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
                    var resolvedState = resolveHiddenPlayerAnimationState(pair.Key);
                    driver.SyncRuntimeState(
                        isVisible: false,
                        resolvedState,
                        resolveHiddenPlayerMotionDurationSeconds(pair.Key, resolvedState));
                }
            }
        }

        public void SyncPlayerRuntimeState(
            int entityId,
            bool isVisible,
            PlayerViewAnimationState resolvedState,
            float resolvedMotionDurationSeconds,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetPlayerAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.SyncRuntimeState(isVisible, resolvedState, resolvedMotionDurationSeconds);
            }
        }

        public PlayerViewAnimationState ResolvePlayerAnimationState(int entityId, bool hasActiveWalkMotion)
        {
            if (_playerViewPresentationStates.TryGetValue(entityId, out var state) &&
                TryResolveActionAnimationState(state.ActiveActionKind, out var authoritativeState))
            {
                return authoritativeState;
            }

            if (hasActiveWalkMotion)
            {
                // Once a new authoritative move presentation takes over, the previous action hold
                // must not resurface after the walk clip completes.
                _playerVisualHoldStates.Remove(entityId);
                return PlayerViewAnimationState.Walk;
            }

            if (_playerVisualHoldStates.TryGetValue(entityId, out var holdState) &&
                holdState.IsActive)
            {
                return holdState.AnimationState;
            }

            return PlayerViewAnimationState.Idle;
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
            _playerVisualHoldStates.Remove(entityId);
            _playerViewPresentationStates.Remove(entityId);
        }

        private void UpdatePlayerVisualHold(
            int entityId,
            in PlayerViewPresentationState state,
            PlayerAnimatorDriver driver,
            Func<int, PlayerActionKind, float> resolvePlayerMotionDurationSeconds)
        {
            if (state.CanceledThisTick)
            {
                _playerVisualHoldStates.Remove(entityId);
                return;
            }

            if (!state.StartedThisTick ||
                !TryResolveActionAnimationState(state.ActiveActionKind, out var animationState))
            {
                return;
            }

            var presentationDurationSeconds = driver.GetPresentationDurationSeconds(
                state.ActiveActionKind,
                resolvePlayerMotionDurationSeconds != null
                    ? resolvePlayerMotionDurationSeconds(entityId, state.ActiveActionKind)
                    : 0f);
            if (presentationDurationSeconds <= 0f)
            {
                _playerVisualHoldStates.Remove(entityId);
                return;
            }

            _playerVisualHoldStates[entityId] = new PlayerVisualPresentationHoldState(
                animationState,
                state.ActiveActionSequence,
                presentationDurationSeconds);
        }

        private static bool TryResolveActionAnimationState(
            PlayerActionKind actionKind,
            out PlayerViewAnimationState animationState)
        {
            switch (actionKind)
            {
                case PlayerActionKind.Push:
                    animationState = PlayerViewAnimationState.Push;
                    return true;

                case PlayerActionKind.Flip:
                    animationState = PlayerViewAnimationState.Flip;
                    return true;

                default:
                    animationState = PlayerViewAnimationState.Idle;
                    return false;
            }
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

        private readonly struct PlayerVisualPresentationHoldState
        {
            public PlayerVisualPresentationHoldState(
                PlayerViewAnimationState animationState,
                int actionSequence,
                float remainingSeconds)
            {
                AnimationState = animationState;
                ActionSequence = actionSequence;
                RemainingSeconds = remainingSeconds;
            }

            public PlayerViewAnimationState AnimationState { get; }

            public int ActionSequence { get; }

            public float RemainingSeconds { get; }

            public bool IsActive => RemainingSeconds > 0f;

            public PlayerVisualPresentationHoldState Advance(float deltaTime)
            {
                return new PlayerVisualPresentationHoldState(
                    AnimationState,
                    ActionSequence,
                    Mathf.Max(0f, RemainingSeconds - deltaTime));
            }
        }
    }
}
