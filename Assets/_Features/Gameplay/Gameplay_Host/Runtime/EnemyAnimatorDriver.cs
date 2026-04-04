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

        private bool _animationTimingResolved;
        private bool _hasAnimationTimingAuthoring;
        private EnemyAnimationTimingSnapshot _animationTiming;

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

            if (state.StartedWindupThisTick)
            {
                WindupSignalCount++;
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
    }
}
