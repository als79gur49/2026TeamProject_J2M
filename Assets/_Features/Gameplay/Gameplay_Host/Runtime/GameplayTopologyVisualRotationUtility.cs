using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayTopologyVisualRotationUtility
    {
        public static float ResolveRestReferenceAngleXDegrees(
            CubeTopologyState topology,
            TopologyRotationVisualMapping topologyRotationVisualMapping)
        {
            var forwardDegrees = topologyRotationVisualMapping == TopologyRotationVisualMapping.ForwardUsesPositiveX
                ? 90f
                : -90f;

            return topology.BottomFace switch
            {
                FaceId.Floor => 0f,
                FaceId.Front => forwardDegrees,
                FaceId.Ceiling => 180f,
                FaceId.Back => -forwardDegrees,
                _ => 0f,
            };
        }

        public static Quaternion ResolveRestReferenceRotation(
            CubeTopologyState topology,
            TopologyRotationVisualMapping topologyRotationVisualMapping)
        {
            return Quaternion.Euler(
                ResolveRestReferenceAngleXDegrees(topology, topologyRotationVisualMapping),
                0f,
                0f);
        }

        public static Quaternion ResolveCameraOrbitRotation(
            CubeTopologyState topology,
            TopologyRotationVisualMapping topologyRotationVisualMapping)
        {
            return Quaternion.Inverse(ResolveRestReferenceRotation(topology, topologyRotationVisualMapping));
        }
    }
}
