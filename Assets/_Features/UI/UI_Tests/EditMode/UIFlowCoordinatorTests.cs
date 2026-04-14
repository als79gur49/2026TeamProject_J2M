using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UIFlowCoordinatorTests
    {
        [Test]
        public void UIFlowCoordinator_HandleBack_UsesPopupFirstThenScreenThenPausePopup()
        {
            var pauseService = new FakeGameplayPauseService();
            using var coordinator = CreateCoordinator(pauseService, out var screenController, out var popupController, out var hudController);

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
            Assert.That(hudController.ViewModel.IsInteractive, Is.False);
        }

        [Test]
        public void UIFlowCoordinator_OpensObjectiveScreen_AndNonPausingObjectiveInfoPopup()
        {
            var pauseService = new FakeGameplayPauseService();
            using var coordinator = CreateCoordinator(pauseService, out var screenController, out var popupController, out var hudController);

            coordinator.Initialize();

            Assert.That(coordinator.OpenObjectiveStatusScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
            Assert.That(hudController.ViewModel.IsInteractive, Is.False);

            Assert.That(coordinator.RequestObjectiveInfoPopup(), Is.True);
            Assert.That(popupController.Contains(PopupId.ObjectiveInfo), Is.True);
            Assert.That(pauseService.IsPaused, Is.False);

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(popupController.Contains(PopupId.ObjectiveInfo), Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(hudController.ViewModel.IsInteractive, Is.True);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            out ScreenController screenController,
            out PopupController popupController,
            out HUDController hudController)
        {
            var presenter = new GameplayHudPresenter(
                new FakeGameplayQueryFacade(
                    new GameplaySessionReadModel(1, false, true, false),
                    new GameplayPlayerHudReadModel(
                        isAvailable: true,
                        playerEntityId: 10,
                        currentHp: 3,
                        facing: GameplayUiDirection.Up,
                        activeActionKind: GameplayUiActionKind.None,
                        activeActionDirection: GameplayUiDirection.None,
                        activeTargetEntityId: 0,
                        isActionInProgress: false,
                        canMoveThisTick: true,
                        canStartActionThisTick: true),
                    new GameplayObjectiveReadModel(false, false, false, false)),
                new FakeGameplayCommandGateway(),
                new FakeGameplayPresentationFeed(),
                pauseService);

            screenController = new ScreenController();
            popupController = new PopupController();
            hudController = new HUDController(presenter);

            return new UIFlowCoordinator(
                screenController,
                popupController,
                hudController,
                new UIBlockPolicy(),
                pauseService);
        }
    }
}
