using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct PlayerAnimationPlaybackResolution
    {
        public PlayerAnimationPlaybackResolution(
            PlayerViewAnimationState state,
            PlayerPresentationPhase phaseOverride = PlayerPresentationPhase.None,
            bool restart = false,
            float resolvedMotionDurationSeconds = 0f)
        {
            State = state;
            PhaseOverride = phaseOverride;
            Restart = restart;
            ResolvedMotionDurationSeconds = resolvedMotionDurationSeconds;
        }

        public PlayerViewAnimationState State { get; }

        public PlayerPresentationPhase PhaseOverride { get; }

        public bool Restart { get; }

        public float ResolvedMotionDurationSeconds { get; }
    }

    public sealed class GameplayAnimationSyncCoordinator
    {
        private readonly List<int> _completedEnemyUtilityAnimationEntityIds = new();
        private readonly List<int> _completedPlayerVisualHoldEntityIds = new();
        private readonly HashSet<int> _contactDelayedEnemyDeathEntityIds = new();
        private readonly HashSet<int> _playerDeathVisualOverrideEntityIds = new();
        private readonly List<int> _playerVisualHoldEntityIds = new();
        private readonly Dictionary<int, EnemyAnimatorDriver> _enemyAnimatorDriversByEntityId = new();
        private readonly Dictionary<int, EnemyUtilityScalePulsePresentationDriver> _enemyScalePulseDriversByEntityId = new();
        private readonly List<int> _enemyUtilityAnimationEntityIds = new();
        private readonly Dictionary<int, EnemyUtilityAnimationPlaybackTrack> _enemyUtilityAnimationTracks = new();
        private readonly EnemyViewPresentationMapper _enemyViewPresentationMapper = new();
        private readonly Dictionary<int, EnemyViewPresentationState> _enemyViewPresentationStates = new();
        private readonly Dictionary<int, PlayerAnimatorDriver> _playerAnimatorDriversByEntityId = new();
        private readonly List<int> _playerFlipOutcomeStateUpdateEntityIds = new();
        private readonly Dictionary<int, PlayerVisualPresentationHoldState> _playerVisualHoldStates = new();
        private readonly PlayerViewPresentationMapper _playerViewPresentationMapper = new();
        private readonly Dictionary<int, PlayerViewPresentationState> _playerViewPresentationStates = new();

        public bool HasActivePlayerVisualHold => _playerVisualHoldStates.Count > 0;

        public float LastStageClearPlayerPresentationDelaySeconds { get; private set; }

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
                if (TryGetEnemyScalePulseDriver(state.EntityId, viewsByEntityId, out var scalePulseDriver))
                {
                    scalePulseDriver.Apply(state);
                }

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
            LastStageClearPlayerPresentationDelaySeconds = 0f;

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
            ApplyTickPresentation(
                result,
                viewsByEntityId,
                jumpLandingCompletionHoldEntityIds: null,
                resolvePlayerMotionDurationSeconds);
        }

        public void ApplyTickPresentation(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            IReadOnlyCollection<int> jumpLandingCompletionHoldEntityIds,
            Func<int, PlayerActionKind, float> resolvePlayerMotionDurationSeconds)
        {
            LastStageClearPlayerPresentationDelaySeconds = 0f;
            BuildContactDelayedEnemyDeathEntityIds(result?.PresentationData);
            _enemyViewPresentationMapper.Build(result, viewsByEntityId, _enemyViewPresentationStates);
            foreach (var pair in _enemyViewPresentationStates)
            {
                var state = pair.Value;
                if (state.DidDie && _contactDelayedEnemyDeathEntityIds.Contains(pair.Key))
                {
                    state = state.WithDidDie(false);
                }

                if (ContainsEntityId(jumpLandingCompletionHoldEntityIds, pair.Key))
                {
                    state = state.WithJumpLandingCompletionHold();
                }

                if (TryGetEnemyAnimatorDriver(pair.Key, viewsByEntityId, out var driver))
                {
                    driver.Apply(state);
                    RefreshEnemyUtilityAnimationTrack(pair.Key, state, driver);
                }

                if (TryGetEnemyScalePulseDriver(pair.Key, viewsByEntityId, out var scalePulseDriver))
                {
                    scalePulseDriver.Apply(state);
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
            AdvancePresentation(deltaTime);
        }

        public void AdvancePresentation(float deltaTime)
        {
            AdvancePresentationBeforeEnemySemantic(deltaTime);
            AdvanceEnemyAutonomousPresentationAfterSemantic(deltaTime);
        }

        public void AdvancePresentationBeforeEnemySemantic(float deltaTime)
        {
            AdvanceEnemyUtilityAnimationTracks(deltaTime);

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

        public void AdvanceEnemyAutonomousPresentationAfterSemantic(float deltaTime)
        {
            foreach (var pair in _enemyScalePulseDriversByEntityId)
            {
                pair.Value?.Advance(deltaTime);
            }
        }

        public bool IsPlayerActionAttemptHoldActive(int entityId)
        {
            return _playerVisualHoldStates.TryGetValue(entityId, out var holdState) &&
                   holdState.Source == PlayerVisualPresentationHoldSource.ActionAttempt &&
                   holdState.IsActive;
        }

        public void CacheDrivers(int entityId, GameplayEntityView view)
        {
            CacheEnemyAnimatorDriver(entityId, view);
            CacheEnemyScalePulseDriver(entityId, view);
            CachePlayerAnimatorDriver(entityId, view);
        }

        public void Reset()
        {
            _completedPlayerVisualHoldEntityIds.Clear();
            _playerVisualHoldEntityIds.Clear();
            foreach (var pair in _enemyScalePulseDriversByEntityId)
            {
                pair.Value?.NormalizeToBaseScale();
            }

            _enemyAnimatorDriversByEntityId.Clear();
            _enemyScalePulseDriversByEntityId.Clear();
            _enemyUtilityAnimationEntityIds.Clear();
            _enemyUtilityAnimationTracks.Clear();
            _enemyViewPresentationStates.Clear();
            _contactDelayedEnemyDeathEntityIds.Clear();
            _playerAnimatorDriversByEntityId.Clear();
            _playerDeathVisualOverrideEntityIds.Clear();
            _playerFlipOutcomeStateUpdateEntityIds.Clear();
            _playerVisualHoldStates.Clear();
            _playerViewPresentationStates.Clear();
            LastStageClearPlayerPresentationDelaySeconds = 0f;
        }

        public void ReleaseEntity(int entityId)
        {
            if (_enemyScalePulseDriversByEntityId.TryGetValue(entityId, out var scalePulseDriver) &&
                scalePulseDriver != null)
            {
                scalePulseDriver.NormalizeToBaseScale();
            }

            _enemyAnimatorDriversByEntityId.Remove(entityId);
            _enemyScalePulseDriversByEntityId.Remove(entityId);
            _enemyUtilityAnimationTracks.Remove(entityId);
            _enemyViewPresentationStates.Remove(entityId);
            _contactDelayedEnemyDeathEntityIds.Remove(entityId);
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
                ApplyEnemyUtilityAnimationTrackTimingIfActive(entityId, driver);
            }
        }

        public void ResyncEnemyAnimatorState(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetEnemyAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.ResyncAnimatorStateFromLastPresentation();
                ApplyEnemyUtilityAnimationTrackTimingIfActive(entityId, driver);
            }
        }

        public void EnsureEnemyJumpAirborneBaseAnimation(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetEnemyAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.EnsureJumpAirborneBaseAnimation();
            }
        }

        public void CompleteEnemyJumpLandingPresentation(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetEnemyAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.CompleteJumpLandingPresentation();
            }
        }

        private void RefreshEnemyUtilityAnimationTrack(
            int entityId,
            in EnemyViewPresentationState state,
            EnemyAnimatorDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            if (state.DidDie)
            {
                _enemyUtilityAnimationTracks.Remove(entityId);
                return;
            }

            if (TryCreateEnemyUtilityAnimationTrack(state, driver, out var track))
            {
                _enemyUtilityAnimationTracks[entityId] = track;
            }

            ApplyEnemyUtilityAnimationTrackTimingIfActive(entityId, driver);
        }

        private bool TryCreateEnemyUtilityAnimationTrack(
            in EnemyViewPresentationState state,
            EnemyAnimatorDriver driver,
            out EnemyUtilityAnimationPlaybackTrack track)
        {
            var phase = EnemyAnimatorDriver.EnemyPresentationPhase.None;
            if (state.StartedUtilityRecoverThisTick)
            {
                phase = EnemyAnimatorDriver.EnemyPresentationPhase.Recovery;
            }
            else if (state.StartedUtilityWindupThisTick &&
                     SupportsUtilityWindupAnimationTrack(state.UtilityPresentationKind))
            {
                phase = EnemyAnimatorDriver.EnemyPresentationPhase.Windup;
            }

            if (phase == EnemyAnimatorDriver.EnemyPresentationPhase.None)
            {
                track = default;
                return false;
            }

            var durationSeconds = driver.GetPresentationDurationSeconds(phase);
            if (durationSeconds <= 0f)
            {
                track = default;
                return false;
            }

            track = new EnemyUtilityAnimationPlaybackTrack(
                phase,
                state.UtilityPresentationKind,
                state.UtilityEffectIndex,
                state.UtilityActivationSequence,
                durationSeconds);
            return true;
        }

        private void AdvanceEnemyUtilityAnimationTracks(float deltaTime)
        {
            _completedEnemyUtilityAnimationEntityIds.Clear();
            _enemyUtilityAnimationEntityIds.Clear();

            foreach (var pair in _enemyUtilityAnimationTracks)
            {
                _enemyUtilityAnimationEntityIds.Add(pair.Key);
            }

            for (var i = 0; i < _enemyUtilityAnimationEntityIds.Count; i++)
            {
                var entityId = _enemyUtilityAnimationEntityIds[i];
                var advancedTrack = _enemyUtilityAnimationTracks[entityId].Advance(deltaTime);
                if (!advancedTrack.IsActive)
                {
                    _completedEnemyUtilityAnimationEntityIds.Add(entityId);
                    continue;
                }

                _enemyUtilityAnimationTracks[entityId] = advancedTrack;
                if (_enemyAnimatorDriversByEntityId.TryGetValue(entityId, out var driver) &&
                    driver != null)
                {
                    ApplyEnemyUtilityAnimationTrackTimingIfActive(entityId, driver);
                }
            }

            for (var i = 0; i < _completedEnemyUtilityAnimationEntityIds.Count; i++)
            {
                var entityId = _completedEnemyUtilityAnimationEntityIds[i];
                _enemyUtilityAnimationTracks.Remove(entityId);
                if (_enemyAnimatorDriversByEntityId.TryGetValue(entityId, out var driver) &&
                    driver != null)
                {
                    driver.RestorePresentationTiming();
                }
            }
        }

        private void ApplyEnemyUtilityAnimationTrackTimingIfActive(int entityId, EnemyAnimatorDriver driver)
        {
            if (driver == null ||
                !_enemyUtilityAnimationTracks.TryGetValue(entityId, out var track) ||
                !track.IsActive ||
                !CanApplyEnemyUtilityAnimationTrack(driver.LastPresentationState))
            {
                return;
            }

            driver.ApplyPresentationPhaseTiming(track.Phase);
        }

        private static bool CanApplyEnemyUtilityAnimationTrack(in EnemyViewPresentationState state)
        {
            if (state.DidDie ||
                state.ActiveActionKind != EnemyActionKind.None ||
                state.JumpPhase != EnemyJumpPhase.None ||
                state.ChargePhase != EnemyChargePhase.None)
            {
                return false;
            }

            return state.GlidePhase == EnemyGlidePhase.Ready ||
                   state.GlidePhase == EnemyGlidePhase.Cooldown;
        }

        private static bool SupportsUtilityWindupAnimationTrack(EnemyUtilityPresentationKind kind)
        {
            return kind == EnemyUtilityPresentationKind.GravityFieldAura;
        }

        private static bool ContainsEntityId(IReadOnlyCollection<int> entityIds, int entityId)
        {
            if (entityIds == null || entityIds.Count == 0)
            {
                return false;
            }

            if (entityIds is HashSet<int> hashSet)
            {
                return hashSet.Contains(entityId);
            }

            foreach (var candidateEntityId in entityIds)
            {
                if (candidateEntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        public float BeginEnemyDeathPresentation(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            _contactDelayedEnemyDeathEntityIds.Remove(entityId);
            _enemyUtilityAnimationTracks.Remove(entityId);
            return TryGetEnemyAnimatorDriver(entityId, viewsByEntityId, out var driver)
                ? driver.PlayDeathPresentation(entityId)
                : 0f;
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

                pair.Value.SyncHiddenRuntimeState(
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
                    driver.SyncHiddenRuntimeState();
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

        public void SyncPlayerRuntimeState(
            int entityId,
            bool isVisible,
            in PlayerAnimationPlaybackResolution playback,
            float resolvedMotionDurationSeconds,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (TryGetPlayerAnimatorDriver(entityId, viewsByEntityId, out var driver))
            {
                driver.SyncRuntimeState(new PlayerAnimationPlaybackRequest(
                    isVisible,
                    playback.State,
                    playback.PhaseOverride,
                    playback.Restart,
                    playback.ResolvedMotionDurationSeconds > 0f
                        ? playback.ResolvedMotionDurationSeconds
                        : resolvedMotionDurationSeconds));
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
            return ResolvePlayerAnimationPlayback(
                entityId,
                shouldPlayWalkLoop,
                hasActiveWalkMotion).State;
        }

        public PlayerAnimationPlaybackResolution ResolvePlayerAnimationPlayback(
            int entityId,
            bool shouldPlayWalkLoop,
            bool hasActiveWalkMotion)
        {
            if (_playerDeathVisualOverrideEntityIds.Contains(entityId))
            {
                _playerVisualHoldStates.Remove(entityId);
                return new PlayerAnimationPlaybackResolution(PlayerViewAnimationState.Death);
            }

            if (_playerViewPresentationStates.TryGetValue(entityId, out var state) &&
                state.HasPlayerOutcome &&
                state.PlayerOutcomeKind == TickPlayerOutcomePresentationKind.StageClearVictory)
            {
                if (_playerVisualHoldStates.TryGetValue(entityId, out var currentStageClearHold) &&
                    currentStageClearHold.IsActive &&
                    currentStageClearHold.Source == PlayerVisualPresentationHoldSource.StageClear)
                {
                    return currentStageClearHold.ToPlaybackResolution();
                }

                return new PlayerAnimationPlaybackResolution(PlayerViewAnimationState.StageClearVictory);
            }

            if (_playerVisualHoldStates.TryGetValue(entityId, out var stageClearHoldState) &&
                stageClearHoldState.IsActive &&
                stageClearHoldState.Source == PlayerVisualPresentationHoldSource.StageClear)
            {
                return stageClearHoldState.ToPlaybackResolution();
            }

            if (_playerViewPresentationStates.TryGetValue(entityId, out state) &&
                TryResolveActionAnimationState(state.ActiveActionKind, out var authoritativeState))
            {
                return new PlayerAnimationPlaybackResolution(authoritativeState);
            }

            if (_playerViewPresentationStates.TryGetValue(entityId, out state) &&
                state.HasActionAttempt &&
                TryResolveActionAnimationState(state.ActionAttemptKind, out var attemptState))
            {
                if (_playerVisualHoldStates.TryGetValue(entityId, out var currentAttemptHold) &&
                    currentAttemptHold.IsActive &&
                    currentAttemptHold.Source == PlayerVisualPresentationHoldSource.ActionAttempt)
                {
                    return currentAttemptHold.ToPlaybackResolution();
                }

                return new PlayerAnimationPlaybackResolution(
                    attemptState,
                    ResolveWindupPhase(state.ActionAttemptKind),
                    restart: true);
            }

            if (_playerVisualHoldStates.TryGetValue(entityId, out var attemptHoldState) &&
                attemptHoldState.IsActive &&
                attemptHoldState.Source == PlayerVisualPresentationHoldSource.ActionAttempt)
            {
                return attemptHoldState.ToPlaybackResolution();
            }

            if ((_playerViewPresentationStates.TryGetValue(entityId, out state) && state.ShouldPlayWalkLoop) ||
                shouldPlayWalkLoop)
            {
                RemovePlayerVisualHoldUnlessActionAttempt(entityId);
                return new PlayerAnimationPlaybackResolution(PlayerViewAnimationState.WalkLoop);
            }

            if (hasActiveWalkMotion)
            {
                // Active move clips only bridge the tail when the session signal has already ended.
                RemovePlayerVisualHoldUnlessActionAttempt(entityId);
                return new PlayerAnimationPlaybackResolution(PlayerViewAnimationState.WalkLoop);
            }

            if (_playerVisualHoldStates.TryGetValue(entityId, out var holdState) &&
                holdState.IsActive)
            {
                return holdState.ToPlaybackResolution();
            }

            return new PlayerAnimationPlaybackResolution(PlayerViewAnimationState.Idle);
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

        private void BuildContactDelayedEnemyDeathEntityIds(TickPresentationData presentationData)
        {
            _contactDelayedEnemyDeathEntityIds.Clear();
            if (presentationData == null)
            {
                return;
            }

            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.Timing != EntityExitPresentationTiming.AtContactTime ||
                    signal.EntityType != EntityType.Unit ||
                    (signal.ExitCause != TickEntityExitCause.EnemyDeath &&
                     signal.ExitCause != TickEntityExitCause.Killed))
                {
                    continue;
                }

                _contactDelayedEnemyDeathEntityIds.Add(signal.ExitedEntityId);
            }
        }

        private void CacheEnemyScalePulseDriver(int entityId, GameplayEntityView view)
        {
            if (view != null &&
                view.TryGetComponent<EnemyUtilityScalePulsePresentationDriver>(out var driver) &&
                driver != null)
            {
                _enemyScalePulseDriversByEntityId[entityId] = driver;
                return;
            }

            _enemyScalePulseDriversByEntityId.Remove(entityId);
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
                RemovePlayerVisualHoldUnlessStageClear(entityId);
                return;
            }

            if (_playerVisualHoldStates.TryGetValue(entityId, out var currentHold) &&
                currentHold.IsActive &&
                currentHold.Source == PlayerVisualPresentationHoldSource.StageClear)
            {
                return;
            }

            if (state.HasPlayerOutcome &&
                state.PlayerOutcomeKind == TickPlayerOutcomePresentationKind.StageClearVictory)
            {
                var presentationDurationSeconds = driver.GetStageClearVictoryPresentationDurationSeconds();
                if (presentationDurationSeconds <= 0f)
                {
                    presentationDurationSeconds = 0.01f;
                }

                _playerVisualHoldStates[entityId] = PlayerVisualPresentationHoldState.CreateStageClear(
                    presentationDurationSeconds);
                LastStageClearPlayerPresentationDelaySeconds = Mathf.Max(
                    LastStageClearPlayerPresentationDelaySeconds,
                    presentationDurationSeconds);
                return;
            }

            if (state.StartedThisTick &&
                TryResolveActionAnimationState(state.ActiveActionKind, out var animationState))
            {
                var presentationDurationSeconds = driver.GetActionHoldPresentationDurationSeconds(
                    state.ActiveActionKind,
                    resolvePlayerMotionDurationSeconds != null
                        ? resolvePlayerMotionDurationSeconds(entityId, state.ActiveActionKind)
                        : 0f);
                if (presentationDurationSeconds <= 0f)
                {
                    _playerVisualHoldStates.Remove(entityId);
                    return;
                }

                _playerVisualHoldStates[entityId] = PlayerVisualPresentationHoldState.CreateActiveAction(
                    animationState,
                    state.ActiveActionKind,
                    state.ActiveActionSequence,
                    presentationDurationSeconds);
                return;
            }

            if (!state.HasActionAttempt ||
                !TryResolveActionAnimationState(state.ActionAttemptKind, out var attemptAnimationState))
            {
                return;
            }

            _playerVisualHoldStates[entityId] = CreateActionAttemptHold(
                attemptAnimationState,
                state.ActionAttemptKind,
                state.ActionAttemptDirection,
                state.ActionAttemptFeedbackKind,
                driver,
                resolvePlayerMotionDurationSeconds != null
                    ? resolvePlayerMotionDurationSeconds(entityId, state.ActionAttemptKind)
                    : 0f);
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

        private PlayerVisualPresentationHoldState CreateActionAttemptHold(
            PlayerViewAnimationState animationState,
            PlayerActionKind actionKind,
            Direction direction,
            PlayerActionAttemptFeedbackKind feedbackKind,
            PlayerAnimatorDriver driver,
            float resolvedMotionDurationSeconds)
        {
            var windupPhase = ResolveWindupPhase(actionKind);
            var recoveryPhase = ResolveRecoveryPhase(actionKind);
            var windupDurationSeconds = driver.GetPresentationDurationSeconds(windupPhase);
            var recoveryDurationSeconds = driver.GetPresentationDurationSeconds(recoveryPhase);
            var totalDurationSeconds = driver.GetPresentationDurationSeconds(actionKind, resolvedMotionDurationSeconds);

            if (windupDurationSeconds <= 0f &&
                recoveryDurationSeconds <= 0f)
            {
                var halfDurationSeconds = totalDurationSeconds > 0f
                    ? totalDurationSeconds * 0.5f
                    : 0.01f;
                windupDurationSeconds = halfDurationSeconds;
                recoveryDurationSeconds = halfDurationSeconds;
            }
            else if (windupDurationSeconds <= 0f)
            {
                windupDurationSeconds = Mathf.Max(0.01f, totalDurationSeconds - recoveryDurationSeconds);
            }
            else if (recoveryDurationSeconds <= 0f)
            {
                recoveryDurationSeconds = Mathf.Max(0.01f, totalDurationSeconds - windupDurationSeconds);
            }

            windupDurationSeconds = Mathf.Max(0.01f, windupDurationSeconds);
            recoveryDurationSeconds = Mathf.Max(0.01f, recoveryDurationSeconds);

            return PlayerVisualPresentationHoldState.CreateActionAttempt(
                animationState,
                actionKind,
                direction,
                feedbackKind,
                windupPhase,
                recoveryPhase,
                windupDurationSeconds,
                recoveryDurationSeconds);
        }

        private static PlayerPresentationPhase ResolveWindupPhase(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => PlayerPresentationPhase.PushWindup,
                PlayerActionKind.Flip => PlayerPresentationPhase.FlipWindup,
                _ => PlayerPresentationPhase.None,
            };
        }

        private static PlayerPresentationPhase ResolveRecoveryPhase(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => PlayerPresentationPhase.PushRecovery,
                PlayerActionKind.Flip => PlayerPresentationPhase.FlipRecovery,
                _ => PlayerPresentationPhase.None,
            };
        }

        private void RemovePlayerVisualHoldUnlessActionAttempt(int entityId)
        {
            if (_playerVisualHoldStates.TryGetValue(entityId, out var holdState) &&
                (holdState.Source == PlayerVisualPresentationHoldSource.ActionAttempt ||
                 holdState.Source == PlayerVisualPresentationHoldSource.StageClear) &&
                holdState.IsActive)
            {
                return;
            }

            _playerVisualHoldStates.Remove(entityId);
        }

        private void RemovePlayerVisualHoldUnlessStageClear(int entityId)
        {
            if (_playerVisualHoldStates.TryGetValue(entityId, out var holdState) &&
                holdState.Source == PlayerVisualPresentationHoldSource.StageClear &&
                holdState.IsActive)
            {
                return;
            }

            _playerVisualHoldStates.Remove(entityId);
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

        private bool TryGetEnemyScalePulseDriver(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            out EnemyUtilityScalePulsePresentationDriver driver)
        {
            if (_enemyScalePulseDriversByEntityId.TryGetValue(entityId, out driver) &&
                driver != null)
            {
                return true;
            }

            if (viewsByEntityId.TryGetValue(entityId, out var view) &&
                view != null &&
                view.TryGetComponent<EnemyUtilityScalePulsePresentationDriver>(out driver) &&
                driver != null)
            {
                _enemyScalePulseDriversByEntityId[entityId] = driver;
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

        private readonly struct EnemyUtilityAnimationPlaybackTrack
        {
            public EnemyUtilityAnimationPlaybackTrack(
                EnemyAnimatorDriver.EnemyPresentationPhase phase,
                EnemyUtilityPresentationKind kind,
                int effectIndex,
                int activationSequence,
                float durationSeconds)
            {
                Phase = phase;
                Kind = kind;
                EffectIndex = effectIndex;
                ActivationSequence = activationSequence;
                DurationSeconds = Mathf.Max(0.0001f, durationSeconds);
                ElapsedSeconds = 0f;
            }

            private EnemyUtilityAnimationPlaybackTrack(
                EnemyAnimatorDriver.EnemyPresentationPhase phase,
                EnemyUtilityPresentationKind kind,
                int effectIndex,
                int activationSequence,
                float durationSeconds,
                float elapsedSeconds)
            {
                Phase = phase;
                Kind = kind;
                EffectIndex = effectIndex;
                ActivationSequence = activationSequence;
                DurationSeconds = Mathf.Max(0.0001f, durationSeconds);
                ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            }

            public EnemyAnimatorDriver.EnemyPresentationPhase Phase { get; }

            public EnemyUtilityPresentationKind Kind { get; }

            public int EffectIndex { get; }

            public int ActivationSequence { get; }

            public float DurationSeconds { get; }

            public float ElapsedSeconds { get; }

            public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

            public bool IsActive => RemainingSeconds > 0f;

            public EnemyUtilityAnimationPlaybackTrack Advance(float deltaTime)
            {
                return new EnemyUtilityAnimationPlaybackTrack(
                    Phase,
                    Kind,
                    EffectIndex,
                    ActivationSequence,
                    DurationSeconds,
                    Mathf.Min(DurationSeconds, ElapsedSeconds + Mathf.Max(0f, deltaTime)));
            }
        }

        private enum PlayerVisualPresentationHoldSource
        {
            ActiveAction,
            ActionAttempt,
            StageClear,
        }

        private readonly struct PlayerVisualPresentationHoldState
        {
            private PlayerVisualPresentationHoldState(
                PlayerVisualPresentationHoldSource source,
                PlayerViewAnimationState animationState,
                PlayerActionKind actionKind,
                int actionSequence,
                Direction direction,
                PlayerActionAttemptFeedbackKind feedbackKind,
                PlayerPresentationPhase windupPhase,
                PlayerPresentationPhase recoveryPhase,
                float windupDurationSeconds,
                float recoveryDurationSeconds,
                float remainingSeconds,
                float elapsedSeconds)
            {
                Source = source;
                AnimationState = animationState;
                ActionKind = actionKind;
                ActionSequence = actionSequence;
                Direction = direction;
                FeedbackKind = feedbackKind;
                WindupPhase = windupPhase;
                RecoveryPhase = recoveryPhase;
                WindupDurationSeconds = windupDurationSeconds;
                RecoveryDurationSeconds = recoveryDurationSeconds;
                RemainingSeconds = remainingSeconds;
                ElapsedSeconds = elapsedSeconds;
            }

            public PlayerVisualPresentationHoldSource Source { get; }

            public PlayerViewAnimationState AnimationState { get; }

            public PlayerActionKind ActionKind { get; }

            public int ActionSequence { get; }

            public Direction Direction { get; }

            public PlayerActionAttemptFeedbackKind FeedbackKind { get; }

            public PlayerPresentationPhase WindupPhase { get; }

            public PlayerPresentationPhase RecoveryPhase { get; }

            public float WindupDurationSeconds { get; }

            public float RecoveryDurationSeconds { get; }

            public float RemainingSeconds { get; }

            public float ElapsedSeconds { get; }

            public bool IsActive => RemainingSeconds > 0f;

            public static PlayerVisualPresentationHoldState CreateActiveAction(
                PlayerViewAnimationState animationState,
                PlayerActionKind actionKind,
                int actionSequence,
                float remainingSeconds)
            {
                return new PlayerVisualPresentationHoldState(
                    PlayerVisualPresentationHoldSource.ActiveAction,
                    animationState,
                    actionKind,
                    actionSequence,
                    Direction.None,
                    PlayerActionAttemptFeedbackKind.None,
                    PlayerPresentationPhase.None,
                    PlayerPresentationPhase.None,
                    windupDurationSeconds: 0f,
                    recoveryDurationSeconds: 0f,
                    remainingSeconds,
                    elapsedSeconds: 0f);
            }

            public static PlayerVisualPresentationHoldState CreateActionAttempt(
                PlayerViewAnimationState animationState,
                PlayerActionKind actionKind,
                Direction direction,
                PlayerActionAttemptFeedbackKind feedbackKind,
                PlayerPresentationPhase windupPhase,
                PlayerPresentationPhase recoveryPhase,
                float windupDurationSeconds,
                float recoveryDurationSeconds)
            {
                return new PlayerVisualPresentationHoldState(
                    PlayerVisualPresentationHoldSource.ActionAttempt,
                    animationState,
                    actionKind,
                    actionSequence: 0,
                    direction,
                    feedbackKind,
                    windupPhase,
                    recoveryPhase,
                    windupDurationSeconds,
                    recoveryDurationSeconds,
                    windupDurationSeconds + recoveryDurationSeconds,
                    elapsedSeconds: 0f);
            }

            public static PlayerVisualPresentationHoldState CreateStageClear(float remainingSeconds)
            {
                return new PlayerVisualPresentationHoldState(
                    PlayerVisualPresentationHoldSource.StageClear,
                    PlayerViewAnimationState.StageClearVictory,
                    PlayerActionKind.None,
                    actionSequence: 0,
                    Direction.None,
                    PlayerActionAttemptFeedbackKind.None,
                    PlayerPresentationPhase.None,
                    PlayerPresentationPhase.None,
                    windupDurationSeconds: 0f,
                    recoveryDurationSeconds: 0f,
                    remainingSeconds,
                    elapsedSeconds: 0f);
            }

            public PlayerAnimationPlaybackResolution ToPlaybackResolution()
            {
                return new PlayerAnimationPlaybackResolution(
                    AnimationState,
                    Source == PlayerVisualPresentationHoldSource.ActionAttempt
                        ? ResolveCurrentAttemptPhase()
                        : PlayerPresentationPhase.None,
                    restart: false,
                    Source == PlayerVisualPresentationHoldSource.StageClear
                        ? RemainingSeconds + ElapsedSeconds
                        : WindupDurationSeconds + RecoveryDurationSeconds);
            }

            public PlayerVisualPresentationHoldState Advance(float deltaTime)
            {
                return new PlayerVisualPresentationHoldState(
                    Source,
                    AnimationState,
                    ActionKind,
                    ActionSequence,
                    Direction,
                    FeedbackKind,
                    WindupPhase,
                    RecoveryPhase,
                    WindupDurationSeconds,
                    RecoveryDurationSeconds,
                    Mathf.Max(0f, RemainingSeconds - deltaTime),
                    Mathf.Max(0f, ElapsedSeconds + Mathf.Max(0f, deltaTime)));
            }

            private PlayerPresentationPhase ResolveCurrentAttemptPhase()
            {
                if (Source != PlayerVisualPresentationHoldSource.ActionAttempt)
                {
                    return PlayerPresentationPhase.None;
                }

                return ElapsedSeconds >= WindupDurationSeconds
                    ? RecoveryPhase
                    : WindupPhase;
            }
        }
    }
}
