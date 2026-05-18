namespace Game.Feature.UI.Application
{
    public enum UiAudioCueId
    {
        NavigateForward = 0,
        NavigateBack = 1,
        Confirm = 2,
        Cancel = 3,
        Select = 4,
        Toggle = 5,
        AdjustValueCommit = 6,
        ChanceGain = 7,
        ChanceLoss = 8,
        LastChance = 9,
        ObjectiveComplete = 10,
        TopologyShift = 11,
        PrimaryMenuCommand = 12,
        StageLaunch = 13,
        KeyboardMove = 14,
    }

    public interface IUiAudioPort
    {
        void Play(UiAudioCueId cueId);
    }
}
