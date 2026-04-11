using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class PlayerFlipInteractionDriver : MonoBehaviour
    {
        [SerializeField] private Transform handIkTarget;
        [SerializeField] private Transform handRestAnchor;

        public Pose GetHandRestWorldPose()
        {
            if (handRestAnchor != null)
            {
                return new Pose(handRestAnchor.position, handRestAnchor.rotation);
            }

            if (handIkTarget != null)
            {
                return new Pose(handIkTarget.position, handIkTarget.rotation);
            }

            return new Pose(transform.position, transform.rotation);
        }

        public void ApplyInteraction(in Pose handTargetWorldPose, float weight)
        {
            if (handIkTarget == null)
            {
                return;
            }

            var restPose = GetHandRestWorldPose();
            var clampedWeight = Mathf.Clamp01(weight);
            handIkTarget.SetPositionAndRotation(
                Vector3.LerpUnclamped(restPose.position, handTargetWorldPose.position, clampedWeight),
                Quaternion.SlerpUnclamped(restPose.rotation, handTargetWorldPose.rotation, clampedWeight));
        }

        public void ResetInteraction()
        {
            if (handIkTarget == null ||
                handRestAnchor == null)
            {
                return;
            }

            handIkTarget.SetPositionAndRotation(handRestAnchor.position, handRestAnchor.rotation);
        }

        private void OnDisable()
        {
            ResetInteraction();
        }

        private void Reset()
        {
            if (handIkTarget == null)
            {
                handIkTarget = FindChildRecursive(transform, "HandIkTarget") ??
                               FindChildRecursive(transform, "HandIKTarget");
            }

            if (handRestAnchor == null)
            {
                handRestAnchor = FindChildRecursive(transform, "HandRestAnchor") ?? handIkTarget;
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                var nestedMatch = FindChildRecursive(child, childName);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }
    }
}
