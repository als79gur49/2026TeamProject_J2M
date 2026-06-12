using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UIFlowCoordinatorTests
    {
        [Test]
        public void UIFlowCoordinator_HandleBack_UsesPopupFirstThenScreenThenPausePopup()
        {
            var pauseService = new FakeGameplayPauseService();
            var runtimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                runtimeFactory,
                out var screenController,
                out var popupController,
                out _);

            coordinator.Initialize();
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

            Assert.That(coordinator.OpenSettingsScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksHudInteraction, Is.True);
        }

        [Test]
        public void UIFlowCoordinator_ScreenTransitions_CloseCurrentPopupStack_Deterministically()
        {
            var pauseService = new FakeGameplayPauseService();
            var runtimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                runtimeFactory,
                out var screenController,
                out var popupController,
                out _);

            coordinator.Initialize();

            var completions = new List<PopupCompletion>();
            Assert.That(coordinator.RequestTooltipPopup(
                new TooltipPopupPayload("Tip", "Body"),
                completions.Add), Is.True);

            Assert.That(coordinator.OpenSettingsScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(completions, Has.Count.EqualTo(1));
            Assert.That(completions[0].CloseReason, Is.EqualTo(PopupCloseReason.ScreenTransition));
        }

        [Test]
        public void UIFlowCoordinator_RequestConfirm_ExposesStageSixValidationDefaults()
        {
            var pauseService = new FakeGameplayPauseService();
            var runtimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                runtimeFactory,
                out _,
                out _,
                out _);

            coordinator.Initialize();

            var confirmResults = new List<PopupCompletion>();
            Assert.That(coordinator.RequestConfirmPopup(
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", true),
                confirmResults.Add), Is.True);
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(confirmResults, Has.Count.EqualTo(1));
            Assert.That(confirmResults[0].CompletionKind, Is.EqualTo(PopupCompletionKind.Cancelled));
        }

        [Test]
        public void UIFlowCoordinator_HandleScreenActionRequested_UsesCentralizedTransitionRules()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                new ManualGameplayUiPresentationSource(),
                out var screenController,
                out var popupController,
                out _);

            coordinator.Initialize();
            Assert.That(coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Body")), Is.True);
            Assert.That(popupController.PopupCount, Is.EqualTo(1));

            coordinator.HandleScreenActionRequested(ScreenAction.Push(
                new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));

            coordinator.HandleScreenActionRequested(ScreenAction.Popup(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false))));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(popupController.PopupCount, Is.EqualTo(1));
            Assert.That(popupController.TopPopup.HasValue, Is.True);
            Assert.That(popupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
        }

        [Test]
        public void UIFlowCoordinator_RequestPausePopup_DoesNotDuplicateExistingPausePopup()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                new ManualGameplayUiPresentationSource(),
                out _,
                out var popupController,
                out _);

            coordinator.Initialize();

            Assert.That(coordinator.RequestPausePopup(), Is.True);
            Assert.That(coordinator.RequestPausePopup(), Is.False);
            Assert.That(popupController.PopupCount, Is.EqualTo(1));
            Assert.That(pauseService.PauseCallCount, Is.EqualTo(1));
        }

        [Test]
        public void UIFlowCoordinator_PausePopupSettingsRequested_EmitsSingleForwardCueWithoutPopupCompletionDoublePlay()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out var screenController,
                out _,
                out var uiAudioPort);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);

            uiAudioPort.Clear();
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateForward }));
            Assert.That(coordinator.LastFlowAudioTrace, Is.Not.Null);
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.OpenForward));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateForward));
        }

        [Test]
        public void UIFlowCoordinator_PausePopupSettingsRequested_OpensSettingsWithoutResuming_AndMarksReturnMode()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out var screenController,
                out var popupController);

            coordinator.Initialize();

            Assert.That(coordinator.RequestPausePopup(), Is.True);
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.EqualTo(0));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("RestorePausePopupAfterBack"));
        }

        [Test]
        public void UIFlowCoordinator_PauseOriginSettingsBack_RestoresFreshPausePopupExactlyOnce_AndClearsReturnMode()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out var screenController,
                out var popupController);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var initialPauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;

            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("RestorePausePopupAfterBack"));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(popupRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.PopupId == PopupId.Pause), Has.Count.EqualTo(2));
            Assert.That(popupRuntimeFactory.CreatedRuntimes[^1].Runtime, Is.Not.SameAs(initialPauseRuntime));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));
        }

        [Test]
        public void UIFlowCoordinator_GameplayOriginSettingsBack_ReturnsDirectlyToGameplay_WithoutPauseRestore()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out var screenController,
                out var popupController);

            coordinator.Initialize();

            Assert.That(coordinator.OpenSettingsScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(pauseService.IsPaused, Is.False);
        }

        [Test]
        public void UIFlowCoordinator_PausePopupClosed_IsResumeEquivalent_AndClearsReturnMode()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out _,
                out var popupController);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);

            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("RestorePausePopupAfterBack"));

            Assert.That(coordinator.RequestPausePopup(), Is.True);
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Closed);

            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));
        }

        [Test]
        public void UIFlowCoordinator_PauseOriginSettings_ForcedScreenTransition_ClearsReturnMode_AndDoesNotRestoreLaterPausePopup()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out var screenController,
                out var popupController);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);

            coordinator.HandleScreenActionRequested(ScreenAction.Push(
                new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-forced")));
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
        }

        [Test]
        public void UIFlowCoordinator_PauseOriginSettings_StageClearAndDispose_ClearReturnMode()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out var screenController,
                out _);

            try
            {
                coordinator.Initialize();
                Assert.That(coordinator.RequestPausePopup(), Is.True);
                popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);
                Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("RestorePausePopupAfterBack"));

                presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
                presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));
                Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
                Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));

                Assert.That(coordinator.OpenSettingsScreen(), Is.True);
                Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));

                coordinator.Dispose();
                Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));
            }
            finally
            {
                coordinator.Dispose();
            }
        }

        [Test]
        public void UIFlowCoordinator_TopmostTooltipRequest_IsSuppressedAcrossCoordinatorAndScreenActions()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                new ManualGameplayUiPresentationSource(),
                out _,
                out var popupController,
                out _);

            coordinator.Initialize();

            Assert.That(coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Body")), Is.True);
            Assert.That(coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Body")), Is.False);
            Assert.That(popupController.PopupCount, Is.EqualTo(1));

            coordinator.HandleScreenActionRequested(ScreenAction.Popup(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Body"))));

            Assert.That(popupController.PopupCount, Is.EqualTo(1));
            Assert.That(popupController.TopPopup.HasValue, Is.True);
            Assert.That(popupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Tooltip));
        }

        [Test]
        public void UIFlowCoordinator_DuplicateTooltipOpen_AndConsumePaths_RemainSilent()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out _,
                out _,
                out var uiAudioPort);

            coordinator.Initialize();

            Assert.That(coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Body")), Is.True);
            uiAudioPort.Clear();

            Assert.That(coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Body")), Is.False);
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Silent));

            Assert.That(coordinator.RequestConfirmPopup(new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)), Is.True);
            uiAudioPort.Clear();

            Assert.That(coordinator.HandlePopupBackdropClicked(), Is.True);
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.Back));
            Assert.That(coordinator.LastFlowAudioTrace.SilenceReason, Is.EqualTo(UiFlowAudioSilenceReason.NoVisibleDelta));
        }

        [Test]
        public void UIFlowCoordinator_ScreenPop_AndPopupCompletionCueMapping_StayStable()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                new ManualGameplayUiPresentationSource(),
                out var screenController,
                out _,
                out var uiAudioPort,
                out _);

            coordinator.Initialize();

            Assert.That(coordinator.OpenSettingsScreen(), Is.True);
            uiAudioPort.Clear();
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateBack }));

            Assert.That(coordinator.RequestConfirmPopup(new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)), Is.True);
            uiAudioPort.Clear();
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Confirmed);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));

            Assert.That(coordinator.RequestConfirmPopup(new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)), Is.True);
            uiAudioPort.Clear();
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Cancel }));

            Assert.That(coordinator.RequestPausePopup(), Is.True);
            uiAudioPort.Clear();
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Resumed);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));
        }

        [Test]
        public void UIFlowCoordinator_PauseOriginSettingsBack_EmitsExactlyOneNavigateBack_WithCompositeTrace()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out var screenController,
                out var popupController,
                out var uiAudioPort);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.SettingsRequested);

            uiAudioPort.Clear();
            Assert.That(coordinator.HandleBackRequested(), Is.True);

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateBack }));
            Assert.That(coordinator.LastFlowAudioTrace, Is.Not.Null);
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.Back));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateBack));
            Assert.That(coordinator.LastFlowAudioTrace.MaxJoinedDepth, Is.EqualTo(1));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.Some.Matches<UiFlowAudioDelta>(delta =>
                    delta.Kind == UiFlowAudioDeltaKind.ScreenTransition &&
                    delta.ScreenTransitionKind == ScreenTransitionKind.Pop));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.Some.Matches<UiFlowAudioDelta>(delta =>
                    delta.Kind == UiFlowAudioDeltaKind.PopupOpened &&
                    delta.PopupId == PopupId.Pause));
        }

        [Test]
        public void UIFlowCoordinator_HandleBackFromGameplay_UsesSingleForwardOutcome_WhenPausePopupOpens()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out _,
                out var popupController,
                out var uiAudioPort);

            coordinator.Initialize();
            uiAudioPort.Clear();

            Assert.That(coordinator.HandleBackRequested(), Is.True);

            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateForward }));
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.Back));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateForward));
        }

        [Test]
        public void UIFlowCoordinator_ScreenTransitionCleanup_DuringForwardTransaction_DoesNotCreateExtraCue()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                out _,
                out _,
                out var uiAudioPort);

            coordinator.Initialize();
            Assert.That(coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Body")), Is.True);
            uiAudioPort.Clear();

            Assert.That(coordinator.OpenSettingsScreen(), Is.True);

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateForward }));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateForward));
        }

        [Test]
        public void UIFlowCoordinator_ConfirmCompletion_CallbackDrivenScreenMutation_StillEmitsSingleConfirmCue()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                new ManualGameplayUiPresentationSource(),
                out var screenController,
                out _,
                out var uiAudioPort,
                out _);

            coordinator.Initialize();
            Assert.That(coordinator.RequestConfirmPopup(
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                _ => coordinator.OpenSettingsScreen()), Is.True);

            uiAudioPort.Clear();
            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Confirmed);

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.Confirm));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Confirm));
            Assert.That(coordinator.LastFlowAudioTrace.MaxJoinedDepth, Is.EqualTo(2));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.Some.Matches<UiFlowAudioDelta>(delta =>
                    delta.Kind == UiFlowAudioDeltaKind.PopupCompleted &&
                    delta.PopupId == PopupId.Confirm &&
                    delta.PopupCompletionKind == PopupCompletionKind.Confirmed));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.Some.Matches<UiFlowAudioDelta>(delta =>
                    delta.Kind == UiFlowAudioDeltaKind.ScreenTransition &&
                    delta.ScreenTransitionKind == ScreenTransitionKind.Push &&
                    delta.CurrentScreenId == ScreenId.Settings));
        }

        [Test]
        public void UIFlowCoordinator_StageClearSystemPresentation_EmitsStageClear_AndRecordsSingleSystemTrace()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out var screenController,
                out var popupController,
                out var uiAudioPort,
                out _);

            coordinator.Initialize();
            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
            uiAudioPort.Clear();

            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.StageClear }));
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.SystemPresentation));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.StageClear));
            Assert.That(coordinator.LastFlowAudioTrace.EmittedCueId, Is.EqualTo(UiAudioCueId.StageClear));
            Assert.That(coordinator.LastFlowAudioTrace.SilenceReason, Is.EqualTo(UiFlowAudioSilenceReason.None));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.Some.Matches<UiFlowAudioDelta>(delta =>
                    delta.Kind == UiFlowAudioDeltaKind.RootScreenSet &&
                    delta.CurrentScreenId == ScreenId.StageResult));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.None.Matches<UiFlowAudioDelta>(delta => delta.Kind == UiFlowAudioDeltaKind.PopupOpened));
        }

        [Test]
        public void UIFlowCoordinator_StageClearedAutoOpensTerminalStageResult_AndConsumesBack()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinatorWithStageLaunchRouter(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out var screenController,
                out var popupController,
                out var stageLaunchRouter);

            coordinator.Initialize();
            Assert.That(coordinator.OpenSettingsScreen(), Is.True);
            Assert.That(screenController.BackStackCount, Is.EqualTo(1));

            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.BackStackCount, Is.EqualTo(0));
            var stageResultRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.StageResult);
            var stagePayload = stageResultRecord.Request.Payload as StageResultScreenPayload;
            Assert.That(stagePayload, Is.Not.Null);
            Assert.That(stagePayload.ContinueLabel, Is.EqualTo("Collect"));
            Assert.That(stagePayload.ContinueStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stagePayload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
            Assert.That(stagePayload.RetryStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stagePayload.RetryStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(screenController.CurrentEntry.HasValue, Is.True);
            Assert.That(screenController.CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.CurrentEntry.Value.Payload, Is.TypeOf<StageResultScreenPayload>());
            Assert.That(popupController.TopPopup.HasValue, Is.False);
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput, Is.True);

            coordinator.HandleScreenActionRequested(ScreenAction.LaunchStage(stagePayload.ContinueStageRequest));
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Continue));

            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.StageResult), Has.Count.EqualTo(1));
        }

        [Test]
        public void UIFlowCoordinator_FinalStageClearedAutoOpensTerminalGameClear_WithoutRewardPopup()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            var mainMenuReturnRouter = new FakeMainMenuReturnRouter();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                mainMenuReturnRouter,
                out var screenController,
                out var popupController,
                out var uiAudioPort,
                out var stageLaunchRouter);

            coordinator.Initialize();
            uiAudioPort.Clear();

            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(
                tickIndex: 9,
                stageIdValue: "stage-4-2"));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.GameClear));
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.GameClear }));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.GameClear));
            Assert.That(coordinator.LastFlowAudioTrace.EmittedCueId, Is.EqualTo(UiAudioCueId.GameClear));
            Assert.That(screenController.CurrentEntry.HasValue, Is.True);
            Assert.That(screenController.CurrentEntry.Value.Payload, Is.TypeOf<GameClearScreenPayload>());
            var gameClearPayload = screenController.CurrentEntry.Value.Payload as GameClearScreenPayload;
            Assert.That(gameClearPayload.TitleText, Is.EqualTo("Game Clear"));
            Assert.That(gameClearPayload.MainLabel, Is.EqualTo("Main"));
            Assert.That(popupController.TopPopup.HasValue, Is.False);
            Assert.That(popupController.PopupCount, Is.EqualTo(0), "Final-stage terminal screen selection bypasses Reward popup in current flow; do not extract or change this policy in PR-1.");
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.StageResult), Is.Empty);
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.GameClear), Has.Count.EqualTo(1));
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.GameClear));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput, Is.True);

            var gameClearRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.GameClear);
            gameClearRecord.Runtime.Emit(ScreenAction.ReturnToMainMenu());
            Assert.That(stageLaunchRouter.Requests, Is.Empty);
            Assert.That(mainMenuReturnRouter.ReturnCallCount, Is.EqualTo(1));
        }

        [Test]
        public void UIFlowCoordinator_LevelFailedEvent_OpensTerminalLevelFailedOnce()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out var screenController,
                out var popupController,
                out var uiAudioPort);

            coordinator.Initialize();
            Assert.That(coordinator.OpenSettingsScreen(), Is.True);
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            uiAudioPort.Clear();

            var restartRequest = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-1-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level");
            var payload = new LevelFailedScreenPayload(
                "Level Failed",
                "All chances were used.",
                "Restart Level",
                "Main",
                restartRequest);

            presentationSource.PublishLevelFailed(payload);
            presentationSource.PublishLevelFailed(payload);

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.LevelFailed));
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.LevelFailed }));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.LevelFailed));
            Assert.That(coordinator.LastFlowAudioTrace.EmittedCueId, Is.EqualTo(UiAudioCueId.LevelFailed));
            Assert.That(screenController.BackStackCount, Is.EqualTo(0));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            var records = screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.LevelFailed);
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(records[0].Request.Payload, Is.SameAs(payload));
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.LevelFailed));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput, Is.True);
        }

        [Test]
        public void UIFlowCoordinator_LevelFailedActions_LaunchSavedRestartRequest_AndUseMainRouter()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            var mainMenuReturnRouter = new FakeMainMenuReturnRouter();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                mainMenuReturnRouter,
                out _,
                out _,
                out _,
                out var stageLaunchRouter);

            coordinator.Initialize();
            var restartRequest = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-1-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level");
            presentationSource.PublishLevelFailed(new LevelFailedScreenPayload(
                "Level Failed",
                "All chances were used.",
                "Restart Level",
                "Main",
                restartRequest));
            var levelFailedRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.LevelFailed);

            levelFailedRecord.Runtime.Emit(ScreenAction.LaunchStage(restartRequest));
            levelFailedRecord.Runtime.Emit(ScreenAction.ReturnToMainMenu());

            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(restartRequest.StageId));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(stageLaunchRouter.Requests[0].Source, Is.EqualTo("level-failed-restart-level"));
            Assert.That(mainMenuReturnRouter.ReturnCallCount, Is.EqualTo(1));
        }

        [Test]
        public void UIFlowCoordinator_PausePopupRetry_LaunchesCurrentStageRetry_AndResumesPause()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out _,
                out var popupController,
                out _,
                out var stageLaunchRouter);
            var stageId = StageId.CreateOrThrow("stage-1-1");

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            Assert.That(coordinator.RequestPausePopup(), Is.True);

            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.RetryRequested);

            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(stageId));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(stageLaunchRouter.Requests[0].Source, Is.EqualTo("pause-retry"));
            Assert.That(stageLaunchRouter.Requests[0].TransitionHint.Kind, Is.EqualTo(StageTransitionKind.StageRetryManual));
        }

        [Test]
        public void UIFlowCoordinator_PausePopupMainMenu_UsesMainRouter_AndResumesPause()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            var mainMenuReturnRouter = new FakeMainMenuReturnRouter();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                screenRuntimeFactory,
                presentationSource,
                mainMenuReturnRouter,
                out _,
                out var popupController,
                out _,
                out var stageLaunchRouter);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);

            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.MainMenuRequested);

            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(0));
            Assert.That(mainMenuReturnRouter.ReturnCallCount, Is.EqualTo(1));
        }

        private static UIFlowCoordinator CreateCoordinatorWithStageLaunchRouter(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            out ScreenController screenController,
            out PopupController popupController,
            out FakeStageLaunchRouter stageLaunchRouter)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out screenController,
                out popupController,
                out _,
                out stageLaunchRouter);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            FakeMainMenuReturnRouter mainMenuReturnRouter,
            out ScreenController screenController,
            out PopupController popupController,
            out RecordingUiAudioPort uiAudioPort,
            out FakeStageLaunchRouter stageLaunchRouter)
        {
            screenController = new ScreenController(screenRuntimeFactory);
            popupController = new PopupController(runtimeFactory);
            uiAudioPort = new RecordingUiAudioPort();
            stageLaunchRouter = new FakeStageLaunchRouter();

            return new UIFlowCoordinator(
                screenController,
                popupController,
                new UIBlockPolicy(),
                pauseService,
                presentationSource,
                uiAudioPort,
                stageLaunchRouter,
                mainMenuReturnRouter);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            out ScreenController screenController,
            out PopupController popupController,
            out RecordingUiAudioPort uiAudioPort,
            out FakeStageLaunchRouter stageLaunchRouter)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                screenRuntimeFactory,
                presentationSource,
                new FakeMainMenuReturnRouter(),
                out screenController,
                out popupController,
                out uiAudioPort,
                out stageLaunchRouter);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            out ScreenController screenController,
            out PopupController popupController)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out screenController,
                out popupController,
                out _,
                out _);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            out ScreenController screenController,
            out PopupController popupController,
            out RecordingUiAudioPort uiAudioPort)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                screenRuntimeFactory,
                presentationSource,
                out screenController,
                out popupController,
                out uiAudioPort,
                out _);
        }

        private static UIFlowCoordinator CreateCoordinatorWithStageLaunchRouter(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            out ScreenController screenController,
            out PopupController popupController,
            out FakeStageLaunchRouter stageLaunchRouter)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                new FakeScreenRuntimeFactory(),
                new ManualGameplayUiPresentationSource(),
                out screenController,
                out popupController,
                out _,
                out stageLaunchRouter);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            out ScreenController screenController,
            out PopupController popupController,
            out RecordingUiAudioPort uiAudioPort)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                new FakeScreenRuntimeFactory(),
                new ManualGameplayUiPresentationSource(),
                out screenController,
                out popupController,
                out uiAudioPort,
                out _);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            out ScreenController screenController,
            out PopupController popupController)
        {
            return CreateCoordinator(
                pauseService,
                runtimeFactory,
                out screenController,
                out popupController,
                out _);
        }

        private static UIPresentationSnapshot CreateSnapshotForStage(StageId stageId)
        {
            return new UIPresentationSnapshot(
                UIPresentationSnapshot.Empty.Tick,
                UIPresentationSnapshot.Empty.Interaction,
                new UIStageSlice(stageId, stageId.Value),
                UIPresentationSnapshot.Empty.Player,
                UIPresentationSnapshot.Empty.Notifications);
        }

        private static UITickEventBatch CreateStageClearedBatch(int tickIndex)
        {
            return new UITickEventBatch(
                tickIndex,
                new[]
                {
                    new UITickEvent(
                        new UITickEventKey(
                            tickIndex,
                            UITickEventKind.StageCleared,
                            actorEntityId: 0,
                            actionKind: Game.Feature.Gameplay.UIAccess.Models.GameplayUiActionKind.None,
                            actionSequence: 0,
                            resolutionKind: Game.Feature.Gameplay.UIAccess.Models.GameplayUiActionResolutionKind.None)),
                });
        }

        private static MinimalStageCompletionReadModel CreateMinimalStageCompletionReadModel(
            int tickIndex,
            string stageIdValue = "payload-stage")
        {
            var stageId = StageId.CreateOrThrow(stageIdValue);
            var nextStageRequest = StageNavigationRequest.None;
            var continueRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Continue,
                "stage-result-continue",
                StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));
            var retryRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "stage-result-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual));
            var result = new MinimalStageCompletionResult(
                stageId,
                new StageRunId("run-01"),
                new StageCompletionAttemptId("attempt-01"),
                StageTerminalReason.Cleared,
                wasCleared: true,
                tickIndex,
                new StageObjectiveProgressSnapshot(true, true, true, true, 1, 1),
                StageClearSource.Objective);

            return new MinimalStageCompletionReadModel(
                stageId,
                "Payload Stage",
                result,
                "Collect",
                continueRequest,
                retryRequest,
                nextStageRequest);
        }

        private static string ReadPauseReturnModeName(UIFlowCoordinator coordinator)
        {
            var field = typeof(UIFlowCoordinator).GetField("_pauseReturnMode", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(coordinator)?.ToString() ?? string.Empty;
        }
    }
}
