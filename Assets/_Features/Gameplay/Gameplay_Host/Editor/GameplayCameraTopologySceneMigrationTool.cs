using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    public static class GameplayCameraTopologySceneMigrationTool
    {
        private const string InstallerMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.CombinedGameplayShowcaseInstaller";
        private const string AuthoringMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.GameplayCameraTopologyAuthoring";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/CombinedGameplayShowcase.unity",
            "Assets/Scenes/TutorialScene.unity",
            "Assets/Scenes/UIAudioScene.unity",
        };

        [MenuItem("Tools/Gameplay/Migration/Extract Camera Topology Authoring (All Showcase Scenes)")]
        public static void ExtractCameraTopologyAuthoringForAllShowcaseScenes()
        {
            for (var i = 0; i < ScenePaths.Length; i++)
            {
                MigrateScene(ScenePaths[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void MigrateScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path is required.", nameof(scenePath));
            }

            var serializedData = ReadSerializedData(scenePath);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            try
            {
                var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                if (installer == null)
                {
                    throw new InvalidOperationException(
                        $"Scene '{scene.path}' is missing {nameof(CombinedGameplayShowcaseInstaller)}.");
                }

                var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>() ??
                                installer.gameObject.AddComponent<GameplayCameraTopologyAuthoring>();
                Apply(authoring, serializedData);
                EditorUtility.SetDirty(authoring);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static SerializedGameplayCameraTopologyAuthoring ReadSerializedData(string scenePath)
        {
            var sceneText = ReadRepoFile(scenePath);
            var sourceBlock = TryReadComponentBlock(sceneText, AuthoringMarker) ??
                              TryReadComponentBlock(sceneText, InstallerMarker);

            if (string.IsNullOrWhiteSpace(sourceBlock))
            {
                throw new InvalidOperationException(
                    $"Scene '{scenePath}' does not contain a supported camera topology source block.");
            }

            var topologyRotationTweenSettingsBlock = ReadSerializedBlock(
                sourceBlock,
                "topologyRotationTweenSettings",
                2);
            var cameraSettingsBlock = ReadSerializedBlock(sourceBlock, "cameraSettings", 2);
            var shakeProfileBlock = ReadSerializedBlock(sourceBlock, "topologyTransitionCameraShakeProfile", 2);
            var postFxProfileBlock = ReadSerializedBlock(sourceBlock, "topologyTransitionPostFxProfile", 2);
            var distortionProfileBlock = ReadSerializedBlock(postFxProfileBlock, "distortionProfile", 4);

            return new SerializedGameplayCameraTopologyAuthoring(
                ReadSerializedBoolValue(sourceBlock, "configureMainCamera"),
                ReadSerializedBoolValue(cameraSettingsBlock, nameof(GameplayCameraSettings.UseAuthoredSceneCameraPose)),
                ReadSerializedBoolValue(cameraSettingsBlock, nameof(GameplayCameraSettings.UseAuthoredSceneCameraLens)),
                (TopologyRotationVisualMapping)ReadSerializedIntValue(sourceBlock, "topologyRotationVisualMapping"),
                new TopologyRotationTweenSettings
                {
                    Ease = (TopologyRotationTweenEase)ReadSerializedIntValue(
                        topologyRotationTweenSettingsBlock,
                        nameof(TopologyRotationTweenSettings.Ease)),
                },
                new GameplayCameraSettings
                {
                    UseAuthoredSceneCameraPose = ReadSerializedBoolValue(cameraSettingsBlock, nameof(GameplayCameraSettings.UseAuthoredSceneCameraPose)),
                    UseAuthoredSceneCameraLens = ReadSerializedBoolValue(cameraSettingsBlock, nameof(GameplayCameraSettings.UseAuthoredSceneCameraLens)),
                    PitchDegrees = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.PitchDegrees)),
                    YawDegrees = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.YawDegrees)),
                    DistanceMode = (CameraDistanceMode)ReadSerializedIntValue(cameraSettingsBlock, nameof(GameplayCameraSettings.DistanceMode)),
                    ManualDistance = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.ManualDistance)),
                    FramingPadding = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.FramingPadding)),
                    PerspectiveFieldOfView = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.PerspectiveFieldOfView)),
                    NearClipPlane = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.NearClipPlane)),
                    FarClipPlane = ReadSerializedFloatValue(cameraSettingsBlock, nameof(GameplayCameraSettings.FarClipPlane)),
                    ClearFlags = (CameraClearFlags)ReadSerializedIntValue(cameraSettingsBlock, nameof(GameplayCameraSettings.ClearFlags)),
                    BackgroundColor = ReadSerializedColorValue(cameraSettingsBlock, nameof(GameplayCameraSettings.BackgroundColor)),
                },
                new TopologyTransitionCameraShakeProfile
                {
                    ImpactStart01 = ReadSerializedFloatValue(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactStart01)),
                    ImpactDuration01 = ReadSerializedFloatValue(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactDuration01)),
                    ImpactOscillationCycles = ReadSerializedIntValue(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactOscillationCycles)),
                    ImpactLocalPositionAmplitude = ReadSerializedVector3Value(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude)),
                    ImpactLocalRotationAmplitudeDegrees = ReadSerializedVector3Value(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.ImpactLocalRotationAmplitudeDegrees)),
                    LandingStart01 = ReadSerializedFloatValue(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.LandingStart01)),
                    LandingDuration01 = ReadSerializedFloatValue(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.LandingDuration01)),
                    LandingOscillationCycles = ReadSerializedIntValue(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.LandingOscillationCycles)),
                    LandingLocalPositionAmplitude = ReadSerializedVector3Value(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.LandingLocalPositionAmplitude)),
                    LandingLocalRotationAmplitudeDegrees = ReadSerializedVector3Value(shakeProfileBlock, nameof(TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees)),
                },
                CreatePostFxProfile(
                    LoadAssetByGuid(ReadSerializedObjectGuid(postFxProfileBlock, "authoritativeVolumeProfile")),
                    ReadSerializedIntValue(postFxProfileBlock, "motionBlurMode"),
                    ReadSerializedIntValue(postFxProfileBlock, "motionBlurQuality"),
                    ReadSerializedFloatValue(postFxProfileBlock, "maxBlurIntensity"),
                    ReadSerializedFloatValue(postFxProfileBlock, "cameraClamp"),
                    ReadSerializedFloatValue(postFxProfileBlock, "angularVelocityResponseExponent"),
                    ReadSerializedFloatValue(postFxProfileBlock, "landingFadeStart01"),
                    ReadSerializedFloatValue(postFxProfileBlock, "landingFadeExponent"),
                    TopologyTransitionDistortionProfile.Create(
                        impactStart01: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.ImpactStart01)),
                        impactDuration01: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.ImpactDuration01)),
                        impactIntensity: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.ImpactIntensity)),
                        landingStart01: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.LandingStart01)),
                        landingDuration01: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.LandingDuration01)),
                        landingIntensity: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.LandingIntensity)),
                        xMultiplier: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.XMultiplier)),
                        yMultiplier: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.YMultiplier)),
                        center: ReadSerializedVector2Value(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.Center)),
                        scale: ReadSerializedFloatValue(distortionProfileBlock, nameof(TopologyTransitionDistortionProfile.Scale)))));
        }

        private static void Apply(
            GameplayCameraTopologyAuthoring authoring,
            SerializedGameplayCameraTopologyAuthoring serializedData)
        {
            if (authoring == null)
            {
                throw new ArgumentNullException(nameof(authoring));
            }

            SetPrivateField(authoring, "configureMainCamera", serializedData.ConfigureMainCamera);
            SetPrivateField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Inline);
            SetPrivateField(authoring, "preset", null);
            SetPrivateField(authoring, "useAuthoredSceneCameraPose", serializedData.UseAuthoredSceneCameraPose);
            SetPrivateField(authoring, "useAuthoredSceneCameraLens", serializedData.UseAuthoredSceneCameraLens);
            SetPrivateField(authoring, "topologyRotationVisualMapping", serializedData.TopologyRotationVisualMapping);
            SetPrivateField(authoring, "topologyRotationTweenSettings", serializedData.TopologyRotationTweenSettings);
            SetPrivateField(authoring, "cameraSettings", serializedData.CameraSettings.Clone());
            SetPrivateField(authoring, "topologyTransitionCameraShakeProfile", serializedData.TopologyTransitionCameraShakeProfile.Clone());
            SetPrivateField(authoring, "topologyTransitionPostFxProfile", serializedData.TopologyTransitionPostFxProfile.Clone());
            authoring.Validate();
        }

        private static TopologyTransitionPostFxProfile CreatePostFxProfile(
            UnityEngine.Object authoritativeVolumeProfile,
            int motionBlurModeValue,
            int motionBlurQualityValue,
            float maxBlurIntensity,
            float cameraClamp,
            float angularVelocityResponseExponent,
            float landingFadeStart01,
            float landingFadeExponent,
            TopologyTransitionDistortionProfile distortionProfile)
        {
            var profile = TopologyTransitionPostFxProfile.CreateDefault();

            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "authoritativeVolumeProfile", authoritativeVolumeProfile);
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "motionBlurMode", CreateEnumValue("UnityEngine.Rendering.Universal.MotionBlurMode", motionBlurModeValue));
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "motionBlurQuality", CreateEnumValue("UnityEngine.Rendering.Universal.MotionBlurQuality", motionBlurQualityValue));
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "maxBlurIntensity", maxBlurIntensity);
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "cameraClamp", cameraClamp);
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "angularVelocityResponseExponent", angularVelocityResponseExponent);
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "landingFadeStart01", landingFadeStart01);
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "landingFadeExponent", landingFadeExponent);
            SetPrivateField(typeof(TopologyTransitionPostFxProfile), profile, "distortionProfile", distortionProfile?.Clone() ?? TopologyTransitionDistortionProfile.CreateDefault());

            return profile;
        }

        private static object CreateEnumValue(string fullTypeName, int rawValue)
        {
            var enumType = ResolveLoadedType(fullTypeName);
            if (enumType == null || !enumType.IsEnum)
            {
                throw new InvalidOperationException($"Could not resolve enum type '{fullTypeName}'.");
            }

            return Enum.ToObject(enumType, rawValue);
        }

        private static Type ResolveLoadedType(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var resolvedType = assemblies[i].GetType(fullTypeName, throwOnError: false);
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            SetPrivateField(typeof(GameplayCameraTopologyAuthoring), target, fieldName, value);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            var field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                throw new InvalidOperationException(
                    $"Missing serialized field '{fieldName}' on {ownerType.Name}.");
            }

            field.SetValue(target, value);
        }

        private static string TryReadComponentBlock(string source, string marker)
        {
            var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return null;
            }

            var blockStart = source.LastIndexOf("--- !u!114", markerIndex, StringComparison.Ordinal);
            if (blockStart < 0)
            {
                return null;
            }

            var blockEnd = source.IndexOf("\n--- !u!", markerIndex, StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                blockEnd = source.Length;
            }

            return source.Substring(blockStart, blockEnd - blockStart);
        }

        private static string ReadSerializedBlock(string source, string key, int indentation)
        {
            var parentIndentation = new string(' ', indentation);
            var childIndentation = new string(' ', indentation + 2);
            var match = Regex.Match(
                source,
                $@"(?ms)^{Regex.Escape(parentIndentation)}{Regex.Escape(key)}:\s*$\n(?<block>(?:^{Regex.Escape(childIndentation)}.*$\n?)*)",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized block '{key}' was not found.");
            }

            return match.Groups["block"].Value;
        }

        private static bool ReadSerializedBoolValue(string source, string key)
        {
            return ReadSerializedIntValue(source, key) != 0;
        }

        private static int ReadSerializedIntValue(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*(-?\d+)\s*$",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized key '{key}' was not found.");
            }

            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static float ReadSerializedFloatValue(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*([-+]?\d*\.?\d+)\s*$",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized key '{key}' was not found.");
            }

            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static Vector2 ReadSerializedVector2Value(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{x:\s*([-+]?\d*\.?\d+), y:\s*([-+]?\d*\.?\d+)\}}\s*$",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized key '{key}' was not found.");
            }

            return new Vector2(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        private static Vector3 ReadSerializedVector3Value(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{x:\s*([-+]?\d*\.?\d+), y:\s*([-+]?\d*\.?\d+), z:\s*([-+]?\d*\.?\d+)\}}\s*$",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized key '{key}' was not found.");
            }

            return new Vector3(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        private static Color ReadSerializedColorValue(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{r:\s*([-+]?\d*\.?\d+), g:\s*([-+]?\d*\.?\d+), b:\s*([-+]?\d*\.?\d+), a:\s*([-+]?\d*\.?\d+)\}}\s*$",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized key '{key}' was not found.");
            }

            return new Color(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture));
        }

        private static string ReadSerializedObjectGuid(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{fileID:\s*\d+,\s*guid:\s*([0-9a-f]+),\s*type:\s*\d+\}}\s*$",
                RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Serialized object key '{key}' was not found.");
            }

            return match.Groups[1].Value;
        }

        private static UnityEngine.Object LoadAssetByGuid(string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new InvalidOperationException($"Could not resolve asset GUID '{assetGuid}'.");
            }

            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private readonly struct SerializedGameplayCameraTopologyAuthoring
        {
            internal SerializedGameplayCameraTopologyAuthoring(
                bool configureMainCamera,
                bool useAuthoredSceneCameraPose,
                bool useAuthoredSceneCameraLens,
                TopologyRotationVisualMapping topologyRotationVisualMapping,
                TopologyRotationTweenSettings topologyRotationTweenSettings,
                GameplayCameraSettings cameraSettings,
                TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile,
                TopologyTransitionPostFxProfile topologyTransitionPostFxProfile)
            {
                ConfigureMainCamera = configureMainCamera;
                UseAuthoredSceneCameraPose = useAuthoredSceneCameraPose;
                UseAuthoredSceneCameraLens = useAuthoredSceneCameraLens;
                TopologyRotationVisualMapping = topologyRotationVisualMapping;
                TopologyRotationTweenSettings = topologyRotationTweenSettings;
                CameraSettings = cameraSettings;
                TopologyTransitionCameraShakeProfile = topologyTransitionCameraShakeProfile;
                TopologyTransitionPostFxProfile = topologyTransitionPostFxProfile;
            }

            internal bool ConfigureMainCamera { get; }

            internal bool UseAuthoredSceneCameraPose { get; }

            internal bool UseAuthoredSceneCameraLens { get; }

            internal TopologyRotationVisualMapping TopologyRotationVisualMapping { get; }

            internal TopologyRotationTweenSettings TopologyRotationTweenSettings { get; }

            internal GameplayCameraSettings CameraSettings { get; }

            internal TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile { get; }

            internal TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile { get; }
        }
    }
}
