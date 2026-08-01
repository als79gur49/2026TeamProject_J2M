using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal enum TerminalFocusCaptureFailureReason
    {
        None = 0,
        InvalidEntityId = 1,
        RegistryNotFound = 2,
        CameraNotFound = 3,
        ViewNotFound = 4,
        NoRenderers = 5,
        InvalidBounds = 6,
        ProjectionBehindCamera = 7,
        ProjectionInvalid = 8,
        OutsideViewport = 9,
        InvalidRadius = 10,
    }

    internal readonly struct TerminalFocusCaptureDiagnostics
    {
        internal TerminalFocusCaptureDiagnostics(
            int entityId,
            TerminalFocusCaptureFailureReason failureReason,
            Camera outputCamera,
            GameplayEntityView view,
            int rendererCount,
            Vector3 playerWorldPosition,
            Vector3 playerViewPosition,
            Vector3 rawWorldToViewportPoint,
            Vector2 projectedCenter,
            Vector2 capturedCenter,
            float capturedRadius,
            bool wasClamped,
            bool isFallback)
        {
            EntityId = entityId;
            FailureReason = failureReason;
            OutputCamera = outputCamera;
            View = view;
            RendererCount = rendererCount;
            PlayerWorldPosition = playerWorldPosition;
            PlayerViewPosition = playerViewPosition;
            RawWorldToViewportPoint = rawWorldToViewportPoint;
            ProjectedCenter = projectedCenter;
            CapturedCenter = capturedCenter;
            CapturedRadius = capturedRadius;
            WasClamped = wasClamped;
            IsFallback = isFallback;
        }

        internal int EntityId { get; }

        internal TerminalFocusCaptureFailureReason FailureReason { get; }

        internal Camera OutputCamera { get; }

        internal GameplayEntityView View { get; }

        internal int RendererCount { get; }

        internal Vector3 PlayerWorldPosition { get; }

        internal Vector3 PlayerViewPosition { get; }

        internal Vector3 RawWorldToViewportPoint { get; }

        internal Vector2 ProjectedCenter { get; }

        internal Vector2 CapturedCenter { get; }

        internal float CapturedRadius { get; }

        internal bool WasClamped { get; }

        internal bool IsFallback { get; }

        internal bool Succeeded =>
            !IsFallback &&
            FailureReason == TerminalFocusCaptureFailureReason.None;
    }

    internal sealed class GameplayTerminalFocusTargetSource : ITerminalFocusTargetSource
    {
        private const float SafeEdge = 0.025f;
        private readonly Camera _camera;
        private readonly GameplayEntityViewRegistry _viewRegistry;

        public GameplayTerminalFocusTargetSource(
            GameplayEntityViewRegistry viewRegistry,
            Camera camera)
        {
            _viewRegistry = viewRegistry;
            _camera = camera;
        }

        internal TerminalFocusCaptureDiagnostics LastCaptureDiagnostics { get; private set; }

        public bool TryCapture(int entityId, out TerminalFocusTarget target)
        {
            target = default;
            if (entityId <= 0)
            {
                return Fail(entityId, TerminalFocusCaptureFailureReason.InvalidEntityId);
            }

            if (_viewRegistry == null)
            {
                return Fail(entityId, TerminalFocusCaptureFailureReason.RegistryNotFound);
            }

            if (_camera == null)
            {
                return Fail(entityId, TerminalFocusCaptureFailureReason.CameraNotFound);
            }

            if (!_viewRegistry.TryGetView(entityId, out var view) || view == null)
            {
                return Fail(entityId, TerminalFocusCaptureFailureReason.ViewNotFound);
            }

            var renderers = view.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
            {
                return Fail(
                    entityId,
                    TerminalFocusCaptureFailureReason.NoRenderers,
                    view);
            }

            var corners = new List<Vector3>(renderers.Length * 8);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !IsFinite(renderer.bounds))
                {
                    continue;
                }

                AddBoundsCorners(renderer.bounds, corners);
            }

            if (corners.Count == 0)
            {
                return Fail(
                    entityId,
                    TerminalFocusCaptureFailureReason.InvalidBounds,
                    view,
                    renderers.Length);
            }

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var projectedCorners = new List<Vector2>(corners.Count);
            for (var i = 0; i < corners.Count; i++)
            {
                var viewport = _camera.WorldToViewportPoint(corners[i]);
                if (viewport.z <= 0f)
                {
                    return Fail(
                        entityId,
                        TerminalFocusCaptureFailureReason.ProjectionBehindCamera,
                        view,
                        renderers.Length);
                }

                if (float.IsNaN(viewport.x) ||
                    float.IsNaN(viewport.y) ||
                    float.IsInfinity(viewport.x) ||
                    float.IsInfinity(viewport.y))
                {
                    return Fail(
                        entityId,
                        TerminalFocusCaptureFailureReason.ProjectionInvalid,
                        view,
                        renderers.Length);
                }

                min = Vector2.Min(min, viewport);
                max = Vector2.Max(max, viewport);
                projectedCorners.Add(viewport);
            }

            if (max.x < 0f || min.x > 1f || max.y < 0f || min.y > 1f)
            {
                return Fail(
                    entityId,
                    TerminalFocusCaptureFailureReason.OutsideViewport,
                    view,
                    renderers.Length);
            }

            var projectedCenter = (min + max) * 0.5f;
            var center = projectedCenter;
            center.x = Mathf.Clamp(center.x, SafeEdge, 1f - SafeEdge);
            center.y = Mathf.Clamp(center.y, SafeEdge, 1f - SafeEdge);
            var rawWorldToViewportPoint = _camera.WorldToViewportPoint(
                CalculateBoundsCenter(renderers));
            var aspect = _camera.pixelHeight > 0
                ? (float)_camera.pixelWidth / _camera.pixelHeight
                : 1f;
            var aspectCorrectedRadius = 0f;
            for (var i = 0; i < projectedCorners.Count; i++)
            {
                var delta = projectedCorners[i] - center;
                delta.x *= aspect;
                aspectCorrectedRadius = Mathf.Max(
                    aspectCorrectedRadius,
                    delta.magnitude);
            }
            if (float.IsNaN(aspectCorrectedRadius) ||
                float.IsInfinity(aspectCorrectedRadius) ||
                aspectCorrectedRadius <= 0f)
            {
                return Fail(
                    entityId,
                    TerminalFocusCaptureFailureReason.InvalidRadius,
                    view,
                    renderers.Length,
                    projectedCenter);
            }

            target = new TerminalFocusTarget(center, aspectCorrectedRadius, isFallback: false);
            LastCaptureDiagnostics = new TerminalFocusCaptureDiagnostics(
                entityId,
                TerminalFocusCaptureFailureReason.None,
                _camera,
                view,
                renderers.Length,
                CalculateBoundsCenter(renderers),
                view.transform.position,
                rawWorldToViewportPoint,
                projectedCenter,
                center,
                aspectCorrectedRadius,
                Vector2.SqrMagnitude(center - projectedCenter) > 0.00000001f,
                isFallback: false);
            return true;
        }

        private bool Fail(
            int entityId,
            TerminalFocusCaptureFailureReason reason,
            GameplayEntityView view = null,
            int rendererCount = 0,
            Vector2 projectedCenter = default)
        {
            LastCaptureDiagnostics = new TerminalFocusCaptureDiagnostics(
                entityId,
                reason,
                _camera,
                view,
                rendererCount,
                default,
                view != null ? view.transform.position : default,
                default,
                projectedCenter,
                default,
                0f,
                wasClamped: false,
                isFallback: true);
            return false;
        }

        private static bool IsFinite(Bounds bounds)
        {
            return IsFinite(bounds.center) &&
                   IsFinite(bounds.extents) &&
                   bounds.extents.sqrMagnitude > 0f;
        }

        private static Vector3 CalculateBoundsCenter(IReadOnlyList<Renderer> renderers)
        {
            var hasBounds = false;
            var combined = default(Bounds);
            for (var index = 0; index < renderers.Count; index++)
            {
                var renderer = renderers[index];
                if (renderer == null || !IsFinite(renderer.bounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? combined.center : Vector3.zero;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z);
        }

        private static void AddBoundsCorners(Bounds bounds, ICollection<Vector3> corners)
        {
            var center = bounds.center;
            var extents = bounds.extents;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        corners.Add(center + Vector3.Scale(extents, new Vector3(x, y, z)));
                    }
                }
            }
        }
    }
}
