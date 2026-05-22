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

        private static readonly int GravityFieldLockedWeightId = Shader.PropertyToID("_GravityFieldLockedWeight");
        private static readonly int GravityFieldLockedTintId = Shader.PropertyToID("_GravityFieldLockedTint");
        private static readonly int GravityFieldDimFactorId = Shader.PropertyToID("_GravityFieldDimFactor");
        private static readonly int GravityFieldTintStrengthId = Shader.PropertyToID("_GravityFieldTintStrength");

        private readonly HashSet<int> _activeEmitterEntityIds = new();
        private MaterialPropertyBlock _propertyBlock;
        private int _debugApplyCount;
        private int _debugClearCount;
        private int _debugLastEmitterEntityId;
        private int _debugLockedBoxOneShotCount;
        private int _debugLastLockedBoxEmitterEntityId;
        private int _debugLastLockedBoxTargetEntityId;
        private SurfaceCell _debugLastLockedBoxEmitterCell;

        public int DebugActiveEmitterCount => _activeEmitterEntityIds.Count;

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

            RefreshVisualState();

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

            RefreshVisualState();

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

        private void OnDisable()
        {
            _activeEmitterEntityIds.Clear();
            RefreshVisualState();
        }

        private void RefreshVisualState()
        {
            var isLocked = _activeEmitterEntityIds.Count > 0;
            SetOptionalActive(lockedVisualRoot, isLocked);
            SetAnimatorBool(lockedBoolName, isLocked);
            SetAnimatorFloat(lockedWeightFloatName, isLocked ? 1f : 0f);
            SetLockedParticles(isLocked);
            ApplyDimming(isLocked ? lockedDimWeight : unlockedDimWeight);
        }

        private void ApplyDimming(float weight)
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
