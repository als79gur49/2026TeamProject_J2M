using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class EnemyFloatingPresentationDriver :
        MonoBehaviour,
        IEnemyVisualSemanticPresentationDriver
    {
        private const float MinimumAxisMagnitudeSquared = 0.000001f;

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float amplitude = 0.12f;
        [SerializeField] private float frequencyHz = 0.6f;
        [SerializeField] private float phaseOffsetSeconds;

        private Vector3 _baseLocalPosition;
        private bool _hasBaseLocalPosition;
        private bool _isSuspended;
        private float _elapsedSeconds;

        public Transform Target => target;

        public Vector3 LocalAxis => localAxis;

        public float Amplitude => amplitude;

        public float FrequencyHz => frequencyHz;

        public float PhaseOffsetSeconds => phaseOffsetSeconds;

        public Vector3 CurrentOffset { get; private set; }

        public bool IsSuspended => _isSuspended;

        private void Reset()
        {
            TryResolveTarget();
        }

        private void Awake()
        {
            CaptureBaseLocalPosition();
        }

        private void OnEnable()
        {
            CaptureBaseLocalPosition();
        }

        private void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        private void OnDisable()
        {
            RestoreBaseLocalPosition();
        }

        private void OnValidate()
        {
            amplitude = Mathf.Max(0f, amplitude);
            frequencyHz = Mathf.Max(0f, frequencyHz);
        }

        public void Advance(float deltaTime)
        {
            if (!TryResolveTarget())
            {
                return;
            }

            if (_isSuspended)
            {
                RestoreBaseLocalPosition();
                return;
            }

            if (!_hasBaseLocalPosition)
            {
                CaptureBaseLocalPosition();
            }

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            CurrentOffset = ResolveOffset(_elapsedSeconds);
            target.localPosition = _baseLocalPosition + CurrentOffset;
        }

        public void Apply(in EnemyVisualSemanticState state)
        {
            SetSuspended(state.ShouldPauseAutonomousPresentation);
        }

        public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state)
        {
            Apply(state);
        }

        public void SetSuspended(bool suspended)
        {
            if (_isSuspended == suspended)
            {
                if (_isSuspended)
                {
                    RestoreBaseLocalPosition();
                }

                return;
            }

            _isSuspended = suspended;
            if (_isSuspended)
            {
                RestoreBaseLocalPosition();
                return;
            }

            CaptureBaseLocalPosition();
        }

        public void CaptureBaseLocalPosition()
        {
            if (!TryResolveTarget())
            {
                _hasBaseLocalPosition = false;
                CurrentOffset = Vector3.zero;
                return;
            }

            _baseLocalPosition = target.localPosition;
            _hasBaseLocalPosition = true;
            _elapsedSeconds = 0f;
            CurrentOffset = Vector3.zero;
        }

        public void RestoreBaseLocalPosition()
        {
            if (target != null && _hasBaseLocalPosition)
            {
                target.localPosition = _baseLocalPosition;
            }

            _elapsedSeconds = 0f;
            CurrentOffset = Vector3.zero;
        }

        private Vector3 ResolveOffset(float elapsedSeconds)
        {
            var resolvedAmplitude = Mathf.Max(0f, amplitude);
            var resolvedFrequency = Mathf.Max(0f, frequencyHz);
            if (resolvedAmplitude <= 0f || resolvedFrequency <= 0f)
            {
                return Vector3.zero;
            }

            var phase = (elapsedSeconds + phaseOffsetSeconds) * resolvedFrequency * Mathf.PI * 2f;
            return ResolveAxis() * (Mathf.Sin(phase) * resolvedAmplitude);
        }

        private Vector3 ResolveAxis()
        {
            return localAxis.sqrMagnitude > MinimumAxisMagnitudeSquared
                ? localAxis.normalized
                : Vector3.up;
        }

        private bool TryResolveTarget()
        {
            if (target != null)
            {
                return true;
            }

            var view = GetComponent<GameplayEntityView>();
            if (view == null || view.ModelRoot == null)
            {
                return false;
            }

            target = view.ModelRoot;
            return true;
        }
    }
}
