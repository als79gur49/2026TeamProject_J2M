using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
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
                    facing: Direction.Up,
                    activeActionKind: PlayerActionKind.None,
                    activeActionDirection: Direction.None,
                    activeTargetEntityId: 0,
                    isActionInProgress: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true),
                new GameplayObjectiveReadModel(false, false, false, false));
            var commandGateway = new FakeGameplayCommandGateway();
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();

            using var presenter = new GameplayHudPresenter(queryFacade, commandGateway, presentationFeed, pauseService);

            Assert.That(presenter.ViewModel.CurrentHp, Is.EqualTo(3));
            Assert.That(presenter.ViewModel.Facing, Is.EqualTo(Direction.Up));
            Assert.That(presenter.ViewModel.CanAcceptGameplayCommands, Is.True);
            Assert.That(presenter.ViewModel.CurrentTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));

            queryFacade.SetSession(new GameplaySessionReadModel(2, true, false, false));
            queryFacade.SetPlayerHud(new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: 10,
                currentHp: 2,
                facing: Direction.Right,
                activeActionKind: PlayerActionKind.Flip,
                activeActionDirection: Direction.Right,
                activeTargetEntityId: 22,
                isActionInProgress: true,
                canMoveThisTick: false,
                canStartActionThisTick: false));

            presentationFeed.PublishFrame(new GameplayPresentationFrame(
                tickIndex: 1,
                finalTopology: new CubeTopologyState(FaceId.Front),
                topology: new Game.Feature.Gameplay.UIAccess.Presentation.GameplayTopologyPresentationSlice(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward)));

            Assert.That(presenter.ViewModel.CurrentHp, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.Facing, Is.EqualTo(Direction.Right));
            Assert.That(presenter.ViewModel.CurrentTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));

            pauseService.Pause();

            Assert.That(presenter.ViewModel.IsPaused, Is.True);
            Assert.That(presenter.ViewModel.CanAcceptGameplayCommands, Is.False);
        }
    }
}
