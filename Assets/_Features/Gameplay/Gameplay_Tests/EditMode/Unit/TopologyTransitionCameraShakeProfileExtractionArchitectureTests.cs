using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public sealed class TopologyTransitionCameraShakeProfileExtractionArchitectureTests
    {
        private const string ProfileRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Profiles/TopologyTransitionCameraShakeProfile.cs";
        private const string LegacyProfileRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyTransitionCameraShakeProfile.cs";
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string RuntimeCameraRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera";
        private const string CameraFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera.meta";
        private const string CameraProfilesFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Profiles.meta";
        private const string ProfileMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Profiles/TopologyTransitionCameraShakeProfile.cs.meta";
        private const string AuthoringPropertyPath = "inlineSharedTuning.topologyTransitionCameraShakeProfile";
        private const string SerializedProfileKey = "topologyTransitionCameraShakeProfile";
        private const string SourceModePropertyPath = "sourceMode";
        private const string PresetPropertyPath = "preset";
        private const string GameplayCameraTopologyAuthoringMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.GameplayCameraTopologyAuthoring";
        private const string ExpectedProfileGuid = "f8a8aa3000874792ae19a20bcbccaa96";

        private static readonly string[] ExpectedSerializedHolders =
        {
            "Assets/Scenes/CombinedGameplayShowcase.unity",
            "Assets/Scenes/TutorialScene.unity",
            "Assets/Scenes/UIAudioScene.unity",
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Camera/Presets/GameplayCameraTopologyPreset_CombinedGameplayShowcase.asset",
            "Assets/_Features/Stages/Stage_TutorialScene/Camera/Presets/GameplayCameraTopologyPreset_TutorialScene.asset",
        };

        private static readonly string[] ExpectedFieldNames =
        {
            nameof(TopologyTransitionCameraShakeProfile.ImpactStart01),
            nameof(TopologyTransitionCameraShakeProfile.ImpactDuration01),
            nameof(TopologyTransitionCameraShakeProfile.ImpactOscillationCycles),
            nameof(TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude),
            nameof(TopologyTransitionCameraShakeProfile.ImpactLocalRotationAmplitudeDegrees),
            nameof(TopologyTransitionCameraShakeProfile.LandingStart01),
            nameof(TopologyTransitionCameraShakeProfile.LandingDuration01),
            nameof(TopologyTransitionCameraShakeProfile.LandingOscillationCycles),
            nameof(TopologyTransitionCameraShakeProfile.LandingLocalPositionAmplitude),
            nameof(TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees),
        };

        private static readonly Dictionary<string, Type> ExpectedFieldTypes = new(StringComparer.Ordinal)
        {
            [nameof(TopologyTransitionCameraShakeProfile.ImpactStart01)] = typeof(float),
            [nameof(TopologyTransitionCameraShakeProfile.ImpactDuration01)] = typeof(float),
            [nameof(TopologyTransitionCameraShakeProfile.ImpactOscillationCycles)] = typeof(int),
            [nameof(TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude)] = typeof(Vector3),
            [nameof(TopologyTransitionCameraShakeProfile.ImpactLocalRotationAmplitudeDegrees)] = typeof(Vector3),
            [nameof(TopologyTransitionCameraShakeProfile.LandingStart01)] = typeof(float),
            [nameof(TopologyTransitionCameraShakeProfile.LandingDuration01)] = typeof(float),
            [nameof(TopologyTransitionCameraShakeProfile.LandingOscillationCycles)] = typeof(int),
            [nameof(TopologyTransitionCameraShakeProfile.LandingLocalPositionAmplitude)] = typeof(Vector3),
            [nameof(TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees)] = typeof(Vector3),
        };

        private static readonly string[] ForbiddenIdentifiers =
        {
            "GameplayCameraRig",
            "GameplayTickViewPresenter",
            "TopologyTransitionVisualState",
            "TopologyTransitionCameraShakeController",
            "GameplayHostRuntimeFactory",
        };

        [Test]
        [Category("Extended")]
        public void CameraShakeProfileFile_Exists_InDedicatedCameraRuntimeProfileLane()
        {
            var profileSource = ReadRepoFile(ProfileRelativePath);

            Assert.That(profileSource, Does.Contain("public sealed class TopologyTransitionCameraShakeProfile"));
            Assert.That(profileSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void CameraRuntimeProfileFolders_Exist_WithTrackedMetaFiles()
        {
            Assert.That(Directory.Exists(GetAbsolutePath(RuntimeCameraRelativeDirectory)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(CameraFolderMetaRelativePath)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(CameraProfilesFolderMetaRelativePath)), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void LegacyTopLevelProfileFile_IsAbsent_AfterExtraction()
        {
            Assert.That(File.Exists(GetAbsolutePath(LegacyProfileRelativePath)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForCameraShakeProfile()
        {
            var runtimeSource = ReadCombinedSource(RuntimeRelativeDirectory);

            Assert.That(
                CountMatches(runtimeSource, @"\bclass\s+TopologyTransitionCameraShakeProfile\b"),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTopLevelFiles_DoNotReintroduce_CameraShakeProfileDeclaration()
        {
            var topLevelFiles = Directory.GetFiles(GetAbsolutePath(RuntimeRelativeDirectory), "*.cs", SearchOption.TopDirectoryOnly);

            foreach (var filePath in topLevelFiles)
            {
                var fileSource = File.ReadAllText(filePath).Replace("\r\n", "\n");
                Assert.That(
                    Regex.IsMatch(
                        fileSource,
                        @"\bclass\s+TopologyTransitionCameraShakeProfile\b",
                        RegexOptions.CultureInvariant),
                    Is.False,
                    $"Unexpected top-level declaration in '{filePath}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfileType_Contract_RemainsUnchanged()
        {
            var profileType = typeof(TopologyTransitionCameraShakeProfile);

            Assert.That(profileType.Namespace, Is.EqualTo("Game.Feature.Gameplay.Host"));
            Assert.That(profileType.IsPublic, Is.True);
            Assert.That(profileType.IsSealed, Is.True);
            Assert.That(profileType.IsDefined(typeof(SerializableAttribute), inherit: false), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfileSource_DeclarationForm_RemainsSerializablePublicSealedClass()
        {
            var profileSource = ReadRepoFile(ProfileRelativePath);

            Assert.That(profileSource, Does.Contain("[Serializable]"));
            Assert.That(
                Regex.IsMatch(
                    profileSource,
                    @"public\s+sealed\s+class\s+TopologyTransitionCameraShakeProfile",
                    RegexOptions.CultureInvariant),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfileFactoryAndCloneSignatures_RemainUnchanged()
        {
            var createDefaultMethod = typeof(TopologyTransitionCameraShakeProfile).GetMethod(
                nameof(TopologyTransitionCameraShakeProfile.CreateDefault),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var cloneMethod = typeof(TopologyTransitionCameraShakeProfile).GetMethod(
                nameof(TopologyTransitionCameraShakeProfile.Clone),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.That(createDefaultMethod, Is.Not.Null);
            Assert.That(createDefaultMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionCameraShakeProfile)));
            Assert.That(createDefaultMethod?.GetParameters(), Is.Empty);

            Assert.That(cloneMethod, Is.Not.Null);
            Assert.That(cloneMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionCameraShakeProfile)));
            Assert.That(cloneMethod?.GetParameters(), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfile_PublicSerializedFields_AndFieldTypes_RemainCanonical()
        {
            var fields = typeof(TopologyTransitionCameraShakeProfile)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEquivalent(ExpectedFieldNames, fields.Select(field => field.Name).ToArray());

            foreach (var field in fields)
            {
                Assert.That(
                    field.FieldType,
                    Is.EqualTo(ExpectedFieldTypes[field.Name]),
                    $"Field '{field.Name}' changed type.");
            }
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfile_DefaultInstanceValues_RemainCanonical()
        {
            var profile = TopologyTransitionCameraShakeProfile.CreateDefault();

            Assert.That(profile.ImpactStart01, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(profile.ImpactDuration01, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(profile.ImpactOscillationCycles, Is.EqualTo(2));
            AssertVector3(profile.ImpactLocalPositionAmplitude, new Vector3(0.012f, 0.008f, 0.018f));
            AssertVector3(profile.ImpactLocalRotationAmplitudeDegrees, new Vector3(0.45f, 0.28f, 0.12f));
            Assert.That(profile.LandingStart01, Is.EqualTo(0.76f).Within(0.0001f));
            Assert.That(profile.LandingDuration01, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(profile.LandingOscillationCycles, Is.EqualTo(1));
            AssertVector3(profile.LandingLocalPositionAmplitude, new Vector3(0.006f, 0.004f, 0.01f));
            AssertVector3(profile.LandingLocalRotationAmplitudeDegrees, new Vector3(0.24f, 0.14f, 0.08f));
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfileMetaGuid_RemainsUnchanged_AfterMove()
        {
            var metaSource = ReadRepoFile(ProfileMetaRelativePath);

            Assert.That(metaSource, Does.Contain($"guid: {ExpectedProfileGuid}"));
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfileSource_DoesNotReferenceForbiddenRuntimeIdentifiers()
        {
            var sanitizedSource = StripCommentsAndStringLiterals(ReadRepoFile(ProfileRelativePath));
            var identifierTokens = GetIdentifierTokens(sanitizedSource);

            foreach (var forbiddenIdentifier in ForbiddenIdentifiers)
            {
                Assert.That(
                    identifierTokens.Contains(forbiddenIdentifier),
                    Is.False,
                    $"Forbidden runtime identifier '{forbiddenIdentifier}' was found in the profile source.");
            }

            Assert.That(
                identifierTokens.Any(identifier => identifier.StartsWith("TopologyTransitionPostFx", StringComparison.Ordinal)),
                Is.False,
                "Camera shake profile must not depend on TopologyTransitionPostFx identifiers.");
        }

        [Test]
        [Category("Extended")]
        public void RepositorySerializedTargets_ForCameraShakeProfile_AreLimitedToKnownCameraTopologyAuthoringAndPresetAssets()
        {
            var assetPaths = Directory.GetFiles(GetAbsolutePath("Assets"), "*.*", SearchOption.AllDirectories)
                .Where(path => HasSerializedAssetExtension(path))
                .Select(ToRepoRelativePath)
                .Where(ContainsCameraShakeProfileHolder)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEquivalent(ExpectedSerializedHolders, assetPaths);
        }

        [Test]
        [Category("Full")]
        public void ShowcaseInstallerScenes_PresetMode_RuntimeMeaning_UsesReferencedPresetCameraShakeProfile()
        {
            foreach (var scenePath in ExpectedSerializedHolders.Where(path => path.EndsWith(".unity", StringComparison.Ordinal)))
            {
                var expectedPresetPath = ResolveExpectedPresetAssetPath(scenePath);
                var expectedProfile = ReadSerializedProfile(ReadRepoFile(expectedPresetPath));

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                try
                {
                    var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                    Assert.That(installer, Is.Not.Null, $"Missing installer in '{scene.path}'.");
                    var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
                    Assert.That(authoring, Is.Not.Null, $"Missing {nameof(GameplayCameraTopologyAuthoring)} in '{scene.path}'.");

                    var serializedObject = new SerializedObject(authoring);
                    var sourceModeProperty = serializedObject.FindProperty(SourceModePropertyPath);
                    var presetProperty = serializedObject.FindProperty(PresetPropertyPath);
                    var profileProperty = serializedObject.FindProperty(AuthoringPropertyPath);

                    Assert.That(sourceModeProperty, Is.Not.Null, $"Missing serialized path '{SourceModePropertyPath}' in '{scene.path}'.");
                    Assert.That(presetProperty, Is.Not.Null, $"Missing serialized path '{PresetPropertyPath}' in '{scene.path}'.");
                    Assert.That(profileProperty, Is.Not.Null, $"Missing serialized path '{AuthoringPropertyPath}' in '{scene.path}'.");
                    serializedObject.Update();
                    Assert.That(sourceModeProperty.enumValueIndex, Is.EqualTo((int)GameplayCameraTopologySourceMode.Preset));
                    Assert.That(AssetDatabase.GetAssetPath(presetProperty.objectReferenceValue), Is.EqualTo(expectedPresetPath));
                    AssertProfileMatches(expectedProfile, installer.GetTopologyTransitionCameraShakeProfile(), scene.path);
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static string ResolveExpectedPresetAssetPath(string scenePath)
        {
            return scenePath == "Assets/Scenes/CombinedGameplayShowcase.unity"
                ? "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Camera/Presets/GameplayCameraTopologyPreset_CombinedGameplayShowcase.asset"
                : "Assets/_Features/Stages/Stage_TutorialScene/Camera/Presets/GameplayCameraTopologyPreset_TutorialScene.asset";
        }

        private static bool ContainsCameraShakeProfileHolder(string relativePath)
        {
            return ReadRepoFile(relativePath).IndexOf($"{SerializedProfileKey}:", StringComparison.Ordinal) >= 0;
        }

        private static bool HasSerializedAssetExtension(string absolutePath)
        {
            var extension = Path.GetExtension(absolutePath);
            return extension.Equals(".unity", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToRepoRelativePath(string absolutePath)
        {
            var normalizedAbsolutePath = absolutePath.Replace('\\', '/');
            var normalizedProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            var projectRootPrefix = $"{normalizedProjectRoot}/";

            if (normalizedAbsolutePath.StartsWith(projectRootPrefix, StringComparison.Ordinal))
            {
                return normalizedAbsolutePath.Substring(projectRootPrefix.Length);
            }

            return normalizedAbsolutePath;
        }

        private static SerializedCameraShakeProfile ReadSerializedProfile(string installerBlock)
        {
            Assert.That(installerBlock, Does.Contain($"{SerializedProfileKey}:"));

            return new SerializedCameraShakeProfile(
                ReadSerializedFloatValue(installerBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactStart01)),
                ReadSerializedFloatValue(installerBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactDuration01)),
                ReadSerializedIntValue(installerBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactOscillationCycles)),
                ReadSerializedVector3Value(installerBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude)),
                ReadSerializedVector3Value(installerBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactLocalRotationAmplitudeDegrees)),
                ReadSerializedFloatValue(installerBlock, nameof(TopologyTransitionCameraShakeProfile.LandingStart01)),
                ReadSerializedFloatValue(installerBlock, nameof(TopologyTransitionCameraShakeProfile.LandingDuration01)),
                ReadSerializedIntValue(installerBlock, nameof(TopologyTransitionCameraShakeProfile.LandingOscillationCycles)),
                ReadSerializedVector3Value(installerBlock, nameof(TopologyTransitionCameraShakeProfile.LandingLocalPositionAmplitude)),
                ReadSerializedVector3Value(installerBlock, nameof(TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees)));
        }

        private static string ReadCombinedGameplayInstallerBlock(string scenePath)
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

        private static int ReadSerializedIntValue(string installerBlock, string key)
        {
            var match = Regex.Match(
                installerBlock,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*(\d+)\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized key '{key}' was not found.");
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static float ReadSerializedFloatValue(string installerBlock, string key)
        {
            var match = Regex.Match(
                installerBlock,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*([-+]?\d*\.?\d+)\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized key '{key}' was not found.");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static Vector3 ReadSerializedVector3Value(string installerBlock, string key)
        {
            var match = Regex.Match(
                installerBlock,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{x:\s*([-+]?\d*\.?\d+), y:\s*([-+]?\d*\.?\d+), z:\s*([-+]?\d*\.?\d+)\}}\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized key '{key}' was not found.");
            return new Vector3(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        private static void AssertSerializedPropertyMatches(
            SerializedProperty profileProperty,
            SerializedCameraShakeProfile expectedProfile,
            string scenePath)
        {
            Assert.That(profileProperty, Is.Not.Null, $"Missing serialized profile property in '{scenePath}'.");

            Assert.That(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.ImpactStart01))?.floatValue,
                Is.EqualTo(expectedProfile.ImpactStart01).Within(0.0001f),
                $"ImpactStart01 drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.ImpactDuration01))?.floatValue,
                Is.EqualTo(expectedProfile.ImpactDuration01).Within(0.0001f),
                $"ImpactDuration01 drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.ImpactOscillationCycles))?.intValue,
                Is.EqualTo(expectedProfile.ImpactOscillationCycles),
                $"ImpactOscillationCycles drifted in '{scenePath}'.");
            AssertVector3(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude))?.vector3Value ?? default,
                expectedProfile.ImpactLocalPositionAmplitude,
                $"{scenePath} ImpactLocalPositionAmplitude");
            AssertVector3(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.ImpactLocalRotationAmplitudeDegrees))?.vector3Value ?? default,
                expectedProfile.ImpactLocalRotationAmplitudeDegrees,
                $"{scenePath} ImpactLocalRotationAmplitudeDegrees");
            Assert.That(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.LandingStart01))?.floatValue,
                Is.EqualTo(expectedProfile.LandingStart01).Within(0.0001f),
                $"LandingStart01 drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.LandingDuration01))?.floatValue,
                Is.EqualTo(expectedProfile.LandingDuration01).Within(0.0001f),
                $"LandingDuration01 drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.LandingOscillationCycles))?.intValue,
                Is.EqualTo(expectedProfile.LandingOscillationCycles),
                $"LandingOscillationCycles drifted in '{scenePath}'.");
            AssertVector3(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.LandingLocalPositionAmplitude))?.vector3Value ?? default,
                expectedProfile.LandingLocalPositionAmplitude,
                $"{scenePath} LandingLocalPositionAmplitude");
            AssertVector3(
                profileProperty.FindPropertyRelative(nameof(TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees))?.vector3Value ?? default,
                expectedProfile.LandingLocalRotationAmplitudeDegrees,
                $"{scenePath} LandingLocalRotationAmplitudeDegrees");
        }

        private static void AssertProfileMatches(
            SerializedCameraShakeProfile expectedProfile,
            TopologyTransitionCameraShakeProfile actualProfile,
            string scenePath)
        {
            Assert.That(actualProfile, Is.Not.Null, $"Installer returned a null camera shake profile for '{scenePath}'.");
            Assert.That(actualProfile.ImpactStart01, Is.EqualTo(expectedProfile.ImpactStart01).Within(0.0001f));
            Assert.That(actualProfile.ImpactDuration01, Is.EqualTo(expectedProfile.ImpactDuration01).Within(0.0001f));
            Assert.That(actualProfile.ImpactOscillationCycles, Is.EqualTo(expectedProfile.ImpactOscillationCycles));
            AssertVector3(actualProfile.ImpactLocalPositionAmplitude, expectedProfile.ImpactLocalPositionAmplitude, $"{scenePath} ImpactLocalPositionAmplitude clone");
            AssertVector3(actualProfile.ImpactLocalRotationAmplitudeDegrees, expectedProfile.ImpactLocalRotationAmplitudeDegrees, $"{scenePath} ImpactLocalRotationAmplitudeDegrees clone");
            Assert.That(actualProfile.LandingStart01, Is.EqualTo(expectedProfile.LandingStart01).Within(0.0001f));
            Assert.That(actualProfile.LandingDuration01, Is.EqualTo(expectedProfile.LandingDuration01).Within(0.0001f));
            Assert.That(actualProfile.LandingOscillationCycles, Is.EqualTo(expectedProfile.LandingOscillationCycles));
            AssertVector3(actualProfile.LandingLocalPositionAmplitude, expectedProfile.LandingLocalPositionAmplitude, $"{scenePath} LandingLocalPositionAmplitude clone");
            AssertVector3(actualProfile.LandingLocalRotationAmplitudeDegrees, expectedProfile.LandingLocalRotationAmplitudeDegrees, $"{scenePath} LandingLocalRotationAmplitudeDegrees clone");
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected, string messagePrefix = "Vector3")
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), $"{messagePrefix} x drifted.");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), $"{messagePrefix} y drifted.");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f), $"{messagePrefix} z drifted.");
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
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => File.ReadAllText(path).Replace("\r\n", "\n")));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static HashSet<string> GetIdentifierTokens(string sanitizedSource)
        {
            return new HashSet<string>(
                Regex.Matches(
                        sanitizedSource,
                        @"\b[_A-Za-z][_A-Za-z0-9]*\b",
                        RegexOptions.CultureInvariant)
                    .Cast<Match>()
                    .Select(match => match.Value),
                StringComparer.Ordinal);
        }

        private static string StripCommentsAndStringLiterals(string source)
        {
            var builder = new StringBuilder(source.Length);
            var inLineComment = false;
            var inBlockComment = false;
            var inString = false;
            var inVerbatimString = false;
            var inCharLiteral = false;

            for (var i = 0; i < source.Length; i++)
            {
                var current = source[i];
                var next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\n')
                    {
                        inLineComment = false;
                        builder.Append('\n');
                    }
                    else
                    {
                        builder.Append(' ');
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        builder.Append("  ");
                        i++;
                        inBlockComment = false;
                    }
                    else
                    {
                        builder.Append(current == '\n' ? '\n' : ' ');
                    }

                    continue;
                }

                if (inString)
                {
                    if (current == '\\' && next != '\0')
                    {
                        builder.Append("  ");
                        i++;
                        continue;
                    }

                    builder.Append(current == '\n' ? '\n' : ' ');
                    if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (inVerbatimString)
                {
                    if (current == '"' && next == '"')
                    {
                        builder.Append("  ");
                        i++;
                        continue;
                    }

                    builder.Append(current == '\n' ? '\n' : ' ');
                    if (current == '"')
                    {
                        inVerbatimString = false;
                    }

                    continue;
                }

                if (inCharLiteral)
                {
                    if (current == '\\' && next != '\0')
                    {
                        builder.Append("  ");
                        i++;
                        continue;
                    }

                    builder.Append(current == '\n' ? '\n' : ' ');
                    if (current == '\'')
                    {
                        inCharLiteral = false;
                    }

                    continue;
                }

                if (current == '/' && next == '/')
                {
                    builder.Append("  ");
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    builder.Append("  ");
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    builder.Append("  ");
                    i++;
                    inVerbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    builder.Append(' ');
                    inString = true;
                    continue;
                }

                if (current == '\'')
                {
                    builder.Append(' ');
                    inCharLiteral = true;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private readonly struct SerializedCameraShakeProfile
        {
            public SerializedCameraShakeProfile(
                float impactStart01,
                float impactDuration01,
                int impactOscillationCycles,
                Vector3 impactLocalPositionAmplitude,
                Vector3 impactLocalRotationAmplitudeDegrees,
                float landingStart01,
                float landingDuration01,
                int landingOscillationCycles,
                Vector3 landingLocalPositionAmplitude,
                Vector3 landingLocalRotationAmplitudeDegrees)
            {
                ImpactStart01 = impactStart01;
                ImpactDuration01 = impactDuration01;
                ImpactOscillationCycles = impactOscillationCycles;
                ImpactLocalPositionAmplitude = impactLocalPositionAmplitude;
                ImpactLocalRotationAmplitudeDegrees = impactLocalRotationAmplitudeDegrees;
                LandingStart01 = landingStart01;
                LandingDuration01 = landingDuration01;
                LandingOscillationCycles = landingOscillationCycles;
                LandingLocalPositionAmplitude = landingLocalPositionAmplitude;
                LandingLocalRotationAmplitudeDegrees = landingLocalRotationAmplitudeDegrees;
            }

            public float ImpactStart01 { get; }

            public float ImpactDuration01 { get; }

            public int ImpactOscillationCycles { get; }

            public Vector3 ImpactLocalPositionAmplitude { get; }

            public Vector3 ImpactLocalRotationAmplitudeDegrees { get; }

            public float LandingStart01 { get; }

            public float LandingDuration01 { get; }

            public int LandingOscillationCycles { get; }

            public Vector3 LandingLocalPositionAmplitude { get; }

            public Vector3 LandingLocalRotationAmplitudeDegrees { get; }
        }
    }
}
