using System;
using System.Collections.Generic;
using Game.Feature.Flow.Audio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class BgmRequestRouterCoreTests
    {
        private readonly List<BgmProfile> ownedProfiles = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = ownedProfiles.Count - 1; i >= 0; i--)
            {
                if (ownedProfiles[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedProfiles[i]);
                }
            }

            ownedProfiles.Clear();
        }

        [Test]
        [Category("Core")]
        public void PlaybackSuppression_RestoresLatestSelectedRequest()
        {
            var sceneProfile = CreateProfile("SuppressedSceneProfile");
            var stageProfile = CreateProfile("SuppressedStageProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var sceneLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));

            var suppression = router.BeginPlaybackSuppression();
            var stageLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                stageProfile));

            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(1));
            Assert.That(coordinator.StopCount, Is.EqualTo(1));
            Assert.That(router.ActiveRequest.HasValue, Is.True);
            Assert.That(
                router.ActiveRequest.Value.SourceKind,
                Is.EqualTo(BgmRequestSourceKind.StageGameplay));

            suppression.Dispose();
            suppression.Dispose();

            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(2));
            Assert.That(coordinator.RequestedProfiles[1], Is.SameAs(stageProfile));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(stageProfile));

            stageLease.Dispose();
            sceneLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void PlaybackSuppression_ReleaseWithoutRestore_WaitsForNextSubmit()
        {
            var sceneProfile = CreateProfile("TransitionSceneProfile");
            var stageProfile = CreateProfile("TransitionStageProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var sceneLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));
            var suppression = router.BeginPlaybackSuppression();
            var stageRequest = BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                stageProfile);
            var firstStageLease = router.Acquire(stageRequest);

            suppression.ReleaseWithoutRestore();

            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(1));
            Assert.That(coordinator.GetCurrentProfile(), Is.Null);

            var secondStageLease = router.Acquire(stageRequest);

            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(2));
            Assert.That(coordinator.RequestedProfiles[1], Is.SameAs(stageProfile));

            firstStageLease.Dispose();
            secondStageLease.Dispose();
            sceneLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void StageProfileLease_Dispose_RestoresSceneProfile()
        {
            var sceneProfile = CreateProfile("SceneProfile");
            var stageProfile = CreateProfile("StageProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var sceneLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));
            var stageLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                stageProfile));

            stageLease.Dispose();

            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(sceneProfile));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { sceneProfile, stageProfile, sceneProfile }));
            Assert.That(coordinator.StopCount, Is.Zero);
            sceneLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void StageStopLease_Dispose_RestoresSceneProfile()
        {
            var sceneProfile = CreateProfile("SceneProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var sceneLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));
            var stageLease = router.Acquire(BgmFlowRequest.StopRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay));

            stageLease.Dispose();

            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(sceneProfile));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { sceneProfile, sceneProfile }));
            Assert.That(coordinator.StopCount, Is.EqualTo(1));
            sceneLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void SameKindReplacement_LateOlderDispose_PreservesNewClaim()
        {
            var firstProfile = CreateProfile("FirstProfile");
            var secondProfile = CreateProfile("SecondProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var firstLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                firstProfile));
            var secondLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                secondProfile));

            firstLease.Dispose();

            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(secondProfile));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { firstProfile, secondProfile }));

            secondLease.Dispose();
            Assert.That(router.ActiveRequest.HasValue, Is.False);
            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(2), "The replaced first claim must not revive.");
        }

        [Test]
        [Category("Core")]
        public void LeaseDispose_IsIdempotent_AndLastReleaseDoesNotStopPlayback()
        {
            var profile = CreateProfile("OnlyProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var lease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                profile));

            lease.Dispose();
            lease.Dispose();

            Assert.That(router.ActiveRequest.HasValue, Is.False);
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { profile }));
            Assert.That(coordinator.StopCount, Is.Zero);
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profile));
        }

        [Test]
        [Category("Core")]
        public void PlaybackSuppression_WinningRelease_UpdatesSelectionWithoutPlayback()
        {
            var sceneProfile = CreateProfile("SceneProfile");
            var stageProfile = CreateProfile("StageProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var sceneLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));
            var stageLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                stageProfile));
            var suppression = router.BeginPlaybackSuppression();

            stageLease.Dispose();

            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(sceneProfile));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { sceneProfile, stageProfile }));
            Assert.That(coordinator.StopCount, Is.EqualTo(1));

            suppression.Dispose();

            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { sceneProfile, stageProfile, sceneProfile }));
            sceneLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void ReleaseWithoutRestore_DefersReleasePlaybackUntilNextAcquisition()
        {
            var sceneProfile = CreateProfile("SceneProfile");
            var stageProfile = CreateProfile("StageProfile");
            var nextProfile = CreateProfile("NextProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var sceneLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));
            var stageLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                stageProfile));
            var suppression = router.BeginPlaybackSuppression();

            suppression.ReleaseWithoutRestore();
            stageLease.Dispose();

            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(sceneProfile));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { sceneProfile, stageProfile }));

            var nextLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                nextProfile));

            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { sceneProfile, stageProfile, nextProfile }));
            nextLease.Dispose();
            sceneLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void StopClaim_UsesSameStaleLeaseIdentityRule()
        {
            var profile = CreateProfile("ReplacementProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var stopLease = router.Acquire(BgmFlowRequest.StopRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay));
            var profileLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                profile));

            stopLease.Dispose();

            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(profile));
            Assert.That(coordinator.StopCount, Is.EqualTo(1));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { profile }));
            profileLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void Acquire_CoordinatorFailure_RollsBackToPreviousClaim()
        {
            var firstProfile = CreateProfile("FirstProfile");
            var rejectedProfile = CreateProfile("RejectedProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var firstLease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                firstProfile));
            coordinator.RequestException = new InvalidOperationException("injected request failure");

            var exception = Assert.Throws<InvalidOperationException>(() => router.Acquire(
                BgmFlowRequest.ProfileRequest(
                    BgmRequestSourceKind.SceneDefault,
                    BgmRequestPriority.SceneDefault,
                    rejectedProfile)));

            Assert.That(exception.Message, Is.EqualTo("injected request failure"));
            Assert.That(router.ActiveRequest.Value.Profile, Is.SameAs(firstProfile));
            Assert.That(coordinator.RequestedProfiles, Is.EqualTo(new[] { firstProfile }));
            coordinator.RequestException = null;
            firstLease.Dispose();
        }

        [Test]
        [Category("Core")]
        public void Acquire_TokenRollover_SkipsZeroAndLeaseCanReleaseClaim()
        {
            var profile = CreateProfile("RolloverProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var tokenField = typeof(BgmRequestRouter).GetField(
                "nextRequestToken",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(tokenField, Is.Not.Null);
            tokenField.SetValue(router, ulong.MaxValue);

            var lease = router.Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                profile));
            lease.Dispose();

            Assert.That(router.ActiveRequest.HasValue, Is.False);
            Assert.That(coordinator.StopCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlaybackSuppression_RejectsConcurrentLease()
        {
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var suppression = router.BeginPlaybackSuppression();

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => router.BeginPlaybackSuppression());
                Assert.That(
                    exception.Message,
                    Is.EqualTo(
                        "BgmRequestRouter supports only one active playback suppression lease."));
            }
            finally
            {
                suppression.Dispose();
            }
        }

        [Test]
        [Category("Core")]
        public void PlaybackSuppression_StopFailure_DoesNotLeaveActiveLease()
        {
            var coordinator = new RecordingBgmFlowCoordinator
            {
                StopException = new InvalidOperationException("injected stop failure"),
            };
            var router = new BgmRequestRouter(coordinator);

            var exception = Assert.Throws<InvalidOperationException>(
                () => router.BeginPlaybackSuppression());
            Assert.That(exception.Message, Is.EqualTo("injected stop failure"));

            coordinator.StopException = null;
            var suppression = router.BeginPlaybackSuppression();
            suppression.Dispose();

            Assert.That(coordinator.StopCount, Is.EqualTo(2));
        }

        private BgmProfile CreateProfile(string name)
        {
            var profile = ScriptableObject.CreateInstance<BgmProfile>();
            profile.name = name;
            ownedProfiles.Add(profile);
            return profile;
        }

        private sealed class RecordingBgmFlowCoordinator : IBgmFlowCoordinator
        {
            private BgmProfile currentProfile;

            public List<BgmProfile> RequestedProfiles { get; } = new();

            public int StopCount { get; private set; }

            public Exception StopException { get; set; }

            public Exception RequestException { get; set; }

            public void RequestSceneDefault(BgmProfile profile)
            {
                if (RequestException != null)
                {
                    throw RequestException;
                }

                RequestedProfiles.Add(profile);
                currentProfile = profile;
            }

            public void StopCurrent()
            {
                StopCount++;
                if (StopException != null)
                {
                    throw StopException;
                }

                currentProfile = null;
            }

            public BgmProfile GetCurrentProfile()
            {
                return currentProfile;
            }
        }
    }
}
