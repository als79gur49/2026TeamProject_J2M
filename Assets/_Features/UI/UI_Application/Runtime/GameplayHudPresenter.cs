using System;
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
        private GameplayUiTopology _currentTopology;

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
            _currentTopology = _presentationFeed.CurrentState.CurrentTopology;

            _presentationFeed.FramePublished += HandleFramePublished;
            _presentationFeed.StateChanged += HandlePresentationStateChanged;
            _pauseService.PauseChanged += HandlePauseChanged;

            Refresh();
        }

        public event Action<GameplayHudState> StateChanged;

        public GameplayHudState CurrentState { get; private set; }

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

            CurrentState = new GameplayHudState(
                playerHud.PlayerEntityId,
                playerHud.CurrentHp,
                playerHud.Facing.ToString(),
                playerHud.ActiveActionKind.ToString(),
                _currentTopology.BottomFace.ToString(),
                playerHud.CanMoveThisTick,
                playerHud.CanStartActionThisTick,
                session.IsPaused,
                session.IsStageCleared,
                session.CanAcceptGameplayCommands);
            StateChanged?.Invoke(CurrentState);
        }

        public GameplayHudCommandResult RequestMoveUp()
        {
            var acceptance = _commandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);
            Refresh();
            return MapAcceptance(acceptance);
        }

        public GameplayHudCommandResult RequestFlipRight()
        {
            var acceptance = _commandGateway.RequestFlip(GameplayUiDirection.Right);
            Refresh();
            return MapAcceptance(acceptance);
        }

        private void HandleFramePublished(GameplayPresentationFrame frame)
        {
            if (frame.Topology.HasValue)
            {
                _currentTopology = frame.Topology.Value.DestinationTopology;
            }
            else
            {
                _currentTopology = frame.FinalTopology;
            }

            Refresh();
        }

        private void HandlePauseChanged(bool _)
        {
            Refresh();
        }

        private void HandlePresentationStateChanged(GameplayPresentationState state)
        {
            _currentTopology = state.CurrentTopology;
            Refresh();
        }

        private static GameplayHudCommandResult MapAcceptance(GameplayCommandAcceptance acceptance)
        {
            if (acceptance.Accepted)
            {
                return GameplayHudCommandResult.Accept();
            }

            switch (acceptance.RejectionReason)
            {
                case GameplayCommandRejectionReason.Paused:
                    return GameplayHudCommandResult.Reject(GameplayHudCommandFailureKind.Paused);
                case GameplayCommandRejectionReason.BlockingPresentation:
                    return GameplayHudCommandResult.Reject(GameplayHudCommandFailureKind.Busy);
                default:
                    return GameplayHudCommandResult.Reject(GameplayHudCommandFailureKind.Unavailable);
            }
        }
    }
}
