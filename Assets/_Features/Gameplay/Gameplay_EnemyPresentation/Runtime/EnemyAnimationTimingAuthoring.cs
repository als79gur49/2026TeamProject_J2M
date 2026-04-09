using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyAnimationTimingSnapshot
    {
        public EnemyAnimationTimingSnapshot(
            float attackWindupAnimatorDurationSeconds,
            float jumpWindupAnimatorDurationSeconds,
            float jumpAirborneAnimatorDurationSeconds,
            float recoverAnimatorDurationSeconds,
            float deathAnimatorDurationSeconds,
            float stateTransitionCrossFadeDurationSeconds,
            float attackWindupReferenceClipLengthSeconds,
            float jumpWindupReferenceClipLengthSeconds,
            float jumpAirborneReferenceClipLengthSeconds,
            float recoverReferenceClipLengthSeconds,
            float deathReferenceClipLengthSeconds)
        {
            AttackWindupAnimatorDurationSeconds = attackWindupAnimatorDurationSeconds;
            JumpWindupAnimatorDurationSeconds = jumpWindupAnimatorDurationSeconds;
            JumpAirborneAnimatorDurationSeconds = jumpAirborneAnimatorDurationSeconds;
            RecoverAnimatorDurationSeconds = recoverAnimatorDurationSeconds;
            DeathAnimatorDurationSeconds = deathAnimatorDurationSeconds;
            StateTransitionCrossFadeDurationSeconds = stateTransitionCrossFadeDurationSeconds;
            AttackWindupReferenceClipLengthSeconds = attackWindupReferenceClipLengthSeconds;
            JumpWindupReferenceClipLengthSeconds = jumpWindupReferenceClipLengthSeconds;
            JumpAirborneReferenceClipLengthSeconds = jumpAirborneReferenceClipLengthSeconds;
            RecoverReferenceClipLengthSeconds = recoverReferenceClipLengthSeconds;
            DeathReferenceClipLengthSeconds = deathReferenceClipLengthSeconds;
        }

        public float AttackWindupAnimatorDurationSeconds { get; }

        public float JumpWindupAnimatorDurationSeconds { get; }

        public float JumpAirborneAnimatorDurationSeconds { get; }

        public float RecoverAnimatorDurationSeconds { get; }

        public float DeathAnimatorDurationSeconds { get; }

        public float StateTransitionCrossFadeDurationSeconds { get; }

        internal float AttackWindupReferenceClipLengthSeconds { get; }

        internal float JumpWindupReferenceClipLengthSeconds { get; }

        internal float JumpAirborneReferenceClipLengthSeconds { get; }

        internal float RecoverReferenceClipLengthSeconds { get; }

        internal float DeathReferenceClipLengthSeconds { get; }

        public bool TryGetAttackWindupAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = AttackWindupAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetJumpWindupAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = JumpWindupAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetJumpAirborneAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = JumpAirborneAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetRecoverAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = RecoverAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetDeathAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = DeathAnimatorDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetStateTransitionCrossFadeDurationOverride(out float durationSeconds)
        {
            durationSeconds = StateTransitionCrossFadeDurationSeconds;
            return EnemyAnimationTimingAuthoring.IsStateTransitionCrossFadeOverride(durationSeconds);
        }

        internal bool TryGetAttackWindupReferenceClipLengthSeconds(out float referenceClipLengthSeconds)
        {
            referenceClipLengthSeconds = AttackWindupReferenceClipLengthSeconds;
            return referenceClipLengthSeconds > 0f;
        }

        internal bool TryGetJumpWindupReferenceClipLengthSeconds(out float referenceClipLengthSeconds)
        {
            referenceClipLengthSeconds = JumpWindupReferenceClipLengthSeconds;
            return referenceClipLengthSeconds > 0f;
        }

        internal bool TryGetJumpAirborneReferenceClipLengthSeconds(out float referenceClipLengthSeconds)
        {
            referenceClipLengthSeconds = JumpAirborneReferenceClipLengthSeconds;
            return referenceClipLengthSeconds > 0f;
        }

        internal bool TryGetRecoverReferenceClipLengthSeconds(out float referenceClipLengthSeconds)
        {
            referenceClipLengthSeconds = RecoverReferenceClipLengthSeconds;
            return referenceClipLengthSeconds > 0f;
        }

        internal bool TryGetDeathReferenceClipLengthSeconds(out float referenceClipLengthSeconds)
        {
            referenceClipLengthSeconds = DeathReferenceClipLengthSeconds;
            return referenceClipLengthSeconds > 0f;
        }
    }

    /// <summary>
    /// Optional enemy animation-only tuning surface.
    /// These overrides affect animator playback and cross-fades only; authoritative AI cadence such as
    /// attack windup, jump phases, recover, and locomotion cooldown stays in EnemyAiProfile and runtime state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationTimingAuthoring : MonoBehaviour
    {
        public const float UseDriverDefaultSentinel = -1f;
        public const float DefaultAnimatorDurationSeconds = UseDriverDefaultSentinel;
        public const float DefaultStateTransitionCrossFadeDurationSeconds = UseDriverDefaultSentinel;

        [SerializeField] private float attackWindupAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float jumpWindupAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float jumpAirborneAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float recoverAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float deathAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float stateTransitionCrossFadeDurationSeconds = DefaultStateTransitionCrossFadeDurationSeconds;
        [SerializeField] private AnimationClip attackWindupReferenceClip;
        [SerializeField] private AnimationClip jumpWindupReferenceClip;
        [SerializeField] private AnimationClip jumpAirborneReferenceClip;
        [SerializeField] private AnimationClip recoverReferenceClip;
        [SerializeField] private AnimationClip deathReferenceClip;

        public float AttackWindupAnimatorDurationSeconds => attackWindupAnimatorDurationSeconds;

        public float JumpWindupAnimatorDurationSeconds => jumpWindupAnimatorDurationSeconds;

        public float JumpAirborneAnimatorDurationSeconds => jumpAirborneAnimatorDurationSeconds;

        public float RecoverAnimatorDurationSeconds => recoverAnimatorDurationSeconds;

        public float DeathAnimatorDurationSeconds => deathAnimatorDurationSeconds;

        public float StateTransitionCrossFadeDurationSeconds => stateTransitionCrossFadeDurationSeconds;

        public void Validate()
        {
            ValidateAnimatorDuration(attackWindupAnimatorDurationSeconds, nameof(attackWindupAnimatorDurationSeconds));
            ValidateAnimatorDuration(jumpWindupAnimatorDurationSeconds, nameof(jumpWindupAnimatorDurationSeconds));
            ValidateAnimatorDuration(jumpAirborneAnimatorDurationSeconds, nameof(jumpAirborneAnimatorDurationSeconds));
            ValidateAnimatorDuration(recoverAnimatorDurationSeconds, nameof(recoverAnimatorDurationSeconds));
            ValidateAnimatorDuration(deathAnimatorDurationSeconds, nameof(deathAnimatorDurationSeconds));
            ValidateCrossFadeDuration(stateTransitionCrossFadeDurationSeconds, nameof(stateTransitionCrossFadeDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                attackWindupAnimatorDurationSeconds,
                attackWindupReferenceClip,
                nameof(attackWindupReferenceClip),
                nameof(attackWindupAnimatorDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                jumpWindupAnimatorDurationSeconds,
                jumpWindupReferenceClip,
                nameof(jumpWindupReferenceClip),
                nameof(jumpWindupAnimatorDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                jumpAirborneAnimatorDurationSeconds,
                jumpAirborneReferenceClip,
                nameof(jumpAirborneReferenceClip),
                nameof(jumpAirborneAnimatorDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                recoverAnimatorDurationSeconds,
                recoverReferenceClip,
                nameof(recoverReferenceClip),
                nameof(recoverAnimatorDurationSeconds));
            ResolveReferenceClipLengthSeconds(
                deathAnimatorDurationSeconds,
                deathReferenceClip,
                nameof(deathReferenceClip),
                nameof(deathAnimatorDurationSeconds));
        }

        public EnemyAnimationTimingSnapshot CreateSnapshot()
        {
            Validate();
            var attackWindupReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                attackWindupAnimatorDurationSeconds,
                attackWindupReferenceClip,
                nameof(attackWindupReferenceClip),
                nameof(attackWindupAnimatorDurationSeconds));
            var jumpWindupReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                jumpWindupAnimatorDurationSeconds,
                jumpWindupReferenceClip,
                nameof(jumpWindupReferenceClip),
                nameof(jumpWindupAnimatorDurationSeconds));
            var jumpAirborneReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                jumpAirborneAnimatorDurationSeconds,
                jumpAirborneReferenceClip,
                nameof(jumpAirborneReferenceClip),
                nameof(jumpAirborneAnimatorDurationSeconds));
            var recoverReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                recoverAnimatorDurationSeconds,
                recoverReferenceClip,
                nameof(recoverReferenceClip),
                nameof(recoverAnimatorDurationSeconds));
            var deathReferenceClipLengthSeconds = ResolveReferenceClipLengthSeconds(
                deathAnimatorDurationSeconds,
                deathReferenceClip,
                nameof(deathReferenceClip),
                nameof(deathAnimatorDurationSeconds));
            return new EnemyAnimationTimingSnapshot(
                attackWindupAnimatorDurationSeconds,
                jumpWindupAnimatorDurationSeconds,
                jumpAirborneAnimatorDurationSeconds,
                recoverAnimatorDurationSeconds,
                deathAnimatorDurationSeconds,
                stateTransitionCrossFadeDurationSeconds,
                attackWindupReferenceClipLengthSeconds,
                jumpWindupReferenceClipLengthSeconds,
                jumpAirborneReferenceClipLengthSeconds,
                recoverReferenceClipLengthSeconds,
                deathReferenceClipLengthSeconds);
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
