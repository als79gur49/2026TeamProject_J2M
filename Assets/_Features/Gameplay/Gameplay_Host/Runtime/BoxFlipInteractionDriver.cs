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
            ResetInteractionForDiagnostics();
        }

        internal BoxFlipInteractionResetResult ResetInteractionForDiagnostics()
        {
            if (visualRoot == null)
            {
                return new BoxFlipInteractionResetResult(
                    requestCount: 1,
                    succeededCount: 0,
                    visualRootPositionResetCount: 0,
                    visualRootRotationResetCount: 0);
            }

            CacheBasePose();
            visualRoot.localPosition = _baseLocalPosition;
            visualRoot.localRotation = _baseLocalRotation;
            return new BoxFlipInteractionResetResult(
                requestCount: 1,
                succeededCount: 1,
                visualRootPositionResetCount: IsPositionReset() ? 1 : 0,
                visualRootRotationResetCount: IsRotationReset() ? 1 : 0);
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

        private bool IsPositionReset()
        {
            return visualRoot != null &&
                   (visualRoot.localPosition - _baseLocalPosition).sqrMagnitude <= 0.00000001f;
        }

        private bool IsRotationReset()
        {
            return visualRoot != null &&
                   Quaternion.Angle(visualRoot.localRotation, _baseLocalRotation) <= 0.001f;
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
