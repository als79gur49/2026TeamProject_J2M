using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct TopologyTransitionVisualState
    {
        public TopologyTransitionVisualState(
            bool isActive,
            float progress01,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            float durationSeconds,
            Quaternion presentedVisualRotation,
            float angularVelocityNormalized)
        {
            IsActive = isActive;
            Progress01 = Mathf.Clamp01(progress01);
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            PresentedVisualRotation = presentedVisualRotation;
            AngularVelocityNormalized = Mathf.Max(0f, angularVelocityNormalized);
        }

        public bool IsActive { get; }

        public float Progress01 { get; }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public float DurationSeconds { get; }

        public Quaternion PresentedVisualRotation { get; }

        public float AngularVelocityNormalized { get; }

        public static TopologyTransitionVisualState Inactive(
            CubeTopologyState topology,
            Quaternion presentedVisualRotation)
        {
            return new TopologyTransitionVisualState(
                isActive: false,
                progress01: 0f,
                sourceTopology: topology,
                destinationTopology: topology,
                rotationKind: CubeRotationKind.None,
                durationSeconds: 0f,
                presentedVisualRotation: presentedVisualRotation,
                angularVelocityNormalized: 0f);
        }
    }

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

        public int ActiveTransientEffectCount => _presentationCoordinator.ActiveTransientEffectCount;

        public TopologyTransitionVisualState CurrentTopologyTransitionVisualState =>
            _presentationCoordinator.CurrentTopologyTransitionVisualState;

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
            GameplayBoardSurfaceRenderer boardSurfaceRenderer = null,
            TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
            TopologyRotationTweenSettings topologyRotationTweenSettings = default)
        {
            _presentationCoordinator.Initialize(
                viewBinder,
                boardBounds,
                initialTopology,
                cellSize,
                timingProfile,
                boardRoot,
                boardSurfaceRenderer,
                topologyRotationVisualMapping,
                topologyRotationTweenSettings);
        }

        public void Present(TickResult result)
        {
            _presentationCoordinator.Present(result);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _presentationCoordinator.PresentInitial(entities, topology);
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            _presentationCoordinator.AttachCameraRig(viewCameraRig);
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
