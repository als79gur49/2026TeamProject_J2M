using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
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
            bool hasJumpAirborneVisualState,
            bool isTopologyTransitionActive,
            bool isJumpTopologySuspended,
            bool isOnVisualFrontFace,
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
            HasJumpAirborneVisualState = hasJumpAirborneVisualState;
            IsTopologyTransitionActive = isTopologyTransitionActive;
            IsJumpTopologySuspended = isJumpTopologySuspended;
            IsOnVisualFrontFace = isOnVisualFrontFace;
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

        public bool HasJumpAirborneVisualState { get; }

        public bool IsTopologyTransitionActive { get; }

        public bool IsJumpTopologySuspended { get; }

        public bool IsOnVisualFrontFace { get; }

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
                   HasJumpAirborneVisualState == other.HasJumpAirborneVisualState &&
                   IsTopologyTransitionActive == other.IsTopologyTransitionActive &&
                   IsJumpTopologySuspended == other.IsJumpTopologySuspended &&
                   IsOnVisualFrontFace == other.IsOnVisualFrontFace &&
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
                hash = (hash * 397) ^ HasJumpAirborneVisualState.GetHashCode();
                hash = (hash * 397) ^ IsTopologyTransitionActive.GetHashCode();
                hash = (hash * 397) ^ IsJumpTopologySuspended.GetHashCode();
                hash = (hash * 397) ^ IsOnVisualFrontFace.GetHashCode();
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
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private readonly HashSet<int> _processingEntityIds = new();
        private readonly List<int> _processingEntityIdBuffer = new();
        private readonly Dictionary<int, EnemySemanticDriverCacheEntry> _enemySemanticDriversByEntityId = new();

        public GameplayEntityPresentationApplier(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayAnimationSyncCoordinator animationSync,
            GameplayMotionTimingResolver motionTimingResolver,
            IEnemyVisualSemanticResolver enemyVisualSemanticResolver,
            GameplayCommittedFrameBuilder committedFrameBuilder)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
            _motionTimingResolver = motionTimingResolver ?? throw new ArgumentNullException(nameof(motionTimingResolver));
            _enemyVisualSemanticResolver =
                enemyVisualSemanticResolver ?? throw new ArgumentNullException(nameof(enemyVisualSemanticResolver));
            _committedFrameBuilder = committedFrameBuilder ?? throw new ArgumentNullException(nameof(committedFrameBuilder));
        }

        public void Apply(
            float deltaTime,
            bool hasActiveBoardRotationTween,
            ResolvedPresentationFrameSet resolvedFrames,
            ResolvedPresentationChannelSet resolvedChannels,
            ResolvedPresentationVisibilitySet resolvedVisibility,
            GameplayEntityViewBinder viewBinder,
            GameplayTimingProfile timingProfile)
        {
            if (resolvedFrames == null)
            {
                throw new ArgumentNullException(nameof(resolvedFrames));
            }

            if (resolvedChannels == null)
            {
                throw new ArgumentNullException(nameof(resolvedChannels));
            }

            if (resolvedVisibility == null)
            {
                throw new ArgumentNullException(nameof(resolvedVisibility));
            }

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
            var processingEntityIds = BuildProcessingEntityIds(resolvedFrames, resolvedChannels, resolvedVisibility);
            for (var i = 0; i < processingEntityIds.Count; i++)
            {
                var entityId = processingEntityIds[i];
                if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                var hasResolvedFrame = resolvedFrames.TryGetFrame(entityId, out var resolvedFrame);
                var hasPresentationPoseOverride = hasResolvedFrame &&
                                                  IsLivePresentationPoseOverride(
                                                      resolvedFrame.Provenance.BaseSource);
                var isActivePresentationPoseLocomotion =
                    hasPresentationPoseOverride && resolvedFrame.IsActiveLocomotion;
                var hasPlayerDeathHoldPose = hasResolvedFrame &&
                                             resolvedFrame.Provenance.TerminalSource ==
                                             PresentationPoseSourceKind.PlayerDeathHold;
                if (!hasResolvedFrame)
                {
                    continue;
                }

                var localPose = resolvedFrame.BasePose;
                var motionVisualScaleMultiplier = Vector3.one;
                if (!hasPresentationPoseOverride &&
                    !hasPlayerDeathHoldPose &&
                    _trackState.OriginalViewMotionTracks.TryGetValue(entityId, out var originalViewMotionTrack))
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
                        _trackState.BoxMotionTelemetry.RecordTrackCompleted(
                            PresentationMotionFactKind.BoxFlipImpact,
                            tickIndex: 0,
                            entityId,
                            originalViewMotionTrack.InstanceKey.GetHashCode());
                    }
                }
                else if (!hasPresentationPoseOverride &&
                         !hasPlayerDeathHoldPose &&
                         _trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack))
                {
                    localPose = motionTrack.SampleAndAdvance(
                        deltaTime,
                        localPose,
                        out motionVisualScaleMultiplier);
                    if (!motionTrack.HasClips)
                    {
                        _trackState.CompletedMotionTrackIds.Add(entityId);
                        if (TryMapBoxMotionSemantic(
                                motionTrack.LastCompletedMotionKind,
                                out var completedSemantic))
                        {
                            _trackState.BoxMotionTelemetry.RecordTrackCompleted(
                                completedSemantic,
                                tickIndex: 0,
                                entityId,
                                dedupeKey: 0);
                        }
                    }
                }

                var hasResolvedAdditiveLocalOffset =
                    resolvedChannels.TryGetAdditiveLocalOffset(entityId, out var additiveLocalOffset);
                if (hasResolvedAdditiveLocalOffset)
                {
                    localPose = new GameplayEntityPose(
                        resolvedFrame.BasePose.Position + additiveLocalOffset.Offset,
                        localPose.Rotation);
                }

                var hasResolvedAdditiveRotation =
                    resolvedChannels.TryGetAdditiveRotation(entityId, out var additiveRotation);
                if (hasResolvedAdditiveRotation)
                {
                    localPose = new GameplayEntityPose(localPose.Position, additiveRotation.Rotation);
                }

                if (_trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var playerFlipResultTurnTrack) &&
                    playerFlipResultTurnTrack.Track.HasClips)
                {
                    var rotation = playerFlipResultTurnTrack.Track.SampleAndAdvance(deltaTime, localPose.Rotation);
                    localPose = new GameplayEntityPose(localPose.Position, rotation);
                    if (!playerFlipResultTurnTrack.Track.HasClips)
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
                var hasResolvedVisibility =
                    resolvedVisibility.TryGetVisibility(entityId, out var resolvedEntityVisibility);
                var isVisible = hasPresentationPoseOverride ||
                                hasPlayerDeathHoldPose ||
                                _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId) ||
                                hasActiveLocalMotion ||
                                hasActiveOriginalViewMotion ||
                                isDeferredExitRetained ||
                                isContactDelayedRetained ||
                                isDeathPresentationPlaying ||
                                (hasResolvedVisibility && resolvedEntityVisibility.IsVisible) ||
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

                var hasActiveMotion = hasPresentationPoseOverride && isActivePresentationPoseLocomotion ||
                                      hasActiveLocalMotion;
                var enemyVisualFacts = BuildEnemyVisualPresentationFacts(
                    entityId,
                    isVisible,
                    hasActiveMotion,
                    hasActiveBoardRotationTween);
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
                        hasPresentationPoseOverride,
                        hasPlayerDeathHoldPose,
                        hasActiveLocalMotion,
                        hasActiveOriginalViewMotion,
                        hasResolvedAdditiveLocalOffset || hasResolvedAdditiveRotation,
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
                ApplyEnemyVisualSemanticPresentationDrivers(entityId, view, enemySemanticState);
                _animationSync.SyncEnemyRuntimeState(
                    entityId,
                    isVisible,
                    hasActiveMotion,
                    enemySemanticState.ShouldPauseAnimatorPlayback,
                    _stateStore.ViewsByEntityId);

                var hasPlayerAnimationPlayback = false;
                var playerAnimationPlayback = default(PlayerAnimationPlaybackResolution);
                var playerAnimationMotionDurationSeconds = 0f;
                if (!isEnemy)
                {
                    playerAnimationPlayback = _animationSync.ResolvePlayerAnimationPlayback(
                        entityId,
                        ShouldPlayPlayerWalkLoop(entityId),
                        HasActivePlayerWalkMotion(entityId, isActivePresentationPoseLocomotion));
                    playerAnimationMotionDurationSeconds =
                        _motionTimingResolver.ResolvePlayerAnimationStateMotionDurationSeconds(
                            entityId,
                            playerAnimationPlayback.State,
                            timingProfile);
                    hasPlayerAnimationPlayback = true;
                    _animationSync.SyncPlayerRuntimeState(
                        entityId,
                        isVisible,
                        playerAnimationPlayback,
                        playerAnimationMotionDurationSeconds,
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
                    if (hasPlayerAnimationPlayback)
                    {
                        _animationSync.SyncPlayerRuntimeState(
                            entityId,
                            isVisible: true,
                            playerAnimationPlayback,
                            playerAnimationMotionDurationSeconds,
                            _stateStore.ViewsByEntityId);
                    }
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
                StoreEnemyApplySignatureIfStable(
                    entityId,
                    hasEnemyApplySignature,
                    requiresEnemyPresentationApply,
                    enemyApplySignature);
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

        public void ResetEnemySemanticPresentationDriverCache()
        {
            _enemySemanticDriversByEntityId.Clear();
        }

        internal BoxFlipInteractionResetResult ResetBoxFlipInteractionsForKnownViews()
        {
            var result = default(BoxFlipInteractionResetResult);
            foreach (var pair in _stateStore.ViewsByEntityId)
            {
                var entityId = pair.Key;
                if (!_stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType) ||
                    entityType != EntityType.Box ||
                    pair.Value == null ||
                    !pair.Value.TryGetComponent<BoxFlipInteractionDriver>(out var boxDriver) ||
                    boxDriver == null)
                {
                    continue;
                }

                result = result.Add(boxDriver.ResetInteractionForDiagnostics());
            }

            return result;
        }

        private IReadOnlyList<int> BuildProcessingEntityIds(
            ResolvedPresentationFrameSet resolvedFrames,
            ResolvedPresentationChannelSet resolvedChannels,
            ResolvedPresentationVisibilitySet resolvedVisibility)
        {
            _processingEntityIds.Clear();
            _processingEntityIdBuffer.Clear();

            var resolvedEntityIds = resolvedFrames.EntityIds;
            for (var i = 0; i < resolvedEntityIds.Count; i++)
            {
                AddProcessingEntityId(resolvedEntityIds[i]);
            }

            var stateStoreEntityIds = _stateStore.BuildProcessingEntityIds();
            for (var i = 0; i < stateStoreEntityIds.Count; i++)
            {
                AddProcessingEntityId(stateStoreEntityIds[i]);
            }

            var resolvedChannelEntityIds = resolvedChannels.EntityIds;
            for (var i = 0; i < resolvedChannelEntityIds.Count; i++)
            {
                AddProcessingEntityId(resolvedChannelEntityIds[i]);
            }

            var resolvedVisibilityEntityIds = resolvedVisibility.EntityIds;
            for (var i = 0; i < resolvedVisibilityEntityIds.Count; i++)
            {
                AddProcessingEntityId(resolvedVisibilityEntityIds[i]);
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

        private static bool IsLivePresentationPoseOverride(PresentationPoseSourceKind sourceKind)
        {
            return sourceKind == PresentationPoseSourceKind.PlayerContinuousLocomotion ||
                   sourceKind == PresentationPoseSourceKind.EnemyKinematicMotion;
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
            _trackState.ClearAirborneJumpTrackKeys(entityId);
            _trackState.JumpLandingCompletionHoldEntityIds.Remove(entityId);
            _trackState.JumpTopologySuspendedEntityIds.Remove(entityId);
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
                if (_trackState.ActiveAirborneJumpTrackKeys.TryGetValue(entityId, out var airborneKey))
                {
                    _trackState.CompletedAirborneJumpTrackKeys.Add(airborneKey);
                    _trackState.ActiveAirborneJumpTrackKeys.Remove(entityId);
                }

                _trackState.JumpTracks.Remove(entityId);
                _trackState.JumpTopologySuspendedEntityIds.Remove(entityId);
                if (wasLandingCompletionHeld)
                {
                    _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
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
                RecordFlipInteractionReset(ResetFlipInteraction(request.PlayerEntityId, request.BoxEntityId));
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
                    RecordFlipInteractionReset(ResetFlipInteraction(track.PlayerEntityId, track.BoxEntityId));
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                    continue;
                }

                var hasPlayerView = _stateStore.ViewsByEntityId.TryGetValue(track.PlayerEntityId, out var playerView) &&
                                    playerView != null;
                var hasBoxView = _stateStore.ViewsByEntityId.TryGetValue(track.BoxEntityId, out var boxView) &&
                                 boxView != null;
                if (!hasPlayerView || !hasBoxView)
                {
                    RecordFlipInteractionReset(ResetFlipInteraction(track.PlayerEntityId, track.BoxEntityId));
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                    continue;
                }

                var boxDriver = boxView.GetComponent<BoxFlipInteractionDriver>();
                var handRestWorldPose = new Pose(playerView.transform.position, playerView.transform.rotation);
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

                track.Advance(deltaTime);
                if (track.IsComplete)
                {
                    RecordFlipInteractionReset(ResetFlipInteraction(track.PlayerEntityId, track.BoxEntityId));
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
            _trackState.ClearPlayerTerminalHold(entityId);
            _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
            _stateStore.CommittedFacesByEntityId.Remove(entityId);
            _stateStore.EnemyAiModesByEntityId.Remove(entityId);
            _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
            _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
            _enemySemanticDriversByEntityId.Remove(entityId);
            _stateStore.EntityTypesByEntityId.Remove(entityId);
            _stateStore.LastEnemyApplySignaturesByEntityId.Remove(entityId);
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

        private BoxFlipInteractionResetResult ResetFlipInteraction(int playerEntityId, int boxEntityId)
        {
            if (_stateStore.ViewsByEntityId.TryGetValue(boxEntityId, out var boxView) &&
                boxView != null &&
                boxView.TryGetComponent<BoxFlipInteractionDriver>(out var boxDriver) &&
                boxDriver != null)
            {
                return boxDriver.ResetInteractionForDiagnostics();
            }

            return default;
        }

        private void RecordFlipInteractionReset(BoxFlipInteractionResetResult result)
        {
            _trackState.BoxMotionTelemetry.RecordPoseReset(result);
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

        private bool HasActivePlayerWalkMotion(int entityId, bool isActivePresentationPoseLocomotion)
        {
            if (isActivePresentationPoseLocomotion)
            {
                return true;
            }

            return _trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                   motionTrack.HasClips &&
                   motionTrack.TailMotionKind == TickEntityMotionKind.Move;
        }

        private static bool TryMapBoxMotionSemantic(
            TickEntityMotionKind motionKind,
            out PresentationMotionFactKind semantic)
        {
            switch (motionKind)
            {
                case TickEntityMotionKind.BoxSlide:
                    semantic = PresentationMotionFactKind.BoxSlide;
                    return true;
                case TickEntityMotionKind.Flip:
                    semantic = PresentationMotionFactKind.BoxFlip;
                    return true;
                default:
                    semantic = PresentationMotionFactKind.None;
                    return false;
            }
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
            bool hasPresentationPoseOverride,
            bool hasPlayerDeathHoldPose,
            bool hasActiveLocalMotion,
            bool hasActiveOriginalViewMotion,
            bool hasResolvedJumpAdditiveChannel,
            bool isDeferredExitRetained,
            bool isContactDelayedRetained,
            bool isDeathPresentationPlaying)
        {
            return hasActiveBoardRotationTween ||
                   hasPresentationPoseOverride ||
                   hasPlayerDeathHoldPose ||
                   hasActiveLocalMotion ||
                   hasActiveOriginalViewMotion ||
                   hasResolvedJumpAdditiveChannel ||
                   isDeferredExitRetained ||
                   isContactDelayedRetained ||
                   isDeathPresentationPlaying ||
                   _trackState.PresentationEventTargetEntityIds.Contains(entityId) ||
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
            else if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedState))
            {
                hasAuthoritativeFace = true;
                authoritativeFace = jumpDetachedState.AuthoritativeFace;
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
                facts.HasJumpAirborneVisualState,
                facts.IsTopologyTransitionActive,
                facts.IsJumpTopologySuspended,
                facts.IsOnVisualFrontFace,
                localPose.Position,
                localPose.Rotation,
                semanticState);
            return true;
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
