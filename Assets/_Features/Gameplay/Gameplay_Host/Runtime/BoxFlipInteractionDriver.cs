using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class BoxFlipInteractionDriver : MonoBehaviour
    {
        [SerializeField] private Transform gripPoint;
        [SerializeField] private Transform visualRoot;

        private bool _cachedBasePose;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;

        public Pose GetGripWorldPose()
        {
            var target = gripPoint != null
                ? gripPoint
                : visualRoot != null
                    ? visualRoot
                    : transform;
            return new Pose(target.position, target.rotation);
        }

        public void ApplyInteraction(
            Vector3 localPositionOffset,
            Quaternion localRotationOffset,
            float weight)
        {
            if (visualRoot == null)
            {
                return;
            }

            CacheBasePose();

            var clampedWeight = Mathf.Clamp01(weight);
            visualRoot.localPosition = _baseLocalPosition + (localPositionOffset * clampedWeight);
            visualRoot.localRotation = _baseLocalRotation *
                                       Quaternion.SlerpUnclamped(Quaternion.identity, localRotationOffset, clampedWeight);
        }

        public void ResetInteraction()
        {
            if (visualRoot == null)
            {
                return;
            }

            CacheBasePose();
            visualRoot.localPosition = _baseLocalPosition;
            visualRoot.localRotation = _baseLocalRotation;
        }

        private void OnDisable()
        {
            ResetInteraction();
        }

        private void Reset()
        {
            if (visualRoot == null &&
                TryGetComponent<GameplayEntityView>(out var view) &&
                view != null)
            {
                visualRoot = view.ModelRoot;
            }

            if (gripPoint == null)
            {
                gripPoint = FindChildRecursive(transform, "GripPoint");
            }
        }

        private void CacheBasePose()
        {
            if (_cachedBasePose || visualRoot == null)
            {
                return;
            }

            _baseLocalPosition = visualRoot.localPosition;
            _baseLocalRotation = visualRoot.localRotation;
            _cachedBasePose = true;
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
