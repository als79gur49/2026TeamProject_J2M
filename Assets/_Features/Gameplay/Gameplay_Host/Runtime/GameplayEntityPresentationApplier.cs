using System;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayEntityPresentationApplier
    {
        private readonly GameplayAnimationSyncCoordinator _animationSync;
        private readonly GameplayCommittedFrameBuilder _committedFrameBuilder;
        private readonly IEnemyVisualSemanticResolver _enemyVisualSemanticResolver;
        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayEntityPresentationApplier(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayPoseResolver poseResolver,
            GameplayAnimationSyncCoordinator animationSync,
            GameplayMotionTimingResolver motionTimingResolver,
            IEnemyVisualSemanticResolver enemyVisualSemanticResolver,
            GameplayCommittedFrameBuilder committedFrameBuilder)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
            _motionTimingResolver = motionTimingResolver ?? throw new ArgumentNullException(nameof(motionTimingResolver));
            _enemyVisualSemanticResolver =
                enemyVisualSemanticResolver ?? throw new ArgumentNullException(nameof(enemyVisualSemanticResolver));
            _committedFrameBuilder = committedFrameBuilder ?? throw new ArgumentNullException(nameof(committedFrameBuilder));
        }

        public void Apply(
            float deltaTime,
            bool hasActiveBoardRotationTween,
            GameplayEntityViewBinder viewBinder,
            GameplayTimingProfile timingProfile)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            _animationSync.AdvancePlayerPresentation(deltaTime);
            CleanupCompletedTopologyTransitionState(hasActiveBoardRotationTween);

            _trackState.CompletedMotionTrackIds.Clear();
            _trackState.CompletedJumpTrackIds.Clear();
            _trackState.CompletedVisibilityTrackIds.Clear();
            _trackState.VisibleEntityIds.Clear();
            _stateStore.EnemyVisualFactsByEntityId.Clear();
            _stateStore.EnemyVisualSemanticStatesByEntityId.Clear();

            var processingEntityIds = _stateStore.BuildProcessingEntityIds();
            for (var i = 0; i < processingEntityIds.Count; i++)
            {
                var entityId = processingEntityIds[i];
                if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                if (!_poseResolver.TryResolveFallbackLocalPose(entityId, out var localPose))
                {
                    continue;
                }

                if (_trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack))
                {
                    localPose = motionTrack.SampleAndAdvance(deltaTime, localPose);
                    if (!motionTrack.HasClips)
                    {
                        _trackState.CompletedMotionTrackIds.Add(entityId);
                    }
                }

                if (_trackState.JumpTracks.TryGetValue(entityId, out var jumpTrack) &&
                    jumpTrack.HasClip)
                {
                    localPose = jumpTrack.SampleAndAdvance(deltaTime, localPose);
                    if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedState))
                    {
                        _stateStore.JumpDetachedVisibilityStates[entityId] =
                            new JumpDetachedVisibilityState(jumpDetachedState.JumpPhase, localPose);
                    }

                    if (!jumpTrack.HasClip)
                    {
                        _trackState.CompletedJumpTrackIds.Add(entityId);
                    }
                }

                var isVisible = _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId) ||
                                _stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId) ||
                                _stateStore.TransitionVisibilityStates.ContainsKey(entityId);
                if (_trackState.VisibilityTracks.TryGetValue(entityId, out var visibilityTrack))
                {
                    isVisible = visibilityTrack.SampleAndAdvance(deltaTime, isVisible);
                    if (visibilityTrack.IsComplete)
                    {
                        _trackState.CompletedVisibilityTrackIds.Add(entityId);
                    }
                }

                var hasActiveMotion = _trackState.LocalMotionTracks.TryGetValue(entityId, out var activeMotionTrack) &&
                                      activeMotionTrack.HasClips;
                var resolvedPlayerAnimationState = _animationSync.ResolvePlayerAnimationState(
                    entityId,
                    ShouldPlayPlayerWalkLoop(entityId),
                    HasActivePlayerWalkMotion(entityId));
                _animationSync.SyncEnemyRuntimeState(entityId, isVisible, hasActiveMotion, _stateStore.ViewsByEntityId);
                _animationSync.SyncPlayerRuntimeState(
                    entityId,
                    isVisible,
                    resolvedPlayerAnimationState,
                    _motionTimingResolver.ResolvePlayerAnimationStateMotionDurationSeconds(
                        entityId,
                        resolvedPlayerAnimationState,
                        timingProfile),
                    _stateStore.ViewsByEntityId);
                UpdateEnemyVisualPresentationState(entityId, isVisible, hasActiveMotion, view);

                if (!isVisible)
                {
                    continue;
                }

                view.SetVisible(true);
                view.ApplyLocalPose(localPose.Position, localPose.Rotation);
                _trackState.VisibleEntityIds.Add(entityId);
            }

            CleanupCompletedMotionTracks();
            CleanupCompletedJumpTracks();
            CleanupCompletedVisibilityTracks();

            viewBinder.HideViewsExcept(_trackState.VisibleEntityIds);
            _animationSync.SyncHiddenDrivers(
                _trackState.VisibleEntityIds,
                entityId => _animationSync.ResolvePlayerAnimationState(
                    entityId,
                    ShouldPlayPlayerWalkLoop(entityId),
                    hasActiveWalkMotion: false),
                (entityId, animationState) => _motionTimingResolver.ResolvePlayerAnimationStateMotionDurationSeconds(
                    entityId,
                    animationState,
                    timingProfile),
                _stateStore.ViewsByEntityId);
        }

        public void ClearJumpPresentationState(int entityId)
        {
            _trackState.JumpTracks.Remove(entityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
        }

        private void CleanupCompletedTopologyTransitionState(bool hasActiveBoardRotationTween)
        {
            if (hasActiveBoardRotationTween ||
                _stateStore.TransitionVisibilityStates.Count == 0)
            {
                return;
            }

            _trackState.CompletedTransitionVisibilityStateIds.Clear();
            foreach (var pair in _stateStore.TransitionVisibilityStates)
            {
                _trackState.CompletedTransitionVisibilityStateIds.Add(pair.Key);
                if (pair.Value.Mode != TickTransitionVisibilityMode.RetainUntilTransitionComplete ||
                    _stateStore.CommittedLocalTargetPoses.ContainsKey(pair.Key) ||
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(pair.Key) ||
                    _stateStore.JumpDetachedVisibilityStates.ContainsKey(pair.Key))
                {
                    continue;
                }

                ClearEntityPresentationMetadataIfFullyHidden(pair.Key);
            }

            for (var i = 0; i < _trackState.CompletedTransitionVisibilityStateIds.Count; i++)
            {
                _stateStore.TransitionVisibilityStates.Remove(_trackState.CompletedTransitionVisibilityStateIds[i]);
            }
        }

        private void CleanupCompletedMotionTracks()
        {
            for (var i = 0; i < _trackState.CompletedMotionTrackIds.Count; i++)
            {
                _trackState.LocalMotionTracks.Remove(_trackState.CompletedMotionTrackIds[i]);
            }
        }

        private void CleanupCompletedJumpTracks()
        {
            for (var i = 0; i < _trackState.CompletedJumpTrackIds.Count; i++)
            {
                _trackState.JumpTracks.Remove(_trackState.CompletedJumpTrackIds[i]);
            }
        }

        private void CleanupCompletedVisibilityTracks()
        {
            for (var i = 0; i < _trackState.CompletedVisibilityTrackIds.Count; i++)
            {
                var entityId = _trackState.CompletedVisibilityTrackIds[i];
                if (!_trackState.VisibilityTracks.TryGetValue(entityId, out var visibilityTrack))
                {
                    continue;
                }

                _trackState.VisibilityTracks.Remove(entityId);
                if (!visibilityTrack.TargetVisibility && !_stateStore.CommittedLocalTargetPoses.ContainsKey(entityId))
                {
                    _stateStore.RetainedLocalTargetPoses.Remove(entityId);
                    if (!_stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId) &&
                        !_stateStore.TransitionVisibilityStates.ContainsKey(entityId))
                    {
                        ClearEntityPresentationMetadataIfFullyHidden(entityId);
                    }
                }
            }
        }

        private void ClearEntityPresentationMetadataIfFullyHidden(int entityId)
        {
            _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
            _stateStore.EnemyAiModesByEntityId.Remove(entityId);
            _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
            _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
            _stateStore.EntityTypesByEntityId.Remove(entityId);
        }

        private bool HasActivePlayerWalkMotion(int entityId)
        {
            return _trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                   motionTrack.HasClips &&
                   motionTrack.TailMotionKind == TickEntityMotionKind.Move;
        }

        private bool ShouldPlayPlayerWalkLoop(int entityId)
        {
            return _trackState.PlayerLocomotionSignalsByEntityId.TryGetValue(entityId, out var signal) &&
                   signal.ShouldPlayWalkLoop;
        }

        private void UpdateEnemyVisualPresentationState(
            int entityId,
            bool isVisible,
            bool hasActiveMotion,
            GameplayEntityView view)
        {
            var facts = BuildEnemyVisualPresentationFacts(entityId, isVisible, hasActiveMotion);
            _stateStore.EnemyVisualFactsByEntityId[entityId] = facts;

            var semanticState = _enemyVisualSemanticResolver.Resolve(facts);
            _stateStore.EnemyVisualSemanticStatesByEntityId[entityId] = semanticState;

            if (view != null &&
                view.TryGetComponent<EnemyInactiveVisualController>(out var controller) &&
                controller != null)
            {
                controller.Apply(semanticState);
            }
        }

        private EnemyVisualPresentationFacts BuildEnemyVisualPresentationFacts(
            int entityId,
            bool isVisible,
            bool hasActiveMotion)
        {
            var isCommittedVisible = _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId);
            var isTransitionVisible = _stateStore.TransitionVisibilityStates.TryGetValue(
                entityId,
                out var transitionVisibilityState);
            var isJumpDetachedVisible = _stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId);
            var isTransitionOnlyVisible = isTransitionVisible && !isCommittedVisible;
            var hasEntityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType);
            var hasEnemyAiMode = _stateStore.EnemyAiModesByEntityId.TryGetValue(entityId, out var aiMode);
            var isEnemy = hasEntityType &&
                          entityType == EntityType.Unit &&
                          hasEnemyAiMode &&
                          aiMode != EnemyAiMode.None;

            return new EnemyVisualPresentationFacts(
                entityId,
                isEnemy,
                isVisible,
                isCommittedVisible,
                isTransitionVisible,
                isTransitionOnlyVisible,
                isJumpDetachedVisible,
                _committedFrameBuilder.ResolveProjectedSlot(
                    entityId,
                    isCommittedVisible,
                    isTransitionVisible,
                    transitionVisibilityState),
                isEnemy ? aiMode : EnemyAiMode.None,
                hasActiveMotion);
        }
    }
}
