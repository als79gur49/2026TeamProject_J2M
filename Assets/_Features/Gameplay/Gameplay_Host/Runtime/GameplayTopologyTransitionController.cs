using System;
using DG.Tweening;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayTopologyTransitionController
    {
        private const float MinimumTopologyTweenDurationSeconds = 0.0001f;

        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private GameplayBoardRoot _boardRoot;
        private Tween _boardRotationTween;
        private GameplayCameraRig _cameraRig;
        private GameplayBoardSurfaceRenderer _boardSurfaceRenderer;
        private TickTopologyMotion? _activeTopologyMotion;
        private float _activeTopologyMotionDurationSeconds;
        private TopologyTransitionVisualState _currentVisualState =
            TopologyTransitionVisualState.Inactive(default, Quaternion.identity);
        private bool _isBoardRotationTweenActive;
        private bool _isBoardSurfaceTransitionActive;
        private CubeTopologyState _lastCommittedTopology;
        private Quaternion _boardSurfaceTransitionStartRotation = Quaternion.identity;
        private Quaternion _boardSurfaceTransitionDestinationRotation = Quaternion.identity;
        // This is a topology presentation reference rotation used to drive the camera orbit.
        // Physical board geometry remains fixed in the camera-only topology model.
        private Quaternion _presentedBoardRotation = Quaternion.identity;
        private float _presentedBoardRotationXDegrees;
        private Func<Vector3> _resolveCubeCenter;
        private GameplayTimingProfile _timingProfile;
        private TopologyRotationTweenSettings _topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault();
        private TopologyRotationVisualMapping _topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX;

        public GameplayTopologyTransitionController(GameplayMotionTimingResolver motionTimingResolver)
        {
            _motionTimingResolver = motionTimingResolver ?? throw new ArgumentNullException(nameof(motionTimingResolver));
        }

        public bool HasActiveBoardRotationTween => _isBoardRotationTweenActive &&
                                                   _boardRotationTween != null;

        // Despite the historical name, this is not a physical board transform rotation.
        // It is the currently presented topology reference rotation consumed by the camera.
        public Quaternion PresentedBoardRotation => _presentedBoardRotation;

        public TopologyTransitionVisualState CurrentVisualState => _currentVisualState;

        public void Configure(
            GameplayBoardRoot boardRoot,
            GameplayBoardSurfaceRenderer boardSurfaceRenderer,
            GameplayTimingProfile timingProfile,
            TopologyRotationVisualMapping topologyRotationVisualMapping,
            TopologyRotationTweenSettings topologyRotationTweenSettings,
            Func<Vector3> resolveCubeCenter)
        {
            _boardRoot = boardRoot;
            _boardSurfaceRenderer = boardSurfaceRenderer != null ? boardSurfaceRenderer : boardRoot?.BoardSurfaceRenderer;
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _topologyRotationVisualMapping = topologyRotationVisualMapping;
            _topologyRotationTweenSettings = NormalizeTopologyRotationTweenSettings(topologyRotationTweenSettings);
            _resolveCubeCenter = resolveCubeCenter ?? throw new ArgumentNullException(nameof(resolveCubeCenter));
        }

        public void AttachCameraRig(GameplayCameraRig cameraRig)
        {
            _cameraRig = cameraRig;
            ApplyPresentedBoardRotation(_presentedBoardRotationXDegrees, forceApply: true);
        }

        public void Reset()
        {
            _activeTopologyMotion = null;
            _activeTopologyMotionDurationSeconds = 0f;
            _lastCommittedTopology = default;
            _boardSurfaceTransitionStartRotation = Quaternion.identity;
            _boardSurfaceTransitionDestinationRotation = Quaternion.identity;
            KillBoardRotationTween();
            _isBoardSurfaceTransitionActive = false;
            ApplyPresentedBoardRotation(0f, forceApply: true);
            UpdateInactiveVisualState();
        }

        public void CompleteInitialTopology(CubeTopologyState topology)
        {
            _lastCommittedTopology = topology;
            var restReferenceRotationXDegrees =
                ResolveNearestRestReferenceAngleXDegrees(_presentedBoardRotationXDegrees, topology);
            var restReferenceRotation = ResolveRotationFromXDegrees(restReferenceRotationXDegrees);
            _boardSurfaceTransitionStartRotation = restReferenceRotation;
            _boardSurfaceTransitionDestinationRotation = restReferenceRotation;
            ApplyPresentedBoardRotation(restReferenceRotationXDegrees, forceApply: true);
            _boardSurfaceRenderer?.CompleteTopologyTransition(topology);
            UpdateInactiveVisualState();
        }

        public void RefreshTopologyTrack(
            TickPresentationData presentationData,
            CubeTopologyState committedTopology)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            KillBoardRotationTween();

            if (!presentationData.TopologyMotion.HasValue ||
                presentationData.TopologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                _activeTopologyMotion = null;
                _activeTopologyMotionDurationSeconds = 0f;
                _lastCommittedTopology = committedTopology;
                var restReferenceRotationXDegrees =
                    ResolveNearestRestReferenceAngleXDegrees(_presentedBoardRotationXDegrees, committedTopology);
                var restReferenceRotation = ResolveRotationFromXDegrees(restReferenceRotationXDegrees);
                _boardSurfaceTransitionStartRotation = restReferenceRotation;
                _boardSurfaceTransitionDestinationRotation = restReferenceRotation;
                ApplyPresentedBoardRotation(restReferenceRotationXDegrees, forceApply: true);
                UpdateInactiveVisualState();
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            var durationSeconds = Mathf.Max(
                _motionTimingResolver.ResolveTopologyMotionDurationSeconds(_timingProfile),
                MinimumTopologyTweenDurationSeconds);
            _activeTopologyMotion = topologyMotion;
            _activeTopologyMotionDurationSeconds = durationSeconds;
            var startRotationXDegrees = _presentedBoardRotationXDegrees;
            var destinationRotationXDegrees =
                ResolveNearestRestReferenceAngleXDegrees(startRotationXDegrees, topologyMotion.DestinationTopology);
            var startRotation = ResolveRotationFromXDegrees(startRotationXDegrees);
            var destinationRotation = ResolveRotationFromXDegrees(destinationRotationXDegrees);
            _boardSurfaceTransitionStartRotation = startRotation;
            _boardSurfaceTransitionDestinationRotation = destinationRotation;
            ApplyPresentedBoardRotation(startRotationXDegrees, forceApply: true);
            RecalculateVisualState(startRotation, deltaTime: 0f);
            StartBoardRotationTween(
                startRotationXDegrees,
                destinationRotationXDegrees,
                durationSeconds);
        }

        public void RefreshBoardSurfaceTransition(
            TickPresentationData presentationData,
            CubeTopologyState committedTopology)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            if (_boardSurfaceRenderer == null)
            {
                return;
            }

            if (!IsTopologyTransitionPresentation(presentationData.TopologyMotion))
            {
                var restReferenceRotationXDegrees =
                    ResolveNearestRestReferenceAngleXDegrees(_presentedBoardRotationXDegrees, committedTopology);
                var restReferenceRotation = ResolveRotationFromXDegrees(restReferenceRotationXDegrees);
                _boardSurfaceRenderer.CompleteTopologyTransition(committedTopology);
                _lastCommittedTopology = committedTopology;
                _boardSurfaceTransitionStartRotation = restReferenceRotation;
                _boardSurfaceTransitionDestinationRotation = restReferenceRotation;
                _isBoardSurfaceTransitionActive = false;
                if (!HasActiveBoardRotationTween)
                {
                    UpdateInactiveVisualState();
                }

                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            _boardSurfaceTransitionStartRotation = _presentedBoardRotation;
            _boardSurfaceTransitionDestinationRotation =
                ResolveRotationFromXDegrees(
                    ResolveNearestRestReferenceAngleXDegrees(
                        _presentedBoardRotationXDegrees,
                        topologyMotion.DestinationTopology));
            _boardSurfaceRenderer.BeginTopologyTransition(
                topologyMotion.SourceTopology,
                topologyMotion.DestinationTopology);
            _isBoardSurfaceTransitionActive = true;
        }

        public void UpdatePresentation(float deltaTime, CubeTopologyState committedTopology)
        {
            var previousPresentedBoardRotation = _presentedBoardRotation;
            if (_isBoardRotationTweenActive &&
                _boardRotationTween != null)
            {
                _boardRotationTween.ManualUpdate(deltaTime, deltaTime);
            }

            if (!HasActiveBoardRotationTween)
            {
                _lastCommittedTopology = committedTopology;
                var restReferenceRotationXDegrees =
                    ResolveNearestRestReferenceAngleXDegrees(_presentedBoardRotationXDegrees, committedTopology);
                var restReferenceRotation = ResolveRotationFromXDegrees(restReferenceRotationXDegrees);
                _boardSurfaceTransitionStartRotation = restReferenceRotation;
                _boardSurfaceTransitionDestinationRotation = restReferenceRotation;
                ApplyPresentedBoardRotation(restReferenceRotationXDegrees);
            }

            UpdateBoardSurfaceTransition(_presentedBoardRotation);
            CleanupCompletedBoardSurfaceTransitionState(committedTopology);
            RecalculateVisualState(previousPresentedBoardRotation, deltaTime);
        }

        private float ResolveRestReferenceAngleXDegrees(CubeTopologyState topology)
        {
            return GameplayTopologyVisualRotationUtility.ResolveRestReferenceAngleXDegrees(
                topology,
                _topologyRotationVisualMapping);
        }

        private float ResolveNearestRestReferenceAngleXDegrees(float currentAngleXDegrees, CubeTopologyState topology)
        {
            var desiredAngleXDegrees = ResolveRestReferenceAngleXDegrees(topology);
            return currentAngleXDegrees + Mathf.DeltaAngle(currentAngleXDegrees, desiredAngleXDegrees);
        }
        private void ApplyPresentedBoardRotation(float visualReferenceRotationXDegrees, bool forceApply = false)
        {
            var visualReferenceRotation = ResolveRotationFromXDegrees(visualReferenceRotationXDegrees);
            if (!forceApply &&
                Quaternion.Angle(_presentedBoardRotation, visualReferenceRotation) <= 0.001f &&
                Mathf.Abs(_presentedBoardRotationXDegrees - visualReferenceRotationXDegrees) <= 0.001f)
            {
                return;
            }

            _presentedBoardRotation = visualReferenceRotation;
            _presentedBoardRotationXDegrees = visualReferenceRotationXDegrees;
            _boardRoot?.ApplyPresentationRotation(visualReferenceRotation, _resolveCubeCenter());
            _cameraRig?.SetPresentedTopologyOrbit(
                Quaternion.Inverse(visualReferenceRotation),
                -visualReferenceRotationXDegrees);
        }

        private void CleanupCompletedBoardSurfaceTransitionState(CubeTopologyState committedTopology)
        {
            if (!_isBoardSurfaceTransitionActive ||
                _boardSurfaceRenderer == null ||
                HasActiveBoardRotationTween)
            {
                return;
            }

            _boardSurfaceRenderer.CompleteTopologyTransition(committedTopology);
            _lastCommittedTopology = committedTopology;
            var restReferenceRotationXDegrees =
                ResolveNearestRestReferenceAngleXDegrees(_presentedBoardRotationXDegrees, committedTopology);
            var restReferenceRotation = ResolveRotationFromXDegrees(restReferenceRotationXDegrees);
            _boardSurfaceTransitionStartRotation = restReferenceRotation;
            _boardSurfaceTransitionDestinationRotation = restReferenceRotation;
            _isBoardSurfaceTransitionActive = false;
        }

        private static bool IsTopologyTransitionPresentation(TickTopologyMotion? topologyMotion)
        {
            return topologyMotion.HasValue &&
                   topologyMotion.Value.RotationKind != CubeRotationKind.None;
        }

        private static TopologyRotationTweenSettings NormalizeTopologyRotationTweenSettings(
            TopologyRotationTweenSettings settings)
        {
            if (!Enum.IsDefined(typeof(TopologyRotationTweenEase), settings.Ease))
            {
                return TopologyRotationTweenSettings.CreateDefault();
            }

            return settings;
        }

        private void KillBoardRotationTween(bool complete = false)
        {
            if (_boardRotationTween != null &&
                _boardRotationTween.IsActive())
            {
                _boardRotationTween.Kill(complete);
            }

            _boardRotationTween = null;
            _isBoardRotationTweenActive = false;
        }

        private Ease ResolveTopologyRotationEase()
        {
            return Enum.TryParse(_topologyRotationTweenSettings.Ease.ToString(), out Ease ease)
                ? ease
                : Ease.OutQuad;
        }

        private void StartBoardRotationTween(
            float startRotationXDegrees,
            float destinationRotationXDegrees,
            float durationSeconds)
        {
            var progress = 0f;
            _isBoardRotationTweenActive = true;
            _boardRotationTween = DOTween
                .To(
                    () => progress,
                    value =>
                    {
                        progress = value;
                        var tweenedRotationXDegrees =
                            Mathf.LerpUnclamped(startRotationXDegrees, destinationRotationXDegrees, progress);
                        var tweenedRotation = ResolveRotationFromXDegrees(tweenedRotationXDegrees);

                        _presentedBoardRotation = tweenedRotation;
                        _presentedBoardRotationXDegrees = tweenedRotationXDegrees;
                        _boardRoot?.ApplyPresentationRotation(tweenedRotation, _resolveCubeCenter());
                        _cameraRig?.SetPresentedTopologyOrbit(
                            Quaternion.Inverse(tweenedRotation),
                            -tweenedRotationXDegrees);
                    },
                    1f,
                    durationSeconds)
                .SetEase(ResolveTopologyRotationEase())
                .SetUpdate(UpdateType.Manual)
                .SetAutoKill(true)
                .OnComplete(() => ApplyPresentedBoardRotation(destinationRotationXDegrees, forceApply: true))
                .OnKill(() =>
                {
                    _boardRotationTween = null;
                    _isBoardRotationTweenActive = false;
                });
        }

        private void UpdateBoardSurfaceTransition(Quaternion presentedBoardRotation)
        {
            if (!_isBoardSurfaceTransitionActive ||
                _boardSurfaceRenderer == null)
            {
                return;
            }

            _boardSurfaceRenderer.UpdateTopologyTransition(
                ResolveBoardSurfaceTransitionProgress(presentedBoardRotation));
        }

        private float ResolveBoardSurfaceTransitionProgress(Quaternion presentedBoardRotation)
        {
            var totalAngle = Quaternion.Angle(
                _boardSurfaceTransitionStartRotation,
                _boardSurfaceTransitionDestinationRotation);
            if (totalAngle <= 0.001f)
            {
                return 1f;
            }

            var remainingAngle = Quaternion.Angle(
                presentedBoardRotation,
                _boardSurfaceTransitionDestinationRotation);
            return Mathf.Clamp01(1f - (remainingAngle / totalAngle));
        }

        private void RecalculateVisualState(Quaternion previousPresentedBoardRotation, float deltaTime)
        {
            if (!HasActiveBoardRotationTween ||
                !_activeTopologyMotion.HasValue)
            {
                UpdateInactiveVisualState();
                return;
            }

            var topologyMotion = _activeTopologyMotion.Value;
            _currentVisualState = new TopologyTransitionVisualState(
                isActive: true,
                progress01: ResolveBoardSurfaceTransitionProgress(_presentedBoardRotation),
                sourceTopology: topologyMotion.SourceTopology,
                destinationTopology: topologyMotion.DestinationTopology,
                rotationKind: topologyMotion.RotationKind,
                durationSeconds: _activeTopologyMotionDurationSeconds,
                presentedVisualRotation: _presentedBoardRotation,
                angularVelocityNormalized: ResolveAngularVelocityNormalized(
                    previousPresentedBoardRotation,
                    deltaTime));
        }

        private float ResolveAngularVelocityNormalized(
            Quaternion previousPresentedBoardRotation,
            float deltaTime)
        {
            if (deltaTime <= 0f ||
                _activeTopologyMotionDurationSeconds <= 0f)
            {
                return 0f;
            }

            var totalAngle = Quaternion.Angle(
                _boardSurfaceTransitionStartRotation,
                _boardSurfaceTransitionDestinationRotation);
            if (totalAngle <= 0.001f)
            {
                return 0f;
            }

            var baselineDegreesPerSecond = totalAngle / _activeTopologyMotionDurationSeconds;
            if (baselineDegreesPerSecond <= 0.001f)
            {
                return 0f;
            }

            var actualDegreesPerSecond =
                Quaternion.Angle(previousPresentedBoardRotation, _presentedBoardRotation) / deltaTime;
            return Mathf.Max(0f, actualDegreesPerSecond / baselineDegreesPerSecond);
        }

        private void UpdateInactiveVisualState()
        {
            _currentVisualState = TopologyTransitionVisualState.Inactive(
                _lastCommittedTopology,
                _presentedBoardRotation);
        }

        private static Quaternion ResolveRotationFromXDegrees(float xDegrees)
        {
            return Quaternion.Euler(xDegrees, 0f, 0f);
        }
    }
}
