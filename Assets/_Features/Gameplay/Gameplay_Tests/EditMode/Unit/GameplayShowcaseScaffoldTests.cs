using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.PlayerControl;
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
            var scene = CreateIsolatedTestScene();

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
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayPresentationCleanup_RemovesLegacyGridOriginContracts()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(
                    installer,
                    "playerControlTiming",
                    new PlayerControlTimingSettings
                    {
                        MoveCooldownSeconds = 0.35f,
                    });
                SetBaseInstallerField(installer, "topologyMotionDurationSeconds", 0.45f);
                SetBaseInstallerField(
                    installer,
                    "topologyRotationVisualMapping",
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);

                var boardBounds = TestGameplayShowcaseInstaller.DefaultBoardBounds;
                var configuration = installer.BuildConfigurationForTests();
                var expectedCameraSettings = GameplayCameraSettings.CreateShowcaseDefault();

                Assert.That(configuration.InitialBoardBounds, Is.EqualTo(boardBounds));
                Assert.That(configuration.CameraSettings, Is.Not.Null);
                AssertCameraSettings(configuration.CameraSettings, expectedCameraSettings);
                Assert.That(configuration.PlayerControlTiming, Is.Not.Null);
                Assert.That(configuration.PlayerControlTiming.MoveCooldownSeconds, Is.EqualTo(0.35f));
                Assert.That(configuration.TopologyMotionDurationSeconds, Is.EqualTo(0.45f));
                Assert.That(
                    configuration.TopologyRotationVisualMapping,
                    Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesPositiveX));
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
                ResetIsolatedTestScene();
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

        private static Scene CreateIsolatedTestScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void ResetIsolatedTestScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            internal static readonly BoardBounds DefaultBoardBounds = new(new Vector2Int(0, 0), new Vector2Int(4, 4));

            public GameplaySceneHostConfiguration BuildConfigurationForTests()
            {
                var createConfiguration = typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                    "CreateConfiguration",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(InitialGameplayState), typeof(GameplayCameraSettings) },
                    modifiers: null);

                Assert.That(createConfiguration, Is.Not.Null);
                return (GameplaySceneHostConfiguration)createConfiguration.Invoke(
                    this,
                    new object[] { BuildInitialGameplayState(), GetCameraSettings() });
            }

            protected override InitialGameplayState BuildInitialGameplayState()
            {
                return new InitialGameplayState(
                    DefaultBoardBounds,
                    new CubeTopologyState(FaceId.Floor),
                    Array.Empty<EntityState>(),
                    Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                    playerEntityId: 10,
                    Array.Empty<EnemyAiProfileOverride>());
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

    public sealed class GameplayShowcaseAssetMigrationTests
    {
        private const string CombinedScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string BoxInteractionScenePath = "Assets/Scenes/BoxInteractionShowcase.unity";
        private const string TraversalScenePath = "Assets/Scenes/CubeSurfaceTraversalShowcase.unity";
        private const string PlayerAnimationTestPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Entity_View_PlayerAnimationTest.prefab";

        [Test]
        public void CombinedGameplayShowcaseScene_SerializesPlayerControlTimingInsteadOfLegacyCooldownField()
        {
            var sceneText = ReadNormalizedText(CombinedScenePath);

            StringAssert.Contains("playerControlTiming:\n    MoveCooldownSeconds: 0.5", sceneText);
            StringAssert.Contains("playerMoveCooldownSeconds: -1", sceneText);
            StringAssert.DoesNotContain("playerMoveCooldownSeconds: 0.5", sceneText);
            StringAssert.Contains(
                "stageDefinition: {fileID: 11400000, guid: 768e58af510a487eafd9bf00b45b4ca0, type: 2}",
                sceneText);
        }

        [Test]
        public void BoxInteractionShowcaseScene_SerializesPlayerControlTimingDefaults()
        {
            var sceneText = ReadNormalizedText(BoxInteractionScenePath);

            StringAssert.Contains("playerControlTiming:\n    MoveCooldownSeconds: -1", sceneText);
            StringAssert.Contains("playerMoveCooldownSeconds: -1", sceneText);
            StringAssert.Contains(
                "stageDefinition: {fileID: 11400000, guid: 394a3b219e254dd68f3ea4cc8647f0c2, type: 2}",
                sceneText);
        }

        [Test]
        public void CubeSurfaceTraversalShowcaseScene_DropsLegacyTickSerializedFields()
        {
            var sceneText = ReadNormalizedText(TraversalScenePath);

            StringAssert.DoesNotContain("initialMoveDelayTicks", sceneText);
            StringAssert.DoesNotContain("repeatedMoveIntervalTicks", sceneText);
            StringAssert.DoesNotContain("tickIntervalSeconds", sceneText);
            StringAssert.Contains("initialMoveDelaySeconds: 0", sceneText);
            StringAssert.Contains("repeatedMoveIntervalSeconds: 0.4", sceneText);
            StringAssert.Contains("playerControlTiming:", sceneText);
            StringAssert.Contains(
                "stageDefinition: {fileID: 11400000, guid: a44d479002364aad91f8cd5b3b1e1242, type: 2}",
                sceneText);
        }

        [Test]
        public void PlayerAnimationTestPrefab_UsesPresentationOnlyTimingAuthoringComponents()
        {
            var prefabText = ReadNormalizedText(PlayerAnimationTestPrefabPath);

            StringAssert.Contains("EntityMotionPresentationAuthoring", prefabText);
            StringAssert.Contains("PlayerAnimationTimingAuthoring", prefabText);
            StringAssert.DoesNotContain("PlayerActionTimingAuthoring", prefabText);
            StringAssert.DoesNotContain("pushPresentationDurationSeconds", prefabText);
            StringAssert.DoesNotContain("flipPresentationDurationSeconds", prefabText);
        }

        private static string ReadNormalizedText(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var normalizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(projectRoot, normalizedAssetPath);
            return File.ReadAllText(fullPath).Replace("\r\n", "\n");
        }
    }
}
