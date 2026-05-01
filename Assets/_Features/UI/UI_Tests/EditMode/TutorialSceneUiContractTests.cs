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
    public sealed class TutorialSceneUiContractTests
    {
        private const string GameplayAudioMapAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_UI-Audio_Test.asset";
        private const string StageCatalogProviderAssetPath =
            "Assets/_Features/Stages/Content/StageCatalogProvider.asset";
        private const string CombinedScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string UiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        [Test]
        [Category("Extended")]
        public void TutorialScene_UsesSingleCanonicalBootstrapPath_WithoutSerializedUiResidue()
        {
            AssertCanonicalBootstrapScene(
                TutorialScenePath,
                "TutorialSceneBootstrapRoot",
                GameplayAudioMapAssetPath);
        }

        [Test]
        [Category("Extended")]
        public void UiAudioScene_UsesCoLocatedAudioRuntimeInstaller_OnCanonicalBootstrapRoot()
        {
            AssertCanonicalBootstrapScene(
                UiAudioScenePath,
                "UIAudioSceneBootstrapRoot",
                GameplayAudioMapAssetPath);
        }

        [Test]
        [Category("Extended")]
        public void CombinedGameplayShowcaseScene_UsesCoLocatedUiAudioDisplayBootstrap_OnCanonicalBootstrapRoot()
        {
            AssertCanonicalBootstrapScene(
                CombinedScenePath,
                "Box Slide Test Scene",
                GameplayAudioMapAssetPath);
        }

        private static void AssertCanonicalBootstrapScene(
            string scenePath,
            string expectedRootName,
            string expectedGameplayAudioMapAssetPath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            try
            {
                var rootObjects = scene.GetRootGameObjects();
                var gameplayBootstrapRoots = rootObjects
                    .Where(root =>
                        root.GetComponent<CombinedGameplayShowcaseInstaller>() != null ||
                        root.GetComponent<GameplaySceneHost>() != null ||
                        root.GetComponent<GameplayUiFlowInstaller>() != null)
                    .ToArray();

                Assert.That(gameplayBootstrapRoots, Has.Length.EqualTo(1));

                var bootstrapRoot = gameplayBootstrapRoots[0];
                var showcaseInstaller = bootstrapRoot.GetComponent<CombinedGameplayShowcaseInstaller>();
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
                Assert.That(CountComponentsInScene<CombinedGameplayShowcaseInstaller>(rootObjects), Is.EqualTo(1));
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
                var serializedGameplayAudioMap = serializedShowcaseInstaller.FindProperty("gameplayAudioMap");
                Assert.That(serializedGameplayAudioMap, Is.Not.Null);
                Assert.That(serializedGameplayAudioMap.objectReferenceValue, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedGameplayAudioMap.objectReferenceValue),
                    Is.EqualTo(expectedGameplayAudioMapAssetPath));
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
                Assert.That(ReadSceneText(scenePath), Does.Not.Contain("defaultStageId:"));
                Assert.That(Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell"), Is.Not.Null);

                AssertSceneContainsNoSerializedComponent<Canvas>(rootObjects);
                AssertSceneContainsNoSerializedComponent<GraphicRaycaster>(rootObjects);
                AssertSceneContainsNoSerializedComponent<EventSystem>(rootObjects);
                AssertSceneContainsNoSerializedComponent<StandaloneInputModule>(rootObjects);
                AssertSceneContainsNoSerializedComponent<GameplayUiCanvasRootView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<UiArchitectureDiagnosticsOverlayView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<HUDRootView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<PlayerStatusView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ActionBarView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<NotificationView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ScreenLayerView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<PopupLayerView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<GlobalAudioFlowRoot>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ObjectiveStatusScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<SettingsScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<StageResultScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<PausePopupView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ObjectiveInfoPopupView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ConfirmPopupView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<TooltipPopupView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<RewardPopupView>(rootObjects);
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
