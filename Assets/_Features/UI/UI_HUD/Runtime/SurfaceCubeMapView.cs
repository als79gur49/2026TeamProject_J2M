using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public enum SurfaceCubeMapFaceRole
    {
        Floor = 0,
        Front = 1,
        Ceiling = 2,
        Back = 3,
        Left = 4,
        Right = 5,
    }

    public sealed class SurfaceCubeMapView : MonoBehaviour
    {
        public const string PreviewLayerName = "SurfacePreview";

        private const int DefaultPreviewTextureSize = 256;
        private const float DefaultOrthographicSize = 0.52f;
        private const float PulseDurationSeconds = 0.28f;

        private static readonly Quaternion PreviewPoseOffset = Quaternion.Euler(12.0f, -18.0f, 0.0f);

        [SerializeField] private RawImage _previewImage;
        [SerializeField] private GameObject _cubeMapPrefab;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private Transform _cubeRoot;
        [SerializeField] private Renderer[] _faceRenderers;
        [SerializeField] private RenderTexture _renderTexture;
        [SerializeField] private int _previewTextureSize = DefaultPreviewTextureSize;
        [SerializeField] private float _orthographicSize = DefaultOrthographicSize;

        private SurfaceIndicatorViewModel _viewModel;
        private GameObject _runtimeRoot;
        private RenderTexture _runtimeRenderTexture;
        private Tween _pulseTween;
        private int _lastPulseSequenceId;
        private bool _ownsCamera;
        private bool _ownsCubeRoot;

        public RawImage PreviewImage => _previewImage;

        public Camera PreviewCamera => _previewCamera;

        public Transform CubeRoot => _cubeRoot;

        public IReadOnlyList<Renderer> FaceRenderers => _faceRenderers ?? Array.Empty<Renderer>();

        public static int PreviewLayer => ResolvePreviewLayerOrThrow();

        public static int PreviewLayerMask => 1 << PreviewLayer;

        public void Bind(SurfaceIndicatorViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_previewImage, nameof(_previewImage));
            RequireReference(_cubeMapPrefab, nameof(_cubeMapPrefab));
        }

        public static bool TryResolveFaceRole(Vector3 localPosition, out SurfaceCubeMapFaceRole role)
        {
            var absX = Math.Abs(localPosition.x);
            var absY = Math.Abs(localPosition.y);
            var absZ = Math.Abs(localPosition.z);

            if (absX >= absY && absX >= absZ && absX > 0.0f)
            {
                role = localPosition.x < 0.0f
                    ? SurfaceCubeMapFaceRole.Left
                    : SurfaceCubeMapFaceRole.Right;
                return true;
            }

            if (absY >= absX && absY >= absZ && absY > 0.0f)
            {
                role = localPosition.y < 0.0f
                    ? SurfaceCubeMapFaceRole.Floor
                    : SurfaceCubeMapFaceRole.Ceiling;
                return true;
            }

            if (absZ > 0.0f)
            {
                role = localPosition.z < 0.0f
                    ? SurfaceCubeMapFaceRole.Back
                    : SurfaceCubeMapFaceRole.Front;
                return true;
            }

            role = default;
            return false;
        }

        public static bool IsSelectableFace(SurfaceCubeMapFaceRole role)
        {
            return role == SurfaceCubeMapFaceRole.Floor ||
                   role == SurfaceCubeMapFaceRole.Front ||
                   role == SurfaceCubeMapFaceRole.Ceiling ||
                   role == SurfaceCubeMapFaceRole.Back;
        }

        public static Quaternion GetTargetRotation(int faceIndex)
        {
            return PreviewPoseOffset * Quaternion.FromToRotation(GetFaceDirection(faceIndex), Vector3.back);
        }

        private void OnEnable()
        {
            RefreshView();
        }

        private void OnDisable()
        {
            KillPulse();
        }

        private void OnDestroy()
        {
            KillPulse();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            DestroyRuntimeRig();
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            ValidateAuthoredStructureOrThrow();
            if (_viewModel == null)
            {
                if (_previewImage != null)
                {
                    _previewImage.enabled = false;
                }

                return;
            }

            if (!IsGraphicsPreviewSupported())
            {
                if (_previewImage != null)
                {
                    _previewImage.enabled = false;
                }

                return;
            }

            EnsureRuntimeRig();

            if (_previewImage != null)
            {
                _previewImage.enabled = true;
            }

            if (_cubeRoot == null)
            {
                return;
            }

            _cubeRoot.localRotation = ResolveRotation(_viewModel);
            PlayPulseIfNeeded(_viewModel.AnimationHint);
        }

        private Quaternion ResolveRotation(SurfaceIndicatorViewModel viewModel)
        {
            if (!viewModel.IsTransitionActive)
            {
                return GetTargetRotation(viewModel.CurrentFaceIndex);
            }

            var sourceIndex = ResolveFaceIndex(viewModel.SourceFaceLabel, viewModel.CurrentFaceIndex);
            var destinationIndex = ResolveFaceIndex(viewModel.DestinationFaceLabel, viewModel.CurrentFaceIndex);
            return Quaternion.Slerp(
                GetTargetRotation(sourceIndex),
                GetTargetRotation(destinationIndex),
                viewModel.Progress01);
        }

        private static int ResolveFaceIndex(string faceLabel, int fallback)
        {
            if (string.IsNullOrWhiteSpace(faceLabel))
            {
                return fallback;
            }

            for (var i = 0; i < SurfaceIndicatorViewModel.CanonicalFaceLabels.Length; i++)
            {
                if (string.Equals(SurfaceIndicatorViewModel.CanonicalFaceLabels[i], faceLabel, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return fallback;
        }

        private static Vector3 GetFaceDirection(int faceIndex)
        {
            switch (faceIndex)
            {
                case 0:
                    return Vector3.down;
                case 1:
                    return Vector3.forward;
                case 2:
                    return Vector3.up;
                case 3:
                    return Vector3.back;
                default:
                    return Vector3.down;
            }
        }

        private void EnsureRuntimeRig()
        {
            if (_previewImage.texture == null)
            {
                _previewImage.texture = ResolveRenderTexture();
            }

            if (_runtimeRoot == null)
            {
                _runtimeRoot = new GameObject("SurfaceCubeMapPreviewRuntime");
                _runtimeRoot.hideFlags = HideFlags.HideAndDontSave;
            }

            EnsureCamera();
            EnsureCubeRoot();
        }

        private static bool IsGraphicsPreviewSupported()
        {
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        }

        private Texture ResolveRenderTexture()
        {
            if (_renderTexture != null)
            {
                return _renderTexture;
            }

            if (_runtimeRenderTexture == null)
            {
                var size = Math.Max(1, _previewTextureSize <= 0 ? DefaultPreviewTextureSize : _previewTextureSize);
                _runtimeRenderTexture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
                {
                    name = "SurfaceCubeMapPreviewRuntimeRT",
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                _runtimeRenderTexture.Create();
            }

            return _runtimeRenderTexture;
        }

        private void EnsureCamera()
        {
            if (_previewCamera == null)
            {
                var cameraObject = new GameObject("SurfaceCubeMapPreviewCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                cameraObject.transform.SetParent(_runtimeRoot.transform, false);
                _previewCamera = cameraObject.AddComponent<Camera>();
                _ownsCamera = true;
            }

            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = Color.clear;
            _previewCamera.orthographic = true;
            _previewCamera.orthographicSize = _orthographicSize > 0.0f ? _orthographicSize : DefaultOrthographicSize;
            _previewCamera.nearClipPlane = 0.01f;
            _previewCamera.farClipPlane = 10.0f;
            _previewCamera.cullingMask = PreviewLayerMask;
            _previewCamera.targetTexture = ResolveRenderTexture() as RenderTexture;
            _previewCamera.transform.SetPositionAndRotation(new Vector3(0.0f, 0.0f, -2.0f), Quaternion.identity);
        }

        private void EnsureCubeRoot()
        {
            if (_cubeRoot == null)
            {
                var cubeObject = Instantiate(_cubeMapPrefab, _runtimeRoot.transform);
                cubeObject.name = "SurfaceCubeMapPreviewCube";
                cubeObject.hideFlags = HideFlags.HideAndDontSave;
                _cubeRoot = cubeObject.transform;
                _ownsCubeRoot = true;
            }

            _cubeRoot.SetParent(_runtimeRoot.transform, false);
            _cubeRoot.localPosition = Vector3.zero;
            _cubeRoot.localScale = Vector3.one;
            SetLayerRecursively(_cubeRoot.gameObject, PreviewLayer);

            if (_faceRenderers == null || _faceRenderers.Length == 0)
            {
                _faceRenderers = _cubeRoot.GetComponentsInChildren<Renderer>(true);
            }
        }

        private void PlayPulseIfNeeded(SurfaceIndicatorAnimationHint animationHint)
        {
            if (!animationHint.PulseDestination || animationHint.SequenceId == _lastPulseSequenceId)
            {
                return;
            }

            _lastPulseSequenceId = animationHint.SequenceId;
            KillPulse();
            _cubeRoot.localScale = Vector3.one;
            _pulseTween = _cubeRoot
                .DOPunchScale(Vector3.one * 0.08f, PulseDurationSeconds, 8, 0.7f)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void KillPulse()
        {
            if (_pulseTween == null)
            {
                return;
            }

            _pulseTween.Kill(false);
            _pulseTween = null;
        }

        private void DestroyRuntimeRig()
        {
            if (_runtimeRenderTexture != null)
            {
                _runtimeRenderTexture.Release();
                DestroyRuntimeObject(_runtimeRenderTexture);
                _runtimeRenderTexture = null;
            }

            if (_ownsCamera && _previewCamera != null)
            {
                DestroyRuntimeObject(_previewCamera.gameObject);
                _previewCamera = null;
            }

            if (_ownsCubeRoot && _cubeRoot != null)
            {
                DestroyRuntimeObject(_cubeRoot.gameObject);
                _cubeRoot = null;
            }

            if (_runtimeRoot != null)
            {
                DestroyRuntimeObject(_runtimeRoot);
                _runtimeRoot = null;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (var i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private static int ResolvePreviewLayerOrThrow()
        {
            var layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceCubeMapView)} requires Unity layer '{PreviewLayerName}' to isolate the render-texture preview from gameplay cameras.");
            }

            return layer;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceCubeMapView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
