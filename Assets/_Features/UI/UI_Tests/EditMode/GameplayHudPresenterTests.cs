using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayHudPresenterTests
    {
        [Test]
        public void GameplayHudPresenter_MapsGameplayStateAndPresentationUpdates()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(nextTickIndex: 1, isPaused: false, canAcceptGameplayCommands: true, isStageCleared: false),
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
                new GameplayObjectiveReadModel(false, false, false, false));
            var commandGateway = new FakeGameplayCommandGateway();
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();

            using var presenter = new GameplayHudPresenter(queryFacade, commandGateway, presentationFeed, pauseService);

            Assert.That(presenter.CurrentState.CurrentHp, Is.EqualTo(3));
            Assert.That(presenter.CurrentState.FacingText, Is.EqualTo("Up"));
            Assert.That(presenter.CurrentState.CanAcceptGameplayCommands, Is.True);
            Assert.That(presenter.CurrentState.TopologyText, Is.EqualTo(GameplayUiFace.Floor.ToString()));

            queryFacade.SetSession(new GameplaySessionReadModel(2, true, false, false));
            queryFacade.SetPlayerHud(new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: 10,
                currentHp: 2,
                facing: GameplayUiDirection.Right,
                activeActionKind: GameplayUiActionKind.Flip,
                activeActionDirection: GameplayUiDirection.Right,
                activeTargetEntityId: 22,
                isActionInProgress: true,
                canMoveThisTick: false,
                canStartActionThisTick: false));

            presentationFeed.PublishFrame(new GameplayPresentationFrame(
                tickIndex: 1,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                topology: new GameplayTopologyPresentationSlice(
                    new GameplayUiTopology(GameplayUiFace.Floor),
                    new GameplayUiTopology(GameplayUiFace.Front),
                    GameplayUiRotationKind.Forward)));

            Assert.That(presenter.CurrentState.CurrentHp, Is.EqualTo(2));
            Assert.That(presenter.CurrentState.FacingText, Is.EqualTo("Right"));
            Assert.That(presenter.CurrentState.ActiveActionText, Is.EqualTo("Flip"));
            Assert.That(presenter.CurrentState.TopologyText, Is.EqualTo(GameplayUiFace.Front.ToString()));

            pauseService.Pause();

            Assert.That(presenter.CurrentState.IsPaused, Is.True);
            Assert.That(presenter.CurrentState.CanAcceptGameplayCommands, Is.False);
        }

        [Test]
        public void GameplayHudPresenter_MapsRejectionReasonsToUiCommandResult()
        {
            var presenter = CreatePresenter(new FakeGameplayCommandGateway
            {
                OnRequestFlip = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.BlockingPresentation),
                OnSetHeldMoveDirection = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.Paused),
            });

            var flipResult = presenter.RequestFlipRight();
            var moveResult = presenter.RequestMoveUp();

            Assert.That(flipResult.Accepted, Is.False);
            Assert.That(flipResult.FailureKind, Is.EqualTo(GameplayHudCommandFailureKind.Busy));
            Assert.That(moveResult.Accepted, Is.False);
            Assert.That(moveResult.FailureKind, Is.EqualTo(GameplayHudCommandFailureKind.Paused));
        }

        private static GameplayHudPresenter CreatePresenter(FakeGameplayCommandGateway commandGateway)
        {
            return new GameplayHudPresenter(
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
                commandGateway,
                new FakeGameplayPresentationFeed(),
                new FakeGameplayPauseService());
        }
    }
}
