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
    internal readonly struct MotionTrackBuildDiagnostic
    {
        public MotionTrackBuildDiagnostic(
            int tickIndex,
            int entityId,
            int operationId,
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            Direction direction,
            bool trackCreated,
            float durationSeconds,
            string reason)
        {
            TickIndex = tickIndex;
            EntityId = entityId;
            OperationId = operationId;
            SourceCell = sourceCell;
            TargetCell = targetCell;
            Direction = direction;
            TrackCreated = trackCreated;
            DurationSeconds = durationSeconds;
            Reason = reason ?? string.Empty;
        }

        public int TickIndex { get; }

        public int EntityId { get; }

        public int OperationId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public bool TrackCreated { get; }

        public float DurationSeconds { get; }

        public string Reason { get; }
    }

    internal readonly struct KinematicTrackBuildDiagnostic
    {
        public KinematicTrackBuildDiagnostic(
            int tickIndex,
            int entityId,
            int operationId,
            bool trackCreated,
            string trackKind,
            SurfaceCell anchorBefore,
            SurfaceCell anchorAfter,
            KinematicOffset2 localOffsetBefore,
            KinematicOffset2 localOffsetAfter,
            KinematicDirectionKind directionKind,
            KinematicFacingPolicy facingPolicy,
            Direction kinematicDirection,
            Direction poseFacing,
            bool shouldUpdateFacing,
            float durationSeconds,
            bool interpolationActive,
            string reason)
        {
            TickIndex = tickIndex;
            EntityId = entityId;
            OperationId = operationId;
            TrackCreated = trackCreated;
            TrackKind = trackKind ?? string.Empty;
            AnchorBefore = anchorBefore;
            AnchorAfter = anchorAfter;
            LocalOffsetBefore = localOffsetBefore;
            LocalOffsetAfter = localOffsetAfter;
            DirectionKind = directionKind;
            FacingPolicy = facingPolicy;
            KinematicDirection = kinematicDirection;
            PoseFacing = poseFacing;
            ShouldUpdateFacing = shouldUpdateFacing;
            DurationSeconds = durationSeconds;
            InterpolationActive = interpolationActive;
            Reason = reason ?? string.Empty;
        }

        public int TickIndex { get; }

        public int EntityId { get; }

        public int OperationId { get; }

        public bool TrackCreated { get; }

        public string TrackKind { get; }

        public SurfaceCell AnchorBefore { get; }

        public SurfaceCell AnchorAfter { get; }

        public KinematicOffset2 LocalOffsetBefore { get; }

        public KinematicOffset2 LocalOffsetAfter { get; }

        public KinematicDirectionKind DirectionKind { get; }

        public KinematicFacingPolicy FacingPolicy { get; }

        public Direction KinematicDirection { get; }

        public Direction PoseFacing { get; }

        public bool ShouldUpdateFacing { get; }

        public float DurationSeconds { get; }

        public bool InterpolationActive { get; }

        public string Reason { get; }
    }

    internal readonly struct KinematicTrackRebaseDiagnostic
    {
        public KinematicTrackRebaseDiagnostic(
            int tickIndex,
            int entityId,
            int operationId,
            SurfaceCell sourceAnchor,
            SurfaceCell destinationAnchor,
            KinematicOffset2 sourceLocalOffset,
            KinematicOffset2 destinationLocalOffset,
            float remainingSeconds,
            Vector3 rebasedSourcePosition,
            Vector3 destinationPosition,
            string reason)
        {
            TickIndex = tickIndex;
            EntityId = entityId;
            OperationId = operationId;
            SourceAnchor = sourceAnchor;
            DestinationAnchor = destinationAnchor;
            SourceLocalOffset = sourceLocalOffset;
            DestinationLocalOffset = destinationLocalOffset;
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
            RebasedSourcePosition = rebasedSourcePosition;
            DestinationPosition = destinationPosition;
            Reason = reason ?? string.Empty;
        }

        public int TickIndex { get; }

        public int EntityId { get; }

        public int OperationId { get; }

        public SurfaceCell SourceAnchor { get; }

        public SurfaceCell DestinationAnchor { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public KinematicOffset2 DestinationLocalOffset { get; }

        public float RemainingSeconds { get; }

        public Vector3 RebasedSourcePosition { get; }

        public Vector3 DestinationPosition { get; }

        public string Reason { get; }
    }

    internal sealed class GameplayTrackPlanner
    {
        internal const float FlipPeakPlayerHeightMultiplier = 1.4f;
        private const float DefaultJumpLandingCompletionDurationSeconds = 0.12f;

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
            _stateStore.LastMotionTrackBuildDiagnostics.Clear();
            _stateStore.LastKinematicTrackBuildDiagnostics.Clear();
            _stateStore.LastKinematicTrackRebaseDiagnostics.Clear();
            RefreshPresentationEventTargets(presentationData);
            var kinematicEntityIds = CollectKinematicEntityIds(presentationData);
            var flipImpactTimingSettings = _motionTimingResolver.ResolveFlipImpactTimingSettings(timingProfile);
            RefreshKinematicTracks(result.TickIndex, presentationData, projector, timingProfile);
            RefreshPlayerDeathHoldTracks(presentationData);
            RefreshMotionClips(
                result.TickIndex,
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
            RefreshJumpWindupRotationTracks(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                projector,
                timingProfile);
            RefreshJumpDetachedVisibilityState(
                result,
                previousCommittedLocalTargetPoses,
                projector,
                timingProfile);
            RefreshPlayerFlipResultTurnTracks(result, projector, timingProfile);
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

        private void RefreshPresentationEventTargets(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var entityIds = _trackState.PresentationEventTargetEntityIds;
            entityIds.Clear();

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EntityMotions[i].EntityId);
            }

            for (var i = 0; i < presentationData.KinematicMotionTracks.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.KinematicMotionTracks[i].EntityId);
            }

            for (var i = 0; i < presentationData.ContinuousLocomotionTracks.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.ContinuousLocomotionTracks[i].EntityId);
            }

            for (var i = 0; i < presentationData.VisibilityChanges.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.VisibilityChanges[i].EntityId);
            }

            for (var i = 0; i < presentationData.TransitionVisibilityChanges.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.TransitionVisibilityChanges[i].EntityId);
            }

            for (var i = 0; i < presentationData.PlayerActionSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionSignals[i];
                AddEventTargetEntityId(entityIds, signal.EntityId);
                AddEventTargetEntityId(entityIds, signal.TargetEntityId);
                AddEventTargetEntityId(entityIds, signal.FlipTargetBoxEntityId);
            }

            for (var i = 0; i < presentationData.PlayerActionAttemptSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionAttemptSignals[i];
                AddEventTargetEntityId(entityIds, signal.EntityId);
                AddEventTargetEntityId(entityIds, signal.TargetEntityId);
            }

            for (var i = 0; i < presentationData.PlayerFlipResultTurnSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.PlayerFlipResultTurnSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.PlayerLocomotionSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.PlayerLocomotionSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.PlayerDamageSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.PlayerDamageSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.PlayerDeathSignals.Count; i++)
            {
                var signal = presentationData.PlayerDeathSignals[i];
                AddEventTargetEntityId(entityIds, signal.EntityId);
                AddEventTargetEntityId(entityIds, signal.SourceEntityId);
            }

            for (var i = 0; i < presentationData.PlayerDeathHoldSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.PlayerDeathHoldSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyDamageSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyDamageSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyActionSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyActionSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyJumpSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyJumpSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyChargeSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyChargeSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyGlideSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyGlideSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyUtilitySignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyUtilitySignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EnemyUtilityCooldownSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.EnemyUtilityCooldownSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                AddEventTargetEntityId(entityIds, signal.ExitedEntityId);
                if (signal.SourceActorEntityId.HasValue)
                {
                    AddEventTargetEntityId(entityIds, signal.SourceActorEntityId.Value);
                }

                if (signal.AnchorEntityId.HasValue)
                {
                    AddEventTargetEntityId(entityIds, signal.AnchorEntityId.Value);
                }
            }

            for (var i = 0; i < presentationData.ImpactTransientSignals.Count; i++)
            {
                AddEventTargetEntityId(entityIds, presentationData.ImpactTransientSignals[i].EntityId);
            }

            for (var i = 0; i < presentationData.FlipImpactSignals.Count; i++)
            {
                var signal = presentationData.FlipImpactSignals[i];
                AddEventTargetEntityId(entityIds, signal.BoxEntityId);
                AddEventTargetEntityId(entityIds, signal.ImpactTargetEntityId);
                AddEventTargetEntityId(entityIds, signal.ActorEntityId);
            }

            for (var i = 0; i < presentationData.FlipFloorImpactSignals.Count; i++)
            {
                var signal = presentationData.FlipFloorImpactSignals[i];
                AddEventTargetEntityId(entityIds, signal.BoxEntityId);
                AddEventTargetEntityId(entityIds, signal.ActorEntityId);
            }

            for (var i = 0; i < presentationData.BoxSlideStopSignals.Count; i++)
            {
                var signal = presentationData.BoxSlideStopSignals[i];
                AddEventTargetEntityId(entityIds, signal.BoxEntityId);
                AddEventTargetEntityId(entityIds, signal.StopperEntityId);
            }

            for (var i = 0; i < presentationData.BoxSlideStartSignals.Count; i++)
            {
                var signal = presentationData.BoxSlideStartSignals[i];
                AddEventTargetEntityId(entityIds, signal.BoxEntityId);
                AddEventTargetEntityId(entityIds, signal.ActorEntityId);
            }

            for (var i = 0; i < presentationData.ForwardCellImpactSignals.Count; i++)
            {
                var signal = presentationData.ForwardCellImpactSignals[i];
                AddEventTargetEntityId(entityIds, signal.OwnerId);
                AddEventTargetEntityId(entityIds, signal.SourceEnemyId);
                AddEventTargetEntityId(entityIds, signal.TargetEntityId);
            }

            for (var i = 0; i < presentationData.ForwardCellProjectileWindupSignals.Count; i++)
            {
                var signal = presentationData.ForwardCellProjectileWindupSignals[i];
                AddEventTargetEntityId(entityIds, signal.OwnerId);
                AddEventTargetEntityId(entityIds, signal.SourceEnemyId);
            }

            for (var i = 0; i < presentationData.ForwardCellProjectileReleaseSignals.Count; i++)
            {
                var signal = presentationData.ForwardCellProjectileReleaseSignals[i];
                AddEventTargetEntityId(entityIds, signal.OwnerId);
                AddEventTargetEntityId(entityIds, signal.SourceEnemyId);
            }

            for (var i = 0; i < presentationData.ForwardCellProjectileClearSignals.Count; i++)
            {
                var signal = presentationData.ForwardCellProjectileClearSignals[i];
                AddEventTargetEntityId(entityIds, signal.OwnerId);
                AddEventTargetEntityId(entityIds, signal.SourceEnemyId);
            }
        }

        private static void AddEventTargetEntityId(HashSet<int> entityIds, int entityId)
        {
            if (entityId > 0)
            {
                entityIds.Add(entityId);
            }
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

        private void RefreshJumpWindupRotationTracks(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            var jumpSignals = result.PresentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (signal.StartedAirborneThisTick ||
                    signal.LandedThisTick ||
                    signal.RetryThisTick)
                {
                    _trackState.JumpWindupRotationTracks.Remove(signal.EntityId);
                    continue;
                }

                if (!signal.StartedWindupThisTick)
                {
                    continue;
                }

                var durationSeconds = ResolveJumpWindupRotationDurationSeconds(signal, timingProfile);
                if (durationSeconds <= 0f ||
                    !TryResolveJumpWindupRotationEndpoints(
                        signal,
                        previousCommittedLocalTargetPoses,
                        previousCommittedTopology,
                        result.FinalTopology,
                        projector,
                        out var startRotation,
                        out var endRotation))
                {
                    _trackState.JumpWindupRotationTracks.Remove(signal.EntityId);
                    continue;
                }

                if (Quaternion.Angle(startRotation, endRotation) <= 0.01f)
                {
                    _trackState.JumpWindupRotationTracks.Remove(signal.EntityId);
                    continue;
                }

                var track = new RotationTrack();
                track.Append(RotationClip.Create(startRotation, endRotation, durationSeconds));
                _trackState.JumpWindupRotationTracks[signal.EntityId] = track;
            }
        }

        private void RefreshPlayerFlipResultTurnTracks(
            TickResult result,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            var signals = result.PresentationData.PlayerFlipResultTurnSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!TryGetFinalEntity(result.FinalEntities, signal.EntityId, out var entity) ||
                    entity.boardPresence != EntityBoardPresence.Occupying ||
                    !_poseResolver.TryResolveLocalPose(
                        projector,
                        signal.EntityId,
                        entity.position,
                        result.FinalTopology,
                        signal.ContactFacing,
                        out var contactPose) ||
                    !_poseResolver.TryResolveLocalPose(
                        projector,
                        signal.EntityId,
                        entity.position,
                        result.FinalTopology,
                        signal.ResultFacing,
                        out var resultPose))
                {
                    _trackState.PlayerFlipResultTurnTracks.Remove(signal.EntityId);
                    continue;
                }

                var durationSeconds = _motionTimingResolver.ResolvePlayerFlipResultTurnDurationSeconds(
                    signal.EntityId,
                    timingProfile);
                var delaySeconds = _motionTimingResolver.ResolvePlayerFlipResultTurnDelaySeconds(
                    signal.EntityId,
                    timingProfile);
                if (durationSeconds <= 0f ||
                    Quaternion.Angle(contactPose.Rotation, resultPose.Rotation) <= 0.01f)
                {
                    _trackState.PlayerFlipResultTurnTracks.Remove(signal.EntityId);
                    continue;
                }

                var track = new RotationTrack();
                if (delaySeconds > 0.0001f)
                {
                    track.Append(RotationClip.Create(contactPose.Rotation, contactPose.Rotation, delaySeconds));
                }

                track.Append(RotationClip.Create(contactPose.Rotation, resultPose.Rotation, durationSeconds));
                _trackState.PlayerFlipResultTurnTracks[signal.EntityId] = track;
            }
        }

        private static float ResolveJumpWindupRotationDurationSeconds(
            TickEnemyJumpPresentationSignal signal,
            GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null ||
                timingProfile.SimulationTicksPerSecond <= 0 ||
                signal.WindupTicks <= 0)
            {
                return 0f;
            }

            return signal.WindupTicks / (float)timingProfile.SimulationTicksPerSecond;
        }

        private bool TryResolveJumpWindupRotationEndpoints(
            TickEnemyJumpPresentationSignal signal,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            CubeTopologyState finalTopology,
            GameplayCubeProjector projector,
            out Quaternion startRotation,
            out Quaternion endRotation)
        {
            if (previousCommittedLocalTargetPoses != null &&
                previousCommittedLocalTargetPoses.TryGetValue(signal.EntityId, out var previousPose))
            {
                startRotation = previousPose.Rotation;
            }
            else if (_poseResolver.TryResolveLocalPose(
                         projector,
                         signal.EntityId,
                         signal.SourceCell,
                         previousCommittedTopology,
                         Direction.Up,
                         out var fallbackStartPose))
            {
                startRotation = fallbackStartPose.Rotation;
            }
            else
            {
                startRotation = default;
                endRotation = default;
                return false;
            }

            var facing = signal.Facing == Direction.None
                ? Direction.Up
                : signal.Facing;
            if (!_poseResolver.TryResolveLocalPose(
                    projector,
                    signal.EntityId,
                    signal.SourceCell,
                    finalTopology,
                    facing,
                    out var endPose))
            {
                endRotation = default;
                return false;
            }

            endRotation = endPose.Rotation;
            return true;
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

        private void RefreshKinematicTracks(
            int tickIndex,
            TickPresentationData presentationData,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
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
                MotionTrack existingKinematicTrack = null;
                _trackState.KinematicMotionTracks.TryGetValue(track.EntityId, out existingKinematicTrack);
                _trackState.LocalMotionTracks.Remove(track.EntityId);
                _stateStore.RetainedLocalTargetPoses.Remove(track.EntityId);
                if (!_poseResolver.TryResolveKinematicLocalPose(
                        projector,
                        track,
                        useDestination: false,
                        out var sourcePose) ||
                    !_poseResolver.TryResolveKinematicLocalPose(
                        projector,
                        track,
                        useDestination: true,
                        out var destinationPose))
                {
                    RecordKinematicTrackBuild(
                        tickIndex,
                        track,
                        trackCreated: false,
                        trackKind: "Kinematic",
                        durationSeconds: 0f,
                        interpolationActive: false,
                        reason: "PoseResolveFailed");
                    continue;
                }

                var shouldRebaseActiveKinematicTrack =
                    ShouldRebaseActiveKinematicTrackOnPresentationBoundary(
                        track,
                        presentationData,
                        existingKinematicTrack);
                var rebasedSourcePose = sourcePose;
                var rebaseRemainingSeconds = 0f;
                var rebaseReason = string.Empty;
                var rebased = shouldRebaseActiveKinematicTrack &&
                              TryResolveKinematicRebaseSourcePose(
                                  track.EntityId,
                                  existingKinematicTrack,
                                  out rebasedSourcePose,
                                  out rebaseRemainingSeconds,
                                  out rebaseReason);
                if (rebased)
                {
                    sourcePose = rebasedSourcePose;
                    RecordKinematicTrackRebase(
                        tickIndex,
                        track,
                        rebaseRemainingSeconds,
                        sourcePose,
                        destinationPose,
                        rebaseReason);
                }

                if (ShouldCreateKinematicInterpolationTrack(sourcePose, destinationPose, track))
                {
                    var durationSeconds = rebased
                        ? rebaseRemainingSeconds
                        : ResolveKinematicTrackDurationSeconds(track, timingProfile);
                    var motionTrack = new MotionTrack();
                    motionTrack.Append(
                        MotionClip.Create(
                            TickEntityMotionKind.Move,
                            sourcePose,
                            destinationPose,
                            durationSeconds,
                            interpolateRotation: true,
                            flipPeakHeightWorld: 0f,
                            operationId: track.OperationId));
                    _trackState.KinematicMotionTracks[track.EntityId] = motionTrack;
                    _trackState.KinematicMotionTrackCarriers[track.EntityId] = track;
                    RecordKinematicTrackBuild(
                        tickIndex,
                        track,
                        trackCreated: true,
                        trackKind: ResolveKinematicTrackKind(track),
                        durationSeconds,
                        interpolationActive: true,
                        reason: "Created");
                    continue;
                }

                _trackState.KinematicMotionTracks.Remove(track.EntityId);
                _trackState.KinematicMotionTrackCarriers.Remove(track.EntityId);
                _trackState.KinematicPoseOverrides[track.EntityId] = new KinematicPresentationPose(
                    destinationPose,
                    track.MotionMode,
                    track.TerminalKind);
                RecordKinematicTrackBuild(
                    tickIndex,
                    track,
                    trackCreated: false,
                    trackKind: ResolveKinematicTrackKind(track),
                    durationSeconds: 0f,
                    interpolationActive: false,
                    reason: "NoVisualDelta");
            }

            for (var i = 0; i < presentationData.ContinuousLocomotionTracks.Count; i++)
            {
                var track = presentationData.ContinuousLocomotionTracks[i];
                _trackState.LocalMotionTracks.Remove(track.EntityId);
                _stateStore.RetainedLocalTargetPoses.Remove(track.EntityId);
                if (!_poseResolver.TryResolveContinuousLocomotionPose(
                        projector,
                        track,
                        useDestination: false,
                        out var sourcePose) ||
                    !_poseResolver.TryResolveContinuousLocomotionPose(
                        projector,
                        track,
                        useDestination: true,
                        out var destinationPose))
                {
                    continue;
                }

                if (ShouldCreateKinematicInterpolationTrack(sourcePose, destinationPose, track.TerminalKind))
                {
                    var durationSeconds = ResolveKinematicTrackDurationSeconds(timingProfile);
                    var motionTrack = new MotionTrack();
                    motionTrack.Append(
                        MotionClip.Create(
                            TickEntityMotionKind.Move,
                            sourcePose,
                            destinationPose,
                            durationSeconds,
                            interpolateRotation: true,
                            flipPeakHeightWorld: 0f));
                    _trackState.KinematicMotionTracks[track.EntityId] = motionTrack;
                    _trackState.KinematicMotionTrackCarriers.Remove(track.EntityId);
                    continue;
                }

                _trackState.KinematicMotionTracks.Remove(track.EntityId);
                _trackState.KinematicMotionTrackCarriers.Remove(track.EntityId);
                _trackState.KinematicPoseOverrides[track.EntityId] = new KinematicPresentationPose(
                    destinationPose,
                    track.Mode == ContinuousLocomotionMode.Moving ||
                    track.Mode == ContinuousLocomotionMode.AlignToAnchor
                        ? MotionMode.Voluntary
                        : MotionMode.Held,
                    track.TerminalKind);
            }
        }

        private static bool ShouldRebaseActiveKinematicTrackOnPresentationBoundary(
            in TickKinematicMotionTrack track,
            TickPresentationData presentationData,
            MotionTrack existingTrack)
        {
            if (track.TerminalKind != TickKinematicMotionTerminalKind.None)
            {
                return false;
            }

            if (existingTrack == null ||
                !existingTrack.HasClips)
            {
                return false;
            }

            // Future: allow rebase for explicit view re-enable / presentation discontinuity
            // once the planner receives a boundary signal before RefreshKinematicTracks.
            return presentationData.TopologyMotion.HasValue;
        }

        private bool TryResolveKinematicRebaseSourcePose(
            int entityId,
            MotionTrack existingTrack,
            out GameplayEntityPose sourcePose,
            out float remainingSeconds,
            out string reason)
        {
            remainingSeconds = existingTrack != null
                ? existingTrack.HeadRemainingSeconds
                : 0f;

            if (_stateStore.PresentedLocalPosesByEntityId.TryGetValue(entityId, out sourcePose))
            {
                reason = "PresentedPose";
                return true;
            }

            if (existingTrack != null &&
                existingTrack.TrySampleCurrentPose(out sourcePose))
            {
                reason = "ActiveTrackSample";
                return true;
            }

            sourcePose = default;
            reason = "MissingCurrentVisualPose";
            return false;
        }

        private static bool ShouldCreateKinematicInterpolationTrack(
            in GameplayEntityPose sourcePose,
            in GameplayEntityPose destinationPose,
            in TickKinematicMotionTrack track)
        {
            return track.TerminalKind == TickKinematicMotionTerminalKind.None &&
                   ShouldCreateKinematicInterpolationTrack(sourcePose, destinationPose, track.TerminalKind);
        }

        private static bool ShouldCreateKinematicInterpolationTrack(
            in GameplayEntityPose sourcePose,
            in GameplayEntityPose destinationPose,
            TickKinematicMotionTerminalKind terminalKind)
        {
            if (terminalKind != TickKinematicMotionTerminalKind.None)
            {
                return false;
            }

            return (destinationPose.Position - sourcePose.Position).sqrMagnitude > 0.000001f ||
                   Quaternion.Angle(sourcePose.Rotation, destinationPose.Rotation) > 0.01f;
        }

        private static float ResolveKinematicTrackDurationSeconds(
            TickKinematicMotionTrack track,
            GameplayTimingProfile timingProfile)
        {
            if (timingProfile != null &&
                track.TotalTicks > 0)
            {
                return Mathf.Max(track.TotalTicks / (float)timingProfile.SimulationTicksPerSecond, 0.0001f);
            }

            return ResolveKinematicTrackDurationSeconds(timingProfile);
        }

        private static float ResolveKinematicTrackDurationSeconds(GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                return GameplayTimingProfile.DefaultMoveMotionDurationSeconds;
            }

            return Mathf.Max(timingProfile.MoveMotionDurationSeconds, 0.0001f);
        }

        private static string ResolveKinematicTrackKind(in TickKinematicMotionTrack track)
        {
            return !track.SourceAnchorCell.Equals(track.DestinationAnchorCell)
                ? "Anchor"
                : "LocalOffset";
        }

        private void RecordKinematicTrackBuild(
            int tickIndex,
            in TickKinematicMotionTrack track,
            bool trackCreated,
            string trackKind,
            float durationSeconds,
            bool interpolationActive,
            string reason)
        {
            var diagnostic = new KinematicTrackBuildDiagnostic(
                tickIndex,
                track.EntityId,
                track.OperationId,
                trackCreated,
                trackKind,
                track.SourceAnchorCell,
                track.DestinationAnchorCell,
                track.SourceLocalOffset,
                track.DestinationLocalOffset,
                track.DirectionKind,
                track.FacingPolicy,
                track.KinematicDirection,
                track.PoseFacing,
                track.ShouldUpdateFacing,
                durationSeconds,
                interpolationActive,
                reason);
            _stateStore.LastKinematicTrackBuildDiagnostics.Add(diagnostic);
        }

        private void RecordKinematicTrackRebase(
            int tickIndex,
            in TickKinematicMotionTrack track,
            float remainingSeconds,
            in GameplayEntityPose sourcePose,
            in GameplayEntityPose destinationPose,
            string reason)
        {
            var diagnostic = new KinematicTrackRebaseDiagnostic(
                tickIndex,
                track.EntityId,
                track.OperationId,
                track.SourceAnchorCell,
                track.DestinationAnchorCell,
                track.SourceLocalOffset,
                track.DestinationLocalOffset,
                remainingSeconds,
                sourcePose.Position,
                destinationPose.Position,
                reason);
            _stateStore.LastKinematicTrackRebaseDiagnostics.Add(diagnostic);
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
            int tickIndex,
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
                    RecordMotionTrackBuild(
                        tickIndex,
                        motion,
                        trackCreated: false,
                        durationSeconds: 0f,
                        "KinematicEntity");
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

                var durationSeconds = ResolveMotionDurationSeconds(presentationData, motion, timingProfile);
                track.Append(
                    MotionClip.Create(
                        motion.MotionKind,
                        startLocalPose,
                        endLocalPose,
                        durationSeconds,
                        IsTopologyTransitionPresentation(presentationData.TopologyMotion),
                        ResolveFlipPeakHeightWorld(
                            presentationData,
                            motion,
                            startLocalPose,
                            endLocalPose,
                            projector,
                            timingProfile),
                        motion.OperationId));
                RecordMotionTrackBuild(
                    tickIndex,
                    motion,
                    trackCreated: true,
                    durationSeconds,
                    "Created");

                if (!_stateStore.CommittedLocalTargetPoses.ContainsKey(motion.EntityId))
                {
                    _stateStore.RetainedLocalTargetPoses[motion.EntityId] = endLocalPose;
                }
            }
        }

        private void RecordMotionTrackBuild(
            int tickIndex,
            in TickEntityMotion motion,
            bool trackCreated,
            float durationSeconds,
            string reason)
        {
            var diagnostic = new MotionTrackBuildDiagnostic(
                tickIndex,
                motion.EntityId,
                motion.OperationId,
                motion.SourceCell,
                motion.DestinationCell,
                motion.Direction,
                trackCreated,
                durationSeconds,
                reason);
            _stateStore.LastMotionTrackBuildDiagnostics.Add(diagnostic);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogBoxActionMotionBuild(motion, diagnostic);
            if (diagnostic.OperationId != 0)
            {
                UnityEngine.Debug.Log(
                    "[MotionTrackBuild]" +
                    $"Tick={diagnostic.TickIndex}" +
                    $"|Entity={diagnostic.EntityId}" +
                    $"|OperationId={diagnostic.OperationId}" +
                    $"|SourceCell={diagnostic.SourceCell}" +
                    $"|TargetCell={diagnostic.TargetCell}" +
                    $"|Direction={diagnostic.Direction}" +
                    $"|TrackCreated={(diagnostic.TrackCreated ? 1 : 0)}" +
                    $"|Duration={diagnostic.DurationSeconds}" +
                    $"|Reason={diagnostic.Reason}");
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogBoxActionMotionBuild(
            in TickEntityMotion motion,
            in MotionTrackBuildDiagnostic diagnostic)
        {
            if (motion.MotionKind == TickEntityMotionKind.BoxSlide)
            {
                UnityEngine.Debug.Log(
                    "[BoxSlideMotion]" +
                    $"Tick={diagnostic.TickIndex}" +
                    $"|OperationId={diagnostic.OperationId}" +
                    $"|BoxEntity={diagnostic.EntityId}" +
                    $"|FromCell={diagnostic.SourceCell}" +
                    $"|ToCell={diagnostic.TargetCell}" +
                    $"|Direction={diagnostic.Direction}" +
                    "|StateTimerTicks=0" +
                    $"|VisualDuration={diagnostic.DurationSeconds}" +
                    $"|DriverInvoked={(diagnostic.TrackCreated ? 1 : 0)}" +
                    $"|Reason={diagnostic.Reason}");
                return;
            }

            if (motion.MotionKind != TickEntityMotionKind.Flip)
            {
                return;
            }

            UnityEngine.Debug.Log(
                "[BoxFlipMotion]" +
                $"Tick={diagnostic.TickIndex}" +
                $"|OperationId={diagnostic.OperationId}" +
                $"|BoxEntity={diagnostic.EntityId}" +
                "|ActorEntity=0" +
                $"|TargetBoxCell={diagnostic.SourceCell}" +
                $"|LandingCell={diagnostic.TargetCell}" +
                $"|FlipDirection={diagnostic.Direction}" +
                $"|FacingBefore={motion.SourceFacing.GetValueOrDefault(Direction.None)}" +
                $"|FacingAfter={motion.DestinationFacing.GetValueOrDefault(Direction.None)}" +
                $"|DriverInvoked={(diagnostic.TrackCreated ? 1 : 0)}" +
                $"|VisualRootOffsetAnimated={(diagnostic.TrackCreated ? 1 : 0)}" +
                $"|VisualRootRotationAnimated={(diagnostic.TrackCreated ? 1 : 0)}" +
                $"|Reason={diagnostic.Reason}");
        }
#endif

        private float ResolveMotionDurationSeconds(
            TickPresentationData presentationData,
            TickEntityMotion motion,
            GameplayTimingProfile timingProfile)
        {
            if (TryResolvePlayerFlipSlamSynchronizedDurationSeconds(
                    presentationData,
                    motion,
                    timingProfile,
                    out var synchronizedDurationSeconds))
            {
                return synchronizedDurationSeconds;
            }

            return _motionTimingResolver.ResolveMotionDurationSeconds(
                motion.EntityId,
                motion.MotionKind,
                timingProfile);
        }

        private bool TryResolvePlayerFlipSlamSynchronizedDurationSeconds(
            TickPresentationData presentationData,
            TickEntityMotion motion,
            GameplayTimingProfile timingProfile,
            out float durationSeconds)
        {
            durationSeconds = 0f;
            if (motion.MotionKind != TickEntityMotionKind.Flip ||
                !TryResolveFlipSourcePlayerEntityId(presentationData, motion.EntityId, out var playerEntityId))
            {
                return false;
            }

            var recoveryDurationSeconds = _motionTimingResolver.ResolvePlayerFlipResultTurnDurationSeconds(
                playerEntityId,
                timingProfile);
            if (recoveryDurationSeconds <= 0f || BoxFlipSlamSampler.SlamEndTime <= 0f)
            {
                return false;
            }

            durationSeconds = recoveryDurationSeconds / BoxFlipSlamSampler.SlamEndTime;
            return durationSeconds > 0f;
        }

        private float ResolveFlipPeakHeightWorld(
            TickPresentationData presentationData,
            TickEntityMotion motion,
            GameplayEntityPose startLocalPose,
            GameplayEntityPose endLocalPose,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            var configuredArcHeightWorld = timingProfile.FlipArcHeightInCells * projector.CellSize;
            if (motion.MotionKind != TickEntityMotionKind.Flip)
            {
                return configuredArcHeightWorld;
            }

            var fallbackHeightWorld = ResolveFallbackFlipPeakHeightWorld(
                configuredArcHeightWorld,
                projector.CellSize);
            if (!BoxFlipSlamSampler.TryResolveLiftAxis(startLocalPose, endLocalPose, out var localLiftAxis) ||
                !TryResolveFlipSourcePlayerView(presentationData, motion.EntityId, out var playerView) ||
                !TryResolvePlayerVisualHeightWorld(playerView, localLiftAxis, out var playerVisualHeightWorld))
            {
                return fallbackHeightWorld;
            }

            return ResolveFlipPeakHeightFromPlayerVisualHeightWorld(playerVisualHeightWorld);
        }

        internal static float ResolveFlipPeakHeightFromPlayerVisualHeightWorld(float playerVisualHeightWorld)
        {
            return Mathf.Max(0f, playerVisualHeightWorld) * FlipPeakPlayerHeightMultiplier;
        }

        internal static float ResolveFallbackFlipPeakHeightWorld(float configuredArcHeightWorld, float cellSize)
        {
            return Mathf.Max(
                Mathf.Max(0f, configuredArcHeightWorld),
                Mathf.Max(0f, cellSize) * FlipPeakPlayerHeightMultiplier);
        }

        internal static bool TryResolvePlayerVisualHeightWorld(
            GameplayEntityView playerView,
            Vector3 localLiftAxis,
            out float heightWorld)
        {
            heightWorld = 0f;
            if (playerView == null || localLiftAxis.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var worldLiftAxis = ResolveWorldLiftAxis(playerView, localLiftAxis);
            if (worldLiftAxis.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var renderers = playerView.ModelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            var minProjection = float.PositiveInfinity;
            var maxProjection = float.NegativeInfinity;
            var hasRenderer = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                ProjectBounds(renderer.bounds, worldLiftAxis, out var rendererMin, out var rendererMax);
                minProjection = Mathf.Min(minProjection, rendererMin);
                maxProjection = Mathf.Max(maxProjection, rendererMax);
                hasRenderer = true;
            }

            if (!hasRenderer || maxProjection < minProjection)
            {
                return false;
            }

            heightWorld = maxProjection - minProjection;
            return heightWorld > 0.0001f;
        }

        private bool TryResolveFlipSourcePlayerView(
            TickPresentationData presentationData,
            int targetBoxEntityId,
            out GameplayEntityView playerView)
        {
            playerView = null;
            if (!TryResolveFlipSourcePlayerEntityId(presentationData, targetBoxEntityId, out var playerEntityId))
            {
                return false;
            }

            if (_stateStore.ViewsByEntityId.TryGetValue(playerEntityId, out playerView) &&
                playerView != null)
            {
                return true;
            }

            playerView = null;
            return false;
        }

        private static bool TryResolveFlipSourcePlayerEntityId(
            TickPresentationData presentationData,
            int targetBoxEntityId,
            out int playerEntityId)
        {
            playerEntityId = 0;
            if (presentationData == null)
            {
                return false;
            }

            for (var i = 0; i < presentationData.PlayerActionSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionSignals[i];
                if (signal.ActiveActionKind != PlayerActionKind.Flip ||
                    (signal.TargetEntityId != targetBoxEntityId &&
                     signal.FlipTargetBoxEntityId != targetBoxEntityId))
                {
                    continue;
                }

                playerEntityId = signal.EntityId;
                return playerEntityId > 0;
            }

            playerEntityId = 0;
            return false;
        }

        private static Vector3 ResolveWorldLiftAxis(GameplayEntityView playerView, Vector3 localLiftAxis)
        {
            var normalizedLocalLiftAxis = localLiftAxis.normalized;
            var parent = playerView.transform.parent;
            return parent != null
                ? parent.TransformDirection(normalizedLocalLiftAxis).normalized
                : normalizedLocalLiftAxis;
        }

        private static void ProjectBounds(
            Bounds bounds,
            Vector3 normalizedAxis,
            out float minProjection,
            out float maxProjection)
        {
            var centerProjection = Vector3.Dot(bounds.center, normalizedAxis);
            var projectionExtent =
                Mathf.Abs(normalizedAxis.x) * bounds.extents.x +
                Mathf.Abs(normalizedAxis.y) * bounds.extents.y +
                Mathf.Abs(normalizedAxis.z) * bounds.extents.z;
            minProjection = centerProjection - projectionExtent;
            maxProjection = centerProjection + projectionExtent;
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
                if (signal.LandedThisTick)
                {
                    _trackState.JumpTopologySuspendedEntityIds.Remove(signal.EntityId);
                    if (TryStartJumpLandingCompletionTrack(
                            signal,
                            previousCommittedLocalTargetPoses,
                            result,
                            projector,
                            timingProfile))
                    {
                        continue;
                    }

                    _entityPresentationApplier.ClearJumpPresentationState(signal.EntityId);
                    continue;
                }

                if (signal.Phase != EnemyJumpPhase.Airborne)
                {
                    _entityPresentationApplier.ClearJumpPresentationState(signal.EntityId);
                    continue;
                }

                activeAirborneEntityIds.Add(signal.EntityId);
                _trackState.VisibilityTracks.Remove(signal.EntityId);
                if (IsJumpTopologySuspended(signal, result.FinalTopology))
                {
                    _trackState.JumpTopologySuspendedEntityIds.Add(signal.EntityId);
                }
                else
                {
                    _trackState.JumpTopologySuspendedEntityIds.Remove(signal.EntityId);
                }

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
                        new JumpDetachedVisibilityState(signal.Phase, localPose, signal.SourceCell);
                    continue;
                }

                if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(signal.EntityId, out var existingState))
                {
                    _stateStore.JumpDetachedVisibilityStates[signal.EntityId] =
                        new JumpDetachedVisibilityState(signal.Phase, existingState.LocalPose, signal.SourceCell);
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

        private static bool IsJumpTopologySuspended(
            TickEnemyJumpPresentationSignal signal,
            CubeTopologyState topology)
        {
            return signal.Phase == EnemyJumpPhase.Airborne &&
                   signal.SourceCell.face != topology.BottomFace;
        }

        private bool TryStartJumpLandingCompletionTrack(
            TickEnemyJumpPresentationSignal signal,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (!ShouldStartJumpLandingCompletionHold(signal, result.PresentationData.TopologyMotion))
            {
                return false;
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

            _trackState.JumpTracks.TryGetValue(signal.EntityId, out var existingTrack);
            var jumpTrack = existingTrack ?? new JumpTrack();
            jumpTrack.Replace(
                JumpClip.Create(
                    startPose,
                    endPose,
                    ResolveJumpLandingCompletionDurationSeconds(timingProfile),
                    arcHeightWorld: 0f));

            _trackState.JumpTracks[signal.EntityId] = jumpTrack;
            _trackState.ActiveAirborneJumpTrackKeys.Remove(signal.EntityId);
            _trackState.JumpLandingCompletionHoldEntityIds.Add(signal.EntityId);
            _trackState.VisibilityTracks.Remove(signal.EntityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(signal.EntityId);
            return true;
        }

        private static bool ShouldStartJumpLandingCompletionHold(
            TickEnemyJumpPresentationSignal signal,
            TickTopologyMotion? topologyMotion)
        {
            if (signal.PresentationTargetCell != signal.LockedTargetCell)
            {
                return true;
            }

            return IsTopologyTransitionPresentation(topologyMotion);
        }

        private static float ResolveJumpLandingCompletionDurationSeconds(GameplayTimingProfile timingProfile)
        {
            return Mathf.Max(
                0.0001f,
                Mathf.Min(DefaultJumpLandingCompletionDurationSeconds, timingProfile.SimulationTickIntervalSeconds));
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
            var key = EnemyAirbornePresentationKey.FromSignal(signal);

            if (!signal.StartedAirborneThisTick &&
                !signal.RetryThisTick &&
                existingTrack != null &&
                existingTrack.HasClip &&
                _stateStore.JumpDetachedVisibilityStates.TryGetValue(signal.EntityId, out var existingState))
            {
                if (!_trackState.ActiveAirborneJumpTrackKeys.TryGetValue(signal.EntityId, out var activeKey) ||
                    activeKey != key)
                {
                    _trackState.ActiveAirborneJumpTrackKeys[signal.EntityId] = key;
                }

                jumpTrack = existingTrack;
                localPose = existingState.LocalPose;
                return true;
            }

            if (!signal.StartedAirborneThisTick &&
                !signal.RetryThisTick &&
                _trackState.CompletedAirborneJumpTrackKeys.Contains(key))
            {
                return false;
            }

            if (!signal.StartedAirborneThisTick &&
                !signal.RetryThisTick &&
                _trackState.ActiveAirborneJumpTrackKeys.TryGetValue(signal.EntityId, out var existingKey) &&
                existingKey == key)
            {
                return false;
            }

            if (!signal.StartedAirborneThisTick &&
                !signal.RetryThisTick &&
                IsJumpTopologySuspended(signal, result.FinalTopology))
            {
                return false;
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
                    _motionTimingResolver.ResolveJumpArcHeightWorld(signal.EntityId, projector),
                    ResolveJumpMotionPresentation(signal.EntityId)));
            _trackState.ActiveAirborneJumpTrackKeys[signal.EntityId] = key;
            _trackState.CompletedAirborneJumpTrackKeys.Remove(key);
            localPose = startPose;
            return true;
        }

        private EnemyJumpMotionPresentationSnapshot? ResolveJumpMotionPresentation(int entityId)
        {
            if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null)
            {
                return null;
            }

            var authoring = EnemyJumpMotionPresentationAuthoring.GetOptionalValidatedAuthoring(view);
            return authoring != null
                ? authoring.CreateSnapshot()
                : null;
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
