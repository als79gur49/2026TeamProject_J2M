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
        private readonly HashSet<int> _playerDeathVisualOverrideEntityIds = new();
        private readonly List<int> _playerVisualHoldEntityIds = new();
        private readonly Dictionary<int, EnemyAnimatorDriver> _enemyAnimatorDriversByEntityId = new();
        private readonly EnemyViewPresentationMapper _enemyViewPresentationMapper = new();
        private readonly Dictionary<int, EnemyViewPresentationState> _enemyViewPresentationStates = new();
        private readonly Dictionary<int, PlayerAnimatorDriver> _playerAnimatorDriversByEntityId = new();
        private readonly List<int> _playerFlipOutcomeStateUpdateEntityIds = new();
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
                driver.SyncRuntimeState(
                    committedLocalTargetPoses.ContainsKey(state.EntityId),
                    isMoving: false,
                    playbackSuppressed: false);
            }
        }

        public void ApplyInitialPlayerPresentation(
            IReadOnlyDictionary<int, GameplayEntityPose> committedLocalTargetPoses)
        {
            _playerDeathVisualOverrideEntityIds.Clear();
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
            PreservePlayerFlipOutcomeState();
            ReleasePlayerDeathOverridesForRespawnSpawns(result.PresentationData, viewsByEntityId);
            foreach (var pair in _playerViewPresentationStates)
            {
                if (TryGetPlayerAnimatorDriver(pair.Key, viewsByEntityId, out var driver))
                {
                    if (pair.Value.DidDie)
                    {
                        _playerDeathVisualOverrideEntityIds.Add(pair.Key);
                    }

                    UpdatePlayerVisualHold(pair.Key, pair.Value, driver, resolvePlayerMotionDurationSeconds);
                    driver.Apply(pair.Value);
                }
            }
        }

        private void PreservePlayerFlipOutcomeState()
        {
            _playerFlipOutcomeStateUpdateEntityIds.Clear();
            foreach (var pair in _playerViewPresentationStates)
            {
                if (!_playerAnimatorDriversByEntityId.TryGetValue(pair.Key, out var driver) ||
                    pair.Value.ActiveActionKind != PlayerActionKind.Flip ||
                    pair.Value.FlipOutcome != TickPlayerFlipOutcomeKind.None)
                {
                    continue;
                }

                var previousState = driver.LastPresentationState;
                if (previousState.ActiveActionKind != PlayerActionKind.Flip ||
                    previousState.ActiveActionSequence != pair.Value.ActiveActionSequence ||
                    previousState.FlipOutcome == TickPlayerFlipOutcomeKind.None)
                {
                    continue;
                }

                _playerFlipOutcomeStateUpdateEntityIds.Add(pair.Key);
            }

            for (var i = 0; i < _playerFlipOutcomeStateUpdateEntityIds.Count; i++)
            {
                var entityId = _playerFlipOutcomeStateUpdateEntityIds[i];
                if (!_playerViewPresentationStates.TryGetValue(entityId, out var currentState) ||
                    !_playerAnimatorDriversByEntityId.TryGetValue(entityId, out var driver))
                {
                    continue;
                }

                var previousState = driver.LastPresentationState;
                if (currentState.ActiveActionKind != PlayerActionKind.Flip ||
                    currentState.FlipOutcome != TickPlayerFlipOutcomeKind.None ||
                    previousState.ActiveActionKind != PlayerActionKind.Flip ||
                    previousState.ActiveActionSequence != currentState.ActiveActionSequence ||
                    previousState.FlipOutcome == TickPlayerFlipOutcomeKind.None)
                {
                    continue;
                }

                _playerViewPresentationStates[entityId] = new PlayerViewPresentationState(
                    currentState.EntityId,
                    currentState.TickIndex,
                    currentState.ActiveActionKind,
                    currentState.ActiveActionSequence,
                    currentState.StartedThisTick,
                    currentState.ExecutedThisTick,
                    currentState.CompletedThisTick,
                    currentState.CanceledThisTick,
                    currentState.ShouldPlayWalkLoop,
                    currentState.IsRecoveryPhase,
                    currentState.DidDie,
                    currentState.DidDieThisTick,
                    currentState.TookDamageThisTick,
                    currentState.ActionPlanId,
                    previousState.FlipOutcome,
                    previousState.HasFlipImpactContactTiming,
                    previousState.FlipTargetBoxEntityId,
                    currentState.DeathSourceEntityId,
                    currentState.ResolvedDamageSourceAvailable,
                    currentState.DamageAmountAtFatalHit,
                    currentState.DeathDirectionHintKind,
                    currentState.DeathFallbackFacing);
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
            _playerDeathVisualOverrideEntityIds.Clear();
            _playerFlipOutcomeStateUpdateEntityIds.Clear();
            _playerVisualHoldStates.Clear();
            _playerViewPresentationStates.Clear();
        }

        public void ReleaseEntity(int entityId)
        {
            _enemyAnimatorDriversByEntityId.Remove(entityId);
            _enemyViewPresentationStates.Remove(entityId);
            _playerAnimatorDriversByEntityId.Remove(entityId);
            _playerDeathVisualOverrideEntityIds.Remove(entityId);
            _playerVisualHoldStates.Remove(entityId);
            _playerViewPresentationStates.Remove(entityId);
        }

        public void SyncEnemyRuntimeState(
            int entityId,
            bool isVisible,
            bool isMoving,
            bool playbackSuppressed,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetEnemyAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.SyncRuntimeState(isVisible, isMoving, playbackSuppressed);
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

                pair.Value.SyncRuntimeState(
                    isVisible: false,
                    isMoving: false,
                    playbackSuppressed: false);
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
                    _playerDeathVisualOverrideEntityIds.Remove(pair.Key);
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

            if (!isVisible)
            {
                _playerDeathVisualOverrideEntityIds.Remove(entityId);
            }
        }

        public PlayerViewAnimationState ResolvePlayerAnimationState(
            int entityId,
            bool shouldPlayWalkLoop,
            bool hasActiveWalkMotion)
        {
            if (_playerDeathVisualOverrideEntityIds.Contains(entityId))
            {
                _playerVisualHoldStates.Remove(entityId);
                return PlayerViewAnimationState.Death;
            }

            if (_playerViewPresentationStates.TryGetValue(entityId, out var state) &&
                TryResolveActionAnimationState(state.ActiveActionKind, out var authoritativeState))
            {
                return authoritativeState;
            }

            if ((_playerViewPresentationStates.TryGetValue(entityId, out state) && state.ShouldPlayWalkLoop) ||
                shouldPlayWalkLoop)
            {
                _playerVisualHoldStates.Remove(entityId);
                return PlayerViewAnimationState.WalkLoop;
            }

            if (hasActiveWalkMotion)
            {
                // Active move clips only bridge the tail when the session signal has already ended.
                _playerVisualHoldStates.Remove(entityId);
                return PlayerViewAnimationState.WalkLoop;
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
            _playerDeathVisualOverrideEntityIds.Remove(entityId);
            _playerVisualHoldStates.Remove(entityId);
            _playerViewPresentationStates.Remove(entityId);
        }

        private void UpdatePlayerVisualHold(
            int entityId,
            in PlayerViewPresentationState state,
            PlayerAnimatorDriver driver,
            Func<int, PlayerActionKind, float> resolvePlayerMotionDurationSeconds)
        {
            if (state.DidDie || _playerDeathVisualOverrideEntityIds.Contains(entityId))
            {
                _playerVisualHoldStates.Remove(entityId);
                return;
            }

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

        private void ReleasePlayerDeathOverridesForRespawnSpawns(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind != TickVisibilityChangeKind.Spawn ||
                    !TryGetPlayerAnimatorDriver(change.EntityId, viewsByEntityId, out _))
                {
                    continue;
                }

                _playerDeathVisualOverrideEntityIds.Remove(change.EntityId);
                _playerVisualHoldStates.Remove(change.EntityId);
            }
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
