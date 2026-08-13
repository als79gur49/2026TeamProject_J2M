using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class CameraShakeImpulseEvaluator
    {
        private const double TwoPi = System.Math.PI * 2.0;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public CameraShakeContribution Evaluate(
            in CameraShakeImpulseRequest request,
            CameraShakeProfileEntry profileEntry,
            double elapsedSeconds)
        {
            if (profileEntry == null ||
                elapsedSeconds < 0.0 ||
                elapsedSeconds >= profileEntry.DurationSeconds)
            {
                return CameraShakeContribution.Inactive;
            }

            var normalized = Mathf.Clamp01((float)(elapsedSeconds / profileEntry.DurationSeconds));
            var attackEnvelope = EvaluateAttackEnvelope(elapsedSeconds, profileEntry.AttackSeconds);
            var decayEnvelope = Mathf.Clamp01(profileEntry.Decay.Evaluate(normalized));
            var envelope = attackEnvelope * decayEnvelope;
            var seed = CreateStableSeed(request.Identity);
            var angularProgress = TwoPi * profileEntry.OscillationCycles * normalized;
            var positionOscillation = new Vector3(
                EvaluateOscillation(angularProgress, seed, 0u),
                EvaluateOscillation(angularProgress, seed, 1u),
                EvaluateOscillation(angularProgress, seed, 2u));
            var rotationOscillation = new Vector3(
                EvaluateOscillation(angularProgress, seed, 3u),
                EvaluateOscillation(angularProgress, seed, 4u),
                EvaluateOscillation(angularProgress, seed, 5u));
            var localPosition = Vector3.Scale(profileEntry.LocalPositionAmplitude, positionOscillation) * envelope;
            var localRotationDegrees =
                Vector3.Scale(profileEntry.LocalRotationAmplitudeDegrees, rotationOscillation) * envelope;

            if (!IsFinite(localPosition) || !IsFinite(localRotationDegrees))
            {
                return CameraShakeContribution.Inactive;
            }

            return CameraShakeContribution.Gameplay(
                request.Priority,
                localPosition,
                localRotationDegrees,
                true);
        }

        internal static uint CreateStableSeed(CameraShakeRequestIdentity identity)
        {
            var hash = FnvOffsetBasis;
            hash = Mix(hash, identity.TickIndex);
            hash = Mix(hash, (int)identity.Semantic);
            hash = Mix(hash, identity.SourceEntityId);
            hash = Mix(hash, identity.SequenceOrActionPlanId);
            return Avalanche(hash);
        }

        private static float EvaluateAttackEnvelope(double elapsedSeconds, float attackSeconds)
        {
            if (attackSeconds <= 0f)
            {
                return 1f;
            }

            var attackProgress = Mathf.Clamp01((float)(elapsedSeconds / attackSeconds));
            return Mathf.Sin(Mathf.PI * 0.5f * attackProgress);
        }

        private static float EvaluateOscillation(double angularProgress, uint seed, uint stream)
        {
            return (float)System.Math.Sin(angularProgress + StablePhase(seed, stream));
        }

        private static double StablePhase(uint seed, uint stream)
        {
            var mixed = Avalanche(seed ^ (0x9e3779b9u * (stream + 1u)));
            var normalized = (mixed & 0x00ffffffu) / 16777216.0;
            return normalized * TwoPi;
        }

        private static uint Mix(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= FnvPrime;
                return hash;
            }
        }

        private static uint Avalanche(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
