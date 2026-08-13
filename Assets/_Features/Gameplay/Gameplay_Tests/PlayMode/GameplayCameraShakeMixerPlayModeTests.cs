using System.Collections;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class GameplayCameraShakeMixerPlayModeTests
    {
        [UnityTest]
        [Category("Full")]
        public IEnumerator SyntheticMediumImpulse_DirectCamera_ProducesFinitePoseThenReturnsToIdentity()
        {
            var root = new GameObject("M1SyntheticDirectCameraRoot");
            var cameraObject = new GameObject("M1SyntheticDirectCamera");
            var profile = CreateProfile();
            try
            {
                var boardRoot = root.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var rig = root.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(
                    cameraObject.AddComponent<Camera>(),
                    boardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));
                var mixer = CreateMixer(profile);
                Assert.That(mixer.Submit(CreateMediumRequest()), Is.True);

                mixer.Advance(0.1f);
                Apply(mixer, rig);
                Assert.That(rig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));
                Assert.That(IsFinite(rig.AdditiveLocalPosition), Is.True);
                Assert.That(IsFinite(rig.AdditiveLocalRotation), Is.True);
                Assert.That(boardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(rig.AdditiveLocalPosition));

                mixer.Advance(0.3f);
                Apply(mixer, rig);
                AssertIdentity(rig, boardRoot);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(cameraObject);
                Object.Destroy(profile);
            }

            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator SyntheticMediumImpulse_DirectAndActualCinemachineBrain_ShareContributionAndResetWithoutDrift()
        {
            var directRootObject = new GameObject("M1SyntheticParityDirectRoot");
            var cinemachineRootObject = new GameObject("M1SyntheticParityCinemachineRoot");
            var directCameraObject = new GameObject("M1SyntheticParityDirectCamera");
            var outputCameraObject = new GameObject("M1SyntheticParityOutputCamera");
            var cinemachineCameraObject = new GameObject("M1SyntheticParityCinemachineCamera");
            var profile = CreateProfile();
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

                var directMixer = CreateMixer(profile);
                var cinemachineMixer = CreateMixer(profile);
                var request = CreateMediumRequest();
                directMixer.Submit(request);
                cinemachineMixer.Submit(request);
                directMixer.Advance(0.1f);
                cinemachineMixer.Advance(0.1f);
                Apply(directMixer, directRig);
                Apply(cinemachineMixer, cinemachineRig);
                brain.ManualUpdate();

                Assert.That(
                    Vector3.Distance(directRig.AdditiveLocalPosition, cinemachineRig.AdditiveLocalPosition),
                    Is.LessThan(0.00001f));
                Assert.That(
                    Quaternion.Angle(directRig.AdditiveLocalRotation, cinemachineRig.AdditiveLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(cinemachineCamera.transform.parent, Is.EqualTo(cinemachineBoardRoot.CameraEffectsRoot));
                Assert.That(IsFinite(outputCamera.transform.position), Is.True);
                Assert.That(IsFinite(outputCamera.transform.rotation), Is.True);

                directMixer.Advance(0.3f);
                cinemachineMixer.Advance(0.3f);
                Apply(directMixer, directRig);
                Apply(cinemachineMixer, cinemachineRig);
                brain.ManualUpdate();
                AssertIdentity(directRig, directBoardRoot);
                AssertIdentity(cinemachineRig, cinemachineBoardRoot);
            }
            finally
            {
                Object.Destroy(directRootObject);
                Object.Destroy(cinemachineRootObject);
                Object.Destroy(directCameraObject);
                Object.Destroy(outputCameraObject);
                Object.Destroy(cinemachineCameraObject);
                Object.Destroy(profile);
            }

            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TopologyOverlapPauseHardCleanupAndOff_KeepCameraRootLifecycleIdentity()
        {
            var root = new GameObject("M1SyntheticLifecycleRoot");
            var cameraObject = new GameObject("M1SyntheticLifecycleCamera");
            var profile = CreateProfile();
            try
            {
                var boardRoot = root.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var rig = root.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(
                    cameraObject.AddComponent<Camera>(),
                    boardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));
                var mixer = CreateMixer(profile);
                var topology = TopologyCameraShakeContributionAdapter.Create(
                    new TopologyTransitionCameraShakeResult(
                        new Vector3(0.012f, 0.008f, -0.018f),
                        Quaternion.Euler(0.45f, 0.28f, -0.12f)),
                    true);
                mixer.SetTopologyContribution(topology);

                Assert.That(mixer.Submit(CreateLightRequest()), Is.False);
                Assert.That(mixer.Submit(CreateHeavyRequest()), Is.True);
                mixer.Advance(0.1f);
                Apply(mixer, rig);
                Assert.That(rig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));
                Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(1));

                mixer.SetPaused(true);
                Apply(mixer, rig);
                AssertIdentity(rig, boardRoot);
                mixer.SetPaused(false);
                mixer.Advance(0.01f);
                Apply(mixer, rig);
                AssertIdentity(rig, boardRoot);

                mixer.SetTopologyContribution(topology);
                mixer.SetMotionLevel(CameraMotionLevel.Off);
                Apply(mixer, rig);
                AssertIdentity(rig, boardRoot);

                mixer.SetMotionLevel(CameraMotionLevel.Full);
                Apply(mixer, rig);
                Assert.That(rig.AdditiveLocalPosition.sqrMagnitude, Is.GreaterThan(0.000001f));
                mixer.HardReset();
                Apply(mixer, rig);
                AssertIdentity(rig, boardRoot);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(cameraObject);
                Object.Destroy(profile);
            }

            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator UnshakenVisibilityProjection_IsStableAcrossActiveAdditiveCameraShake()
        {
            var root = new GameObject("M3BUnshakenVisibilityRoot");
            var cameraObject = new GameObject("M3BUnshakenVisibilityCamera");
            try
            {
                var boardRoot = root.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var rig = root.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(
                    cameraObject.AddComponent<Camera>(),
                    boardRoot.CameraTargetRoot,
                    new Bounds(Vector3.zero, Vector3.one));

                Assert.That(
                    rig.TryProjectUnshakenWorldPoint(boardRoot.CameraTargetRoot.position, out var beforeShake),
                    Is.True);
                rig.ApplyAdditivePose(
                    new Vector3(0.25f, -0.2f, 0.15f),
                    Quaternion.Euler(8f, -6f, 4f));
                Assert.That(
                    rig.TryProjectUnshakenWorldPoint(boardRoot.CameraTargetRoot.position, out var duringShake),
                    Is.True);

                Assert.That(Vector3.Distance(duringShake, beforeShake), Is.LessThan(0.000001f));
                Assert.That(duringShake.x, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(duringShake.y, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(duringShake.z, Is.GreaterThan(0f));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }

        private static GameplayCameraShakeMixer CreateMixer(GameplayCameraShakeProfile profile)
        {
            var mixer = new GameplayCameraShakeMixer();
            mixer.ConfigureProfile(profile);
            return mixer;
        }

        private static GameplayCameraShakeProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<GameplayCameraShakeProfile>();
            profile.SetEntriesForTests(new[]
            {
                CreateEntry(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light),
                CreateEntry(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium),
                CreateEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Medium,
                    CameraShakeVariant.FlipHostileStay),
                CreateEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Medium,
                    CameraShakeVariant.FlipHostileDestroySelf),
                CreateEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Heavy,
                    CameraShakeVariant.FlipHostileFollowThrough),
                CreateEntry(CameraShakeSemantic.PlayerDamageImpact, CameraShakePriority.Light),
                CreateEntry(CameraShakeSemantic.PlayerLethalImpact, CameraShakePriority.Heavy),
                CreateEntry(CameraShakeSemantic.HeavyEnemyJumpLanding, CameraShakePriority.Medium),
            });
            return profile;
        }

        private static CameraShakeProfileEntry CreateEntry(
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

        private static CameraShakeImpulseRequest CreateLightRequest()
        {
            return new CameraShakeImpulseRequest(
                1,
                CameraShakeSemantic.PushSlideLaunch,
                1,
                1,
                CameraShakePriority.Light);
        }

        private static CameraShakeImpulseRequest CreateMediumRequest()
        {
            return new CameraShakeImpulseRequest(
                1,
                CameraShakeSemantic.FlipFloorLanding,
                2,
                2,
                CameraShakePriority.Medium);
        }

        private static CameraShakeImpulseRequest CreateHeavyRequest()
        {
            return new CameraShakeImpulseRequest(
                1,
                CameraShakeSemantic.PlayerLethalImpact,
                3,
                3,
                CameraShakePriority.Heavy);
        }

        private static void Apply(GameplayCameraShakeMixer mixer, GameplayCameraRig rig)
        {
            rig.ApplyAdditivePose(
                mixer.CurrentResult.LocalPosition,
                mixer.CurrentResult.LocalRotation);
        }

        private static void AssertIdentity(GameplayCameraRig rig, GameplayBoardRoot boardRoot)
        {
            Assert.That(rig.AdditiveLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(rig.AdditiveLocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(boardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(boardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                   !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }
    }
}
