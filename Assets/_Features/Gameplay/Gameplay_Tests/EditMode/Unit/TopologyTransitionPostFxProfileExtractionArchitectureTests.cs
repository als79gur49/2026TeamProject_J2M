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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TopologyTransitionPostFxProfileExtractionArchitectureTests
    {
        private const string ProfileRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Profiles/TopologyTransitionPostFxProfile.cs";
        private const string LegacyProfileRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyTransitionPostFxProfile.cs";
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string RuntimePostFxRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx";
        private const string RuntimePostFxProfilesRelativeDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Profiles";
        private const string PostFxFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx.meta";
        private const string PostFxProfilesFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Profiles.meta";
        private const string ProfileMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Profiles/TopologyTransitionPostFxProfile.cs.meta";
        private const string AuthoringPropertyPath = "topologyTransitionPostFxProfile";
        private const string SourceModePropertyPath = "sourceMode";
        private const string PresetPropertyPath = "preset";
        private const string GameplayCameraTopologyAuthoringMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.GameplayCameraTopologyAuthoring";
        private const string ExpectedProfileGuid = "e9c2d74380794db68d7b3d0c8acaf812";

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
            "authoritativeVolumeProfile",
            "motionBlurMode",
            "motionBlurQuality",
            "maxBlurIntensity",
            "cameraClamp",
            "angularVelocityResponseExponent",
            "landingFadeStart01",
            "landingFadeExponent",
            "distortionProfile",
        };

        private static readonly Dictionary<string, Type> ExpectedFieldTypes = new(StringComparer.Ordinal)
        {
            ["authoritativeVolumeProfile"] = typeof(VolumeProfile),
            ["motionBlurMode"] = typeof(MotionBlurMode),
            ["motionBlurQuality"] = typeof(MotionBlurQuality),
            ["maxBlurIntensity"] = typeof(float),
            ["cameraClamp"] = typeof(float),
            ["angularVelocityResponseExponent"] = typeof(float),
            ["landingFadeStart01"] = typeof(float),
            ["landingFadeExponent"] = typeof(float),
            ["distortionProfile"] = typeof(TopologyTransitionDistortionProfile),
        };

        private static readonly string[] ExpectedDistortionFieldNames =
        {
            nameof(TopologyTransitionDistortionProfile.ImpactStart01),
            nameof(TopologyTransitionDistortionProfile.ImpactDuration01),
            nameof(TopologyTransitionDistortionProfile.ImpactIntensity),
            nameof(TopologyTransitionDistortionProfile.LandingStart01),
            nameof(TopologyTransitionDistortionProfile.LandingDuration01),
            nameof(TopologyTransitionDistortionProfile.LandingIntensity),
            nameof(TopologyTransitionDistortionProfile.XMultiplier),
            nameof(TopologyTransitionDistortionProfile.YMultiplier),
            nameof(TopologyTransitionDistortionProfile.Center),
            nameof(TopologyTransitionDistortionProfile.Scale),
        };

        private static readonly Dictionary<string, Type> ExpectedDistortionFieldTypes = new(StringComparer.Ordinal)
        {
            [nameof(TopologyTransitionDistortionProfile.ImpactStart01)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.ImpactDuration01)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.ImpactIntensity)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.LandingStart01)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.LandingDuration01)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.LandingIntensity)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.XMultiplier)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.YMultiplier)] = typeof(float),
            [nameof(TopologyTransitionDistortionProfile.Center)] = typeof(Vector2),
            [nameof(TopologyTransitionDistortionProfile.Scale)] = typeof(float),
        };

        private static readonly string[] ForbiddenIdentifiers =
        {
            "GameplayTickViewPresenter",
            "GameplayHostRuntimeFactory",
            "GameplayShowcaseSceneScaffold",
            "GameplayCameraRig",
            "TopologyTransitionCameraShakeController",
            "TopologyTransitionPostFxController",
            "FindObjectOfType",
            "FindObjectsOfType",
            "FindFirstObjectByType",
            "FindAnyObjectByType",
            "SceneManager",
            "MonoBehaviour",
            "GameObject",
            "Transform",
            "Camera",
        };

        [Test]
        [Category("Extended")]
        public void PostFxProfileFile_Exists_InDedicatedPostFxRuntimeProfileLane()
        {
            var profileSource = ReadRepoFile(ProfileRelativePath);

            Assert.That(profileSource, Does.Contain("public sealed class TopologyTransitionPostFxProfile"));
            Assert.That(profileSource, Does.Contain("public sealed class TopologyTransitionDistortionProfile"));
            Assert.That(profileSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void PostFxRuntimeProfileFolders_Exist_WithTrackedMetaFiles()
        {
            Assert.That(Directory.Exists(GetAbsolutePath(RuntimePostFxRelativeDirectory)), Is.True);
            Assert.That(Directory.Exists(GetAbsolutePath(RuntimePostFxProfilesRelativeDirectory)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(PostFxFolderMetaRelativePath)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(PostFxProfilesFolderMetaRelativePath)), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void LegacyTopLevelPostFxProfileFile_IsAbsent_AfterExtraction()
        {
            Assert.That(File.Exists(GetAbsolutePath(LegacyProfileRelativePath)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForTopologyTransitionPostFxProfile()
        {
            var runtimeSource = ReadCombinedSource(RuntimeRelativeDirectory);

            Assert.That(
                CountMatches(runtimeSource, @"\bclass\s+TopologyTransitionPostFxProfile\b"),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTopLevelFiles_DoNotReintroduce_TopologyTransitionPostFxProfileDeclaration()
        {
            var topLevelFiles = Directory.GetFiles(GetAbsolutePath(RuntimeRelativeDirectory), "*.cs", SearchOption.TopDirectoryOnly);

            foreach (var filePath in topLevelFiles)
            {
                var fileSource = File.ReadAllText(filePath).Replace("\r\n", "\n");
                Assert.That(
                    Regex.IsMatch(
                        fileSource,
                        @"\bclass\s+TopologyTransitionPostFxProfile\b",
                        RegexOptions.CultureInvariant),
                    Is.False,
                    $"Unexpected top-level declaration in '{filePath}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfileType_Contract_RemainsUnchanged()
        {
            var profileType = typeof(TopologyTransitionPostFxProfile);

            Assert.That(profileType.Namespace, Is.EqualTo("Game.Feature.Gameplay.Host"));
            Assert.That(profileType.IsPublic, Is.True);
            Assert.That(profileType.IsSealed, Is.True);
            Assert.That(profileType.IsDefined(typeof(SerializableAttribute), inherit: false), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionDistortionProfileType_Contract_RemainsUnchanged()
        {
            var distortionType = typeof(TopologyTransitionDistortionProfile);

            Assert.That(distortionType.Namespace, Is.EqualTo("Game.Feature.Gameplay.Host"));
            Assert.That(distortionType.IsPublic, Is.True);
            Assert.That(distortionType.IsSealed, Is.True);
            Assert.That(distortionType.IsDefined(typeof(SerializableAttribute), inherit: false), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfileSource_DeclarationForms_RemainSerializablePublicSealedClasses()
        {
            var profileSource = ReadRepoFile(ProfileRelativePath);

            Assert.That(
                Regex.IsMatch(
                    profileSource,
                    @"\[Serializable\]\s+public\s+sealed\s+class\s+TopologyTransitionDistortionProfile",
                    RegexOptions.CultureInvariant),
                Is.True);
            Assert.That(
                Regex.IsMatch(
                    profileSource,
                    @"\[Serializable\]\s+public\s+sealed\s+class\s+TopologyTransitionPostFxProfile",
                    RegexOptions.CultureInvariant),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfileFactoryAndCloneSignatures_RemainUnchanged()
        {
            var createDefaultMethod = typeof(TopologyTransitionPostFxProfile).GetMethod(
                nameof(TopologyTransitionPostFxProfile.CreateDefault),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var createMethod = typeof(TopologyTransitionPostFxProfile).GetMethod(
                nameof(TopologyTransitionPostFxProfile.Create),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var cloneMethod = typeof(TopologyTransitionPostFxProfile).GetMethod(
                nameof(TopologyTransitionPostFxProfile.Clone),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.That(createDefaultMethod, Is.Not.Null);
            Assert.That(createDefaultMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionPostFxProfile)));
            Assert.That(createDefaultMethod?.GetParameters(), Is.Empty);

            Assert.That(createMethod, Is.Not.Null);
            Assert.That(createMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionPostFxProfile)));
            Assert.That(
                createMethod?.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(VolumeProfile),
                    typeof(MotionBlurMode),
                    typeof(MotionBlurQuality),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(TopologyTransitionDistortionProfile),
                }));

            Assert.That(cloneMethod, Is.Not.Null);
            Assert.That(cloneMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionPostFxProfile)));
            Assert.That(cloneMethod?.GetParameters(), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionDistortionProfileFactoryAndCloneSignatures_RemainUnchanged()
        {
            var createDefaultMethod = typeof(TopologyTransitionDistortionProfile).GetMethod(
                nameof(TopologyTransitionDistortionProfile.CreateDefault),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var createMethod = typeof(TopologyTransitionDistortionProfile).GetMethod(
                nameof(TopologyTransitionDistortionProfile.Create),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var cloneMethod = typeof(TopologyTransitionDistortionProfile).GetMethod(
                nameof(TopologyTransitionDistortionProfile.Clone),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.That(createDefaultMethod, Is.Not.Null);
            Assert.That(createDefaultMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionDistortionProfile)));
            Assert.That(createDefaultMethod?.GetParameters(), Is.Empty);

            Assert.That(createMethod, Is.Not.Null);
            Assert.That(createMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionDistortionProfile)));
            Assert.That(
                createMethod?.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector2?),
                    typeof(float),
                }));

            Assert.That(cloneMethod, Is.Not.Null);
            Assert.That(cloneMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionDistortionProfile)));
            Assert.That(cloneMethod?.GetParameters(), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfile_SerializedFields_AndFieldTypes_RemainCanonical()
        {
            var fields = typeof(TopologyTransitionPostFxProfile)
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
        public void TopologyTransitionDistortionProfile_SerializedFields_AndFieldTypes_RemainCanonical()
        {
            var fields = typeof(TopologyTransitionDistortionProfile)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEquivalent(ExpectedDistortionFieldNames, fields.Select(field => field.Name).ToArray());

            foreach (var field in fields)
            {
                Assert.That(
                    field.FieldType,
                    Is.EqualTo(ExpectedDistortionFieldTypes[field.Name]),
                    $"Distortion field '{field.Name}' changed type.");
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfile_DefaultInstanceValues_RemainCanonical()
        {
            var profile = TopologyTransitionPostFxProfile.CreateDefault();

            Assert.That(profile.AuthoritativeVolumeProfile, Is.Null);
            Assert.That(profile.MotionBlurMode, Is.EqualTo(MotionBlurMode.CameraOnly));
            Assert.That(profile.MotionBlurQuality, Is.EqualTo(MotionBlurQuality.Low));
            Assert.That(profile.MaxBlurIntensity, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(profile.CameraClamp, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(profile.AngularVelocityResponseExponent, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(profile.LandingFadeStart01, Is.EqualTo(0.82f).Within(0.0001f));
            Assert.That(profile.LandingFadeExponent, Is.EqualTo(3f).Within(0.0001f));
            AssertDistortionMatches(profile.DistortionProfile, TopologyTransitionDistortionProfile.CreateDefault(), "default");
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionDistortionProfile_DefaultInstanceValues_RemainCanonical()
        {
            var profile = TopologyTransitionDistortionProfile.CreateDefault();

            Assert.That(profile.ImpactStart01, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(profile.ImpactDuration01, Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(profile.ImpactIntensity, Is.EqualTo(-0.2f).Within(0.0001f));
            Assert.That(profile.LandingStart01, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(profile.LandingDuration01, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(profile.LandingIntensity, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(profile.XMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(profile.YMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(profile.Center, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(profile.Scale, Is.EqualTo(1.05f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfileMetaGuid_RemainsUnchanged_AfterMove()
        {
            var metaSource = ReadRepoFile(ProfileMetaRelativePath);

            Assert.That(metaSource, Does.Contain($"guid: {ExpectedProfileGuid}"));
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxProfileSource_AllowsTopologyTransitionVisualStateEvaluationSurface_AndAvoidsForbiddenOwnershipDependencies()
        {
            var sanitizedSource = StripCommentsAndStringLiterals(ReadRepoFile(ProfileRelativePath));
            var identifierTokens = GetIdentifierTokens(sanitizedSource);

            Assert.That(
                identifierTokens.Contains(nameof(TopologyTransitionVisualState)),
                Is.True,
                "TopologyTransitionVisualState is the allowed pure evaluation surface for the post-fx profile.");

            foreach (var forbiddenIdentifier in ForbiddenIdentifiers)
            {
                Assert.That(
                    identifierTokens.Contains(forbiddenIdentifier),
                    Is.False,
                    $"Forbidden ownership identifier '{forbiddenIdentifier}' was found in the profile source.");
            }
        }

        [Test]
        [Category("Extended")]
        public void RepositorySerializedTargets_ForPostFxProfile_AreLimitedToKnownCameraTopologyAuthoringAndPresetAssets()
        {
            var assetPaths = Directory.GetFiles(GetAbsolutePath("Assets"), "*.*", SearchOption.AllDirectories)
                .Where(path => HasSerializedAssetExtension(path))
                .Select(ToRepoRelativePath)
                .Where(ContainsPostFxProfileHolder)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEquivalent(ExpectedSerializedHolders, assetPaths);
        }

        [Test]
        [Category("Full")]
        public void ShowcaseInstallerScenes_PresetMode_RuntimeMeaning_UsesReferencedPostFxPreset()
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
                    AssertProfileMatches(expectedProfile, installer.GetTopologyTransitionPostFxProfile(), scene.path);
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

        private static bool ContainsPostFxProfileHolder(string relativePath)
        {
            return ReadRepoFile(relativePath).IndexOf($"{AuthoringPropertyPath}:", StringComparison.Ordinal) >= 0;
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

        private static SerializedTopologyTransitionPostFxProfile ReadSerializedProfile(string installerBlock)
        {
            Assert.That(installerBlock, Does.Contain($"{AuthoringPropertyPath}:"));
            var profileBlock = ReadSerializedBlock(installerBlock, AuthoringPropertyPath, 2);
            var distortionBlock = ReadSerializedBlock(profileBlock, "distortionProfile", 4);

            return new SerializedTopologyTransitionPostFxProfile(
                ReadSerializedObjectGuid(profileBlock, "authoritativeVolumeProfile"),
                ReadSerializedIntValue(profileBlock, "motionBlurMode"),
                ReadSerializedIntValue(profileBlock, "motionBlurQuality"),
                ReadSerializedFloatValue(profileBlock, "maxBlurIntensity"),
                ReadSerializedFloatValue(profileBlock, "cameraClamp"),
                ReadSerializedFloatValue(profileBlock, "angularVelocityResponseExponent"),
                ReadSerializedFloatValue(profileBlock, "landingFadeStart01"),
                ReadSerializedFloatValue(profileBlock, "landingFadeExponent"),
                new SerializedTopologyTransitionDistortionProfile(
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.ImpactStart01)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.ImpactDuration01)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.ImpactIntensity)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.LandingStart01)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.LandingDuration01)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.LandingIntensity)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.XMultiplier)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.YMultiplier)),
                    ReadSerializedVector2Value(distortionBlock, nameof(TopologyTransitionDistortionProfile.Center)),
                    ReadSerializedFloatValue(distortionBlock, nameof(TopologyTransitionDistortionProfile.Scale))));
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

        private static string ReadSerializedObjectGuid(string installerBlock, string key)
        {
            var match = Regex.Match(
                installerBlock,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{fileID:\s*\d+,\s*guid:\s*([0-9a-f]+),\s*type:\s*\d+\}}\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized object key '{key}' was not found.");
            return match.Groups[1].Value;
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

        private static Vector2 ReadSerializedVector2Value(string installerBlock, string key)
        {
            var match = Regex.Match(
                installerBlock,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{x:\s*([-+]?\d*\.?\d+), y:\s*([-+]?\d*\.?\d+)\}}\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized key '{key}' was not found.");
            return new Vector2(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        private static string ReadSerializedBlock(string source, string key, int indentation)
        {
            var parentIndentation = new string(' ', indentation);
            var childIndentation = new string(' ', indentation + 2);
            var match = Regex.Match(
                source,
                $@"(?ms)^{Regex.Escape(parentIndentation)}{Regex.Escape(key)}:\s*$\n(?<block>(?:^{Regex.Escape(childIndentation)}.*$\n?)*)",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized block '{key}' was not found.");
            return match.Groups["block"].Value;
        }

        private static void AssertSerializedPropertyMatches(
            SerializedProperty profileProperty,
            SerializedTopologyTransitionPostFxProfile expectedProfile,
            string scenePath)
        {
            Assert.That(profileProperty, Is.Not.Null, $"Missing serialized profile property in '{scenePath}'.");

            var authoritativeProfileProperty = profileProperty.FindPropertyRelative("authoritativeVolumeProfile");
            var authoritativeVolumeProfile = authoritativeProfileProperty?.objectReferenceValue as VolumeProfile;
            Assert.That(authoritativeProfileProperty, Is.Not.Null, $"Missing authoritative volume profile property in '{scenePath}'.");
            Assert.That(authoritativeVolumeProfile, Is.Not.Null, $"Missing authoritative volume profile reference in '{scenePath}'.");
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(authoritativeVolumeProfile)),
                Is.EqualTo(expectedProfile.AuthoritativeVolumeProfileGuid),
                $"Authoritative volume profile drifted in '{scenePath}'.");

            Assert.That(
                profileProperty.FindPropertyRelative("motionBlurMode")?.enumValueIndex,
                Is.EqualTo(expectedProfile.MotionBlurMode),
                $"motionBlurMode drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative("motionBlurQuality")?.enumValueIndex,
                Is.EqualTo(expectedProfile.MotionBlurQuality),
                $"motionBlurQuality drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative("maxBlurIntensity")?.floatValue,
                Is.EqualTo(expectedProfile.MaxBlurIntensity).Within(0.0001f),
                $"maxBlurIntensity drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative("cameraClamp")?.floatValue,
                Is.EqualTo(expectedProfile.CameraClamp).Within(0.0001f),
                $"cameraClamp drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative("angularVelocityResponseExponent")?.floatValue,
                Is.EqualTo(expectedProfile.AngularVelocityResponseExponent).Within(0.0001f),
                $"angularVelocityResponseExponent drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative("landingFadeStart01")?.floatValue,
                Is.EqualTo(expectedProfile.LandingFadeStart01).Within(0.0001f),
                $"landingFadeStart01 drifted in '{scenePath}'.");
            Assert.That(
                profileProperty.FindPropertyRelative("landingFadeExponent")?.floatValue,
                Is.EqualTo(expectedProfile.LandingFadeExponent).Within(0.0001f),
                $"landingFadeExponent drifted in '{scenePath}'.");

            var distortionProfileProperty = profileProperty.FindPropertyRelative("distortionProfile");
            Assert.That(distortionProfileProperty, Is.Not.Null, $"Missing distortionProfile in '{scenePath}'.");
            AssertSerializedDistortionPropertyMatches(distortionProfileProperty, expectedProfile.DistortionProfile, scenePath);
        }

        private static void AssertSerializedDistortionPropertyMatches(
            SerializedProperty distortionProperty,
            SerializedTopologyTransitionDistortionProfile expectedProfile,
            string scenePath)
        {
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.ImpactStart01))?.floatValue,
                Is.EqualTo(expectedProfile.ImpactStart01).Within(0.0001f),
                $"ImpactStart01 drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.ImpactDuration01))?.floatValue,
                Is.EqualTo(expectedProfile.ImpactDuration01).Within(0.0001f),
                $"ImpactDuration01 drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.ImpactIntensity))?.floatValue,
                Is.EqualTo(expectedProfile.ImpactIntensity).Within(0.0001f),
                $"ImpactIntensity drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.LandingStart01))?.floatValue,
                Is.EqualTo(expectedProfile.LandingStart01).Within(0.0001f),
                $"LandingStart01 drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.LandingDuration01))?.floatValue,
                Is.EqualTo(expectedProfile.LandingDuration01).Within(0.0001f),
                $"LandingDuration01 drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.LandingIntensity))?.floatValue,
                Is.EqualTo(expectedProfile.LandingIntensity).Within(0.0001f),
                $"LandingIntensity drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.XMultiplier))?.floatValue,
                Is.EqualTo(expectedProfile.XMultiplier).Within(0.0001f),
                $"XMultiplier drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.YMultiplier))?.floatValue,
                Is.EqualTo(expectedProfile.YMultiplier).Within(0.0001f),
                $"YMultiplier drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.Center))?.vector2Value,
                Is.EqualTo(expectedProfile.Center),
                $"Center drifted in '{scenePath}'.");
            Assert.That(
                distortionProperty.FindPropertyRelative(nameof(TopologyTransitionDistortionProfile.Scale))?.floatValue,
                Is.EqualTo(expectedProfile.Scale).Within(0.0001f),
                $"Scale drifted in '{scenePath}'.");
        }

        private static void AssertProfileMatches(
            SerializedTopologyTransitionPostFxProfile expectedProfile,
            TopologyTransitionPostFxProfile actualProfile,
            string scenePath)
        {
            Assert.That(actualProfile, Is.Not.Null, $"Installer returned a null post-fx profile for '{scenePath}'.");
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(actualProfile.AuthoritativeVolumeProfile)),
                Is.EqualTo(expectedProfile.AuthoritativeVolumeProfileGuid));
            Assert.That(actualProfile.MotionBlurMode, Is.EqualTo((MotionBlurMode)expectedProfile.MotionBlurMode));
            Assert.That(actualProfile.MotionBlurQuality, Is.EqualTo((MotionBlurQuality)expectedProfile.MotionBlurQuality));
            Assert.That(actualProfile.MaxBlurIntensity, Is.EqualTo(expectedProfile.MaxBlurIntensity).Within(0.0001f));
            Assert.That(actualProfile.CameraClamp, Is.EqualTo(expectedProfile.CameraClamp).Within(0.0001f));
            Assert.That(
                actualProfile.AngularVelocityResponseExponent,
                Is.EqualTo(expectedProfile.AngularVelocityResponseExponent).Within(0.0001f));
            Assert.That(actualProfile.LandingFadeStart01, Is.EqualTo(expectedProfile.LandingFadeStart01).Within(0.0001f));
            Assert.That(actualProfile.LandingFadeExponent, Is.EqualTo(expectedProfile.LandingFadeExponent).Within(0.0001f));
            AssertDistortionMatches(actualProfile.DistortionProfile, expectedProfile.DistortionProfile, scenePath);
        }

        private static void AssertDistortionMatches(
            TopologyTransitionDistortionProfile actualProfile,
            SerializedTopologyTransitionDistortionProfile expectedProfile,
            string scenePath)
        {
            Assert.That(actualProfile, Is.Not.Null, $"Distortion profile was null for '{scenePath}'.");
            Assert.That(actualProfile.ImpactStart01, Is.EqualTo(expectedProfile.ImpactStart01).Within(0.0001f));
            Assert.That(actualProfile.ImpactDuration01, Is.EqualTo(expectedProfile.ImpactDuration01).Within(0.0001f));
            Assert.That(actualProfile.ImpactIntensity, Is.EqualTo(expectedProfile.ImpactIntensity).Within(0.0001f));
            Assert.That(actualProfile.LandingStart01, Is.EqualTo(expectedProfile.LandingStart01).Within(0.0001f));
            Assert.That(actualProfile.LandingDuration01, Is.EqualTo(expectedProfile.LandingDuration01).Within(0.0001f));
            Assert.That(actualProfile.LandingIntensity, Is.EqualTo(expectedProfile.LandingIntensity).Within(0.0001f));
            Assert.That(actualProfile.XMultiplier, Is.EqualTo(expectedProfile.XMultiplier).Within(0.0001f));
            Assert.That(actualProfile.YMultiplier, Is.EqualTo(expectedProfile.YMultiplier).Within(0.0001f));
            Assert.That(actualProfile.Center, Is.EqualTo(expectedProfile.Center));
            Assert.That(actualProfile.Scale, Is.EqualTo(expectedProfile.Scale).Within(0.0001f));
        }

        private static void AssertDistortionMatches(
            TopologyTransitionDistortionProfile actualProfile,
            TopologyTransitionDistortionProfile expectedProfile,
            string messagePrefix)
        {
            Assert.That(actualProfile, Is.Not.Null, $"{messagePrefix} distortion profile was null.");
            Assert.That(expectedProfile, Is.Not.Null, $"{messagePrefix} expected distortion profile was null.");
            Assert.That(actualProfile.ImpactStart01, Is.EqualTo(expectedProfile.ImpactStart01).Within(0.0001f));
            Assert.That(actualProfile.ImpactDuration01, Is.EqualTo(expectedProfile.ImpactDuration01).Within(0.0001f));
            Assert.That(actualProfile.ImpactIntensity, Is.EqualTo(expectedProfile.ImpactIntensity).Within(0.0001f));
            Assert.That(actualProfile.LandingStart01, Is.EqualTo(expectedProfile.LandingStart01).Within(0.0001f));
            Assert.That(actualProfile.LandingDuration01, Is.EqualTo(expectedProfile.LandingDuration01).Within(0.0001f));
            Assert.That(actualProfile.LandingIntensity, Is.EqualTo(expectedProfile.LandingIntensity).Within(0.0001f));
            Assert.That(actualProfile.XMultiplier, Is.EqualTo(expectedProfile.XMultiplier).Within(0.0001f));
            Assert.That(actualProfile.YMultiplier, Is.EqualTo(expectedProfile.YMultiplier).Within(0.0001f));
            Assert.That(actualProfile.Center, Is.EqualTo(expectedProfile.Center));
            Assert.That(actualProfile.Scale, Is.EqualTo(expectedProfile.Scale).Within(0.0001f));
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

        private readonly struct SerializedTopologyTransitionPostFxProfile
        {
            public SerializedTopologyTransitionPostFxProfile(
                string authoritativeVolumeProfileGuid,
                int motionBlurMode,
                int motionBlurQuality,
                float maxBlurIntensity,
                float cameraClamp,
                float angularVelocityResponseExponent,
                float landingFadeStart01,
                float landingFadeExponent,
                SerializedTopologyTransitionDistortionProfile distortionProfile)
            {
                AuthoritativeVolumeProfileGuid = authoritativeVolumeProfileGuid;
                MotionBlurMode = motionBlurMode;
                MotionBlurQuality = motionBlurQuality;
                MaxBlurIntensity = maxBlurIntensity;
                CameraClamp = cameraClamp;
                AngularVelocityResponseExponent = angularVelocityResponseExponent;
                LandingFadeStart01 = landingFadeStart01;
                LandingFadeExponent = landingFadeExponent;
                DistortionProfile = distortionProfile;
            }

            public string AuthoritativeVolumeProfileGuid { get; }

            public int MotionBlurMode { get; }

            public int MotionBlurQuality { get; }

            public float MaxBlurIntensity { get; }

            public float CameraClamp { get; }

            public float AngularVelocityResponseExponent { get; }

            public float LandingFadeStart01 { get; }

            public float LandingFadeExponent { get; }

            public SerializedTopologyTransitionDistortionProfile DistortionProfile { get; }
        }

        private readonly struct SerializedTopologyTransitionDistortionProfile
        {
            public SerializedTopologyTransitionDistortionProfile(
                float impactStart01,
                float impactDuration01,
                float impactIntensity,
                float landingStart01,
                float landingDuration01,
                float landingIntensity,
                float xMultiplier,
                float yMultiplier,
                Vector2 center,
                float scale)
            {
                ImpactStart01 = impactStart01;
                ImpactDuration01 = impactDuration01;
                ImpactIntensity = impactIntensity;
                LandingStart01 = landingStart01;
                LandingDuration01 = landingDuration01;
                LandingIntensity = landingIntensity;
                XMultiplier = xMultiplier;
                YMultiplier = yMultiplier;
                Center = center;
                Scale = scale;
            }

            public float ImpactStart01 { get; }

            public float ImpactDuration01 { get; }

            public float ImpactIntensity { get; }

            public float LandingStart01 { get; }

            public float LandingDuration01 { get; }

            public float LandingIntensity { get; }

            public float XMultiplier { get; }

            public float YMultiplier { get; }

            public Vector2 Center { get; }

            public float Scale { get; }
        }
    }
}
