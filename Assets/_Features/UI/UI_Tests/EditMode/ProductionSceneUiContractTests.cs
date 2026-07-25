using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class ProductionSceneUiContractTests
    {
        private static readonly string[] ProductionScenePaths =
        {
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/UIAudioScene.unity",
        };
        private const string ScreenCatalogPath =
            "Assets/_Features/UI/UI_Screens/Prefabs/GameplayScreenPrefabCatalog.asset";
        private const string TypographyThemePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";

        [Test]
        public void ProductionScenes_ShareSettingsCatalogAndContainNoLegacyFontOrPrefabResidue()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScreenPrefabCatalog>(ScreenCatalogPath);
            var expectedTheme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(TypographyThemePath);
            Assert.That(catalog, Is.Not.Null, ScreenCatalogPath);
            Assert.That(catalog.SettingsPrefab, Is.Not.Null, "Catalog SettingsPrefab");
            Assert.That(catalog.SettingsTypographyTheme, Is.Not.Null, "Catalog SettingsTypographyTheme");
            Assert.That(catalog.SettingsTypographyTheme, Is.SameAs(expectedTheme));

            var mainMenuScene = EditorSceneManager.OpenScene(ProductionScenePaths[0], OpenSceneMode.Single);
            var mainMenuInstaller = FindSceneComponents(mainMenuScene).OfType<MainMenuUiFlowInstaller>().Single();
            var mainMenuSerialized = new SerializedObject(mainMenuInstaller);
            Assert.That(
                mainMenuSerialized.FindProperty("_screenPrefabCatalog").objectReferenceValue,
                Is.SameAs(catalog));

            var gameplayScene = EditorSceneManager.OpenScene(ProductionScenePaths[1], OpenSceneMode.Single);
            var gameplayInstaller = FindSceneComponents(gameplayScene).OfType<GameplayUiFlowInstaller>().Single();
            var gameplaySerialized = new SerializedObject(gameplayInstaller);
            Assert.That(
                gameplaySerialized.FindProperty("_screenPrefabCatalog").objectReferenceValue,
                Is.SameAs(catalog));

            foreach (var scenePath in ProductionScenePaths)
            {
                var yaml = File.ReadAllText(scenePath);
                Assert.That(yaml, Does.Not.Contain("_koreanSettingsFont:"), scenePath);
            }

            Assert.That(
                File.ReadAllText(ProductionScenePaths[0]),
                Does.Not.Contain("_settingsScreenPrefab:"),
                ProductionScenePaths[0]);
        }

        [Test]
        public void AuthoredUiTree_IsAbsentFromProductionScenes()
        {
            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var authoredUiComponents = FindSceneComponents(scene)
                    .Where(IsAuthoredRuntimeUiTreeComponent)
                    .Select(component => $"{scenePath}: {component.GetType().FullName} on {component.gameObject.name}")
                    .OrderBy(value => value)
                    .ToArray();

                Assert.That(authoredUiComponents, Is.Empty);
            }
        }

        [Test]
        public void ProductionScenes_UseRuntimeUiBootstrapOnly()
        {
            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var installers = FindSceneComponents(scene)
                    .Where(component =>
                        component is GameplayUiFlowInstaller ||
                        component is MainMenuUiFlowInstaller)
                    .ToArray();

                Assert.That(installers, Has.Length.EqualTo(1), scenePath);
            }
        }

        [Test]
        public void ProductionScenes_DoNotContainDebugUiResidue()
        {
            var forbiddenNameTokens = new[]
            {
                "Diagnostics",
                "DebugCommands",
                "DiagnosticsOverlay",
                "DiagnosticsLayer",
            };

            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var residue = FindSceneGameObjects(scene)
                    .Where(gameObject => forbiddenNameTokens.Any(token => gameObject.name.Contains(token, StringComparison.Ordinal)))
                    .Select(gameObject => $"{scenePath}: {gameObject.name}")
                    .OrderBy(value => value)
                    .ToArray();

                Assert.That(residue, Is.Empty);
            }
        }

        private static bool IsAuthoredRuntimeUiTreeComponent(Component component)
        {
            return component is Canvas ||
                   component is EventSystem ||
                   component is HUDRootView ||
                   component is IScreenView ||
                   component is IPopupView;
        }

        private static IEnumerable<Component> FindSceneComponents(Scene scene)
        {
            return FindSceneGameObjects(scene)
                .SelectMany(gameObject => gameObject.GetComponents<Component>())
                .Where(component => component != null);
        }

        private static IEnumerable<GameObject> FindSceneGameObjects(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject);
        }
    }
}
