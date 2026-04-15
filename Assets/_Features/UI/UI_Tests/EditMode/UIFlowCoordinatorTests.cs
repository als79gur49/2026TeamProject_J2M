using System.Collections.Generic;
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
            using var coordinator = CreateCoordinator(pauseService, runtimeFactory, out var screenController, out var popupController);

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
            using var coordinator = CreateCoordinator(pauseService, runtimeFactory, out var screenController, out var popupController);

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
            using var coordinator = CreateCoordinator(pauseService, runtimeFactory, out var screenController, out var popupController);

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
            using var coordinator = CreateCoordinator(pauseService, runtimeFactory, out _, out var popupController);

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
                out _);

            coordinator.Initialize();
            Assert.That(coordinator.OpenHelpScreen(), Is.True);
            Assert.That(screenController.BackStackCount, Is.EqualTo(1));

            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));

            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
            Assert.That(screenController.BackStackCount, Is.EqualTo(0));
            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));

            presentationSource.PublishTickEvents(CreateStageClearedBatch(tickIndex: 9));
            Assert.That(screenRuntimeFactory.CreatedRuntimes.FindAll(record => record.Request.ScreenId == ScreenId.StageResult), Has.Count.EqualTo(1));
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            FakePopupRuntimeFactory runtimeFactory,
            FakeScreenRuntimeFactory screenRuntimeFactory,
            ManualGameplayUiPresentationSource presentationSource,
            out ScreenController screenController,
            out PopupController popupController)
        {
            screenController = new ScreenController(screenRuntimeFactory);
            popupController = new PopupController(runtimeFactory);

            return new UIFlowCoordinator(
                screenController,
                popupController,
                new UIBlockPolicy(),
                pauseService,
                presentationSource);
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
                new FakeScreenRuntimeFactory(),
                new ManualGameplayUiPresentationSource(),
                out screenController,
                out popupController);
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
    }
}
