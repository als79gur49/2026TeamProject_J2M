using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class HUDControllerTests
    {
        [Test]
        public void HUDController_RequestFlipRight_WhenLocallyBlocked_DoesNotForwardCommand()
        {
            var commandGateway = new FakeGameplayCommandGateway();
            var controller = CreateController(commandGateway);

            controller.ApplyBlockSnapshot(new UIBlockSnapshot(blocksHudInteraction: true, blocksScreenInteraction: false, popupConsumesBack: false));

            var acceptance = controller.RequestFlipRight();

            Assert.That(acceptance.HasValue, Is.False);
            Assert.That(commandGateway.RequestFlipCallCount, Is.EqualTo(0));
            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo("HUD blocked"));
            Assert.That(controller.ViewModel.LastCommandAcceptance.HasValue, Is.False);
        }

        [Test]
        public void HUDController_RequestFlipRight_StoresMappedFeedbackForGameplayRejection()
        {
            var commandGateway = new FakeGameplayCommandGateway
            {
                OnRequestFlip = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.BlockingPresentation),
            };
            var controller = CreateController(commandGateway);

            controller.ApplyBlockSnapshot(new UIBlockSnapshot(blocksHudInteraction: false, blocksScreenInteraction: false, popupConsumesBack: false));
            var acceptance = controller.RequestFlipRight();

            Assert.That(acceptance.HasValue, Is.True);
            Assert.That(acceptance.Value.Accepted, Is.False);
            Assert.That(commandGateway.RequestFlipCallCount, Is.EqualTo(1));
            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo("Busy"));
            Assert.That(controller.ViewModel.LastCommandAcceptance.HasValue, Is.True);
            Assert.That(controller.ViewModel.LastCommandAcceptance.Value.RejectionReason, Is.EqualTo(GameplayCommandRejectionReason.BlockingPresentation));
        }

        private static HUDController CreateController(FakeGameplayCommandGateway commandGateway)
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                new GameplayPlayerHudReadModel(
                    isAvailable: true,
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: Direction.Up,
                    activeActionKind: PlayerActionKind.None,
                    activeActionDirection: Direction.None,
                    activeTargetEntityId: 0,
                    isActionInProgress: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true),
                new GameplayObjectiveReadModel(false, false, false, false));

            var presenter = new GameplayHudPresenter(
                queryFacade,
                commandGateway,
                new FakeGameplayPresentationFeed(),
                new FakeGameplayPauseService());

            return new HUDController(presenter);
        }
    }
}
