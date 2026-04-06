using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyAnimationTimingSnapshot
    {
        public EnemyAnimationTimingSnapshot(
            float attackWindupAnimatorDurationSeconds,
            float recoverAnimatorDurationSeconds,
            float stateTransitionCrossFadeDurationSeconds,
            float attackWindupReferenceClipLengthSeconds,
            float recoverReferenceClipLengthSeconds)
        {
            AttackWindupAnimatorDurationSeconds = attackWindupAnimatorDurationSeconds;
            RecoverAnimatorDurationSeconds = recoverAnimatorDurationSeconds;
            StateTransitionCrossFadeDurationSeconds = stateTransitionCrossFadeDurationSeconds;
            AttackWindupReferenceClipLengthSeconds = attackWindupReferenceClipLengthSeconds;
            RecoverReferenceClipLengthSeconds = recoverReferenceClipLengthSeconds;
        }

        public float AttackWindupAnimatorDurationSeconds { get; }

        public float RecoverAnimatorDurationSeconds { get; }

        public float StateTransitionCrossFadeDurationSeconds { get; }

        internal float AttackWindupReferenceClipLengthSeconds { get; }

        internal float RecoverReferenceClipLengthSeconds { get; }

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

        internal bool TryGetReferenceClipLengthSeconds(
            EnemyPresentationPhase phase,
            out float referenceClipLengthSeconds)
        {
            switch (phase)
            {
                case EnemyPresentationPhase.Windup:
                    referenceClipLengthSeconds = AttackWindupReferenceClipLengthSeconds;
                    return referenceClipLengthSeconds > 0f;

                case EnemyPresentationPhase.Recovery:
                    referenceClipLengthSeconds = RecoverReferenceClipLengthSeconds;
                    return referenceClipLengthSeconds > 0f;

                default:
                    referenceClipLengthSeconds = 0f;
                    return false;
            }
        }
    }

    /// <summary>
    /// Optional enemy animation-only tuning surface.
    /// These overrides affect animator playback and cross-fades only; authoritative AI cadence such as
    /// windup, recover, and locomotion cooldown stays in EnemyAiProfile and runtime state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationTimingAuthoring : MonoBehaviour
    {
        public const float UseDriverDefaultSentinel = -1f;
        public const float DefaultAnimatorDurationSeconds = UseDriverDefaultSentinel;
        public const float DefaultStateTransitionCrossFadeDurationSeconds = UseDriverDefaultSentinel;

        [SerializeField] private float attackWindupAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float recoverAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float stateTransitionCrossFadeDurationSeconds = DefaultStateTransitionCrossFadeDurationSeconds;
        [SerializeField] private AnimationClip attackWindupReferenceClip;
        [SerializeField] private AnimationClip recoverReferenceClip;

        public float AttackWindupAnimatorDurationSeconds => attackWindupAnimatorDurationSeconds;

        public float RecoverAnimatorDurationSeconds => recoverAnimatorDurationSeconds;

        public float StateTransitionCrossFadeDurationSeconds => stateTransitionCrossFadeDurationSeconds;

        public void Validate()
        {
            ValidateAnimatorDuration(attackWindupAnimatorDurationSeconds, nameof(attackWindupAnimatorDurationSeconds));
            ValidateAnimatorDuration(recoverAnimatorDurationSeconds, nameof(recoverAnimatorDurationSeconds));
            ValidateCrossFadeDuration(stateTransitionCrossFadeDurationSeconds, nameof(stateTransitionCrossFadeDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                attackWindupAnimatorDurationSeconds,
                attackWindupReferenceClip,
                nameof(attackWindupReferenceClip),
                nameof(attackWindupAnimatorDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                recoverAnimatorDurationSeconds,
                recoverReferenceClip,
                nameof(recoverReferenceClip),
                nameof(recoverAnimatorDurationSeconds));
        }

        public EnemyAnimationTimingSnapshot CreateSnapshot()
        {
            Validate();
            var attackWindupReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                attackWindupAnimatorDurationSeconds,
                attackWindupReferenceClip,
                nameof(attackWindupReferenceClip),
                nameof(attackWindupAnimatorDurationSeconds));
            var recoverReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                recoverAnimatorDurationSeconds,
                recoverReferenceClip,
                nameof(recoverReferenceClip),
                nameof(recoverAnimatorDurationSeconds));
            return new EnemyAnimationTimingSnapshot(
                attackWindupAnimatorDurationSeconds,
                recoverAnimatorDurationSeconds,
                stateTransitionCrossFadeDurationSeconds,
                attackWindupReferenceClipLengthSeconds,
                recoverReferenceClipLengthSeconds);
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

        private static float ResolveReferenceClipLengthSeconds(
            float animatorDurationSeconds,
            AnimationClip referenceClip,
            string clipFieldName,
            string durationFieldName)
        {
            if (referenceClip == null)
            {
                if (animatorDurationSeconds == UseDriverDefaultSentinel)
                {
                    return 0f;
                }

                throw new InvalidOperationException(
                    $"{clipFieldName} must be assigned when {durationFieldName} overrides animator duration.");
            }

            var clipLengthSeconds = referenceClip.length;
            if (clipLengthSeconds <= 0f ||
                float.IsNaN(clipLengthSeconds) ||
                float.IsInfinity(clipLengthSeconds))
            {
                throw new InvalidOperationException(
                    $"{clipFieldName} must reference an AnimationClip with a finite positive length.");
            }

            return clipLengthSeconds;
        }
    }
}
