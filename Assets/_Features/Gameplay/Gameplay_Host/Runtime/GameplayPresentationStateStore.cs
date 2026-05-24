using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayPresentationStateStore
    {
        private readonly Dictionary<int, GameplayEntityPose> _committedLocalTargetPoses = new();
        private readonly Dictionary<int, FaceId> _committedFacesByEntityId = new();
        private readonly Dictionary<int, GameplayProjectedFaceSlot> _committedProjectedSlotsByEntityId = new();
        private readonly Dictionary<int, EnemyAiMode> _enemyAiModesByEntityId = new();
        private readonly Dictionary<int, EnemyVisualPresentationFacts> _enemyVisualFactsByEntityId = new();
        private readonly Dictionary<int, EnemyVisualSemanticState> _enemyVisualSemanticStatesByEntityId = new();
        private readonly Dictionary<int, EntityType> _entityTypesByEntityId = new();
        private readonly Dictionary<int, EntityPresentationApplySignature> _lastEnemyApplySignaturesByEntityId = new();
        private readonly Dictionary<int, UnitRole> _unitRolesByEntityId = new();
        private readonly Dictionary<int, JumpDetachedVisibilityState> _jumpDetachedVisibilityStates = new();
        private readonly HashSet<int> _processingEntityIds = new();
        private readonly List<int> _processingEntityIdBuffer = new();
        private readonly Dictionary<int, GameplayEntityPose> _presentedLocalPosesByEntityId = new();
        private readonly Dictionary<int, GameplayEntityPose> _retainedLocalTargetPoses = new();
        private readonly Dictionary<int, TransitionVisibilityState> _transitionVisibilityStates = new();
        private readonly Dictionary<int, GameplayEntityView> _viewsByEntityId = new();

        public Dictionary<int, GameplayEntityPose> CommittedLocalTargetPoses => _committedLocalTargetPoses;

        public CubeTopologyState CommittedTopology { get; set; }

        public Dictionary<int, FaceId> CommittedFacesByEntityId => _committedFacesByEntityId;

        public Dictionary<int, GameplayProjectedFaceSlot> CommittedProjectedSlotsByEntityId => _committedProjectedSlotsByEntityId;

        public Dictionary<int, EnemyAiMode> EnemyAiModesByEntityId => _enemyAiModesByEntityId;

        public Dictionary<int, EnemyVisualPresentationFacts> EnemyVisualFactsByEntityId => _enemyVisualFactsByEntityId;

        public Dictionary<int, EnemyVisualSemanticState> EnemyVisualSemanticStatesByEntityId => _enemyVisualSemanticStatesByEntityId;

        public Dictionary<int, EntityType> EntityTypesByEntityId => _entityTypesByEntityId;

        public Dictionary<int, UnitRole> UnitRolesByEntityId => _unitRolesByEntityId;

        public bool HasAnyCommittedFrame { get; set; }

        internal Dictionary<int, EntityPresentationApplySignature> LastEnemyApplySignaturesByEntityId =>
            _lastEnemyApplySignaturesByEntityId;

        internal EntityPresentationApplyDiagnostics LastEntityPresentationApplyDiagnostics { get; set; }

        public Dictionary<int, JumpDetachedVisibilityState> JumpDetachedVisibilityStates => _jumpDetachedVisibilityStates;

        public Dictionary<int, GameplayEntityPose> PresentedLocalPosesByEntityId => _presentedLocalPosesByEntityId;

        public Dictionary<int, GameplayEntityPose> RetainedLocalTargetPoses => _retainedLocalTargetPoses;

        public Dictionary<int, TransitionVisibilityState> TransitionVisibilityStates => _transitionVisibilityStates;

        public Dictionary<int, GameplayEntityView> ViewsByEntityId => _viewsByEntityId;

        public void ResetSession(CubeTopologyState topology)
        {
            CommittedTopology = topology;
            HasAnyCommittedFrame = false;
            _committedLocalTargetPoses.Clear();
            _committedFacesByEntityId.Clear();
            _committedProjectedSlotsByEntityId.Clear();
            _enemyAiModesByEntityId.Clear();
            _enemyVisualFactsByEntityId.Clear();
            _enemyVisualSemanticStatesByEntityId.Clear();
            _entityTypesByEntityId.Clear();
            _lastEnemyApplySignaturesByEntityId.Clear();
            _unitRolesByEntityId.Clear();
            _jumpDetachedVisibilityStates.Clear();
            _presentedLocalPosesByEntityId.Clear();
            _retainedLocalTargetPoses.Clear();
            _transitionVisibilityStates.Clear();
            _viewsByEntityId.Clear();
            _processingEntityIds.Clear();
            _processingEntityIdBuffer.Clear();
            LastEntityPresentationApplyDiagnostics = default;
        }

        public void BeginCommittedFrame(CubeTopologyState topology)
        {
            CommittedTopology = topology;
            _committedLocalTargetPoses.Clear();
            _committedFacesByEntityId.Clear();
            _committedProjectedSlotsByEntityId.Clear();
            _enemyVisualFactsByEntityId.Clear();
            _enemyVisualSemanticStatesByEntityId.Clear();
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
