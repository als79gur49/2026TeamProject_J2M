using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayShowcaseScaffoldTests
    {
        [Test]
        public void GameplayShowcaseSceneScaffold_EnsureInstallerScaffold_Creates3DScaffoldAndRemovesLegacyLabels()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var legacyLabel = new GameObject("Label_LegacyTraversal");
                legacyLabel.AddComponent<TextMesh>();
                SceneManager.MoveGameObjectToScene(legacyLabel, scene);

                GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                    installerObject,
                    new GameplayShowcaseOverlayContent(
                        "Traversal",
                        "Summary",
                        "Move: WASD",
                        new[] { "First highlight", "Second highlight" }));

                Assert.That(installerObject.GetComponent<GameplayCameraRig>(), Is.Not.Null);
                Assert.That(installerObject.GetComponent<GameplayShowcaseOverlay>(), Is.Not.Null);

                var overlay = installerObject.GetComponent<GameplayShowcaseOverlay>();
                Assert.That(overlay.Title, Is.EqualTo("Traversal"));
                Assert.That(overlay.Summary, Is.EqualTo("Summary"));
                Assert.That(overlay.ControlsText, Is.EqualTo("Move: WASD"));
                Assert.That(overlay.HighlightsText, Does.Contain("- First highlight"));

                var boardRoot = installerObject.GetComponentInChildren<GameplayBoardRoot>();
                Assert.That(boardRoot, Is.Not.Null);
                Assert.That(boardRoot.transform.parent, Is.EqualTo(installerObject.transform));
                Assert.That(boardRoot.BoardSurfaceRoot, Is.Not.Null);
                Assert.That(boardRoot.EntityRoot, Is.Not.Null);
                Assert.That(boardRoot.CameraTargetRoot, Is.Not.Null);

                Assert.That(legacyLabel == null, Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void GameplayPresentationCleanup_RemovesLegacyGridOriginContracts()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);

                var createConfiguration = typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                    "CreateConfiguration",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createConfiguration, Is.Not.Null);

                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4));
                var configuration = (GameplaySceneHostConfiguration)createConfiguration.Invoke(
                    installer,
                    new object[] { boardBounds });

                Assert.That(configuration.InitialBoardBounds, Is.EqualTo(boardBounds));
                Assert.That(
                    typeof(GameplaySceneHostConfiguration).GetField(
                        "GridOrigin",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    Is.Null);
                Assert.That(
                    typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                        "CalculateCenteredGridOrigin",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(actions);
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void SetBaseInstallerField(object target, string fieldName, object value)
        {
            var field = typeof(GameplayShowcaseSceneInstallerBase).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private sealed class TestGameplayShowcaseInstaller : GameplayShowcaseSceneInstallerBase
        {
            protected override BoardBounds CreateBoardBounds()
            {
                return new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
            }

            protected override void PopulateInitialEntities(List<EntityState> entities, BoardBounds boardBounds)
            {
            }

            protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
            {
                return new GameplayShowcaseOverlayContent(
                    "Test Showcase",
                    "Summary",
                    "Move: WASD",
                    new[] { "Highlight" });
            }
        }
    }
}
