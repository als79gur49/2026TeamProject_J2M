using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class GameplayHudPresenter : IDisposable
    {
        private readonly IGameplayCommandGateway _commandGateway;
        private readonly IGameplayPauseService _pauseService;
        private readonly IGameplayPresentationFeed _presentationFeed;
        private readonly IGameplayQueryFacade _queryFacade;

        public GameplayHudPresenter(
            IGameplayQueryFacade queryFacade,
            IGameplayCommandGateway commandGateway,
            IGameplayPresentationFeed presentationFeed,
            IGameplayPauseService pauseService)
        {
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _commandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            _presentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));

            ViewModel = new GameplayHudViewModel();
            _presentationFeed.FramePublished += HandleFramePublished;
            _presentationFeed.StateChanged += HandlePresentationStateChanged;
            _pauseService.PauseChanged += HandlePauseChanged;

            Refresh();
            ViewModel.SetCurrentTopology(_presentationFeed.CurrentState.CurrentTopology);
        }

        public GameplayHudViewModel ViewModel { get; }

        public void Dispose()
        {
            _presentationFeed.FramePublished -= HandleFramePublished;
            _presentationFeed.StateChanged -= HandlePresentationStateChanged;
            _pauseService.PauseChanged -= HandlePauseChanged;
        }

        public void Refresh()
        {
            var session = _queryFacade.Session.Read();
            var playerHud = _queryFacade.PlayerHud.Read();

            ViewModel.ApplyGameplayState(session, playerHud);
        }

        public GameplayCommandAcceptance SetHeldMoveDirection(Direction direction)
        {
            var acceptance = _commandGateway.SetHeldMoveDirection(direction);
            Refresh();
            return acceptance;
        }

        public GameplayCommandAcceptance ClearHeldMoveDirection()
        {
            var acceptance = _commandGateway.ClearHeldMoveDirection();
            Refresh();
            return acceptance;
        }

        public GameplayCommandAcceptance RequestFlip(Direction direction)
        {
            var acceptance = _commandGateway.RequestFlip(direction);
            Refresh();
            return acceptance;
        }

        private void HandleFramePublished(GameplayPresentationFrame frame)
        {
            if (frame.Topology.HasValue)
            {
                ViewModel.SetCurrentTopology(frame.Topology.Value.DestinationTopology);
            }
            else
            {
                ViewModel.SetCurrentTopology(frame.FinalTopology);
            }

            Refresh();
        }

        private void HandlePauseChanged(bool _)
        {
            Refresh();
        }

        private void HandlePresentationStateChanged(GameplayPresentationState state)
        {
            ViewModel.SetCurrentTopology(state.CurrentTopology);
        }
    }
}
