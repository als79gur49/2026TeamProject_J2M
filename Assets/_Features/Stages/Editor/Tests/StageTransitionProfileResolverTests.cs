using NUnit.Framework;
using UnityEngine;

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
                StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart),
                SceneTransitionIntent.ManualRetry);

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
                "campaign-death-retry",
                transitionIntent: SceneTransitionIntent.DeathRetry);

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.Kind, Is.EqualTo(StageTransitionKind.DeathRetryChanceLost));
            Assert.That(profile.OverlayKind, Is.EqualTo(TransitionOverlayKind.ChanceLost));
        }

        [Test]
        public void Resolve_DeathRetryProfile_UsesExplicitCompletionWithoutFixedDelays()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry",
                transitionIntent: SceneTransitionIntent.DeathRetry);

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.PreOverlayDelaySeconds, Is.Zero);
            Assert.That(profile.MinimumVisibleSeconds, Is.Zero);
            Assert.That(profile.HoldSceneActivationUntilMinimumElapsed, Is.False);
            Assert.That(profile.RequiresExplicitContentCompletion, Is.True);
            Assert.That(profile.RequiresOpaqueTakeover, Is.True);
        }

        [Test]
        public void Resolve_DeathRetryProfile_StartsPreloadWithoutLegacyPreOverlayBarrier()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry",
                transitionIntent: SceneTransitionIntent.DeathRetry);

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.BlockInput, Is.True);
            Assert.That(profile.BlockInputDuringPreOverlayDelay, Is.False);
            Assert.That(profile.StartAsyncLoadBeforeOverlay, Is.True);
        }

        [Test]
        public void Resolve_SourceMapsLevelFailedRestartSeparatelyFromDeathRetry()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level",
                StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart),
                SceneTransitionIntent.ManualRetry);

            var profile = resolver.Resolve(request, "UIAudioScene", "UIAudioScene");

            Assert.That(profile.Kind, Is.EqualTo(StageTransitionKind.LevelFailedRestart));
            Assert.That(profile.OverlayKind, Is.EqualTo(TransitionOverlayKind.Restart));
            Assert.That(profile.PreOverlayDelaySeconds, Is.EqualTo(1.0f));
            Assert.That(profile.MinimumVisibleSeconds, Is.EqualTo(0.35f));
            Assert.That(profile.OverlayKind, Is.Not.EqualTo(TransitionOverlayKind.ChanceLost));
        }

        [Test]
        public void Resolve_UnknownIntent_DoesNotInferFromRetryNavigationOrSource()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "generic-retry-button");

            Assert.Throws<System.InvalidOperationException>(() =>
                resolver.Resolve(request, "UIAudioScene", "UIAudioScene"));
        }

        [Test]
        public void Resolve_SameScenePair_CannotInferOrOverrideExplicitIntent()
        {
            var ignoredUnknownProfile = new StageTransitionProfile(
                StageTransitionKind.Unknown,
                "UIAudioScene",
                "UIAudioScene",
                0.1f,
                true,
                true,
                true,
                TransitionOverlayKind.GameplayEntry);
            var resolver = new StageTransitionProfileResolver(new[] { ignoredUnknownProfile });
            var deathRetry = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry",
                transitionIntent: SceneTransitionIntent.DeathRetry);
            var levelFailedRestart = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level",
                StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart),
                SceneTransitionIntent.ManualRetry);

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
                "campaign-auto-next",
                transitionIntent: SceneTransitionIntent.StageAdvance);
            var retry = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "stage-result-retry",
                transitionIntent: SceneTransitionIntent.ManualRetry);

            var nextProfile = resolver.Resolve(next, "UIAudioScene", "UIAudioScene");
            var retryProfile = resolver.Resolve(retry, "UIAudioScene", "UIAudioScene");

            Assert.That(nextProfile.Kind, Is.EqualTo(StageTransitionKind.StageClearNext));
            Assert.That(nextProfile.PreOverlayDelaySeconds, Is.EqualTo(0.15f));
            Assert.That(nextProfile.MinimumVisibleSeconds, Is.EqualTo(0.25f));
            Assert.That(nextProfile.PreOverlayDelaySeconds, Is.LessThanOrEqualTo(0.15f));
            Assert.That(nextProfile.MinimumVisibleSeconds, Is.LessThanOrEqualTo(0.25f));
            Assert.That(retryProfile.Kind, Is.EqualTo(StageTransitionKind.StageRetryManual));
            Assert.That(retryProfile.MinimumVisibleSeconds, Is.EqualTo(0.35f));
            Assert.That(nextProfile.OverlayKind, Is.Not.EqualTo(retryProfile.OverlayKind));
        }

        [Test]
        public void Resolve_UnregisteredIntent_DoesNotReturnGenericDefaultProfile()
        {
            var resolver = new StageTransitionProfileResolver();
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Continue,
                "unknown-source");

            Assert.Throws<System.InvalidOperationException>(() =>
                resolver.Resolve(request, "UnknownFrom", "UnknownTo"));
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
                "campaign-death-retry",
                "Chance Lost",
                "Retrying.");
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "campaign-death-retry",
                StageTransitionHint.ForChanceLost(payload),
                SceneTransitionIntent.DeathRetry);

            Assert.That(request.TransitionHint.HasChanceLostPayload, Is.True);
            Assert.That(request.TransitionHint.ChanceLostPayload.PreviousRemainingChances, Is.EqualTo(2));
            Assert.That(request.TransitionHint.ChanceLostPayload.CurrentRemainingChances, Is.EqualTo(1));
            Assert.That(request.TransitionHint.ChanceLostPayload.TotalChances, Is.EqualTo(3));
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

        [Test]
        public void TerminalIrisRuntimePresets_BlackAtIsOwnedOnlyByFocusHoldAndClose()
        {
            var victory = CreateVictoryPreset();
            var defeat = CreateDefeatPreset();
            Assert.That(
                victory.BlackAt,
                Is.EqualTo(
                    victory.FocusDuration +
                    victory.HoldDuration +
                    victory.CloseDuration));
            Assert.That(
                defeat.BlackAt,
                Is.EqualTo(
                    defeat.FocusDuration +
                    defeat.HoldDuration +
                    defeat.CloseDuration));
            Assert.That(victory.BlackAt, Is.Not.EqualTo(defeat.BlackAt));
        }

        [Test]
        public void TerminalPlayback_LargeUnscaledDelta_ReachesBlackExactlyOnce_ThenRevealsExactlyOnce()
        {
            var preset = CreateDefeatPreset();
            var playback = new TerminalTransitionPlayback(preset);
            var blackCount = 0;
            var revealCount = 0;
            var request = new TerminalTransitionRequest(
                TerminalTransitionKind.Defeat,
                focusEntityId: 10,
                claimId: 7,
                destinationMode: TerminalTransitionDestinationMode.SameScene);
            playback.BlackReached += _ => blackCount++;
            playback.RevealCompleted += _ => revealCount++;

            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Hidden));
            Assert.That(
                playback.TryBegin(
                    request,
                    new TerminalFocusTarget(new Vector2(0.25f, 0.75f), 0.15f, false)),
                Is.True);
            Assert.That(
                playback.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Defeat,
                        focusEntityId: 10,
                        claimId: 8,
                        destinationMode: TerminalTransitionDestinationMode.SameScene),
                    default),
                Is.False);

            playback.Advance(preset.BlackAt + 10f);
            playback.Advance(10f);

            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Black));
            Assert.That(blackCount, Is.EqualTo(1));
            Assert.That(
                playback.RequestReveal(new TerminalSessionToken(999, 1)),
                Is.False);
            Assert.That(playback.RequestReveal(request.Token), Is.True);

            playback.Advance(preset.RevealPreset.Value.TotalDuration + 10f);
            playback.Advance(10f);

            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Completed));
            Assert.That(revealCount, Is.EqualTo(1));
        }

        [Test]
        public void TerminalPlayback_SceneHandoffRequiresMatchingClaimAtBlack()
        {
            var preset = CreateDefeatPreset();
            var playback = new TerminalTransitionPlayback(preset);
            var request = new TerminalTransitionRequest(
                TerminalTransitionKind.Defeat,
                focusEntityId: 10,
                claimId: 42,
                destinationMode: TerminalTransitionDestinationMode.SceneHandoff);
            playback.TryBegin(
                request,
                new TerminalFocusTarget(Vector2.one * 0.5f, 0.12f, false));

            playback.Advance(preset.BlackAt);

            Assert.That(
                playback.CompleteHandoff(
                    new TerminalSessionToken(
                        request.Token.AuthorityGeneration,
                        request.Token.Sequence - 1)),
                Is.False);
            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Black));
            Assert.That(playback.CompleteHandoff(request.Token), Is.True);
            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Completed));
        }

        [Test]
        public void TerminalPlayback_CancelAndDisposeAreSeparateAndDisposeSuppressesCallbacks()
        {
            var playback = new TerminalTransitionPlayback(CreateDefeatPreset());
            var cancelCount = 0;
            var blackCount = 0;
            playback.Cancelled += _ => cancelCount++;
            playback.BlackReached += _ => blackCount++;
            var request = new TerminalTransitionRequest(
                TerminalTransitionKind.Defeat,
                focusEntityId: 10,
                claimId: 11,
                destinationMode: TerminalTransitionDestinationMode.SameScene);
            playback.TryBegin(
                request,
                new TerminalFocusTarget(Vector2.one * 0.5f, 0.12f, false));

            Assert.That(
                playback.Cancel(
                    new TerminalSessionToken(
                        request.Token.AuthorityGeneration,
                        request.Token.Sequence + 1)),
                Is.False);
            Assert.That(playback.Cancel(request.Token), Is.True);
            Assert.That(playback.Cancel(request.Token), Is.False);
            Assert.That(cancelCount, Is.EqualTo(1));

            playback.Dispose();
            playback.Advance(100f);

            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Disposed));
            Assert.That(blackCount, Is.Zero);
            Assert.That(cancelCount, Is.EqualTo(1));
        }

        private static TerminalIrisRuntimePreset CreateVictoryPreset()
        {
            return new TerminalIrisRuntimePreset(
                0.24f,
                0.34f,
                0.42f,
                new Vector2(0.5f, 0.5f),
                0.22f,
                0.12f,
                0.035f,
                4f,
                CreateEdge(new Color(1f, 0.84f, 0.28f, 0f)),
                TerminalIrisEasing.Linear,
                TerminalIrisEasing.Linear);
        }

        private static TerminalIrisRuntimePreset CreateDefeatPreset()
        {
            var reveal = new TerminalIrisRuntimeOpenPreset(
                0f,
                0.3f,
                0.01f,
                4f,
                CreateEdge(new Color(0.86f, 0.12f, 0.1f, 0f)),
                TerminalIrisEasing.Linear);
            return new TerminalIrisRuntimePreset(
                0.18f,
                0.2f,
                0.32f,
                new Vector2(0.5f, 0.5f),
                0.2f,
                0.11f,
                0.035f,
                4f,
                CreateEdge(new Color(0.86f, 0.12f, 0.1f, 0f)),
                TerminalIrisEasing.Linear,
                TerminalIrisEasing.Linear,
                reveal);
        }

        private static TerminalIrisRuntimeEdgeSettings CreateEdge(Color rimColor)
        {
            return new TerminalIrisRuntimeEdgeSettings(
                0.82f,
                0.82f,
                1f,
                0f,
                0.85f,
                3f,
                rimColor);
        }
    }
}
