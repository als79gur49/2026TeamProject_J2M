using System;
using Game.Feature.Flow.Audio;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Shared.Audio;
using Game.Shared.Display;
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
        private const string GameplayAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_UI-Audio_Test.asset";
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";
        private const string HudPrefabPath = "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";
        private const string ScreenCatalogPath =
            "Assets/_Features/UI/UI_Screens/Prefabs/GameplayScreenPrefabCatalog.asset";
        private const string PopupCatalogPath =
            "Assets/_Features/UI/UI_Popups/Prefabs/GameplayPopupPrefabCatalog.asset";

        [MenuItem("Tools/Gameplay/Build Combined Gameplay Showcase Scene")]
        public static void BuildScene()
        {
            BuildScene<CombinedGameplayShowcaseInstaller>(
                CombinedScenePath,
                "Box Slide Test Scene",
                CombinedStageAssetPath,
                DefaultSimulationTimingPresetAssetPath,
                DefaultPresentationTimingPresetAssetPath,
                gameplayAudioMapAssetPath: GameplayAudioMapAssetPath);
        }

        private static void BuildScene<TInstaller>(
            string scenePath,
            string rootObjectName,
            string stageAssetPath,
            string simulationTimingPresetAssetPath,
            string presentationTimingPresetAssetPath,
            string gameplayAudioMapAssetPath)
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
            AssignOptionalObjectReference(
                installer,
                "gameplayAudioMap",
                gameplayAudioMapAssetPath);
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                installerObject,
                installer.GetCameraSettings(),
                installer.GetTopologyTransitionCameraShakeProfile());
            EnsureCanonicalBootstrapRuntime(installerObject);
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

        private static void AssignOptionalObjectReference(
            Component installer,
            string fieldName,
            string assetPath)
        {
            var serializedObject = new SerializedObject(installer);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Installer '{installer.GetType().Name}' does not expose a serialized {fieldName} field.");
            }

            property.objectReferenceValue = string.IsNullOrWhiteSpace(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            if (!string.IsNullOrWhiteSpace(assetPath) && property.objectReferenceValue == null)
            {
                throw new InvalidOperationException($"Missing asset at '{assetPath}'.");
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureCanonicalBootstrapRuntime(GameObject installerObject)
        {
            var sceneHost = installerObject.GetComponent<GameplaySceneHost>() ??
                            installerObject.AddComponent<GameplaySceneHost>();
            var audioRuntimeInstaller = installerObject.GetComponent<AudioRuntimeInstaller>() ??
                                        installerObject.AddComponent<AudioRuntimeInstaller>();
            ConfigureAudioRuntimeInstaller(audioRuntimeInstaller);

            var displayRuntimeInstaller = installerObject.GetComponent<DisplayRuntimeInstaller>() ??
                                          installerObject.AddComponent<DisplayRuntimeInstaller>();
            ConfigureDisplayRuntimeInstaller(displayRuntimeInstaller);

            var bgmBootstrap = installerObject.GetComponent<GlobalAudioFlowBootstrap>() ??
                               installerObject.AddComponent<GlobalAudioFlowBootstrap>();
            AssignSceneObjectReference(bgmBootstrap, "audioRuntimeInstaller", audioRuntimeInstaller);
            AssignSceneObjectReference(bgmBootstrap, "persistentRoot", null);

            var uiFlowInstaller = installerObject.GetComponent<GameplayUiFlowInstaller>() ??
                                  installerObject.AddComponent<GameplayUiFlowInstaller>();
            AssignSceneObjectReference(uiFlowInstaller, "_sceneHost", sceneHost);
            AssignSceneObjectReference(uiFlowInstaller, "_rootView", null);
            AssignHudPrefabReference(uiFlowInstaller);
            AssignObjectReference(uiFlowInstaller, "_screenPrefabCatalog", ScreenCatalogPath);
            AssignObjectReference(uiFlowInstaller, "_popupPrefabCatalog", PopupCatalogPath);
            AssignBool(uiFlowInstaller, "_installOnStart", true);
        }

        private static void ConfigureAudioRuntimeInstaller(AudioRuntimeInstaller installer)
        {
            AssignBool(installer, "installOnAwake", true);
            AssignEnum(installer, "bindingMode", (int)AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime);
            AssignSceneObjectReference(installer, "runtimeRoot", null);
        }

        private static void ConfigureDisplayRuntimeInstaller(DisplayRuntimeInstaller installer)
        {
            AssignBool(installer, "installOnAwake", true);
        }

        private static void AssignHudPrefabReference(Component installer)
        {
            var hudPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (hudPrefabRoot == null)
            {
                throw new InvalidOperationException($"Missing HUD prefab at '{HudPrefabPath}'.");
            }

            var hudRootView = hudPrefabRoot.GetComponent<HUDRootView>();
            if (hudRootView == null)
            {
                throw new InvalidOperationException($"HUD prefab at '{HudPrefabPath}' is missing {nameof(HUDRootView)}.");
            }

            AssignSceneObjectReference(installer, "_hudPrefab", hudRootView);
        }

        private static void AssignSceneObjectReference(
            Component component,
            string fieldName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Component '{component.GetType().Name}' does not expose a serialized {fieldName} field.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBool(Component component, string fieldName, bool value)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Component '{component.GetType().Name}' does not expose a serialized {fieldName} field.");
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignEnum(Component component, string fieldName, int value)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Component '{component.GetType().Name}' does not expose a serialized {fieldName} field.");
            }

            property.enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
