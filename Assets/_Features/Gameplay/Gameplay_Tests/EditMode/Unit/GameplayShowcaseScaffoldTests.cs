using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Timing;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
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

                var mainCameraObject = new GameObject("Main Camera");
                mainCameraObject.AddComponent<Camera>();
                var brain = mainCameraObject.AddComponent<CinemachineBrain>();
                SceneManager.MoveGameObjectToScene(mainCameraObject, scene);

                var expectedCameraSettings = GameplayCameraSettings.CreateShowcaseDefault();
                var cinemachineCameraObject = new GameObject("CinemachineCamera");
                var cinemachineCamera = cinemachineCameraObject.AddComponent<CinemachineCamera>();
                var cinemachineLens = cinemachineCamera.Lens;
                cinemachineLens.FieldOfView = expectedCameraSettings.PerspectiveFieldOfView;
                cinemachineLens.NearClipPlane = expectedCameraSettings.NearClipPlane;
                cinemachineLens.FarClipPlane = expectedCameraSettings.FarClipPlane;
                cinemachineCamera.Lens = cinemachineLens;
                SceneManager.MoveGameObjectToScene(cinemachineCameraObject, scene);

                var legacyLabel = new GameObject("Label_LegacyTraversal");
                legacyLabel.AddComponent<TextMesh>();
                SceneManager.MoveGameObjectToScene(legacyLabel, scene);

                GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(installerObject);

                var rig = installerObject.GetComponent<GameplayCameraRig>();
                Assert.That(rig, Is.Not.Null);
                AssertCameraSettings(rig, expectedCameraSettings);

                var boardRoot = installerObject.GetComponentInChildren<GameplayBoardRoot>();
                Assert.That(boardRoot, Is.Not.Null);
                Assert.That(boardRoot.transform.parent, Is.EqualTo(installerObject.transform));
                Assert.That(boardRoot.BoardSurfaceRoot, Is.Not.Null);
                Assert.That(boardRoot.EntityRoot, Is.Not.Null);
                Assert.That(boardRoot.CameraTargetRoot, Is.Not.Null);
                Assert.That(boardRoot.CameraOrbitPivot, Is.Not.Null);
                Assert.That(boardRoot.CameraPoseRoot, Is.Not.Null);
                Assert.That(boardRoot.CameraEffectsRoot, Is.Not.Null);

                Assert.That(cinemachineCamera.transform.parent, Is.EqualTo(boardRoot.CameraEffectsRoot));
                Assert.That(cinemachineCamera.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(cinemachineCamera.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(cinemachineCamera.Target.TrackingTarget, Is.EqualTo(boardRoot.CameraTargetRoot));
                Assert.That(cinemachineCamera.Target.LookAtTarget, Is.EqualTo(boardRoot.CameraTargetRoot));
                Assert.That(cinemachineCamera.Target.CustomLookAtTarget, Is.True);
                Assert.That(
                    cinemachineCamera.Lens.FieldOfView,
                    Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(
                    cinemachineCamera.Lens.NearClipPlane,
                    Is.EqualTo(expectedCameraSettings.NearClipPlane).Within(0.0001f));
                Assert.That(
                    cinemachineCamera.Lens.FarClipPlane,
                    Is.EqualTo(expectedCameraSettings.FarClipPlane).Within(0.0001f));
                Assert.That(brain.DefaultBlend.Style, Is.EqualTo(CinemachineBlendDefinition.Styles.Cut));
                Assert.That(brain.DefaultBlend.BlendTime, Is.EqualTo(0f).Within(0.0001f));

                Assert.That(legacyLabel == null, Is.True);
            }
            finally
            {
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayShowcaseSceneScaffold_EnsureInstallerScaffold_CapturesAuthoredCinemachinePoseAsCameraBaseline()
        {
            var scene = CreateIsolatedTestScene();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var mainCameraObject = new GameObject("Main Camera");
                mainCameraObject.AddComponent<Camera>();
                mainCameraObject.AddComponent<CinemachineBrain>();
                SceneManager.MoveGameObjectToScene(mainCameraObject, scene);

                var cinemachineCameraObject = new GameObject("CinemachineCamera");
                var cinemachineCamera = cinemachineCameraObject.AddComponent<CinemachineCamera>();
                var lens = cinemachineCamera.Lens;
                lens.FieldOfView = 44f;
                lens.NearClipPlane = 0.2f;
                lens.FarClipPlane = 90f;
                cinemachineCamera.Lens = lens;
                cinemachineCamera.transform.SetPositionAndRotation(
                    new Vector3(3f, 4f, -8f),
                    Quaternion.LookRotation(new Vector3(-3f, -4f, 8f).normalized, Vector3.up));
                SceneManager.MoveGameObjectToScene(cinemachineCameraObject, scene);

                var baseCameraSettings = new GameplayCameraSettings
                {
                    UseAuthoredSceneCameraPose = true,
                    UseAuthoredSceneCameraLens = true,
                    PitchDegrees = 10f,
                    YawDegrees = 15f,
                    DistanceMode = GameplayCameraRig.DistanceMode.AutoFit,
                    ManualDistance = 2f,
                    FramingPadding = 1.2f,
                    PerspectiveFieldOfView = 60f,
                    NearClipPlane = 0.03f,
                    FarClipPlane = 100f,
                };

                GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                    installerObject,
                    baseCameraSettings);

                var rig = installerObject.GetComponent<GameplayCameraRig>();
                Assert.That(rig, Is.Not.Null);
                var resolvedCameraSettings = rig.ResolveConfiguredSettings(
                    baseCameraSettings,
                    Vector3.zero,
                    new CubeTopologyState(FaceId.Floor),
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);
                var boardRoot = installerObject.GetComponentInChildren<GameplayBoardRoot>();
                rig.ApplySettings(resolvedCameraSettings);
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                Assert.That(resolvedCameraSettings.PerspectiveFieldOfView, Is.EqualTo(44f).Within(0.0001f));
                Assert.That(resolvedCameraSettings.NearClipPlane, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(resolvedCameraSettings.FarClipPlane, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(cinemachineCamera.transform.parent, Is.EqualTo(boardRoot.CameraEffectsRoot));
                Assert.That(cinemachineCamera.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(
                    Vector3.Distance(boardRoot.CameraEffectsRoot.position, new Vector3(3f, 4f, -8f)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        boardRoot.CameraEffectsRoot.rotation,
                        Quaternion.LookRotation(new Vector3(-3f, -4f, 8f).normalized, Vector3.up)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayShowcaseSceneScaffold_EnsureInstallerScaffold_AppliesProvidedShakeProfileToRig()
        {
            var scene = CreateIsolatedTestScene();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var shakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();
                shakeProfile.ImpactLocalPositionAmplitude = Vector3.zero;
                shakeProfile.ImpactLocalRotationAmplitudeDegrees = Vector3.zero;
                shakeProfile.LandingLocalPositionAmplitude = Vector3.zero;
                shakeProfile.LandingLocalRotationAmplitudeDegrees = Vector3.zero;

                GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                    installerObject,
                    GameplayCameraSettings.CreateShowcaseDefault(),
                    shakeProfile);

                var rig = installerObject.GetComponent<GameplayCameraRig>();
                Assert.That(rig, Is.Not.Null);

                rig.ApplyTopologyTransitionVisualState(
                    new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: 0.12f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: CubeRotationKind.Forward,
                        durationSeconds: 0.2f,
                        presentedVisualRotation: Quaternion.Euler(12f, 0f, 0f),
                        angularVelocityNormalized: 1f));

                Assert.That(rig.TopologyTransitionShakeLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(rig.TopologyTransitionShakeLocalRotation, Is.EqualTo(Quaternion.identity));
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
            var simulationTimingPreset = CreateSimulationTimingPreset(moveCooldownSeconds: 0.35f);
            var presentationTimingPreset = CreatePresentationTimingPreset(topologyMotionDurationSeconds: 0.45f);

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "simulationTimingPreset", simulationTimingPreset);
                SetBaseInstallerField(installer, "presentationTimingPreset", presentationTimingPreset);
                SetBaseInstallerField(
                    installer,
                    "topologyRotationVisualMapping",
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);
                SetBaseInstallerField(
                    installer,
                    "topologyRotationTweenSettings",
                    new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.InOutBounce,
                    });

                var boardBounds = TestGameplayShowcaseInstaller.DefaultBoardBounds;
                var configuration = installer.BuildConfigurationForTests();
                var expectedCameraSettings = GameplayCameraSettings.CreateShowcaseDefault();

                Assert.That(configuration.InitialBoardBounds, Is.EqualTo(boardBounds));
                Assert.That(configuration.CameraSettings, Is.Not.Null);
                Assert.That(configuration.TopologyTransitionCameraShakeProfile, Is.Not.Null);
                AssertCameraSettings(configuration.CameraSettings, expectedCameraSettings);
                Assert.That(configuration.PlayerControlTiming, Is.Not.Null);
                Assert.That(configuration.PlayerControlTiming.MoveCooldownSeconds, Is.EqualTo(0.35f));
                Assert.That(configuration.TopologyMotionDurationSeconds, Is.EqualTo(0.45f));
                Assert.That(
                    configuration.TopologyRotationVisualMapping,
                    Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesPositiveX));
                Assert.That(
                    configuration.TopologyRotationTween.Ease,
                    Is.EqualTo(TopologyRotationTweenEase.InOutBounce));
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
                UnityEngine.Object.DestroyImmediate(simulationTimingPreset);
                UnityEngine.Object.DestroyImmediate(presentationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayShowcaseInstaller_CreateConfiguration_MissingSimulationTimingPreset_ThrowsInstallerName()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var presentationTimingPreset = CreatePresentationTimingPreset();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "presentationTimingPreset", presentationTimingPreset);

                var exception = Assert.Throws<TargetInvocationException>(() => installer.BuildConfigurationForTests());
                Assert.That(exception?.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception?.InnerException?.Message, Does.Contain(nameof(TestGameplayShowcaseInstaller)));
                Assert.That(exception?.InnerException?.Message, Does.Contain(nameof(GameplaySimulationTimingPreset)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presentationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayShowcaseInstaller_CreateConfiguration_MissingPresentationTimingPreset_ThrowsInstallerName()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var simulationTimingPreset = CreateSimulationTimingPreset();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "simulationTimingPreset", simulationTimingPreset);

                var exception = Assert.Throws<TargetInvocationException>(() => installer.BuildConfigurationForTests());
                Assert.That(exception?.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception?.InnerException?.Message, Does.Contain(nameof(TestGameplayShowcaseInstaller)));
                Assert.That(exception?.InnerException?.Message, Does.Contain(nameof(GameplayPresentationTimingPreset)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(simulationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayPresentationCleanup_CreateConfiguration_DoesNotDuplicatePlayerPrefabWhenViewFactoryProvidesIt()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var simulationTimingPreset = CreateSimulationTimingPreset();
            var presentationTimingPreset = CreatePresentationTimingPreset();
            var playerPrefabObject = new GameObject("GameplayPresentationCleanup_PlayerPrefab");

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);
                SceneManager.MoveGameObjectToScene(playerPrefabObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "simulationTimingPreset", simulationTimingPreset);
                SetBaseInstallerField(installer, "presentationTimingPreset", presentationTimingPreset);

                var playerPrefabView = playerPrefabObject.AddComponent<GameplayEntityView>();
                playerPrefabObject.AddComponent<PlayerAnimatorDriver>();
                playerPrefabObject.AddComponent<PlayerAnimationTimingAuthoring>();

                installer.PlayerViewPrefabOverride = playerPrefabView;
                installer.ViewFactoryOverride = new TestPlayerPrefabSourceViewFactory(playerPrefabView);

                var configuration = installer.BuildConfigurationForTests();

                Assert.That(configuration.ViewFactory, Is.SameAs(installer.ViewFactoryOverride));
                Assert.That(configuration.PlayerViewPrefab, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(simulationTimingPreset);
                UnityEngine.Object.DestroyImmediate(presentationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayShowcaseInstaller_CreateConfiguration_PropagatesExplicitFaceSeamGap()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var simulationTimingPreset = CreateSimulationTimingPreset();
            var presentationTimingPreset = CreatePresentationTimingPreset();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "simulationTimingPreset", simulationTimingPreset);
                SetBaseInstallerField(installer, "presentationTimingPreset", presentationTimingPreset);
                SetBaseInstallerField(installer, "faceSeamGap", 0.35f);

                var configuration = installer.BuildConfigurationForTests();

                Assert.That(configuration.CellSize, Is.EqualTo(1.15f).Within(0.0001f));
                Assert.That(configuration.FaceSeamGap, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(configuration.ResolveFaceSeamGap(), Is.EqualTo(0.35f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(simulationTimingPreset);
                UnityEngine.Object.DestroyImmediate(presentationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayCameraRig_InitializeWithoutDirectCamera_DrivesOrbitHierarchyPose()
        {
            var scene = CreateIsolatedTestScene();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var boardRootObject = new GameObject("GameplayBoardRoot");
                boardRootObject.transform.SetParent(installerObject.transform, worldPositionStays: false);
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                boardRoot.CameraTargetRoot.position = new Vector3(2f, 3f, -4f);

                var rig = installerObject.AddComponent<GameplayCameraRig>();
                var cameraSettings = GameplayCameraSettings.CreateShowcaseDefault();
                rig.ApplySettings(cameraSettings);
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                var orbitRotation = Quaternion.Euler(90f, 0f, 0f);
                rig.SetPresentedTopologyOrbit(orbitRotation);

                var expectedLocalRotation = Quaternion.Euler(
                    cameraSettings.PitchDegrees,
                    cameraSettings.YawDegrees,
                    0f);
                var expectedWorldRotation = orbitRotation * expectedLocalRotation;
                var expectedWorldPosition =
                    boardRoot.CameraTargetRoot.position + (expectedWorldRotation * (Vector3.back * cameraSettings.ManualDistance));

                Assert.That(Quaternion.Angle(boardRoot.CameraOrbitPivot.localRotation, orbitRotation), Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraPoseRoot.localRotation, expectedLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(boardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(boardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(
                    Vector3.Distance(
                        boardRoot.CameraPoseRoot.localPosition,
                        Vector3.back * cameraSettings.ManualDistance),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraPoseRoot.rotation, expectedWorldRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(boardRoot.CameraPoseRoot.position, expectedWorldPosition),
                    Is.LessThan(0.0001f));
            }
            finally
            {
                ResetIsolatedTestScene();
            }
        }

        private static void SetBaseInstallerField(object target, string fieldName, object value)
        {
            SetPrivateField(typeof(GameplayShowcaseSceneInstallerBase), target, fieldName, value);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            var field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static GameplaySimulationTimingPreset CreateSimulationTimingPreset(
            float initialMoveDelaySeconds = 0f,
            float moveCooldownSeconds = -1f,
            float repeatedMoveIntervalSeconds = 0.6f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float projectileStepIntervalSeconds = 0.2f)
        {
            var preset = ScriptableObject.CreateInstance<GameplaySimulationTimingPreset>();
            var playerControlTiming = new PlayerControlTimingSettings
            {
                MoveCooldownSeconds = moveCooldownSeconds,
            };

            SetPrivateField(typeof(GameplaySimulationTimingPreset), preset, "initialMoveDelaySeconds", initialMoveDelaySeconds);
            SetPrivateField(typeof(GameplaySimulationTimingPreset), preset, "playerControlTiming", playerControlTiming);
            SetPrivateField(typeof(GameplaySimulationTimingPreset), preset, "repeatedMoveIntervalSeconds", repeatedMoveIntervalSeconds);
            SetPrivateField(typeof(GameplaySimulationTimingPreset), preset, "boxSlideStepIntervalSeconds", boxSlideStepIntervalSeconds);
            SetPrivateField(typeof(GameplaySimulationTimingPreset), preset, "projectileStepIntervalSeconds", projectileStepIntervalSeconds);
            return preset;
        }

        private static GameplayPresentationTimingPreset CreatePresentationTimingPreset(
            float moveMotionDurationSeconds = -1f,
            float pushMotionDurationSeconds = 0.2f,
            float flipMotionDurationSeconds = 0.2f,
            float topologyMotionDurationSeconds = -1f,
            float itemConsumeEffectDurationSeconds = -1f,
            float boxDestroyEffectDurationSeconds = -1f)
        {
            var preset = ScriptableObject.CreateInstance<GameplayPresentationTimingPreset>();
            SetPrivateField(typeof(GameplayPresentationTimingPreset), preset, "moveMotionDurationSeconds", moveMotionDurationSeconds);
            SetPrivateField(typeof(GameplayPresentationTimingPreset), preset, "pushMotionDurationSeconds", pushMotionDurationSeconds);
            SetPrivateField(typeof(GameplayPresentationTimingPreset), preset, "flipMotionDurationSeconds", flipMotionDurationSeconds);
            SetPrivateField(typeof(GameplayPresentationTimingPreset), preset, "topologyMotionDurationSeconds", topologyMotionDurationSeconds);
            SetPrivateField(typeof(GameplayPresentationTimingPreset), preset, "itemConsumeEffectDurationSeconds", itemConsumeEffectDurationSeconds);
            SetPrivateField(typeof(GameplayPresentationTimingPreset), preset, "boxDestroyEffectDurationSeconds", boxDestroyEffectDurationSeconds);
            return preset;
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
            Assert.That(actual.UseAuthoredSceneCameraPose, Is.EqualTo(expected.UseAuthoredSceneCameraPose));
            Assert.That(actual.UseAuthoredSceneCameraLens, Is.EqualTo(expected.UseAuthoredSceneCameraLens));
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

        [Test]
        public void GameplayShowcaseInstaller_CreateConfiguration_ClonesTopologyTransitionCameraShakeProfile()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var simulationTimingPreset = CreateSimulationTimingPreset();
            var presentationTimingPreset = CreatePresentationTimingPreset();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                var authoredShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();
                authoredShakeProfile.ImpactStart01 = 0.05f;
                authoredShakeProfile.ImpactDuration01 = 0.11f;
                authoredShakeProfile.ImpactLocalPositionAmplitude = new Vector3(0.031f, 0.017f, 0.021f);
                authoredShakeProfile.LandingStart01 = 0.73f;
                authoredShakeProfile.LandingDuration01 = 0.14f;
                authoredShakeProfile.LandingLocalRotationAmplitudeDegrees = new Vector3(0.11f, 0.22f, 0.33f);

                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "simulationTimingPreset", simulationTimingPreset);
                SetBaseInstallerField(installer, "presentationTimingPreset", presentationTimingPreset);
                SetBaseInstallerField(installer, "topologyTransitionCameraShakeProfile", authoredShakeProfile);

                var configuration = installer.BuildConfigurationForTests();

                Assert.That(configuration.TopologyTransitionCameraShakeProfile, Is.Not.Null);
                Assert.That(configuration.TopologyTransitionCameraShakeProfile, Is.Not.SameAs(authoredShakeProfile));
                Assert.That(configuration.TopologyTransitionCameraShakeProfile.ImpactStart01, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(configuration.TopologyTransitionCameraShakeProfile.ImpactDuration01, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(
                    configuration.TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude,
                    Is.EqualTo(new Vector3(0.031f, 0.017f, 0.021f)));
                Assert.That(configuration.TopologyTransitionCameraShakeProfile.LandingStart01, Is.EqualTo(0.73f).Within(0.0001f));
                Assert.That(configuration.TopologyTransitionCameraShakeProfile.LandingDuration01, Is.EqualTo(0.14f).Within(0.0001f));
                Assert.That(
                    configuration.TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees,
                    Is.EqualTo(new Vector3(0.11f, 0.22f, 0.33f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(simulationTimingPreset);
                UnityEngine.Object.DestroyImmediate(presentationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        [Test]
        public void GameplayShowcaseInstaller_CreateConfiguration_ClonesTopologyTransitionPostFxProfile()
        {
            var scene = CreateIsolatedTestScene();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var simulationTimingPreset = CreateSimulationTimingPreset();
            var presentationTimingPreset = CreatePresentationTimingPreset();
            var authoritativeProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var installerObject = new GameObject("GameplayShowcaseInstaller");
                SceneManager.MoveGameObjectToScene(installerObject, scene);

                var installer = installerObject.AddComponent<TestGameplayShowcaseInstaller>();
                var authoredPostFxProfile = TopologyTransitionPostFxProfile.Create(
                    authoritativeProfile,
                    maxBlurIntensity: 0.42f,
                    cameraClamp: 0.08f,
                    angularVelocityResponseExponent: 0.71f,
                    landingFadeStart01: 0.81f,
                    landingFadeExponent: 2.5f,
                    distortionProfile: TopologyTransitionDistortionProfile.Create(
                        impactStart01: 0.06f,
                        impactDuration01: 0.14f,
                        impactIntensity: -0.28f,
                        landingStart01: 0.7f,
                        landingDuration01: 0.2f,
                        landingIntensity: 0.18f,
                        xMultiplier: 0.75f,
                        yMultiplier: 0.55f,
                        center: new Vector2(0.47f, 0.53f),
                        scale: 1.1f));

                SetBaseInstallerField(installer, "actions", actions);
                SetBaseInstallerField(installer, "simulationTimingPreset", simulationTimingPreset);
                SetBaseInstallerField(installer, "presentationTimingPreset", presentationTimingPreset);
                SetBaseInstallerField(installer, "topologyTransitionPostFxProfile", authoredPostFxProfile);

                var configuration = installer.BuildConfigurationForTests();

                Assert.That(configuration.TopologyTransitionPostFxProfile, Is.Not.Null);
                Assert.That(configuration.TopologyTransitionPostFxProfile, Is.Not.SameAs(authoredPostFxProfile));
                Assert.That(configuration.TopologyTransitionPostFxProfile.AuthoritativeVolumeProfile, Is.SameAs(authoritativeProfile));
                Assert.That(configuration.TopologyTransitionPostFxProfile.MaxBlurIntensity, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(configuration.TopologyTransitionPostFxProfile.CameraClamp, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(
                    configuration.TopologyTransitionPostFxProfile.AngularVelocityResponseExponent,
                    Is.EqualTo(0.71f).Within(0.0001f));
                Assert.That(configuration.TopologyTransitionPostFxProfile.LandingFadeStart01, Is.EqualTo(0.81f).Within(0.0001f));
                Assert.That(configuration.TopologyTransitionPostFxProfile.LandingFadeExponent, Is.EqualTo(2.5f).Within(0.0001f));
                Assert.That(
                    configuration.TopologyTransitionPostFxProfile.DistortionProfile.ImpactIntensity,
                    Is.EqualTo(-0.28f).Within(0.0001f));
                Assert.That(
                    configuration.TopologyTransitionPostFxProfile.DistortionProfile.LandingIntensity,
                    Is.EqualTo(0.18f).Within(0.0001f));
                Assert.That(
                    configuration.TopologyTransitionPostFxProfile.DistortionProfile.Center,
                    Is.EqualTo(new Vector2(0.47f, 0.53f)));
                Assert.That(
                    configuration.TopologyTransitionPostFxProfile.DistortionProfile.Scale,
                    Is.EqualTo(1.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoritativeProfile);
                UnityEngine.Object.DestroyImmediate(simulationTimingPreset);
                UnityEngine.Object.DestroyImmediate(presentationTimingPreset);
                UnityEngine.Object.DestroyImmediate(actions);
                ResetIsolatedTestScene();
            }
        }

        private sealed class TestGameplayShowcaseInstaller : GameplayShowcaseSceneInstallerBase
        {
            internal static readonly BoardBounds DefaultBoardBounds = new(new Vector2Int(0, 0), new Vector2Int(4, 4));

            public GameplayEntityView PlayerViewPrefabOverride { get; set; }

            public IGameplayEntityViewFactory ViewFactoryOverride { get; set; }

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

            protected override IGameplayEntityViewFactory CreateViewFactory(
                GameplayBoardRoot boardRoot,
                in InitialGameplayState initialState)
            {
                return ViewFactoryOverride ?? base.CreateViewFactory(boardRoot, initialState);
            }

            protected override GameplayEntityView ResolvePlayerViewPrefab()
            {
                return PlayerViewPrefabOverride;
            }

            protected override InitialGameplayState BuildInitialGameplayState()
            {
                return new InitialGameplayState(
                    DefaultBoardBounds,
                    new CubeTopologyState(FaceId.Floor),
                    Array.Empty<EntityState>(),
                    Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                    playerEntityId: 10,
                    Array.Empty<EnemyAiProfileOverride>(),
                    Array.Empty<EnemyPresentationBinding>(),
                    Array.Empty<StaticEntityPresentationBinding>());
            }

        }

        private sealed class TestPlayerPrefabSourceViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
        {
            public TestPlayerPrefabSourceViewFactory(GameplayEntityView playerViewPrefab)
            {
                PlayerViewPrefab = playerViewPrefab;
            }

            public GameplayEntityView PlayerViewPrefab { get; }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                return null;
            }
        }
    }

    public sealed class GameplayShowcaseAssetMigrationTests
    {
        private const string CombinedScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CombinedSceneInstallerIdentifier =
            "Game.Feature.Gameplay.Host::Game.Feature.Gameplay.Host.CombinedGameplayShowcaseInstaller";
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset";
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";
        private const string PlayerAnimationTestPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Entity_View_PlayerAnimationTest.prefab";

        [Test]
        public void CombinedGameplayShowcaseScene_SerializesTimingPresetReferencesInsteadOfLegacyTimingFields()
        {
            var installerBlock = ReadInstallerBlock(CombinedScenePath, CombinedSceneInstallerIdentifier);

            AssertUsesTimingPresetReferences(
                installerBlock,
                CombinedStageAssetPath,
                DefaultSimulationTimingPresetAssetPath,
                DefaultPresentationTimingPresetAssetPath);
            StringAssert.DoesNotContain("initialMoveDelaySeconds:", installerBlock);
            StringAssert.DoesNotContain("playerControlTiming:", installerBlock);
            StringAssert.DoesNotContain("repeatedMoveIntervalSeconds:", installerBlock);
            StringAssert.DoesNotContain("boxSlideStepIntervalSeconds:", installerBlock);
            StringAssert.DoesNotContain("projectileStepIntervalSeconds:", installerBlock);
            StringAssert.DoesNotContain("moveMotionDurationSeconds:", installerBlock);
            StringAssert.DoesNotContain("pushMotionDurationSeconds:", installerBlock);
            StringAssert.DoesNotContain("flipMotionDurationSeconds:", installerBlock);
            StringAssert.DoesNotContain("topologyMotionDurationSeconds:", installerBlock);
            StringAssert.DoesNotContain("itemConsumeEffectDurationSeconds:", installerBlock);
            StringAssert.DoesNotContain("boxDestroyEffectDurationSeconds:", installerBlock);
            StringAssert.DoesNotContain("playerMoveCooldownSeconds:", installerBlock);
            StringAssert.DoesNotContain("topologyRotationTweenSettings:\n    Mode:", installerBlock);
        }

        [Test]
        public void CombinedGameplayShowcaseScene_SerializesEnemyPresentationCatalogReference()
        {
            var installerBlock = ReadInstallerBlock(CombinedScenePath, CombinedSceneInstallerIdentifier);

            StringAssert.Contains("enemyPresentationCatalog:", installerBlock);
            StringAssert.DoesNotContain("enemyAnimationTimingOverrides:", installerBlock);
        }

        [Test]
        public void PlayerAnimationTestPrefab_UsesPresentationOnlyTimingAuthoringComponents()
        {
            var prefabText = ReadNormalizedText(PlayerAnimationTestPrefabPath);

            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            StringAssert.Contains("EntityMotionPresentationAuthoring", prefabText);
            StringAssert.Contains("PlayerAnimationTimingAuthoring", prefabText);
            StringAssert.Contains("deathAnimatorDurationSeconds:", prefabText);
            StringAssert.DoesNotContain("PlayerActionTimingAuthoring", prefabText);
            StringAssert.DoesNotContain("pushPresentationDurationSeconds", prefabText);
            StringAssert.DoesNotContain("flipPresentationDurationSeconds", prefabText);
        }

        [Test]
        public void ShowcaseScenes_SerializeCinemachineBootstrapWithoutExtraBlendDamping()
        {
            AssertShowcaseSceneUsesCutBrainBlend(CombinedScenePath);
            AssertShowcaseSceneUsesCutBrainBlend(TutorialScenePath);
        }

        private static string ReadNormalizedText(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var normalizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(projectRoot, normalizedAssetPath);
            return File.ReadAllText(fullPath).Replace("\r\n", "\n");
        }

        private static string ReadInstallerBlock(string assetPath, string editorClassIdentifier)
        {
            var sceneText = ReadNormalizedText(assetPath);
            var marker = $"m_EditorClassIdentifier: {editorClassIdentifier}";
            var markerIndex = sceneText.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0), $"Missing installer block '{editorClassIdentifier}'.");

            var blockStart = sceneText.LastIndexOf("--- !u!114", markerIndex, StringComparison.Ordinal);
            Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), "Could not find the start of the installer MonoBehaviour block.");

            var blockEnd = sceneText.IndexOf("\n--- !u!", markerIndex, StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                blockEnd = sceneText.Length;
            }

            return sceneText.Substring(blockStart, blockEnd - blockStart);
        }

        private static void AssertUsesTimingPresetReferences(
            string installerBlock,
            string stageAssetPath,
            string simulationTimingPresetAssetPath,
            string presentationTimingPresetAssetPath)
        {
            StringAssert.Contains(
                $"stageDefinition: {{fileID: 11400000, guid: {AssetDatabase.AssetPathToGUID(stageAssetPath)}, type: 2}}",
                installerBlock);
            StringAssert.Contains(
                $"simulationTimingPreset: {{fileID: 11400000, guid: {AssetDatabase.AssetPathToGUID(simulationTimingPresetAssetPath)}, type: 2}}",
                installerBlock);
            StringAssert.Contains(
                $"presentationTimingPreset: {{fileID: 11400000, guid: {AssetDatabase.AssetPathToGUID(presentationTimingPresetAssetPath)}, type: 2}}",
                installerBlock);
        }

        private static void AssertShowcaseSceneUsesCutBrainBlend(string assetPath)
        {
            var sceneText = ReadNormalizedText(assetPath);
            StringAssert.Contains("DefaultBlend:\n    Style: 0\n    Time: 0", sceneText);
            StringAssert.Contains("FieldOfView: 50", sceneText);
            StringAssert.Contains("NearClipPlane: 0.03", sceneText);
            StringAssert.Contains("FarClipPlane: 100", sceneText);
        }
    }
}
