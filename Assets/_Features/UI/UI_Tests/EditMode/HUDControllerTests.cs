using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class HUDControllerTests
    {
        [Test]
        public void HUDController_RequestFlipRight_WhenLocallyBlocked_DoesNotForwardCommand()
        {
            var commandGateway = new FakeGameplayCommandGateway();
            var controller = CreateController(commandGateway, out var queryFacade);

            controller.ApplyBlockSnapshot(new UIBlockSnapshot(blocksHudInteraction: true, blocksScreenInteraction: false, popupConsumesBack: false));

            var commandResult = controller.RequestFlipRight();

            Assert.That(commandResult.HasValue, Is.False);
            Assert.That(commandGateway.RequestFlipCallCount, Is.EqualTo(0));
            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo("HUD blocked"));
            Assert.That(controller.ViewModel.LastCommandResult.HasValue, Is.False);

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, true, false));
            controller.ApplyBlockSnapshot(new UIBlockSnapshot(blocksHudInteraction: false, blocksScreenInteraction: false, popupConsumesBack: false));

            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo(string.Empty));
        }

        [Test]
        public void HUDController_RequestFlipRight_StoresMappedFeedbackForGameplayRejection()
        {
            var commandGateway = new FakeGameplayCommandGateway
            {
                OnRequestFlip = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.BlockingPresentation),
            };
            var controller = CreateController(commandGateway, out _);

            controller.ApplyBlockSnapshot(new UIBlockSnapshot(blocksHudInteraction: false, blocksScreenInteraction: false, popupConsumesBack: false));
            var commandResult = controller.RequestFlipRight();

            Assert.That(commandResult.HasValue, Is.True);
            Assert.That(commandResult.Value.Accepted, Is.False);
            Assert.That(commandGateway.RequestFlipCallCount, Is.EqualTo(1));
            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo("Busy"));
            Assert.That(controller.ViewModel.LastCommandResult.HasValue, Is.True);
            Assert.That(controller.ViewModel.LastCommandResult.Value.FailureKind, Is.EqualTo(GameplayHudCommandFailureKind.Busy));
        }

        [Test]
        public void HUDController_ClearsGameplayFeedback_WhenCommandReadyReturns()
        {
            var commandGateway = new FakeGameplayCommandGateway
            {
                OnRequestFlip = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.BlockingPresentation),
            };
            var controller = CreateController(commandGateway, out var queryFacade);

            controller.ApplyBlockSnapshot(new UIBlockSnapshot(blocksHudInteraction: false, blocksScreenInteraction: false, popupConsumesBack: false));
            controller.RequestFlipRight();

            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo("Busy"));

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, true, false));
            queryFacade.SetPlayerHud(new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: 10,
                currentHp: 3,
                facing: Direction.Up,
                activeActionKind: PlayerActionKind.None,
                activeActionDirection: Direction.None,
                activeTargetEntityId: 0,
                isActionInProgress: false,
                canMoveThisTick: true,
                canStartActionThisTick: true));
            controller.RequestMoveUp();

            Assert.That(controller.ViewModel.FeedbackText, Is.EqualTo(string.Empty));
            Assert.That(controller.ViewModel.LastCommandResult.HasValue, Is.True);
        }

        private static HUDController CreateController(
            FakeGameplayCommandGateway commandGateway,
            out FakeGameplayQueryFacade queryFacade)
        {
            queryFacade = new FakeGameplayQueryFacade(
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
