using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveStatusPresenter : IDisposable
    {
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly IGameplayQueryFacade _queryFacade;

        public ObjectiveStatusPresenter(
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource)
        {
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));

            _presentationSource.SnapshotChanged += HandleSnapshotChanged;

            Refresh();
        }

        public event Action<ObjectiveStatusScreenState> StateChanged;

        public ObjectiveStatusScreenState CurrentState { get; private set; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandleSnapshotChanged;
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

        private void HandleSnapshotChanged(UIPresentationSnapshot _)
        {
            Refresh();
        }
    }
}
