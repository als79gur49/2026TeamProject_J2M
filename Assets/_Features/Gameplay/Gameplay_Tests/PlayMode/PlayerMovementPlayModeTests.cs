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
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Shared.Input;
using NUnit.Framework;
using UnityEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    // Final presentation guardrail:
    // Keep the input/tick scenarios in this file and validate them through the
    // cube projector plus board-root world transform instead of strip-space literals.
    public sealed partial class PlayerMovementPlayModeTests : InputTestFixture
    {
        private const string PlayerS1PrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private const string TutorialPassiveContactProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";
        private const string JumpChaserProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset";
        private const string CampaignCameraShakeProfilePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Authoring/GameplayCameraShakeProfile_CampaignV1.asset";
        private const float ProjectedViewPositionTolerance = 1f / SimulationFixed.UnitsPerCell;
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
        public IEnumerator CampaignSaveFailure_StopsCatchUpBeforeSecondTickAndSurvivesPauseRelease()
        {
            var host = CreateHost(new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            var completed = 0;
            host.InputHost.TickCompleted += _ =>
            {
                completed++;
                typeof(GameplayInputHost).GetMethod("AbandonCampaignRun", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(host.InputHost, null);
            };
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds * 6f), Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(1));
            var next = host.TickRunner.NextTickIndex;
            typeof(GameplayInputHost).GetMethod("SetSimulationPaused", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(host.InputHost, new object[] { false });
            Assert.That(host.InputHost.AdvanceTime(1f), Is.Zero);
            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(next));
            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PresentationLock_DuringTopologyTransitionPreventsTickAndBurst()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });
            SetPlayerForwardTopologyOvershootPose(host, 10);

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
            SetPlayerForwardTopologyOvershootPose(host, 10);

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds), Is.EqualTo(1));
            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.06f);
            var pausedProgress = host.Presenter.CurrentTopologyTransitionVisualState.Progress01;
            var cameraRig = host.GetComponent<GameplayCameraRig>();
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(
                cameraRig.AdditiveLocalPosition.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(cameraRig.AdditiveLocalRotation, Quaternion.identity) > 0.001f,
                Is.True,
                "The pause regression must begin from a non-zero topology camera contribution.");

            InvokePauseService(host, "Pause");
            Assert.That(cameraRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(cameraRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(host.BoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(host.BoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.Progress01, Is.EqualTo(pausedProgress).Within(0.0001f));
            Assert.That(cameraRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(cameraRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));

            InvokePauseService(host, "Resume");
            Assert.That(cameraRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(cameraRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
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
            SetPlayerForwardTopologyOvershootPose(host, 10);

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
            SetPlayerForwardTopologyOvershootPose(host, 10);

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
            SetPlayerForwardTopologyOvershootPose(host, 10);

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
        public IEnumerator TopologyTransitionCameraShake_PresenterDisableAndDestroy_ClearResidualAdditivePose()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });
            SetPlayerForwardTopologyOvershootPose(host, 10);

            host.InputHost.SetRawMoveInput(Vector2.up);
            Assert.That(host.InputHost.AdvanceTime(host.TimingProfile.SimulationTickIntervalSeconds), Is.EqualTo(1));
            host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.06f);

            var presenter = host.Presenter;
            var cameraRig = host.GetComponent<GameplayCameraRig>();
            var effectsRoot = host.BoardRoot.CameraEffectsRoot;
            Assert.That(cameraRig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));

            presenter.enabled = false;
            Assert.That(cameraRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(cameraRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(effectsRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(effectsRoot.localRotation, Is.EqualTo(Quaternion.identity));

            presenter.enabled = true;
            presenter.UpdatePresentation(0f);
            Assert.That(cameraRig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));

            UnityEngine.Object.Destroy(presenter);
            yield return null;
            Assert.That(effectsRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(effectsRoot.localRotation, Is.EqualTo(Quaternion.identity));

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TopologyTransitionCameraOutput_EnabledAndNoOp_PreserveAuthoritativeResultAndDeterminismHash()
        {
            TickResult cameraEnabledResult;
            var cameraEnabledHost = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });
            SetPlayerForwardTopologyOvershootPose(cameraEnabledHost, 10);
            cameraEnabledHost.InputHost.SetRawMoveInput(Vector2.up);
            cameraEnabledResult = cameraEnabledHost.InputHost.RunSingleTick();
            Assert.That(cameraEnabledResult, Is.Not.Null);
            cameraEnabledHost.Presenter.UpdatePresentation(
                cameraEnabledHost.TimingProfile.TopologyMotionDurationSeconds * 0.06f);
            Assert.That(
                cameraEnabledHost.GetComponent<GameplayCameraRig>().AdditiveLocalPosition.sqrMagnitude,
                Is.GreaterThan(0.000001f));
            yield return DestroyHost(cameraEnabledHost);

            var cameraOffHost = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });
            SetPlayerForwardTopologyOvershootPose(cameraOffHost, 10);
            cameraOffHost.Presenter.SetCameraMotionLevel(CameraMotionLevel.Off);
            cameraOffHost.InputHost.SetRawMoveInput(Vector2.up);
            var cameraOffResult = cameraOffHost.InputHost.RunSingleTick();
            Assert.That(cameraOffResult, Is.Not.Null);
            cameraOffHost.Presenter.UpdatePresentation(
                cameraOffHost.TimingProfile.TopologyMotionDurationSeconds * 0.06f);
            Assert.That(cameraOffHost.GetComponent<GameplayCameraRig>().AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(cameraOffHost.GetComponent<GameplayCameraRig>().AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(cameraOffResult.DeterminismHash, Is.EqualTo(cameraEnabledResult.DeterminismHash));
            Assert.That(cameraOffResult.FinalTopology, Is.EqualTo(cameraEnabledResult.FinalTopology));
            Assert.That(cameraOffResult.FinalEntities, Is.EqualTo(cameraEnabledResult.FinalEntities));
            Assert.That(cameraOffResult.EventLog, Is.EqualTo(cameraEnabledResult.EventLog));
            Assert.That(cameraOffHost.TickRunner.NextTickIndex, Is.EqualTo(2));
            yield return DestroyHost(cameraOffHost);

            var noOpHost = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 8)),
            });
            SetPlayerForwardTopologyOvershootPose(noOpHost, 10);
            noOpHost.Presenter.AttachCameraRig(null);
            noOpHost.InputHost.SetRawMoveInput(Vector2.up);
            var noOpResult = noOpHost.InputHost.RunSingleTick();
            Assert.That(noOpResult, Is.Not.Null);
            noOpHost.Presenter.UpdatePresentation(noOpHost.TimingProfile.TopologyMotionDurationSeconds * 0.06f);

            Assert.That(noOpResult.DeterminismHash, Is.EqualTo(cameraEnabledResult.DeterminismHash));
            Assert.That(noOpResult.FinalTopology, Is.EqualTo(cameraEnabledResult.FinalTopology));
            Assert.That(noOpResult.FinalEntities, Is.EqualTo(cameraEnabledResult.FinalEntities));
            Assert.That(noOpResult.EventLog, Is.EqualTo(cameraEnabledResult.EventLog));
            Assert.That(noOpHost.TickRunner.NextTickIndex, Is.EqualTo(2));
            Assert.That(noOpHost.InputHost.RunSingleTick(), Is.Null);

            yield return DestroyHost(noOpHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraRuntime_RigReplacement_ResetsOldBeforeAttachAndStartsNewAtIdentity()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var oldRig = host.GetComponent<GameplayCameraRig>();
            var replacementRootObject = new GameObject("PlayModeGameplayCameraRigReplacement");
            var replacementBoardRoot = replacementRootObject.AddComponent<GameplayBoardRoot>();
            replacementBoardRoot.EnsureHierarchy();
            var replacementRig = replacementRootObject.AddComponent<GameplayCameraRig>();
            replacementRig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
            replacementRig.Initialize(
                null,
                replacementBoardRoot.CameraTargetRoot,
                new Bounds(Vector3.zero, Vector3.one));

            var nonZeroPosition = new Vector3(0.01f, -0.02f, 0.03f);
            var nonZeroRotation = Quaternion.Euler(1f, 2f, 3f);
            oldRig.ApplyAdditivePose(nonZeroPosition, nonZeroRotation);
            replacementRig.ApplyAdditivePose(nonZeroPosition, nonZeroRotation);
            Assert.That(host.BoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(nonZeroPosition));
            Assert.That(replacementBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(nonZeroPosition));

            host.Presenter.AttachCameraRig(replacementRig);

            Assert.That(oldRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(oldRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(host.BoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(host.BoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(replacementRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(replacementRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(replacementBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(replacementBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));

            yield return DestroyHost(host);
            UnityEngine.Object.Destroy(replacementRootObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeMixer_PresenterSyntheticImpulse_SettingsPauseCleanupAndActiveRigReplacement()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var profile = CreateCameraShakeTestProfile();
            var oldRig = host.GetComponent<GameplayCameraRig>();
            var oldEffectsRoot = host.BoardRoot.CameraEffectsRoot;
            var replacementRootObject = new GameObject("M1PresenterSyntheticRigReplacement");
            var replacementBoardRoot = replacementRootObject.AddComponent<GameplayBoardRoot>();
            replacementBoardRoot.EnsureHierarchy();
            var replacementRig = replacementRootObject.AddComponent<GameplayCameraRig>();
            replacementRig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
            replacementRig.Initialize(
                null,
                replacementBoardRoot.CameraTargetRoot,
                new Bounds(Vector3.zero, Vector3.one));

            try
            {
                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                Assert.That(
                    host.Presenter.CameraShakeImpulseSink.Submit(new CameraShakeImpulseRequest(
                        tickIndex: 1,
                        semantic: CameraShakeSemantic.FlipFloorLanding,
                        sourceEntityId: 10,
                        sequenceOrActionPlanId: 100,
                        priority: CameraShakePriority.Medium)),
                    Is.True);
                host.Presenter.UpdatePresentation(0.1f);
                var fullPositionMagnitude = oldRig.AdditiveLocalPosition.magnitude;
                var fullRotationAngle = Quaternion.Angle(oldRig.AdditiveLocalRotation, Quaternion.identity);
                Assert.That(fullPositionMagnitude, Is.GreaterThan(0.000001f));
                Assert.That(fullRotationAngle, Is.GreaterThan(0.001f));

                host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Reduced);
                Assert.That(oldRig.AdditiveLocalPosition.magnitude, Is.LessThan(fullPositionMagnitude));
                Assert.That(
                    Quaternion.Angle(oldRig.AdditiveLocalRotation, Quaternion.identity),
                    Is.LessThan(fullRotationAngle));
                host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Off);
                Assert.That(oldRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(oldRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
                host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
                Assert.That(oldRig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));

                host.Presenter.AttachCameraRig(replacementRig);
                Assert.That(oldEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(oldEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(replacementBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(replacementBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                host.Presenter.UpdatePresentation(0f);
                Assert.That(replacementRig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));

                host.Presenter.SetPresentationPaused(true);
                Assert.That(replacementRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(replacementRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
                host.Presenter.SetPresentationPaused(false);
                host.Presenter.UpdatePresentation(0f);
                Assert.That(replacementRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(replacementRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));

                Assert.That(
                    host.Presenter.CameraShakeImpulseSink.Submit(new CameraShakeImpulseRequest(
                        tickIndex: 2,
                        semantic: CameraShakeSemantic.PlayerLethalImpact,
                        sourceEntityId: 20,
                        sequenceOrActionPlanId: 101,
                        priority: CameraShakePriority.Heavy)),
                    Is.True);
                host.Presenter.UpdatePresentation(0.1f);
                Assert.That(replacementRig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));
                host.Presenter.DebugHardCleanupPresentationExtensions();
                Assert.That(replacementRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(replacementRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(replacementBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(replacementBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                UnityEngine.Object.Destroy(profile);
                UnityEngine.Object.Destroy(replacementRootObject);
            }

            yield return DestroyHost(host);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_PushLaunch_FullReducedOffAndContinuationUseOneVisibleStart()
        {
            var profile = LoadCampaignCameraShakeProfile();
            var fullHost = CreatePushCameraShakeHost();
            var reducedHost = CreatePushCameraShakeHost();
            var offHost = CreatePushCameraShakeHost();

            var full = RunPushCameraShakeLaunch(fullHost, profile, CameraMotionLevel.Full);
            var reduced = RunPushCameraShakeLaunch(reducedHost, profile, CameraMotionLevel.Reduced);
            var off = RunPushCameraShakeLaunch(offHost, profile, CameraMotionLevel.Off);

            Assert.That(full.StartTick.PresentationData.BoxSlideStartSignals, Is.Empty);
            Assert.That(full.ExecuteTick.PresentationData.BoxSlideStartSignals, Has.Count.EqualTo(1));
            Assert.That(full.ExecuteTick.PresentationData.BoxSlideStartSignals[0].BoxEntityId, Is.EqualTo(30));
            Assert.That(full.ExecuteTick.PresentationData.BoxSlideStartSignals[0].SourceActionPlanId, Is.GreaterThan(0));
            Assert.That(full.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(full.PeakRotationMagnitude, Is.GreaterThan(0.001f));
            Assert.That(reduced.PeakPositionMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakPositionMagnitude, Is.LessThan(full.PeakPositionMagnitude));
            Assert.That(reduced.PeakRotationMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakRotationMagnitude, Is.LessThan(full.PeakRotationMagnitude));
            Assert.That(off.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(off.PeakRotationMagnitude, Is.Zero.Within(0.0001f));

            Assert.That(reduced.ExecuteTick.DeterminismHash, Is.EqualTo(full.ExecuteTick.DeterminismHash));
            Assert.That(off.ExecuteTick.DeterminismHash, Is.EqualTo(full.ExecuteTick.DeterminismHash));
            Assert.That(reduced.ExecuteTick.FinalEntities, Is.EqualTo(full.ExecuteTick.FinalEntities));
            Assert.That(off.ExecuteTick.FinalEntities, Is.EqualTo(full.ExecuteTick.FinalEntities));
            Assert.That(reduced.ExecuteTick.EventLog, Is.EqualTo(full.ExecuteTick.EventLog));
            Assert.That(off.ExecuteTick.EventLog, Is.EqualTo(full.ExecuteTick.EventLog));
            Assert.That(CaptureAuthoritativeSnapshot(reducedHost).Topology, Is.EqualTo(CaptureAuthoritativeSnapshot(fullHost).Topology));
            Assert.That(CaptureAuthoritativeSnapshot(offHost).Topology, Is.EqualTo(CaptureAuthoritativeSnapshot(fullHost).Topology));
            Assert.That(reducedHost.TickRunner.NextTickIndex, Is.EqualTo(fullHost.TickRunner.NextTickIndex));
            Assert.That(offHost.TickRunner.NextTickIndex, Is.EqualTo(fullHost.TickRunner.NextTickIndex));

            AssertCameraShakeReturnsToIdentity(fullHost);
            AssertCameraShakeReturnsToIdentity(reducedHost);
            AssertCameraShakeReturnsToIdentity(offHost);

            var continuedSlideTick = fullHost.InputHost.RunSingleTick();
            Assert.That(continuedSlideTick, Is.Not.Null);
            Assert.That(continuedSlideTick.PresentationData.BoxSlideStartSignals, Is.Empty);
            fullHost.Presenter.UpdatePresentation(0.025f);
            Assert.That(fullHost.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);
            AssertCameraShakePoseIdentity(fullHost.GetComponent<GameplayCameraRig>());

            TestContext.WriteLine(
                $"M2A Push tick={full.ExecuteTick.TickIndex} submit=visible-start " +
                $"peakPos={full.PeakPositionMagnitude:F6} peakRot={full.PeakRotationMagnitude:F6} " +
                $"duration=0.100000 return=0.125000");

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_OrdinaryFlipLanding_CrossesCanonicalContactOnceWithSettingsAndPause()
        {
            var profile = LoadCampaignCameraShakeProfile();
            var fullHost = CreateFlipCameraShakeHost();
            var reducedHost = CreateFlipCameraShakeHost();
            var offHost = CreateFlipCameraShakeHost();

            var full = RunFlipLandingCameraShake(fullHost, profile, CameraMotionLevel.Full, pauseBeforeContact: true);
            var reduced = RunFlipLandingCameraShake(reducedHost, profile, CameraMotionLevel.Reduced, pauseBeforeContact: false);
            var off = RunFlipLandingCameraShake(offHost, profile, CameraMotionLevel.Off, pauseBeforeContact: false);

            Assert.That(full.StartTick.PresentationData.FlipFloorImpactSignals, Is.Empty);
            Assert.That(full.ExecuteTick.PresentationData.FlipFloorImpactSignals, Has.Count.EqualTo(1));
            Assert.That(full.ExecuteTick.PresentationData.FlipFloorImpactSignals[0].Kind, Is.EqualTo(FlipFloorImpactPresentationKind.Landing));
            Assert.That(
                full.ExecuteTick.PresentationData.FlipFloorImpactSignals[0].VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime).Within(0.000001f));
            Assert.That(full.BeforeContactWasIdentity, Is.True);
            Assert.That(full.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(full.PeakRotationMagnitude, Is.GreaterThan(0.001f));
            Assert.That(reduced.PeakPositionMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakPositionMagnitude, Is.LessThan(full.PeakPositionMagnitude));
            Assert.That(reduced.PeakRotationMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakRotationMagnitude, Is.LessThan(full.PeakRotationMagnitude));
            Assert.That(off.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(off.PeakRotationMagnitude, Is.Zero.Within(0.0001f));

            Assert.That(reduced.ExecuteTick.DeterminismHash, Is.EqualTo(full.ExecuteTick.DeterminismHash));
            Assert.That(off.ExecuteTick.DeterminismHash, Is.EqualTo(full.ExecuteTick.DeterminismHash));
            Assert.That(reduced.ExecuteTick.FinalEntities, Is.EqualTo(full.ExecuteTick.FinalEntities));
            Assert.That(off.ExecuteTick.FinalEntities, Is.EqualTo(full.ExecuteTick.FinalEntities));
            Assert.That(reduced.ExecuteTick.EventLog, Is.EqualTo(full.ExecuteTick.EventLog));
            Assert.That(off.ExecuteTick.EventLog, Is.EqualTo(full.ExecuteTick.EventLog));
            Assert.That(CaptureAuthoritativeSnapshot(reducedHost).Topology, Is.EqualTo(CaptureAuthoritativeSnapshot(fullHost).Topology));
            Assert.That(CaptureAuthoritativeSnapshot(offHost).Topology, Is.EqualTo(CaptureAuthoritativeSnapshot(fullHost).Topology));
            Assert.That(reducedHost.TickRunner.NextTickIndex, Is.EqualTo(fullHost.TickRunner.NextTickIndex));
            Assert.That(offHost.TickRunner.NextTickIndex, Is.EqualTo(fullHost.TickRunner.NextTickIndex));
            Assert.That(fullHost.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
            AssertCameraShakeReturnsToIdentity(fullHost);
            AssertCameraShakeReturnsToIdentity(reducedHost);
            AssertCameraShakeReturnsToIdentity(offHost);

            TestContext.WriteLine(
                $"M2A Flip tick={full.ExecuteTick.TickIndex} contactN={GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime:F3} " +
                $"submitN={full.ContactCrossingNormalizedTime:F3} peakPos={full.PeakPositionMagnitude:F6} " +
                $"peakRot={full.PeakRotationMagnitude:F6} duration=0.150000 return=0.175000");

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_RepeatedOrdinaryFlipSameBox_ActualActionsSubmitAtEachLanding()
        {
            var profile = LoadCampaignCameraShakeProfile();
            var host = CreateFlipCameraShakeHost();
            host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);

            var first = ExecuteFlipLandingCameraShakeOnce(
                host,
                CameraMotionLevel.Full,
                Vector2.left,
                expectedAcceptedBefore: 0,
                pauseBeforeContact: false);
            AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, 1, 0));

            var cleanupTick = RunTicksUntil(
                host,
                _ => CaptureAuthoritativeSnapshot(host).TryGetPlayerControlState(10, out var state) &&
                     state.activeAction.kind == PlayerActionKind.None,
                maxTicks: 4);
            Assert.That(cleanupTick, Is.Not.Null, "The first Flip action must release before the second input.");

            var second = ExecuteFlipLandingCameraShakeOnce(
                host,
                CameraMotionLevel.Full,
                Vector2.right,
                expectedAcceptedBefore: 1,
                pauseBeforeContact: false);

            var firstSignal = first.ExecuteTick.PresentationData.FlipFloorImpactSignals.Single();
            var secondSignal = second.ExecuteTick.PresentationData.FlipFloorImpactSignals.Single();
            Assert.That(second.ExecuteTick.TickIndex, Is.GreaterThan(first.ExecuteTick.TickIndex));
            Assert.That(firstSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(secondSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(secondSignal.SourceActionPlanId, Is.EqualTo(firstSignal.SourceActionPlanId));
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(2));
            Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
            AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, -1, 0));
            AssertCameraShakeReturnsToIdentity(host);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_PushAndFlip_DirectAndActualCinemachineShareFiniteLocalContributionAndReset()
        {
            var directPushCameraObject = new GameObject("M2A_DirectPushCamera");
            var cinemachinePushOutputObject = new GameObject("M2A_CinemachinePushOutput");
            var directFlipCameraObject = new GameObject("M2A_DirectFlipCamera");
            var cinemachineFlipOutputObject = new GameObject("M2A_CinemachineFlipOutput");
            var directPushCamera = directPushCameraObject.AddComponent<Camera>();
            var cinemachinePushOutput = cinemachinePushOutputObject.AddComponent<Camera>();
            var pushBrain = cinemachinePushOutputObject.AddComponent<CinemachineBrain>();
            pushBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            var directFlipCamera = directFlipCameraObject.AddComponent<Camera>();
            var cinemachineFlipOutput = cinemachineFlipOutputObject.AddComponent<Camera>();
            var flipBrain = cinemachineFlipOutputObject.AddComponent<CinemachineBrain>();
            flipBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;

            var directPushHost = CreatePushCameraShakeHost(directPushCamera);
            var cinemachinePushHost = CreatePushCameraShakeHost(cinemachinePushOutput);
            var directFlipHost = CreateFlipCameraShakeHost(directFlipCamera);
            var cinemachineFlipHost = CreateFlipCameraShakeHost(cinemachineFlipOutput);
            var pushVirtualCamera = CreateProductionCinemachineCamera(cinemachinePushHost, "M2A_PushCinemachineCamera");
            var flipVirtualCamera = CreateProductionCinemachineCamera(cinemachineFlipHost, "M2A_FlipCinemachineCamera");

            yield return null;
            pushBrain.ManualUpdate();
            flipBrain.ManualUpdate();
            Assert.That(pushBrain.isActiveAndEnabled, Is.True);
            Assert.That(flipBrain.isActiveAndEnabled, Is.True);
            Assert.That(pushVirtualCamera.isActiveAndEnabled, Is.True);
            Assert.That(flipVirtualCamera.isActiveAndEnabled, Is.True);

            var profile = LoadCampaignCameraShakeProfile();
            var directPush = RunPushCameraShakeLaunch(directPushHost, profile, CameraMotionLevel.Full);
            var cinemachinePush = RunPushCameraShakeLaunch(cinemachinePushHost, profile, CameraMotionLevel.Full);
            var directFlip = RunFlipLandingCameraShake(directFlipHost, profile, CameraMotionLevel.Full, pauseBeforeContact: false);
            var cinemachineFlip = RunFlipLandingCameraShake(cinemachineFlipHost, profile, CameraMotionLevel.Full, pauseBeforeContact: false);

            Assert.That(cinemachinePush.PeakPositionMagnitude, Is.EqualTo(directPush.PeakPositionMagnitude).Within(0.000001f));
            Assert.That(cinemachinePush.PeakRotationMagnitude, Is.EqualTo(directPush.PeakRotationMagnitude).Within(0.0001f));
            Assert.That(cinemachineFlip.PeakPositionMagnitude, Is.EqualTo(directFlip.PeakPositionMagnitude).Within(0.000001f));
            Assert.That(cinemachineFlip.PeakRotationMagnitude, Is.EqualTo(directFlip.PeakRotationMagnitude).Within(0.0001f));
            AssertFiniteCameraPose(directPush.PeakOutputWorldPosition, directPush.PeakOutputWorldRotation);
            AssertFiniteCameraPose(cinemachinePush.PeakOutputWorldPosition, cinemachinePush.PeakOutputWorldRotation);
            AssertFiniteCameraPose(directFlip.PeakOutputWorldPosition, directFlip.PeakOutputWorldRotation);
            AssertFiniteCameraPose(cinemachineFlip.PeakOutputWorldPosition, cinemachineFlip.PeakOutputWorldRotation);
            AssertCameraShakeReturnsToIdentity(directPushHost);
            AssertCameraShakeReturnsToIdentity(cinemachinePushHost);
            AssertCameraShakeReturnsToIdentity(directFlipHost);
            AssertCameraShakeReturnsToIdentity(cinemachineFlipHost);

            yield return DestroyHost(directPushHost);
            yield return DestroyHost(cinemachinePushHost);
            yield return DestroyHost(directFlipHost);
            yield return DestroyHost(cinemachineFlipHost);
            UnityEngine.Object.Destroy(directPushCameraObject);
            UnityEngine.Object.Destroy(cinemachinePushOutputObject);
            UnityEngine.Object.Destroy(directFlipCameraObject);
            UnityEngine.Object.Destroy(cinemachineFlipOutputObject);
            yield return null;
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

            SetPlayerForwardTopologyOvershootPose(host, 10);
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
        public IEnumerator TopologyTransitionCameraShake_PlayMode_DirectAndRealCinemachineBrain_ShareAdditivePoseAndReset()
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
                directRig.Initialize(
                    directCameraObject.AddComponent<Camera>(),
                    directBoardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));

                var cinemachineRig = cinemachineRootObject.AddComponent<GameplayCameraRig>();
                cinemachineRig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                cinemachineRig.Initialize(
                    null,
                    cinemachineBoardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));

                var outputCamera = outputCameraObject.AddComponent<Camera>();
                var brain = outputCameraObject.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                var cinemachineCamera = cinemachineCameraObject.AddComponent<CinemachineCamera>();
                cinemachineCameraObject.transform.SetParent(
                    cinemachineBoardRoot.CameraEffectsRoot,
                    worldPositionStays: false);
                cinemachineCameraObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                cinemachineCamera.Target = new CameraTarget
                {
                    TrackingTarget = cinemachineBoardRoot.CameraTargetRoot,
                    LookAtTarget = cinemachineBoardRoot.CameraTargetRoot,
                    CustomLookAtTarget = true,
                };

                yield return null;
                brain.ManualUpdate();
                Assert.That(brain.isActiveAndEnabled, Is.True);
                Assert.That(cinemachineCamera.isActiveAndEnabled, Is.True);

                var orbit = Quaternion.Euler(90f, 0f, 0f);
                directRig.SetPresentedTopologyOrbit(orbit);
                cinemachineRig.SetPresentedTopologyOrbit(orbit);
                var directBasePosition = directBoardRoot.CameraPoseRoot.localPosition;
                var directBaseRotation = directBoardRoot.CameraPoseRoot.localRotation;
                var cinemachineBasePosition = cinemachineBoardRoot.CameraPoseRoot.localPosition;
                var cinemachineBaseRotation = cinemachineBoardRoot.CameraPoseRoot.localRotation;

                var evaluator = new TopologyTransitionCameraShakeController();
                var profile = TopologyTransitionCameraShakeProfile.CreateDefault();
                var impactState = new TopologyTransitionVisualState(
                    isActive: true,
                    progress01: 0.12f,
                    sourceTopology: new CubeTopologyState(FaceId.Floor),
                    destinationTopology: new CubeTopologyState(FaceId.Front),
                    rotationKind: CubeRotationKind.Forward,
                    durationSeconds: 0.2f,
                    presentedVisualRotation: Quaternion.Euler(12f, 0f, 0f),
                    angularVelocityNormalized: 1f);
                var impactPose = evaluator.Evaluate(impactState, profile);
                directRig.ApplyAdditivePose(impactPose.LocalPosition, impactPose.LocalRotation);
                cinemachineRig.ApplyAdditivePose(impactPose.LocalPosition, impactPose.LocalRotation);
                brain.ManualUpdate();

                Assert.That(
                    Vector3.Distance(
                        directRig.AdditiveLocalPosition,
                        cinemachineRig.AdditiveLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        directRig.AdditiveLocalRotation,
                        cinemachineRig.AdditiveLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    directRig.AdditiveLocalPosition.sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(directRig.AdditiveLocalRotation, Quaternion.identity) > 0.001f,
                    Is.True);
                Assert.That(
                    Vector3.Distance(
                        cinemachineBoardRoot.CameraEffectsRoot.localPosition,
                        cinemachineRig.AdditiveLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        cinemachineBoardRoot.CameraEffectsRoot.localRotation,
                        cinemachineRig.AdditiveLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(cinemachineCamera.transform.parent, Is.EqualTo(cinemachineBoardRoot.CameraEffectsRoot));
                Assert.That(directBoardRoot.CameraPoseRoot.localPosition, Is.EqualTo(directBasePosition));
                Assert.That(Quaternion.Angle(directBoardRoot.CameraPoseRoot.localRotation, directBaseRotation), Is.LessThan(0.001f));
                Assert.That(cinemachineBoardRoot.CameraPoseRoot.localPosition, Is.EqualTo(cinemachineBasePosition));
                Assert.That(Quaternion.Angle(cinemachineBoardRoot.CameraPoseRoot.localRotation, cinemachineBaseRotation), Is.LessThan(0.001f));
                Assert.That(float.IsNaN(outputCamera.transform.position.x), Is.False);
                Assert.That(float.IsInfinity(outputCamera.transform.position.x), Is.False);
                Assert.That(float.IsNaN(outputCamera.transform.rotation.x), Is.False);
                Assert.That(float.IsInfinity(outputCamera.transform.rotation.x), Is.False);

                var landingState = new TopologyTransitionVisualState(
                    isActive: true,
                    progress01: 0.84f,
                    sourceTopology: new CubeTopologyState(FaceId.Floor),
                    destinationTopology: new CubeTopologyState(FaceId.Front),
                    rotationKind: CubeRotationKind.Forward,
                    durationSeconds: 0.2f,
                    presentedVisualRotation: Quaternion.Euler(78f, 0f, 0f),
                    angularVelocityNormalized: 0.45f);
                var landingPose = evaluator.Evaluate(landingState, profile);
                directRig.ApplyAdditivePose(landingPose.LocalPosition, landingPose.LocalRotation);
                cinemachineRig.ApplyAdditivePose(landingPose.LocalPosition, landingPose.LocalRotation);
                brain.ManualUpdate();

                Assert.That(
                    Vector3.Distance(
                        directRig.AdditiveLocalPosition,
                        cinemachineRig.AdditiveLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        directRig.AdditiveLocalRotation,
                        cinemachineRig.AdditiveLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    directRig.AdditiveLocalPosition.sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(directRig.AdditiveLocalRotation, Quaternion.identity) > 0.001f,
                    Is.True);
                Assert.That(cinemachineCamera.transform.parent, Is.EqualTo(cinemachineBoardRoot.CameraEffectsRoot));

                directRig.ResetAdditivePose();
                cinemachineRig.ResetAdditivePose();
                brain.ManualUpdate();

                Assert.That(directRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(directRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(cinemachineRig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(cinemachineRig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(cinemachineBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(cinemachineBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(directBoardRoot.CameraPoseRoot.localPosition, Is.EqualTo(directBasePosition));
                Assert.That(Quaternion.Angle(directBoardRoot.CameraPoseRoot.localRotation, directBaseRotation), Is.LessThan(0.001f));
                Assert.That(cinemachineBoardRoot.CameraPoseRoot.localPosition, Is.EqualTo(cinemachineBasePosition));
                Assert.That(Quaternion.Angle(cinemachineBoardRoot.CameraPoseRoot.localRotation, cinemachineBaseRotation), Is.LessThan(0.001f));

                var repeatedDirections = new[]
                {
                    CubeRotationKind.Forward,
                    CubeRotationKind.Backward,
                    CubeRotationKind.Forward,
                    CubeRotationKind.Backward,
                };
                for (var transitionIndex = 0; transitionIndex < repeatedDirections.Length; transitionIndex++)
                {
                    var direction = repeatedDirections[transitionIndex];
                    var repeatedOrbit = Quaternion.Euler(direction == CubeRotationKind.Forward ? 90f : 0f, 0f, 0f);
                    directRig.SetPresentedTopologyOrbit(repeatedOrbit);
                    var unshakenPosition = directCameraObject.transform.position;
                    var unshakenRotation = directCameraObject.transform.rotation;
                    var repeatedState = new TopologyTransitionVisualState(
                        isActive: true,
                        progress01: transitionIndex % 2 == 0 ? 0.11f : 0.82f,
                        sourceTopology: new CubeTopologyState(FaceId.Floor),
                        destinationTopology: new CubeTopologyState(FaceId.Front),
                        rotationKind: direction,
                        durationSeconds: 0.2f,
                        presentedVisualRotation: repeatedOrbit,
                        angularVelocityNormalized: 1f);
                    var repeatedPose = evaluator.Evaluate(repeatedState, profile);
                    directRig.ApplyAdditivePose(repeatedPose.LocalPosition, repeatedPose.LocalRotation);
                    directRig.ResetAdditivePose();

                    Assert.That(directBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(directBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(directBoardRoot.CameraPoseRoot.localPosition, Is.EqualTo(directBasePosition));
                    Assert.That(Quaternion.Angle(directBoardRoot.CameraPoseRoot.localRotation, directBaseRotation), Is.LessThan(0.001f));
                    Assert.That(Vector3.Distance(directCameraObject.transform.position, unshakenPosition), Is.LessThan(0.0001f));
                    Assert.That(Quaternion.Angle(directCameraObject.transform.rotation, unshakenRotation), Is.LessThan(0.001f));
                }

                cinemachineRig.ApplyAdditivePose(impactPose.LocalPosition, impactPose.LocalRotation);
                cinemachineRig.enabled = false;
                Assert.That(cinemachineBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(cinemachineBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));

                cinemachineRig.enabled = true;
                cinemachineRig.ApplyAdditivePose(impactPose.LocalPosition, impactPose.LocalRotation);
                UnityEngine.Object.Destroy(cinemachineRig);
                yield return null;
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
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            host.InputHost.SetRawMoveInput(Vector2.right);
            var firstTick = host.InputHost.RunSingleTick();

            Assert.That(firstTick, Is.Not.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            var secondTick = host.InputHost.RunSingleTick();
            Assert.That(secondTick, Is.Not.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            RunTicksAssertingNoBlockingPresentation(host, 8);
            AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 1, 0));

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
            },
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();

            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            var executeTick = host.InputHost.RunSingleTick();
            Assert.That(executeTick, Is.Not.Null);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            var completionTick = host.InputHost.RunSingleTick();
            Assert.That(completionTick, Is.Not.Null);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(4));

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
            host.InputHost.BufferPush();
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
            },
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile());
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
            },
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile());
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
            },
                playerControlTiming: CreateFlipTimingSettings(
                    flipExecuteDelayTicks: 1,
                    flipInputLockDurationTicks: 1));

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();
            var startTick = host.InputHost.RunSingleTick();

            Assert.That(startTick, Is.Not.Null);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(2));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            host.InputHost.SetRawMoveInput(Vector2.zero);
            var executeTick = host.InputHost.RunSingleTick();

            Assert.That(executeTick, Is.Not.Null);
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(3));

            var completionTick = host.InputHost.RunSingleTick();
            Assert.That(completionTick, Is.Not.Null);
            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(4));
            AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, 1, 0));

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
            var actions = CloneProductionInputActions();
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
                    CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 91, position: new SurfaceCell(FaceId.Floor, -6, 0)),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 6, 0)),
                },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            SetKeyboardState(_keyboard, Key.D, Key.J);

            var startTick = host.InputHost.RunSingleTick();
            var startSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(startTick, Is.Not.Null);
            Assert.That(startTick.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(startTick.PresentationData.PlayerActionSignals[0].StartedThisTick, Is.True);
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

            Release(_keyboard.jKey);
            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushStartedEdgeOnly_KeyboardPushConsumesOnce()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                CreateWall(entityId: 91, position: new SurfaceCell(FaceId.Floor, -4, 0)),
            },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            SetKeyboardState(_keyboard, Key.A);
            SetKeyboardState(_keyboard, Key.A, Key.J);

            var startTick = host.InputHost.RunSingleTick();
            var startSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(startTick, Is.Not.Null);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startSnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.sequence, Is.EqualTo(1));
            Assert.That(controlState.activeAction.direction, Is.EqualTo(Direction.Left));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(30));

            var executeTick = host.InputHost.RunSingleTick();
            var executeSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(executeTick, Is.Not.Null);
            Assert.That(executeSnapshot.TryGetEntity(30, out var rightBox), Is.True);
            Assert.That(executeSnapshot.TryGetEntity(31, out var leftBox), Is.True);
            Assert.That(rightBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -2, 0)));
            Assert.That(leftBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));

            Release(_keyboard.jKey);
            Release(_keyboard.aKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushKeyAlone_StartsAgainstFacingBox()
        {
            var actions = CloneProductionInputActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            }, actions: actions);

            SetKeyboardState(_keyboard, Key.J);
            var result = host.InputHost.RunSingleTick();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(result.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            Assert.That(result.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Is.Empty);

            SetKeyboardState(_keyboard);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipKeyAlone_StartsAgainstFacingBox()
        {
            var actions = CloneProductionInputActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            }, actions: actions);

            SetKeyboardState(_keyboard, Key.K);
            var result = host.InputHost.RunSingleTick();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(result.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            Assert.That(result.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Is.Empty);

            SetKeyboardState(_keyboard);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_DirectionlessActionAtProductionBoxClamp_QueuesAssist(
            [Values(Key.J, Key.K)] Key actionKey,
            [Values(Key.D, Key.RightArrow)] Key approachKey,
            [Values(Direction.Right, Direction.Left, Direction.Up)] Direction initialFacing,
            [Values(false, true)] bool runIdleTickAfterRelease)
        {
            var actions = CloneProductionInputActions();
            var capability = actionKey == Key.J ? BoxCapabilities.Push : BoxCapabilities.Flip;
            var bindingStore = new PlayModeKeyboardBindingStore();
            bindingStore.SaveMovementScheme(
                approachKey == Key.RightArrow
                    ? KeyboardMovementScheme.ArrowKeys
                    : KeyboardMovementScheme.Wasd);
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: initialFacing),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: capability),
            },
                actions: actions,
                keyboardBindingStore: bindingStore,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = 0.28125f,
                    ActionAssistSettleWindowCells = 0.421875f,
                });

            SetKeyboardState(_keyboard, approachKey);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            }

            SetKeyboardState(_keyboard);
            if (runIdleTickAfterRelease)
            {
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            }

            var clampedSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(clampedSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
            Assert.That(clampedSnapshot.TryGetUnitContinuousLocomotionPose(10, out var pose), Is.True);
            Assert.That(pose.LocalOffset.X.RawValue, Is.EqualTo(896));
            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(clampedSnapshot, 10), Is.False);

            SetKeyboardState(_keyboard, actionKey);
            var result = host.InputHost.RunSingleTick();
            var queuedSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(result, Is.Not.Null);
            Assert.That(queuedSnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.True);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Is.Empty);

            SetKeyboardState(_keyboard);
            var started = false;
            for (var i = 0; i < 30 && !started; i++)
            {
                var nextResult = host.InputHost.RunSingleTick();
                Assert.That(nextResult, Is.Not.Null);
                Assert.That(nextResult.PresentationData.PlayerActionAttemptSignals, Is.Empty);
                started = nextResult.PresentationData.PlayerActionSignals.Any(signal =>
                    signal.StartedThisTick &&
                    signal.TargetEntityId == 30 &&
                    signal.Direction == Direction.Right);
            }

            Assert.That(started, Is.True);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipKeyThenDown_PreservesFacingTargetAcrossInputUpdates()
        {
            var inputGapsSeconds = new[] { 0.005, 0.02, 0.05 };
            foreach (var separateInputUpdates in new[] { false, true })
            foreach (var inputGapSeconds in inputGapsSeconds)
            {
                var actions = CreateKeyboardMoveActions();
                var host = CreateHost(new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
                },
                    actions: actions,
                    flipPresentationDurationSeconds: 0.5f,
                    playerControlTiming: CreateFlipTimingSettings(
                        flipExecuteDelayTicks: 1,
                        flipInputLockDurationTicks: 1),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

                QueueActionThenDownInputEvents(_keyboard, Key.K, inputGapSeconds, separateInputUpdates);

                var startTick = host.InputHost.RunSingleTick();
                Assert.That(startTick, Is.Not.Null, $"Gap={inputGapSeconds}s; SeparateUpdates={separateInputUpdates}");
                Assert.That(startTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
                Assert.That(startTick.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
                var startSignal = startTick.PresentationData.PlayerActionSignals[0];
                Assert.That(startSignal.StartedThisTick, Is.True, $"Gap={inputGapSeconds}s; SeparateUpdates={separateInputUpdates}");
                Assert.That(startSignal.Direction, Is.EqualTo(Direction.Right), $"Gap={inputGapSeconds}s; SeparateUpdates={separateInputUpdates}");
                Assert.That(startSignal.TargetEntityId, Is.EqualTo(30), $"Gap={inputGapSeconds}s; SeparateUpdates={separateInputUpdates}");
                host.Presenter.UpdatePresentation(0f);
                AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);

                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                host.Presenter.UpdatePresentation(0f);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                host.Presenter.UpdatePresentation(0f);
                Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);
                AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));
                AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, -1, 0));

                SetKeyboardState(_keyboard);
                yield return DestroyHost(host, actions);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PlayerS1FlipStartedThenDown_KeepsRealTargetAndPlayback()
        {
            var inputGapsSeconds = new[] { 0.005f, 0.02f, 0.05f };
            foreach (var inputGapSeconds in inputGapsSeconds)
            {
                var actions = CreateKeyboardMoveActions();
                var host = CreateHost(new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
                },
                    actions: actions,
                    playerViewPrefabOverride: LoadPlayerS1ViewPrefab(),
                    playerControlTiming: CreateFlipTimingSettings(
                        flipExecuteDelayTicks: 1,
                        flipInputLockDurationTicks: 1),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

                SetKeyboardState(_keyboard, Key.K);
                var startTick = host.InputHost.RunSingleTick();
                Assert.That(startTick, Is.Not.Null, $"Gap={inputGapSeconds}s");
                Assert.That(startTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
                Assert.That(startTick.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
                Assert.That(startTick.PresentationData.PlayerActionSignals[0].StartedThisTick, Is.True);
                Assert.That(startTick.PresentationData.PlayerActionSignals[0].Direction, Is.EqualTo(Direction.Right));
                Assert.That(startTick.PresentationData.PlayerActionSignals[0].TargetEntityId, Is.EqualTo(30));
                host.Presenter.UpdatePresentation(0f);

                Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                var animator = playerView.GetComponentInChildren<Animator>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(playerView.ModelRoot, Is.Not.Null);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);

                AdvanceInputAndPresentationTime(host, inputGapSeconds);

                Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);
                SetKeyboardState(_keyboard, Key.K, Key.S);
                var downTick = host.InputHost.RunSingleTick();
                Assert.That(downTick, Is.Not.Null, $"Gap={inputGapSeconds}s");
                Assert.That(downTick.Trace.Text, Does.Not.Contain("Destination=(0,-1)|Command=Move"));
                Assert.That(downTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
                host.Presenter.UpdatePresentation(0f);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(driver.LastCrossFadedStateName, Does.StartWith("Flip_"));

                for (var i = 0; i < 4; i++)
                {
                    var heldTick = host.InputHost.RunSingleTick();
                    Assert.That(heldTick, Is.Not.Null);
                    Assert.That(heldTick.Trace.Text, Does.Not.Contain("Destination=(0,-1)|Command=Move"));
                    Assert.That(heldTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
                    host.Presenter.UpdatePresentation(0f);
                }

                AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));
                AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, -1, 0));
                Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);
                AdvancePresentation(host, 2f);
                Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.False);
                AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Left);

                SetKeyboardState(_keyboard);
                yield return DestroyHost(host, actions);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PlayerS1PushStartedThenDown_KeepsModelFacingAndMovesBox()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            },
                actions: actions,
                playerViewPrefabOverride: LoadPlayerS1ViewPrefab(),
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            SetKeyboardState(_keyboard, Key.J);
            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            Assert.That(startTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            Assert.That(startTick.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(startTick.PresentationData.PlayerActionSignals[0].StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals[0].Direction, Is.EqualTo(Direction.Right));
            Assert.That(startTick.PresentationData.PlayerActionSignals[0].TargetEntityId, Is.EqualTo(30));
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(playerView.ModelRoot, Is.Not.Null);
            AdvanceInputAndPresentationTime(host, 0.05f);
            Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);
            var modelRotationBeforeDown = playerView.ModelRoot.rotation;

            SetKeyboardState(_keyboard, Key.J, Key.S);
            var downTick = host.InputHost.RunSingleTick();
            Assert.That(downTick, Is.Not.Null);
            Assert.That(downTick.Trace.Text, Does.Not.Contain("Destination=(0,-1)|Command=Move"));
            Assert.That(downTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            host.Presenter.UpdatePresentation(0f);
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);
            Assert.That(Quaternion.Angle(playerView.ModelRoot.rotation, modelRotationBeforeDown), Is.LessThan(0.1f));
            AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, 2, 0));

            SetKeyboardState(_keyboard);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PlayerS1FakeFlipStartedThenDown_KeepsFacingAndBlocksMove()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
            },
                actions: actions,
                playerViewPrefabOverride: LoadPlayerS1ViewPrefab(),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            SetKeyboardState(_keyboard, Key.K);
            var attemptTick = host.InputHost.RunSingleTick();
            Assert.That(attemptTick, Is.Not.Null);
            Assert.That(attemptTick.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(attemptTick.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(attemptTick.PresentationData.PlayerActionAttemptSignals[0].Direction, Is.EqualTo(Direction.Right));
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(playerView.ModelRoot, Is.Not.Null);
            AdvanceInputAndPresentationTime(host, 0.05f);
            Assert.That(host.Presenter.IsPlayerActionAttemptPlaybackActive(10), Is.True);
            var modelRotationBeforeDown = playerView.ModelRoot.rotation;

            SetKeyboardState(_keyboard, Key.K, Key.S);
            var downTick = host.InputHost.RunSingleTick();
            Assert.That(downTick, Is.Not.Null);
            Assert.That(downTick.Trace.Text, Does.Not.Contain("Destination=(0,-1)|Command=Move"));
            Assert.That(downTick.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(downTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            host.Presenter.UpdatePresentation(0f);
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);
            Assert.That(Quaternion.Angle(playerView.ModelRoot.rotation, modelRotationBeforeDown), Is.LessThan(0.1f));
            Assert.That(host.Presenter.IsPlayerActionAttemptPlaybackActive(10), Is.True);
            AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));

            SetKeyboardState(_keyboard);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_PushKeyThenDownInSameInputUpdate_PreservesFacingTarget()
        {
            var inputGapsSeconds = new[] { 0.005, 0.02, 0.05 };
            foreach (var inputGapSeconds in inputGapsSeconds)
            {
                var actions = CreateKeyboardMoveActions();
                var host = CreateHost(new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                    actions: actions,
                    pushPresentationDurationSeconds: 0.5f,
                    playerControlTiming: CreatePushTimingSettings(
                        pushExecuteDelayTicks: 1,
                        pushInputLockDurationTicks: 1),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

                QueueActionThenDownInputEvents(_keyboard, Key.J, inputGapSeconds, separateInputUpdates: false);

                var startTick = host.InputHost.RunSingleTick();
                Assert.That(startTick, Is.Not.Null, $"Gap={inputGapSeconds}s");
                Assert.That(startTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
                Assert.That(startTick.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
                var startSignal = startTick.PresentationData.PlayerActionSignals[0];
                Assert.That(startSignal.StartedThisTick, Is.True, $"Gap={inputGapSeconds}s");
                Assert.That(startSignal.Direction, Is.EqualTo(Direction.Right), $"Gap={inputGapSeconds}s");
                Assert.That(startSignal.TargetEntityId, Is.EqualTo(30), $"Gap={inputGapSeconds}s");
                host.Presenter.UpdatePresentation(0f);
                AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);

                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                host.Presenter.UpdatePresentation(0f);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                host.Presenter.UpdatePresentation(0f);
                Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);
                AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));
                AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, 2, 0));

                SetKeyboardState(_keyboard);
                yield return DestroyHost(host, actions);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FailedFlipThenDownInSameInputUpdate_KeepsFakeFacing()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
            },
                actions: actions,
                flipPresentationDurationSeconds: 0.5f,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            QueueActionThenDownInputEvents(_keyboard, Key.K, 0.005, separateInputUpdates: false);

            var attemptTick = host.InputHost.RunSingleTick();
            Assert.That(attemptTick, Is.Not.Null);
            Assert.That(attemptTick.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(attemptTick.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(attemptTick.PresentationData.PlayerActionAttemptSignals[0].Direction, Is.EqualTo(Direction.Right));
            Assert.That(host.Presenter.IsPlayerActionAttemptPlaybackActive(10), Is.True);
            host.Presenter.UpdatePresentation(0f);
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);

            var heldTick = host.InputHost.RunSingleTick();
            Assert.That(heldTick, Is.Not.Null);
            Assert.That(heldTick.Trace.Text, Does.Not.Contain("Destination=(0,-1)|Command=Move"));
            host.Presenter.UpdatePresentation(0f);
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Right);
            AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));

            SetKeyboardState(_keyboard);
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
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item | BoxCapabilities.Push),
                },
                moveMotionDurationSeconds: 0.05f,
                repeatedMoveIntervalSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                itemConsumeEffectDurationSeconds: 0.3f);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
            TickResult pickupTick = null;
            var pickupObserved = false;
            for (var i = 0; i < GameplayTimingProfile.DefaultSimulationTicksPerSecond; i++)
            {
                pickupTick = host.InputHost.RunSingleTick();
                Assert.That(pickupTick, Is.Not.Null);
                host.Presenter.UpdatePresentation(host.TimingProfile.MoveMotionDurationSeconds);

                if (!CaptureAuthoritativeSnapshot(host).TryGetEntity(30, out _))
                {
                    pickupObserved = true;
                    break;
                }

                host.InputHost.SetRawMoveInput(Vector2.right);
            }

            Assert.That(pickupTick, Is.Not.Null);
            Assert.That(pickupObserved, Is.True);
            Assert.That(host.ViewRegistry.TryGetView(30, out var itemView), Is.True);
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(30, out _), Is.False);
            Assert.That(itemView.gameObject.activeSelf, Is.False);

            var consumeEffectTicks = Mathf.CeilToInt(
                host.TimingProfile.ItemConsumeEffectDurationSeconds /
                host.TimingProfile.SimulationTickIntervalSeconds);
            for (var i = 0; i < consumeEffectTicks - 1; i++)
            {
                host.InputHost.SetRawMoveInput(Vector2.right);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);

                Assert.That(itemView.gameObject.activeSelf, Is.False);
            }

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
                boxDestroyEffectDurationSeconds: 0.3f,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
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
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile(),
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

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayMode_SameFaceSlide_EnemyOnSuppressedBarricade_WhenEnemyDies_RemovesBoxView()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var barricadeCell = new SurfaceCell(FaceId.Front, 0, 1);
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
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 3, 3)),
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
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile(),
                initialTopology: new CubeTopologyState(FaceId.Floor));

            Assert.That(host.ViewRegistry.TryGetView(20, out var boxView), Is.True);
            Assert.That(host.ViewRegistry.TryGetView(30, out var enemyView), Is.True);

            var result = host.InputHost.RunSingleTick();
            var finalSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.True);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 20 && signal.ExitCause == TickEntityExitCause.BoxDestroy), Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
            Assert.That(boxView.gameObject.activeSelf, Is.False);
            Assert.That(enemyView.gameObject.activeSelf, Is.False);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayMode_Flip_EnemyOnSuppressedBarricade_WhenEnemyDies_RemovesBoxViewAndShowsCrush()
        {
            var host = CreateFlipSuppressedBarricadeHost(enemyHp: 1);
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            Assert.That(host.ViewRegistry.TryGetView(20, out var boxView), Is.True);
            Assert.That(host.ViewRegistry.TryGetView(30, out var enemyView), Is.True);

            var result = host.InputHost.RunSingleTick();
            var finalSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.True);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 20 && signal.ExitCause == TickEntityExitCause.BoxDestroy), Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Front, 0, 1), out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(landingCell, out _), Is.False);
            Assert.That(boxView.gameObject.activeSelf, Is.False);
            Assert.That(enemyView.gameObject.activeSelf, Is.False);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayMode_Flip_EnemyOnSuppressedBarricade_WhenEnemySurvives_DestroysSelfViewAccordingToFlipContract()
        {
            var host = CreateFlipSuppressedBarricadeHost(enemyHp: 2);
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            Assert.That(host.ViewRegistry.TryGetView(20, out var boxView), Is.True);
            Assert.That(host.ViewRegistry.TryGetView(30, out var enemyView), Is.True);

            var result = host.InputHost.RunSingleTick();
            var finalSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.FlipImpactSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.FlipImpactSignals[0].Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.position, Is.EqualTo(landingCell));
            Assert.That(enemyAfter.hp, Is.EqualTo(1));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Front, 0, 1), out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(landingCell, out _), Is.False);
            Assert.That(boxView.gameObject.activeSelf, Is.False);
            Assert.That(enemyView.gameObject.activeSelf, Is.True);

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
            AdvancePresentation(host, host.TimingProfile.FlipMotionDurationSeconds + host.TimingProfile.SimulationTickIntervalSeconds);

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
            host.Presenter.UpdatePresentation(0f);
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Left);

            host.InputHost.SetRawMoveInput(Vector2.zero);
            var executeTick = RunTicksUntil(
                host,
                result => result.Trace.Text.Contains("FlipResultFacingCommitted", StringComparison.Ordinal),
                maxTicks: 64);
            var executeSnapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(executeTick, Is.Not.Null, "Player_S1 Flip never reached the result-facing commit tick.");
            Assert.That(executeSnapshot.TryGetEntity(10, out var executePlayer), Is.True);
            Assert.That(executePlayer.facing, Is.EqualTo(Direction.Right));
            AdvancePresentation(
                host,
                ResolvePlayerPresentationPhaseDurationSecondsForTest(host, 10, PlayerPresentationPhase.FlipWindup) +
                ResolvePlayerPresentationPhaseDurationSecondsForTest(host, 10, PlayerPresentationPhase.FlipRecovery) * 0.5f);
            AssertViewRotationBetweenFacings(
                host,
                entityId: 10,
                sourceFacing: Direction.Left,
                targetFacing: Direction.Right);

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

            SetKeyboardState(_keyboard, Key.D, Key.J);

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

            Release(_keyboard.jKey);
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
            AdvancePresentation(host, host.TimingProfile.FlipMotionDurationSeconds + host.TimingProfile.SimulationTickIntervalSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipVisualHold_BlocksMoveCommandUntilPlaybackCompletes()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                actions: actions,
                repeatedMoveIntervalSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                moveMotionDurationSeconds: 1f,
                flipPresentationDurationSeconds: 0.5f,
                playerControlTiming: CreateFlipTimingSettings(
                    flipExecuteDelayTicks: 1,
                    flipInputLockDurationTicks: 1),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            SetKeyboardState(_keyboard, Key.A);
            host.InputHost.BufferFlip();

            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);
            Assert.That(
                startTick.PresentationData.PlayerActionSignals.Any(signal =>
                    signal.EntityId == 10 &&
                    signal.ActiveActionKind == PlayerActionKind.Flip &&
                    signal.StartedThisTick),
                Is.True);
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            Assert.That(host.Presenter.HasBlockingPresentation, Is.False);

            var followupMoveTick = host.InputHost.RunSingleTick();
            Assert.That(followupMoveTick, Is.Not.Null);
            Assert.That(followupMoveTick.Trace.Text, Does.Not.Contain("Destination=(-1,0)|Command=Move"));
            host.Presenter.UpdatePresentation(0f);
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
            Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);
            AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));
            for (var i = 0; i < 9; i++)
            {
                Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
                var heldTick = host.InputHost.RunSingleTick();
                Assert.That(heldTick, Is.Not.Null);
                Assert.That(heldTick.Trace.Text, Does.Not.Contain("Destination=(-1,0)|Command=Move"));
                Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            }
            AssertAuthoritativePosition(host, entityId: 10, new SurfaceCell(FaceId.Floor, 0, 0));

            host.Presenter.UpdatePresentation(0.5f);
            Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.False);
            var releasedTick = host.InputHost.RunSingleTick();
            Assert.That(releasedTick, Is.Not.Null);
            Assert.That(releasedTick.Trace.Text, Does.Contain("Destination=(-1,0)|Command=Move"));
            host.Presenter.UpdatePresentation(0f);
            Assert.That(driver.CurrentState, Is.Not.EqualTo(PlayerViewAnimationState.Flip));

            var movedAfterPlayback = false;
            for (var i = 0; i < 60; i++)
            {
                var tick = host.InputHost.RunSingleTick();
                Assert.That(tick, Is.Not.Null);
                host.Presenter.UpdatePresentation(host.TimingProfile.SimulationTickIntervalSeconds);
                var snapshot = CaptureAuthoritativeSnapshot(host);
                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                if (player.position == new SurfaceCell(FaceId.Floor, -1, 0))
                {
                    movedAfterPlayback = true;
                    break;
                }
            }
            Assert.That(movedAfterPlayback, Is.True, "Held direction never moved the player after Flip playback ended.");

            SetKeyboardState(_keyboard);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_NoSampledDirection_PreservesBufferedFlipAsFakeAttempt()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(new Vector2(1f, 1f));
            host.InputHost.BufferFlip();
            var result = host.InputHost.RunSingleTick();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].ActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].Direction, Is.EqualTo(Direction.Right));
            Assert.That(host.Presenter.IsPlayerActionAttemptPlaybackActive(10), Is.True);
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
            Assert.That(driver.LastPresentationState.HasActionAttempt, Is.True);
            Assert.That(driver.LastPresentationState.CanceledThisTick, Is.False);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
            var gatedTick = host.InputHost.RunSingleTick();
            Assert.That(gatedTick, Is.Not.Null);
            Assert.That(gatedTick.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(gatedTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            Assert.That(gatedTick.PresentationData.EntityMotions, Is.Empty);

            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);
            AssertViewMatchesProjectedState(host, entityId: 31);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_OffCenterFailedFlip_TurnsViewAndContinuousFacing()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            InvokeWorldWriteContextMethod(
                host.WorldState,
                "SetUnitContinuousLocomotionState",
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(SimulationFixed.FromRaw(512), SimulationFixed.Zero),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());

            host.InputHost.SetRawMoveInput(Vector2.up);
            host.InputHost.BufferFlip();
            var result = host.InputHost.RunSingleTick();
            var snapshot = CaptureAuthoritativeSnapshot(host);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].Direction, Is.EqualTo(Direction.Up));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.facing, Is.EqualTo(Direction.Up));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionPose(10, out var pose), Is.True);
            Assert.That(pose.State.facing, Is.EqualTo(Direction.Up));
            Assert.That(pose.LocalOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(host.Presenter.IsPlayerActionAttemptPlaybackActive(10), Is.True);

            host.Presenter.UpdatePresentation(host.TimingProfile.SimulationTickIntervalSeconds);
            AssertViewFacing(host, entityId: 10, expectedFacing: Direction.Up);

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
                    CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 6, 0)),
                },
                actions: actions,
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1));

            SetKeyboardState(_keyboard, Key.D, Key.J);

            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            SetKeyboardState(_keyboard);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);

            var releasedTick = host.InputHost.RunSingleTick();
            var releasedSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(releasedTick, Is.Not.Null);
            Assert.That(releasedSnapshot.TryGetPlayerControlState(10, out var releasedState), Is.True);
            Assert.That(releasedState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(releasedState.actionSequenceCounter, Is.EqualTo(1));

            SetKeyboardState(_keyboard, Key.A);
            SetKeyboardState(_keyboard, Key.A, Key.J);

            var stillPlayingTick = host.InputHost.RunSingleTick();
            Assert.That(stillPlayingTick, Is.Not.Null);
            Assert.That(stillPlayingTick.PresentationData.PlayerActionSignals.Any(signal => signal.StartedThisTick), Is.False);
            Assert.That(stillPlayingTick.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.True);

            SetKeyboardState(_keyboard, Key.A);
            AdvancePresentation(host, host.TimingProfile.PushMotionDurationSeconds + 1f);
            Assert.That(host.Presenter.IsPlayerInteractionPlaybackActive(10), Is.False);
            SetKeyboardState(_keyboard, Key.A, Key.J);

            TickResult restartTick = null;
            TickPlayerActionPresentationSignal restartSignal = default;
            var foundRestartSignal = false;
            for (var i = 0; i < 3; i++)
            {
                restartTick = host.InputHost.RunSingleTick();
                Assert.That(restartTick, Is.Not.Null);
                if (TryFindStartedPlayerActionSignal(
                        restartTick,
                        entityId: 10,
                        PlayerActionKind.Push,
                        out restartSignal))
                {
                    foundRestartSignal = true;
                    break;
                }
            }

            var restartSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(foundRestartSignal, Is.True, "Fresh push press did not start the next push.");
            Assert.That(restartSignal.StartedThisTick, Is.True);
            Assert.That(restartSnapshot.TryGetPlayerControlState(10, out var restartState), Is.True);
            Assert.That(restartState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(restartState.activeAction.sequence, Is.EqualTo(2));
            Assert.That(restartState.activeAction.direction, Is.EqualTo(Direction.Left));
            Assert.That(restartState.activeAction.targetEntityId, Is.EqualTo(31));

            var secondExecuteTick = host.InputHost.RunSingleTick();
            var secondExecuteSnapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(secondExecuteTick, Is.Not.Null);
            Assert.That(secondExecuteSnapshot.TryGetEntity(31, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -2, 0)));

            Release(_keyboard.jKey);
            Release(_keyboard.aKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayInputHost_FlipAfterDirectionChange_PreservesProjectedViewState()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                actions: null,
                staticEntityLogics: null,
                initialMoveDelaySeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            AdvancePresentation(host, host.TimingProfile.FlipMotionDurationSeconds + host.TimingProfile.SimulationTickIntervalSeconds);

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
            var actions = CloneProductionInputActions();
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

        private readonly struct ProductionCameraShakeSample
        {
            public ProductionCameraShakeSample(
                TickResult startTick,
                TickResult executeTick,
                float peakPositionMagnitude,
                float peakRotationMagnitude,
                bool beforeContactWasIdentity = true,
                float contactCrossingNormalizedTime = 0f,
                Vector3 peakOutputWorldPosition = default,
                Quaternion peakOutputWorldRotation = default)
            {
                StartTick = startTick;
                ExecuteTick = executeTick;
                PeakPositionMagnitude = peakPositionMagnitude;
                PeakRotationMagnitude = peakRotationMagnitude;
                BeforeContactWasIdentity = beforeContactWasIdentity;
                ContactCrossingNormalizedTime = contactCrossingNormalizedTime;
                PeakOutputWorldPosition = peakOutputWorldPosition;
                PeakOutputWorldRotation = peakOutputWorldRotation;
            }

            public TickResult StartTick { get; }

            public TickResult ExecuteTick { get; }

            public float PeakPositionMagnitude { get; }

            public float PeakRotationMagnitude { get; }

            public bool BeforeContactWasIdentity { get; }

            public float ContactCrossingNormalizedTime { get; }

            public Vector3 PeakOutputWorldPosition { get; }

            public Quaternion PeakOutputWorldRotation { get; }
        }

        private static GameplayCameraShakeProfile LoadCampaignCameraShakeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameplayCameraShakeProfile>(CampaignCameraShakeProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing production camera shake profile at {CampaignCameraShakeProfilePath}.");
            profile.ValidateOrThrow();
            return profile;
        }

        private static GameplaySceneHost CreatePushCameraShakeHost(Camera viewCamera = null)
        {
            return CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                playerControlTiming: CreatePushTimingSettings(
                    pushExecuteDelayTicks: 1,
                    pushInputLockDurationTicks: 1),
                viewCamera: viewCamera);
        }

        private static GameplaySceneHost CreateFlipCameraShakeHost(Camera viewCamera = null)
        {
            return CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                playerControlTiming: CreateFlipTimingSettings(
                    flipExecuteDelayTicks: 1,
                    flipInputLockDurationTicks: 1),
                viewCamera: viewCamera);
        }

        private static ProductionCameraShakeSample RunPushCameraShakeLaunch(
            GameplaySceneHost host,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel)
        {
            host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
            host.Presenter.SetCameraMotionLevel(motionLevel);
            var rig = host.GetComponent<GameplayCameraRig>();
            AssertCameraShakePoseIdentity(rig);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);
            AssertCameraShakePoseIdentity(rig);

            var executeTick = host.InputHost.RunSingleTick();
            Assert.That(executeTick, Is.Not.Null);
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.EqualTo(1));
            Assert.That(host.Presenter.CurrentCameraShakeMixResult.IsActive, Is.EqualTo(motionLevel != CameraMotionLevel.Off));

            host.Presenter.UpdatePresentation(0.02f);
            var peakPositionMagnitude = rig.AdditiveLocalPosition.magnitude;
            var peakRotationMagnitude = MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation);
            var peakOutputWorldPosition = host.ViewCamera != null ? host.ViewCamera.transform.position : Vector3.zero;
            var peakOutputWorldRotation = host.ViewCamera != null ? host.ViewCamera.transform.rotation : Quaternion.identity;
            host.Presenter.UpdatePresentation(0.105f);

            return new ProductionCameraShakeSample(
                startTick,
                executeTick,
                peakPositionMagnitude,
                peakRotationMagnitude,
                peakOutputWorldPosition: peakOutputWorldPosition,
                peakOutputWorldRotation: peakOutputWorldRotation);
        }

        private static ProductionCameraShakeSample RunFlipLandingCameraShake(
            GameplaySceneHost host,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel,
            bool pauseBeforeContact)
        {
            host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
            host.Presenter.SetCameraMotionLevel(motionLevel);
            return ExecuteFlipLandingCameraShakeOnce(
                host,
                motionLevel,
                Vector2.left,
                expectedAcceptedBefore: 0,
                pauseBeforeContact: pauseBeforeContact);
        }

        private static ProductionCameraShakeSample ExecuteFlipLandingCameraShakeOnce(
            GameplaySceneHost host,
            CameraMotionLevel motionLevel,
            Vector2 flipInput,
            int expectedAcceptedBefore,
            bool pauseBeforeContact)
        {
            var rig = host.GetComponent<GameplayCameraRig>();
            AssertCameraShakePoseIdentity(rig);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(expectedAcceptedBefore));

            host.InputHost.SetRawMoveInput(flipInput);
            host.InputHost.BufferFlip();
            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);

            host.InputHost.SetRawMoveInput(Vector2.zero);
            var executeTick = host.InputHost.RunSingleTick();
            Assert.That(executeTick, Is.Not.Null);
            var floorSignal = executeTick.PresentationData.FlipFloorImpactSignals.Single();
            Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);
            AssertCameraShakePoseIdentity(rig);

            var beforeContactWasIdentity = true;
            var pauseVerified = !pauseBeforeContact;
            var contactCrossingNormalizedTime = 0f;
            for (var updateIndex = 0; updateIndex < 100; updateIndex++)
            {
                host.Presenter.UpdatePresentation(0.005f);
                var progress = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(sample =>
                    sample.EntityId == 30 &&
                    sample.MotionKind == TickEntityMotionKind.Flip &&
                    sample.SourceKind == MotionTrackProgressSourceKind.LocalMotion &&
                    sample.TickIndex == executeTick.TickIndex &&
                    sample.SequenceOrActionPlanId == floorSignal.SourceActionPlanId);
                Assert.That(progress.IsValid, Is.True, "The ordinary Flip must expose its actual presentation clip progress.");

                if (!pauseVerified &&
                    progress.CurrentNormalizedTime >= 0.7f &&
                    progress.CurrentNormalizedTime < GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime)
                {
                    host.Presenter.SetPresentationPaused(true);
                    host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);
                    Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.EqualTo(1));
                    Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);
                    AssertCameraShakePoseIdentity(rig);
                    host.Presenter.SetPresentationPaused(false);
                    pauseVerified = true;
                }

                if (progress.CurrentNormalizedTime < GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime)
                {
                    beforeContactWasIdentity &=
                        rig.AdditiveLocalPosition.sqrMagnitude <= 0.000000000001f &&
                        rig.AdditiveLocalRotation == Quaternion.identity &&
                        host.Presenter.ActiveGameplayCameraImpulseCount == 0;
                    Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.EqualTo(1));
                    continue;
                }

                Assert.That(
                    progress.PreviousNormalizedTime,
                    Is.LessThan(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
                contactCrossingNormalizedTime = progress.CurrentNormalizedTime;
                break;
            }

            Assert.That(pauseVerified, Is.True);
            Assert.That(contactCrossingNormalizedTime, Is.GreaterThanOrEqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
            Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.EqualTo(1));
            Assert.That(
                host.Presenter.AcceptedGameplayCameraImpulseCount,
                Is.EqualTo(expectedAcceptedBefore + 1));
            Assert.That(host.Presenter.CurrentCameraShakeMixResult.IsActive, Is.EqualTo(motionLevel != CameraMotionLevel.Off));

            host.Presenter.UpdatePresentation(0.02f);
            var peakPositionMagnitude = rig.AdditiveLocalPosition.magnitude;
            var peakRotationMagnitude = MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation);
            var peakOutputWorldPosition = host.ViewCamera != null ? host.ViewCamera.transform.position : Vector3.zero;
            var peakOutputWorldRotation = host.ViewCamera != null ? host.ViewCamera.transform.rotation : Quaternion.identity;
            host.Presenter.UpdatePresentation(0.155f);

            return new ProductionCameraShakeSample(
                startTick,
                executeTick,
                peakPositionMagnitude,
                peakRotationMagnitude,
                beforeContactWasIdentity,
                contactCrossingNormalizedTime,
                peakOutputWorldPosition,
                peakOutputWorldRotation);
        }

        private static void AssertCameraShakeReturnsToIdentity(GameplaySceneHost host)
        {
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);
            Assert.That(host.Presenter.CurrentCameraShakeMixResult.IsActive, Is.False);
            AssertCameraShakePoseIdentity(host.GetComponent<GameplayCameraRig>());
        }

        private static void AssertCameraShakePoseIdentity(GameplayCameraRig rig)
        {
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(rig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(rig.AdditiveLocalPosition.x, Is.Not.NaN);
            Assert.That(rig.AdditiveLocalRotation.x, Is.Not.NaN);
        }

        private static float MeasureSmallQuaternionAngleDegrees(Quaternion rotation)
        {
            var vectorMagnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z);
            return 2f * Mathf.Atan2(vectorMagnitude, Mathf.Abs(rotation.w)) * Mathf.Rad2Deg;
        }

        private static CinemachineCamera CreateProductionCinemachineCamera(
            GameplaySceneHost host,
            string objectName)
        {
            var cameraObject = new GameObject(objectName);
            cameraObject.transform.SetParent(host.BoardRoot.CameraEffectsRoot, worldPositionStays: false);
            cameraObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
            cinemachineCamera.Target = new CameraTarget
            {
                TrackingTarget = host.BoardRoot.CameraTargetRoot,
                LookAtTarget = host.BoardRoot.CameraTargetRoot,
                CustomLookAtTarget = true,
            };
            return cinemachineCamera;
        }

        private static void AssertFiniteCameraPose(Vector3 position, Quaternion rotation)
        {
            Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x), Is.False);
            Assert.That(float.IsNaN(position.y) || float.IsInfinity(position.y), Is.False);
            Assert.That(float.IsNaN(position.z) || float.IsInfinity(position.z), Is.False);
            Assert.That(float.IsNaN(rotation.x) || float.IsInfinity(rotation.x), Is.False);
            Assert.That(float.IsNaN(rotation.y) || float.IsInfinity(rotation.y), Is.False);
            Assert.That(float.IsNaN(rotation.z) || float.IsInfinity(rotation.z), Is.False);
            Assert.That(float.IsNaN(rotation.w) || float.IsInfinity(rotation.w), Is.False);
        }

        private static GameplayCameraShakeProfile CreateCameraShakeTestProfile()
        {
            var profile = ScriptableObject.CreateInstance<GameplayCameraShakeProfile>();
            profile.SetEntriesForTests(new[]
            {
                CreateCameraShakeEntry(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light),
                CreateCameraShakeEntry(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium),
                CreateCameraShakeEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Medium,
                    CameraShakeVariant.FlipHostileStay),
                CreateCameraShakeEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Medium,
                    CameraShakeVariant.FlipHostileDestroySelf),
                CreateCameraShakeEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Heavy,
                    CameraShakeVariant.FlipHostileFollowThrough),
                CreateCameraShakeEntry(CameraShakeSemantic.PlayerDamageImpact, CameraShakePriority.Light),
                CreateCameraShakeEntry(CameraShakeSemantic.PlayerLethalImpact, CameraShakePriority.Heavy),
                CreateCameraShakeEntry(CameraShakeSemantic.HeavyEnemyJumpLanding, CameraShakePriority.Medium),
            });
            return profile;
        }

        private static CameraShakeProfileEntry CreateCameraShakeEntry(
            CameraShakeSemantic semantic,
            CameraShakePriority priority,
            CameraShakeVariant variant = CameraShakeVariant.Default)
        {
            return new CameraShakeProfileEntry
            {
                Semantic = semantic,
                Variant = variant,
                Priority = priority,
                DurationSeconds = 0.4f,
                OscillationCycles = 2,
                LocalPositionAmplitude = new Vector3(0.02f, 0.015f, 0.025f),
                LocalRotationAmplitudeDegrees = new Vector3(1f, 0.75f, 0.5f),
                AttackSeconds = 0.05f,
                Decay = AnimationCurve.Linear(0f, 1f, 1f, 0f),
                CooldownSeconds = 0.2f,
            };
        }

        private static GameplaySceneHost CreateHost(
            EntityState[] initialEntities,
            InputActionAsset actions = null,
            IEntityLogic[] staticEntityLogics = null,
            float initialMoveDelaySeconds = GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
            float repeatedMoveIntervalSeconds = GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
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
            CubeTopologyState? initialTopology = null,
            EnemyAiProfile defaultEnemyAiProfile = null,
            IKeyboardBindingStore keyboardBindingStore = null,
            GameplayEntityView enemyViewPrefabOverride = null,
            GameplayCameraShakeProfile gameplayCameraShakeProfile = null,
            PlayerContinuousLocomotionSettings playerContinuousLocomotion = null)
        {
            return CreateHostCore(
                initialEntities,
                actions,
                staticEntityLogics,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
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
                initialTopology,
                defaultEnemyAiProfile,
                keyboardBindingStore,
                enemyViewPrefabOverride,
                gameplayCameraShakeProfile,
                playerContinuousLocomotion);
        }

        private static GameplaySceneHost CreateHostCore(
            EntityState[] initialEntities,
            InputActionAsset actions,
            IEntityLogic[] staticEntityLogics,
            float initialMoveDelaySeconds,
            float repeatedMoveIntervalSeconds,
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
            CubeTopologyState? initialTopology,
            EnemyAiProfile defaultEnemyAiProfile,
            IKeyboardBindingStore keyboardBindingStore,
            GameplayEntityView enemyViewPrefabOverride,
            GameplayCameraShakeProfile gameplayCameraShakeProfile,
            PlayerContinuousLocomotionSettings playerContinuousLocomotion)
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
                KeyboardBindingStore = keyboardBindingStore ?? new PlayModeKeyboardBindingStore(),
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                BoxSlideStepIntervalSeconds = 0.2f,
                CellSize = 1f,
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
                PlayerContinuousLocomotion = playerContinuousLocomotion ?? PlayerContinuousLocomotionSettings.CreateDefault(),
                PlayerViewPrefab = playerViewPrefab,
                PushMotionDurationSeconds = 0.2f,
                RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds,
                SimulationTicksPerSecond = 60,
                StaticEntityLogics = staticEntityLogics ?? System.Array.Empty<IEntityLogic>(),
                SnapViewCameraToTarget = viewCamera != null,
                TopologyTransitionPostFxProfile = topologyTransitionPostFxProfile ?? TopologyTransitionPostFxProfile.CreateDefault(),
                ViewCamera = viewCamera,
                DefaultEnemyAiProfile = defaultEnemyAiProfile,
                ViewFactory = enemyViewPrefabOverride != null
                    ? new PlayModeEnemyPresentationViewFactory(hostObject.transform, enemyViewPrefabOverride)
                    : new PlayModePresentationTestViewFactory(hostObject.transform, playerViewPrefab, initialEntities),
                GameplayCameraShakeProfile = gameplayCameraShakeProfile,
            };
            if (runtimeFeatureFlags.HasValue)
            {
                configuration.ApplyRuntimeFeatureFlags(runtimeFeatureFlags.Value);
            }

            host.Initialize(configuration);

            return host;
        }

        // Each fixture explicitly supplies its synthetic non-Player entities. Player authoring
        // continues through the production prefab validation and instantiation path.
        private sealed class PlayModePresentationTestViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
        {
            private readonly Transform _parent;
            private DefaultGameplayEntityViewFactory _playerFactory;
            private readonly Dictionary<int, EntityType> _syntheticEntityTypes;

            public PlayModePresentationTestViewFactory(
                Transform parent,
                GameplayEntityView playerPrefab,
                IEnumerable<EntityState> initialEntities)
            {
                _parent = parent;
                PlayerViewPrefab = playerPrefab;
                _syntheticEntityTypes = initialEntities
                    .Where(entity => entity.entityId != 10)
                    .ToDictionary(entity => entity.entityId, entity => entity.type);
            }

            public GameplayEntityView PlayerViewPrefab { get; }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                // The host creates its board hierarchy after accepting configuration. Resolve
                // the actual registry search root here so later hierarchy rebuilds retain views.
                var entityRoot = _parent.GetComponentInChildren<GameplayBoardRoot>().EntityRoot;
                if (entity.entityId == 10)
                {
                    _playerFactory ??= new DefaultGameplayEntityViewFactory(entityRoot, 1f, 10, PlayerViewPrefab);
                    return _playerFactory.CreateView(entity);
                }

                if (!_syntheticEntityTypes.TryGetValue(entity.entityId, out var expectedType) ||
                    expectedType != entity.type ||
                    (entity.type != EntityType.Unit &&
                     entity.type != EntityType.Box &&
                     entity.type != EntityType.None &&
                     entity.type != EntityType.Wall))
                {
                    throw new InvalidOperationException($"Unconfigured test View: entity {entity.entityId}, type {entity.type}.");
                }

                var root = new GameObject($"PlayModeTestView_{entity.entityId}");
                root.transform.SetParent(entityRoot, worldPositionStays: false);
                var view = root.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                if (entity.type == EntityType.Unit && EntityRolePolicy.IsEnemyUnit(entity))
                {
                    root.AddComponent<EnemyAnimatorDriver>();
                    root.AddComponent<EnemyInactiveVisualController>().ConfigureLegacyColorFallback(true);
                }

                var profile = GameplayEntityVisualProfile.Create(entity.type, 1f);
                view.ConfigureModelRoot(profile.ModelLocalPosition, profile.ModelLocalRotation);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(view.ModelRoot, worldPositionStays: false);
                visual.transform.localScale = profile.ModelLocalScale;
                var collider = visual.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
                return view;
            }
        }

        private sealed class PlayModeEnemyPresentationViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;
            private readonly GameplayEntityView _enemyPrefab;

            public PlayModeEnemyPresentationViewFactory(
                Transform parent,
                GameplayEntityView enemyPrefab)
            {
                _parent = parent;
                _enemyPrefab = enemyPrefab;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                GameplayEntityView view;
                if (entity.unitRole == UnitRole.Enemy)
                {
                    view = UnityEngine.Object.Instantiate(_enemyPrefab, _parent);
                    view.gameObject.SetActive(true);
                }
                else
                {
                    var viewObject = new GameObject($"M3B_TestView_{entity.entityId}");
                    viewObject.transform.SetParent(_parent, worldPositionStays: false);
                    view = viewObject.AddComponent<GameplayEntityView>();
                }

                view.Initialize(entity.entityId);
                return view;
            }
        }

        private static void SetKeyboardState(Keyboard keyboard, params Key[] pressedKeys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressedKeys));
            InputSystem.Update();
        }

        private void QueueActionThenDownInputEvents(
            Keyboard keyboard,
            Key actionKey,
            double inputGapSeconds,
            bool separateInputUpdates)
        {
            currentTime += 1.0;
            var eventTime = currentTime - 0.1;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(actionKey), eventTime);
            if (separateInputUpdates)
            {
                InputSystem.Update();
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(actionKey, Key.S), eventTime + inputGapSeconds);
            InputSystem.Update();
        }

        private void AdvanceInputAndPresentationTime(GameplaySceneHost host, float durationSeconds)
        {
            var remainingSeconds = durationSeconds;
            while (remainingSeconds > 0f)
            {
                var stepSeconds = Mathf.Min(host.TimingProfile.SimulationTickIntervalSeconds, remainingSeconds);
                currentTime += stepSeconds;
                host.InputHost.AdvanceTime(stepSeconds);
                host.Presenter.UpdatePresentation(stepSeconds);
                remainingSeconds -= stepSeconds;
            }
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
                type = EntityType.Wall,
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
                boundEntityId: 0);
        }

        private static GameplaySceneHost CreateFlipSuppressedBarricadeHost(int enemyHp)
        {
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var enemy = CreateUnit(30, landingCell);
            enemy.hp = enemyHp;
            enemy.maxHp = enemyHp;
            enemy.teamId = 2;
            enemy.unitRole = UnitRole.Enemy;
            enemy.aiMode = EnemyAiMode.Chase;
            return CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 4, 4)),
                    CreateUnit(entityId: 11, position: new SurfaceCell(FaceId.Front, 1, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 1), capabilities: BoxCapabilities.Flip),
                    enemy,
                },
                initialTileFeatures: new[]
                {
                    CreateTileFeature(100, landingCell, TileFeatureKind.Barricade),
                },
                tileFeatureDefinitions: new[]
                {
                    CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                },
                staticEntityLogics: new IEntityLogic[]
                {
                    new PlayModeScriptedMovementLogic(new RawMovementIntent(11, 100, new Vector2Int(0, 1), MovementCommandKind.Flip)),
                },
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile(),
                initialTopology: new CubeTopologyState(FaceId.Floor));
        }

        private static EnemyAiProfile LoadTutorialPassiveContactProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(TutorialPassiveContactProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{TutorialPassiveContactProfilePath}'.");
            return profile;
        }

        private static EnemyAiProfile LoadJumpChaserProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(JumpChaserProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{JumpChaserProfilePath}'.");
            return profile;
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

        private static float ResolvePlayerPresentationPhaseDurationSecondsForTest(
            GameplaySceneHost host,
            int entityId,
            PlayerPresentationPhase phase)
        {
            var actionKind = phase switch
            {
                PlayerPresentationPhase.PushWindup => PlayerActionKind.Push,
                PlayerPresentationPhase.PushRecovery => PlayerActionKind.Push,
                PlayerPresentationPhase.FlipWindup => PlayerActionKind.Flip,
                PlayerPresentationPhase.FlipRecovery => PlayerActionKind.Flip,
                _ => PlayerActionKind.None,
            };
            var resolvedActionDurationSeconds = actionKind switch
            {
                PlayerActionKind.Push => host.TimingProfile.PushMotionDurationSeconds,
                PlayerActionKind.Flip => host.TimingProfile.FlipMotionDurationSeconds,
                _ => 0f,
            };

            if (host.ViewRegistry.TryGetView(entityId, out var view) &&
                view != null &&
                view.TryGetComponent<PlayerAnimationTimingAuthoring>(out var authoring) &&
                authoring != null)
            {
                var snapshot = authoring.CreateSnapshot();
                if (snapshot.TryGetAnimatorDurationOverride(phase, out var phaseDurationSeconds))
                {
                    return phaseDurationSeconds;
                }

                if (actionKind != PlayerActionKind.None &&
                    snapshot.TryGetLegacyAnimatorDurationOverride(actionKind, out var legacyActionDurationSeconds))
                {
                    return Mathf.Max(0.0001f, legacyActionDurationSeconds * 0.5f);
                }
            }

            if (resolvedActionDurationSeconds > 0f)
            {
                return Mathf.Max(0.0001f, resolvedActionDurationSeconds * 0.5f);
            }

            return host.TimingProfile.SimulationTickIntervalSeconds;
        }

        private static void RunTicksAssertingNoBlockingPresentation(GameplaySceneHost host, int tickCount)
        {
            for (var i = 0; i < tickCount; i++)
            {
                Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(host.Presenter.HasBlockingPresentation, Is.False);
            }
        }

        private static void AssertAuthoritativePosition(GameplaySceneHost host, int entityId, SurfaceCell expectedPosition)
        {
            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(expectedPosition));
        }

        private static bool TryFindStartedPlayerActionSignal(
            TickResult tick,
            int entityId,
            PlayerActionKind actionKind,
            out TickPlayerActionPresentationSignal signal)
        {
            if (tick == null)
            {
                signal = default;
                return false;
            }

            var signals = tick.PresentationData.PlayerActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var candidate = signals[i];
                if (candidate.EntityId == entityId &&
                    candidate.ActiveActionKind == actionKind &&
                    candidate.StartedThisTick)
                {
                    signal = candidate;
                    return true;
                }
            }

            signal = default;
            return false;
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
            var expectedLocalPosition = projectedPose.LocalPosition;
            var expectedFacing = entity.facing;
            if (snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var continuousPose))
            {
                expectedLocalPosition +=
                    projectedPose.LocalRotation *
                    new Vector3(
                        continuousPose.LocalOffset.X.RawValue / (float)SimulationFixed.UnitsPerCell,
                        continuousPose.LocalOffset.Y.RawValue / (float)SimulationFixed.UnitsPerCell,
                        0f);
                if (continuousPose.State.facing != Direction.None)
                {
                    expectedFacing = continuousPose.State.facing;
                }
            }

            Assert.That(
                projector.TryResolveEntityRotation(entity.position, snapshot.Topology, expectedFacing, out var projectedRotation),
                Is.True);
            var expectedWorldPosition = host.BoardRoot.transform.TransformPoint(expectedLocalPosition);
            var actualWorldPosition = view.transform.position;
            var positionDelta = Vector3.Distance(actualWorldPosition, expectedWorldPosition);
            Assert.That(
                positionDelta,
                Is.LessThanOrEqualTo(ProjectedViewPositionTolerance),
                $"entity {entityId} root position mismatch at next tick {host.TickRunner.NextTickIndex}. " +
                $"expected={FormatVector3(expectedWorldPosition)} actual={FormatVector3(actualWorldPosition)} " +
                $"delta={positionDelta:R} expectedLocal={FormatVector3(expectedLocalPosition)} " +
                $"cell={entity.position} facing={entity.facing} expectedFacing={expectedFacing}");
            Assert.That(
                Quaternion.Angle(
                    view.transform.rotation,
                    host.BoardRoot.transform.rotation * projectedRotation),
                Is.LessThan(0.1f));
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:R}, {value.y:R}, {value.z:R})";
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

        private static void AssertViewRotationBetweenFacings(
            GameplaySceneHost host,
            int entityId,
            Direction sourceFacing,
            Direction targetFacing)
        {
            var snapshot = CaptureAuthoritativeSnapshot(host);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            var projector = new GameplayCubeProjector(snapshot.BoardBounds, 1f);
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(entity.position, snapshot.Topology, sourceFacing, out var sourceRotation),
                Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(entity.position, snapshot.Topology, targetFacing, out var targetRotation),
                Is.True);

            var sourceWorldRotation = host.BoardRoot.transform.rotation * sourceRotation;
            var targetWorldRotation = host.BoardRoot.transform.rotation * targetRotation;
            Assert.That(Quaternion.Angle(sourceWorldRotation, targetWorldRotation), Is.GreaterThan(1f));
            Assert.That(
                Quaternion.Angle(view.transform.rotation, sourceWorldRotation),
                Is.GreaterThan(0.5f),
                "Recovery midpoint should have advanced away from the contact-facing rotation.");
            Assert.That(
                Quaternion.Angle(view.transform.rotation, targetWorldRotation),
                Is.GreaterThan(0.5f),
                "Recovery midpoint should not snap directly to the result-facing rotation.");
        }

        private static WorldSnapshot CaptureAuthoritativeSnapshot(GameplaySceneHost host)
        {
            return GameplayCompositionRoot.CreateSnapshot(host.WorldState);
        }

        private static void SetPlayerForwardTopologyOvershootPose(GameplaySceneHost host, int entityId)
        {
            var speed = PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .SpeedUnitsPerTick;
            InvokeWorldWriteContextMethod(
                host.WorldState,
                "SetUnitContinuousLocomotionState",
                entityId,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.Zero,
                        SimulationFixed.FromRaw(SimulationFixed.MaxPositiveLocalOffset)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Up,
                    lastMoveDirection = Direction.Up,
                    speedUnitsPerTick = speed,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
        }

        private static InputActionAsset CloneProductionInputActions()
        {
            var production = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(production, Is.Not.Null);
            return InputActionAsset.FromJson(production.ToJson());
        }

        private static InputActionAsset CreateKeyboardMoveActions(bool includeArrowKeys = false)
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap(GameplayInputActionPaths.PlayerActionMap);
            var move = map.AddAction(GameplayInputActionPaths.MoveAction, InputActionType.Value);
            var pushAction = map.AddAction(GameplayInputActionPaths.PushAction, InputActionType.Button);
            var flipAction = map.AddAction(GameplayInputActionPaths.FlipAction, InputActionType.Button);
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

            pushAction.AddBinding("<Keyboard>/j");
            flipAction.AddBinding("<Keyboard>/k");
            actions.AddActionMap(map);

            var uiMap = new InputActionMap(GameplayInputActionPaths.UiActionMap);
            var navigate = uiMap.AddAction(GameplayInputActionPaths.NavigateAction, InputActionType.PassThrough);
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            if (includeArrowKeys)
            {
                navigate.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
            }

            actions.AddActionMap(uiMap);
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

        private static PlayerControlTimingSettings CreateFlipTimingSettings(
            int flipExecuteDelayTicks,
            int flipInputLockDurationTicks)
        {
            var ticksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond;
            return new PlayerControlTimingSettings
            {
                FlipExecuteDelaySeconds = flipExecuteDelayTicks / (float)ticksPerSecond,
                FlipInputLockDurationSeconds = flipInputLockDurationTicks / (float)ticksPerSecond,
            };
        }

        private sealed class PlayModeScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _movementIntent;

            public PlayModeScriptedMovementLogic(RawMovementIntent movementIntent)
            {
                _movementIntent = movementIntent;
            }

            public int ControlledEntityId => _movementIntent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                buffer.Add(_movementIntent);
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
