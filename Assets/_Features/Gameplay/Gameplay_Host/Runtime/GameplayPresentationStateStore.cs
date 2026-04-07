using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayPresentationStateStore
    {
        private readonly Dictionary<int, GameplayEntityPose> _committedLocalTargetPoses = new();
        private readonly Dictionary<int, EntityType> _entityTypesByEntityId = new();
        private readonly Dictionary<int, JumpDetachedVisibilityState> _jumpDetachedVisibilityStates = new();
        private readonly HashSet<int> _processingEntityIds = new();
        private readonly List<int> _processingEntityIdBuffer = new();
        private readonly Dictionary<int, GameplayEntityPose> _retainedLocalTargetPoses = new();
        private readonly Dictionary<int, TransitionVisibilityState> _transitionVisibilityStates = new();
        private readonly Dictionary<int, GameplayEntityView> _viewsByEntityId = new();

        public Dictionary<int, GameplayEntityPose> CommittedLocalTargetPoses => _committedLocalTargetPoses;

        public CubeTopologyState CommittedTopology { get; set; }

        public Dictionary<int, EntityType> EntityTypesByEntityId => _entityTypesByEntityId;

        public bool HasAnyCommittedFrame { get; set; }

        public Dictionary<int, JumpDetachedVisibilityState> JumpDetachedVisibilityStates => _jumpDetachedVisibilityStates;

        public Dictionary<int, GameplayEntityPose> RetainedLocalTargetPoses => _retainedLocalTargetPoses;

        public Dictionary<int, TransitionVisibilityState> TransitionVisibilityStates => _transitionVisibilityStates;

        public Dictionary<int, GameplayEntityView> ViewsByEntityId => _viewsByEntityId;

        public void ResetSession(CubeTopologyState topology)
        {
            CommittedTopology = topology;
            HasAnyCommittedFrame = false;
            _committedLocalTargetPoses.Clear();
            _entityTypesByEntityId.Clear();
            _jumpDetachedVisibilityStates.Clear();
            _retainedLocalTargetPoses.Clear();
            _transitionVisibilityStates.Clear();
            _viewsByEntityId.Clear();
            _processingEntityIds.Clear();
            _processingEntityIdBuffer.Clear();
        }

        public void BeginCommittedFrame(CubeTopologyState topology)
        {
            CommittedTopology = topology;
            _committedLocalTargetPoses.Clear();
        }

        public IReadOnlyList<int> BuildProcessingEntityIds()
        {
            _processingEntityIds.Clear();
            _processingEntityIdBuffer.Clear();

            foreach (var pair in _committedLocalTargetPoses)
            {
                AddProcessingEntityId(pair.Key);
            }

            foreach (var pair in _retainedLocalTargetPoses)
            {
                AddProcessingEntityId(pair.Key);
            }

            foreach (var pair in _jumpDetachedVisibilityStates)
            {
                AddProcessingEntityId(pair.Key);
            }

            foreach (var pair in _transitionVisibilityStates)
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
    }
}
