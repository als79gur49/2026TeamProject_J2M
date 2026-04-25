using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayCameraStartupPlanComposer
    {
        internal static GameplayResolvedCameraStartupPlan Compose(
            GameObject hostObject,
            GameplaySceneHostConfiguration configuration,
            Transform viewCameraTarget)
        {
            if (hostObject == null)
            {
                throw new ArgumentNullException(nameof(hostObject));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var viewCamera = ResolveViewCamera(configuration);
            var outputCamera = ResolveOutputCamera(configuration);
            var outputCameraBrain = ResolveOutputCameraBrain(outputCamera);
            var usesHierarchyCinemachinePath = outputCameraBrain != null;
            var usesDirectCameraPath = !usesHierarchyCinemachinePath && viewCamera != null;
            var cameraRig = hostObject.GetComponent<GameplayCameraRig>() ?? hostObject.AddComponent<GameplayCameraRig>();
            var resolvedCameraSettings = ResolveConfiguredCameraSettings(cameraRig, configuration, viewCameraTarget);

            return new GameplayResolvedCameraStartupPlan(
                resolvedCameraSettings,
                configuration.CameraBaselineAuthoringPolicy,
                configuration.SnapViewCameraToTarget,
                viewCamera,
                outputCamera,
                outputCameraBrain,
                configuration.TopologyTransitionCameraShakeProfile,
                configuration.TopologyTransitionPostFxProfile,
                usesDirectCameraPath,
                usesHierarchyCinemachinePath);
        }

        private static GameplayCameraSettings ResolveConfiguredCameraSettings(
            GameplayCameraRig cameraRig,
            GameplaySceneHostConfiguration configuration,
            Transform viewCameraTarget)
        {
            var configuredCameraSettings = configuration.CameraSettings ?? GameplayCameraSettings.CreateRuntimeDefault();
            if (viewCameraTarget == null)
            {
                return configuredCameraSettings.Clone();
            }

            return cameraRig.ResolveConfiguredSettings(
                configuredCameraSettings,
                configuration.CameraBaselineAuthoringPolicy,
                viewCameraTarget.position,
                configuration.InitialTopology,
                configuration.TopologyRotationVisualMapping);
        }

        private static Camera ResolveViewCamera(GameplaySceneHostConfiguration configuration)
        {
            return configuration.ViewCamera ?? (configuration.SnapViewCameraToTarget ? Camera.main : null);
        }

        private static Camera ResolveOutputCamera(GameplaySceneHostConfiguration configuration)
        {
            return configuration.ViewCamera ?? Camera.main;
        }

        private static CinemachineBrain ResolveOutputCameraBrain(Camera outputCamera)
        {
            return outputCamera != null
                ? outputCamera.GetComponent<CinemachineBrain>()
                : null;
        }
    }
}
