using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Host
{
    public static class GameplayShowcaseSceneScaffold
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";
        private const string LegacyLabelRootPrefix = "Label_";

        public static void EnsureInstallerScaffold(GameObject installerRoot)
        {
            EnsureInstallerScaffold(
                installerRoot,
                GameplayCameraSettings.CreateShowcaseDefault(),
                GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault(),
                TopologyTransitionCameraShakeProfile.CreateDefault());
        }

        public static void EnsureInstallerScaffold(
            GameObject installerRoot,
            GameplayCameraSettings cameraSettings)
        {
            EnsureInstallerScaffold(
                installerRoot,
                cameraSettings,
                GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault(),
                TopologyTransitionCameraShakeProfile.CreateDefault());
        }

        public static void EnsureInstallerScaffold(
            GameObject installerRoot,
            GameplayCameraSettings cameraSettings,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile)
        {
            EnsureInstallerScaffold(
                installerRoot,
                cameraSettings,
                GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault(),
                topologyTransitionCameraShakeProfile);
        }

        public static void EnsureInstallerScaffold(
            GameObject installerRoot,
            GameplayCameraSettings cameraSettings,
            GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy)
        {
            EnsureInstallerScaffold(
                installerRoot,
                cameraSettings,
                baselineAuthoringPolicy,
                TopologyTransitionCameraShakeProfile.CreateDefault());
        }

        public static void EnsureInstallerScaffold(
            GameObject installerRoot,
            GameplayCameraSettings cameraSettings,
            GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy,
            TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile)
        {
            if (installerRoot == null)
            {
                throw new ArgumentNullException(nameof(installerRoot));
            }

            DestroyLegacyWorldLabels(installerRoot.scene);
            var boardRoot = EnsureBoardRoot(installerRoot.transform);
            GameplayShowcaseSceneCameraBootstrap.Bootstrap(installerRoot, boardRoot);
        }

        public static void ConfigureDefaultSceneCamera(Camera camera)
        {
            ConfigureDefaultSceneCamera(camera, GameplayCameraSettings.CreateShowcaseDefault());
        }

        public static void ConfigureDefaultSceneCamera(Camera camera, GameplayCameraSettings cameraSettings)
        {
            ConfigureDefaultSceneCamera(camera, cameraSettings, Vector3.zero, default);
        }

        public static void ConfigureDefaultSceneCamera(
            Camera camera,
            GameplayCameraSettings cameraSettings,
            Vector3 targetPosition,
            Bounds visibleCubeBounds)
        {
            if (camera == null)
            {
                return;
            }

            // This stays a scene utility wrapper only; bootstrap and runtime wiring live elsewhere.
            var resolvedCameraSettings = cameraSettings ?? GameplayCameraSettings.CreateShowcaseDefault();
            GameplayCameraRig.ApplySettingsToCamera(
                camera,
                resolvedCameraSettings,
                targetPosition,
                visibleCubeBounds);
        }

        private static GameplayBoardRoot EnsureBoardRoot(Transform installerRoot)
        {
            var boardRoot = FindBoardRoot(installerRoot);
            if (boardRoot == null)
            {
                var boardRootObject = new GameObject(BoardRootObjectName);
                boardRootObject.transform.SetParent(installerRoot, worldPositionStays: false);
                boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
            }
            else
            {
                boardRoot.transform.SetParent(installerRoot, worldPositionStays: false);
                boardRoot.gameObject.name = BoardRootObjectName;
            }

            boardRoot.EnsureHierarchy();
            return boardRoot;
        }

        private static GameplayBoardRoot FindBoardRoot(Transform installerRoot)
        {
            for (var i = 0; i < installerRoot.childCount; i++)
            {
                var child = installerRoot.GetChild(i);
                if (child.TryGetComponent<GameplayBoardRoot>(out var boardRoot))
                {
                    return boardRoot;
                }
            }

            var namedChild = installerRoot.Find(BoardRootObjectName);
            if (namedChild == null)
            {
                return null;
            }

            return namedChild.GetComponent<GameplayBoardRoot>() ??
                   namedChild.gameObject.AddComponent<GameplayBoardRoot>();
        }

        private static void DestroyLegacyWorldLabels(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null ||
                    !rootObject.name.StartsWith(LegacyLabelRootPrefix, StringComparison.Ordinal) ||
                    rootObject.GetComponentInChildren<TextMesh>(includeInactive: true) == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(rootObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(rootObject);
                }
            }
        }
    }
}
