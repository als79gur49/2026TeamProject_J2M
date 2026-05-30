using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GravityFieldVisualTargetView :
        MonoBehaviour,
        IGravityFieldActivatedVisualTarget,
        IGravityFieldExpiredVisualTarget,
        IGravityFieldContinuousVisualTarget,
        IGameplayPresentationPausable
    {
        [SerializeField] private GameObject activeVisualRoot;
        [SerializeField] private GameObject chargingVisualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private string activeBoolName = "GravityFieldActive";
        [SerializeField] private string chargingBoolName = "GravityFieldCharging";
        [SerializeField] private string progressFloatName = "GravityFieldProgress";
        [SerializeField] private string activatedTriggerName = "GravityFieldActivated";
        [SerializeField] private string expiredTriggerName = "GravityFieldExpired";
        [SerializeField] private ParticleSystem activeAuraParticles;
        [SerializeField] private ParticleSystem activatedParticles;
        [SerializeField] private ParticleSystem expiredParticles;
        [SerializeField] private GameObject[] areaCellSlots;
        [SerializeField] private UnityEvent activatedPlayed;
        [SerializeField] private UnityEvent expiredPlayed;
        [SerializeField] private UnityEvent continuousStateApplied;
        [SerializeField] private UnityEvent continuousStateCleared;

        private int _debugApplyContinuousStateCount;
        private int _debugClearContinuousStateCount;
        private int _debugPlayActivatedCount;
        private int _debugPlayExpiredCount;
        private int _debugLastEmitterEntityId;
        private GravityFieldPhase _debugLastPhase = GravityFieldPhase.None;
        private int _debugLastTimerTicks;
        private int _debugLastDurationTicks;
        private float _debugLastProgress01;
        private int _debugLastAreaCellCount;
        private int _debugVisibleAreaSlotCount;
        private bool _isGameplayPresentationPaused;

        public int DebugApplyContinuousStateCount => _debugApplyContinuousStateCount;

        public int DebugClearContinuousStateCount => _debugClearContinuousStateCount;

        public int DebugPlayActivatedCount => _debugPlayActivatedCount;

        public int DebugPlayExpiredCount => _debugPlayExpiredCount;

        public int DebugLastEmitterEntityId => _debugLastEmitterEntityId;

        public GravityFieldPhase DebugLastPhase => _debugLastPhase;

        public int DebugLastTimerTicks => _debugLastTimerTicks;

        public int DebugLastDurationTicks => _debugLastDurationTicks;

        public float DebugLastProgress01 => _debugLastProgress01;

        public int DebugLastAreaCellCount => _debugLastAreaCellCount;

        public int DebugVisibleAreaSlotCount => _debugVisibleAreaSlotCount;

        public void PlayGravityFieldActivated()
        {
            _debugPlayActivatedCount++;
            SetAnimatorTrigger(activatedTriggerName);
            PlayParticlesRespectingPause(activatedParticles);
            activatedPlayed?.Invoke();
        }

        public void PlayGravityFieldExpired()
        {
            _debugPlayExpiredCount++;
            SetAnimatorTrigger(expiredTriggerName);
            PlayParticlesRespectingPause(expiredParticles);
            expiredPlayed?.Invoke();
        }

        public void ApplyGravityFieldVisualState(GravityFieldVisualState state)
        {
            _debugApplyContinuousStateCount++;
            _debugLastEmitterEntityId = state.EmitterEntityId;
            _debugLastPhase = state.Phase;
            _debugLastTimerTicks = state.TimerTicks;
            _debugLastDurationTicks = state.DurationTicks;
            _debugLastProgress01 = state.Progress01;
            _debugLastAreaCellCount = state.AreaCells.Count;

            var isActive = state.Phase == GravityFieldPhase.Active;
            var isCharging = state.Phase == GravityFieldPhase.Charging;
            SetOptionalActive(activeVisualRoot, isActive);
            SetOptionalActive(chargingVisualRoot, isCharging);
            SetAnimatorBool(activeBoolName, isActive);
            SetAnimatorBool(chargingBoolName, isCharging);
            SetAnimatorFloat(progressFloatName, state.Progress01);
            SetActiveAura(isActive);
            ApplyAreaSlots(isActive ? state.AreaFootprint : GravityFieldAreaFootprint.Empty);
            continuousStateApplied?.Invoke();
        }

        public void ClearGravityFieldVisualState()
        {
            _debugClearContinuousStateCount++;
            _debugLastEmitterEntityId = 0;
            _debugLastPhase = GravityFieldPhase.None;
            _debugLastTimerTicks = 0;
            _debugLastDurationTicks = 0;
            _debugLastProgress01 = 0f;
            _debugLastAreaCellCount = 0;

            SetOptionalActive(activeVisualRoot, false);
            SetOptionalActive(chargingVisualRoot, false);
            SetAnimatorBool(activeBoolName, false);
            SetAnimatorBool(chargingBoolName, false);
            SetAnimatorFloat(progressFloatName, 0f);
            SetActiveAura(false);
            ApplyAreaSlots(GravityFieldAreaFootprint.Empty);
            continuousStateCleared?.Invoke();
        }

        private void ApplyAreaSlots(GravityFieldAreaFootprint footprint)
        {
            _debugVisibleAreaSlotCount = 0;
            if (areaCellSlots == null)
            {
                return;
            }

            for (var i = 0; i < areaCellSlots.Length; i++)
            {
                var isVisible = i < GravityFieldAreaFootprint.SlotCount &&
                                footprint.IsSlotVisible(i);
                if (isVisible)
                {
                    _debugVisibleAreaSlotCount++;
                }

                SetOptionalActive(areaCellSlots[i], isVisible);
            }
        }

        private void SetActiveAura(bool isActive)
        {
            if (activeAuraParticles == null)
            {
                return;
            }

            if (isActive)
            {
                PlayParticlesRespectingPause(activeAuraParticles);
                return;
            }

            activeAuraParticles.Stop(
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

        public void SetPresentationPaused(bool paused)
        {
            _isGameplayPresentationPaused = paused;
        }

        private void PlayParticlesRespectingPause(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            particles.Play(withChildren: true);
            if (_isGameplayPresentationPaused)
            {
                particles.Pause(withChildren: true);
            }
        }

        private void SetAnimatorTrigger(string parameterName)
        {
            if (CanUseAnimatorParameter(parameterName))
            {
                animator.SetTrigger(Animator.StringToHash(parameterName));
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
