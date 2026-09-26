using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayCameraViewSnapshot
    {
        public GameplayCameraViewSnapshot(
            Vector3 worldPosition,
            Quaternion worldRotation,
            float verticalFieldOfViewDegrees,
            float aspect,
            float nearClipPlane)
        {
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            VerticalFieldOfViewDegrees = verticalFieldOfViewDegrees;
            Aspect = aspect;
            NearClipPlane = nearClipPlane;
        }

        public Vector3 WorldPosition { get; }

        public Quaternion WorldRotation { get; }

        public float VerticalFieldOfViewDegrees { get; }

        public float Aspect { get; }

        public float NearClipPlane { get; }

        public bool IsValid =>
            VerticalFieldOfViewDegrees > 0f && VerticalFieldOfViewDegrees < 180f &&
            Aspect > 0f && NearClipPlane > 0f &&
            IsFinite(WorldPosition.x) && IsFinite(WorldPosition.y) && IsFinite(WorldPosition.z) &&
            IsFinite(WorldRotation.x) && IsFinite(WorldRotation.y) &&
            IsFinite(WorldRotation.z) && IsFinite(WorldRotation.w) &&
            IsFinite(VerticalFieldOfViewDegrees) && IsFinite(Aspect) && IsFinite(NearClipPlane);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public interface IGameplayCameraAdditivePosePort
    {
        void ApplyAdditivePose(Vector3 localPosition, Quaternion localRotation);

        void ResetAdditivePose();
    }

    [DisallowMultipleComponent]
    public sealed class GameplayCameraRig : MonoBehaviour,
        IGameplayCameraAdditivePosePort,
        IGameplayCameraVisibilityPort
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
        private Transform _target;
        private Vector3 _additiveLocalPosition;
        private Quaternion _additiveLocalRotation = Quaternion.identity;
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

        internal Vector3 AdditiveLocalPosition => _additiveLocalPosition;

        internal Quaternion AdditiveLocalRotation => _additiveLocalRotation;

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

        public void ApplyAdditivePose(Vector3 localPosition, Quaternion localRotation)
        {
            _additiveLocalPosition = localPosition;
            _additiveLocalRotation = localRotation;

            if (_isInitialized)
            {
                ApplyCameraPose();
            }
        }

        public void ResetAdditivePose()
        {
            _additiveLocalPosition = Vector3.zero;
            _additiveLocalRotation = Quaternion.identity;

            if (_isInitialized)
            {
                ApplyCameraPose();
            }
        }

        public bool TryProjectUnshakenWorldPoint(
            Vector3 worldPosition,
            out Vector3 viewportPoint)
        {
            viewportPoint = default;
            if (!_isInitialized || _target == null || !IsFinite(worldPosition))
            {
                return false;
            }

            var unshakenPose = ResolveUnshakenPresentedPose();
            var cameraLocalPoint = Quaternion.Inverse(unshakenPose.WorldRotation) *
                                   (worldPosition - unshakenPose.WorldPosition);
            var depth = cameraLocalPoint.z;
            var verticalTangent = Mathf.Tan(
                Mathf.Deg2Rad * Mathf.Clamp(perspectiveFieldOfView, 1f, 179f) * 0.5f);
            var aspect = ResolveCameraAspect();
            var projectionDepth = Mathf.Abs(depth) > 0.000001f
                ? depth
                : depth >= 0f ? 0.000001f : -0.000001f;
            viewportPoint = new Vector3(
                0.5f + (cameraLocalPoint.x / (2f * projectionDepth * verticalTangent * aspect)),
                0.5f + (cameraLocalPoint.y / (2f * projectionDepth * verticalTangent)),
                depth);
            return IsFinite(viewportPoint);
        }

        public bool TryResolveUnshakenViewForOrbit(
            Quaternion orbitRotation,
            out GameplayCameraViewSnapshot snapshot)
        {
            snapshot = default;
            if (!_isInitialized || _target == null ||
                !IsFinite(orbitRotation.x) || !IsFinite(orbitRotation.y) ||
                !IsFinite(orbitRotation.z) || !IsFinite(orbitRotation.w))
            {
                return false;
            }

            var pose = ResolveUnshakenPoseForOrbit(orbitRotation);
            snapshot = new GameplayCameraViewSnapshot(
                pose.WorldPosition,
                pose.WorldRotation,
                perspectiveFieldOfView,
                ResolveCameraAspect(),
                nearClipPlane);
            return snapshot.IsValid;
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
            ApplyCachedAdditivePoseToHierarchy();
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

        private void ResolvePoseHierarchy()
        {
            _orbitPivot = _target != null ? _target.Find(CameraOrbitPivotObjectName) : null;
            _cameraPoseRoot = _orbitPivot != null ? _orbitPivot.Find(CameraPoseRootObjectName) : null;
            _cameraEffectsRoot = _cameraPoseRoot != null ? _cameraPoseRoot.Find(CameraEffectsRootObjectName) : null;
        }

        private UnshakenCameraPose ResolveUnshakenPresentedPose()
        {
            return ResolveUnshakenPoseForOrbit(_presentedTopologyOrbit);
        }

        private UnshakenCameraPose ResolveUnshakenPoseForOrbit(Quaternion orbitRotation)
        {
            if (_useAuthoredSceneCameraPoseAsBaseline)
            {
                return ResolveAuthoredSceneCameraBaselinePose(orbitRotation);
            }

            return ResolveOrbitDistanceCameraPose(
                _target.position,
                orbitRotation,
                ResolveCameraAspect(),
                _visibleCubeBounds,
                pitchDegrees,
                yawDegrees,
                distanceMode,
                manualDistance,
                framingPadding,
                perspectiveFieldOfView);
        }

        private UnshakenCameraPose ResolveAuthoredSceneCameraBaselinePose(Quaternion orbitRotation)
        {
            var worldRotation = orbitRotation * _authoredSceneCameraBaselineLocalRotation;
            var worldPosition = _target.position + (orbitRotation * _authoredSceneCameraBaselineLocalPosition);
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

        private void ApplyCachedAdditivePoseToHierarchy()
        {
            if (_cameraEffectsRoot == null)
            {
                return;
            }

            _cameraEffectsRoot.SetLocalPositionAndRotation(
                _additiveLocalPosition,
                _additiveLocalRotation);
            _cameraEffectsRoot.localScale = Vector3.one;
        }

        private void ApplyDirectCameraPose(UnshakenCameraPose unshakenPose)
        {
            if (_viewCamera == null)
            {
                return;
            }

            var resolvedWorldPosition = unshakenPose.WorldPosition +
                                        (unshakenPose.WorldRotation * _additiveLocalPosition);
            var resolvedWorldRotation = unshakenPose.WorldRotation * _additiveLocalRotation;
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

        private void OnDisable()
        {
            ResetAdditivePose();
        }

        private void OnDestroy()
        {
            ResetAdditivePose();
        }

        private float ResolveCameraAspect()
        {
            if (_viewCamera != null)
            {
                return Mathf.Max(_viewCamera.aspect, 0.01f);
            }

            return Mathf.Max((float)Screen.width / Mathf.Max(Screen.height, 1), 0.01f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
