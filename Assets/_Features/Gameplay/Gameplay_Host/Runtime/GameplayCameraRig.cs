using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayCameraRig : MonoBehaviour
    {
        private readonly struct UnshakenCameraPose
        {
            internal UnshakenCameraPose(
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 worldPosition,
                Quaternion worldRotation)
            {
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                WorldPosition = worldPosition;
                WorldRotation = worldRotation;
            }

            internal Vector3 LocalPosition { get; }

            internal Quaternion LocalRotation { get; }

            internal Vector3 WorldPosition { get; }

            internal Quaternion WorldRotation { get; }
        }

        private static readonly GameplayCameraSettings DefaultSettings = GameplayCameraSettings.CreateRuntimeDefault();
        private const string CameraOrbitPivotObjectName = "CameraOrbitPivot";
        private const string CameraPoseRootObjectName = "CameraPoseRoot";
        private const string CameraEffectsRootObjectName = "CameraEffectsRoot";

        [SerializeField] private float pitchDegrees = DefaultSettings.PitchDegrees;
        [SerializeField] private float yawDegrees = DefaultSettings.YawDegrees;
        [SerializeField] private CameraDistanceMode distanceMode = DefaultSettings.DistanceMode;
        [SerializeField] private float manualDistance = DefaultSettings.ManualDistance;
        [SerializeField] private float framingPadding = DefaultSettings.FramingPadding;
        [SerializeField] private float perspectiveFieldOfView = DefaultSettings.PerspectiveFieldOfView;
        [SerializeField] private float nearClipPlane = DefaultSettings.NearClipPlane;
        [SerializeField] private float farClipPlane = DefaultSettings.FarClipPlane;
        [SerializeField] private CameraClearFlags clearFlags = DefaultSettings.ClearFlags;
        [SerializeField] private Color backgroundColor = DefaultSettings.BackgroundColor;

        private Transform _cameraEffectsRoot;
        private Transform _cameraPoseRoot;
        private bool _isInitialized;
        private Transform _orbitPivot;
        private Quaternion _presentedTopologyOrbit = Quaternion.identity;
        private float _presentedTopologyOrbitXDegrees;
        private readonly TopologyTransitionCameraShakeController _topologyTransitionCameraShakeController = new();
        private TopologyTransitionCameraShakeProfile _topologyTransitionCameraShakeProfile =
            TopologyTransitionCameraShakeProfile.CreateDefault();
        private Transform _target;
        private Vector3 _topologyTransitionShakeLocalPosition;
        private Quaternion _topologyTransitionShakeLocalRotation = Quaternion.identity;
        private Bounds _visibleCubeBounds;
        private Camera _viewCamera;
        private bool _hasAuthoredSceneCameraPose;
        private Vector3 _authoredSceneCameraWorldPosition;
        private Quaternion _authoredSceneCameraWorldRotation = Quaternion.identity;
        private float _authoredSceneCameraFieldOfView = DefaultSettings.PerspectiveFieldOfView;
        private float _authoredSceneCameraNearClipPlane = DefaultSettings.NearClipPlane;
        private float _authoredSceneCameraFarClipPlane = DefaultSettings.FarClipPlane;
        private bool _useAuthoredSceneCameraPoseAsBaseline;
        private Vector3 _authoredSceneCameraBaselineLocalPosition;
        private Quaternion _authoredSceneCameraBaselineLocalRotation = Quaternion.identity;

        public CameraDistanceMode CurrentDistanceMode => distanceMode;

        public float ManualDistance => manualDistance;

        public float PitchDegrees => pitchDegrees;

        public float YawDegrees => yawDegrees;

        public float PerspectiveFieldOfView => perspectiveFieldOfView;

        public float FramingPadding => framingPadding;

        public float NearClipPlane => nearClipPlane;

        public float FarClipPlane => farClipPlane;

        public CameraClearFlags ClearFlags => clearFlags;

        public Color BackgroundColor => backgroundColor;

        public Quaternion PresentedTopologyOrbit => _presentedTopologyOrbit;

        internal float PresentedTopologyOrbitXDegrees => _presentedTopologyOrbitXDegrees;

        internal Vector3 TopologyTransitionShakeLocalPosition => _topologyTransitionShakeLocalPosition;

        internal Quaternion TopologyTransitionShakeLocalRotation => _topologyTransitionShakeLocalRotation;

        public void CaptureAuthoredSceneCameraPose(
            Transform cameraTransform,
            float fieldOfView,
            float nearClipPlane,
            float farClipPlane)
        {
            if (cameraTransform == null || _hasAuthoredSceneCameraPose)
            {
                return;
            }

            _hasAuthoredSceneCameraPose = true;
            _authoredSceneCameraWorldPosition = cameraTransform.position;
            _authoredSceneCameraWorldRotation = cameraTransform.rotation;
            _authoredSceneCameraFieldOfView = Mathf.Max(1f, fieldOfView);
            _authoredSceneCameraNearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            _authoredSceneCameraFarClipPlane = Mathf.Max(_authoredSceneCameraNearClipPlane + 0.01f, farClipPlane);
        }

        internal GameplayCameraSettings ResolveConfiguredSettings(
            GameplayCameraSettings baseSettings,
            GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy,
            Vector3 targetWorldPosition,
            CubeTopologyState topology,
            TopologyRotationVisualMapping topologyRotationVisualMapping)
        {
            var resolvedSettings = CloneConfiguredSettings(baseSettings);
            ResetAuthoredSceneCameraBaselineUsage();
            if (!HasApplicableAuthoredSceneCameraSettings(baselineAuthoringPolicy))
            {
                return resolvedSettings;
            }

            if (baselineAuthoringPolicy.UseAuthoredSceneCameraPose)
            {
                PrepareAuthoredSceneCameraBaseline(
                    targetWorldPosition,
                    topology,
                    topologyRotationVisualMapping);
            }

            if (baselineAuthoringPolicy.UseAuthoredSceneCameraLens)
            {
                ApplyAuthoredSceneCameraLens(resolvedSettings);
            }

            return resolvedSettings;
        }

        public void Initialize(Camera viewCamera, Transform target, Bounds visibleCubeBounds)
        {
            _viewCamera = viewCamera;
            _target = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            _visibleCubeBounds = visibleCubeBounds;
            ResolvePoseHierarchy();
            _isInitialized = true;
            SnapToTarget();
        }

        internal void ConfigureTopologyTransitionCameraShake(TopologyTransitionCameraShakeProfile profile)
        {
            _topologyTransitionCameraShakeProfile = profile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            _topologyTransitionShakeLocalPosition = Vector3.zero;
            _topologyTransitionShakeLocalRotation = Quaternion.identity;

            if (_isInitialized)
            {
                SnapToTarget();
            }
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

        public void SetPresentedTopologyOrbit(Quaternion orbitRotation)
        {
            SetPresentedTopologyOrbit(
                orbitRotation,
                ResolveNearestEquivalentAngleXDegrees(_presentedTopologyOrbitXDegrees, orbitRotation));
        }

        internal void SetPresentedTopologyOrbit(Quaternion orbitRotation, float orbitRotationXDegrees)
        {
            if (Quaternion.Angle(_presentedTopologyOrbit, orbitRotation) <= 0.001f &&
                Mathf.Abs(_presentedTopologyOrbitXDegrees - orbitRotationXDegrees) <= 0.001f)
            {
                return;
            }

            _presentedTopologyOrbit = orbitRotation;
            _presentedTopologyOrbitXDegrees = orbitRotationXDegrees;
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

            var unshakenPose = ResolveOrbitDistanceCameraPose(
                targetPosition,
                Quaternion.identity,
                Mathf.Max(viewCamera.aspect, 0.01f),
                visibleCubeBounds,
                settings.PitchDegrees,
                settings.YawDegrees,
                settings.DistanceMode,
                settings.ManualDistance,
                settings.FramingPadding,
                settings.PerspectiveFieldOfView);
            ApplyResolvedCameraPose(
                viewCamera,
                unshakenPose.WorldPosition,
                unshakenPose.WorldRotation,
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

        internal void ApplyTopologyTransitionVisualState(in TopologyTransitionVisualState visualState)
        {
            var shakeResult = _topologyTransitionCameraShakeController.Evaluate(
                visualState,
                _topologyTransitionCameraShakeProfile);
            CacheTopologyTransitionShakeResult(shakeResult);
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
            if (_target == null)
            {
                return;
            }

            ResolvePoseHierarchy();
            var unshakenPose = ResolveUnshakenPresentedPose();
            ApplyPresentedPoseToHierarchy(unshakenPose);
            ApplyCachedShakeToHierarchy();
            ApplyDirectCameraPose(unshakenPose);
        }

        private static GameplayCameraSettings CloneConfiguredSettings(GameplayCameraSettings baseSettings)
        {
            return baseSettings?.Clone() ?? GameplayCameraSettings.CreateRuntimeDefault();
        }

        private void ResetAuthoredSceneCameraBaselineUsage()
        {
            _useAuthoredSceneCameraPoseAsBaseline = false;
        }

        private bool HasApplicableAuthoredSceneCameraSettings(GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy)
        {
            return _hasAuthoredSceneCameraPose &&
                   (baselineAuthoringPolicy.UseAuthoredSceneCameraPose ||
                    baselineAuthoringPolicy.UseAuthoredSceneCameraLens);
        }

        private void PrepareAuthoredSceneCameraBaseline(
            Vector3 targetWorldPosition,
            CubeTopologyState topology,
            TopologyRotationVisualMapping topologyRotationVisualMapping)
        {
            var topologyOrbit = GameplayTopologyVisualRotationUtility.ResolveCameraOrbitRotation(
                topology,
                topologyRotationVisualMapping);
            _authoredSceneCameraBaselineLocalPosition =
                Quaternion.Inverse(topologyOrbit) * (_authoredSceneCameraWorldPosition - targetWorldPosition);
            _authoredSceneCameraBaselineLocalRotation =
                Quaternion.Inverse(topologyOrbit) * _authoredSceneCameraWorldRotation;
            _useAuthoredSceneCameraPoseAsBaseline = true;
        }

        private void ApplyAuthoredSceneCameraLens(GameplayCameraSettings resolvedSettings)
        {
            resolvedSettings.PerspectiveFieldOfView = _authoredSceneCameraFieldOfView;
            resolvedSettings.NearClipPlane = _authoredSceneCameraNearClipPlane;
            resolvedSettings.FarClipPlane = _authoredSceneCameraFarClipPlane;
        }

        private void CacheTopologyTransitionShakeResult(TopologyTransitionCameraShakeResult shakeResult)
        {
            _topologyTransitionShakeLocalPosition = shakeResult.LocalPosition;
            _topologyTransitionShakeLocalRotation = shakeResult.LocalRotation;
        }

        private void ResolvePoseHierarchy()
        {
            _orbitPivot = _target != null ? _target.Find(CameraOrbitPivotObjectName) : null;
            _cameraPoseRoot = _orbitPivot != null ? _orbitPivot.Find(CameraPoseRootObjectName) : null;
            _cameraEffectsRoot = _cameraPoseRoot != null ? _cameraPoseRoot.Find(CameraEffectsRootObjectName) : null;
        }

        private UnshakenCameraPose ResolveUnshakenPresentedPose()
        {
            if (_useAuthoredSceneCameraPoseAsBaseline)
            {
                return ResolveAuthoredSceneCameraBaselinePose();
            }

            return ResolveOrbitDistanceCameraPose(
                _target.position,
                _presentedTopologyOrbit,
                ResolveCameraAspect(),
                _visibleCubeBounds,
                pitchDegrees,
                yawDegrees,
                distanceMode,
                manualDistance,
                framingPadding,
                perspectiveFieldOfView);
        }

        private UnshakenCameraPose ResolveAuthoredSceneCameraBaselinePose()
        {
            var worldRotation = _presentedTopologyOrbit * _authoredSceneCameraBaselineLocalRotation;
            var worldPosition = _target.position + (_presentedTopologyOrbit * _authoredSceneCameraBaselineLocalPosition);
            return new UnshakenCameraPose(
                _authoredSceneCameraBaselineLocalPosition,
                _authoredSceneCameraBaselineLocalRotation,
                worldPosition,
                worldRotation);
        }

        private void ApplyPresentedPoseToHierarchy(UnshakenCameraPose unshakenPose)
        {
            if (_orbitPivot != null)
            {
                _orbitPivot.SetLocalPositionAndRotation(Vector3.zero, _presentedTopologyOrbit);
                _orbitPivot.localScale = Vector3.one;
            }

            if (_cameraPoseRoot != null)
            {
                _cameraPoseRoot.SetLocalPositionAndRotation(unshakenPose.LocalPosition, unshakenPose.LocalRotation);
                _cameraPoseRoot.localScale = Vector3.one;
            }
        }

        private void ApplyCachedShakeToHierarchy()
        {
            if (_cameraEffectsRoot == null)
            {
                return;
            }

            _cameraEffectsRoot.SetLocalPositionAndRotation(
                _topologyTransitionShakeLocalPosition,
                _topologyTransitionShakeLocalRotation);
            _cameraEffectsRoot.localScale = Vector3.one;
        }

        private void ApplyDirectCameraPose(UnshakenCameraPose unshakenPose)
        {
            if (_viewCamera == null)
            {
                return;
            }

            var resolvedWorldPosition = unshakenPose.WorldPosition +
                                        (unshakenPose.WorldRotation * _topologyTransitionShakeLocalPosition);
            var resolvedWorldRotation = unshakenPose.WorldRotation * _topologyTransitionShakeLocalRotation;
            ApplyResolvedCameraPose(
                _viewCamera,
                resolvedWorldPosition,
                resolvedWorldRotation,
                perspectiveFieldOfView,
                nearClipPlane,
                farClipPlane,
                clearFlags,
                backgroundColor);
        }

        private float ResolveCameraAspect()
        {
            if (_viewCamera != null)
            {
                return Mathf.Max(_viewCamera.aspect, 0.01f);
            }

            return Mathf.Max((float)Screen.width / Mathf.Max(Screen.height, 1), 0.01f);
        }

        private static float ResolveNearestEquivalentAngleXDegrees(float currentAngleXDegrees, Quaternion rotation)
        {
            var candidateAngleXDegrees = Mathf.DeltaAngle(0f, rotation.eulerAngles.x);
            return currentAngleXDegrees + Mathf.DeltaAngle(currentAngleXDegrees, candidateAngleXDegrees);
        }

        private static void ApplyResolvedCameraPose(
            Camera viewCamera,
            Vector3 worldPosition,
            Quaternion worldRotation,
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
            viewCamera.transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        private static UnshakenCameraPose ResolveOrbitDistanceCameraPose(
            Vector3 targetPosition,
            Quaternion orbitRotation,
            float aspect,
            Bounds visibleCubeBounds,
            float pitchDegrees,
            float yawDegrees,
            CameraDistanceMode distanceMode,
            float manualDistance,
            float framingPadding,
            float perspectiveFieldOfView)
        {
            var lookRotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var distance = ResolveCameraDistance(
                aspect,
                visibleCubeBounds,
                distanceMode,
                manualDistance,
                framingPadding,
                perspectiveFieldOfView);
            var localPosition = Vector3.back * distance;
            var worldRotation = orbitRotation * lookRotation;
            var worldPosition = targetPosition + (worldRotation * localPosition);
            return new UnshakenCameraPose(
                localPosition,
                lookRotation,
                worldPosition,
                worldRotation);
        }

        private static float ResolveCameraDistance(
            float aspect,
            Bounds visibleCubeBounds,
            CameraDistanceMode distanceMode,
            float manualDistance,
            float framingPadding,
            float perspectiveFieldOfView)
        {
            return distanceMode == CameraDistanceMode.Manual
                ? Mathf.Max(0.01f, manualDistance)
                : ResolveAutoFitDistance(
                    aspect,
                    perspectiveFieldOfView,
                    visibleCubeBounds.extents.magnitude,
                    framingPadding);
        }

        private static float ResolveAutoFitDistance(
            float aspect,
            float perspectiveFieldOfView,
            float boundingRadius,
            float framingPadding)
        {
            var verticalHalfFovRadians = Mathf.Deg2Rad * Mathf.Clamp(perspectiveFieldOfView, 1f, 179f) * 0.5f;
            var horizontalHalfFovRadians = Mathf.Atan(Mathf.Tan(verticalHalfFovRadians) * Mathf.Max(aspect, 0.01f));
            var limitingHalfFov = Mathf.Min(verticalHalfFovRadians, horizontalHalfFovRadians);
            var safeRadius = Mathf.Max(0.01f, boundingRadius * Mathf.Max(1f, framingPadding));
            return safeRadius / Mathf.Tan(Mathf.Max(0.01f, limitingHalfFov));
        }
    }
}
