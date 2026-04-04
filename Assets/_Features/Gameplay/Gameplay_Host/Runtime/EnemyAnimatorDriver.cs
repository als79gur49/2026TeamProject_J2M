using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class EnemyAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyAnimationTimingAuthoring animationTimingAuthoring;
        [SerializeField] private string aiModeParameterName = "EnemyAiMode";
        [SerializeField] private string activeActionKindParameterName = "EnemyActionKind";
        [SerializeField] private string movingParameterName = "IsMoving";
        [SerializeField] private string windupStateName = "Windup";
        [SerializeField] private string recoveryStateName = "Recover";
        [SerializeField] private string windupTriggerName = "Windup";
        [SerializeField] private string attackTriggerName = "Attack";
        [SerializeField] private string recoveryTriggerName = "Recover";
        [SerializeField] private string hitTriggerName = "Hit";
        [SerializeField] private string deathTriggerName = "Death";

        public EnemyViewPresentationState LastPresentationState { get; private set; }

        public EnemyAiMode CurrentAiMode { get; private set; }

        public EnemyActionKind CurrentActiveActionKind { get; private set; }

        public bool IsMoving { get; private set; }

        public bool IsVisible { get; private set; }

        public int WindupSignalCount { get; private set; }

        public int AttackSignalCount { get; private set; }

        public int RecoverySignalCount { get; private set; }

        public int HitSignalCount { get; private set; }

        public int DeathSignalCount { get; private set; }

        public float CurrentAnimatorSpeed { get; private set; } = 1f;

        public float CurrentPresentationDurationSeconds { get; private set; }

        public float LastCrossFadeDurationSeconds { get; private set; }

        public string LastCrossFadedStateName { get; private set; } = string.Empty;

        private bool _animationTimingResolved;
        private bool _hasAnimationTimingAuthoring;
        private EnemyAnimationTimingSnapshot _animationTiming;
        private readonly Dictionary<string, float> _clipLengthCache = new();
        private RuntimeAnimatorController _cachedClipLengthController;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            animationTimingAuthoring = GetComponent<EnemyAnimationTimingAuthoring>();
        }

        public void Apply(in EnemyViewPresentationState state)
        {
            LastPresentationState = state;
            CurrentAiMode = state.AiMode;
            CurrentActiveActionKind = state.ActiveActionKind;
            IsMoving = state.IsMoving;

            var targetAnimator = ResolveAnimator();
            if (targetAnimator != null)
            {
                SetIntegerParameter(targetAnimator, aiModeParameterName, (int)state.AiMode);
                SetIntegerParameter(targetAnimator, activeActionKindParameterName, (int)state.ActiveActionKind);
                SetBoolParameter(targetAnimator, movingParameterName, state.IsMoving);
            }

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(state));

            if (state.StartedWindupThisTick)
            {
                WindupSignalCount++;
                ApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Windup);
                SetTrigger(targetAnimator, windupTriggerName);
            }

            if (state.ExecutedThisTick)
            {
                AttackSignalCount++;
                SetTrigger(targetAnimator, attackTriggerName);
            }

            if (state.StartedRecoveryThisTick)
            {
                RecoverySignalCount++;
                ApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Recovery);
                SetTrigger(targetAnimator, recoveryTriggerName);
            }

            if (state.TookDamage)
            {
                HitSignalCount++;
                SetTrigger(targetAnimator, hitTriggerName);
            }

            if (state.DidDie)
            {
                DeathSignalCount++;
                SetTrigger(targetAnimator, deathTriggerName);
            }
        }

        public void SyncRuntimeState(bool isVisible, bool isMoving)
        {
            IsVisible = isVisible;
            IsMoving = isMoving;

            var targetAnimator = ResolveAnimator();
            if (targetAnimator != null)
            {
                SetBoolParameter(targetAnimator, movingParameterName, isMoving);
            }

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(LastPresentationState));
        }

        public bool TryGetAttackWindupAnimatorDurationOverride(out float durationSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            return animationTiming.TryGetAttackWindupAnimatorDurationOverride(out durationSeconds);
        }

        public bool TryGetRecoverAnimatorDurationOverride(out float durationSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            return animationTiming.TryGetRecoverAnimatorDurationOverride(out durationSeconds);
        }

        public bool TryGetStateTransitionCrossFadeDurationOverride(out float durationSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            return animationTiming.TryGetStateTransitionCrossFadeDurationOverride(out durationSeconds);
        }

        private Animator ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            return animator;
        }

        private bool TryResolveAnimationTiming(out EnemyAnimationTimingSnapshot animationTiming)
        {
            if (_animationTimingResolved)
            {
                animationTiming = _animationTiming;
                return _hasAnimationTimingAuthoring;
            }

            if (animationTimingAuthoring == null)
            {
                animationTimingAuthoring = GetComponent<EnemyAnimationTimingAuthoring>();
            }

            if (animationTimingAuthoring == null)
            {
                _animationTimingResolved = true;
                _hasAnimationTimingAuthoring = false;
                _animationTiming = default;
                animationTiming = default;
                return false;
            }

            _animationTiming = animationTimingAuthoring.CreateSnapshot();
            _animationTimingResolved = true;
            _hasAnimationTimingAuthoring = true;
            animationTiming = _animationTiming;
            return true;
        }

        private void ApplyAnimatorTiming(Animator targetAnimator, EnemyPresentationPhase phase)
        {
            var targetSpeed = ResolveAnimatorSpeed(targetAnimator, phase, out var presentationDurationSeconds);
            CurrentAnimatorSpeed = targetSpeed;
            CurrentPresentationDurationSeconds = presentationDurationSeconds;

            if (targetAnimator != null)
            {
                targetAnimator.speed = targetSpeed;
            }
        }

        private void ApplyPresentationCrossFade(Animator targetAnimator, EnemyPresentationPhase phase)
        {
            if (!TryGetStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds))
            {
                return;
            }

            var stateName = ResolveStateName(phase);
            if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            LastCrossFadeDurationSeconds = Mathf.Max(0f, crossFadeDurationSeconds);
            LastCrossFadedStateName = stateName;
            targetAnimator.CrossFadeInFixedTime(
                Animator.StringToHash(stateName),
                LastCrossFadeDurationSeconds);
        }

        private float ResolveAnimatorSpeed(
            Animator targetAnimator,
            EnemyPresentationPhase phase,
            out float presentationDurationSeconds)
        {
            if (phase == EnemyPresentationPhase.None)
            {
                presentationDurationSeconds = 0f;
                return 1f;
            }

            var stateName = ResolveStateName(phase);
            var referenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(targetAnimator, stateName);
            if (!TryResolveAnimatorDurationOverride(phase, out var overrideDurationSeconds))
            {
                presentationDurationSeconds = referenceClipLengthSeconds;
                return 1f;
            }

            presentationDurationSeconds = overrideDurationSeconds;
            return Mathf.Max(0.01f, referenceClipLengthSeconds / overrideDurationSeconds);
        }

        private bool TryResolveAnimatorDurationOverride(
            EnemyPresentationPhase phase,
            out float durationSeconds)
        {
            switch (phase)
            {
                case EnemyPresentationPhase.Windup:
                    return TryGetAttackWindupAnimatorDurationOverride(out durationSeconds);

                case EnemyPresentationPhase.Recovery:
                    return TryGetRecoverAnimatorDurationOverride(out durationSeconds);

                default:
                    durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                    return false;
            }
        }

        private string ResolveStateName(EnemyPresentationPhase phase)
        {
            switch (phase)
            {
                case EnemyPresentationPhase.Windup:
                    return windupStateName;

                case EnemyPresentationPhase.Recovery:
                    return recoveryStateName;

                default:
                    return string.Empty;
            }
        }

        private float ResolveReferenceClipLengthSeconds(Animator targetAnimator, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return 1f;
            }

            var controller = targetAnimator?.runtimeAnimatorController;
            if (_cachedClipLengthController != controller)
            {
                _clipLengthCache.Clear();
                _cachedClipLengthController = controller;
            }

            if (_clipLengthCache.TryGetValue(stateName, out var cachedLength))
            {
                return cachedLength;
            }

            var clips = controller?.animationClips;
            if (clips != null)
            {
                for (var i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    if (clip != null &&
                        string.Equals(clip.name, stateName, StringComparison.Ordinal))
                    {
                        var resolvedLength = Mathf.Max(clip.length, 0.01f);
                        _clipLengthCache[stateName] = resolvedLength;
                        return resolvedLength;
                    }
                }
            }

            _clipLengthCache[stateName] = 1f;
            return 1f;
        }

        private static EnemyPresentationPhase ResolvePresentationPhase(in EnemyViewPresentationState state)
        {
            switch (state.AiMode)
            {
                case EnemyAiMode.Attack:
                    return EnemyPresentationPhase.Windup;

                case EnemyAiMode.Recover:
                    return EnemyPresentationPhase.Recovery;

                default:
                    return EnemyPresentationPhase.None;
            }
        }

        private static void SetBoolParameter(Animator targetAnimator, string parameterName, bool value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetBool(Animator.StringToHash(parameterName), value);
        }

        private static void SetIntegerParameter(Animator targetAnimator, string parameterName, int value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetInteger(Animator.StringToHash(parameterName), value);
        }

        private static void SetTrigger(Animator targetAnimator, string parameterName)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            targetAnimator.SetTrigger(Animator.StringToHash(parameterName));
        }

        private enum EnemyPresentationPhase
        {
            None = 0,
            Windup = 1,
            Recovery = 2,
        }
    }
}
