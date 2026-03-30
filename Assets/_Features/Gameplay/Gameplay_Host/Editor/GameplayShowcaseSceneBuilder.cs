using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    public static class GameplayShowcaseSceneBuilder
    {
        private const string ActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string TraversalScenePath = "Assets/Scenes/CubeSurfaceTraversalShowcase.unity";
        private const string BoxScenePath = "Assets/Scenes/BoxInteractionShowcase.unity";
        private const string CombinedScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";

        [MenuItem("Tools/Gameplay/Build Showcase Scenes")]
        public static void BuildAllScenes()
        {
            BuildTraversalScene();
            BuildBoxInteractionScene();
            BuildCombinedGameplayScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildTraversalScene()
        {
            BuildScene<CubeSurfaceTraversalShowcaseInstaller>(
                TraversalScenePath,
                "Cube Surface Traversal Showcase");
        }

        public static void BuildBoxInteractionScene()
        {
            BuildScene<BoxInteractionShowcaseInstaller>(
                BoxScenePath,
                "Box Interaction Showcase");
        }

        public static void BuildCombinedGameplayScene()
        {
            BuildScene<CombinedGameplayShowcaseInstaller>(
                CombinedScenePath,
                "Combined Gameplay Showcase");
        }

        private static void BuildScene<TInstaller>(
            string scenePath,
            string rootObjectName)
            where TInstaller : GameplayShowcaseSceneInstallerBase
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameplayShowcaseSceneScaffold.ConfigureDefaultSceneCamera(Camera.main);

            var installerObject = new GameObject(rootObjectName);
            var installer = installerObject.AddComponent<TInstaller>();
            AssignActions(installer);
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(installerObject, installer.GetShowcaseOverlayContent());

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void AssignActions(Component installer)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
            if (actions == null)
            {
                throw new InvalidOperationException($"Missing InputActionAsset at '{ActionsAssetPath}'.");
            }

            var serializedObject = new SerializedObject(installer);
            serializedObject.FindProperty("actions").objectReferenceValue = actions;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
