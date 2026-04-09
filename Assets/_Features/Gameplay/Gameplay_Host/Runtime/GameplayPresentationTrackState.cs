using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayPresentationTrackState
    {
        private readonly List<int> _completedJumpTrackIds = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedTransitionVisibilityStateIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly Dictionary<int, JumpTrack> _jumpTracks = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _playerLocomotionSignalsByEntityId = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();

        public List<int> CompletedJumpTrackIds => _completedJumpTrackIds;

        public List<int> CompletedMotionTrackIds => _completedMotionTrackIds;

        public List<int> CompletedTransitionVisibilityStateIds => _completedTransitionVisibilityStateIds;

        public List<int> CompletedVisibilityTrackIds => _completedVisibilityTrackIds;

        public Dictionary<int, JumpTrack> JumpTracks => _jumpTracks;

        public Dictionary<int, MotionTrack> LocalMotionTracks => _localMotionTracks;

        public Dictionary<int, TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignalsByEntityId =>
            _playerLocomotionSignalsByEntityId;

        public HashSet<int> VisibleEntityIds => _visibleEntityIds;

        public Dictionary<int, VisibilityTrack> VisibilityTracks => _visibilityTracks;

        public void ResetSession()
        {
            _completedJumpTrackIds.Clear();
            _completedMotionTrackIds.Clear();
            _completedTransitionVisibilityStateIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _jumpTracks.Clear();
            _localMotionTracks.Clear();
            _playerLocomotionSignalsByEntityId.Clear();
            _visibleEntityIds.Clear();
            _visibilityTracks.Clear();
        }
    }
}
