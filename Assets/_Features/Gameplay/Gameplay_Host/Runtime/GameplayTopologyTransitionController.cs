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
        private GameplayBoardSurfaceRenderer _boardSurfaceRenderer;
        private bool _isBoardRotationTweenActive;
        private bool _isBoardSurfaceTransitionActive;
        private Quaternion _boardSurfaceTransitionStartRotation = Quaternion.identity;
        private Quaternion _presentedBoardRotation = Quaternion.identity;
        private Func<Vector3> _resolveCubeCenter;
        private GameplayTimingProfile _timingProfile;
        private TopologyRotationTweenSettings _topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault();
        private TopologyRotationVisualMapping _topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX;

        public GameplayTopologyTransitionController(GameplayMotionTimingResolver motionTimingResolver)
        {
            _motionTimingResolver = motionTimingResolver ?? throw new ArgumentNullException(nameof(motionTimingResolver));
        }

        public bool HasActiveBoardRotationTween => _isBoardRotationTweenActive &&
                                                   _boardRotationTween != null;

        public Quaternion PresentedBoardRotation => _presentedBoardRotation;

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

        public void Reset()
        {
            _boardSurfaceTransitionStartRotation = Quaternion.identity;
            KillBoardRotationTween();
            _isBoardSurfaceTransitionActive = false;
            ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true);
        }

        public void CompleteInitialTopology(CubeTopologyState topology)
        {
            _boardSurfaceRenderer?.CompleteTopologyTransition(topology);
        }

        public void RefreshTopologyTrack(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            KillBoardRotationTween();

            if (!presentationData.TopologyMotion.HasValue ||
                presentationData.TopologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true);
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            var startRotation = _presentedBoardRotation * ResolveTopologyRotationOffset(topologyMotion.RotationKind);
            ApplyPresentedBoardRotation(startRotation, forceApply: true);
            StartBoardRotationTween(
                startRotation,
                _motionTimingResolver.ResolveTopologyMotionDurationSeconds(_timingProfile));
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
                _boardSurfaceRenderer.CompleteTopologyTransition(committedTopology);
                _boardSurfaceTransitionStartRotation = Quaternion.identity;
                _isBoardSurfaceTransitionActive = false;
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            _boardSurfaceTransitionStartRotation = _presentedBoardRotation;
            _boardSurfaceRenderer.BeginTopologyTransition(
                topologyMotion.SourceTopology,
                topologyMotion.DestinationTopology,
                _boardSurfaceTransitionStartRotation);
            _isBoardSurfaceTransitionActive = true;
        }

        public void UpdatePresentation(float deltaTime, CubeTopologyState committedTopology)
        {
            if (_isBoardRotationTweenActive &&
                _boardRotationTween != null)
            {
                _boardRotationTween.ManualUpdate(deltaTime, deltaTime);
            }

            UpdateBoardSurfaceTransition(_presentedBoardRotation);
            CleanupCompletedBoardSurfaceTransitionState(committedTopology);
        }

        public Quaternion ResolveTopologyRotationOffset(CubeRotationKind rotationKind)
        {
            var forwardDegrees = _topologyRotationVisualMapping == TopologyRotationVisualMapping.ForwardUsesPositiveX
                ? 90f
                : -90f;

            return rotationKind switch
            {
                CubeRotationKind.Forward => Quaternion.Euler(forwardDegrees, 0f, 0f),
                CubeRotationKind.Backward => Quaternion.Euler(-forwardDegrees, 0f, 0f),
                _ => Quaternion.identity,
            };
        }

        private void ApplyPresentedBoardRotation(Quaternion boardRotation, bool forceApply = false)
        {
            if (!forceApply &&
                Quaternion.Angle(_presentedBoardRotation, boardRotation) <= 0.001f)
            {
                return;
            }

            _presentedBoardRotation = boardRotation;
            _boardRoot?.ApplyPresentationRotation(boardRotation, _resolveCubeCenter());
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
            _boardSurfaceTransitionStartRotation = Quaternion.identity;
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
            if (!Enum.IsDefined(typeof(TopologyRotationTweenMode), settings.Mode) ||
                !Enum.IsDefined(typeof(TopologyRotationTweenEase), settings.Ease))
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

        private Quaternion ResolveTweenedBoardRotation(Quaternion startRotation, float progress)
        {
            return _topologyRotationTweenSettings.Mode switch
            {
                TopologyRotationTweenMode.QuaternionSlerp => Quaternion.SlerpUnclamped(
                    startRotation,
                    Quaternion.identity,
                    progress),
                _ => Quaternion.Euler(
                    Mathf.LerpUnclamped(
                        NormalizeSignedAxisAngle(startRotation.eulerAngles.x),
                        0f,
                        progress),
                    0f,
                    0f),
            };
        }

        private void StartBoardRotationTween(Quaternion startRotation, float durationSeconds)
        {
            var progress = 0f;
            _isBoardRotationTweenActive = true;
            _boardRotationTween = DOTween
                .To(
                    () => progress,
                    value =>
                    {
                        progress = value;
                        ApplyPresentedBoardRotation(ResolveTweenedBoardRotation(startRotation, progress));
                    },
                    1f,
                    Mathf.Max(durationSeconds, MinimumTopologyTweenDurationSeconds))
                .SetEase(ResolveTopologyRotationEase())
                .SetUpdate(UpdateType.Manual)
                .SetAutoKill(true)
                .OnComplete(() => ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true))
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
            var totalAngle = Quaternion.Angle(_boardSurfaceTransitionStartRotation, Quaternion.identity);
            if (totalAngle <= 0.001f)
            {
                return 1f;
            }

            var remainingAngle = Quaternion.Angle(presentedBoardRotation, Quaternion.identity);
            return Mathf.Clamp01(1f - (remainingAngle / totalAngle));
        }

        private static float NormalizeSignedAxisAngle(float eulerDegrees)
        {
            return Mathf.DeltaAngle(0f, eulerDegrees);
        }
    }
}
