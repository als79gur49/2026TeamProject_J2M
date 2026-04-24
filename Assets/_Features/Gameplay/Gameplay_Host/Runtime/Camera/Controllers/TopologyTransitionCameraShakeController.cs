using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class TopologyTransitionCameraShakeController
    {
        private const float ZeroEpsilon = 0.000001f;

        public TopologyTransitionCameraShakeResult Evaluate(
            in TopologyTransitionVisualState visualState,
            TopologyTransitionCameraShakeProfile profile)
        {
            if (!visualState.IsActive)
            {
                return TopologyTransitionCameraShakeResult.Zero;
            }

            var resolvedProfile = profile ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            var directionSign = visualState.RotationKind == CubeRotationKind.Backward ? -1f : 1f;
            var localPosition =
                EvaluatePulse(
                    visualState.Progress01,
                    resolvedProfile.ImpactStart01,
                    resolvedProfile.ImpactDuration01,
                    ResolveSignedPositionAmplitude(resolvedProfile.ImpactLocalPositionAmplitude, directionSign),
                    Mathf.Max(1, resolvedProfile.ImpactOscillationCycles),
                    phaseOffsetRadians: -Mathf.PI * 0.5f) +
                EvaluatePulse(
                    visualState.Progress01,
                    resolvedProfile.LandingStart01,
                    resolvedProfile.LandingDuration01,
                    ResolveSignedPositionAmplitude(resolvedProfile.LandingLocalPositionAmplitude, -directionSign),
                    Mathf.Max(1, resolvedProfile.LandingOscillationCycles),
                    phaseOffsetRadians: Mathf.PI * 0.25f);
            var localRotationEuler =
                EvaluatePulse(
                    visualState.Progress01,
                    resolvedProfile.ImpactStart01,
                    resolvedProfile.ImpactDuration01,
                    ResolveSignedRotationAmplitude(resolvedProfile.ImpactLocalRotationAmplitudeDegrees, directionSign),
                    Mathf.Max(1, resolvedProfile.ImpactOscillationCycles),
                    phaseOffsetRadians: -Mathf.PI * 0.5f) +
                EvaluatePulse(
                    visualState.Progress01,
                    resolvedProfile.LandingStart01,
                    resolvedProfile.LandingDuration01,
                    ResolveSignedRotationAmplitude(resolvedProfile.LandingLocalRotationAmplitudeDegrees, -directionSign),
                    Mathf.Max(1, resolvedProfile.LandingOscillationCycles),
                    phaseOffsetRadians: Mathf.PI * 0.25f);
            var sanitizedLocalPosition = Sanitize(localPosition);
            var sanitizedLocalRotationEuler = Sanitize(localRotationEuler);
            var localRotation = localRotationEuler.sqrMagnitude <= ZeroEpsilon
                ? Quaternion.identity
                : Quaternion.Euler(sanitizedLocalRotationEuler);
            return new TopologyTransitionCameraShakeResult(sanitizedLocalPosition, localRotation);
        }

        private static Vector3 EvaluatePulse(
            float progress01,
            float pulseStart01,
            float pulseDuration01,
            Vector3 amplitude,
            int oscillationCycles,
            float phaseOffsetRadians)
        {
            var clampedDuration01 = Mathf.Max(0.0001f, pulseDuration01);
            if (progress01 < pulseStart01 ||
                progress01 > pulseStart01 + clampedDuration01)
            {
                return Vector3.zero;
            }

            var pulseProgress01 = Mathf.InverseLerp(pulseStart01, pulseStart01 + clampedDuration01, progress01);
            var window = Mathf.Sin(Mathf.PI * pulseProgress01);
            var oscillation = Mathf.Sin((Mathf.PI * 2f * oscillationCycles * pulseProgress01) + phaseOffsetRadians);
            return amplitude * (window * window * oscillation);
        }

        private static Vector3 ResolveSignedPositionAmplitude(Vector3 amplitude, float directionSign)
        {
            return new Vector3(amplitude.x * directionSign, amplitude.y, -amplitude.z);
        }

        private static Vector3 ResolveSignedRotationAmplitude(Vector3 amplitude, float directionSign)
        {
            return new Vector3(amplitude.x * directionSign, amplitude.y * directionSign, -amplitude.z);
        }

        private static Vector3 Sanitize(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x) <= ZeroEpsilon ? 0f : value.x,
                Mathf.Abs(value.y) <= ZeroEpsilon ? 0f : value.y,
                Mathf.Abs(value.z) <= ZeroEpsilon ? 0f : value.z);
        }
    }
}
