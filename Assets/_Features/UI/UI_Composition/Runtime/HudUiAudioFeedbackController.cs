using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Composition
{
    internal sealed class HudUiAudioFeedbackController : IDisposable
    {
        private readonly IUiAudioPort _uiAudioPort;
        private readonly ChancePanelViewModel _chanceViewModel;
        private readonly SurfaceIndicatorViewModel _surfaceIndicatorViewModel;
        private int _lastChanceSequenceId;
        private int _lastTopologySequenceId;

        public HudUiAudioFeedbackController(
            IUiAudioPort uiAudioPort,
            ChancePanelViewModel chanceViewModel,
            ObjectiveHudViewModel objectiveViewModel,
            SurfaceIndicatorViewModel surfaceIndicatorViewModel)
        {
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _chanceViewModel = chanceViewModel ?? throw new ArgumentNullException(nameof(chanceViewModel));
            _ = objectiveViewModel ?? throw new ArgumentNullException(nameof(objectiveViewModel));
            _surfaceIndicatorViewModel = surfaceIndicatorViewModel ?? throw new ArgumentNullException(nameof(surfaceIndicatorViewModel));

            _chanceViewModel.Changed += HandleChanceChanged;
            _surfaceIndicatorViewModel.Changed += HandleTopologyChanged;
        }

        public void Dispose()
        {
            _chanceViewModel.Changed -= HandleChanceChanged;
            _surfaceIndicatorViewModel.Changed -= HandleTopologyChanged;
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

        private void HandleTopologyChanged()
        {
            var hint = _surfaceIndicatorViewModel.AnimationHint;
            if (hint.SequenceId <= 0 || hint.SequenceId == _lastTopologySequenceId)
            {
                return;
            }

            _lastTopologySequenceId = hint.SequenceId;
            if (hint.PulseDestination)
            {
                _uiAudioPort.Play(UiAudioCueId.TopologyShift);
            }
        }
    }
}
