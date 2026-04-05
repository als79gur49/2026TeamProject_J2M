using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct UnitLocomotionPresentationSnapshot
    {
        public UnitLocomotionPresentationSnapshot(float moveMotionDurationSeconds)
        {
            MoveMotionDurationSeconds = moveMotionDurationSeconds;
        }

        public float MoveMotionDurationSeconds { get; }

        public bool TryGetOverrideDurationSeconds(
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            durationSeconds = motionKind == TickEntityMotionKind.Move
                ? MoveMotionDurationSeconds
                : UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel;

            return UnitLocomotionPresentationAuthoring.IsOverrideDuration(durationSeconds);
        }
    }

    [DisallowMultipleComponent]
    public sealed class UnitLocomotionPresentationAuthoring : MonoBehaviour
    {
        public const float UseGlobalTimingSentinel = EntityMotionPresentationAuthoring.UseGlobalTimingSentinel;

        [SerializeField] private float moveMotionDurationSeconds = UseGlobalTimingSentinel;

        public float MoveMotionDurationSeconds => moveMotionDurationSeconds;

        public void Validate()
        {
            ValidateOverrideDuration(moveMotionDurationSeconds, nameof(moveMotionDurationSeconds));
        }

        public UnitLocomotionPresentationSnapshot CreateSnapshot()
        {
            Validate();
            return new UnitLocomotionPresentationSnapshot(moveMotionDurationSeconds);
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

        public static UnitLocomotionPresentationAuthoring GetOptionalValidatedAuthoring(GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<UnitLocomotionPresentationAuthoring>(out var authoring) ||
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
                    "Move motion duration override must be greater than zero, or -1 to use the global timing fallback.");
            }
        }
    }
}
