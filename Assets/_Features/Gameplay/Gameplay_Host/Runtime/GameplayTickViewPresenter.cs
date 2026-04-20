using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Unity.Cinemachine;
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
        private bool _hasObservedPresentationState;
        private bool _lastHasBlockingPresentation;
        private bool _lastIsPresentationActive;
        private bool _lastIsTopologyTransitionActive;
        private CubeTopologyState _lastObservedTopology;
        private TopologyTransitionPostFxController _topologyTransitionPostFxController;
        private CinemachineBrain _viewCameraBrain;
        private GameplayCameraRig _viewCameraRig;

        public event System.Action<CubeTopologyState> TopologyCommitted
        {
            add => _presentationCoordinator.TopologyCommitted += value;
            remove => _presentationCoordinator.TopologyCommitted -= value;
        }

        public event System.Action PresentationStateChanged;

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
            TopologyRotationTweenSettings topologyRotationTweenSettings = default,
            float faceSeamGap = -1f)
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
                topologyRotationTweenSettings,
                faceSeamGap);
            CapturePresentationState();
        }

        public void Present(TickResult result)
        {
            _presentationCoordinator.Present(result);
            NotifyPresentationStateChangedIfNeeded();
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _presentationCoordinator.PresentInitial(entities, topology);
            CapturePresentationState();
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            _presentationCoordinator.AttachCameraRig(viewCameraRig);
            _viewCameraRig = viewCameraRig;
            _viewCameraBrain = null;
        }

        public void AttachOutputCamera(Camera outputCamera)
        {
            _presentationCoordinator.AttachOutputCamera(outputCamera);
        }

        internal void AttachGameplayAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            _presentationCoordinator.AttachGameplayAudioRuntime(playbackPort, gameplayAudioMap);
        }

        internal void DebugRefreshGameplayAudioPlan(TickResult result)
        {
            _presentationCoordinator.DebugRefreshGameplayAudioPlan(result);
        }

        internal void SetPresentationTraceSink(System.Action<string> traceSink)
        {
            _presentationCoordinator.SetTraceSink(traceSink);
        }

        internal int PendingGameplayAudioRequestCount => _presentationCoordinator.PendingGameplayAudioRequestCount;

        public void AttachCameraRuntime(GameplayCameraRig viewCameraRig, CinemachineBrain viewCameraBrain)
        {
            _presentationCoordinator.AttachCameraRig(viewCameraRig);
            _viewCameraRig = viewCameraRig;
            _viewCameraBrain = viewCameraBrain;

            if (_viewCameraBrain != null)
            {
                _viewCameraBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            }
        }

        public void AttachTopologyTransitionPostFxController(TopologyTransitionPostFxController topologyTransitionPostFxController)
        {
            _topologyTransitionPostFxController = topologyTransitionPostFxController;
            RefreshTopologyTransitionPostFx();
        }

        public void UpdatePresentation(float deltaTime)
        {
            _presentationCoordinator.UpdatePresentation(deltaTime);
            SyncViewCameraRuntime();
            RefreshTopologyTransitionPostFx();
            NotifyPresentationStateChangedIfNeeded();
        }

        private void RefreshTopologyTransitionPostFx()
        {
            _topologyTransitionPostFxController?.Apply(_presentationCoordinator.CurrentTopologyTransitionVisualState);
        }

        private void SyncViewCameraRuntime()
        {
            if (_viewCameraRig != null)
            {
                _viewCameraRig.ApplyTopologyTransitionVisualState(_presentationCoordinator.CurrentTopologyTransitionVisualState);
                _viewCameraRig.SnapToTarget();
            }

            if (_viewCameraBrain != null &&
                _viewCameraBrain.isActiveAndEnabled)
            {
                _viewCameraBrain.ManualUpdate();
            }
        }

        private void LateUpdate()
        {
            if (_presentationCoordinator.IsInitialized)
            {
                UpdatePresentation(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            _presentationCoordinator.DetachGameplayAudioRuntime();
        }

        private void CapturePresentationState()
        {
            _lastObservedTopology = _presentationCoordinator.CurrentTopology;
            _lastIsPresentationActive = _presentationCoordinator.IsPresentationActive;
            _lastHasBlockingPresentation = _presentationCoordinator.HasBlockingPresentation;
            _lastIsTopologyTransitionActive = _presentationCoordinator.IsTopologyTransitionActive;
            _hasObservedPresentationState = true;
        }

        private void NotifyPresentationStateChangedIfNeeded()
        {
            if (!_hasObservedPresentationState)
            {
                CapturePresentationState();
                return;
            }

            var currentTopology = _presentationCoordinator.CurrentTopology;
            var isPresentationActive = _presentationCoordinator.IsPresentationActive;
            var hasBlockingPresentation = _presentationCoordinator.HasBlockingPresentation;
            var isTopologyTransitionActive = _presentationCoordinator.IsTopologyTransitionActive;

            if (currentTopology.Equals(_lastObservedTopology) &&
                isPresentationActive == _lastIsPresentationActive &&
                hasBlockingPresentation == _lastHasBlockingPresentation &&
                isTopologyTransitionActive == _lastIsTopologyTransitionActive)
            {
                return;
            }

            _lastObservedTopology = currentTopology;
            _lastIsPresentationActive = isPresentationActive;
            _lastHasBlockingPresentation = hasBlockingPresentation;
            _lastIsTopologyTransitionActive = isTopologyTransitionActive;
            PresentationStateChanged?.Invoke();
        }
    }
}
