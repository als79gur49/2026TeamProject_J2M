using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GravityFieldLockedTargetVisualTargetView :
        MonoBehaviour,
        IGravityFieldLockedTargetVisualTarget,
        IGravityFieldLockedTargetRevealVisualTarget,
        IGravityFieldLockedBoxOneShotVisualTarget
    {
        [SerializeField] private GameObject lockedVisualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private string lockedBoolName = "GravityFieldLockedTarget";
        [SerializeField] private string lockedWeightFloatName = "GravityFieldLockedTargetWeight";
        [SerializeField] private string lockedBoxOneShotAnimatorTrigger = "GravityFieldLockedBox";
        [SerializeField] private ParticleSystem lockedParticles;
        [SerializeField] private ParticleSystem lockedBoxOneShotParticles;
        [SerializeField] private UnityEvent lockedTargetApplied;
        [SerializeField] private UnityEvent lockedTargetCleared;
        [SerializeField] private UnityEvent lockedBoxOneShot;
        [SerializeField] private Renderer[] dimRenderers;
        [SerializeField, Range(0f, 1f)] private float lockedDimWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float unlockedDimWeight = 0f;
        [SerializeField, Range(0f, 1f)] private float gravityFieldDimFactor = 0.55f;
        [SerializeField] private Color gravityFieldLockedTint = new(0.45f, 0.55f, 0.85f, 1f);
        [SerializeField, Range(0f, 1f)] private float gravityFieldTintStrength = 0.15f;
        [SerializeField, Min(0f)] private float lockRevealInSeconds = 0.234f;
        [SerializeField, Min(0f)] private float lockRevealOutSeconds = 0.208f;
        [SerializeField, Range(0.001f, 0.5f)] private float gravityFieldLockEdgeWidth = 0.08f;
        [SerializeField] private AnimationCurve lockRevealCurve;

        private static readonly int GravityFieldLockedWeightId = Shader.PropertyToID("_GravityFieldLockedWeight");
        private static readonly int GravityFieldLockRevealId = Shader.PropertyToID("_GravityFieldLockReveal");
        private static readonly int GravityFieldLockEdgeWidthId = Shader.PropertyToID("_GravityFieldLockEdgeWidth");
        private static readonly int GravityFieldLockedTintId = Shader.PropertyToID("_GravityFieldLockedTint");
        private static readonly int GravityFieldDimFactorId = Shader.PropertyToID("_GravityFieldDimFactor");
        private static readonly int GravityFieldTintStrengthId = Shader.PropertyToID("_GravityFieldTintStrength");
        private const float RevealEpsilon = 0.0001f;

        private readonly HashSet<int> _activeEmitterEntityIds = new();
        private MaterialPropertyBlock _propertyBlock;
        private float _currentLockReveal;
        private float _targetLockReveal;
        private float _fadeOutElapsedSeconds;
        private bool _isFadeOutActive;
        private int _debugApplyCount;
        private int _debugClearCount;
        private int _debugLastEmitterEntityId;
        private int _debugLockedBoxOneShotCount;
        private int _debugLastLockedBoxEmitterEntityId;
        private int _debugLastLockedBoxTargetEntityId;
        private SurfaceCell _debugLastLockedBoxEmitterCell;

        public int DebugActiveEmitterCount => _activeEmitterEntityIds.Count;

        public float DebugCurrentLockReveal => _currentLockReveal;

        public float DebugTargetLockReveal => _targetLockReveal;

        public int DebugApplyCount => _debugApplyCount;

        public int DebugClearCount => _debugClearCount;

        public int DebugLastEmitterEntityId => _debugLastEmitterEntityId;

        public IReadOnlyCollection<int> DebugActiveEmitterEntityIds => _activeEmitterEntityIds;

        public int DebugLockedBoxOneShotCount => _debugLockedBoxOneShotCount;

        public int DebugLastLockedBoxEmitterEntityId => _debugLastLockedBoxEmitterEntityId;

        public int DebugLastLockedBoxTargetEntityId => _debugLastLockedBoxTargetEntityId;

        public SurfaceCell DebugLastLockedBoxEmitterCell => _debugLastLockedBoxEmitterCell;

        public void ApplyGravityFieldLockedTarget(int emitterEntityId)
        {
            if (emitterEntityId <= 0)
            {
                return;
            }

            var wasLocked = _activeEmitterEntityIds.Count > 0;
            if (!_activeEmitterEntityIds.Add(emitterEntityId))
            {
                return;
            }

            _debugApplyCount++;
            _debugLastEmitterEntityId = emitterEntityId;

            _targetLockReveal = 1f;
            _isFadeOutActive = false;
            _fadeOutElapsedSeconds = 0f;
            RefreshVisualState(isLockedGateActive: true);

            if (!wasLocked)
            {
                lockedTargetApplied?.Invoke();
            }
        }

        public void ClearGravityFieldLockedTarget(int emitterEntityId)
        {
            if (emitterEntityId <= 0)
            {
                return;
            }

            var wasLocked = _activeEmitterEntityIds.Count > 0;
            if (!_activeEmitterEntityIds.Remove(emitterEntityId))
            {
                return;
            }

            _debugClearCount++;
            _debugLastEmitterEntityId = emitterEntityId;

            if (_activeEmitterEntityIds.Count == 0)
            {
                _targetLockReveal = 0f;
                _isFadeOutActive = true;
                _fadeOutElapsedSeconds = 0f;
                RefreshVisualState(isLockedGateActive: true);
                SetLockedParticles(isLocked: false);
            }
            else
            {
                _targetLockReveal = 1f;
                RefreshVisualState(isLockedGateActive: true);
            }

            if (wasLocked && _activeEmitterEntityIds.Count == 0)
            {
                lockedTargetCleared?.Invoke();
            }
        }

        public void PlayGravityFieldLockedBox(GravityFieldLockedBoxPayload payload)
        {
            if (!payload.IsValid)
            {
                return;
            }

            _debugLockedBoxOneShotCount++;
            _debugLastLockedBoxEmitterEntityId = payload.EmitterEntityId;
            _debugLastLockedBoxTargetEntityId = payload.TargetEntityId;
            _debugLastLockedBoxEmitterCell = payload.EmitterCell;

            TriggerAnimator(lockedBoxOneShotAnimatorTrigger);
            if (lockedBoxOneShotParticles != null)
            {
                lockedBoxOneShotParticles.Play(withChildren: true);
            }

            lockedBoxOneShot?.Invoke();
        }

        public bool UpdateGravityFieldLockedTargetReveal(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            AdvanceCurrentReveal(deltaTime);
            if (_isFadeOutActive)
            {
                _fadeOutElapsedSeconds += deltaTime;
            }

            var shouldKeepLockedGate = ShouldKeepLockedGate();
            if (!shouldKeepLockedGate)
            {
                RefreshVisualState(isLockedGateActive: false);
                return false;
            }

            RefreshVisualState(isLockedGateActive: true);
            return !IsRevealStable() || _isFadeOutActive;
        }

        public void ResetGravityFieldLockedTargetReveal()
        {
            _activeEmitterEntityIds.Clear();
            _currentLockReveal = 0f;
            _targetLockReveal = 0f;
            _fadeOutElapsedSeconds = 0f;
            _isFadeOutActive = false;
            RefreshVisualState(isLockedGateActive: false);
        }

        private void OnDisable()
        {
            ResetGravityFieldLockedTargetReveal();
        }

        private void RefreshVisualState(bool isLockedGateActive)
        {
            SetOptionalActive(lockedVisualRoot, isLockedGateActive);
            SetAnimatorBool(lockedBoolName, isLockedGateActive);
            SetAnimatorFloat(lockedWeightFloatName, isLockedGateActive ? 1f : 0f);
            if (isLockedGateActive && _activeEmitterEntityIds.Count > 0)
            {
                SetLockedParticles(isLocked: true);
            }
            else if (!isLockedGateActive)
            {
                SetLockedParticles(isLocked: false);
            }

            ApplyDimming(
                ResolveLockedGateWeight(isLockedGateActive),
                isLockedGateActive ? EvaluateLockReveal(_currentLockReveal) : 0f);
        }

        private void AdvanceCurrentReveal(float deltaTime)
        {
            var targetReveal = Mathf.Clamp01(_targetLockReveal);
            var duration = targetReveal > _currentLockReveal
                ? lockRevealInSeconds
                : lockRevealOutSeconds;

            if (duration <= RevealEpsilon)
            {
                _currentLockReveal = targetReveal;
                return;
            }

            _currentLockReveal = Mathf.MoveTowards(
                _currentLockReveal,
                targetReveal,
                deltaTime / duration);
        }

        private bool ShouldKeepLockedGate()
        {
            if (_activeEmitterEntityIds.Count > 0 ||
                _targetLockReveal > RevealEpsilon ||
                _currentLockReveal > RevealEpsilon)
            {
                return true;
            }

            if (!_isFadeOutActive)
            {
                return false;
            }

            var fadeOutDuration = Mathf.Max(0f, lockRevealOutSeconds);
            if (_fadeOutElapsedSeconds + RevealEpsilon >= fadeOutDuration)
            {
                _isFadeOutActive = false;
                return false;
            }

            return true;
        }

        private bool IsRevealStable()
        {
            return Mathf.Abs(_currentLockReveal - _targetLockReveal) <= RevealEpsilon;
        }

        private float EvaluateLockReveal(float reveal)
        {
            var clampedReveal = Mathf.Clamp01(reveal);
            if (lockRevealCurve == null ||
                lockRevealCurve.length == 0)
            {
                return clampedReveal;
            }

            return Mathf.Clamp01(lockRevealCurve.Evaluate(clampedReveal));
        }

        private float ResolveLockedGateWeight(bool isLockedGateActive)
        {
            // Keep legacy serialized weights readable while the shader gate stays binary.
            _ = lockedDimWeight;
            _ = unlockedDimWeight;
            return isLockedGateActive ? 1f : 0f;
        }

        private void ApplyDimming(float weight, float reveal)
        {
            if (dimRenderers == null ||
                dimRenderers.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            for (var i = 0; i < dimRenderers.Length; i++)
            {
                var targetRenderer = dimRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(GravityFieldLockedWeightId, weight);
                _propertyBlock.SetFloat(GravityFieldLockRevealId, reveal);
                _propertyBlock.SetFloat(GravityFieldLockEdgeWidthId, gravityFieldLockEdgeWidth);
                _propertyBlock.SetColor(GravityFieldLockedTintId, gravityFieldLockedTint);
                _propertyBlock.SetFloat(GravityFieldDimFactorId, gravityFieldDimFactor);
                _propertyBlock.SetFloat(GravityFieldTintStrengthId, gravityFieldTintStrength);
                targetRenderer.SetPropertyBlock(_propertyBlock);
                _propertyBlock.Clear();
            }
        }

        private void SetLockedParticles(bool isLocked)
        {
            if (lockedParticles == null)
            {
                return;
            }

            if (isLocked)
            {
                lockedParticles.Play(withChildren: true);
                return;
            }

            lockedParticles.Stop(
                withChildren: true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void SetOptionalActive(GameObject target, bool isActive)
        {
            if (target != null &&
                target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private void SetAnimatorBool(string parameterName, bool value)
        {
            if (CanUseAnimatorParameter(parameterName))
            {
                animator.SetBool(Animator.StringToHash(parameterName), value);
            }
        }

        private void SetAnimatorFloat(string parameterName, float value)
        {
            if (CanUseAnimatorParameter(parameterName))
            {
                animator.SetFloat(Animator.StringToHash(parameterName), value);
            }
        }

        private void TriggerAnimator(string parameterName)
        {
            if (CanUseAnimatorParameter(parameterName))
            {
                animator.SetTrigger(Animator.StringToHash(parameterName));
            }
        }

        private bool CanUseAnimatorParameter(string parameterName)
        {
            return animator != null &&
                   animator.runtimeAnimatorController != null &&
                   !string.IsNullOrWhiteSpace(parameterName);
        }
    }
}
