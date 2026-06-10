using System.IO;
using System.Linq;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayShellUiAudioContractTests
    {
        private const string GameplayPresentationAudioConfigAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Authoring/GameplayPresentationAudioConfig_CampaignV1.asset";
        private const string GameplayAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset";
        private const string BlockAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset";
        private const string PlayerLocomotionAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset";
        private const string TopologyAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_TopologyAudio/Maps/TopologyAudioMap_ObjectSounds.asset";
        private const string GravityFieldAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset";
        private const string TileFeatureMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_TileFeature" + "Audio/Maps/TileFeature" + "AudioMap_ObjectSounds.asset";
        private const string StageCatalogProviderAssetPath =
            StageContentPaths.StageCatalogProviderAssetPath;
        private const string UiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        [Test]
        [Category("Extended")]
        public void UiAudioScene_UsesCoLocatedAudioRuntimeInstaller_OnCanonicalBootstrapRoot()
        {
            AssertCanonicalBootstrapScene(
                UiAudioScenePath,
                "UIAudioSceneBootstrapRoot",
                GameplayPresentationAudioConfigAssetPath);
        }

        [Test]
        [Category("Extended")]
        public void UiAudioScene_UsesStageAudioPath_WithoutEnabledSceneDefaultBgmOverride()
        {
            var scene = EditorSceneManager.OpenScene(UiAudioScenePath, OpenSceneMode.Single);

            try
            {
                var rootObjects = scene.GetRootGameObjects();
                var bootstrapRoot = rootObjects.Single(root => root.name == "UIAudioSceneBootstrapRoot");
                var showcaseInstaller = bootstrapRoot.GetComponent<StageBackedGameplaySceneInstaller>();
                var bgmBootstrap = bootstrapRoot.GetComponent<GlobalAudioFlowBootstrap>();
                var requestSource = rootObjects
                    .SelectMany(root => root.GetComponentsInChildren<SceneBgmRequestSource>(true))
                    .Where(source => source.enabled)
                    .SingleOrDefault();

                Assert.That(showcaseInstaller, Is.Not.Null);
                Assert.That(bgmBootstrap, Is.Not.Null);
                Assert.That(requestSource, Is.Null);

                var serializedShowcaseInstaller = new SerializedObject(showcaseInstaller);
                Assert.That(
                    serializedShowcaseInstaller.FindProperty("globalAudioFlowBootstrap").objectReferenceValue,
                    Is.SameAs(bgmBootstrap));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void AssertCanonicalBootstrapScene(
            string scenePath,
            string expectedRootName,
            string expectedGameplayPresentationAudioConfigAssetPath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            try
            {
                var rootObjects = scene.GetRootGameObjects();
                var gameplayBootstrapRoots = rootObjects
                    .Where(root =>
                        root.GetComponent<StageBackedGameplaySceneInstaller>() != null ||
                        root.GetComponent<GameplaySceneHost>() != null ||
                        root.GetComponent<GameplayUiFlowInstaller>() != null)
                    .ToArray();

                Assert.That(gameplayBootstrapRoots, Has.Length.EqualTo(1));

                var bootstrapRoot = gameplayBootstrapRoots[0];
                var showcaseInstaller = bootstrapRoot.GetComponent<StageBackedGameplaySceneInstaller>();
                var bgmBootstrap = bootstrapRoot.GetComponent<GlobalAudioFlowBootstrap>();
                var sceneHost = bootstrapRoot.GetComponent<GameplaySceneHost>();
                var uiInstaller = bootstrapRoot.GetComponent<GameplayUiFlowInstaller>();
                var audioInstaller = bootstrapRoot.GetComponent<AudioRuntimeInstaller>();
                var displayInstaller = bootstrapRoot.GetComponent<DisplayRuntimeInstaller>();

                Assert.That(bootstrapRoot.name, Is.EqualTo(expectedRootName));
                Assert.That(showcaseInstaller, Is.Not.Null);
                Assert.That(bgmBootstrap, Is.Not.Null);
                Assert.That(sceneHost, Is.Not.Null);
                Assert.That(uiInstaller, Is.Not.Null);
                Assert.That(audioInstaller, Is.Not.Null);
                Assert.That(displayInstaller, Is.Not.Null);
                Assert.That(CountComponentsInScene<StageBackedGameplaySceneInstaller>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<GlobalAudioFlowBootstrap>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<GameplaySceneHost>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<GameplayUiFlowInstaller>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<AudioRuntimeInstaller>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<DisplayRuntimeInstaller>(rootObjects), Is.EqualTo(1));

                var serializedInstaller = new SerializedObject(uiInstaller);
                var serializedAudioInstaller = new SerializedObject(audioInstaller);
                var serializedBgmBootstrap = new SerializedObject(bgmBootstrap);
                var serializedShowcaseInstaller = new SerializedObject(showcaseInstaller);
                Assert.That(serializedInstaller.FindProperty("_sceneHost").objectReferenceValue, Is.SameAs(sceneHost));
                Assert.That(serializedInstaller.FindProperty("_rootView").objectReferenceValue, Is.Null);
                Assert.That(serializedInstaller.FindProperty("_hudPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedInstaller.FindProperty("_hudPrefab").objectReferenceValue),
                    Is.EqualTo(UiTestPrefabAssetUtility.HudPrefabPath));
                Assert.That(serializedInstaller.FindProperty("_screenPrefabCatalog").objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedInstaller.FindProperty("_screenPrefabCatalog").objectReferenceValue),
                    Is.EqualTo(UiTestPrefabAssetUtility.ScreenCatalogPath));
                Assert.That(serializedInstaller.FindProperty("_popupPrefabCatalog").objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedInstaller.FindProperty("_popupPrefabCatalog").objectReferenceValue),
                    Is.EqualTo(UiTestPrefabAssetUtility.PopupCatalogPath));
                Assert.That(serializedInstaller.FindProperty("_uiAudioCueMap").objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedInstaller.FindProperty("_uiAudioCueMap").objectReferenceValue),
                    Is.EqualTo(UiTestPrefabAssetUtility.UiAudioCueMapAssetPath));
                Assert.That(serializedInstaller.FindProperty("_installOnStart").boolValue, Is.True);
                Assert.That(
                    serializedAudioInstaller.FindProperty("bindingMode").enumValueIndex,
                    Is.EqualTo((int)AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime));
                Assert.That(
                    serializedBgmBootstrap.FindProperty("audioRuntimeInstaller").objectReferenceValue,
                    Is.SameAs(audioInstaller));
                Assert.That(serializedBgmBootstrap.FindProperty("persistentRoot").objectReferenceValue, Is.Null);
                var serializedGameplayPresentationAudioConfig =
                    serializedShowcaseInstaller.FindProperty("gameplayPresentationAudioConfig");
                Assert.That(serializedGameplayPresentationAudioConfig, Is.Not.Null);
                Assert.That(serializedGameplayPresentationAudioConfig.objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedGameplayPresentationAudioConfig.objectReferenceValue),
                    Is.EqualTo(expectedGameplayPresentationAudioConfigAssetPath));
                Assert.That(serializedShowcaseInstaller.FindProperty("gameplayAudioMap"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("tileFeatureAudioMap"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("topologyAudioMap"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("gravityFieldAudioMap"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("blockAudioMap"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("playerLocomotionAudioMap"), Is.Null);

                var serializedGameplayPresentationAudio =
                    new SerializedObject(serializedGameplayPresentationAudioConfig.objectReferenceValue);
                AssertSerializedReferencePath(
                    serializedGameplayPresentationAudio,
                    "gameplayAudioMap",
                    GameplayAudioMapAssetPath);
                AssertSerializedReferencePath(
                    serializedGameplayPresentationAudio,
                    "blockAudioMap",
                    BlockAudioMapAssetPath);
                AssertSerializedReferencePath(
                    serializedGameplayPresentationAudio,
                    "playerLocomotionAudioMap",
                    PlayerLocomotionAudioMapAssetPath);
                AssertSerializedReferencePath(
                    serializedGameplayPresentationAudio,
                    "topologyAudioMap",
                    TopologyAudioMapAssetPath);
                AssertSerializedReferencePath(
                    serializedGameplayPresentationAudio,
                    "gravityFieldAudioMap",
                    GravityFieldAudioMapAssetPath);
                AssertSerializedReferencePath(
                    serializedGameplayPresentationAudio,
                    "tileFeatureAudioMap",
                    TileFeatureMapAssetPath);
                Assert.That(serializedShowcaseInstaller.FindProperty("stageLoadSourceMode"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("stageContentEntry"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("stageDefinition"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("enemyPresentationCatalog"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("staticEntityPresentationCatalog"), Is.Null);
                Assert.That(serializedShowcaseInstaller.FindProperty("defaultStageId"), Is.Null);
                var stageCatalogProvider = serializedShowcaseInstaller.FindProperty("stageCatalogProvider");
                Assert.That(stageCatalogProvider, Is.Not.Null);
                Assert.That(stageCatalogProvider.objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(stageCatalogProvider.objectReferenceValue),
                    Is.EqualTo(StageCatalogProviderAssetPath));
                var sceneText = ReadSceneText(scenePath);
                Assert.That(sceneText, Does.Not.Contain("defaultStageId:"));
                Assert.That(sceneText, Does.Not.Contain("UiArchitectureDiagnostics"));
                Assert.That(sceneText, Does.Not.Contain("DiagnosticsLayer"));
                Assert.That(sceneText, Does.Not.Contain("UiDiagnostics"));
                Assert.That(sceneText, Does.Not.Contain("4f1df27cab6e4a7a8f6fcf0f86960af1"));
                Assert.That(Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell"), Is.Not.Null);

                AssertSceneContainsNoSerializedComponent<Canvas>(rootObjects);
                AssertSceneContainsNoSerializedComponent<GraphicRaycaster>(rootObjects);
                AssertSceneContainsNoSerializedComponent<EventSystem>(rootObjects);
                AssertSceneContainsNoSerializedComponent<StandaloneInputModule>(rootObjects);
                AssertSceneContainsNoSerializedComponent<GameplayUiCanvasRootView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<HUDRootView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<PlayerStatusView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ScreenLayerView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<PopupLayerView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<GlobalAudioFlowRoot>(rootObjects);
                AssertSceneContainsNoSerializedComponent<SettingsScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<StageResultScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<PausePopupView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ConfirmPopupView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<TooltipPopupView>(rootObjects);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void AssertSceneContainsNoSerializedComponent<T>(GameObject[] rootObjects) where T : Component
        {
            Assert.That(CountComponentsInScene<T>(rootObjects), Is.Zero, typeof(T).Name);
        }

        private static void AssertSerializedReferencePath(
            SerializedObject serializedObject,
            string propertyName,
            string expectedPath)
        {
            var property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            Assert.That(AssetDatabase.GetAssetPath(property.objectReferenceValue), Is.EqualTo(expectedPath));
        }

        private static int CountComponentsInScene<T>(GameObject[] rootObjects) where T : Component
        {
            return rootObjects.Sum(root => root.GetComponentsInChildren<T>(true).Length);
        }

        private static string ReadSceneText(string scenePath)
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? string.Empty;
            return File.ReadAllText(Path.Combine(projectRoot, scenePath)).Replace("\r\n", "\n");
        }
    }
}
