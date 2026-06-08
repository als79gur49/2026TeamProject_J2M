using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
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
