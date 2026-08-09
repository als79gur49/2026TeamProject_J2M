using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.UI.Tests
{
    public sealed class UIFlowCoordinatorTests
    {
        [SetUp]
        public void ResetTerminalAuthority()
        {
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            EditorDirectPlayContextStore.Clear();
        }

        [TearDown]
        public void ClearTerminalAuthority()
        {
            EditorDirectPlayContextStore.Clear();
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
        }

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
            Assert.That(coordinator.RequestConfirmPopup(
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
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
            Assert.That(coordinator.RequestConfirmPopup(
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)), Is.True);
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
        public void UIFlowCoordinator_RequestPausePopup_ReadsFreshProgressionForEveryOpen()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var progressionSource = new MutablePauseProgressionReadSource
            {
                Snapshot = CreateProgressionSnapshot("stage-1-1"),
            };
            using var coordinator = new UIFlowCoordinator(
                new ScreenController(new FakeScreenRuntimeFactory()),
                new PopupController(popupRuntimeFactory),
                new UIBlockPolicy(),
                pauseService,
                new ManualGameplayUiPresentationSource(),
                new RecordingUiAudioPort(),
                new FakeStageLaunchRouter(),
                new FakeMainMenuReturnRouter(),
                progressionSource);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var firstPayload = (PausePopupPayload)popupRuntimeFactory.CreatedRuntimes[^1].Request.Payload;
            Assert.That(firstPayload.Progression.CurrentStageKey, Is.EqualTo("stage-1-1"));

            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Resumed);
            progressionSource.Snapshot = CreateProgressionSnapshot("stage-1-2");

            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var secondPayload = (PausePopupPayload)popupRuntimeFactory.CreatedRuntimes[^1].Request.Payload;
            Assert.That(secondPayload.Progression.CurrentStageKey, Is.EqualTo("stage-1-2"));
            Assert.That(progressionSource.ReadCallCount, Is.EqualTo(2));
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

                var terminalToken = BeginSameSceneTerminal(
                    TerminalDestinationKind.SameSceneStageResult);
                presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
                presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9, terminalToken));
                Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
                Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));
                CompleteTerminal(terminalToken);

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
        public void UIFlowCoordinator_BackdropConsumePath_RemainsSilent()
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
            Assert.That(coordinator.RequestConfirmPopup(
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)), Is.True);
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
        public void UIFlowCoordinator_StageClearSfx_WaitsForContentEntranceMilestoneAndPlaysExactlyOnce()
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
            var terminalToken = BeginSameSceneTerminal(
                TerminalDestinationKind.SameSceneStageResult);
            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
            uiAudioPort.Clear();

            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9, terminalToken));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            Assert.That(coordinator.LastFlowAudioTrace.RootIntent, Is.EqualTo(UiFlowAudioIntentKind.SystemPresentation));
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Silent));
            Assert.That(coordinator.LastFlowAudioTrace.EmittedCueId, Is.Null);
            Assert.That(
                coordinator.LastFlowAudioTrace.SilenceReason,
                Is.EqualTo(UiFlowAudioSilenceReason.SystemPresentationPolicy));
            Assert.That(
                coordinator.LastFlowAudioTrace.Deltas,
                Has.None.Matches<UiFlowAudioDelta>(delta => delta.Kind == UiFlowAudioDeltaKind.PopupOpened));
            Assert.That(
                TerminalSessionRegistry.TryAdvance(
                    terminalToken,
                    TerminalSessionPhase.ResultBackdropHandoff),
                Is.True);
            Assert.That(
                TerminalSessionRegistry.TryAdvance(
                    terminalToken,
                    TerminalSessionPhase.WaitingResultInteraction),
                Is.True);
            var milestone = new ResultContentEntranceMilestone(
                terminalToken,
                TerminalDestinationKind.SameSceneStageResult);
            Assert.That(coordinator.NotifyResultContentEntranceStarted(milestone), Is.True);
            Assert.That(coordinator.NotifyResultContentEntranceStarted(milestone), Is.False);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.StageClear }));
            CompleteTerminal(terminalToken);
        }

        [Test]
        public void UIFlowCoordinator_StaleOrWrongDestinationEventsAreRejectedBeforeAnyUiMutation()
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
            var currentToken = BeginSameSceneTerminal(
                TerminalDestinationKind.SameSceneStageResult);
            var staleToken = new TerminalSessionToken(
                currentToken.AuthorityGeneration + 1,
                currentToken.Sequence);
            var phaseBefore = TerminalSessionRegistry.Current.Phase;
            var screenBefore = screenController.CurrentScreenId;
            var popupCountBefore = popupController.PopupCount;
            presentationSource.PublishMinimalStageCompletion(
                CreateMinimalStageCompletionReadModel(tickIndex: 91));

            presentationSource.PublishTickEvents(
                CreateStageClearedBatch(tickIndex: 91, staleToken));
            presentationSource.PublishLevelFailed(new LevelFailedScreenPayload(
                "Level Failed",
                "stale",
                "Restart",
                "Main",
                new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Retry,
                    "stale-level-failed",
                    transitionIntent: SceneTransitionIntent.ManualRetry),
                staleToken));
            presentationSource.PublishLevelFailed(new LevelFailedScreenPayload(
                "Level Failed",
                "wrong destination",
                "Restart",
                "Main",
                new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Retry,
                    "wrong-destination-level-failed",
                    transitionIntent: SceneTransitionIntent.ManualRetry),
                currentToken));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(screenBefore));
            Assert.That(popupController.PopupCount, Is.EqualTo(popupCountBefore));
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            Assert.That(TerminalSessionRegistry.Current.Token, Is.EqualTo(currentToken));
            Assert.That(TerminalSessionRegistry.Current.Phase, Is.EqualTo(phaseBefore));
            Assert.That(
                screenRuntimeFactory.CreatedRuntimes.FindAll(
                    record => record.Request.ScreenId == ScreenId.StageResult ||
                              record.Request.ScreenId == ScreenId.LevelFailed),
                Is.Empty);
            CompleteTerminal(currentToken);
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

            var terminalToken = BeginSameSceneTerminal(
                TerminalDestinationKind.SameSceneStageResult);
            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9, terminalToken));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.BackStackCount, Is.EqualTo(0));
            var stageResultRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.StageResult);
            var stagePayload = stageResultRecord.Request.Payload as StageResultScreenPayload;
            Assert.That(stagePayload, Is.Not.Null);
            Assert.That(stagePayload.ContinueStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stagePayload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
            Assert.That(stagePayload.RetryStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stagePayload.RetryStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(screenController.CurrentEntry.HasValue, Is.True);
            Assert.That(screenController.CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.CurrentEntry.Value.Payload, Is.TypeOf<StageResultScreenPayload>());
            Assert.That(popupController.TopPopup.HasValue, Is.False);
            Assert.That(coordinator.HandleBackRequested(), Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput, Is.True);

            coordinator.HandleScreenActionRequested(ScreenAction.LaunchStage(stagePayload.ContinueStageRequest));
            Assert.That(stageLaunchRouter.Requests, Is.Empty);
            CompleteTerminal(terminalToken);
            coordinator.HandleScreenActionRequested(ScreenAction.LaunchStage(stagePayload.ContinueStageRequest));
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Continue));

            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(tickIndex: 9));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9, terminalToken));
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.StageResult), Has.Count.EqualTo(1));
        }

        [Test]
        public void UIFlowCoordinator_CanonicalStageLaunchThrow_CancelsMatchingClaimAndAllowsRetry()
        {
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                sceneHandle: 7501,
                sceneName: "CanonicalLaunchThrowSource");
            var failure = new ApplicationException("Injected synchronous launch failure.");
            var router = new ControlledStageLaunchRouter
            {
                ExceptionToThrow = failure,
            };
            using var coordinator = CreateCoordinator(router);
            var request = CreateCanonicalGameplayEntryRequest("claim-rollback-retry");

            var thrown = Assert.Throws<ApplicationException>(() =>
                coordinator.TryLaunchStage(request));

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);

            router.ExceptionToThrow = null;
            Assert.That(coordinator.TryLaunchStage(request), Is.True);
            Assert.That(router.Requests, Has.Count.EqualTo(1));
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.Claimed));
        }

        [Test]
        public void UIFlowCoordinator_CanonicalStageLaunchThrow_PreservesRouterFailedHoldingCover()
        {
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                sceneHandle: 7502,
                sceneName: "CanonicalLaunchFailHoldingSource");
            const string routerFailure = "Router already owns the opaque failure.";
            var router = new ControlledStageLaunchRouter
            {
                BeforeThrow = () =>
                {
                    Assert.That(
                        SceneEntryPresentationRegistry.TryFailHoldingCover(
                            SceneEntryPresentationRegistry.Current.Token,
                            routerFailure),
                        Is.True);
                },
                ExceptionToThrow = new ApplicationException("Injected router failure."),
            };
            using var coordinator = CreateCoordinator(router);

            Assert.Throws<ApplicationException>(() =>
                coordinator.TryLaunchStage(
                    CreateCanonicalGameplayEntryRequest("preserve-fail-holding")));

            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
            Assert.That(
                SceneEntryPresentationRegistry.Current.FailureReason,
                Is.EqualTo(routerFailure));
        }

        [Test]
        public void UIFlowCoordinator_CanonicalStageLaunchThrow_DoesNotCancelNewerClaim()
        {
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                sceneHandle: 7503,
                sceneName: "CanonicalLaunchStaleTokenSource");
            var request = CreateCanonicalGameplayEntryRequest("stale-token-isolation");
            var newerToken = default(SceneEntrySessionToken);
            var router = new ControlledStageLaunchRouter
            {
                BeforeThrow = () =>
                {
                    Assert.That(
                        SceneEntryPresentationRegistry.TryCancelClaim(
                            SceneEntryPresentationRegistry.Current.Token),
                        Is.True);
                    Assert.That(
                        SceneEntryPresentationRegistry.TryClaim(
                            SceneTransitionIntent.GameplayEntry,
                            request.StageId,
                            generation,
                            out newerToken),
                        Is.True);
                },
                ExceptionToThrow = new ApplicationException("Injected stale-token failure."),
            };
            using var coordinator = CreateCoordinator(router);

            Assert.Throws<ApplicationException>(() => coordinator.TryLaunchStage(request));

            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(SceneEntryPresentationRegistry.Current.Token, Is.EqualTo(newerToken));
            Assert.That(
                SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.Claimed));
        }

        [Test]
        public void UIFlowCoordinator_FinalStageClearedAutoOpensTerminalGameClear_ResultOnly()
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

            var terminalToken = BeginSameSceneTerminal(
                TerminalDestinationKind.SameSceneGameClear);
            presentationSource.PublishMinimalStageCompletion(CreateMinimalStageCompletionReadModel(
                tickIndex: 9,
                stageIdValue: "stage-4-3"));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9, terminalToken));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.GameClear));
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Silent));
            Assert.That(coordinator.LastFlowAudioTrace.EmittedCueId, Is.Null);
            Assert.That(screenController.CurrentEntry.HasValue, Is.True);
            Assert.That(screenController.CurrentEntry.Value.Payload, Is.TypeOf<GameClearScreenPayload>());
            var gameClearPayload = screenController.CurrentEntry.Value.Payload as GameClearScreenPayload;
            Assert.That(
                gameClearPayload.TitleTextDescriptor,
                Is.EqualTo(TerminalResultTextDescriptors.GameClearTitle));
            Assert.That(
                gameClearPayload.MainMenuLabelDescriptor,
                Is.EqualTo(TerminalResultTextDescriptors.MainMenu));
            Assert.That(popupController.TopPopup.HasValue, Is.False);
            Assert.That(popupController.PopupCount, Is.EqualTo(0), "Final-stage terminal screen selection remains result-screen-only; do not extract or change this policy in PR-1.");
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.StageResult), Is.Empty);
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.GameClear), Has.Count.EqualTo(1));
            Assert.That(coordinator.HandleBackRequested(), Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.GameClear));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput, Is.True);
            Assert.That(
                TerminalSessionRegistry.TryAdvance(
                    terminalToken,
                    TerminalSessionPhase.ResultBackdropHandoff),
                Is.True);
            Assert.That(
                TerminalSessionRegistry.TryAdvance(
                    terminalToken,
                    TerminalSessionPhase.WaitingResultInteraction),
                Is.True);
            var milestone = new ResultContentEntranceMilestone(
                terminalToken,
                TerminalDestinationKind.SameSceneGameClear);
            Assert.That(coordinator.NotifyResultContentEntranceStarted(milestone), Is.True);
            Assert.That(coordinator.NotifyResultContentEntranceStarted(milestone), Is.False);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.GameClear }));

            var gameClearRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.GameClear);
            gameClearRecord.Runtime.Emit(ScreenAction.ReturnToMainMenu());
            Assert.That(mainMenuReturnRouter.ReturnCallCount, Is.Zero);
            CompleteTerminal(terminalToken);
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
                "level-failed-restart-level",
                transitionIntent: SceneTransitionIntent.ManualRetry);
            var terminalToken = BeginSameSceneTerminal(
                TerminalDestinationKind.SameSceneLevelFailed);
            var payload = new LevelFailedScreenPayload(
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                restartRequest,
                terminalToken);

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
            Assert.That(coordinator.HandleBackRequested(), Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.LevelFailed));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput, Is.True);
            CompleteTerminal(terminalToken);
        }

        [Test]
        public void UIFlowCoordinator_LevelFailedActions_LaunchSavedRestartRequest_AndBlockConcurrentMainNavigation()
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
                "level-failed-restart-level",
                transitionIntent: SceneTransitionIntent.ManualRetry);
            var terminalToken = BeginSameSceneTerminal(
                TerminalDestinationKind.SameSceneLevelFailed);
            presentationSource.PublishLevelFailed(new LevelFailedScreenPayload(
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                restartRequest,
                terminalToken));
            var levelFailedRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.LevelFailed);

            levelFailedRecord.Runtime.Emit(ScreenAction.LaunchStage(restartRequest));
            levelFailedRecord.Runtime.Emit(ScreenAction.ReturnToMainMenu());
            Assert.That(stageLaunchRouter.Requests, Is.Empty);
            Assert.That(mainMenuReturnRouter.ReturnCallCount, Is.Zero);
            CompleteTerminal(terminalToken);
            levelFailedRecord.Runtime.Emit(ScreenAction.LaunchStage(restartRequest));
            levelFailedRecord.Runtime.Emit(ScreenAction.ReturnToMainMenu());

            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(restartRequest.StageId));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(stageLaunchRouter.Requests[0].Source, Is.EqualTo("level-failed-restart-level"));
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(
                mainMenuReturnRouter.ReturnCallCount,
                Is.Zero,
                "The active ManualRetry entry session must reject concurrent main-menu navigation.");
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
            var directPlayContext = new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignProductionSlot,
                stageId,
                string.Empty,
                string.Empty,
                SaveSlotStore.DefaultRemainingChances,
                suppressCampaignFlow: false);
            var callSequence = new List<string>();
            stageLaunchRouter.AfterLaunch = _ => callSequence.Add("route-accepted");
            pauseService.BeforeResume = () => callSequence.Add("resume");

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            EditorDirectPlayContextStore.SetCurrent(directPlayContext);
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                sceneHandle: 7401,
                sceneName: "PauseRetrySource");
            Assert.That(coordinator.RequestPausePopup(), Is.True);

            popupRuntimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.RetryRequested);

            Assert.That(
                popupController.PopupCount,
                Is.EqualTo(1),
                "Pause Retry keeps the source popup rendered beneath the closing Iris until scene activation.");
            Assert.That(
                popupRuntimeFactory.CreatedRuntimes[^1].Runtime.IsTopmost,
                Is.False);
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(pauseService.ResumeCallCount, Is.EqualTo(1));
            Assert.That(callSequence, Is.EqualTo(new[] { "route-accepted", "resume" }));
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(stageId));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(stageLaunchRouter.Requests[0].Source, Is.EqualTo("pause-retry"));
            Assert.That(stageLaunchRouter.Requests[0].TransitionHint.Kind, Is.EqualTo(StageTransitionKind.StageRetryManual));
            Assert.That(stageLaunchRouter.Requests[0].EditorDirectPlayContext, Is.EqualTo(directPlayContext));
            EditorDirectPlayContextStore.Clear();
        }

        [Test]
        public void PauseRetry_RoutingThrowsBeforeOwnership_RestoresPopupAndKeepsSimulationPaused()
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
                out var uiAudioPort,
                out var stageLaunchRouter);
            var stageId = StageId.CreateOrThrow("pause-retry-throw-stage");
            var routingFailure = new ApplicationException("Injected pre-ownership routing failure.");
            stageLaunchRouter.ExceptionToThrow = routingFailure;

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                sceneHandle: 7402,
                sceneName: "PauseRetryThrowSource");
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;
            uiAudioPort.Clear();

            var thrown = Assert.Throws<ApplicationException>(() =>
                pauseRuntime.Emit(PopupCompletionKind.RetryRequested));

            Assert.That(thrown, Is.SameAs(routingFailure));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(popupController.TopPopup?.PopupId, Is.EqualTo(PopupId.Pause));
            Assert.That(pauseRuntime.IsTopmost, Is.True);
            Assert.That(stageLaunchRouter.LaunchCallCount, Is.EqualTo(1));
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            Assert.That(coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.Silent));
            Assert.That(coordinator.LastFlowAudioTrace.SilenceReason, Is.EqualTo(UiFlowAudioSilenceReason.Aborted));
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));
        }

        [Test]
        public void PauseRetry_RoutingRejected_RestoresPopupAndKeepsSimulationPaused()
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
            var stageId = StageId.CreateOrThrow("pause-retry-rejected-stage");

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                sceneHandle: 7403,
                sceneName: "PauseRetryRejectedSource");
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.GameplayEntry,
                    stageId,
                    generation,
                    out _),
                Is.True);
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;

            Exception thrown = null;
            try
            {
                pauseRuntime.Emit(PopupCompletionKind.RetryRequested);
            }
            catch (Exception exception)
            {
                thrown = exception;
            }

            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(thrown, Is.TypeOf<InvalidOperationException>());
            Assert.That(
                thrown.Message,
                Does.Contain("Pause Retry route was rejected before transition ownership."));
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(popupController.TopPopup?.PopupId, Is.EqualTo(PopupId.Pause));
            Assert.That(pauseRuntime.IsTopmost, Is.True);
            Assert.That(stageLaunchRouter.LaunchCallCount, Is.Zero);
            Assert.That(ReadPauseReturnModeName(coordinator), Is.EqualTo("None"));
        }

        [Test]
        public void PauseRetry_AfterRoutingThrow_SecondRetryDispatchesExactlyOnce()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                new FakeScreenRuntimeFactory(),
                presentationSource,
                out _,
                out var popupController,
                out var uiAudioPort,
                out var stageLaunchRouter);
            var stageId = StageId.CreateOrThrow("pause-retry-second-after-throw");
            var firstFailure = new ApplicationException("Injected first Retry routing failure.");
            stageLaunchRouter.ExceptionToThrow = firstFailure;

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(7404, "PauseRetrySecondThrowSource");
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;
            uiAudioPort.Clear();

            Assert.That(
                Assert.Throws<ApplicationException>(() =>
                    pauseRuntime.Emit(PopupCompletionKind.RetryRequested)),
                Is.SameAs(firstFailure));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);

            stageLaunchRouter.ExceptionToThrow = null;
            pauseRuntime.Emit(PopupCompletionKind.RetryRequested);

            Assert.That(stageLaunchRouter.LaunchCallCount, Is.EqualTo(2));
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(pauseService.ResumeCallCount, Is.EqualTo(1));
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(pauseRuntime.IsTopmost, Is.False);
            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));
        }

        [Test]
        public void PauseRetry_AfterRoutingRejection_SecondRetryDispatchesExactlyOnce()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                new FakeScreenRuntimeFactory(),
                presentationSource,
                out _,
                out var popupController,
                out _,
                out var stageLaunchRouter);
            var stageId = StageId.CreateOrThrow("pause-retry-second-after-rejection");

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                7405,
                "PauseRetrySecondRejectedSource");
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.GameplayEntry,
                    stageId,
                    generation,
                    out var blockingToken),
                Is.True);
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;

            var rejection = Assert.Throws<InvalidOperationException>(() =>
                pauseRuntime.Emit(PopupCompletionKind.RetryRequested));
            Assert.That(
                rejection.Message,
                Does.Contain("Pause Retry route was rejected before transition ownership."));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(SceneEntryPresentationRegistry.TryCancelClaim(blockingToken), Is.True);

            pauseRuntime.Emit(PopupCompletionKind.RetryRequested);

            Assert.That(stageLaunchRouter.LaunchCallCount, Is.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(pauseService.ResumeCallCount, Is.EqualTo(1));
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(pauseRuntime.IsTopmost, Is.False);
        }

        [Test]
        public void PauseRetry_RouterAdvancesToFailureOwnerThenThrows_DoesNotRollbackTransitionOwnership()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                new FakeScreenRuntimeFactory(),
                presentationSource,
                out _,
                out var popupController,
                out _,
                out var stageLaunchRouter);
            var stageId = StageId.CreateOrThrow("pause-retry-router-advanced");
            var routingFailure = new ApplicationException("Injected advanced-owner routing failure.");
            const string failureReason = "Router owns the failed opaque cover.";
            stageLaunchRouter.BeforeLaunch = _ =>
            {
                Assert.That(
                    SceneEntryPresentationRegistry.TryFailHoldingCover(
                        SceneEntryPresentationRegistry.Current.Token,
                        failureReason),
                    Is.True);
            };
            stageLaunchRouter.ExceptionToThrow = routingFailure;

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(7406, "PauseRetryAdvancedOwnerSource");
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;

            Assert.That(
                Assert.Throws<ApplicationException>(() =>
                    pauseRuntime.Emit(PopupCompletionKind.RetryRequested)),
                Is.SameAs(routingFailure));

            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(
                SceneEntryPresentationRegistry.Current.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
            Assert.That(SceneEntryPresentationRegistry.Current.FailureReason, Is.EqualTo(failureReason));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(popupController.TopPopup?.PopupId, Is.EqualTo(PopupId.Pause));
            Assert.That(pauseRuntime.IsTopmost, Is.True);
        }

        [Test]
        public void PauseRetry_RoutingAcceptedWhenResumeThrows_PreservesTransitionOwnerWithoutPopupRollback()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                new FakeScreenRuntimeFactory(),
                presentationSource,
                out _,
                out var popupController,
                out _,
                out var stageLaunchRouter);
            var stageId = StageId.CreateOrThrow("pause-retry-resume-failure");
            var resumeFailure = new ApplicationException("Injected accepted-route Resume failure.");
            pauseService.BeforeResume = () => throw resumeFailure;

            coordinator.Initialize();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(stageId));
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(7407, "PauseRetryResumeFailureSource");
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;
            LogAssert.Expect(LogType.Exception, new Regex("Injected accepted-route Resume failure\\."));

            pauseRuntime.Emit(PopupCompletionKind.RetryRequested);

            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.EqualTo(1));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);
            Assert.That(pauseRuntime.IsTopmost, Is.False);
        }

        [Test]
        public void PauseMainMenu_RoutingThrows_RetainsPopupAndPauseState()
        {
            var pauseService = new FakeGameplayPauseService();
            var popupRuntimeFactory = new FakePopupRuntimeFactory();
            var presentationSource = new ManualGameplayUiPresentationSource();
            var mainMenuRouter = new FakeMainMenuReturnRouter
            {
                ExceptionToThrow = new ApplicationException("Injected Pause Main Menu routing failure."),
            };
            using var coordinator = CreateCoordinator(
                pauseService,
                popupRuntimeFactory,
                new FakeScreenRuntimeFactory(),
                presentationSource,
                mainMenuRouter,
                out _,
                out var popupController,
                out _,
                out _);

            coordinator.Initialize();
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            var pauseRuntime = popupRuntimeFactory.CreatedRuntimes[^1].Runtime;

            Assert.That(
                Assert.Throws<ApplicationException>(() =>
                    pauseRuntime.Emit(PopupCompletionKind.MainMenuRequested)),
                Is.SameAs(mainMenuRouter.ExceptionToThrow));
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(pauseService.ResumeCallCount, Is.Zero);
            Assert.That(popupController.TopPopup?.PopupId, Is.EqualTo(PopupId.Pause));
            Assert.That(pauseRuntime.IsTopmost, Is.True);
        }

        [Test]
        public void UIFlowCoordinator_PausePopupMainMenu_UsesMainRouter_AndRetainsPausedSource()
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

            Assert.That(
                popupController.PopupCount,
                Is.EqualTo(1),
                "The source popup remains rendered beneath the closing Main Menu Iris.");
            Assert.That(pauseService.IsPaused, Is.True);
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

        private static UIFlowCoordinator CreateCoordinator(IStageLaunchRouter stageLaunchRouter)
        {
            return new UIFlowCoordinator(
                new ScreenController(new FakeScreenRuntimeFactory()),
                new PopupController(new FakePopupRuntimeFactory()),
                new UIBlockPolicy(),
                new FakeGameplayPauseService(),
                new ManualGameplayUiPresentationSource(),
                new RecordingUiAudioPort(),
                stageLaunchRouter,
                new FakeMainMenuReturnRouter());
        }

        private static StageNavigationRequest CreateCanonicalGameplayEntryRequest(string source)
        {
            return new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Continue,
                source,
                transitionIntent: SceneTransitionIntent.GameplayEntry);
        }

        private sealed class ControlledStageLaunchRouter : IStageLaunchRouter
        {
            private readonly List<StageNavigationRequest> _requests = new();

            public IReadOnlyList<StageNavigationRequest> Requests => _requests;

            public Action BeforeThrow { get; set; }

            public Exception ExceptionToThrow { get; set; }

            public void Launch(StageNavigationRequest request)
            {
                BeforeThrow?.Invoke();
                if (ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }

                _requests.Add(request);
            }
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
                new UIStageSlice(stageId, StageDisplayNameKeys.ForStage(stageId)),
                UIPresentationSnapshot.Empty.Player,
                UIPresentationSnapshot.Empty.Notifications);
        }

        private static PauseProgressionSnapshot CreateProgressionSnapshot(string currentStageKey)
        {
            return new PauseProgressionSnapshot(
                isAvailable: true,
                new[]
                {
                    new PauseProgressionStageSnapshot("stage-1-1", "world-1"),
                    new PauseProgressionStageSnapshot("stage-1-2", "world-1"),
                },
                currentStageKey);
        }

        private sealed class MutablePauseProgressionReadSource : IPauseProgressionReadSource
        {
            public PauseProgressionSnapshot Snapshot { get; set; } = PauseProgressionSnapshot.Unavailable;

            public int ReadCallCount { get; private set; }

            public bool TryRead(out PauseProgressionSnapshot snapshot)
            {
                ReadCallCount++;
                snapshot = Snapshot;
                return snapshot.IsAvailable;
            }
        }

        private static UITickEventBatch CreateStageClearedBatch(
            int tickIndex,
            TerminalSessionToken terminalToken = default)
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
                            resolutionKind: Game.Feature.Gameplay.UIAccess.Models.GameplayUiActionResolutionKind.None),
                        terminalToken: terminalToken),
                });
        }

        private static TerminalSessionToken BeginSameSceneTerminal(
            TerminalDestinationKind destinationKind)
        {
            var authority = TerminalSessionRegistry.Authority;
            var generation = authority.RegisterSceneBootstrap(7001, "ui-flow-test");
            var claim = authority.TryClaim(new TerminalClaimRequest(
                destinationKind == TerminalDestinationKind.SameSceneLevelFailed
                    ? TerminalTransitionKind.Defeat
                    : TerminalTransitionKind.Victory,
                generation,
                destinationKind));
            Assert.That(claim.Accepted, Is.True);
            Assert.That(
                authority.TryAdvancePhase(claim.Token, TerminalSessionPhase.Iris),
                Is.True);
            Assert.That(
                authority.TryAdvancePhase(claim.Token, TerminalSessionPhase.Black),
                Is.True);
            Assert.That(
                authority.TryAdvancePhase(
                    claim.Token,
                    TerminalSessionPhase.WaitingSameSceneDestination),
                Is.True);
            return claim.Token;
        }

        private static void CompleteTerminal(TerminalSessionToken token)
        {
            if (TerminalSessionRegistry.Current.Phase ==
                TerminalSessionPhase.WaitingSameSceneDestination)
            {
                Assert.That(
                    TerminalSessionRegistry.Authority.TryAdvancePhase(
                        token,
                        TerminalSessionPhase.Revealing),
                    Is.True);
            }

            Assert.That(
                TerminalSessionRegistry.Authority.TryComplete(token),
                Is.True);
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
                StageTransitionHint.ForKind(StageTransitionKind.StageClearNext),
                SceneTransitionIntent.StageAdvance);
            var retryRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "stage-result-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry);
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
                StageDisplayNameKeys.ForStage(stageId),
                result,
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
