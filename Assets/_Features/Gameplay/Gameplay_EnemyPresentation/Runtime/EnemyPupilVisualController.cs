using System;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class EnemyPupilVisualController : MonoBehaviour
    {
        private const float UseSharedMaterialBaseBorder = -1f;
        private static readonly int BorderHash = Shader.PropertyToID("_Border");

        [SerializeField] private EnemyAnimatorDriver animatorDriver;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private float baseBorderOverride = UseSharedMaterialBaseBorder;
        [SerializeField] private float windupExpandedBorder = 0.64f;
        [SerializeField] private float attackContractedBorder = 0.22f;
        [SerializeField] [Range(0.5f, 0.98f)] private float windupExpandRatio = 0.82f;
        [SerializeField] private float fallbackWindupDurationSeconds = 1f;
        [SerializeField] private float fallbackRecoverDurationSeconds = 1f;
        [SerializeField] private float attackHoldSeconds = 0.04f;

        private MaterialPropertyBlock _propertyBlock;
        private RendererBorderEntry[] _rendererEntries = Array.Empty<RendererBorderEntry>();
        private PupilPhase _phase;
        private float _phaseElapsedSeconds;
        private float _phaseDurationSeconds;
        private bool _pendingRecover;
        private float _pendingRecoverDurationSeconds;
        private float _recoverStartBorder;
        private bool _isGameplayPresentationPaused;
        private bool _signalCountsInitialized;
        private int _lastWindupSignalCount;
        private int _lastAttackSignalCount;
        private int _lastRecoverySignalCount;

        public float CurrentBorder { get; private set; }

        private void Reset()
        {
            animatorDriver = GetComponent<EnemyAnimatorDriver>();
        }

        private void Awake()
        {
            CacheDependencies();
            InitializeSignalCounts();
            ApplyCurrentBorder();
        }

        private void OnEnable()
        {
            CacheDependencies();
            InitializeSignalCounts();
            ApplyCurrentBorder();
        }

        private void OnDisable()
        {
            _phase = PupilPhase.None;
            _phaseElapsedSeconds = 0f;
            _phaseDurationSeconds = 0f;
            _pendingRecover = false;
            _pendingRecoverDurationSeconds = 0f;
            _recoverStartBorder = 0f;
            _signalCountsInitialized = false;

            CacheDependencies();
            ApplyCurrentBorder();
        }

        private void Update()
        {
            if (_isGameplayPresentationPaused)
            {
                return;
            }

            Advance(Time.deltaTime);
        }

        internal void Advance(float deltaTime)
        {
            if (_isGameplayPresentationPaused)
            {
                return;
            }

            CacheDependencies();
            SyncDriverSignals();
            AdvancePhase(Mathf.Max(0f, deltaTime));
            ApplyCurrentBorder();
        }

        public void SetPresentationPaused(bool paused)
        {
            _isGameplayPresentationPaused = paused;
        }

        private void CacheDependencies()
        {
            if (animatorDriver == null)
            {
                animatorDriver = GetComponent<EnemyAnimatorDriver>();
            }

            CacheRenderers();
        }

        private void InitializeSignalCounts()
        {
            if (animatorDriver == null)
            {
                return;
            }

            _lastWindupSignalCount = animatorDriver.WindupSignalCount;
            _lastAttackSignalCount = animatorDriver.AttackSignalCount;
            _lastRecoverySignalCount = animatorDriver.RecoverySignalCount;
            _signalCountsInitialized = true;
        }

        private void CacheRenderers()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            var validCount = 0;
            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null || !sharedMaterial.HasProperty(BorderHash))
                {
                    continue;
                }

                validCount++;
            }

            if (_rendererEntries.Length == validCount)
            {
                var unchanged = true;
                var entryIndex = 0;
                for (var i = 0; i < targetRenderers.Length; i++)
                {
                    var renderer = targetRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    var sharedMaterial = renderer.sharedMaterial;
                    if (sharedMaterial == null || !sharedMaterial.HasProperty(BorderHash))
                    {
                        continue;
                    }

                    if (_rendererEntries[entryIndex].Renderer != renderer)
                    {
                        unchanged = false;
                        break;
                    }

                    entryIndex++;
                }

                if (unchanged)
                {
                    return;
                }
            }

            _rendererEntries = new RendererBorderEntry[validCount];
            var targetIndex = 0;
            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null || !sharedMaterial.HasProperty(BorderHash))
                {
                    continue;
                }

                _rendererEntries[targetIndex++] = new RendererBorderEntry(
                    renderer,
                    sharedMaterial.GetFloat(BorderHash));
            }
        }

        private void SyncDriverSignals()
        {
            if (animatorDriver == null)
            {
                return;
            }

            if (!_signalCountsInitialized)
            {
                _lastWindupSignalCount = animatorDriver.WindupSignalCount;
                _lastAttackSignalCount = animatorDriver.AttackSignalCount;
                _lastRecoverySignalCount = animatorDriver.RecoverySignalCount;
                _signalCountsInitialized = true;
                return;
            }

            var windupStarted = animatorDriver.WindupSignalCount != _lastWindupSignalCount;
            var attackExecuted = animatorDriver.AttackSignalCount != _lastAttackSignalCount;
            var recoveryStarted = animatorDriver.RecoverySignalCount != _lastRecoverySignalCount;

            _lastWindupSignalCount = animatorDriver.WindupSignalCount;
            _lastAttackSignalCount = animatorDriver.AttackSignalCount;
            _lastRecoverySignalCount = animatorDriver.RecoverySignalCount;

            if (windupStarted)
            {
                BeginWindup(ResolveDuration(
                    animatorDriver.CurrentPresentationDurationSeconds,
                    fallbackWindupDurationSeconds));
            }

            if (attackExecuted)
            {
                var recoverDurationSeconds = recoveryStarted
                    ? ResolveDuration(
                        animatorDriver.CurrentPresentationDurationSeconds,
                        fallbackRecoverDurationSeconds)
                    : 0f;

                if (recoveryStarted &&
                    animatorDriver.CurrentActiveActionKind == EnemyActionKind.ForwardCellProjectile)
                {
                    BeginRecover(recoverDurationSeconds);
                    return;
                }

                BeginAttackHold();

                if (recoveryStarted)
                {
                    _pendingRecover = true;
                    _pendingRecoverDurationSeconds = recoverDurationSeconds;
                }

                return;
            }

            if (recoveryStarted)
            {
                BeginRecover(ResolveDuration(
                    animatorDriver.CurrentPresentationDurationSeconds,
                    fallbackRecoverDurationSeconds));
            }
        }

        private void AdvancePhase(float deltaTime)
        {
            switch (_phase)
            {
                case PupilPhase.None:
                    return;

                case PupilPhase.Windup:
                    _phaseElapsedSeconds = Mathf.Min(_phaseDurationSeconds, _phaseElapsedSeconds + deltaTime);
                    if (_phaseElapsedSeconds >= _phaseDurationSeconds)
                    {
                        _phase = PupilPhase.Contracted;
                        _phaseElapsedSeconds = 0f;
                        _phaseDurationSeconds = 0f;
                    }

                    return;

                case PupilPhase.AttackHold:
                    _phaseElapsedSeconds = Mathf.Min(_phaseDurationSeconds, _phaseElapsedSeconds + deltaTime);
                    if (_phaseElapsedSeconds < _phaseDurationSeconds)
                    {
                        return;
                    }

                    if (_pendingRecover)
                    {
                        BeginRecover(_pendingRecoverDurationSeconds, attackContractedBorder);
                    }
                    else
                    {
                        _phase = PupilPhase.Contracted;
                        _phaseElapsedSeconds = 0f;
                        _phaseDurationSeconds = 0f;
                    }

                    return;

                case PupilPhase.Contracted:
                    if (_pendingRecover)
                    {
                        BeginRecover(_pendingRecoverDurationSeconds, attackContractedBorder);
                    }

                    return;

                case PupilPhase.Recover:
                    _phaseElapsedSeconds = Mathf.Min(_phaseDurationSeconds, _phaseElapsedSeconds + deltaTime);
                    if (_phaseElapsedSeconds >= _phaseDurationSeconds)
                    {
                        _phase = PupilPhase.None;
                        _phaseElapsedSeconds = 0f;
                        _phaseDurationSeconds = 0f;
                    }

                    return;
            }
        }

        private void ApplyCurrentBorder()
        {
            if (_rendererEntries.Length == 0)
            {
                CurrentBorder = ResolveFallbackBaseBorder();
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (var i = 0; i < _rendererEntries.Length; i++)
            {
                var entry = _rendererEntries[i];
                var renderer = entry.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                var border = ResolveCurrentBorder(entry.DefaultBorder);
                if (i == 0)
                {
                    CurrentBorder = border;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(BorderHash, border);
                renderer.SetPropertyBlock(_propertyBlock);
                _propertyBlock.Clear();
            }
        }

        private float ResolveCurrentBorder(float defaultBorder)
        {
            var baseBorder = ResolveRestingBorder(defaultBorder);

            switch (_phase)
            {
                case PupilPhase.Windup:
                {
                    var duration = Mathf.Max(0.0001f, _phaseDurationSeconds);
                    var normalizedTime = Mathf.Clamp01(_phaseElapsedSeconds / duration);
                    if (normalizedTime < windupExpandRatio)
                    {
                        var expandTime = Mathf.Clamp01(normalizedTime / Mathf.Max(0.0001f, windupExpandRatio));
                        return Mathf.Lerp(baseBorder, windupExpandedBorder, EaseOutSine(expandTime));
                    }

                    var contractTime = Mathf.Clamp01(
                        (normalizedTime - windupExpandRatio) / Mathf.Max(0.0001f, 1f - windupExpandRatio));
                    return Mathf.Lerp(windupExpandedBorder, attackContractedBorder, EaseOutCubic(contractTime));
                }

                case PupilPhase.AttackHold:
                case PupilPhase.Contracted:
                    return attackContractedBorder;

                case PupilPhase.Recover:
                {
                    var duration = Mathf.Max(0.0001f, _phaseDurationSeconds);
                    var normalizedTime = Mathf.Clamp01(_phaseElapsedSeconds / duration);
                    return Mathf.Lerp(_recoverStartBorder, baseBorder, EaseOutSine(normalizedTime));
                }

                default:
                    return baseBorder;
            }
        }

        private float ResolveRestingBorder(float defaultBorder)
        {
            return baseBorderOverride >= 0f
                ? baseBorderOverride
                : defaultBorder;
        }

        private float ResolveFallbackBaseBorder()
        {
            return baseBorderOverride >= 0f
                ? baseBorderOverride
                : 0f;
        }

        private void BeginWindup(float durationSeconds)
        {
            _phase = PupilPhase.Windup;
            _phaseElapsedSeconds = 0f;
            _phaseDurationSeconds = durationSeconds;
            _pendingRecover = false;
            _pendingRecoverDurationSeconds = 0f;
            _recoverStartBorder = 0f;
        }

        private void BeginAttackHold()
        {
            _phase = PupilPhase.AttackHold;
            _phaseElapsedSeconds = 0f;
            _phaseDurationSeconds = Mathf.Max(0f, attackHoldSeconds);
        }

        private void BeginRecover(float durationSeconds)
        {
            BeginRecover(durationSeconds, CurrentBorder);
        }

        private void BeginRecover(float durationSeconds, float startBorder)
        {
            _phase = PupilPhase.Recover;
            _phaseElapsedSeconds = 0f;
            _phaseDurationSeconds = durationSeconds;
            _recoverStartBorder = startBorder;
            _pendingRecover = false;
            _pendingRecoverDurationSeconds = 0f;
        }

        private static float ResolveDuration(float preferredDurationSeconds, float fallbackDurationSeconds)
        {
            return preferredDurationSeconds > 0f
                ? preferredDurationSeconds
                : Mathf.Max(0.0001f, fallbackDurationSeconds);
        }

        private static float EaseOutSine(float value)
        {
            return Mathf.Sin(Mathf.Clamp01(value) * Mathf.PI * 0.5f);
        }

        private static float EaseOutCubic(float value)
        {
            var clampedValue = 1f - Mathf.Clamp01(value);
            return 1f - (clampedValue * clampedValue * clampedValue);
        }

        private readonly struct RendererBorderEntry
        {
            public RendererBorderEntry(Renderer renderer, float defaultBorder)
            {
                Renderer = renderer;
                DefaultBorder = defaultBorder;
            }

            public Renderer Renderer { get; }

            public float DefaultBorder { get; }
        }

        private enum PupilPhase
        {
            None = 0,
            Windup = 1,
            AttackHold = 2,
            Contracted = 3,
            Recover = 4,
        }
    }
}
