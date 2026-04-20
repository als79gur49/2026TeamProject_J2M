using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal enum FlipInteractionPhase
    {
        None = 0,
        Windup = 1,
        AirborneFollow = 2,
        Recovery = 3,
        Complete = 4,
    }

    internal readonly struct FlipInteractionSample
    {
        public FlipInteractionSample(
            Pose handTargetWorldPose,
            float handWeight,
            Vector3 boxLocalPositionOffset,
            Quaternion boxLocalRotationOffset,
            float boxWeight)
        {
            HandTargetWorldPose = handTargetWorldPose;
            HandWeight = handWeight;
            BoxLocalPositionOffset = boxLocalPositionOffset;
            BoxLocalRotationOffset = boxLocalRotationOffset;
            BoxWeight = boxWeight;
        }

        public Pose HandTargetWorldPose { get; }

        public float HandWeight { get; }

        public Vector3 BoxLocalPositionOffset { get; }

        public Quaternion BoxLocalRotationOffset { get; }

        public float BoxWeight { get; }
    }

    internal readonly struct FlipInteractionResetRequest
    {
        public FlipInteractionResetRequest(int playerEntityId, int boxEntityId)
        {
            PlayerEntityId = playerEntityId;
            BoxEntityId = boxEntityId;
        }

        public int PlayerEntityId { get; }

        public int BoxEntityId { get; }
    }

    internal sealed class FlipInteractionTrack
    {
        private const float DefaultBoxWindupLift = 0.06f;
        private const float DefaultBoxFollowLift = 0.025f;
        private const float DefaultWindupTiltDegrees = 10f;
        private const float DefaultFollowTiltDegrees = 6f;

        public FlipInteractionTrack(
            int playerEntityId,
            int boxEntityId,
            int actionSequence,
            Direction direction,
            float windupDurationSeconds,
            float followDurationSeconds,
            float recoveryDurationSeconds,
            FlipInteractionPhase phase = FlipInteractionPhase.None)
            : this(
                playerEntityId,
                boxEntityId,
                actionSequence,
                direction,
                windupDurationSeconds,
                followDurationSeconds,
                recoveryDurationSeconds,
                new FlipImpactTimingSettings(0.70f, 0.35f, 0.30f, 0.10f),
                TickPlayerFlipOutcomeKind.None,
                phase)
        {
        }

        public FlipInteractionTrack(
            int playerEntityId,
            int boxEntityId,
            int actionSequence,
            Direction direction,
            float windupDurationSeconds,
            float followDurationSeconds,
            float recoveryDurationSeconds,
            FlipImpactTimingSettings flipImpactTimingSettings,
            TickPlayerFlipOutcomeKind flipOutcome = TickPlayerFlipOutcomeKind.None,
            FlipInteractionPhase phase = FlipInteractionPhase.None)
        {
            PlayerEntityId = playerEntityId;
            BoxEntityId = boxEntityId;
            ActionSequence = actionSequence;
            Direction = direction;
            WindupDurationSeconds = Mathf.Max(0.0001f, windupDurationSeconds);
            FollowDurationSeconds = Mathf.Max(0.0001f, followDurationSeconds);
            RecoveryDurationSeconds = Mathf.Max(0.0001f, recoveryDurationSeconds);
            FlipImpactTimingSettings = flipImpactTimingSettings;
            FlipOutcome = flipOutcome;
            Phase = phase;
            ElapsedSeconds = 0f;
        }

        public int PlayerEntityId { get; }

        public int BoxEntityId { get; }

        public int ActionSequence { get; }

        public Direction Direction { get; }

        public FlipInteractionPhase Phase { get; private set; }

        public float ElapsedSeconds { get; private set; }

        public float WindupDurationSeconds { get; }

        public float FollowDurationSeconds { get; }

        public float RecoveryDurationSeconds { get; }

        public FlipImpactTimingSettings FlipImpactTimingSettings { get; }

        public TickPlayerFlipOutcomeKind FlipOutcome { get; private set; }

        public bool IsComplete => Phase == FlipInteractionPhase.Complete;

        public void SetPhase(FlipInteractionPhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            ElapsedSeconds = 0f;
        }

        public void UpdateFlipOutcome(
            TickPlayerFlipOutcomeKind flipOutcome,
            FlipImpactTimingSettings flipImpactTimingSettings)
        {
            FlipOutcome = flipOutcome;
        }

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f ||
                Phase == FlipInteractionPhase.None ||
                Phase == FlipInteractionPhase.Complete)
            {
                return;
            }

            ElapsedSeconds = Mathf.Min(GetPhaseDurationSeconds(), ElapsedSeconds + deltaTime);
            if (Phase == FlipInteractionPhase.Recovery &&
                ElapsedSeconds >= RecoveryDurationSeconds - 0.0001f)
            {
                Phase = FlipInteractionPhase.Complete;
            }
        }

        public FlipInteractionSample Sample(in Pose handRestWorldPose, in Pose boxGripWorldPose)
        {
            return Phase switch
            {
                FlipInteractionPhase.Windup => SampleWindup(handRestWorldPose, boxGripWorldPose),
                FlipInteractionPhase.AirborneFollow => SampleAirborneFollow(boxGripWorldPose),
                FlipInteractionPhase.Recovery => SampleRecovery(handRestWorldPose, boxGripWorldPose),
                _ => new FlipInteractionSample(
                    handRestWorldPose,
                    0f,
                    Vector3.zero,
                    Quaternion.identity,
                    0f),
            };
        }

        private float GetNormalizedPhaseTime()
        {
            return Mathf.Clamp01(ElapsedSeconds / GetPhaseDurationSeconds());
        }

        private float GetPhaseDurationSeconds()
        {
            return Phase switch
            {
                FlipInteractionPhase.Windup => WindupDurationSeconds,
                FlipInteractionPhase.AirborneFollow => FollowDurationSeconds,
                FlipInteractionPhase.Recovery => RecoveryDurationSeconds,
                _ => 0.0001f,
            };
        }

        private FlipInteractionSample SampleWindup(in Pose handRestWorldPose, in Pose boxGripWorldPose)
        {
            var t = EaseInOut(GetNormalizedPhaseTime());
            return new FlipInteractionSample(
                LerpPose(handRestWorldPose, boxGripWorldPose, t),
                t,
                Vector3.LerpUnclamped(Vector3.zero, ResolveBoxPositionOffset(DefaultBoxWindupLift), t),
                Quaternion.SlerpUnclamped(Quaternion.identity, ResolveBoxRotationOffset(DefaultWindupTiltDegrees), t),
                t);
        }

        private FlipInteractionSample SampleAirborneFollow(in Pose boxGripWorldPose)
        {
            if (FlipOutcome == TickPlayerFlipOutcomeKind.DestroySelf ||
                FlipOutcome == TickPlayerFlipOutcomeKind.Stay)
            {
                return SampleImpactAwareAirborneFollow(boxGripWorldPose);
            }

            return new FlipInteractionSample(
                boxGripWorldPose,
                1f,
                ResolveBoxPositionOffset(DefaultBoxFollowLift),
                ResolveBoxRotationOffset(DefaultFollowTiltDegrees),
                1f);
        }

        private FlipInteractionSample SampleRecovery(in Pose handRestWorldPose, in Pose boxGripWorldPose)
        {
            var t = EaseInOut(GetNormalizedPhaseTime());
            var startWeight = FlipOutcome == TickPlayerFlipOutcomeKind.Stay
                ? 0.45f
                : 1f;
            return new FlipInteractionSample(
                LerpPose(boxGripWorldPose, handRestWorldPose, t),
                Mathf.Lerp(startWeight, 0f, t),
                Vector3.LerpUnclamped(ResolveBoxPositionOffset(DefaultBoxFollowLift), Vector3.zero, t),
                Quaternion.SlerpUnclamped(ResolveBoxRotationOffset(DefaultFollowTiltDegrees), Quaternion.identity, t),
                Mathf.Lerp(startWeight, 0f, t));
        }

        private FlipInteractionSample SampleImpactAwareAirborneFollow(in Pose boxGripWorldPose)
        {
            var normalizedTime = GetNormalizedPhaseTime();
            var contactTime = FlipImpactTimingSettings.ContactNormalizedTime;
            if (normalizedTime <= contactTime)
            {
                return new FlipInteractionSample(
                    boxGripWorldPose,
                    1f,
                    ResolveBoxPositionOffset(DefaultBoxFollowLift),
                    ResolveBoxRotationOffset(DefaultFollowTiltDegrees),
                    1f);
            }

            var postContactDenominator = Mathf.Max(0.0001f, 1f - contactTime);
            var postContactTime = Mathf.Clamp01((normalizedTime - contactTime) / postContactDenominator);
            if (FlipOutcome == TickPlayerFlipOutcomeKind.DestroySelf)
            {
                // DestroySelf keeps the centralized onset threshold as the player release point even
                // when the box visual continues its remaining flip flight while breaking.
                var weight = 1f - EaseInOut(postContactTime);
                return new FlipInteractionSample(
                    boxGripWorldPose,
                    weight,
                    Vector3.LerpUnclamped(ResolveBoxPositionOffset(DefaultBoxFollowLift), Vector3.zero, postContactTime),
                    Quaternion.SlerpUnclamped(ResolveBoxRotationOffset(DefaultFollowTiltDegrees), Quaternion.identity, postContactTime),
                    weight);
            }

            var holdDuration = Mathf.Clamp01(FlipImpactTimingSettings.StayPostContactHoldNormalizedDuration);
            if (postContactTime <= holdDuration)
            {
                return new FlipInteractionSample(
                    boxGripWorldPose,
                    1f,
                    ResolveBoxPositionOffset(DefaultBoxFollowLift),
                    ResolveBoxRotationOffset(DefaultFollowTiltDegrees),
                    1f);
            }

            var releaseDenominator = Mathf.Max(0.0001f, 1f - holdDuration);
            var releaseTime = Mathf.Clamp01((postContactTime - holdDuration) / releaseDenominator);
            var stayWeight = Mathf.Lerp(1f, 0.45f, EaseInOut(releaseTime));
            return new FlipInteractionSample(
                boxGripWorldPose,
                stayWeight,
                Vector3.LerpUnclamped(ResolveBoxPositionOffset(DefaultBoxFollowLift), Vector3.zero, releaseTime),
                Quaternion.SlerpUnclamped(ResolveBoxRotationOffset(DefaultFollowTiltDegrees), Quaternion.identity, releaseTime),
                stayWeight);
        }

        private Vector3 ResolveBoxPositionOffset(float liftAmount)
        {
            return -Vector3.forward * Mathf.Max(0f, liftAmount);
        }

        private Quaternion ResolveBoxRotationOffset(float tiltDegrees)
        {
            var clampedTilt = Mathf.Max(0f, tiltDegrees);
            return Direction switch
            {
                Direction.Up => Quaternion.AngleAxis(-clampedTilt, Vector3.right),
                Direction.Down => Quaternion.AngleAxis(clampedTilt, Vector3.right),
                Direction.Left => Quaternion.AngleAxis(clampedTilt, Vector3.up),
                Direction.Right => Quaternion.AngleAxis(-clampedTilt, Vector3.up),
                _ => Quaternion.identity,
            };
        }

        private static float EaseInOut(float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        }

        private static Pose LerpPose(in Pose start, in Pose end, float t)
        {
            var clampedT = Mathf.Clamp01(t);
            return new Pose(
                Vector3.LerpUnclamped(start.position, end.position, clampedT),
                Quaternion.SlerpUnclamped(start.rotation, end.rotation, clampedT));
        }
    }
}
