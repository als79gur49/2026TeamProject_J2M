using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class TileFeatureVisualPoseSynchronizer
    {
        private readonly TileFeatureVisualRegistry _registry;
        private readonly ISurfaceCellPresentationPoseResolver _poseResolver;

        public TileFeatureVisualPoseSynchronizer(
            TileFeatureVisualRegistry registry,
            ISurfaceCellPresentationPoseResolver poseResolver)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
        }

        public void RefreshAll()
        {
            foreach (var target in _registry.Targets)
            {
                Refresh(target);
            }
        }

        public void RefreshAll(CubeTopologyState topology)
        {
            if (_poseResolver is BoardSurfaceCellPresentationPoseResolver boardSurfacePoseResolver)
            {
                boardSurfacePoseResolver.RefreshTopology(topology);
            }

            RefreshAll();
        }

        public void Refresh(ITileFeatureVisualTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (target is UnityEngine.Object unityTarget &&
                unityTarget == null)
            {
                return;
            }

            if (!TryResolvePresentationRoot(target, out var presentationRoot))
            {
                UnityEngine.Debug.LogWarning(
                    $"TileFeature visual target for TileId {target.TileId} cannot be positioned because it is not backed by a Unity Component.");
                return;
            }

            if (_poseResolver.TryResolvePose(target.Cell, out var pose))
            {
                presentationRoot.localPosition = pose.LocalPosition;
                presentationRoot.localRotation = pose.LocalRotation;
                presentationRoot.localScale = pose.LocalScale;
                presentationRoot.gameObject.SetActive(true);
                return;
            }

            presentationRoot.gameObject.SetActive(false);
        }

        private static bool TryResolvePresentationRoot(
            ITileFeatureVisualTarget target,
            out Transform presentationRoot)
        {
            if (target is TileFeatureVisualTargetView targetView)
            {
                presentationRoot = targetView.PresentationRoot;
                return presentationRoot != null;
            }

            if (target is Component component)
            {
                presentationRoot = component.transform;
                return presentationRoot != null;
            }

            presentationRoot = null;
            return false;
        }
    }
}
