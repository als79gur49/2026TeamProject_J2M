using System;
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

                var rig = installerObject.GetComponent<GameplayCameraRig>();
                var expectedCameraSettings = GameplayCameraSettings.CreateShowcaseDefault();
                Assert.That(rig, Is.Not.Null);
                AssertCameraSettings(rig, expectedCameraSettings);
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
                var expectedCameraSettings = GameplayCameraSettings.CreateShowcaseDefault();

                Assert.That(configuration.InitialBoardBounds, Is.EqualTo(boardBounds));
                Assert.That(configuration.CameraSettings, Is.Not.Null);
                AssertCameraSettings(configuration.CameraSettings, expectedCameraSettings);
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
                UnityEngine.Object.DestroyImmediate(actions);
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void SampleSceneInstaller_CreateConfiguration_UsesSampleCameraSettingsPreset()
        {
            var installerObject = new GameObject("SampleSceneInstaller");
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();

            try
            {
                var installer = installerObject.AddComponent<SampleSceneInstaller>();
                SetPrivateField(typeof(SampleSceneInstaller), installer, "actions", actions);

                var createConfiguration = typeof(SampleSceneInstaller).GetMethod(
                    "CreateConfiguration",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                Assert.That(createConfiguration, Is.Not.Null);

                var configuration = (GameplaySceneHostConfiguration)createConfiguration.Invoke(installer, null);
                var expectedCameraSettings = GameplayCameraSettings.CreateSampleDefault();

                Assert.That(configuration.CameraSettings, Is.Not.Null);
                AssertCameraSettings(configuration.CameraSettings, expectedCameraSettings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actions);
                UnityEngine.Object.DestroyImmediate(installerObject);
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

        private static void SetPrivateField(Type type, object target, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void AssertCameraSettings(GameplayCameraRig rig, GameplayCameraSettings expected)
        {
            Assert.That(rig.CurrentDistanceMode, Is.EqualTo(expected.DistanceMode));
            Assert.That(rig.PitchDegrees, Is.EqualTo(expected.PitchDegrees).Within(0.0001f));
            Assert.That(rig.YawDegrees, Is.EqualTo(expected.YawDegrees).Within(0.0001f));
            Assert.That(rig.ManualDistance, Is.EqualTo(expected.ManualDistance).Within(0.0001f));
            Assert.That(rig.FramingPadding, Is.EqualTo(expected.FramingPadding).Within(0.0001f));
            Assert.That(rig.PerspectiveFieldOfView, Is.EqualTo(expected.PerspectiveFieldOfView).Within(0.0001f));
            Assert.That(rig.NearClipPlane, Is.EqualTo(expected.NearClipPlane).Within(0.0001f));
            Assert.That(rig.FarClipPlane, Is.EqualTo(expected.FarClipPlane).Within(0.0001f));
            Assert.That(rig.ClearFlags, Is.EqualTo(expected.ClearFlags));
            Assert.That(rig.BackgroundColor, Is.EqualTo(expected.BackgroundColor));
        }

        private static void AssertCameraSettings(GameplayCameraSettings actual, GameplayCameraSettings expected)
        {
            Assert.That(actual.DistanceMode, Is.EqualTo(expected.DistanceMode));
            Assert.That(actual.PitchDegrees, Is.EqualTo(expected.PitchDegrees).Within(0.0001f));
            Assert.That(actual.YawDegrees, Is.EqualTo(expected.YawDegrees).Within(0.0001f));
            Assert.That(actual.ManualDistance, Is.EqualTo(expected.ManualDistance).Within(0.0001f));
            Assert.That(actual.FramingPadding, Is.EqualTo(expected.FramingPadding).Within(0.0001f));
            Assert.That(actual.PerspectiveFieldOfView, Is.EqualTo(expected.PerspectiveFieldOfView).Within(0.0001f));
            Assert.That(actual.NearClipPlane, Is.EqualTo(expected.NearClipPlane).Within(0.0001f));
            Assert.That(actual.FarClipPlane, Is.EqualTo(expected.FarClipPlane).Within(0.0001f));
            Assert.That(actual.ClearFlags, Is.EqualTo(expected.ClearFlags));
            Assert.That(actual.BackgroundColor, Is.EqualTo(expected.BackgroundColor));
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
