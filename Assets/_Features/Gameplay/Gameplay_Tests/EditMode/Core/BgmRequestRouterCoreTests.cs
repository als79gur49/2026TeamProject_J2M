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
            router.Submit(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));

            var suppression = router.BeginPlaybackSuppression();
            router.Submit(BgmFlowRequest.ProfileRequest(
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
        }

        [Test]
        [Category("Core")]
        public void PlaybackSuppression_ReleaseWithoutRestore_WaitsForNextSubmit()
        {
            var sceneProfile = CreateProfile("TransitionSceneProfile");
            var stageProfile = CreateProfile("TransitionStageProfile");
            var coordinator = new RecordingBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            router.Submit(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                sceneProfile));
            var suppression = router.BeginPlaybackSuppression();
            var stageRequest = BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.StageGameplay,
                BgmRequestPriority.StageGameplay,
                stageProfile);
            router.Submit(stageRequest);

            suppression.ReleaseWithoutRestore();

            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(1));
            Assert.That(coordinator.GetCurrentProfile(), Is.Null);

            router.Submit(stageRequest);

            Assert.That(coordinator.RequestedProfiles, Has.Count.EqualTo(2));
            Assert.That(coordinator.RequestedProfiles[1], Is.SameAs(stageProfile));
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

            public void RequestSceneDefault(BgmProfile profile)
            {
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
