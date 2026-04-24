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
            GameplaySceneHostConfiguration configuration,
            GameplayTickViewPresenter presenter,
            Transform viewCameraTarget,
            Bounds visibleCubeBounds)
        {
            if (hostObject == null)
            {
                throw new ArgumentNullException(nameof(hostObject));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            var viewCamera = configuration.ViewCamera ?? (configuration.SnapViewCameraToTarget ? Camera.main : null);
            var outputCamera = configuration.ViewCamera ?? Camera.main;
            var outputCameraBrain = outputCamera != null
                ? outputCamera.GetComponent<CinemachineBrain>()
                : null;
            presenter.AttachOutputCamera(outputCamera);

            var viewCameraRig = AttachViewCameraRig(
                hostObject,
                configuration,
                viewCamera,
                outputCameraBrain,
                viewCameraTarget,
                visibleCubeBounds);
            presenter.AttachCameraRuntime(viewCameraRig, outputCameraBrain);

            var topologyTransitionPostFxController = hostObject.GetComponent<TopologyTransitionPostFxController>() ??
                                                     hostObject.AddComponent<TopologyTransitionPostFxController>();
            topologyTransitionPostFxController.Initialize(configuration.TopologyTransitionPostFxProfile, outputCamera);
            presenter.AttachTopologyTransitionPostFxController(topologyTransitionPostFxController);

            return new GameplayHostTopologyVisualRuntimeBootstrapResult(viewCamera, viewCameraRig);
        }

        private static GameplayCameraRig AttachViewCameraRig(
            GameObject hostObject,
            GameplaySceneHostConfiguration configuration,
            Camera viewCamera,
            CinemachineBrain outputCameraBrain,
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
            cameraRig.ConfigureTopologyTransitionCameraShake(configuration.TopologyTransitionCameraShakeProfile);
            var resolvedCameraSettings = cameraRig.ResolveConfiguredSettings(
                configuration.CameraSettings ?? GameplayCameraSettings.CreateRuntimeDefault(),
                viewCameraTarget.position,
                configuration.InitialTopology,
                configuration.TopologyRotationVisualMapping);
            cameraRig.ApplySettings(resolvedCameraSettings);
            cameraRig.Initialize(
                outputCameraBrain == null ? viewCamera : null,
                viewCameraTarget,
                visibleCubeBounds);
            return cameraRig;
        }
    }
}
