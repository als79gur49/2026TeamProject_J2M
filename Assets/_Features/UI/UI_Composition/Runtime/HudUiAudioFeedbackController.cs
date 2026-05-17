using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Composition
{
    internal sealed class HudUiAudioFeedbackController : IDisposable
    {
        private readonly IUiAudioPort _uiAudioPort;
        private readonly ChancePanelViewModel _chanceViewModel;
        private int _lastChanceSequenceId;

        public HudUiAudioFeedbackController(
            IUiAudioPort uiAudioPort,
            ChancePanelViewModel chanceViewModel,
            ObjectiveHudViewModel objectiveViewModel)
        {
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _chanceViewModel = chanceViewModel ?? throw new ArgumentNullException(nameof(chanceViewModel));
            _ = objectiveViewModel ?? throw new ArgumentNullException(nameof(objectiveViewModel));

            _chanceViewModel.Changed += HandleChanceChanged;
        }

        public void Dispose()
        {
            _chanceViewModel.Changed -= HandleChanceChanged;
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
    }
}
