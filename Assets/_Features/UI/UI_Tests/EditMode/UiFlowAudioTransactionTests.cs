using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiFlowAudioTransactionTests
    {
        [Test]
        public void UiFlowAudioOutcomeClassifier_BackPlusPopAndPopupOpen_ResolvesToNavigateBack()
        {
            var result = UiFlowAudioOutcomeClassifier.Classify(
                UiFlowAudioIntentKind.Back,
                new[]
                {
                    CreateScreenTransitionDelta(ScreenTransitionKind.Pop, ScreenId.Settings, ScreenId.Gameplay),
                    CreatePopupOpenDelta(PopupId.Pause),
                },
                isAborted: false,
                UiFlowAudioSilenceReason.None);

            Assert.That(result.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateBack));
            Assert.That(result.EmittedCueId, Is.EqualTo(UiAudioCueId.NavigateBack));
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_OpenForwardPlusScreenPush_ResolvesToNavigateForward()
        {
            var result = UiFlowAudioOutcomeClassifier.Classify(
                UiFlowAudioIntentKind.OpenForward,
                new[]
                {
                    CreateScreenTransitionDelta(ScreenTransitionKind.Push, ScreenId.Gameplay, ScreenId.Settings),
                },
                isAborted: false,
                UiFlowAudioSilenceReason.None);

            Assert.That(result.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateForward));
            Assert.That(result.EmittedCueId, Is.EqualTo(UiAudioCueId.NavigateForward));
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_ConfirmWithPopupCompletedAndNestedTransition_ResolvesToConfirm()
        {
            var result = UiFlowAudioOutcomeClassifier.Classify(
                UiFlowAudioIntentKind.Confirm,
                new[]
                {
                    CreatePopupCompletionDelta(PopupId.Confirm, PopupCompletionKind.Confirmed, PopupCloseReason.UserAction),
                    CreateScreenTransitionDelta(ScreenTransitionKind.Replace, ScreenId.Settings, ScreenId.StageResult),
                },
                isAborted: false,
                UiFlowAudioSilenceReason.None);

            Assert.That(result.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Confirm));
            Assert.That(result.EmittedCueId, Is.EqualTo(UiAudioCueId.Confirm));
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_CancelWithCleanup_ResolvesToCancel()
        {
            var result = UiFlowAudioOutcomeClassifier.Classify(
                UiFlowAudioIntentKind.Cancel,
                new[]
                {
                    CreatePopupCompletionDelta(PopupId.Confirm, PopupCompletionKind.Cancelled, PopupCloseReason.Back),
                    UiFlowAudioDelta.FromRootScreenSet(ScreenId.Gameplay),
                },
                isAborted: false,
                UiFlowAudioSilenceReason.None);

            Assert.That(result.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Cancel));
            Assert.That(result.EmittedCueId, Is.EqualTo(UiAudioCueId.Cancel));
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_SystemPresentation_DefaultsToSilent()
        {
            var result = UiFlowAudioOutcomeClassifier.Classify(
                UiFlowAudioIntentKind.SystemPresentation,
                new[]
                {
                    UiFlowAudioDelta.FromRootScreenSet(ScreenId.Gameplay),
                    CreatePopupOpenDelta(PopupId.Tooltip),
                },
                isAborted: false,
                UiFlowAudioSilenceReason.None);

            Assert.That(result.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Silent));
            Assert.That(result.EmittedCueId, Is.Null);
            Assert.That(result.SilenceReason, Is.EqualTo(UiFlowAudioSilenceReason.SystemPresentationPolicy));
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_SystemPresentation_GameClearRootScreen_EmitsGameClear()
        {
            AssertSystemPresentationResult(ScreenId.GameClear, UiFlowAudioOutcomeKind.GameClear, UiAudioCueId.GameClear);
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_SystemPresentation_StageResultRootScreen_EmitsStageClear()
        {
            AssertSystemPresentationResult(ScreenId.StageResult, UiFlowAudioOutcomeKind.StageClear, UiAudioCueId.StageClear);
        }

        [Test]
        public void UiFlowAudioOutcomeClassifier_SystemPresentation_LevelFailedRootScreen_EmitsLevelFailed()
        {
            AssertSystemPresentationResult(ScreenId.LevelFailed, UiFlowAudioOutcomeKind.LevelFailed, UiAudioCueId.LevelFailed);
        }

        private static void AssertSystemPresentationResult(
            ScreenId screenId,
            UiFlowAudioOutcomeKind expectedOutcomeKind,
            UiAudioCueId expectedCueId)
        {
            var result = UiFlowAudioOutcomeClassifier.Classify(
                UiFlowAudioIntentKind.SystemPresentation,
                new[]
                {
                    UiFlowAudioDelta.FromRootScreenSet(screenId),
                    CreatePopupOpenDelta(PopupId.Tooltip),
                },
                isAborted: false,
                UiFlowAudioSilenceReason.None);

            Assert.That(result.OutcomeKind, Is.EqualTo(expectedOutcomeKind));
            Assert.That(result.EmittedCueId, Is.EqualTo(expectedCueId));
            Assert.That(result.SilenceReason, Is.EqualTo(UiFlowAudioSilenceReason.None));
        }

        [Test]
        public void UiFlowAudioTransaction_RepeatedFinalizeAndAbort_AreIdempotent()
        {
            var transaction = new UiFlowAudioTransaction(UiFlowAudioIntentKind.OpenForward);
            transaction.Record(CreatePopupOpenDelta(PopupId.Confirm));
            transaction.Abort(UiFlowAudioSilenceReason.Cleanup);
            transaction.Abort(UiFlowAudioSilenceReason.FailedOperation);
            transaction.Leave();

            var firstTrace = transaction.FinalizeTrace();
            var secondTrace = transaction.FinalizeTrace();

            Assert.That(firstTrace, Is.SameAs(secondTrace));
            Assert.That(firstTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Silent));
            Assert.That(firstTrace.SilenceReason, Is.EqualTo(UiFlowAudioSilenceReason.Cleanup));
        }

        private static UiFlowAudioDelta CreateScreenTransitionDelta(
            ScreenTransitionKind kind,
            ScreenId previousScreenId,
            ScreenId currentScreenId)
        {
            return UiFlowAudioDelta.FromScreenTransition(new ScreenTransitionedEvent(
                kind,
                CreateEntry(previousScreenId),
                CreateEntry(currentScreenId)));
        }

        private static UiFlowAudioDelta CreatePopupOpenDelta(PopupId popupId)
        {
            return UiFlowAudioDelta.FromPopupOpened(new PopupOpenedEvent(new PopupEntry(
                new PopupInstanceId(1),
                popupId,
                CreatePayload(popupId),
                default,
                null)));
        }

        private static UiFlowAudioDelta CreatePopupCompletionDelta(
            PopupId popupId,
            PopupCompletionKind completionKind,
            PopupCloseReason closeReason)
        {
            var entry = new PopupEntry(
                new PopupInstanceId(1),
                popupId,
                CreatePayload(popupId),
                default,
                null);
            var completion = new PopupCompletion(entry.InstanceId, popupId, completionKind, closeReason);
            return UiFlowAudioDelta.FromPopupCompleted(new PopupCompletedEvent(entry, completion));
        }

        private static ScreenEntry? CreateEntry(ScreenId screenId)
        {
            if (screenId == ScreenId.None)
            {
                return null;
            }

            return new ScreenEntry(
                new ScreenInstanceId(1),
                screenId,
                CreatePayload(screenId),
                new ScreenPolicy(ScreenPolicyClass.GameplayAdjacentOverlay, ScreenRetentionMode.RetainMountedHistory, ScreenBackAction.Pop, HudShellMode.Visible, true),
                screenId.ToString());
        }

        private static IScreenPayload CreatePayload(ScreenId screenId)
        {
            return screenId switch
            {
                ScreenId.Gameplay => GameplayRootPayload.Default,
                ScreenId.Settings => SettingsScreenPayload.Default,
                ScreenId.StageResult => new StageResultScreenPayload(
                    "Title",
                    "Summary",
                    "Detail",
                    "Continue",
                    Game.Feature.Stages.StageNavigationRequest.None,
                    Game.Feature.Stages.StageNavigationRequest.None,
                    Game.Feature.Stages.StageNavigationRequest.None),
                _ => GameplayRootPayload.Default,
            };
        }

        private static IPopupPayload CreatePayload(PopupId popupId)
        {
            return popupId switch
            {
                PopupId.Pause => PausePopupPayload.Default,
                PopupId.Confirm => new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                PopupId.Tooltip => new TooltipPopupPayload("Tip", "Body"),
                _ => new TooltipPopupPayload("Tip", "Body"),
            };
        }
    }
}
