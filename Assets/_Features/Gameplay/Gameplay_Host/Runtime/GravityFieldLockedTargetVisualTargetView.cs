using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GravityFieldLockedTargetVisualTargetView :
        MonoBehaviour,
        IGravityFieldLockedTargetVisualTarget
    {
        [SerializeField] private GameObject lockedVisualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private string lockedBoolName = "GravityFieldLockedTarget";
        [SerializeField] private string lockedWeightFloatName = "GravityFieldLockedTargetWeight";
        [SerializeField] private ParticleSystem lockedParticles;
        [SerializeField] private UnityEvent lockedTargetApplied;
        [SerializeField] private UnityEvent lockedTargetCleared;

        private readonly HashSet<int> _activeEmitterEntityIds = new();
        private int _debugApplyCount;
        private int _debugClearCount;
        private int _debugLastEmitterEntityId;

        public int DebugActiveEmitterCount => _activeEmitterEntityIds.Count;

        public int DebugApplyCount => _debugApplyCount;

        public int DebugClearCount => _debugClearCount;

        public int DebugLastEmitterEntityId => _debugLastEmitterEntityId;

        public IReadOnlyCollection<int> DebugActiveEmitterEntityIds => _activeEmitterEntityIds;

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

        private bool CanUseAnimatorParameter(string parameterName)
        {
            return animator != null &&
                   animator.runtimeAnimatorController != null &&
                   !string.IsNullOrWhiteSpace(parameterName);
        }
    }
}
