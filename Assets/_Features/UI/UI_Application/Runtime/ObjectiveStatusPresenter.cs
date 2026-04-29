using System;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveStatusPresenter : IDisposable
    {
        private readonly IGameplayUiPresentationSource _presentationSource;

        public ObjectiveStatusPresenter(IGameplayUiPresentationSource presentationSource)
        {
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
            var objective = _presentationSource.CurrentSnapshot.Objective;

            CurrentState = new ObjectiveStatusScreenState(
                objective.HasObjective,
                objective.GoalReached,
                objective.AllConditionsSatisfied,
                objective.IsCleared,
                objective.Title,
                objective.Summary);
            StateChanged?.Invoke(CurrentState);
        }

        private void HandleSnapshotChanged(UIPresentationSnapshot _)
        {
            Refresh();
        }
    }
}
