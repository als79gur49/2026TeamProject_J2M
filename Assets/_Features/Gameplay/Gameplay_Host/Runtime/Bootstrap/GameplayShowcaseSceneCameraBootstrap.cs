using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayShowcaseSceneCameraBootstrap
    {
        internal static void Bootstrap(
            GameObject installerRoot,
            GameplayBoardRoot boardRoot,
            GameplayCameraSettings cameraSettings,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile)
        {
            if (installerRoot == null)
            {
                throw new ArgumentNullException(nameof(installerRoot));
            }

            var resolvedCameraSettings = cameraSettings ?? GameplayCameraSettings.CreateShowcaseDefault();
            var rig = installerRoot.GetComponent<GameplayCameraRig>() ?? installerRoot.AddComponent<GameplayCameraRig>();
            rig.ApplySettings(resolvedCameraSettings);
            rig.ConfigureTopologyTransitionCameraShake(
                topologyTransitionCameraShakeProfile ?? TopologyTransitionCameraShakeProfile.CreateDefault());

            ConfigureSceneOutputCameras(installerRoot.scene);
            ConfigureSceneCinemachinePath(installerRoot.scene, boardRoot, rig, resolvedCameraSettings);
        }

        private static void ConfigureSceneCinemachinePath(
            Scene scene,
            GameplayBoardRoot boardRoot,
            GameplayCameraRig rig,
            GameplayCameraSettings cameraSettings)
        {
            if (!scene.IsValid() || boardRoot == null)
            {
                return;
            }

            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null)
                {
                    continue;
                }

                var cinemachineCameras = rootObject.GetComponentsInChildren<CinemachineCamera>(includeInactive: true);
                for (var j = 0; j < cinemachineCameras.Length; j++)
                {
                    ConfigureCinemachineCamera(cinemachineCameras[j], boardRoot, rig, cameraSettings);
                }

                var brains = rootObject.GetComponentsInChildren<CinemachineBrain>(includeInactive: true);
                for (var j = 0; j < brains.Length; j++)
                {
                    brains[j].DefaultBlend =
                        new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                }
            }
        }

        private static void ConfigureSceneOutputCameras(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null)
                {
                    continue;
                }

                var cameras = rootObject.GetComponentsInChildren<Camera>(includeInactive: true);
                for (var j = 0; j < cameras.Length; j++)
                {
                    var camera = cameras[j];
                    if (camera == null ||
                        (!camera.CompareTag("MainCamera") &&
                         camera.GetComponent<CinemachineBrain>() == null))
                    {
                        continue;
                    }

                    camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                }
            }
        }

        private static void ConfigureCinemachineCamera(
            CinemachineCamera cinemachineCamera,
            GameplayBoardRoot boardRoot,
            GameplayCameraRig rig,
            GameplayCameraSettings cameraSettings)
        {
            if (cinemachineCamera == null ||
                boardRoot?.CameraEffectsRoot == null ||
                boardRoot.CameraTargetRoot == null)
            {
                return;
            }

            var cameraTransform = cinemachineCamera.transform;
            var lens = cinemachineCamera.Lens;
            rig?.CaptureAuthoredSceneCameraPose(
                cameraTransform,
                lens.FieldOfView,
                lens.NearClipPlane,
                lens.FarClipPlane);
            cameraTransform.SetParent(boardRoot.CameraEffectsRoot, worldPositionStays: false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.localScale = Vector3.one;

            cinemachineCamera.Target = new CameraTarget
            {
                TrackingTarget = boardRoot.CameraTargetRoot,
                LookAtTarget = boardRoot.CameraTargetRoot,
                CustomLookAtTarget = true,
            };

            if (!cameraSettings.UseAuthoredSceneCameraLens)
            {
                lens.FieldOfView = cameraSettings.PerspectiveFieldOfView;
                lens.NearClipPlane = cameraSettings.NearClipPlane;
                lens.FarClipPlane = cameraSettings.FarClipPlane;
                cinemachineCamera.Lens = lens;
            }

            cinemachineCamera.BlendHint = 0;
        }
    }
}
