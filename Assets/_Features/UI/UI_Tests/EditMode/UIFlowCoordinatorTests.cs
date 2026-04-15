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
            using var coordinator = CreateCoordinator(pauseService, out var screenController, out var popupController);

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
        public void UIFlowCoordinator_OpensObjectiveScreen_AndNonPausingObjectiveInfoPopup()
        {
            var pauseService = new FakeGameplayPauseService();
            using var coordinator = CreateCoordinator(pauseService, out var screenController, out var popupController);

            coordinator.Initialize();

            Assert.That(coordinator.OpenObjectiveStatusScreen(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksHudInteraction, Is.True);

            Assert.That(coordinator.RequestObjectiveInfoPopup(), Is.True);
            Assert.That(popupController.Contains(PopupId.ObjectiveInfo), Is.True);
            Assert.That(pauseService.IsPaused, Is.False);

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(popupController.Contains(PopupId.ObjectiveInfo), Is.False);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));

            Assert.That(coordinator.HandleBackRequested(), Is.True);
            Assert.That(screenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(coordinator.CurrentBlockSnapshot.BlocksHudInteraction, Is.False);
        }

        private static UIFlowCoordinator CreateCoordinator(
            FakeGameplayPauseService pauseService,
            out ScreenController screenController,
            out PopupController popupController)
        {
            screenController = new ScreenController();
            popupController = new PopupController();

            return new UIFlowCoordinator(
                screenController,
                popupController,
                new UIBlockPolicy(),
                pauseService);
        }
    }
}
