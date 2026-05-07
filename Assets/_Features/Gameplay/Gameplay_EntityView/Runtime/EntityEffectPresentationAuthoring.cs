using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum DeathPresentationOwnershipMode
    {
        CloneSourceView = 0,
        AnchorToNamedTransform = 1,
    }

    public readonly struct EntityEffectPresentationSnapshot
    {
        public EntityEffectPresentationSnapshot(
            float hitEffectDurationSeconds,
            float deathEffectDurationSeconds,
            float deathViewTailSeconds,
            DeathPresentationOwnershipMode deathOwnershipMode,
            string deathAnchorName)
        {
            HitEffectDurationSeconds = hitEffectDurationSeconds;
            DeathEffectDurationSeconds = deathEffectDurationSeconds;
            DeathViewTailSeconds = deathViewTailSeconds;
            DeathOwnershipMode = deathOwnershipMode;
            DeathAnchorName = deathAnchorName ?? string.Empty;
        }

        public float HitEffectDurationSeconds { get; }

        public float DeathEffectDurationSeconds { get; }

        public float DeathViewTailSeconds { get; }

        public DeathPresentationOwnershipMode DeathOwnershipMode { get; }

        public string DeathAnchorName { get; }

        public bool HasHitEffectDurationOverride => EntityEffectPresentationAuthoring.IsOverrideDuration(HitEffectDurationSeconds);

        public bool HasDeathEffectDurationOverride => EntityEffectPresentationAuthoring.IsOverrideDuration(DeathEffectDurationSeconds);

        public bool HasDeathViewTailOverride => EntityEffectPresentationAuthoring.IsOverrideDuration(DeathViewTailSeconds);
    }

    [DisallowMultipleComponent]
    public sealed class EntityEffectPresentationAuthoring : MonoBehaviour
    {
        public const float UseGlobalTimingSentinel = EntityMotionPresentationAuthoring.UseGlobalTimingSentinel;

        [SerializeField] private float hitEffectDurationSeconds = UseGlobalTimingSentinel;
        [SerializeField] private float deathEffectDurationSeconds = UseGlobalTimingSentinel;
        [SerializeField] private float deathViewTailSeconds = UseGlobalTimingSentinel;

        [SerializeField] private DeathPresentationOwnershipMode deathOwnershipMode;
        [SerializeField] private string deathAnchorName = "Chest";

        public float HitEffectDurationSeconds => hitEffectDurationSeconds;

        public float DeathEffectDurationSeconds => deathEffectDurationSeconds;

        public float DeathViewTailSeconds => deathViewTailSeconds;

        public DeathPresentationOwnershipMode DeathOwnershipMode => deathOwnershipMode;

        public string DeathAnchorName => deathAnchorName;

        public void Validate()
        {
            ValidateOverrideDuration(hitEffectDurationSeconds, nameof(hitEffectDurationSeconds));
            ValidateOverrideDuration(deathEffectDurationSeconds, nameof(deathEffectDurationSeconds));
            ValidateOverrideDuration(deathViewTailSeconds, nameof(deathViewTailSeconds));

            if (deathOwnershipMode == DeathPresentationOwnershipMode.AnchorToNamedTransform &&
                string.IsNullOrWhiteSpace(deathAnchorName))
            {
                throw new ArgumentException(
                    "Death anchor name is required when using named-transform ownership.",
                    nameof(deathAnchorName));
            }
        }

        public EntityEffectPresentationSnapshot CreateSnapshot()
        {
            Validate();
            return new EntityEffectPresentationSnapshot(
                hitEffectDurationSeconds,
                deathEffectDurationSeconds,
                deathViewTailSeconds,
                deathOwnershipMode,
                deathAnchorName);
        }

        public static bool IsOverrideDuration(float durationSeconds)
        {
            return durationSeconds > 0f;
        }

        public static EntityEffectPresentationAuthoring GetOptionalValidatedAuthoring(GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EntityEffectPresentationAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }

        private static void ValidateOverrideDuration(float durationSeconds, string parameterName)
        {
            if (durationSeconds == UseGlobalTimingSentinel)
            {
                return;
            }

            if (durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Effect duration override must be greater than zero, or -1 to use the global timing fallback.");
            }
        }
    }
}
