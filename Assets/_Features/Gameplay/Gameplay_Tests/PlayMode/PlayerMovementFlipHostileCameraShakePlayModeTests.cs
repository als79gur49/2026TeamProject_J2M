using System;
using System.Collections;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed partial class PlayerMovementPlayModeTests
    {
        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_FlipHostileStay_ContactExactlyOnceAndSimulationIsolated()
        {
            var profile = LoadCampaignCameraShakeProfile();
            var fullHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.Stay);
            var reducedHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.Stay);
            var offHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.Stay);

            var full = RunHostileFlipCameraShake(
                fullHost,
                profile,
                CameraMotionLevel.Full,
                FlipFloorImpactPresentationKind.Stay,
                pauseBeforeMilestone: true);
            var reduced = RunHostileFlipCameraShake(
                reducedHost,
                profile,
                CameraMotionLevel.Reduced,
                FlipFloorImpactPresentationKind.Stay,
                pauseBeforeMilestone: false);
            var off = RunHostileFlipCameraShake(
                offHost,
                profile,
                CameraMotionLevel.Off,
                FlipFloorImpactPresentationKind.Stay,
                pauseBeforeMilestone: false);

            AssertHostileSettingsAndSimulationIsolation(full, reduced, off);
            Assert.That(full.ExecuteTick.PresentationData.FlipImpactSignals.Single().Disposition,
                Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(full.ExecuteTick.PresentationData.EntityExitSignals.Count, Is.EqualTo(2));
            WriteHostileCameraShakeTrace("Stay", full);

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_RepeatedFlipHostileStaySameBox_ActualActionsSubmitAtEachContact()
        {
            var host = CreateRepeatedHostileStayCameraShakeHost();
            host.Presenter.ConfigureGameplayCameraShakeProfile(LoadCampaignCameraShakeProfile());
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);

            var first = ExecuteRepeatedHostileStayOnce(
                host,
                Vector2.left,
                expectedAcceptedBefore: 0);
            AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, -1, 0));
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(20, out _), Is.False);
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(21, out _), Is.False);

            var actionCleanupTick = RunTicksUntil(
                host,
                _ => CaptureAuthoritativeSnapshot(host).TryGetPlayerControlState(10, out var state) &&
                     state.activeAction.kind == PlayerActionKind.None,
                maxTicks: 4);
            Assert.That(actionCleanupTick, Is.Not.Null, "First Flip action did not leave recovery.");
            var blockerResetTick = RunTicksUntil(
                host,
                _ =>
                {
                    var snapshot = CaptureAuthoritativeSnapshot(host);
                    return snapshot.TryGetEntity(12, out var blocker) &&
                           blocker.position == new SurfaceCell(FaceId.Floor, 1, 1) &&
                           UnitSpatialQuery.IsSettledAtAnchor(snapshot, 12) &&
                           UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10);
                },
                maxTicks: 128);
            Assert.That(blockerResetTick, Is.Not.Null,
                "Player and Stay reservation blocker did not settle before the second Flip.");

            var second = ExecuteRepeatedHostileStayOnce(
                host,
                Vector2.left,
                expectedAcceptedBefore: 1);
            var firstImpact = first.ExecuteTick.PresentationData.FlipImpactSignals.Single();
            var secondImpact = second.ExecuteTick.PresentationData.FlipImpactSignals.Single();
            Assert.That(second.ExecuteTick.TickIndex, Is.GreaterThan(first.ExecuteTick.TickIndex));
            Assert.That(firstImpact.BoxEntityId, Is.EqualTo(30));
            Assert.That(secondImpact.BoxEntityId, Is.EqualTo(30));
            Assert.That(firstImpact.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(secondImpact.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(secondImpact.SourceActionPlanId, Is.EqualTo(firstImpact.SourceActionPlanId));
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(2));
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(22, out _), Is.False);
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(23, out _), Is.False);
            AssertAuthoritativePosition(host, entityId: 30, new SurfaceCell(FaceId.Floor, -1, 0));
            AssertCameraShakeReturnsToIdentity(host);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_FlipHostileDestroySelf_BreakContactExactlyOnceAndSimulationIsolated()
        {
            var profile = LoadCampaignCameraShakeProfile();
            var fullHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.DestroySelf);
            var reducedHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.DestroySelf);
            var offHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.DestroySelf);

            var full = RunHostileFlipCameraShake(
                fullHost,
                profile,
                CameraMotionLevel.Full,
                FlipFloorImpactPresentationKind.DestroySelf,
                pauseBeforeMilestone: true);
            var reduced = RunHostileFlipCameraShake(
                reducedHost,
                profile,
                CameraMotionLevel.Reduced,
                FlipFloorImpactPresentationKind.DestroySelf,
                pauseBeforeMilestone: false);
            var off = RunHostileFlipCameraShake(
                offHost,
                profile,
                CameraMotionLevel.Off,
                FlipFloorImpactPresentationKind.DestroySelf,
                pauseBeforeMilestone: false);

            AssertHostileSettingsAndSimulationIsolation(full, reduced, off);
            Assert.That(full.ExecuteTick.PresentationData.FlipImpactSignals.Single().Disposition,
                Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(
                full.ExecuteTick.PresentationData.EntityExitSignals.Count(signal =>
                    signal.ExitedEntityId == 30 && signal.ExitCause == TickEntityExitCause.BoxDestroy),
                Is.EqualTo(1));
            WriteHostileCameraShakeTrace("DestroySelf", full);

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_FlipHostileStay_LargeDeltaCompletionPreservesContactCrossing()
        {
            var host = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.Stay);

            AssertHostileLargeDeltaCompletionCrossing(
                host,
                FlipFloorImpactPresentationKind.Stay,
                MotionTrackProgressSourceKind.OriginalViewMotion);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_FlipHostileDestroySelf_LargeDeltaCompletionPreservesContactCrossing()
        {
            var host = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.DestroySelf);

            AssertHostileLargeDeltaCompletionCrossing(
                host,
                FlipFloorImpactPresentationKind.DestroySelf,
                MotionTrackProgressSourceKind.FlipInteraction);

            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_FlipHostileFollowThrough_InitialContactSilentFinalLandingExactlyOnce()
        {
            var profile = LoadCampaignCameraShakeProfile();
            var fullHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.FollowThrough);
            var reducedHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.FollowThrough);
            var offHost = CreateHostileFlipCameraShakeHost(FlipFloorImpactPresentationKind.FollowThrough);

            var full = RunHostileFlipCameraShake(
                fullHost,
                profile,
                CameraMotionLevel.Full,
                FlipFloorImpactPresentationKind.FollowThrough,
                pauseBeforeMilestone: true);
            var reduced = RunHostileFlipCameraShake(
                reducedHost,
                profile,
                CameraMotionLevel.Reduced,
                FlipFloorImpactPresentationKind.FollowThrough,
                pauseBeforeMilestone: false);
            var off = RunHostileFlipCameraShake(
                offHost,
                profile,
                CameraMotionLevel.Off,
                FlipFloorImpactPresentationKind.FollowThrough,
                pauseBeforeMilestone: false);

            AssertHostileSettingsAndSimulationIsolation(full, reduced, off);
            Assert.That(full.ExecuteTick.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(full.InitialHostileContactWasSilent, Is.True);
            Assert.That(full.MilestoneNormalizedTime,
                Is.GreaterThanOrEqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
            Assert.That(
                full.ExecuteTick.PresentationData.EntityExitSignals.Count(signal =>
                    signal.ExitedEntityId == 20 && signal.ExitCause == TickEntityExitCause.EnemyDeath),
                Is.EqualTo(1));
            WriteHostileCameraShakeTrace("FollowThrough", full);

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayCameraShakeProduction_FlipHostileFollowThrough_DirectAndActualCinemachineShareContributionAndReset()
        {
            var directCameraObject = new GameObject("M2B_FollowThroughDirectCamera");
            var cinemachineOutputObject = new GameObject("M2B_FollowThroughCinemachineOutput");
            var directCamera = directCameraObject.AddComponent<Camera>();
            var cinemachineOutput = cinemachineOutputObject.AddComponent<Camera>();
            var brain = cinemachineOutputObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            var directHost = CreateHostileFlipCameraShakeHost(
                FlipFloorImpactPresentationKind.FollowThrough,
                directCamera);
            var cinemachineHost = CreateHostileFlipCameraShakeHost(
                FlipFloorImpactPresentationKind.FollowThrough,
                cinemachineOutput);
            var virtualCamera = CreateProductionCinemachineCamera(cinemachineHost, "M2B_FollowThroughCinemachineCamera");

            yield return null;
            brain.ManualUpdate();
            Assert.That(brain.isActiveAndEnabled, Is.True);
            Assert.That(virtualCamera.isActiveAndEnabled, Is.True);

            var profile = LoadCampaignCameraShakeProfile();
            var direct = RunHostileFlipCameraShake(
                directHost,
                profile,
                CameraMotionLevel.Full,
                FlipFloorImpactPresentationKind.FollowThrough,
                pauseBeforeMilestone: false);
            var cinemachine = RunHostileFlipCameraShake(
                cinemachineHost,
                profile,
                CameraMotionLevel.Full,
                FlipFloorImpactPresentationKind.FollowThrough,
                pauseBeforeMilestone: false,
                onPeak: brain.ManualUpdate);

            Assert.That(cinemachine.PeakPositionMagnitude, Is.EqualTo(direct.PeakPositionMagnitude).Within(0.000001f));
            Assert.That(cinemachine.PeakRotationMagnitude, Is.EqualTo(direct.PeakRotationMagnitude).Within(0.0001f));
            AssertFiniteCameraPose(direct.PeakOutputWorldPosition, direct.PeakOutputWorldRotation);
            AssertFiniteCameraPose(cinemachine.PeakOutputWorldPosition, cinemachine.PeakOutputWorldRotation);
            Assert.That(cinemachine.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(cinemachine.PeakRotationMagnitude, Is.GreaterThan(0.001f));
            AssertCameraShakeReturnsToIdentity(directHost);
            AssertCameraShakeReturnsToIdentity(cinemachineHost);

            yield return DestroyHost(directHost);
            yield return DestroyHost(cinemachineHost);
            UnityEngine.Object.Destroy(directCameraObject);
            UnityEngine.Object.Destroy(cinemachineOutputObject);
            yield return null;
        }

        private static GameplaySceneHost CreateHostileFlipCameraShakeHost(
            FlipFloorImpactPresentationKind disposition,
            Camera viewCamera = null)
        {
            var enemyHp = disposition == FlipFloorImpactPresentationKind.DestroySelf ? 2 : 1;
            var enemy = CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0));
            enemy.hp = enemyHp;
            enemy.maxHp = enemyHp;
            enemy.teamId = 2;
            enemy.unitRole = UnitRole.Enemy;
            enemy.aiMode = EnemyAiMode.Chase;

            if (disposition == FlipFloorImpactPresentationKind.Stay)
            {
                var secondEnemy = CreateUnit(21, new SurfaceCell(FaceId.Floor, 1, 0));
                secondEnemy.hp = 1;
                secondEnemy.maxHp = 1;
                secondEnemy.teamId = 2;
                secondEnemy.unitRole = UnitRole.Enemy;
                secondEnemy.aiMode = EnemyAiMode.Chase;
                return AddFlipInteractionDriver(CreateHost(
                    new[]
                    {
                        CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateUnit(entityId: 12, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                        CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                        enemy,
                        secondEnemy,
                    },
                    staticEntityLogics: new IEntityLogic[] { new HostileStayScriptedMovementLogic() },
                    defaultEnemyAiProfile: LoadTutorialPassiveContactProfile(),
                    viewCamera: viewCamera));
            }

            return AddFlipInteractionDriver(CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                    enemy,
                },
                playerControlTiming: CreateFlipTimingSettings(
                    flipExecuteDelayTicks: 1,
                    flipInputLockDurationTicks: 1),
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile(),
                viewCamera: viewCamera));
        }

        private static GameplaySceneHost CreateRepeatedHostileStayCameraShakeHost()
        {
            var firstEnemy = CreateHostileCameraShakeEnemy(20, new SurfaceCell(FaceId.Floor, 1, 0));
            var firstEnemyPeer = CreateHostileCameraShakeEnemy(21, new SurfaceCell(FaceId.Floor, 1, 0));
            var secondEnemy = CreateHostileCameraShakeEnemy(22, new SurfaceCell(FaceId.Floor, 2, 0));
            var secondEnemyPeer = CreateHostileCameraShakeEnemy(23, new SurfaceCell(FaceId.Floor, 2, 0));
            return AddFlipInteractionDriver(CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateUnit(entityId: 12, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                    firstEnemy,
                    firstEnemyPeer,
                    secondEnemy,
                    secondEnemyPeer,
                },
                staticEntityLogics: new IEntityLogic[] { new RepeatedHostileStayReservationLogic() },
                initialMoveDelaySeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                repeatedMoveIntervalSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                playerControlTiming: CreateFlipTimingSettings(
                    flipExecuteDelayTicks: 1,
                    flipInputLockDurationTicks: 1),
                defaultEnemyAiProfile: LoadTutorialPassiveContactProfile()));
        }

        private static EntityState CreateHostileCameraShakeEnemy(int entityId, SurfaceCell position)
        {
            var enemy = CreateUnit(entityId, position);
            enemy.hp = 1;
            enemy.maxHp = 1;
            enemy.teamId = 2;
            enemy.unitRole = UnitRole.Enemy;
            enemy.aiMode = EnemyAiMode.Chase;
            return enemy;
        }

        private static GameplaySceneHost AddFlipInteractionDriver(GameplaySceneHost host)
        {
            Assert.That(host.ViewRegistry.TryGetView(30, out var boxView), Is.True);
            if (!boxView.TryGetComponent<BoxFlipInteractionDriver>(out _))
            {
                boxView.gameObject.AddComponent<BoxFlipInteractionDriver>();
            }

            return host;
        }

        private static HostileProductionCameraShakeSample RunHostileFlipCameraShake(
            GameplaySceneHost host,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel,
            FlipFloorImpactPresentationKind disposition,
            bool pauseBeforeMilestone,
            Action onPeak = null)
        {
            host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
            host.Presenter.SetCameraMotionLevel(motionLevel);
            var rig = host.GetComponent<GameplayCameraRig>();
            AssertCameraShakePoseIdentity(rig);

            TickResult startTick = null;
            TickResult executeTick;
            if (disposition == FlipFloorImpactPresentationKind.Stay)
            {
                executeTick = host.InputHost.RunSingleTick();
            }
            else
            {
                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                startTick = host.InputHost.RunSingleTick();
                Assert.That(startTick, Is.Not.Null);
                Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
                host.InputHost.SetRawMoveInput(Vector2.zero);
                executeTick = host.InputHost.RunSingleTick();
            }

            Assert.That(executeTick, Is.Not.Null);
            var floorSignal = executeTick.PresentationData.FlipFloorImpactSignals.Single();
            Assert.That(floorSignal.Kind, Is.EqualTo(disposition));
            Assert.That(floorSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(floorSignal.SourceActionPlanId, Is.GreaterThan(0));
            Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);
            AssertCameraShakePoseIdentity(rig);
            if (pauseBeforeMilestone)
            {
                host.Presenter.SetPresentationPaused(true);
                host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);
                Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.EqualTo(1));
                Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);
                AssertCameraShakePoseIdentity(rig);
                host.Presenter.SetPresentationPaused(false);
            }

            var requiredSource = disposition switch
            {
                FlipFloorImpactPresentationKind.Stay => MotionTrackProgressSourceKind.OriginalViewMotion,
                FlipFloorImpactPresentationKind.DestroySelf => MotionTrackProgressSourceKind.FlipInteraction,
                _ => MotionTrackProgressSourceKind.LocalMotion,
            };
            var targetMilestone = disposition == FlipFloorImpactPresentationKind.FollowThrough
                ? GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime
                : GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime;
            var beforeMilestoneWasIdentity = true;
            var initialHostileContactWasSilent = true;
            var pauseVerified = true;
            var milestoneNormalizedTime = 0f;

            for (var updateIndex = 0; updateIndex < 160; updateIndex++)
            {
                host.Presenter.UpdatePresentation(0.005f);
                var progress = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(sample =>
                    sample.EntityId == 30 &&
                    sample.MotionKind == TickEntityMotionKind.Flip &&
                    sample.TickIndex == executeTick.TickIndex &&
                    sample.SequenceOrActionPlanId == floorSignal.SourceActionPlanId &&
                    sample.SourceKind == requiredSource);
                if (!progress.IsValid)
                {
                    continue;
                }

                if (disposition == FlipFloorImpactPresentationKind.FollowThrough &&
                    progress.CurrentNormalizedTime >= GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime &&
                    progress.CurrentNormalizedTime < targetMilestone)
                {
                    initialHostileContactWasSilent &=
                        host.Presenter.AcceptedGameplayCameraImpulseCount == 0 &&
                        rig.AdditiveLocalPosition.sqrMagnitude <= 0.000000000001f &&
                        rig.AdditiveLocalRotation == Quaternion.identity;
                }

                if (progress.CurrentNormalizedTime < targetMilestone)
                {
                    beforeMilestoneWasIdentity &=
                        host.Presenter.AcceptedGameplayCameraImpulseCount == 0 &&
                        rig.AdditiveLocalPosition.sqrMagnitude <= 0.000000000001f &&
                        rig.AdditiveLocalRotation == Quaternion.identity;
                    continue;
                }

                Assert.That(progress.PreviousNormalizedTime, Is.LessThan(targetMilestone));
                milestoneNormalizedTime = progress.CurrentNormalizedTime;
                break;
            }

            Assert.That(pauseVerified, Is.True);
            Assert.That(beforeMilestoneWasIdentity, Is.True);
            Assert.That(milestoneNormalizedTime, Is.GreaterThanOrEqualTo(targetMilestone));
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.PendingFlipLandingCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.EqualTo(1));
            Assert.That(host.Presenter.CurrentCameraShakeMixResult.IsActive, Is.EqualTo(motionLevel != CameraMotionLevel.Off));

            host.Presenter.UpdatePresentation(0.02f);
            onPeak?.Invoke();
            var peakPositionMagnitude = rig.AdditiveLocalPosition.magnitude;
            var peakRotationMagnitude = MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation);
            var peakOutputWorldPosition = host.ViewCamera != null ? host.ViewCamera.transform.position : Vector3.zero;
            var peakOutputWorldRotation = host.ViewCamera != null ? host.ViewCamera.transform.rotation : Quaternion.identity;
            host.Presenter.UpdatePresentation(0.5f);

            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));
            AssertCameraShakeReturnsToIdentity(host);
            return new HostileProductionCameraShakeSample(
                startTick,
                executeTick,
                peakPositionMagnitude,
                peakRotationMagnitude,
                beforeMilestoneWasIdentity,
                initialHostileContactWasSilent,
                milestoneNormalizedTime,
                peakOutputWorldPosition,
                peakOutputWorldRotation);
        }

        private static HostileProductionCameraShakeSample ExecuteRepeatedHostileStayOnce(
            GameplaySceneHost host,
            Vector2 flipInput,
            int expectedAcceptedBefore)
        {
            var rig = host.GetComponent<GameplayCameraRig>();
            AssertCameraShakePoseIdentity(rig);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(expectedAcceptedBefore));

            host.InputHost.SetRawMoveInput(flipInput);
            host.InputHost.BufferFlip();
            var startTick = host.InputHost.RunSingleTick();
            Assert.That(startTick, Is.Not.Null);
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetPlayerControlState(10, out var startedState), Is.True);
            Assert.That(startedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(startedState.activeAction.executionAttempted, Is.False);
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);

            host.InputHost.SetRawMoveInput(Vector2.zero);
            var executeTick = host.InputHost.RunSingleTick();
            Assert.That(executeTick, Is.Not.Null);
            var floorSignal = executeTick.PresentationData.FlipFloorImpactSignals.Single();
            Assert.That(floorSignal.Kind, Is.EqualTo(FlipFloorImpactPresentationKind.Stay));
            var impactSignal = executeTick.PresentationData.FlipImpactSignals.Single();
            Assert.That(impactSignal.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(expectedAcceptedBefore));

            var crossedContact = false;
            for (var updateIndex = 0; updateIndex < 160; updateIndex++)
            {
                host.Presenter.UpdatePresentation(0.005f);
                var progress = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(sample =>
                    sample.EntityId == floorSignal.BoxEntityId &&
                    sample.MotionKind == TickEntityMotionKind.Flip &&
                    sample.TickIndex == executeTick.TickIndex &&
                    sample.SequenceOrActionPlanId == floorSignal.SourceActionPlanId &&
                    sample.SourceKind == MotionTrackProgressSourceKind.OriginalViewMotion);
                if (!progress.IsValid ||
                    progress.CurrentNormalizedTime < GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime)
                {
                    Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(expectedAcceptedBefore));
                    continue;
                }

                Assert.That(
                    progress.PreviousNormalizedTime,
                    Is.LessThan(GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime));
                crossedContact = true;
                break;
            }

            Assert.That(crossedContact, Is.True);
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(expectedAcceptedBefore + 1));
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.EqualTo(1));

            host.Presenter.UpdatePresentation(0.02f);
            var peakPositionMagnitude = rig.AdditiveLocalPosition.magnitude;
            var peakRotationMagnitude = MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation);
            Assert.That(peakPositionMagnitude, Is.GreaterThan(0.000001f));
            host.Presenter.UpdatePresentation(0.5f);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(expectedAcceptedBefore + 1));
            AssertCameraShakeReturnsToIdentity(host);

            return new HostileProductionCameraShakeSample(
                startTick,
                executeTick,
                peakPositionMagnitude,
                peakRotationMagnitude,
                beforeMilestoneWasIdentity: true,
                initialHostileContactWasSilent: true,
                milestoneNormalizedTime: GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime,
                peakOutputWorldPosition: Vector3.zero,
                peakOutputWorldRotation: Quaternion.identity);
        }

        private static void AssertHostileLargeDeltaCompletionCrossing(
            GameplaySceneHost host,
            FlipFloorImpactPresentationKind disposition,
            MotionTrackProgressSourceKind expectedProgressSource)
        {
            host.Presenter.ConfigureGameplayCameraShakeProfile(LoadCampaignCameraShakeProfile());
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);

            TickResult executeTick;
            if (disposition == FlipFloorImpactPresentationKind.Stay)
            {
                executeTick = host.InputHost.RunSingleTick();
            }
            else
            {
                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                host.InputHost.RunSingleTick();
                host.InputHost.SetRawMoveInput(Vector2.zero);
                executeTick = host.InputHost.RunSingleTick();
            }

            Assert.That(executeTick, Is.Not.Null);
            var floorSignal = executeTick.PresentationData.FlipFloorImpactSignals.Single();
            Assert.That(floorSignal.Kind, Is.EqualTo(disposition));
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);

            const float preContactNormalizedTime = 0.55f;
            var motionDuration = host.TimingProfile.FlipMotionDurationSeconds;
            host.Presenter.UpdatePresentation(motionDuration * preContactNormalizedTime);

            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);

            host.Presenter.UpdatePresentation(motionDuration);
            var completionProgress = host.Presenter.CurrentMotionTrackProgressSamples.LastOrDefault(sample =>
                sample.EntityId == floorSignal.BoxEntityId &&
                sample.MotionKind == TickEntityMotionKind.Flip &&
                sample.TickIndex == executeTick.TickIndex &&
                sample.SequenceOrActionPlanId == floorSignal.SourceActionPlanId &&
                sample.SourceKind == expectedProgressSource);

            Assert.That(completionProgress.IsValid, Is.True);
            Assert.That(
                completionProgress.PreviousNormalizedTime,
                Is.EqualTo(preContactNormalizedTime).Within(0.0001f));
            Assert.That(
                completionProgress.PreviousNormalizedTime,
                Is.LessThan(GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime));
            Assert.That(
                completionProgress.CurrentNormalizedTime,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                completionProgress.CurrentNormalizedTime,
                Is.GreaterThanOrEqualTo(
                    GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime));
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));

            host.Presenter.UpdatePresentation(motionDuration);
            Assert.That(host.Presenter.PendingFlipHostileImpactCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));
        }

        private static void AssertHostileSettingsAndSimulationIsolation(
            HostileProductionCameraShakeSample full,
            HostileProductionCameraShakeSample reduced,
            HostileProductionCameraShakeSample off)
        {
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
            Assert.That(reduced.ExecuteTick.FinalTopology, Is.EqualTo(full.ExecuteTick.FinalTopology));
            Assert.That(off.ExecuteTick.FinalTopology, Is.EqualTo(full.ExecuteTick.FinalTopology));
            Assert.That(reduced.ExecuteTick.TickIndex, Is.EqualTo(full.ExecuteTick.TickIndex));
            Assert.That(off.ExecuteTick.TickIndex, Is.EqualTo(full.ExecuteTick.TickIndex));
        }

        private static void WriteHostileCameraShakeTrace(string disposition, HostileProductionCameraShakeSample sample)
        {
            TestContext.WriteLine(
                $"M2B {disposition} tick={sample.ExecuteTick.TickIndex} " +
                $"contactN={GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime:F3} " +
                $"submitN={sample.MilestoneNormalizedTime:F3} accepted=1 ordinaryLanding=0 " +
                $"peakPos={sample.PeakPositionMagnitude:F6} peakRot={sample.PeakRotationMagnitude:F6}");
        }

        private readonly struct HostileProductionCameraShakeSample
        {
            public HostileProductionCameraShakeSample(
                TickResult startTick,
                TickResult executeTick,
                float peakPositionMagnitude,
                float peakRotationMagnitude,
                bool beforeMilestoneWasIdentity,
                bool initialHostileContactWasSilent,
                float milestoneNormalizedTime,
                Vector3 peakOutputWorldPosition,
                Quaternion peakOutputWorldRotation)
            {
                StartTick = startTick;
                ExecuteTick = executeTick;
                PeakPositionMagnitude = peakPositionMagnitude;
                PeakRotationMagnitude = peakRotationMagnitude;
                BeforeMilestoneWasIdentity = beforeMilestoneWasIdentity;
                InitialHostileContactWasSilent = initialHostileContactWasSilent;
                MilestoneNormalizedTime = milestoneNormalizedTime;
                PeakOutputWorldPosition = peakOutputWorldPosition;
                PeakOutputWorldRotation = peakOutputWorldRotation;
            }

            public TickResult StartTick { get; }
            public TickResult ExecuteTick { get; }
            public float PeakPositionMagnitude { get; }
            public float PeakRotationMagnitude { get; }
            public bool BeforeMilestoneWasIdentity { get; }
            public bool InitialHostileContactWasSilent { get; }
            public float MilestoneNormalizedTime { get; }
            public Vector3 PeakOutputWorldPosition { get; }
            public Quaternion PeakOutputWorldRotation { get; }
        }

        private sealed class HostileStayScriptedMovementLogic : IMovementEntityLogic
        {
            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                System.Collections.Generic.List<RawMovementIntent> buffer)
            {
                buffer.Add(new RawMovementIntent(
                    12,
                    200,
                    new Vector2Int(1, 0),
                    MovementCommandKind.Move));
                buffer.Add(new RawMovementIntent(
                    10,
                    100,
                    new Vector2Int(-1, 0),
                    MovementCommandKind.Flip));
            }
        }

        private sealed class RepeatedHostileStayReservationLogic : IMovementEntityLogic
        {
            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                System.Collections.Generic.List<RawMovementIntent> buffer)
            {
                if (!snapshot.TryGetPlayerControlState(10, out var controlState))
                {
                    return;
                }

                var action = controlState.activeAction;
                if (action.kind == PlayerActionKind.Flip &&
                    action.sequence >= 2 &&
                    !action.executionAttempted &&
                    input.TickIndex == action.startTick)
                {
                    buffer.Add(new RawMovementIntent(
                        sourceId: 22,
                        priority: 210,
                        destination: new Vector2Int(1, 0),
                        commandKind: MovementCommandKind.Move));
                    buffer.Add(new RawMovementIntent(
                        sourceId: 23,
                        priority: 210,
                        destination: new Vector2Int(1, 0),
                        commandKind: MovementCommandKind.Move));
                    return;
                }

                if (action.kind == PlayerActionKind.Flip &&
                    action.executionAttempted &&
                    input.TickIndex == action.executeTick)
                {
                    buffer.Add(new RawMovementIntent(
                        sourceId: 12,
                        priority: 200,
                        destination: new Vector2Int(1, 0),
                        commandKind: MovementCommandKind.Move));
                    return;
                }

                if (!snapshot.TryGetEntity(20, out _) &&
                    !snapshot.TryGetEntity(21, out _) &&
                    snapshot.TryGetEntity(12, out var blocker) &&
                    blocker.position == new SurfaceCell(FaceId.Floor, 1, 0))
                {
                    buffer.Add(new RawMovementIntent(
                        sourceId: 12,
                        priority: 200,
                        destination: new Vector2Int(1, 1),
                        commandKind: MovementCommandKind.Move));
                }
            }
        }
    }
}
