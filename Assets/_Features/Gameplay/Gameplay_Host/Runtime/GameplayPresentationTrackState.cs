using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayPresentationTrackState
    {
        private readonly List<int> _completedFlipInteractionTrackIds = new();
        private readonly List<int> _completedJumpTrackIds = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedMotionVisualScaleEntityIds = new();
        private readonly List<int> _completedPlayerDeathDisplacementTrackIds = new();
        private readonly List<int> _completedStayFlipImpactTrackIds = new();
        private readonly List<FlipInteractionResetRequest> _flipInteractionResetRequests = new();
        private readonly Dictionary<int, FlipInteractionTrack> _flipInteractionTracks = new();
        private readonly HashSet<FlipImpactInstanceKey> _completedFlipImpactKeys = new();
        private readonly List<int> _completedTransitionVisibilityStateIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly Dictionary<int, JumpTrack> _jumpTracks = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly HashSet<int> _motionVisualScaleEntityIds = new();
        private readonly Dictionary<int, PlayerDeathDisplacementTrack> _playerDeathDisplacementTracks = new();
        private readonly Dictionary<int, FlipImpactTrack> _stayFlipImpactTracks = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _playerLocomotionSignalsByEntityId = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();

        public List<int> CompletedFlipInteractionTrackIds => _completedFlipInteractionTrackIds;

        public List<int> CompletedJumpTrackIds => _completedJumpTrackIds;

        public List<int> CompletedMotionTrackIds => _completedMotionTrackIds;

        public List<int> CompletedMotionVisualScaleEntityIds => _completedMotionVisualScaleEntityIds;

        public List<int> CompletedPlayerDeathDisplacementTrackIds => _completedPlayerDeathDisplacementTrackIds;

        public List<int> CompletedStayFlipImpactTrackIds => _completedStayFlipImpactTrackIds;

        public List<int> CompletedTransitionVisibilityStateIds => _completedTransitionVisibilityStateIds;

        public List<int> CompletedVisibilityTrackIds => _completedVisibilityTrackIds;

        public List<FlipInteractionResetRequest> FlipInteractionResetRequests => _flipInteractionResetRequests;

        public Dictionary<int, FlipInteractionTrack> FlipInteractionTracks => _flipInteractionTracks;

        public HashSet<FlipImpactInstanceKey> CompletedFlipImpactKeys => _completedFlipImpactKeys;

        public Dictionary<int, JumpTrack> JumpTracks => _jumpTracks;

        public Dictionary<int, MotionTrack> LocalMotionTracks => _localMotionTracks;

        public HashSet<int> MotionVisualScaleEntityIds => _motionVisualScaleEntityIds;

        public Dictionary<int, PlayerDeathDisplacementTrack> PlayerDeathDisplacementTracks => _playerDeathDisplacementTracks;

        public Dictionary<int, FlipImpactTrack> StayFlipImpactTracks => _stayFlipImpactTracks;

        public Dictionary<int, TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignalsByEntityId =>
            _playerLocomotionSignalsByEntityId;

        public HashSet<int> VisibleEntityIds => _visibleEntityIds;

        public Dictionary<int, VisibilityTrack> VisibilityTracks => _visibilityTracks;

        public void ResetSession()
        {
            _completedFlipInteractionTrackIds.Clear();
            _completedFlipImpactKeys.Clear();
            _completedJumpTrackIds.Clear();
            _completedMotionTrackIds.Clear();
            _completedMotionVisualScaleEntityIds.Clear();
            _completedPlayerDeathDisplacementTrackIds.Clear();
            _completedStayFlipImpactTrackIds.Clear();
            _flipInteractionResetRequests.Clear();
            _flipInteractionTracks.Clear();
            _completedTransitionVisibilityStateIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _jumpTracks.Clear();
            _localMotionTracks.Clear();
            _motionVisualScaleEntityIds.Clear();
            _playerDeathDisplacementTracks.Clear();
            _stayFlipImpactTracks.Clear();
            _playerLocomotionSignalsByEntityId.Clear();
            _visibleEntityIds.Clear();
            _visibilityTracks.Clear();
        }
    }
}
