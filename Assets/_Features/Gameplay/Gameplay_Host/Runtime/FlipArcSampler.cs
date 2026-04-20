using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal static class FlipArcSampler
    {
        public static GameplayEntityPose Sample(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime,
            float arcHeightWorld)
        {
            var clampedTime = Mathf.Clamp01(normalizedTime);
            if (!TryResolveFlipBasis(startPose, endPose, out var liftAxis, out var flipAxis) ||
                !TrySampleFlipArcPosition(startPose, endPose, clampedTime, arcHeightWorld, liftAxis, flipAxis, out var position))
            {
                return SampleFallback(startPose, endPose, clampedTime);
            }

            return new GameplayEntityPose(
                position,
                SampleFlipRotation(startPose, endPose, clampedTime, flipAxis));
        }

        private static GameplayEntityPose SampleFallback(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime)
        {
            var easedTime = EaseOutQuad(normalizedTime);
            return new GameplayEntityPose(
                Vector3.LerpUnclamped(startPose.Position, endPose.Position, easedTime),
                Quaternion.SlerpUnclamped(startPose.Rotation, endPose.Rotation, easedTime));
        }

        private static float EaseOutQuad(float normalizedTime)
        {
            var inverse = 1f - Mathf.Clamp01(normalizedTime);
            return 1f - (inverse * inverse);
        }

        private static Quaternion SampleFlipRotation(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime,
            Vector3 flipAxis)
        {
            var tumbleRotation = Quaternion.AngleAxis(180f * normalizedTime, flipAxis);
            var endCorrection =
                endPose.Rotation *
                Quaternion.Inverse(Quaternion.AngleAxis(180f, flipAxis) * startPose.Rotation);
            var correctionRotation = Quaternion.Slerp(Quaternion.identity, endCorrection, normalizedTime);
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

        private static bool TrySampleFlipArcPosition(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float normalizedTime,
            float arcHeightWorld,
            Vector3 liftAxis,
            Vector3 flipAxis,
            out Vector3 position)
        {
            position = default;

            var chord = endPose.Position - startPose.Position;
            var chordLength = chord.magnitude;
            if (chordLength <= 0.000001f)
            {
                return false;
            }

            var halfChord = chordLength * 0.5f;
            var sagitta = Mathf.Clamp(arcHeightWorld, 0.0001f, halfChord * 0.95f);
            var radius = ((halfChord * halfChord) + (sagitta * sagitta)) / (2f * sagitta);
            var midpoint = (startPose.Position + endPose.Position) * 0.5f;

            var center = midpoint - (liftAxis * (radius - sagitta));
            var startRadius = startPose.Position - center;
            var endRadius = endPose.Position - center;
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
