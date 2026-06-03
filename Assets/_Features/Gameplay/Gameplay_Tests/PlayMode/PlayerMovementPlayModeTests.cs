using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Shared.Input;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    // Final presentation guardrail:
    // Keep the input/tick scenarios in this file and validate them through the
    // cube projector plus board-root world transform instead of strip-space literals.
    public sealed class PlayerMovementPlayModeTests : InputTestFixture
    {
        private const string PlayerS1PrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private Keyboard _keyboard;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PresentationLock_DuringTopologyTransitionPreventsTickAndBurst()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });

            host.InputHost.SetRawMoveInput(Vector2.up);

            Assert.That(
                host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds),
                Is.EqualTo(1));
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
            Assert.That(host.Presenter.IsPresentationActive, Is.True);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(
                host.Presenter.CurrentTopologyTransitionVisualState.DestinationTopology,
                Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));

            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));

            Assert.That(
                host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 6f),
                Is.EqualTo(0));
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);
            Assert.That(host.Presenter.IsPresentationActive, Is.False);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));

            Assert.That(host.InputHost.AdvanceTime(0f), Is.EqualTo(1));
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));
            Assert.That(host.Presenter.CurrentTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TopologyTransition_GameplayPause_FreezesVisualProgressUntilResume()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds), Is.EqualTo(1));
            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.25f);
            var pausedProgress = host.Presenter.CurrentTopologyTransitionVisualState.Progress01;

            InvokePauseService(host, "Pause");
            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.Progress01, Is.EqualTo(pausedProgress).Within(0.0001f));

            InvokePauseService(host, "Resume");
            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TopologyTransition_PauseResume_KeepsPresentationLockedUntilTransitionCompletes()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds), Is.EqualTo(1));

            InvokePauseService(host, "Pause");
            InvokePauseService(host, "Resume");

            Assert.That(host.Presenter.HasBlockingPresentation, Is.True);
            Assert.That(host.InputHost.RunSingleTick(), Is.Null);

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TopologyTransition_GameplayPauseDoesNotCompleteWhilePaused()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds), Is.EqualTo(1));
            InvokePauseService(host, "Pause");

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 2f);

            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.True);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TopologyTransition_ResumeCompletesAndReleasesPresentationLock()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds), Is.EqualTo(1));
            InvokePauseService(host, "Pause");
            InvokePauseService(host, "Resume");

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator RespawnTopologyReset_UsesExistingTopologyTransitionInputLock()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 0)),
            });

            SetAuthoritativeTopology(host.WorldState, new CubeTopologyState(FaceId.Back));
            ApplyAuthoritativeDamage(host.WorldState, entityId: 10, amount: 3);

            var deathTick = host.InputHost.RunSingleTick();
            var afterDeathSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(deathTick, Is.Not.Null);
            Assert.That(afterDeathSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));

            var firstResetTick = host.InputHost.RunSingleTick();

            Assert.That(firstResetTick, Is.Not.Null);
            Assert.That(firstResetTick.PresentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(firstResetTick.PresentationData.TopologyMotion.Value.SourceTopology, Is.EqualTo(new CubeTopologyState(FaceId.Back)));
            Assert.That(firstResetTick.PresentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(
                firstResetTick.PresentationData.VisibilityChanges.Any(change =>
                    change.EntityId == 10 &&
                    change.ChangeKind == TickVisibilityChangeKind.Spawn),
                Is.False);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(
                host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 6f),
                Is.EqualTo(0));
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(host.Presenter.CurrentTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));

            var secondResetTick = host.InputHost.RunSingleTick();

            Assert.That(secondResetTick, Is.Not.Null);
            Assert.That(secondResetTick.PresentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(secondResetTick.PresentationData.TopologyMotion.Value.SourceTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(secondResetTick.PresentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(
                secondResetTick.PresentationData.VisibilityChanges.Any(change =>
                    change.EntityId == 10 &&
                    change.ChangeKind == TickVisibilityChangeKind.Spawn),
                Is.False);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(4));

            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(
                host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 6f),
                Is.EqualTo(0));
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(4));

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(host.Presenter.CurrentTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));

            var respawnTick = host.InputHost.RunSingleTick();
            var afterRespawnSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(respawnTick, Is.Not.Null);
            Assert.That(respawnTick.EventLog, Does.Contain("RespawnCommitted|E=10|Pos=(0,0)|Face=Front|Facing=Right|Tick=4"));
            Assert.That(afterRespawnSnapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(afterRespawnSnapshot.TryGetEntity(10, out var respawnedPlayer), Is.True);
            Assert.That(respawnedPlayer.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_TopologyTransition_CameraMotionBlurActivatesThenResets()
        {
            var outputCameraObject = new GameObject("PlayModeTopologyTransitionOutputCamera");
            var sourceProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            sourceProfile.Add<MotionBlur>(overrides: true);
            var outputCamera = outputCameraObject.AddComponent<Camera>();
            var postFxProfile = TopologyTransitionPostFxProfile.Create(sourceProfile, maxBlurIntensity: 0.5f);

            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
                },
                topologyTransitionPostFxProfile: postFxProfile,
                viewCamera: outputCamera);

            var controller = host.GetComponent<TopologyTransitionPostFxController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.MotionBlurOverride, Is.Not.Null);
            Assert.That(controller.LensDistortionOverride, Is.Not.Null);
            Assert.That(outputCamera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(
                host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds),
                Is.EqualTo(1));

            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(controller.MotionBlurOverride.intensity.value, Is.EqualTo(0f));
            Assert.That(controller.LensDistortionOverride.intensity.value, Is.EqualTo(0f));

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);
            Assert.That(controller.MotionBlurOverride.intensity.value, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(controller.LensDistortionOverride.intensity.value), Is.GreaterThan(0f));
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(outputCamera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            Assert.That(controller.MotionBlurOverride.intensity.value, Is.EqualTo(0f));
            Assert.That(controller.LensDistortionOverride.intensity.value, Is.EqualTo(0f));

            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);
            Assert.That(controller.MotionBlurOverride.intensity.value, Is.EqualTo(0f));
            Assert.That(controller.LensDistortionOverride.intensity.value, Is.EqualTo(0f));

            yield return DestroyHost(host);
            UnityEngine.Object.Destroy(outputCameraObject);
            UnityEngine.Object.Destroy(sourceProfile);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TopologyTransitionCameraShake_PlayMode_DirectAndCinemachinePaths_SharePulseTimingAndReset()
        {
            var directRootObject = new GameObject("PlayModeTopologyTransitionCameraShake_DirectRoot");
            var cinemachineRootObject = new GameObject("PlayModeTopologyTransitionCameraShake_CinemachineRoot");
            var directCameraObject = new GameObject("PlayModeTopologyTransitionCameraShake_DirectCamera");
            var outputCameraObject = new GameObject("PlayModeTopologyTransitionCameraShake_OutputCamera");
            var cinemachineCameraObject = new GameObject("PlayModeTopologyTransitionCameraShake_CinemachineCamera");

            try
            {
                var directBoardRoot = directRootObject.AddComponent<GameplayBoardRoot>();
                directBoardRoot.EnsureHierarchy();
                var cinemachineBoardRoot = cinemachineRootObject.AddComponent<GameplayBoardRoot>();
                cinemachineBoardRoot.EnsureHierarchy();

                var directRig = directRootObject.AddComponent<GameplayCameraRig>();
                directRig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                GameplayCameraRigReflectionAdapter.ConfigureTopologyTransitionCameraShake(
                    directRig,
                    TopologyTransitionCameraShakeProfile.CreateDefault());
                directRig.Initialize(
                    directCameraObject.AddComponent<Camera>(),
                    directBoardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));

                var cinemachineRig = cinemachineRootObject.AddComponent<GameplayCameraRig>();
                cinemachineRig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                GameplayCameraRigReflectionAdapter.ConfigureTopologyTransitionCameraShake(
                    cinemachineRig,
                    TopologyTransitionCameraShakeProfile.CreateDefault());
                cinemachineRig.Initialize(
                    null,
                    cinemachineBoardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));

                var outputCamera = outputCameraObject.AddComponent<Camera>();
                var brain = outputCameraObject.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;

                var cinemachineCamera = cinemachineCameraObject.AddComponent<CinemachineCamera>();
                cinemachineCameraObject.transform.SetParent(cinemachineBoardRoot.CameraEffectsRoot, worldPositionStays: false);
                cinemachineCameraObject.transform.localPosition = Vector3.zero;
                cinemachineCameraObject.transform.localRotation = Quaternion.identity;
                cinemachineCamera.Target = new CameraTarget
                {
                    TrackingTarget = cinemachineBoardRoot.CameraTargetRoot,
                    LookAtTarget = cinemachineBoardRoot.CameraTargetRoot,
                    CustomLookAtTarget = true,
                };

                var orbit = Quaternion.Euler(90f, 0f, 0f);
                directRig.SetPresentedTopologyOrbit(orbit);
                cinemachineRig.SetPresentedTopologyOrbit(orbit);
                brain.ManualUpdate();

                var impactState = new TopologyTransitionVisualState(
                    isActive: true,
                    progress01: 0.12f,
                    sourceTopology: new CubeTopologyState(FaceId.Floor),
                    destinationTopology: new CubeTopologyState(FaceId.Front),
                    rotationKind: CubeRotationKind.Forward,
                    durationSeconds: 0.2f,
                    presentedVisualRotation: Quaternion.Euler(12f, 0f, 0f),
                    angularVelocityNormalized: 1f);
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(directRig, impactState);
                directRig.SnapToTarget();
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(cinemachineRig, impactState);
                cinemachineRig.SnapToTarget();
                brain.ManualUpdate();

                Assert.That(
                    Vector3.Distance(
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(directRig),
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(cinemachineRig)),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(directRig),
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(cinemachineRig)),
                    Is.LessThan(0.001f));
                Assert.That(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(directRig).sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(directRig),
                        Quaternion.identity) > 0.001f,
                    Is.True);
                Assert.That(
                    Vector3.Distance(
                        cinemachineBoardRoot.CameraEffectsRoot.localPosition,
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(cinemachineRig)),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        cinemachineBoardRoot.CameraEffectsRoot.localRotation,
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(cinemachineRig)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(outputCamera.transform.position, cinemachineCamera.transform.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(outputCamera.transform.rotation, cinemachineCamera.transform.rotation),
                    Is.LessThan(0.001f));

                var landingState = new TopologyTransitionVisualState(
                    isActive: true,
                    progress01: 0.84f,
                    sourceTopology: new CubeTopologyState(FaceId.Floor),
                    destinationTopology: new CubeTopologyState(FaceId.Front),
                    rotationKind: CubeRotationKind.Forward,
                    durationSeconds: 0.2f,
                    presentedVisualRotation: Quaternion.Euler(78f, 0f, 0f),
                    angularVelocityNormalized: 0.45f);
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(directRig, landingState);
                directRig.SnapToTarget();
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(cinemachineRig, landingState);
                cinemachineRig.SnapToTarget();
                brain.ManualUpdate();

                Assert.That(
                    Vector3.Distance(
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(directRig),
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(cinemachineRig)),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(directRig),
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(cinemachineRig)),
                    Is.LessThan(0.001f));
                Assert.That(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(directRig).sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(
                        GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(directRig),
                        Quaternion.identity) > 0.001f,
                    Is.True);

                var inactiveState = TopologyTransitionVisualState.Inactive(
                    new CubeTopologyState(FaceId.Front),
                    Quaternion.Euler(90f, 0f, 0f));
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(directRig, inactiveState);
                directRig.SnapToTarget();
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(cinemachineRig, inactiveState);
                cinemachineRig.SnapToTarget();
                brain.ManualUpdate();

                Assert.That(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(directRig),
                    Is.EqualTo(Vector3.zero));
                Assert.That(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(directRig),
                    Is.EqualTo(Quaternion.identity));
                Assert.That(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(cinemachineRig),
                    Is.EqualTo(Vector3.zero));
                Assert.That(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(cinemachineRig),
                    Is.EqualTo(Quaternion.identity));
                Assert.That(cinemachineBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(cinemachineBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                UnityEngine.Object.Destroy(directRootObject);
                UnityEngine.Object.Destroy(cinemachineRootObject);
                UnityEngine.Object.Destroy(directCameraObject);
                UnityEngine.Object.Destroy(outputCameraObject);
                UnityEngine.Object.Destroy(cinemachineCameraObject);
            }

            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_MovePresentation_DoesNotBlockSubsequentTicks()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            var firstTick = host.InputHost.RunSingleTick();

            Assert.That(firstTick, Is.Not.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            var secondTick = host.InputHost.RunSingleTick();
            Assert.That(secondTick, Is.Not.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_BoxSlidePresentation_DoesNotBlockSimulationTicks()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);

            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            Assert.That(host.Presenter.IsPresentationActive, Is.False);

            var executeTick = host.InputHost.RunSingleTick();
            Assert.That(executeTick, Is.Not.Null);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            Assert.That(host.Presenter.IsPresentationActive, Is.True);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplayInputHost_BoxSlidePauseService_FreezesWorldPresentationUntilResume()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            host.InputHost.SetRawMoveInput(Vector2.right);
            InvokeInputHostBufferUiPush(host.InputHost, Direction.Right);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);

            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

            host.Presenter.UpdatePresentation(host.TimingProfile.MoveMotionDurationSeconds * 0.5f);
            var pausedPosition = GetViewPosition(host, entityId: 30);

            InvokePauseService(host, "Pause");
            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(host.Presenter.IsPresentationPaused, Is.True);

            host.Presenter.UpdatePresentation(host.TimingProfile.MoveMotionDurationSeconds);
            Assert.That(GetViewPosition(host, entityId: 30), Is.EqualTo(pausedPosition));

            InvokePauseService(host, "Resume");
            Assert.That(host.Presenter.IsPresentationPaused, Is.False);
            host.Presenter.UpdatePresentation(host.TimingProfile.MoveMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PauseService_AstretonAirborne_DoesNotAdvanceEnemyJumpState()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, -2, 0)),
                CreateAirborneEnemy(entityId: 40, position: sourceCell),
            });
            SeedAirborneJumpState(host, 40, sourceCell, landingCell, landingTick: 30);
            var beforeTickIndex = host.TickRunner.NextTickIndex;
            var beforeState = GetEnemyJumpState(host, 40);
            var beforeEnemy = GetEntity(host, 40);

            InvokePauseService(host, "Pause");

            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 10f), Is.Zero);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(beforeTickIndex));
            var afterState = GetEnemyJumpState(host, 40);
            var afterEnemy = GetEntity(host, 40);
            Assert.That(afterState.phase, Is.EqualTo(beforeState.phase));
            Assert.That(afterState.sequence, Is.EqualTo(beforeState.sequence));
            Assert.That(afterState.sourceCell, Is.EqualTo(beforeState.sourceCell));
            Assert.That(afterState.lockedTargetCell, Is.EqualTo(beforeState.lockedTargetCell));
            Assert.That(afterState.landingTick, Is.EqualTo(beforeState.landingTick));
            Assert.That(afterEnemy.position, Is.EqualTo(beforeEnemy.position));
            Assert.That(afterEnemy.boardPresence, Is.EqualTo(beforeEnemy.boardPresence));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PauseService_AstretonAirborne_DoesNotAdvanceDeterminismHash()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, -2, 0)),
                CreateAirborneEnemy(entityId: 40, position: sourceCell),
            });
            SeedAirborneJumpState(host, 40, sourceCell, landingCell, landingTick: 30);
            var baselineTick = host.InputHost.RunSingleTick();
            Assert.That(baselineTick, Is.Not.Null);
            var beforeTickIndex = host.TickRunner.NextTickIndex;
            var beforeHash = baselineTick.DeterminismHash;
            var beforeLastResult = host.TickRunner.LastResult;

            InvokePauseService(host, "Pause");
            host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 10f);
            var blockedTick = host.InputHost.RunSingleTick();

            Assert.That(blockedTick, Is.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(beforeTickIndex));
            Assert.That(host.TickRunner.LastResult, Is.SameAs(beforeLastResult));
            Assert.That(host.TickRunner.LastResult.DeterminismHash, Is.EqualTo(beforeHash));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PauseService_AstretonAirborne_DoesNotCreateNewTickResult()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, -2, 0)),
                CreateAirborneEnemy(entityId: 40, position: sourceCell),
            });
            SeedAirborneJumpState(host, 40, sourceCell, landingCell, landingTick: 30);
            var tickCompletedCount = 0;
            host.InputHost.TickCompleted += _ => tickCompletedCount++;

            InvokePauseService(host, "Pause");
            var singleTick = host.InputHost.RunSingleTick();
            var advancedTickCount = host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 10f);

            Assert.That(singleTick, Is.Null);
            Assert.That(advancedTickCount, Is.Zero);
            Assert.That(tickCompletedCount, Is.Zero);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipPresentation_DoesNotBlockSubsequentTicks()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();
            var startTick = host.InputHost.RunSingleTick();

            Assert.That(startTick, Is.Not.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            host.InputHost.SetRawMoveInput(Vector2.zero);
            var executeTick = host.InputHost.RunSingleTick();

            Assert.That(executeTick, Is.Not.Null);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_PresenterRefreshesTransformAfterTick()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_InputActionCallback_ProducesTickMove()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: actions);

            Press(_keyboard.dKey);
            yield return null;

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);

            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_KeyboardPerpendicularPress_UsesNewestAxisDirection()
        {
            yield return AssertKeyboardTurnDestination(_keyboard.wKey, _keyboard.dKey, "(1,0)");
            yield return AssertKeyboardTurnDestination(_keyboard.wKey, _keyboard.aKey, "(-1,0)");
            yield return AssertKeyboardTurnDestination(_keyboard.dKey, _keyboard.wKey, "(0,1)");
            yield return AssertKeyboardTurnDestination(_keyboard.aKey, _keyboard.sKey, "(0,-1)");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_RuntimeMovementSchemeChange_RebuildsKeyboardOrderForArrowKeys()
        {
            var actions = CreateKeyboardMoveActions(includeArrowKeys: true);
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: actions,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            using var service = new KeyboardBindingSettingsService(actions, new PlayModeKeyboardBindingStore());

            var result = service.SetMovementScheme(KeyboardMovementScheme.ArrowKeys);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.Success));

            Press(_keyboard.upArrowKey);
            yield return null;
            Press(_keyboard.rightArrowKey);
            yield return null;

            var firstTick = host.InputHost.RunSingleTick();

            Assert.That(firstTick, Is.Not.Null);
            Assert.That(firstTick.Trace.Text, Does.Contain("Movement.RawIntents"));
            Assert.That(firstTick.Trace.Text, Does.Contain("Destination=(1,0)|Command=Move"));

            Release(_keyboard.rightArrowKey);
            Release(_keyboard.upArrowKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_MoveIntoPushBox_DoesNotSlideWithoutPushInput()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                actions: actions);

            Press(_keyboard.dKey);
            yield return null;

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerMove_PlayMode_PushStartedEdgeOnly_HeldKeyDoesNotAutoRetrigger()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 6, 0)),
                },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            Press(_keyboard.dKey);
            Press(_keyboard.eKey);
            yield return null;

            var startTick = host.InputHost.RunSingleTick();
            var startSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(startTick, Is.Not.Null);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startSnapshot.TryGetPlayerControlState(10, out var startedState), Is.True);
            Assert.That(startedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(startedState.activeAction.sequence, Is.EqualTo(1));
            Assert.That(startedState.activeAction.executionAttempted, Is.False);

            var executeTick = host.InputHost.RunSingleTick();
            var executeSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(executeTick, Is.Not.Null);
            Assert.That(executeSnapshot.TryGetEntity(30, out var movedBox), Is.True);
            Assert.That(movedBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));

            var clearTick = host.InputHost.RunSingleTick();
            var clearSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(clearTick, Is.Not.Null);
            Assert.That(clearSnapshot.TryGetPlayerControlState(10, out var clearedState), Is.True);
            Assert.That(clearedState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(clearedState.actionSequenceCounter, Is.EqualTo(1));

            var heldTick = host.InputHost.RunSingleTick();
            var heldSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(heldTick, Is.Not.Null);
            Assert.That(heldSnapshot.TryGetPlayerControlState(10, out var heldState), Is.True);
            Assert.That(heldState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(heldState.actionSequenceCounter, Is.EqualTo(1));
            Assert.That(heldSnapshot.TryGetEntity(30, out var heldBox), Is.True);
            Assert.That(heldBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));

            Release(_keyboard.eKey);
            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushStartedEdgeOnly_KeyboardAndUiPushSameTick_ConsumesOnceAndUiDirectionWins()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                CreateWall(entityId: 91, position: new SurfaceCell(FaceId.Floor, -4, 0)),
            },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            Press(_keyboard.aKey);
            Press(_keyboard.eKey);
            yield return null;

            InvokeInputHostBufferUiPush(host.InputHost, Direction.Right);
            var startTick = host.InputHost.RunSingleTick();
            var startSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(startTick, Is.Not.Null);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startSnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.sequence, Is.EqualTo(1));
            Assert.That(controlState.activeAction.direction, Is.EqualTo(Direction.Right));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(30));

            var executeTick = host.InputHost.RunSingleTick();
            var executeSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(executeTick, Is.Not.Null);
            Assert.That(executeSnapshot.TryGetEntity(30, out var rightBox), Is.True);
            Assert.That(executeSnapshot.TryGetEntity(31, out var leftBox), Is.True);
            Assert.That(rightBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(leftBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));

            Release(_keyboard.eKey);
            Release(_keyboard.aKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_ItemPickup_HidesOriginalItemBeforeNextMoveWhileConsumeEffectContinues()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
                },
                moveMotionDurationSeconds: 0.05f,
                repeatedMoveIntervalSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                itemConsumeEffectDurationSeconds: 0.3f);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();

            Assert.That(host.ViewRegistry.TryGetView(30, out var itemView), Is.True);
            Assert.That(itemView.gameObject.activeSelf, Is.False);

            host.Presenter.UpdatePresentation(host.TimingProfile.MoveMotionDurationSeconds);

            EntityState playerEntity = default;
            var reachedNextTile = false;
            for (var i = 0; i < 4; i++)
            {
                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.RunSingleTick();

                var snapshot = CaptureAuthoritativeSnapshot(host);
                Assert.That(snapshot.TryGetEntity(10, out playerEntity), Is.True);
                Assert.That(itemView.gameObject.activeSelf, Is.False);
                if (playerEntity.position == new SurfaceCell(FaceId.Floor, 2, 0))
                {
                    reachedNextTile = true;
                    break;
                }
            }

            Assert.That(reachedNextTile, Is.True, "Player never completed the follow-up move while the consume effect was active.");
            Assert.That(playerEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(itemView.gameObject.activeSelf, Is.False);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushDestroyBox_HidesOriginalBoxWhileDestroyEffectContinues()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 2, 0)),
                },
                boxDestroyEffectDurationSeconds: 0.3f);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.InputHost.RunSingleTick();

            Assert.That(host.ViewRegistry.TryGetView(30, out var boxView), Is.True);
            Assert.That(boxView.gameObject.activeSelf, Is.False);

            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            Assert.That(boxView.gameObject.activeSelf, Is.False);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayMode_BottomToFrontSlide_EnemyOnSuppressedFrontBarricade_WhenEnemyDies_RemovesBoxView()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 8);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, -8);
            var slidingBox = CreateBox(20, sourceCell, BoxCapabilities.Push);
            slidingBox.state = EntityPhaseState.Sliding;
            slidingBox.stateTimer = 0;
            slidingBox.facing = Direction.Up;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateUnit(30, barricadeCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            enemy.teamId = 2;
            enemy.unitRole = UnitRole.Enemy;
            enemy.aiMode = EnemyAiMode.Chase;
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    slidingBox,
                    enemy,
                },
                initialTileFeatures: new[]
                {
                    CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade),
                },
                tileFeatureDefinitions: new[]
                {
                    CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                },
                initialTopology: new CubeTopologyState(FaceId.Floor));

            Assert.That(host.ViewRegistry.TryGetView(20, out var boxView), Is.True);
            Assert.That(boxView.gameObject.activeSelf, Is.True);

            var result = host.InputHost.RunSingleTick();
            var finalSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(result, Is.Not.Null);
            WriteBottomToFrontPlayModeTrace(result, finalSnapshot, sourceCell, barricadeCell, boxView);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.True);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 20 && signal.ExitCause == TickEntityExitCause.BoxDestroy), Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
            Assert.That(boxView.gameObject.activeSelf, Is.False);

            yield return DestroyHost(host);
        }

        private static void WriteBottomToFrontPlayModeTrace(
            TickResult result,
            WorldSnapshot finalSnapshot,
            SurfaceCell sourceCell,
            SurfaceCell barricadeCell,
            GameplayEntityView boxView)
        {
            TestContext.WriteLine("BottomToFrontPlayModeTrace.Presentation");
            foreach (var tileEvent in result.PresentationData.TileEvents)
            {
                TestContext.WriteLine(
                    $"  TileEvent={tileEvent.EventKind}|Tile={tileEvent.TileId}|Cell={tileEvent.Cell}|Target={tileEvent.TargetEntityId}");
            }

            foreach (var exitSignal in result.PresentationData.EntityExitSignals)
            {
                TestContext.WriteLine(
                    $"  ExitSignal=E{exitSignal.ExitedEntityId}|Cause={exitSignal.ExitCause}|Source={exitSignal.SourceCell}|Target={exitSignal.PresentationTargetCell}|Timing={exitSignal.Timing}");
            }

            TestContext.WriteLine("BottomToFrontPlayModeTrace.Final");
            if (finalSnapshot.TryGetEntity(20, out var boxAfter))
            {
                TestContext.WriteLine(
                    $"  Box=exists|Cell={boxAfter.position}|Presence={boxAfter.boardPresence}|State={boxAfter.state}|Timer={boxAfter.stateTimer}|Facing={boxAfter.facing}|Hp={boxAfter.hp}|Marked={boxAfter.markedForDeath}");
            }
            else
            {
                TestContext.WriteLine("  Box=missing");
            }

            if (finalSnapshot.TryGetEntity(30, out var enemyAfter))
            {
                TestContext.WriteLine(
                    $"  Enemy=exists|Cell={enemyAfter.position}|Presence={enemyAfter.boardPresence}|Hp={enemyAfter.hp}|Marked={enemyAfter.markedForDeath}");
            }
            else
            {
                TestContext.WriteLine("  Enemy=missing");
            }

            TestContext.WriteLine(
                finalSnapshot.TryGetSolidSemanticAt(sourceCell, out var sourceSolid)
                    ? $"  SourceSolid=E{sourceSolid.Entity.entityId}"
                    : "  SourceSolid=none");
            TestContext.WriteLine(
                finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out var barricadeSolid)
                    ? $"  DestinationSolid=E{barricadeSolid.Entity.entityId}"
                    : "  DestinationSolid=none");
            TestContext.WriteLine($"  BoxViewActive={boxView.gameObject.activeSelf}");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipBufferedAtTickBoundary_PrioritizesFlipOverMove()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();

            host.InputHost.RunSingleTick();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_PlayMode_FlipLeft_RootEndsFacingRight()
        {
            var playerViewPrefab = LoadPlayerS1ViewPrefab();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                playerViewPrefabOverride: playerViewPrefab,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();

            var startTick = host.InputHost.RunSingleTick();
            var startSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(startTick, Is.Not.Null);
            Assert.That(startTick.PresentationData.PlayerFlipResultTurnSignals, Has.Count.EqualTo(1));
            Assert.That(startTick.PresentationData.PlayerFlipResultTurnSignals[0].ContactFacing, Is.EqualTo(Direction.Left));
            Assert.That(startTick.PresentationData.PlayerFlipResultTurnSignals[0].ResultFacing, Is.EqualTo(Direction.Right));
            Assert.That(startSnapshot.TryGetEntity(10, out var startPlayer), Is.True);
            Assert.That(startPlayer.facing, Is.EqualTo(Direction.Left));

            host.InputHost.SetRawMoveInput(Vector2.zero);
            var executeTick = RunTicksUntil(
                host,
                result => result.Trace.Text.Contains("FlipResultFacingCommitted", StringComparison.Ordinal),
                maxTicks: 64);
            var executeSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(executeTick, Is.Not.Null, "Player_S1 Flip never reached the result-facing commit tick.");
            Assert.That(executeSnapshot.TryGetEntity(10, out var executePlayer), Is.True);
            Assert.That(executePlayer.facing, Is.EqualTo(Direction.Right));

            var cleanupTick = RunTicksUntil(
                host,
                _ =>
                {
                    var snapshot = CaptureAuthoritativeSnapshot(host);
                    return snapshot.TryGetPlayerControlState(10, out var controlState) &&
                           controlState.activeAction.kind == PlayerActionKind.None;
                },
                maxTicks: 64);
            Assert.That(cleanupTick, Is.Not.Null, "Player_S1 Flip action never completed cleanup.");
            AdvancePresentation(host, host.TimingProfile.FlipMotionDurationSeconds + 5f);
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushStartedEdgeOnly_HeldKeyDuringRecovery_DoesNotAutoRestartAfterCompletion()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 6, 0)),
            },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 3));

            Press(_keyboard.dKey);
            Press(_keyboard.eKey);
            yield return null;

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);

            var heldAfterRecoveryTick = host.InputHost.RunSingleTick();
            var heldAfterRecoverySnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(heldAfterRecoveryTick, Is.Not.Null);
            Assert.That(heldAfterRecoverySnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.actionSequenceCounter, Is.EqualTo(1));
            Assert.That(heldAfterRecoverySnapshot.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));

            Release(_keyboard.eKey);
            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipBufferedDuringRepeatLock_IsNotDropped()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipVisualHold_YieldsImmediatelyToNewWalkPresentation()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                repeatedMoveIntervalSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                moveMotionDurationSeconds: 1f,
                flipPresentationDurationSeconds: 0.5f);

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));

            var followupMoveTick = host.InputHost.RunSingleTick();
            Assert.That(followupMoveTick, Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);

            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

            host.Presenter.UpdatePresentation(0.5f);
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_NoSampledDirection_DropsBufferedPushAndFlip()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(new Vector2(1f, 1f));
            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);
            AssertViewMatchesProjectedState(host, entityId: 31);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_InteractionTick_DoesNotConsumePlainMoveCadence()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushStartedEdgeOnly_ReleaseThenFreshPressStartsNextPush()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 6, 0)),
                },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            Press(_keyboard.dKey);
            Press(_keyboard.eKey);
            yield return null;

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);

            Release(_keyboard.eKey);
            yield return null;

            var releasedTick = host.InputHost.RunSingleTick();
            var releasedSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(releasedTick, Is.Not.Null);
            Assert.That(releasedSnapshot.TryGetPlayerControlState(10, out var releasedState), Is.True);
            Assert.That(releasedState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(releasedState.actionSequenceCounter, Is.EqualTo(1));

            Press(_keyboard.eKey);
            yield return null;

            var restartTick = host.InputHost.RunSingleTick();
            var restartSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(restartTick, Is.Not.Null);
            Assert.That(restartTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(restartSnapshot.TryGetPlayerControlState(10, out var restartState), Is.True);
            Assert.That(restartState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(restartState.activeAction.sequence, Is.EqualTo(2));

            var secondExecuteTick = host.InputHost.RunSingleTick();
            var secondExecuteSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(secondExecuteTick, Is.Not.Null);
            Assert.That(secondExecuteSnapshot.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 0)));

            Release(_keyboard.eKey);
            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipBufferedDuringDirectionChangeDelay_UsesSampledDirection()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                actions: null,
                staticEntityLogics: null,
                initialMoveDelaySeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                directionChangeConsumesDelay: true);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            for (var i = 0; i < host.TimingProfile.InitialMoveDelayTicks - 1; i++)
            {
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(0f);
                AssertViewMatchesProjectedState(host, entityId: 10);
            }

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_Reenable_RebindsInputActions()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: actions);

            host.InputHost.enabled = false;
            yield return null;

            Press(_keyboard.dKey);
            yield return null;

            host.InputHost.enabled = true;
            yield return null;

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);

            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_HoldInputRepeatsAtConfiguredTickInterval()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            for (var i = 0; i < host.TimingProfile.RepeatedMoveIntervalTicks - 1; i++)
            {
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(0f);
            }

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_TapRelease_BuffersAcrossShortCooldown()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                repeatedMoveIntervalSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.SetRawMoveInput(Vector2.zero);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerMove_PlayMode_SpawnedEntity_BecomesVisibleAfterTick()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: null,
                staticEntityLogics: new IEntityLogic[]
                {
                    new FireProjectileLogic(sourceId: 10, priority: 5),
                });

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.ViewRegistry.TryGetView(11, out var projectileView), Is.True);
            Assert.That(projectileView.gameObject.activeSelf, Is.True);
            AssertViewMatchesProjectedState(host, entityId: 11);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerMove_PlayMode_BlockedCell_DoesNotVisuallyDrift()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 1, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        private static GameplaySceneHost CreateHost(EntityState[] initialEntities)
        {
            return CreateHost(initialEntities, actions: null, staticEntityLogics: null);
        }

        private static GameplaySceneHost CreateHost(
            EntityState[] initialEntities,
            InputActionAsset actions = null,
            IEntityLogic[] staticEntityLogics = null,
            float initialMoveDelaySeconds = GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
            float repeatedMoveIntervalSeconds = GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
            bool directionChangeConsumesDelay = false,
            float moveMotionDurationSeconds = 0.2f,
            float itemConsumeEffectDurationSeconds = -1f,
            float boxDestroyEffectDurationSeconds = -1f,
            float pushPresentationDurationSeconds = -1f,
            float flipPresentationDurationSeconds = -1f,
            PlayerControlTimingSettings playerControlTiming = null,
            TopologyTransitionPostFxProfile topologyTransitionPostFxProfile = null,
            Camera viewCamera = null,
            GameplayEntityView playerViewPrefabOverride = null,
            GameplayRuntimeFeatureFlags? runtimeFeatureFlags = null,
            TileFeatureState[] initialTileFeatures = null,
            TileFeatureRuntimeDefinition[] tileFeatureDefinitions = null,
            CubeTopologyState? initialTopology = null)
        {
            return CreateHostCore(
                initialEntities,
                actions,
                staticEntityLogics,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
                directionChangeConsumesDelay,
                moveMotionDurationSeconds,
                itemConsumeEffectDurationSeconds,
                boxDestroyEffectDurationSeconds,
                pushPresentationDurationSeconds,
                flipPresentationDurationSeconds,
                playerControlTiming,
                topologyTransitionPostFxProfile,
                viewCamera,
                playerViewPrefabOverride,
                runtimeFeatureFlags,
                initialTileFeatures,
                tileFeatureDefinitions,
                initialTopology);
        }

        private static GameplaySceneHost CreateHostCore(
            EntityState[] initialEntities,
            InputActionAsset actions,
            IEntityLogic[] staticEntityLogics,
            float initialMoveDelaySeconds,
            float repeatedMoveIntervalSeconds,
            bool directionChangeConsumesDelay,
            float moveMotionDurationSeconds,
            float itemConsumeEffectDurationSeconds,
            float boxDestroyEffectDurationSeconds,
            float pushPresentationDurationSeconds,
            float flipPresentationDurationSeconds,
            PlayerControlTimingSettings playerControlTiming,
            TopologyTransitionPostFxProfile topologyTransitionPostFxProfile,
            Camera viewCamera,
            GameplayEntityView playerViewPrefabOverride,
            GameplayRuntimeFeatureFlags? runtimeFeatureFlags,
            TileFeatureState[] initialTileFeatures,
            TileFeatureRuntimeDefinition[] tileFeatureDefinitions,
            CubeTopologyState? initialTopology)
        {
            var hostObject = new GameObject("PlayModeGameplaySceneHost");
            var host = hostObject.AddComponent<GameplaySceneHost>();
            var playerViewPrefab = playerViewPrefabOverride;
            PlayerAnimationTimingAuthoring playerTimingAuthoring = null;
            if (playerViewPrefab == null)
            {
                var playerViewPrefabObject = new GameObject("PlayModeGameplaySceneHost_PlayerViewPrefab");
                playerViewPrefabObject.transform.SetParent(hostObject.transform, worldPositionStays: false);
                playerViewPrefab = playerViewPrefabObject.AddComponent<GameplayEntityView>();
                playerViewPrefab.Initialize(10);
                playerTimingAuthoring = playerViewPrefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
                playerViewPrefabObject.AddComponent<PlayerAnimatorDriver>();
            }

            if (playerTimingAuthoring != null &&
                pushPresentationDurationSeconds > 0f)
            {
                SetSerializedField(playerTimingAuthoring, "legacyPushAnimatorDurationSeconds", pushPresentationDurationSeconds);
            }

            if (playerTimingAuthoring != null &&
                flipPresentationDurationSeconds > 0f)
            {
                SetSerializedField(playerTimingAuthoring, "legacyFlipAnimatorDurationSeconds", flipPresentationDurationSeconds);
            }

            var configuration = new GameplaySceneHostConfiguration
            {
                Actions = actions,
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                BoxSlideStepIntervalSeconds = 0.2f,
                CellSize = 1f,
                DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                FlipArcHeightInCells = 0.65f,
                FlipMotionDurationSeconds = 0.2f,
                InitialBoardBounds = new BoardBounds(new Vector2Int(-8, -8), new Vector2Int(8, 8)),
                InitialMoveDelaySeconds = initialMoveDelaySeconds,
                InitialEntities = initialEntities,
                InitialTileFeatures = initialTileFeatures ?? Array.Empty<TileFeatureState>(),
                TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>(),
                InitialTopology = initialTopology ?? new CubeTopologyState(FaceId.Floor),
                MaxTicksPerFrame = 8,
                MoveDeadzone = 0.5f,
                MoveMotionDurationSeconds = moveMotionDurationSeconds,
                ItemConsumeEffectDurationSeconds = itemConsumeEffectDurationSeconds,
                BoxDestroyEffectDurationSeconds = boxDestroyEffectDurationSeconds,
                PlayerEntityId = 10,
                PlayerControlTiming = playerControlTiming ?? new PlayerControlTimingSettings(),
                PlayerViewPrefab = playerViewPrefab,
                PushMotionDurationSeconds = 0.2f,
                RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds,
                SimulationTicksPerSecond = 60,
                StaticEntityLogics = staticEntityLogics ?? System.Array.Empty<IEntityLogic>(),
                SnapViewCameraToTarget = viewCamera != null,
                TopologyTransitionPostFxProfile = topologyTransitionPostFxProfile ?? TopologyTransitionPostFxProfile.CreateDefault(),
                ViewCamera = viewCamera,
            };
            if (runtimeFeatureFlags.HasValue)
            {
                configuration.ApplyRuntimeFeatureFlags(runtimeFeatureFlags.Value);
            }

            host.Initialize(configuration);

            return host;
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = facing,
            };
        }

        private static EntityState CreateAirborneEnemy(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                aiMode = EnemyAiMode.Chase,
                boardPresence = EntityBoardPresence.Detached,
            };
        }

        private static void SeedAirborneJumpState(
            GameplaySceneHost host,
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell landingCell,
            int landingTick)
        {
            InvokeWorldWriteContextMethod(
                host.WorldState,
                "SetEnemyJumpState",
                entityId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = landingCell,
                    landingTick = landingTick,
                });
        }

        private static EnemyJumpRuntimeState GetEnemyJumpState(GameplaySceneHost host, int entityId)
        {
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEnemyJumpState(entityId, out var state), Is.True);
            return state;
        }

        private static EntityState GetEntity(GameplaySceneHost host, int entityId)
        {
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return CreateWall(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        private static EntityState CreateBox(int entityId, Vector2Int position, BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = capabilities,
            };
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureFlags flags = TileFeatureFlags.None)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                flags,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
        }

        private static IEnumerator DestroyHost(GameplaySceneHost host, UnityEngine.Object ownedActions = null)
        {
            if (host != null)
            {
                UnityEngine.Object.Destroy(host.gameObject);
            }

            if (ownedActions != null)
            {
                UnityEngine.Object.Destroy(ownedActions);
            }

            yield return null;
        }

        private IEnumerator AssertKeyboardTurnDestination(KeyControl heldKey, KeyControl newKey, string expectedDestination)
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: actions,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            Press(heldKey);
            yield return null;
            Press(newKey);
            yield return null;

            var firstTick = host.InputHost.RunSingleTick();

            Assert.That(firstTick, Is.Not.Null);
            Assert.That(firstTick.Trace.Text, Does.Contain("Movement.RawIntents"));
            Assert.That(firstTick.Trace.Text, Does.Contain($"Destination={expectedDestination}|Command=Move"));

            Release(newKey);
            Release(heldKey);
            yield return DestroyHost(host, actions);
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetAuthoritativeTopology(WorldState worldState, CubeTopologyState topology)
        {
            InvokeWorldWriteContextMethod(worldState, "SetTopology", topology);
        }

        private static void ApplyAuthoritativeDamage(WorldState worldState, int entityId, int amount)
        {
            InvokeWorldWriteContextMethod(worldState, "ApplyDamage", entityId, amount);
        }

        private static void InvokeWorldWriteContextMethod(WorldState worldState, string methodName, params object[] arguments)
        {
            var createWriteContextMethod = typeof(WorldState).GetMethod(
                "CreateWriteContext",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(createWriteContextMethod, Is.Not.Null, "WorldState.CreateWriteContext should remain available for authoritative playmode setup.");

            var writeContext = createWriteContextMethod.Invoke(worldState, parameters: null);
            Assert.That(writeContext, Is.Not.Null);

            var argumentTypes = new Type[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
            {
                argumentTypes[index] = arguments[index].GetType();
            }

            var method = writeContext.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: argumentTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {writeContext.GetType().Name}.");
            method.Invoke(writeContext, arguments);
        }

        private static void InvokeInputHostBufferUiPush(GameplayInputHost inputHost, Direction direction)
        {
            var method = typeof(GameplayInputHost).GetMethod(
                "BufferUiPush",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "GameplayInputHost.BufferUiPush should remain available for UI push merge coverage.");
            method.Invoke(inputHost, new object[] { direction });
        }

        // TODO(CubeSurface3D): Retire this helper once playmode tests stop using
        // projector-derived world positions as their primary oracle. Preserve the scenario
        // coverage in this file instead of deleting the tests.
        private static Vector3 GetViewPosition(GameplaySceneHost host, int entityId)
        {
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            return view.transform.position;
        }

        private static void InvokePauseService(GameplaySceneHost host, string methodName)
        {
            var uiAccess = (object)host.UiAccess;
            Assert.That(uiAccess, Is.Not.Null);
            var pauseServiceProperty = uiAccess
                .GetType()
                .GetProperty("PauseService", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(pauseServiceProperty, Is.Not.Null);

            var pauseService = pauseServiceProperty.GetValue(uiAccess);
            Assert.That(pauseService, Is.Not.Null);
            var method = pauseService.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(pauseService, Array.Empty<object>());
        }

        private static GameplayEntityView LoadPlayerS1ViewPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(PlayerS1PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing Player_S1 prefab at {PlayerS1PrefabPath}.");
            return prefab;
        }

        private static TickResult RunTicksUntil(
            GameplaySceneHost host,
            Func<TickResult, bool> predicate,
            int maxTicks)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                var result = host.InputHost.RunSingleTick();
                Assert.That(result, Is.Not.Null, $"Expected tick {i + 1} while waiting for playmode condition.");
                if (predicate(result))
                {
                    return result;
                }
            }

            return null;
        }

        private static void AdvancePresentation(GameplaySceneHost host, float durationSeconds)
        {
            var stepSeconds = Mathf.Max(host.TimingProfile.SimulationTickIntervalSeconds, 1f / 60f);
            var elapsedSeconds = 0f;
            while (elapsedSeconds < durationSeconds)
            {
                var deltaSeconds = Mathf.Min(stepSeconds, durationSeconds - elapsedSeconds);
                host.Presenter.UpdatePresentation(deltaSeconds);
                elapsedSeconds += deltaSeconds;
            }
        }

        private static void AssertViewMatchesProjectedState(GameplaySceneHost host, int entityId)
        {
            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            var projector = new GameplayCubeProjector(snapshot.BoardBounds, 1f);
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            Assert.That(
                projector.TryProjectEntityCell(entity.position, snapshot.Topology, entity.type, out var projectedPose),
                Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(entity.position, snapshot.Topology, entity.facing, out var projectedRotation),
                Is.True);
            Assert.That(
                view.transform.position,
                Is.EqualTo(host.BoardRoot.transform.TransformPoint(projectedPose.LocalPosition)));
            Assert.That(
                Quaternion.Angle(
                    view.transform.rotation,
                    host.BoardRoot.transform.rotation * projectedRotation),
                Is.LessThan(0.1f));
        }

        private static void AssertViewFacing(GameplaySceneHost host, int entityId, Direction expectedFacing)
        {
            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            var projector = new GameplayCubeProjector(snapshot.BoardBounds, 1f);
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(entity.position, snapshot.Topology, expectedFacing, out var projectedRotation),
                Is.True);
            Assert.That(
                Quaternion.Angle(
                    view.transform.rotation,
                    host.BoardRoot.transform.rotation * projectedRotation),
                Is.LessThan(0.1f));
        }

        private static WorldSnapshot CaptureAuthoritativeSnapshot(GameplaySceneHost host)
        {
            return GameplayCompositionRoot.CreateSnapshot(host.WorldState);
        }

        private static class GameplayCameraRigReflectionAdapter
        {
            private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            private const string ConfigureTopologyTransitionCameraShakeMethodName = "ConfigureTopologyTransitionCameraShake";
            private const string ApplyTopologyTransitionVisualStateMethodName = "ApplyTopologyTransitionVisualState";
            private const string TopologyTransitionShakeLocalPositionPropertyName = "TopologyTransitionShakeLocalPosition";
            private const string TopologyTransitionShakeLocalRotationPropertyName = "TopologyTransitionShakeLocalRotation";

            private static readonly MethodInfo ConfigureTopologyTransitionCameraShakeMethod =
                GetRequiredMethod(ConfigureTopologyTransitionCameraShakeMethodName, parameterCount: 1);

            private static readonly MethodInfo ApplyTopologyTransitionVisualStateMethod =
                GetRequiredMethod(ApplyTopologyTransitionVisualStateMethodName, parameterCount: 1);

            private static readonly PropertyInfo TopologyTransitionShakeLocalPositionProperty =
                GetRequiredProperty(TopologyTransitionShakeLocalPositionPropertyName);

            private static readonly PropertyInfo TopologyTransitionShakeLocalRotationProperty =
                GetRequiredProperty(TopologyTransitionShakeLocalRotationPropertyName);

            public static void ConfigureTopologyTransitionCameraShake(
                GameplayCameraRig rig,
                TopologyTransitionCameraShakeProfile profile)
            {
                InvokeRequired(ConfigureTopologyTransitionCameraShakeMethod, rig, profile);
            }

            public static void ApplyTopologyTransitionVisualState(
                GameplayCameraRig rig,
                TopologyTransitionVisualState visualState)
            {
                InvokeRequired(ApplyTopologyTransitionVisualStateMethod, rig, visualState);
            }

            public static Vector3 GetTopologyTransitionShakeLocalPosition(GameplayCameraRig rig)
            {
                return (Vector3)GetRequiredValue(TopologyTransitionShakeLocalPositionProperty, rig);
            }

            public static Quaternion GetTopologyTransitionShakeLocalRotation(GameplayCameraRig rig)
            {
                return (Quaternion)GetRequiredValue(TopologyTransitionShakeLocalRotationProperty, rig);
            }

            private static MethodInfo GetRequiredMethod(string name, int parameterCount)
            {
                return typeof(GameplayCameraRig)
                    .GetMethods(InstanceFlags)
                    .Single(method => method.Name == name && method.GetParameters().Length == parameterCount);
            }

            private static PropertyInfo GetRequiredProperty(string name)
            {
                return typeof(GameplayCameraRig).GetProperty(name, InstanceFlags) ??
                       throw new InvalidOperationException($"Missing internal GameplayCameraRig property '{name}'.");
            }

            private static object GetRequiredValue(PropertyInfo property, GameplayCameraRig rig)
            {
                if (rig == null)
                {
                    throw new ArgumentNullException(nameof(rig));
                }

                return property.GetValue(rig) ??
                       throw new InvalidOperationException($"Property '{property.Name}' returned null unexpectedly.");
            }

            private static void InvokeRequired(MethodInfo method, GameplayCameraRig rig, object argument)
            {
                if (rig == null)
                {
                    throw new ArgumentNullException(nameof(rig));
                }

                method.Invoke(rig, new[] { argument });
            }
        }

        private static InputActionAsset CreateKeyboardMoveActions(bool includeArrowKeys = false)
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            var move = map.AddAction("Move", InputActionType.Value);
            var pushAction = map.AddAction("Push", InputActionType.Button);
            var flipAction = map.AddAction("Flip", InputActionType.Button);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            if (includeArrowKeys)
            {
                move.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
            }

            pushAction.AddBinding("<Keyboard>/e");
            flipAction.AddBinding("<Keyboard>/q");
            actions.AddActionMap(map);
            return actions;
        }

        private static PlayerControlTimingSettings CreatePushTimingSettings(
            int pushExecuteDelayTicks,
            int pushInputLockDurationTicks)
        {
            var ticksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond;
            return new PlayerControlTimingSettings
            {
                PushExecuteDelaySeconds = pushExecuteDelayTicks / (float)ticksPerSecond,
                PushInputLockDurationSeconds = pushInputLockDurationTicks / (float)ticksPerSecond,
            };
        }

        private sealed class FireProjectileLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _priority;
            private readonly int _sourceId;

            public FireProjectileLogic(int sourceId, int priority)
            {
                _sourceId = sourceId;
                _priority = priority;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
            {
                if (snapshot.TryGetEntity(_sourceId, out var source) && source.hp > 0 && !source.markedForDeath)
                {
                    buffer.Add(RawAttackIntent.CreateFireProjectile(_sourceId, _priority));
                }
            }
        }

        private sealed class PlayModeKeyboardBindingStore : IKeyboardBindingStore
        {
            private KeyboardMovementScheme? _movementScheme;
            private string _bindingOverridesJson;

            public bool TryLoadMovementScheme(out KeyboardMovementScheme scheme)
            {
                scheme = _movementScheme ?? KeyboardMovementScheme.Wasd;
                return _movementScheme.HasValue;
            }

            public void SaveMovementScheme(KeyboardMovementScheme scheme)
            {
                _movementScheme = scheme;
            }

            public bool TryLoadBindingOverridesJson(out string json)
            {
                json = _bindingOverridesJson;
                return !string.IsNullOrWhiteSpace(json);
            }

            public void SaveBindingOverridesJson(string json)
            {
                _bindingOverridesJson = json;
            }

            public void ClearBindingOverridesJson()
            {
                _bindingOverridesJson = null;
            }

            public void Save()
            {
            }
        }
    }
}
