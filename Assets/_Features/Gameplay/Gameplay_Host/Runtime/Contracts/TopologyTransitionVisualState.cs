using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct TopologyTransitionVisualState
    {
        public TopologyTransitionVisualState(
            bool isActive,
            float progress01,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            float durationSeconds,
            Quaternion presentedVisualRotation,
            float angularVelocityNormalized)
        {
            IsActive = isActive;
            Progress01 = Mathf.Clamp01(progress01);
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            PresentedVisualRotation = presentedVisualRotation;
            AngularVelocityNormalized = Mathf.Max(0f, angularVelocityNormalized);
        }

        public bool IsActive { get; }

        public float Progress01 { get; }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public float DurationSeconds { get; }

        public Quaternion PresentedVisualRotation { get; }

        public float AngularVelocityNormalized { get; }

        public static TopologyTransitionVisualState Inactive(
            CubeTopologyState topology,
            Quaternion presentedVisualRotation)
        {
            return new TopologyTransitionVisualState(
                isActive: false,
                progress01: 0f,
                sourceTopology: topology,
                destinationTopology: topology,
                rotationKind: CubeRotationKind.None,
                durationSeconds: 0f,
                presentedVisualRotation: presentedVisualRotation,
                angularVelocityNormalized: 0f);
        }
    }
}
