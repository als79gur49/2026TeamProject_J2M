using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.UI.HUD
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
            _pauseService.PauseChanged += HandlePauseChanged;

            Refresh();
            ViewModel.CurrentTopology = _presentationFeed.CurrentState.CurrentTopology;
        }

        public GameplayHudViewModel ViewModel { get; }

        public void Dispose()
        {
            _presentationFeed.FramePublished -= HandleFramePublished;
            _pauseService.PauseChanged -= HandlePauseChanged;
        }

        public void Refresh()
        {
            var session = _queryFacade.Session.Read();
            var playerHud = _queryFacade.PlayerHud.Read();

            ViewModel.PlayerEntityId = playerHud.PlayerEntityId;
            ViewModel.CurrentHp = playerHud.CurrentHp;
            ViewModel.Facing = playerHud.Facing;
            ViewModel.ActiveActionKind = playerHud.ActiveActionKind;
            ViewModel.CanMoveThisTick = playerHud.CanMoveThisTick;
            ViewModel.CanStartActionThisTick = playerHud.CanStartActionThisTick;
            ViewModel.IsPaused = session.IsPaused;
            ViewModel.IsStageCleared = session.IsStageCleared;
        }

        public GameplayCommandAcceptance SetHeldMoveDirection(Direction direction)
        {
            return ApplyCommandAcceptance(_commandGateway.SetHeldMoveDirection(direction));
        }

        public GameplayCommandAcceptance ClearHeldMoveDirection()
        {
            return ApplyCommandAcceptance(_commandGateway.ClearHeldMoveDirection());
        }

        public GameplayCommandAcceptance RequestFlip(Direction direction)
        {
            return ApplyCommandAcceptance(_commandGateway.RequestFlip(direction));
        }

        public void TogglePause()
        {
            _pauseService.Toggle();
        }

        private GameplayCommandAcceptance ApplyCommandAcceptance(GameplayCommandAcceptance acceptance)
        {
            ViewModel.LastCommandAcceptance = acceptance;
            Refresh();
            return acceptance;
        }

        private void HandleFramePublished(GameplayPresentationFrame frame)
        {
            if (frame.Topology.HasValue)
            {
                ViewModel.CurrentTopology = frame.Topology.Value.DestinationTopology;
                return;
            }

            ViewModel.CurrentTopology = frame.FinalTopology;
        }

        private void HandlePauseChanged(bool _)
        {
            Refresh();
        }
    }
}
