using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayCameraRig : MonoBehaviour
    {
        [SerializeField] private float pitchDegrees = 26f;
        [SerializeField] private float yawDegrees = 32f;
        [SerializeField] private float framingPadding = 1.2f;
        [SerializeField] private float perspectiveFieldOfView = 50f;
        [SerializeField] private float nearClipPlane = 0.03f;
        [SerializeField] private float farClipPlane = 100f;

        private Camera _viewCamera;
        private Bounds _visibleCubeBounds;
        private bool _isInitialized;
        private Transform _target;

        public void Initialize(Camera viewCamera, Transform target, Bounds visibleCubeBounds)
        {
            _viewCamera = viewCamera != null
                ? viewCamera
                : throw new ArgumentNullException(nameof(viewCamera));
            _target = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            _visibleCubeBounds = visibleCubeBounds;
            _isInitialized = true;
            SnapToTarget();
        }

        public void RefreshVisibleCubeBounds(Bounds visibleCubeBounds)
        {
            _visibleCubeBounds = visibleCubeBounds;
            if (_isInitialized)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (!_isInitialized)
            {
                return;
            }

            ApplyCameraPose();
        }

        private void LateUpdate()
        {
            if (_isInitialized)
            {
                ApplyCameraPose();
            }
        }

        private void ApplyCameraPose()
        {
            if (_viewCamera == null || _target == null)
            {
                return;
            }

            _viewCamera.orthographic = false;
            _viewCamera.fieldOfView = perspectiveFieldOfView;
            _viewCamera.nearClipPlane = nearClipPlane;
            _viewCamera.farClipPlane = farClipPlane;

            var lookRotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var targetPosition = _target.position;
            var distance = ResolveCameraDistance(_viewCamera, _visibleCubeBounds.extents.magnitude);
            var forward = lookRotation * Vector3.forward;

            _viewCamera.transform.SetPositionAndRotation(
                targetPosition - (forward * distance),
                lookRotation);
        }

        private float ResolveCameraDistance(Camera viewCamera, float boundingRadius)
        {
            var verticalHalfFovRadians = Mathf.Deg2Rad * Mathf.Clamp(viewCamera.fieldOfView, 1f, 179f) * 0.5f;
            var horizontalHalfFovRadians = Mathf.Atan(Mathf.Tan(verticalHalfFovRadians) * Mathf.Max(viewCamera.aspect, 0.01f));
            var limitingHalfFov = Mathf.Min(verticalHalfFovRadians, horizontalHalfFovRadians);
            var safeRadius = Mathf.Max(0.01f, boundingRadius * Mathf.Max(1f, framingPadding));
            return safeRadius / Mathf.Tan(Mathf.Max(0.01f, limitingHalfFov));
        }
    }
}
