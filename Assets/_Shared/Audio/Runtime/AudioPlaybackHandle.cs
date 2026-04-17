namespace Game.Shared.Audio
{
    internal interface IAudioPlaybackController
    {
        bool IsValid { get; }

        void Stop();

        void Pause();

        void Resume();

        void SetVolume(float volume);
    }

    public sealed class AudioPlaybackHandle
    {
        private static readonly AudioPlaybackHandle InvalidHandle = new(null);
        private readonly IAudioPlaybackController controller;

        internal AudioPlaybackHandle(IAudioPlaybackController controller)
        {
            this.controller = controller;
        }

        public static AudioPlaybackHandle Invalid => InvalidHandle;

        public bool IsValid => controller?.IsValid ?? false;

        public void Stop()
        {
            controller?.Stop();
        }

        public void Pause()
        {
            controller?.Pause();
        }

        public void Resume()
        {
            controller?.Resume();
        }

        public void SetVolume(float volume)
        {
            controller?.SetVolume(volume);
        }
    }
}
