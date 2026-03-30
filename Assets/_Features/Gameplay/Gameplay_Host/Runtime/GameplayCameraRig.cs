using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayCameraRig : MonoBehaviour
    {
        private static readonly GameplayCameraSettings DefaultSettings = GameplayCameraSettings.CreateRuntimeDefault();

        public enum DistanceMode
        {
            AutoFit = 0,
            Manual = 1,
        }

        [SerializeField] private float pitchDegrees = DefaultSettings.PitchDegrees;
        [SerializeField] private float yawDegrees = DefaultSettings.YawDegrees;
        [SerializeField] private DistanceMode distanceMode = DefaultSettings.DistanceMode;
        [SerializeField] private float manualDistance = DefaultSettings.ManualDistance;
        [SerializeField] private float framingPadding = DefaultSettings.FramingPadding;
        [SerializeField] private float perspectiveFieldOfView = DefaultSettings.PerspectiveFieldOfView;
        [SerializeField] private float nearClipPlane = DefaultSettings.NearClipPlane;
        [SerializeField] private float farClipPlane = DefaultSettings.FarClipPlane;
        [SerializeField] private CameraClearFlags clearFlags = DefaultSettings.ClearFlags;
        [SerializeField] private Color backgroundColor = DefaultSettings.BackgroundColor;

        private Camera _viewCamera;
        private Bounds _visibleCubeBounds;
        private bool _isInitialized;
        private Transform _target;

        public DistanceMode CurrentDistanceMode => distanceMode;

        public float ManualDistance => manualDistance;

        public float PitchDegrees => pitchDegrees;

        public float YawDegrees => yawDegrees;

        public float PerspectiveFieldOfView => perspectiveFieldOfView;

        public float FramingPadding => framingPadding;

        public float NearClipPlane => nearClipPlane;

        public float FarClipPlane => farClipPlane;

        public CameraClearFlags ClearFlags => clearFlags;

        public Color BackgroundColor => backgroundColor;

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

        public void ApplySettings(GameplayCameraSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.PerspectiveFieldOfView <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Field of view must be greater than zero.");
            }

            pitchDegrees = settings.PitchDegrees;
            yawDegrees = settings.YawDegrees;
            perspectiveFieldOfView = settings.PerspectiveFieldOfView;
            framingPadding = settings.FramingPadding;
            distanceMode = settings.DistanceMode;
            manualDistance = Mathf.Max(0.01f, settings.ManualDistance);
            nearClipPlane = Mathf.Max(0.001f, settings.NearClipPlane);
            farClipPlane = Mathf.Max(nearClipPlane + 0.01f, settings.FarClipPlane);
            clearFlags = settings.ClearFlags;
            backgroundColor = settings.BackgroundColor;

            if (_isInitialized)
            {
                SnapToTarget();
            }
        }

        public static void ApplySettingsToCamera(
            Camera viewCamera,
            GameplayCameraSettings settings,
            Vector3 targetPosition,
            Bounds visibleCubeBounds)
        {
            if (viewCamera == null)
            {
                throw new ArgumentNullException(nameof(viewCamera));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.PerspectiveFieldOfView <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Field of view must be greater than zero.");
            }

            ApplyResolvedCameraPose(
                viewCamera,
                targetPosition,
                visibleCubeBounds,
                settings.PitchDegrees,
                settings.YawDegrees,
                settings.DistanceMode,
                settings.ManualDistance,
                settings.FramingPadding,
                settings.PerspectiveFieldOfView,
                settings.NearClipPlane,
                settings.FarClipPlane,
                settings.ClearFlags,
                settings.BackgroundColor);
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

            ApplyResolvedCameraPose(
                _viewCamera,
                _target.position,
                _visibleCubeBounds,
                pitchDegrees,
                yawDegrees,
                distanceMode,
                manualDistance,
                framingPadding,
                perspectiveFieldOfView,
                nearClipPlane,
                farClipPlane,
                clearFlags,
                backgroundColor);
        }

        private static void ApplyResolvedCameraPose(
            Camera viewCamera,
            Vector3 targetPosition,
            Bounds visibleCubeBounds,
            float pitchDegrees,
            float yawDegrees,
            DistanceMode distanceMode,
            float manualDistance,
            float framingPadding,
            float perspectiveFieldOfView,
            float nearClipPlane,
            float farClipPlane,
            CameraClearFlags clearFlags,
            Color backgroundColor)
        {
            var sanitizedNearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            var sanitizedFarClipPlane = Mathf.Max(sanitizedNearClipPlane + 0.01f, farClipPlane);

            viewCamera.orthographic = false;
            viewCamera.fieldOfView = perspectiveFieldOfView;
            viewCamera.nearClipPlane = sanitizedNearClipPlane;
            viewCamera.farClipPlane = sanitizedFarClipPlane;
            viewCamera.clearFlags = clearFlags;
            viewCamera.backgroundColor = backgroundColor;

            var lookRotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var distance = ResolveCameraDistance(
                viewCamera,
                visibleCubeBounds,
                distanceMode,
                manualDistance,
                framingPadding);
            var forward = lookRotation * Vector3.forward;

            viewCamera.transform.SetPositionAndRotation(
                targetPosition - (forward * distance),
                lookRotation);
        }

        private static float ResolveCameraDistance(
            Camera viewCamera,
            Bounds visibleCubeBounds,
            DistanceMode distanceMode,
            float manualDistance,
            float framingPadding)
        {
            return distanceMode == DistanceMode.Manual
                ? Mathf.Max(0.01f, manualDistance)
                : ResolveAutoFitDistance(viewCamera, visibleCubeBounds.extents.magnitude, framingPadding);
        }

        private static float ResolveAutoFitDistance(Camera viewCamera, float boundingRadius, float framingPadding)
        {
            var verticalHalfFovRadians = Mathf.Deg2Rad * Mathf.Clamp(viewCamera.fieldOfView, 1f, 179f) * 0.5f;
            var horizontalHalfFovRadians = Mathf.Atan(Mathf.Tan(verticalHalfFovRadians) * Mathf.Max(viewCamera.aspect, 0.01f));
            var limitingHalfFov = Mathf.Min(verticalHalfFovRadians, horizontalHalfFovRadians);
            var safeRadius = Mathf.Max(0.01f, boundingRadius * Mathf.Max(1f, framingPadding));
            return safeRadius / Mathf.Tan(Mathf.Max(0.01f, limitingHalfFov));
        }
    }
}
