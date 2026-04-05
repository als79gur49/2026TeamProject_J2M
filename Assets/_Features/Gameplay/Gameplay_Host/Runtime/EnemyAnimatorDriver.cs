using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class EnemyAnimatorDriver : MonoBehaviour
    {
        private const string AiModeParameterName = "EnemyAiMode";
        private const string ActiveActionKindParameterName = "EnemyActionKind";
        private const string MovingParameterName = "IsMoving";
        private static readonly int AiModeParameterHash = Animator.StringToHash(AiModeParameterName);
        private static readonly int ActiveActionKindParameterHash = Animator.StringToHash(ActiveActionKindParameterName);
        private static readonly int MovingParameterHash = Animator.StringToHash(MovingParameterName);

        [SerializeField] private Animator animator;
        [SerializeField] private EnemyAnimationTimingAuthoring animationTimingAuthoring;
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
        private Animator _validatedOptionalParameterAnimator;
        private RuntimeAnimatorController _validatedOptionalParameterController;
        private bool _supportsAiModeParameter;
        private bool _supportsActiveActionKindParameter;
        private bool _supportsMovingParameter;
        private int _windupTriggerDispatchCount;
        private int _recoveryTriggerDispatchCount;

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
            SyncOptionalParameters(targetAnimator, state);

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(state));

            if (state.StartedWindupThisTick)
            {
                WindupSignalCount++;
                if (!TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Windup))
                {
                    DispatchWindupTrigger(targetAnimator);
                }
            }

            if (state.ExecutedThisTick)
            {
                AttackSignalCount++;
                SetTrigger(targetAnimator, attackTriggerName);
            }

            if (state.StartedRecoveryThisTick)
            {
                RecoverySignalCount++;
                if (!TryApplyPresentationCrossFade(targetAnimator, EnemyPresentationPhase.Recovery))
                {
                    DispatchRecoveryTrigger(targetAnimator);
                }
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
            SyncOptionalMovingParameter(targetAnimator, isMoving);

            ApplyAnimatorTiming(targetAnimator, ResolvePresentationPhase(LastPresentationState));
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

        private bool TryApplyPresentationCrossFade(Animator targetAnimator, EnemyPresentationPhase phase)
        {
            if (!TryResolveStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds))
            {
                return false;
            }

            var stateName = ResolveStateName(phase);
            if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            LastCrossFadeDurationSeconds = Mathf.Max(0f, crossFadeDurationSeconds);
            LastCrossFadedStateName = stateName;
            targetAnimator.CrossFadeInFixedTime(
                Animator.StringToHash(stateName),
                LastCrossFadeDurationSeconds);
            return true;
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
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            switch (phase)
            {
                case EnemyPresentationPhase.Windup:
                    return animationTiming.TryGetAttackWindupAnimatorDurationOverride(out durationSeconds);

                case EnemyPresentationPhase.Recovery:
                    return animationTiming.TryGetRecoverAnimatorDurationOverride(out durationSeconds);

                default:
                    durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                    return false;
            }
        }

        private bool TryResolveStateTransitionCrossFadeDurationOverride(out float durationSeconds)
        {
            if (!TryResolveAnimationTiming(out var animationTiming))
            {
                durationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                return false;
            }

            return animationTiming.TryGetStateTransitionCrossFadeDurationOverride(out durationSeconds);
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

        private void SyncOptionalParameters(Animator targetAnimator, in EnemyViewPresentationState state)
        {
            if (SupportsAiModeParameter(targetAnimator))
            {
                targetAnimator.SetInteger(AiModeParameterHash, (int)state.AiMode);
            }

            if (SupportsActiveActionKindParameter(targetAnimator))
            {
                targetAnimator.SetInteger(ActiveActionKindParameterHash, (int)state.ActiveActionKind);
            }

            SyncOptionalMovingParameter(targetAnimator, state.IsMoving);
        }

        private void SyncOptionalMovingParameter(Animator targetAnimator, bool isMoving)
        {
            if (!SupportsMovingParameter(targetAnimator))
            {
                return;
            }

            targetAnimator.SetBool(MovingParameterHash, isMoving);
        }

        private bool SupportsAiModeParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsAiModeParameter;
        }

        private bool SupportsActiveActionKindParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsActiveActionKindParameter;
        }

        private bool SupportsMovingParameter(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            RefreshOptionalParameterSupport(targetAnimator);
            return _supportsMovingParameter;
        }

        private void RefreshOptionalParameterSupport(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return;
            }

            var controller = targetAnimator.runtimeAnimatorController;
            if (_validatedOptionalParameterAnimator == targetAnimator &&
                _validatedOptionalParameterController == controller)
            {
                return;
            }

            _validatedOptionalParameterAnimator = targetAnimator;
            _validatedOptionalParameterController = controller;
            _supportsAiModeParameter = false;
            _supportsActiveActionKindParameter = false;
            _supportsMovingParameter = false;

            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Int)
                {
                    if (parameter.nameHash == AiModeParameterHash)
                    {
                        _supportsAiModeParameter = true;
                    }
                    else if (parameter.nameHash == ActiveActionKindParameterHash)
                    {
                        _supportsActiveActionKindParameter = true;
                    }
                }
                else if (parameter.type == AnimatorControllerParameterType.Bool &&
                         parameter.nameHash == MovingParameterHash)
                {
                    _supportsMovingParameter = true;
                }
            }
        }

        private void DispatchWindupTrigger(Animator targetAnimator)
        {
            if (SetTrigger(targetAnimator, windupTriggerName))
            {
                _windupTriggerDispatchCount++;
            }
        }

        private void DispatchRecoveryTrigger(Animator targetAnimator)
        {
            if (SetTrigger(targetAnimator, recoveryTriggerName))
            {
                _recoveryTriggerDispatchCount++;
            }
        }

        private static bool SetTrigger(Animator targetAnimator, string parameterName)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            targetAnimator.SetTrigger(Animator.StringToHash(parameterName));
            return true;
        }

        private enum EnemyPresentationPhase
        {
            None = 0,
            Windup = 1,
            Recovery = 2,
        }
    }
}
