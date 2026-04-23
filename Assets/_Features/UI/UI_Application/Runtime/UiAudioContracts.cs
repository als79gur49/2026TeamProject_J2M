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
    }

    public interface IUiAudioPort
    {
        void Play(UiAudioCueId cueId);
    }
}
