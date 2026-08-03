using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageTransitionProfileResolverTests
    {
        [Test]
        public void Resolve_ExplicitKind_WinsOverSource()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry",
                StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart));

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.Kind, Is.EqualTo(StageTransitionKind.LevelFailedRestart));
            Assert.That(profile.OverlayKind, Is.EqualTo(TransitionOverlayKind.Restart));
        }

        [Test]
        public void Resolve_SourceMapsDeathRetryToChanceLost()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry");

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.Kind, Is.EqualTo(StageTransitionKind.DeathRetryChanceLost));
            Assert.That(profile.OverlayKind, Is.EqualTo(TransitionOverlayKind.ChanceLost));
        }

        [Test]
        public void Resolve_DeathRetryProfile_HasPreOverlayDelay()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry");

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.PreOverlayDelaySeconds, Is.EqualTo(1.0f));
            Assert.That(profile.MinimumVisibleSeconds, Is.EqualTo(1.75f));
            Assert.That(profile.HoldSceneActivationUntilMinimumElapsed, Is.True);
        }

        [Test]
        public void Resolve_DeathRetryProfile_BlocksInputDuringPreOverlay()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry");

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.BlockInput, Is.True);
            Assert.That(profile.BlockInputDuringPreOverlayDelay, Is.True);
            Assert.That(profile.StartAsyncLoadBeforeOverlay, Is.True);
        }

        [Test]
        public void Resolve_SourceMapsLevelFailedRestartSeparatelyFromDeathRetry()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level");

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.Kind, Is.EqualTo(StageTransitionKind.LevelFailedRestart));
            Assert.That(profile.OverlayKind, Is.EqualTo(TransitionOverlayKind.Restart));
            Assert.That(profile.PreOverlayDelaySeconds, Is.EqualTo(1.0f));
            Assert.That(profile.OverlayKind, Is.Not.EqualTo(TransitionOverlayKind.ChanceLost));
        }

        [Test]
        public void Resolve_RetryNavigationKindAlone_DoesNotCollapseRetryIntents()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "generic-retry-button");

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.Kind, Is.EqualTo(StageTransitionKind.Unknown));
            Assert.That(profile.OverlayKind, Is.Not.EqualTo(TransitionOverlayKind.ChanceLost));
        }

        [Test]
        public void Resolve_SameScenePair_IsFallbackOnlyAndCannotOverrideIntent()
        {
            var sameSceneFallback = new StageTransitionProfile(
                StageTransitionKind.Unknown,
                "UIAudioScene",
                "UIAudioScene",
                0.1f,
                true,
                true,
                true,
                TransitionOverlayKind.GenericLoading);
            var resolver = new StageTransitionProfileResolver(new[] { sameSceneFallback });
            var deathRetry = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry");
            var levelFailedRestart = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level");

            var deathProfile = resolver.Resolve(deathRetry, "UIAudioScene", "UIAudioScene");
            var restartProfile = resolver.Resolve(levelFailedRestart, "UIAudioScene", "UIAudioScene");

            Assert.That(deathProfile.Kind, Is.EqualTo(StageTransitionKind.DeathRetryChanceLost));
            Assert.That(restartProfile.Kind, Is.EqualTo(StageTransitionKind.LevelFailedRestart));
            Assert.That(deathProfile.OverlayKind, Is.Not.EqualTo(restartProfile.OverlayKind));
        }

        [Test]
        public void Resolve_StageClearNextAndManualRetry_AreSeparate()
        {
            var resolver = new StageTransitionProfileResolver();
            var next = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-2"),
                StageNavigationKind.NextStage,
                "campaign-auto-next");
            var retry = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "stage-result-retry");

            var nextProfile = resolver.Resolve(next, "UIAudioScene", "UIAudioScene");
            var retryProfile = resolver.Resolve(retry, "UIAudioScene", "UIAudioScene");

            Assert.That(nextProfile.Kind, Is.EqualTo(StageTransitionKind.StageClearNext));
            Assert.That(nextProfile.PreOverlayDelaySeconds, Is.EqualTo(1.0f));
            Assert.That(retryProfile.Kind, Is.EqualTo(StageTransitionKind.StageRetryManual));
            Assert.That(nextProfile.OverlayKind, Is.Not.EqualTo(retryProfile.OverlayKind));
        }

        [Test]
        public void Resolve_DefaultProfileFallback_Works()
        {
            var defaultProfile = new StageTransitionProfile(
                StageTransitionKind.Unknown,
                string.Empty,
                string.Empty,
                0.2f,
                true,
                true,
                false,
                TransitionOverlayKind.None);
            var resolver = new StageTransitionProfileResolver(defaultProfile: defaultProfile);
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Continue,
                "unknown-source");

            var profile = resolver.Resolve(request, "UnknownFrom", "UnknownTo");

            Assert.That(profile, Is.SameAs(defaultProfile));
        }

        [Test]
        public void DeathRetryPayload_PreservesPreviousAndCurrentChances()
        {
            var payload = new StageTransitionChanceLostPayload(
                2,
                1,
                3,
                StageId.CreateOrThrow("stage-0-1"),
                StageId.CreateOrThrow("stage-0-1"),
                4,
                "campaign-death-retry");
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry",
                StageTransitionHint.ForChanceLost(payload));

            Assert.That(request.TransitionHint.HasChanceLostPayload, Is.True);
            Assert.That(request.TransitionHint.ChanceLostPayload.PreviousRemainingChances, Is.EqualTo(2));
            Assert.That(request.TransitionHint.ChanceLostPayload.CurrentRemainingChances, Is.EqualTo(1));
            Assert.That(request.TransitionHint.ChanceLostPayload.TotalChances, Is.EqualTo(3));
            Assert.That(request.TransitionHint.Kind, Is.EqualTo(StageTransitionKind.DeathRetryChanceLost));
            Assert.That(request.TransitionHint.ChanceLostPayload.CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-0-1")));
            Assert.That(request.TransitionHint.ChanceLostPayload.RetryStageId, Is.EqualTo(StageId.CreateOrThrow("stage-0-1")));
            Assert.That(request.TransitionHint.ChanceLostPayload.DeathCount, Is.EqualTo(4));
            Assert.That(request.TransitionHint.ChanceLostPayload.Source, Is.EqualTo("campaign-death-retry"));
        }

        [Test]
        public void LaunchGuard_RejectsDuplicateRequestUntilCompleted()
        {
            var guard = new StageTransitionLaunchGuard();

            Assert.That(guard.TryBegin(out var firstId), Is.True);
            Assert.That(guard.TryBegin(out var duplicateId), Is.False);
            Assert.That(duplicateId, Is.EqualTo(firstId));

            guard.Complete(firstId);

            Assert.That(guard.TryBegin(out var secondId), Is.True);
            Assert.That(secondId, Is.Not.EqualTo(firstId));
        }
    }
}
