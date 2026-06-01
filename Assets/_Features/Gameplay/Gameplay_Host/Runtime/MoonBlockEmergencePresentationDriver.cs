using DG.Tweening;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class MoonBlockEmergencePresentationDriver : MonoBehaviour
    {
        private GameplayEntityView _view;
        private Transform _modelRoot;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale = Vector3.one;
        private Sequence _sequence;
        private float _durationSeconds;
        private float _elapsedSeconds;
        private bool _isPlaying;
        private bool _hasBaseState;
        private bool _hasPreparedHiddenReady;
        private int _debugPlayCount;

        [SerializeField] private Ease ease = Ease.OutBack;

        public bool IsPlaying => _isPlaying;

        public int DebugPlayCount => _debugPlayCount;

        public int DebugEntityId => _view != null ? _view.EntityId : 0;

        public float DebugDurationSeconds => _durationSeconds;

        public float DebugElapsedSeconds => _elapsedSeconds;

        public Vector3 DebugModelRootLocalPosition => _modelRoot != null ? _modelRoot.localPosition : Vector3.zero;

        public Vector3 DebugModelRootLocalScale => _modelRoot != null ? _modelRoot.localScale : Vector3.zero;

        public void Play(
            float launchDelaySeconds,
            float launchDurationSeconds,
            float startScaleMultiplier,
            float launchScaleMultiplier,
            Vector3 startLocalOffset,
            Vector3 launchLocalOffset)
        {
            if (_isPlaying)
            {
                return;
            }

            _view = _view != null ? _view : GetComponent<GameplayEntityView>();
            if (_view == null)
            {
                return;
            }

            _modelRoot = _view.ModelRoot;
            if (!_hasPreparedHiddenReady)
            {
                _baseLocalPosition = _modelRoot.localPosition;
                _baseLocalScale = _modelRoot.localScale;
                _hasBaseState = true;
            }

            _hasPreparedHiddenReady = false;
            var delaySeconds = Mathf.Max(0f, launchDelaySeconds);
            var motionDurationSeconds = Mathf.Max(0.0001f, launchDurationSeconds);
            var liftDurationSeconds = motionDurationSeconds * 0.55f;
            var settleDurationSeconds = Mathf.Max(0.0001f, motionDurationSeconds - liftDurationSeconds);
            _durationSeconds = delaySeconds + liftDurationSeconds + settleDurationSeconds;
            _elapsedSeconds = 0f;
            _modelRoot.localPosition = _baseLocalPosition + startLocalOffset;
            _modelRoot.localScale = _baseLocalScale * Mathf.Max(0f, startScaleMultiplier);
            KillTween();
            _sequence = DOTween.Sequence()
                .SetAutoKill(false)
                .Pause();
            if (delaySeconds > 0f)
            {
                _sequence.AppendInterval(delaySeconds);
            }

            _sequence.Append(
                _modelRoot
                    .DOLocalMove(_baseLocalPosition + launchLocalOffset, liftDurationSeconds)
                    .SetEase(Ease.OutQuad));
            _sequence.Join(
                _modelRoot
                    .DOScale(_baseLocalScale * Mathf.Max(0.0001f, launchScaleMultiplier), liftDurationSeconds)
                    .SetEase(ease));
            _sequence.Append(
                _modelRoot
                    .DOLocalMove(_baseLocalPosition, settleDurationSeconds)
                    .SetEase(Ease.InQuad));
            _sequence.Join(
                _modelRoot
                    .DOScale(_baseLocalScale, settleDurationSeconds)
                    .SetEase(Ease.OutQuad));
            _isPlaying = true;
            _debugPlayCount++;
        }

        public void PrepareHiddenReady()
        {
            if (_isPlaying)
            {
                return;
            }

            _view = _view != null ? _view : GetComponent<GameplayEntityView>();
            if (_view == null)
            {
                return;
            }

            _modelRoot = _view.ModelRoot;
            if (_modelRoot == null)
            {
                return;
            }

            _baseLocalPosition = _modelRoot.localPosition;
            _baseLocalScale = _modelRoot.localScale;
            _hasBaseState = true;
            _hasPreparedHiddenReady = true;
            _modelRoot.localPosition = _baseLocalPosition;
            _modelRoot.localScale = Vector3.zero;
        }

        public void Advance(float deltaTime)
        {
            if (!_isPlaying)
            {
                return;
            }

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            _sequence?.Goto(Mathf.Min(_elapsedSeconds, _durationSeconds), andPlay: false);
            if (_elapsedSeconds >= _durationSeconds)
            {
                NormalizeToFinalState();
            }
        }

        public void NormalizeToFinalState()
        {
            KillTween();
            if (_modelRoot == null)
            {
                _view = _view != null ? _view : GetComponent<GameplayEntityView>();
                _modelRoot = _view != null ? _view.ModelRoot : null;
            }

            if (_modelRoot != null)
            {
                if (!_hasBaseState)
                {
                    _baseLocalPosition = _modelRoot.localPosition;
                    _baseLocalScale = _modelRoot.localScale;
                    _hasBaseState = true;
                }

                _modelRoot.localPosition = _baseLocalPosition;
                _modelRoot.localScale = _baseLocalScale;
            }

            _isPlaying = false;
            _elapsedSeconds = 0f;
        }

        private void KillTween()
        {
            if (_sequence == null)
            {
                return;
            }

            _sequence.Kill(complete: false);
            _sequence = null;
        }

        private void OnDisable()
        {
            NormalizeToFinalState();
        }

        private void OnDestroy()
        {
            NormalizeToFinalState();
        }
    }
}
