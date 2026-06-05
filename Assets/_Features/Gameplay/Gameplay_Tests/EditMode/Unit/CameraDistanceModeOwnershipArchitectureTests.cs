using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CameraDistanceModeOwnershipArchitectureTests
    {
        private const string CameraDistanceModeRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Contracts/CameraDistanceMode.cs";
        private const string GameplayCameraRigRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs";
        private const string GameplayCameraSettingsRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraSettings.cs";
        private const string GuardrailRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/CameraDistanceModeOwnershipArchitectureTests.cs";
        private const string AuthoringCameraDistanceModePropertyPath = "inlineSharedTuning.cameraSettings.DistanceMode";
        private const string GameplayCameraTopologyAuthoringMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.GameplayCameraTopologyAuthoring";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/UIAudioScene.unity",
        };

        [Test]
        [Category("Extended")]
        public void CameraDistanceModeContractFile_Exists_AsStandaloneDeclaration()
        {
            var contractSource = ReadRepoFile(CameraDistanceModeRelativePath);

            Assert.That(contractSource, Does.Contain("public enum CameraDistanceMode"));
        }

        [Test]
        [Category("Extended")]
        public void CameraDistanceMode_EnumValues_RemainUnchanged()
        {
            Assert.That((int)CameraDistanceMode.AutoFit, Is.EqualTo(0));
            Assert.That((int)CameraDistanceMode.Manual, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForCameraDistanceMode()
        {
            var runtimeSource = ReadCombinedSource("Assets/_Features/Gameplay/Gameplay_Host/Runtime");

            Assert.That(CountMatches(runtimeSource, @"\benum\s+CameraDistanceMode\b"), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_DoesNotReintroduce_NestedDistanceModeDeclaration()
        {
            var rigSource = ReadRepoFile(GameplayCameraRigRelativePath);

            Assert.That(rigSource, Does.Not.Contain("enum DistanceMode"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraSettings_DoesNotReference_GameplayCameraRigDistanceMode()
        {
            var settingsSource = ReadRepoFile(GameplayCameraSettingsRelativePath);

            Assert.That(settingsSource, Does.Not.Contain("GameplayCameraRig.DistanceMode"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_CurrentDistanceMode_ReturnType_IsCameraDistanceMode()
        {
            var property = typeof(GameplayCameraRig).GetProperty(
                nameof(GameplayCameraRig.CurrentDistanceMode),
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(property, Is.Not.Null);
            Assert.That(property?.PropertyType, Is.EqualTo(typeof(CameraDistanceMode)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayRuntimeAndTests_DoNotContainLegacyNestedEnumPatterns()
        {
            var gameplaySource = string.Join(
                "\n",
                Directory.GetFiles(GetAbsolutePath("Assets/_Features/Gameplay"), "*.cs", SearchOption.AllDirectories)
                    .Where(path => !Path.GetFullPath(path).Equals(GetAbsolutePath(GuardrailRelativePath)))
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));

            Assert.That(gameplaySource, Does.Not.Contain("GameplayCameraRig.DistanceMode"));
            Assert.That(gameplaySource, Does.Not.Contain("typeof(GameplayCameraRig.DistanceMode)"));
            Assert.That(gameplaySource, Does.Not.Contain("nameof(GameplayCameraRig.DistanceMode)"));
            Assert.That(gameplaySource, Does.Not.Contain("GameplayCameraRig+DistanceMode"));
        }

        [Test]
        [Category("Full")]
        public void GameplayShellScene_PreservesSerializedCameraDistanceModeMeaning()
        {
            foreach (var scenePath in ScenePaths)
            {
                var authoringBlock = ReadCombinedGameplayCameraTopologyAuthoringBlock(scenePath);
                Assert.That(
                    ReadSerializedDistanceModeValue(authoringBlock),
                    Is.EqualTo((int)CameraDistanceMode.Manual),
                    $"Unexpected authored DistanceMode in {scenePath}.");

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                try
                {
                    var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                    Assert.That(installer, Is.Not.Null, $"Missing installer in {scene.path}.");
                    var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
                    Assert.That(authoring, Is.Not.Null, $"Missing {nameof(GameplayCameraTopologyAuthoring)} in {scene.path}.");

                    var serializedObject = new SerializedObject(authoring);
                    var distanceModeProperty = serializedObject.FindProperty(AuthoringCameraDistanceModePropertyPath);

                    Assert.That(distanceModeProperty, Is.Not.Null, $"Missing serialized path in {scene.path}.");
                    serializedObject.Update();
                    Assert.That(distanceModeProperty.intValue, Is.EqualTo((int)CameraDistanceMode.Manual));
                    Assert.That(installer.GetCameraSettings().DistanceMode, Is.EqualTo(CameraDistanceMode.Manual));
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        [Test]
        [Category("Full")]
        public void ShowcaseInstallerSerializedDistanceMode_Path_RetainsSameFieldSlotAcrossOwnershipCleanup()
        {
            EditorSceneManager.OpenScene(ScenePaths[0], OpenSceneMode.Single);

            try
            {
                var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                Assert.That(installer, Is.Not.Null);
                var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
                Assert.That(authoring, Is.Not.Null);

                var serializedObject = new SerializedObject(authoring);
                var sourceModeProperty = serializedObject.FindProperty("sourceMode");
                var distanceModeProperty = serializedObject.FindProperty(AuthoringCameraDistanceModePropertyPath);

                Assert.That(sourceModeProperty, Is.Not.Null);
                Assert.That(distanceModeProperty, Is.Not.Null);
                serializedObject.Update();

                var originalSourceMode = sourceModeProperty.enumValueIndex;
                var originalValue = distanceModeProperty.intValue;
                Assert.That(originalValue, Is.EqualTo((int)CameraDistanceMode.Manual));

                sourceModeProperty.enumValueIndex = (int)GameplayCameraTopologySourceMode.Inline;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                distanceModeProperty.intValue = (int)CameraDistanceMode.AutoFit;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(installer.GetCameraSettings().DistanceMode, Is.EqualTo(CameraDistanceMode.AutoFit));

                serializedObject.Update();
                distanceModeProperty.intValue = (int)CameraDistanceMode.Manual;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(installer.GetCameraSettings().DistanceMode, Is.EqualTo(CameraDistanceMode.Manual));

                serializedObject.Update();
                distanceModeProperty.intValue = originalValue;
                sourceModeProperty.enumValueIndex = originalSourceMode;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static string ReadCombinedGameplayCameraTopologyAuthoringBlock(string scenePath)
        {
            var sceneText = ReadRepoFile(scenePath);
            var markerIndex = sceneText.IndexOf(GameplayCameraTopologyAuthoringMarker, StringComparison.Ordinal);

            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0), $"Missing installer block in {scenePath}.");

            var blockStart = sceneText.LastIndexOf("--- !u!114", markerIndex, StringComparison.Ordinal);
            Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), $"Missing block start in {scenePath}.");

            var blockEnd = sceneText.IndexOf("\n--- !u!", markerIndex, StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                blockEnd = sceneText.Length;
            }

            return sceneText.Substring(blockStart, blockEnd - blockStart);
        }

        private static int ReadSerializedDistanceModeValue(string installerBlock)
        {
            var match = Regex.Match(
                installerBlock,
                @"(?m)^\s+DistanceMode:\s*(\d+)\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, "DistanceMode key was not found in installer block.");
            return int.Parse(match.Groups[1].Value);
        }

        private static int CountMatches(string source, string pattern)
        {
            return Regex.Matches(source, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
        }

        private static string ReadCombinedSource(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(GetAbsolutePath(relativeDirectory), "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
