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
        private const string TraversalStageAssetPath =
            "Assets/_Features/Stages/Stage_CubeSurfaceTraversalShowcase/Stage_CubeSurfaceTraversalShowcase.asset";
        private const string BoxStageAssetPath =
            "Assets/_Features/Stages/Stage_BoxInteractionShowcase/Stage_BoxInteractionShowcase.asset";
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset";

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
                "Cube Surface Traversal Showcase",
                TraversalStageAssetPath);
        }

        public static void BuildBoxInteractionScene()
        {
            BuildScene<BoxInteractionShowcaseInstaller>(
                BoxScenePath,
                "Box Interaction Showcase",
                BoxStageAssetPath);
        }

        public static void BuildCombinedGameplayScene()
        {
            BuildScene<CombinedGameplayShowcaseInstaller>(
                CombinedScenePath,
                "Box Slide Test Scene",
                CombinedStageAssetPath);
        }

        private static void BuildScene<TInstaller>(
            string scenePath,
            string rootObjectName,
            string stageAssetPath)
            where TInstaller : GameplayShowcaseSceneInstallerBase
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var installerObject = new GameObject(rootObjectName);
            var installer = installerObject.AddComponent<TInstaller>();
            AssignActions(installer);
            AssignStageDefinition(installer, stageAssetPath);
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                installerObject,
                installer.GetShowcaseOverlayContent(),
                installer.GetCameraSettings());
            installer.ConfigureBootstrapCamera(Camera.main);

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

        private static void AssignStageDefinition(Component installer, string stageAssetPath)
        {
            var stageAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(stageAssetPath);
            if (stageAsset == null)
            {
                throw new InvalidOperationException($"Missing stage asset at '{stageAssetPath}'.");
            }

            var serializedObject = new SerializedObject(installer);
            var stageDefinitionProperty = serializedObject.FindProperty("stageDefinition");
            if (stageDefinitionProperty == null)
            {
                throw new InvalidOperationException(
                    $"Installer '{installer.GetType().Name}' does not expose a serialized stageDefinition field.");
            }

            stageDefinitionProperty.objectReferenceValue = stageAsset;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
