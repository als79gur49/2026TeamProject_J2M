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
        private const string CombinedScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset";
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";

        [MenuItem("Tools/Gameplay/Build Combined Gameplay Showcase Scene")]
        public static void BuildScene()
        {
            BuildScene<CombinedGameplayShowcaseInstaller>(
                CombinedScenePath,
                "Box Slide Test Scene",
                CombinedStageAssetPath,
                DefaultSimulationTimingPresetAssetPath,
                DefaultPresentationTimingPresetAssetPath);
        }

        private static void BuildScene<TInstaller>(
            string scenePath,
            string rootObjectName,
            string stageAssetPath,
            string simulationTimingPresetAssetPath,
            string presentationTimingPresetAssetPath)
            where TInstaller : GameplayShowcaseSceneInstallerBase
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var installerObject = new GameObject(rootObjectName);
            var installer = installerObject.AddComponent<TInstaller>();
            AssignActions(installer);
            AssignStageDefinition(installer, stageAssetPath);
            AssignObjectReference(
                installer,
                "simulationTimingPreset",
                simulationTimingPresetAssetPath);
            AssignObjectReference(
                installer,
                "presentationTimingPreset",
                presentationTimingPresetAssetPath);
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

        private static void AssignObjectReference(
            Component installer,
            string fieldName,
            string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Missing asset at '{assetPath}'.");
            }

            var serializedObject = new SerializedObject(installer);
            var presetProperty = serializedObject.FindProperty(fieldName);
            if (presetProperty == null)
            {
                throw new InvalidOperationException(
                    $"Installer '{installer.GetType().Name}' does not expose a serialized {fieldName} field.");
            }

            presetProperty.objectReferenceValue = asset;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
