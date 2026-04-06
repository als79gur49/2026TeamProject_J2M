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
        }

        public void Clear()
        {
            _clips.Clear();
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
            if (_clips.Count == 0)
            {
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
                if (!clip.IsComplete)
                {
                    return pose;
                }

                _clips.RemoveAt(0);
                if (_clips.Count == 0)
                {
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
        private readonly float _flipArcHeightWorld;
        private readonly bool _interpolateRotation;
        private readonly TickEntityMotionKind _motionKind;

        private MotionClip(
            TickEntityMotionKind motionKind,
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds,
            bool interpolateRotation,
            float flipArcHeightWorld)
        {
            _motionKind = motionKind;
            _interpolateRotation = interpolateRotation;
            _flipArcHeightWorld = flipArcHeightWorld;
            StartPose = startPose;
            EndPose = endPose;
            DurationSeconds = durationSeconds;
            ElapsedSeconds = 0f;
        }

        public float DurationSeconds { get; }

        public float ElapsedSeconds { get; private set; }

        public GameplayEntityPose EndPose { get; private set; }

        public bool IsComplete => RemainingSeconds <= 0.0001f;

        public TickEntityMotionKind MotionKind => _motionKind;

        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

        public GameplayEntityPose StartPose { get; private set; }

        public static MotionClip Create(
            TickEntityMotionKind motionKind,
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds,
            bool interpolateRotation,
            float flipArcHeightWorld)
        {
            return new MotionClip(
                motionKind,
                startPose,
                endPose,
                Mathf.Max(durationSeconds, 0.0001f),
                interpolateRotation,
                flipArcHeightWorld);
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
            var easedT = Mathf.Clamp01(t);
            if (!TryResolveFlipBasis(out var liftAxis, out var flipAxis))
            {
                return SampleLinear(easedT);
            }

            if (!TrySampleFlipArcPosition(easedT, liftAxis, flipAxis, out var position))
            {
                return SampleLinear(easedT);
            }

            return new GameplayEntityPose(
                position,
                SampleFlipRotation(easedT, flipAxis));
        }

        private Quaternion SampleFlipRotation(float normalizedTime, Vector3 flipAxis)
        {
            var tumbleRotation = Quaternion.AngleAxis(180f * normalizedTime, flipAxis);
            var endCorrection = EndPose.Rotation * Quaternion.Inverse(Quaternion.AngleAxis(180f, flipAxis) * StartPose.Rotation);
            var correctionRotation = Quaternion.Slerp(Quaternion.identity, endCorrection, normalizedTime);
            return correctionRotation * tumbleRotation * StartPose.Rotation;
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

        private Vector3 ResolveFlipSurfaceNormal()
        {
            var startNormal = StartPose.Rotation * Vector3.forward;
            var endNormal = EndPose.Rotation * Vector3.forward;
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

        private bool TryResolveFlipBasis(out Vector3 liftAxis, out Vector3 flipAxis)
        {
            liftAxis = default;
            flipAxis = default;

            var travelDelta = EndPose.Position - StartPose.Position;
            if (travelDelta.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var surfaceNormal = ResolveFlipSurfaceNormal();
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

        private bool TrySampleFlipArcPosition(
            float normalizedTime,
            Vector3 liftAxis,
            Vector3 flipAxis,
            out Vector3 position)
        {
            position = default;

            var chord = EndPose.Position - StartPose.Position;
            var chordLength = chord.magnitude;
            if (chordLength <= 0.000001f)
            {
                return false;
            }

            var halfChord = chordLength * 0.5f;
            var sagitta = Mathf.Clamp(_flipArcHeightWorld, 0.0001f, halfChord * 0.95f);
            var radius = ((halfChord * halfChord) + (sagitta * sagitta)) / (2f * sagitta);
            var midpoint = (StartPose.Position + EndPose.Position) * 0.5f;

            var center = midpoint - (liftAxis * (radius - sagitta));
            var startRadius = StartPose.Position - center;
            var endRadius = EndPose.Position - center;
            if (startRadius.sqrMagnitude <= 0.000001f ||
                endRadius.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var totalAngle = Vector3.SignedAngle(startRadius, endRadius, flipAxis);
            if (Mathf.Abs(totalAngle) <= 0.0001f)
            {
                return false;
            }

            var rotatedRadius = Quaternion.AngleAxis(totalAngle * normalizedTime, flipAxis) * startRadius;
            position = center + rotatedRadius;
            return true;
        }
    }
}
