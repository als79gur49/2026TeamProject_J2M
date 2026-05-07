using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    /// <summary>
    /// Board-local presentation pose for a visual attached to a SurfaceCell.
    /// Prefab roots are expected to use cell center as their pivot, with visual-only
    /// colliders treated as presentation/selection helpers rather than gameplay authority.
    /// </summary>
    public readonly struct SurfaceCellPresentationPose
    {
        public SurfaceCellPresentationPose(
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public Vector3 LocalScale { get; }
    }

    public interface ISurfaceCellPresentationPoseResolver
    {
        bool TryResolvePose(SurfaceCell cell, out SurfaceCellPresentationPose pose);
    }
}
