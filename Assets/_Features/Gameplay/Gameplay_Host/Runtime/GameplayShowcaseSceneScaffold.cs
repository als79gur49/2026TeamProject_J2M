using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Host
{
    public static class GameplayShowcaseSceneScaffold
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";
        private const string LegacyLabelRootPrefix = "Label_";

        public static void EnsureInstallerScaffold(GameObject installerRoot, GameplayShowcaseOverlayContent overlayContent)
        {
            if (installerRoot == null)
            {
                throw new ArgumentNullException(nameof(installerRoot));
            }

            DestroyLegacyWorldLabels(installerRoot.scene);
            EnsureBoardRoot(installerRoot.transform);
            EnsureCameraRig(installerRoot);
            EnsureOverlay(installerRoot, overlayContent);
        }

        public static void ConfigureDefaultSceneCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.transform.position = new Vector3(0f, 3.5f, 7.5f);
            camera.transform.rotation = Quaternion.Euler(24f, 152f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.93f, 0.95f, 0.98f);
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

        private static GameplayCameraRig EnsureCameraRig(GameObject installerRoot)
        {
            return installerRoot.GetComponent<GameplayCameraRig>() ??
                   installerRoot.AddComponent<GameplayCameraRig>();
        }

        private static GameplayShowcaseOverlay EnsureOverlay(GameObject installerRoot, GameplayShowcaseOverlayContent overlayContent)
        {
            var overlay = installerRoot.GetComponent<GameplayShowcaseOverlay>() ??
                          installerRoot.AddComponent<GameplayShowcaseOverlay>();
            overlay.Configure(overlayContent);
            return overlay;
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
