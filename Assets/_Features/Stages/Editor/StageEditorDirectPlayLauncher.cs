using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages.Editor
{
    [InitializeOnLoad]
    public static class StageEditorDirectPlayLauncher
    {
        private const string LastScenePathSessionKey = "Game.Feature.Stages.LastEditorDirectPlayScenePath";
        private const string CombinedGameplayShowcaseScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string UiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        static StageEditorDirectPlayLauncher()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/Stages/Direct Play/Launch Current Scene")]
        public static void LaunchCurrentScene()
        {
            var scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException("Open a saved stage-backed scene before using direct play.");
            }

            LaunchScene(scenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Replay Last Stage-Backed Scene")]
        public static void LaunchLastScene()
        {
            var scenePath = SessionState.GetString(LastScenePathSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException("No stage-backed scene has been launched yet in this editor session.");
            }

            LaunchScene(scenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Scenes/Combined Gameplay Showcase")]
        public static void LaunchCombinedGameplayShowcase()
        {
            LaunchScene(CombinedGameplayShowcaseScenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Scenes/Tutorial Scene")]
        public static void LaunchTutorialScene()
        {
            LaunchScene(TutorialScenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Scenes/UI Audio Scene")]
        public static void LaunchUiAudioScene()
        {
            LaunchScene(UiAudioScenePath);
        }

        public static StageId PrimePendingLaunchForScene(string scenePath)
        {
            if (!TryPrimePendingLaunchForScene(scenePath, out var stageId))
            {
                throw new InvalidOperationException(BuildMissingCatalogMessage(scenePath));
            }

            return stageId;
        }

        public static bool TryPrimePendingLaunchForScene(string scenePath, out StageId stageId)
        {
            var catalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (catalog == null || !catalog.TryResolveScenePath(scenePath, out stageId))
            {
                stageId = StageId.None;
                return false;
            }

            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
            RememberLastLaunch(scenePath);
            return true;
        }

        private static void LaunchScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                PrimePendingLaunchForScene(scenePath);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RememberLastLaunch(scenePath);
                EditorApplication.isPlaying = true;
            }
            catch
            {
                StageLaunchContextStore.Clear();
                throw;
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            var scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            var catalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (catalog == null || !catalog.TryResolveScenePath(scenePath, out var expectedStageId))
            {
                return;
            }

            if (StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _))
            {
                return;
            }

            Debug.LogWarning(
                $"Scene '{scenePath}' is stage-backed and requires a canonical StageId launch context. Use Tools/Stages/Direct Play/Launch Current Scene to inject '{expectedStageId.Value}' before entering Play mode.");
        }

        private static void RememberLastLaunch(string scenePath)
        {
            SessionState.SetString(LastScenePathSessionKey, scenePath ?? string.Empty);
        }

        private static string BuildMissingCatalogMessage(string scenePath)
        {
            return
                $"Scene '{scenePath}' is not registered in {nameof(StageEditorDirectPlayCatalog)} at '{StageEditorDirectPlayCatalog.DefaultAssetPath}'.";
        }
    }
}
