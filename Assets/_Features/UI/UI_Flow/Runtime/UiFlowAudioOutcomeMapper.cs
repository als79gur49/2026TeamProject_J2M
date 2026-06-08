using Game.Feature.UI.Application;

namespace Game.Feature.UI.Flow
{
    internal static class UiFlowAudioOutcomeMapper
    {
        public static bool TryMap(UiFlowAudioOutcomeKind outcomeKind, out UiAudioCueId cueId)
        {
            switch (outcomeKind)
            {
                case UiFlowAudioOutcomeKind.NavigateForward:
                    cueId = UiAudioCueId.NavigateForward;
                    return true;

                case UiFlowAudioOutcomeKind.NavigateBack:
                    cueId = UiAudioCueId.NavigateBack;
                    return true;

                case UiFlowAudioOutcomeKind.Confirm:
                    cueId = UiAudioCueId.Confirm;
                    return true;

                case UiFlowAudioOutcomeKind.Cancel:
                    cueId = UiAudioCueId.Cancel;
                    return true;

                case UiFlowAudioOutcomeKind.GameClear:
                    cueId = UiAudioCueId.GameClear;
                    return true;

                case UiFlowAudioOutcomeKind.StageClear:
                    cueId = UiAudioCueId.StageClear;
                    return true;

                case UiFlowAudioOutcomeKind.LevelFailed:
                    cueId = UiAudioCueId.LevelFailed;
                    return true;

                default:
                    cueId = default;
                    return false;
            }
        }
    }
}
