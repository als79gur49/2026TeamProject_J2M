namespace Game.Shared.Audio
{
    public interface IAudioPlaybackPauseService
    {
        void PauseGroup(AudioPlaybackPauseGroup group, AudioPauseReason reason);

        void ResumeGroup(AudioPlaybackPauseGroup group, AudioPauseReason reason);

        bool IsGroupPaused(AudioPlaybackPauseGroup group, AudioPauseReason reason);
    }
}
