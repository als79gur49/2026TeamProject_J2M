using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct KinematicPresentationPose
    {
        public KinematicPresentationPose(
            GameplayEntityPose localPose,
            MotionMode motionMode,
            TickKinematicMotionTerminalKind terminalKind)
        {
            LocalPose = localPose;
            MotionMode = motionMode;
            TerminalKind = terminalKind;
        }

        public GameplayEntityPose LocalPose { get; }

        public MotionMode MotionMode { get; }

        public TickKinematicMotionTerminalKind TerminalKind { get; }

        public bool IsActiveLocomotion =>
            TerminalKind == TickKinematicMotionTerminalKind.None &&
            (MotionMode == MotionMode.Voluntary ||
             MotionMode == MotionMode.Charge);
    }

    internal sealed class GameplayPresentationTrackState
    {
        private readonly List<int> _completedFlipInteractionTrackIds = new();
        private readonly List<int> _completedJumpTrackIds = new();
        private readonly List<int> _completedJumpWindupRotationTrackIds = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedMotionVisualScaleEntityIds = new();
        private readonly List<int> _completedPlayerDeathDisplacementTrackIds = new();
        private readonly List<int> _completedPlayerFlipResultTurnTrackIds = new();
        private readonly List<int> _completedOriginalViewMotionTrackIds = new();
        private readonly List<FlipInteractionResetRequest> _flipInteractionResetRequests = new();
        private readonly Dictionary<int, FlipInteractionTrack> _flipInteractionTracks = new();
        private readonly HashSet<PresentationMotionInstanceKey> _completedPresentationMotionKeys = new();
        private readonly HashSet<int> _contactDelayedRetainedEntityIds = new();
        private readonly HashSet<int> _deathPresentationPlayingEntityIds = new();
        private readonly HashSet<int> _deferredExitRetainedEntityIds = new();
        private readonly HashSet<int> _jumpLandingCompletionHoldEntityIds = new();
        private readonly HashSet<int> _jumpTopologySuspendedEntityIds = new();
        private readonly List<int> _completedTransitionVisibilityStateIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly Dictionary<int, JumpTrack> _jumpTracks = new();
        private readonly Dictionary<int, RotationTrack> _jumpWindupRotationTracks = new();
        private readonly Dictionary<int, RotationTrack> _playerFlipResultTurnTracks = new();
        private readonly Dictionary<int, KinematicPresentationPose> _kinematicPoseOverrides = new();
        private readonly Dictionary<int, Vector3> _glidePresentationOffsetsByEntityId = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly HashSet<int> _motionVisualScaleEntityIds = new();
        private readonly Dictionary<int, GameplayEntityPose> _playerDeathHoldPoses = new();
        private readonly HashSet<int> _playerDeathHoldSignalEntityIds = new();
        private readonly Dictionary<int, PlayerDeathDisplacementTrack> _playerDeathDisplacementTracks = new();
        private readonly Dictionary<int, PresentationMotionTrack> _originalViewMotionTracks = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _playerLocomotionSignalsByEntityId = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();

        public List<int> CompletedFlipInteractionTrackIds => _completedFlipInteractionTrackIds;

        public List<int> CompletedJumpTrackIds => _completedJumpTrackIds;

        public List<int> CompletedJumpWindupRotationTrackIds => _completedJumpWindupRotationTrackIds;

        public List<int> CompletedMotionTrackIds => _completedMotionTrackIds;

        public List<int> CompletedMotionVisualScaleEntityIds => _completedMotionVisualScaleEntityIds;

        public List<int> CompletedPlayerDeathDisplacementTrackIds => _completedPlayerDeathDisplacementTrackIds;

        public List<int> CompletedPlayerFlipResultTurnTrackIds => _completedPlayerFlipResultTurnTrackIds;

        public List<int> CompletedOriginalViewMotionTrackIds => _completedOriginalViewMotionTrackIds;

        public List<int> CompletedTransitionVisibilityStateIds => _completedTransitionVisibilityStateIds;

        public List<int> CompletedVisibilityTrackIds => _completedVisibilityTrackIds;

        public List<FlipInteractionResetRequest> FlipInteractionResetRequests => _flipInteractionResetRequests;

        public Dictionary<int, FlipInteractionTrack> FlipInteractionTracks => _flipInteractionTracks;

        public HashSet<PresentationMotionInstanceKey> CompletedPresentationMotionKeys => _completedPresentationMotionKeys;

        public HashSet<int> ContactDelayedRetainedEntityIds => _contactDelayedRetainedEntityIds;

        public HashSet<int> DeathPresentationPlayingEntityIds => _deathPresentationPlayingEntityIds;

        public HashSet<int> DeferredExitRetainedEntityIds => _deferredExitRetainedEntityIds;

        public HashSet<int> JumpLandingCompletionHoldEntityIds => _jumpLandingCompletionHoldEntityIds;

        public HashSet<int> JumpTopologySuspendedEntityIds => _jumpTopologySuspendedEntityIds;

        public Dictionary<int, JumpTrack> JumpTracks => _jumpTracks;

        public Dictionary<int, RotationTrack> JumpWindupRotationTracks => _jumpWindupRotationTracks;

        public Dictionary<int, RotationTrack> PlayerFlipResultTurnTracks => _playerFlipResultTurnTracks;

        public Dictionary<int, KinematicPresentationPose> KinematicPoseOverrides => _kinematicPoseOverrides;

        public Dictionary<int, Vector3> GlidePresentationOffsetsByEntityId => _glidePresentationOffsetsByEntityId;

        public Dictionary<int, MotionTrack> LocalMotionTracks => _localMotionTracks;

        public HashSet<int> MotionVisualScaleEntityIds => _motionVisualScaleEntityIds;

        public Dictionary<int, GameplayEntityPose> PlayerDeathHoldPoses => _playerDeathHoldPoses;

        public HashSet<int> PlayerDeathHoldSignalEntityIds => _playerDeathHoldSignalEntityIds;

        public Dictionary<int, PlayerDeathDisplacementTrack> PlayerDeathDisplacementTracks => _playerDeathDisplacementTracks;

        public Dictionary<int, PresentationMotionTrack> OriginalViewMotionTracks => _originalViewMotionTracks;

        public Dictionary<int, TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignalsByEntityId =>
            _playerLocomotionSignalsByEntityId;

        public HashSet<int> VisibleEntityIds => _visibleEntityIds;

        public Dictionary<int, VisibilityTrack> VisibilityTracks => _visibilityTracks;

        public void ResetSession()
        {
            _completedFlipInteractionTrackIds.Clear();
            _completedPresentationMotionKeys.Clear();
            _completedJumpTrackIds.Clear();
            _completedJumpWindupRotationTrackIds.Clear();
            _completedMotionTrackIds.Clear();
            _completedMotionVisualScaleEntityIds.Clear();
            _completedPlayerDeathDisplacementTrackIds.Clear();
            _completedPlayerFlipResultTurnTrackIds.Clear();
            _completedOriginalViewMotionTrackIds.Clear();
            _flipInteractionResetRequests.Clear();
            _flipInteractionTracks.Clear();
            _contactDelayedRetainedEntityIds.Clear();
            _deathPresentationPlayingEntityIds.Clear();
            _deferredExitRetainedEntityIds.Clear();
            _jumpLandingCompletionHoldEntityIds.Clear();
            _jumpTopologySuspendedEntityIds.Clear();
            _completedTransitionVisibilityStateIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _jumpTracks.Clear();
            _jumpWindupRotationTracks.Clear();
            _playerFlipResultTurnTracks.Clear();
            _kinematicPoseOverrides.Clear();
            _glidePresentationOffsetsByEntityId.Clear();
            _localMotionTracks.Clear();
            _motionVisualScaleEntityIds.Clear();
            _playerDeathHoldPoses.Clear();
            _playerDeathHoldSignalEntityIds.Clear();
            _playerDeathDisplacementTracks.Clear();
            _originalViewMotionTracks.Clear();
            _playerLocomotionSignalsByEntityId.Clear();
            _visibleEntityIds.Clear();
            _visibilityTracks.Clear();
        }
    }
}
