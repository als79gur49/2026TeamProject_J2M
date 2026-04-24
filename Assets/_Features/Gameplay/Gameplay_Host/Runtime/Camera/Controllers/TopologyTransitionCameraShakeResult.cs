using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct TopologyTransitionCameraShakeResult
    {
        public TopologyTransitionCameraShakeResult(Vector3 localPosition, Quaternion localRotation)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
        }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public static TopologyTransitionCameraShakeResult Zero => new(Vector3.zero, Quaternion.identity);
    }
}
