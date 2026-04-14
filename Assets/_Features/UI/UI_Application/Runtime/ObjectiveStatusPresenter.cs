using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveStatusPresenter : IDisposable
    {
        private readonly IGameplayPauseService _pauseService;
        private readonly IGameplayPresentationFeed _presentationFeed;
        private readonly IGameplayQueryFacade _queryFacade;

        public ObjectiveStatusPresenter(
            IGameplayQueryFacade queryFacade,
            IGameplayPresentationFeed presentationFeed,
            IGameplayPauseService pauseService)
        {
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _presentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));

            _presentationFeed.FramePublished += HandleRefreshSignal;
            _presentationFeed.StateChanged += HandleRefreshSignal;
            _pauseService.PauseChanged += HandlePauseChanged;

            Refresh();
        }

        public event Action<ObjectiveStatusScreenState> StateChanged;

        public ObjectiveStatusScreenState CurrentState { get; private set; }

        public void Dispose()
        {
            _presentationFeed.FramePublished -= HandleRefreshSignal;
            _presentationFeed.StateChanged -= HandleRefreshSignal;
            _pauseService.PauseChanged -= HandlePauseChanged;
        }

        public void Refresh()
        {
            var session = _queryFacade.Session.Read();
            var objectives = _queryFacade.Objectives.Read();

            CurrentState = new ObjectiveStatusScreenState(
                objectives.HasObjective,
                objectives.GoalReached,
                objectives.AllConditionsSatisfied,
                objectives.IsCleared,
                session.NextTickIndex,
                session.IsPaused,
                session.CanAcceptGameplayCommands,
                session.IsStageCleared);
            StateChanged?.Invoke(CurrentState);
        }

        private void HandlePauseChanged(bool _)
        {
            Refresh();
        }

        private void HandleRefreshSignal(GameplayPresentationFrame _)
        {
            Refresh();
        }

        private void HandleRefreshSignal(GameplayPresentationState _)
        {
            Refresh();
        }
    }
}
