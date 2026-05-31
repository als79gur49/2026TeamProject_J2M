using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class MotionTrack
    {
        private readonly List<MotionClip> _clips = new();

        public bool HasClips => _clips.Count > 0;

        public int LastOperationId { get; private set; }

        public TickEntityMotionKind TailMotionKind => _clips[_clips.Count - 1].MotionKind;

        public GameplayEntityPose TailEndPose => _clips[_clips.Count - 1].EndPose;

        public float TotalRemainingSeconds
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < _clips.Count; i++)
                {
                    total += _clips[i].RemainingSeconds;
                }

                return total;
            }
        }

        public void Append(MotionClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            _clips.Add(clip);
            LastOperationId = clip.OperationId;
        }

        public void Clear()
        {
            _clips.Clear();
            LastOperationId = 0;
        }

        public void AlignToCommittedTargetPose(GameplayEntityPose committedTargetPose)
        {
            if (_clips.Count == 0)
            {
                return;
            }

            var positionDelta = committedTargetPose.Position - TailEndPose.Position;
            if (positionDelta.sqrMagnitude > 0f)
            {
                for (var i = 0; i < _clips.Count; i++)
                {
                    _clips[i].Translate(positionDelta);
                }
            }

            _clips[_clips.Count - 1].SetEndPose(committedTargetPose);
        }

        public GameplayEntityPose SampleAndAdvance(float deltaTime, GameplayEntityPose fallbackPose)
        {
            return SampleAndAdvance(deltaTime, fallbackPose, out _);
        }

        public GameplayEntityPose SampleAndAdvance(
            float deltaTime,
            GameplayEntityPose fallbackPose,
            out Vector3 visualScaleMultiplier)
        {
            visualScaleMultiplier = Vector3.one;
            if (_clips.Count == 0)
            {
                LastOperationId = 0;
                return fallbackPose;
            }

            var remainingDeltaTime = deltaTime;
            while (_clips.Count > 0)
            {
                var clip = _clips[0];
                remainingDeltaTime = clip.Advance(remainingDeltaTime);
                var pose = clip.IsComplete
                    ? clip.EndPose
                    : clip.Sample();
                visualScaleMultiplier = clip.SampleVisualScaleMultiplier();
                if (!clip.IsComplete)
                {
                    return pose;
                }

                _clips.RemoveAt(0);
                if (_clips.Count == 0)
                {
                    visualScaleMultiplier = Vector3.one;
                    LastOperationId = 0;
                    return fallbackPose;
                }

                if (remainingDeltaTime <= 0f)
                {
                    return pose;
                }
            }

            return fallbackPose;
        }
    }

    public sealed class MotionClip
    {
        private readonly float _flipPeakHeightWorld;
        private readonly bool _interpolateRotation;
        private readonly TickEntityMotionKind _motionKind;

        private MotionClip(
            TickEntityMotionKind motionKind,
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds,
            bool interpolateRotation,
            float flipPeakHeightWorld,
            int operationId)
        {
            _motionKind = motionKind;
            _interpolateRotation = interpolateRotation;
            _flipPeakHeightWorld = flipPeakHeightWorld;
            StartPose = startPose;
            EndPose = endPose;
            DurationSeconds = durationSeconds;
            ElapsedSeconds = 0f;
            OperationId = operationId;
        }

        public float DurationSeconds { get; }

        public float ElapsedSeconds { get; private set; }

        public GameplayEntityPose EndPose { get; private set; }

        public bool IsComplete => RemainingSeconds <= 0.0001f;

        public TickEntityMotionKind MotionKind => _motionKind;

        public int OperationId { get; }

        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

        public GameplayEntityPose StartPose { get; private set; }

        public static MotionClip Create(
            TickEntityMotionKind motionKind,
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds,
            bool interpolateRotation,
            float flipPeakHeightWorld,
            int operationId = 0)
        {
            return new MotionClip(
                motionKind,
                startPose,
                endPose,
                Mathf.Max(durationSeconds, 0.0001f),
                interpolateRotation,
                flipPeakHeightWorld,
                operationId);
        }

        public float Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            var consumedTime = Mathf.Min(RemainingSeconds, deltaTime);
            ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
            return Mathf.Max(0f, deltaTime - consumedTime);
        }

        public GameplayEntityPose Sample()
        {
            var t = DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);

            return _motionKind switch
            {
                TickEntityMotionKind.Flip => SampleFlip(t),
                TickEntityMotionKind.BoxSlide => SampleLinearConstant(t),
                TickEntityMotionKind.Move => SampleLinearConstant(t),
                _ => SampleLinear(t),
            };
        }

        public Vector3 SampleVisualScaleMultiplier()
        {
            var t = DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);

            return _motionKind switch
            {
                TickEntityMotionKind.Flip => BoxMotionVisualScaleSampler.SampleFlipMotion(t),
                TickEntityMotionKind.BoxSlide => BoxMotionVisualScaleSampler.SampleSlideMotion(
                    t,
                    Quaternion.Inverse(StartPose.Rotation) * (EndPose.Position - StartPose.Position)),
                _ => Vector3.one,
            };
        }

        public void SetEndPose(GameplayEntityPose endPose)
        {
            EndPose = endPose;
        }

        public void Translate(Vector3 positionDelta)
        {
            if (positionDelta.sqrMagnitude <= 0f)
            {
                return;
            }

            StartPose = new GameplayEntityPose(StartPose.Position + positionDelta, StartPose.Rotation);
            EndPose = new GameplayEntityPose(EndPose.Position + positionDelta, EndPose.Rotation);
        }

        private static float EaseOutQuad(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - (inverse * inverse);
        }

        private GameplayEntityPose SampleFlip(float t)
        {
            return BoxFlipSlamSampler.Sample(
                StartPose,
                EndPose,
                Mathf.Clamp01(t),
                _flipPeakHeightWorld);
        }

        private GameplayEntityPose SampleLinear(float t)
        {
            var easedT = EaseOutQuad(t);
            return new GameplayEntityPose(
                Vector3.LerpUnclamped(StartPose.Position, EndPose.Position, easedT),
                _interpolateRotation
                    ? Quaternion.SlerpUnclamped(StartPose.Rotation, EndPose.Rotation, easedT)
                    : EndPose.Rotation);
        }

        private GameplayEntityPose SampleLinearConstant(float t)
        {
            var clampedT = Mathf.Clamp01(t);
            return new GameplayEntityPose(
                Vector3.LerpUnclamped(StartPose.Position, EndPose.Position, clampedT),
                _interpolateRotation
                    ? Quaternion.SlerpUnclamped(StartPose.Rotation, EndPose.Rotation, clampedT)
                    : EndPose.Rotation);
        }
    }

    internal static class BoxFlipSlamSampler
    {
        public const float LiftEndTime = 0.34f;
        public const float HoldEndTime = 0.48f;
        public const float SlamEndTime = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;

        private const float LateralArcTravelFraction = 0.18f;

        public static GameplayEntityPose Sample(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime,
            float peakHeightWorld)
        {
            var t = Mathf.Clamp01(normalizedTime);
            if (!TryResolveFlipBasis(startPose, endPose, out var liftAxis, out var flipAxis))
            {
                return SampleFallback(startPose, endPose, t);
            }

            var arcProgress = SampleArcProgress(t);
            var position = SamplePivotArcPosition(
                startPose.Position,
                endPose.Position,
                liftAxis,
                flipAxis,
                arcProgress,
                peakHeightWorld);

            return new GameplayEntityPose(
                t >= 1f ? endPose.Position : position,
                SampleFlipRotation(startPose, endPose, arcProgress, flipAxis));
        }

        private static GameplayEntityPose SampleFallback(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime)
        {
            var phaseTime = SampleArcProgress(normalizedTime);
            return new GameplayEntityPose(
                Vector3.LerpUnclamped(startPose.Position, endPose.Position, phaseTime),
                Quaternion.SlerpUnclamped(startPose.Rotation, endPose.Rotation, phaseTime));
        }

        public static bool TryResolveLiftAxis(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            out Vector3 liftAxis)
        {
            if (TryResolveFlipBasis(startPose, endPose, out liftAxis, out _))
            {
                return true;
            }

            liftAxis = default;
            return false;
        }

        private static Vector3 SamplePivotArcPosition(
            Vector3 startPosition,
            Vector3 endPosition,
            Vector3 liftAxis,
            Vector3 lateralAxis,
            float arcProgress,
            float peakHeightWorld)
        {
            var clampedProgress = Mathf.Clamp01(arcProgress);
            var travelDelta = endPosition - startPosition;
            var travelLength = travelDelta.magnitude;
            if (travelLength <= 0.000001f)
            {
                return Vector3.LerpUnclamped(startPosition, endPosition, clampedProgress);
            }

            var travelDirection = travelDelta / travelLength;
            var pivot = (startPosition + endPosition) * 0.5f;
            var halfTravel = travelLength * 0.5f;
            var arcAngle = Mathf.PI * clampedProgress;
            var alongTravel = -Mathf.Cos(arcAngle) * halfTravel;
            var lift = Mathf.Sin(arcAngle) * Mathf.Max(0f, peakHeightWorld);
            var lateral = Mathf.Sin(arcAngle) * travelLength * LateralArcTravelFraction;
            return pivot +
                   (travelDirection * alongTravel) +
                   (liftAxis * lift) +
                   (lateralAxis.normalized * lateral);
        }

        private static float SampleArcProgress(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            if (t <= LiftEndTime)
            {
                return 0.5f * EaseOutSine(t / LiftEndTime);
            }

            if (t <= HoldEndTime)
            {
                return 0.5f;
            }

            if (t <= SlamEndTime)
            {
                var slamTime = (t - HoldEndTime) / (SlamEndTime - HoldEndTime);
                return Mathf.LerpUnclamped(0.5f, 1f, EaseInCubic(slamTime));
            }

            return 1f;
        }

        private static Quaternion SampleFlipRotation(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float tumbleFraction,
            Vector3 flipAxis)
        {
            var clampedTumble = Mathf.Clamp01(tumbleFraction);
            var tumbleRotation = Quaternion.AngleAxis(180f * clampedTumble, flipAxis);
            var endCorrection =
                endPose.Rotation *
                Quaternion.Inverse(Quaternion.AngleAxis(180f, flipAxis) * startPose.Rotation);
            var correctionRotation = Quaternion.Slerp(Quaternion.identity, endCorrection, clampedTumble);
            return correctionRotation * tumbleRotation * startPose.Rotation;
        }

        private static Vector3 ResolveFlipSurfaceNormal(GameplayEntityPose startPose, GameplayEntityPose endPose)
        {
            var startNormal = startPose.Rotation * Vector3.forward;
            var endNormal = endPose.Rotation * Vector3.forward;
            var averagedNormal = startNormal + endNormal;

            if (averagedNormal.sqrMagnitude > 0.000001f)
            {
                return averagedNormal.normalized;
            }

            if (startNormal.sqrMagnitude > 0.000001f)
            {
                return startNormal.normalized;
            }

            return endNormal.sqrMagnitude > 0.000001f
                ? endNormal.normalized
                : Vector3.zero;
        }

        private static bool TryResolveFlipBasis(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            out Vector3 liftAxis,
            out Vector3 flipAxis)
        {
            liftAxis = default;
            flipAxis = default;

            var travelDelta = endPose.Position - startPose.Position;
            if (travelDelta.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var surfaceNormal = ResolveFlipSurfaceNormal(startPose, endPose);
            if (surfaceNormal.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            liftAxis = -surfaceNormal.normalized;
            flipAxis = Vector3.Cross(travelDelta.normalized, liftAxis);
            if (flipAxis.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            flipAxis.Normalize();
            return true;
        }

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - (inverse * inverse * inverse);
        }

        private static float EaseInCubic(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return clamped * clamped * clamped;
        }

        private static float EaseOutSine(float t)
        {
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
        }

        private static float EaseInOutQuad(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return clamped < 0.5f
                ? 2f * clamped * clamped
                : 1f - (Mathf.Pow(-2f * clamped + 2f, 2f) * 0.5f);
        }
    }

    internal static class BoxMotionVisualScaleSampler
    {
        private static readonly Vector3 FlipPeakScale = new(1.1f, 1.1f, 1.08f);
        private static readonly Vector3 FlipPreImpactScale = new(0.96f, 0.96f, 0.96f);
        private static readonly Vector3 FlipImpactSquashScale = new(1.18f, 1.18f, 0.74f);
        private static readonly Vector3 FlipReboundScale = new(0.95f, 0.95f, 1.10f);

        private static readonly Vector3 SlideXStretchScale = new(1.06f, 0.985f, 0.97f);
        private static readonly Vector3 SlideXBrakeScale = new(0.96f, 1.035f, 0.94f);
        private static readonly Vector3 SlideYStretchScale = new(0.985f, 1.06f, 0.97f);
        private static readonly Vector3 SlideYBrakeScale = new(1.035f, 0.96f, 0.94f);

        public static Vector3 SampleFlipMotion(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            if (t <= BoxFlipSlamSampler.SlamEndTime)
            {
                return SampleFlipFlight(t / BoxFlipSlamSampler.SlamEndTime);
            }

            return SampleFlipSettle((t - BoxFlipSlamSampler.SlamEndTime) / (1f - BoxFlipSlamSampler.SlamEndTime));
        }

        public static Vector3 SampleFlipFlight(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            if (t <= 0.46f)
            {
                return Vector3.LerpUnclamped(Vector3.one, FlipPeakScale, EaseOutQuad(t / 0.46f));
            }

            if (t <= 0.82f)
            {
                return Vector3.LerpUnclamped(FlipPeakScale, FlipPreImpactScale, EaseInQuad((t - 0.46f) / 0.36f));
            }

            return Vector3.LerpUnclamped(FlipPreImpactScale, FlipImpactSquashScale, EaseInQuad((t - 0.82f) / 0.18f));
        }

        public static Vector3 SampleFlipSettle(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            if (t <= 0.45f)
            {
                return Vector3.LerpUnclamped(FlipImpactSquashScale, FlipReboundScale, EaseOutQuad(t / 0.45f));
            }

            return Vector3.LerpUnclamped(FlipReboundScale, Vector3.one, EaseOutQuad((t - 0.45f) / 0.55f));
        }

        public static Vector3 SampleSlideMotion(float normalizedTime, Vector3 localTravelDirection)
        {
            var stretchScale = Mathf.Abs(localTravelDirection.y) > Mathf.Abs(localTravelDirection.x)
                ? SlideYStretchScale
                : SlideXStretchScale;
            var brakeScale = Mathf.Abs(localTravelDirection.y) > Mathf.Abs(localTravelDirection.x)
                ? SlideYBrakeScale
                : SlideXBrakeScale;
            var t = Mathf.Clamp01(normalizedTime);

            if (t <= 0.18f)
            {
                return Vector3.LerpUnclamped(Vector3.one, stretchScale, EaseOutQuad(t / 0.18f));
            }

            if (t <= 0.72f)
            {
                return stretchScale;
            }

            if (t <= 0.88f)
            {
                return Vector3.LerpUnclamped(stretchScale, brakeScale, EaseInOutQuad((t - 0.72f) / 0.16f));
            }

            return Vector3.LerpUnclamped(brakeScale, Vector3.one, EaseOutQuad((t - 0.88f) / 0.12f));
        }

        private static float EaseInQuad(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return clamped * clamped;
        }

        private static float EaseOutQuad(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - (inverse * inverse);
        }

        private static float EaseInOutQuad(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return clamped < 0.5f
                ? 2f * clamped * clamped
                : 1f - (Mathf.Pow(-2f * clamped + 2f, 2f) * 0.5f);
        }
    }
}
