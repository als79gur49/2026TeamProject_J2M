using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyAnimationTimingSnapshot
    {
        public EnemyAnimationTimingSnapshot(
            float attackWindupAnimatorDurationSeconds,
            float recoverAnimatorDurationSeconds,
            float stateTransitionCrossFadeDurationSeconds)
        {
            AttackWindupAnimatorDurationSeconds = attackWindupAnimatorDurationSeconds;
            RecoverAnimatorDurationSeconds = recoverAnimatorDurationSeconds;
            StateTransitionCrossFadeDurationSeconds = stateTransitionCrossFadeDurationSeconds;
        }

        public float AttackWindupAnimatorDurationSeconds { get; }

        public float RecoverAnimatorDurationSeconds { get; }

        public float StateTransitionCrossFadeDurationSeconds { get; }

        public bool TryGetAttackWindupAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = AttackWindupAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetRecoverAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = RecoverAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetStateTransitionCrossFadeDurationOverride(out float durationSeconds)
        {
            durationSeconds = StateTransitionCrossFadeDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsStateTransitionCrossFadeOverride(durationSeconds);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnemyAnimationTimingAuthoring : MonoBehaviour
    {
        public const float UseDriverDefaultSentinel = -1f;
        public const float DefaultAnimatorDurationSeconds = UseDriverDefaultSentinel;
        public const float DefaultStateTransitionCrossFadeDurationSeconds = UseDriverDefaultSentinel;

        [SerializeField] private float attackWindupAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float recoverAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float stateTransitionCrossFadeDurationSeconds = DefaultStateTransitionCrossFadeDurationSeconds;

        public float AttackWindupAnimatorDurationSeconds => attackWindupAnimatorDurationSeconds;

        public float RecoverAnimatorDurationSeconds => recoverAnimatorDurationSeconds;

        public float StateTransitionCrossFadeDurationSeconds => stateTransitionCrossFadeDurationSeconds;

        public void Validate()
        {
            ValidateAnimatorDuration(attackWindupAnimatorDurationSeconds, nameof(attackWindupAnimatorDurationSeconds));
            ValidateAnimatorDuration(recoverAnimatorDurationSeconds, nameof(recoverAnimatorDurationSeconds));
            ValidateCrossFadeDuration(stateTransitionCrossFadeDurationSeconds, nameof(stateTransitionCrossFadeDurationSeconds));
        }

        public EnemyAnimationTimingSnapshot CreateSnapshot()
        {
            Validate();
            return new EnemyAnimationTimingSnapshot(
                attackWindupAnimatorDurationSeconds,
                recoverAnimatorDurationSeconds,
                stateTransitionCrossFadeDurationSeconds);
        }

        public void ApplyOverrides(
            float attackWindupAnimatorDurationSeconds,
            float recoverAnimatorDurationSeconds,
            float stateTransitionCrossFadeDurationSeconds)
        {
            this.attackWindupAnimatorDurationSeconds = attackWindupAnimatorDurationSeconds;
            this.recoverAnimatorDurationSeconds = recoverAnimatorDurationSeconds;
            this.stateTransitionCrossFadeDurationSeconds = stateTransitionCrossFadeDurationSeconds;
            Validate();
        }

        public bool TryGetAttackWindupAnimatorDurationOverride(out float durationSeconds)
        {
            return CreateSnapshot().TryGetAttackWindupAnimatorDurationOverride(out durationSeconds);
        }

        public bool TryGetRecoverAnimatorDurationOverride(out float durationSeconds)
        {
            return CreateSnapshot().TryGetRecoverAnimatorDurationOverride(out durationSeconds);
        }

        public bool TryGetStateTransitionCrossFadeDurationOverride(out float durationSeconds)
        {
            return CreateSnapshot().TryGetStateTransitionCrossFadeDurationOverride(out durationSeconds);
        }

        public static bool IsAnimatorDurationOverride(float animatorDurationSeconds)
        {
            return animatorDurationSeconds > 0f;
        }

        public static bool IsStateTransitionCrossFadeOverride(float crossFadeDurationSeconds)
        {
            return crossFadeDurationSeconds >= 0f;
        }

        internal static EnemyAnimationTimingAuthoring GetOptionalValidatedAuthoring(GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EnemyAnimationTimingAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }

        private static void ValidateAnimatorDuration(float animatorDurationSeconds, string parameterName)
        {
            if (animatorDurationSeconds == UseDriverDefaultSentinel)
            {
                return;
            }

            if (animatorDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Animator duration must be greater than zero, or -1 to keep the driver default behavior.");
            }
        }

        private static void ValidateCrossFadeDuration(float crossFadeDurationSeconds, string parameterName)
        {
            if (crossFadeDurationSeconds == UseDriverDefaultSentinel)
            {
                return;
            }

            if (crossFadeDurationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Cross-fade duration must be zero or greater, or -1 to keep the driver default behavior.");
            }
        }
    }
}
