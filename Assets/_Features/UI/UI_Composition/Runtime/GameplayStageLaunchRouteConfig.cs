using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "GameplayStageLaunchRouteConfig",
        menuName = "Game/UI/Gameplay Stage Launch Route Config")]
    public sealed class GameplayStageLaunchRouteConfig : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset _mainMenuSceneAsset;
        [SerializeField] private SceneAsset _gameplayShellSceneAsset;
#endif
        [SerializeField] private string _mainMenuScenePath;
        [SerializeField] private string _mainMenuSceneName;
        [SerializeField] private string _gameplayShellScenePath;
        [SerializeField] private string _gameplayShellSceneName;

        public string MainMenuScenePath => _mainMenuScenePath ?? string.Empty;

        public string MainMenuSceneName => _mainMenuSceneName ?? string.Empty;

        public string GameplayShellScenePath => _gameplayShellScenePath ?? string.Empty;

        public string GameplayShellSceneName => _gameplayShellSceneName ?? string.Empty;

        public bool HasValidMainMenuScene =>
            !string.IsNullOrWhiteSpace(MainMenuSceneName) &&
            !string.IsNullOrWhiteSpace(MainMenuScenePath);

        public bool HasValidGameplayShellScene =>
            !string.IsNullOrWhiteSpace(GameplayShellSceneName) &&
            !string.IsNullOrWhiteSpace(GameplayShellScenePath);

        public void SetScenePathsForTests(string mainMenuScenePath, string gameplayShellScenePath)
        {
            _mainMenuScenePath = NormalizeScenePath(mainMenuScenePath);
            _mainMenuSceneName = SceneNameFromPath(_mainMenuScenePath);
            _gameplayShellScenePath = NormalizeScenePath(gameplayShellScenePath);
            _gameplayShellSceneName = SceneNameFromPath(_gameplayShellScenePath);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            SyncSceneAssetPaths();
#endif
        }

#if UNITY_EDITOR
        private void SyncSceneAssetPaths()
        {
            SyncSceneAssetPath(_mainMenuSceneAsset, ref _mainMenuScenePath, ref _mainMenuSceneName);
            SyncSceneAssetPath(_gameplayShellSceneAsset, ref _gameplayShellScenePath, ref _gameplayShellSceneName);
        }

        private static void SyncSceneAssetPath(
            SceneAsset sceneAsset,
            ref string scenePath,
            ref string sceneName)
        {
            if (sceneAsset == null)
            {
                return;
            }

            scenePath = NormalizeScenePath(AssetDatabase.GetAssetPath(sceneAsset));
            sceneName = SceneNameFromPath(scenePath);
        }
#endif

        private static string NormalizeScenePath(string scenePath)
        {
            return (scenePath ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string SceneNameFromPath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(scenePath);
        }
    }
}
