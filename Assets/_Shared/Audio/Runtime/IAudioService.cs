using UnityEngine;

namespace Game.Shared.Audio
{
    public interface IAudioService
    {
        AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default);

        AudioPlaybackHandle PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context = default);

        AudioPlaybackHandle PlayBgm(AudioBgmPlaybackRequest request);

        void Stop(AudioPlaybackHandle handle);

        void StopBgm(AudioBgmStopRequest request);
    }
}
