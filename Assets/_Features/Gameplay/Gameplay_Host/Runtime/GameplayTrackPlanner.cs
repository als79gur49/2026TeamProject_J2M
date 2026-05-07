using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayTrackPlanner
    {
        private readonly GameplayEntityPresentationApplier _entityPresentationApplier;
        private readonly GameplayExitPresentationController _exitPresentationController;
        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private readonly PlayerDeathDisplacementPlanner _playerDeathDisplacementPlanner;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayTrackPlanner(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayExitPresentationController exitPresentationController,
            GameplayEntityPresentationApplier entityPresentationApplier)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _motionTimingResolver = motionTimingResolver ?? throw new ArgumentNullException(nameof(motionTimingResolver));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
            _exitPresentationController =
                exitPresentationController ?? throw new ArgumentNullException(nameof(exitPresentationController));
            _entityPresentationApplier =
                entityPresentationApplier ?? throw new ArgumentNullException(nameof(entityPresentationApplier));
            _playerDeathDisplacementPlanner = new PlayerDeathDisplacementPlanner(
                _stateStore,
                _trackState,
                _poseResolver);
        }

        public void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot)
        {
            _playerDeathDisplacementPlanner.ConfigureOutputCamera(outputCamera, localSpaceRoot);
        }

        public void RefreshPlayerLocomotionSignals(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _trackState.PlayerLocomotionSignalsByEntityId.Clear();
            for (var i = 0; i < presentationData.PlayerLocomotionSignals.Count; i++)
            {
                var signal = presentationData.PlayerLocomotionSignals[i];
                _trackState.PlayerLocomotionSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        public void RefreshTracks(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            var presentationData = result.PresentationData;
            var kinematicEntityIds = CollectKinematicEntityIds(presentationData);
            var flipImpactTimingSettings = _motionTimingResolver.ResolveFlipImpactTimingSettings(timingProfile);
            RefreshKinematicTracks(presentationData, projector);
            RefreshPlayerDeathHoldTracks(presentationData);
            RefreshMotionClips(
                presentationData,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                projector,
                timingProfile,
                kinematicEntityIds);
            RefreshOriginalViewMotionTracks(
                result,
                projector,
                timingProfile);
            RefreshJumpDetachedVisibilityState(
                result,
                previousCommittedLocalTargetPoses,
                projector,
                timingProfile);
            RefreshGlidePresentationOffsets(presentationData, projector);
            RefreshVisibilityTracks(
                presentationData,
                previousCommittedLocalTargetPoses,
                projector,
                timingProfile);
            RefreshTransitionVisibilityState(presentationData, projector);
            RefreshFlipInteractionTracks(presentationData, timingProfile, flipImpactTimingSettings);
            _playerDeathDisplacementPlanner.RefreshTracks(presentationData, projector, timingProfile);
        }

        private void RefreshGlidePresentationOffsets(
            TickPresentationData presentationData,
            GameplayCubeProjector projector)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            _trackState.GlidePresentationOffsetsByEntityId.Clear();
            var glideSignals = presentationData.EnemyGlideSignals;
            for (var i = 0; i < glideSignals.Count; i++)
            {
                var signal = glideSignals[i];
                if (signal.IsTerminalZero || signal.CurrentHeightUnits == 0)
                {
                    continue;
                }

                if (_poseResolver.TryResolveGlidePresentationOffset(
                        projector,
                        signal,
                        _stateStore.CommittedTopology,
                        out var offset))
                {
                    _trackState.GlidePresentationOffsetsByEntityId[signal.EntityId] = offset;
                }
            }
        }

        private HashSet<int> CollectKinematicEntityIds(TickPresentationData presentationData)
        {
            var entityIds = new HashSet<int>();
            if (presentationData == null)
            {
                return entityIds;
            }

            for (var i = 0; i < presentationData.KinematicMotionTracks.Count; i++)
            {
                entityIds.Add(presentationData.KinematicMotionTracks[i].EntityId);
            }

            for (var i = 0; i < presentationData.ContinuousLocomotionTracks.Count; i++)
            {
                entityIds.Add(presentationData.ContinuousLocomotionTracks[i].EntityId);
            }

            return entityIds;
        }

        private void RefreshKinematicTracks(TickPresentationData presentationData, GameplayCubeProjector projector)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            _trackState.KinematicPoseOverrides.Clear();
            for (var i = 0; i < presentationData.KinematicMotionTracks.Count; i++)
            {
                var track = presentationData.KinematicMotionTracks[i];
                _trackState.LocalMotionTracks.Remove(track.EntityId);
                _stateStore.RetainedLocalTargetPoses.Remove(track.EntityId);
                if (!_poseResolver.TryResolveKinematicLocalPose(
                        projector,
                        track,
                        useDestination: true,
                        out var localPose))
                {
                    continue;
                }

                _trackState.KinematicPoseOverrides[track.EntityId] = new KinematicPresentationPose(
                    localPose,
                    track.MotionMode,
                    track.TerminalKind);
            }

            for (var i = 0; i < presentationData.ContinuousLocomotionTracks.Count; i++)
            {
                var track = presentationData.ContinuousLocomotionTracks[i];
                _trackState.LocalMotionTracks.Remove(track.EntityId);
                _stateStore.RetainedLocalTargetPoses.Remove(track.EntityId);
                if (!_poseResolver.TryResolveContinuousLocomotionPose(
                        projector,
                        track,
                        useDestination: true,
                        out var localPose))
                {
                    continue;
                }

                _trackState.KinematicPoseOverrides[track.EntityId] = new KinematicPresentationPose(
                    localPose,
                    track.Mode == ContinuousLocomotionMode.Moving ||
                    track.Mode == ContinuousLocomotionMode.AlignToAnchor
                        ? MotionMode.Voluntary
                        : MotionMode.Held,
                    track.TerminalKind);
            }
        }

        private void RefreshPlayerDeathHoldTracks(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _trackState.PlayerDeathHoldSignalEntityIds.Clear();
            for (var i = 0; i < presentationData.PlayerDeathHoldSignals.Count; i++)
            {
                var signal = presentationData.PlayerDeathHoldSignals[i];
                _trackState.PlayerDeathHoldSignalEntityIds.Add(signal.EntityId);

                if (_trackState.KinematicPoseOverrides.TryGetValue(signal.EntityId, out var kinematicPose))
                {
                    _trackState.PlayerDeathHoldPoses[signal.EntityId] = kinematicPose.LocalPose;
                    continue;
                }

                if (_trackState.PlayerDeathHoldPoses.ContainsKey(signal.EntityId))
                {
                    continue;
                }

                if (_stateStore.PresentedLocalPosesByEntityId.TryGetValue(signal.EntityId, out var presentedPose))
                {
                    _trackState.PlayerDeathHoldPoses[signal.EntityId] = presentedPose;
                    continue;
                }

                if (_stateStore.RetainedLocalTargetPoses.TryGetValue(signal.EntityId, out var retainedPose))
                {
                    _trackState.PlayerDeathHoldPoses[signal.EntityId] = retainedPose;
                    continue;
                }

                if (_stateStore.CommittedLocalTargetPoses.TryGetValue(signal.EntityId, out var committedPose))
                {
                    _trackState.PlayerDeathHoldPoses[signal.EntityId] = committedPose;
                }
            }

            _trackState.CompletedMotionTrackIds.Clear();
            foreach (var pair in _trackState.PlayerDeathHoldPoses)
            {
                if (!_trackState.PlayerDeathHoldSignalEntityIds.Contains(pair.Key))
                {
                    _trackState.CompletedMotionTrackIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _trackState.CompletedMotionTrackIds.Count; i++)
            {
                var entityId = _trackState.CompletedMotionTrackIds[i];
                _trackState.PlayerDeathHoldPoses.Remove(entityId);
                _trackState.VisibilityTracks.Remove(entityId);
                _stateStore.RetainedLocalTargetPoses.Remove(entityId);
            }
        }

        private void RefreshMotionClips(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile,
            ISet<int> kinematicEntityIds)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var motionEntityIds = new HashSet<int>();
            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var entityId = presentationData.EntityMotions[i].EntityId;
                if (kinematicEntityIds == null ||
                    !kinematicEntityIds.Contains(entityId))
                {
                    motionEntityIds.Add(entityId);
                }
            }

            _trackState.CompletedMotionTrackIds.Clear();
            foreach (var pair in _trackState.LocalMotionTracks)
            {
                if (kinematicEntityIds != null &&
                    kinematicEntityIds.Contains(pair.Key))
                {
                    _trackState.CompletedMotionTrackIds.Add(pair.Key);
                    continue;
                }

                if (motionEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                if (_poseResolver.TryResolveFallbackLocalPose(pair.Key, out var targetLocalPose))
                {
                    pair.Value.AlignToCommittedTargetPose(targetLocalPose);
                    continue;
                }

                _trackState.CompletedMotionTrackIds.Add(pair.Key);
            }

            for (var i = 0; i < _trackState.CompletedMotionTrackIds.Count; i++)
            {
                _trackState.LocalMotionTracks.Remove(_trackState.CompletedMotionTrackIds[i]);
            }

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var motion = presentationData.EntityMotions[i];
                if (kinematicEntityIds != null &&
                    kinematicEntityIds.Contains(motion.EntityId))
                {
                    continue;
                }

                var endLocalPose = ResolveMotionEndPose(motion, presentationData.TopologyMotion, projector);
                var startLocalPose = ResolveMotionStartPose(
                    motion,
                    previousCommittedLocalTargetPoses,
                    previousCommittedTopology,
                    presentationData.TopologyMotion,
                    endLocalPose,
                    projector);

                if (_trackState.LocalMotionTracks.TryGetValue(motion.EntityId, out var existingTrack) &&
                    existingTrack.HasClips)
                {
                    if (existingTrack.TailMotionKind == TickEntityMotionKind.Flip &&
                        motion.MotionKind != TickEntityMotionKind.Flip)
                    {
                        startLocalPose = existingTrack.TailEndPose;
                        existingTrack.Clear();
                    }
                    else
                    {
                        startLocalPose = existingTrack.TailEndPose;
                    }
                }

                if (!_trackState.LocalMotionTracks.TryGetValue(motion.EntityId, out var track))
                {
                    track = new MotionTrack();
                    _trackState.LocalMotionTracks[motion.EntityId] = track;
                }

                track.Append(
                    MotionClip.Create(
                        motion.MotionKind,
                        startLocalPose,
                        endLocalPose,
                        _motionTimingResolver.ResolveMotionDurationSeconds(
                            motion.EntityId,
                            motion.MotionKind,
                            timingProfile),
                        IsTopologyTransitionPresentation(presentationData.TopologyMotion),
                        timingProfile.FlipArcHeightInCells * projector.CellSize));

                if (!_stateStore.CommittedLocalTargetPoses.ContainsKey(motion.EntityId))
                {
                    _stateStore.RetainedLocalTargetPoses[motion.EntityId] = endLocalPose;
                }
            }
        }

        private void RefreshTransitionVisibilityState(
            TickPresentationData presentationData,
            GameplayCubeProjector projector)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _stateStore.TransitionVisibilityStates.Clear();

            if (!IsTopologyTransitionPresentation(presentationData.TopologyMotion))
            {
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            for (var i = 0; i < presentationData.TransitionVisibilityChanges.Count; i++)
            {
                var change = presentationData.TransitionVisibilityChanges[i];
                // Topology transition presentation now treats destination topology as the only authoritative
                // visible set. Runtime transition cache is reserved for destination-only shows.
                if (change.Mode != TickTransitionVisibilityMode.ShowAtTransitionStart ||
                    _exitPresentationController.IsExitOwned(change.EntityId) ||
                    !_poseResolver.TryResolveTransitionLocalPose(
                        projector,
                        change.EntityId,
                        change.Cell,
                        change.Topology,
                        topologyMotion.DestinationTopology,
                        change.Facing,
                        out var localPose))
                {
                    continue;
                }

                var projectedSlot = projector.TryGetProjectedTransitionEntitySlot(
                    change.Cell,
                    change.Topology,
                    topologyMotion.DestinationTopology,
                    out var resolvedProjectedSlot)
                    // Transition visibility keeps the physical face slot while topology only gates visibility.
                    ? (GameplayProjectedFaceSlot?)resolvedProjectedSlot
                    : null;
                _stateStore.TransitionVisibilityStates[change.EntityId] = new TransitionVisibilityState(
                    change.Mode,
                    localPose,
                    projectedSlot,
                    change.Cell.face);
            }
        }

        private void RefreshVisibilityTracks(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var highestPriorityChanges = new Dictionary<int, TickVisibilityChange>();
            for (var i = 0; i < presentationData.VisibilityChanges.Count; i++)
            {
                var change = presentationData.VisibilityChanges[i];
                if (!highestPriorityChanges.TryGetValue(change.EntityId, out var existingChange) ||
                    GetVisibilityPriority(change.ChangeKind) > GetVisibilityPriority(existingChange.ChangeKind))
                {
                    highestPriorityChanges[change.EntityId] = change;
                }
            }

            foreach (var pair in highestPriorityChanges)
            {
                var entityId = pair.Key;
                var change = pair.Value;
                if (_exitPresentationController.IsExitOwned(entityId))
                {
                    continue;
                }

                if (_stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId))
                {
                    _trackState.VisibilityTracks.Remove(entityId);
                    continue;
                }

                if (change.ChangeKind == TickVisibilityChangeKind.Spawn)
                {
                    _stateStore.RetainedLocalTargetPoses.Remove(entityId);
                    _trackState.VisibilityTracks[entityId] = VisibilityTrack.CreateShow();
                    continue;
                }

                if (!_poseResolver.TryResolveVisibilityLocalPose(
                        projector,
                        change,
                        previousCommittedLocalTargetPoses,
                        presentationData.TopologyMotion,
                        out var retainedLocalPose))
                {
                    continue;
                }

                _stateStore.RetainedLocalTargetPoses[entityId] = retainedLocalPose;
                _trackState.VisibilityTracks[entityId] = VisibilityTrack.CreateHide(
                    _motionTimingResolver.ResolveVisibilityDurationSeconds(entityId, change.ChangeKind, timingProfile));
            }
        }

        private void RefreshJumpDetachedVisibilityState(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (previousCommittedLocalTargetPoses == null)
            {
                throw new ArgumentNullException(nameof(previousCommittedLocalTargetPoses));
            }

            var activeAirborneEntityIds = new HashSet<int>();
            var jumpSignals = result.PresentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (signal.Phase != EnemyJumpPhase.Airborne || signal.LandedThisTick)
                {
                    _entityPresentationApplier.ClearJumpPresentationState(signal.EntityId);
                    continue;
                }

                activeAirborneEntityIds.Add(signal.EntityId);
                _trackState.VisibilityTracks.Remove(signal.EntityId);

                if (TryResolveJumpTrack(
                        signal,
                        previousCommittedLocalTargetPoses,
                        result,
                        projector,
                        timingProfile,
                        out var jumpTrack,
                        out var localPose))
                {
                    _trackState.JumpTracks[signal.EntityId] = jumpTrack;
                    _stateStore.JumpDetachedVisibilityStates[signal.EntityId] =
                        new JumpDetachedVisibilityState(signal.Phase, localPose);
                    continue;
                }

                if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(signal.EntityId, out var existingState))
                {
                    _stateStore.JumpDetachedVisibilityStates[signal.EntityId] =
                        new JumpDetachedVisibilityState(signal.Phase, existingState.LocalPose);
                }
            }

            _trackState.CompletedTransitionVisibilityStateIds.Clear();
            foreach (var pair in _stateStore.JumpDetachedVisibilityStates)
            {
                if (!activeAirborneEntityIds.Contains(pair.Key))
                {
                    _trackState.CompletedTransitionVisibilityStateIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _trackState.CompletedTransitionVisibilityStateIds.Count; i++)
            {
                _entityPresentationApplier.ClearJumpPresentationState(_trackState.CompletedTransitionVisibilityStateIds[i]);
            }

            var entityExitSignals = result.PresentationData.EntityExitSignals;
            for (var i = 0; i < entityExitSignals.Count; i++)
            {
                _entityPresentationApplier.ClearJumpPresentationState(entityExitSignals[i].ExitedEntityId);
            }

            var visibilityChanges = result.PresentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                if (visibilityChanges[i].ChangeKind != TickVisibilityChangeKind.Remove)
                {
                    continue;
                }

                _entityPresentationApplier.ClearJumpPresentationState(visibilityChanges[i].EntityId);
            }
        }

        private GameplayEntityPose ResolveMotionEndPose(
            TickEntityMotion motion,
            TickTopologyMotion? topologyMotion,
            GameplayCubeProjector projector)
        {
            if (_stateStore.CommittedLocalTargetPoses.TryGetValue(motion.EntityId, out var committedPose))
            {
                return committedPose;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(motion.EntityId, out var retainedPose))
            {
                return retainedPose;
            }

            var destinationTopology = motion.DestinationTopology ?? _stateStore.CommittedTopology;
            var destinationFacing = motion.DestinationFacing ?? motion.SourceFacing ?? Direction.Up;
            var sourceTopology = motion.SourceTopology ?? destinationTopology;
            if (IsTopologyTransitionPresentation(topologyMotion) &&
                _poseResolver.TryResolveTransitionLocalPose(
                    projector,
                    motion.EntityId,
                    motion.DestinationCell,
                    sourceTopology,
                    destinationTopology,
                    destinationFacing,
                    out var transitionEndPose))
            {
                return transitionEndPose;
            }

            return _poseResolver.TryResolveLocalPose(
                projector,
                motion.EntityId,
                motion.DestinationCell,
                destinationTopology,
                destinationFacing,
                out var destinationPose)
                ? destinationPose
                : default;
        }

        private GameplayEntityPose ResolveMotionStartPose(
            TickEntityMotion motion,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            TickTopologyMotion? topologyMotion,
            GameplayEntityPose fallbackPose,
            GameplayCubeProjector projector)
        {
            if (_trackState.LocalMotionTracks.TryGetValue(motion.EntityId, out var track) &&
                track.HasClips)
            {
                return track.TailEndPose;
            }

            var sourceTopology = motion.SourceTopology ?? previousCommittedTopology;
            var destinationTopology = motion.DestinationTopology ?? _stateStore.CommittedTopology;
            var sourceFacing = motion.SourceFacing ?? motion.DestinationFacing ?? Direction.Up;
            if (IsTopologyTransitionPresentation(topologyMotion) &&
                _poseResolver.TryResolveTransitionLocalPose(
                    projector,
                    motion.EntityId,
                    motion.SourceCell,
                    sourceTopology,
                    destinationTopology,
                    sourceFacing,
                    out var transitionStartPose))
            {
                return transitionStartPose;
            }

            if (previousCommittedLocalTargetPoses.TryGetValue(motion.EntityId, out var previousCommittedPose))
            {
                return previousCommittedPose;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(motion.EntityId, out var retainedPose))
            {
                return retainedPose;
            }

            return _poseResolver.TryResolveLocalPose(
                projector,
                motion.EntityId,
                motion.SourceCell,
                sourceTopology,
                sourceFacing,
                out var sourcePose)
                ? sourcePose
                : fallbackPose;
        }

        private bool TryResolveJumpDetachedLocalPose(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            GameplayCubeProjector projector,
            out GameplayEntityPose localPose)
        {
            if (previousCommittedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var existingState))
            {
                localPose = existingState.LocalPose;
                return true;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            var visibilityChanges = result.PresentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.EntityId != entityId ||
                    change.ChangeKind != TickVisibilityChangeKind.Detach)
                {
                    continue;
                }

                return _poseResolver.TryResolveVisibilityLocalPose(
                    projector,
                    change,
                    previousCommittedLocalTargetPoses,
                    result.PresentationData.TopologyMotion,
                    out localPose);
            }

            var finalEntities = result.FinalEntities;
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (entity.entityId != entityId)
                {
                    continue;
                }

                return _poseResolver.TryResolveLocalPose(
                    projector,
                    entityId,
                    entity.position,
                    result.FinalTopology,
                    entity.facing,
                    out localPose);
            }

            localPose = default;
            return false;
        }

        private bool TryResolveJumpTrack(
            TickEnemyJumpPresentationSignal signal,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile,
            out JumpTrack jumpTrack,
            out GameplayEntityPose localPose)
        {
            jumpTrack = null;
            localPose = default;
            _trackState.JumpTracks.TryGetValue(signal.EntityId, out var existingTrack);

            if (!signal.StartedAirborneThisTick &&
                !signal.RetryThisTick &&
                existingTrack != null &&
                existingTrack.HasClip &&
                _stateStore.JumpDetachedVisibilityStates.TryGetValue(signal.EntityId, out var existingState))
            {
                jumpTrack = existingTrack;
                localPose = existingState.LocalPose;
                return true;
            }

            if (!TryResolveJumpTrackStartPose(
                    signal.EntityId,
                    previousCommittedLocalTargetPoses,
                    result,
                    projector,
                    out var startPose) ||
                !TryResolveJumpTrackEndPose(signal, projector, out var endPose))
            {
                return false;
            }

            jumpTrack = existingTrack ?? new JumpTrack();
            jumpTrack.Replace(
                JumpClip.Create(
                    startPose,
                    endPose,
                    _motionTimingResolver.ResolveJumpDurationSeconds(signal, timingProfile),
                    _motionTimingResolver.ResolveJumpArcHeightWorld(signal.EntityId, projector)));
            localPose = startPose;
            return true;
        }

        private bool TryResolveJumpTrackStartPose(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            GameplayCubeProjector projector,
            out GameplayEntityPose localPose)
        {
            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var existingState))
            {
                localPose = existingState.LocalPose;
                return true;
            }

            if (_trackState.JumpTracks.TryGetValue(entityId, out var existingTrack) &&
                existingTrack.HasClip)
            {
                localPose = existingTrack.CurrentPose;
                return true;
            }

            return TryResolveJumpDetachedLocalPose(
                entityId,
                previousCommittedLocalTargetPoses,
                result,
                projector,
                out localPose);
        }

        private bool TryResolveJumpTrackEndPose(
            TickEnemyJumpPresentationSignal signal,
            GameplayCubeProjector projector,
            out GameplayEntityPose localPose)
        {
            return _poseResolver.TryResolveLocalPose(
                projector,
                signal.EntityId,
                signal.PresentationTargetCell,
                _stateStore.CommittedTopology,
                signal.Facing == Direction.None ? Direction.Up : signal.Facing,
                out localPose);
        }

        private static int GetVisibilityPriority(TickVisibilityChangeKind changeKind)
        {
            return changeKind switch
            {
                TickVisibilityChangeKind.Remove => 3,
                TickVisibilityChangeKind.Detach => 2,
                TickVisibilityChangeKind.Spawn => 1,
                _ => 0,
            };
        }

        private static bool IsTopologyTransitionPresentation(TickTopologyMotion? topologyMotion)
        {
            return topologyMotion.HasValue &&
                   topologyMotion.Value.RotationKind != CubeRotationKind.None;
        }

        private void RefreshOriginalViewMotionTracks(
            TickResult result,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var motionEntityIds = new HashSet<int>();
            var motions = result.PresentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                motionEntityIds.Add(motions[i].EntityId);
            }

            _trackState.CompletedOriginalViewMotionTrackIds.Clear();
            foreach (var pair in _trackState.OriginalViewMotionTracks)
            {
                var entityId = pair.Key;
                var track = pair.Value;
                if (track == null ||
                    track.IsComplete ||
                    motionEntityIds.Contains(entityId) ||
                    !TryGetFinalEntity(result.FinalEntities, entityId, out var entity) ||
                    entity.boardPresence != EntityBoardPresence.Occupying ||
                    !track.HasRequiredFinalCell ||
                    entity.position != track.RequiredFinalCell)
                {
                    _trackState.CompletedOriginalViewMotionTrackIds.Add(entityId);
                    if (track != null)
                    {
                        _trackState.CompletedPresentationMotionKeys.Add(track.InstanceKey);
                    }
                }
            }

            for (var i = 0; i < _trackState.CompletedOriginalViewMotionTrackIds.Count; i++)
            {
                _trackState.OriginalViewMotionTracks.Remove(_trackState.CompletedOriginalViewMotionTrackIds[i]);
            }

            var flipImpactSignals = result.PresentationData.FlipImpactSignals;
            for (var i = 0; i < flipImpactSignals.Count; i++)
            {
                var signal = flipImpactSignals[i];
                if (signal.Disposition != FlipImpactPresentationDisposition.Stay)
                {
                    continue;
                }

                var key = PresentationMotionInstanceKey.CreateFlipImpactStay(signal, result.TickIndex);
                if (_trackState.CompletedPresentationMotionKeys.Contains(key))
                {
                    continue;
                }

                if (_trackState.OriginalViewMotionTracks.TryGetValue(signal.BoxEntityId, out var existingTrack))
                {
                    if (existingTrack.InstanceKey.Equals(key))
                    {
                        continue;
                    }

                    _trackState.OriginalViewMotionTracks.Remove(signal.BoxEntityId);
                }

                if (!TryGetFinalEntity(result.FinalEntities, signal.BoxEntityId, out var finalEntity) ||
                    finalEntity.boardPresence != EntityBoardPresence.Occupying ||
                    finalEntity.position != signal.SourceCell ||
                    motionEntityIds.Contains(signal.BoxEntityId))
                {
                    continue;
                }

                if (!FlipImpactStayMotionCommandBuilder.TryBuild(
                        signal,
                        result.TickIndex,
                        timingProfile,
                        _motionTimingResolver,
                        _poseResolver,
                        projector,
                        out var command))
                {
                    continue;
                }

                _trackState.OriginalViewMotionTracks[signal.BoxEntityId] =
                    PresentationMotionTrack.CreateFlipImpactStay(command);
            }
        }

        private void RefreshFlipInteractionTracks(
            TickPresentationData presentationData,
            GameplayTimingProfile timingProfile,
            FlipImpactTimingSettings flipImpactTimingSettings)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            var signalsByEntityId = new Dictionary<int, TickPlayerActionPresentationSignal>();
            var playerActionSignals = presentationData.PlayerActionSignals;
            for (var i = 0; i < playerActionSignals.Count; i++)
            {
                var signal = playerActionSignals[i];
                signalsByEntityId[signal.EntityId] = signal;
            }

            _trackState.CompletedFlipInteractionTrackIds.Clear();
            foreach (var pair in _trackState.FlipInteractionTracks)
            {
                var track = pair.Value;
                if (!HasInteractionView(track.PlayerEntityId) ||
                    !HasInteractionView(track.BoxEntityId))
                {
                    QueueFlipInteractionReset(track);
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                    continue;
                }

                if (!signalsByEntityId.TryGetValue(pair.Key, out var signal))
                {
                    continue;
                }

                if (signal.CompletedThisTick ||
                    signal.CanceledThisTick)
                {
                    QueueFlipInteractionReset(track);
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _trackState.CompletedFlipInteractionTrackIds.Count; i++)
            {
                _trackState.FlipInteractionTracks.Remove(_trackState.CompletedFlipInteractionTrackIds[i]);
            }

            for (var i = 0; i < playerActionSignals.Count; i++)
            {
                var signal = playerActionSignals[i];
                if (signal.ActiveActionKind != PlayerActionKind.Flip &&
                    !signal.CompletedThisTick &&
                    !signal.CanceledThisTick)
                {
                    continue;
                }

                if (signal.ActiveActionKind == PlayerActionKind.Flip &&
                    signal.StartedThisTick &&
                    signal.TargetEntityId > 0)
                {
                    ReplaceFlipInteractionTrack(signal, timingProfile, flipImpactTimingSettings);
                }

                if (!_trackState.FlipInteractionTracks.TryGetValue(signal.EntityId, out var track))
                {
                    continue;
                }

                track.UpdateFlipOutcome(signal.FlipOutcome, flipImpactTimingSettings);

                if (signal.ExecutedThisTick)
                {
                    track.SetPhase(FlipInteractionPhase.AirborneFollow);
                    continue;
                }

                if (signal.IsRecoveryPhase &&
                    track.Phase == FlipInteractionPhase.AirborneFollow)
                {
                    track.SetPhase(FlipInteractionPhase.Recovery);
                }
            }
        }

        private void ReplaceFlipInteractionTrack(
            in TickPlayerActionPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            FlipImpactTimingSettings flipImpactTimingSettings)
        {
            if (_trackState.FlipInteractionTracks.TryGetValue(signal.EntityId, out var existingTrack))
            {
                QueueFlipInteractionReset(existingTrack);
            }

            var track = new FlipInteractionTrack(
                signal.EntityId,
                signal.TargetEntityId,
                signal.ActiveActionSequence,
                signal.Direction,
                _motionTimingResolver.ResolvePlayerPresentationPhaseDurationSeconds(
                    signal.EntityId,
                    PlayerPresentationPhase.FlipWindup,
                    timingProfile),
                _motionTimingResolver.ResolveMotionDurationSeconds(
                    signal.TargetEntityId,
                    TickEntityMotionKind.Flip,
                    timingProfile),
                _motionTimingResolver.ResolvePlayerPresentationPhaseDurationSeconds(
                    signal.EntityId,
                    PlayerPresentationPhase.FlipRecovery,
                    timingProfile),
                flipImpactTimingSettings,
                signal.FlipOutcome,
                signal.ExecutedThisTick
                    ? FlipInteractionPhase.AirborneFollow
                    : FlipInteractionPhase.Windup);
            _trackState.FlipInteractionTracks[signal.EntityId] = track;
        }

        private bool HasInteractionView(int entityId)
        {
            return _stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                   view != null;
        }

        private void QueueFlipInteractionReset(FlipInteractionTrack track)
        {
            _trackState.FlipInteractionResetRequests.Add(
                new FlipInteractionResetRequest(track.PlayerEntityId, track.BoxEntityId));
        }

        private static bool TryGetFinalEntity(
            IReadOnlyList<EntityState> finalEntities,
            int entityId,
            out EntityState entity)
        {
            for (var i = 0; i < finalEntities.Count; i++)
            {
                if (finalEntities[i].entityId == entityId)
                {
                    entity = finalEntities[i];
                    return true;
                }
            }

            entity = default;
            return false;
        }
    }
}
