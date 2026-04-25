using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct GameplayHostTopologyVisualRuntimeBootstrapResult
    {
        internal GameplayHostTopologyVisualRuntimeBootstrapResult(
            Camera viewCamera,
            GameplayCameraRig viewCameraRig)
        {
            ViewCamera = viewCamera;
            ViewCameraRig = viewCameraRig;
        }

        internal Camera ViewCamera { get; }

        internal GameplayCameraRig ViewCameraRig { get; }
    }

    internal static class GameplayHostTopologyVisualRuntimeBootstrap
    {
        internal static GameplayHostTopologyVisualRuntimeBootstrapResult Attach(
            GameObject hostObject,
            GameplayResolvedCameraStartupPlan startupPlan,
            GameplayTickViewPresenter presenter,
            Transform viewCameraTarget,
            Bounds visibleCubeBounds)
        {
            if (hostObject == null)
            {
                throw new ArgumentNullException(nameof(hostObject));
            }

            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            presenter.AttachOutputCamera(startupPlan.OutputCamera);

            var viewCameraRig = AttachViewCameraRig(
                hostObject,
                startupPlan,
                viewCameraTarget,
                visibleCubeBounds);
            presenter.AttachCameraRuntime(viewCameraRig, startupPlan.OutputCameraBrain);

            var topologyTransitionPostFxController = hostObject.GetComponent<TopologyTransitionPostFxController>() ??
                                                     hostObject.AddComponent<TopologyTransitionPostFxController>();
            topologyTransitionPostFxController.Initialize(
                startupPlan.TopologyTransitionPostFxProfile,
                startupPlan.OutputCamera);
            presenter.AttachTopologyTransitionPostFxController(topologyTransitionPostFxController);

            return new GameplayHostTopologyVisualRuntimeBootstrapResult(startupPlan.ViewCamera, viewCameraRig);
        }

        private static GameplayCameraRig AttachViewCameraRig(
            GameObject hostObject,
            GameplayResolvedCameraStartupPlan startupPlan,
            Transform viewCameraTarget,
            Bounds visibleCubeBounds)
        {
            if (viewCameraTarget == null)
            {
                var existingRig = hostObject.GetComponent<GameplayCameraRig>();
                if (existingRig != null)
                {
                    existingRig.enabled = false;
                }

                return existingRig;
            }

            var cameraRig = hostObject.GetComponent<GameplayCameraRig>() ?? hostObject.AddComponent<GameplayCameraRig>();
            cameraRig.enabled = true;
            cameraRig.ConfigureTopologyTransitionCameraShake(startupPlan.TopologyTransitionCameraShakeProfile);
            cameraRig.ApplySettings(startupPlan.ResolvedCameraSettings);
            cameraRig.Initialize(
                startupPlan.UsesDirectCameraPath ? startupPlan.ViewCamera : null,
                viewCameraTarget,
                visibleCubeBounds);
            return cameraRig;
        }
    }
}
