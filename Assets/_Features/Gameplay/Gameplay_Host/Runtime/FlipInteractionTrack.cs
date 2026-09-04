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
        private float _previousPresentedNormalizedTime;

        private const float DefaultBoxWindupLift = 0.11f;
        private const float DefaultBoxFollowLift = 0.055f;
        private const float DefaultWindupTiltDegrees = 18f;
        private const float DefaultFollowTiltDegrees = 14f;
        private const float FollowThroughLiftMultiplier = 1.35f;
        private const float FollowThroughTiltMultiplier = 1.45f;
        private const float FollowThroughPostContactHoldNormalizedDuration = 0.08f;
        private const float StayLiftMultiplier = 1.35f;
        private const float StayTiltMultiplier = 1.40f;
        private const float DestroySelfLiftMultiplier = 1.25f;
        private const float DestroySelfTiltMultiplier = 1.30f;
        private const float BlockedInitialLiftMultiplier = 0.80f;
        private const float BlockedInitialTiltMultiplier = 0.65f;
        private const float BlockedRecoveryStartWeight = 0.25f;
        private const float StayRecoveryStartWeight = 0.55f;

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
                new FlipImpactTimingSettings(0.62f, 0.55f, 0.22f, 0.18f),
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
            _previousPresentedNormalizedTime = 0f;
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

        public int SourceActionPlanId { get; private set; }

        public int SourceTickIndex { get; private set; }

        public bool IsComplete => Phase == FlipInteractionPhase.Complete;

        public void SetPhase(FlipInteractionPhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            ElapsedSeconds = 0f;
            _previousPresentedNormalizedTime = 0f;
        }

        public void UpdateFlipOutcome(
            TickPlayerFlipOutcomeKind flipOutcome,
            FlipImpactTimingSettings flipImpactTimingSettings)
        {
            FlipOutcome = flipOutcome;
        }

        public void CorrelateSourceActionPlan(int sourceTickIndex, int sourceActionPlanId)
        {
            if (sourceTickIndex >= 0 &&
                sourceActionPlanId > 0 &&
                SourceActionPlanId <= 0)
            {
                SourceTickIndex = sourceTickIndex;
                SourceActionPlanId = sourceActionPlanId;
            }
        }

        internal bool TryCaptureProgress(out MotionTrackProgressSample progressSample)
        {
            if (Phase != FlipInteractionPhase.AirborneFollow ||
                SourceActionPlanId <= 0)
            {
                progressSample = default;
                return false;
            }

            var currentNormalizedTime = GetNormalizedPhaseTime();
            progressSample = new MotionTrackProgressSample(
                SourceTickIndex,
                BoxEntityId,
                TickEntityMotionKind.Flip,
                _previousPresentedNormalizedTime,
                currentNormalizedTime,
                SourceActionPlanId,
                MotionTrackProgressSourceKind.FlipInteraction);
            _previousPresentedNormalizedTime = currentNormalizedTime;
            return true;
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
            return FlipOutcome switch
            {
                TickPlayerFlipOutcomeKind.FollowThrough => SampleFollowThroughAirborneFollow(boxGripWorldPose),
                TickPlayerFlipOutcomeKind.DestroySelf => SampleDestroySelfAirborneFollow(boxGripWorldPose),
                TickPlayerFlipOutcomeKind.Stay => SampleStayAirborneFollow(boxGripWorldPose),
                TickPlayerFlipOutcomeKind.Blocked => SampleBlockedAirborneFollow(boxGripWorldPose),
                _ => CreateAirborneFollowSample(
                    boxGripWorldPose,
                    handWeight: 1f,
                    boxWeight: 1f,
                    liftAmount: DefaultBoxFollowLift,
                    tiltDegrees: DefaultFollowTiltDegrees),
            };
        }

        private FlipInteractionSample SampleRecovery(in Pose handRestWorldPose, in Pose boxGripWorldPose)
        {
            var t = EaseInOut(GetNormalizedPhaseTime());
            var startWeight = FlipOutcome switch
            {
                TickPlayerFlipOutcomeKind.Stay => StayRecoveryStartWeight,
                TickPlayerFlipOutcomeKind.Blocked => BlockedRecoveryStartWeight,
                _ => 1f,
            };
            return new FlipInteractionSample(
                LerpPose(boxGripWorldPose, handRestWorldPose, t),
                Mathf.Lerp(startWeight, 0f, t),
                Vector3.LerpUnclamped(ResolveBoxPositionOffset(DefaultBoxFollowLift), Vector3.zero, t),
                Quaternion.SlerpUnclamped(ResolveBoxRotationOffset(DefaultFollowTiltDegrees), Quaternion.identity, t),
                Mathf.Lerp(startWeight, 0f, t));
        }

        private FlipInteractionSample SampleFollowThroughAirborneFollow(in Pose boxGripWorldPose)
        {
            var normalizedTime = GetNormalizedPhaseTime();
            var contactTime = FlipImpactTimingSettings.ContactNormalizedTime;
            if (normalizedTime <= contactTime)
            {
                var preContactTime = NormalizeBranchTime(normalizedTime, contactTime);
                var buildTime = EaseOutCubic(preContactTime);
                return CreateAirborneFollowSample(
                    boxGripWorldPose,
                    handWeight: 1f,
                    boxWeight: 1f,
                    liftAmount: Mathf.Lerp(
                        DefaultBoxFollowLift,
                        DefaultBoxFollowLift * FollowThroughLiftMultiplier,
                        buildTime),
                    tiltDegrees: Mathf.Lerp(
                        DefaultFollowTiltDegrees,
                        DefaultFollowTiltDegrees * FollowThroughTiltMultiplier,
                        buildTime));
            }

            var postContactTime = NormalizeBranchTime(normalizedTime - contactTime, 1f - contactTime);
            if (postContactTime <= FollowThroughPostContactHoldNormalizedDuration)
            {
                return CreateAirborneFollowSample(
                    boxGripWorldPose,
                    handWeight: 1f,
                    boxWeight: 1f,
                    liftAmount: DefaultBoxFollowLift * FollowThroughLiftMultiplier,
                    tiltDegrees: DefaultFollowTiltDegrees * FollowThroughTiltMultiplier);
            }

            var releaseTime = NormalizeBranchTime(
                postContactTime - FollowThroughPostContactHoldNormalizedDuration,
                1f - FollowThroughPostContactHoldNormalizedDuration);
            var easedReleaseTime = EaseInOut(releaseTime);
            return CreateAirborneFollowSample(
                boxGripWorldPose,
                handWeight: 1f,
                boxWeight: 1f,
                liftAmount: Mathf.Lerp(
                    DefaultBoxFollowLift * FollowThroughLiftMultiplier,
                    DefaultBoxFollowLift,
                    easedReleaseTime),
                tiltDegrees: Mathf.Lerp(
                    DefaultFollowTiltDegrees * FollowThroughTiltMultiplier,
                    DefaultFollowTiltDegrees,
                    easedReleaseTime));
        }

        private FlipInteractionSample SampleDestroySelfAirborneFollow(in Pose boxGripWorldPose)
        {
            var normalizedTime = GetNormalizedPhaseTime();
            var contactTime = FlipImpactTimingSettings.ContactNormalizedTime;
            if (normalizedTime <= contactTime)
            {
                var preContactTime = NormalizeBranchTime(normalizedTime, contactTime);
                var buildTime = EaseOutCubic(preContactTime);
                return CreateAirborneFollowSample(
                    boxGripWorldPose,
                    handWeight: 1f,
                    boxWeight: 1f,
                    liftAmount: Mathf.Lerp(
                        DefaultBoxFollowLift,
                        DefaultBoxFollowLift * DestroySelfLiftMultiplier,
                        buildTime),
                    tiltDegrees: Mathf.Lerp(
                        DefaultFollowTiltDegrees,
                        DefaultFollowTiltDegrees * DestroySelfTiltMultiplier,
                        buildTime));
            }

            var postContactTime = NormalizeBranchTime(normalizedTime - contactTime, 1f - contactTime);
            // DestroySelf keeps the centralized onset threshold as the player release point even
            // when the box visual continues its remaining flip flight while breaking.
            var releaseTime = EaseOutCubic(postContactTime);
            var weight = Mathf.Lerp(1f, 0f, releaseTime);
            return CreateAirborneFollowSample(
                boxGripWorldPose,
                handWeight: weight,
                boxWeight: weight,
                liftAmount: Mathf.Lerp(
                    DefaultBoxFollowLift * DestroySelfLiftMultiplier,
                    0f,
                    releaseTime),
                tiltDegrees: Mathf.Lerp(
                    DefaultFollowTiltDegrees * DestroySelfTiltMultiplier,
                    0f,
                    releaseTime));
        }

        private FlipInteractionSample SampleStayAirborneFollow(in Pose boxGripWorldPose)
        {
            var normalizedTime = GetNormalizedPhaseTime();
            var contactTime = FlipImpactTimingSettings.ContactNormalizedTime;
            if (normalizedTime <= contactTime)
            {
                var preContactTime = NormalizeBranchTime(normalizedTime, contactTime);
                var buildTime = EaseOutCubic(preContactTime);
                return CreateAirborneFollowSample(
                    boxGripWorldPose,
                    handWeight: 1f,
                    boxWeight: 1f,
                    liftAmount: Mathf.Lerp(
                        DefaultBoxFollowLift,
                        DefaultBoxFollowLift * StayLiftMultiplier,
                        buildTime),
                    tiltDegrees: Mathf.Lerp(
                        DefaultFollowTiltDegrees,
                        DefaultFollowTiltDegrees * StayTiltMultiplier,
                        buildTime));
            }

            var postContactTime = NormalizeBranchTime(normalizedTime - contactTime, 1f - contactTime);
            var holdDuration = Mathf.Clamp01(FlipImpactTimingSettings.StayPostContactHoldNormalizedDuration);
            if (postContactTime <= holdDuration)
            {
                return CreateAirborneFollowSample(
                    boxGripWorldPose,
                    handWeight: 1f,
                    boxWeight: 1f,
                    liftAmount: DefaultBoxFollowLift * StayLiftMultiplier,
                    tiltDegrees: DefaultFollowTiltDegrees * StayTiltMultiplier);
            }

            var releaseTime = NormalizeBranchTime(postContactTime - holdDuration, 1f - holdDuration);
            var stayWeight = Mathf.Lerp(1f, StayRecoveryStartWeight, EaseInOut(releaseTime));
            return CreateAirborneFollowSample(
                boxGripWorldPose,
                handWeight: stayWeight,
                boxWeight: stayWeight,
                liftAmount: Mathf.Lerp(
                    DefaultBoxFollowLift * StayLiftMultiplier,
                    0f,
                    releaseTime),
                tiltDegrees: Mathf.Lerp(
                    DefaultFollowTiltDegrees * StayTiltMultiplier,
                    0f,
                    releaseTime));
        }

        private FlipInteractionSample SampleBlockedAirborneFollow(in Pose boxGripWorldPose)
        {
            var abortTime = EaseOutCubic(GetNormalizedPhaseTime());
            var blockedWeight = Mathf.Lerp(1f, BlockedRecoveryStartWeight, abortTime);
            return CreateAirborneFollowSample(
                boxGripWorldPose,
                handWeight: blockedWeight,
                boxWeight: blockedWeight,
                liftAmount: Mathf.Lerp(
                    DefaultBoxFollowLift * BlockedInitialLiftMultiplier,
                    0f,
                    abortTime),
                tiltDegrees: Mathf.Lerp(
                    DefaultFollowTiltDegrees * BlockedInitialTiltMultiplier,
                    0f,
                    abortTime));
        }

        private FlipInteractionSample CreateAirborneFollowSample(
            in Pose boxGripWorldPose,
            float handWeight,
            float boxWeight,
            float liftAmount,
            float tiltDegrees)
        {
            return new FlipInteractionSample(
                boxGripWorldPose,
                handWeight,
                ResolveBoxPositionOffset(liftAmount),
                ResolveBoxRotationOffset(tiltDegrees),
                boxWeight);
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

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - (inverse * inverse * inverse);
        }

        private static float NormalizeBranchTime(float numerator, float denominator)
        {
            return Mathf.Clamp01(numerator / Mathf.Max(0.0001f, denominator));
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
