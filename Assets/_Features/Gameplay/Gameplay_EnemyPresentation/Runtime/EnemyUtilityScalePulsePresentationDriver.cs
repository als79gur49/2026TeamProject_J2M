using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class EnemyUtilityScalePulsePresentationDriver :
        MonoBehaviour,
        IEnemyVisualSemanticPresentationDriver
    {
        private const int LegacySummonPresentationKindValue = 3;
        private const float MinimumDurationSeconds = 0.0001f;

        [SerializeField] private EnemyUtilityPresentationKind utilityKind =
            (EnemyUtilityPresentationKind)LegacySummonPresentationKindValue;
        [SerializeField] private float windupDurationSeconds = 1.7f;
        [SerializeField] private float windupPeakTimeSeconds = 1.05f;
        [SerializeField] private float recoverDurationSeconds = 0.7f;
        [SerializeField] private float peakScaleMultiplier = 1.1f;
        [SerializeField] private float windupEndScaleMultiplier = 0.75f;
        [SerializeField] private float recoverEndScaleMultiplier = 1f;

        private GameplayEntityView _view;
        private Transform _modelRoot;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _hasBaseLocalScale;
        private bool _presentationPlaybackSuppressed;
        private ScalePulsePhase _phase;
        private float _elapsedSeconds;
        private float _phaseStartScaleMultiplier = 1f;
        private float _currentScaleMultiplier = 1f;

        public bool IsPlaying => _phase == ScalePulsePhase.Windup || _phase == ScalePulsePhase.Recover;

        public EnemyUtilityPresentationKind UtilityKind => utilityKind;

        public float CurrentScaleMultiplier => _currentScaleMultiplier;

        public float WindupDurationSeconds => windupDurationSeconds;

        public float RecoverDurationSeconds => recoverDurationSeconds;

        public float PeakScaleMultiplier => peakScaleMultiplier;

        public float WindupEndScaleMultiplier => windupEndScaleMultiplier;

        public void Apply(in EnemyViewPresentationState state)
        {
            if (state.DidDie)
            {
                NormalizeToBaseScale();
                return;
            }

            if (!MatchesPresentationKind(state))
            {
                return;
            }

            if (IsSummonPresentationKind
                    ? state.SummonCanceledThisTick
                    : state.UtilityCanceledThisTick)
            {
                NormalizeToBaseScale();
                return;
            }

            if (IsSummonPresentationKind
                    ? state.StartedSummonWindupThisTick
                    : state.StartedUtilityWindupThisTick)
            {
                BeginWindup();
            }

            if (IsSummonPresentationKind
                    ? state.StartedSummonRecoverThisTick
                    : state.StartedRecoveryThisTick)
            {
                BeginRecover();
            }
        }

        public void Advance(float deltaTime)
        {
            if (_presentationPlaybackSuppressed)
            {
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            if (_phase == ScalePulsePhase.Windup)
            {
                AdvanceWindup();
                return;
            }

            AdvanceRecover();
        }

        public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state)
        {
            _presentationPlaybackSuppressed = state.ShouldPauseAutonomousPresentation;
        }

        public void NormalizeToBaseScale()
        {
            if (!TryResolveModelRoot())
            {
                ResetState();
                return;
            }

            if (!_hasBaseLocalScale)
            {
                CaptureBaseLocalScale();
            }

            _modelRoot.localScale = _baseLocalScale;
            ResetState();
        }

        private bool IsSummonPresentationKind =>
            (int)utilityKind == LegacySummonPresentationKindValue;

        private bool MatchesPresentationKind(in EnemyViewPresentationState state)
        {
            return IsSummonPresentationKind
                ? state.StartedSummonWindupThisTick ||
                  state.StartedSummonRecoverThisTick ||
                  state.SummonCanceledThisTick
                : state.UtilityPresentationKind == utilityKind;
        }

        private void BeginWindup()
        {
            if (!TryResolveModelRoot())
            {
                return;
            }

            _phaseStartScaleMultiplier = _phase == ScalePulsePhase.Idle
                ? 1f
                : _currentScaleMultiplier;
            _elapsedSeconds = 0f;
            _phase = ScalePulsePhase.Windup;
            ApplyScaleMultiplier(_phaseStartScaleMultiplier);
        }

        private void BeginRecover()
        {
            if (!TryResolveModelRoot())
            {
                return;
            }

            _phaseStartScaleMultiplier = _phase == ScalePulsePhase.Idle
                ? windupEndScaleMultiplier
                : _currentScaleMultiplier;
            _elapsedSeconds = 0f;
            _phase = ScalePulsePhase.Recover;
            ApplyScaleMultiplier(_phaseStartScaleMultiplier);
        }

        private void AdvanceWindup()
        {
            var peakTime = Mathf.Clamp(
                windupPeakTimeSeconds,
                MinimumDurationSeconds,
                Mathf.Max(MinimumDurationSeconds, windupDurationSeconds));
            var windupDuration = Mathf.Max(MinimumDurationSeconds, windupDurationSeconds);

            if (_elapsedSeconds < peakTime)
            {
                var progress = Mathf.Clamp01(_elapsedSeconds / peakTime);
                ApplyScaleMultiplier(Mathf.LerpUnclamped(
                    _phaseStartScaleMultiplier,
                    peakScaleMultiplier,
                    EaseOutCubic(progress)));
                return;
            }

            var shrinkDuration = Mathf.Max(MinimumDurationSeconds, windupDuration - peakTime);
            var shrinkProgress = Mathf.Clamp01((_elapsedSeconds - peakTime) / shrinkDuration);
            ApplyScaleMultiplier(Mathf.LerpUnclamped(
                peakScaleMultiplier,
                windupEndScaleMultiplier,
                EaseInOutCubic(shrinkProgress)));

            if (_elapsedSeconds >= windupDuration)
            {
                ApplyScaleMultiplier(windupEndScaleMultiplier);
                _phase = ScalePulsePhase.WindupHold;
                _elapsedSeconds = 0f;
            }
        }

        private void AdvanceRecover()
        {
            var recoverDuration = Mathf.Max(MinimumDurationSeconds, recoverDurationSeconds);
            var progress = Mathf.Clamp01(_elapsedSeconds / recoverDuration);
            ApplyScaleMultiplier(Mathf.LerpUnclamped(
                _phaseStartScaleMultiplier,
                recoverEndScaleMultiplier,
                EaseOutCubic(progress)));

            if (_elapsedSeconds >= recoverDuration)
            {
                NormalizeToBaseScale();
            }
        }

        private bool TryResolveModelRoot()
        {
            if (_modelRoot != null)
            {
                return true;
            }

            if (_view == null)
            {
                _view = GetComponent<GameplayEntityView>();
            }

            if (_view == null)
            {
                return false;
            }

            _modelRoot = _view.ModelRoot;
            CaptureBaseLocalScale();
            return _modelRoot != null;
        }

        private void CaptureBaseLocalScale()
        {
            if (_modelRoot == null)
            {
                return;
            }

            _baseLocalScale = _modelRoot.localScale;
            _hasBaseLocalScale = true;
        }

        private void ApplyScaleMultiplier(float scaleMultiplier)
        {
            if (!TryResolveModelRoot())
            {
                return;
            }

            if (!_hasBaseLocalScale)
            {
                CaptureBaseLocalScale();
            }

            _currentScaleMultiplier = Mathf.Max(0.0001f, scaleMultiplier);
            _modelRoot.localScale = _baseLocalScale * _currentScaleMultiplier;
        }

        private void ResetState()
        {
            _presentationPlaybackSuppressed = false;
            _phase = ScalePulsePhase.Idle;
            _elapsedSeconds = 0f;
            _phaseStartScaleMultiplier = 1f;
            _currentScaleMultiplier = 1f;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private void OnDisable()
        {
            NormalizeToBaseScale();
        }

        private void OnDestroy()
        {
            NormalizeToBaseScale();
        }

        private enum ScalePulsePhase
        {
            Idle = 0,
            Windup = 1,
            WindupHold = 2,
            Recover = 3,
        }
    }
}
