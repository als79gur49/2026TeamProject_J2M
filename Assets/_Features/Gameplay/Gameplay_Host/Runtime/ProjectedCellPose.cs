using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct ProjectedCellPose
    {
        public ProjectedCellPose(Vector3 localPosition, Quaternion localRotation, Vector3 normal)
        {
            if (normal.sqrMagnitude <= 0.000001f)
            {
                throw new ArgumentOutOfRangeException(nameof(normal), "Projected cell pose normal must be non-zero.");
            }

            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Normal = normal.normalized;
        }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public Vector3 Normal { get; }
    }
}
