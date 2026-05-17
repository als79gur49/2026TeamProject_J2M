using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Composition
{
    internal sealed class HudUiAudioFeedbackController : IDisposable
    {
        private readonly IUiAudioPort _uiAudioPort;
        private readonly ChancePanelViewModel _chanceViewModel;
        private readonly SurfaceBeltViewModel _surfaceBeltViewModel;
        private int _lastChanceSequenceId;
        private int _lastSurfaceBeltSequenceId;

        public HudUiAudioFeedbackController(
            IUiAudioPort uiAudioPort,
            ChancePanelViewModel chanceViewModel,
            ObjectiveHudViewModel objectiveViewModel,
            SurfaceBeltViewModel surfaceBeltViewModel)
        {
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _chanceViewModel = chanceViewModel ?? throw new ArgumentNullException(nameof(chanceViewModel));
            _ = objectiveViewModel ?? throw new ArgumentNullException(nameof(objectiveViewModel));
            _surfaceBeltViewModel = surfaceBeltViewModel ?? throw new ArgumentNullException(nameof(surfaceBeltViewModel));

            _chanceViewModel.Changed += HandleChanceChanged;
            _surfaceBeltViewModel.Changed += HandleSurfaceBeltChanged;
        }

        public void Dispose()
        {
            _chanceViewModel.Changed -= HandleChanceChanged;
            _surfaceBeltViewModel.Changed -= HandleSurfaceBeltChanged;
        }

        private void HandleChanceChanged()
        {
            var hint = _chanceViewModel.AnimationHint;
            if (hint.SequenceId <= 0 || hint.SequenceId == _lastChanceSequenceId)
            {
                return;
            }

            _lastChanceSequenceId = hint.SequenceId;
            switch (hint.Kind)
            {
                case ChanceChangeKind.Gained:
                    _uiAudioPort.Play(UiAudioCueId.ChanceGain);
                    break;
                case ChanceChangeKind.LastChanceEntered:
                    _uiAudioPort.Play(UiAudioCueId.LastChance);
                    break;
                case ChanceChangeKind.Lost:
                    _uiAudioPort.Play(UiAudioCueId.ChanceLoss);
                    break;
            }
        }

        private void HandleSurfaceBeltChanged()
        {
            var sequenceId = _surfaceBeltViewModel.TransitionSequenceId;
            if (sequenceId <= 0 ||
                sequenceId == _lastSurfaceBeltSequenceId ||
                !_surfaceBeltViewModel.IsTransitioning ||
                _surfaceBeltViewModel.Direction == SurfaceBeltDirection.None)
            {
                return;
            }

            _lastSurfaceBeltSequenceId = sequenceId;
            _uiAudioPort.Play(UiAudioCueId.TopologyShift);
        }
    }
}
