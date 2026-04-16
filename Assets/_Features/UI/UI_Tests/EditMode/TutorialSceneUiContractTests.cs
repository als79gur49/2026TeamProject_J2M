using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
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
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

        [Test]
        [Category("Extended")]
        public void TutorialScene_UsesSingleCanonicalBootstrapPath_WithoutSerializedUiResidue()
        {
            var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

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
                var sceneHost = bootstrapRoot.GetComponent<GameplaySceneHost>();
                var uiInstaller = bootstrapRoot.GetComponent<GameplayUiFlowInstaller>();

                Assert.That(bootstrapRoot.name, Is.EqualTo("TutorialSceneBootstrapRoot"));
                Assert.That(showcaseInstaller, Is.Not.Null);
                Assert.That(sceneHost, Is.Not.Null);
                Assert.That(uiInstaller, Is.Not.Null);
                Assert.That(CountComponentsInScene<CombinedGameplayShowcaseInstaller>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<GameplaySceneHost>(rootObjects), Is.EqualTo(1));
                Assert.That(CountComponentsInScene<GameplayUiFlowInstaller>(rootObjects), Is.EqualTo(1));

                var serializedInstaller = new SerializedObject(uiInstaller);
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
                Assert.That(serializedInstaller.FindProperty("_installOnStart").boolValue, Is.True);
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
                AssertSceneContainsNoSerializedComponent<GameplayScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<HelpScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<ObjectiveStatusScreenView>(rootObjects);
                AssertSceneContainsNoSerializedComponent<InventoryScreenView>(rootObjects);
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
    }
}
