using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Feature.UI.Tests
{
    public sealed class SlotCinematicFlowTests
    {
        [Test]
        public void CinematicStageLaunchRouter_UsesHandoffSlot_ForIntroFlag()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_UsesHandoffSlot_ForIntroFlag));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                saveStore.SaveSlot(CreateSlot(2, stageId));
                var handoffStore = new RecordingCampaignLaunchHandoffStore();
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = true };
                var request = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Continue,
                    "main-menu-new-game",
                    StageTransitionHint.ForKindWithMinimum(StageTransitionKind.MainToGameplay, 0.25f));
                handoffStore.TryBegin(
                    2,
                    request.StageId,
                    request.NavigationKind,
                    request.Source,
                    out _);
                var router = new CinematicStageLaunchRouter(
                    inner,
                    saveStore,
                    handoffStore,
                    player);

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.EqualTo(1));
                Assert.That(inner.Requests, Is.Empty);
                Assert.That(saveStore.LoadSlot(2).IntroPlayed, Is.False);

                player.CompleteIntro();

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(2).IntroPlayed, Is.True);
                Assert.That(inner.Requests, Has.Count.EqualTo(1));
                AssertRequestsEqual(request, inner.Requests[0]);

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.EqualTo(1));
                Assert.That(inner.Requests, Has.Count.EqualTo(2));
                AssertRequestsEqual(request, inner.Requests[1]);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicStageLaunchRouter_PendingHandoffDoesNotModifyActiveSlot()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_PendingHandoffDoesNotModifyActiveSlot));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(3, stageId));
                activeSlotProvider.SetActiveSlot(3);
                var handoffStore = new RecordingCampaignLaunchHandoffStore();
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = true };
                var request = new StageNavigationRequest(stageId, StageNavigationKind.Continue, "handoff");
                handoffStore.TryBegin(
                    3,
                    request.StageId,
                    request.NavigationKind,
                    request.Source,
                    out _);
                var router = new CinematicStageLaunchRouter(
                    inner,
                    saveStore,
                    handoffStore,
                    player);

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.EqualTo(1));
                Assert.That(inner.Requests, Is.Empty);

                player.CompleteIntro();

                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(3));
                Assert.That(saveStore.LoadSlot(3).IntroPlayed, Is.True);
                Assert.That(inner.Requests, Has.Count.EqualTo(1));
                AssertRequestsEqual(request, inner.Requests[0]);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicStageLaunchRouter_MissingIntroClip_DelegatesImmediately()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_MissingIntroClip_DelegatesImmediately));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                var handoffStore = new RecordingCampaignLaunchHandoffStore();
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = false };
                var request = new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test");
                handoffStore.TryBegin(
                    1,
                    request.StageId,
                    request.NavigationKind,
                    request.Source,
                    out _);
                var router = new CinematicStageLaunchRouter(inner, saveStore, handoffStore, player);

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.Zero);
                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(inner.Requests, Has.Count.EqualTo(1));
                AssertRequestsEqual(request, inner.Requests[0]);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicStageLaunchRouter_MissingPendingHandoff_FailsClosed()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_MissingPendingHandoff_FailsClosed));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                var handoffStore = new RecordingCampaignLaunchHandoffStore();
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = true };
                var router = new CinematicStageLaunchRouter(inner, saveStore, handoffStore, player);
                var request = new StageNavigationRequest(stageId, StageNavigationKind.Continue, "missing-pending-slot");

                Assert.Throws<InvalidOperationException>(() => router.Launch(request));

                Assert.That(player.PlayIntroCallCount, Is.Zero);
                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(inner.Requests, Is.Empty);
            }
            finally
            {
                keys.Clear();
            }
        }

        [TestCase(CinematicPlaybackCompletionKind.Failed)]
        [TestCase(CinematicPlaybackCompletionKind.Cancelled)]
        public void CinematicStageLaunchRouter_FailureOrCancellation_ClearsMatchingHandoff(
            CinematicPlaybackCompletionKind completionKind)
        {
            var keys = TestKeys.Create(
                nameof(CinematicStageLaunchRouter_FailureOrCancellation_ClearsMatchingHandoff));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                var handoffStore = new RecordingCampaignLaunchHandoffStore();
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = true };
                var request = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Continue,
                    "cinematic-failure");
                handoffStore.TryBegin(
                    1,
                    request.StageId,
                    request.NavigationKind,
                    request.Source,
                    out _);
                var router = new CinematicStageLaunchRouter(
                    inner,
                    saveStore,
                    handoffStore,
                    player);

                router.Launch(request);
                player.CompleteIntro(completionKind);

                Assert.That(handoffStore.TryPeek(out _), Is.False);
                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(inner.Requests, Is.Empty);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void DeletePendingSlot_LateCompletedCallbackDoesNotMutateProfileOrRoute()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(DeletePendingSlot_LateCompletedCallbackDoesNotMutateProfileOrRoute));
            harness.StartCinematic();

            harness.SaveStore.DeleteSlot(harness.Handoff.SlotNumber);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.RawSaveStore.LoadSlot(harness.Handoff.SlotNumber).IsEmpty, Is.True);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
        }

        [Test]
        public void ClearAll_LateSkippedCallbackDoesNotMutateProfileOrRoute()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(ClearAll_LateSkippedCallbackDoesNotMutateProfileOrRoute));
            harness.StartCinematic();

            harness.SaveStore.ClearAll();
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Skipped);

            Assert.That(harness.RawSaveStore.LoadSlot(harness.Handoff.SlotNumber).IsEmpty, Is.True);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
        }

        [Test]
        public void ExpiredHandoff_LateCallbackIsNoOp()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(ExpiredHandoff_LateCallbackIsNoOp));
            harness.StartCinematic();
            Assert.That(harness.HandoffStore.TryClear(harness.Handoff.Token), Is.True);
            harness.HandoffStore.ResetClearCounts();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
            Assert.That(harness.HandoffStore.ClearAttemptCount, Is.Zero);
            Assert.That(harness.RawSaveStore.LoadSlot(harness.Handoff.SlotNumber).IntroPlayed, Is.False);
        }

        [Test]
        public void LateCallback_DoesNotClearNewerHandoff()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(LateCallback_DoesNotClearNewerHandoff));
            harness.StartCinematic();
            Assert.That(harness.HandoffStore.TryClear(harness.Handoff.Token), Is.True);
            Assert.That(
                harness.HandoffStore.TryBegin(
                    2,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "newer-operation",
                    out var newer),
                Is.True);
            harness.HandoffStore.ResetClearCounts();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Failed);

            Assert.That(harness.HandoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(newer));
            Assert.That(harness.HandoffStore.ClearAttemptCount, Is.Zero);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
        }

        [Test]
        public void SameTokenDifferentStage_CallbackIsRejected()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(SameTokenDifferentStage_CallbackIsRejected));
            harness.StartCinematic();
            harness.HandoffStore.ForcePending(new CampaignLaunchHandoff(
                harness.Handoff.SlotNumber,
                StageId.CreateOrThrow("stage-9-9"),
                harness.Handoff.NavigationKind,
                harness.Handoff.Source,
                harness.Handoff.Token));

            AssertOwnedCallbackIsRejected(harness);
        }

        [Test]
        public void SameTokenDifferentNavigation_CallbackIsRejected()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(SameTokenDifferentNavigation_CallbackIsRejected));
            harness.StartCinematic();
            harness.HandoffStore.ForcePending(new CampaignLaunchHandoff(
                harness.Handoff.SlotNumber,
                harness.Handoff.StageId,
                StageNavigationKind.Retry,
                harness.Handoff.Source,
                harness.Handoff.Token));

            AssertOwnedCallbackIsRejected(harness);
        }

        [Test]
        public void SameTokenDifferentSource_CallbackIsRejected()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(SameTokenDifferentSource_CallbackIsRejected));
            harness.StartCinematic();
            harness.HandoffStore.ForcePending(new CampaignLaunchHandoff(
                harness.Handoff.SlotNumber,
                harness.Handoff.StageId,
                harness.Handoff.NavigationKind,
                "different-source",
                harness.Handoff.Token));

            AssertOwnedCallbackIsRejected(harness);
        }

        [Test]
        public void WrongSlot_CallbackIsRejected()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(WrongSlot_CallbackIsRejected));
            harness.StartCinematic();
            harness.HandoffStore.ForcePending(new CampaignLaunchHandoff(
                2,
                harness.Handoff.StageId,
                harness.Handoff.NavigationKind,
                harness.Handoff.Source,
                harness.Handoff.Token));

            AssertOwnedCallbackIsRejected(harness);
        }

        [Test]
        public void CompletedThenCancelled_ProcessesOnlyCompleted()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(CompletedThenCancelled_ProcessesOnlyCompleted));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Cancelled);

            AssertSuccessfulTerminalProcessedOnce(harness);
            Assert.That(harness.HandoffStore.ClearAttemptCount, Is.Zero);
            Assert.That(harness.HandoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(harness.Handoff));
        }

        [Test]
        public void SkippedThenCompleted_RoutesOnlyOnce()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(SkippedThenCompleted_RoutesOnlyOnce));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Skipped);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            AssertSuccessfulTerminalProcessedOnce(harness);
        }

        [Test]
        public void DuplicateCompleted_WritesProgressAndRoutesOnce()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(DuplicateCompleted_WritesProgressAndRoutesOnce));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            AssertSuccessfulTerminalProcessedOnce(harness);
        }

        [Test]
        public void FailedThenCancelled_ClearsMatchingHandoffOnce()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(FailedThenCancelled_ClearsMatchingHandoffOnce));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Failed);
            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Cancelled);

            Assert.That(harness.HandoffStore.ClearAttemptCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.ClearSuccessCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
        }

        [Test]
        public void RouteRejected_DoesNotWriteIntroProgressAndClearsMatchingHandoff()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(RouteRejected_DoesNotWriteIntroProgressAndClearsMatchingHandoff));
            harness.Route.RejectOnLaunch = true;
            harness.StartCinematic();

            Assert.Throws<ImmediateRouteRejectedException>(() =>
                harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed));
            Assert.DoesNotThrow(() =>
                harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Cancelled));

            Assert.That(harness.Route.AttemptCount, Is.EqualTo(1));
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.HandoffStore.ClearSuccessCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void RouteThrows_DoesNotWriteIntroProgressAndClearsMatchingHandoff()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(RouteThrows_DoesNotWriteIntroProgressAndClearsMatchingHandoff));
            harness.Route.ExceptionToThrow = new ApplicationException("Injected route exception.");
            harness.StartCinematic();

            Assert.Throws<ApplicationException>(() =>
                harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Skipped));

            Assert.That(harness.Route.AttemptCount, Is.EqualTo(1));
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.HandoffStore.ClearSuccessCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void ValidCompleted_RoutesAndWritesIntroProgressOnce()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(ValidCompleted_RoutesAndWritesIntroProgressOnce));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            AssertSuccessfulTerminalProcessedOnce(harness);
            Assert.That(harness.ActiveSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
        }

        [Test]
        public void ValidSkipped_RoutesAndWritesIntroProgressOnce()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(ValidSkipped_RoutesAndWritesIntroProgressOnce));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Skipped);

            AssertSuccessfulTerminalProcessedOnce(harness);
        }

        [Test]
        public void ValidFailed_ClearsMatchingHandoffWithoutProgress()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(ValidFailed_ClearsMatchingHandoffWithoutProgress));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Failed);

            AssertFailedTerminalProcessedOnce(harness);
        }

        [Test]
        public void ValidCancelled_ClearsMatchingHandoffWithoutProgress()
        {
            using var harness = new CinematicLaunchHarness(
                nameof(ValidCancelled_ClearsMatchingHandoffWithoutProgress));
            harness.StartCinematic();

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Cancelled);

            AssertFailedTerminalProcessedOnce(harness);
        }

        [Test]
        public void CinematicMainMenuReturnRouter_OutroPlaysOnceOnlyForFinalClearMainReturn()
        {
            var keys = TestKeys.Create(nameof(CinematicMainMenuReturnRouter_OutroPlaysOnceOnlyForFinalClearMainReturn));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                saveStore.SaveSlot(CreateSlot(1, StageId.CreateOrThrow("stage-4-1"), campaignCompleted: true));
                activeSlotProvider.SetActiveSlot(1);
                var inner = new RecordingMainMenuReturnRouter();
                var player = new ManualSlotCinematicPlayer { HasOutroClipValue = true };
                var router = new CinematicMainMenuReturnRouter(
                    inner,
                    saveStore,
                    activeSlotProvider,
                    player,
                    () => true);

                router.ReturnToMainMenu();

                Assert.That(player.PlayOutroCallCount, Is.EqualTo(1));
                Assert.That(inner.ReturnCallCount, Is.Zero);

                player.CompleteOutro();

                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.True);
                Assert.That(inner.ReturnCallCount, Is.EqualTo(1));

                router.ReturnToMainMenu();

                Assert.That(player.PlayOutroCallCount, Is.EqualTo(1));
                Assert.That(inner.ReturnCallCount, Is.EqualTo(2));
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicMainMenuReturnRouter_LevelFailedMainReturn_BypassesOutro()
        {
            var keys = TestKeys.Create(nameof(CinematicMainMenuReturnRouter_LevelFailedMainReturn_BypassesOutro));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                saveStore.SaveSlot(CreateSlot(1, StageId.CreateOrThrow("stage-0-1")));
                activeSlotProvider.SetActiveSlot(1);
                var inner = new RecordingMainMenuReturnRouter();
                var player = new ManualSlotCinematicPlayer { HasOutroClipValue = true };
                var router = new CinematicMainMenuReturnRouter(
                    inner,
                    saveStore,
                    activeSlotProvider,
                    player,
                    () => false);

                router.ReturnToMainMenu();

                Assert.That(player.PlayOutroCallCount, Is.Zero);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.False);
                Assert.That(inner.ReturnCallCount, Is.EqualTo(1));
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void SaveSlotStore_CinematicFlags_RoundTripAndResetWithNewGameAndDelete()
        {
            var keys = TestKeys.Create(nameof(SaveSlotStore_CinematicFlags_RoundTripAndResetWithNewGameAndDelete));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = stageId,
                    IntroPlayed = true,
                    OutroPlayed = true,
                });

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.True);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.True);

                saveStore.SaveSlot(CreateSlot(1, stageId));

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.False);

                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = stageId,
                    IntroPlayed = true,
                    OutroPlayed = true,
                });
                saveStore.DeleteSlot(1);

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.True);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void SlotCinematicProgressStore_RemainsExplicitSlotNeutral()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/SlotCinematicProgressStore.cs");
            var keys = TestKeys.Create(nameof(SlotCinematicProgressStore_RemainsExplicitSlotNeutral));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                saveStore.SaveSlot(CreateSlot(2, stageId));
                saveStore.SaveSlot(CreateSlot(3, stageId));
                var progressStore = new SlotCinematicProgressStore(saveStore);

                progressStore.MarkIntroPlayed(2);
                progressStore.MarkOutroPlayed(3);

                Assert.That(source, Does.Not.Contain("IPendingLaunchSlotProvider"));
                Assert.That(source, Does.Not.Contain("ActiveSlotProvider"));
                Assert.That(progressStore.IsIntroPlayed(1), Is.False);
                Assert.That(progressStore.IsOutroPlayed(1), Is.False);
                Assert.That(progressStore.IsIntroPlayed(2), Is.True);
                Assert.That(progressStore.IsOutroPlayed(2), Is.False);
                Assert.That(progressStore.IsIntroPlayed(3), Is.False);
                Assert.That(progressStore.IsOutroPlayed(3), Is.True);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicVideoOverlayView_UsesAudioSourceOutput_AndBlocksLowerInput()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_UsesAudioSourceOutput_AndBlocksLowerInput), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;

                view.Play(
                    clip,
                    CreatePlaybackOptions(),
                    _ => completionCount++);

                Assert.That(view.ConfiguredAudioOutputMode, Is.EqualTo(VideoAudioOutputMode.AudioSource));
                Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);

                view.CompleteForTesting(CinematicPlaybackCompletionKind.Completed);
                view.AdvanceFadeForTesting(1f);
                view.CompleteForTesting(CinematicPlaybackCompletionKind.Skipped);

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_Skip_DoesNotCompleteUntilExitFadeCompletes()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_Skip_DoesNotCompleteUntilExitFadeCompletes), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => completionCount++);
                AdvanceToPlaying(view);

                view.RequestSkip();

                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.ExitFadeToBlack));
                Assert.That(completionCount, Is.Zero);

                view.AdvanceFadeForTesting(0.3f);

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Completed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_NaturalEnd_DoesNotCompleteUntilExitFadeCompletes()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_NaturalEnd_DoesNotCompleteUntilExitFadeCompletes), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => completionCount++);
                AdvanceToPlaying(view);

                view.RequestExitFade(CinematicExitReason.NaturalEnd);

                Assert.That(completionCount, Is.Zero);

                view.AdvanceFadeForTesting(0.3f);

                Assert.That(completionCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_SkipAndNaturalEnd_ShareCompleteOncePath()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_SkipAndNaturalEnd_ShareCompleteOncePath), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => completionCount++);
                AdvanceToPlaying(view);

                view.RequestSkip();
                view.RequestExitFade(CinematicExitReason.NaturalEnd);
                view.AdvanceFadeForTesting(1f);

                Assert.That(completionCount, Is.EqualTo(1));

                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => completionCount++);
                AdvanceToPlaying(view);

                view.RequestExitFade(CinematicExitReason.NaturalEnd);
                view.RequestSkip();
                view.AdvanceFadeForTesting(1f);

                Assert.That(completionCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_ExitFade_IgnoresAdditionalSkip()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_ExitFade_IgnoresAdditionalSkip), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => completionCount++);
                AdvanceToPlaying(view);

                view.RequestSkip();
                view.RequestSkip();
                view.RequestSkip();

                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.ExitFadeToBlack));
                Assert.That(completionCount, Is.Zero);

                view.AdvanceFadeForTesting(1f);

                Assert.That(completionCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_VisualFadeAlphaSequence()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_VisualFadeAlphaSequence), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => { });

                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.EnterFadeToBlack));
                Assert.That(view.CurrentFadeAlpha, Is.EqualTo(0f).Within(0.0001f));

                view.AdvanceFadeForTesting(0.25f);
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.PreparingVideo));
                Assert.That(view.CurrentFadeAlpha, Is.EqualTo(1f).Within(0.0001f));

                view.NotifyPreparedFirstFrame();
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.RevealFadeFromBlack));
                Assert.That(view.CurrentFadeAlpha, Is.EqualTo(1f).Within(0.0001f));

                view.AdvanceFadeForTesting(0.25f);
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Playing));
                Assert.That(view.CurrentFadeAlpha, Is.EqualTo(0f).Within(0.0001f));

                view.RequestSkip();
                view.AdvanceFadeForTesting(0.3f);
                Assert.That(view.CurrentFadeAlpha, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_AudioFadeGain_ExitFade_InterpolatesOneToZero()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_AudioFadeGain_ExitFade_InterpolatesOneToZero), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => { });
                AdvanceToPlaying(view);

                view.RequestSkip();

                Assert.That(view.CurrentAudioFadeGain, Is.EqualTo(1f).Within(0.0001f));

                view.AdvanceFadeForTesting(0.15f);

                Assert.That(view.CurrentAudioFadeGain, Is.GreaterThan(0f));
                Assert.That(view.CurrentAudioFadeGain, Is.LessThan(1f));

                view.AdvanceFadeForTesting(0.15f);

                Assert.That(view.CurrentAudioFadeGain, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicStageLaunchRouter_CompletionSideEffects_AfterExitFadeOnly()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_CompletionSideEffects_AfterExitFadeOnly));
            var clip = LoadTestClip();
            var definition = CreateDefinition(clip, CreateFadeSettings(enter: 0f, reveal: 0f, exit: 0.3f));
            var root = new GameObject(nameof(CinematicStageLaunchRouter_CompletionSideEffects_AfterExitFadeOnly), typeof(RectTransform));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                var handoffStore = new RecordingCampaignLaunchHandoffStore();
                var inner = new RecordingStageLaunchRouter();
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var audioFocus = root.AddComponent<CinematicAudioFocusController>();
                var player = new CinematicFlowCoordinator(definition, view, audioFocus);
                var request = new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test");
                handoffStore.TryBegin(
                    1,
                    request.StageId,
                    request.NavigationKind,
                    request.Source,
                    out _);
                var router = new CinematicStageLaunchRouter(
                    inner,
                    saveStore,
                    handoffStore,
                    player);

                router.Launch(request);
                view.NotifyPreparedFirstFrame();
                view.RequestSkip();

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(inner.Requests, Is.Empty);

                view.AdvanceFadeForTesting(0.3f);

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.True);
                Assert.That(inner.Requests, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(definition);
                keys.Clear();
            }
        }

        [Test]
        public void CinematicVideoOverlayView_LowerInputBlock_RemainsActiveDuringAllFadeStates()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_LowerInputBlock_RemainsActiveDuringAllFadeStates), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Play(clip, CreatePlaybackOptions(fadeSettings: CreateFadeSettings()), _ => { });
                var group = root.GetComponent<CanvasGroup>();

                Assert.That(group.blocksRaycasts, Is.True);

                view.AdvanceFadeForTesting(0.25f);
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.PreparingVideo));
                Assert.That(group.blocksRaycasts, Is.True);

                view.NotifyPreparedFirstFrame();
                Assert.That(group.blocksRaycasts, Is.True);

                view.AdvanceFadeForTesting(0.25f);
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Playing));
                Assert.That(group.blocksRaycasts, Is.True);

                view.RequestSkip();
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.ExitFadeToBlack));
                Assert.That(group.blocksRaycasts, Is.True);

                view.AdvanceFadeForTesting(0.3f);
                Assert.That(group.blocksRaycasts, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicFlowCoordinator_MissingClip_Fallback_RemainsImmediate()
        {
            var definition = ScriptableObject.CreateInstance<SlotCinematicDefinition>();
            var root = new GameObject(nameof(CinematicFlowCoordinator_MissingClip_Fallback_RemainsImmediate), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var player = new CinematicFlowCoordinator(definition, view, null);
                var completionCount = 0;

                player.PlayIntro(_ => completionCount++);

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_ZeroDurationFade_CompletesDeterministically()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_ZeroDurationFade_CompletesDeterministically), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;
                view.Play(
                    clip,
                    CreatePlaybackOptions(fadeSettings: CreateFadeSettings(enter: 0f, reveal: 0f, exit: 0f)),
                    _ => completionCount++);

                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.PreparingVideo));

                view.NotifyPreparedFirstFrame();
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Playing));

                view.RequestSkip();

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Completed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1920f, 1080f, 1.77778f)]
        [TestCase(2560f, 1080f, 2.37037f)]
        [TestCase(1280f, 1024f, 1.25f)]
        public void CinematicVideoOverlayView_AutoResolvedViewport_UsesResolvedViewportAspect(
            float width,
            float height,
            float expectedAspect)
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_AutoResolvedViewport_UsesResolvedViewportAspect), typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(width, height);
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, new ManualViewportProvider(new Vector2(width, height)));

                view.Play(clip, CreatePlaybackOptions(), _ => { });

                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(expectedAspect).Within(0.0001f));
                Assert.That(view.ConfiguredPresentationAspectRatio, Is.Not.EqualTo(clip.width / (float)clip.height).Within(0.0001f));
                Assert.That(view.ConfiguredContentAspectRatio, Is.EqualTo(clip.width / (float)clip.height).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolvedCinematicViewportProvider_FallsBackThroughCanvasCameraScreenAndDisplay()
        {
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            try
            {
                var overlayRect = (RectTransform)overlay.transform;
                var canvasRect = (RectTransform)canvas.transform;
                var camera = cameraObject.GetComponent<Camera>();

                overlayRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.sizeDelta = new Vector2(2560f, 1080f);
                var provider = new ResolvedCinematicViewportProvider(
                    overlayRect,
                    canvasRect,
                    camera,
                    cameraPixelRectSizeProvider: () => Vector2.zero,
                    screenSizeProvider: () => new Vector2(1920f, 1080f),
                    displaySizeProvider: () => new Vector2(1600f, 900f));

                Assert.That(provider.TryGetAspectRatio(out var aspect), Is.True);
                Assert.That(aspect, Is.EqualTo(1920f / 1080f).Within(0.0001f));

                overlayRect.sizeDelta = Vector2.zero;
                Assert.That(provider.TryGetAspectRatio(out aspect), Is.True);
                Assert.That(aspect, Is.EqualTo(2560f / 1080f).Within(0.0001f));

                canvasRect.sizeDelta = Vector2.zero;
                provider = new ResolvedCinematicViewportProvider(
                    overlayRect,
                    canvasRect,
                    camera,
                    cameraPixelRectSizeProvider: () => new Vector2(1280f, 1024f),
                    screenSizeProvider: () => new Vector2(1920f, 1080f),
                    displaySizeProvider: () => new Vector2(1600f, 900f));
                Assert.That(provider.TryGetAspectRatio(out aspect), Is.True);
                Assert.That(aspect, Is.EqualTo(1.25f).Within(0.0001f));

                provider = new ResolvedCinematicViewportProvider(
                    overlayRect,
                    canvasRect,
                    null,
                    cameraPixelRectSizeProvider: () => Vector2.zero,
                    screenSizeProvider: () => new Vector2(1920f, 1080f),
                    displaySizeProvider: () => new Vector2(1600f, 900f));
                Assert.That(provider.TryGetAspectRatio(out aspect), Is.True);
                Assert.That(aspect, Is.EqualTo(1920f / 1080f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvas);
                UnityEngine.Object.DestroyImmediate(overlay);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_AutoResolvedViewport_UsesSixteenByNineWhenAllSourcesAreInvalid()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_AutoResolvedViewport_UsesSixteenByNineWhenAllSourcesAreInvalid), typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(
                    null,
                    new ResolvedCinematicViewportProvider(
                        rootRect,
                        null,
                        null,
                        cameraPixelRectSizeProvider: () => Vector2.zero,
                        screenSizeProvider: () => Vector2.zero,
                        displaySizeProvider: () => Vector2.zero));

                view.Play(clip, CreatePlaybackOptions(), _ => { });

                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(16f / 9f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_SettingsSelectedAspect_IsOnlyUsedForExplicitMode()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_SettingsSelectedAspect_IsOnlyUsedForExplicitMode), typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1920f, 1080f);
                var selectedAspectProvider = new ManualSelectedAspectProvider(4f / 3f);
                var viewportProvider = new ManualViewportProvider(new Vector2(1920f, 1080f));
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, viewportProvider, selectedAspectProvider);

                view.Play(clip, CreatePlaybackOptions(), _ => { });

                Assert.That(selectedAspectProvider.CallCount, Is.Zero);
                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(16f / 9f).Within(0.0001f));

                view.CompleteForTesting(CinematicPlaybackCompletionKind.Completed);
                view.AdvanceFadeForTesting(1f);
                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.SettingsSelectedAspect),
                    _ => { });

                Assert.That(selectedAspectProvider.CallCount, Is.EqualTo(1));
                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(4f / 3f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_InvalidSettingsSelectedAspect_FallsBackToAutoViewport()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_InvalidSettingsSelectedAspect_FallsBackToAutoViewport), typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(2560f, 1080f);
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(
                    null,
                    new ManualViewportProvider(new Vector2(2560f, 1080f)),
                    new ManualSelectedAspectProvider(0f));

                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.SettingsSelectedAspect),
                    _ => { });

                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(2560f / 1080f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_VideoClipAspect_UsesClipMetadataOnlyWhenExplicit()
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_VideoClipAspect_UsesClipMetadataOnlyWhenExplicit), typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1920f, 1080f);
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(
                    null,
                    new ManualViewportProvider(new Vector2(1920f, 1080f)));

                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.VideoClipAspect, CinematicScaleMode.FitInsideViewport),
                    _ => { });

                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(1872f / 1080f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(CinematicScaleMode.StretchToViewport, false, AspectRatioFitter.AspectMode.None)]
        [TestCase(CinematicScaleMode.CropToFillViewport, false, AspectRatioFitter.AspectMode.None)]
        [TestCase(CinematicScaleMode.FitInsideViewport, false, AspectRatioFitter.AspectMode.None)]
        public void CinematicVideoOverlayView_ScaleMode_ConfiguresContentLayout(
            CinematicScaleMode scaleMode,
            bool contentFitterEnabled,
            AspectRatioFitter.AspectMode expectedAspectMode)
        {
            var clip = LoadTestClip();
            var root = new GameObject(nameof(CinematicVideoOverlayView_ScaleMode_ConfiguresContentLayout), typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1920f, 1080f);
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(
                    null,
                    new ManualViewportProvider(new Vector2(1920f, 1080f)));

                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.AutoResolvedViewport, scaleMode),
                    _ => { });

                Assert.That(view.IsViewportAspectFitterEnabled, Is.False);
                Assert.That(view.IsContentAspectFitterEnabled, Is.EqualTo(contentFitterEnabled));
                Assert.That(view.ConfiguredScaleMode, Is.EqualTo(scaleMode));
                if (contentFitterEnabled)
                {
                    Assert.That(view.ConfiguredContentAspectMode, Is.EqualTo(expectedAspectMode));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1920f, 1080f, 0f, 0.0125f, 1f, 0.975f)]
        [TestCase(2560f, 1080f, 0f, 0.134375f, 1f, 0.73125f)]
        [TestCase(1920f, 1200f, 0.0384615f, 0f, 0.9230769f, 1f)]
        [TestCase(1440f, 1080f, 0.1153846f, 0f, 0.7692308f, 1f)]
        [TestCase(1280f, 1024f, 0.1394231f, 0f, 0.7211539f, 1f)]
        public void CinematicVideoOverlayView_CropToFillViewport_CropsSourceUvInsideFullViewport(
            float viewportWidth,
            float viewportHeight,
            float expectedUvX,
            float expectedUvY,
            float expectedUvWidth,
            float expectedUvHeight)
        {
            var clip = LoadTestClip();
            var canvas = CreateCanvasRoot(viewportWidth, viewportHeight, out var root, nameof(CinematicVideoOverlayView_CropToFillViewport_CropsSourceUvInsideFullViewport));
            try
            {
                var rootRect = (RectTransform)root.transform;
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, new ManualViewportProvider(new Vector2(viewportWidth, viewportHeight)));

                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.AutoResolvedViewport, CinematicScaleMode.CropToFillViewport),
                    _ => { });
                RebuildCinematicLayout(rootRect);

                var viewport = GetVideoViewport(root);
                var image = GetVideoImage(root);

                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(viewportWidth / viewportHeight).Within(0.0001f));
                Assert.That(view.ConfiguredContentAspectRatio, Is.EqualTo(1872f / 1080f).Within(0.0001f));
                AssertVector2Within(viewport.rect.size, new Vector2(viewportWidth, viewportHeight), 0.01f);
                AssertVector2Within(image.rectTransform.rect.size, new Vector2(viewportWidth, viewportHeight), 0.01f);
                AssertRectWithin(image.uvRect, new Rect(expectedUvX, expectedUvY, expectedUvWidth, expectedUvHeight), 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_CropToFillViewport_UsesSourceAspectRenderTexture()
        {
            var clip = LoadTestClip();
            var canvas = CreateCanvasRoot(1920f, 1080f, out var root, nameof(CinematicVideoOverlayView_CropToFillViewport_UsesSourceAspectRenderTexture));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, new ManualViewportProvider(new Vector2(1920f, 1080f)));

                view.Play(
                    clip,
                    CreatePlaybackOptions(
                        CinematicAspectSource.AutoResolvedViewport,
                        CinematicScaleMode.CropToFillViewport,
                        renderTextureWidth: 1920,
                        renderTextureHeight: 1080),
                    _ => { });

                Assert.That(view.ConfiguredContentAspectRatio, Is.EqualTo(1872f / 1080f).Within(0.0001f));
                Assert.That(view.ConfiguredRenderTextureWidth, Is.EqualTo(1872));
                Assert.That(view.ConfiguredRenderTextureHeight, Is.EqualTo(1080));
                Assert.That(view.ConfiguredRenderTextureAspectRatio, Is.EqualTo(view.ConfiguredContentAspectRatio).Within(0.0001f));
                Assert.That(view.ConfiguredRenderTextureAspectRatio, Is.Not.EqualTo(1920f / 1080f).Within(0.0001f));
                Assert.That(view.ConfiguredVideoPlayerAspectRatio, Is.EqualTo(VideoAspectRatio.FitInside));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_StretchToViewport_OnlyWhenExplicit_UsesFullViewportRect()
        {
            var clip = LoadTestClip();
            var canvas = CreateCanvasRoot(1920f, 1080f, out var root, nameof(CinematicVideoOverlayView_StretchToViewport_OnlyWhenExplicit_UsesFullViewportRect));
            try
            {
                var rootRect = (RectTransform)root.transform;
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, new ManualViewportProvider(new Vector2(1920f, 1080f)));

                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.AutoResolvedViewport, CinematicScaleMode.StretchToViewport),
                    _ => { });
                RebuildCinematicLayout(rootRect);

                var image = GetVideoImage(root);
                Assert.That(view.ConfiguredScaleMode, Is.EqualTo(CinematicScaleMode.StretchToViewport));
                Assert.That(view.IsContentAspectFitterEnabled, Is.False);
                AssertVector2Within(image.rectTransform.rect.size, new Vector2(1920f, 1080f), 0.01f);
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_FitInsideViewport_ExplicitMode_AllowsPillarbox()
        {
            var clip = LoadTestClip();
            var canvas = CreateCanvasRoot(1920f, 1080f, out var root, nameof(CinematicVideoOverlayView_FitInsideViewport_ExplicitMode_AllowsPillarbox));
            try
            {
                var rootRect = (RectTransform)root.transform;
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, new ManualViewportProvider(new Vector2(1920f, 1080f)));

                view.Play(
                    clip,
                    CreatePlaybackOptions(CinematicAspectSource.AutoResolvedViewport, CinematicScaleMode.FitInsideViewport),
                    _ => { });
                RebuildCinematicLayout(rootRect);
                InvokeOverlayUpdate(view);
                RebuildCinematicLayout(rootRect);

                var image = GetVideoImage(root);
                Assert.That(view.ConfiguredScaleMode, Is.EqualTo(CinematicScaleMode.FitInsideViewport));
                AssertVector2Within(image.rectTransform.rect.size, new Vector2(1872f, 1080f), 0.05f);
                Assert.That(image.rectTransform.rect.width, Is.LessThan(1920f));
                Assert.That(image.rectTransform.rect.height, Is.EqualTo(1080f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void CinematicVideoOverlayView_AutoResolvedViewport_RecomputesAfterViewportResize()
        {
            var clip = LoadTestClip();
            var canvas = CreateCanvasRoot(1920f, 1080f, out var root, nameof(CinematicVideoOverlayView_AutoResolvedViewport_RecomputesAfterViewportResize));
            try
            {
                var canvasRect = (RectTransform)canvas.transform;
                var rootRect = (RectTransform)root.transform;
                var viewportProvider = new MutableViewportProvider(new Vector2(1920f, 1080f));
                var view = root.AddComponent<CinematicVideoOverlayView>();
                view.Initialize(null, viewportProvider);

                view.Play(clip, CreatePlaybackOptions(), _ => { });
                RebuildCinematicLayout(rootRect);

                var image = GetVideoImage(root);
                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(16f / 9f).Within(0.0001f));
                AssertVector2Within(image.rectTransform.rect.size, new Vector2(1920f, 1080f), 0.01f);
                AssertRectWithin(image.uvRect, new Rect(0f, 0.0125f, 1f, 0.975f), 0.0001f);

                canvasRect.sizeDelta = new Vector2(1280f, 1024f);
                viewportProvider.Size = new Vector2(1280f, 1024f);
                InvokeOverlayUpdate(view);
                RebuildCinematicLayout(rootRect);

                image = GetVideoImage(root);
                Assert.That(view.ConfiguredPresentationAspectRatio, Is.EqualTo(1.25f).Within(0.0001f));
                AssertVector2Within(GetVideoViewport(root).rect.size, new Vector2(1280f, 1024f), 0.01f);
                AssertVector2Within(image.rectTransform.rect.size, new Vector2(1280f, 1024f), 0.01f);
                AssertRectWithin(image.uvRect, new Rect(0.1394231f, 0f, 0.7211539f, 1f), 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void CinematicSources_DoNotUseDirectVideoOutputOrUiSfxDispatch()
        {
            var overlaySource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/CinematicVideoOverlayView.cs");
            var focusSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/CinematicAudioFocusController.cs");
            var gameplayInstallerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");

            Assert.That(overlaySource, Does.Not.Contain("VideoAudioOutputMode.Direct"));
            Assert.That(focusSource, Does.Not.Contain("IUiAudioPort"));
            Assert.That(focusSource, Does.Not.Contain("AudioChannel.Bgm"));
            Assert.That(focusSource, Does.Not.Contain("AudioChannel.Sfx"));
            Assert.That(focusSource, Does.Not.Contain("AudioChannel.Ui"));
            Assert.That(focusSource, Does.Not.Contain("new BgmFlowCoordinator"));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("GlobalAudioFlowRoot"));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("GlobalAudioFlowBootstrap"));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("IBgmFlowCoordinator"));
        }

        private static SaveSlotData CreateSlot(
            int slotNumber,
            StageId stageId,
            bool campaignCompleted = false)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "test-group",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                CampaignCompleted = campaignCompleted,
            };
        }

        private static void AssertRequestsEqual(StageNavigationRequest expected, StageNavigationRequest actual)
        {
            Assert.That(actual.StageId, Is.EqualTo(expected.StageId));
            Assert.That(actual.NavigationKind, Is.EqualTo(expected.NavigationKind));
            Assert.That(actual.Source, Is.EqualTo(expected.Source));
            Assert.That(actual.TransitionHint.Kind, Is.EqualTo(expected.TransitionHint.Kind));
            Assert.That(actual.TransitionHint.HasMinimumVisibleSecondsOverride, Is.EqualTo(expected.TransitionHint.HasMinimumVisibleSecondsOverride));
            Assert.That(actual.TransitionHint.MinimumVisibleSecondsOverride, Is.EqualTo(expected.TransitionHint.MinimumVisibleSecondsOverride));
        }

        private static void AssertOwnedCallbackIsRejected(CinematicLaunchHarness harness)
        {
            var expectedCurrent = harness.HandoffStore.Current;

            harness.Player.EmitIntro(CinematicPlaybackCompletionKind.Completed);

            Assert.That(harness.HandoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(expectedCurrent));
            Assert.That(harness.HandoffStore.ClearAttemptCount, Is.Zero);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
            Assert.That(harness.RawSaveStore.LoadSlot(harness.Handoff.SlotNumber).IntroPlayed, Is.False);
        }

        private static void AssertSuccessfulTerminalProcessedOnce(CinematicLaunchHarness harness)
        {
            Assert.That(harness.Route.AttemptCount, Is.EqualTo(1));
            Assert.That(harness.Route.Requests, Has.Count.EqualTo(1));
            AssertRequestsEqual(harness.Request, harness.Route.Requests[0]);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.EqualTo(1));
            Assert.That(
                harness.ProgressStore.UpdatedSlotNumbers,
                Is.EqualTo(new[] { harness.Handoff.SlotNumber }));
            Assert.That(harness.RawSaveStore.LoadSlot(harness.Handoff.SlotNumber).IntroPlayed, Is.True);
        }

        private static void AssertFailedTerminalProcessedOnce(CinematicLaunchHarness harness)
        {
            Assert.That(harness.HandoffStore.ClearAttemptCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.ClearSuccessCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ProgressStore.UpdateSlotCallCount, Is.Zero);
            Assert.That(harness.Route.AttemptCount, Is.Zero);
            Assert.That(harness.RawSaveStore.LoadSlot(harness.Handoff.SlotNumber).IntroPlayed, Is.False);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static GameObject CreateCanvasRoot(float width, float height, out GameObject root, string rootName)
        {
            var canvas = new GameObject(rootName + "Canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(width, height);
            root = new GameObject(rootName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            return canvas;
        }

        private static RectTransform GetVideoViewport(GameObject root)
        {
            var viewport = root.transform.Find("Video") as RectTransform;
            Assert.That(viewport, Is.Not.Null);
            return viewport;
        }

        private static RawImage GetVideoImage(GameObject root)
        {
            var viewport = GetVideoViewport(root);
            var image = viewport.GetComponentInChildren<RawImage>(includeInactive: true);
            Assert.That(image, Is.Not.Null);
            return image;
        }

        private static void RebuildCinematicLayout(RectTransform rootRect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetVideoViewport(rootRect.gameObject));
            Canvas.ForceUpdateCanvases();
        }

        private static void InvokeOverlayUpdate(CinematicVideoOverlayView view)
        {
            typeof(CinematicVideoOverlayView)
                .GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(view, null);
        }

        private static VideoClip LoadTestClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/3DM/VQ 인트로.mp4");
            Assert.That(clip, Is.Not.Null, "VQ 인트로.mp4 must remain importable as a VideoClip.");
            return clip;
        }

        private static void AdvanceToPlaying(CinematicVideoOverlayView view)
        {
            view.AdvanceFadeForTesting(1f);
            Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.PreparingVideo));
            view.NotifyPreparedFirstFrame();
            view.AdvanceFadeForTesting(1f);
            Assert.That(view.CurrentPresentationState, Is.EqualTo(CinematicPresentationState.Playing));
        }

        private static void AssertVector2Within(Vector2 actual, Vector2 expected, float tolerance = 0.001f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
        }

        private static void AssertRectWithin(Rect actual, Rect expected, float tolerance = 0.001f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(tolerance));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(tolerance));
        }

        private static SlotCinematicPlaybackOptions CreatePlaybackOptions(
            CinematicAspectSource aspectSource = CinematicAspectSource.AutoResolvedViewport,
            CinematicScaleMode scaleMode = CinematicScaleMode.CropToFillViewport,
            float fixedAspectRatio = 16f / 9f,
            int renderTextureWidth = 320,
            int renderTextureHeight = 180,
            CinematicFadeSettings? fadeSettings = null)
        {
            return new SlotCinematicPlaybackOptions(
                true,
                aspectSource,
                scaleMode,
                fixedAspectRatio,
                renderTextureWidth,
                renderTextureHeight,
                fadeSettings ?? CinematicFadeSettings.Default);
        }

        private static CinematicFadeSettings CreateFadeSettings(
            float enter = 0.25f,
            float reveal = 0.25f,
            float exit = 0.3f,
            bool audioFadeOutWithExit = true,
            CinematicPlaybackStartPolicy playbackStartPolicy = CinematicPlaybackStartPolicy.AfterRevealFade,
            CinematicSkipDuringFadePolicy skipDuringFadePolicy = CinematicSkipDuringFadePolicy.IgnoreUntilPlaying)
        {
            return new CinematicFadeSettings(
                enter,
                reveal,
                exit,
                Color.black,
                CinematicFadeEase.SmoothStep,
                audioFadeOutWithExit,
                playbackStartPolicy,
                skipDuringFadePolicy);
        }

        private static SlotCinematicDefinition CreateDefinition(VideoClip clip, CinematicFadeSettings fadeSettings)
        {
            var definition = ScriptableObject.CreateInstance<SlotCinematicDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_introClip").objectReferenceValue = clip;
            serialized.FindProperty("_skipEnabled").boolValue = true;
            serialized.FindProperty("_aspectSource").enumValueIndex = (int)CinematicAspectSource.AutoResolvedViewport;
            serialized.FindProperty("_scaleMode").enumValueIndex = (int)CinematicScaleMode.CropToFillViewport;
            serialized.FindProperty("_fixedAspectRatio").floatValue = 16f / 9f;
            serialized.FindProperty("_renderTextureWidth").intValue = 320;
            serialized.FindProperty("_renderTextureHeight").intValue = 180;

            var fade = serialized.FindProperty("_fadeSettings");
            fade.FindPropertyRelative("_enterFadeDuration").floatValue = fadeSettings.EnterFadeDuration;
            fade.FindPropertyRelative("_revealFadeDuration").floatValue = fadeSettings.RevealFadeDuration;
            fade.FindPropertyRelative("_exitFadeDuration").floatValue = fadeSettings.ExitFadeDuration;
            fade.FindPropertyRelative("_fadeColor").colorValue = fadeSettings.FadeColor;
            fade.FindPropertyRelative("_fadeEase").enumValueIndex = (int)fadeSettings.FadeEase;
            fade.FindPropertyRelative("_audioFadeOutWithExit").boolValue = fadeSettings.AudioFadeOutWithExit;
            fade.FindPropertyRelative("_playbackStartPolicy").enumValueIndex = (int)fadeSettings.PlaybackStartPolicy;
            fade.FindPropertyRelative("_skipDuringFadePolicy").enumValueIndex = (int)fadeSettings.SkipDuringFadePolicy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private readonly struct TestKeys
        {
            private TestKeys(string saveKey, string activeKey)
            {
                SaveKey = saveKey;
                ActiveKey = activeKey;
            }

            public string SaveKey { get; }

            public string ActiveKey { get; }

            public static TestKeys Create(string suffix)
            {
                var prefix = "Game.Feature.UI.Tests.SlotCinematic." + suffix + "." + Guid.NewGuid().ToString("N");
                return new TestKeys(prefix + ".Save", prefix + ".Active");
            }

            public void Clear()
            {
                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.DeleteKey(ActiveKey);
                PlayerPrefs.Save();
            }
        }

        private sealed class ManualSlotCinematicPlayer : ISlotCinematicPlayer
        {
            private Action<CinematicPlaybackCompletion> _introCompletion;
            private Action<CinematicPlaybackCompletion> _outroCompletion;

            public bool HasIntroClipValue { get; set; }

            public bool HasOutroClipValue { get; set; }

            public bool HasIntroClip => HasIntroClipValue;

            public bool HasOutroClip => HasOutroClipValue;

            public bool IsPlaying { get; private set; }

            public int PlayIntroCallCount { get; private set; }

            public int PlayOutroCallCount { get; private set; }

            public void PlayIntro(Action<CinematicPlaybackCompletion> completion)
            {
                PlayIntroCallCount++;
                IsPlaying = true;
                _introCompletion = completion;
            }

            public void PlayOutro(Action<CinematicPlaybackCompletion> completion)
            {
                PlayOutroCallCount++;
                IsPlaying = true;
                _outroCompletion = completion;
            }

            public void RequestSkip()
            {
                CompleteIntro();
                CompleteOutro();
            }

            public void CompleteIntro(
                CinematicPlaybackCompletionKind kind = CinematicPlaybackCompletionKind.Completed)
            {
                var completion = _introCompletion;
                _introCompletion = null;
                IsPlaying = false;
                completion?.Invoke(new CinematicPlaybackCompletion(kind));
            }

            public void EmitIntro(CinematicPlaybackCompletionKind kind)
            {
                IsPlaying = false;
                _introCompletion?.Invoke(new CinematicPlaybackCompletion(kind));
            }

            public void CompleteOutro(
                CinematicPlaybackCompletionKind kind = CinematicPlaybackCompletionKind.Completed)
            {
                var completion = _outroCompletion;
                _outroCompletion = null;
                IsPlaying = false;
                completion?.Invoke(new CinematicPlaybackCompletion(kind));
            }
        }

        private sealed class ManualSelectedAspectProvider : ICinematicSelectedAspectProvider
        {
            private readonly float _aspectRatio;

            public ManualSelectedAspectProvider(float aspectRatio)
            {
                _aspectRatio = aspectRatio;
            }

            public int CallCount { get; private set; }

            public bool TryGetAspectRatio(out float aspectRatio)
            {
                CallCount++;
                aspectRatio = _aspectRatio;
                return _aspectRatio > 0f;
            }
        }

        private sealed class ManualViewportProvider : IResolvedCinematicViewportProvider
        {
            private readonly Vector2 _size;

            public ManualViewportProvider(Vector2 size)
            {
                _size = size;
            }

            public bool TryGetAspectRatio(out float aspectRatio)
            {
                if (TryGetViewportSize(out var size))
                {
                    aspectRatio = size.x / size.y;
                    return true;
                }

                aspectRatio = 0f;
                return false;
            }

            public bool TryGetViewportSize(out Vector2 size)
            {
                size = _size;
                return _size.x > 0f && _size.y > 0f;
            }
        }

        private sealed class MutableViewportProvider : IResolvedCinematicViewportProvider
        {
            public MutableViewportProvider(Vector2 size)
            {
                Size = size;
            }

            public Vector2 Size { get; set; }

            public bool TryGetAspectRatio(out float aspectRatio)
            {
                if (TryGetViewportSize(out var size))
                {
                    aspectRatio = size.x / size.y;
                    return true;
                }

                aspectRatio = 0f;
                return false;
            }

            public bool TryGetViewportSize(out Vector2 size)
            {
                size = Size;
                return Size.x > 0f && Size.y > 0f;
            }
        }

        private sealed class RecordingStageLaunchRouter : IStageLaunchRouter
        {
            private readonly List<StageNavigationRequest> _requests = new();

            public IReadOnlyList<StageNavigationRequest> Requests => _requests;

            public int AttemptCount { get; private set; }

            public bool RejectOnLaunch { get; set; }

            public Exception ExceptionToThrow { get; set; }

            public void Launch(StageNavigationRequest request)
            {
                AttemptCount++;
                if (RejectOnLaunch)
                {
                    throw new ImmediateRouteRejectedException();
                }

                if (ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }

                _requests.Add(request);
            }
        }

        private sealed class ImmediateRouteRejectedException : InvalidOperationException
        {
            public ImmediateRouteRejectedException()
                : base("Injected immediate route rejection.")
            {
            }
        }

        private sealed class CinematicLaunchHarness : IDisposable
        {
            private readonly TestKeys _keys;

            public CinematicLaunchHarness(string testName)
            {
                _keys = TestKeys.Create(testName);
                StageLaunchContextStore.Clear();
                RawSaveStore = new SaveSlotStore(_keys.SaveKey);
                ProgressStore = new RecordingUpdateSaveSlotStore(RawSaveStore);
                HandoffStore = new ControllableCampaignLaunchHandoffStore();
                var activeStorage = new PlayerPrefsActiveSlotStorage(_keys.ActiveKey);
                ActiveSlotProvider = new ActiveSlotProvider(activeStorage);
                SaveStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                    ProgressStore,
                    activeStorage,
                    HandoffStore);
                Route = new RecordingStageLaunchRouter();
                Player = new ManualSlotCinematicPlayer { HasIntroClipValue = true };
                Request = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "main-menu-new-game",
                    StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay));
                SaveStore.SaveSlot(CreateSlot(1, Request.StageId));
                Assert.That(
                    HandoffStore.TryBegin(
                        1,
                        Request.StageId,
                        Request.NavigationKind,
                        Request.Source,
                        out var handoff),
                    Is.True);
                Handoff = handoff;
                Router = new CinematicStageLaunchRouter(
                    Route,
                    SaveStore,
                    HandoffStore,
                    Player);
            }

            public SaveSlotStore RawSaveStore { get; }

            public RecordingUpdateSaveSlotStore ProgressStore { get; }

            public CampaignLaunchStateRepairingCampaignSaveSlotStore SaveStore { get; }

            public ControllableCampaignLaunchHandoffStore HandoffStore { get; }

            public ActiveSlotProvider ActiveSlotProvider { get; }

            public RecordingStageLaunchRouter Route { get; }

            public ManualSlotCinematicPlayer Player { get; }

            public StageNavigationRequest Request { get; }

            public CampaignLaunchHandoff Handoff { get; }

            public CinematicStageLaunchRouter Router { get; }

            public void StartCinematic()
            {
                Router.Launch(Request);
                Assert.That(Player.PlayIntroCallCount, Is.EqualTo(1));
            }

            public void Dispose()
            {
                StageLaunchContextStore.Clear();
                _keys.Clear();
            }
        }

        private sealed class ControllableCampaignLaunchHandoffStore : ICampaignLaunchHandoffStore
        {
            private CampaignLaunchHandoff _pending;

            public CampaignLaunchHandoff Current => _pending;

            public int ClearAttemptCount { get; private set; }

            public int ClearSuccessCount { get; private set; }

            public bool TryBegin(
                int slotNumber,
                StageId stageId,
                StageNavigationKind navigationKind,
                string source,
                out CampaignLaunchHandoff handoff)
            {
                if (_pending != null)
                {
                    handoff = _pending;
                    return false;
                }

                _pending = new CampaignLaunchHandoff(
                    slotNumber,
                    stageId,
                    navigationKind,
                    source,
                    Guid.NewGuid());
                handoff = _pending;
                return true;
            }

            public bool TryPeek(out CampaignLaunchHandoff handoff)
            {
                handoff = _pending;
                return handoff != null;
            }

            public bool TryClear(Guid token)
            {
                ClearAttemptCount++;
                if (_pending == null || _pending.Token != token)
                {
                    return false;
                }

                _pending = null;
                ClearSuccessCount++;
                return true;
            }

            public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
            {
                if (_pending == null || _pending.Token != token)
                {
                    handoff = null;
                    return false;
                }

                handoff = _pending;
                _pending = null;
                return true;
            }

            public void ForcePending(CampaignLaunchHandoff handoff)
            {
                _pending = handoff ?? throw new ArgumentNullException(nameof(handoff));
            }

            public void ResetClearCounts()
            {
                ClearAttemptCount = 0;
                ClearSuccessCount = 0;
            }
        }

        private sealed class RecordingUpdateSaveSlotStore : ICampaignSaveSlotStore
        {
            private readonly ICampaignSaveSlotStore _inner;

            public RecordingUpdateSaveSlotStore(ICampaignSaveSlotStore inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public int UpdateSlotCallCount { get; private set; }

            public List<int> UpdatedSlotNumbers { get; } = new();

            public string DiagnosticsKey => _inner.DiagnosticsKey;

            public CampaignSaveLoadReport LastCampaignLoadReport => _inner.LastCampaignLoadReport;

            public SaveSlotData[] LoadAll()
            {
                return _inner.LoadAll();
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                return _inner.LoadAllWithReport();
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                return _inner.LoadSlot(slotNumber);
            }

            public void SaveSlot(SaveSlotData slot)
            {
                _inner.SaveSlot(slot);
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                return _inner.InitializeNewGame(slotNumber, sequenceResolver, lastPlayedAt);
            }

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
            {
                UpdateSlotCallCount++;
                UpdatedSlotNumbers.Add(slotNumber);
                _inner.UpdateSlot(slotNumber, mutation);
            }

            public void DeleteSlot(int slotNumber)
            {
                _inner.DeleteSlot(slotNumber);
            }

            public void ClearAll()
            {
                _inner.ClearAll();
            }
        }

        private sealed class RecordingMainMenuReturnRouter : IMainMenuReturnRouter
        {
            public int ReturnCallCount { get; private set; }

            public void ReturnToMainMenu()
            {
                ReturnCallCount++;
            }
        }
    }
}
