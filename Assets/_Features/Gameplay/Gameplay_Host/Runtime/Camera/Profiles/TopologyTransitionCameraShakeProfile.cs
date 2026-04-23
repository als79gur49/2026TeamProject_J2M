using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class TopologyTransitionCameraShakeProfile
    {
        [Min(0f)] public float ImpactStart01 = 0.02f;
        [Min(0.0001f)] public float ImpactDuration01 = 0.18f;
        [Min(1)] public int ImpactOscillationCycles = 2;
        public Vector3 ImpactLocalPositionAmplitude = new(0.012f, 0.008f, 0.018f);
        public Vector3 ImpactLocalRotationAmplitudeDegrees = new(0.45f, 0.28f, 0.12f);

        [Min(0f)] public float LandingStart01 = 0.76f;
        [Min(0.0001f)] public float LandingDuration01 = 0.18f;
        [Min(1)] public int LandingOscillationCycles = 1;
        public Vector3 LandingLocalPositionAmplitude = new(0.006f, 0.004f, 0.01f);
        public Vector3 LandingLocalRotationAmplitudeDegrees = new(0.24f, 0.14f, 0.08f);

        public static TopologyTransitionCameraShakeProfile CreateDefault()
        {
            return new TopologyTransitionCameraShakeProfile();
        }

        public TopologyTransitionCameraShakeProfile Clone()
        {
            return new TopologyTransitionCameraShakeProfile
            {
                ImpactStart01 = ImpactStart01,
                ImpactDuration01 = ImpactDuration01,
                ImpactOscillationCycles = ImpactOscillationCycles,
                ImpactLocalPositionAmplitude = ImpactLocalPositionAmplitude,
                ImpactLocalRotationAmplitudeDegrees = ImpactLocalRotationAmplitudeDegrees,
                LandingStart01 = LandingStart01,
                LandingDuration01 = LandingDuration01,
                LandingOscillationCycles = LandingOscillationCycles,
                LandingLocalPositionAmplitude = LandingLocalPositionAmplitude,
                LandingLocalRotationAmplitudeDegrees = LandingLocalRotationAmplitudeDegrees,
            };
        }
    }
}
