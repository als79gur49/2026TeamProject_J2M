using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class TopologyTransitionCameraShakeController
    {
        private const float ZeroEpsilon = 0.000001f;

        private TopologyTransitionCameraShakeProfile _profile = TopologyTransitionCameraShakeProfile.CreateDefault();

        public Vector3 LocalPosition { get; private set; }

        public Quaternion LocalRotation { get; private set; } = Quaternion.identity;

        public void Initialize(TopologyTransitionCameraShakeProfile profile)
        {
            _profile = profile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            Reset();
        }

        public void Apply(in TopologyTransitionVisualState visualState)
        {
            if (!visualState.IsActive)
            {
                Reset();
                return;
            }

            var directionSign = visualState.RotationKind == CubeRotationKind.Backward ? -1f : 1f;
            var localPosition =
                EvaluatePulse(
                    visualState.Progress01,
                    _profile.ImpactStart01,
                    _profile.ImpactDuration01,
                    ResolveSignedPositionAmplitude(_profile.ImpactLocalPositionAmplitude, directionSign),
                    Mathf.Max(1, _profile.ImpactOscillationCycles),
                    phaseOffsetRadians: -Mathf.PI * 0.5f) +
                EvaluatePulse(
                    visualState.Progress01,
                    _profile.LandingStart01,
                    _profile.LandingDuration01,
                    ResolveSignedPositionAmplitude(_profile.LandingLocalPositionAmplitude, -directionSign),
                    Mathf.Max(1, _profile.LandingOscillationCycles),
                    phaseOffsetRadians: Mathf.PI * 0.25f);
            var localRotationEuler =
                EvaluatePulse(
                    visualState.Progress01,
                    _profile.ImpactStart01,
                    _profile.ImpactDuration01,
                    ResolveSignedRotationAmplitude(_profile.ImpactLocalRotationAmplitudeDegrees, directionSign),
                    Mathf.Max(1, _profile.ImpactOscillationCycles),
                    phaseOffsetRadians: -Mathf.PI * 0.5f) +
                EvaluatePulse(
                    visualState.Progress01,
                    _profile.LandingStart01,
                    _profile.LandingDuration01,
                    ResolveSignedRotationAmplitude(_profile.LandingLocalRotationAmplitudeDegrees, -directionSign),
                    Mathf.Max(1, _profile.LandingOscillationCycles),
                    phaseOffsetRadians: Mathf.PI * 0.25f);

            LocalPosition = Sanitize(localPosition);
            LocalRotation = localRotationEuler.sqrMagnitude <= ZeroEpsilon
                ? Quaternion.identity
                : Quaternion.Euler(Sanitize(localRotationEuler));
        }

        public void Reset()
        {
            LocalPosition = Vector3.zero;
            LocalRotation = Quaternion.identity;
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
