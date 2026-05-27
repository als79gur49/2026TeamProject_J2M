using System;
using Game.Feature.Stages;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.DemoStageControl.UI
{
    public sealed class DemoStageControlPanelRuntime : IPopupRuntime, IUiNavigationTargetProvider
    {
        private readonly IDemoStageControlCommandPort _commandPort;
        private readonly Action _dispose;
        private readonly DemoStageControlPanelView _view;
        private readonly DemoStageControlPanelViewModel _viewModel;
        private StageId _selectedStageId = StageId.None;

        public DemoStageControlPanelRuntime(
            DemoStageControlPanelView view,
            IDemoStageControlCommandPort commandPort,
            DemoStageControlPanelPayload initialPayload,
            Action dispose)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _commandPort = commandPort ?? throw new ArgumentNullException(nameof(commandPort));
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
            _viewModel = new DemoStageControlPanelViewModel();
            _view.Bind(_viewModel);
            _view.SelectedStageChanged += HandleSelectedStageChanged;
            _view.StartStageClicked += HandleStartStageClicked;
            _view.ForceClearClicked += HandleForceClearClicked;
            ApplyPayload(initialPayload);
        }

        public event Action<PopupCompletionKind> CompletionRequested
        {
            add => _view.CompletionRequested += value;
            remove => _view.CompletionRequested -= value;
        }

        public void Dispose()
        {
            _view.SelectedStageChanged -= HandleSelectedStageChanged;
            _view.StartStageClicked -= HandleStartStageClicked;
            _view.ForceClearClicked -= HandleForceClearClicked;
            _view.Bind(null);
            _view.IsVisible = false;
            _dispose();
        }

        public void SetIsTopmost(bool isTopmost)
        {
            _view.IsVisible = true;
            _view.SetIsTopmost(isTopmost);
        }

        public bool TryGetNavigationTarget(out IUiNavigationTarget target)
        {
            target = _view;
            return true;
        }

        private void HandleSelectedStageChanged(StageId stageId)
        {
            _selectedStageId = stageId;
        }

        private void HandleStartStageClicked(StageId stageId)
        {
            _selectedStageId = stageId;
            _commandPort.StartStage(stageId);
            Refresh();
        }

        private void HandleForceClearClicked()
        {
            _commandPort.ForceClearCurrentStage();
            Refresh();
        }

        private void ApplyPayload(DemoStageControlPanelPayload payload)
        {
            if (payload == null)
            {
                Refresh();
                return;
            }

            _viewModel.Apply(payload, _selectedStageId);
            _selectedStageId = _viewModel.SelectedStageId;
        }

        private void Refresh()
        {
            ApplyPayload(new DemoStageControlPanelPayload(
                _commandPort.GetStages(),
                _commandPort.GetStatus()));
        }
    }
}
