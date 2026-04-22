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

            Assert.That(coordinator.OpenHelpScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
            Assert.That(coordinator.RequestPausePopup(), Is.True);
            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(popupController.Contains(PopupId.Pause), Is.True);

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));

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

            Assert.That(coordinator.OpenHelpScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));
            Assert.That(completions, Has.Count.EqualTo(1));
            Assert.That(completions[0].CloseReason, Is.EqualTo(PopupCloseReason.ScreenTransition));
        }

        [Test]
        public void UIFlowCoordinator_OpensObjectiveInfoAndKeepsPopupPolicyOutOfCoordinator()
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
            Assert.That(coordinator.OpenObjectiveStatusScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));

            var payload = new ObjectiveInfoPopupPayload("Info", "Body");
            Assert.That(coordinator.RequestObjectiveInfoPopup(payload), Is.True);
            Assert.That(popupController.TopPopup.HasValue, Is.True);
            Assert.That(popupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.ObjectiveInfo));
            Assert.That(popupController.TopPopup.Value.Policy.PolicyClass, Is.EqualTo(PopupPolicyClass.NonModalInformational));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksScreenInteraction, Is.False);
            Assert.That(coordinator.CurrentBlockSnapshot.ShowsPopupDim, Is.False);
        }

        [Test]
        public void UIFlowCoordinator_RequestConfirmAndReward_ExposeStageSixValidationDefaults()
        {
            var pauseService = new FakeGameplayPauseService();
            var runtimeFactory = new FakePopupRuntimeFactory();
            var screenRuntimeFactory = new FakeScreenRuntimeFactory();
            using var coordinator = CreateCoordinator(
                pauseService,
                runtimeFactory,
                out _,
                out var popupController,
                out _);

            coordinator.Initialize();

            var confirmResults = new List<PopupCompletion>();
            Assert.That(coordinator.RequestConfirmPopup(
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", true),
                confirmResults.Add), Is.True);
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(confirmResults, Has.Count.EqualTo(1));
            Assert.That(confirmResults[0].CompletionKind, Is.EqualTo(PopupCompletionKind.Cancelled));

            var rewardResults = new List<PopupCompletion>();
            Assert.That(coordinator.RequestRewardPopup(
                new RewardPopupPayload(
                    "Reward",
                    new[] { new RewardPopupItemPayload("Crystal", 3) },
                    "Summary",
                    "Claim"),
                rewardResults.Add), Is.True);
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(rewardResults, Is.Empty);
            Assert.That(popupController.PopupCount, Is.EqualTo(1));
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
                new ScreenRequest(ScreenId.Help, HelpScreenPayload.Default, ScreenId.Help.ToString())));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
            Assert.That(popupController.PopupCount, Is.EqualTo(0));

            coordinator.HandleScreenActionRequested(ScreenAction.Popup(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false))));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
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

            Assert.That(coordinator.OpenHelpScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
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

                presentationSource.PublishStageCompletion(CreateStageCompletionReadModel(tickIndex: 9, includeReward: false));
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
        public void UIFlowCoordinator_StageClearedAutoOpensTerminalStageResult_AndConsumesBack()
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
                out var stageLaunchRouter);

            coordinator.Initialize();
            Assert.That(coordinator.OpenHelpScreen(), Is.True);
            Assert.That(screenController.BackStackCount, Is.EqualTo(1));

            presentationSource.PublishStageCompletion(CreateStageCompletionReadModel(tickIndex: 9, includeReward: true));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.BackStackCount, Is.EqualTo(0));
            var stageResultRecord = screenRuntimeFactory.CreatedRuntimes.Find(record => record.Request.ScreenId == ScreenId.StageResult);
            var stagePayload = stageResultRecord.Request.Payload as StageResultScreenPayload;
            Assert.That(stagePayload, Is.Not.Null);
            Assert.That(stagePayload.TitleText, Is.EqualTo("Payload Title"));
            Assert.That(stagePayload.SummaryText, Does.Contain("Payload Stage"));
            Assert.That(stagePayload.DetailText, Does.Contain("Tick 9"));
            Assert.That(stagePayload.ContinueLabel, Is.EqualTo("Collect"));
            Assert.That(stagePayload.ContinueStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stagePayload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
            Assert.That(stagePayload.RetryStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stagePayload.RetryStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(screenController.CurrentEntry.HasValue, Is.True);
            Assert.That(screenController.CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.CurrentEntry.Value.Payload, Is.TypeOf<StageResultScreenPayload>());
            Assert.That(popupController.TopPopup.HasValue, Is.True);
            Assert.That(popupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Reward));
            var rewardPayload = popupController.TopPopup.Value.Payload as RewardPopupPayload;
            Assert.That(rewardPayload, Is.Not.Null);
            Assert.That(rewardPayload.Items.Count, Is.EqualTo(1));
            Assert.That(rewardPayload.SummaryText, Does.Contain("First-clear"));
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));

            coordinator.HandleScreenActionRequested(ScreenAction.LaunchStage(stagePayload.ContinueStageRequest));
            Assert.That(stageLaunchRouter.Requests, Has.Count.EqualTo(1));
            Assert.That(stageLaunchRouter.Requests[0].StageId, Is.EqualTo(StageId.CreateOrThrow("payload-stage")));
            Assert.That(stageLaunchRouter.Requests[0].NavigationKind, Is.EqualTo(StageNavigationKind.Continue));

            presentationSource.PublishStageCompletion(CreateStageCompletionReadModel(tickIndex: 9, includeReward: true));
            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.StageResult), Has.Count.EqualTo(1));
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            out ScreenController screenController,
            out PopupController popupController,
            out FakeStageLaunchRouter stageLaunchRouter)
        {
            screenController = new ScreenController(screenRuntimeFactory);
            popupController = new PopupController(runtimeFactory);
            stageLaunchRouter = new FakeStageLaunchRouter();

            return new UIFlowCoordinator(
                screenController,
                popupController,
                new UIBlockPolicy(),
                pauseService,
                presentationSource,
                stageLaunchRouter);
        }

        private static UIFlowCoordinator CreateCoordinator(
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
                out stageLaunchRouter);
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

        private static StageCompletionReadModel CreateStageCompletionReadModel(int tickIndex, bool includeReward)
        {
            var stageId = StageId.CreateOrThrow("payload-stage");
            var runId = new StageRunId("run-01");
            var clearResult = new StageClearResult(
                stageId,
                runId,
                StageTerminalReason.Cleared,
                wasCleared: true,
                tickIndex,
                new StageObjectiveProgressSnapshot(true, true, true, true, 1, 1),
                System.Array.Empty<StageSessionMetricValue>(),
                System.Array.Empty<StageChallengeRuntimeState>());
            var evaluationResult = new StageClearEvaluationResult(
                stageId,
                runId,
                wasCleared: true,
                score: 120,
                starsEarned: 3,
                rankId: "S",
                challengeResults: System.Array.Empty<StageChallengeEvaluationResult>());
            var rewardId = new RewardGrantId($"{stageId.Value}:clear");
            var rewardResult = includeReward
                ? new RewardGrantResult(
                    stageId,
                    runId,
                    new[]
                    {
                        new RewardGrantEntry(
                            "clear",
                            new RewardEntry
                            {
                                RewardId = "Crystal",
                                Amount = 2,
                            },
                            rewardId),
                    },
                    new[] { "clear" },
                    new[] { rewardId },
                    wasFirstClear: true)
                : new RewardGrantResult(
                    stageId,
                    runId,
                    System.Array.Empty<RewardGrantEntry>(),
                    System.Array.Empty<string>(),
                    System.Array.Empty<RewardGrantId>(),
                    wasFirstClear: false);

            return new StageCompletionReadModel(
                stageId,
                "Payload Stage",
                "Payload Title",
                string.Empty,
                string.Empty,
                "Collect",
                clearResult,
                evaluationResult,
                rewardResult,
                PlayerStageProgress.CreateEmpty(stageId));
        }

        private static string ReadPauseReturnModeName(UIFlowCoordinator coordinator)
        {
            var field = typeof(UIFlowCoordinator).GetField("_pauseReturnMode", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(coordinator)?.ToString() ?? string.Empty;
        }
    }
}
