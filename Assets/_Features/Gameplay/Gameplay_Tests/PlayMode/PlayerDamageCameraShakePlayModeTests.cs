using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed partial class PlayerMovementPlayModeTests
    {
        private const string AstretonPresentationPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Astreton.prefab";
        private const string NonHeavyPresentationPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_RocketFace.prefab";

        [UnityTest]
        [Category("Full")]
        public IEnumerator HeavyEnemyJumpLanding_ProductionAstreton_FullReducedOffRemainAuthoritativelyEquivalent()
        {
            var fullCameraObject = new GameObject("M3B_FullDirectCamera");
            var reducedCameraObject = new GameObject("M3B_ReducedDirectCamera");
            var offCameraObject = new GameObject("M3B_OffDirectCamera");
            var fullHost = CreateHeavyEnemyJumpLandingHost(fullCameraObject.AddComponent<Camera>());
            var reducedHost = CreateHeavyEnemyJumpLandingHost(reducedCameraObject.AddComponent<Camera>());
            var offHost = CreateHeavyEnemyJumpLandingHost(offCameraObject.AddComponent<Camera>());

            var full = RunHeavyEnemyJumpLanding(fullHost, CameraMotionLevel.Full);
            var reduced = RunHeavyEnemyJumpLanding(reducedHost, CameraMotionLevel.Reduced);
            var off = RunHeavyEnemyJumpLanding(offHost, CameraMotionLevel.Off);

            Assert.That(full.LandingSignal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Landed));
            Assert.That(full.LandingSignal.EntityId, Is.EqualTo(40));
            Assert.That(full.LandingSignal.Sequence, Is.EqualTo(1));
            Assert.That(fullHost.Presenter.ObservedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(fullHost.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(full.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(full.PeakRotationMagnitude, Is.GreaterThan(0.001f));
            Assert.That(reduced.PeakPositionMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakPositionMagnitude, Is.LessThan(full.PeakPositionMagnitude));
            Assert.That(reduced.PeakRotationMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakRotationMagnitude, Is.LessThan(full.PeakRotationMagnitude));
            Assert.That(off.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(off.PeakRotationMagnitude, Is.Zero.Within(0.0001f));
            AssertAuthoritativeCameraMotionParity(full.LandingTick, reduced.LandingTick, off.LandingTick);
            Assert.That(GetEntity(reducedHost, 40), Is.EqualTo(GetEntity(fullHost, 40)));
            Assert.That(GetEntity(offHost, 40), Is.EqualTo(GetEntity(fullHost, 40)));
            Assert.That(CaptureAuthoritativeSnapshot(fullHost).TryGetEnemyJumpState(40, out var fullJump), Is.True);
            Assert.That(CaptureAuthoritativeSnapshot(reducedHost).TryGetEnemyJumpState(40, out var reducedJump), Is.True);
            Assert.That(CaptureAuthoritativeSnapshot(offHost).TryGetEnemyJumpState(40, out var offJump), Is.True);
            Assert.That(reducedJump.phase, Is.EqualTo(fullJump.phase));
            Assert.That(offJump.phase, Is.EqualTo(fullJump.phase));
            Assert.That(reducedJump.sequence, Is.EqualTo(fullJump.sequence));
            Assert.That(offJump.sequence, Is.EqualTo(fullJump.sequence));
            AssertCameraShakeReturnsToIdentity(fullHost);
            AssertCameraShakeReturnsToIdentity(reducedHost);
            AssertCameraShakeReturnsToIdentity(offHost);

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
            Object.Destroy(fullCameraObject);
            Object.Destroy(reducedCameraObject);
            Object.Destroy(offCameraObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator HeavyEnemyJumpLanding_VisibilityAndPresentationEligibility_DoNotChangeSimulation()
        {
            var visibleCameraObject = new GameObject("M3B_VisibleCamera");
            var offscreenCameraObject = new GameObject("M3B_OffscreenCamera");
            var nonHeavyCameraObject = new GameObject("M3B_NonHeavyCamera");
            var visibleHost = CreateHeavyEnemyJumpLandingHost(visibleCameraObject.AddComponent<Camera>());
            var offscreenHost = CreateHeavyEnemyJumpLandingHost(offscreenCameraObject.AddComponent<Camera>());
            var nonHeavyHost = CreateHeavyEnemyJumpLandingHost(
                nonHeavyCameraObject.AddComponent<Camera>(),
                presentationId: "rocket_face");

            var visible = RunHeavyEnemyJumpLanding(visibleHost, CameraMotionLevel.Full);
            var offscreen = RunHeavyEnemyJumpLanding(
                offscreenHost,
                CameraMotionLevel.Full,
                beforeLanding: () =>
                    offscreenHost.BoardRoot.CameraTargetRoot.position += new Vector3(100f, 0f, 0f));
            var nonHeavy = RunHeavyEnemyJumpLanding(nonHeavyHost, CameraMotionLevel.Full);

            Assert.That(visibleHost.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(offscreenHost.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.Zero);
            Assert.That(offscreenHost.Presenter.OffscreenHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(nonHeavyHost.Presenter.ObservedHeavyEnemyJumpLandingCameraShakeCount, Is.Zero);
            Assert.That(nonHeavyHost.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.Zero);
            Assert.That(offscreen.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(nonHeavy.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(offscreen.LandingTick.DeterminismHash, Is.EqualTo(visible.LandingTick.DeterminismHash));
            Assert.That(nonHeavy.LandingTick.DeterminismHash, Is.EqualTo(visible.LandingTick.DeterminismHash));
            Assert.That(offscreen.LandingTick.FinalEntities, Is.EqualTo(visible.LandingTick.FinalEntities));
            Assert.That(nonHeavy.LandingTick.FinalEntities, Is.EqualTo(visible.LandingTick.FinalEntities));

            yield return DestroyHost(visibleHost);
            yield return DestroyHost(offscreenHost);
            yield return DestroyHost(nonHeavyHost);
            Object.Destroy(visibleCameraObject);
            Object.Destroy(offscreenCameraObject);
            Object.Destroy(nonHeavyCameraObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator HeavyEnemyJumpLanding_CrushedBoxAndLanded_SubmitsOneSemanticAndPreservesCrushResult()
        {
            var cameraObject = new GameObject("M3B_CrushedLandingCamera");
            var host = CreateHeavyEnemyJumpLandingHost(
                cameraObject.AddComponent<Camera>(),
                includeLandingBox: true);

            var sample = RunHeavyEnemyJumpLanding(host, CameraMotionLevel.Full);

            Assert.That(
                sample.LandingSignal.Outcome,
                Is.EqualTo(TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded));
            Assert.That(host.Presenter.ObservedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            Assert.That(CaptureAuthoritativeSnapshot(host).TryGetEntity(30, out _), Is.False);
            Assert.That(sample.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            AssertCameraShakeReturnsToIdentity(host);

            yield return DestroyHost(host);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator HeavyEnemyJumpLanding_DirectMatchesActualCinemachineAndReturnsToIdentity()
        {
            var directCameraObject = new GameObject("M3B_DirectCamera");
            var cinemachineOutputObject = new GameObject("M3B_CinemachineOutput");
            var directHost = CreateHeavyEnemyJumpLandingHost(directCameraObject.AddComponent<Camera>());
            var outputCamera = cinemachineOutputObject.AddComponent<Camera>();
            var brain = cinemachineOutputObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            var cinemachineHost = CreateHeavyEnemyJumpLandingHost(outputCamera);
            var cinemachineCamera = CreateProductionCinemachineCamera(
                cinemachineHost,
                "M3B_HeavyLandingCinemachineCamera");

            yield return null;
            brain.ManualUpdate();
            Assert.That(brain.isActiveAndEnabled, Is.True);
            Assert.That(cinemachineCamera.isActiveAndEnabled, Is.True);

            var direct = RunHeavyEnemyJumpLanding(directHost, CameraMotionLevel.Full);
            var cinemachine = RunHeavyEnemyJumpLanding(cinemachineHost, CameraMotionLevel.Full);
            brain.ManualUpdate();

            Assert.That(
                cinemachine.PeakPositionMagnitude,
                Is.EqualTo(direct.PeakPositionMagnitude).Within(0.000001f));
            Assert.That(
                cinemachine.PeakRotationMagnitude,
                Is.EqualTo(direct.PeakRotationMagnitude).Within(0.0001f));
            AssertFiniteCameraPose(direct.PeakOutputWorldPosition, direct.PeakOutputWorldRotation);
            AssertFiniteCameraPose(cinemachine.PeakOutputWorldPosition, cinemachine.PeakOutputWorldRotation);
            AssertCameraShakeReturnsToIdentity(directHost);
            AssertCameraShakeReturnsToIdentity(cinemachineHost);

            yield return DestroyHost(directHost);
            yield return DestroyHost(cinemachineHost);
            Object.Destroy(directCameraObject);
            Object.Destroy(cinemachineOutputObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator HeavyEnemyJumpLanding_PauseBeforeLanding_FreezesThenSubmitsAtVisibleCompletion()
        {
            var cameraObject = new GameObject("M3B_PauseBeforeLandingCamera");
            var host = CreateHeavyEnemyJumpLandingHost(cameraObject.AddComponent<Camera>());
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
            var preLandingTick = host.InputHost.RunSingleTick();
            Assert.That(preLandingTick, Is.Not.Null);
            Assert.That(preLandingTick.PresentationData.EnemyJumpSignals.Any(signal => signal.LandedThisTick), Is.False);

            InvokePauseService(host, "Pause");
            Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            Assert.That(host.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.Zero);
            InvokePauseService(host, "Resume");

            var landingTick = host.InputHost.RunSingleTick();
            Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.True);
            Assert.That(host.Presenter.AcceptedHeavyEnemyJumpLandingCameraShakeCount, Is.EqualTo(1));
            host.Presenter.UpdatePresentation(0.02f);
            Assert.That(host.GetComponent<GameplayCameraRig>().AdditiveLocalPosition.magnitude, Is.GreaterThan(0.000001f));
            host.Presenter.UpdatePresentation(0.5f);
            AssertCameraShakeReturnsToIdentity(host);

            yield return DestroyHost(host);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerDamageCameraShake_ProductionOutcome_FullReducedOffRemainAuthoritativelyEquivalent()
        {
            var fullHost = CreatePlayerDamageCameraShakeHost(playerHp: 3);
            var reducedHost = CreatePlayerDamageCameraShakeHost(playerHp: 3);
            var offHost = CreatePlayerDamageCameraShakeHost(playerHp: 3);
            var profile = LoadCampaignCameraShakeProfile();

            var full = RunPlayerImpact(fullHost, profile, CameraMotionLevel.Full, lethal: false);
            var reduced = RunPlayerImpact(reducedHost, profile, CameraMotionLevel.Reduced, lethal: false);
            var off = RunPlayerImpact(offHost, profile, CameraMotionLevel.Off, lethal: false);

            Assert.That(full.Result.PresentationData.PlayerDamageSignals, Has.Count.EqualTo(1));
            Assert.That(full.Result.PresentationData.PlayerDamageSignals[0].DamageAmount, Is.EqualTo(1));
            Assert.That(full.Result.PresentationData.PlayerDeathSignals, Is.Empty);
            Assert.That(full.HitReactionCount, Is.EqualTo(1));
            Assert.That(fullHost.Presenter.ObservedPlayerDamageImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(fullHost.Presenter.AcceptedPlayerDamageImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(fullHost.Presenter.ObservedPlayerLethalImpactCameraShakeCount, Is.Zero);
            Assert.That(full.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(full.PeakRotationMagnitude, Is.GreaterThan(0.001f));
            Assert.That(reduced.PeakPositionMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakPositionMagnitude, Is.LessThan(full.PeakPositionMagnitude));
            Assert.That(reduced.PeakRotationMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakRotationMagnitude, Is.LessThan(full.PeakRotationMagnitude));
            Assert.That(off.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(off.PeakRotationMagnitude, Is.Zero.Within(0.0001f));

            AssertAuthoritativeCameraMotionParity(full.Result, reduced.Result, off.Result);
            Assert.That(GetEntity(fullHost, 10).hp, Is.EqualTo(2));
            Assert.That(GetEntity(reducedHost, 10).hp, Is.EqualTo(2));
            Assert.That(GetEntity(offHost, 10).hp, Is.EqualTo(2));
            AssertCameraShakeReturnsToIdentity(fullHost);
            AssertCameraShakeReturnsToIdentity(reducedHost);
            AssertCameraShakeReturnsToIdentity(offHost);

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerLethalCameraShake_ProductionOutcome_FullReducedOffRemainAuthoritativelyEquivalent()
        {
            var fullHost = CreatePlayerDamageCameraShakeHost(playerHp: 1);
            var reducedHost = CreatePlayerDamageCameraShakeHost(playerHp: 1);
            var offHost = CreatePlayerDamageCameraShakeHost(playerHp: 1);
            var profile = LoadCampaignCameraShakeProfile();

            var full = RunPlayerImpact(fullHost, profile, CameraMotionLevel.Full, lethal: true);
            var reduced = RunPlayerImpact(reducedHost, profile, CameraMotionLevel.Reduced, lethal: true);
            var off = RunPlayerImpact(offHost, profile, CameraMotionLevel.Off, lethal: true);

            Assert.That(full.Result.PresentationData.PlayerDamageSignals, Has.Count.EqualTo(1));
            Assert.That(full.Result.PresentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
            Assert.That(full.HitReactionCount, Is.Zero);
            Assert.That(fullHost.Presenter.ObservedPlayerDamageImpactCameraShakeCount, Is.Zero);
            Assert.That(fullHost.Presenter.ObservedPlayerLethalImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(fullHost.Presenter.AcceptedPlayerLethalImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(full.PeakPositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(full.PeakRotationMagnitude, Is.GreaterThan(0.001f));
            Assert.That(reduced.PeakPositionMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakPositionMagnitude, Is.LessThan(full.PeakPositionMagnitude));
            Assert.That(reduced.PeakRotationMagnitude, Is.GreaterThan(0f));
            Assert.That(reduced.PeakRotationMagnitude, Is.LessThan(full.PeakRotationMagnitude));
            Assert.That(off.PeakPositionMagnitude, Is.Zero.Within(0.0000001f));
            Assert.That(off.PeakRotationMagnitude, Is.Zero.Within(0.0001f));

            AssertAuthoritativeCameraMotionParity(full.Result, reduced.Result, off.Result);
            Assert.That(CaptureAuthoritativeSnapshot(fullHost).TryGetEntity(10, out _), Is.False);
            Assert.That(CaptureAuthoritativeSnapshot(reducedHost).TryGetEntity(10, out _), Is.False);
            Assert.That(CaptureAuthoritativeSnapshot(offHost).TryGetEntity(10, out _), Is.False);
            AssertCameraShakeReturnsToIdentity(fullHost);
            AssertCameraShakeReturnsToIdentity(reducedHost);
            AssertCameraShakeReturnsToIdentity(offHost);

            yield return DestroyHost(fullHost);
            yield return DestroyHost(reducedHost);
            yield return DestroyHost(offHost);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerDamageCameraShake_RepeatedAcceptedDamage_UsesCameraCooldownWithoutChangingGameplayCadence()
        {
            var host = CreatePlayerDamageCameraShakeHost(playerHp: 10);
            host.Presenter.ConfigureGameplayCameraShakeProfile(LoadCampaignCameraShakeProfile());
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
            var acceptedDamageTicks = new List<int>();

            for (var index = 0; index < 9; index++)
            {
                var result = host.InputHost.RunSingleTick();
                Assert.That(result, Is.Not.Null);
                if (result.PresentationData.PlayerDamageSignals.Count == 1)
                {
                    acceptedDamageTicks.Add(result.TickIndex);
                }

                host.Presenter.UpdatePresentation(host.TimingProfile.SimulationTickIntervalSeconds);
            }

            Assert.That(acceptedDamageTicks, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));
            Assert.That(GetEntity(host, 10).hp, Is.EqualTo(5));
            Assert.That(host.Presenter.ObservedPlayerDamageImpactCameraShakeCount, Is.EqualTo(5));
            Assert.That(
                host.Presenter.AcceptedPlayerDamageImpactCameraShakeCount,
                Is.EqualTo(2),
                "The first impact and the post-cooldown tick-nine impact should be accepted.");

            host.Presenter.UpdatePresentation(0.5f);
            AssertCameraShakeReturnsToIdentity(host);
            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerLethalCameraShake_PromotesSameTickDamageAndResetsAtTerminalIrisCloseHandoff()
        {
            var host = CreatePlayerDamageCameraShakeHost(playerHp: 1);
            var profile = LoadCampaignCameraShakeProfile();
            host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();

            var result = host.InputHost.RunSingleTick();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.PlayerDamageSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
            Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));
            Assert.That(driver.HitSignalCount, Is.Zero);
            Assert.That(host.Presenter.ObservedPlayerDamageImpactCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.AcceptedPlayerDamageImpactCameraShakeCount, Is.Zero);
            Assert.That(host.Presenter.ObservedPlayerLethalImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(host.Presenter.AcceptedPlayerLethalImpactCameraShakeCount, Is.EqualTo(1));

            host.Presenter.UpdatePresentation(0.02f);
            var rig = host.GetComponent<GameplayCameraRig>();
            Assert.That(rig.AdditiveLocalPosition.magnitude, Is.GreaterThan(0.000001f));
            Assert.That(MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation), Is.GreaterThan(0.001f));

            host.Presenter.ApplyStageTerminalPresentation(
                GameplayStageTerminalPresentationReason.PlayerDeathRetry,
                result);
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.EqualTo(1));
            Assert.That(rig.AdditiveLocalPosition.magnitude, Is.GreaterThan(0.000001f));

            host.Presenter.CompleteStageTerminalCameraHandoff();
            AssertCameraShakeReturnsToIdentity(host);
            host.Presenter.UpdatePresentation(0.5f);
            AssertCameraShakeReturnsToIdentity(host);
            yield return DestroyHost(host);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerLethalCameraShake_DamageCooldownCannotSuppressLethalAndDirectMatchesActualCinemachine()
        {
            var directCameraObject = new GameObject("M3A_DirectLethalCamera");
            var cinemachineOutputObject = new GameObject("M3A_CinemachineLethalOutput");
            var directCamera = directCameraObject.AddComponent<Camera>();
            var cinemachineOutput = cinemachineOutputObject.AddComponent<Camera>();
            var brain = cinemachineOutputObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            var directHost = CreatePlayerDamageCameraShakeHost(playerHp: 2, directCamera);
            var cinemachineHost = CreatePlayerDamageCameraShakeHost(playerHp: 2, cinemachineOutput);
            var virtualCamera = CreateProductionCinemachineCamera(cinemachineHost, "M3A_LethalCinemachineCamera");
            var profile = LoadCampaignCameraShakeProfile();

            yield return null;
            brain.ManualUpdate();
            Assert.That(brain.isActiveAndEnabled, Is.True);
            Assert.That(virtualCamera.isActiveAndEnabled, Is.True);

            var directDamage = RunPlayerImpact(directHost, profile, CameraMotionLevel.Full, lethal: false, settle: false);
            var cinemachineDamage = RunPlayerImpact(cinemachineHost, profile, CameraMotionLevel.Full, lethal: false, settle: false);
            Assert.That(directDamage.Result.PresentationData.PlayerDeathSignals, Is.Empty);
            Assert.That(cinemachineDamage.Result.PresentationData.PlayerDeathSignals, Is.Empty);

            var directCooldownTick = directHost.InputHost.RunSingleTick();
            var cinemachineCooldownTick = cinemachineHost.InputHost.RunSingleTick();
            Assert.That(directCooldownTick.PresentationData.PlayerDamageSignals, Is.Empty);
            Assert.That(cinemachineCooldownTick.PresentationData.PlayerDamageSignals, Is.Empty);
            directHost.Presenter.UpdatePresentation(directHost.TimingProfile.SimulationTickIntervalSeconds);
            cinemachineHost.Presenter.UpdatePresentation(cinemachineHost.TimingProfile.SimulationTickIntervalSeconds);

            var directLethal = RunPlayerImpact(
                directHost,
                profile,
                CameraMotionLevel.Full,
                lethal: true,
                configure: false);
            var cinemachineLethal = RunPlayerImpact(
                cinemachineHost,
                profile,
                CameraMotionLevel.Full,
                lethal: true,
                configure: false);
            Assert.That(directLethal.Result.PresentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
            Assert.That(cinemachineLethal.Result.PresentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
            Assert.That(directHost.Presenter.AcceptedPlayerDamageImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(directHost.Presenter.AcceptedPlayerLethalImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(cinemachineHost.Presenter.AcceptedPlayerDamageImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(cinemachineHost.Presenter.AcceptedPlayerLethalImpactCameraShakeCount, Is.EqualTo(1));
            Assert.That(directLethal.PeakPositionMagnitude, Is.EqualTo(cinemachineLethal.PeakPositionMagnitude).Within(0.000001f));
            Assert.That(directLethal.PeakRotationMagnitude, Is.EqualTo(cinemachineLethal.PeakRotationMagnitude).Within(0.0001f));
            AssertFiniteCameraPose(directLethal.PeakOutputWorldPosition, directLethal.PeakOutputWorldRotation);
            AssertFiniteCameraPose(cinemachineLethal.PeakOutputWorldPosition, cinemachineLethal.PeakOutputWorldRotation);

            yield return DestroyHost(directHost);
            yield return DestroyHost(cinemachineHost);
            Object.Destroy(directCameraObject);
            Object.Destroy(cinemachineOutputObject);
            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PlayerDamageCameraShake_PauseCancelsActiveImpactAndDoesNotReplayOnResume()
        {
            var host = CreatePlayerDamageCameraShakeHost(playerHp: 3);
            host.Presenter.ConfigureGameplayCameraShakeProfile(LoadCampaignCameraShakeProfile());
            host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
            Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
            host.Presenter.UpdatePresentation(0.02f);
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.EqualTo(1));

            InvokePauseService(host, "Pause");
            AssertCameraShakeReturnsToIdentity(host);
            InvokePauseService(host, "Resume");
            host.Presenter.UpdatePresentation(0.2f);
            AssertCameraShakeReturnsToIdentity(host);
            Assert.That(host.Presenter.AcceptedPlayerDamageImpactCameraShakeCount, Is.EqualTo(1));
            yield return DestroyHost(host);
        }

        private static GameplaySceneHost CreatePlayerDamageCameraShakeHost(int playerHp, Camera viewCamera = null)
        {
            var host = CreateHost(
                new[]
                {
                    CreateDamageCameraUnit(10, teamId: 1, unitRole: UnitRole.Player, hp: playerHp),
                    CreateDamageCameraUnit(40, teamId: 2, unitRole: UnitRole.Enemy, hp: 3),
                },
                staticEntityLogics: new IEntityLogic[] { new RepeatingPassiveContactAttackLogic(40, 10) },
                playerControlTiming: new PlayerControlTimingSettings
                {
                    DamageCooldownSeconds = 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                },
                viewCamera: viewCamera);
            DisablePlayerDamageCameraTerminalOutcomes(host);

            return host;
        }

        private static GameplaySceneHost CreateHeavyEnemyJumpLandingHost(
            Camera viewCamera,
            string presentationId = "astreton",
            bool includeLandingBox = false)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var enemy = CreateAirborneEnemy(entityId: 40, position: sourceCell);
            enemy.boardPresence = EntityBoardPresence.Occupying;
            var entities = new List<EntityState>
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, -2, 0)),
                enemy,
            };
            if (includeLandingBox)
            {
                entities.Add(CreateBox(
                    entityId: 30,
                    position: landingCell,
                    capabilities: BoxCapabilities.JumpCrushable));
            }

            var prefabPath = presentationId == "astreton"
                ? AstretonPresentationPrefabPath
                : NonHeavyPresentationPrefabPath;
            var enemyViewPrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(enemyViewPrefab, Is.Not.Null, prefabPath);
            var host = CreateHost(
                entities.ToArray(),
                viewCamera: viewCamera,
                defaultEnemyAiProfile: LoadJumpChaserProfile(),
                enemyViewPrefabOverride: enemyViewPrefab,
                gameplayCameraShakeProfile: LoadCampaignCameraShakeProfile());
            SeedAirborneJumpState(host, 40, sourceCell, landingCell, landingTick: 2);
            InvokeWorldWriteContextMethod(
                host.WorldState,
                "SetBoardPresence",
                40,
                EntityBoardPresence.Detached);
            Assert.That(host.ViewRegistry.TryGetView(40, out var enemyView), Is.True);
            Assert.That(enemyView, Is.Not.Null);
            if (presentationId == "astreton")
            {
                Assert.That(
                    enemyView.GetComponent<EnemyJumpMotionPresentationAuthoring>()?.JumpLandingCameraFeedback,
                    Is.EqualTo(EnemyJumpLandingCameraFeedbackKind.Heavy));
            }

            return host;
        }

        private static HeavyEnemyLandingSample RunHeavyEnemyJumpLanding(
            GameplaySceneHost host,
            CameraMotionLevel motionLevel,
            System.Action beforeLanding = null)
        {
            host.Presenter.SetCameraMotionLevel(motionLevel);
            var rig = host.GetComponent<GameplayCameraRig>();
            AssertCameraShakePoseIdentity(rig);

            var preLandingTick = host.InputHost.RunSingleTick();
            Assert.That(preLandingTick, Is.Not.Null);
            Assert.That(
                preLandingTick.PresentationData.EnemyJumpSignals.Any(signal => signal.LandedThisTick),
                Is.False,
                "Windup/airborne presentation must not start a camera impulse.");
            Assert.That(host.Presenter.ActiveGameplayCameraImpulseCount, Is.Zero);
            AssertCameraShakePoseIdentity(rig);
            host.Presenter.UpdatePresentation(host.TimingProfile.SimulationTickIntervalSeconds);

            beforeLanding?.Invoke();
            var landingTick = host.InputHost.RunSingleTick();
            Assert.That(landingTick, Is.Not.Null);
            var landingSignal = landingTick.PresentationData.EnemyJumpSignals.Single(signal => signal.LandedThisTick);
            host.Presenter.UpdatePresentation(0.02f);
            var sample = new HeavyEnemyLandingSample(
                landingTick,
                landingSignal,
                rig.AdditiveLocalPosition.magnitude,
                MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation),
                host.ViewCamera != null ? host.ViewCamera.transform.position : Vector3.zero,
                host.ViewCamera != null ? host.ViewCamera.transform.rotation : Quaternion.identity);
            host.Presenter.UpdatePresentation(0.5f);
            return sample;
        }

        private readonly struct HeavyEnemyLandingSample
        {
            public HeavyEnemyLandingSample(
                TickResult landingTick,
                TickEnemyJumpPresentationSignal landingSignal,
                float peakPositionMagnitude,
                float peakRotationMagnitude,
                Vector3 peakOutputWorldPosition,
                Quaternion peakOutputWorldRotation)
            {
                LandingTick = landingTick;
                LandingSignal = landingSignal;
                PeakPositionMagnitude = peakPositionMagnitude;
                PeakRotationMagnitude = peakRotationMagnitude;
                PeakOutputWorldPosition = peakOutputWorldPosition;
                PeakOutputWorldRotation = peakOutputWorldRotation;
            }

            public TickResult LandingTick { get; }
            public TickEnemyJumpPresentationSignal LandingSignal { get; }
            public float PeakPositionMagnitude { get; }
            public float PeakRotationMagnitude { get; }
            public Vector3 PeakOutputWorldPosition { get; }
            public Quaternion PeakOutputWorldRotation { get; }
        }

        private static void DisablePlayerDamageCameraTerminalOutcomes(GameplaySceneHost host)
        {
            var uiAccess = typeof(GameplaySceneHost)
                .GetProperty("UiAccess", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                ?.GetValue(host);
            var presentationFeed = uiAccess?.GetType()
                .GetProperty("PresentationFeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                ?.GetValue(uiAccess);
            var disableMethod = presentationFeed?.GetType().GetMethod(
                "DisableTerminalOutcomes",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(disableMethod, Is.Not.Null);
            disableMethod.Invoke(presentationFeed, null);
        }

        private static EntityState CreateDamageCameraUnit(int entityId, int teamId, UnitRole unitRole, int hp)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = teamId == 1 ? Direction.Right : Direction.Left,
            };
        }

        private static PlayerImpactCameraSample RunPlayerImpact(
            GameplaySceneHost host,
            GameplayCameraShakeProfile profile,
            CameraMotionLevel motionLevel,
            bool lethal,
            bool settle = true,
            bool configure = true)
        {
            if (configure)
            {
                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(motionLevel);
            }
            Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
            var driver = playerView.GetComponent<PlayerAnimatorDriver>();
            var result = host.InputHost.RunSingleTick();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PresentationData.PlayerDeathSignals.Count > 0, Is.EqualTo(lethal));

            host.Presenter.UpdatePresentation(0.02f);
            var rig = host.GetComponent<GameplayCameraRig>();
            var sample = new PlayerImpactCameraSample(
                result,
                rig.AdditiveLocalPosition.magnitude,
                MeasureSmallQuaternionAngleDegrees(rig.AdditiveLocalRotation),
                driver.HitSignalCount,
                host.ViewCamera != null ? host.ViewCamera.transform.position : Vector3.zero,
                host.ViewCamera != null ? host.ViewCamera.transform.rotation : Quaternion.identity);
            if (settle)
            {
                host.Presenter.UpdatePresentation(0.5f);
            }

            return sample;
        }

        private static void AssertAuthoritativeCameraMotionParity(
            TickResult full,
            TickResult reduced,
            TickResult off)
        {
            Assert.That(reduced.DeterminismHash, Is.EqualTo(full.DeterminismHash));
            Assert.That(off.DeterminismHash, Is.EqualTo(full.DeterminismHash));
            Assert.That(reduced.FinalEntities, Is.EqualTo(full.FinalEntities));
            Assert.That(off.FinalEntities, Is.EqualTo(full.FinalEntities));
            Assert.That(reduced.EventLog, Is.EqualTo(full.EventLog));
            Assert.That(off.EventLog, Is.EqualTo(full.EventLog));
        }

        private readonly struct PlayerImpactCameraSample
        {
            public PlayerImpactCameraSample(
                TickResult result,
                float peakPositionMagnitude,
                float peakRotationMagnitude,
                int hitReactionCount,
                Vector3 peakOutputWorldPosition,
                Quaternion peakOutputWorldRotation)
            {
                Result = result;
                PeakPositionMagnitude = peakPositionMagnitude;
                PeakRotationMagnitude = peakRotationMagnitude;
                HitReactionCount = hitReactionCount;
                PeakOutputWorldPosition = peakOutputWorldPosition;
                PeakOutputWorldRotation = peakOutputWorldRotation;
            }

            public TickResult Result { get; }
            public float PeakPositionMagnitude { get; }
            public float PeakRotationMagnitude { get; }
            public int HitReactionCount { get; }
            public Vector3 PeakOutputWorldPosition { get; }
            public Quaternion PeakOutputWorldRotation { get; }
        }

        private sealed class RepeatingPassiveContactAttackLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _controlledEntityId;
            private readonly int _targetEntityId;

            public RepeatingPassiveContactAttackLogic(int controlledEntityId, int targetEntityId)
            {
                _controlledEntityId = controlledEntityId;
                _targetEntityId = targetEntityId;
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (!snapshot.TryGetEntity(_controlledEntityId, out var source) ||
                    !snapshot.TryGetEntity(_targetEntityId, out var target) ||
                    source.position != target.position)
                {
                    return;
                }

                buffer.Add(new RawAttackIntent(
                    _controlledEntityId,
                    priority: 5,
                    targetId: _targetEntityId,
                    sourceKind: AttackSourceKind.PassiveContact,
                    localSequence: 1));
            }
        }
    }
}
