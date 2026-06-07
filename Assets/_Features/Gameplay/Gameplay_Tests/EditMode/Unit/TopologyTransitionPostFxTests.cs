using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Tests.Unit
{
    /*
     * Phase 5A post-fx extraction guardrail:
     * - representative blur/distortion samples freeze extraction parity only
     * - these samples are not long-term tuning law
     * - runtime clone assertions protect authoritative asset immutability during extraction
     */
    public sealed class TopologyTransitionPostFxTests
    {
        private const string UiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string CombinedPresetAssetPath =
            StageContentPaths.SharedTopologyPresentationRoot + "/CameraProfiles/GameplayCameraTopologyPreset_CampaignMainFastPostFx.asset";
        private const string QualityPostFxPresetAssetPath =
            StageContentPaths.SharedTopologyPresentationRoot + "/CameraProfiles/GameplayCameraTopologyPreset_CampaignMainQualityPostFx.asset";
        private const string AuthoritativeVolumeProfileAssetPath = "Assets/_Post/_Post Profile.asset";
        private const string DeprecatedVolumeProfileGuid = "eda47df5b85f4f249abf7abd73db2cb2";

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_TopologyTransitionPostFx_CreatesRuntimeVolumeCloneFromAuthoritativeAsset()
        {
            var hostObject = new GameObject("GameplaySceneHost_TopologyTransitionPostFx");
            var cameraObject = new GameObject("TopologyTransitionPostFxCamera");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("TopologyTransitionPostFx_PlayerPrefab");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                var authoritativeProfile =
                    AssetDatabase.LoadAssetAtPath<VolumeProfile>(AuthoritativeVolumeProfileAssetPath);

                Assert.That(authoritativeProfile, Is.Not.Null);
                Assert.That(authoritativeProfile.TryGet(out MotionBlur authoritativeMotionBlur), Is.True);
                Assert.That(authoritativeProfile.TryGet(out LensDistortion authoritativeLensDistortion), Is.True);
                var authoritativeMotionBlurMode = authoritativeMotionBlur.mode.value;
                var authoritativeMotionBlurQuality = authoritativeMotionBlur.quality.value;
                var authoritativeMotionBlurIntensity = authoritativeMotionBlur.intensity.value;
                var authoritativeLensDistortionIntensity = authoritativeLensDistortion.intensity.value;
                var authoritativeLensDistortionScale = authoritativeLensDistortion.scale.value;

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                            },
                        },
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        PushMotionDurationSeconds = 0.2f,
                        RepeatedMoveIntervalSeconds = 0.2f,
                        SimulationTicksPerSecond = 60,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = System.Array.Empty<IEntityLogic>(),
                        TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.Create(authoritativeProfile),
                        ViewCamera = outputCamera,
                    });

                var controller = host.GetComponent<TopologyTransitionPostFxController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.OutputCamera, Is.SameAs(outputCamera));
                Assert.That(controller.RuntimeVolume, Is.Not.Null);
                Assert.That(controller.RuntimeVolume.enabled, Is.True);
                Assert.That(controller.RuntimeVolumeProfile, Is.Not.Null);
                Assert.That(controller.RuntimeVolumeProfile, Is.Not.SameAs(authoritativeProfile));
                Assert.That(controller.RuntimeVolume.sharedProfile, Is.SameAs(authoritativeProfile));
                Assert.That(controller.RuntimeVolume.HasInstantiatedProfile(), Is.True);
                Assert.That(controller.RuntimeVolume.profile, Is.SameAs(controller.RuntimeVolumeProfile));
                Assert.That(controller.MotionBlurOverride, Is.Not.Null);
                Assert.That(controller.LensDistortionOverride, Is.Not.Null);
                Assert.That(controller.MotionBlurOverride.mode.value, Is.EqualTo(MotionBlurMode.CameraOnly));
                Assert.That(controller.MotionBlurOverride.quality.value, Is.EqualTo(MotionBlurQuality.Low));
                Assert.That(controller.MotionBlurOverride.intensity.value, Is.EqualTo(0f));
                Assert.That(controller.LensDistortionOverride.intensity.value, Is.EqualTo(0f));
                Assert.That(controller.LensDistortionOverride.scale.value, Is.EqualTo(1.05f).Within(0.0001f));
                Assert.That(outputCamera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);

                Assert.That(authoritativeMotionBlur.mode.value, Is.EqualTo(authoritativeMotionBlurMode));
                Assert.That(authoritativeMotionBlur.quality.value, Is.EqualTo(authoritativeMotionBlurQuality));
                Assert.That(authoritativeMotionBlur.intensity.value, Is.EqualTo(authoritativeMotionBlurIntensity));
                Assert.That(authoritativeLensDistortion.intensity.value, Is.EqualTo(authoritativeLensDistortionIntensity));
                Assert.That(authoritativeLensDistortion.scale.value, Is.EqualTo(authoritativeLensDistortionScale));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerViewPrefab.gameObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxController_ExtractionParityFreeze_RepresentativeBlurEnvelopeSamples_PreserveAngularVelocityAndLandingFade()
        {
            var controllerObject = new GameObject("TopologyTransitionPostFxController");
            var cameraObject = new GameObject("TopologyTransitionPostFxOutputCamera");
            var sourceProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var controller = controllerObject.AddComponent<TopologyTransitionPostFxController>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                sourceProfile.Add<MotionBlur>(overrides: true);

                controller.Initialize(
                    TopologyTransitionPostFxProfile.Create(
                        sourceProfile,
                        maxBlurIntensity: 0.5f,
                        angularVelocityResponseExponent: 0.65f,
                        landingFadeStart01: 0.8f,
                        landingFadeExponent: 3f),
                    outputCamera);

                controller.Apply(
                    new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: 0.5f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: CubeRotationKind.Forward,
                        durationSeconds: 0.4f,
                        presentedVisualRotation: Quaternion.identity,
                        angularVelocityNormalized: 0.5f));
                var partialVelocityIntensity = controller.MotionBlurOverride.intensity.value;

                controller.Apply(
                    new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: 0.5f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: CubeRotationKind.Forward,
                        durationSeconds: 0.4f,
                        presentedVisualRotation: Quaternion.identity,
                        angularVelocityNormalized: 1f));
                var midIntensity = controller.MotionBlurOverride.intensity.value;

                controller.Apply(
                    new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: 0.9f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: CubeRotationKind.Forward,
                        durationSeconds: 0.4f,
                        presentedVisualRotation: Quaternion.identity,
                        angularVelocityNormalized: 1f));
                var landingIntensity = controller.MotionBlurOverride.intensity.value;

                controller.Apply(TopologyTransitionVisualState.Inactive(new CubeTopologyState(FaceId.Front), Quaternion.identity));
                var inactiveIntensity = controller.MotionBlurOverride.intensity.value;

                var expectedPartialVelocityIntensity = 0.5f * Mathf.Pow(0.5f, 0.65f);

                Assert.That(partialVelocityIntensity, Is.EqualTo(expectedPartialVelocityIntensity).Within(0.0001f));
                Assert.That(partialVelocityIntensity, Is.GreaterThan(0.25f));
                Assert.That(midIntensity, Is.GreaterThan(0f));
                Assert.That(landingIntensity, Is.GreaterThan(0f));
                Assert.That(landingIntensity, Is.LessThan(midIntensity));
                Assert.That(inactiveIntensity, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sourceProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxController_ExtractionParityFreeze_RepresentativeDistortionPulseSamples_PreserveConfiguredImpactAndLandingPulses()
        {
            var controllerObject = new GameObject("TopologyTransitionPostFxController_Distortion");
            var cameraObject = new GameObject("TopologyTransitionPostFxOutputCamera_Distortion");
            var sourceProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var controller = controllerObject.AddComponent<TopologyTransitionPostFxController>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                sourceProfile.Add<MotionBlur>(overrides: true);
                sourceProfile.Add<LensDistortion>(overrides: true);

                controller.Initialize(
                    TopologyTransitionPostFxProfile.Create(
                        sourceProfile,
                        distortionProfile: TopologyTransitionDistortionProfile.Create(
                            impactStart01: 0.1f,
                            impactDuration01: 0.2f,
                            impactIntensity: -0.3f,
                            landingStart01: 0.7f,
                            landingDuration01: 0.2f,
                            landingIntensity: 0.15f,
                            xMultiplier: 0.8f,
                            yMultiplier: 0.6f,
                            center: new Vector2(0.45f, 0.55f),
                            scale: 1.08f)),
                    outputCamera);

                Assert.That(controller.LensDistortionOverride, Is.Not.Null);
                Assert.That(controller.LensDistortionOverride.xMultiplier.value, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(controller.LensDistortionOverride.yMultiplier.value, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(controller.LensDistortionOverride.center.value, Is.EqualTo(new Vector2(0.45f, 0.55f)));
                Assert.That(controller.LensDistortionOverride.scale.value, Is.EqualTo(1.08f).Within(0.0001f));

                controller.Apply(
                    new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: 0.2f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: CubeRotationKind.Forward,
                        durationSeconds: 0.4f,
                        presentedVisualRotation: Quaternion.identity,
                        angularVelocityNormalized: 1f));
                var impactIntensity = controller.LensDistortionOverride.intensity.value;

                controller.Apply(
                    new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: 0.8f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: CubeRotationKind.Forward,
                        durationSeconds: 0.4f,
                        presentedVisualRotation: Quaternion.identity,
                        angularVelocityNormalized: 1f));
                var landingIntensity = controller.LensDistortionOverride.intensity.value;

                controller.Apply(TopologyTransitionVisualState.Inactive(new CubeTopologyState(FaceId.Front), Quaternion.identity));
                var inactiveIntensity = controller.LensDistortionOverride.intensity.value;

                Assert.That(impactIntensity, Is.EqualTo(-0.3f).Within(0.0001f));
                Assert.That(landingIntensity, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(inactiveIntensity, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sourceProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionPostFxController_ExtractionParityFreeze_RuntimeCloneMutation_DoesNotModifyAuthoritativeAssetAfterApply()
        {
            var controllerObject = new GameObject("TopologyTransitionPostFxController_AuthoritativeAsset");
            var cameraObject = new GameObject("TopologyTransitionPostFxOutputCamera_AuthoritativeAsset");
            var authoritativeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AuthoritativeVolumeProfileAssetPath);

            try
            {
                Assert.That(authoritativeProfile, Is.Not.Null);
                Assert.That(authoritativeProfile.TryGet(out MotionBlur authoritativeMotionBlur), Is.True);
                Assert.That(authoritativeProfile.TryGet(out LensDistortion authoritativeLensDistortion), Is.True);

                var baselineMotionBlurIntensity = authoritativeMotionBlur.intensity.value;
                var baselineMotionBlurMode = authoritativeMotionBlur.mode.value;
                var baselineMotionBlurQuality = authoritativeMotionBlur.quality.value;
                var baselineLensDistortionIntensity = authoritativeLensDistortion.intensity.value;
                var baselineLensDistortionScale = authoritativeLensDistortion.scale.value;

                var controller = controllerObject.AddComponent<TopologyTransitionPostFxController>();
                var outputCamera = cameraObject.AddComponent<Camera>();

                controller.Initialize(
                    TopologyTransitionPostFxProfile.Create(
                        authoritativeProfile,
                        maxBlurIntensity: 0.5f,
                        distortionProfile: TopologyTransitionDistortionProfile.Create(
                            impactStart01: 0.1f,
                            impactDuration01: 0.2f,
                            impactIntensity: -0.3f,
                            landingStart01: 0.7f,
                            landingDuration01: 0.2f,
                            landingIntensity: 0.15f,
                            xMultiplier: 0.8f,
                            yMultiplier: 0.6f,
                            center: new Vector2(0.45f, 0.55f),
                            scale: 1.08f)),
                    outputCamera);

                controller.Apply(CreateActiveVisualState(progress01: 0.2f, angularVelocityNormalized: 1f));

                Assert.That(controller.RuntimeVolumeProfile, Is.Not.Null);
                Assert.That(controller.RuntimeVolumeProfile, Is.Not.SameAs(authoritativeProfile));
                Assert.That(controller.RuntimeVolume.sharedProfile, Is.SameAs(authoritativeProfile));
                Assert.That(controller.RuntimeVolume.profile, Is.SameAs(controller.RuntimeVolumeProfile));
                Assert.That(controller.MotionBlurOverride.intensity.value, Is.GreaterThan(0f));
                Assert.That(Mathf.Abs(controller.LensDistortionOverride.intensity.value), Is.GreaterThan(0f));

                Assert.That(authoritativeMotionBlur.intensity.value, Is.EqualTo(baselineMotionBlurIntensity).Within(0.0001f));
                Assert.That(authoritativeMotionBlur.mode.value, Is.EqualTo(baselineMotionBlurMode));
                Assert.That(authoritativeMotionBlur.quality.value, Is.EqualTo(baselineMotionBlurQuality));
                Assert.That(authoritativeLensDistortion.intensity.value, Is.EqualTo(baselineLensDistortionIntensity).Within(0.0001f));
                Assert.That(authoritativeLensDistortion.scale.value, Is.EqualTo(baselineLensDistortionScale).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void ShowcaseCameraTopologyPresetAssets_UseAssetsDefaultVolumeProfileInsteadOfDeprecatedSettingsProfile()
        {
            var combinedPresetText = ReadNormalizedText(CombinedPresetAssetPath);
            var qualityPostFxPresetText = ReadNormalizedText(QualityPostFxPresetAssetPath);
            var uiAudioSceneText = ReadNormalizedText(UiAudioScenePath);
            var authoritativeGuid = AssetDatabase.AssetPathToGUID(AuthoritativeVolumeProfileAssetPath);
            var qualityPostFxPresetGuid = AssetDatabase.AssetPathToGUID(QualityPostFxPresetAssetPath);

            StringAssert.Contains($"authoritativeVolumeProfile: {{fileID: 11400000, guid: {authoritativeGuid}, type: 2}}", combinedPresetText);
            StringAssert.Contains($"authoritativeVolumeProfile: {{fileID: 11400000, guid: {authoritativeGuid}, type: 2}}", qualityPostFxPresetText);
            StringAssert.Contains("angularVelocityResponseExponent: 0.65", combinedPresetText);
            StringAssert.Contains("angularVelocityResponseExponent: 0.65", qualityPostFxPresetText);
            StringAssert.Contains("distortionProfile:", combinedPresetText);
            StringAssert.Contains("ImpactIntensity: -0.2", combinedPresetText);
            StringAssert.Contains("distortionProfile:", qualityPostFxPresetText);
            StringAssert.Contains("ImpactIntensity: -0.2", qualityPostFxPresetText);
            StringAssert.Contains($"preset: {{fileID: 11400000, guid: {qualityPostFxPresetGuid}, type: 2}}", uiAudioSceneText);
            StringAssert.DoesNotContain(DeprecatedVolumeProfileGuid, uiAudioSceneText);
            StringAssert.DoesNotContain(DeprecatedVolumeProfileGuid, combinedPresetText);
            StringAssert.DoesNotContain(DeprecatedVolumeProfileGuid, qualityPostFxPresetText);
        }

        [Test]
        [Category("Full")]
        public void GameplayShell_HostStartup_EnablesPostProcessingOnOutputCamera()
        {
            AssertSceneHostStartupEnablesOutputCameraPostProcessing(UiAudioScenePath, "mechanics-showcase");
            AssertSceneHostStartupEnablesOutputCameraPostProcessing(UiAudioScenePath, "onboarding");
        }

        private static void AssertSceneHostStartupEnablesOutputCameraPostProcessing(string scenePath, string stageIdValue)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            try
            {
                var installer = Object.FindFirstObjectByType<CombinedGameplayShowcaseInstaller>();
                Assert.That(installer, Is.Not.Null, $"Missing installer in '{scenePath}'.");
                var host = installer.GetComponent<GameplaySceneHost>();
                Assert.That(host, Is.Not.Null, $"Missing {nameof(GameplaySceneHost)} in '{scenePath}'.");

                GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                    installer.gameObject,
                    installer.GetCameraSettings(),
                    installer.GetBaselineAuthoringPolicy(),
                    installer.GetTopologyTransitionCameraShakeProfile());
                host.Initialize(BuildConfiguration(installer, stageIdValue));

                var outputCamera = Camera.main;
                Assert.That(outputCamera, Is.Not.Null, $"Missing Main Camera in '{scenePath}'.");
                Assert.That(outputCamera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static string ReadNormalizedText(string assetPath)
        {
            var fullPath = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            return System.IO.File.ReadAllText(fullPath).Replace("\r\n", "\n");
        }

        private static GameplaySceneHostConfiguration BuildConfiguration(
            CombinedGameplayShowcaseInstaller installer,
            string stageIdValue)
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
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            var stageId = StageId.CreateOrThrow(stageIdValue);
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));

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
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            }
        }

        private static TopologyTransitionVisualState CreateActiveVisualState(float progress01, float angularVelocityNormalized)
        {
            return new TopologyTransitionVisualState(
                isActive: true,
                progress01: progress01,
                sourceTopology: new CubeTopologyState(FaceId.Floor),
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: CubeRotationKind.Forward,
                durationSeconds: 0.4f,
                presentedVisualRotation: Quaternion.identity,
                angularVelocityNormalized: angularVelocityNormalized);
        }
    }
}
