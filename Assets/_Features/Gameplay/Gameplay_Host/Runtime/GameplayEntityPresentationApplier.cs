using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct EntityPresentationApplyDiagnostics
    {
        public EntityPresentationApplyDiagnostics(
            int candidateCount,
            int executedCount,
            int skippedCount,
            int activeBypassCount,
            int enemyCandidateCount,
            int enemySkippedCount,
            int playerExecutedCount,
            int signatureChangedCount,
            int signatureUnchangedCount)
        {
            CandidateCount = candidateCount;
            ExecutedCount = executedCount;
            SkippedCount = skippedCount;
            ActiveBypassCount = activeBypassCount;
            EnemyCandidateCount = enemyCandidateCount;
            EnemySkippedCount = enemySkippedCount;
            PlayerExecutedCount = playerExecutedCount;
            SignatureChangedCount = signatureChangedCount;
            SignatureUnchangedCount = signatureUnchangedCount;
        }

        public int CandidateCount { get; }

        public int ExecutedCount { get; }

        public int SkippedCount { get; }

        public int ActiveBypassCount { get; }

        public int EnemyCandidateCount { get; }

        public int EnemySkippedCount { get; }

        public int PlayerExecutedCount { get; }

        public int SignatureChangedCount { get; }

        public int SignatureUnchangedCount { get; }
    }

    internal readonly struct EntityPresentationApplySignature : IEquatable<EntityPresentationApplySignature>
    {
        public EntityPresentationApplySignature(
            int entityId,
            EntityType entityType,
            UnitRole unitRole,
            bool isVisible,
            bool isCommittedVisible,
            bool isTransitionVisible,
            bool isTransitionOnlyVisible,
            bool isJumpDetachedVisible,
            bool isJumpLandingCompletionHeld,
            bool hasProjectedSlot,
            GameplayProjectedFaceSlot projectedSlot,
            bool hasAuthoritativeFace,
            FaceId authoritativeFace,
            bool isGameplayAutonomySuppressed,
            EnemyAiMode aiMode,
            bool hasActiveMotion,
            Vector3 localPosition,
            Quaternion localRotation,
            EnemyVisualSemanticState enemyVisualState)
        {
            EntityId = entityId;
            EntityType = entityType;
            UnitRole = unitRole;
            IsVisible = isVisible;
            IsCommittedVisible = isCommittedVisible;
            IsTransitionVisible = isTransitionVisible;
            IsTransitionOnlyVisible = isTransitionOnlyVisible;
            IsJumpDetachedVisible = isJumpDetachedVisible;
            IsJumpLandingCompletionHeld = isJumpLandingCompletionHeld;
            HasProjectedSlot = hasProjectedSlot;
            ProjectedSlot = projectedSlot;
            HasAuthoritativeFace = hasAuthoritativeFace;
            AuthoritativeFace = authoritativeFace;
            IsGameplayAutonomySuppressed = isGameplayAutonomySuppressed;
            AiMode = aiMode;
            HasActiveMotion = hasActiveMotion;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            EnemyVisualState = enemyVisualState;
        }

        public int EntityId { get; }

        public EntityType EntityType { get; }

        public UnitRole UnitRole { get; }

        public bool IsVisible { get; }

        public bool IsCommittedVisible { get; }

        public bool IsTransitionVisible { get; }

        public bool IsTransitionOnlyVisible { get; }

        public bool IsJumpDetachedVisible { get; }

        public bool IsJumpLandingCompletionHeld { get; }

        public bool HasProjectedSlot { get; }

        public GameplayProjectedFaceSlot ProjectedSlot { get; }

        public bool HasAuthoritativeFace { get; }

        public FaceId AuthoritativeFace { get; }

        public bool IsGameplayAutonomySuppressed { get; }

        public EnemyAiMode AiMode { get; }

        public bool HasActiveMotion { get; }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public EnemyVisualSemanticState EnemyVisualState { get; }

        public bool Equals(EntityPresentationApplySignature other)
        {
            return EntityId == other.EntityId &&
                   EntityType == other.EntityType &&
                   UnitRole == other.UnitRole &&
                   IsVisible == other.IsVisible &&
                   IsCommittedVisible == other.IsCommittedVisible &&
                   IsTransitionVisible == other.IsTransitionVisible &&
                   IsTransitionOnlyVisible == other.IsTransitionOnlyVisible &&
                   IsJumpDetachedVisible == other.IsJumpDetachedVisible &&
                   IsJumpLandingCompletionHeld == other.IsJumpLandingCompletionHeld &&
                   HasProjectedSlot == other.HasProjectedSlot &&
                   (!HasProjectedSlot || ProjectedSlot == other.ProjectedSlot) &&
                   HasAuthoritativeFace == other.HasAuthoritativeFace &&
                   (!HasAuthoritativeFace || AuthoritativeFace == other.AuthoritativeFace) &&
                   IsGameplayAutonomySuppressed == other.IsGameplayAutonomySuppressed &&
                   AiMode == other.AiMode &&
                   HasActiveMotion == other.HasActiveMotion &&
                   LocalPosition == other.LocalPosition &&
                   LocalRotation == other.LocalRotation &&
                   EnemyVisualState.ActivityState == other.EnemyVisualState.ActivityState &&
                   EnemyVisualState.ShouldPauseAnimatorPlayback == other.EnemyVisualState.ShouldPauseAnimatorPlayback &&
                   EnemyVisualState.ShouldPauseAutonomousPresentation ==
                   other.EnemyVisualState.ShouldPauseAutonomousPresentation;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityPresentationApplySignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = EntityId;
                hash = (hash * 397) ^ (int)EntityType;
                hash = (hash * 397) ^ (int)UnitRole;
                hash = (hash * 397) ^ IsVisible.GetHashCode();
                hash = (hash * 397) ^ IsCommittedVisible.GetHashCode();
                hash = (hash * 397) ^ IsTransitionVisible.GetHashCode();
                hash = (hash * 397) ^ IsTransitionOnlyVisible.GetHashCode();
                hash = (hash * 397) ^ IsJumpDetachedVisible.GetHashCode();
                hash = (hash * 397) ^ IsJumpLandingCompletionHeld.GetHashCode();
                hash = (hash * 397) ^ HasProjectedSlot.GetHashCode();
                hash = (hash * 397) ^ (HasProjectedSlot ? (int)ProjectedSlot : 0);
                hash = (hash * 397) ^ HasAuthoritativeFace.GetHashCode();
                hash = (hash * 397) ^ (HasAuthoritativeFace ? (int)AuthoritativeFace : 0);
                hash = (hash * 397) ^ IsGameplayAutonomySuppressed.GetHashCode();
                hash = (hash * 397) ^ (int)AiMode;
                hash = (hash * 397) ^ HasActiveMotion.GetHashCode();
                hash = (hash * 397) ^ LocalPosition.GetHashCode();
                hash = (hash * 397) ^ LocalRotation.GetHashCode();
                hash = (hash * 397) ^ (int)EnemyVisualState.ActivityState;
                hash = (hash * 397) ^ EnemyVisualState.ShouldPauseAnimatorPlayback.GetHashCode();
                hash = (hash * 397) ^ EnemyVisualState.ShouldPauseAutonomousPresentation.GetHashCode();
                return hash;
            }
        }
    }

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

            var candidateCount = 0;
            var executedCount = 0;
            var skippedCount = 0;
            var activeBypassCount = 0;
            var enemyCandidateCount = 0;
            var enemySkippedCount = 0;
            var playerExecutedCount = 0;
            var signatureChangedCount = 0;
            var signatureUnchangedCount = 0;
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

                candidateCount++;
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
                                _stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId) ||
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
                var enemyVisualFacts = BuildEnemyVisualPresentationFacts(entityId, isVisible, hasActiveMotion);
                _stateStore.EnemyVisualFactsByEntityId[entityId] = enemyVisualFacts;
                var enemySemanticState = _enemyVisualSemanticResolver.Resolve(enemyVisualFacts);
                _stateStore.EnemyVisualSemanticStatesByEntityId[entityId] = enemySemanticState;

                var hasEntityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType);
                var hasUnitRole = _stateStore.UnitRolesByEntityId.TryGetValue(entityId, out var unitRole);
                var isEnemy = hasEntityType &&
                              hasUnitRole &&
                              EntityRolePolicy.IsEnemyUnit(entityType, unitRole);
                var isPlayer = hasEntityType &&
                               hasUnitRole &&
                               EntityRolePolicy.IsPlayerUnit(entityType, unitRole);
                var hasEnemyApplySignature = false;
                var enemyApplySignature = default(EntityPresentationApplySignature);
                var requiresEnemyPresentationApply = false;
                if (isEnemy &&
                    TryBuildEnemyApplySignature(
                        entityId,
                        entityType,
                        unitRole,
                        isVisible,
                        hasActiveMotion,
                        localPose,
                        enemyVisualFacts,
                        enemySemanticState,
                        out enemyApplySignature))
                {
                    enemyCandidateCount++;
                    hasEnemyApplySignature = true;
                    requiresEnemyPresentationApply = RequiresEnemyPresentationApply(
                        entityId,
                        hasActiveBoardRotationTween,
                        hasKinematicPoseOverride,
                        hasPlayerDeathHoldPose,
                        hasActiveLocalMotion,
                        hasActiveOriginalViewMotion,
                        isDeferredExitRetained,
                        isContactDelayedRetained,
                        isDeathPresentationPlaying);

                    if (_stateStore.LastEnemyApplySignaturesByEntityId.TryGetValue(
                            entityId,
                            out var previousSignature) &&
                        previousSignature.Equals(enemyApplySignature))
                    {
                        signatureUnchangedCount++;
                        if (!requiresEnemyPresentationApply)
                        {
                            skippedCount++;
                            enemySkippedCount++;
                            if (isVisible)
                            {
                                _stateStore.PresentedLocalPosesByEntityId[entityId] = localPose;
                                _trackState.VisibleEntityIds.Add(entityId);
                            }

                            continue;
                        }

                        activeBypassCount++;
                        _stateStore.LastEnemyApplySignaturesByEntityId.Remove(entityId);
                    }
                    else
                    {
                        signatureChangedCount++;
                    }
                }

                executedCount++;
                if (isPlayer)
                {
                    playerExecutedCount++;
                }

                var wasViewActiveInHierarchy = view.gameObject.activeInHierarchy;
                ApplyEnemyVisualPresentationState(view, enemySemanticState);
                _animationSync.SyncEnemyRuntimeState(
                    entityId,
                    isVisible,
                    hasActiveMotion,
                    enemySemanticState.ShouldPauseAnimatorPlayback,
                    _stateStore.ViewsByEntityId);

                if (!isEnemy)
                {
                    var playerAnimationPlayback = _animationSync.ResolvePlayerAnimationPlayback(
                        entityId,
                        ShouldPlayPlayerWalkLoop(entityId),
                        HasActivePlayerWalkMotion(entityId));
                    _animationSync.SyncPlayerRuntimeState(
                        entityId,
                        isVisible,
                        playerAnimationPlayback,
                        _motionTimingResolver.ResolvePlayerAnimationStateMotionDurationSeconds(
                            entityId,
                            playerAnimationPlayback.State,
                            timingProfile),
                        _stateStore.ViewsByEntityId);
                }

                if (!isVisible)
                {
                    ResetPlayerDeathDisplacement(view);
                    ResetMotionVisualScaleIfApplied(entityId, view);
                    StoreEnemyApplySignatureIfStable(
                        entityId,
                        hasEnemyApplySignature,
                        requiresEnemyPresentationApply,
                        enemyApplySignature);
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
                _trackState.VisibleEntityIds.Add(entityId);
                StoreEnemyApplySignatureIfStable(
                    entityId,
                    hasEnemyApplySignature,
                    requiresEnemyPresentationApply,
                    enemyApplySignature);
            }

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
            _trackState.PresentationEventTargetEntityIds.Clear();
            _stateStore.LastEntityPresentationApplyDiagnostics = new EntityPresentationApplyDiagnostics(
                candidateCount,
                executedCount,
                skippedCount,
                activeBypassCount,
                enemyCandidateCount,
                enemySkippedCount,
                playerExecutedCount,
                signatureChangedCount,
                signatureUnchangedCount);
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

        public void ClearJumpPresentationState(int entityId)
        {
            _trackState.JumpTracks.Remove(entityId);
            _trackState.JumpLandingCompletionHoldEntityIds.Remove(entityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
            _stateStore.LastEnemyApplySignaturesByEntityId.Remove(entityId);
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
            _stateStore.EntityTypesByEntityId.Remove(entityId);
            _stateStore.LastEnemyApplySignaturesByEntityId.Remove(entityId);
            _stateStore.UnitRolesByEntityId.Remove(entityId);
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

        private void StoreEnemyApplySignatureIfStable(
            int entityId,
            bool hasEnemyApplySignature,
            bool requiresEnemyPresentationApply,
            EntityPresentationApplySignature enemyApplySignature)
        {
            if (!hasEnemyApplySignature)
            {
                _stateStore.LastEnemyApplySignaturesByEntityId.Remove(entityId);
                return;
            }

            if (requiresEnemyPresentationApply)
            {
                _stateStore.LastEnemyApplySignaturesByEntityId.Remove(entityId);
                return;
            }

            _stateStore.LastEnemyApplySignaturesByEntityId[entityId] = enemyApplySignature;
        }

        private bool RequiresEnemyPresentationApply(
            int entityId,
            bool hasActiveBoardRotationTween,
            bool hasKinematicPoseOverride,
            bool hasPlayerDeathHoldPose,
            bool hasActiveLocalMotion,
            bool hasActiveOriginalViewMotion,
            bool isDeferredExitRetained,
            bool isContactDelayedRetained,
            bool isDeathPresentationPlaying)
        {
            return hasActiveBoardRotationTween ||
                   hasKinematicPoseOverride ||
                   hasPlayerDeathHoldPose ||
                   hasActiveLocalMotion ||
                   hasActiveOriginalViewMotion ||
                   isDeferredExitRetained ||
                   isContactDelayedRetained ||
                   isDeathPresentationPlaying ||
                   _trackState.PresentationEventTargetEntityIds.Contains(entityId) ||
                   _trackState.JumpTracks.ContainsKey(entityId) ||
                   _trackState.JumpWindupRotationTracks.ContainsKey(entityId) ||
                   _trackState.PlayerFlipResultTurnTracks.ContainsKey(entityId) ||
                   _trackState.VisibilityTracks.ContainsKey(entityId) ||
                   _trackState.GlidePresentationOffsetsByEntityId.ContainsKey(entityId) ||
                   _trackState.JumpLandingCompletionHoldEntityIds.Contains(entityId) ||
                   _trackState.PlayerDeathDisplacementTracks.ContainsKey(entityId) ||
                   _trackState.MotionVisualScaleEntityIds.Contains(entityId) ||
                   _stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId) ||
                   _stateStore.TransitionVisibilityStates.ContainsKey(entityId);
        }

        private bool TryBuildEnemyApplySignature(
            int entityId,
            EntityType entityType,
            UnitRole unitRole,
            bool isVisible,
            bool hasActiveMotion,
            GameplayEntityPose localPose,
            in EnemyVisualPresentationFacts facts,
            in EnemyVisualSemanticState semanticState,
            out EntityPresentationApplySignature signature)
        {
            var hasAuthoritativeFace = false;
            var authoritativeFace = default(FaceId);
            if (facts.IsCommittedVisible &&
                _stateStore.CommittedFacesByEntityId.TryGetValue(entityId, out var committedFace))
            {
                hasAuthoritativeFace = true;
                authoritativeFace = committedFace;
            }
            else if (facts.IsTransitionVisible &&
                     _stateStore.TransitionVisibilityStates.TryGetValue(
                         entityId,
                         out var transitionVisibilityState) &&
                     transitionVisibilityState.SurfaceFace.HasValue)
            {
                hasAuthoritativeFace = true;
                authoritativeFace = transitionVisibilityState.SurfaceFace.Value;
            }

            var hasProjectedSlot = facts.ProjectedSlot.HasValue;
            signature = new EntityPresentationApplySignature(
                entityId,
                entityType,
                unitRole,
                isVisible,
                facts.IsCommittedVisible,
                facts.IsTransitionVisible,
                facts.IsTransitionOnlyVisible,
                facts.IsJumpDetachedVisible,
                facts.IsJumpLandingCompletionHeld,
                hasProjectedSlot,
                hasProjectedSlot ? facts.ProjectedSlot.Value : default,
                hasAuthoritativeFace,
                authoritativeFace,
                facts.IsGameplayAutonomySuppressed,
                facts.AiMode,
                hasActiveMotion,
                localPose.Position,
                localPose.Rotation,
                semanticState);
            return true;
        }

        private static void ApplyEnemyVisualPresentationState(
            GameplayEntityView view,
            in EnemyVisualSemanticState semanticState)
        {
            if (view != null &&
                view.TryGetComponent<EnemyInactiveVisualController>(out var controller) &&
                controller != null)
            {
                controller.Apply(semanticState);
            }

            if (view != null &&
                view.TryGetComponent<EnemyFloatingPresentationDriver>(out var floatingDriver) &&
                floatingDriver != null)
            {
                floatingDriver.Apply(semanticState);
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
            var isJumpLandingCompletionHeld = _trackState.JumpLandingCompletionHoldEntityIds.Contains(entityId);
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

            var isGameplayAutonomySuppressed = isEnemy &&
                                               authoritativeFace.HasValue &&
                                               authoritativeFace.Value != _stateStore.CommittedTopology.BottomFace;

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
                hasActiveMotion);
        }
    }
}
