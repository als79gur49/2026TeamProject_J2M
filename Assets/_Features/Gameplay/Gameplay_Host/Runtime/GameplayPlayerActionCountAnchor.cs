using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayPlayerActionCountAnchor : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(0f)] private float surfaceInsetDistance = 1.5f;
        [SerializeField, Min(0f)] private float cameraRightOffsetDistance = 0.25f;

        public void Initialize(
            Transform targetTransform,
            float insetDistance,
            float rightOffsetDistance,
            Camera outputCamera = null)
        {
            target = targetTransform != null
                ? targetTransform
                : throw new ArgumentNullException(nameof(targetTransform));
            targetCamera = outputCamera;
            surfaceInsetDistance = Mathf.Max(0f, insetDistance);
            cameraRightOffsetDistance = Mathf.Max(0f, rightOffsetDistance);
            RefreshPose();
        }

        public void RefreshPose()
        {
            if (target == null)
            {
                return;
            }

            var viewCamera = ResolveCamera();
            transform.position = target.position -
                                 (target.forward * surfaceInsetDistance) +
                                 (viewCamera != null
                                     ? viewCamera.transform.right * cameraRightOffsetDistance
                                     : Vector3.zero);
            if (viewCamera == null)
            {
                return;
            }

            var toCamera = transform.position - viewCamera.transform.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(toCamera.normalized, viewCamera.transform.up);
        }

        private void LateUpdate()
        {
            RefreshPose();
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }
    }
}
