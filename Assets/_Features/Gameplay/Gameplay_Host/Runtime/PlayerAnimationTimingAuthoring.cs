using System;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Host
{
    public enum PlayerPresentationPhase
    {
        None = 0,
        PushWindup = 1,
        PushRecovery = 2,
        FlipWindup = 3,
        FlipRecovery = 4,
    }

    public readonly struct PlayerAnimationTimingSnapshot
    {
        public PlayerAnimationTimingSnapshot(
            float pushWindupAnimatorDurationSeconds,
            float pushRecoveryAnimatorDurationSeconds,
            float flipWindupAnimatorDurationSeconds,
            float flipRecoveryAnimatorDurationSeconds,
            float deathAnimatorDurationSeconds,
            float legacyPushAnimatorDurationSeconds,
            float legacyFlipAnimatorDurationSeconds)
        {
            PushWindupAnimatorDurationSeconds = pushWindupAnimatorDurationSeconds;
            PushRecoveryAnimatorDurationSeconds = pushRecoveryAnimatorDurationSeconds;
            FlipWindupAnimatorDurationSeconds = flipWindupAnimatorDurationSeconds;
            FlipRecoveryAnimatorDurationSeconds = flipRecoveryAnimatorDurationSeconds;
            DeathAnimatorDurationSeconds = deathAnimatorDurationSeconds;
            LegacyPushAnimatorDurationSeconds = legacyPushAnimatorDurationSeconds;
            LegacyFlipAnimatorDurationSeconds = legacyFlipAnimatorDurationSeconds;
        }

        public float PushWindupAnimatorDurationSeconds { get; }

        public float PushRecoveryAnimatorDurationSeconds { get; }

        public float FlipWindupAnimatorDurationSeconds { get; }

        public float FlipRecoveryAnimatorDurationSeconds { get; }

        public float DeathAnimatorDurationSeconds { get; }

        public float LegacyPushAnimatorDurationSeconds { get; }

        public float LegacyFlipAnimatorDurationSeconds { get; }

        public bool TryGetAnimatorDurationOverride(
            PlayerPresentationPhase phase,
            out float durationSeconds)
        {
            durationSeconds = phase switch
            {
                PlayerPresentationPhase.PushWindup => PushWindupAnimatorDurationSeconds,
                PlayerPresentationPhase.PushRecovery => PushRecoveryAnimatorDurationSeconds,
                PlayerPresentationPhase.FlipWindup => FlipWindupAnimatorDurationSeconds,
                PlayerPresentationPhase.FlipRecovery => FlipRecoveryAnimatorDurationSeconds,
                _ => PlayerAnimationTimingAuthoring.UseResolvedMotionDurationSentinel,
            };

            return PlayerAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetLegacyAnimatorDurationOverride(
            PlayerActionKind actionKind,
            out float durationSeconds)
        {
            durationSeconds = actionKind switch
            {
                PlayerActionKind.Push => LegacyPushAnimatorDurationSeconds,
                PlayerActionKind.Flip => LegacyFlipAnimatorDurationSeconds,
                _ => PlayerAnimationTimingAuthoring.UseResolvedMotionDurationSentinel,
            };

            return PlayerAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }

        public bool TryGetDeathAnimatorDurationOverride(out float durationSeconds)
        {
            durationSeconds = DeathAnimatorDurationSeconds;
            return PlayerAnimationTimingAuthoring.IsAnimatorDurationOverride(durationSeconds);
        }
    }

    [MovedFrom(false, "Game.Feature.Gameplay.Host", "Game.Feature.Gameplay.Host", "PlayerActionTimingAuthoring")]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationTimingAuthoring : MonoBehaviour
    {
        public const float UseResolvedMotionDurationSentinel = -1f;
        public const float DefaultAnimatorDurationSeconds = UseResolvedMotionDurationSentinel;

        [SerializeField] private float pushWindupAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float pushRecoveryAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float flipWindupAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float flipRecoveryAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [SerializeField] private float deathAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [FormerlySerializedAs("pushAnimatorDurationSeconds")]
        [FormerlySerializedAs("pushPresentationDurationSeconds")]
        [SerializeField, HideInInspector] private float legacyPushAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [FormerlySerializedAs("flipAnimatorDurationSeconds")]
        [FormerlySerializedAs("flipPresentationDurationSeconds")]
        [SerializeField, HideInInspector] private float legacyFlipAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;

        public float PushWindupAnimatorDurationSeconds => pushWindupAnimatorDurationSeconds;

        public float PushRecoveryAnimatorDurationSeconds => pushRecoveryAnimatorDurationSeconds;

        public float FlipWindupAnimatorDurationSeconds => flipWindupAnimatorDurationSeconds;

        public float FlipRecoveryAnimatorDurationSeconds => flipRecoveryAnimatorDurationSeconds;

        public float DeathAnimatorDurationSeconds => deathAnimatorDurationSeconds;

        public void Validate()
        {
            ValidateAnimatorDuration(pushWindupAnimatorDurationSeconds, nameof(pushWindupAnimatorDurationSeconds));
            ValidateAnimatorDuration(pushRecoveryAnimatorDurationSeconds, nameof(pushRecoveryAnimatorDurationSeconds));
            ValidateAnimatorDuration(flipWindupAnimatorDurationSeconds, nameof(flipWindupAnimatorDurationSeconds));
            ValidateAnimatorDuration(flipRecoveryAnimatorDurationSeconds, nameof(flipRecoveryAnimatorDurationSeconds));
            ValidateAnimatorDuration(deathAnimatorDurationSeconds, nameof(deathAnimatorDurationSeconds));
            ValidateAnimatorDuration(legacyPushAnimatorDurationSeconds, nameof(legacyPushAnimatorDurationSeconds));
            ValidateAnimatorDuration(legacyFlipAnimatorDurationSeconds, nameof(legacyFlipAnimatorDurationSeconds));
        }

        public PlayerAnimationTimingSnapshot CreateSnapshot()
        {
            Validate();
            return new PlayerAnimationTimingSnapshot(
                pushWindupAnimatorDurationSeconds,
                pushRecoveryAnimatorDurationSeconds,
                flipWindupAnimatorDurationSeconds,
                flipRecoveryAnimatorDurationSeconds,
                deathAnimatorDurationSeconds,
                legacyPushAnimatorDurationSeconds,
                legacyFlipAnimatorDurationSeconds);
        }

        public static bool IsAnimatorDurationOverride(float animatorDurationSeconds)
        {
            return animatorDurationSeconds > 0f;
        }

        private static void ValidateAnimatorDuration(float animatorDurationSeconds, string parameterName)
        {
            if (animatorDurationSeconds == UseResolvedMotionDurationSentinel)
            {
                return;
            }

            if (animatorDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Animator duration must be greater than zero, or -1 to use the resolved motion duration fallback.");
            }
        }
    }

    internal static class PlayerViewPrefabRequirements
    {
        public static PlayerAnimationTimingAuthoring GetAnimationTimingAuthoring(
            GameplayEntityView playerViewPrefab,
            string ownerDescription)
        {
            if (playerViewPrefab == null)
            {
                throw new ArgumentNullException(nameof(playerViewPrefab));
            }

            if (!playerViewPrefab.TryGetComponent<PlayerAnimationTimingAuthoring>(out var authoring) ||
                authoring == null)
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} requires PlayerAnimationTimingAuthoring on the player prefab root.");
            }

            authoring.Validate();
            return authoring;
        }

        public static void ValidatePlayerViewPrefab(GameplayEntityView playerViewPrefab, string ownerDescription)
        {
            if (playerViewPrefab == null)
            {
                throw new ArgumentNullException(nameof(playerViewPrefab));
            }

            if (!playerViewPrefab.TryGetComponent<PlayerAnimatorDriver>(out _))
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} requires PlayerAnimatorDriver on the player prefab root.");
            }

            GetAnimationTimingAuthoring(playerViewPrefab, ownerDescription);
            UnitLocomotionPresentationAuthoring.GetOptionalValidatedAuthoring(playerViewPrefab);
            EntityMotionPresentationAuthoring.GetOptionalValidatedAuthoring(playerViewPrefab);
            EntityEffectPresentationAuthoring.GetOptionalValidatedAuthoring(playerViewPrefab);
            GameplayActionAudioPrefabRequirements.GetOptionalValidatedAuthoring(playerViewPrefab, ownerDescription);
        }

        public static void ValidatePlayerViewInstance(GameplayEntityView playerViewInstance, string ownerDescription)
        {
            ValidatePlayerViewPrefab(playerViewInstance, ownerDescription);
        }
    }
}
