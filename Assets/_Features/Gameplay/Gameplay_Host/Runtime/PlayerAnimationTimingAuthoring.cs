using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct PlayerAnimationTimingSnapshot
    {
        public PlayerAnimationTimingSnapshot(
            float pushAnimatorDurationSeconds,
            float flipAnimatorDurationSeconds)
        {
            PushAnimatorDurationSeconds = pushAnimatorDurationSeconds;
            FlipAnimatorDurationSeconds = flipAnimatorDurationSeconds;
        }

        public float PushAnimatorDurationSeconds { get; }

        public float FlipAnimatorDurationSeconds { get; }
    }

    [MovedFrom(false, "Game.Feature.Gameplay.Host", "Game.Feature.Gameplay.Host", "PlayerActionTimingAuthoring")]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationTimingAuthoring : MonoBehaviour
    {
        public const float DefaultAnimatorDurationSeconds = GameplayTimingProfile.DefaultPushMotionDurationSeconds;

        [FormerlySerializedAs("pushPresentationDurationSeconds")]
        [SerializeField] private float pushAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;
        [FormerlySerializedAs("flipPresentationDurationSeconds")]
        [SerializeField] private float flipAnimatorDurationSeconds = DefaultAnimatorDurationSeconds;

        public float PushAnimatorDurationSeconds => pushAnimatorDurationSeconds;

        public float FlipAnimatorDurationSeconds => flipAnimatorDurationSeconds;

        public void Validate()
        {
            ValidateAnimatorDuration(pushAnimatorDurationSeconds, nameof(pushAnimatorDurationSeconds));
            ValidateAnimatorDuration(flipAnimatorDurationSeconds, nameof(flipAnimatorDurationSeconds));
        }

        public PlayerAnimationTimingSnapshot CreateSnapshot()
        {
            Validate();
            return new PlayerAnimationTimingSnapshot(
                pushAnimatorDurationSeconds,
                flipAnimatorDurationSeconds);
        }

        private static void ValidateAnimatorDuration(float animatorDurationSeconds, string parameterName)
        {
            if (animatorDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Animator duration must be greater than zero.");
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
            EntityMotionPresentationAuthoring.GetOptionalValidatedAuthoring(playerViewPrefab);
        }

        public static void ValidatePlayerViewInstance(GameplayEntityView playerViewInstance, string ownerDescription)
        {
            ValidatePlayerViewPrefab(playerViewInstance, ownerDescription);
        }
    }
}
