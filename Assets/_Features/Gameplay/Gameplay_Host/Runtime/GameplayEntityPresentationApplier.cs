using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

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
        private readonly HashSet<int> _processingEntityIds = new();
        private readonly List<int> _processingEntityIdBuffer = new();
        private readonly Dictionary<int, EnemySemanticDriverCacheEntry> _enemySemanticDriversByEntityId = new();

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

            _animationSync.AdvancePresentationBeforeEnemySemantic(deltaTime);
            CleanupCompletedTopologyTransitionState(hasActiveBoardRotationTween);

            _trackState.CompletedMotionTrackIds.Clear();
            _trackState.CompletedMotionVisualScaleEntityIds.Clear();
            _trackState.CompletedJumpTrackIds.Clear();
            _trackState.CompletedJumpWindupRotationTrackIds.Clear();
            _trackState.CompletedPlayerFlipResultTurnTrackIds.Clear();
            _trackState.CompletedVisibilityTrackIds.Clear();
            _trackState.VisibleEntityIds.Clear();
            _stateStore.EnemyVisualFactsByEntityId.Clear();
            _stateStore.EnemyVisualSemanticStatesByEntityId.Clear();
            _stateStore.PresentedLocalPosesByEntityId.Clear();
            AdvancePlayerDeathDisplacementTracks(deltaTime);

            var processingEntityIds = BuildProcessingEntityIds();
            for (var i = 0; i < processingEntityIds.Count; i++)
            {
                var entityId = processingEntityIds[i];
                if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                var hasKinematicPoseOverride = _trackState.KinematicPoseOverrides.TryGetValue(
                    entityId,
                    out var kinematicPoseOverride);
                var hasPlayerDeathHoldPose = _trackState.PlayerDeathHoldPoses.TryGetValue(
                    entityId,
                    out var playerDeathHoldPose);
                if (!_poseResolver.TryResolveFallbackLocalPose(entityId, out var localPose))
                {
                    if (hasKinematicPoseOverride)
                    {
                        localPose = kinematicPoseOverride.LocalPose;
                    }
                    else if (hasPlayerDeathHoldPose)
                    {
                        localPose = playerDeathHoldPose;
                    }
                    else
                    {
                        continue;
                    }
                }

                var motionVisualScaleMultiplier = Vector3.one;
                if (hasKinematicPoseOverride)
                {
                    localPose = kinematicPoseOverride.LocalPose;
                }
                else if (hasPlayerDeathHoldPose)
                {
                    localPose = playerDeathHoldPose;
                }
                else if (_trackState.OriginalViewMotionTracks.TryGetValue(entityId, out var originalViewMotionTrack))
                {
                    var sample = originalViewMotionTrack.Sample();
                    localPose = sample.LocalPose;
                    motionVisualScaleMultiplier = sample.VisualScaleMultiplier;
                    originalViewMotionTrack.Advance(deltaTime);
                    if (originalViewMotionTrack.IsComplete)
                    {
                        localPose = sample.CompletionPose;
                        motionVisualScaleMultiplier = Vector3.one;
                        _trackState.CompletedOriginalViewMotionTrackIds.Add(entityId);
                        _trackState.CompletedPresentationMotionKeys.Add(originalViewMotionTrack.InstanceKey);
                    }
                }
                else if (_trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack))
                {
                    localPose = motionTrack.SampleAndAdvance(
                        deltaTime,
                        localPose,
                        out motionVisualScaleMultiplier);
                    if (!motionTrack.HasClips)
                    {
                        _trackState.CompletedMotionTrackIds.Add(entityId);
                    }
                }

                if (_trackState.JumpTracks.TryGetValue(entityId, out var jumpTrack) &&
                    jumpTrack.HasClip)
                {
                    var freezeJumpTrack =
                        hasActiveBoardRotationTween ||
                        _trackState.JumpTopologySuspendedEntityIds.Contains(entityId);
                    localPose = freezeJumpTrack
                        ? jumpTrack.CurrentPose
                        : jumpTrack.SampleAndAdvance(deltaTime, localPose);
                    if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedState))
                    {
                        _stateStore.JumpDetachedVisibilityStates[entityId] =
                            new JumpDetachedVisibilityState(
                                jumpDetachedState.JumpPhase,
                                localPose,
                                jumpDetachedState.AuthoritativeCell);
                    }

                    if (!freezeJumpTrack && !jumpTrack.HasClip)
                    {
                        _trackState.CompletedJumpTrackIds.Add(entityId);
                    }
                }

                if (_trackState.JumpWindupRotationTracks.TryGetValue(entityId, out var jumpWindupRotationTrack) &&
                    jumpWindupRotationTrack.HasClips)
                {
                    var rotation = jumpWindupRotationTrack.SampleAndAdvance(deltaTime, localPose.Rotation);
                    localPose = new GameplayEntityPose(localPose.Position, rotation);
                    if (!jumpWindupRotationTrack.HasClips)
                    {
                        _trackState.CompletedJumpWindupRotationTrackIds.Add(entityId);
                    }
                }

                if (_trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var playerFlipResultTurnTrack) &&
                    playerFlipResultTurnTrack.HasClips)
                {
                    var rotation = playerFlipResultTurnTrack.SampleAndAdvance(deltaTime, localPose.Rotation);
                    localPose = new GameplayEntityPose(localPose.Position, rotation);
                    if (!playerFlipResultTurnTrack.HasClips)
                    {
                        _trackState.CompletedPlayerFlipResultTurnTrackIds.Add(entityId);
                    }
                }

                var hasActiveLocalMotion = _trackState.LocalMotionTracks.TryGetValue(
                    entityId,
                    out var activeMotionTrack) &&
                    activeMotionTrack.HasClips;
                var hasActiveOriginalViewMotion = _trackState.OriginalViewMotionTracks.TryGetValue(
                    entityId,
                    out var activeOriginalViewMotionTrack) &&
                    !activeOriginalViewMotionTrack.IsComplete;
                var isDeferredExitRetained =
                    _trackState.DeferredExitRetainedEntityIds.Contains(entityId) &&
                    hasActiveLocalMotion &&
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId);
                var isContactDelayedRetained =
                    _trackState.ContactDelayedRetainedEntityIds.Contains(entityId) &&
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId);
                var isDeathPresentationPlaying =
                    _trackState.DeathPresentationPlayingEntityIds.Contains(entityId) &&
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId);
                var isVisible = hasKinematicPoseOverride ||
                                hasPlayerDeathHoldPose ||
                                _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId) ||
                                hasActiveLocalMotion ||
                                hasActiveOriginalViewMotion ||
                                isDeferredExitRetained ||
                                isContactDelayedRetained ||
                                isDeathPresentationPlaying ||
                                IsJumpDetachedVisibleForTopology(entityId, _stateStore.CommittedTopology) ||
                                _stateStore.TransitionVisibilityStates.ContainsKey(entityId);
                if (!hasPlayerDeathHoldPose &&
                    _trackState.VisibilityTracks.TryGetValue(entityId, out var visibilityTrack))
                {
                    isVisible = visibilityTrack.SampleAndAdvance(deltaTime, isVisible);
                    if (visibilityTrack.IsComplete)
                    {
                        _trackState.CompletedVisibilityTrackIds.Add(entityId);
                    }
                }

                var hasActiveMotion = hasKinematicPoseOverride && kinematicPoseOverride.IsActiveLocomotion ||
                                      hasActiveLocalMotion;
                var playerAnimationPlayback = _animationSync.ResolvePlayerAnimationPlayback(
                    entityId,
                    ShouldPlayPlayerWalkLoop(entityId),
                    HasActivePlayerWalkMotion(entityId));
                var wasViewActiveInHierarchy = view.gameObject.activeInHierarchy;
                var enemySemanticState = UpdateEnemyVisualPresentationState(
                    entityId,
                    isVisible,
                    hasActiveMotion,
                    hasActiveBoardRotationTween,
                    view);
                _animationSync.SyncEnemyRuntimeState(
                    entityId,
                    isVisible,
                    hasActiveMotion,
                    enemySemanticState.ShouldPauseAnimatorPlayback,
                    _stateStore.ViewsByEntityId);
                _animationSync.SyncPlayerRuntimeState(
                    entityId,
                    isVisible,
                    playerAnimationPlayback,
                    _motionTimingResolver.ResolvePlayerAnimationStateMotionDurationSeconds(
                        entityId,
                        playerAnimationPlayback.State,
                        timingProfile),
                    _stateStore.ViewsByEntityId);
                if (!isVisible)
                {
                    ResetPlayerDeathDisplacement(view);
                    ResetMotionVisualScaleIfApplied(entityId, view);
                    continue;
                }

                if (_trackState.GlidePresentationOffsetsByEntityId.TryGetValue(entityId, out var glideOffset))
                {
                    localPose = new GameplayEntityPose(localPose.Position + glideOffset, localPose.Rotation);
                }

                view.SetVisible(true);
                if (!wasViewActiveInHierarchy && view.gameObject.activeInHierarchy)
                {
                    _animationSync.ResyncEnemyAnimatorState(entityId, _stateStore.ViewsByEntityId);
                }

                view.ApplyLocalPose(localPose.Position, localPose.Rotation);
                ApplyMotionVisualScale(entityId, view, motionVisualScaleMultiplier);
                _stateStore.PresentedLocalPosesByEntityId[entityId] = localPose;
                ApplyPlayerDeathDisplacement(entityId, view);
                if (HasJumpAirborneVisualState(entityId) &&
                    !enemySemanticState.ShouldPauseAnimatorPlayback)
                {
                    _animationSync.EnsureEnemyJumpAirborneBaseAnimation(entityId, _stateStore.ViewsByEntityId);
                }

                _trackState.VisibleEntityIds.Add(entityId);
            }

            _animationSync.AdvanceEnemyAutonomousPresentationAfterSemantic(deltaTime);
            CleanupCompletedMotionTracks();
            CleanupCompletedOriginalViewMotionTracks();
            CleanupCompletedJumpTracks();
            CleanupCompletedJumpWindupRotationTracks();
            CleanupCompletedPlayerFlipResultTurnTracks();
            CleanupCompletedVisibilityTracks();
            ApplyPendingFlipInteractionResets();
            ApplyFlipInteractionTracks(deltaTime);
            CleanupHiddenPlayerDeathDisplacementTracks();
            CleanupHiddenMotionVisualScales();

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

        public void ResetAllPlayerDeathDisplacements()
        {
            foreach (var pair in _stateStore.ViewsByEntityId)
            {
                ResetPlayerDeathDisplacement(pair.Value);
            }

            _trackState.PlayerDeathDisplacementTracks.Clear();
            _stateStore.PresentedLocalPosesByEntityId.Clear();
        }

        public void ResetEnemySemanticPresentationDriverCache()
        {
            _enemySemanticDriversByEntityId.Clear();
        }

        private IReadOnlyList<int> BuildProcessingEntityIds()
        {
            _processingEntityIds.Clear();
            _processingEntityIdBuffer.Clear();

            var stateStoreEntityIds = _stateStore.BuildProcessingEntityIds();
            for (var i = 0; i < stateStoreEntityIds.Count; i++)
            {
                AddProcessingEntityId(stateStoreEntityIds[i]);
            }

            foreach (var pair in _trackState.KinematicPoseOverrides)
            {
                AddProcessingEntityId(pair.Key);
            }

            foreach (var pair in _trackState.PlayerDeathHoldPoses)
            {
                AddProcessingEntityId(pair.Key);
            }

            foreach (var pair in _trackState.PlayerFlipResultTurnTracks)
            {
                AddProcessingEntityId(pair.Key);
            }

            _processingEntityIdBuffer.Sort();
            return _processingEntityIdBuffer;
        }

        private void AddProcessingEntityId(int entityId)
        {
            if (_processingEntityIds.Add(entityId))
            {
                _processingEntityIdBuffer.Add(entityId);
            }
        }

        private bool HasJumpAirborneVisualState(int entityId)
        {
            if (_trackState.JumpTracks.ContainsKey(entityId))
            {
                return true;
            }

            return _stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var detachedState) &&
                   detachedState.JumpPhase == EnemyJumpPhase.Airborne;
        }

        private bool IsJumpDetachedVisibleForTopology(int entityId, CubeTopologyState topology)
        {
            return _stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var detachedState) &&
                   IsJumpDetachedVisibleForTopology(detachedState, topology);
        }

        private static bool IsJumpDetachedVisibleForTopology(
            in JumpDetachedVisibilityState detachedState,
            CubeTopologyState topology)
        {
            return detachedState.JumpPhase == EnemyJumpPhase.Airborne &&
                   topology.IsFaceActive(detachedState.AuthoritativeFace);
        }

        public void ClearJumpPresentationState(int entityId)
        {
            _trackState.JumpTracks.Remove(entityId);
            _trackState.JumpLandingCompletionHoldEntityIds.Remove(entityId);
            _trackState.JumpTopologySuspendedEntityIds.Remove(entityId);
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
                if (_stateStore.CommittedLocalTargetPoses.ContainsKey(pair.Key) ||
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

        private void CleanupCompletedOriginalViewMotionTracks()
        {
            for (var i = 0; i < _trackState.CompletedOriginalViewMotionTrackIds.Count; i++)
            {
                _trackState.OriginalViewMotionTracks.Remove(_trackState.CompletedOriginalViewMotionTrackIds[i]);
            }
        }

        private void CleanupCompletedJumpTracks()
        {
            for (var i = 0; i < _trackState.CompletedJumpTrackIds.Count; i++)
            {
                var entityId = _trackState.CompletedJumpTrackIds[i];
                var wasLandingCompletionHeld = _trackState.JumpLandingCompletionHoldEntityIds.Remove(entityId);
                _trackState.JumpTracks.Remove(entityId);
                _trackState.JumpTopologySuspendedEntityIds.Remove(entityId);
                _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
                if (wasLandingCompletionHeld)
                {
                    _animationSync.CompleteEnemyJumpLandingPresentation(entityId, _stateStore.ViewsByEntityId);
                }
            }
        }

        private void CleanupCompletedJumpWindupRotationTracks()
        {
            for (var i = 0; i < _trackState.CompletedJumpWindupRotationTrackIds.Count; i++)
            {
                _trackState.JumpWindupRotationTracks.Remove(_trackState.CompletedJumpWindupRotationTrackIds[i]);
            }
        }

        private void CleanupCompletedPlayerFlipResultTurnTracks()
        {
            for (var i = 0; i < _trackState.CompletedPlayerFlipResultTurnTrackIds.Count; i++)
            {
                _trackState.PlayerFlipResultTurnTracks.Remove(_trackState.CompletedPlayerFlipResultTurnTrackIds[i]);
            }
        }

        private void AdvancePlayerDeathDisplacementTracks(float deltaTime)
        {
            foreach (var pair in _trackState.PlayerDeathDisplacementTracks)
            {
                pair.Value?.Advance(deltaTime);
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

        private void CleanupHiddenPlayerDeathDisplacementTracks()
        {
            if (_trackState.PlayerDeathDisplacementTracks.Count == 0)
            {
                return;
            }

            _trackState.CompletedPlayerDeathDisplacementTrackIds.Clear();
            foreach (var pair in _trackState.PlayerDeathDisplacementTracks)
            {
                if (_trackState.VisibleEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                if (_stateStore.ViewsByEntityId.TryGetValue(pair.Key, out var view))
                {
                    ResetPlayerDeathDisplacement(view);
                }

                pair.Value?.Clear();
                _trackState.CompletedPlayerDeathDisplacementTrackIds.Add(pair.Key);
            }

            for (var i = 0; i < _trackState.CompletedPlayerDeathDisplacementTrackIds.Count; i++)
            {
                _trackState.PlayerDeathDisplacementTracks.Remove(_trackState.CompletedPlayerDeathDisplacementTrackIds[i]);
            }
        }

        private void CleanupHiddenMotionVisualScales()
        {
            if (_trackState.MotionVisualScaleEntityIds.Count == 0)
            {
                return;
            }

            _trackState.CompletedMotionVisualScaleEntityIds.Clear();
            foreach (var entityId in _trackState.MotionVisualScaleEntityIds)
            {
                if (_trackState.VisibleEntityIds.Contains(entityId))
                {
                    continue;
                }

                if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                    view != null)
                {
                    view.ResetModelRootVisualScale();
                }

                _trackState.CompletedMotionVisualScaleEntityIds.Add(entityId);
            }

            for (var i = 0; i < _trackState.CompletedMotionVisualScaleEntityIds.Count; i++)
            {
                _trackState.MotionVisualScaleEntityIds.Remove(_trackState.CompletedMotionVisualScaleEntityIds[i]);
            }
        }

        private void ApplyMotionVisualScale(
            int entityId,
            GameplayEntityView view,
            Vector3 visualScaleMultiplier)
        {
            if (view == null ||
                !_stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType) ||
                entityType != EntityType.Box)
            {
                ResetMotionVisualScaleIfApplied(entityId, view);
                return;
            }

            if (IsApproximatelyOne(visualScaleMultiplier))
            {
                ResetMotionVisualScaleIfApplied(entityId, view);
                return;
            }

            view.ApplyModelRootVisualScale(visualScaleMultiplier);
            _trackState.MotionVisualScaleEntityIds.Add(entityId);
        }

        private void ResetMotionVisualScaleIfApplied(int entityId, GameplayEntityView view)
        {
            if (!_trackState.MotionVisualScaleEntityIds.Remove(entityId) ||
                view == null)
            {
                return;
            }

            view.ResetModelRootVisualScale();
        }

        private static bool IsApproximatelyOne(Vector3 scale)
        {
            return Mathf.Abs(scale.x - 1f) <= 0.0001f &&
                   Mathf.Abs(scale.y - 1f) <= 0.0001f &&
                   Mathf.Abs(scale.z - 1f) <= 0.0001f;
        }

        private void ApplyPlayerDeathDisplacement(int entityId, GameplayEntityView view)
        {
            if (view == null)
            {
                return;
            }

            if (!_trackState.PlayerDeathDisplacementTracks.TryGetValue(entityId, out var track) ||
                track == null ||
                track.State == PlayerDeathDisplacementTrackState.Cleared)
            {
                ResetPlayerDeathDisplacement(view);
                return;
            }

            var driver = GetOrAddPlayerDeathDisplacementDriver(view);
            driver?.ApplyDisplacement(track.CurrentOffset);
        }

        private static PlayerDeathDisplacementDriver GetOrAddPlayerDeathDisplacementDriver(GameplayEntityView view)
        {
            if (view == null ||
                !view.TryGetComponent<PlayerAnimatorDriver>(out _))
            {
                return null;
            }

            if (view.TryGetComponent<PlayerDeathDisplacementDriver>(out var driver) &&
                driver != null)
            {
                driver.Initialize(view.ModelRoot);
                return driver;
            }

            driver = view.gameObject.AddComponent<PlayerDeathDisplacementDriver>();
            driver.Initialize(view.ModelRoot);
            return driver;
        }

        private static void ResetPlayerDeathDisplacement(GameplayEntityView view)
        {
            if (view == null ||
                !view.TryGetComponent<PlayerDeathDisplacementDriver>(out var driver) ||
                driver == null)
            {
                return;
            }

            driver.ResetDisplacement();
        }

        private void ApplyPendingFlipInteractionResets()
        {
            for (var i = 0; i < _trackState.FlipInteractionResetRequests.Count; i++)
            {
                var request = _trackState.FlipInteractionResetRequests[i];
                ResetFlipInteraction(request.PlayerEntityId, request.BoxEntityId);
            }

            _trackState.FlipInteractionResetRequests.Clear();
        }

        private void ApplyFlipInteractionTracks(float deltaTime)
        {
            _trackState.CompletedFlipInteractionTrackIds.Clear();

            foreach (var pair in _trackState.FlipInteractionTracks)
            {
                var track = pair.Value;
                if (track.IsComplete)
                {
                    ResetFlipInteraction(track.PlayerEntityId, track.BoxEntityId);
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                    continue;
                }

                var hasPlayerView = _stateStore.ViewsByEntityId.TryGetValue(track.PlayerEntityId, out var playerView) &&
                                    playerView != null;
                var hasBoxView = _stateStore.ViewsByEntityId.TryGetValue(track.BoxEntityId, out var boxView) &&
                                 boxView != null;
                if (!hasPlayerView || !hasBoxView)
                {
                    ResetFlipInteraction(track.PlayerEntityId, track.BoxEntityId);
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                    continue;
                }

                var playerDriver = playerView.GetComponent<PlayerFlipInteractionDriver>();
                var boxDriver = boxView.GetComponent<BoxFlipInteractionDriver>();
                var handRestWorldPose = playerDriver != null
                    ? playerDriver.GetHandRestWorldPose()
                    : new Pose(playerView.transform.position, playerView.transform.rotation);
                var boxGripWorldPose = boxDriver != null
                    ? boxDriver.GetGripWorldPose()
                    : ResolveFallbackGripPose(boxView);
                var sample = track.Sample(handRestWorldPose, boxGripWorldPose);
                var suppressBoxOverlay = HasSuppressingOriginalViewMotion(track.BoxEntityId);

                if (boxDriver != null && !suppressBoxOverlay)
                {
                    boxDriver.ApplyInteraction(
                        sample.BoxLocalPositionOffset,
                        sample.BoxLocalRotationOffset,
                        sample.BoxWeight);
                }
                else if (boxDriver != null)
                {
                    boxDriver.ResetInteraction();
                }

                if (playerDriver != null)
                {
                    playerDriver.ApplyInteraction(sample.HandTargetWorldPose, sample.HandWeight);
                }

                track.Advance(deltaTime);
                if (track.IsComplete)
                {
                    ResetFlipInteraction(track.PlayerEntityId, track.BoxEntityId);
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _trackState.CompletedFlipInteractionTrackIds.Count; i++)
            {
                _trackState.FlipInteractionTracks.Remove(_trackState.CompletedFlipInteractionTrackIds[i]);
            }
        }

        private void ClearEntityPresentationMetadataIfFullyHidden(int entityId)
        {
            _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
            _stateStore.CommittedFacesByEntityId.Remove(entityId);
            _stateStore.EnemyAiModesByEntityId.Remove(entityId);
            _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
            _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
            _enemySemanticDriversByEntityId.Remove(entityId);
            _stateStore.EntityTypesByEntityId.Remove(entityId);
            _stateStore.UnitRolesByEntityId.Remove(entityId);
        }

        private readonly struct EnemySemanticDriverCacheEntry
        {
            public EnemySemanticDriverCacheEntry(
                int viewInstanceId,
                IEnemyVisualSemanticPresentationDriver[] drivers)
            {
                ViewInstanceId = viewInstanceId;
                Drivers = drivers;
            }

            public int ViewInstanceId { get; }

            public IEnemyVisualSemanticPresentationDriver[] Drivers { get; }
        }

        private void ResetFlipInteraction(int playerEntityId, int boxEntityId)
        {
            if (_stateStore.ViewsByEntityId.TryGetValue(playerEntityId, out var playerView) &&
                playerView != null &&
                playerView.TryGetComponent<PlayerFlipInteractionDriver>(out var playerDriver) &&
                playerDriver != null)
            {
                playerDriver.ResetInteraction();
            }

            if (_stateStore.ViewsByEntityId.TryGetValue(boxEntityId, out var boxView) &&
                boxView != null &&
                boxView.TryGetComponent<BoxFlipInteractionDriver>(out var boxDriver) &&
                boxDriver != null)
            {
                boxDriver.ResetInteraction();
            }
        }

        private bool HasSuppressingOriginalViewMotion(int entityId)
        {
            return _trackState.OriginalViewMotionTracks.TryGetValue(entityId, out var track) &&
                   track != null &&
                   !track.IsComplete &&
                   track.InteractionPolicy == PresentationMotionInteractionPolicy.SuppressBoxInteractionOverlay;
        }

        private static Pose ResolveFallbackGripPose(GameplayEntityView boxView)
        {
            var target = boxView != null && boxView.ModelRoot != null
                ? boxView.ModelRoot
                : boxView != null
                    ? boxView.transform
                    : null;
            return target != null
                ? new Pose(target.position, target.rotation)
                : new Pose(Vector3.zero, Quaternion.identity);
        }

        private bool HasActivePlayerWalkMotion(int entityId)
        {
            if (_trackState.KinematicPoseOverrides.TryGetValue(entityId, out var kinematicPose) &&
                kinematicPose.IsActiveLocomotion)
            {
                return true;
            }

            return _trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                   motionTrack.HasClips &&
                   motionTrack.TailMotionKind == TickEntityMotionKind.Move;
        }

        private bool ShouldPlayPlayerWalkLoop(int entityId)
        {
            return _trackState.PlayerLocomotionSignalsByEntityId.TryGetValue(entityId, out var signal) &&
                   signal.ShouldPlayWalkLoop;
        }

        private EnemyVisualSemanticState UpdateEnemyVisualPresentationState(
            int entityId,
            bool isVisible,
            bool hasActiveMotion,
            bool hasActiveBoardRotationTween,
            GameplayEntityView view)
        {
            var facts = BuildEnemyVisualPresentationFacts(
                entityId,
                isVisible,
                hasActiveMotion,
                hasActiveBoardRotationTween);
            _stateStore.EnemyVisualFactsByEntityId[entityId] = facts;

            var semanticState = _enemyVisualSemanticResolver.Resolve(facts);
            _stateStore.EnemyVisualSemanticStatesByEntityId[entityId] = semanticState;
            ApplyEnemyVisualSemanticPresentationDrivers(entityId, view, semanticState);

            return semanticState;
        }

        private void ApplyEnemyVisualSemanticPresentationDrivers(
            int entityId,
            GameplayEntityView view,
            in EnemyVisualSemanticState state)
        {
            var drivers = ResolveEnemySemanticPresentationDrivers(entityId, view);
            for (var i = 0; i < drivers.Length; i++)
            {
                drivers[i]?.ApplyEnemyVisualSemanticState(state);
            }
        }

        private IEnemyVisualSemanticPresentationDriver[] ResolveEnemySemanticPresentationDrivers(
            int entityId,
            GameplayEntityView view)
        {
            if (view == null)
            {
                _enemySemanticDriversByEntityId.Remove(entityId);
                return Array.Empty<IEnemyVisualSemanticPresentationDriver>();
            }

            var viewInstanceId = view.GetInstanceID();
            if (_enemySemanticDriversByEntityId.TryGetValue(entityId, out var cached) &&
                cached.ViewInstanceId == viewInstanceId &&
                cached.Drivers != null)
            {
                return cached.Drivers;
            }

            var behaviours = view.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            var drivers = new List<IEnemyVisualSemanticPresentationDriver>(behaviours.Length);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IEnemyVisualSemanticPresentationDriver driver)
                {
                    drivers.Add(driver);
                }
            }

            var resolvedDrivers = drivers.Count == 0
                ? Array.Empty<IEnemyVisualSemanticPresentationDriver>()
                : drivers.ToArray();
            _enemySemanticDriversByEntityId[entityId] =
                new EnemySemanticDriverCacheEntry(viewInstanceId, resolvedDrivers);
            return resolvedDrivers;
        }

        private EnemyVisualPresentationFacts BuildEnemyVisualPresentationFacts(
            int entityId,
            bool isVisible,
            bool hasActiveMotion,
            bool hasActiveBoardRotationTween)
        {
            var isCommittedVisible = _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId);
            var isTransitionVisible = _stateStore.TransitionVisibilityStates.TryGetValue(
                entityId,
                out var transitionVisibilityState);
            var hasJumpDetachedState = _stateStore.JumpDetachedVisibilityStates.TryGetValue(
                entityId,
                out var jumpDetachedState);
            var isJumpDetachedVisible = hasJumpDetachedState &&
                                        IsJumpDetachedVisibleForTopology(
                                            jumpDetachedState,
                                            _stateStore.CommittedTopology);
            var isJumpLandingCompletionHeld = _trackState.JumpLandingCompletionHoldEntityIds.Contains(entityId);
            var isJumpTopologySuspended = _trackState.JumpTopologySuspendedEntityIds.Contains(entityId);
            var isTransitionOnlyVisible = isTransitionVisible && !isCommittedVisible;
            var hasEntityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType);
            var hasUnitRole = _stateStore.UnitRolesByEntityId.TryGetValue(entityId, out var unitRole);
            var hasEnemyAiMode = _stateStore.EnemyAiModesByEntityId.TryGetValue(entityId, out var aiMode);
            var isEnemy = hasEntityType &&
                          hasUnitRole &&
                          EntityRolePolicy.IsEnemyUnit(entityType, unitRole);
            FaceId? authoritativeFace = null;
            if (isCommittedVisible &&
                _stateStore.CommittedFacesByEntityId.TryGetValue(entityId, out var committedFace))
            {
                authoritativeFace = committedFace;
            }
            else if (isTransitionVisible)
            {
                authoritativeFace = transitionVisibilityState.SurfaceFace;
            }
            else if (hasJumpDetachedState)
            {
                authoritativeFace = jumpDetachedState.AuthoritativeFace;
            }

            var isGameplayAutonomySuppressed = isEnemy &&
                                               authoritativeFace.HasValue &&
                                               authoritativeFace.Value != _stateStore.CommittedTopology.BottomFace;
            var isOnVisualFrontFace = isEnemy &&
                                      authoritativeFace.HasValue &&
                                      authoritativeFace.Value == _stateStore.CommittedTopology.FrontFace;

            return new EnemyVisualPresentationFacts(
                entityId,
                isEnemy,
                isVisible,
                isCommittedVisible,
                isTransitionVisible,
                isTransitionOnlyVisible,
                isJumpDetachedVisible,
                isJumpLandingCompletionHeld,
                _committedFrameBuilder.ResolveProjectedSlot(
                    entityId,
                    isCommittedVisible,
                    isTransitionVisible,
                    transitionVisibilityState),
                isGameplayAutonomySuppressed,
                isEnemy ? aiMode : EnemyAiMode.None,
                hasActiveMotion,
                HasJumpAirborneVisualState(entityId),
                hasActiveBoardRotationTween,
                isJumpTopologySuspended,
                isOnVisualFrontFace);
        }
    }
}
