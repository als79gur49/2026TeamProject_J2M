using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum EnemyJumpLandingCameraFeedbackKind
    {
        None = 0,
        Heavy = 1,
    }

    public readonly struct EnemyJumpMotionPresentationSnapshot
    {
        public EnemyJumpMotionPresentationSnapshot(float horizontalHoldBias, float apexHoldPower)
        {
            HorizontalHoldBias = horizontalHoldBias;
            ApexHoldPower = apexHoldPower;
            Validate();
        }

        public float HorizontalHoldBias { get; }

        public float ApexHoldPower { get; }

        public void Validate()
        {
            EnemyJumpMotionPresentationAuthoring.ValidateSettings(
                HorizontalHoldBias,
                ApexHoldPower);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnemyJumpMotionPresentationAuthoring : MonoBehaviour
    {
        [SerializeField] private float horizontalHoldBias = 0.08f;
        [SerializeField] private float apexHoldPower = 3.5f;
        [SerializeField]
        private EnemyJumpLandingCameraFeedbackKind jumpLandingCameraFeedback =
            EnemyJumpLandingCameraFeedbackKind.None;

        public float HorizontalHoldBias => horizontalHoldBias;

        public float ApexHoldPower => apexHoldPower;

        public EnemyJumpLandingCameraFeedbackKind JumpLandingCameraFeedback =>
            jumpLandingCameraFeedback;

        public void Validate()
        {
            ValidateSettings(horizontalHoldBias, apexHoldPower);
            if (!Enum.IsDefined(
                    typeof(EnemyJumpLandingCameraFeedbackKind),
                    jumpLandingCameraFeedback))
            {
                throw new InvalidOperationException(
                    $"Enemy jump presentation authoring on '{name}' has an invalid landing camera feedback kind.");
            }
        }

        internal void SetJumpLandingCameraFeedbackForTests(
            EnemyJumpLandingCameraFeedbackKind kind)
        {
            jumpLandingCameraFeedback = kind;
            Validate();
        }

        public EnemyJumpMotionPresentationSnapshot CreateSnapshot()
        {
            Validate();
            return new EnemyJumpMotionPresentationSnapshot(horizontalHoldBias, apexHoldPower);
        }

        public static EnemyJumpMotionPresentationAuthoring GetOptionalValidatedAuthoring(
            GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EnemyJumpMotionPresentationAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }

        internal static void ValidateSettings(float horizontalHoldBias, float apexHoldPower)
        {
            if (horizontalHoldBias < 0f || horizontalHoldBias > 0.25f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(horizontalHoldBias),
                    "Horizontal jump hold bias must stay between 0 and 0.25 to keep path progress continuous.");
            }

            if (apexHoldPower < 2f || apexHoldPower > 8f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(apexHoldPower),
                    "Jump apex hold power must stay between 2 and 8. A value of 2 matches the legacy parabola.");
            }
        }
    }
}
