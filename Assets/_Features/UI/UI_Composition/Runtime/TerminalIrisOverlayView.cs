using System;
using Game.Feature.Stages;
using Game.Feature.UI.ViewShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal enum TerminalIrisSetupOperation
    {
        ConfigureTransitionColor = 0,
        Show = 1,
        ApplyClosedEntry = 2,
    }

    internal interface ITerminalIrisSetupView
    {
        ResultTransitionVisualStyle RequireVisualStyle();

        void ConfigureDimSnapshot(ResultDimVisualSnapshot snapshot);

        void ConfigureTransitionColor(Color color);

        float CalculateFullyRevealedRadius(Vector2 center, float fullOpenMargin);

        void Show();

        void Apply(TerminalTransitionPlayback playback);

        Vector2 ReadMaterialCenterForDiagnostics();

        Vector2 LastAppliedCenterForDiagnostics { get; }

        int LastMaterialApplicationFrameForDiagnostics { get; }

        void Hide();
    }

    [DisallowMultipleComponent]
    internal sealed class TerminalIrisOverlayView : MonoBehaviour, ITerminalIrisSetupView
    {
        // Test-only fault seam. Production leaves this null.
        internal static Action<TerminalIrisSetupOperation> BeforeSetupOperationForTests;

        internal const string IrisShaderName = "UI/TerminalIris";
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ClosedOvershootPixelsId =
            Shader.PropertyToID("_ClosedOvershootPixels");
        private static readonly int OuterColorId = Shader.PropertyToID("_OuterColor");
        private static readonly int OuterOpacityId = Shader.PropertyToID("_OuterOpacity");
        private static readonly int EdgeAntiAliasScaleId =
            Shader.PropertyToID("_EdgeAntiAliasScale");
        private static readonly int MinimumAAPixelsId =
            Shader.PropertyToID("_MinimumAAPixels");
        private static readonly int ArtisticFeatherHalfWidthPixelsId =
            Shader.PropertyToID("_ArtisticFeatherHalfWidthPixels");
        private static readonly int RimWidthPixelsId =
            Shader.PropertyToID("_RimWidthPixels");
        private static readonly int RimSoftnessPixelsId =
            Shader.PropertyToID("_RimSoftnessPixels");
        private static readonly int RimFadeOutPixelsId =
            Shader.PropertyToID("_RimFadeOutPixels");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        [SerializeField] private Image _image;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Material _authoredMaterial;
        [SerializeField] private ResultTransitionVisualStyle _resultTransitionStyle;
        private Material _runtimeMaterial;
        private ResultDimVisualSnapshot? _activeDimSnapshot;
        private Color? _activeTransitionColor;
        private int _entryClosedRequestFrame = -1;
        private int _entryClosedRenderFrame = -1;
        private Vector2 _lastAppliedCenter;
        private int _lastMaterialApplicationFrame = -1;
        private int _lastVisibleRenderFrame = -1;

        internal bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;

        internal bool BlocksRaycasts => _canvasGroup != null && _canvasGroup.blocksRaycasts;

        internal Material RuntimeMaterialForTests => _runtimeMaterial;

        internal Material AuthoredMaterialForTests => _authoredMaterial;

        internal Vector2 LastAppliedCenterForDiagnostics => _lastAppliedCenter;

        internal int LastMaterialApplicationFrameForDiagnostics =>
            _lastMaterialApplicationFrame;

        Vector2 ITerminalIrisSetupView.LastAppliedCenterForDiagnostics =>
            LastAppliedCenterForDiagnostics;

        int ITerminalIrisSetupView.LastMaterialApplicationFrameForDiagnostics =>
            LastMaterialApplicationFrameForDiagnostics;

        internal int LastVisibleRenderFrameForDiagnostics => _lastVisibleRenderFrame;

        internal int RuntimeMaterialInstanceIdForDiagnostics =>
            _runtimeMaterial != null ? _runtimeMaterial.GetInstanceID() : 0;

        internal int ImageMaterialInstanceIdForDiagnostics =>
            _image != null && _image.material != null
                ? _image.material.GetInstanceID()
                : 0;

        internal int MaterialForRenderingInstanceIdForDiagnostics =>
            _image != null && _image.materialForRendering != null
                ? _image.materialForRendering.GetInstanceID()
                : 0;

        internal bool HasRenderedEntryClosedFrame =>
            IsVisible &&
            _entryClosedRequestFrame >= 0 &&
            _entryClosedRenderFrame > _entryClosedRequestFrame &&
            _image != null &&
            _image.canvasRenderer != null &&
            !_image.canvasRenderer.cull;

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        }

        internal void Initialize(
            Image image,
            CanvasGroup canvasGroup,
            Material authoredMaterial = null)
        {
            _image = image != null ? image : throw new ArgumentNullException(nameof(image));
            _canvasGroup = canvasGroup != null
                ? canvasGroup
                : throw new ArgumentNullException(nameof(canvasGroup));
            if (authoredMaterial != null)
            {
                _authoredMaterial = authoredMaterial;
            }

            EnsureRuntimeMaterial();
            Hide();
        }

        internal void Show()
        {
            BeforeSetupOperationForTests?.Invoke(TerminalIrisSetupOperation.Show);
            EnsureInitialized();
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _image.raycastTarget = true;
        }

        void ITerminalIrisSetupView.Show() => Show();

        internal void Apply(TerminalTransitionPlayback playback)
        {
            if (playback == null)
            {
                throw new ArgumentNullException(nameof(playback));
            }

            EnsureInitialized();
            _runtimeMaterial.SetVector(CenterId, playback.CurrentCenter);
            _lastAppliedCenter = playback.CurrentCenter;
            _lastMaterialApplicationFrame = Time.frameCount;
            _runtimeMaterial.SetFloat(RadiusId, playback.CurrentRadius);
            _runtimeMaterial.SetFloat(
                ClosedOvershootPixelsId,
                playback.CurrentClosedOvershootPixels);
            ApplyEdge(playback.CurrentEdge);
            _runtimeMaterial.SetColor(
                OuterColorId,
                RequireTransitionColor());
            _runtimeMaterial.SetFloat(OuterOpacityId, 1f);
        }

        void ITerminalIrisSetupView.Apply(TerminalTransitionPlayback playback) =>
            Apply(playback);

        public Vector2 ReadMaterialCenterForDiagnostics()
        {
            EnsureInitialized();
            var center = _runtimeMaterial.GetVector(CenterId);
            return new Vector2(center.x, center.y);
        }

        internal void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_image != null)
            {
                _image.raycastTarget = false;
            }

            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetFloat(ClosedOvershootPixelsId, 0f);
            }

            gameObject.SetActive(false);
        }

        void ITerminalIrisSetupView.Hide() => Hide();

        private void HandleWillRenderCanvases()
        {
            if (IsVisible)
            {
                _lastVisibleRenderFrame = Time.frameCount;
            }

            if (_entryClosedRequestFrame >= 0 && IsVisible)
            {
                _entryClosedRenderFrame = Time.frameCount;
            }
        }

        internal void ApplyClosedEntry(
            Vector2 center,
            TerminalIrisRuntimeOpenPreset preset)
        {
            BeforeSetupOperationForTests?.Invoke(
                TerminalIrisSetupOperation.ApplyClosedEntry);
            EnsureInitialized();
            _runtimeMaterial.SetVector(CenterId, center);
            _lastAppliedCenter = center;
            _lastMaterialApplicationFrame = Time.frameCount;
            _runtimeMaterial.SetFloat(RadiusId, 0f);
            _runtimeMaterial.SetFloat(
                ClosedOvershootPixelsId,
                preset.FinalClosedOvershootPixels);
            ApplyEdge(preset.Edge);
            _runtimeMaterial.SetColor(OuterColorId, RequireTransitionColor());
            _runtimeMaterial.SetFloat(OuterOpacityId, 1f);
            _entryClosedRequestFrame = Time.frameCount;
            _entryClosedRenderFrame = -1;
            Canvas.ForceUpdateCanvases();
        }

        internal void ApplyEntryRadius(float radius, float closedOvershootPixels)
        {
            EnsureInitialized();
            _runtimeMaterial.SetFloat(RadiusId, radius);
            _runtimeMaterial.SetFloat(
                ClosedOvershootPixelsId,
                closedOvershootPixels);
        }

        internal float CalculateFullyRevealedRadius(Vector2 center, float fullOpenMargin)
        {
            EnsureInitialized();
            var canvas = _image.canvas;
            var pixelRect = canvas != null ? canvas.pixelRect : default;
            var aspect = pixelRect.height > 0f
                ? pixelRect.width / pixelRect.height
                : 1f;
            return TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                center,
                aspect,
                fullOpenMargin);
        }

        float ITerminalIrisSetupView.CalculateFullyRevealedRadius(
            Vector2 center,
            float fullOpenMargin) =>
            CalculateFullyRevealedRadius(center, fullOpenMargin);

        internal ResultTransitionVisualStyle RequireVisualStyle()
        {
            if (_resultTransitionStyle == null)
            {
                throw new InvalidOperationException(
                    "TerminalIrisOverlayView requires the canonical ResultTransitionVisualStyle asset.");
            }

            return _resultTransitionStyle;
        }

        ResultTransitionVisualStyle ITerminalIrisSetupView.RequireVisualStyle() =>
            RequireVisualStyle();

        internal void ConfigureDimSnapshot(ResultDimVisualSnapshot snapshot)
        {
            if (!snapshot.IsFullStretch || snapshot.RaycastTarget)
            {
                throw new InvalidOperationException(
                    "Terminal Iris requires the canonical full-stretch visual-only Result Dim snapshot.");
            }

            _activeDimSnapshot = snapshot;
            ConfigureTransitionColor(snapshot.OpaqueColor);
        }

        void ITerminalIrisSetupView.ConfigureDimSnapshot(ResultDimVisualSnapshot snapshot) =>
            ConfigureDimSnapshot(snapshot);

        internal void ConfigureTransitionColor(Color color)
        {
            BeforeSetupOperationForTests?.Invoke(
                TerminalIrisSetupOperation.ConfigureTransitionColor);
            if (float.IsNaN(color.r) ||
                float.IsInfinity(color.r) ||
                float.IsNaN(color.g) ||
                float.IsInfinity(color.g) ||
                float.IsNaN(color.b) ||
                float.IsInfinity(color.b) ||
                float.IsNaN(color.a) ||
                float.IsInfinity(color.a))
            {
                throw new ArgumentOutOfRangeException(nameof(color));
            }

            color.a = 1f;
            _activeTransitionColor = color;
        }

        void ITerminalIrisSetupView.ConfigureTransitionColor(Color color) =>
            ConfigureTransitionColor(color);

        internal void RequestClosedRenderAcknowledgement()
        {
            EnsureInitialized();
            _entryClosedRequestFrame = Time.frameCount;
            _entryClosedRenderFrame = -1;
            Canvas.ForceUpdateCanvases();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }

        private void EnsureInitialized()
        {
            if (_image == null || _canvasGroup == null)
            {
                throw new InvalidOperationException(
                    "TerminalIrisOverlayView must be initialized by GameplayUiCanvasRootView.");
            }

            EnsureRuntimeMaterial();
        }

        private ResultDimVisualSnapshot RequireDimSnapshot()
        {
            return _activeDimSnapshot ??
                   throw new InvalidOperationException(
                       "Terminal Iris must capture an immutable Result Dim snapshot before playback.");
        }

        private Color RequireTransitionColor()
        {
            return _activeTransitionColor ??
                   throw new InvalidOperationException(
                       "Terminal Iris must capture an immutable transition color before playback.");
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
            {
                return;
            }

            if (_authoredMaterial == null)
            {
                throw new InvalidOperationException(
                    "TerminalIrisOverlayView requires a serialized authored material.");
            }

            if (_authoredMaterial.shader == null ||
                !string.Equals(_authoredMaterial.shader.name, IrisShaderName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Terminal Iris authored material must reference shader '{IrisShaderName}'.");
            }

            _runtimeMaterial = new Material(_authoredMaterial)
            {
                name = "TerminalIrisOverlayView (Runtime)",
                hideFlags = HideFlags.DontSave,
            };
            _image.material = _runtimeMaterial;
            _image.color = Color.white;
        }

        private void ApplyEdge(TerminalIrisRuntimeEdgeSettings edge)
        {
            _runtimeMaterial.SetFloat(EdgeAntiAliasScaleId, edge.EdgeAntiAliasScale);
            _runtimeMaterial.SetFloat(MinimumAAPixelsId, edge.MinimumAAPixels);
            _runtimeMaterial.SetFloat(
                ArtisticFeatherHalfWidthPixelsId,
                edge.ArtisticFeatherHalfWidthPixels);
            _runtimeMaterial.SetFloat(RimWidthPixelsId, edge.RimWidthPixels);
            _runtimeMaterial.SetFloat(RimSoftnessPixelsId, edge.RimSoftnessPixels);
            _runtimeMaterial.SetFloat(RimFadeOutPixelsId, edge.RimFadeOutPixels);
            _runtimeMaterial.SetColor(RimColorId, edge.RimColor);
        }
    }
}
