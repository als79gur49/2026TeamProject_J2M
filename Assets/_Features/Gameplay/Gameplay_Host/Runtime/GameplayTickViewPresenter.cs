using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum GameplayPresentationPhase
    {
        Idle = 0,
        EntityMotion = 1,
        TopologyTransition = 2,
    }

    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private readonly GameplayTickPresentationCoordinator _presentationCoordinator = new();

        public event System.Action<CubeTopologyState> TopologyCommitted
        {
            add => _presentationCoordinator.TopologyCommitted += value;
            remove => _presentationCoordinator.TopologyCommitted -= value;
        }

        public CubeTopologyState CurrentTopology => _presentationCoordinator.CurrentTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => _presentationCoordinator.CurrentPresentationPhase;

        public bool IsPresentationActive => _presentationCoordinator.IsPresentationActive;

        public bool HasBlockingPresentation => _presentationCoordinator.HasBlockingPresentation;

        public bool IsTopologyTransitionActive => _presentationCoordinator.IsTopologyTransitionActive;

        public Quaternion PresentedBoardRotation => _presentationCoordinator.PresentedBoardRotation;

        public Vector3 CubeCenter => _presentationCoordinator.CubeCenter;

        public Bounds VisibleCubeBounds => _presentationCoordinator.VisibleCubeBounds;

        public void Initialize(
            GameplayEntityViewBinder viewBinder,
            BoardBounds boardBounds,
            CubeTopologyState initialTopology,
            float cellSize,
            GameplayTimingProfile timingProfile,
            GameplayBoardRoot boardRoot = null,
            TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX)
        {
            _presentationCoordinator.Initialize(
                viewBinder,
                boardBounds,
                initialTopology,
                cellSize,
                timingProfile,
                boardRoot,
                topologyRotationVisualMapping);
        }

        public void Present(TickResult result)
        {
            _presentationCoordinator.Present(result);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _presentationCoordinator.PresentInitial(entities, topology);
        }

        public void UpdatePresentation(float deltaTime)
        {
            _presentationCoordinator.UpdatePresentation(deltaTime);
        }

        private void LateUpdate()
        {
            if (_presentationCoordinator.IsInitialized)
            {
                UpdatePresentation(Time.deltaTime);
            }
        }
    }
}
