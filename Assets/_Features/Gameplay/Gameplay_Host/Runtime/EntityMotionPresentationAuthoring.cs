using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EntityMotionPresentationSnapshot
    {
        public EntityMotionPresentationSnapshot(
            float moveMotionDurationSeconds,
            float pushMotionDurationSeconds,
            float flipMotionDurationSeconds)
        {
            MoveMotionDurationSeconds = moveMotionDurationSeconds;
            PushMotionDurationSeconds = pushMotionDurationSeconds;
            FlipMotionDurationSeconds = flipMotionDurationSeconds;
        }

        public float MoveMotionDurationSeconds { get; }

        public float PushMotionDurationSeconds { get; }

        public float FlipMotionDurationSeconds { get; }

        public bool TryGetOverrideDurationSeconds(
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            durationSeconds = motionKind switch
            {
                TickEntityMotionKind.Move => MoveMotionDurationSeconds,
                TickEntityMotionKind.Push => PushMotionDurationSeconds,
                TickEntityMotionKind.Flip => FlipMotionDurationSeconds,
                _ => EntityMotionPresentationAuthoring.UseGlobalTimingSentinel,
            };

            return EntityMotionPresentationAuthoring.IsOverrideDuration(durationSeconds);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EntityMotionPresentationAuthoring : MonoBehaviour
    {
        public const float UseGlobalTimingSentinel = -1f;

        [SerializeField] private float moveMotionDurationSeconds = UseGlobalTimingSentinel;
        [SerializeField] private float pushMotionDurationSeconds = UseGlobalTimingSentinel;
        [SerializeField] private float flipMotionDurationSeconds = UseGlobalTimingSentinel;

        public float MoveMotionDurationSeconds => moveMotionDurationSeconds;

        public float PushMotionDurationSeconds => pushMotionDurationSeconds;

        public float FlipMotionDurationSeconds => flipMotionDurationSeconds;

        public void Validate()
        {
            ValidateOverrideDuration(moveMotionDurationSeconds, nameof(moveMotionDurationSeconds));
            ValidateOverrideDuration(pushMotionDurationSeconds, nameof(pushMotionDurationSeconds));
            ValidateOverrideDuration(flipMotionDurationSeconds, nameof(flipMotionDurationSeconds));
        }

        public EntityMotionPresentationSnapshot CreateSnapshot()
        {
            Validate();
            return new EntityMotionPresentationSnapshot(
                moveMotionDurationSeconds,
                pushMotionDurationSeconds,
                flipMotionDurationSeconds);
        }

        public bool TryGetMotionDurationOverride(
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            return CreateSnapshot().TryGetOverrideDurationSeconds(motionKind, out durationSeconds);
        }

        public static bool IsOverrideDuration(float motionDurationSeconds)
        {
            return motionDurationSeconds > 0f;
        }

        internal static EntityMotionPresentationAuthoring GetOptionalValidatedAuthoring(GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EntityMotionPresentationAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }

        private static void ValidateOverrideDuration(float motionDurationSeconds, string parameterName)
        {
            if (motionDurationSeconds == UseGlobalTimingSentinel)
            {
                return;
            }

            if (motionDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Motion duration override must be greater than zero, or -1 to use the global timing fallback.");
            }
        }
    }
}
