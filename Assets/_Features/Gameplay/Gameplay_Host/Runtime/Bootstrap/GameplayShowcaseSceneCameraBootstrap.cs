using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayShowcaseSceneCameraBootstrap
    {
        internal static void Bootstrap(
            GameObject installerRoot,
            GameplayBoardRoot boardRoot)
        {
            if (installerRoot == null)
            {
                throw new ArgumentNullException(nameof(installerRoot));
            }

            var rig = installerRoot.GetComponent<GameplayCameraRig>() ?? installerRoot.AddComponent<GameplayCameraRig>();
            ConfigureSceneCinemachinePath(
                installerRoot.scene,
                boardRoot,
                rig);
        }

        internal static void ApplyResolvedStartupLens(
            Scene scene,
            GameplayResolvedCameraStartupPlan startupPlan)
        {
            if (!scene.IsValid() || !startupPlan.UsesHierarchyCinemachinePath)
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
                    ApplyResolvedStartupLens(cinemachineCameras[j], startupPlan.ResolvedCameraSettings);
                }
            }
        }

        private static void ConfigureSceneCinemachinePath(
            Scene scene,
            GameplayBoardRoot boardRoot,
            GameplayCameraRig rig)
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
                    ConfigureCinemachineCamera(
                        cinemachineCameras[j],
                        boardRoot,
                        rig);
                }

                var brains = rootObject.GetComponentsInChildren<CinemachineBrain>(includeInactive: true);
                for (var j = 0; j < brains.Length; j++)
                {
                    brains[j].DefaultBlend =
                        new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                }
            }
        }

        private static void ConfigureCinemachineCamera(
            CinemachineCamera cinemachineCamera,
            GameplayBoardRoot boardRoot,
            GameplayCameraRig rig)
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

            cinemachineCamera.BlendHint = 0;
        }

        private static void ApplyResolvedStartupLens(
            CinemachineCamera cinemachineCamera,
            GameplayCameraSettings resolvedCameraSettings)
        {
            if (cinemachineCamera == null || resolvedCameraSettings == null)
            {
                return;
            }

            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = resolvedCameraSettings.PerspectiveFieldOfView;
            lens.NearClipPlane = resolvedCameraSettings.NearClipPlane;
            lens.FarClipPlane = resolvedCameraSettings.FarClipPlane;
            cinemachineCamera.Lens = lens;
        }
    }
}
