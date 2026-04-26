using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayCameraTopologyAuthoringTests
    {
        [Test]
        [Category("Full")]
        public void GameplayCameraTopologyAuthoring_CreateSnapshot_RoundTripsAllFields_AndClonesMutableAuthoringData()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_CreateSnapshot");
            var authoritativeProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var baselineAuthoringPolicy = new GameplayCameraBaselineAuthoringPolicy
                {
                    UseAuthoredSceneCameraPose = false,
                    UseAuthoredSceneCameraLens = false,
                };
                var cameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 18f,
                    YawDegrees = 22f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                    ManualDistance = 7.5f,
                    FramingPadding = 1.4f,
                    PerspectiveFieldOfView = 58f,
                    NearClipPlane = 0.06f,
                    FarClipPlane = 120f,
                    ClearFlags = CameraClearFlags.Depth,
                    BackgroundColor = new Color(0.2f, 0.3f, 0.4f, 1f),
                };
                var shakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();
                shakeProfile.ImpactDuration01 = 0.14f;
                shakeProfile.LandingLocalRotationAmplitudeDegrees = new Vector3(0.21f, 0.32f, 0.43f);
                var postFxProfile = TopologyTransitionPostFxProfile.Create(
                    authoritativeProfile,
                    MotionBlurMode.CameraAndObjects,
                    MotionBlurQuality.High,
                    maxBlurIntensity: 0.41f,
                    cameraClamp: 0.08f,
                    angularVelocityResponseExponent: 0.72f,
                    landingFadeStart01: 0.81f,
                    landingFadeExponent: 2.7f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(
                        impactStart01: 0.05f,
                        impactDuration01: 0.17f,
                        impactIntensity: -0.24f,
                        landingStart01: 0.71f,
                        landingDuration01: 0.19f,
                        landingIntensity: 0.13f,
                        xMultiplier: 0.83f,
                        yMultiplier: 0.67f,
                        center: new Vector2(0.42f, 0.57f),
                        scale: 1.12f));

                SetAuthoringField(authoring, "configureMainCamera", false);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyRotationVisualMapping),
                    TopologyRotationVisualMapping.ForwardUsesNegativeX);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyRotationTweenSettings),
                    new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.InOutBounce,
                    });
                SetAuthoringField(authoring, "baselineAuthoringPolicy", baselineAuthoringPolicy);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.CameraSettings),
                    cameraSettings);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionCameraShakeProfile),
                    shakeProfile);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionPostFxProfile),
                    postFxProfile);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.ConfigureMainCamera, Is.False);
                AssertBaselineAuthoringPolicy(snapshot.BaselineAuthoringPolicy, baselineAuthoringPolicy);
                Assert.That(snapshot.SharedTuning.TopologyRotationVisualMapping, Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesNegativeX));
                Assert.That(snapshot.SharedTuning.TopologyRotationTweenSettings.Ease, Is.EqualTo(TopologyRotationTweenEase.InOutBounce));
                Assert.That(snapshot.SharedTuning.CameraSettings, Is.Not.SameAs(cameraSettings));
                Assert.That(snapshot.SharedTuning.TopologyTransitionCameraShakeProfile, Is.Not.SameAs(shakeProfile));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile, Is.Not.SameAs(postFxProfile));
                AssertCameraSettings(snapshot.SharedTuning.CameraSettings, cameraSettings);
                Assert.That(snapshot.SharedTuning.TopologyTransitionCameraShakeProfile.ImpactDuration01, Is.EqualTo(0.14f).Within(0.0001f));
                Assert.That(
                    snapshot.SharedTuning.TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees,
                    Is.EqualTo(new Vector3(0.21f, 0.32f, 0.43f)));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.AuthoritativeVolumeProfile, Is.SameAs(authoritativeProfile));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.MotionBlurMode, Is.EqualTo(MotionBlurMode.CameraAndObjects));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.MotionBlurQuality, Is.EqualTo(MotionBlurQuality.High));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.MaxBlurIntensity, Is.EqualTo(0.41f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.DistortionProfile.Center, Is.EqualTo(new Vector2(0.42f, 0.57f)));
            }
            finally
            {
                Object.DestroyImmediate(authoritativeProfile);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraTopologyAuthoring_Validate_RestoresNullProfilesAndCameraSettingsToDefaults()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_Validate");

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                SetAuthoringField(
                    authoring,
                    "inlineSharedTuning",
                    CreateSharedTuningWithNullNestedReferences());

                authoring.Validate();
                var snapshot = authoring.CreateSnapshot();

                AssertBaselineAuthoringPolicy(
                    snapshot.BaselineAuthoringPolicy,
                    GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault());
                AssertCameraSettings(snapshot.SharedTuning.CameraSettings, GameplayCameraSettings.CreateShowcaseDefault());
                Assert.That(snapshot.SharedTuning.TopologyTransitionCameraShakeProfile.ImpactStart01, Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.MaxBlurIntensity, Is.EqualTo(0.3f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PresetMode_LocalBaselinePolicy_RemainsFinalAuthority()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_LocalBaselinePolicy");
            var preset = ScriptableObject.CreateInstance<GameplayCameraTopologyPreset>();

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var presetCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 41f,
                    YawDegrees = 6f,
                    DistanceMode = CameraDistanceMode.Manual,
                    ManualDistance = 18f,
                    FramingPadding = 1.15f,
                    PerspectiveFieldOfView = 47f,
                    NearClipPlane = 0.04f,
                    FarClipPlane = 90f,
                    ClearFlags = CameraClearFlags.Depth,
                    BackgroundColor = new Color(0.1f, 0.2f, 0.3f, 1f),
                };

                SetPresetField(preset, "cameraSettings", presetCameraSettings);
                SetAuthoringField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Preset);
                SetAuthoringField(authoring, "preset", preset);
                SetAuthoringField(
                    authoring,
                    "baselineAuthoringPolicy",
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = false,
                    });

                var snapshot = authoring.CreateSnapshot();

                AssertBaselineAuthoringPolicy(
                    snapshot.BaselineAuthoringPolicy,
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = false,
                    });
                Assert.That(snapshot.SharedTuning.CameraSettings.PitchDegrees, Is.EqualTo(41f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.CameraSettings.PerspectiveFieldOfView, Is.EqualTo(47f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PresetMode_BaselinePolicy_IsCarriedIntoComposedConfiguration()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_BaselinePolicy");
            var preset = ScriptableObject.CreateInstance<GameplayCameraTopologyPreset>();
            var viewCameraObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_ViewCamera");

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var presetCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 38f,
                    YawDegrees = 2f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                    ManualDistance = 9f,
                    FramingPadding = 1.25f,
                    PerspectiveFieldOfView = 52f,
                    NearClipPlane = 0.05f,
                    FarClipPlane = 95f,
                    ClearFlags = CameraClearFlags.SolidColor,
                    BackgroundColor = new Color(0.6f, 0.5f, 0.4f, 1f),
                };

                SetPresetField(preset, "cameraSettings", presetCameraSettings);
                SetAuthoringField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Preset);
                SetAuthoringField(authoring, "preset", preset);
                SetAuthoringField(
                    authoring,
                    "baselineAuthoringPolicy",
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = true,
                    });

                var snapshot = authoring.CreateSnapshot();
                var configuration = new GameplaySceneHostConfiguration();
                var viewCamera = viewCameraObject.AddComponent<Camera>();

                InvokeComposer(configuration, snapshot, snapshot.SharedTuning.CameraSettings, viewCamera);

                AssertBaselineAuthoringPolicy(
                    configuration.CameraBaselineAuthoringPolicy,
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = true,
                    });
                AssertCameraSettings(configuration.CameraSettings, snapshot.SharedTuning.CameraSettings);
                Assert.That(configuration.CameraSettings, Is.Not.SameAs(snapshot.SharedTuning.CameraSettings));
            }
            finally
            {
                Object.DestroyImmediate(viewCameraObject);
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PresetMode_InlineSharedTuning_IsNonAuthoritative()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_InlineSharedTuning");
            var preset = ScriptableObject.CreateInstance<GameplayCameraTopologyPreset>();

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var presetCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 44f,
                    YawDegrees = 13f,
                    DistanceMode = CameraDistanceMode.Manual,
                    ManualDistance = 17f,
                    FramingPadding = 1.05f,
                    PerspectiveFieldOfView = 46f,
                    NearClipPlane = 0.07f,
                    FarClipPlane = 130f,
                    ClearFlags = CameraClearFlags.Depth,
                    BackgroundColor = new Color(0.7f, 0.2f, 0.1f, 1f),
                };
                var presetShake = TopologyTransitionCameraShakeProfile.CreateDefault();
                presetShake.ImpactDuration01 = 0.13f;
                var presetPostFx = TopologyTransitionPostFxProfile.Create(
                    null,
                    MotionBlurMode.CameraOnly,
                    MotionBlurQuality.Medium,
                    maxBlurIntensity: 0.31f,
                    cameraClamp: 0.09f,
                    angularVelocityResponseExponent: 0.73f,
                    landingFadeStart01: 0.79f,
                    landingFadeExponent: 2.6f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(scale: 1.13f));

                SetPresetField(preset, "topologyRotationVisualMapping", TopologyRotationVisualMapping.ForwardUsesNegativeX);
                SetPresetField(
                    preset,
                    "topologyRotationTweenSettings",
                    new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.Linear,
                    });
                SetPresetField(preset, "cameraSettings", presetCameraSettings);
                SetPresetField(preset, "topologyTransitionCameraShakeProfile", presetShake);
                SetPresetField(preset, "topologyTransitionPostFxProfile", presetPostFx);

                SetAuthoringField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Preset);
                SetAuthoringField(authoring, "preset", preset);
                SetAuthoringField(
                    authoring,
                    "baselineAuthoringPolicy",
                    GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault());
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyRotationVisualMapping),
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyRotationTweenSettings),
                    new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.InOutBounce,
                    });
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.CameraSettings),
                    new GameplayCameraSettings
                    {
                        PitchDegrees = 12f,
                        PerspectiveFieldOfView = 78f,
                    });
                var inlineShake = TopologyTransitionCameraShakeProfile.CreateDefault();
                inlineShake.ImpactDuration01 = 0.44f;
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionCameraShakeProfile),
                    inlineShake);
                var inlinePostFx = TopologyTransitionPostFxProfile.Create(
                    null,
                    MotionBlurMode.CameraAndObjects,
                    MotionBlurQuality.High,
                    maxBlurIntensity: 0.92f,
                    cameraClamp: 0.17f,
                    angularVelocityResponseExponent: 0.91f,
                    landingFadeStart01: 0.64f,
                    landingFadeExponent: 1.8f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(scale: 1.44f));
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionPostFxProfile),
                    inlinePostFx);

                var snapshot = authoring.CreateSnapshot();

                AssertBaselineAuthoringPolicy(
                    snapshot.BaselineAuthoringPolicy,
                    GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault());
                Assert.That(snapshot.SharedTuning.TopologyRotationVisualMapping, Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesNegativeX));
                Assert.That(snapshot.SharedTuning.TopologyRotationTweenSettings.Ease, Is.EqualTo(TopologyRotationTweenEase.Linear));
                Assert.That(snapshot.SharedTuning.CameraSettings.PitchDegrees, Is.EqualTo(44f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.CameraSettings.PerspectiveFieldOfView, Is.EqualTo(46f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.TopologyTransitionCameraShakeProfile.ImpactDuration01, Is.EqualTo(0.13f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.MaxBlurIntensity, Is.EqualTo(0.31f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PresetMode_RuntimeSnapshot_UsesPresetSharedValues_NotInlineFallback()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_RuntimeSnapshot");
            var preset = ScriptableObject.CreateInstance<GameplayCameraTopologyPreset>();
            var authoritativeProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var presetPostFx = TopologyTransitionPostFxProfile.Create(
                    authoritativeProfile,
                    MotionBlurMode.CameraOnly,
                    MotionBlurQuality.High,
                    maxBlurIntensity: 0.27f,
                    cameraClamp: 0.06f,
                    angularVelocityResponseExponent: 0.68f,
                    landingFadeStart01: 0.83f,
                    landingFadeExponent: 3.1f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(
                        impactIntensity: -0.22f,
                        scale: 1.08f));

                SetPresetField(
                    preset,
                    "cameraSettings",
                    new GameplayCameraSettings
                    {
                        PitchDegrees = 29f,
                        YawDegrees = 7f,
                        DistanceMode = CameraDistanceMode.Manual,
                        ManualDistance = 15f,
                        FramingPadding = 1.18f,
                        PerspectiveFieldOfView = 51f,
                        NearClipPlane = 0.08f,
                        FarClipPlane = 110f,
                        ClearFlags = CameraClearFlags.Depth,
                        BackgroundColor = new Color(0.12f, 0.24f, 0.36f, 1f),
                    });
                SetPresetField(preset, "topologyTransitionPostFxProfile", presetPostFx);

                SetAuthoringField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Preset);
                SetAuthoringField(authoring, "preset", preset);
                SetAuthoringField(
                    authoring,
                    "baselineAuthoringPolicy",
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = true,
                    });
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.CameraSettings),
                    new GameplayCameraSettings
                    {
                        PitchDegrees = 9f,
                        PerspectiveFieldOfView = 79f,
                        NearClipPlane = 0.2f,
                        FarClipPlane = 20f,
                        BackgroundColor = Color.magenta,
                    });
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionPostFxProfile),
                    TopologyTransitionPostFxProfile.Create(
                        null,
                        MotionBlurMode.CameraAndObjects,
                        MotionBlurQuality.Low,
                        maxBlurIntensity: 0.91f,
                        cameraClamp: 0.18f,
                        angularVelocityResponseExponent: 0.95f,
                        landingFadeStart01: 0.55f,
                        landingFadeExponent: 1.2f,
                        distortionProfile: TopologyTransitionDistortionProfile.Create(scale: 1.39f)));

                var snapshot = authoring.CreateSnapshot();

                AssertBaselineAuthoringPolicy(
                    snapshot.BaselineAuthoringPolicy,
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = true,
                    });
                Assert.That(snapshot.SharedTuning.CameraSettings.PitchDegrees, Is.EqualTo(29f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.CameraSettings.NearClipPlane, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.AuthoritativeVolumeProfile, Is.SameAs(authoritativeProfile));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.MaxBlurIntensity, Is.EqualTo(0.27f).Within(0.0001f));
                Assert.That(snapshot.SharedTuning.TopologyTransitionPostFxProfile.DistortionProfile.Scale, Is.EqualTo(1.08f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(authoritativeProfile);
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PresetMode_CompatibilityGetters_ResolveThroughPresetAuthority()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_CompatibilityGetters");
            var preset = ScriptableObject.CreateInstance<GameplayCameraTopologyPreset>();

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var presetShake = TopologyTransitionCameraShakeProfile.CreateDefault();
                presetShake.ImpactDuration01 = 0.16f;
                var presetPostFx = TopologyTransitionPostFxProfile.Create(
                    null,
                    MotionBlurMode.CameraOnly,
                    MotionBlurQuality.Medium,
                    maxBlurIntensity: 0.45f,
                    cameraClamp: 0.07f,
                    angularVelocityResponseExponent: 0.69f,
                    landingFadeStart01: 0.77f,
                    landingFadeExponent: 2.4f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(scale: 1.07f));

                SetPresetField(
                    preset,
                    "cameraSettings",
                    new GameplayCameraSettings
                    {
                        PitchDegrees = 33f,
                        YawDegrees = 5f,
                        DistanceMode = CameraDistanceMode.Manual,
                        ManualDistance = 14f,
                        FramingPadding = 1.22f,
                        PerspectiveFieldOfView = 48f,
                    });
                SetPresetField(preset, "topologyTransitionCameraShakeProfile", presetShake);
                SetPresetField(preset, "topologyTransitionPostFxProfile", presetPostFx);

                SetAuthoringField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Preset);
                SetAuthoringField(authoring, "preset", preset);
                SetAuthoringField(
                    authoring,
                    "baselineAuthoringPolicy",
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = false,
                    });
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.CameraSettings),
                    new GameplayCameraSettings
                    {
                        PitchDegrees = 11f,
                    });
                var inlineShake = TopologyTransitionCameraShakeProfile.CreateDefault();
                inlineShake.ImpactDuration01 = 0.48f;
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionCameraShakeProfile),
                    inlineShake);
                var inlinePostFx = TopologyTransitionPostFxProfile.Create(
                    null,
                    MotionBlurMode.CameraAndObjects,
                    MotionBlurQuality.High,
                    maxBlurIntensity: 0.87f,
                    cameraClamp: 0.14f,
                    angularVelocityResponseExponent: 0.84f,
                    landingFadeStart01: 0.7f,
                    landingFadeExponent: 1.9f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(scale: 1.28f));
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionPostFxProfile),
                    inlinePostFx);

                var cameraSettings = authoring.GetCameraSettings();
                var baselineAuthoringPolicy = authoring.GetBaselineAuthoringPolicy();
                var shakeProfile = authoring.GetTopologyTransitionCameraShakeProfile();
                var postFxProfile = authoring.GetTopologyTransitionPostFxProfile();

                AssertBaselineAuthoringPolicy(
                    baselineAuthoringPolicy,
                    new GameplayCameraBaselineAuthoringPolicy
                    {
                        UseAuthoredSceneCameraPose = false,
                        UseAuthoredSceneCameraLens = false,
                    });
                Assert.That(cameraSettings.PitchDegrees, Is.EqualTo(33f).Within(0.0001f));
                Assert.That(shakeProfile.ImpactDuration01, Is.EqualTo(0.16f).Within(0.0001f));
                Assert.That(postFxProfile.MaxBlurIntensity, Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(postFxProfile.DistortionProfile.Scale, Is.EqualTo(1.07f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void InlineMode_LocalBaselinePolicy_RemainsIndependentFromCameraSettings()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_InlineMode_LocalBaselinePolicy");

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.CameraSettings),
                    new GameplayCameraSettings
                    {
                        PitchDegrees = 26f,
                    });
                SetAuthoringField(
                    authoring,
                    "baselineAuthoringPolicy",
                    GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault());

                var snapshot = authoring.CreateSnapshot();

                AssertBaselineAuthoringPolicy(
                    snapshot.BaselineAuthoringPolicy,
                    GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault());
                Assert.That(snapshot.SharedTuning.CameraSettings.PitchDegrees, Is.EqualTo(26f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PresetMode_CreateSnapshot_WithoutPreset_ThrowsExplicitAuthorityMessage()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_PresetMode_MissingPreset");

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                SetAuthoringField(authoring, "sourceMode", GameplayCameraTopologySourceMode.Preset);

                var exception = Assert.Throws<InvalidOperationException>(() => authoring.CreateSnapshot());

                Assert.That(exception?.Message, Does.Contain("Inline shared-tuning fallback is not authoritative in Preset mode"));
                Assert.That(exception?.Message, Does.Contain("switch Source Mode to Inline"));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraTopologyAuthoring_GetRequiredValidated_MissingComponent_Throws()
        {
            var rootObject = new GameObject("GameplayCameraTopologyAuthoring_GetRequiredValidated_Missing");

            try
            {
                var owner = rootObject.AddComponent<CombinedGameplayShowcaseInstaller>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => GameplayCameraTopologyAuthoring.GetRequiredValidated(owner));

                Assert.That(exception?.Message, Does.Contain(nameof(GameplayCameraTopologyAuthoring)));
                Assert.That(exception?.Message, Does.Contain(nameof(CombinedGameplayShowcaseInstaller)));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraTopologyConfigurationComposer_ApplyTo_ComposesSnapshotValues_WithClones()
        {
            var rootObject = new GameObject("GameplayCameraTopologyConfigurationComposer_ApplyTo");

            try
            {
                var authoring = rootObject.AddComponent<GameplayCameraTopologyAuthoring>();
                var baselineAuthoringPolicy = new GameplayCameraBaselineAuthoringPolicy
                {
                    UseAuthoredSceneCameraPose = true,
                    UseAuthoredSceneCameraLens = false,
                };
                var resolvedCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 27f,
                    YawDegrees = 4f,
                    DistanceMode = CameraDistanceMode.Manual,
                    ManualDistance = 12f,
                    FramingPadding = 1.3f,
                    PerspectiveFieldOfView = 49f,
                    NearClipPlane = 0.05f,
                    FarClipPlane = 80f,
                    ClearFlags = CameraClearFlags.SolidColor,
                    BackgroundColor = new Color(0.6f, 0.7f, 0.8f, 1f),
                };
                var shakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();
                shakeProfile.LandingDuration01 = 0.21f;
                var postFxProfile = TopologyTransitionPostFxProfile.Create(
                    null,
                    MotionBlurMode.CameraOnly,
                    MotionBlurQuality.Medium,
                    maxBlurIntensity: 0.37f,
                    cameraClamp: 0.04f,
                    angularVelocityResponseExponent: 0.69f,
                    landingFadeStart01: 0.8f,
                    landingFadeExponent: 2.2f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(scale: 1.09f));

                SetAuthoringField(authoring, "configureMainCamera", false);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyRotationVisualMapping),
                    TopologyRotationVisualMapping.ForwardUsesNegativeX);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyRotationTweenSettings),
                    new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.Linear,
                    });
                SetAuthoringField(authoring, "baselineAuthoringPolicy", baselineAuthoringPolicy);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionCameraShakeProfile),
                    shakeProfile);
                SetInlineSharedTuningProperty(
                    authoring,
                    nameof(GameplayCameraTopologySharedTuning.TopologyTransitionPostFxProfile),
                    postFxProfile);

                var configuration = new GameplaySceneHostConfiguration();
                var viewCameraObject = new GameObject("GameplayCameraTopologyConfigurationComposer_ViewCamera");

                try
                {
                    var viewCamera = viewCameraObject.AddComponent<Camera>();
                    var snapshot = authoring.CreateSnapshot();
                    InvokeComposer(configuration, snapshot, resolvedCameraSettings, viewCamera);

                    Assert.That(configuration.SnapViewCameraToTarget, Is.False);
                    AssertBaselineAuthoringPolicy(configuration.CameraBaselineAuthoringPolicy, baselineAuthoringPolicy);
                    Assert.That(configuration.TopologyRotationVisualMapping, Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesNegativeX));
                    Assert.That(configuration.TopologyRotationTween.Ease, Is.EqualTo(TopologyRotationTweenEase.Linear));
                    Assert.That(configuration.ViewCamera, Is.SameAs(viewCamera));
                    Assert.That(configuration.CameraSettings, Is.Not.SameAs(resolvedCameraSettings));
                    AssertCameraSettings(configuration.CameraSettings, resolvedCameraSettings);
                    Assert.That(configuration.TopologyTransitionCameraShakeProfile, Is.Not.SameAs(snapshot.SharedTuning.TopologyTransitionCameraShakeProfile));
                    Assert.That(configuration.TopologyTransitionPostFxProfile, Is.Not.SameAs(snapshot.SharedTuning.TopologyTransitionPostFxProfile));
                    Assert.That(configuration.TopologyTransitionCameraShakeProfile.LandingDuration01, Is.EqualTo(0.21f).Within(0.0001f));
                    Assert.That(configuration.TopologyTransitionPostFxProfile.MotionBlurQuality, Is.EqualTo(MotionBlurQuality.Medium));
                    Assert.That(configuration.TopologyTransitionPostFxProfile.DistortionProfile.Scale, Is.EqualTo(1.09f).Within(0.0001f));
                }
                finally
                {
                    Object.DestroyImmediate(viewCameraObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        private static void InvokeComposer(
            GameplaySceneHostConfiguration configuration,
            GameplayCameraTopologyAuthoringSnapshot snapshot,
            GameplayCameraSettings resolvedCameraSettings,
            Camera viewCamera)
        {
            var composerType = typeof(GameplayShowcaseSceneInstallerBase).Assembly.GetType(
                "Game.Feature.Gameplay.Host.GameplayCameraTopologyConfigurationComposer");
            var applyTo = composerType?.GetMethod("ApplyTo", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(composerType, Is.Not.Null);
            Assert.That(applyTo, Is.Not.Null);
            applyTo.Invoke(null, new object[] { configuration, snapshot, resolvedCameraSettings, viewCamera });
        }

        private static void SetAuthoringField(GameplayCameraTopologyAuthoring authoring, string fieldName, object value)
        {
            SetPrivateField(typeof(GameplayCameraTopologyAuthoring), authoring, fieldName, value);
        }

        private static void SetInlineSharedTuningProperty(
            GameplayCameraTopologyAuthoring authoring,
            string propertyName,
            object value)
        {
            var inlineSharedTuningField = typeof(GameplayCameraTopologyAuthoring).GetField(
                "inlineSharedTuning",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(inlineSharedTuningField, Is.Not.Null, "Missing private field 'inlineSharedTuning'.");

            var inlineSharedTuning =
                (GameplayCameraTopologySharedTuning)inlineSharedTuningField.GetValue(authoring) ??
                GameplayCameraTopologySharedTuning.CreateShowcaseDefault();

            inlineSharedTuningField.SetValue(authoring, inlineSharedTuning);
            SetSharedTuningField(inlineSharedTuning, ToSerializedFieldName(propertyName), value);
        }

        private static void SetPresetField(GameplayCameraTopologyPreset preset, string fieldName, object value)
        {
            var presetField = typeof(GameplayCameraTopologyPreset).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (presetField != null)
            {
                presetField.SetValue(preset, value);
                return;
            }

            var sharedTuningField = typeof(GameplayCameraTopologyPreset).GetField(
                "sharedTuning",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(sharedTuningField, Is.Not.Null, "Missing private field 'sharedTuning'.");

            var sharedTuning =
                (GameplayCameraTopologySharedTuning)sharedTuningField.GetValue(preset) ??
                GameplayCameraTopologySharedTuning.CreateShowcaseDefault();

            sharedTuningField.SetValue(preset, sharedTuning);
            SetSharedTuningField(sharedTuning, fieldName, value);
        }

        private static void SetSharedTuningField(
            GameplayCameraTopologySharedTuning sharedTuning,
            string fieldName,
            object value)
        {
            SetPrivateField(typeof(GameplayCameraTopologySharedTuning), sharedTuning, fieldName, value);
        }

        private static GameplayCameraTopologySharedTuning CreateSharedTuningWithNullNestedReferences()
        {
            var sharedTuning = GameplayCameraTopologySharedTuning.CreateShowcaseDefault();
            SetSharedTuningField(sharedTuning, "cameraSettings", null);
            SetSharedTuningField(sharedTuning, "topologyTransitionCameraShakeProfile", null);
            SetSharedTuningField(sharedTuning, "topologyTransitionPostFxProfile", null);
            return sharedTuning;
        }

        private static string ToSerializedFieldName(string propertyName)
        {
            Assert.That(string.IsNullOrWhiteSpace(propertyName), Is.False);
            return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            var field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{ownerType.Name}'.");
            field.SetValue(target, value);
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

        private static void AssertBaselineAuthoringPolicy(
            GameplayCameraBaselineAuthoringPolicy actual,
            GameplayCameraBaselineAuthoringPolicy expected)
        {
            Assert.That(actual.UseAuthoredSceneCameraPose, Is.EqualTo(expected.UseAuthoredSceneCameraPose));
            Assert.That(actual.UseAuthoredSceneCameraLens, Is.EqualTo(expected.UseAuthoredSceneCameraLens));
        }
    }

    public sealed class GameplayCameraTopologyAuthoringExtractionArchitectureTests
    {
        private const string BaselinePolicyRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraBaselineAuthoringPolicy.cs";
        private const string AuthoringRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologyAuthoring.cs";
        private const string SharedTuningRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologySharedTuning.cs";
        private const string LegacyInlineSharedTuningRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologyInlineSharedTuning.cs";
        private const string SourceModeRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologySourceMode.cs";
        private const string PresetRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologyPreset.cs";
        private const string ComposerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologyConfigurationComposer.cs";
        private const string InstallerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs";
        private const string AuthoringEditorRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Editor/GameplayCameraTopologyAuthoringEditor.cs";
        private const string CombinedGameplayShowcasePresetAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Camera/Presets/GameplayCameraTopologyPreset_CombinedGameplayShowcase.asset";
        private const string TutorialScenePresetAssetPath =
            "Assets/_Features/Stages/Stage_TutorialScene/Camera/Presets/GameplayCameraTopologyPreset_TutorialScene.asset";
        private const string CombinedGameplayShowcaseInstallerMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.CombinedGameplayShowcaseInstaller";
        private const string GameplayCameraTopologyAuthoringMarker =
            "m_EditorClassIdentifier: Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.GameplayCameraTopologyAuthoring";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/CombinedGameplayShowcase.unity",
            "Assets/Scenes/TutorialScene.unity",
            "Assets/Scenes/UIAudioScene.unity",
        };

        private static readonly string[] ForbiddenInstallerFieldFragments =
        {
            "[SerializeField] private bool configureMainCamera = true;",
            "[SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX;",
            "[SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault();",
            "[SerializeField] private GameplayCameraSettings cameraSettings = GameplayCameraSettings.CreateShowcaseDefault();",
            "[SerializeField] private TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();",
            "[SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault();",
        };

        [Test]
        [Category("Extended")]
        public void GameplayCameraTopologyAuthoring_Source_Exists_WithCanonicalPublicSurface()
        {
            var source = ReadRepoFile(AuthoringRelativePath);

            Assert.That(source, Does.Contain("public readonly struct GameplayCameraTopologyAuthoringSnapshot"));
            Assert.That(source, Does.Contain("public sealed class GameplayCameraTopologyAuthoring : MonoBehaviour"));
            Assert.That(source, Does.Contain("public GameplayCameraTopologyAuthoringSnapshot CreateSnapshot()"));
            Assert.That(source, Does.Contain("public void Validate()"));
            Assert.That(source, Does.Contain("public GameplayCameraBaselineAuthoringPolicy GetBaselineAuthoringPolicy()"));
            Assert.That(source, Does.Contain("public static GameplayCameraTopologyAuthoring GetRequiredValidated(Component owner)"));
            Assert.That(source, Does.Contain("public GameplayCameraTopologySharedTuning SharedTuning { get; }"));
            Assert.That(source, Does.Contain("[SerializeField] private GameplayCameraTopologySharedTuning inlineSharedTuning"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private GameplayCameraSettings cameraSettings"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private TopologyTransitionCameraShakeProfile topologyTransitionCameraShakeProfile"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private TopologyTransitionPostFxProfile topologyTransitionPostFxProfile"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraBaselineAuthoringPolicy_Source_Exists_WithCanonicalPublicSurface()
        {
            var source = ReadRepoFile(BaselinePolicyRelativePath);

            Assert.That(source, Does.Contain("public struct GameplayCameraBaselineAuthoringPolicy"));
            Assert.That(source, Does.Contain("public bool UseAuthoredSceneCameraPose;"));
            Assert.That(source, Does.Contain("public bool UseAuthoredSceneCameraLens;"));
            Assert.That(source, Does.Contain("public static GameplayCameraBaselineAuthoringPolicy CreateRuntimeDefault()"));
            Assert.That(source, Does.Contain("public static GameplayCameraBaselineAuthoringPolicy CreateShowcaseDefault()"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraTopologySharedTuning_Source_Exists_WithCanonicalPublicSurface()
        {
            var source = ReadRepoFile(SharedTuningRelativePath);

            Assert.That(source, Does.Contain("public sealed class GameplayCameraTopologySharedTuning"));
            Assert.That(source, Does.Contain("public void Validate()"));
            Assert.That(source, Does.Contain("public GameplayCameraTopologySharedTuning Clone()"));
            Assert.That(source, Does.Contain("public static GameplayCameraTopologySharedTuning CreateRuntimeDefault()"));
            Assert.That(source, Does.Contain("public static GameplayCameraTopologySharedTuning CreateShowcaseDefault()"));
            Assert.That(source, Does.Contain("[SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping"));
            Assert.That(source, Does.Contain("[SerializeField] private GameplayCameraSettings cameraSettings"));
            Assert.That(source, Does.Contain("public GameplayCameraSettings CameraSettings => cameraSettings;"));
        }

        [Test]
        [Category("Extended")]
        public void LegacyGameplayCameraTopologyInlineSharedTuning_File_IsAbsent()
        {
            Assert.That(File.Exists(GetAbsolutePath(LegacyInlineSharedTuningRelativePath)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraTopologyPreset_Source_Exists_WithCanonicalPublicSurface()
        {
            var presetSource = ReadRepoFile(PresetRelativePath);
            var sourceModeSource = ReadRepoFile(SourceModeRelativePath);

            Assert.That(sourceModeSource, Does.Contain("public enum GameplayCameraTopologySourceMode"));
            Assert.That(sourceModeSource, Does.Contain("Inline = 0"));
            Assert.That(sourceModeSource, Does.Contain("Preset = 1"));
            Assert.That(presetSource, Does.Contain("public sealed class GameplayCameraTopologyPreset : ScriptableObject"));
            Assert.That(presetSource, Does.Contain("[SerializeField] private GameplayCameraTopologySharedTuning sharedTuning"));
            Assert.That(presetSource, Does.Contain("public GameplayCameraTopologySharedTuning CreateSnapshot()"));
            Assert.That(presetSource, Does.Contain("public void Validate()"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraTopologyAuthoring_Source_UsesExplicitPresetAuthorityResolutionRule()
        {
            var source = ReadRepoFile(AuthoringRelativePath);

            Assert.That(source, Does.Contain("private GameplayCameraTopologySharedTuning ResolveSharedTuning()"));
            Assert.That(source, Does.Contain("GameplayCameraTopologySourceMode.Inline => inlineSharedTuning.Clone()"));
            Assert.That(source, Does.Contain("GameplayCameraTopologySourceMode.Preset => ResolveRequiredPreset().CreateSnapshot()"));
            Assert.That(source, Does.Contain("Preset mode never reads inline shared tuning"));
            Assert.That(source, Does.Contain("inlineSharedTuning.Validate();"));
            Assert.That(source, Does.Contain("baselineAuthoringPolicy"));
            Assert.That(source, Does.Contain("public GameplayCameraBaselineAuthoringPolicy BaselineAuthoringPolicy { get; }"));
            Assert.That(source, Does.Contain("public GameplayCameraBaselineAuthoringPolicy GetBaselineAuthoringPolicy()"));
            Assert.That(source, Does.Not.Contain("ResolveEffectiveUseAuthoredSceneCameraPose("));
            Assert.That(source, Does.Not.Contain("ResolveEffectiveUseAuthoredSceneCameraLens("));
            Assert.That(source, Does.Contain("Inline shared-tuning fallback is not authoritative in Preset mode"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraTopologyAuthoringEditor_Source_HidesInlineSharedTuning_InPresetMode()
        {
            var source = ReadRepoFile(AuthoringEditorRelativePath);

            Assert.That(source, Does.Contain("[CustomEditor(typeof(GameplayCameraTopologyAuthoring))]"));
            Assert.That(source, Does.Contain("serializedObject.FindProperty(\"inlineSharedTuning\")"));
            Assert.That(source, Does.Contain("GameplayCameraTopologySourceMode.Inline"));
            Assert.That(source, Does.Contain("EditorGUILayout.PropertyField(_inlineSharedTuningProperty, includeChildren: true);"));
            Assert.That(source, Does.Contain("EditorGUILayout.HelpBox("));
            Assert.That(source, Does.Contain("not authoritative in Preset mode"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneInstallerBase_Source_NoLongerDeclaresLegacyCameraTopologyFields()
        {
            var source = ReadRepoFile(InstallerRelativePath);

            foreach (var forbiddenFragment in ForbiddenInstallerFieldFragments)
            {
                Assert.That(
                    source.Contains(forbiddenFragment),
                    Is.False,
                    $"Installer must not retain legacy camera topology raw field '{forbiddenFragment}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayShowcaseSceneInstallerBase_Source_RetainsGetterWrappers_And_ComposesThroughAuthoring()
        {
            var source = ReadRepoFile(InstallerRelativePath);

            Assert.That(source, Does.Contain("public GameplayCameraSettings GetCameraSettings()"));
            Assert.That(source, Does.Contain("public GameplayCameraBaselineAuthoringPolicy GetBaselineAuthoringPolicy()"));
            Assert.That(source, Does.Contain("public TopologyTransitionCameraShakeProfile GetTopologyTransitionCameraShakeProfile()"));
            Assert.That(source, Does.Contain("public TopologyTransitionPostFxProfile GetTopologyTransitionPostFxProfile()"));
            Assert.That(source, Does.Contain("ResolveCameraTopologyAuthoring().GetCameraSettings();"));
            Assert.That(source, Does.Contain("ResolveCameraTopologyAuthoring().GetBaselineAuthoringPolicy();"));
            Assert.That(source, Does.Contain("ResolveCameraTopologyAuthoring().GetTopologyTransitionCameraShakeProfile();"));
            Assert.That(source, Does.Contain("ResolveCameraTopologyAuthoring().GetTopologyTransitionPostFxProfile();"));
            Assert.That(source, Does.Contain("GameplayCameraTopologyAuthoring.GetRequiredValidated(this)"));
            Assert.That(source, Does.Contain("GameplayCameraTopologyConfigurationComposer.ApplyTo("));
            Assert.That(source, Does.Contain("GameplayShowcaseSceneScaffold.EnsureInstallerScaffold("));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraTopologyConfigurationComposer_Source_ComposesCanonicalHostFields()
        {
            var source = ReadRepoFile(ComposerRelativePath);

            Assert.That(source, Does.Contain("configuration.SnapViewCameraToTarget = snapshot.ConfigureMainCamera;"));
            Assert.That(source, Does.Contain("configuration.CameraBaselineAuthoringPolicy = snapshot.BaselineAuthoringPolicy;"));
            Assert.That(source, Does.Contain("var sharedTuning = snapshot.SharedTuning ?? GameplayCameraTopologySharedTuning.CreateShowcaseDefault();"));
            Assert.That(source, Does.Contain("configuration.TopologyRotationVisualMapping = sharedTuning.TopologyRotationVisualMapping;"));
            Assert.That(source, Does.Contain("configuration.TopologyRotationTween = sharedTuning.TopologyRotationTweenSettings;"));
            Assert.That(source, Does.Contain("cameraSettings?.Clone()"));
            Assert.That(source, Does.Not.Contain("resolvedCameraSettings?.Clone()"));
            Assert.That(source, Does.Contain("sharedTuning.CameraSettings?.Clone()"));
            Assert.That(source, Does.Contain("configuration.TopologyTransitionCameraShakeProfile ="));
            Assert.That(source, Does.Contain("configuration.TopologyTransitionPostFxProfile ="));
            Assert.That(source, Does.Contain("configuration.ViewCamera = viewCamera;"));
        }

        [Test]
        [Category("Full")]
        public void CanonicalShowcaseScenes_MoveCameraTopologyFields_ToCoLocatedAuthoringBlock()
        {
            foreach (var scenePath in ScenePaths)
            {
                var sceneText = ReadRepoFile(scenePath);
                var installerBlock = ReadSceneComponentBlock(sceneText, CombinedGameplayShowcaseInstallerMarker);
                var authoringBlock = ReadSceneComponentBlock(sceneText, GameplayCameraTopologyAuthoringMarker);
                var inlineSharedTuningBlock = ReadSerializedBlock(authoringBlock, "inlineSharedTuning", 2);
                var cameraSettingsBlock = ReadSerializedBlock(inlineSharedTuningBlock, "cameraSettings", 4);

                Assert.That(CountMatches(sceneText, Regex.Escape(GameplayCameraTopologyAuthoringMarker)), Is.EqualTo(1));
                StringAssert.DoesNotContain("configureMainCamera:", installerBlock);
                StringAssert.DoesNotContain("topologyRotationVisualMapping:", installerBlock);
                StringAssert.DoesNotContain("topologyRotationTweenSettings:", installerBlock);
                StringAssert.DoesNotContain("cameraSettings:", installerBlock);
                StringAssert.DoesNotContain("topologyTransitionCameraShakeProfile:", installerBlock);
                StringAssert.DoesNotContain("topologyTransitionPostFxProfile:", installerBlock);
                StringAssert.Contains("configureMainCamera:", authoringBlock);
                StringAssert.Contains("sourceMode:", authoringBlock);
                StringAssert.Contains("preset:", authoringBlock);
                StringAssert.Contains("baselineAuthoringPolicy:", authoringBlock);
                StringAssert.Contains("inlineSharedTuning:", authoringBlock);
                StringAssert.DoesNotContain("useAuthoredSceneCameraPose:", authoringBlock);
                StringAssert.DoesNotContain("useAuthoredSceneCameraLens:", authoringBlock);
                StringAssert.DoesNotContain("\n  topologyRotationVisualMapping:", authoringBlock);
                StringAssert.DoesNotContain("\n  topologyRotationTweenSettings:", authoringBlock);
                StringAssert.DoesNotContain("\n  cameraSettings:", authoringBlock);
                StringAssert.DoesNotContain("UseAuthoredSceneCameraPose:", cameraSettingsBlock);
                StringAssert.DoesNotContain("UseAuthoredSceneCameraLens:", cameraSettingsBlock);
                StringAssert.DoesNotContain("\n  topologyTransitionCameraShakeProfile:", authoringBlock);
                StringAssert.DoesNotContain("\n  topologyTransitionPostFxProfile:", authoringBlock);
                StringAssert.Contains("topologyRotationVisualMapping:", inlineSharedTuningBlock);
                StringAssert.Contains("topologyRotationTweenSettings:", inlineSharedTuningBlock);
                StringAssert.Contains("cameraSettings:", inlineSharedTuningBlock);
                StringAssert.Contains("topologyTransitionCameraShakeProfile:", inlineSharedTuningBlock);
                StringAssert.Contains("topologyTransitionPostFxProfile:", inlineSharedTuningBlock);
            }
        }

        [Test]
        [Category("Full")]
        public void CanonicalShowcaseScenes_UsePresetModeAndExpectedPresetReferences()
        {
            var combinedPresetGuid = AssetDatabase.AssetPathToGUID(CombinedGameplayShowcasePresetAssetPath);
            var tutorialPresetGuid = AssetDatabase.AssetPathToGUID(TutorialScenePresetAssetPath);

            foreach (var scenePath in ScenePaths)
            {
                var authoringBlock = ReadSceneComponentBlock(ReadRepoFile(scenePath), GameplayCameraTopologyAuthoringMarker);
                var baselineAuthoringPolicyBlock = ReadSerializedBlock(authoringBlock, "baselineAuthoringPolicy", 2);
                var expectedPresetGuid = scenePath == "Assets/Scenes/CombinedGameplayShowcase.unity"
                    ? combinedPresetGuid
                    : tutorialPresetGuid;

                Assert.That(ReadSerializedIntValue(authoringBlock, "sourceMode"), Is.EqualTo((int)GameplayCameraTopologySourceMode.Preset));
                Assert.That(ReadSerializedObjectGuid(authoringBlock, "preset"), Is.EqualTo(expectedPresetGuid));
                Assert.That(ReadSerializedIntValue(baselineAuthoringPolicyBlock, "UseAuthoredSceneCameraPose"), Is.EqualTo(1));
                Assert.That(ReadSerializedIntValue(baselineAuthoringPolicyBlock, "UseAuthoredSceneCameraLens"), Is.EqualTo(1));
            }
        }

        [Test]
        [Category("Full")]
        public void TutorialStageScenes_ShareCameraTopologyPresetReference()
        {
            var tutorialBlock = ReadSceneComponentBlock(ReadRepoFile("Assets/Scenes/TutorialScene.unity"), GameplayCameraTopologyAuthoringMarker);
            var uiAudioBlock = ReadSceneComponentBlock(ReadRepoFile("Assets/Scenes/UIAudioScene.unity"), GameplayCameraTopologyAuthoringMarker);
            var combinedBlock = ReadSceneComponentBlock(ReadRepoFile("Assets/Scenes/CombinedGameplayShowcase.unity"), GameplayCameraTopologyAuthoringMarker);
            var tutorialPresetGuid = ReadSerializedObjectGuid(tutorialBlock, "preset");
            var uiAudioPresetGuid = ReadSerializedObjectGuid(uiAudioBlock, "preset");
            var combinedPresetGuid = ReadSerializedObjectGuid(combinedBlock, "preset");

            Assert.That(tutorialPresetGuid, Is.EqualTo(uiAudioPresetGuid));
            Assert.That(combinedPresetGuid, Is.Not.EqualTo(tutorialPresetGuid));
        }

        [Test]
        [Category("Full")]
        public void CameraTopologyPresetAssets_SerializeCameraSettingsAsTuningOnly()
        {
            var combinedPresetText = ReadRepoFile(CombinedGameplayShowcasePresetAssetPath);
            var tutorialPresetText = ReadRepoFile(TutorialScenePresetAssetPath);
            var combinedSharedTuningBlock = ReadSerializedBlock(combinedPresetText, "sharedTuning", 2);
            var tutorialSharedTuningBlock = ReadSerializedBlock(tutorialPresetText, "sharedTuning", 2);
            var combinedPresetCameraSettings = ReadSerializedBlock(combinedSharedTuningBlock, "cameraSettings", 4);
            var tutorialPresetCameraSettings = ReadSerializedBlock(tutorialSharedTuningBlock, "cameraSettings", 4);

            StringAssert.Contains("sharedTuning:", combinedPresetText);
            StringAssert.Contains("sharedTuning:", tutorialPresetText);
            StringAssert.DoesNotContain("\n  cameraSettings:", combinedPresetText);
            StringAssert.DoesNotContain("\n  cameraSettings:", tutorialPresetText);
            StringAssert.DoesNotContain("UseAuthoredSceneCameraPose:", combinedPresetCameraSettings);
            StringAssert.DoesNotContain("UseAuthoredSceneCameraLens:", combinedPresetCameraSettings);
            StringAssert.DoesNotContain("UseAuthoredSceneCameraPose:", tutorialPresetCameraSettings);
            StringAssert.DoesNotContain("UseAuthoredSceneCameraLens:", tutorialPresetCameraSettings);
        }

        [Test]
        [Category("Full")]
        public void ShowcaseScenes_PreserveSerializedConfigureMainCameraMeaning()
        {
            foreach (var scenePath in ScenePaths)
            {
                var authoringBlock = ReadSceneComponentBlock(ReadRepoFile(scenePath), GameplayCameraTopologyAuthoringMarker);
                Assert.That(ReadSerializedIntValue(authoringBlock, "configureMainCamera"), Is.EqualTo(0));

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                try
                {
                    var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                    Assert.That(installer, Is.Not.Null);
                    var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
                    Assert.That(authoring, Is.Not.Null);

                    var serializedObject = new SerializedObject(authoring);
                    var configureMainCameraProperty = serializedObject.FindProperty("configureMainCamera");

                    Assert.That(configureMainCameraProperty, Is.Not.Null);
                    serializedObject.Update();
                    Assert.That(configureMainCameraProperty.boolValue, Is.False);
                    Assert.That(BuildConfiguration(installer).SnapViewCameraToTarget, Is.False);
                    Assert.That(installer.GetComponents<GameplayCameraTopologyAuthoring>().Length, Is.EqualTo(1));
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        [Test]
        [Category("Full")]
        public void ShowcaseScenes_PreserveSerializedTopologyRotationVisualMappingMeaning()
        {
            foreach (var scenePath in ScenePaths)
            {
                var authoringBlock = ReadSceneComponentBlock(ReadRepoFile(scenePath), GameplayCameraTopologyAuthoringMarker);
                var inlineSharedTuningBlock = ReadSerializedBlock(authoringBlock, "inlineSharedTuning", 2);
                Assert.That(
                    ReadSerializedIntValue(inlineSharedTuningBlock, "topologyRotationVisualMapping"),
                    Is.EqualTo((int)TopologyRotationVisualMapping.ForwardUsesPositiveX));

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                try
                {
                    var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                    Assert.That(installer, Is.Not.Null);
                    var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
                    Assert.That(authoring, Is.Not.Null);

                    var serializedObject = new SerializedObject(authoring);
                    var mappingProperty = serializedObject.FindProperty("inlineSharedTuning.topologyRotationVisualMapping");

                    Assert.That(mappingProperty, Is.Not.Null);
                    serializedObject.Update();
                    Assert.That(mappingProperty.enumValueIndex, Is.EqualTo((int)TopologyRotationVisualMapping.ForwardUsesPositiveX));
                    Assert.That(
                        BuildConfiguration(installer).TopologyRotationVisualMapping,
                        Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesPositiveX));
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        [Test]
        [Category("Full")]
        public void ShowcaseScenes_PreserveSerializedTopologyRotationTweenEaseMeaning()
        {
            foreach (var scenePath in ScenePaths)
            {
                var authoringBlock = ReadSceneComponentBlock(ReadRepoFile(scenePath), GameplayCameraTopologyAuthoringMarker);
                var inlineSharedTuningBlock = ReadSerializedBlock(authoringBlock, "inlineSharedTuning", 2);
                var tweenSettingsBlock = ReadSerializedBlock(inlineSharedTuningBlock, "topologyRotationTweenSettings", 4);
                Assert.That(
                    ReadSerializedIntValue(tweenSettingsBlock, "Ease"),
                    Is.EqualTo((int)TopologyRotationTweenEase.InOutCubic));

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                try
                {
                    var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                    Assert.That(installer, Is.Not.Null);
                    var authoring = installer.GetComponent<GameplayCameraTopologyAuthoring>();
                    Assert.That(authoring, Is.Not.Null);

                    var serializedObject = new SerializedObject(authoring);
                    var easeProperty = serializedObject.FindProperty("inlineSharedTuning.topologyRotationTweenSettings.Ease");

                    Assert.That(easeProperty, Is.Not.Null);
                    serializedObject.Update();
                    Assert.That(easeProperty.enumValueIndex, Is.EqualTo((int)TopologyRotationTweenEase.InOutCubic));
                    Assert.That(BuildConfiguration(installer).TopologyRotationTween.Ease, Is.EqualTo(TopologyRotationTweenEase.InOutCubic));
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static GameplaySceneHostConfiguration BuildConfiguration(CombinedGameplayShowcaseInstaller installer)
        {
            var buildInitialGameplayState = typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                "BuildInitialGameplayState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var createConfiguration = typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                "CreateConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { buildInitialGameplayState?.ReturnType, typeof(GameplayCameraSettings) },
                modifiers: null);

            Assert.That(buildInitialGameplayState, Is.Not.Null);
            Assert.That(createConfiguration, Is.Not.Null);

            StageLaunchContextStore.Clear();
            StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("stage-1-1"));

            try
            {
                var initialGameplayState = buildInitialGameplayState.Invoke(installer, null);
                return (GameplaySceneHostConfiguration)createConfiguration.Invoke(
                    installer,
                    new[] { initialGameplayState, installer.GetCameraSettings() });
            }
            finally
            {
                StageLaunchContextStore.Clear();
            }
        }

        private static string ReadSceneComponentBlock(string sceneText, string marker)
        {
            var markerIndex = sceneText.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0), $"Missing component block '{marker}'.");

            var blockStart = sceneText.LastIndexOf("--- !u!114", markerIndex, StringComparison.Ordinal);
            Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), "Missing component block start.");

            var blockEnd = sceneText.IndexOf("\n--- !u!", markerIndex, StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                blockEnd = sceneText.Length;
            }

            return sceneText.Substring(blockStart, blockEnd - blockStart);
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

        private static int ReadSerializedIntValue(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*(-?\d+)\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized key '{key}' was not found.");
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static string ReadSerializedObjectGuid(string source, string key)
        {
            var match = Regex.Match(
                source,
                $@"(?m)^\s+{Regex.Escape(key)}:\s*\{{fileID:\s*\d+,\s*guid:\s*([0-9a-f]+),\s*type:\s*\d+\}}\s*$",
                RegexOptions.CultureInvariant);

            Assert.That(match.Success, Is.True, $"Serialized object key '{key}' was not found.");
            return match.Groups[1].Value;
        }

        private static int CountMatches(string source, string pattern)
        {
            return Regex.Matches(source, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
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

    public sealed class TopologyViewCameraAuthorityDocumentationTests
    {
        private const string AuthorityDocRelativePath =
            "Docs/Architecture/Topology-View-Camera-Canonical-Ownership-2026-04-24.md";

        [Test]
        [Category("Extended")]
        public void TopologyViewCameraAuthorityDoc_StatesPresetAndSceneLocalAuthorityModel()
        {
            var doc = ReadRepoFile(AuthorityDocRelativePath);

            Assert.That(doc, Does.Contain("`GameplayCameraTopologyAuthoring` is the scene-local authority entrypoint."));
            Assert.That(doc, Does.Contain("`GameplayCameraTopologyPreset` owns stage-scoped shared tuning only."));
            Assert.That(doc, Does.Contain("`GameplayCameraSettings` is shared tuning only."));
            Assert.That(doc, Does.Contain("`GameplayCameraBaselineAuthoringPolicy` is the dedicated scene-local authored-baseline policy type."));
            Assert.That(doc, Does.Contain("`inlineSharedTuning` on `GameplayCameraTopologyAuthoring` is the explicit inline-mode field using the canonical shared-tuning schema."));
            Assert.That(doc, Does.Contain("`configureMainCamera` remains local."));
            Assert.That(doc, Does.Contain("`UseAuthoredSceneCameraPose/Lens` live in scene-local baseline policy, not `GameplayCameraSettings`."));
            Assert.That(doc, Does.Contain("`Preset` mode ignores inline shared-tuning values for runtime snapshot resolution."));
            Assert.That(doc, Does.Not.Contain("future cleanup candidate A"));
            Assert.That(doc, Does.Contain("Candidate A is complete:"));
            Assert.That(doc, Does.Contain("Candidate B is complete:"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
